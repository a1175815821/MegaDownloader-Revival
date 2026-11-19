#nullable enable

namespace MegaDownloader;

internal static class ApplicationDataDirectory
{
    // A process can opt into a separate profile, for example when validating a build
    // alongside an existing installation. Normal launches retain the legacy paths.
    public static string? Override { get; } = ReadOverride();

    public static string GetLegacyRoot() => Override ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader");

    private static string? ReadOverride()
    {
        string? value = Environment.GetEnvironmentVariable("MEGADOWNLOADER_DATA_DIR");
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value)
            ? Path.GetFullPath(value)
            : null;
    }
}
