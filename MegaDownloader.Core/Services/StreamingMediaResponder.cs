using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MegaDownloader
{
    /// <summary>
    /// Serves <c>/streaming</c>: resolves a MEGA file, opens a ranged upstream request and
    /// streams the decrypted bytes back with RFC 7233 semantics.
    /// </summary>
    /// <remarks>
    /// This is the Kestrel replacement for the embedded HttpServer <c>StreamingModule</c>.
    /// The range arithmetic, the upstream-alignment trick needed for CTR decryption and the
    /// status-code choices are carried over unchanged; only the transport changed.
    /// </remarks>
    public sealed class StreamingMediaResponder
    {
        internal const string PageStreaming = "/streaming";

        private static readonly object UrlsLock = new object();
        private static readonly Dictionary<string, KeyValuePair<DateTime, Conexion.InformacionFichero>> Urls =
            new Dictionary<string, KeyValuePair<DateTime, Conexion.InformacionFichero>>(StringComparer.Ordinal);
        private const int MaxUrlCacheEntries = 64;
        private static readonly Regex RxRange = new Regex(@"bytes=(\d*)-(\d*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly Configuracion _config;

        public StreamingMediaResponder(Configuracion config)
        {
            _config = config;
        }

        /// <summary>Test seam: lets a caller pre-populate the file-information cache.</summary>
        public static Dictionary<string, KeyValuePair<DateTime, Conexion.InformacionFichero>> Cache => Urls;

        internal async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            try
            {
                if (!CheckPassword(context, out string passwordError))
                {
                    await WriteTextAsync(context, StatusCodes.Status403Forbidden, passwordError, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!ResolveFile(context, out string fileId, out string fileKey, out int resolveStatus, out string resolveError))
                {
                    await WriteTextAsync(context, resolveStatus, resolveError, cancellationToken).ConfigureAwait(false);
                    return;
                }

                Conexion.InformacionFichero info = GetFileInfo(fileId, fileKey);
                if (info is null)
                {
                    await WriteTextAsync(context, StatusCodes.Status502BadGateway, "Error: " + resolveError, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                fileKey = info.FileKey;
                // Folder links carry the display name after the key.
                int marker = fileKey.IndexOf("=###n=", StringComparison.Ordinal);
                if (marker >= 0)
                    fileKey = fileKey.Substring(0, marker);

                await StreamAsync(context, info, fileKey, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client went away; nothing to report.
            }
            catch (Exception ex)
            {
                Log.WriteError("Streaming failed: " + Log.SafeException(ex));
                if (!context.Response.HasStarted)
                {
                    try
                    {
                        await WriteTextAsync(context, StatusCodes.Status502BadGateway, "Streaming failed.", cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception sendEx)
                    {
                        Log.WriteError("Streaming: could not send the error response: " + Log.SafeException(sendEx));
                    }
                }
            }
        }

        private bool CheckPassword(HttpContext context, out string error)
        {
            error = null;
            string configured = _config.ServidorStreamingPassword;
            if (string.IsNullOrEmpty(configured))
                return true;

            string supplied = context.Request.Query["p"];
            if (supplied is not null &&
                Criptografia.FixedTimeEquals(Criptografia.Utf8BytesOrNull(supplied), Criptografia.Utf8BytesOrNull(configured)))
            {
                return true;
            }

            // The password is a query parameter rather than a challenge, so 403 is the
            // honest answer: a 401 without WWW-Authenticate would only confuse players.
            error = "Error: Access denied";
            return false;
        }

        private bool ResolveFile(HttpContext context, out string fileId, out string fileKey, out int status, out string error)
        {
            fileId = string.Empty;
            fileKey = string.Empty;
            status = StatusCodes.Status400BadRequest;
            error = "Missing FileID and/or FileKey";

            string mega = context.Request.Query["mega"];
            if (!string.IsNullOrEmpty(mega))
            {
                fileKey = SplitStreamingMega(mega, 2);
                fileId = SplitStreamingMega(mega, 1);
                return true;
            }

            string id = context.Request.Query["id"];
            if (!string.IsNullOrEmpty(id))
            {
                LibraryElement element = StreamingLibraryManager.GetElementByID(id);
                if (element is null)
                {
                    status = StatusCodes.Status404NotFound;
                    error = "Invalid ID";
                    return false;
                }
                string link = Criptografia.ToInsecureString(element.Link);
                fileKey = Fichero.ExtraerFileKey(link);
                fileId = Fichero.ExtraerFileID(link);
                return true;
            }

            string temp = context.Request.Query["t"];
            if (!string.IsNullOrEmpty(temp))
            {
                string resolvedId = string.Empty;
                string resolvedKey = string.Empty;
                if (!StreamingHelper.GetFileDataFromTempID(temp, ref resolvedId, ref resolvedKey))
                {
                    status = StatusCodes.Status404NotFound;
                    error = "Invalid ID";
                    return false;
                }
                fileId = resolvedId;
                fileKey = resolvedKey;
                return true;
            }

            if (string.IsNullOrEmpty(fileId))
            {
                status = StatusCodes.Status400BadRequest;
                error = "Missing FileID and/or FileKey";
                return false;
            }

            return true;
        }

        private Conexion.InformacionFichero GetFileInfo(string fileId, string fileKey)
        {
            string cacheKey = fileId + "|" + fileKey;
            lock (UrlsLock)
            {
                PruneUrlCacheUnlocked();
                if (Urls.TryGetValue(cacheKey, out var cached) && cached.Key >= DateTime.Now)
                    return cached.Value;
            }

            Conexion.InformacionFichero info = Conexion.ObtenerInformacionFichero(_config, fileId, fileKey, true);
            if (info is null || info.Err != Conexion.TipoError.SinErrores)
                return null;

            lock (UrlsLock)
                Urls[cacheKey] = new KeyValuePair<DateTime, Conexion.InformacionFichero>(DateTime.Now.AddMinutes(3d), info);
            return info;
        }

        private async Task StreamAsync(HttpContext context, Conexion.InformacionFichero info, string fileKey, CancellationToken cancellationToken)
        {
            Criptografia.SicSeekableBlockCipher cipher = Criptografia.GetInstaceCipher(fileKey);

            long rangeStart = 0L;
            long rangeEnd = 0L;
            long requestRangeStart = 0L;
            long requestRangeEnd = 0L;
            long content = 0L;
            int status = RangeAdjust(ref rangeStart, ref rangeEnd, ref requestRangeStart, ref requestRangeEnd, ref content,
                context.Request.Headers.Range.ToString(), info.Tamano);

            if (status == StatusCodes.Status416RangeNotSatisfiable)
            {
                context.Response.StatusCode = status;
                context.Response.Headers.ContentRange = "bytes */" + info.Tamano.ToString();
                context.Response.ContentLength = 0L;
                return;
            }

            HttpWebRequest upstream = Conexion.CreateHttpWebRequest(info.URL);
            // AddRange takes longs, so the >2 GB reflection hack the old module needed is gone.
            upstream.AddRange(rangeStart, rangeEnd);

            using var webResponse = (HttpWebResponse)upstream.GetResponse();
            using Stream source = webResponse.GetResponseStream();

            cipher.SeekToFileOffset(rangeStart);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength = content;
            context.Response.Headers.ContentDisposition = ContentDispositionHeader(info.Nombre);
            context.Response.Headers.CacheControl = "private";
            if (info.Tamano > 0L)
                context.Response.Headers.AcceptRanges = "bytes";
            if (status == StatusCodes.Status206PartialContent)
                context.Response.Headers.ContentRange = "bytes " + requestRangeStart + "-" + requestRangeEnd + "/" + info.Tamano;

            const int packageSize = 1024 * 8 * 2;
            long downloadIndex = 0L;
            byte[] readBytes = new byte[packageSize];
            byte[] decrypted = new byte[packageSize + 16];
            Stream body = context.Response.Body;

            while (downloadIndex < content)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int skip = downloadIndex == 0L ? (int)(requestRangeStart - rangeStart) : 0;
                int wanted = (int)Math.Min(packageSize, content - downloadIndex + skip);
                int currentPackageSize = 0;
                bool finishAfterSend = false;
                // Read enough ciphertext for both the requested bytes and the aligned
                // prefix that gets discarded from the first decrypted block.
                while (currentPackageSize < wanted)
                {
                    int got = await source.ReadAsync(readBytes.AsMemory(currentPackageSize, wanted - currentPackageSize), cancellationToken)
                        .ConfigureAwait(false);
                    if (got == 0)
                    {
                        finishAfterSend = true;
                        break;
                    }
                    currentPackageSize += got;
                }

                // Upstream ended early (node/proxy truncation). Delivering nothing more
                // would spin forever, so finish cleanly with what was already sent.
                if (currentPackageSize == 0)
                {
                    Log.WriteWarning("Streaming: upstream ended early (" + downloadIndex + "/" + content + " bytes sent); finishing response.");
                    break;
                }

                if (currentPackageSize <= skip)
                    break;

                int paddedSize = (currentPackageSize + 15) / 16 * 16;
                // CTR can decrypt a final partial block with zero padding, but only the
                // bytes actually read may be sent to the client.
                Array.Clear(readBytes, currentPackageSize, paddedSize - currentPackageSize);
                if (decrypted.Length < paddedSize)
                    decrypted = new byte[paddedSize];
                for (int offset = 0; offset < paddedSize; offset += cipher.GetBlockSize())
                    cipher.ProcessBlock(readBytes, offset, decrypted, offset);

                int sendCount = (int)Math.Min(currentPackageSize - skip, content - downloadIndex);
                if (sendCount <= 0)
                    break;

                await body.WriteAsync(decrypted.AsMemory(skip, sendCount), cancellationToken).ConfigureAwait(false);
                // Advance by the bytes actually sent: the first package trims the aligned
                // prefix and the last is truncated to the declared length.
                downloadIndex += sendCount;

                if (finishAfterSend)
                    break;
            }
        }

        /// <summary>
        /// Calculates the client range and the upstream range aligned for decryption.
        /// Returns the response status code; writes nothing.
        /// </summary>
        private static int RangeAdjust(ref long rangeStart, ref long rangeEnd, ref long requestRangeStart,
            ref long requestRangeEnd, ref long content, string rangeHeader, long fileSize)
        {
            rangeStart = 0L;
            rangeEnd = 0L;
            // A Range header this server cannot parse must be ignored per RFC 7233 §3.1:
            // answer 200 with the full body. Treating it as 0-0 used to promise the whole
            // file in Content-Range while only a 16-byte block was ever sent.
            bool rangeIgnored = false;
            // bytes=0-0 is a single-byte probe with both ends explicit; it must not be
            // confused with the open-ended "bytes=N-" form below.
            bool singleByteProbe = false;

            if (!string.IsNullOrEmpty(rangeHeader))
            {
                Match match = RxRange.Match(rangeHeader);
                if (match.Success)
                {
                    bool hasStart = !string.IsNullOrEmpty(match.Groups[1].Value);
                    bool hasEnd = !string.IsNullOrEmpty(match.Groups[2].Value);
                    if (!hasStart && !hasEnd)
                    {
                        // "bytes=-" matches the regex but is invalid: a suffix length is required.
                        rangeIgnored = true;
                        Log.WriteWarning("Streaming: ignoring invalid Range header '" + rangeHeader + "' (empty byte-range-spec); serving the full file (RFC 7233 §3.1).");
                    }
                    else
                    {
                        if (hasStart)
                            long.TryParse(match.Groups[1].Value, out rangeStart);
                        if (hasEnd)
                            long.TryParse(match.Groups[2].Value, out rangeEnd);

                        if (hasStart && hasEnd && rangeStart == 0L && rangeEnd == 0L)
                            singleByteProbe = true;

                        if (!hasStart && hasEnd)
                        {
                            // bytes=-N: the last N bytes.
                            rangeStart = Math.Max(0L, fileSize - rangeEnd);
                            rangeEnd = fileSize - 1L;
                        }
                        else if (hasStart && !hasEnd)
                        {
                            rangeEnd = fileSize - 1L;
                        }
                    }
                }
                else
                {
                    rangeIgnored = true;
                    Log.WriteWarning("Streaming: ignoring unparseable Range header '" + rangeHeader + "'; serving the full file (RFC 7233 §3.1).");
                }
            }

            requestRangeStart = rangeStart;
            requestRangeEnd = rangeEnd;

            // A start past the end of the file must be answered 416, not silently clamped.
            if (fileSize > 0L && requestRangeStart > fileSize - 1L)
                return StatusCodes.Status416RangeNotSatisfiable;

            if (rangeStart % 16L != 0L)
                rangeStart -= rangeStart % 16L;
            if (rangeEnd % 16L != 0L)
                rangeEnd = rangeEnd - rangeEnd % 16L + 16L;
            if (rangeStart == rangeEnd && rangeStart != 0L)
                rangeEnd += 16L;

            if (fileSize <= 0L)
            {
                rangeStart = 0L;
                rangeEnd = 0L;
                requestRangeStart = 0L;
                requestRangeEnd = 0L;
                content = 0L;
                return StatusCodes.Status200OK;
            }

            if (rangeEnd == 0L)
            {
                if (rangeIgnored || string.IsNullOrEmpty(rangeHeader))
                    rangeEnd = fileSize - 1L;
                else
                    rangeEnd = Math.Min(rangeStart + 15L, fileSize - 1L);
            }

            // bytes=0-0: the end is explicit, so it must not be widened to the file size.
            requestRangeEnd = singleByteProbe || requestRangeEnd > 0L ? requestRangeEnd : fileSize - 1L;

            if (rangeStart > fileSize - 1L)
                rangeStart = fileSize - 1L;
            if (rangeEnd > fileSize - 1L)
                rangeEnd = fileSize - 1L;
            if (requestRangeStart > fileSize - 1L)
                requestRangeStart = fileSize - 1L;
            if (requestRangeEnd > fileSize - 1L)
                requestRangeEnd = fileSize - 1L;
            if (rangeEnd < rangeStart)
                rangeEnd = rangeStart;
            if (requestRangeEnd < requestRangeStart)
                requestRangeEnd = requestRangeStart;

            content = requestRangeEnd - requestRangeStart + 1L;

            return rangeIgnored || string.IsNullOrEmpty(rangeHeader) ? StatusCodes.Status200OK : StatusCodes.Status206PartialContent;
        }

        private static string ContentDispositionHeader(string fileName)
        {
            string safe = Utils.RemoveDiacritics(fileName ?? string.Empty);
            safe = safe.Replace("\\", "_").Replace("\"", "'").Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (safe.Length == 0)
                safe = "download";
            return "attachment; filename=\"" + safe + "\"";
        }

        internal static async Task WriteTextAsync(HttpContext context, int status, string text, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            string acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
            if (!string.IsNullOrEmpty(acceptEncoding) &&
                acceptEncoding.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                using var compressed = new MemoryStream();
                using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
                    gzip.Write(bytes, 0, bytes.Length);
                bytes = compressed.ToArray();
                context.Response.Headers.ContentEncoding = "gzip";
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        private static void PruneUrlCacheUnlocked()
        {
            if (Urls.Count <= MaxUrlCacheEntries)
                return;
            foreach (string key in Urls.Where(pair => pair.Value.Key < DateTime.Now).Select(pair => pair.Key).ToList())
                Urls.Remove(key);
            if (Urls.Count <= MaxUrlCacheEntries)
                return;
            foreach (string key in Urls.OrderBy(pair => pair.Value.Key).Take(Urls.Count - MaxUrlCacheEntries).Select(pair => pair.Key).ToList())
                Urls.Remove(key);
        }

        /// <summary>Parses <c>!FileID!FileKey</c>; malformed values yield an empty string.</summary>
        internal static string SplitStreamingMega(string megaValue, int index)
        {
            if (string.IsNullOrEmpty(megaValue))
                return string.Empty;
            string[] parts = megaValue.Split('!');
            if (parts.Length != 3 || !string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                return string.Empty;
            return index is >= 1 and <= 2 ? parts[index] : string.Empty;
        }
    }
}
