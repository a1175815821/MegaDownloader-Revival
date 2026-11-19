using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MegaDownloader.Platform;

/// <summary>
/// Windows implementation preserving the original DPAPI entropy chain and registry
/// integration. Compiled on every target so cross-compilation stays simple; methods
/// no-op or throw PlatformNotSupportedException when invoked elsewhere.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServices : IPlatformServices
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.Unicode.GetBytes("MegaDownloader"));

    public PlatformKind CurrentPlatform => PlatformKind.Windows;

    public string GetAppDataDirectory(string applicationName)
    {
        EnsureWindows();
        if (applicationName == "MegaDownloader")
            return ApplicationDataDirectory.GetLegacyRoot();
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName);
    }

    public string ProtectSecret(string plaintext)
    {
        EnsureWindows();
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var bytes = ProtectedData.Protect(
            Encoding.Unicode.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public string UnprotectSecret(string ciphertext)
    {
        EnsureWindows();
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        var bytes = ProtectedData.Unprotect(
            Convert.FromBase64String(ciphertext), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.Unicode.GetString(bytes);
    }

    public void SetAutoStart(bool enabled, string executablePath, string arguments)
    {
        EnsureWindows();
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null) return;
        if (enabled)
            key.SetValue("MegaDownloader", $"\"{executablePath}\" {arguments}");
        else
            key.DeleteValue("MegaDownloader", throwOnMissingValue: false);
    }

    public void SetMegaProtocolHandler(bool enabled, string executablePath)
    {
        EnsureWindows();
        using var root = Registry.CurrentUser.OpenSubKey("Software\\Classes", writable: true);
        if (root is null) return;
        if (!enabled)
        {
            root.DeleteSubKeyTree("mega", throwOnMissingSubKey: false);
            return;
        }
        using var protocol = root.CreateSubKey("mega");
        protocol.SetValue(string.Empty, "URL:Mega Downloader");
        protocol.SetValue("URL Protocol", string.Empty);
        using var command = protocol.CreateSubKey("shell\\open\\command");
        command.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
    }

    public void OpenWithDefaultApplication(string pathOrUrl)
    {
        EnsureWindows();
        Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
    }

    public void ScheduleShutdown(TimeSpan delay)
    {
        EnsureWindows();
        Process.Start(new ProcessStartInfo("shutdown", $"-s -t {(int)delay.TotalSeconds}")
        {
            UseShellExecute = false
        });
    }

    public string FindExecutable(string name)
    {
        EnsureWindows();
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe");
            if (File.Exists(candidate)) return candidate;
        }
        return string.Empty;
    }

    private static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("This implementation is Windows-only.");
    }
}
