using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels.Pages;

/// <summary>
/// Anonymous public-folder subscriptions. Nothing here needs a signed-in account: the
/// share key inside the link is enough to list the folder and to queue its files.
/// </summary>
public partial class PublicFoldersViewModel : ObservableObject, IDisposable
{
    private readonly MegaPublicFolderStore _store;
    private readonly MegaPublicFolderService _service;
    private readonly DownloadEngineService _engine;
    private readonly IDialogService _dialogs;
    private readonly HashSet<(string Folder, string File)> _checked = new();
    private bool _initialized;
    private bool _disposed;
    private bool _loading;

    [ObservableProperty]
    private ObservableCollection<PublicFolderRowViewModel> subscriptions = new();

    [ObservableProperty]
    private ObservableCollection<PublicFolderFileRowViewModel> reports = new();

    [ObservableProperty]
    private PublicFolderRowViewModel? selectedSubscription;

    [ObservableProperty]
    private string linkText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string status = UiText.Get("Paste a public folder link to subscribe to it.");

    [ObservableProperty]
    private int checkedCount;

    public PublicFoldersViewModel(MegaPublicFolderStore store, MegaPublicFolderService service,
        DownloadEngineService engine, IDialogService dialogs)
    {
        _store = store;
        _service = service;
        _engine = engine;
        _dialogs = dialogs;
        _service.Changed += OnServiceChanged;
    }

    public Func<MegaPublicFolderSubscription, Task<MegaFolderSubscriptionSettings?>>? EditSubscriptionAsync { get; set; }

    public bool HasSubscriptions => Subscriptions.Count > 0;
    public bool HasReports => Reports.Count > 0;
    public bool CanAdd => CheckedCount > 0 && !IsBusy;
    public bool HasLinkText => !string.IsNullOrWhiteSpace(LinkText);
    public bool CanSubscribe => HasLinkText && !IsBusy;

    public string AddButtonText => CheckedCount > 0
        ? UiText.Format("Add {0} selected", CheckedCount)
        : UiText.Get("Add to queue");

