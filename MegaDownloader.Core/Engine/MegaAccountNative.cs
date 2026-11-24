using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MegaDownloader
{
    /// <summary>SDK client handle owned by the account service.</summary>
    public sealed class MegaAccountHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private MegaAccountHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            MegaAccountNative.md_close(handle);
            return true;
        }
    }

    /// <summary>
    /// Native bridge to the MEGA SDK. The library is built per platform
    /// (<c>MegaAccountBridge.dll</c>, <c>libMegaAccountBridge.dylib</c>,
    /// <c>libMegaAccountBridge.so</c>) and probed next to the executable, in the
    /// application's native folder, and in the platform bundle directories.
    /// </summary>
    internal static class MegaAccountNative
    {
        internal const string Library = "MegaAccountBridge";
        private static readonly object NativeGate = new object();
        private static int _state; // 0 = unknown, 1 = loaded, -1 = missing
        private static IntPtr _handle;
        private static bool _resolverInstalled;

        static MegaAccountNative()
        {
            InstallResolver();
        }

        internal static bool IsReady => EnsureNative();

        internal static bool EnsureNative()
        {
            if (System.Threading.Volatile.Read(ref _state) == 1)
                return true;
            if (System.Threading.Volatile.Read(ref _state) == -1)
                return false;
            lock (NativeGate)
            {
                if (_state != 0)
                    return _state == 1;
                if (!Environment.Is64BitProcess)
                {
                    _state = -1;
                    return false;
                }
                if (TryLoadBridge(out IntPtr handle))
                {
                    _handle = handle;
                    _state = 1;
                    Log.WriteInfo("MEGA account module loaded.");
                    return true;
                }
                _state = -1;
                return false;
            }
        }

        private static bool TryLoadBridge(out IntPtr handle)
        {
            handle = IntPtr.Zero;
            string fileName = OperatingSystem.IsWindows()
                ? "MegaAccountBridge.dll"
                : OperatingSystem.IsMacOS() ? "libMegaAccountBridge.dylib" : "libMegaAccountBridge.so";
            foreach (string directory in CandidateDirectories())
            {
                string path = Path.Combine(directory, fileName);
                if (!File.Exists(path))
                    continue;
                try
                {
                    handle = OperatingSystem.IsWindows() ? LoadWindows(path) : NativeLibrary.Load(path);
                    if (handle != IntPtr.Zero)
                        return true;
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Could not load the MEGA account module from " + path + ": " + Log.SafeException(ex));
                }
            }

            // Let the OS search its own paths as a last resort (development builds that
            // put the library on PATH or in the dynamic-loader search path).
            try
            {
                if (NativeLibrary.TryLoad(fileName, out handle))
                    return true;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Could not search for the MEGA account module: " + Log.SafeException(ex));
            }
            return false;
        }

        private static System.Collections.Generic.IEnumerable<string> CandidateDirectories()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string configured = Environment.GetEnvironmentVariable("MEGADOWNLOADER_NATIVE_DIR");
            if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured))
                yield return Path.GetFullPath(configured);

            yield return baseDirectory;
            yield return Path.Combine(baseDirectory, "native");
            yield return Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");

            if (OperatingSystem.IsMacOS())
            {
                // Files inside MegaDownloader.app: Contents/MacOS (base) and the sibling
                // Frameworks/Resources directories.
                yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "Frameworks"));
                yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "Resources"));
            }
            else if (OperatingSystem.IsLinux())
            {
                yield return "/usr/lib/megadownloader";
                yield return "/usr/local/lib/megadownloader";
            }

            string dataRoot = ApplicationDataDirectory.GetLegacyRoot();
            yield return Path.Combine(dataRoot, "native", typeof(MegaAccountHandle).Assembly.GetName().Version?.ToString() ?? "1");
        }

        private static IntPtr LoadWindows(string path)
        {
            // Resolve the VC++ runtime from the module's own directory for this load.
            return LoadLibraryExW(path, IntPtr.Zero, 0x00000008);
        }

        private static void InstallResolver()
        {
            if (_resolverInstalled)
                return;
            _resolverInstalled = true;
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(MegaAccountNative).Assembly,
                    (libraryName, assembly, searchPath) =>
                    {
                        if (!string.Equals(libraryName, Library, StringComparison.OrdinalIgnoreCase) &&
                            !libraryName.Contains("MegaAccountBridge", StringComparison.OrdinalIgnoreCase))
                        {
                            return IntPtr.Zero;
                        }
                        return EnsureNative() ? _handle : IntPtr.Zero;
                    });
            }
            catch (InvalidOperationException)
            {
                // Another part of the process installed a resolver for this assembly.
            }
        }

        [DllImport("kernel32", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string path, IntPtr reserved, uint flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void Progress(long received, long total, long speed);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int Cancelled();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate long DownloadLimit();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int DriveNode(ulong id, ulong parent, [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            int folder, long bytes, long modified);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int StreamData(IntPtr buffer, int count, long speed);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern MegaAccountHandle md_open(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string cache,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string proxy,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string user,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string password);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void md_close(IntPtr handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_set_proxy(MegaAccountHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string user,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string password);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void md_free_string(IntPtr value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_sign_in(MegaAccountHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string email,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pin,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string session);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr md_session(MegaAccountHandle handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_sign_out(MegaAccountHandle handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_account_details(MegaAccountHandle handle, out int plan, out long storageUsed,
            out long storageTotal, out long transferUsed, out long transferTotal);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_inspect(MegaAccountHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string link, [Out] byte[] name, int capacity, out long size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_list_drive(MegaAccountHandle handle, DriveNode visit, out ulong root);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_list_shares(MegaAccountHandle handle, DriveNode visit, out ulong root);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_inspect_node(MegaAccountHandle handle, ulong node, [Out] byte[] name,
            int capacity, out long size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_download_with_limit(MegaAccountHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string link,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path, Progress progress, Cancelled cancel,
            out long quotaWaitSeconds, DownloadLimit readLimit);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_inspect_node_for_resume(MegaAccountHandle handle, ulong node,
            [Out] byte[] name, int nameCapacity, out long size, [Out] byte[] key, int keyCapacity);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int md_stream_node_with_limit(MegaAccountHandle handle, ulong node,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key, long size, long offset, StreamData write,
            Cancelled cancel, out long quotaWaitSeconds, DownloadLimit readLimit);
    }
}
