using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;

namespace MegaDownloader
{
    /// <summary>
    /// Browser remote control: status, start, stop and link intake for the running
    /// downloader. This is the Kestrel replacement for the embedded HttpServer
    /// <c>WebInterfaceModule</c>; the page contract, the login flow and the CSRF rules are
    /// unchanged so existing bookmarks and templates keep working.
    /// </summary>
    internal sealed class RemoteControlResponder
    {
        internal const string PageLogin = "/login";
        internal const string PageLogout = "/logout";
        internal const string PageMain = "/main";
        internal const string PageCss = "/style.css";
        internal const string PageFavIcon = "/favicon.ico";
        internal const string PageAjaxStatus = "/ajax_status";
        internal const string PageAjaxStop = "/ajax_stopdownload";
        internal const string PageAjaxPlay = "/ajax_resumedownload";
        internal const string PageAjaxAddLink = "/ajax_addlink";

        private const string ContentToken = "%CONTENT%";
        private const string HeadToken = "%HEAD%";
        private const string JavaScriptToken = "%JAVASCRIPT%";
        private const string TitleToken = "%TITLE%";
        private const string ErrorToken = "%ERROR%";

        // Login throttle: every attempt runs PBKDF2 (100k iterations) on the request
        // thread, so an unthrottled POST flood would starve the thread pool.
        private const int MaxConcurrentLogins = 4;
        private const int MaxLoginFailuresPerWindow = 10;
        private const int LoginFailureWindowSeconds = 60;

        private readonly IDownloaderService _downloader;
        private readonly XmlDocument _template;
        private readonly string _title;
        private readonly int _sessionTimeoutSeconds;
        private readonly byte[] _passwordSalt;
        private readonly byte[] _passwordHash;
        private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
        private readonly object _loginRateLock = new object();
        private readonly List<DateTime> _loginFailures = new List<DateTime>();
        private int _activeLogins;

        internal RemoteControlResponder(IDownloaderService downloader, string templatePath, string password,
            string title, int sessionTimeoutSeconds)
        {
            _downloader = downloader;
            _template = WebTemplates.LoadRemoteControlTemplate(templatePath);
            CheckTemplate(_template);
            _title = string.IsNullOrEmpty(title)
                ? InternalConfiguration.ObtenerNombreApp() + InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER")
                : title;
            _sessionTimeoutSeconds = sessionTimeoutSeconds;

            _passwordSalt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(_passwordSalt);
            _passwordHash = DeriveLoginHash(password ?? string.Empty, _passwordSalt);
        }

        internal async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            Session session = GetSession(context);
            // A GET renders the page from a clean slate; a POST carries the error of the
            // submission it is answering.
            if (context.Request.Method != HttpMethods.Post)
                session.Error = string.Empty;

            if (!CheckAccess(context, session, out bool handled))
            {
                // A handled request already wrote its own body (an AJAX error); otherwise
                // the access check points at the page the client should go to.
                if (!handled)
                    context.Response.Redirect(session.LastRedirect ?? PageLogin);
                return;
            }

            if (!await ProcessPageAsync(context, session, cancellationToken).ConfigureAwait(false))
                return;

            await PaintPageAsync(context, session, session.Error, cancellationToken).ConfigureAwait(false);
        }

        private static void CheckTemplate(XmlDocument document)
        {
            if (document?.DocumentElement is null)
                throw new ApplicationException("Error template not found");
            Require(document, "Template/HtmlTemplate");
            Require(document, "Template/FavIcon");
            Require(document, "Template/FavIconMimeType");
            Require(document, "Content/HeadMain");
            Require(document, "Content/BodyMain");
            Require(document, "Content/BodyJavaScript");
            Require(document, "Content/BodyLogin");
            Require(document, "Content/CSS");
        }

        private static void Require(XmlDocument document, string path)
        {
            if (document.DocumentElement.SelectSingleNode(path) is null)
                throw new ApplicationException("Template is not correct, missing node " + path);
        }

