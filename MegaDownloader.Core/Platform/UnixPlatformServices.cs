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
        _appData = ApplicationDataDirectory.Override ?? (kind == PlatformKind.MacOS
            ? Path.Combine(home, "Library", "Application Support", applicationName)
            : Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share"), applicationName));
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

    public void SetAutoStart(bool enabled, string executablePath, string arguments) =>
        ApplyAutoStart(_kind, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            enabled, executablePath, arguments);

    /// <summary>
    /// Creates or removes the login-autostart entry under <paramref name="homeDirectory"/>.
    /// Split out so the enable/disable cycle can be checked without touching the real
    /// profile.
    /// </summary>
    public static void ApplyAutoStart(PlatformKind kind, string homeDirectory, bool enabled,
        string executablePath, string arguments)
    {
        string file = AutoStartFile(kind, homeDirectory);
        if (!enabled)
        {
            if (File.Exists(file))
                File.Delete(file);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, BuildAutoStartEntry(kind, executablePath, arguments));
    }

    public static bool IsAutoStartEnabled(PlatformKind kind, string homeDirectory) =>
        File.Exists(AutoStartFile(kind, homeDirectory));

    /// <summary>The file a login session reads for this platform and user.</summary>
    public static string AutoStartFile(PlatformKind kind, string homeDirectory) =>
        kind == PlatformKind.MacOS
            ? Path.Combine(homeDirectory, "Library", "LaunchAgents", "com.megadownloader.plist")
            : Path.Combine(homeDirectory, ".config", "autostart", "MegaDownloader.desktop");

    /// <summary>
    /// The login-autostart entry for one platform. An installed package is started
    /// through its stable entry point (<c>/usr/bin/megadownloader</c>) so the entry
    /// survives an upgrade that replaces the binary in <c>/usr/lib/megadownloader</c>.
    /// </summary>
    public static string BuildAutoStartEntry(PlatformKind kind, string executablePath, string arguments)
    {
        string command = StableExecutable(executablePath);
        string quoted = Quote(command);
        string suffix = string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments.Trim();
        if (kind == PlatformKind.MacOS)
        {
            // ProgramArguments is an array, so a path with spaces needs no quoting.
            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0"><dict>
                  <key>Label</key><string>com.megadownloader</string>
                  <key>ProgramArguments</key><array><string>{System.Security.SecurityElement.Escape(command)}</string></array>
                  <key>RunAtLoad</key><true/>
                </dict></plist>
                """;
        }
        return $"""
            [Desktop Entry]
            Type=Application
            Name=Mega Downloader
            Exec={quoted}{suffix}
            Hidden=false
            X-GNOME-Autostart-enabled=true
            """;
    }

    /// <summary>
    /// Maps a binary inside an installed package to its launcher. Anything else (a
    /// development build, a self-contained folder) keeps the path it was started from.
    /// </summary>
    public static string StableExecutable(string executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return string.Empty;
        foreach (string prefix in new[] { "/usr/lib/megadownloader/", "/opt/megadownloader/" })
        {
            if (executablePath.StartsWith(prefix, StringComparison.Ordinal))
                return "/usr/bin/megadownloader";
        }
        return executablePath;
    }

    public void SetMegaProtocolHandler(bool enabled, string executablePath)
    {
        if (_kind != PlatformKind.Linux || !enabled) return;
        // An installed package ships its own desktop entry with x-scheme-handler/mega;
        // a per-user copy would shadow it and pin the association to one binary path.
        if (executablePath.StartsWith("/usr/", StringComparison.Ordinal) ||
            executablePath.StartsWith("/opt/", StringComparison.Ordinal))
            return;
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
