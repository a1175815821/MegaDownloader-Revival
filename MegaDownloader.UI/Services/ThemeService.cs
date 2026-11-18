using Avalonia;
using Avalonia.Styling;

namespace MegaDownloader.UI.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
        Mode = Parse(settings.Current.Theme);
    }

    public ThemeMode Mode { get; private set; }

    public event EventHandler? ModeChanged;

    public void Initialize() => Apply(Mode);

    public void SetMode(ThemeMode mode)
    {
        Mode = mode;
        _settings.Current.Theme = mode.ToString();
        _settings.Save();
        Apply(mode);
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Apply(ThemeMode mode)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = mode switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
    }

    private static ThemeMode Parse(string value) =>
        Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode) ? mode : ThemeMode.Auto;
}
