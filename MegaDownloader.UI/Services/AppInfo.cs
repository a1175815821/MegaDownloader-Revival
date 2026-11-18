using System.Reflection;

namespace MegaDownloader.UI.Services;

/// <summary>Static app metadata shared by the shell and the settings page.</summary>
public static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    public static string Runtime => $".NET {Environment.Version.Major} · Avalonia 12";

    private static string ReadVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
            return "0.0.0";

        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
