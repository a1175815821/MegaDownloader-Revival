namespace MegaDownloader.UI.Services;

/// <summary>Shell-level settings, stored separately from the legacy download settings.</summary>
public sealed class UiSettings
{
    public string Theme { get; set; } = "Auto";
}

public interface ISettingsService
{
    UiSettings Current { get; }

    void Save();
}
