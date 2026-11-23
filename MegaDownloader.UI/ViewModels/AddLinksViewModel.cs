using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// Two-step link intake for the Avalonia shell. Step one collects links; step two shows
/// the folder tree and the destination before anything is queued. Metadata inspection
/// and package construction reuse the Core engine (<see cref="PreflightSession"/>,
/// <see cref="PreflightTree"/>, <see cref="DownloadPackageBuilder"/>); this class only
/// owns the flow and the selection state.
/// </summary>
public partial class AddLinksViewModel : ViewModelBase, IDisposable
{
    private readonly DownloadEngineService _engine;
    private readonly Dictionary<BatchLinkPreflight.Entry, PreflightRowViewModel> _rowsByEntry =
        new(ReferenceEqualityComparer.Instance);
    private PreflightSelection? _selection;
    private PreflightTree? _tree;
    private PreflightSession? _session;
    private DispatcherTimer? _progressTimer;
    private CancellationTokenSource? _importCancellation;
    private string? _preparedText;
    private bool _disposed;

    [ObservableProperty]
    private int step;

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
    private string categoryFilter = string.Empty;

    [ObservableProperty]
    private string selectionSummary = UiText.Get("No links selected");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private int selectedCount;

    [ObservableProperty]
    private string destinationPreview = string.Empty;

    [ObservableProperty]
    private string spaceText = string.Empty;

    [ObservableProperty]
    private bool spaceInsufficient;

    [ObservableProperty]
    private string pendingBatch = string.Empty;

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

    /// <summary>The rows currently shown: folders and files in display order.</summary>
    public ObservableCollection<PreflightRowViewModel> Rows { get; } = new();

    public ObservableCollection<string> Categories { get; } = new();

    /// <summary>Set by the window so Browse can use the platform folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>Raised with true when a package was added, false when the user cancels.</summary>
    public event Action<bool>? CloseRequested;

    public bool IsStepOne => Step == 0;

    public bool IsStepTwo => Step == 1;

    public bool CanCheck => !IsInspecting && !IsImporting && !string.IsNullOrWhiteSpace(LinksText);

    public bool CanAdd => SelectedCount > 0 && !IsImporting && !IsInspecting && !SpaceInsufficient;

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    public bool HasPendingBatch => !string.IsNullOrWhiteSpace(PendingBatch);

    public bool CanGoBack => !IsImporting && !IsInspecting;

    public string AddButtonText => SelectedCount > 0 ? UiText.Format("Add {0} selected", SelectedCount) : UiText.Get("Add selected");

    internal void ClipboardNotice(string message) => SetNotice(UiText.Get(message), true);

    /// <summary>Links held for the next batch while this window was open.</summary>
    internal void SetPendingBatch(int count) =>
        PendingBatch = count > 0 ? UiText.Format("{0} new link(s) will open in a new batch after this one.", count) : string.Empty;

    /// <summary>Starts on the given text, keeping the previous results when it is the
    /// same batch (going back and forth must not re-resolve or lose the selection).</summary>
    internal async Task BeginAsync(string? initialText)
    {
        if (!string.IsNullOrWhiteSpace(initialText))
            LinksText = initialText;

        if (_tree is not null && string.Equals(_preparedText, LinksText, StringComparison.Ordinal))
        {
            Step = 1;
            return;
        }

        if (!string.IsNullOrWhiteSpace(LinksText) && CheckCommand.CanExecute(null))
            await CheckAsync();
    }

