using MegaDownloader.UI.Services;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// Link intake for the Avalonia shell. Metadata inspection and package construction
/// reuse the Core engine (<see cref="PreflightSession"/>, <see cref="DownloadPackageBuilder"/>);
/// the page only owns selection state and presentation.
/// </summary>
public partial class AddLinksViewModel : ViewModelBase, IDisposable
{
    private readonly DownloadEngineService _engine;
    private readonly Dictionary<BatchLinkPreflight.Entry, PreflightRowViewModel> _rowsByEntry =
        new(ReferenceEqualityComparer.Instance);
    private PreflightSelection? _selection;
    private PreflightSession? _session;
    private DispatcherTimer? _progressTimer;
    private CancellationTokenSource? _importCancellation;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    private string linksText = string.Empty;

    [ObservableProperty]
    private string destination = string.Empty;

    [ObservableProperty]
    private string packageName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool createSubdirectory;

    [ObservableProperty]
    private bool extractArchives;

    [ObservableProperty]
    private bool startDownload = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private bool isInspecting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private bool isImporting;

    [ObservableProperty]
    private bool hasEntries;

    [ObservableProperty]
    private string status = UiText.Get("Paste links, choose a folder, then check them.");

    [ObservableProperty]
    private string notice = string.Empty;

    [ObservableProperty]
    private bool noticeIsError;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private bool isProgressIndeterminate;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string selectionSummary = UiText.Get("No links selected");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private int selectedCount;

    public AddLinksViewModel(DownloadEngineService engine)
    {
        _engine = engine;

        var config = engine.Config;
        bool reuseLast = config.MantenerUltimaConfiguracion && UltimaConfiguracionUsada.ExisteUltimaConfiguracion;
        Destination = reuseLast && !string.IsNullOrWhiteSpace(UltimaConfiguracionUsada.RutaDescarga)
            ? UltimaConfiguracionUsada.RutaDescarga
            : LinkIntakeService.DownloadFolder(config);
        CreateSubdirectory = reuseLast ? UltimaConfiguracionUsada.CrearDirectorioPaquete : config.CrearDirectorioPaquete;
        ExtractArchives = reuseLast ? UltimaConfiguracionUsada.ExtraerAutomaticamente : config.ExtraerAutomaticamente;
        StartDownload = reuseLast ? UltimaConfiguracionUsada.IniciarDescarga : true;
    }

    public ObservableCollection<PreflightRowViewModel> Entries { get; } = new();

    public ObservableCollection<PreflightRowViewModel> VisibleEntries { get; } = new();

    /// <summary>Set by the window so Browse can use the platform folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>Raised with true when the package was added, false when the user cancels.</summary>
    public event Action<bool>? CloseRequested;

    internal void ClipboardNotice(string message) => SetNotice(UiText.Get(message), true);

    public bool CanCheck => !IsInspecting && !IsImporting && !string.IsNullOrWhiteSpace(LinksText);

    public bool CanAdd => SelectedCount > 0 && !IsImporting && !IsInspecting;

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    public string AddButtonText => SelectedCount > 0 ? UiText.Format("Add {0} selected", SelectedCount) : UiText.Get("Add selected");

