using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.Platform;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// Queue page backed by <see cref="DownloadEngineService"/>. The engine publishes its
/// queue summary once per tick; this page only walks the rows it actually displays and
/// rebuilds the visible row list when the queue or the filter changes, so selection and
/// scroll survive ordinary progress updates.
/// </summary>
public partial class DownloadsPageViewModel : ViewModelBase, IDisposable
{
    private readonly DownloadEngineService _engine;
    private readonly IDialogService _dialogs;
    private readonly IPlatformServices _platform;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _searchTimer;
    private readonly Dictionary<object, DownloadItemViewModel> _rowCache = new(ReferenceEqualityComparer.Instance);
    private bool _loading;
    private bool _disposed;
    private bool _loadFailed;
    private string? _actionFeedback;
    private DateTime _feedbackUntil;
    private readonly Queue<double> _speedSamples = new(60);
    private int _lastSummaryVersion = -1;
    private int _lastQueueVersion = -1;
    private string _appliedSearch = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<double> speedHistory = Array.Empty<double>();

    [ObservableProperty]
    private string transferSpeed = "0 KB/s";

    [ObservableProperty]
    private int activeCount;

    [ObservableProperty]
    private int queuedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaitingText))]
    private int pausedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    private int failedCount;

    public bool HasFailed => FailedCount > 0;

    [ObservableProperty]
    private int completedCount;

    [ObservableProperty]
    private string waitingText = string.Empty;

    [ObservableProperty]
    private bool showMetrics = true;

    [ObservableProperty]
    private string detailSpeed = string.Empty;

    [ObservableProperty]
    private string detailProgress = string.Empty;

    [ObservableProperty]
    private string detailIdentity = string.Empty;

    [ObservableProperty]
    private string detailSource = string.Empty;

    [ObservableProperty]
    private string detailDownloaded = string.Empty;

    [ObservableProperty]
    private string detailEta = string.Empty;

    public bool HasSelectedDownload => SelectedDownload is not null;

    [ObservableProperty]
    private bool hasFiles;

    [ObservableProperty]
    private bool canAddFromEmpty;

    [ObservableProperty]
    private bool canResetFilter;

    [ObservableProperty]
    private string status = UiText.Get("Loading saved queue…");

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private bool isEmpty = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canStartAll;

    [ObservableProperty]
    private bool canPauseAll;

    [ObservableProperty]
    private bool canStopAll;

    [ObservableProperty]
    private DownloadItemViewModel? selectedDownload;

    [ObservableProperty]
    private QueueFilterOption? selectedFilter;

    [ObservableProperty]
    private string emptyTitle = UiText.Get("Loading saved queue…");

    [ObservableProperty]
    private string emptyMessage = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool hasSearch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsToggleText))]
    private bool showDetails = false;

    public string DetailsToggleText => UiText.Get(ShowDetails ? "Collapse details" : "Show details");

    [ObservableProperty]
    private string detailTitle = string.Empty;

    [ObservableProperty]
    private string detailState = string.Empty;

    [ObservableProperty]
    private string detailPath = string.Empty;

    [ObservableProperty]
    private string detailSize = string.Empty;

    [ObservableProperty]
    private string detailError = string.Empty;

    [ObservableProperty]
    private bool hasDetailError;

    [ObservableProperty]
    private int selectionCount;

    public IReadOnlyList<QueueFilterOption> Filters { get; } =
    [
        new(QueueFilter.All, "All statuses"),
        new(QueueFilter.Active, "Downloading"),
        new(QueueFilter.Waiting, "Waiting"),
        new(QueueFilter.Completed, "Completed"),
        new(QueueFilter.Failed, "Failed")
    ];

    public DownloadsPageViewModel(DownloadEngineService engine, IDialogService dialogs, IPlatformServices platform)
    {
        _engine = engine;
        _dialogs = dialogs;
        _platform = platform;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => Refresh(sampleSpeed: true);
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplySearch();
        };
        selectedFilter = Filters[0];
        UiText.Changed += ApplyLanguage;

        _ = LoadAsync();
    }

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();

    /// <summary>Rows the user selected; batch commands act on this set.</summary>
    public ObservableCollection<DownloadItemViewModel> SelectedDownloads { get; } = new();

    /// <summary>
    /// Raised after the visible rows were rearranged, so the view can put the list's
    /// own selection back on the rows that are still on screen. The view is the only
    /// place that can write <c>ListBox.SelectedItems</c>.
    /// </summary>
    public event Action? SelectionSyncRequested;

    public bool CanBatch => SelectionCount > 1;

    /// <summary>Called by the view after the list selection changed.</summary>
    public void UpdateBatchSelection() => SelectionCount = SelectedDownloads.Count;

    /// <summary>Drops the low-frequency speed column and the detail panel on narrow windows.</summary>
    public void SetCompact(bool compact)
    {
        ShowMetrics = !compact;
        if (compact)
            ShowDetails = false;
        foreach (var row in _rowCache.Values)
            row.ShowMetrics = ShowMetrics;
    }

    /// <summary>
    /// The category rail costs 190 DIP of width. Below that point the queue itself needs
    /// the room more than the rail does, so it folds away and the status chips on each
    /// row keep carrying the same information.
    /// </summary>
    [ObservableProperty]
    private bool showRail = true;

    /// <summary>Applies every width-dependent decision from one measurement.</summary>
    public void SetAvailableWidth(double width)
    {
        if (width <= 0)
            return;
        SetCompact(width < 980);
        ShowRail = width >= 880;
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        Status = UiText.Get("Loading saved queue…");

        try
        {
            await _engine.LoadAsync();
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            Log.WriteError("Queue loading failed: " + Log.SafeException(ex));
        }
        finally
        {
            IsBusy = false;
        }

        if (_disposed)
            return;

        // The queue is ready to show now; the optional servers start afterwards so a
        // slow bind never delays the workbench.
        Refresh();
        _refreshTimer.Start();
        _ = StartServersAsync();
    }

    private async Task StartServersAsync()
    {
        try
        {
            string error = await Task.Run(() => WebServerHost.Start(_engine, _engine.Config));
            if (!string.IsNullOrEmpty(error))
                Log.WriteError("Web server did not start: " + error);
        }
        catch (Exception ex)
        {
            Log.WriteError("Starting the web server failed: " + Log.SafeException(ex));
        }
    }

    private void ApplyLanguage(object? sender, EventArgs args)
    {
        foreach (var filter in Filters)
            filter.ApplyLanguage();
        foreach (var item in _rowCache.Values)
            item.ApplyLanguage();
        Refresh(forceRebuild: true);
    }

    [ObservableProperty]
    private string draftLinks = string.Empty;

    [RelayCommand]
    private Task EncodeLinksAsync() => _dialogs.ShowEncodeLinksAsync();

    [RelayCommand]
    private Task GenerateElcAsync() => _dialogs.ShowElcGenerateAsync();

    [RelayCommand]
    private async Task AddLinksAsync()
    {
        if (await _dialogs.ShowAddLinksAsync(DraftLinks) is { } result)
        {
            DraftLinks = string.Empty;
            SetFeedback(result);
            Refresh(forceRebuild: true);
        }
    }

    [RelayCommand]
    private void ResetFilter()
    {
        SearchText = string.Empty;
        _appliedSearch = string.Empty;
        HasSearch = false;
        SelectedFilter = Filters[0];
        Refresh(forceRebuild: true);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void StartAll()
    {
        // The engine refuses to start outside the download schedule or during a
        // quota cooldown, and explains why.
        if (!_engine.TryStartAll(out string reason))
            SetFeedback(reason);
        else
            _actionFeedback = null;
        Refresh();
    }

    [RelayCommand]
    private void PauseAll()
    {
        _actionFeedback = null;
        _engine.PauseAll();
        Refresh();
    }

    [RelayCommand]
    private void StopAll()
    {
        _actionFeedback = null;
        _engine.StopAll();
        Refresh();
    }

    [RelayCommand]
    private void RetryFailed()
    {
        int retried = _engine.RetryFailed();
        SetFeedback(retried > 0
            ? UiText.Format("Retrying {0} failed task(s).", retried)
            : UiText.Get("Nothing to retry: the remaining failures are permanent."));
        Refresh();
    }

    [RelayCommand]
    private async Task BatchPauseAsync()
    {
        var items = SelectedDownloads.Select(row => row.Item).ToArray();
        if (items.Length == 0)
            return;
        await Task.Run(() =>
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case Fichero file:
                        _engine.PauseFile(file);
                        break;
                    case Paquete package:
                        foreach (Fichero child in package.SnapshotFicheros())
                            _engine.PauseFile(child);
                        break;
                }
            }
        });
        SetFeedback(UiText.Format("Paused {0} item(s).", items.Length));
        Refresh(forceRebuild: true);
    }

    [RelayCommand]
    private async Task BatchRetryAsync()
    {
        var items = SelectedDownloads.Select(row => row.Item).ToArray();
        if (items.Length == 0)
            return;
        int retried = await Task.Run(() =>
        {
            int count = 0;
            foreach (var item in items)
            {
                switch (item)
                {
                    case Fichero file:
                        if (_engine.RetryFile(file))
                            count++;
                        break;
                    case Paquete package:
                        foreach (Fichero child in package.SnapshotFicheros())
                        {
                            if (_engine.RetryFile(child))
                                count++;
                        }
                        break;
                }
            }
            return count;
        });
        SetFeedback(retried > 0
            ? UiText.Format("Retrying {0} task(s).", retried)
            : UiText.Get("Nothing to retry: the remaining failures are permanent."));
        Refresh(forceRebuild: true);
    }

    [RelayCommand]
    private async Task BatchRemoveAsync()
    {
        var items = SelectedDownloads.Select(row => row.Item).ToArray();
        if (items.Length == 0)
            return;
        bool confirmed = await _dialogs.ConfirmAsync(
            UiText.Get("Remove download"),
            UiText.Format("Remove {0} item(s) from the queue? Files already downloaded are kept.", items.Length),
            UiText.Get("Remove"),
            UiText.Get("Cancel"));
        if (!confirmed)
            return;

        try
        {
            await Task.Run(() =>
            {
                foreach (var item in items)
                    _engine.Remove(item, deleteFiles: false);
            });
            SetFeedback(UiText.Format("Removed {0} item(s).", items.Length));
        }
        catch (Exception ex)
        {
            Log.WriteError("Removing downloads failed: " + Log.SafeException(ex));
            SetFeedback(UiText.Get("Could not remove the download."));
        }
        Refresh(forceRebuild: true);
    }

    [RelayCommand]
    private void OpenSelectedFile() => OpenOutput(SelectedDownload, openFolder: false);

    [RelayCommand]
    private void OpenSelectedFolder() => OpenOutput(SelectedDownload, openFolder: true);

    internal void StartItem(object item)
    {
        switch (item)
        {
            case Fichero file:
                _engine.StartFile(file);
                break;
            case Paquete package:
                foreach (Fichero file in package.SnapshotFicheros())
                {
                    if (file.DescargaEstado() is Estado.EnCola or Estado.Pausado or Estado.Erroneo)
                        _engine.StartFile(file);
                }
                break;
        }

        Refresh();
    }

    internal void PauseItem(object item)
    {
        switch (item)
        {
            case Fichero file:
                _engine.PauseFile(file);
                break;
            case Paquete package:
                foreach (Fichero file in package.SnapshotFicheros())
                    _engine.PauseFile(file);
                break;
        }

        Refresh();
    }

    internal void RetryItem(object item)
    {
        switch (item)
        {
            case Fichero file:
                if (!_engine.RetryFile(file))
                    SetFeedback(UiText.Get("This failure is permanent: the link is no longer valid."));
                break;
            case Paquete package:
                foreach (Fichero file in package.SnapshotFicheros())
                    _engine.RetryFile(file);
                break;
        }

        Refresh(forceRebuild: true);
    }

    internal bool CanMoveBeside(DownloadItemViewModel source, DownloadItemViewModel target) =>
        _engine.CanMoveBeside(source.Item, target.Item);

    internal void MoveBeside(DownloadItemViewModel source, DownloadItemViewModel target, bool after)
    {
        if (!_engine.MoveBeside(source.Item, target.Item, after))
            return;
        Refresh(forceRebuild: true);
    }

    internal void MoveItem(object item, QueueMove move)
    {
        if (!_engine.MoveTo(item, move))
            return;
        // The scheduler walks the queue in list order, so the new position is what the
        // next free slot picks up.
        Refresh(forceRebuild: true);
    }

    internal void OpenOutput(object? item, bool openFolder)
    {
        string? path = null;
        try
        {
            switch (item)
            {
                case Fichero file:
                    string? directory = file.RutaLocal;
                    string? name = file.NombreFichero;
                    path = openFolder
                        ? directory
                        : (directory is not null && name is not null ? Path.Combine(directory, name) : directory);
                    break;
                case Paquete package:
                    path = package.RutaLocal;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Resolving the output path failed: " + Log.SafeException(ex));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            SetFeedback(UiText.Get("This task has no local output yet."));
            Refresh();
            return;
        }

        try
        {
            _platform.OpenWithDefaultApplication(path);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Opening the output failed: " + Log.SafeException(ex));
            SetFeedback(UiText.Get("Could not open the output."));
            Refresh();
        }
    }

    /// <summary>Set by the view: pick a download identity for the selected rows.</summary>
    public Func<string, Task<IdentityPickResult?>>? PickIdentityAsync { get; set; }

    internal async Task ChangeIdentityAsync(DownloadItemViewModel row)
    {
        if (PickIdentityAsync is null)
            return;
        var targets = SelectedDownloads.Contains(row) ? SelectedDownloads.ToList() : new List<DownloadItemViewModel> { row };
        var files = new List<Fichero>();
        var seen = new HashSet<Fichero>(ReferenceEqualityComparer.Instance);
        foreach (DownloadItemViewModel target in targets)
        {
            IEnumerable<Fichero> items = target.Item switch
            {
                Fichero file => new[] { file },
                Paquete package => package.SnapshotFicheros(),
                _ => Array.Empty<Fichero>()
            };
            foreach (Fichero file in items)
            {
                if (seen.Add(file))
                    files.Add(file);
            }
        }
        files.RemoveAll(file => file.DescargaEstado() == Estado.Completado);
        if (files.Count == 0)
            return;
        string current = files[0].EffectiveMegaAccountEmail;
        IdentityPickResult? picked = await PickIdentityAsync(current);
        if (picked is null || !picked.Confirmed)
            return;
        string problem = _engine.ChangeDownloadIdentity(files, picked.Email);
        if (problem.Length > 0)
        {
            string text = problem switch
            {
                "MegaDrive_AccountBound" => UiText.Get("Private cloud tasks keep their original MEGA account."),
                "MegaAccount_QueueAccountInUse" => UiText.Get("Stop the selected downloads before changing their identity."),
                "MegaAccount_QueueOnlyFiles" => UiText.Get("MEGA accounts can only be assigned to public single-file tasks."),
                _ => MegaAccountText.ErrorText(-1003)
            };
            await _dialogs.ShowMessageAsync(UiText.Get("Download identity"), text);
            return;
        }
        SetFeedback(UiText.Get("Download identity changed."));
        Refresh(forceRebuild: true);
    }

    internal async Task ShowPropertiesAsync(object item)
    {
        if (await _dialogs.ShowPropertiesAsync(item))
            Refresh(forceRebuild: true);
    }

    internal async Task RemoveItemAsync(object item, bool deleteFiles)
    {
        string name = item switch
        {
            Paquete package => package.DescargaNombre(),
            Fichero file => file.DescargaNombre(),
            _ => UiText.Get("this download")
        };

        string message = deleteFiles
            ? UiText.Format("Remove “{0}” and delete its downloaded files from disk?", name)
            : UiText.Format("Remove “{0}” from the queue? Files already downloaded are kept.", name);

        bool confirmed = await _dialogs.ConfirmAsync(
            deleteFiles ? UiText.Get("Delete download") : UiText.Get("Remove download"),
            message,
            deleteFiles ? UiText.Get("Delete") : UiText.Get("Remove"),
            UiText.Get("Cancel"));

        if (!confirmed)
            return;

        if (deleteFiles && !await ConfirmUnverifiedOutputsAsync(item))
            return;

        // Stop() can wait up to five seconds for an in-flight write; keep it off the UI thread.
        try
        {
            await Task.Run(() => _engine.Remove(item, deleteFiles));
            SetFeedback(UiText.Format(deleteFiles ? "Deleted {0}" : "Removed {0}", name));
        }
        catch (Exception ex)
        {
            Log.WriteError("Removing a download failed: " + Log.SafeException(ex));
            SetFeedback(UiText.Get("Could not remove the download."));
        }
        Refresh(forceRebuild: true);
    }

    /// <summary>
    /// Files from older links without an integrity checksum cannot be attributed
    /// automatically. The WinForms shell asks once and keeps the files when the user
    /// declines; without this the task disappears while its output stays on disk.
    /// </summary>
    private async Task<bool> ConfirmUnverifiedOutputsAsync(object item)
    {
        var candidates = new List<Fichero>();
        switch (item)
        {
            case Fichero file:
                candidates.Add(file);
                break;
            case Paquete package:
                candidates.AddRange(package.SnapshotFicheros());
                break;
        }

        if (candidates.Count == 0)
            return true;

        // Ownership can require reading the whole file to compute its checksum, so it
        // must not run on the UI thread.
        await Task.Run(() =>
        {
            foreach (Fichero file in candidates)
                file.PrepareOutputOwnership();
        });

        var unverified = candidates
            .Where(file => file.OutputNeedsConfirmation && !string.IsNullOrEmpty(file.OutputConfirmationPath))
            .ToList();
        if (unverified.Count == 0)
            return true;

        bool confirmed = await _dialogs.ConfirmAsync(
            UiText.Get("Delete download"),
            unverified.Count == 1
                ? UiText.Get("This file comes from an older link without an integrity checksum, so it cannot be verified automatically. Delete it anyway?")
                : UiText.Format("{0} files come from older links without an integrity checksum, so they cannot be verified automatically. Delete them anyway?", unverified.Count),
            UiText.Get("Delete anyway"),
            UiText.Get("Cancel"));

        if (!confirmed)
        {
            SetFeedback(UiText.Get("Nothing was deleted."));
            Refresh();
            return false;
        }

        foreach (Fichero file in unverified)
            file.ConfirmUnverifiedOutputDeletion = true;
        return true;
    }

    private void SetFeedback(string message)
    {
        _actionFeedback = message;
        _feedbackUntil = DateTime.UtcNow.AddSeconds(10);
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void ApplySearch()
    {
        if (string.Equals(_appliedSearch, SearchText ?? string.Empty, StringComparison.Ordinal))
            return;
        _appliedSearch = SearchText ?? string.Empty;
        HasSearch = _appliedSearch.Length > 0;
        Refresh(forceRebuild: true);
    }

    partial void OnSelectedFilterChanged(QueueFilterOption? value) => Refresh(forceRebuild: true);

    partial void OnSelectedDownloadChanged(DownloadItemViewModel? value)
    {
        UpdateDetails(value);
        OnPropertyChanged(nameof(HasSelectedDownload));
    }

    partial void OnSelectionCountChanged(int value) => OnPropertyChanged(nameof(CanBatch));

    private void UpdateDetails(DownloadItemViewModel? row)
    {
        if (row is null)
        {
            DetailTitle = string.Empty;
            DetailState = string.Empty;
            DetailPath = string.Empty;
            DetailSize = string.Empty;
            DetailSpeed = string.Empty;
            DetailProgress = string.Empty;
            DetailIdentity = string.Empty;
            DetailSource = string.Empty;
            DetailDownloaded = string.Empty;
            DetailEta = string.Empty;
            DetailError = string.Empty;
            HasDetailError = false;
            return;
        }

        DetailTitle = row.Title;
        DetailState = row.StateText;
        DetailSize = row.SizeText;
        DetailSpeed = row.SpeedText;
        DetailProgress = row.ProgressText;
        DetailIdentity = row.IdentityText;
        DetailSource = string.Empty;
        DetailDownloaded = string.Empty;
        DetailEta = string.Empty;
        switch (row.Item)
        {
            case Fichero file:
                DetailPath = file.RutaLocal is null
                    ? string.Empty
                    : Path.Combine(file.RutaLocal, file.NombreFichero ?? string.Empty);
                DetailSource = file.LinkVisible && !string.IsNullOrWhiteSpace(file.URL)
                    ? file.URL
                    : UiText.Get("Private MEGA file");
                DetailDownloaded = FormatBytes((long)(file.TamanoBytes * (double)file.DescargaPorcentaje() / 100d)) +
                    " / " + FormatBytes(file.TamanoBytes);
                DetailEta = file.DescargaTiempoEstimadoDescarga();
                DetailError = file.DescripcionError ?? string.Empty;
                break;
            case Paquete package:
                DetailPath = package.RutaLocal ?? string.Empty;
                DetailSource = UiText.Get("Package");
                DetailDownloaded = FormatBytes(package.DescargaTamanoBytes()) + " · " +
                    ((double)package.DescargaPorcentaje()).ToString("0.0", UiText.Culture) + "%";
                DetailError = string.Empty;
                break;
        }
        HasDetailError = !string.IsNullOrWhiteSpace(DetailError);
    }

    internal void Refresh(bool sampleSpeed = false, bool forceRebuild = false)
    {
        if (_disposed || _loading)
            return;

        _loading = true;
        try
        {
            var packages = _engine.SnapshotPackages();
            var engineSummary = _engine.Summary;
            int summaryVersion = _engine.SummaryVersion;
            int queueVersion = _engine.QueueVersion;

            bool queueChanged = queueVersion != _lastQueueVersion;
            bool rebuild = forceRebuild || queueChanged || summaryVersion != _lastSummaryVersion;
            _lastQueueVersion = queueVersion;
            _lastSummaryVersion = summaryVersion;

            if (rebuild)
                RebuildRows(packages);
            else
                UpdateVisibleRows();

            // Only tasks can leave the queue when the queue version moved; a quiet tick
            // does not need to walk every file to prune rows.
            if (queueChanged || forceRebuild)
                PruneRowCache(packages);

            IsEmpty = Downloads.Count == 0;
            HasFiles = packages.Count > 0;
            CanAddFromEmpty = !HasFiles && _engine.IsLoaded;
            CanResetFilter = IsEmpty && HasFiles;
            if (IsBusy)
            {
                EmptyTitle = UiText.Get("Loading saved queue…");
                EmptyMessage = string.Empty;
            }
            else if (_loadFailed)
            {
                EmptyTitle = UiText.Get("Could not load the download queue");
                EmptyMessage = UiText.Get("Check the application log for details.");
            }
            else if (!HasFiles)
            {
                EmptyTitle = UiText.Get("Add links");
                EmptyMessage = UiText.Get("Add a MEGA file or folder link to start a download.");
            }
            else if (HasSearch)
            {
                EmptyTitle = UiText.Get("Nothing matches this search");
                EmptyMessage = UiText.Get("Clear the search to see the whole queue.");
            }
            else
            {
                EmptyTitle = UiText.Get("Nothing matches this filter");
                EmptyMessage = UiText.Get("Choose another status to see more downloads.");
            }

            CanStartAll = _engine.IsLoaded && HasFiles && _engine.State != DownloadEngineState.Running;
            CanPauseAll = _engine.IsLoaded && _engine.State == DownloadEngineState.Running;
            CanStopAll = _engine.IsLoaded && _engine.State != DownloadEngineState.Stopped;
            ActiveCount = engineSummary.Active;
            QueuedCount = engineSummary.Queued;
            PausedCount = engineSummary.Paused;
            CompletedCount = engineSummary.Completed;
            FailedCount = engineSummary.Failed;
            WaitingText = BuildWaitingText(engineSummary);
            UpdateFilterCounts(engineSummary.Active, engineSummary.Queued, engineSummary.Paused,
                engineSummary.Completed, engineSummary.Failed);
            TransferSpeed = FormatSpeed(engineSummary.SpeedKBs);
            if (sampleSpeed)
            {
                if (_speedSamples.Count == 60)
                    _speedSamples.Dequeue();
                _speedSamples.Enqueue((double)engineSummary.SpeedKBs);
                SpeedHistory = _speedSamples.ToArray();
            }

            Summary = UiText.Format("{0} active · {1} queued · {2} paused · {3} completed · {4} failed",
                engineSummary.Active, engineSummary.Queued, engineSummary.Paused, engineSummary.Completed, engineSummary.Failed);
            Status = _actionFeedback is not null && DateTime.UtcNow < _feedbackUntil
                ? _actionFeedback
                : UiText.Get(_loadFailed ? "Could not load the download queue" : _engine.State switch
                {
                    DownloadEngineState.Running => "Queue running",
                    // The scheduler pauses and resumes by itself; saying why keeps the
                    // paused queue from looking like a manual pause.
                    DownloadEngineState.Paused when !_engine.ScheduleAllowsDownloadsNow => "Paused by the download schedule",
                    DownloadEngineState.Paused => "Queue paused",
                    _ => "Queue stopped"
                });
        }
        catch (Exception ex)
        {
            Status = UiText.Get("Queue refresh failed.");
            Log.WriteError("Queue refresh failed: " + Log.SafeException(ex));
        }
        finally
        {
            _loading = false;
        }
    }

    private string BuildWaitingText(QueueSummary summary)
    {
        if (MegaQuotaManager.IsQuarantined() && MegaQuotaManager.GetRemaining() is TimeSpan remaining)
            return UiText.Format("MEGA quota cooldown · {0}", FormatDuration(remaining));
        if (_engine.State == DownloadEngineState.Paused && !_engine.ScheduleAllowsDownloadsNow)
            return UiText.Get("Paused by the download schedule");
        int waiting = summary.Queued + summary.Paused;
        return waiting > 0
            ? UiText.Format("{0} waiting", waiting)
            : UiText.Get("No waiting tasks");
    }

    private static string FormatDuration(TimeSpan span) =>
        span.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}h {span.Minutes}m")
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (int)Math.Ceiling(span.TotalMinutes))} min");

    private void UpdateFilterCounts(int active, int queued, int paused, int completed, int failed)
    {
        foreach (var filter in Filters)
        {
            filter.Count = filter.Value switch
            {
                QueueFilter.Active => active,
                QueueFilter.Waiting => queued + paused,
                QueueFilter.Completed => completed,
                QueueFilter.Failed => failed,
                _ => active + queued + paused + completed + failed
            };
        }
    }

    private void RebuildRows(IReadOnlyList<Paquete> packages)
    {
        var desired = new List<DownloadItemViewModel>();
        foreach (Paquete package in packages)
        {
            var header = GetOrCreateRow(package, isPackage: true);
            var files = package.SnapshotFicheros();
            UpdatePackageRow(header, package, files);
            header.ChildCount = files.Count;

            var visibleFiles = new List<Fichero>(files.Count);
            foreach (Fichero file in files)
            {
                if (MatchesRow(package, file))
                    visibleFiles.Add(file);
            }

            bool packageMatches = MatchesRow(package, null);
            if (visibleFiles.Count == 0 && !packageMatches)
                continue;

            header.HasChildren = files.Count > 0;
            desired.Add(header);

            if (!header.IsExpanded)
                continue;
            foreach (Fichero file in visibleFiles)
            {
                var child = GetOrCreateRow(file, isPackage: false);
                UpdateFileRow(child, file);
                desired.Add(child);
            }
        }

        Reconcile(desired);
    }

    private void UpdateVisibleRows()
    {
        foreach (var row in Downloads)
        {
            switch (row.Item)
            {
                case Paquete package:
                    UpdatePackageRow(row, package, package.SnapshotFicheros());
                    break;
                case Fichero file:
                    UpdateFileRow(row, file);
                    break;
            }
        }
        if (SelectedDownload is not null)
            UpdateDetails(SelectedDownload);
    }

    private bool MatchesRow(Paquete package, Fichero? file)
    {
        var state = file?.DescargaEstado() ?? package.DescargaEstado();
        if (!MatchesFilter(state))
            return false;
        if (_appliedSearch.Length == 0)
            return true;

        if (file is null)
        {
            if (Contains(package.DescargaNombre(), _appliedSearch))
                return true;
            foreach (Fichero child in package.SnapshotFicheros())
            {
                if (MatchesSearch(child))
                    return true;
            }
            return false;
        }
        return MatchesSearch(file);
    }

    private bool MatchesSearch(Fichero file) =>
        Contains(FileTitle(file), _appliedSearch) ||
        Contains(file.NombreFichero, _appliedSearch) ||
        Contains(file.RutaRelativa, _appliedSearch);

    private static bool Contains(string? value, string needle) =>
        !string.IsNullOrEmpty(value) && value.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    private bool MatchesFilter(Estado state) => SelectedFilter?.Value switch
    {
        QueueFilter.Active => state is Estado.Descargando or Estado.CreandoLocal or Estado.Verificando
            or Estado.ComprobandoMD5 or Estado.Descomprimiendo or Estado.EsperandoVolumenes,
        QueueFilter.Waiting => state is Estado.EnCola or Estado.Pausado,
        QueueFilter.Completed => state == Estado.Completado,
        QueueFilter.Failed => state == Estado.Erroneo,
        _ => true
    };

    private DownloadItemViewModel GetOrCreateRow(object item, bool isPackage)
    {
        if (_rowCache.TryGetValue(item, out var row))
            return row;

        row = new DownloadItemViewModel(this, item, isPackage) { ShowMetrics = ShowMetrics };
        _rowCache[item] = row;
        return row;
    }

    /// <summary>Drops rows whose engine objects left the queue. Rows hidden by the
    /// current filter stay cached so their expansion state survives filter changes.</summary>
    private void PruneRowCache(IReadOnlyList<Paquete> packages)
    {
        if (_rowCache.Count == 0)
            return;

        var liveItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (Paquete package in packages)
        {
            liveItems.Add(package);
            foreach (Fichero file in package.SnapshotFicheros())
                liveItems.Add(file);
        }

        List<object>? stale = null;
        foreach (object key in _rowCache.Keys)
        {
            if (liveItems.Contains(key))
                continue;
            stale ??= new List<object>();
            stale.Add(key);
        }

        if (stale is null)
            return;
        foreach (object key in stale)
            _rowCache.Remove(key);
    }

    private static void UpdatePackageRow(DownloadItemViewModel row, Paquete package, IReadOnlyList<Fichero> files)
    {
        string? title = package.DescargaNombre();
        if (string.IsNullOrWhiteSpace(title))
            title = files.Count == 1 ? FileTitle(files[0]) : Language.GetText("New package");
        row.Title = title;
        row.StateText = StateLabel(package.DescargaEstado());
        row.Progress = (double)Math.Clamp(package.DescargaPorcentaje(), 0m, 100m);
        row.SizeText = FormatBytes(package.DescargaTamanoBytes());
        row.SpeedText = package.DescargaVelocidadKBs() > 0 ? FormatSpeed(package.DescargaVelocidadKBs()) : string.Empty;
        row.DetailText = UiText.Format("{0} files · {1}", files.Count, row.SizeText);
        string[] identities = files.Select(file => file.EffectiveMegaAccountEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        row.HasIdentity = identities.Any(identity => identity.Length > 0);
        row.IdentityText = identities.Length == 1
            ? MegaAccountText.IdentityCaption(identities[0])
            : UiText.Get("Mixed");
        row.CanStart = package.DescargaEstado() is Estado.EnCola or Estado.Pausado or Estado.Erroneo;
        row.CanPause = package.DescargaEstado() is Estado.Descargando or Estado.CreandoLocal;
        row.CanRetry = package.DescargaEstado() == Estado.Erroneo && files.Any(file => !file.EsErrorPermanente);
        ApplyRowState(row, package.DescargaEstado());
    }

    private static void ApplyRowState(DownloadItemViewModel row, Estado state)
    {
        row.IsActive = state is Estado.Descargando or Estado.CreandoLocal or Estado.Verificando
            or Estado.ComprobandoMD5 or Estado.Descomprimiendo or Estado.EsperandoVolumenes;
        row.IsWaiting = state is Estado.EnCola or Estado.Pausado;
        row.IsCompleted = state == Estado.Completado;
        row.IsFailed = state == Estado.Erroneo;
    }

    private static string FileTitle(Fichero file)
    {
        string name = file.DescargaNombre();
        if (!string.IsNullOrWhiteSpace(name) && !name.Contains("://", StringComparison.Ordinal))
            return name;
        return string.IsNullOrWhiteSpace(file.FileID) ? UiText.Get("(unnamed link)") : "MEGA · " + file.FileID;
    }

    private static void UpdateFileRow(DownloadItemViewModel row, Fichero file)
    {
        row.Title = FileTitle(file);
        row.StateText = StateLabel(file.DescargaEstado());
        row.Progress = (double)Math.Clamp(file.DescargaPorcentaje(), 0m, 100m);
        row.SizeText = FormatBytes(file.DescargaTamanoBytes());

        decimal speed = file.DescargaVelocidadKBs();
        row.SpeedText = speed > 0 ? FormatSpeed(speed) : string.Empty;

        string eta = file.DescargaTiempoEstimadoDescarga();
        row.DetailText = string.IsNullOrWhiteSpace(eta)
            ? row.SizeText
            : UiText.Format("{0} · {1} remaining", row.SizeText, eta);
        row.HasIdentity = file.UsesMegaAccount;
        row.IdentityText = file.UsesMegaAccount ? MegaAccountText.IdentityCaption(file.EffectiveMegaAccountEmail) : string.Empty;

        row.CanStart = file.DescargaEstado() is Estado.EnCola or Estado.Pausado or Estado.Erroneo;
        row.CanPause = file.DescargaEstado() is Estado.Descargando or Estado.CreandoLocal;
        row.CanRetry = file.DescargaEstado() == Estado.Erroneo && !file.EsErrorPermanente;
        ApplyRowState(row, file.DescargaEstado());
    }

    /// <summary>
    /// Brings <see cref="Downloads"/> in line with <paramref name="desired"/> by moving,
    /// inserting and removing rows instead of clearing the list, so selection, focus and
    /// the scroll anchor survive.
    /// </summary>
    private void Reconcile(List<DownloadItemViewModel> desired)
    {
        bool sameOrder = Downloads.Count == desired.Count;
        if (sameOrder)
        {
            for (int i = 0; i < desired.Count; i++)
            {
                if (!ReferenceEquals(Downloads[i], desired[i]))
                {
                    sameOrder = false;
                    break;
                }
            }
        }

        if (sameOrder)
            return;

        var selected = SelectedDownload;
        var selectedSet = new HashSet<DownloadItemViewModel>(SelectedDownloads);

        for (int index = 0; index < desired.Count; index++)
        {
            var row = desired[index];
            if (index < Downloads.Count && ReferenceEquals(Downloads[index], row))
                continue;
            int existing = IndexOf(row);
            if (existing >= 0)
                Downloads.Move(existing, index);
            else
                Downloads.Insert(index, row);
        }

        while (Downloads.Count > desired.Count)
            Downloads.RemoveAt(Downloads.Count - 1);

        SelectedDownload = selected is not null && desired.Contains(selected) ? selected : null;

        // Restore the batch selection to the rows that are still visible, then let the
        // view mirror it back into the list so the highlighted rows match the count in
        // the batch bar.
        SelectedDownloads.Clear();
        foreach (var row in desired)
        {
            if (selectedSet.Contains(row))
                SelectedDownloads.Add(row);
        }
        SelectionCount = SelectedDownloads.Count;
        SelectionSyncRequested?.Invoke();
    }

    private int IndexOf(DownloadItemViewModel row)
    {
        for (int index = 0; index < Downloads.Count; index++)
        {
            if (ReferenceEquals(Downloads[index], row))
                return index;
        }
        return -1;
    }

    internal static string StateLabel(Estado state) => state switch
    {
        Estado.Verificando => UiText.Get("Checking"),
        Estado.CreandoLocal => UiText.Get("Preparing"),
        Estado.EnCola => UiText.Get("Queued"),
        Estado.Descargando => UiText.Get("Downloading"),
        Estado.Pausado => UiText.Get("Paused"),
        Estado.Completado => UiText.Get("Completed"),
        Estado.ComprobandoMD5 => UiText.Get("Verifying"),
        Estado.Descomprimiendo => UiText.Get("Extracting"),
        Estado.Erroneo => UiText.Get("Error"),
        Estado.EsperandoVolumenes => UiText.Get("Waiting for volumes"),
        _ => state.ToString()
    };

    internal static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {units[unit]}");
    }

    internal static string FormatSpeed(decimal kilobytesPerSecond)
    {
        if (kilobytesPerSecond >= 1024)
            return string.Create(CultureInfo.InvariantCulture, $"{kilobytesPerSecond / 1024:0.#} MB/s");

        return string.Create(CultureInfo.InvariantCulture, $"{kilobytesPerSecond:0.#} KB/s");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _refreshTimer.Stop();
        _searchTimer.Stop();
        UiText.Changed -= ApplyLanguage;
    }
}