    partial void OnStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStepOne));
        OnPropertyChanged(nameof(IsStepTwo));
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnLinksTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanCheck));
        if (IsInspecting || _tree is null)
            return;

        // The results belong to the previous text; never let Add enqueue them.
        if (string.Equals(value, _preparedText, StringComparison.Ordinal))
            return;
        _session?.Invalidate();
        _tree = null;
        _selection = null;
        Rows.Clear();
        _rowsByEntry.Clear();
        Categories.Clear();
        HasEntries = false;
        SelectedCount = 0;
        UpdateSelectionSummary();
        if (Step == 1)
            Step = 0;
        Status = UiText.Get("Links changed — check them again.");
    }

    partial void OnIsInspectingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnIsImportingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnSelectedCountChanged(int value) => OnPropertyChanged(nameof(AddButtonText));

    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));

    partial void OnPendingBatchChanged(string value) => OnPropertyChanged(nameof(HasPendingBatch));

    partial void OnSpaceInsufficientChanged(bool value) => OnPropertyChanged(nameof(CanAdd));

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnCategoryFilterChanged(string value) => ApplyFilter();

    partial void OnDestinationChanged(string value) => UpdateDestinationPreview();

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
        Rows.Clear();
        _rowsByEntry.Clear();
        Categories.Clear();
        _tree = null;
        _selection = null;
        SelectedCount = 0;
        UpdateSelectionSummary();

        _session ??= new PreflightSession(_engine.Config);
        string requestedText = LinksText;
        var request = _session.Prepare(requestedText);
        _preparedText = requestedText;
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

            _tree = new PreflightTree(_selection);
            _rowsByEntry.Clear();
            foreach (var entry in reconciled)
            {
                var leaf = _tree.LeafNode(entry);
                _rowsByEntry[entry] = leaf is null
                    ? new PreflightRowViewModel(this, entry)
                    : new PreflightRowViewModel(this, entry, leaf);
            }
            BuildCategoryList();
            ApplyFilter();
            UpdateDestinationPreview();

            HasEntries = Rows.Count > 0;
            UpdateSelectionSummary();
            Status = reconciled.Count == 0
                ? UiText.Get("No valid MEGA links were found.")
                : UiText.Format("Checked {0} link(s).", reconciled.Count);

            // Nothing usable: stay on step one so the user can fix the links instead of
            // landing on an empty file tree.
            if (HasEntries)
                Step = 1;
            else
                SetNotice(Status, true);
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
    private void Back()
    {
        if (!CanGoBack)
            return;
        Step = 0;
    }

    [RelayCommand]
    private async Task RecheckAsync()
    {
        _session?.Invalidate(clearCache: true);
        _tree = null;
        _selection = null;
        _preparedText = null;
        await CheckAsync();
    }

    [RelayCommand]
    private void SelectAll() => ApplyToTree(PreflightSelection.SelectionOperation.SelectAll);

    [RelayCommand]
    private void ClearSelection() => ApplyToTree(PreflightSelection.SelectionOperation.Clear);

    [RelayCommand]
    private void ExpandAll() => ToggleAll(expand: true);

    [RelayCommand]
    private void CollapseAll() => ToggleAll(expand: false);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    private void ToggleAll(bool expand)
    {
        _tree?.ExpandAll(expand);
        RebuildRows();
    }

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

        // Snapshot the selection at submit time so a later tree change cannot alter it.
        var chosen = new List<URLProcessor.FileURL>();
        foreach (var entry in _selection.Entries)
        {
            if (entry.Selected && entry.CanAdd && entry.Source is not null)
                chosen.Add(entry.Source);
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

            string summary = UiText.Format("Added {0} file(s) to the queue.", added);
            if (chosen.Count != added)
                summary += " " + UiText.Format("{0} already queued or skipped.", chosen.Count - added);

            if (StartDownload && !_engine.TryStartPending(out string reason))
            {
                // Queued, but the schedule or the quota kept the queue from starting.
                Status = summary + " " + reason;
                CloseRequested?.Invoke(true);
                return;
            }

            Status = summary;
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

    /// <summary>A leaf checkbox changed. Folder tri-state is recomputed by the tree.</summary>
    internal void SetEntrySelected(BatchLinkPreflight.Entry entry, bool selected)
    {
        if (_selection is null)
            return;
        _selection.SetSelected(entry, selected);
        if (entry.Selected != selected)
            _rowsByEntry[entry].SetSelectedSilently(entry.Selected);
        RefreshRowStates();
        UpdateSelectionSummary();
    }

    /// <summary>A folder row was clicked: select or clear everything underneath it.</summary>
    internal void ToggleFolderSelection(PreflightTree.Node node)
    {
        if (_selection is null || _tree is null || !node.IsFolder)
            return;
        var operation = node.CheckState == PreflightTree.TriState.Checked
            ? PreflightSelection.SelectionOperation.Clear
            : PreflightSelection.SelectionOperation.SelectAll;
        _tree.Apply(_tree.NodesUnder(node), operation);
        RefreshRowStates();
        UpdateSelectionSummary();
    }

    internal void ToggleFolder(PreflightTree.Node node)
    {
        _tree?.Toggle(node);
        RebuildRows();
    }

    private void ApplyToTree(PreflightSelection.SelectionOperation operation)
    {
        if (_selection is null || _tree is null)
            return;
        _selection.Apply(operation, visibleOnly: true);
        RebuildRows();
        UpdateSelectionSummary();
    }

    private void BuildCategoryList()
    {
        Categories.Clear();
        if (_selection is null)
            return;
        foreach (var category in _selection.Categories())
            Categories.Add(category);
    }

    private void ApplyFilter()
    {
        if (_selection is null || _tree is null)
            return;
        _selection.Filter(FilterText ?? string.Empty, CategoryFilter ?? string.Empty);
        _tree.Filter(!string.IsNullOrWhiteSpace(FilterText) || !string.IsNullOrWhiteSpace(CategoryFilter));
        RebuildRows();
    }

    /// <summary>
    /// Refreshes check states and folder counts without touching the row collection.
    /// A selection change never changes which rows are visible, so rebuilding the list
    /// would only throw away the user's scroll position.
    /// </summary>
    private void RefreshRowStates()
    {
        foreach (var row in Rows)
            row.SyncFromEntry();
    }

    private void RebuildRows()
    {
        if (_tree is null)
            return;
        Rows.Clear();
        foreach (var node in _tree.Rows)
        {
            if (node.IsFolder)
            {
                var folderRow = new PreflightRowViewModel(this, node);
                folderRow.SyncFromEntry();
                Rows.Add(folderRow);
            }
            else if (_rowsByEntry.TryGetValue(node.Entry, out var row))
            {
                row.SyncFromEntry();
                Rows.Add(row);
            }
        }
    }

    private void UpdateDestinationPreview()
    {
        if (_selection is null)
        {
            DestinationPreview = string.Empty;
            SpaceText = string.Empty;
            SpaceInsufficient = false;
            return;
        }

        var chosen = _selection.Entries.Where(entry => entry.Selected && entry.CanAdd && entry.Source is not null)
            .Select(entry => entry.Source).ToList();
        if (chosen.Count == 0)
        {
            DestinationPreview = UiText.Get("Select files to see where they will be saved.");
            SpaceText = string.Empty;
            SpaceInsufficient = false;
            return;
        }

        var preview = DownloadPackageBuilder.PreviewDestination(chosen, PackageName.Trim(), Destination.Trim(), CreateSubdirectory);
        DestinationPreview = preview.PackageRoot is null
            ? UiText.Format("Files go into {0} (the folder is named after the file once verified).", Destination.Trim())
            : UiText.Format("Files go into {0}", preview.PackageRoot);

        long? available = BatchLinkPreflight.AvailableSpace(Destination.Trim());
        if (available.HasValue && _selection.SelectedBytes > 0)
        {
            SpaceText = UiText.Format("{0} selected · {1} free on the target drive",
                DownloadsPageViewModel.FormatBytes((long)_selection.SelectedBytes),
                DownloadsPageViewModel.FormatBytes(available.Value));
            SpaceInsufficient = _selection.SelectedBytes > available.Value;
        }
        else
        {
            SpaceText = string.Empty;
            SpaceInsufficient = false;
        }
    }

    private void UpdateSelectionSummary()
    {
        if (_selection is null)
        {
            SelectedCount = 0;
            SelectionSummary = UiText.Get("No links selected");
            UpdateDestinationPreview();
            return;
        }

        SelectedCount = _selection.SelectedCount;
        string bytes = DownloadsPageViewModel.FormatBytes((long)_selection.SelectedBytes);
        string unknown = _selection.UnknownSizes > 0
            ? UiText.Format(" + {0} unknown", _selection.UnknownSizes)
            : string.Empty;
        SelectionSummary = UiText.Format("{0} selected · {1}{2}", _selection.SelectedCount, bytes, unknown);
        UpdateDestinationPreview();
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

/// <summary>One preflight row: a folder node or a file leaf.</summary>
public partial class PreflightRowViewModel : ObservableObject
{
    private readonly AddLinksViewModel _owner;
    private readonly PreflightTree.Node? _node;
    private bool _suppress;

    /// <summary>File row.</summary>
    public PreflightRowViewModel(AddLinksViewModel owner, BatchLinkPreflight.Entry entry)
    {
        _owner = owner;
        Entry = entry;
        isSelected = entry.Selected;
    }

    /// <summary>File row that already knows its tree leaf (for the indent).</summary>
    public PreflightRowViewModel(AddLinksViewModel owner, BatchLinkPreflight.Entry entry, PreflightTree.Node leaf)
        : this(owner, entry)
    {
        _node = leaf;
    }

    /// <summary>Folder row.</summary>
    public PreflightRowViewModel(AddLinksViewModel owner, PreflightTree.Node node)
    {
        _owner = owner;
        _node = node;
        IsFolder = true;
        isExpanded = node.Expanded;
    }

    public BatchLinkPreflight.Entry? Entry { get; }

    public bool IsFolder { get; }

    public bool IsFile => !IsFolder;

    public string Name => IsFolder ? _node!.Name : (string.IsNullOrWhiteSpace(Entry!.Name) ? UiText.Get("(unnamed link)") : Entry.Name);

    /// <summary>Left inset that shows the folder hierarchy (DIPs, not spaces).</summary>
    public double Indent => Math.Max(0, IsFolder ? _node!.Depth : (_node!.Parent?.Depth ?? -1) + 1) * 18d;

    public string SizeText => IsFolder
        ? (_node!.SizeBytes > 0 ? DownloadsPageViewModel.FormatBytes((long)_node.SizeBytes) : "—")
        : Entry!.SizeBytes.HasValue ? DownloadsPageViewModel.FormatBytes(Entry.SizeBytes.Value) : "—";

    public string StateText => IsFolder
        ? UiText.Format("{0} of {1}", _node!.SelectedCount, _node.MatchCount)
        : Entry!.State switch
        {
            BatchLinkPreflight.LinkState.Ready => UiText.Get("Ready"),
            BatchLinkPreflight.LinkState.AlreadyQueued => UiText.Get("Already queued"),
            BatchLinkPreflight.LinkState.Duplicate => UiText.Get("Duplicate"),
            BatchLinkPreflight.LinkState.Invalid => UiText.Get("Invalid"),
            _ => UiText.Get("Unavailable")
        };

    public bool CanSelect => IsFolder || Entry!.CanAdd;

    public bool IsReady => !IsFolder && Entry!.State == BatchLinkPreflight.LinkState.Ready;

    public bool IsErrorState => !IsFolder && Entry!.State is BatchLinkPreflight.LinkState.Invalid
        or BatchLinkPreflight.LinkState.Unavailable;

    public bool HasDetail => !IsFolder && !string.IsNullOrWhiteSpace(Entry!.Detail);

    public string Detail => IsFolder ? string.Empty : (Entry!.Detail ?? string.Empty);

    public string PathText => IsFolder || Entry?.Source is null
        ? string.Empty
        : (Entry.Source.Path ?? string.Empty).Replace('\\', '/');

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool? isChecked;

    [ObservableProperty]
    private bool isExpanded;

    public string Chevron => IsFolder ? (IsExpanded ? "⌄" : "›") : string.Empty;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Chevron));

    partial void OnIsCheckedChanged(bool? value)
    {
        if (_suppress || !IsFolder || _node is null)
            return;
        // Folder checkbox: checked selects everything below, anything else clears it.
        if (value == true)
            _owner.ToggleFolderSelection(_node);
        else if (value == false && _node.CheckState != PreflightTree.TriState.Unchecked)
            _owner.ToggleFolderSelection(_node);
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_suppress || IsFolder || Entry is null)
            return;
        _owner.SetEntrySelected(Entry, value);
    }

    /// <summary>Refreshes the displayed values after the selection or tree changed.</summary>
    internal void SyncFromEntry()
    {
        _suppress = true;
        try
        {
            if (IsFolder && _node is not null)
            {
                IsChecked = _node.CheckState switch
                {
                    PreflightTree.TriState.Checked => true,
                    PreflightTree.TriState.Indeterminate => null,
                    _ => false
                };
                IsExpanded = _node.Expanded;
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(Detail));
                OnPropertyChanged(nameof(HasDetail));
            }
            else if (Entry is not null)
            {
                IsSelected = Entry.Selected;
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    internal void SetSelectedSilently(bool value)
    {
        if (IsSelected == value)
            return;
        _suppress = true;
        IsSelected = value;
        _suppress = false;
    }

    [RelayCommand]
    private void Toggle()
    {
        if (IsFolder && _node is not null)
            _owner.ToggleFolder(_node);
    }

    [RelayCommand]
    private void ToggleSelection()
    {
        if (IsFolder && _node is not null)
            _owner.ToggleFolderSelection(_node);
        else if (Entry is not null)
            _owner.SetEntrySelected(Entry, !Entry.Selected);
    }
}
