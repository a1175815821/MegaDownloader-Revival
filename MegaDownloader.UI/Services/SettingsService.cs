using System.Text.Json;
using MegaDownloader.Platform;

namespace MegaDownloader.UI.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsService(IPlatformServices platform)
    {
        var directory = platform.GetAppDataDirectory("MegaDownloader");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "ui-settings.json");
        Current = Load(_path);
    }

    public UiSettings Current { get; }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static UiSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var settings = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(path));
                if (settings is not null)
                    return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return new UiSettings();
    }
}
