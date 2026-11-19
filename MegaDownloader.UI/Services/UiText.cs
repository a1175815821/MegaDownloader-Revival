using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;

namespace MegaDownloader.UI.Services;

internal static class UiText
{
    private static readonly Dictionary<string, string> Resources = Load("resources");
    private static readonly Lazy<Dictionary<string, string>> LegacyKeys = new(BuildLegacyKeys);
    private static Dictionary<string, string> _translations = new(StringComparer.Ordinal);
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    private static ResourceDictionary? _localizedResources;

    public static event EventHandler? Changed;

    public static void Refresh()
    {
        string code = Language.GetCurrentLanguageCode();
        Culture = string.IsNullOrEmpty(code) ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo(code);
        _translations = Load(Culture.Name);
        if (_translations.Count == 0 && Culture.Parent.Name.Length > 0)
            _translations = Load(Culture.Parent.Name);

        if (Application.Current is { } app)
        {
            var localized = new ResourceDictionary();
            foreach (var (key, english) in Resources)
                localized[key] = Get(english);

            app.Resources.MergedDictionaries.Add(localized);
            if (_localizedResources is not null)
                app.Resources.MergedDictionaries.Remove(_localizedResources);
            _localizedResources = localized;
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static bool ApplyLanguage(string preference)
    {
        try
        {
            Language.InitLanguage(preference);
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Applying the selected language failed: " + Log.SafeException(ex));
            return false;
        }
    }

    public static string Get(string english)
    {
        if (_translations.TryGetValue(english, out string? value))
            return value;
        if (Culture.TwoLetterISOLanguageName == "en" ||
            !LegacyKeys.Value.TryGetValue(Normalize(english), out string? key))
            return english;

        var localized = Language.GetText(key).Replace("&", string.Empty);
        return string.IsNullOrWhiteSpace(localized) ? english : localized;
    }

    public static string Format(string english, params object[] args) =>
        string.Format(Culture, Get(english), args);

    private static Dictionary<string, string> Load(string name)
    {
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream("Desktop." + name + ".json");
        return stream is null ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
    }

    private static Dictionary<string, string> BuildLegacyKeys()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var assembly = typeof(Language).Assembly;
        string? resource = assembly.GetManifestResourceNames().FirstOrDefault(name => name.EndsWith("en-US-Language.xml", StringComparison.Ordinal));
        if (resource is null || assembly.GetManifestResourceStream(resource) is not { } stream)
            return result;

        using (stream)
        {
            foreach (var element in XDocument.Load(stream).Root!.Elements("Text"))
            {
                string? key = (string?)element.Attribute("key");
                if (string.IsNullOrWhiteSpace(key) || element.Value.Contains('{'))
                    continue;
                result.TryAdd(Normalize(element.Value), key);
                if (!key.Contains('{'))
                    result.TryAdd(Normalize(key), key);
            }
        }

        result[Normalize("Download folder")] = "Download path";
        result[Normalize("Default folder")] = "Download path";
        result[Normalize("Choose a folder")] = "Select directory";
        result[Normalize("Choose download folder")] = "Select directory";
        result[Normalize("Choose default download folder")] = "Select directory";
        result[Normalize("Browse…")] = "Select directory";
        result[Normalize("Package options")] = "Options";
        result[Normalize("Package defaults")] = "General options";
        result[Normalize("Package name (optional)")] = "Name (optional)";
        result[Normalize("Archive password (optional)")] = "Password (optional)";
        result[Normalize("Extract archives automatically")] = "Automatic extraction";
        result[Normalize("Create a subfolder for this package")] = "Create folder per package";
        result[Normalize("Create a subfolder for each package")] = "Create folder per package";
        result[Normalize("Remember the last used add-links options")] = "Save / use last used configuration";
        result[Normalize("Add MEGA links copied to the clipboard")] = "Capture links from clipboard";
        result[Normalize("Simultaneous downloads")] = "Number of parallel downloads";
        result[Normalize("Advanced")] = "Advanced options";
        result[Normalize("General")] = "General options";
        result[Normalize("Download schedule")] = "Schedule_Title";
        result[Normalize("Enable the streaming server")] = "Use streaming server";
        result[Normalize("Streaming server")] = "Streaming configuration";
        result[Normalize("Only download inside a scheduled window")] = "Schedule_Enable";
        result[Normalize("LAN access")] = "Allow LAN access";
        result[Normalize("Allow other devices on the network")] = "Allow LAN access";
        result[Normalize("ELC accounts")] = "ELC Accounts";
        result[Normalize("Manage accounts…")] = "Accounts";
        result[Normalize("Use as the default account")] = "Main account";
        result[Normalize("Friendly name")] = "Name";
        result[Normalize("Select all")] = "Preflight_SelectAll";
        result[Normalize("VLC folder")] = "VLC Configuration";
        result[Normalize("Account could not be removed.")] = "Account could not be deleted";
        result[Normalize("Location")] = "Download path";
        result[Normalize("Already queued")] = "Preflight_State_AlreadyQueued";
        result[Normalize("Link preview")] = "Preflight_Title";
        result[Normalize("Loading saved queue…")] = "Loading please wait";
        result[Normalize("Could not load the download queue")] = "Queue_LoadFailed";
        return result;
    }

    private static string Normalize(string text) =>
        text.Trim().TrimEnd('.', '…').Replace("&", string.Empty).ToUpperInvariant();
}
