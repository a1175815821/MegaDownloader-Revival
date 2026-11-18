using System.Runtime.InteropServices;

namespace MegaDownloader.Platform;

/// <summary>
/// Cross-platform services required by the core download engine. Implementations
/// are provided per target platform; Windows can preserve the original behaviour
/// (DPAPI, registry autostart, mega:// protocol) while macOS/Linux use their own
/// equivalents.
/// </summary>
public interface IPlatformServices
{
    PlatformKind CurrentPlatform { get; }

    /// <summary>Folder used for configuration, logs and queues.</summary>
    string GetAppDataDirectory(string applicationName);

    /// <summary>Reversible encryption of secrets; replaces direct DPAPI calls.</summary>
    string ProtectSecret(string plaintext);
    string UnprotectSecret(string ciphertext);

    /// <summary>Register or remove "run at login" for this application.</summary>
    void SetAutoStart(bool enabled, string executablePath, string arguments);

    /// <summary>Register or remove the mega:// URL protocol handler.</summary>
    void SetMegaProtocolHandler(bool enabled, string executablePath);

    /// <summary>Open a URL or file with the operating system's default handler.</summary>
    void OpenWithDefaultApplication(string pathOrUrl);

    /// <summary>Power off the machine after the given delay (used by "finish and shutdown").</summary>
    void ScheduleShutdown(TimeSpan delay);

    /// <summary>Locate an external tool (VLC, 7-Zip) on PATH or platform standard locations.</summary>
    string FindExecutable(string name);
}

public enum PlatformKind
{
    Windows,
    MacOS,
    Linux,
    Unknown
}

public static class PlatformServicesFactory
{
    public static PlatformKind DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return PlatformKind.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return PlatformKind.MacOS;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return PlatformKind.Linux;
        return PlatformKind.Unknown;
    }

    /// <summary>
    /// Creates the platform implementation without a DI container. Used before the
    /// composition root exists (single-instance gate, crash logging).
    /// </summary>
    public static IPlatformServices Create(string applicationName = "MegaDownloader")
    {
        PlatformKind kind = DetectPlatform();
        return kind == PlatformKind.Windows
            ? new WindowsPlatformServices()
            : new UnixPlatformServices(kind, applicationName);
    }

    /// <summary>
    /// The secret protector for this OS: DPAPI on Windows (with its legacy entropy
    /// chain), the OS keychain on macOS/Linux with an encrypted key file as the fallback.
    /// </summary>
    public static ISecretProtector CreateSecretProtector(IPlatformServices platform)
    {
        if (platform.CurrentPlatform == PlatformKind.Windows)
            return new WindowsDpapiSecretProtector();

        return new KeychainSecretProtector(platform.GetAppDataDirectory("MegaDownloader"));
    }
}
