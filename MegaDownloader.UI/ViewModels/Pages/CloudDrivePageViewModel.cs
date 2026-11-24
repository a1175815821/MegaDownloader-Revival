using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels.Pages;

/// <summary>Browse one MEGA account's cloud drive or incoming shares and queue files.</summary>
public partial class CloudDrivePageViewModel : ViewModelBase, IDisposable
{
    private sealed class AccountDrive
    {
        public MegaDriveSnapshot? Snapshot;
        public MegaDrivePreferences Preferences;
        public readonly HashSet<ulong> PersonalSelection = new();
        public readonly HashSet<ulong> SharedSelection = new();
        public ulong Folder;
        public bool Incoming;

        public AccountDrive(MegaDrivePreferences preferences)
        {
            Preferences = preferences;
        }

        public HashSet<ulong> Selected => Incoming ? SharedSelection : PersonalSelection;
    }

    private readonly MegaAccountService _accounts;
    private readonly DownloadEngineService _engine;
    private readonly IDialogService _dialogs;
    private readonly Dictionary<string, AccountDrive> _drives = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, CloudDriveRowViewModel> _rowsByNode = new();
    private CancellationTokenSource? _loadCancellation;
    private bool _initialized;
    private bool _disposed;
    private string _currentEmail = string.Empty;
    private AccountDrive? _drive;
    private MegaDriveNode[] _shownNodes = Array.Empty<MegaDriveNode>();
    private string _loadMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CloudDriveAccountChoice> accounts = new();

    [ObservableProperty]
    private CloudDriveAccountChoice? selectedAccount;

    [ObservableProperty]
    private ObservableCollection<CloudDriveSourceChoice> sources = new();

    [ObservableProperty]
    private CloudDriveSourceChoice? selectedSource;

    [ObservableProperty]
    private ObservableCollection<CloudDriveTypeChoice> types = new();

    [ObservableProperty]
    private CloudDriveTypeChoice? selectedType;

    [ObservableProperty]
    private ObservableCollection<CloudDriveRowViewModel> rows = new();

    [ObservableProperty]
    private ObservableCollection<CloudCrumbViewModel> breadcrumbs = new();

    [ObservableProperty]
    private ObservableCollection<CloudFavoriteViewModel> favorites = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool wholeDrive;

    [ObservableProperty]
    private bool showSelectedOnly;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string status = UiText.Get("Choose a MEGA account.");

    [ObservableProperty]
    private int selectionCount;

    [ObservableProperty]
    private bool isBound;

    [ObservableProperty]
    private bool hasSnapshot;

    [ObservableProperty]
    private bool locationVisible;

    [ObservableProperty]
    private string sortNameGlyph = "↑";

    [ObservableProperty]
    private string sortTimeGlyph = string.Empty;

    [ObservableProperty]
    private string refreshGlyph = "⟳";

    public CloudDrivePageViewModel(MegaAccountService accounts, DownloadEngineService engine, IDialogService dialogs)
    {
        _accounts = accounts;
        _engine = engine;
        _dialogs = dialogs;

        Sources.Add(new CloudDriveSourceChoice(false, UiText.Get("Cloud drive")));
        Sources.Add(new CloudDriveSourceChoice(true, UiText.Get("Shared with me")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.All, UiText.Get("All types")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Video, UiText.Get("Videos")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Archive, UiText.Get("Archives")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Document, UiText.Get("Documents")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Image, UiText.Get("Images")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Audio, UiText.Get("Audio")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Other, UiText.Get("Other")));
        SelectedSource = Sources[0];
        SelectedType = Types[0];
        _accounts.Changed += OnAccountsChanged;
    }

    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public string DownloadDestination => string.IsNullOrWhiteSpace(_engine.Config.RutaDefecto)
        ? UiText.Get("Not set")
        : _engine.Config.RutaDefecto;

    /// <summary>View callback that opens the bound-folder settings dialog.</summary>
    public Func<MegaDriveSubscription, Task<MegaFolderSubscriptionSettings?>>? EditSubscriptionAsync { get; set; }

    /// <summary>View callback that opens the download preview and returns the confirmation.</summary>
    public Func<CloudDownloadRequest, Task<CloudDownloadConfirmation?>>? ShowDownloadPreviewAsync { get; set; }

    public bool CanUp => HasSnapshot && _drive?.Folder != _drive?.Snapshot?.Root;

    public bool CanDownload => SelectionCount > 0 && HasSnapshot && !IsLoading;

    public bool CanCheckNew => IsBound && HasSnapshot && !IsLoading;