        private bool CheckAccess(HttpContext context, Session session, out bool handled)
        {
            handled = false;
            string path = context.Request.Path.Value ?? string.Empty;

            bool requiresLogin = false;
            bool isAjax = false;
            switch (path)
            {
                case PageMain:
                    requiresLogin = true;
                    break;
                case PageAjaxStatus:
                case PageAjaxStop:
                case PageAjaxPlay:
                case PageAjaxAddLink:
                    requiresLogin = true;
                    isAjax = true;
                    break;
            }

            if (requiresLogin && !IsLoggedIn(session))
            {
                if (isAjax)
                {
                    WriteAjaxBody(context, ErrorAjax(Language.GetText("Error, session expired")));
                    handled = true;
                }
                else
                {
                    session.LastRedirect = PageLogin;
                }
                return false;
            }

            switch (path)
            {
                case PageLogin:
                case PageLogout:
                case PageMain:
                case PageCss:
                case PageFavIcon:
                case PageAjaxStatus:
                case PageAjaxPlay:
                case PageAjaxStop:
                case PageAjaxAddLink:
                    return true;
                default:
                    // Unknown pages go to the main page, matching the original module.
                    session.LastRedirect = PageMain;
                    return false;
            }
        }

        private async Task<bool> ProcessPageAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            switch (context.Request.Path.Value ?? string.Empty)
            {
                case PageLogin:
                    return await ProcessLoginAsync(context, session, cancellationToken).ConfigureAwait(false);
                case PageLogout:
                    session.LoggedIn = false;
                    session.LoginDate = default;
                    context.Response.Redirect(PageLogin);
                    return false;
                case PageAjaxAddLink:
                    await ProcessAddLinksAsync(context, session, cancellationToken).ConfigureAwait(false);
                    return true;
                case PageAjaxStop:
                    await ProcessStopAsync(context, session, cancellationToken).ConfigureAwait(false);
                    return true;
                case PageAjaxPlay:
                    await ProcessPlayAsync(context, session, cancellationToken).ConfigureAwait(false);
                    return true;
                case PageAjaxStatus:
                    ProcessStatus(session);
                    return true;
                default:
                    return true;
            }
        }

        private async Task<bool> ProcessLoginAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            if (context.Request.Method != HttpMethods.Post)
                return true;

            IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            string submitted = form["Password"];
            if (submitted is null)
                return true;

            if (LoginLockedOut())
            {
                session.Error = Language.GetText("Too many login attempts, please try again later");
                return true;
            }

            if (Interlocked.Increment(ref _activeLogins) > MaxConcurrentLogins)
            {
                Interlocked.Decrement(ref _activeLogins);
                session.Error = Language.GetText("Too many login attempts, please try again later");
                return true;
            }

            try
            {
                if (FixedTimeEquals(DeriveLoginHash(submitted, _passwordSalt), _passwordHash))
                {
                    session.LoggedIn = true;
                    session.LoginDate = DateTime.Now;
                    EnsureCsrf(session);
                    context.Response.Redirect(PageMain);
                    return false;
                }

                RecordLoginFailure();
                session.Error = Language.GetText("Invalid password");
                return true;
            }
            finally
            {
                Interlocked.Decrement(ref _activeLogins);
            }
        }

        private async Task ProcessStopAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (context.Request.Method != HttpMethods.Post || !ValidateCsrf(session, form["csrf"]))
            {
                session.Ajax = "<span class='error'>Forbidden</span>";
                return;
            }

            _downloader.ControlRemotoParar();
            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            session.Ajax = Language.GetText("Download stopped");
        }

        private async Task ProcessPlayAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (context.Request.Method != HttpMethods.Post || !ValidateCsrf(session, form["csrf"]))
            {
                session.Ajax = "<span class='error'>Forbidden</span>";
                return;
            }

            _downloader.ControlRemotoDescargar();
            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            session.Ajax = Language.GetText("Download started");
        }

        private async Task ProcessAddLinksAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            session.Ajax = ErrorAjax(Language.GetText("Invalid links"));
            if (context.Request.Method != HttpMethods.Post)
                return;

            IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            string links = form["links"];
            string packageName = form["pckname"];
            string createDirectory = form["createdirpck"];
            if (links is null || packageName is null || createDirectory is null || !ValidateCsrf(session, form["csrf"]))
                return;

            await Task.Delay(400, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(links))
            {
                session.Ajax = ErrorAjax(Language.GetText("Invalid links"));
            }
            else if (string.Equals(createDirectory, "true", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(packageName))
            {
                session.Ajax = ErrorAjax(Language.GetText("Invalid package name"));
            }
            else
            {
                string response = _downloader.ControlRemotoAgregarLinks(
                    links, packageName, string.Equals(createDirectory, "true", StringComparison.OrdinalIgnoreCase));
                session.Ajax = string.IsNullOrEmpty(response)
                    ? Language.GetText("Links added successfully")
                    : ErrorAjax(response);
            }
        }

        private void ProcessStatus(Session session)
        {
            var text = new StringBuilder();
            text.Append("<span class='StatusMuyImportante'><strong>").Append(Language.GetText("Status")).Append("</strong>: ");
            switch (_downloader.ControlRemotoObtenerEstado())
            {
                case DownloaderEstado.Descargando:
                    text.Append("<span class='iconPlay'>").Append(Language.GetText("Downloading")).Append("</span>");
                    break;
                case DownloaderEstado.Pausa:
                    text.Append("<span class='iconPause'>").Append(Language.GetText("Paused")).Append("</span>");
                    break;
                default:
                    text.Append("<span class='iconPause'>").Append(Language.GetText("Stopped")).Append("</span>");
                    break;
            }

            text.Append("</span><br/>\n");
            text.Append("<span class='StatusImportante'><strong>").Append(Language.GetText("Speed")).Append("</strong>: ");
            decimal? speed = _downloader.ControlRemotoObtenerVelocidad();
            text.Append(speed.HasValue ? FormatSpeed(speed.Value) : "-");
            text.Append("</span><br/>\n");

            text.Append("<span class='StatusImportante'><strong>").Append(Language.GetText("Active downloads")).Append("</strong>: ");
            int? active = _downloader.ControlRemotoObtenerDescargasActivas();
            text.Append(active.HasValue ? active.Value.ToString() : "-");
            text.Append("</span><br/>\n");

            text.Append("<span><strong>").Append(Language.GetText("Queued, error, completed")).Append("</strong>: <span style='white-space:nowrap;'>");
            int? queued = _downloader.ControlRemotoObtenerDescargasEnCola();
            int? failed = _downloader.ControlRemotoObtenerDescargasErroneas();
            int? completed = _downloader.ControlRemotoObtenerDescargasCompletadas();
            text.Append(queued.HasValue ? queued.Value.ToString() : "-").Append(" / ");
            text.Append(failed.HasValue ? failed.Value.ToString() : "-").Append(" / ");
            text.Append(completed.HasValue ? completed.Value.ToString() : "-");
            text.Append("</span></span><br/>\n");

            text.Append("<span><strong>").Append(Language.GetText("Hour")).Append("</strong>: ");
            text.Append(DateTime.Now.ToString("HH:mm:ss")).Append("</span>");

            session.Ajax = text.ToString();
        }

        private async Task PaintPageAsync(HttpContext context, Session session, string error, CancellationToken cancellationToken)
        {
            string path = context.Request.Path.Value ?? string.Empty;

            if (path == PageFavIcon)
            {
                context.Response.ContentType = FavIconMimeType();
                context.Response.Headers.CacheControl = "public, max-age=604800";
                byte[] icon = Convert.FromBase64String(FavIconBase64());
                context.Response.ContentLength = icon.Length;
                await context.Response.Body.WriteAsync(icon.AsMemory(), cancellationToken).ConfigureAwait(false);
                return;
            }

            string body;
            switch (path)
            {
                case PageAjaxStatus:
                case PageAjaxPlay:
                case PageAjaxStop:
                case PageAjaxAddLink:
                    body = session.Ajax ?? string.Empty;
                    break;
                case PageCss:
                    context.Response.ContentType = "text/css";
                    context.Response.Headers.CacheControl = "public, max-age=604800";
                    body = Css();
                    break;
                case PageLogin:
                    body = RenderLogin(error);
                    break;
                case PageMain:
                    body = RenderMain(session);
                    break;
                default:
                    body = string.Empty;
                    break;
            }

            await WriteHtmlAsync(context, body, cancellationToken).ConfigureAwait(false);
        }

        private static async Task WriteHtmlAsync(HttpContext context, string body, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
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

            if (string.IsNullOrEmpty(context.Response.ContentType))
                context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        private void WriteAjaxBody(HttpContext context, string text)
        {
            // UTF8Encoding(false) writes no BOM: a preamble would break strict JSON readers.
            byte[] bytes = new UTF8Encoding(false).GetBytes(text ?? string.Empty);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength = bytes.Length;
            context.Response.Body.Write(bytes, 0, bytes.Length);
        }

        private string RenderLogin(string error)
        {
            string html = Template().Replace(ContentToken, BodyLogin()).Replace(HeadToken, HeadMain(false)).Replace(TitleToken, "Login");
            return FillCommon(html, error);
        }

        private string RenderMain(Session session)
        {
            string csrf = EnsureCsrf(session);
            string script = "var CSRF_TOKEN='" + csrf + "';\n" + BodyJavaScript();
            string html = Template()
                .Replace(ContentToken, BodyMain())
                .Replace(JavaScriptToken, script)
                .Replace(HeadToken, HeadMain(true))
                .Replace(TitleToken, _title);
            return FillCommon(html, null);
        }

        private static string FillCommon(string html, string error) => html
            .Replace(ContentToken, string.Empty)
            .Replace(ErrorToken, error ?? string.Empty)
            .Replace(HeadToken, string.Empty)
            .Replace(JavaScriptToken, string.Empty)
            .Replace(TitleToken, string.Empty);

        private string Node(string path) => _template.DocumentElement.SelectSingleNode(path)?.InnerText ?? string.Empty;

        private string FavIconMimeType() => Node("Template/FavIconMimeType");
        private string FavIconBase64() => Node("Template/FavIcon");
        private string Css() => Node("Content/CSS");
        private string Template() => Node("Template/HtmlTemplate");
        private string BodyJavaScript() => Node("Content/BodyJavaScript");
        private string BodyMain() => Node("Content/BodyMain");
        private string BodyLogin() => Node("Content/BodyLogin");

        private string HeadMain(bool loggedIn)
        {
            string head = string.Empty;
            if (loggedIn)
                head += "<link rel=\"shortcut icon\" href=\"/favicon.ico\" type=\"" + FavIconMimeType() + "\" />\n";
            return head + Node("Content/HeadMain");
        }

        private Session GetSession(HttpContext context)
        {
            string id = context.Request.Cookies[WebServerHost.RemoteControlCookieName];
            if (string.IsNullOrEmpty(id))
            {
                id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                context.Response.Cookies.Append(WebServerHost.RemoteControlCookieName, id, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
            }

            return _sessions.GetOrAdd(id, _ => new Session());
        }

        private bool IsLoggedIn(Session session)
        {
            if (!session.LoggedIn)
                return false;
            if (_sessionTimeoutSeconds > 0 && session.LoginDate != default &&
                session.LoginDate.AddSeconds(_sessionTimeoutSeconds) <= DateTime.Now)
            {
                session.LoggedIn = false;
                session.LoginDate = default;
                return false;
            }
            return true;
        }

        private static string EnsureCsrf(Session session)
        {
            if (string.IsNullOrEmpty(session.Csrf))
                session.Csrf = Guid.NewGuid().ToString("N");
            return session.Csrf;
        }

        private static bool ValidateCsrf(Session session, string supplied) =>
            !string.IsNullOrEmpty(supplied) && string.Equals(supplied, EnsureCsrf(session), StringComparison.Ordinal);

        private bool LoginLockedOut()
        {
            lock (_loginRateLock)
            {
                DateTime cutoff = DateTime.Now.AddSeconds(-LoginFailureWindowSeconds);
                _loginFailures.RemoveAll(stamp => stamp < cutoff);
                return _loginFailures.Count >= MaxLoginFailuresPerWindow;
            }
        }

        private void RecordLoginFailure()
        {
            lock (_loginRateLock)
                _loginFailures.Add(DateTime.Now);
        }

        private static byte[] DeriveLoginHash(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password ?? string.Empty, salt, 100000, HashAlgorithmName.SHA1);
            return pbkdf2.GetBytes(32);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left is null || right is null || left.Length != right.Length)
                return false;
            return CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static string FormatSpeed(decimal kilobytesPerSecond)
        {
            string unit = "KB/s";
            if (kilobytesPerSecond > 1024m)
            {
                unit = "MB/s";
                kilobytesPerSecond /= 1024m;
            }
            return kilobytesPerSecond.ToString("F2") + " " + unit;
        }

        private static string ErrorAjax(string message) => "<span class='error'>" + message + "</span>";

        private sealed class Session
        {
            internal volatile bool LoggedIn;
            internal DateTime LoginDate;
            internal string Csrf;
            internal volatile string Ajax;
            internal volatile string Error;
            internal string LastRedirect;
        }
    }
}
