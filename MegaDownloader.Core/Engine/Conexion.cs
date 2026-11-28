using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public class Conexion
    {

        // 是否使用 Global CDN (G 后缀的 URL) 还是 EU 区域 (E 后缀)。
        // 保留硬编码 True 的原因:Global CDN 通常更快、对全球用户更友好;
        // 改为可配置会让用户面对不熟悉的术语,误选 EU 反而拖慢速度。
        private static bool useGlobalCDN = true;

        // 进程内自增序列号,保证每次请求得到不同的 %SEQ% / %ID%
        // (原实现用 DateTime.Now.Millisecond 的 ticks,毫秒部分只有 0-999,
        // 短时间内并发请求会得到完全相同的值,不是真正的序列号)
        private static int _seqCounter = 0;

        private static string GetSequenceId()
        {
            int seq = System.Threading.Interlocked.Increment(ref _seqCounter);
            return (seq & int.MaxValue).ToString();
        }

        /// <summary>
    /// 防御性 JSON 字符串转义:FileID 来自链接解析,正常应为 base64url 字符集,
    /// 但畸形输入直接拼进 JSON 会产生非法请求,这里转义引号与反斜杠。
    /// </summary>
        private static string JsonEscapeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace(@"\", @"\\").Replace("\"", @"\""");
        }


        public enum TipoError
        {
            SinErrores,
            UsuarioInvalido,
            ErrorConexion,
            Otros,
            /// <summary>v2.5 beta: MEGA 配额耗尽(HTTP 509 / API -17)。调度器据此熔断而非普通重试。</summary>
            QuotaExceeded
        }

        public class InformacionFichero
        {
            public string URL;
            public string FileID;
            public string FileKey;
            public string Nombre;
            public string MD5;
            public long Tamano;
            public TipoError Err = TipoError.SinErrores;
            public string Errtxt;
        }

        internal class Respuesta
        {
            public HttpStatusCode Status;
            public string Mensaje;
            public Exception Excepcion;
            public string Cookies;
        }

        internal sealed class ProxySettings
        {
            public readonly Uri Address;
            public readonly string Username;
            public readonly string Password;

            public ProxySettings(Uri address, string username, string password)
            {
                Address = address;
                Username = username;
                Password = password;
            }
        }

        private static ProxySettings _proxy;

        public static void SetProxy(Configuracion Config)
        {
            ProxySettings settings = null;
            if (Config.UsarProxy)
            {
                Uri address = null;
                string host = NormalizeProxyHost(Config.ProxyIP);
                int port = Config.ProxyPort > 65535 ? 65535 : Config.ProxyPort < 1 ? 8080 : Config.ProxyPort;
                if (host is not null)
                    TryCreateProxyAddress(host, port, ref address);
                settings = new ProxySettings(address, Config.ProxyUser, Config.ProxyPassword);
            }
            ProxySettings previous = GetMegaAccountProxy();
            if (previous is null && settings is null)
                return;
            if (previous is not null && settings is not null &&
                Equals(previous.Address, settings.Address) &&
                previous.Username == settings.Username &&
                previous.Password == settings.Password)
            {
                return;
            }
            System.Threading.Volatile.Write(ref _proxy, settings);
            MegaAccountService.Current.UpdateProxies();
        }

        internal static ProxySettings GetMegaAccountProxy() => System.Threading.Volatile.Read(ref _proxy);

        /// <summary>Accepts a host, IP, or HTTP proxy address. The separate port field takes precedence.
    /// An inline port ("127.0.0.1:8000", "[::1]:8000") is stripped; the configured port wins.</summary>
        internal static string NormalizeProxyHost(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string host = value.Trim();
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(7);
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(8);
            host = host.TrimEnd('/', '\\');
            if (host.IndexOfAny(new char[] { '/', '\\', '?', '#', '@' }) >= 0 || host.Any(char.IsWhiteSpace))
                return null;

            // [IPv6]:port → [IPv6]
            if (host.StartsWith("[", StringComparison.Ordinal))
            {
                int close = host.IndexOf(']');
                if (close < 0)
                    return null;
                host = host.Substring(0, close + 1);
            }
            else
            {
                int firstColon = host.IndexOf(':');
                if (firstColon >= 0)
                {
                    int secondColon = host.IndexOf(':', firstColon + 1);
                    if (secondColon < 0)
                    {
                        if (firstColon == 0)
                            return null; // ":8000" is not a host
                        host = host.Substring(0, firstColon);
                    }
                }
            }
            if (string.IsNullOrEmpty(host))
                return null;

            IPAddress ip = null;
            if (!host.StartsWith("[", StringComparison.Ordinal) && IPAddress.TryParse(host, out ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                host = "[" + host + "]";

            Uri address = null;
            if (!Uri.TryCreate("http://" + host, UriKind.Absolute, out address) || address.HostNameType == UriHostNameType.Unknown || address.UserInfo.Length != 0 || address.AbsolutePath != "/")
                return null;
            string bare = address.Host;
            int bracket = bare.IndexOf('[');
            if (bracket >= 0 && bare.EndsWith("]"))
                bare = bare.Substring(bracket + 1, bare.Length - bracket - 2);
            return bare;
        }

        private static bool TryCreateProxyAddress(string host, int port, ref Uri address)
        {
            string authority = host.StartsWith("[", StringComparison.Ordinal) ? host : host.IndexOf(':') >= 0 ? "[" + host + "]" : host;
            return Uri.TryCreate("http://" + authority + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture), UriKind.Absolute, out address);
        }

        // Global ServicePointManager defaults were rewritten on every request. They are
        // process-wide, so apply them once on first use instead.
        private static readonly Lazy<bool> _globalNetworkSettingsApplied = new Lazy<bool>(() =>
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.DefaultConnectionLimit = 10000;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Could not apply global network settings: " + Log.SafeException(ex));
            }
            return true;
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        public static HttpWebRequest CreateHttpWebRequest(string Url)
        {
            _ = _globalNetworkSettingsApplied.Value;

            HttpWebRequest webrq = (HttpWebRequest)WebRequest.Create(Url);
            webrq.Timeout = 60000;
            webrq.ReadWriteTimeout = 120000;

            var settings = System.Threading.Volatile.Read(ref _proxy);
            if (settings is not null)
            {
                if (settings.Address is null)
                    throw new UriFormatException("Invalid proxy address.");
                var proxy = new WebProxy(settings.Address);
                if (!string.IsNullOrEmpty(settings.Username))
                {
                    proxy.Credentials = new NetworkCredential(settings.Username, settings.Password);
                }
                webrq.Proxy = proxy;
            }

            return webrq;
        }

        public static string Get_MEGA_API_Url(string Session)
        {

            string Url;
            if (string.IsNullOrEmpty(Session))
            {
                Url = InternalConfiguration.ObtenerValueFromInternalConfig(useGlobalCDN ? "URL_MEGA_API_NOSESION_G" : "URL_MEGA_API_NOSESION_EU");
                if (!Url.ToUpper().StartsWith("HTTP"))
                    Url = Criptografia.AES_DecryptString(Url, keyUrl);
            }
            else
            {
                Url = InternalConfiguration.ObtenerValueFromInternalConfig(useGlobalCDN ? "URL_MEGA_API_SESION_G" : "URL_MEGA_API_SESION_EU");
                if (!Url.ToUpper().StartsWith("HTTP"))
                    Url = Criptografia.AES_DecryptString(Url, keyUrl);
                Url = Url.Replace("%SESSION%", Session);
            }

            Url = Url.Replace("%SEQ%", GetSequenceId());
            return Url;
        }

        internal static Respuesta SendJSON(string URL, string JSON, string ContentType = "", bool SendAppId = true, System.Threading.CancellationToken ct = default)
        {


            HttpWebRequest webRQ = null;
            HttpWebResponse webRS = null;
            StreamReader inStream = null;

            var Resultado = new Respuesta();
            Resultado.Status = HttpStatusCode.Unused;
            Resultado.Excepcion = null;
            Resultado.Mensaje = "";

            // 真取消：net48 的 HttpWebRequest 不认 CancellationToken，阻塞在 GetResponse /
            // ReadToEnd 里的几十秒（超时 60s + 读写 120s，黑洞代理下打满）协作式取消根本进不来，
            // 文件夹解析窗表现为"一直在转且取消键无效"。此处用 ct.Register 调 Abort()
            // 把阻塞调用炸出来，转为 OperationCanceledException 走正常取消路径。
            // 不传 ct 的调用方（下载/更新等）行为与原来完全一致。
            System.Threading.CancellationTokenRegistration abortReg = default;
            bool hasAbortReg = false;
            try
            {
                // 请求构造必须在 Try 内：非法代理串会让 New WebProxy 抛 UriFormatException，
                // 逸出后验证流程既不置错误态也不停止重选，整个队列会永久卡在"验证中"。
                webRQ = CreateHttpWebRequest(URL);
                webRQ.Method = "POST";

                byte[] data = System.Text.Encoding.UTF8.GetBytes(JSON);

                webRQ.ContentLength = data.Length;
                if (!string.IsNullOrEmpty(ContentType))
                {
                    webRQ.ContentType = ContentType;
                }

                if (ct.CanBeCanceled)
                {
                    var reqToAbort = webRQ;
                    abortReg = ct.Register(() =>
                        {
                            try
                            {
                                reqToAbort.Abort();
                            }
                            catch
                            {
                            }
                        });
                    hasAbortReg = true;
                }
                using (var newStream = webRQ.GetRequestStream())
                {
                    newStream.Write(data, 0, data.Length);
                }

                webRS = (HttpWebResponse)webRQ.GetResponse();

                inStream = new StreamReader(webRS.GetResponseStream());
                Resultado.Mensaje = inStream.ReadToEnd();
                Resultado.Status = webRS.StatusCode;

                Resultado.Cookies = webRS.Headers["Set-Cookie"];
            }

            catch (WebException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Request aborted by user.", ex);
                }
                try
                {
                    if (ex.Response is not null)
                    {
                        Resultado.Status = ((HttpWebResponse)ex.Response).StatusCode;
                        using (var Stream = ex.Response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(Stream))
                            {
                                Resultado.Mensaje = reader.ReadToEnd();
                            }
                        }
                    }
                }

                catch (WebException exc)
                {
                    // Oh, well, we tried
                }
                Log.WriteError("Error accessing the URL: " + Log.SafeException(ex) + " - Message received: " + Log.Redact(Resultado.Mensaje));
                Resultado.Excepcion = ex;
            }
            catch (Exception ex)
            {
                Log.WriteError("Error accessing the URL: " + Log.SafeException(ex));
                Resultado.Excepcion = ex;
            }
            finally
            {
                if (hasAbortReg)
                {
                    try
                    {
                        abortReg.Dispose();
                    }
                    catch
                    {
                    }
                }
                if (webRS is not null)
                    webRS.Close();
                if (inStream is not null)
                    inStream.Close();
            }

            return Resultado;
        }


        internal static Respuesta SendPOST(string URL, System.Collections.Specialized.NameValueCollection PostParameters, string ContentType = "application/x-www-form-urlencoded", bool SendAppId = true, System.Threading.CancellationToken ct = default)


        {
            var str = new System.Text.StringBuilder();
            foreach (string @param in PostParameters.Keys)
            {
                string value = PostParameters[@param];
                if (str.Length > 0)
                    str.Append("&");
                str.Append(System.Web.HttpUtility.UrlEncode(@param));
                str.Append("=");
                str.Append(System.Web.HttpUtility.UrlEncode(value));
            }
            if (str.Length == 0)
            {
                throw new ArgumentException("No POST parameters found");
            }
            return SendJSON(URL, str.ToString(), ContentType, SendAppId, ct);
        }

        internal static string ObtenerUrlDesdeAcortador(string URL)
        {
            HttpWebRequest webReq = null;
            HttpWebResponse webResp = null;

            try
            {
                webReq = CreateHttpWebRequest(URL);
                webResp = (HttpWebResponse)webReq.GetResponse();

                return webResp.ResponseUri.ToString();
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
            finally
            {
                if (webResp is not null)
                    webResp.Close();
            }
        }


        internal static Respuesta LeerURL(string URL, System.Text.Encoding encoding = null, string userAgent = null, string referer = null, System.Collections.Specialized.NameValueCollection Headers = null)



        {
            HttpWebRequest webReq = null;
            HttpWebResponse webResp = null;
            StreamReader inStream = null;

            var Res = new Respuesta();
            Res.Status = HttpStatusCode.Unused;
            Res.Excepcion = null;
            Res.Mensaje = "";


            try
            {
                webReq = CreateHttpWebRequest(URL);

                if (Headers is not null)
                {
                    foreach (var header in Headers.AllKeys)
                        webReq.Headers.Add(header, Headers[header]);

                }


                if (!string.IsNullOrEmpty(referer))
                {
                    webReq.Referer = referer;
                }
                if (!string.IsNullOrEmpty(userAgent))
                {
                    webReq.UserAgent = userAgent;
                }
                webResp = (HttpWebResponse)webReq.GetResponse();

                var encode = encoding;
                if (encode is null)
                    encode = System.Text.Encoding.GetEncoding("utf-8");

                inStream = new StreamReader(webResp.GetResponseStream(), encode);
                Res.Mensaje = inStream.ReadToEnd();
                Res.Status = webResp.StatusCode;
            }
            catch (WebException ex)
            {
                try
                {
                    if (ex.Response is not null)
                    {
                        Res.Status = ((HttpWebResponse)ex.Response).StatusCode;
                        using (var Stream = ex.Response.GetResponseStream())
                        {
                            var encode = encoding;
                            if (encode is null)
                                encode = System.Text.Encoding.GetEncoding("utf-8");
                            using (var reader = new StreamReader(Stream, encode))
                            {
                                Res.Mensaje = reader.ReadToEnd();
                            }
                        }
                    }
                }

                catch (WebException exc)
                {
                    // Oh, well, we tried
                }
                Log.WriteError("Error accessing the URL: " + Log.SafeException(ex) + " - Message received: " + Log.Redact(Res.Mensaje));
                Res.Excepcion = ex;
            }
            catch (Exception ex)
            {
                Log.WriteError("Error accessing the URL: " + Log.SafeException(ex));
                Res.Excepcion = ex;
            }
            finally
            {
                if (webResp is not null)
                    webResp.Close();
                if (inStream is not null)
                    inStream.Close();
            }
            return Res;
        }



        private const string keyUrl = "81379874BC2815E6825E98F986F98410EFC68D6DABC6EF8B54968C75387551F2";

        public static string GetUpdateCheckURL()
        {
            // Update check has been redirected to the GitHub repository.
            // The version metadata XML file is hosted in docs/version.xml and points to the latest release.
            const string RepoRawUrl = "https://raw.githubusercontent.com/a1175815821/MegaDownloader-Revival/main/docs/version.xml";
            return RepoRawUrl;
        }


        internal const string patternGetFileName = @"MEGA.*?""n""\s*:\s*""(?<FileName>.*?)""";

        /// <summary>
    /// 从解密后的节点 attribute(形如 MEGA{"n":"文件名",...})中提取文件名。
    /// 旧正则遇到文件名含转义引号(\" )会提前截断,\uXXXX 也原样透出;
    /// 这里先走 JSON 解析(Newtonsoft 已引用),失败再回退历史正则,行为只增不减。
    /// 空/解析不出返回 Nothing,由调用方按原逻辑跳过或报错。
    /// </summary>
        internal static string ExtractNameFromFileInfo(string fileInfoDec)
        {
            if (string.IsNullOrEmpty(fileInfoDec))
                return null;
            try
            {
                int jsonStart = fileInfoDec.IndexOf('{');
                if (jsonStart >= 0)
                {
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(fileInfoDec.Substring(jsonStart));
                    var n = obj["n"];
                    if (n is not null)
                    {
                        string name = (string)n;
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch
            {
                // 解析失败走下面的正则回退
            }
            try
            {
                var ex = new System.Text.RegularExpressions.Regex(patternGetFileName);
                if (ex.IsMatch(fileInfoDec))
                    return ex.Match(fileInfoDec).Groups["FileName"].Value;
            }
            catch
            {
            }
            return null;
        }

        /// <summary>v2.5 beta: 仅识别 API 配额语义(-17 / EOVERQUOTA),不做 "509" 文本匹配。</summary>
        internal static bool IsQuotaErrorText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            string t = s.Trim();
            if (t == "-17" || t == "[-17]")
                return true;
            return t.IndexOf("EOVERQUOTA", StringComparison.OrdinalIgnoreCase) >= 0;
        }


        public static InformacionFichero ObtenerInformacionFichero(Configuracion Config, string FileID, string FileKey, bool ComprobacionAntesDescarga, System.Threading.CancellationToken ct = default)
        {

            var Info = new InformacionFichero();
            try
            {
                URLExtractor.CheckFileIDAndFileKey(ref FileID, ref FileKey);
            }
            catch (Exception ex)
            {
                Info.Err = TipoError.Otros;
                if (ex is ArgumentOutOfRangeException)
                {
                    Info.Errtxt = "Error getting ID and File Key: the provided link seems to be incomplete";
                }
                else
                {
                    Info.Errtxt = "Error getting ID and File Key: " + ex.Message;
                }

                Log.WriteError("Error getting ID and File Key: " + ex.ToString());
                return Info;
            }


            Info.FileID = FileID;
            Info.FileKey = FileKey;

            string URL = InternalConfiguration.ObtenerValueFromInternalConfig(useGlobalCDN ? "URL_MEGA_API_DOWN_GET_G" : "URL_MEGA_API_DOWN_GET_EU");
            if (!URL.ToUpper().StartsWith("HTTP"))
                URL = Criptografia.AES_DecryptString(URL, keyUrl);


            URL = URL.Replace("%ID%", GetSequenceId());

            const string useSSL = "1";

            string json;
            if (FileID.StartsWith("megafolder?") && FileID.Split('?').Length == 3) // Private file from folder: old system (just for compatibility)
            {
                json = string.Format("[{{\"a\":\"g\",\"g\":\"1\",\"ssl\":{0},\"n\":\"{1}\"}}]", useSSL, JsonEscapeString(FileID.Split('?')[2]));
                URL += "&n=" + Uri.EscapeDataString(FileID.Split('?')[1]);
            }
            else if (FileID.StartsWith("N?") && FileID.Split('?').Length == 2) // Private file from folder: new system
            {
                if (Info.FileKey.Contains("=###n="))
                {
                    URL += "&n=" + Uri.EscapeDataString(Info.FileKey.Substring(Info.FileKey.IndexOf("=###n=") + 6));
                }
                json = string.Format("[{{\"a\":\"g\",\"g\":\"1\",\"ssl\":{0},\"n\":\"{1}\"}}]", useSSL, JsonEscapeString(FileID.Split('?')[1]));
            }
            else
            {
                if (Info.FileKey.Contains("=###n="))
                {
                    URL += "&n=" + Uri.EscapeDataString(Info.FileKey.Substring(Info.FileKey.IndexOf("=###n=") + 6));
                }
                json = string.Format("[{{\"a\":\"g\",\"g\":\"1\",\"ssl\":{0},\"p\":\"{1}\"}}]", useSSL, JsonEscapeString(FileID));
            }

            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            var Resultado = SendJSON(URL, json, ct: ct);


            if (Resultado.Excepcion is null)
            {

                Dictionary<string, object> FileInfoRS;
                try
                {
                    string str = Resultado.Mensaje;
                    if (str.StartsWith("["))
                    {
                        str = str.Substring(1);
                    }
                    if (str.EndsWith("]"))
                    {
                        str = str.Trim(']');
                    }

                    if (Information.IsNumeric(str))
                    {
                        throw MEGA_ErrorHandler.GetErrorFromMegaResponse(str, "when retrieving file information");
                    }

                    FileInfoRS = (Dictionary<string, object>)Newtonsoft.Json.JsonConvert.DeserializeObject(str, typeof(Dictionary<string, object>));


                    if (FileInfoRS.ContainsKey("e") && FileInfoRS["e"] is not null)
                    {
                        throw MEGA_ErrorHandler.GetErrorFromMegaResponse(FileInfoRS["e"].ToString(), "when retrieving file information");
                    }

                    if (!FileInfoRS.ContainsKey("s"))
                        throw new Exception();
                    if (!FileInfoRS.ContainsKey("at"))
                        throw new Exception();
                    if (!FileInfoRS.ContainsKey("g"))
                        throw new Exception();
                }
                catch (Exception excFileInfo)
                {
                    if (IsQuotaErrorText(excFileInfo.Message) || IsQuotaErrorText(Resultado.Mensaje))
                    {
                        Info.Err = TipoError.QuotaExceeded;
                        Info.Errtxt = "MEGA transfer quota exceeded (EOVERQUOTA). Queue will pause and retry automatically.";
                        MegaQuotaManager.ReportQuota();
                        Log.WriteWarning("MEGA quota (EOVERQUOTA -17) while retrieving file info for " + FileID);
                        return Info;
                    }
                    Info.Err = TipoError.Otros;
                    if (excFileInfo.Message.Contains("when retrieving file information"))
                    {
                        Info.Errtxt = excFileInfo.Message;
                    }
                    else
                    {
                        Info.Errtxt = "Response not expected: " + Resultado.Mensaje;
                    }
                    Log.WriteError("Error getting the info for file " + FileID + ": Response not expected - Message received: " + Resultado.Mensaje + " - Error: " + excFileInfo.ToString());
                    return Info;
                }

                string FileInfo = Conversions.ToString(FileInfoRS["at"]);
                string FileSize = Conversions.ToString(FileInfoRS["s"]);
                string Download = Conversions.ToString(FileInfoRS["g"]);

                string FileInfoDec = "";

                FileInfoDec = PreSharedKeyManager.DecryptFileInfo(FileInfo, Info.FileKey);
                if (string.IsNullOrEmpty(FileInfoDec))
                {
                    foreach (string PreSharedKey in PreSharedKeyManager.GetFileKeyFromPreSharedKeys(ref Config))
                    {
                        FileInfoDec = PreSharedKeyManager.DecryptFileInfo(FileInfo, PreSharedKey);
                        if (!string.IsNullOrEmpty(FileInfoDec))
                        {
                            Info.FileKey = PreSharedKey;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(FileInfoDec))
                {
                    Info.Err = TipoError.Otros;
                    Info.Errtxt = "File could not be decrypted. Check the file Key is present on the URL and it is correct; check that the link doesn't have spaces in the middle (some forums/webpages add spaces in the middle of long words). In case of using a pre-shared key, make sure you have configured it.";
                    Log.WriteError("Error getting the info for file " + FileID + ": Error decrypting fileinfo - Message received: " + Resultado.Mensaje);
                    return Info;
                }

                string nodeName = ExtractNameFromFileInfo(FileInfoDec);
                if (!string.IsNullOrEmpty(nodeName))
                {
                    Info.Nombre = nodeName;
                    Info.URL = Download;
                    Info.Tamano = 0L;
                    long.TryParse(FileSize, out Info.Tamano);
                }
                else
                {
                    Info.Err = TipoError.Otros;
                    Info.Errtxt = "File name could not be retrieved";
                    Log.WriteError("Error getting the info for file " + FileID + ": File name could not be retrieved - Message received: " + Resultado.Mensaje + " - Fileinfo: " + FileInfoDec);
                }
            }



            else if (Resultado.Excepcion is WebException)
            {
                WebException wex = (WebException)Resultado.Excepcion;
                if (MegaQuotaManager.IsQuotaWebException(wex))
                {
                    var hint = MegaQuotaManager.TryGetRetryAfterSeconds(wex);
                    MegaQuotaManager.ReportQuota(hint.HasValue ? hint.Value : 0L);
                    Info.Err = TipoError.QuotaExceeded;
                    Info.Errtxt = "MEGA transfer quota exceeded (HTTP 509). Queue will pause and retry automatically.";
                    Log.WriteWarning("MEGA quota (HTTP 509) while retrieving file info for " + FileID);
                    return Info;
                }
                Info.Err = TipoError.ErrorConexion;
                Info.Errtxt = "Connection error: " + Resultado.Excepcion.Message + " - Message received: " + Resultado.Mensaje;
                Log.WriteError("Error getting the info for file " + FileID + ": " + Resultado.Excepcion.Message + " - Message received: " + Resultado.Mensaje);
            }
            else
            {
                Info.Err = TipoError.Otros;
                Info.Errtxt = "Description: " + Resultado.Excepcion.ToString() + " - Message received: " + Resultado.Mensaje;
                Log.WriteError("Error getting the info for file " + FileID + ": " + Resultado.Excepcion.Message + " - Message received: " + Resultado.Mensaje);
            }
            return Info;
        }


        #region Ping
        /// <summary>
    /// Analytics pings (PingMega / PingNewUser / PingNewVersion) have been removed
    /// because they used the discontinued goo.gl short links. No replacement
    /// analytics are sent. Methods kept as no-ops for caller compatibility.
    /// </summary>
        public static void PingMega()
        {
            // No-op: analytics discontinued (goo.gl links dead)
        }

        public static void PingNewUser()
        {
            // No-op: analytics discontinued (goo.gl links dead)
        }

        public static void PingNewVersion()
        {
            // No-op: analytics discontinued (goo.gl links dead)
        }
        #endregion



    }
}