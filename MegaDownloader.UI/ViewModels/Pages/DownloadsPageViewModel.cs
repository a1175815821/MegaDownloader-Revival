using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// Queue page backed by <see cref="DownloadEngineService"/>. The engine does not raise
/// change notifications, so the page polls it on a dispatcher timer and reconciles a
/// stable row set (rows are reused per engine object so selection survives refreshes).
/// </summary>
public partial class DownloadsPageViewModel : ViewModelBase, IDisposable
{
    private readonly DownloadEngineService _engine;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<object, DownloadItemViewModel> _rowCache = new(ReferenceEqualityComparer.Instance);
    private bool _loading;
    private bool _disposed;
    private bool _loadFailed;
    private string? _actionFeedback;
    private DateTime _feedbackUntil;
    private readonly Queue<double> _speedSamples = new(60);

    [ObservableProperty]
    private IReadOnlyList<double> speedHistory = Array.Empty<double>();

    [ObservableProperty]
    private string transferSpeed = "0 KB/s";

    [ObservableProperty]
    private int activeCount;

    [ObservableProperty]
    private int queuedCount;

    [ObservableProperty]
    private int completedCount;

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

    public IReadOnlyList<QueueFilterOption> Filters { get; } =
    [
        new(QueueFilter.All, "All statuses"),
        new(QueueFilter.Active, "Active"),
        new(QueueFilter.Queued, "Queued"),
        new(QueueFilter.Paused, "Paused"),
        new(QueueFilter.Completed, "Completed"),
        new(QueueFilter.Failed, "Failed")
    ];

    public DownloadsPageViewModel(DownloadEngineService engine, IDialogService dialogs)
    {
        _engine = engine;
        _dialogs = dialogs;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => Refresh(sampleSpeed: true);
        selectedFilter = Filters[0];
        UiText.Changed += ApplyLanguage;

        _ = LoadAsync();
    }

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();

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

        // The servers are started even when the queue failed to load: they are
        // configured separately, and a broken DownloadList.xml must not keep the
        // browser remote control down until the next settings save.
        await Task.Run(() =>
        {
            string error = WebServerHost.Start(_engine, _engine.Config);
            if (!string.IsNullOrEmpty(error))
                Log.WriteError("Web server did not start: " + error);
        });