    public bool CanFavorite => HasSnapshot && _drive?.Folder != _drive?.Snapshot?.Root;

    public bool HasAccount => SelectedAccount is not null;

    public bool HasAccounts => Accounts.Count > 0;

    public string ViewSelectedText => UiText.Get(ShowSelectedOnly ? "Back to files" : "View selected");

    public string SelectionText => UiText.Format("{0} selected", SelectionCount);

    public string AddButtonText => SelectionCount > 0
        ? UiText.Format("Download {0} selected", SelectionCount)
        : UiText.Get("Download selected");

    public void ApplyLanguage()
    {
        int sourceIndex = SelectedSource?.Incoming == true ? 1 : 0;
        int typeIndex = SelectedType is null ? 0 : Types.IndexOf(SelectedType);
        Sources.Clear();
        Sources.Add(new CloudDriveSourceChoice(false, UiText.Get("Cloud drive")));
        Sources.Add(new CloudDriveSourceChoice(true, UiText.Get("Shared with me")));
        Types.Clear();
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.All, UiText.Get("All types")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Video, UiText.Get("Videos")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Archive, UiText.Get("Archives")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Document, UiText.Get("Documents")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Image, UiText.Get("Images")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Audio, UiText.Get("Audio")));
        Types.Add(new CloudDriveTypeChoice(MegaDriveNode.FileKind.Other, UiText.Get("Other")));
        SelectedSource = Sources[Math.Clamp(sourceIndex, 0, Sources.Count - 1)];
        SelectedType = Types[Math.Clamp(typeIndex, 0, Types.Count - 1)];
        RebuildAccounts();
    }

    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;
        RebuildAccounts();
        if (SelectedAccount is not null)
            _ = LoadDriveAsync();
    }

    private void OnAccountsChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
                RebuildAccounts();
        });
    }

    private void RebuildAccounts()
    {
        string keep = SelectedAccount?.Email ?? string.Empty;
        var choices = new ObservableCollection<CloudDriveAccountChoice>();
        foreach (MegaAccountService.Status status in _accounts.GetAccounts())
        {
            if (!status.SignedIn && !status.NeedsLogin && !status.Connecting)
                continue;
            choices.Add(new CloudDriveAccountChoice(status.Email,
                status.NeedsLogin
                    ? MegaAccountText.AccountCaption(status) + " · " + UiText.Get("Sign in to MEGA again")
                    : MegaAccountText.AccountCaption(status)));
        }

        if (keep.Length == 0)
            keep = _accounts.DefaultEmail;
        bool rosterChanged = choices.Count != Accounts.Count ||
            choices.Zip(Accounts).Any(pair => !string.Equals(pair.First.Caption, pair.Second.Caption, StringComparison.Ordinal));
        Accounts = choices;
        if (choices.Count == 0)
        {
            SelectedAccount = null;
            _drive = null;
            HasSnapshot = false;
            Rows.Clear();
            Breadcrumbs.Clear();
            Favorites.Clear();
            Status = MegaAccountService.Availability() == MegaAccountService.BackendMissing
                ? UiText.Get("This build does not include the MEGA account module. Anonymous downloads are still available.")
                : UiText.Get("Sign in on the MEGA account page to browse the cloud drive.");
            OnPropertyChanged(nameof(HasAccount));
            OnPropertyChanged(nameof(HasAccounts));
            return;
        }

        CloudDriveAccountChoice? selected = choices.FirstOrDefault(choice =>
            string.Equals(choice.Email, keep, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        _selectingAccount = true;
        try
        {
            SelectedAccount = selected;
        }
        finally
        {
            _selectingAccount = false;
        }
        OnPropertyChanged(nameof(HasAccount));
        OnPropertyChanged(nameof(HasAccounts));
        if (rosterChanged || _drive is null ||
            !string.Equals(selected.Email, _currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            ChooseAccount(reload: true);
        }
    }

    private bool _selectingAccount;

    partial void OnSelectedAccountChanged(CloudDriveAccountChoice? value)
    {
        if (_selectingAccount)
            return;
        ChooseAccount(reload: true);
    }

    private void ChooseAccount(bool reload)
    {
        string email = SelectedAccount?.Email ?? string.Empty;
        if (email == _currentEmail && _drive is not null)
        {
            if (reload)
                _ = LoadDriveAsync();
            return;
        }

        SavePreferences();
        if (_drive is not null)
            _drive.Snapshot = null;
        _currentEmail = email;
        _drive = null;
        if (email.Length > 0)
        {
            if (!_drives.TryGetValue(email, out _drive))
            {
                _drive = new AccountDrive(_accounts.GetDrivePreferences(email));
                _drives.Add(email, _drive);
            }
        }

        ShowSelectedOnly = false;
        SearchText = string.Empty;
        WholeDrive = false;
        if (reload)
            _ = LoadDriveAsync();
    }

    partial void OnSelectedSourceChanged(CloudDriveSourceChoice? value)
    {
        if (_drive is null)
            return;
        SavePreferences();
        ShowSelectedOnly = false;
        SearchText = string.Empty;
        WholeDrive = false;
        _ = LoadDriveAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_initialized && _drive?.Snapshot is not null)
            RefreshRows();
    }

    partial void OnSelectedTypeChanged(CloudDriveTypeChoice? value)
    {
        if (_initialized && _drive?.Snapshot is not null)
            RefreshRows();
    }

    partial void OnWholeDriveChanged(bool value)
    {
        if (_initialized && _drive?.Snapshot is not null)
            RefreshRows();
    }

    partial void OnShowSelectedOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(ViewSelectedText));
        if (_initialized && _drive?.Snapshot is not null)
            RefreshRows();
    }

    partial void OnSelectionCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(AddButtonText));
    }

    partial void OnIsBoundChanged(bool value) => OnPropertyChanged(nameof(CanCheckNew));

    partial void OnHasSnapshotChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUp));
        OnPropertyChanged(nameof(CanFavorite));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanCheckNew));
    }

    private async Task LoadDriveAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _loadMessage = string.Empty;
        if (_drive is null || SelectedAccount is null)
        {
            HasSnapshot = false;
            RefreshRows();
            return;
        }

        int access = _accounts.GetAccessError(_currentEmail);
        if (access != 0)
        {
            _drive.Snapshot = null;
            HasSnapshot = false;
            _loadMessage = DriveLoadError(access);
            RefreshRows();
            return;
        }

        var source = new CancellationTokenSource();
        _loadCancellation = source;
        AccountDrive target = _drive;
        bool incoming = SelectedSource?.Incoming == true;
        target.Incoming = incoming;
        IsLoading = true;
        Status = UiText.Get("Loading the MEGA cloud drive…");
        try
        {
            string email = _currentEmail;
            (int code, MegaDriveSnapshot? snapshot) = await Task.Run(() =>
            {
                int result = _accounts.ReadDrive(email, source.Token, out MegaDriveSnapshot loaded,
                    incomingShares: incoming);
                return (result, loaded);
            }, source.Token);
            if (source.IsCancellationRequested || _disposed)
                return;
            if (code == 0 && snapshot is not null)
            {
                target.Snapshot = snapshot;
                ulong? lastFolder = incoming ? target.Preferences.LastSharedFolder : target.Preferences.LastFolder;
                ulong folder = lastFolder ?? snapshot.Root;
                target.Folder = snapshot.IsFolder(folder) ? folder : snapshot.Root;
                target.Selected.RemoveWhere(id => snapshot.Find(id) is null);
                HasSnapshot = true;
            }
            else
            {
                target.Snapshot = null;
                HasSnapshot = false;
                _loadMessage = DriveLoadError(code);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Log.WriteWarning("MEGA cloud drive list failed: " + Log.SafeException(ex));
            target.Snapshot = null;
            HasSnapshot = false;
            _loadMessage = UiText.Get("Could not load the MEGA cloud drive.");
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, source))
            {
                _loadCancellation = null;
                IsLoading = false;
                if (!_disposed)
                    RefreshRows();
            }
            source.Dispose();
        }
    }

    private static string DriveLoadError(int code) => code switch
    {
        MegaAccountService.SessionExpired => UiText.Get("Sign in to MEGA again"),
        MegaAccountService.BackendMissing => UiText.Get("This build does not include the MEGA account module. Anonymous downloads are still available."),
        MegaAccountService.Busy => UiText.Get("Connecting to MEGA…"),
        _ => UiText.Get("Could not load the MEGA cloud drive.") + " " + MegaAccountService.ErrorText(code)
    };

    private void RefreshRows()
    {
        _rowsByNode.Clear();
        Rows.Clear();
        Breadcrumbs.Clear();
        Favorites.Clear();
        _shownNodes = Array.Empty<MegaDriveNode>();
        AccountDrive? drive = _drive;
        MegaDriveSnapshot? snapshot = drive?.Snapshot;
        bool ready = snapshot is not null && _loadCancellation is null;
        LocationVisible = WholeDrive || ShowSelectedOnly;
        OnPropertyChanged(nameof(CanUp));
        OnPropertyChanged(nameof(CanFavorite));
        OnPropertyChanged(nameof(CanCheckNew));

        if (!ready || drive is null || snapshot is null)
        {
            Status = _loadMessage.Length > 0 ? _loadMessage : UiText.Get("Choose a MEGA account.");
            SelectionCount = 0;
            IsBound = false;
            UpdateActions();
            return;
        }

        MegaDriveNode.FileKind kind = SelectedType?.Kind ?? MegaDriveNode.FileKind.All;
        MegaDriveNode[] nodes = ShowSelectedOnly
            ? MegaDriveSnapshot.Sort(drive.Selected.Select(snapshot.Find).Where(node => node is not null), _sortByTime, _descending)
            : snapshot.Browse(drive.Folder, SearchText.Trim(), WholeDrive, kind, _sortByTime, _descending);

        foreach (MegaDriveNode node in nodes)
        {
            var row = new CloudDriveRowViewModel(this, node, drive.Selected.Contains(node.Id),
                LocationText(snapshot, drive, node.ParentId));
            _rowsByNode[node.Id] = row;
            Rows.Add(row);
        }
        _shownNodes = nodes;

        foreach (MegaDriveNode crumb in snapshot.Breadcrumbs(drive.Folder))
        {
            Breadcrumbs.Add(new CloudCrumbViewModel(this, crumb.Id,
                crumb.Id == snapshot.Root ? (SelectedSource?.Caption ?? UiText.Get("Cloud drive")) : crumb.Name));
        }
        foreach (ulong favoriteId in drive.Preferences.Favorites.Where(snapshot.IsFolder))
        {
            MegaDriveNode node = snapshot.Find(favoriteId);
            Favorites.Add(new CloudFavoriteViewModel(this, node.Id, node.Name));
        }

        SelectionCount = drive.Selected.Count;
        IsBound = drive.Preferences.FolderDestinations.ContainsKey(drive.Folder);
        if (Rows.Count == 0)
        {
            string emptyKey = ShowSelectedOnly
                ? "No files selected."
                : SearchText.Length > 0 || kind != MegaDriveNode.FileKind.All
                    ? "No matches in this folder."
                    : drive.Incoming && drive.Folder == snapshot.Root && !snapshot.InDirectory(drive.Folder).Any()
                        ? "No shared folders."
                        : "This folder is empty.";
            Status = UiText.Get(emptyKey);
        }
        else
        {
            Status = UiText.Format("{0} items · {1} selected", Rows.Count, drive.Selected.Count);
        }
        UpdateActions();
    }

    private bool _sortByTime;
    private bool _descending;

    private void UpdateActions()
    {
        OnPropertyChanged(nameof(CanUp));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanCheckNew));
        OnPropertyChanged(nameof(CanFavorite));
        OnPropertyChanged(nameof(AddButtonText));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(ViewSelectedText));
    }

    private string LocationText(MegaDriveSnapshot snapshot, AccountDrive drive, ulong folder)
    {
        MegaDriveNode[] trail = snapshot.Breadcrumbs(folder).ToArray();
        if (trail.Length == 0)
            return string.Empty;
        return string.Join(" / ", trail.Select(node =>
            node.Id == snapshot.Root ? (SelectedSource?.Caption ?? UiText.Get("Cloud drive")) : node.Name));
    }

    internal void SetSelected(MegaDriveNode node, bool selected)
    {
        if (_drive is null)
            return;
        if (selected)
            _drive.Selected.Add(node.Id);
        else
            _drive.Selected.Remove(node.Id);
        SelectionCount = _drive.Selected.Count;
        if (ShowSelectedOnly && !selected)
            Dispatcher.UIThread.Post(RefreshRows);
    }

    [RelayCommand]
    private void Refresh() => _ = LoadDriveAsync();

    [RelayCommand]
    private void Up()
    {
        if (_drive?.Snapshot is null)
            return;
        Navigate(_drive.Snapshot.Find(_drive.Folder)?.ParentId ?? _drive.Snapshot.Root);
    }

    internal void Navigate(ulong folder)
    {
        if (_drive?.Snapshot is null || !_drive.Snapshot.IsFolder(folder))
            return;
        ShowSelectedOnly = false;
        _drive.Folder = folder;
        if (_drive.Incoming)
            _drive.Preferences.LastSharedFolder = folder;
        else
            _drive.Preferences.LastFolder = folder;
        SearchText = string.Empty;
        WholeDrive = false;
        RefreshRows();
        SavePreferences();
    }

    [RelayCommand]
    private void GoToCrumb(CloudCrumbViewModel? crumb)
    {
        if (crumb is not null)
            Navigate(crumb.Id);
    }

    [RelayCommand]
    private void GoToFavorite(CloudFavoriteViewModel? favorite)
    {
        if (favorite is not null)
            Navigate(favorite.Id);
    }

    [RelayCommand]
    internal void OpenRow(CloudDriveRowViewModel? row)
    {
        if (row is null)
            return;
        if (row.Node.IsFolder)
            Navigate(row.Node.Id);
        else
            _ = DownloadSelectionAsync(new[] { row.Node.Id }, boundDestination: null);
    }

    [RelayCommand]
    internal void GoToFolder(CloudDriveRowViewModel? row)
    {
        if (row is not null)
            Navigate(row.Node.ParentId);
    }

    [RelayCommand]
    private void SelectAllVisible()
    {
        if (_drive is null)
            return;
        IEnumerable<MegaDriveNode> items = ShowSelectedOnly
            ? _shownNodes
            : _shownNodes.Where(node => node.IsFolder || SelectedType?.Kind == MegaDriveNode.FileKind.All ||
                node.Kind == (SelectedType?.Kind ?? MegaDriveNode.FileKind.All));
        foreach (MegaDriveNode node in items.ToArray())
        {
            _drive.Selected.Add(node.Id);
            if (_rowsByNode.TryGetValue(node.Id, out CloudDriveRowViewModel? row))
                row.SyncSelection(true);
        }
        SelectionCount = _drive.Selected.Count;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (_drive is null)
            return;
        _drive.Selected.Clear();
        foreach (CloudDriveRowViewModel row in Rows)
            row.SyncSelection(false);
        SelectionCount = 0;
        if (ShowSelectedOnly)
            RefreshRows();
    }

    [RelayCommand]
    private void ToggleViewSelected() => ShowSelectedOnly = !ShowSelectedOnly;

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (_drive is null || _drive.Snapshot is null)
            return;
        ulong folder = _drive.Folder;
        if (_drive.Preferences.Favorites.Contains(folder))
            _drive.Preferences.Favorites.Remove(folder);
        else
            _drive.Preferences.Favorites.Add(folder);
        SavePreferences();
        RefreshRows();
    }

    [RelayCommand]
    private void SortByName()
    {
        _descending = _sortByTime ? false : !_descending;
        _sortByTime = false;
        SortNameGlyph = _descending ? "↓" : "↑";
        SortTimeGlyph = string.Empty;
        RefreshRows();
    }

    [RelayCommand]
    private void SortByTime()
    {
        _descending = !_sortByTime ? true : !_descending;
        _sortByTime = true;
        SortTimeGlyph = _descending ? "↓" : "↑";
        SortNameGlyph = string.Empty;
        RefreshRows();
    }

    [RelayCommand]
    private async Task BindFolderAsync()
    {
        if (_drive?.Snapshot is null)
            return;
        ulong folder = _drive.Folder;
        MegaDriveNode? node = _drive.Snapshot.Find(folder);
        if (node is null || !node.IsFolder)
            return;
        MegaDriveSubscription? existing = _accounts.GetDriveSubscriptions(_currentEmail)
            .FirstOrDefault(item => item.Folder == folder);
        MegaDriveSubscription settings = existing?.Copy() ?? new MegaDriveSubscription
        {
            Folder = folder,
            Destination = _engine.Config.RutaDefecto,
            Incoming = _drive.Incoming
        };
        settings.Name = node.Name;
        settings.Incoming = _drive.Incoming;
        if (EditSubscriptionAsync is null)
            return;
        MegaFolderSubscriptionSettings? result = await EditSubscriptionAsync(settings);
        if (result is null)
            return;
        settings.Destination = result.Destination;
        settings.Extensions = result.Extensions;
        settings.CheckMinutes = result.CheckMinutes;
        settings.AutoQueue = result.AutoQueue;
        settings.Paused = result.Paused;
        int saved = await Task.Run(() => _accounts.SaveDriveSubscriptionSettings(_currentEmail, settings));
        if (saved != 0)
        {
            await _dialogs.ShowMessageAsync(UiText.Get("MEGA cloud drive"), MegaAccountService.ErrorText(saved));
            return;
        }
        _drive.Preferences = _accounts.GetDrivePreferences(_currentEmail);
        RefreshRows();
    }

    [RelayCommand]
    private void UnbindFolder()
    {
        if (_drive is null)
            return;
        _drive.Preferences.FolderDestinations.Remove(_drive.Folder);
        _drive.Preferences.BoundFolderPaths.Remove(_drive.Folder);
        SavePreferences();
        RefreshRows();
    }

    [RelayCommand]
    private async Task CheckNewAsync()
    {
        if (_drive?.Snapshot is null || !IsBound || _drive.Preferences.FolderDestinations.TryGetValue(_drive.Folder, out string? destination) == false)
            return;
        ulong folder = _drive.Folder;
        AccountDrive selectedDrive = _drive;
        MegaDriveSnapshot snapshot = selectedDrive.Snapshot;
        bool incoming = selectedDrive.Incoming;
        var paths = new Dictionary<ulong, string>();
        if (selectedDrive.Preferences.BoundFolderPaths.TryGetValue(folder, out Dictionary<ulong, string>? previous) && previous is not null)
            paths = new Dictionary<ulong, string>(previous);
        List<MegaDriveQueueItem> queue = _engine.SnapshotCloudQueue(_currentEmail);
        Status = UiText.Get("Checking the bound folder…");
        try
        {
            string email = _currentEmail;
            MegaDriveChanges? changes = await Task.Run(() =>
            {
                int result = _accounts.ReadDrive(email, CancellationToken.None, out MegaDriveSnapshot latest,
                    incomingShares: incoming);
                if (result != 0 || latest is null || !latest.IsFolder(folder))
                    throw new ApplicationException(MegaAccountService.ErrorText(result == 0 ? -9 : result));
                return _accounts.DriveHistory.FindBoundChanges(email, latest, folder, destination!, queue, paths);
            });
            if (changes is null)
                return;
            if (changes.Plan.Files.Count == 0)
            {
                Status = UiText.Get("No new or changed files in this folder.");
                return;
            }
            await ShowPlanAsync(changes.Plan, snapshot.Find(folder)?.Name ?? UiText.Get("Cloud drive"),
                destination!, changes.Pending, changes.Updated, incremental: true, trackedFolder: folder);
            selectedDrive.Preferences.BoundFolderPaths[folder] = paths;
            SavePreferences();
        }
        catch (ApplicationException ex)
        {
            await _dialogs.ShowMessageAsync(UiText.Get("MEGA cloud drive"), ex.Message);
            Status = ex.Message;
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Cloud drive change check failed: " + Log.SafeException(ex));
            await _dialogs.ShowMessageAsync(UiText.Get("MEGA cloud drive"), UiText.Get("Could not check the bound folder."));
            Status = UiText.Get("Could not check the bound folder.");
        }
    }

    private async Task DownloadSelectionAsync(IEnumerable<ulong> selection, string? boundDestination)
    {
        if (_drive?.Snapshot is null || SelectedAccount is null)
            return;
        ulong[] ids = selection.ToArray();
        if (ids.Length == 0)
            return;
        MegaDriveSnapshot snapshot = _drive.Snapshot;
        string name = ids.Length == 1 ? snapshot.Find(ids[0])?.Name ?? UiText.Get("Cloud drive") : UiText.Get("Cloud drive");
        MegaDriveDownload plan = await Task.Run(() => snapshot.PlanDownload(ids));
        HashSet<ulong> queued = _engine.SnapshotCloudQueue(_currentEmail).Select(item => item.Handle).ToHashSet();
        await ShowPlanAsync(plan, name, boundDestination ?? _engine.Config.RutaDefecto, queued,
            new HashSet<ulong>(), incremental: false, trackedFolder: null);
    }

    private async Task ShowPlanAsync(MegaDriveDownload plan, string title, string initialDestination,
        HashSet<ulong> queued, HashSet<ulong> updated, bool incremental, ulong? trackedFolder)
    {
        if (SelectedAccount is null || ShowDownloadPreviewAsync is null)
            return;
        if (plan.Files.Count == 0 && plan.Directories.Count == 0)
        {
            Status = UiText.Get("There is nothing to download.");
            return;
        }

        var request = new CloudDownloadRequest
        {
            Plan = plan,
            Title = title,
            InitialDestination = initialDestination,
            Queued = queued,
            Incremental = incremental,
            Updated = updated
        };
        CloudDownloadConfirmation? confirmation = await ShowDownloadPreviewAsync.Invoke(request);
        if (confirmation is null)
            return;
        try
        {
            Status = UiText.Get("Adding files to the queue…");
            string email = _currentEmail;
            Paquete? package = await Task.Run(() => confirmation.Plan.Build(email, confirmation.Destination,
                title, _engine.Config.ExtraerAutomaticamente, CancellationToken.None));
            if (package is null)
                return;
            if (confirmation.Plan.Files.Count == 0)
            {
                foreach (Fichero file in package.SnapshotFicheros())
                    file.Dispose();
                RefreshRows();
                Status = UiText.Get("Folders created at the selected save path.");
                return;
            }
            int added = _engine.AddUniquePackage(package);
            if (added <= 0)
            {
                foreach (Fichero file in package.SnapshotFicheros())
                    file.Dispose();
                Status = added == 0
                    ? UiText.Get("All selected files are already queued.")
                    : UiText.Get("The download queue is still loading.");
                return;
            }
            if (!trackedFolder.HasValue && _drive is not null)
                _drive.Selected.ExceptWith(plan.Files.Select(file => file.Key.Id));
            SelectionCount = _drive?.Selected.Count ?? 0;
            RefreshRows();
            Status = UiText.Format("Added {0} file(s) to the queue.", added);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Cloud drive queue creation failed: " + Log.SafeException(ex));
            Status = UiText.Get("Could not add the cloud files.");
            await _dialogs.ShowMessageAsync(UiText.Get("MEGA cloud drive"), UiText.Get("Could not add the cloud files."));
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (_drive is null)
            return;
        await DownloadSelectionAsync(_drive.Selected.ToArray(), boundDestination: null);
    }

    private void SavePreferences()
    {
        if (_drive is null || _currentEmail.Length == 0)
            return;
        string email = _currentEmail;
        MegaDrivePreferences preferences = _drive.Preferences.Copy();
        _ = Task.Run(() =>
        {
            int result = _accounts.SaveDrivePreferences(email, preferences);
            if (result != 0 && result != MegaAccountService.SessionExpired)
                Log.WriteWarning("Could not save MEGA cloud drive preferences: " + result);
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _accounts.Changed -= OnAccountsChanged;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        foreach (AccountDrive drive in _drives.Values)
            drive.Snapshot = null;
        SavePreferencesOnShutdown();
    }

    private void SavePreferencesOnShutdown()
    {
        if (_drive is null || _currentEmail.Length == 0)
            return;
        _accounts.SaveDrivePreferences(_currentEmail, _drive.Preferences);
    }
}

public sealed class CloudDriveAccountChoice
{
    public CloudDriveAccountChoice(string email, string caption)
    {
        Email = email;
        Caption = caption;
    }

    public string Email { get; }
    public string Caption { get; }
}

public sealed class CloudDriveSourceChoice
{
    public CloudDriveSourceChoice(bool incoming, string caption)
    {
        Incoming = incoming;
        Caption = caption;
    }

    public bool Incoming { get; }
    public string Caption { get; }
}

public sealed class CloudDriveTypeChoice
{
    public CloudDriveTypeChoice(MegaDriveNode.FileKind kind, string caption)
    {
        Kind = kind;
        Caption = caption;
    }

    public MegaDriveNode.FileKind Kind { get; }
    public string Caption { get; }
}

public partial class CloudCrumbViewModel : ObservableObject
{
    private readonly CloudDrivePageViewModel _owner;

    public CloudCrumbViewModel(CloudDrivePageViewModel owner, ulong id, string caption)
    {
        _owner = owner;
        Id = id;
        Caption = caption;
    }

    public ulong Id { get; }
    public string Caption { get; }

    [RelayCommand]
    private void Go() => _owner.Navigate(Id);
}

public partial class CloudFavoriteViewModel : ObservableObject
{
    private readonly CloudDrivePageViewModel _owner;

    public CloudFavoriteViewModel(CloudDrivePageViewModel owner, ulong id, string caption)
    {
        _owner = owner;
        Id = id;
        Caption = caption;
    }

    public ulong Id { get; }
    public string Caption { get; }

    [RelayCommand]
    private void Go() => _owner.Navigate(Id);
}

public partial class CloudDriveRowViewModel : ObservableObject
{
    private readonly CloudDrivePageViewModel _owner;
    private bool _suppress;

    public CloudDriveRowViewModel(CloudDrivePageViewModel owner, MegaDriveNode node, bool selected, string location)
    {
        _owner = owner;
        Node = node;
        Location = location;
        isSelected = selected;
    }

    public MegaDriveNode Node { get; }

    public bool IsFolder => Node.IsFolder;

    public string Name => Node.Name;

    public string SizeText => Node.IsFolder ? string.Empty : MegaAccountText.FormatBytes(Node.Bytes);

    public string ModifiedText => Node.Modified > 0
        ? DateTimeOffset.FromUnixTimeSeconds(Node.Modified).LocalDateTime.ToString("g")
        : string.Empty;

    public string Location { get; }

    public string FolderMark => Node.IsFolder ? "▸" : string.Empty;

    [ObservableProperty]
    private bool isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppress)
            _owner.SetSelected(Node, value);
    }

    internal void SyncSelection(bool value)
    {
        if (IsSelected == value)
            return;
        _suppress = true;
        IsSelected = value;
        _suppress = false;
    }

    [RelayCommand]
    private void Open() => _owner.OpenRow(this);

    [RelayCommand]
    private void GoToFolder() => _owner.GoToFolder(this);
}