/// <summary>One flat, virtualized row: a package header or one of its files.</summary>
public sealed class IdentityPickResult
{
    public IdentityPickResult(string email, bool confirmed)
    {
        Email = email;
        Confirmed = confirmed;
    }

    public string Email { get; }
    public bool Confirmed { get; }
}

public partial class DownloadItemViewModel : ObservableObject
{
    private readonly DownloadsPageViewModel _owner;

    public DownloadItemViewModel(DownloadsPageViewModel owner, object item, bool isPackage)
    {
        _owner = owner;
        Item = item;
        IsPackage = isPackage;
        RowMargin = isPackage ? new Thickness(0) : new Thickness(16, 0, 0, 0);
    }

    public object Item { get; }

    public bool IsPackage { get; }

    public Thickness RowMargin { get; }

    public FontWeight TitleWeight => IsPackage ? FontWeight.SemiBold : FontWeight.Normal;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string stateText = string.Empty;

    [ObservableProperty]
    private string detailText = string.Empty;

    [ObservableProperty]
    private string identityText = string.Empty;

    [ObservableProperty]
    private bool hasIdentity;

    [ObservableProperty]
    private string sizeText = string.Empty;

    [ObservableProperty]
    private string speedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private double progress;

    public string ProgressText => Progress <= 0 && !IsCompleted
        ? "—"
        : (Progress / 100).ToString("0.0%", UiText.Culture);

