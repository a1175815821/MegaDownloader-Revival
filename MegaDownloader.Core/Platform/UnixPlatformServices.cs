using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MegaDownloader.Platform;

/// <summary>
/// macOS/Linux implementation. Secrets are stored in a 0600 (or user-only) file in
/// the application data directory encrypted with a key derived from the user's
/// machine ID. A production build should swap this for the OS keychain / libsecret.
/// </summary>
public sealed class UnixPlatformServices : IPlatformServices
{
    private readonly PlatformKind _kind;
    private readonly string _appData;

    public UnixPlatformServices(PlatformKind kind, string applicationName = "MegaDownloader")
    {
        _kind = kind;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _appData = kind == PlatformKind.MacOS
            ? Path.Combine(home, "Library", "Application Support", applicationName)
            : Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share"), applicationName);
        Directory.CreateDirectory(_appData);
    }

    public PlatformKind CurrentPlatform => _kind;

    public string GetAppDataDirectory(string applicationName) => _appData;

    public string ProtectSecret(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var key = DeriveKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            cs.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public string UnprotectSecret(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        var all = Convert.FromBase64String(ciphertext);
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        var iv = new byte[16];
        Array.Copy(all, iv, 16);
        aes.IV = iv;
        using var ms = new MemoryStream(all, 16, all.Length - 16);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var reader = new StreamReader(cs, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public void SetAutoStart(bool enabled, string executablePath, string arguments)
    {
        var autostartDir = _kind == PlatformKind.MacOS
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
        Directory.CreateDirectory(autostartDir);
        var file = _kind == PlatformKind.MacOS
            ? Path.Combine(autostartDir, "com.megadownloader.plist")
            : Path.Combine(autostartDir, "MegaDownloader.desktop");
        if (!enabled)
        {
            if (File.Exists(file)) File.Delete(file);
            return;
        }
        if (_kind == PlatformKind.MacOS)
        {
            File.WriteAllText(file, $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0"><dict>
                  <key>Label</key><string>com.megadownloader</string>
                  <key>ProgramArguments</key><array><string>{executablePath}</string></array>
                  <key>RunAtLoad</key><true/>
                </dict></plist>
                """);
        }
        else
        {
            File.WriteAllText(file, $"""
                [Desktop Entry]
                Type=Application
                Name=Mega Downloader
                Exec={executablePath} {arguments}
                Hidden=false
                X-GNOME-Autostart-enabled=true
                """);
        }
    }

    public void SetMegaProtocolHandler(bool enabled, string executablePath)
    {
        if (_kind != PlatformKind.Linux || !enabled) return;
        var applications = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");
        Directory.CreateDirectory(applications);
        var desktop = Path.Combine(applications, "MegaDownloader.desktop");
        File.WriteAllText(desktop, $"""
            [Desktop Entry]
            Type=Application
            Name=Mega Downloader
            Exec={executablePath} %u
            MimeType=x-scheme-handler/mega;
            """);
        Run("xdg-mime", "default MegaDownloader.desktop x-scheme-handler/mega");
    }

    public void OpenWithDefaultApplication(string pathOrUrl)
    {
        Run(_kind == PlatformKind.MacOS ? "open" : "xdg-open", Quote(pathOrUrl));
    }

    public void ScheduleShutdown(TimeSpan delay)
    {
        if (_kind == PlatformKind.MacOS)
            Run("sudo", $"-n shutdown -h +{Math.Max(1, (int)Math.Ceiling(delay.TotalMinutes))}");
        else
            Run("shutdown", $"-P +{Math.Max(1, (int)Math.Ceiling(delay.TotalMinutes))}");
    }

    public string FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return string.Empty;
    }

    private byte[] DeriveKey()
    {
        var seed = $"{_kind}-{Environment.UserName}-{Environment.MachineName}-MegaDownloader";
        return SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    }

    private static string Quote(string s) => $"\"{s.Replace("\"", "\\\"")}\"";

    private static void Run(string file, string arguments)
    {
        Process.Start(new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })?.WaitForExit(5000);
    }
}