public sealed class CloudDownloadRequest
{
    public MegaDriveDownload Plan { get; init; } = null!;
    public string Title { get; init; } = string.Empty;
    public string InitialDestination { get; init; } = string.Empty;
    public HashSet<ulong> Queued { get; init; } = new();
    public HashSet<ulong> Updated { get; init; } = new();
    public bool Incremental { get; init; }
}

public sealed class CloudDownloadConfirmation
{
    public MegaDriveDownload Plan { get; init; } = null!;
    public string Destination { get; init; } = string.Empty;
}

/// <summary>Preview and destination confirmation for a cloud-drive download.</summary>
public partial class CloudDownloadViewModel : ObservableObject
{
    private readonly CloudDownloadRequest _request;
    private MegaDriveDownload _confirmedPlan;

    [ObservableProperty]
    private string destination;

    [ObservableProperty]
    private bool skipQueued;

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private string acceptText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CloudDownloadRow> rows = new();

    public CloudDownloadViewModel(CloudDownloadRequest request)
    {
        _request = request;
        _confirmedPlan = request.Plan;
        destination = request.InitialDestination;
        skipQueued = request.Incremental;
        Rebuild();
    }

    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public MegaDriveDownload ConfirmedPlan => _confirmedPlan;

    public bool HasQueued => _request.Plan.Files.Any(file => _request.Queued.Contains(file.Key.Id));

