using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// File/package properties. Mirrors the WinForms <c>PropiedadesDescarga</c> rules:
/// the path only moves files that have not started, speed limits are only written when
/// the user changed them, and package edits propagate to every child file.
/// </summary>
public partial class PropertiesViewModel : ViewModelBase
{
    private readonly IDescarga _item;
    private readonly Fichero? _file;
    private readonly Paquete? _package;
    private readonly bool _limitWasActive;
    private readonly int _limitInitialKb;

    [ObservableProperty]
    private string header = "Properties";

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private string md5 = string.Empty;

    [ObservableProperty]
    private string sizeText = string.Empty;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private bool canEditPath = true;

    [ObservableProperty]
    private bool limitSpeed;

    [ObservableProperty]
    private string speedLimitText = string.Empty;

    [ObservableProperty]
    private bool extractArchives;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string notice = string.Empty;

    [ObservableProperty]
    private bool noticeIsError;

    public PropertiesViewModel(object item)
    {
        _item = item as IDescarga ?? throw new ArgumentException("The item is not a download.", nameof(item));
        _file = item as Fichero;
        _package = item as Paquete;

        Header = _file is not null ? "File properties" : "Package properties";
        Name = _item.DescargaNombre();
        StatusText = DownloadsPageViewModel.StateLabel(_item.DescargaEstado());
        long total = _item.DescargaTamanoBytes();
        long downloaded = (long)(_item.DescargaPorcentaje() * total / 100m);
        SizeText = $"{DownloadsPageViewModel.FormatBytes(downloaded)} of {DownloadsPageViewModel.FormatBytes(total)}";
        ExtractArchives = _item.DescargaExtraccionAutomatica();
        Password = _item.DescargaExtraccionPassword() ?? string.Empty;

        if (_file is not null)
        {
            Url = _file.LinkVisible ? _file.URL : Fichero.HIDDEN_LINK_DESC;
            Md5 = string.IsNullOrEmpty(_file.MD5) ? "Not available" : _file.MD5;
            Path = _file.RutaLocal ?? string.Empty;
            CanEditPath = !_file.DescargaComenzada;
            if (_file.LimiteVelocidad > 0)
            {
                LimitSpeed = true;
                SpeedLimitText = Utils.FormatSpeedLimit(_file.LimiteVelocidad);
                _limitInitialKb = _file.LimiteVelocidad;
            }
        }
        else if (_package is not null)
        {
            Url = "Not applied";
            Md5 = "Not applied";
            Path = _package.RutaLocal ?? string.Empty;
            int common = -1;
            foreach (Fichero file in _package.SnapshotFicheros())
            {
                if (common == -1)
                    common = file.LimiteVelocidad;
                else if (common != file.LimiteVelocidad)
                {
                    common = -2;
                    break;
                }
            }
            if (common > 0)
            {
                LimitSpeed = true;
                SpeedLimitText = Utils.FormatSpeedLimit(common);
                _limitInitialKb = common;
            }
        }

        _limitWasActive = LimitSpeed;
    }

    public bool IsPackage => _package is not null;

    /// <summary>Set by the window so Browse can use the platform folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>Raised with true when the edits were saved.</summary>
    public event Action<bool>? CloseRequested;

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (PickFolderAsync is null)
            return;
        string? folder = await PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
            Path = folder;
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    [RelayCommand]
    private void Save()
    {
        int limitKb = 0;
        if (LimitSpeed && !Utils.TryParseSpeedLimit(SpeedLimitText, ref limitKb))
        {
            SetNotice("Invalid speed limit. Use a value like 512 KB/s or 2 MB/s.");
            return;
        }

        bool limitChanged = LimitSpeed != _limitWasActive || (LimitSpeed && limitKb != _limitInitialKb);
        string newPath = (Path ?? string.Empty).Trim();

        if (_file is not null)
        {
            Fichero file = _file;
            if (CanEditPath && !string.Equals(newPath, file.RutaLocal ?? string.Empty, StringComparison.Ordinal))
            {
                if (!ValidateDirectory(newPath))
                    return;
                file.RutaLocal = newPath;
            }

            file.set_SetDescargaExtraccionAutomatica(Password, ExtractArchives);
            if (limitChanged)
            {
                int value = LimitSpeed ? limitKb : 0;
                file.LimiteVelocidad = value;
                ThrottledStreamController.GetController().SetMaxSpeed(file.FileID, value * 1024L);
            }
        }
        else if (_package is not null)
        {
            Paquete package = _package;
            bool rootChanged = !string.Equals(newPath, package.RutaLocal ?? string.Empty, StringComparison.Ordinal);
            if (rootChanged && !ValidateDirectory(newPath))
                return;

            if (rootChanged)
            {
                foreach (Fichero file in package.ListaFicheros)
                {
                    if (file.DescargaComenzada)
                        continue;
                    try
                    {
                        file.RutaLocal = PathGuard.GetSafePathUnderRoot(newPath, file.RutaRelativa ?? string.Empty, allowRoot: true);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Properties: invalid relocated path, keeping the old one for " + file.FileID + ": " + Log.SafeException(ex));
                    }
                }
                package.RutaLocal = newPath;
            }

            bool extractionChanged = package.DescargaExtraccionAutomatica() != ExtractArchives ||
                                     (package.DescargaExtraccionPassword() ?? string.Empty) != (Password ?? string.Empty);
            foreach (Fichero file in package.ListaFicheros)
            {
                if (extractionChanged)
                    file.set_SetDescargaExtraccionAutomatica(Password, ExtractArchives);
                if (limitChanged)
                {
                    int value = LimitSpeed ? limitKb : 0;
                    file.LimiteVelocidad = value;
                    ThrottledStreamController.GetController().SetMaxSpeed(file.FileID, value * 1024L);
                }
            }

            if (extractionChanged)
                package.set_SetDescargaExtraccionAutomatica(Password, ExtractArchives);
        }

        CloseRequested?.Invoke(true);
    }

    private bool ValidateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            SetNotice("Enter a download folder.");
            return false;
        }
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception ex)
        {
            Log.WriteError("Properties: invalid path '" + path + "': " + Log.SafeException(ex));
            SetNotice("Invalid directory.");
            return false;
        }
    }

    private void SetNotice(string text)
    {
        Notice = text;
        NoticeIsError = true;
    }
}
