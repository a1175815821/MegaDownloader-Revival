using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MegaDownloader
{

    public class StreamingHelper
    {

        private const int MaxTempCacheEntries = 500;
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, string> TempStreamingCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, DateTime> TempIdCreatedUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static int NextTempId = 1;

        public static bool WatchOnline(string VLCPath, string URLStreamning)
        {
            if (string.IsNullOrEmpty(URLStreamning))
                return false;
            // Only allow local streaming URLs to avoid argument injection / remote launch abuse
            Uri uri = null;
            if (!Uri.TryCreate(URLStreamning, UriKind.Absolute, out uri))
                return false;
            if ((uri.Scheme ?? "") != (Uri.UriSchemeHttp ?? "") && (uri.Scheme ?? "") != (Uri.UriSchemeHttps ?? ""))
                return false;
            if (!(uri.Host == "127.0.0.1" || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                return false;

            string exe = ResolveVlcExecutable(VLCPath);
            if (exe is null)
                return false;

            try
            {
                var p = new Process();
                p.StartInfo.FileName = exe;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.ArgumentList.Add(URLStreamning);
                p.Start();
                return true;
            }
            catch (Exception ex)
            {
                // 启动失败(权限/文件损坏等)不让异常裸抛到 UI 造成闪退,调用方据返回值提示用户
                Log.WriteError("Failed to start VLC: " + ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// VLC's executable on this platform: the configured folder or binary first, then
        /// the usual install location (macOS bundle) and PATH.
        /// </summary>
        private static string ResolveVlcExecutable(string vlcpPath)
        {
            if (!string.IsNullOrWhiteSpace(vlcpPath))
            {
                var candidates = new List<string>();
                if (OperatingSystem.IsWindows())
                {
                    candidates.Add(System.IO.Path.Combine(vlcpPath, "vlc.exe"));
                    candidates.Add(System.IO.Path.Combine(vlcpPath, "vlcportable.exe"));
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // .../VLC.app, .../VLC.app/Contents/MacOS, or that folder itself.
                    candidates.Add(System.IO.Path.Combine(vlcpPath, "Contents", "MacOS", "VLC"));
                    candidates.Add(System.IO.Path.Combine(vlcpPath, "VLC"));
                    candidates.Add(System.IO.Path.Combine(vlcpPath, "VLC.app", "Contents", "MacOS", "VLC"));
                }
                else
                {
                    candidates.Add(System.IO.Path.Combine(vlcpPath, "vlc"));
                }

                foreach (string candidate in candidates)
                {
                    if (System.IO.File.Exists(candidate))
                        return candidate;
                }
                if (System.IO.File.Exists(vlcpPath) && !vlcpPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return vlcpPath;
            }

            if (OperatingSystem.IsMacOS() &&
                System.IO.File.Exists("/Applications/VLC.app/Contents/MacOS/VLC"))
                return "/Applications/VLC.app/Contents/MacOS/VLC";

            if (!OperatingSystem.IsWindows())
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (string directory in path.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = System.IO.Path.Combine(directory, "vlc");
                    if (System.IO.File.Exists(candidate))
                        return candidate;
                }
            }
            return null;
        }

        public static bool GetFileDataFromTempID(string TempID, ref string FileID, ref string FileKey)
        {
            lock (CacheLock)
            {
                PruneTempCacheUnlocked();
                if (!TempStreamingCache.ContainsKey(TempID))
                    return false;
                string Key = TempStreamingCache[TempID];
                if (string.IsNullOrEmpty(Key) || !Key.Contains("|"))
                    return false;
                FileID = Key.Split('|')[0];
                FileKey = Key.Split('|')[1];
                return true;
            }
        }

        public static string CreateStreamingLink(string URLMega, int StreamingPort, ref Configuracion Config)
        {

            var listaURLs = new List<string>();
            listaURLs.Add(URLMega);
            var listaURLs2 = URLProcessor.ProcessURLs(listaURLs, ref Config);
            if (listaURLs2.Count > 1)
            {
                UserNotification.ShowInfo(Language.GetText("The link is a folder with %NUM files. Now only the first will be used, if you want to use all, import the link into the streaming library.").Replace("%NUM", listaURLs2.Count.ToString()), Language.GetText("Note"));

            }
            else if (listaURLs2.Count == 0)
            {
                return string.Empty;
            }

            string FileID = Fichero.ExtraerFileID(listaURLs2[0].URL);
            string FileKey = Fichero.ExtraerFileKey(listaURLs2[0].URL);

            if (string.IsNullOrEmpty(FileID))
                return string.Empty;

            string key = FileID + "|" + FileKey;
            string TempID = "";
            lock (CacheLock)
            {
                PruneTempCacheUnlocked();
                if (TempStreamingCache.ContainsKey(key))
                {
                    TempID = TempStreamingCache[key];
                }
                else
                {
                    TempID = Interlocked.Increment(ref NextTempId).ToString();
                    TempStreamingCache[key] = TempID;
                    TempStreamingCache[TempID] = key;
                    TempIdCreatedUtc[TempID] = DateTime.UtcNow;
                }
            }

            // Fixed loopback origin — never trust Host / Authority from clients
            string URLStreaming = "http://127.0.0.1:" + StreamingPort.ToString() + "/streaming?t=" + TempID;
            if (!string.IsNullOrEmpty(Config.ServidorStreamingPassword))
            {
                URLStreaming += "&p=" + Uri.EscapeDataString(Config.ServidorStreamingPassword);
            }

            return URLStreaming;
        }

        public static string CreateStreamingLinkFromLibrary(string ID, string CurrentURL, ref Configuracion Config)
        {
            if (StreamingLibraryManager.GetElementByID(ID) is null)
            {
                return string.Empty;
            }

            // Ignore client-supplied Host; always bind media URLs to loopback
            int port = Config.ServidorStreamingPuerto;
            string URLStreaming = "http://127.0.0.1:" + port.ToString() + "/streaming?id=" + Uri.EscapeDataString(ID);
            if (!string.IsNullOrEmpty(Config.ServidorStreamingPassword))
            {
                URLStreaming += "&p=" + Uri.EscapeDataString(Config.ServidorStreamingPassword);
            }

            return URLStreaming;
        }

        public static string LibraryManagerURL(int StreamingPort, bool Manage)
        {
            string URL = "http://127.0.0.1:" + StreamingPort.ToString();
            if (Manage)
            {
                URL += StreamingLibraryRoutes.PaginaManagement;
            }
            else
            {
                URL += StreamingLibraryRoutes.PaginaMain;
            }
            return URL;
        }

        private static void PruneTempCacheUnlocked()
        {
            if (TempStreamingCache.Count <= MaxTempCacheEntries)
                return;
            var ordered = TempIdCreatedUtc.OrderBy(kv => kv.Value).ToList();
            int toRemove = Math.Max(1, ordered.Count / 4);
            for (int i = 0, loopTo = Math.Min(toRemove, ordered.Count) - 1; i <= loopTo; i++)
            {
                string tempId = ordered[i].Key;
                string fileKey = null;
                if (TempStreamingCache.TryGetValue(tempId, out fileKey))
                {
                    TempStreamingCache.Remove(tempId);
                    if (fileKey is not null)
                        TempStreamingCache.Remove(fileKey);
                }
                TempIdCreatedUtc.Remove(tempId);
            }
        }

    }
}