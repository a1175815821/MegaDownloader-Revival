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

/// <summary>Bound cloud folders, their latest check result and the files waiting for review.</summary>
public partial class SubscriptionsPageViewModel : ViewModelBase, IDisposable
{
    private readonly MegaAccountService _accounts;
    private readonly MegaSubscriptionService _subscriptions;
    private readonly IDialogService _dialogs;
    private readonly HashSet<(ulong Folder, ulong Handle)> _checked = new();
    private bool _initialized;
    private bool _disposed;
    private bool _loading;
    private string _currentEmail = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SubscriptionAccountChoice> accounts = new();

    [ObservableProperty]
    private SubscriptionAccountChoice? selectedAccount;

    [ObservableProperty]
    private ObservableCollection<SubscriptionRowViewModel> subscriptions = new();

    [ObservableProperty]
    private SubscriptionRowViewModel? selectedSubscription;

    [ObservableProperty]
    private ObservableCollection<SubscriptionReportRowViewModel> reports = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string status = UiText.Get("Choose a MEGA account.");

    [ObservableProperty]
    private int checkedCount;

    public SubscriptionsPageViewModel(MegaAccountService accounts, MegaSubscriptionService subscriptions,
        IDialogService dialogs, PublicFoldersViewModel publicFolders)
    {
        _accounts = accounts;
        _subscriptions = subscriptions;
        _dialogs = dialogs;
        PublicFolders = publicFolders;
        _subscriptions.Changed += OnSubscriptionsChanged;
        _accounts.Changed += OnAccountsChanged;
    }

    /// <summary>Anonymous public folder links; no signed-in account is involved.</summary>
    public PublicFoldersViewModel PublicFolders { get; }

    /// <summary>
    /// True while the page shows public folder links instead of account-bound folders.
    /// Both sources live on this page because they answer the same question.
    /// </summary>
    [ObservableProperty]
    private bool showPublicFolders;

    public bool ShowAccountFolders => !ShowPublicFolders;