    internal void ApplyLanguage() => OnPropertyChanged(nameof(ProgressText));

    [ObservableProperty]
    private bool canStart;

    [ObservableProperty]
    private bool canPause;

    [ObservableProperty]
    private bool canRetry;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isWaiting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private bool isCompleted;

    [ObservableProperty]
    private bool isFailed;

    [ObservableProperty]
    private bool showMetrics = true;

    [ObservableProperty]
    private bool hasChildren;

    [ObservableProperty]
    private int childCount;

    [ObservableProperty]
    private bool isExpanded = true;

    public string Chevron => IsExpanded ? "⌄" : "›";

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Chevron));

    [RelayCommand]
    private void Start() => _owner.StartItem(Item);

    [RelayCommand]
    private void Pause() => _owner.PauseItem(Item);

    [RelayCommand]
    private void Retry() => _owner.RetryItem(Item);

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        _owner.Refresh(forceRebuild: true);
    }

    [RelayCommand]
    private void MoveToTop() => _owner.MoveItem(Item, QueueMove.Top);

    [RelayCommand]
    private void MoveUp() => _owner.MoveItem(Item, QueueMove.Up);

    [RelayCommand]
    private void MoveDown() => _owner.MoveItem(Item, QueueMove.Down);

    [RelayCommand]
    private Task ChangeIdentityAsync() => _owner.ChangeIdentityAsync(this);

    [RelayCommand]
    private void OpenFile() => _owner.OpenOutput(Item, openFolder: false);

    [RelayCommand]
    private void OpenFolder() => _owner.OpenOutput(Item, openFolder: true);

    [RelayCommand]
    private Task PropertiesAsync() => _owner.ShowPropertiesAsync(Item);

    [RelayCommand]
    private Task RemoveAsync() => _owner.RemoveItemAsync(Item, deleteFiles: false);

    [RelayCommand]
    private Task DeleteAsync() => _owner.RemoveItemAsync(Item, deleteFiles: true);
}

public enum QueueFilter { All, Active, Waiting, Completed, Failed }

public sealed partial class QueueFilterOption(QueueFilter value, string label) : ObservableObject
{
    public QueueFilter Value { get; } = value;
    public string Label => UiText.Get(label);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    private int count;

    public string CountText => Count > 0 ? Count.ToString(UiText.Culture) : string.Empty;

    internal void ApplyLanguage()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(CountText));
    }
}
