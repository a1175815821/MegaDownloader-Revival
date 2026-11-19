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
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.WriteWarning("UI settings could not be saved: " + Log.SafeException(error));
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Log.WriteWarning("Temporary UI settings could not be removed: " + Log.SafeException(error));
            }
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