    partial void OnLinksTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanCheck));
        if (IsInspecting || !HasEntries)
            return;

        // The visible results belong to the previous text; never let Add enqueue them.
        HasEntries = false;
        Entries.Clear();
        VisibleEntries.Clear();
        _rowsByEntry.Clear();
        _selection = null;
        SelectedCount = 0;
        SelectionSummary = UiText.Get("No links selected");
        Status = UiText.Get("Links changed — check them again.");
    }

    partial void OnIsInspectingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanAdd));
    }

    partial void OnIsImportingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanAdd));
    }

    partial void OnSelectedCountChanged(int value) => OnPropertyChanged(nameof(AddButtonText));

    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        SetNotice(string.Empty);
        if (string.IsNullOrWhiteSpace(LinksText))
        {
            SetNotice(UiText.Get("Paste at least one link."), true);
            return;
        }
        if (string.IsNullOrWhiteSpace(Destination))
        {
            SetNotice(UiText.Get("Choose a download folder."), true);
            return;
        }

        IsInspecting = true;
        HasEntries = false;
        Entries.Clear();
        VisibleEntries.Clear();
        _rowsByEntry.Clear();
        _selection = null;
        UpdateSelectionSummary();

        _session?.Dispose();
        _session = new PreflightSession(_engine.Config);
        string requestedText = LinksText;
        var request = _session.Prepare(requestedText);
        StartProgressTimer(request);

        try
        {
            var entries = await request.Results;
            if (_disposed)
                return;
            if (!string.Equals(LinksText, requestedText, StringComparison.Ordinal))
            {
                Status = UiText.Get("Links changed — check them again.");
                return;
            }

            var reconciled = PreflightSession.Reconcile(entries, _engine.GetQueuedLinkSnapshot());
            _selection = new PreflightSelection(reconciled);
            foreach (var entry in reconciled)
            {
                var row = new PreflightRowViewModel(this, entry);
                Entries.Add(row);
                _rowsByEntry[entry] = row;
            }

            ApplyFilter();
            HasEntries = Entries.Count > 0;
            UpdateSelectionSummary();
            Status = reconciled.Count == 0
                ? UiText.Get("No valid MEGA links were found.")
                : UiText.Format("Checked {0} link(s).", reconciled.Count);
            ProgressValue = 100;
        }
        catch (OperationCanceledException)
        {
            Status = UiText.Get("Check cancelled.");
        }
        catch (Exception ex)
        {
            Status = UiText.Get("Check failed.");
            SetNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
        }
        finally
        {
            StopProgressTimer();
            IsInspecting = false;
            IsProgressIndeterminate = false;
        }
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
    private void SelectAll() => ApplySelection(PreflightSelection.SelectionOperation.SelectAll);

    [RelayCommand]
    private void ClearSelection() => ApplySelection(PreflightSelection.SelectionOperation.Clear);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        if (_selection is null || IsImporting)
            return;
        if (!Path.IsPathFullyQualified(Destination.Trim()))
        {
            SetNotice(UiText.Get("Choose a download folder."), true);
            return;
        }
        if (!_engine.IsLoaded)
        {
            SetNotice(UiText.Get("The download queue is still loading. Try again in a moment."), true);
            return;
        }

        var chosen = new List<URLProcessor.FileURL>();
        foreach (var row in Entries)
        {
            if (row.Entry.Selected && row.Entry.CanAdd && row.Entry.Source is not null)
                chosen.Add(row.Entry.Source);
        }
        if (chosen.Count == 0)
        {
            SetNotice(UiText.Get("Select at least one link to add."), true);
            return;
        }

        IsImporting = true;
        SetNotice(string.Empty);
        _importCancellation = new CancellationTokenSource();
        var token = _importCancellation.Token;
        try
        {
            Status = UiText.Get("Checking the queue for duplicates…");
            var latest = await Task.Run(() => _engine.GetQueuedLinkSnapshot(), token);
            var filtered = await Task.Run(() =>
            {
                var known = new HashSet<string>(latest.Keys, StringComparer.Ordinal);
                var result = new List<URLProcessor.FileURL>();
                foreach (var file in chosen)
                {
                    token.ThrowIfCancellationRequested();
                    string? key = BatchLinkPreflight.Identity(file);
                    if (key is null || known.Add(key))
                        result.Add(file);
                }
                return result;
            }, token);

            if (filtered.Count == 0)
            {
                SetNotice(UiText.Get("All selected links are already queued."), true);
                return;
            }

            Status = UiText.Format("Building package for {0} file(s)…", filtered.Count);
            var package = await Task.Run(
                () => DownloadPackageBuilder.Build(
                    filtered,
                    PackageName.Trim(),
                    Destination.Trim(),
                    CreateSubdirectory,
                    ExtractArchives,
                    Password,
                    token),
                token);

            if (!StartDownload)
            {
                foreach (Fichero file in package.SnapshotFicheros())
                    file.PausaIndividual = true;
            }
            int added = token.IsCancellationRequested ? -1 : _engine.AddUniquePackage(package);
            if (added <= 0)
            {
                if (package.ListaFicheros is not null)
                {
                    foreach (var file in package.ListaFicheros)
                        file.Dispose();
                }
                token.ThrowIfCancellationRequested();
                SetNotice(UiText.Get(added == 0 ? "All selected links are already queued." : "The queue is not ready yet. Try again in a moment."), true);
                return;
            }

            UltimaConfiguracionUsada.ExisteUltimaConfiguracion = true;
            UltimaConfiguracionUsada.CrearDirectorioPaquete = CreateSubdirectory;
            UltimaConfiguracionUsada.ExtraerAutomaticamente = ExtractArchives;
            UltimaConfiguracionUsada.RutaDescarga = Destination;
            UltimaConfiguracionUsada.IniciarDescarga = StartDownload;

            if (StartDownload && !_engine.TryStartPending(out string reason))
            {
                // The tasks are queued but the queue refused to start (schedule window
                // or quota cooldown). Say so instead of claiming they are running.
                Status = UiText.Format("Added {0} file(s) to the queue.", added) + " " + reason;
                CloseRequested?.Invoke(true);
                return;
            }

            Status = UiText.Format("Added {0} file(s) to the queue.", added);
            CloseRequested?.Invoke(true);
        }
        catch (OperationCanceledException)
        {
            Status = UiText.Get("Import cancelled.");
        }
        catch (Exception ex)
        {
            Status = UiText.Get("Could not add the links.");
            SetNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
        }
        finally
        {
            IsImporting = false;
            _importCancellation?.Dispose();
            _importCancellation = null;
        }
    }

    internal void SetRowSelected(PreflightRowViewModel row, bool selected)
    {
        if (_selection is null)
            return;
        _selection.SetSelected(row.Entry, selected);
        if (row.Entry.Selected != selected)
            row.SetSelectedSilently(row.Entry.Selected);
        UpdateSelectionSummary();
    }

    private void ApplySelection(PreflightSelection.SelectionOperation operation)
    {
        if (_selection is null)
            return;
        _selection.Apply(operation, visibleOnly: true);
        foreach (var row in Entries)
            row.SetSelectedSilently(row.Entry.Selected);
        UpdateSelectionSummary();
    }

    private void ApplyFilter()
    {
        if (_selection is null)
            return;
        _selection.Filter(FilterText ?? string.Empty, string.Empty);
        VisibleEntries.Clear();
        foreach (var entry in _selection.Visible)
        {
            if (_rowsByEntry.TryGetValue(entry, out var row))
                VisibleEntries.Add(row);
        }
    }

    private void UpdateSelectionSummary()
    {
        if (_selection is null)
        {
            SelectedCount = 0;
            SelectionSummary = UiText.Get("No links selected");
            return;
        }

        SelectedCount = _selection.SelectedCount;
        string bytes = DownloadsPageViewModel.FormatBytes((long)_selection.SelectedBytes);
        string unknown = _selection.UnknownSizes > 0
            ? UiText.Format(" + {0} unknown", _selection.UnknownSizes)
            : string.Empty;
        SelectionSummary = UiText.Format("{0} selected · {1}{2}", _selection.SelectedCount, bytes, unknown);
    }

    private void StartProgressTimer(PreflightSession.Request request)
    {
        StopProgressTimer();
        IsProgressIndeterminate = true;
        ProgressValue = 0;
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _progressTimer.Tick += (_, _) => UpdateProgress(request.Progress);
        _progressTimer.Start();
        UpdateProgress(request.Progress);
    }

    private void StopProgressTimer()
    {
        if (_progressTimer is null)
            return;
        _progressTimer.Stop();
        _progressTimer = null;
    }

    private void UpdateProgress(PreflightProgress progress)
    {
        switch (progress.Stage)
        {
            case PreflightProgress.Phase.Parsing:
                Status = UiText.Get("Reading links…");
                IsProgressIndeterminate = true;
                break;
            case PreflightProgress.Phase.Waiting:
                Status = UiText.Format("Resolving {0} link(s)…", progress.LinksTotal);
                IsProgressIndeterminate = true;
                break;
            case PreflightProgress.Phase.Reading:
                IsProgressIndeterminate = progress.LinksTotal == 0;
                ProgressValue = progress.LinksTotal > 0
                    ? Math.Min(100d, 100d * progress.LinksCompleted / progress.LinksTotal)
                    : 0d;
                Status = progress.LinksTotal > 0
                    ? UiText.Format("Reading link {0} of {1}…", Math.Min(progress.LinksTotal, progress.LinksCompleted + 1), progress.LinksTotal)
                    : UiText.Get("Reading link contents…");
                break;
            case PreflightProgress.Phase.Checking:
                IsProgressIndeterminate = progress.ItemsTotal == 0;
                ProgressValue = progress.ItemsTotal > 0
                    ? Math.Min(100d, 100d * progress.ItemsProcessed / progress.ItemsTotal)
                    : 0d;
                Status = progress.ItemsTotal > 0
                    ? UiText.Format("Checking file {0} of {1}…", progress.ItemsProcessed, progress.ItemsTotal)
                    : UiText.Get("Checking files…");
                break;
            case PreflightProgress.Phase.Completed:
                IsProgressIndeterminate = false;
                ProgressValue = 100;
                Status = UiText.Format("Checked {0} file(s).", progress.FilesFound);
                break;
            case PreflightProgress.Phase.Cancelled:
                IsProgressIndeterminate = false;
                Status = UiText.Get("Check cancelled.");
                break;
            case PreflightProgress.Phase.Failed:
                IsProgressIndeterminate = false;
                Status = UiText.Get("Check failed.");
                break;
        }
    }

    private void SetNotice(string text, bool isError = false)
    {
        Notice = text;
        NoticeIsError = isError;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopProgressTimer();
        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _importCancellation = null;
        _session?.Dispose();
        _session = null;
    }
}

