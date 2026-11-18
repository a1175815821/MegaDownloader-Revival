using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using HttpServer;
using HttpServer.HttpModules;
using HttpServer.Sessions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{


    public class WebInterfaceModule : HttpModule
    {


        // P0-3：只依赖 IDownloaderService（9 方法 + DownloaderEstado），不再持有 Main 窗体实例。
        // 实现仍是 Main（ServidorWebController.StartWebServer 传入 Me），行为不变。
        public IDownloaderService Downloader;


        public const string PaginaLogin = "/login";
        public const string PaginaLogout = "/logout";
        public const string PaginaMain = "/main";
        public const string PaginaCSS = "/style.css";
        public const string PaginaFavIcon = "/favicon.ico";
        public const string PaginaAjaxStatus = "/ajax_status";
        public const string PaginaAjaxStop = "/ajax_stopdownload";
        public const string PaginaAjaxPlay = "/ajax_resumedownload";
        public const string PaginaAjaxAddLink = "/ajax_addlink";

        public const string CONST_CONTENT = "%CONTENT%";
        public const string CONST_HEAD = "%HEAD%";
        public const string CONST_JAVASCRIPT = "%JAVASCRIPT%";
        public const string CONST_TITLE = "%TITLE%";
        public const string CONST_ERROR = "%ERROR%";


        public System.Xml.XmlDocument XmlTemplateData = null;
        // Per-request state. The HttpServer framework dispatches each request on a
        // thread-pool thread inside an isolated ExecutionContext, so AsyncLocal keeps
        // concurrent requests from overwriting each other's response (a plain instance
        // field is shared by every request because the module is constructed once).
        private static System.Threading.AsyncLocal<string> _RespuestaAjaxLocal = new System.Threading.AsyncLocal<string>();

        // 登录口令校验。旧实现存无盐 MD5——同样的哈希可被彩虹表直接反查。
        // 现改为：每次启动生成随机 salt，内存中仅保留 PBKDF2 派生值；salt 是
        // 进程内随机数，彩虹表/预计算完全失效，比对用常量时间比较。
        private byte[] _PasswordSalt;
        private byte[] _PasswordHash;

        private static byte[] DeriveLoginHash(string password, byte[] salt)
        {
            using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 100000))
            {
                return pbkdf2.GetBytes(32);
            }
        }

        // 登录限速:每次登录尝试都在请求线程上同步执行 PBKDF2(100k 次),
        // 无限速的 POST 轰炸会打满线程池使整个 Web 服务不可用。
        // 两道闸:并发上限 + 滑动窗口内的失败锁定
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

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a is null || b is null || a.Length != b.Length)
                return false;
            byte diff = 0;
            for (int i = 0, loopTo = a.Length - 1; i <= loopTo; i++)
                diff = (byte)(diff | a[i] ^ b[i]);
            return diff == 0;
        }
        private string _TitlePersonalizado;
        private int _TimeoutSesion;
        private string _Language;

        public WebInterfaceModule(IDownloaderService Downloader, string TemplatePath, string Password, string TituloVentana, int TimeOutSesion, string LanguageCode)




        {
            if (string.IsNullOrEmpty(TemplatePath) || !File.Exists(TemplatePath))
            {

                foreach (string resname in System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames())
                {
                    if (resname.EndsWith("XmlTemplateData.xml"))
                    {
                        using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname))
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                XmlTemplateData = new System.Xml.XmlDocument();
                                XmlTemplateData.LoadXml(reader.ReadToEnd());
                            }
                        }
                        break;
                    }
                }
            }
            else
            {
                XmlTemplateData = new System.Xml.XmlDocument();
                XmlTemplateData.Load(TemplatePath);
            }


            CheckTemplate();

            if (string.IsNullOrEmpty(TituloVentana))
            {
                _TitlePersonalizado = InternalConfiguration.ObtenerNombreApp() + InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER");
            }
            else
            {
                _TitlePersonalizado = TituloVentana;
            }
            this.Downloader = Downloader;
            _TimeoutSesion = TimeOutSesion;
            _Language = LanguageCode;
            var salt = new byte[16];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }
            _PasswordSalt = salt;
            _PasswordHash = DeriveLoginHash(Password, salt);
        }

        private void CheckTemplate()
        {
            if (XmlTemplateData is null)
                throw new ApplicationException("Error template not found");
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Template/HtmlTemplate") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Template/HtmlTemplate");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIcon") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Template/FavIcon");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIconMimeType") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Template/FavIconMimeType");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Content/HeadMain") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Content/HeadMain");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyMain") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Content/BodyMain");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyJavaScript") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Content/BodyJavaScript");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyLogin") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Content/BodyLogin");
            }
            if (XmlTemplateData.DocumentElement.SelectSingleNode("Content/CSS") is null)
            {
                throw new ApplicationException("Template is not correct, missing node Content/CSS");
            }

        }



        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
        {

            SetHeaders(ref response);

            ResetRequestVar(ref request);

            if (!ComprobarAcceso(ref request, ref response, ref session))
                return true;
            if (!ProcesarPagina(ref request, ref response, ref session))
                return true;
            PintarPagina(ref request, ref response, ref session);

            return true;
        }

        #region Funciones varias privadas
        private static System.Threading.AsyncLocal<string> _ErrorLocal = new System.Threading.AsyncLocal<string>();
        private bool IsPostBack(ref IHttpRequest request)
        {
            return request.Method == "POST";
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


        private bool UsuarioLogueado(ref IHttpSession session)
        {
            bool resultado = session["Logueado"] is not null && Conversions.ToString(session["Logueado"]) == "1";
            if (resultado & _TimeoutSesion > 0 & session["LoginDate"] is not null && session["LoginDate"] is DateTime)
            {
                resultado = Conversions.ToDate(session["LoginDate"]).AddSeconds(_TimeoutSesion) > DateTime.Now;
            }
            return resultado;
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
            bool PaginaRequiereLogueo = false;
            bool PaginaAjax = false;
            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaMain:
                    PaginaRequiereLogueo = true;
                    break;
                case PaginaAjaxStatus:
                case PaginaAjaxStop:
                case PaginaAjaxPlay:
                case PaginaAjaxAddLink:
                    PaginaRequiereLogueo = true;
                    PaginaAjax = true;
                    break;
            }


            if (!UsuarioLogueado(ref session) & PaginaRequiereLogueo)
            {
                if (PaginaAjax)
                {
                    // R-P3-1: UTF8Encoding(False) 明确不写 BOM(照抄 StreamingLibraryModule.vb:202)。
                    // Encoding.UTF8 静态实例带 preamble,会把 EF BB BF 写进响应体开头,
                    // 严格 JSON 解析的前端逻辑会失败。
                    using (var writer = new StreamWriter(response.Body, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true))
                    {
                        writer.Write(ErrorAjax(Language.GetText("Error, session expired")));
                        writer.Flush();
                    }
                    return false;
                }
                else
                {
                    response.Redirect(PaginaLogin);
                    return false;
                }
            }

            else
            {

                // Comprobamos que la página sea válida, sino lo enviamos a main
                switch (request.Uri.LocalPath ?? "")
                {
                    case PaginaLogin:
                    case PaginaLogout:
                    case PaginaMain:
                    case PaginaCSS:
                    case PaginaFavIcon:
                    case PaginaAjaxStatus:
                    case PaginaAjaxPlay:
                    case PaginaAjaxStop:
                    case PaginaAjaxAddLink:
                        return true;
                    default:
                        response.Redirect(PaginaMain);
                        return false;
                }
            }

        }

        private bool ProcesarPagina(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaLogin:
                    return ProcesoLogin(ref request, ref response, ref session);
                case PaginaLogout:
                    return ProcesoLogout(ref request, ref response, ref session);
                case PaginaAjaxAddLink:
                    return ProcesoAddLinks(ref request, ref response, ref session);
                case PaginaAjaxStop:
                    return ProcesoStop(ref request, ref response, ref session);
                case PaginaAjaxPlay:
                    return ProcesoPlay(ref request, ref response, ref session);
                case PaginaAjaxStatus:
                    return ProcesoStatus(ref request, ref response, ref session);
            }
            return true;
        }

        private void PintarPagina(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            // Icono: (no es texto así que lo tratamos de forma distinta)
            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaFavIcon:
                    {
                        response.AddHeader("Content-Type", FavIconMimeType());
                        var writer = new BinaryWriter(response.Body);
                        writer.Write(Convert.FromBase64String(FavIconBase64()));
                        writer.Flush();
                        SetCSSControlCache(ref response);
                        return;
                    }
            }


            var responseBody = new System.Text.StringBuilder();


            switch (request.Uri.LocalPath ?? "")
            {
                case PaginaAjaxStatus:
                    responseBody.Append(CargarAjax());
                    break;
                case PaginaAjaxPlay:
                    responseBody.Append(CargarAjax());
                    break;
                case PaginaAjaxStop:
                    responseBody.Append(CargarAjax());
                    break;
                case PaginaAjaxAddLink:
                    responseBody.Append(CargarAjax());
                    break;
                case PaginaCSS:
                    response.AddHeader("Content-Type", "text/css");
                    responseBody.Append(CSS());
                    SetCSSControlCache(ref response);
                    break;
                case PaginaLogin:
                    responseBody.Append(PintarPaginaLogin());
                    break;
                case PaginaMain:
                    responseBody.Append(PintarPaginaMain(ref session));
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
                // R-P3-1: UTF8Encoding(False) 明确不写 BOM(与 gzip 分支的 GetBytes 路径及
                // StreamingLibraryModule.vb:202 行为一致);Encoding.UTF8 静态实例会带
                // preamble 写入响应体。
                using (var writer = new StreamWriter(response.Body, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true))
                {
                    writer.Write(ResponseBody);
                    writer.Flush();
                }
            }
        }

        #region Proceso páginas

        private bool ProcesoStatus(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            _RespuestaAjaxLocal.Value = "<span class='StatusMuyImportante'><strong>" + Language.GetText("Status") + "</strong>: ";
            switch (Downloader.ControlRemotoObtenerEstado())
            {
                case DownloaderEstado.Descargando:
                    _RespuestaAjaxLocal.Value += "<span class='iconPlay'>" + Language.GetText("Downloading") + "</span>";
                    break;
                case DownloaderEstado.Parado:
                    _RespuestaAjaxLocal.Value += "<span class='iconPause'>" + Language.GetText("Stopped") + "</span>";
                    break;
                case DownloaderEstado.Pausa:
                    _RespuestaAjaxLocal.Value += "<span class='iconPause'>" + Language.GetText("Paused") + "</span>";
                    break;

            }
            _RespuestaAjaxLocal.Value += "</span><br/>" + Constants.vbNewLine;
            _RespuestaAjaxLocal.Value += "<span class='StatusImportante'><strong>" + Language.GetText("Speed") + "</strong>: ";
            var v = Downloader.ControlRemotoObtenerVelocidad();
            if (v.HasValue)
            {
                _RespuestaAjaxLocal.Value += PintarVelocidadDescarga(v.Value);
            }
            else
            {
                _RespuestaAjaxLocal.Value += "-";
            }
            _RespuestaAjaxLocal.Value += "</span><br/>" + Constants.vbNewLine;
            _RespuestaAjaxLocal.Value += "<span class='StatusImportante'><strong>" + Language.GetText("Active downloads") + "</strong>: ";
            var da = Downloader.ControlRemotoObtenerDescargasActivas();
            if (da.HasValue)
            {
                _RespuestaAjaxLocal.Value += da.Value.ToString();
            }
            else
            {
                _RespuestaAjaxLocal.Value += "-";
            }
            _RespuestaAjaxLocal.Value += "</span><br/>" + Constants.vbNewLine;
            _RespuestaAjaxLocal.Value += "<span><strong>" + Language.GetText("Queued, error, completed") + "</strong>: <span style='white-space:nowrap;'>";
            var dec = Downloader.ControlRemotoObtenerDescargasEnCola();
            var de = Downloader.ControlRemotoObtenerDescargasErroneas();
            var dc = Downloader.ControlRemotoObtenerDescargasCompletadas();
            if (dec.HasValue)
            {
                _RespuestaAjaxLocal.Value += dec.Value.ToString() + " / ";
            }
            else
            {
                _RespuestaAjaxLocal.Value += "- / ";
            }
            if (de.HasValue)
            {
                _RespuestaAjaxLocal.Value += de.Value.ToString() + " / ";
            }
            else
            {
                _RespuestaAjaxLocal.Value += "- / ";
            }
            if (dc.HasValue)
            {
                _RespuestaAjaxLocal.Value += dc.Value.ToString();
            }
            else
            {
                _RespuestaAjaxLocal.Value += "-";
            }
            _RespuestaAjaxLocal.Value += "</span></span><br/>" + Constants.vbNewLine;
            _RespuestaAjaxLocal.Value += "<span><strong>" + Language.GetText("Hour") + "</strong>: ";
            _RespuestaAjaxLocal.Value += DateTime.Now.ToString("HH:mm:ss");
            _RespuestaAjaxLocal.Value += "</span>";

            return true;
        }

        private static string PintarVelocidadDescarga(decimal vel)
        {
            string Dato = "KB/s";
            if (vel > 1024m)
            {
                Dato = "MB/s";
                vel = vel / 1024m;
            }
            return vel.ToString("F2") + " " + Dato;
        }

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

        private bool ProcesoStop(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            if (!IsPostBack(ref request) || !ValidateCsrf(ref request, ref session))
            {
                _RespuestaAjaxLocal.Value = "<span class='error'>Forbidden</span>";
                return true;
            }
            Downloader.ControlRemotoParar();
            System.Threading.Thread.Sleep(400);
            _RespuestaAjaxLocal.Value = Language.GetText("Download stopped");
            return true;
        }

        private bool ProcesoPlay(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            if (!IsPostBack(ref request) || !ValidateCsrf(ref request, ref session))
            {
                _RespuestaAjaxLocal.Value = "<span class='error'>Forbidden</span>";
                return true;
            }
            Downloader.ControlRemotoDescargar();
            System.Threading.Thread.Sleep(400);
            _RespuestaAjaxLocal.Value = Language.GetText("Download started");
            return true;
        }

        private bool ProcesoLogin(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            if (request.Param is not null)
            {
                if (request.Param["Password"] is not null & IsPostBack(ref request))
                {
                    // 登录限速:失败锁定或并发超限时直接拒绝,不执行 PBKDF2
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
                        string submittedPassword = request.Param["Password"].Value ?? "";
                        if (FixedTimeEquals(DeriveLoginHash(submittedPassword, _PasswordSalt), _PasswordHash))
                        {
                            session["Logueado"] = "1";
                            session["LoginDate"] = DateTime.Now;
                            EnsureCsrf(ref session);
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

        private bool ProcesoLogout(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            session["Logueado"] = "0";
            response.Redirect(PaginaLogin);
            return false;
        }

        private bool ProcesoAddLinks(ref IHttpRequest request, ref IHttpResponse response, ref IHttpSession session)
        {
            _RespuestaAjaxLocal.Value = ErrorAjax(Language.GetText("Invalid links")); // Por defecto mostramos un error, si va bien lo cambiamos
            if (request.Param is not null)
            {
                if (request.Param["links"] is not null & request.Param["pckname"] is not null & request.Param["createdirpck"] is not null & IsPostBack(ref request) && ValidateCsrf(ref request, ref session))


                {

                    string links = request.Param["links"].Value;
                    string createdirpck = request.Param["createdirpck"].Value;
                    string pckname = request.Param["pckname"].Value;

                    System.Threading.Thread.Sleep(400);

                    if (string.IsNullOrEmpty(links))
                    {
                        _RespuestaAjaxLocal.Value = ErrorAjax(Language.GetText("Invalid links"));
                    }
                    else if (createdirpck == "true" & string.IsNullOrEmpty(pckname))
                    {
                        _RespuestaAjaxLocal.Value = ErrorAjax(Language.GetText("Invalid package name"));
                    }
                    else
                    {
                        string respuesta = Downloader.ControlRemotoAgregarLinks(links, pckname, createdirpck == "true");
                        if (string.IsNullOrEmpty(respuesta))
                        {
                            _RespuestaAjaxLocal.Value = Language.GetText("Links added successfully");
                        }
                        else
                        {
                            _RespuestaAjaxLocal.Value = ErrorAjax(respuesta);
                        }

                    }
                }
            }
            return true;
        }


        #endregion

        #region Pintado

        private string PintarComun(ref string str)
        {
            return str.Replace(CONST_CONTENT, "").Replace(CONST_ERROR, _ErrorLocal.Value).Replace(CONST_HEAD, "").Replace(CONST_JAVASCRIPT, "").Replace(CONST_TITLE, "");
        }

        private string PintarPaginaLogin()
        {
            string argstr = Template().Replace(CONST_CONTENT, BodyLogin()).Replace(CONST_HEAD, HeadMain(false)).Replace(CONST_TITLE, "Login");
            return PintarComun(ref argstr);
        }
        private string PintarPaginaMain(ref IHttpSession session)
        {
            string csrf = EnsureCsrf(ref session);
            string js = "var CSRF_TOKEN='" + csrf + "';" + Constants.vbNewLine + BodyJavaScript();
            string argstr = Template().Replace(CONST_CONTENT, BodyMain()).Replace(CONST_JAVASCRIPT, js).Replace(CONST_HEAD, HeadMain(true)).Replace(CONST_TITLE, _TitlePersonalizado);


            return PintarComun(ref argstr);
        }

        #endregion

        #region Carga templates y ficheros

        private string FavIconMimeType()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIconMimeType").InnerText;
        }
        private string FavIconBase64()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIcon").InnerText;
        }

        private string CSS()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Content/CSS").InnerText;
        }

        private string Template()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Template/HtmlTemplate").InnerText;
        }


        private string HeadMain(bool Logueado)
        {
            string str = "";
            if (Logueado)
            {
                str += "<link rel=\"shortcut icon\" href=\"/favicon.ico\" type=\"" + FavIconMimeType() + "\" />" + Constants.vbNewLine;
            }
            str += XmlTemplateData.DocumentElement.SelectSingleNode("Content/HeadMain").InnerText;
            return str;
        }
        private string BodyJavaScript()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyJavaScript").InnerText;
        }
        private string BodyMain()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyMain").InnerText;
        }

        private string BodyLogin()
        {
            return XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyLogin").InnerText;
        }




        private string CargarAjax()
        {
            return _RespuestaAjaxLocal.Value ?? "";   // the old field defaulted to "", keep that
        }


        private void SetError(string msj)
        {
            _ErrorLocal.Value = "<br/><div class='error'>" + msj + "</div><br/>";
        }

        private string ErrorAjax(string msj)
        {
            return "<span class='error'>" + msj + "</span>";
        }
        #endregion




    }
}