    public bool IsIncremental => _request.Incremental;

    public string Title => _request.Title;

    partial void OnSkipQueuedChanged(bool value) => Rebuild();

    partial void OnDestinationChanged(string value) => OnPropertyChanged(nameof(CanAccept));

    public bool CanAccept => !string.IsNullOrWhiteSpace(Destination) && Path.IsPathFullyQualified(Destination.Trim()) &&
        (_confirmedPlan.Files.Count > 0 || _confirmedPlan.Directories.Count > 0);

    private void Rebuild()
    {
        _confirmedPlan = SkipQueued
            ? _request.Plan.KeepFiles(file => !_request.Queued.Contains(file.Key.Id), preserveEmpty: true)
            : _request.Plan;
        Rows.Clear();
        foreach (KeyValuePair<MegaDriveNode, string> file in _confirmedPlan.Files)
        {
            string status = _request.Queued.Contains(file.Key.Id)
                ? UiText.Get(SkipQueued ? "Skipped (already queued)" : "Already queued")
                : _request.Incremental
                    ? UiText.Get(_request.Updated.Contains(file.Key.Id) ? "Changed file" : "New file")
                    : string.Empty;
            Rows.Add(new CloudDownloadRow(file.Key.Name, string.IsNullOrEmpty(file.Value) ? "." : file.Value,
                MegaAccountText.FormatBytes(file.Key.Bytes), status));
        }
        string size = MegaAccountText.FormatBytes((long)Math.Min(_confirmedPlan.TotalBytes, long.MaxValue));
        Summary = UiText.Format("{0} file(s) · {1} folder(s) · {2}", _confirmedPlan.Files.Count,
            _confirmedPlan.Directories.Count, size);
        AcceptText = _confirmedPlan.Files.Count > 0 ? UiText.Get("Add to queue") : UiText.Get("Create folders");
        OnPropertyChanged(nameof(CanAccept));
        OnPropertyChanged(nameof(HasQueued));
        OnPropertyChanged(nameof(IsIncremental));
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (PickFolderAsync is null)
            return;
        string? folder = await PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
            Destination = folder;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (!CanAccept)
            return;
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);
}

public sealed class CloudDownloadRow
{
    public CloudDownloadRow(string name, string folder, string size, string status)
    {
        Name = name;
        Folder = folder;
        Size = size;
        Status = status;
    }

    public string Name { get; }
    public string Folder { get; }
    public string Size { get; }
    public string Status { get; }
}