/// <summary>One preflight row; selection is delegated to <see cref="PreflightSelection"/>.</summary>
public partial class PreflightRowViewModel : ObservableObject
{
    private readonly AddLinksViewModel _owner;
    private bool _suppress;

    public PreflightRowViewModel(AddLinksViewModel owner, BatchLinkPreflight.Entry entry)
    {
        _owner = owner;
        Entry = entry;
        isSelected = entry.Selected;
    }

    public BatchLinkPreflight.Entry Entry { get; }

    public string Name => string.IsNullOrWhiteSpace(Entry.Name) ? UiText.Get("(unnamed link)") : Entry.Name;

    public string SizeText => Entry.SizeBytes.HasValue
        ? DownloadsPageViewModel.FormatBytes(Entry.SizeBytes.Value)
        : "—";

    public string StateText => Entry.State switch
    {
        BatchLinkPreflight.LinkState.Ready => UiText.Get("Ready"),
        BatchLinkPreflight.LinkState.AlreadyQueued => UiText.Get("Already queued"),
        BatchLinkPreflight.LinkState.Duplicate => UiText.Get("Duplicate"),
        BatchLinkPreflight.LinkState.Invalid => UiText.Get("Invalid"),
        _ => UiText.Get("Unavailable")
    };

    public bool CanSelect => Entry.CanAdd;

    public bool IsReady => Entry.State == BatchLinkPreflight.LinkState.Ready;

    public bool IsErrorState => Entry.State is BatchLinkPreflight.LinkState.Invalid
        or BatchLinkPreflight.LinkState.Unavailable;

    public bool HasDetail => !string.IsNullOrWhiteSpace(Entry.Detail);

    public string Detail => Entry.Detail ?? string.Empty;

    [ObservableProperty]
    private bool isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (_suppress)
            return;
        _owner.SetRowSelected(this, value);
    }

    internal void SetSelectedSilently(bool value)
    {
        if (IsSelected == value)
            return;
        _suppress = true;
        IsSelected = value;
        _suppress = false;
    }
}