    partial void OnShowPublicFoldersChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAccountFolders));
        if (value)
            PublicFolders.Initialize();
    }

    [RelayCommand]
    private void ShowAccountMode() => ShowPublicFolders = false;

    [RelayCommand]
    private void ShowPublicMode() => ShowPublicFolders = true;

    private void OnAccountsChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !_initialized)
                return;
            RebuildAccounts();
            _ = LoadAsync();
        });

    public Func<MegaDriveSubscription, Task<MegaFolderSubscriptionSettings?>>? EditSubscriptionAsync { get; set; }

    public bool HasAccount => SelectedAccount is not null;

    public bool HasSubscriptions => Subscriptions.Count > 0;

    public bool CanAdd => CheckedCount > 0 && !IsBusy;

    public string AddButtonText => CheckedCount > 0
        ? UiText.Format("Add {0} selected", CheckedCount)
        : UiText.Get("Add to queue");

    public void ApplyLanguage()
    {
        RebuildAccounts();
        if (_initialized)
            _ = LoadAsync();
        PublicFolders.ApplyLanguage();
    }

    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;
        PublicFolders.Initialize();
        RebuildAccounts();
        // Without a signed-in account the public-folder links are the only source this
        // page can show, so open on them instead of an empty account panel.
        if (Accounts.Count == 0)
            ShowPublicFolders = true;
        else if (SelectedAccount is not null)
            _ = LoadAsync();
    }

    private void OnSubscriptionsChanged(object? sender, EventArgs e)
    {
        if (_disposed || !_initialized)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed && !_loading && !IsBusy)
                _ = LoadAsync();
        });
    }

    private void RebuildAccounts()
    {
        string keep = SelectedAccount?.Email ?? _currentEmail;
        var choices = new ObservableCollection<SubscriptionAccountChoice>();
        foreach (MegaAccountService.Status status in _accounts.GetAccounts())
        {
            if (!status.SignedIn && !status.NeedsLogin && !status.Connecting)
                continue;
            choices.Add(new SubscriptionAccountChoice(status.Email,
                status.NeedsLogin
                    ? MegaAccountText.AccountCaption(status) + " · " + UiText.Get("Sign in to MEGA again")
                    : MegaAccountText.AccountCaption(status)));
        }

        if (keep.Length == 0)
            keep = _accounts.DefaultEmail;
        Accounts = choices;
        if (choices.Count == 0)
        {
            SelectedAccount = null;
            Subscriptions.Clear();
            Reports.Clear();
            Status = UiText.Get("Sign in on the MEGA account page to manage cloud directory subscriptions.");
            OnPropertyChanged(nameof(HasAccount));
            return;
        }

        _selecting = true;
        try
        {
            SelectedAccount = choices.FirstOrDefault(choice =>
                string.Equals(choice.Email, keep, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        }
        finally
        {
            _selecting = false;
        }
        OnPropertyChanged(nameof(HasAccount));
        ChooseAccount();
    }

    private bool _selecting;

    partial void OnSelectedAccountChanged(SubscriptionAccountChoice? value)
    {
        if (_selecting)
            return;
        ChooseAccount();
    }

    private void ChooseAccount()
    {
        string email = SelectedAccount?.Email ?? string.Empty;
        if (string.Equals(email, _currentEmail, StringComparison.OrdinalIgnoreCase) && _initialized)
            return;
        _currentEmail = email;
        _checked.Clear();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading || _disposed)
            return;
        _loading = true;
        if (_currentEmail.Length == 0)
        {
            Subscriptions.Clear();
            Reports.Clear();
            Status = UiText.Get("Sign in on the MEGA account page to manage cloud directory subscriptions.");
            _loading = false;
            return;
        }

        int access = _accounts.GetAccessError(_currentEmail);
        if (access != 0)
        {
            Subscriptions.Clear();
            Reports.Clear();
            Status = MegaAccountText.Summary(_accounts.GetStatus(_currentEmail)) + " " +
                MegaAccountService.ErrorText(access, connecting: true);
            _loading = false;
            return;
        }

        IsBusy = true;
        string keep = SelectedSubscription?.Folder.ToString() ?? string.Empty;
        try
        {
            List<MegaDriveSubscription> snapshot = await _subscriptions.ReadReportsAsync(_currentEmail);
            if (_disposed)
                return;
            var rows = new ObservableCollection<SubscriptionRowViewModel>();
            var reportRows = new ObservableCollection<SubscriptionReportRowViewModel>();
            var pending = new HashSet<(ulong, ulong)>();
            foreach (MegaDriveSubscription subscription in snapshot)
            {
                rows.Add(new SubscriptionRowViewModel(this, subscription));
                foreach (MegaDriveSubscriptionFile file in subscription.Files)
                {
                    var key = (subscription.Folder, file.Handle);
                    bool checkable = file.Status == MegaDriveSubscriptionFileState.PendingReview;
                    if (checkable)
                        pending.Add(key);
                    reportRows.Add(new SubscriptionReportRowViewModel(this, subscription, file,
                        checkable && _checked.Contains(key)));
                }
            }
            _checked.IntersectWith(pending);
            Subscriptions = rows;
            Reports = reportRows;
            SelectedSubscription = rows.FirstOrDefault(row => row.Folder.ToString() == keep)
                ?? (rows.Count > 0 ? rows[0] : null);
            CheckedCount = _checked.Count;
            Status = ReportSummary(snapshot);
            OnPropertyChanged(nameof(HasSubscriptions));
            OnPropertyChanged(nameof(CanAdd));
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Could not read cloud directory subscriptions: " + Log.SafeException(ex));
            Status = UiText.Get("Could not read the cloud directory subscriptions.");
        }
        finally
        {
            IsBusy = false;
            _loading = false;
        }
    }

    private string ReportSummary(List<MegaDriveSubscription> snapshot)
    {
        if (snapshot.Count == 0)
            return UiText.Get("No bound folders yet. Bind a folder from the cloud drive.");
        if (SelectedSubscription is not null)
        {
            MegaDriveSubscription? selected = snapshot.Find(item => item.Folder == SelectedSubscription.Folder);
            if (selected is not null && selected.LastError != 0)
                return MegaAccountService.ErrorText(selected.LastError);
        }
        if (snapshot.Any(item => item.LastError != 0))
            return UiText.Get("Some bound folders could not be checked.");
        if (snapshot.All(item => !item.LastCheckedUtc.HasValue))
            return UiText.Get("No checks have run yet.");
        if (Reports.Count == 0)
            return UiText.Get("No changes were found in the bound folders.");
        return UiText.Format("{0} change(s) to review.", Reports.Count);
    }

    partial void OnCheckedCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(AddButtonText));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanAdd));

    partial void OnSelectedSubscriptionChanged(SubscriptionRowViewModel? value)
    {
        if (value is null)
            return;
        Status = value.StatusText.Length > 0 ? value.StatusText : Status;
    }

    internal void SetChecked(ulong folder, ulong handle, bool value)
    {
        var key = (folder, handle);
        if (value)
            _checked.Add(key);
        else
            _checked.Remove(key);
        CheckedCount = _checked.Count;
    }

    internal void Edit(SubscriptionRowViewModel row) => _ = EditAsync(row);

    private async Task EditAsync(SubscriptionRowViewModel row)
    {
        if (IsBusy || EditSubscriptionAsync is null)
            return;
        MegaDriveSubscription? settings = _accounts.GetDriveSubscriptions(_currentEmail)
            .FirstOrDefault(item => item.Folder == row.Folder);
        if (settings is null)
            return;
        MegaFolderSubscriptionSettings? edited = await EditSubscriptionAsync(settings);
        if (edited is null)
            return;
        bool destinationChanged = !string.Equals(settings.Destination, edited.Destination,
            StringComparison.OrdinalIgnoreCase);
        if (!settings.CheckMinutes.Equals(edited.CheckMinutes))
            settings.ScheduleFromUtc = DateTime.UtcNow;
        settings.Destination = edited.Destination;
        settings.Extensions = edited.Extensions;
        settings.CheckMinutes = edited.CheckMinutes;
        settings.AutoQueue = edited.AutoQueue;
        settings.Paused = edited.Paused;
        if (destinationChanged)
        {
            settings.Files.Clear();
            settings.FolderPaths.Clear();
        }
        await SaveSettingsAsync(settings);
    }

    internal void TogglePause(SubscriptionRowViewModel row) => _ = TogglePauseAsync(row);

    private async Task TogglePauseAsync(SubscriptionRowViewModel row)
    {
        if (IsBusy)
            return;
        MegaDriveSubscription? settings = _accounts.GetDriveSubscriptions(_currentEmail)
            .FirstOrDefault(item => item.Folder == row.Folder);
        if (settings is null)
            return;
        settings.Paused = !settings.Paused;
        await SaveSettingsAsync(settings);
    }

    private async Task SaveSettingsAsync(MegaDriveSubscription settings)
    {
        IsBusy = true;
        try
        {
            int result = await Task.Run(() => _accounts.SaveDriveSubscriptionSettings(_currentEmail, settings));
            if (result != 0)
            {
                await _dialogs.ShowMessageAsync(UiText.Get("Cloud directory subscriptions"),
                    MegaAccountService.ErrorText(result));
                return;
            }
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void Check(SubscriptionRowViewModel row) => _ = CheckAsync(row.Folder);

    private async Task CheckAsync(ulong? folder)
    {
        if (IsBusy || _currentEmail.Length == 0)
            return;
        IsBusy = true;
        Status = UiText.Get("Checking bound folders…");
        try
        {
            await _subscriptions.CheckAsync(_currentEmail, folder, scheduled: false);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Cloud directory check failed: " + Log.SafeException(ex));
            Status = UiText.Get("Could not check the bound folders.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task CheckSelectedAsync() => CheckAsync(SelectedSubscription?.Folder);

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
    private void SelectPending()
    {
        foreach (SubscriptionReportRowViewModel row in Reports)
        {
            if (!row.IsCheckable)
                continue;
            row.SyncChecked(true);
            _checked.Add((row.Folder, row.Handle));
        }
        CheckedCount = _checked.Count;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (SubscriptionReportRowViewModel row in Reports)
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

        Dictionary<ulong, HashSet<ulong>> selection = _checked
            .GroupBy(item => item.Folder)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Handle).ToHashSet());
        IsBusy = true;
        Status = UiText.Get("Adding files to the queue…");
        try
        {
            int result = await _subscriptions.QueueFilesAsync(_currentEmail, selection);
            Status = result < 0
                ? MegaAccountService.ErrorText(result)
                : result == 0
                    ? UiText.Get("No files were added.")
                    : UiText.Format("Added {0} file(s) to the queue.", result);
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
        _subscriptions.Changed -= OnSubscriptionsChanged;
        _accounts.Changed -= OnAccountsChanged;
        PublicFolders.Dispose();
    }
}

public sealed class SubscriptionAccountChoice
{
    public SubscriptionAccountChoice(string email, string caption)
    {
        Email = email;
        Caption = caption;
    }

    public string Email { get; }
    public string Caption { get; }
}

public partial class SubscriptionRowViewModel : ObservableObject
{
    private readonly SubscriptionsPageViewModel _owner;

    public SubscriptionRowViewModel(SubscriptionsPageViewModel owner, MegaDriveSubscription subscription)
    {
        _owner = owner;
        Subscription = subscription;
        Folder = subscription.Folder;
        Name = string.IsNullOrEmpty(subscription.Name) ? subscription.Destination : subscription.Name;
        Destination = subscription.Destination;
        LastCheckText = subscription.LastCheckedUtc.HasValue
            ? subscription.LastCheckedUtc.Value.ToLocalTime().ToString("g")
            : "—";
        DateTime? next = subscription.NextCheckLocal();
        NextCheckText = subscription.Paused
            ? UiText.Get("Paused")
            : next.HasValue ? next.Value.ToString("g") : UiText.Get("Manual check only");
        StatusText = subscription.LastError != 0
            ? MegaAccountService.ErrorText(subscription.LastError)
            : string.Empty;
        IsPaused = subscription.Paused;
        HasError = subscription.LastError != 0;
    }

    public MegaDriveSubscription Subscription { get; }
    public ulong Folder { get; }
    public string Name { get; }
    public string Destination { get; }
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
}

public partial class SubscriptionReportRowViewModel : ObservableObject
{
    private readonly SubscriptionsPageViewModel _owner;
    private bool _suppress;

    public SubscriptionReportRowViewModel(SubscriptionsPageViewModel owner, MegaDriveSubscription subscription,
        MegaDriveSubscriptionFile file, bool isChecked)
    {
        _owner = owner;
        Folder = subscription.Folder;
        Handle = file.Handle;
        FolderName = string.IsNullOrEmpty(subscription.Name) ? subscription.Destination : subscription.Name;
        File = file;
        this.isChecked = isChecked;
    }

    private MegaDriveSubscriptionFile File { get; }

    public ulong Folder { get; }
    public ulong Handle { get; }
    public string FolderName { get; }
    public string Name => File.Name;
    public string PathText => string.IsNullOrEmpty(File.RelativePath) ? File.Name : File.RelativePath + "/" + File.Name;
    public string SizeText => MegaAccountText.FormatBytes(File.Bytes);
    public string ChangeText => UiText.Get(File.Updated ? "Changed file" : "New file");
    public bool IsCheckable => File.Status == MegaDriveSubscriptionFileState.PendingReview;
    public string StatusText => File.Status switch
    {
        MegaDriveSubscriptionFileState.PendingReview => UiText.Get("Pending review"),
        MegaDriveSubscriptionFileState.Queued => UiText.Get("Already queued"),
        _ => UiText.Get("Downloaded")
    };

    [ObservableProperty]
    private bool isChecked;

    partial void OnIsCheckedChanged(bool value)
    {
        if (!_suppress)
            _owner.SetChecked(Folder, Handle, value);
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
