namespace MegaDownloader.UI.Services;

public enum ThemeMode
{
    Auto,
    Light,
    Dark
}

/// <summary>
/// Wraps <see cref="Avalonia.Application.RequestedThemeVariant"/> so view models
/// never touch Avalonia types directly.
/// </summary>
public interface IThemeService
{
    ThemeMode Mode { get; }

    event EventHandler? ModeChanged;

    void Initialize();

    void SetMode(ThemeMode mode);
}
