using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HttpServer;
using HttpServer.HttpModules;
using HttpServer.Sessions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{


    public class StreamingLibraryModule : HttpModule
    {


        // P0-3：与 WebInterfaceModule 保持一致，不再引用 Main 类型。
        // 当前本模块未调用 ControlRemoto* 成员，仅保留引用以备后用。
        public IDownloaderService Downloader;
        public Configuracion Config;


        public const string PaginaMain = "/library";
        public const string PaginaManagement = "/manage";
        public const string PaginaAjax = "/ajax";
        public const string PaginaLogin = "/login";


        public string TemplateManagerData;
        public string TemplateData;
        public string TemplateLogin;
        // AsyncLocal isolates the value per async/await execution context. The HttpServer
        // framework dispatches each request on a thread-pool thread inside an isolated
        // ExecutionContext, so concurrent requests no longer overwrite each other's
        // AJAX response (previously a shared instance field caused cross-request pollution).
        private static System.Threading.AsyncLocal<string> _RespuestaAjax = new System.Threading.AsyncLocal<string>();
        private int _TimeoutSesion;

        public StreamingLibraryModule(IDownloaderService Downloader, ref Configuracion Config)
        {

            foreach (string resname in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (resname.EndsWith("StreamingLibraryManagerTemplateData.htm"))
                {
                    var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname);
                    var reader = new StreamReader(stream);
                    TemplateManagerData = reader.ReadToEnd();
                }
                else if (resname.EndsWith("StreamingLibraryTemplateData.htm"))
                {
                    var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname);
                    var reader = new StreamReader(stream);
                    TemplateData = reader.ReadToEnd();
                }
                else if (resname.EndsWith("StreamingLibraryTemplateLogin.htm"))
                {
                    var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname);
                    var reader = new StreamReader(stream);
                    TemplateLogin = reader.ReadToEnd();
                }
            }


            _TimeoutSesion = 3600 * 24 * 7;
            this.Downloader = Downloader;
            this.Config = Config;
        }



        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
        {

            try
            {
                SetHeaders(ref response);

                ResetRequestVar(ref request);

                if (!ComprobarAcceso(ref request, ref response, ref session))
                    return false;
                if (!ProcesarPagina(ref request, ref response, ref session))
                    return true;
                PintarPagina(ref request, ref response, ref session);

                return true;
            }
            catch (Exception ex)
            {
                Log.WriteError("Error in StreamingLibraryModule: " + ex.ToString());
                // 同 StreamingModule:抛回 HttpServer 等于不发送任何响应(它未订阅 ExceptionThrown
                // 时只写日志就 return),页面与 AJAX 会一直转圈。这里兜底写回 5xx。
                try
                {
                    if (!response.HeadersSent)
                    {
                        response.Status = System.Net.HttpStatusCode.InternalServerError;
                        response.Body.SetLength(0L);
                        StreamingModule.WriteTextResponse(request, response, "Internal server error");
                    }
                }
                catch (Exception sendEx)
                {
                    Log.WriteError("StreamingLibraryModule: could not send the error response: " + sendEx.ToString());
                }
                return true;
            }
        }

        #region Funciones varias privadas
        private static System.Threading.AsyncLocal<string> _ErrorLocal = new System.Threading.AsyncLocal<string>();
        private bool IsPostBack(ref IHttpRequest request)
        {
            return request.Method == "POST";
        }

        /// <summary>获取(必要时生成)当前会话的 CSRF token</summary>
        private string EnsureCsrf(ref IHttpSession session)
        {
            if (session["csrf"] is null || string.IsNullOrEmpty(Conversions.ToString(session["csrf"])))
            {
                session["csrf"] = Guid.NewGuid().ToString("N");
            }
            return Conversions.ToString(session["csrf"]);
        }

        private bool ValidateCsrf(ref IHttpRequest request, ref IHttpSession session)
        {
            string expected = EnsureCsrf(ref session);
            if (request.Param is null || request.Param["csrf"] is null)
                return false;
            return string.Equals(request.Param["csrf"].Value, expected, StringComparison.Ordinal);
        }



        private void ResetRequestVar(ref IHttpRequest request)
        {
            if (!IsPostBack(ref request)) // Pedimos datos; si es un POST, es que enviamos al servidor
            {
                _ErrorLocal.Value = "";
            }
        }

        private void SetHeaders(ref IHttpResponse response)
        {
            response.AddHeader("Server", "Internal");
        }
        private void SetCSSControlCache(ref IHttpResponse response)
        {
            response.AddHeader("Cache-Control", "public, max-age=604800"); // Una semana, 3600*24*7
        }


        #endregion


        /// <summary>
    /// Examina el usuario y la página solicitada para redirigir a donde haga falta.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="session"></param>
    /// <returns>Devuelve true si se puede que continuar, false si hay que abortar</returns>
    /// <remarks></remarks>
        private bool ComprobarAcceso(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {

            // Miramos si hace falta loguearse
            if (!string.IsNullOrEmpty(Config.ServidorStreamingPassword) & !UsuarioLogueado(ref session) & (request.Uri.LocalPath ?? "") != PaginaLogin)
            {
                response.Redirect(PaginaLogin);
                return false;
            }

            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaMain:
                case PaginaManagement:
                case PaginaAjax:
                case PaginaLogin:
                    return true;
                default:
                    response.Redirect(PaginaMain);
                    return false;
            }

        }

        private bool UsuarioLogueado(ref IHttpSession session)
        {
            bool resultado = session["Logueado"] is not null && Conversions.ToString(session["Logueado"]) == "1";
            if (resultado & _TimeoutSesion > 0 & session["LoginDate"] is not null && session["LoginDate"] is DateTime)
            {
                // 超时则清除登录状态,避免 session 永远停留在已过期的时间戳上
                if (Conversions.ToDate(session["LoginDate"]).AddSeconds(_TimeoutSesion) <= DateTime.Now)
                {
                    session["Logueado"] = null;
                    session["LoginDate"] = null;
                    resultado = false;
                }
            }
            return resultado;
        }

        private bool ProcesarPagina(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaLogin:
                    return ProcesoLogin(ref request, ref response, ref session);
                case PaginaAjax:
                    return ProcesoAjax(ref request, ref response, ref session);
            }
            return true;
        }

        private void PintarPagina(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {

            var responseBody = new System.Text.StringBuilder();


            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaManagement:
                    // 渲染时注入 CSRF token,页面内所有 AJAX 请求(见模板 $.ajaxSetup)会自动携带
                    responseBody.Append(TemplateManagerData.Replace("%CSRF_TOKEN%", EnsureCsrf(ref session)));
                    break;
                case PaginaMain:
                    responseBody.Append(TemplateData.Replace("%CSRF_TOKEN%", EnsureCsrf(ref session)));
                    break;
                case PaginaAjax:
                    responseBody.Append(CargarAjax());
                    break;
                case PaginaLogin:
                    responseBody.Append(TemplateLogin.Replace("%ERROR%", _ErrorLocal.Value));
                    break;
            }

            string argResponseBody = responseBody.ToString();
            ComprimirRespuesta(ref request, ref response, ref argResponseBody);
        }

        private void ComprimirRespuesta(ref IHttpRequest request, ref IHttpResponse response, ref string ResponseBody)
        {

            string acceptEncoding = request.Headers["Accept-Encoding"];

            if (!string.IsNullOrEmpty(acceptEncoding) && acceptEncoding.ToLower().Contains("gzip"))
            {
                response.AddHeader("Content-Encoding", "gzip");


                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(ResponseBody);
                using (var memWriter = new MemoryStream())
                {
                    using (var writer = new GZipStream(memWriter, CompressionMode.Compress))
                    {
                        writer.Write(bytes, 0, bytes.Length);
                        writer.Flush();
                    }
                    bytes = memWriter.ToArray();
                    response.Body.Write(bytes, 0, bytes.Length);
                }
            }
            else
            {
                // leaveOpen 保证响应流不被关闭(框架负责其生命周期),同时确定性释放 writer;
                // UTF8Encoding(False) 明确不写 BOM
                using (var writer = new StreamWriter(response.Body, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true))
                {
                    writer.Write(ResponseBody);
                    writer.Flush();
                }
            }
        }

        #region Proceso páginas

        private string CargarAjax()
        {
            return _RespuestaAjax.Value;
        }


        // 登录限速:与 WebInterfaceModule 对齐。此前本页密码 POST 无任何限制,
        // 虽然服务只监听 127.0.0.1,本机任意进程仍可无限速爆破流媒体库口令。
        // 两道闸:并发上限(防打满线程池) + 滑动窗口内的失败锁定。
        private static readonly object _LoginRateLock = new object();
        private static readonly List<DateTime> _LoginFailures = new List<DateTime>();
        private static int _ActiveLogins = 0;
        private const int MaxConcurrentLogins = 4;
        private const int MaxLoginFailuresPerWindow = 10;
        private const int LoginFailureWindowSeconds = 60;

        /// <summary>失败锁定窗口检查:窗口内失败次数达到上限则拒绝服务</summary>
        private static bool LoginLockedOut()
        {
            lock (_LoginRateLock)
            {
                var cutoff = DateTime.Now.AddSeconds(-LoginFailureWindowSeconds);
                _LoginFailures.RemoveAll(d => d < cutoff);
                return _LoginFailures.Count >= MaxLoginFailuresPerWindow;
            }
        }

        private static void RecordLoginFailure()
        {
            lock (_LoginRateLock)
                _LoginFailures.Add(DateTime.Now);
        }

        private bool ProcesoLogin(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            if (request.Param is not null)
            {
                if (request.Param["Password"] is not null && IsPostBack(ref request))
                {
                    // 限速:锁定期内或并发超限时直接拒绝,不执行口令比较
                    if (LoginLockedOut())
                    {
                        SetError(Language.GetText("Too many login attempts, please try again later"));
                        return true;
                    }
                    if (System.Threading.Interlocked.Increment(ref _ActiveLogins) > MaxConcurrentLogins)
                    {
                        System.Threading.Interlocked.Decrement(ref _ActiveLogins);
                        SetError(Language.GetText("Too many login attempts, please try again later"));
                        return true;
                    }
                    try
                    {
                        if (Criptografia.FixedTimeEquals(Criptografia.Utf8BytesOrNull(request.Param["Password"].Value), Criptografia.Utf8BytesOrNull(Config.ServidorStreamingPassword)))
                        {
                            session["Logueado"] = "1";
                            session["LoginDate"] = DateTime.Now;
                            response.Redirect(PaginaMain);
                            return false;
                        }
                        else
                        {
                            RecordLoginFailure();
                            SetError(Language.GetText("Invalid password"));
                        }
                    }
                    finally
                    {
                        System.Threading.Interlocked.Decrement(ref _ActiveLogins);
                    }
                }
            }
            return true;
        }


        private void SetError(string msj)
        {
            _ErrorLocal.Value = "<br/><div class='error'>" + msj + "</div><br/>";
        }


        private bool ProcesoAjax(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {

            string url = request.Uri.Authority;

            if (request.Param is not null)
            {
                if (request.Param["Action"] is null || string.IsNullOrEmpty(request.Param["Action"].Value))
                {
                    PrepareAjaxResponse("Action not specified");
                    return true;
                }

                string actionName = request.Param["Action"].Value;
                bool mutating = actionName == "Delete" || actionName == "Save" || actionName == "OpenVLC" || actionName == "ImportLinks" || actionName == "ExportLinks";
                // 状态变更操作除必须 POST 外,还要携带有效 CSRF token:
                // 仅靠 IsPostBack 挡不住已登录用户被第三方页面 CSRF 删改流媒体库
                if (mutating && (!IsPostBack(ref request) || !ValidateCsrf(ref request, ref session)))
                {
                    PrepareAjaxResponse("Forbidden");
                    return true;
                }

                switch (actionName ?? "")
                {

                    case "Load":
                        {
                            if (request.Param["Element"] is null || string.IsNullOrEmpty(request.Param["Element"].Value))
                            {
                                PrepareAjaxResponse("Element not specified");
                                return true;
                            }

                            var Ele = StreamingLibraryManager.GetElementByID(request.Param["Element"].Value);
                            if (Ele is null)
                            {
                                PrepareAjaxResponse("Element not found");
                                return true;
                            }
                            else
                            {
                                PrepareAjaxResponse(url, Ele);
                            }

                            break;
                        }

                    case "Delete":

                        if (request.Param["ID"] is null || !Information.IsNumeric(request.Param["ID"].Value))
                        {
                            PrepareAjaxResponse("ID not specified");
                            return true;
                        }

                        StreamingLibraryManager.RemoveElement(request.Param["ID"].Value);
                        PrepareAjaxResponse("");
                        return true;

                    case "Save":
                        {

                            if (request.Param["ID"] is null)
                            {
                                PrepareAjaxResponse("ID not specified");
                                return true;
                            }
                            else if (request.Param["IMDB"] is null)
                            {
                                PrepareAjaxResponse("IMDB not specified");
                                return true;
                            }
                            else if (request.Param["Allocine"] is null)
                            {
                                PrepareAjaxResponse("Allocine not specified");
                                return true;
                            }
                            else if (request.Param["Filmaffinity"] is null)
                            {
                                PrepareAjaxResponse("Filmaffinity not specified");
                                return true;
                            }
                            else if (request.Param["Name"] is null)
                            {
                                PrepareAjaxResponse("Name not specified");
                                return true;
                            }
                            else if (request.Param["Desc"] is null)
                            {
                                PrepareAjaxResponse("Desc not specified");
                                return true;
                            }
                            else if (request.Param["Poster"] is null)
                            {
                                PrepareAjaxResponse("Poster not specified");
                                return true;
                            }
                            else if (request.Param["Comments"] is null)
                            {
                                PrepareAjaxResponse("Comments not specified");
                                return true;
                            }
                            else if (request.Param["Link"] is null || string.IsNullOrEmpty(request.Param["Link"].Value))
                            {
                                PrepareAjaxResponse("Link not specified");
                                return true;
                            }

                            if ((request.Param["Link"].Value ?? "") != LibraryElement.HIDDEN_LINK_DESC)
                            {
                                if (string.IsNullOrEmpty(Fichero.ExtraerFileID(request.Param["Link"].Value)))
                                {
                                    PrepareAjaxResponse("Invalid link");
                                    return true;
                                }
                            }

                            bool LinkVisible = true;
                            string ID = request.Param["ID"].Value;
                            string Name = request.Param["Name"].Value;
                            string Desc = request.Param["Desc"].Value;
                            string Comments = request.Param["Comments"].Value;
                            string Poster = request.Param["Poster"].Value;
                            string IMDB = request.Param["IMDB"].Value;
                            string Filmaffinity = request.Param["Filmaffinity"].Value;
                            string Allocine = request.Param["Allocine"].Value;
                            string Link = request.Param["Link"].Value;

                            // Movie info services (IMDB/Allocine/Filmaffinity) have been removed because of API changes.
                            // Only the ID values are stored for manual cataloging.

                            if (Information.IsNumeric(ID))
                            {
                                var Ele = StreamingLibraryManager.ModifyElement(ID, Name, Desc, Comments, Poster, Link, LinkVisible, IMDB, Allocine, Filmaffinity);









                                if (Ele is null)
                                {
                                    PrepareAjaxResponse("Element not found");
                                    return true;
                                }
                                else
                                {
                                    PrepareAjaxResponse(url, Ele);
                                }
                            }
                            else
                            {
                                // New element
                                var Ele = StreamingLibraryManager.AddElement(Name, Desc, Comments, Poster, Link, LinkVisible, IMDB, Allocine, Filmaffinity);








                                if (Ele is null)
                                {
                                    PrepareAjaxResponse("Element not created");
                                    return true;
                                }
                                else
                                {
                                    PrepareAjaxResponse(url, Ele);
                                }
                            }

                            break;
                        }


                    case "LoadAll":

                        PrepareAjaxResponse(url, StreamingLibraryManager.GetElements());
                        break;

                    case "OpenVLC":
                        if (request.Param["URL"] is null || string.IsNullOrEmpty(request.Param["URL"].Value))
                        {
                            PrepareAjaxResponse("URL not specified");
                            return true;
                        }

                        if (!StreamingHelper.WatchOnline(Config.VLCPath, request.Param["URL"].Value))
                        {
                            PrepareAjaxResponse("VLC could not be loaded");
                        }
                        else
                        {
                            // The page script evaluates the body and reads .error off it, so an
                            // empty success body throws a JS TypeError. Send the same success
                            // envelope the Delete action already uses.
                            PrepareAjaxResponse("");
                        }
                        return true;


                    case "ImportLinks":
                        {
                            if (request.Param["Links"] is null)
                            {
                                PrepareAjaxResponse("Links not specified");
                                return true;
                            }

                            string links = request.Param["Links"].Value;

                            if (StreamingLibraryManager.IsImportedLibrary(links))
                            {
                                PrepareAjaxResponseImport(-1, StreamingLibraryManager.ImportLibrary(links));
                                return true;
                            }
                            else
                            {
                                // Guarded unescape (same pattern as URLExtractor.NormalizeLinkForExtraction):
                                // a stray "%" not followed by 2 hex digits makes UnescapeDataString throw
                                // UriFormatException, which would surface as HTTP 500 and leave the
                                // frontend "importing" mask stuck. Fall back to the raw text on failure.
                                string plainLinks = links;
                                try
                                {
                                    string unescaped = Uri.UnescapeDataString(links);
                                    if (!string.IsNullOrEmpty(unescaped))
                                        plainLinks = unescaped;
                                }
                                catch
                                {
                                }
                                var URLs = URLExtractor.ExtraerURLs(plainLinks);
                                PrepareAjaxResponseImport(URLs.Count, StreamingLibraryManager.ImportLinks(ref Config, URLs));
                                return true;
                            }
                        }


                    case "ExportLinks":
                        {
                            if (request.Param["IDs"] is null)
                            {
                                PrepareAjaxResponse("IDs not specified");
                                return true;
                            }

                            bool HideLinks = false;
                            bool PlainText = false;
                            if (request.Param["HideLinks"] is not null)
                            {
                                bool.TryParse(request.Param["HideLinks"].Value, out HideLinks);
                            }
                            if (request.Param["PlainText"] is not null)
                            {
                                bool.TryParse(request.Param["PlainText"].Value, out PlainText);
                            }

                            PrepareAjaxResponseExport(StreamingLibraryManager.ExportElements(request.Param["IDs"].Value.Split(';').ToList(), HideLinks, PlainText));
                            return true;
                        }

                    default:
                        PrepareAjaxResponse("Action " + request.Param["Action"].Value + " not recognized");
                        break;
                }


            }
            return true;
        }

        private void PrepareAjaxResponseExport(string code)
        {
            _RespuestaAjax.Value = "{\"error\":\"\",\"code\":" + LibraryElement.JsonString(code) + "}";
        }

        private void PrepareAjaxResponseImport(int numReceived, int numImported)
        {
            _RespuestaAjax.Value = "{\"error\":\"\",\"numR\":" + LibraryElement.JsonString(numReceived.ToString()) + ",\"numI\":" + LibraryElement.JsonString(numImported.ToString()) + "}";
        }
        private void PrepareAjaxResponse(string CurrentURL, LibraryElement ele)
        {
            _RespuestaAjax.Value = "{\"error\":\"\",\"Data\":[" + ele.ToJSON(CurrentURL, ref Config) + "]}";
        }

        private void PrepareAjaxResponse(string CurrentURL, IEnumerable<LibraryElement> ele)
        {
            var str = new System.Text.StringBuilder();
            str.Append("{\"error\":\"\",\"Data\":[");
            bool primero = true;
            foreach (LibraryElement e in ele)
            {
                if (!primero)
                    str.Append(",");
                str.Append(e.ToJSON(CurrentURL, ref Config));
                primero = false;
            }
            str.Append("]}");
            _RespuestaAjax.Value = str.ToString();
        }

        private void PrepareAjaxResponse(string ErrorMessage)
        {
            _RespuestaAjax.Value = "{\"error\":" + LibraryElement.JsonString(ErrorMessage) + "}";
        }


        #endregion




    }
}