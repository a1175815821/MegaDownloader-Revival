#nullable enable

namespace MegaDownloader;

internal static class ApplicationDataDirectory
{
    // A process can opt into a separate profile, for example when validating a build
    // alongside an existing installation. Normal launches retain the legacy paths.
    public static string? Override { get; } = ReadOverride();

    public static string GetLegacyRoot()
    {
        if (Override is not null)
            return Override;

        // macOS keeps the platform's Application Support location; .NET's
        // LocalApplicationData is XDG-oriented there and would scatter the data.
        if (OperatingSystem.IsMacOS())
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(profile, "Library", "Application Support", "MegaDownloader");
        }

        // Linux resolves to $XDG_DATA_HOME or ~/.local/share; Windows keeps
        // %LOCALAPPDATA%\MegaDownloader.
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MegaDownloader");
    }

    private static string? ReadOverride()
    {
        string? value = Environment.GetEnvironmentVariable("MEGADOWNLOADER_DATA_DIR");
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value)
            ? Path.GetFullPath(value)
            : null;
    }
}