        if (_disposed)
            return;
        Refresh();
        _refreshTimer.Start();
    }

    private void ApplyLanguage(object? sender, EventArgs args)
    {
        foreach (var filter in Filters)
            filter.ApplyLanguage();
        foreach (var item in _rowCache.Values)
            item.ApplyLanguage();
        Refresh();
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
            Refresh();
        }
    }

    [RelayCommand]
    private void ResetFilter() => SelectedFilter = Filters[0];

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

    internal async Task ShowPropertiesAsync(object item)
    {
        if (await _dialogs.ShowPropertiesAsync(item))
            Refresh();
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
        Refresh();
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

    internal void Refresh(bool sampleSpeed = false)
    {
        if (_disposed || _loading)
            return;

        _loading = true;
        try
        {
            var packages = _engine.SnapshotPackages();
            var desired = new List<DownloadItemViewModel>(Downloads.Count);
            var liveItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int downloading = 0, queued = 0, paused = 0, completed = 0, failed = 0;
            decimal totalSpeed = 0m;

            foreach (Paquete package in packages)
            {
                liveItems.Add(package);
                var header = GetOrCreateRow(package, isPackage: true);
                var files = package.SnapshotFicheros();
                UpdatePackageRow(header, package, files);


                var visibleFiles = new List<Fichero>(files.Count);
                foreach (Fichero file in files)
                {
                    liveItems.Add(file);
                    CountState(file.DescargaEstado(), ref downloading, ref queued, ref paused, ref completed, ref failed);
                    totalSpeed += file.DescargaVelocidadKBs();
                    if (MatchesFilter(file.DescargaEstado()))
                        visibleFiles.Add(file);
                }

                bool packageMatches = MatchesFilter(package.DescargaEstado());
                if (visibleFiles.Count == 0 && !packageMatches)
                    continue;

                header.HasChildren = files.Count > 0;
                desired.Add(header);

                foreach (Fichero file in visibleFiles)
                {
                    var child = GetOrCreateRow(file, isPackage: false);
                    UpdateFileRow(child, file);
                    if (header.IsExpanded)
                        desired.Add(child);
                }
            }

            Reconcile(desired);
            PruneRowCache(liveItems);

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
            else
            {
                EmptyTitle = UiText.Get("Nothing matches this filter");
                EmptyMessage = UiText.Get("Choose another status to see more downloads.");
            }

            CanStartAll = _engine.IsLoaded && HasFiles && _engine.State != DownloadEngineState.Running;
            CanPauseAll = _engine.IsLoaded && _engine.State == DownloadEngineState.Running;
            CanStopAll = _engine.IsLoaded && _engine.State != DownloadEngineState.Stopped;
            ActiveCount = downloading;
            QueuedCount = queued;
            CompletedCount = completed;
            TransferSpeed = FormatSpeed(totalSpeed);
            if (sampleSpeed)
            {
                if (_speedSamples.Count == 60)
                    _speedSamples.Dequeue();
                _speedSamples.Enqueue((double)totalSpeed);
                SpeedHistory = _speedSamples.ToArray();
            }

            Summary = UiText.Format("{0} active · {1} queued · {2} paused · {3} completed · {4} failed",
                downloading, queued, paused, completed, failed);
            Status = _actionFeedback is not null && DateTime.UtcNow < _feedbackUntil
                ? _actionFeedback
                : UiText.Get(_loadFailed ? "Could not load the download queue" : _engine.State switch
                {
                    DownloadEngineState.Running => UiText.Get("Queue running"),
                    DownloadEngineState.Paused => UiText.Get("Queue paused"),
                    _ => UiText.Get("Queue stopped")
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

    private bool MatchesFilter(Estado state) => SelectedFilter?.Value switch
    {
        QueueFilter.Active => state is Estado.Descargando or Estado.CreandoLocal or Estado.Verificando
            or Estado.ComprobandoMD5 or Estado.Descomprimiendo or Estado.EsperandoVolumenes,
        QueueFilter.Queued => state == Estado.EnCola,
        QueueFilter.Paused => state == Estado.Pausado,
        QueueFilter.Completed => state == Estado.Completado,
        QueueFilter.Failed => state == Estado.Erroneo,
        _ => true
    };

    partial void OnSelectedFilterChanged(QueueFilterOption? value) => Refresh();

    private static void CountState(Estado state, ref int downloading, ref int queued, ref int paused, ref int completed, ref int failed)
    {
        switch (state)
        {
            case Estado.Descargando:
            case Estado.CreandoLocal:
            case Estado.Verificando:
            case Estado.ComprobandoMD5:
            case Estado.Descomprimiendo:
            case Estado.EsperandoVolumenes:
                downloading++;
                break;
            case Estado.EnCola:
                queued++;
                break;
            case Estado.Pausado:
                paused++;
                break;
            case Estado.Completado:
                completed++;
                break;
            case Estado.Erroneo:
                failed++;
                break;
        }
    }

    private DownloadItemViewModel GetOrCreateRow(object item, bool isPackage)
    {
        if (_rowCache.TryGetValue(item, out var row))
            return row;

        row = new DownloadItemViewModel(this, item, isPackage);
        _rowCache[item] = row;
        return row;
    }

    /// <summary>Drops rows whose engine objects left the queue. Rows hidden by the
    /// current filter stay cached so their expansion state survives filter changes.</summary>
    private void PruneRowCache(HashSet<object> liveItems)
    {
        if (_rowCache.Count == 0)
            return;

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
        row.CanStart = package.DescargaEstado() is Estado.EnCola or Estado.Pausado or Estado.Erroneo;
        row.CanPause = package.DescargaEstado() is Estado.Descargando or Estado.CreandoLocal;
        row.IsCompleted = package.DescargaEstado() == Estado.Completado;
        row.IsFailed = package.DescargaEstado() == Estado.Erroneo;
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

        row.CanStart = file.DescargaEstado() is Estado.EnCola or Estado.Pausado or Estado.Erroneo;
        row.CanPause = file.DescargaEstado() is Estado.Descargando or Estado.CreandoLocal;
        row.IsCompleted = file.DescargaEstado() == Estado.Completado;
        row.IsFailed = file.DescargaEstado() == Estado.Erroneo;
    }

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
        Downloads.Clear();
        foreach (var row in desired)
            Downloads.Add(row);

        SelectedDownload = selected is not null && desired.Contains(selected) ? selected : null;
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
        UiText.Changed -= ApplyLanguage;
    }
}

/// <summary>One flat, virtualized row: a package header or one of its files.</summary>
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
    private string sizeText = string.Empty;

    [ObservableProperty]
    private string speedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private double progress;

    public string ProgressText => (Progress / 100).ToString("0.0%", UiText.Culture);

    internal void ApplyLanguage() => OnPropertyChanged(nameof(ProgressText));

    [ObservableProperty]
    private bool canStart;

    [ObservableProperty]
    private bool canPause;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isFailed;

    [ObservableProperty]
    private bool hasChildren;

    [ObservableProperty]
    private bool isExpanded = true;

    public string Chevron => IsExpanded ? "⌄" : "›";

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Chevron));

    [RelayCommand]
    private void Start() => _owner.StartItem(Item);

    [RelayCommand]
    private void Pause() => _owner.PauseItem(Item);

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        _owner.Refresh();
    }

    [RelayCommand]
    private Task PropertiesAsync() => _owner.ShowPropertiesAsync(Item);

    [RelayCommand]
    private Task RemoveAsync() => _owner.RemoveItemAsync(Item, deleteFiles: false);

    [RelayCommand]
    private Task DeleteAsync() => _owner.RemoveItemAsync(Item, deleteFiles: true);
}

public enum QueueFilter { All, Active, Queued, Paused, Completed, Failed }

public sealed class QueueFilterOption(QueueFilter value, string label) : ObservableObject
{
    public QueueFilter Value { get; } = value;
    public string Label => UiText.Get(label);

    internal void ApplyLanguage() => OnPropertyChanged(nameof(Label));
}
