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

    [ObservableProperty]
    private string status = "Loading queue…";

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
    private string selectedFilter = "All";

    [ObservableProperty]
    private string emptyTitle = "No downloads yet";

    [ObservableProperty]
    private string emptyMessage =
        "Add links with the toolbar button. Queued items appear here with live progress, speed and per-item controls.";

    public IReadOnlyList<string> Filters { get; } = new[] { "All", "Active", "Queued", "Paused", "Completed", "Failed" };

    public DownloadsPageViewModel(DownloadEngineService engine, IDialogService dialogs)
    {
        _engine = engine;
        _dialogs = dialogs;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => Refresh();

        _ = LoadAsync();
    }

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();

    private async Task LoadAsync()
    {
        IsBusy = true;
        Status = "Loading queue…";

        try
        {
            await _engine.LoadAsync();
            Status = "Queue loaded";
        }
        catch (Exception ex)
        {
            Status = "Could not load the download queue: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        Refresh();
        _refreshTimer.Start();
    }

    [RelayCommand]
    private void StartAll()
    {
        _engine.StartAll();
        Refresh();
    }

    [RelayCommand]
    private void PauseAll()
    {
        _engine.PauseAll();
        Refresh();
    }

    [RelayCommand]
    private void StopAll()
    {
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
            _ => "this download"
        };

        string message = deleteFiles
            ? $"Remove “{name}” and delete its downloaded files from disk?"
            : $"Remove “{name}” from the queue? Files already downloaded are kept.";

        bool confirmed = await _dialogs.ConfirmAsync(
            deleteFiles ? "Delete download" : "Remove download",
            message,
            deleteFiles ? "Delete" : "Remove",
            "Cancel");

        if (!confirmed)
            return;

        // Stop() can wait up to five seconds for an in-flight write; keep it off the UI thread.
        await Task.Run(() => _engine.Remove(item, deleteFiles));
        Status = deleteFiles ? $"Deleted {name}" : $"Removed {name}";
        Refresh();
    }

    internal void Refresh()
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
                UpdatePackageRow(header, package);

                var files = package.SnapshotFicheros();
                CountState(package.DescargaEstado(), ref downloading, ref queued, ref paused, ref completed, ref failed);

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
            if (packages.Count == 0)
            {
                EmptyTitle = "No downloads yet";
                EmptyMessage = "Add links with the toolbar button. Queued items appear here with live progress, speed and per-item controls.";
            }
            else
            {
                EmptyTitle = "Nothing matches this filter";
                EmptyMessage = "Pick another status filter to see the rest of the queue.";
            }
            CanStartAll = _engine.State != DownloadEngineState.Running;
            CanPauseAll = _engine.State == DownloadEngineState.Running;
            CanStopAll = _engine.State != DownloadEngineState.Stopped;

            string stateText = _engine.State switch
            {
                DownloadEngineState.Running => "Running",
                DownloadEngineState.Paused => "Paused",
                _ => "Stopped"
            };

            Summary = string.Create(
                CultureInfo.InvariantCulture,
                $"{downloading} downloading · {queued} queued · {paused} paused · {completed} completed · {failed} failed");

            Status = totalSpeed > 0
                ? $"{stateText} · {FormatSpeed(totalSpeed)} · {downloading} active"
                : stateText;
        }
        catch (Exception ex)
        {
            Status = "Queue refresh failed: " + ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private bool MatchesFilter(Estado state) => SelectedFilter switch
    {
        "Active" => state is Estado.Descargando or Estado.CreandoLocal or Estado.Verificando
            or Estado.ComprobandoMD5 or Estado.Descomprimiendo or Estado.EsperandoVolumenes,
        "Queued" => state == Estado.EnCola,
        "Paused" => state == Estado.Pausado,
        "Completed" => state == Estado.Completado,
        "Failed" => state == Estado.Erroneo,
        _ => true
    };

    partial void OnSelectedFilterChanged(string value) => Refresh();

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

    private static void UpdatePackageRow(DownloadItemViewModel row, Paquete package)
    {
        row.Title = package.DescargaNombre();
        row.StateText = StateLabel(package.DescargaEstado());
        row.Progress = (double)Math.Clamp(package.DescargaPorcentaje(), 0m, 100m);
        row.SizeText = FormatBytes(package.DescargaTamanoBytes());
        row.SpeedText = package.DescargaVelocidadKBs() > 0 ? FormatSpeed(package.DescargaVelocidadKBs()) : string.Empty;
        row.DetailText = $"{package.ListaFicheros?.Count ?? 0} files · {row.SizeText}";
        row.CanStart = package.DescargaEstado() is Estado.EnCola or Estado.Pausado or Estado.Erroneo;
        row.CanPause = package.DescargaEstado() is Estado.Descargando or Estado.CreandoLocal;
        row.IsCompleted = package.DescargaEstado() == Estado.Completado;
        row.IsFailed = package.DescargaEstado() == Estado.Erroneo;
    }

    private static void UpdateFileRow(DownloadItemViewModel row, Fichero file)
    {
        row.Title = file.DescargaNombre();
        row.StateText = StateLabel(file.DescargaEstado());
        row.Progress = (double)Math.Clamp(file.DescargaPorcentaje(), 0m, 100m);
        row.SizeText = FormatBytes(file.DescargaTamanoBytes());

        decimal speed = file.DescargaVelocidadKBs();
        row.SpeedText = speed > 0 ? FormatSpeed(speed) : string.Empty;

        string eta = file.DescargaTiempoEstimadoDescarga();
        row.DetailText = string.IsNullOrWhiteSpace(eta)
            ? row.SizeText
            : $"{row.SizeText} · ETA {eta}";

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
        Estado.Verificando => "Checking",
        Estado.CreandoLocal => "Preparing",
        Estado.EnCola => "Queued",
        Estado.Descargando => "Downloading",
        Estado.Pausado => "Paused",
        Estado.Completado => "Completed",
        Estado.ComprobandoMD5 => "Verifying",
        Estado.Descomprimiendo => "Extracting",
        Estado.Erroneo => "Error",
        Estado.EsperandoVolumenes => "Waiting for volumes",
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
        RowMargin = isPackage ? new Thickness(0) : new Thickness(24, 0, 0, 0);
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
    private double progress;

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

    public string Chevron => IsExpanded ? "▾" : "▸";

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