    partial void OnLinkTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasLinkText));
        OnPropertyChanged(nameof(CanSubscribe));
    }

    partial void OnCheckedCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(AddButtonText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanSubscribe));
    }

    private void OnServiceChanged(object? sender, EventArgs e)
    {
        if (_disposed || !_initialized)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed && !_loading && !IsBusy)
                _ = LoadAsync();
        });
    }

    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;
        _ = LoadAsync();
    }

    public void ApplyLanguage()
    {
        if (_initialized)
            _ = LoadAsync();
        else
            Status = UiText.Get("Paste a public folder link to subscribe to it.");
    }

    private async Task LoadAsync()
    {
        if (_loading || _disposed)
            return;
        _loading = true;
        IsBusy = true;
        string keep = SelectedSubscription?.FolderId ?? string.Empty;
        try
        {
            List<MegaPublicFolderSubscription> snapshot = await Task.Run(() => _store.Snapshot());
            if (_disposed)
                return;
            var rows = new ObservableCollection<PublicFolderRowViewModel>();
            var reportRows = new ObservableCollection<PublicFolderFileRowViewModel>();
            var pending = new HashSet<(string, string)>();
            foreach (MegaPublicFolderSubscription subscription in snapshot)
            {
                rows.Add(new PublicFolderRowViewModel(this, subscription));
                foreach (MegaPublicFolderFile file in subscription.Files)
                {
                    var key = (subscription.FolderId, file.FileId);
                    bool checkable = file.Status == MegaPublicFolderFileState.PendingReview;
                    if (checkable)
                        pending.Add(key);
                    reportRows.Add(new PublicFolderFileRowViewModel(this, subscription, file,
                        checkable && _checked.Contains(key)));
                }
            }
            _checked.IntersectWith(pending);
            Subscriptions = rows;
            Reports = reportRows;
            SelectedSubscription = rows.FirstOrDefault(row => row.FolderId == keep)
                ?? (rows.Count > 0 ? rows[0] : null);
            CheckedCount = _checked.Count;
            Status = Summary(snapshot);
            OnPropertyChanged(nameof(HasSubscriptions));
            OnPropertyChanged(nameof(HasReports));
            OnPropertyChanged(nameof(CanAdd));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.WriteWarning("Could not read the public folder subscriptions: " + Log.SafeException(ex));
            Status = UiText.Get("Could not read the public folder subscriptions.");
        }
        finally
        {
            IsBusy = false;
            _loading = false;
        }
    }

    private string Summary(List<MegaPublicFolderSubscription> snapshot)
    {
        if (snapshot.Count == 0)
            return UiText.Get("Paste a public folder link to subscribe to it.");
        if (SelectedSubscription is not null)
        {
            MegaPublicFolderSubscription? selected = snapshot.Find(item => item.FolderId == SelectedSubscription.FolderId);
            if (selected is not null && selected.LastError != 0)
                return ErrorText(selected.LastError);
        }
        if (snapshot.Any(item => item.LastError != 0))
            return UiText.Get("Some public folders could not be checked.");
        if (snapshot.All(item => !item.LastCheckedUtc.HasValue))
            return UiText.Get("No checks have run yet.");
        return Reports.Count == 0
            ? UiText.Get("No changes were found in the public folders.")
            : UiText.Format("{0} change(s) to review.", Reports.Count);
    }

    [RelayCommand]
    private async Task SubscribeAsync()
    {
        if (IsBusy)
            return;
        if (!MegaPublicFolderSubscription.TryParseLink(LinkText, out string folderId, out string folderKey,
                out string link))
        {
            Status = UiText.Get("That is not a MEGA folder link with its key. Paste the full folder link.");
            return;
        }

        MegaPublicFolderSubscription? existing = await Task.Run(() => _store.Find(folderId));
        MegaPublicFolderSubscription settings = existing?.Copy() ?? new MegaPublicFolderSubscription
        {
            FolderId = folderId,
            FolderKey = folderKey,
            Destination = _engine.Config.RutaDefecto
        };
        settings.Link = link;
        // Until a check succeeds the folder has no decrypted name; show the link's
        // folder id rather than repeating the destination in the name column.
        if (string.IsNullOrEmpty(settings.Name))
            settings.Name = folderId;
        if (EditSubscriptionAsync is null)
            return;
        MegaFolderSubscriptionSettings? edited = await EditSubscriptionAsync(settings);
        if (edited is null)
            return;

        bool destinationChanged = !string.Equals(settings.Destination, edited.Destination,
            StringComparison.OrdinalIgnoreCase);
        if (!settings.CheckMinutes.Equals(edited.CheckMinutes))
            settings.ScheduleFromUtc = DateTime.UtcNow;
        settings.ApplySettings(edited, destinationChanged);
        settings.LastError = 0;
        IsBusy = true;
        try
        {
            int saved = await Task.Run(() => _store.Upsert(settings));
            if (saved != 0)
            {
                await _dialogs.ShowMessageAsync(UiText.Get("Cloud directory subscriptions"),
                    MegaAccountService.ErrorText(saved));
                return;
            }
            LinkText = string.Empty;
            Status = UiText.Get("Checking the public folder…");
            await _service.CheckAsync(folderId, scheduled: false);
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal static string ErrorText(int code) => code == MegaPublicFolderService.ReadFailed
        ? UiText.Get("Could not read this folder. Check the link, its key, or that the folder still exists.")
        : MegaAccountService.ErrorText(code);

    internal void SetChecked(string folderId, string fileId, bool value)
    {
        var key = (folderId, fileId);
        if (value)
            _checked.Add(key);
        else
            _checked.Remove(key);
        CheckedCount = _checked.Count;
    }

    internal void Edit(PublicFolderRowViewModel row) => _ = EditAsync(row);

    private async Task EditAsync(PublicFolderRowViewModel row)
    {
        if (IsBusy || EditSubscriptionAsync is null)
            return;
        MegaPublicFolderSubscription? settings = _store.Find(row.FolderId);
        if (settings is null)
            return;
        MegaFolderSubscriptionSettings? edited = await EditSubscriptionAsync(settings);
        if (edited is null)
            return;
        bool destinationChanged = !string.Equals(settings.Destination, edited.Destination,
            StringComparison.OrdinalIgnoreCase);
        if (!settings.CheckMinutes.Equals(edited.CheckMinutes))
            settings.ScheduleFromUtc = DateTime.UtcNow;
        settings.ApplySettings(edited, destinationChanged);
        IsBusy = true;
        try
        {
            int saved = await Task.Run(() => _store.Upsert(settings));
            if (saved != 0)
            {
                await _dialogs.ShowMessageAsync(UiText.Get("Cloud directory subscriptions"),
                    MegaAccountService.ErrorText(saved));
                return;
            }
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void TogglePause(PublicFolderRowViewModel row) => _ = TogglePauseAsync(row);

    private async Task TogglePauseAsync(PublicFolderRowViewModel row)
    {
        if (IsBusy)
            return;
        MegaPublicFolderSubscription? settings = _store.Find(row.FolderId);
        if (settings is null)
            return;
        settings.Paused = !settings.Paused;
        IsBusy = true;
        try
        {
            int saved = await Task.Run(() => _store.Upsert(settings));
            if (saved != 0)
            {
                await _dialogs.ShowMessageAsync(UiText.Get("Cloud directory subscriptions"),
                    MegaAccountService.ErrorText(saved));
                return;
            }
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void Remove(PublicFolderRowViewModel row) => _ = RemoveAsync(row);

    private async Task RemoveAsync(PublicFolderRowViewModel row)
    {
        if (IsBusy)
            return;
        bool confirmed = await _dialogs.ConfirmAsync(
            UiText.Get("Cloud directory subscriptions"),
            UiText.Format("Stop watching “{0}”? Files already downloaded stay on disk.", row.Name),
            UiText.Get("Remove"));
        if (!confirmed)
            return;
        IsBusy = true;
        try
        {
            int result = await Task.Run(() => _store.Remove(row.FolderId));
            if (result != 0)
            {
                await _dialogs.ShowMessageAsync(UiText.Get("Cloud directory subscriptions"),
                    MegaAccountService.ErrorText(result));
                return;
            }
            _checked.RemoveWhere(item => item.Folder == row.FolderId);
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void Check(PublicFolderRowViewModel row) => _ = CheckAsync(row.FolderId);

    private async Task CheckAsync(string? folderId)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        Status = UiText.Get("Checking public folders…");
        try
        {
            await _service.CheckAsync(folderId, scheduled: false);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.WriteWarning("Public folder check failed: " + Log.SafeException(ex));
            Status = UiText.Get("Could not check the public folders.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task CheckSelectedAsync() => CheckAsync(SelectedSubscription?.FolderId);

    [RelayCommand]
    private Task CheckAllAsync() => CheckAsync(null);

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedSubscription is not null)
            Edit(SelectedSubscription);
    }

    [RelayCommand]
    private void TogglePauseSelected()
    {
        if (SelectedSubscription is not null)
            TogglePause(SelectedSubscription);
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedSubscription is not null)
            Remove(SelectedSubscription);
    }

    [RelayCommand]
    private void SelectPending()
    {
        foreach (PublicFolderFileRowViewModel row in Reports)
        {
            if (!row.IsCheckable)
                continue;
            row.SyncChecked(true);
            _checked.Add((row.FolderId, row.FileId));
        }
        CheckedCount = _checked.Count;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (PublicFolderFileRowViewModel row in Reports)
            row.SyncChecked(false);
        _checked.Clear();
        CheckedCount = 0;
    }

    [RelayCommand]
    private async Task AddSelectedAsync()
    {
        if (CheckedCount == 0 || IsBusy)
            return;
        bool confirmed = await _dialogs.ConfirmAsync(
            UiText.Get("Cloud directory subscriptions"),
            UiText.Format("Add {0} selected file(s) to the download queue?", CheckedCount),
            UiText.Get("Add to queue"));
        if (!confirmed)
            return;

        IsBusy = true;
        Status = UiText.Get("Adding files to the queue…");
        try
        {
            int added = 0;
            int failure = 0;
            foreach (IGrouping<string, (string Folder, string File)> group in _checked.GroupBy(item => item.Folder))
            {
                var selected = group.Select(item => item.File).ToHashSet(StringComparer.Ordinal);
                int result = await _service.QueueFilesAsync(group.Key, selected);
                if (result < 0)
                {
                    failure = result;
                    break;
                }
                added += result;
            }
            Status = failure < 0
                ? MegaAccountService.ErrorText(failure)
                : added == 0
                    ? UiText.Get("No files were added.")
                    : UiText.Format("Added {0} file(s) to the queue.", added);
            _checked.Clear();
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _service.Changed -= OnServiceChanged;
    }
}

public partial class PublicFolderRowViewModel : ObservableObject
{
    private readonly PublicFoldersViewModel _owner;

    public PublicFolderRowViewModel(PublicFoldersViewModel owner, MegaPublicFolderSubscription subscription)
    {
        _owner = owner;
        FolderId = subscription.FolderId;
        Name = subscription.Caption;
        Destination = subscription.Destination;
        Link = subscription.Link;
        FileCount = subscription.Files.Count;
        LastCheckText = subscription.LastCheckedUtc.HasValue
            ? subscription.LastCheckedUtc.Value.ToLocalTime().ToString("g")
            : "—";
        DateTime? next = subscription.NextCheckLocal();
        NextCheckText = subscription.Paused
            ? UiText.Get("Paused")
            : next.HasValue ? next.Value.ToString("g") : UiText.Get("Manual check only");
        StatusText = subscription.LastError != 0
            ? PublicFoldersViewModel.ErrorText(subscription.LastError)
            : string.Empty;
        IsPaused = subscription.Paused;
        HasError = subscription.LastError != 0;
    }

    public string FolderId { get; }
    public string Name { get; }
    public string Destination { get; }
    public string Link { get; }
    public int FileCount { get; }
    public string LastCheckText { get; }
    public string NextCheckText { get; }
    public string StatusText { get; }
    public bool IsPaused { get; }
    public bool HasError { get; }

    [RelayCommand]
    private void Edit() => _owner.Edit(this);

    [RelayCommand]
    private void Check() => _owner.Check(this);

    [RelayCommand]
    private void TogglePause() => _owner.TogglePause(this);

    [RelayCommand]
    private void Remove() => _owner.Remove(this);
}

public partial class PublicFolderFileRowViewModel : ObservableObject
{
    private readonly PublicFoldersViewModel _owner;
    private bool _suppress;

    public PublicFolderFileRowViewModel(PublicFoldersViewModel owner, MegaPublicFolderSubscription subscription,
        MegaPublicFolderFile file, bool isChecked)
    {
        _owner = owner;
        FolderId = subscription.FolderId;
        FolderName = subscription.Caption;
        FileId = file.FileId;
        File = file;
        this.isChecked = isChecked;
    }

    private MegaPublicFolderFile File { get; }

    public string FolderId { get; }
    public string FolderName { get; }
    public string FileId { get; }
    public string Name => File.Name;
    public string PathText => string.IsNullOrEmpty(File.RelativePath) ? File.Name : File.RelativePath + "/" + File.Name;
    public string SizeText => MegaAccountText.FormatBytes(File.Bytes);
    public string ChangeText => UiText.Get(File.Updated ? "Changed file" : "New file");
    public bool IsCheckable => File.Status == MegaPublicFolderFileState.PendingReview;
    public string StatusText => File.Status switch
    {
        MegaPublicFolderFileState.PendingReview => UiText.Get("Pending review"),
        MegaPublicFolderFileState.Queued => UiText.Get("Already queued"),
        _ => UiText.Get("Downloaded")
    };

    [ObservableProperty]
    private bool isChecked;

    partial void OnIsCheckedChanged(bool value)
    {
        if (!_suppress)
            _owner.SetChecked(FolderId, FileId, value);
    }

    internal void SyncChecked(bool value)
    {
        if (IsChecked == value)
            return;
        _suppress = true;
        IsChecked = value;
        _suppress = false;
    }
}
