
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using HttpServer;
using HttpServer.HttpModules;
using HttpServer.Sessions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{


    public class StreamingModule : HttpModule
    {


        private Configuracion _Config;
        public const string PaginaStreaming = "/streaming";

        public StreamingModule(ref Configuracion Config)
        {
            _Config = Config;
        }



        private static readonly object UrlsLock = new object();
        private static Dictionary<string, KeyValuePair<DateTime, Conexion.InformacionFichero>> Urls = new Dictionary<string, KeyValuePair<DateTime, Conexion.InformacionFichero>>();
        private const int MaxUrlCacheEntries = 64;
        // B3-⑧:Range 解析正则单例,每次流式请求不再 New Regex。
        private static readonly System.Text.RegularExpressions.Regex rxRange = new System.Text.RegularExpressions.Regex(@"bytes=(\d*)-(\d*)", System.Text.RegularExpressions.RegexOptions.Compiled);

        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)

        {

            try
            {

                if (!IsStreaming(ref request))
                    return false;


                if (!string.IsNullOrEmpty(_Config.ServidorStreamingPassword))
                {
                    if (request.Param["p"] is null || !Criptografia.FixedTimeEquals(Criptografia.Utf8BytesOrNull(request.Param["p"].Value), Criptografia.Utf8BytesOrNull(_Config.ServidorStreamingPassword)))
                    {
                        // 403 而非 200:密码是 query 参数而非质询式认证,401 需要
                        // WWW-Authenticate 头才有意义;403 让播放器/浏览器按真正的
                        // 错误处理,而不是把 "Error: Access denied" 文本当媒体数据
                        response.Status = HttpStatusCode.Forbidden;
                        WriteTextResponse(request, response, "Error: Access denied");
                        return true;
                    }
                }


                string FileKey = string.Empty;
                string FileID = string.Empty;
                if (request.Param["mega"] is not null && !string.IsNullOrEmpty(request.Param["mega"].Value))
                {
                    // http://localhost:5000/streaming?mega=!vNFx2D4Y!a1uUcgUF0OV6quLPf2UT83PvGKo_VvTgG4hl-yk0FdU
                    // 直接使用框架解析好的参数值:此前手动 Substring(IndexOf("?")+6) 只认得
                    // mega 恰好是第一个查询参数的情形(?p=密码&mega=... 会解析失败返回空)
                    string megaValue = request.Param["mega"].Value;
                    FileKey = ExtraerStreamingFileKey(megaValue);
                    FileID = ExtraerStreamingFileID(megaValue);
                }
                else if (request.Param["id"] is not null && !string.IsNullOrEmpty(request.Param["id"].Value))
                {
                    var ele = StreamingLibraryManager.GetElementByID(request.Param["id"].Value);
                    if (ele is null)
                    {
                        // 第16项:错误分支也要设 4xx,200+文本体会被播放器当媒体数据
                        response.Status = HttpStatusCode.NotFound;
                        WriteTextResponse(request, response, "Invalid ID");
                        return true;
                    }
                    string link = Criptografia.ToInsecureString(ele.Link);
                    FileKey = Fichero.ExtraerFileKey(link);
                    FileID = Fichero.ExtraerFileID(link);
                }
                else if (request.Param["t"] is not null && !string.IsNullOrEmpty(request.Param["t"].Value))
                {
                    if (!StreamingHelper.GetFileDataFromTempID(request.Param["t"].Value, ref FileID, ref FileKey))
                    {
                        // 第16项:同上,错误分支设 4xx
                        response.Status = HttpStatusCode.NotFound;
                        WriteTextResponse(request, response, "Invalid ID");
                        return true;
                    }
                }


                if (string.IsNullOrEmpty(FileID))
                {
                    // 第16项:错误分支设 4xx,防播放器把错误文本当媒体数据
                    response.Status = HttpStatusCode.BadRequest;
                    WriteTextResponse(request, response, "Missing FileID and/or FileKey");
                    return true;
                }

                // Cache key must not include password/secret query values
                string cacheKey = FileID + "|" + FileKey;

                Conexion.InformacionFichero InfoFichero = null;
                lock (UrlsLock)
                {
                    PruneUrlCacheUnlocked();
                    if (Urls.ContainsKey(cacheKey) && Urls[cacheKey].Key >= DateTime.Now)
                    {
                        InfoFichero = Urls[cacheKey].Value;
                    }
                }

                if (InfoFichero is null)
                {
                    InfoFichero = Conexion.ObtenerInformacionFichero(_Config, FileID, FileKey, true);
                    if (InfoFichero is null || InfoFichero.Err != Conexion.TipoError.SinErrores)
                    {
                        string errTxt = InfoFichero is null ? "Unknown" : InfoFichero.Errtxt;
                        // 第16项:上游 MEGA 取文件信息失败同样不能回 200,502 表示网关侧错误
                        response.Status = HttpStatusCode.BadGateway;
                        WriteTextResponse(request, response, "Error: " + errTxt);
                        return true;
                    }
                    lock (UrlsLock)
                        Urls[cacheKey] = new KeyValuePair<DateTime, Conexion.InformacionFichero>(DateTime.Now.AddMinutes(3d), InfoFichero);
                }
                FileKey = InfoFichero.FileKey;
                FileID = InfoFichero.FileID;

                // Folder links (new format)
                if (FileKey.Contains("=###n="))
                {
                    FileKey = FileKey.Substring(0, FileKey.IndexOf("=###n="));
                }


                // Iniciamos la descarga!!
                HttpWebRequest webReq = null;
                HttpWebResponse webResp = null;
                var oCipher = Criptografia.GetInstaceCipher(FileKey);

                try
                {

                    // Range adjustment
                    long rangeStart = 0L;
                    long rangeEnd = 0L;
                    long requestRangeStart = 0L;
                    long requestRangeEnd = 0L;
                    long content = 0L;
                    var status = RangeAdjust(ref rangeStart, ref rangeEnd, ref requestRangeStart, ref requestRangeEnd, ref content, request, InfoFichero.Tamano);
                    if (status == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        response.Status = status;
                        response.AddHeader("Content-Range", "bytes */" + InfoFichero.Tamano.ToString());
                        response.ContentLength = 0L;
                        return true;
                    }


                    var disposition = new System.Net.Mime.ContentDisposition() { FileName = Utils.RemoveDiacritics(InfoFichero.Nombre) };

                    webReq = Conexion.CreateHttpWebRequest(InfoFichero.URL);

                    // Pequeño hack para soportar ficheros de más de 2 GB:
                    // http://forums.codeguru.com/showthread.php?467570-WebRequest.AddRange-what-about-files-gt-2gb&p=1794639
                    // http://www.freesoft.org/CIE/RFC/2068/198.htm
                    string key = "Range";
                    string val = string.Format("bytes={0}-{1}", rangeStart, rangeEnd);

                    var @method = typeof(WebHeaderCollection).GetMethod("AddWithoutValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    @method.Invoke(webReq.Headers, new object[] { key, val });

                    webResp = (HttpWebResponse)webReq.GetResponse();


                    oCipher.SeekToFileOffset(rangeStart);



                    int PackageSize = 1024 * 8 * 2;
                    long DownloadIndex = 0L;
                    int currentPackageSize = 0;
                    var readBytes = new byte[PackageSize];

                    var Stream = webResp.GetResponseStream();
                    try
                    {
                        // Publish media headers only after opening the upstream stream.
                        response.Status = status;
                        response.Connection = ConnectionType.Close;
                        response.ContentLength = content;
                        response.ContentType = "application/octet-stream";
                        response.AddHeader("Content-Disposition", disposition.ToString());
                        response.AddHeader("Cache-Control", "private");
                        if (InfoFichero.Tamano > 0L)
                            response.AddHeader("Accept-Ranges", "bytes");
                        if (status == HttpStatusCode.PartialContent)
                        {
                            response.AddHeader("Content-Range", "bytes " + requestRangeStart + "-" + requestRangeEnd + "/" + InfoFichero.Tamano);
                        }
                        response.SendHeaders();

                        bool finishAfterSend = false;
                        // While DownloadIndex < InfoFichero.Tamano And ClientConnected(response)
                        while (DownloadIndex < content & ClientConnected(response))
                        {

                            int skip = DownloadIndex == 0L ? (int)(requestRangeStart - rangeStart) : 0;
                            int wanted = (int)Math.Min(PackageSize, content - DownloadIndex + skip);
                            currentPackageSize = 0;
                            // Read enough ciphertext for both the requested bytes and the
                            // aligned prefix discarded from the first decrypted block.
                            while (currentPackageSize < wanted)
                            {
                                int got = Stream.Read(readBytes, currentPackageSize, wanted - currentPackageSize);
                                if (got == 0)
                                {
                                    finishAfterSend = true;
                                    break;
                                }
                                currentPackageSize += got;
                            }

                            // EOF 防护:上游流提前结束(MEGA 节点/代理截断响应)时 Read=0。
                            // 旧代码以零长 Buffer 空转发,DownloadIndex 永不推进 → 死循环占满线程,
                            // 直到客户端断开;ClientConnected 反射降级为"视为已连接"时永不停止。
                            // 已送达字节正常交付,在当前位置干净收尾。
                            if (currentPackageSize == 0)
                            {
                                Log.WriteWarning("StreamingModule: upstream ended early (" + DownloadIndex + "/" + content + " bytes sent); finishing response.");
                                break;
                            }

                            if (currentPackageSize <= skip)
                                break;
                            int paddedSize = (currentPackageSize + 15) / 16 * 16;
                            // CTR can decrypt a final partial block with zero padding, but
                            // only the bytes actually read may be sent to the client.
                            Array.Clear(readBytes, currentPackageSize, paddedSize - currentPackageSize);
                            var decrypted = new byte[paddedSize];
                            for (int tempIndex = 0, loopTo = paddedSize - 1; oCipher.GetBlockSize() >= 0 ? tempIndex <= loopTo : tempIndex >= loopTo; tempIndex += oCipher.GetBlockSize())
                                oCipher.ProcessBlock(readBytes, tempIndex, decrypted, tempIndex);
                            int sendCount = (int)Math.Min(currentPackageSize - skip, content - DownloadIndex);
                            var Buffer = new byte[sendCount];
                            System.Buffer.BlockCopy(decrypted, skip, Buffer, 0, sendCount);

                            response.SendBody(Buffer);

                            // P2-3(a): advance by the bytes ACTUALLY sent. The first package
                            // trims up to 15 bytes from the front (requestRangeStart -
                            // rangeStart) and the last one is truncated to content, so
                            // currentPackageSize over-counts what the client received. The
                            // inflated DownloadIndex made the final truncation cut skip extra
                            // bytes, so the client got fewer bytes than the declared
                            // Content-Length. Buffer.Length is post-trim/truncation = sent bytes.
                            DownloadIndex += Buffer.Length;

                            // 块中间 EOF:已按 16 字节边界发完最后一段,到此收尾
                            if (finishAfterSend)
                                break;

                        }
                    }
                    catch (Exception ex)
                    {
                        string ErrMsg = ex.ToString();
                        throw;
                    }
                    finally
                    {
                        Stream.Close();
                    }
                }


                catch (Exception ex)
                {
                    string ErrMsg = ex.ToString();
                    throw;
                }
                finally
                {

                    if (webResp is not null)
                        webResp.Close();
                }
                return true;
            }

            catch (Exception ex)
            {
                Log.WriteError("Error in StreamingModule: " + ex.ToString());
                try
                {
                    if (!response.HeadersSent)
                    {
                        response.Status = HttpStatusCode.BadGateway;
                        WriteTextResponse(request, response, "Streaming failed.");
                    }
                }
                catch (Exception sendEx)
                {
                    Log.WriteError("StreamingModule: could not send the error response: " + sendEx.ToString());
                }
                return true;
            }

        }


        /// <summary>Calculates the client range and the upstream range aligned for decryption.
    /// Does not write response headers.</summary>
        private HttpStatusCode RangeAdjust(ref long rangeStart, ref long rangeEnd, ref long requestRangeStart, ref long requestRangeEnd, ref long content, IHttpRequest request, long FileSize)


        {
            rangeStart = 0L;
            rangeEnd = 0L;
            // P2-3(b): a Range header this server cannot parse (wrong unit, garbage
            // syntax) must be IGNORED per RFC 7233 §3.1 — respond 200 with the full
            // body. Previously the un-parsed header left the 0/0 state, which then fell
            // through the "rangeEnd = 0" branch below: the INTERNAL range was clamped to
            // one 16-byte block while requestRangeEnd was defaulted to FileSize-1, so the
            // response promised the whole file (206 + full Content-Range) but only ~16
            // bytes were ever sent (upstream ended early → clean finish). Players saw a
            // file truncated to its declared full length.
            bool rangeIgnored = false;
            // bytes=0-0 单字节探测标志:两端显式为 0。必须与 "bytes=N-" 开区间(端点未指定)区分——
            // 下文 requestRangeEnd=0 时补 FileSize-1 的默认逻辑曾把 0-0 误当开区间,content 被算成
            // 整个文件,而上游只按对齐块取了 16 字节,主循环 Read=0 后无限空转发(必现死循环)。
            bool singleByteProbe = false;

            if (!string.IsNullOrEmpty(request.Headers["Range"]))
            {
                var mRange = rxRange.Match(request.Headers["Range"]);
                if (mRange.Success)
                {
                    // range = mRange.Groups[1].Value + "-" + mRange.Groups[2].Value;
                    bool hasStart = !string.IsNullOrEmpty(mRange.Groups[1].Value);
                    bool hasEnd = !string.IsNullOrEmpty(mRange.Groups[2].Value);
                    if (!hasStart && !hasEnd)
                    {
                        // P1-4: "bytes=-"(两组皆空)正则能匹配但按 RFC 7233 非法 ——
                        // suffix-length 必须是数字。旧代码把它当 0-0 之外的默认情形:
                        // 内部范围只取 16 字节对齐块,而 requestRangeEnd 被补成 FileSize-1,
                        // 206 + Content-Range 声称整个文件,实际只发 16 字节就 EOF,
                        // 播放器挂死等余下字节。与无法解析的头一样按 §3.1 忽略 → 200 全长。
                        rangeIgnored = true;
                        Log.WriteWarning("StreamingModule: ignoring invalid Range header '" + request.Headers["Range"] + "' (empty byte-range-spec); serving the full file (RFC 7233 §3.1).");
                    }
                    else
                    {
                        if (hasStart)
                        {
                            long.TryParse(mRange.Groups[1].Value, out rangeStart);
                        }
                        if (hasEnd)
                        {
                            long.TryParse(mRange.Groups[2].Value, out rangeEnd);
                        }
                        // 0-0:两端都显式给出了值且均为 0(RFC 7233 单字节探测),不是"未指定"
                        if (hasStart && hasEnd && rangeStart == 0L && rangeEnd == 0L)
                        {
                            singleByteProbe = true;
                        }
                        // RFC 7233 此前不支持的形式:
                        // bytes=-N  后缀区间 = 最后 N 个字节
                        // bytes=N- 开放区间 = 从 N 到文件末尾
                        if (!hasStart && hasEnd)
                        {
                            rangeStart = Math.Max(0L, FileSize - rangeEnd);
                            rangeEnd = FileSize - 1L;
                        }
                        else if (hasStart && !hasEnd)
                        {
                            rangeEnd = FileSize - 1L;
                        }
                    }
                }
                else
                {
                    rangeIgnored = true;
                    Log.WriteWarning("StreamingModule: ignoring unparseable Range header '" + request.Headers["Range"] + "'; serving the full file (RFC 7233 §3.1).");
                }
            }

            requestRangeStart = rangeStart;
            requestRangeEnd = rangeEnd;

            // RFC 7233:起始位置超出文件末尾必须回应 416(带 bytes */size),不能静默钳位
            if (FileSize > 0L && requestRangeStart > FileSize - 1L)
            {
                return HttpStatusCode.RequestedRangeNotSatisfiable;
            }

            if (rangeStart % 16L != 0L)
            {
                rangeStart = rangeStart - rangeStart % 16L;
            }
            if (rangeEnd % 16L != 0L)
            {
                rangeEnd = rangeEnd - rangeEnd % 16L + 16L;
            }
            if (rangeStart == rangeEnd && rangeStart != 0L)
            {
                rangeEnd += 16L;
            }


            if (FileSize <= 0L)
            {
                rangeStart = 0L;
                rangeEnd = 0L;
                requestRangeStart = 0L;
                requestRangeEnd = 0L;
                content = 0L;
                return HttpStatusCode.OK;
            }

            // rangeEnd=0 的三种来源:无 Range 头(取整个文件)、P2-3(b) 无法解析的 Range 头
            // (按 RFC 7233 忽略,同样取整个文件)、bytes=0-0 单字节探测(只取一个 16 字节
            // 对齐块)
            if (rangeEnd == 0L)
            {
                if (rangeIgnored || string.IsNullOrEmpty(request.Headers["Range"]))
                {
                    rangeEnd = FileSize - 1L;
                }
                else
                {
                    rangeEnd = Math.Min(rangeStart + 15L, FileSize - 1L);
                }
            }
            // bytes=0-0:端点显式为 0,不是"未指定",不能补 FileSize-1;content=1 才符合单字节探测语义
            requestRangeEnd = singleByteProbe || requestRangeEnd > 0L ? requestRangeEnd : FileSize - 1L;

            if (rangeStart > FileSize - 1L)
                rangeStart = FileSize - 1L;
            if (rangeEnd > FileSize - 1L)
                rangeEnd = FileSize - 1L;
            if (requestRangeStart > FileSize - 1L)
                requestRangeStart = FileSize - 1L;
            if (requestRangeEnd > FileSize - 1L)
                requestRangeEnd = FileSize - 1L;
            if (rangeEnd < rangeStart)
                rangeEnd = rangeStart;
            if (requestRangeEnd < requestRangeStart)
                requestRangeEnd = requestRangeStart;

            content = requestRangeEnd - requestRangeStart + 1L;

            return rangeIgnored || string.IsNullOrEmpty(request.Headers["Range"]) ? HttpStatusCode.OK : HttpStatusCode.PartialContent;
        }

        // 通过反射访问 HttpServer 库的私有字段 _context / Stream.Connected,
        // 因为该库未公开 "客户端是否仍连接" 的检查接口。成员查找结果静态缓存；
        // 每一步都做 Nothing 防护——库升级导致成员改名时降级为 "视为已连接"
        // (记一次日志),由后续写异常自然终止传输,而不是让每个流式响应直接 NRE 崩掉。
        private static System.Reflection.FieldInfo _ContextField;
        private static System.Reflection.PropertyInfo _StreamProp;
        private static System.Reflection.PropertyInfo _ConnectedProp;
        private static bool _ReflectBrokenLogged = false;

        private static bool ReflectBroken()
        {
            if (!_ReflectBrokenLogged)
            {
                _ReflectBrokenLogged = true;
                Log.WriteWarning("ClientConnected: cannot access HttpServer internals (_context/Stream/Connected) via reflection; assuming clients stay connected. Transfers will end on write errors instead.");
            }
            return true;
        }

        private bool ClientConnected(IHttpResponse response)
        {
            try
            {
                if (_ContextField is null)
                {
                    _ContextField = response.GetType().GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (_ContextField is null)
                    return ReflectBroken();

                var Contexto = _ContextField.GetValue(response);
                if (Contexto is null)
                    return ReflectBroken();

                if (_StreamProp is null)
                {
                    _StreamProp = Contexto.GetType().GetProperty("Stream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (_StreamProp is null)
                    return ReflectBroken();

                var St = _StreamProp.GetValue(Contexto, null);
                if (St is null)
                    return ReflectBroken();

                if (_ConnectedProp is null)
                {
                    _ConnectedProp = St.GetType().GetProperty("Connected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                if (_ConnectedProp is null)
                    return ReflectBroken();

                var ConnectedValue = _ConnectedProp.GetValue(St, null);
                if (ConnectedValue is null)
                    return ReflectBroken();

                return Conversions.ToBoolean(ConnectedValue);
            }
            catch (Exception ex)
            {
                return ReflectBroken();
            }
        }

        private static byte[] TextResponseBytes(IHttpRequest request, IHttpResponse response, string text)
        {
            byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(text);
            string acceptEncoding = request.Headers["Accept-Encoding"];
            if (string.IsNullOrEmpty(acceptEncoding) || !acceptEncoding.ToLowerInvariant().Contains("gzip"))
                return bytes;
            using (var compressed = new MemoryStream())
            {
                using (var gzip = new GZipStream(compressed, CompressionMode.Compress, true))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }
                byte[] result = compressed.ToArray();
                response.AddHeader("Content-Encoding", "gzip");
                return result;
            }
        }

        internal static void WriteTextResponse(IHttpRequest request, IHttpResponse response, string text)
        {
            byte[] bytes = TextResponseBytes(request, response, text);
            response.Connection = ConnectionType.Close;
            response.Chunked = false;
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength = bytes.Length;
            response.Body.Write(bytes, 0, bytes.Length);
        }

        private bool IsStreaming(ref IHttpRequest request)
        {
            return (request.Uri.LocalPath ?? "") == PaginaStreaming;
        }

        private static void PruneUrlCacheUnlocked()
        {
            var expired = Urls.Where(kv => kv.Value.Key < DateAndTime.Now).Select(kv => kv.Key).ToList();
            foreach (string k in expired)
                Urls.Remove(k);
            if (Urls.Count <= MaxUrlCacheEntries)
                return;
            var ordered = Urls.OrderBy(kv => kv.Value.Key).ToList();
            int removeCount = Urls.Count - MaxUrlCacheEntries;
            for (int i = 0, loopTo = removeCount - 1; i <= loopTo; i++)
                Urls.Remove(ordered[i].Key);
        }


        // B3:畸形 mega 参数(?mega=!abc / 多余 ! / 空)此前越界抛 IndexOutOfRange,
        // 外层 Catch 转 500。现返回 "" 走 "Missing FileID" 400 分支。
        // 合法形如 !FileID!FileKey → Split 为 {"", id, key}(首段必须空)。
        private static string[] SplitStreamingMega(string megaValue)
        {
            if (string.IsNullOrEmpty(megaValue))
                return null;
            string[] parts = megaValue.Split('!');
            if (parts.Length != 3)
                return null;
            if (!string.IsNullOrEmpty(parts[0]))
                return null;
            if (string.IsNullOrEmpty(parts[1]))
                return null;
            return parts;
        }

        /// <summary>从 streaming 链接的 mega 参数值(形如 !FileID!FileKey)提取 FileKey</summary>
        public static string ExtraerStreamingFileKey(string megaValue)
        {
            string[] parts = SplitStreamingMega(megaValue);
            if (parts is null)
                return "";
            return parts[2];
        }

        /// <summary>从 streaming 链接的 mega 参数值(形如 !FileID!FileKey)提取 FileID</summary>
        public static string ExtraerStreamingFileID(string megaValue)
        {
            string[] parts = SplitStreamingMega(megaValue);
            if (parts is null)
                return "";
            return parts[1];
        }


    }
}