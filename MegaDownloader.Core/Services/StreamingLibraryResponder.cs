using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MegaDownloader
{
    /// <summary>
    /// Streaming library pages and the JSON API behind them (<c>/library</c>,
    /// <c>/manage</c>, <c>/ajax</c>). Kestrel replacement for the embedded HttpServer
    /// <c>StreamingLibraryModule</c>; the JSON contract is unchanged so the existing
    /// jQuery page keeps working.
    /// </summary>
    public sealed class StreamingLibraryResponder
    {
        internal const string PageAjax = "/ajax";
        internal const string PageLogin = "/login";

        private const int MaxConcurrentLogins = 4;
        private const int MaxLoginFailuresPerWindow = 10;
        private const int LoginFailureWindowSeconds = 60;
        private const int SessionTimeoutSeconds = 3600 * 24 * 7;

        // Not readonly: LibraryElement.ToJSON and ImportLinks take the configuration by ref.
        private Configuracion _config;
        private readonly string _templateManager;
        private readonly string _templateMain;
        private readonly string _templateLogin;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Session> _sessions =
            new(StringComparer.Ordinal);

        private readonly object _loginRateLock = new object();
        private readonly List<DateTime> _loginFailures = new List<DateTime>();
        private int _activeLogins;

        public StreamingLibraryResponder(Configuracion config)
        {
            _config = config;
            _templateManager = WebTemplates.ReadRequired("StreamingLibraryManagerTemplateData.htm");
            _templateMain = WebTemplates.ReadRequired("StreamingLibraryTemplateData.htm");
            _templateLogin = WebTemplates.ReadRequired("StreamingLibraryTemplateLogin.htm");
        }

        internal async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            try
            {
                Session session = GetSession(context);
                if (context.Request.Method != HttpMethods.Post)
                    session.Error = string.Empty;

                string path = context.Request.Path.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(_config.ServidorStreamingPassword) && !IsLoggedIn(session) && path != PageLogin)
                {
                    context.Response.Redirect(PageLogin);
                    return;
                }

                switch (path)
                {
                    case StreamingLibraryRoutes.PaginaMain:
                    case StreamingLibraryRoutes.PaginaManagement:
                    case PageAjax:
                    case PageLogin:
                        break;
                    default:
                        context.Response.Redirect(StreamingLibraryRoutes.PaginaMain);
                        return;
                }

                if (path == PageLogin)
                {
                    if (!await ProcessLoginAsync(context, session, cancellationToken).ConfigureAwait(false))
                        return;
                }
                else if (path == PageAjax)
                {
                    await ProcessAjaxAsync(context, session, cancellationToken).ConfigureAwait(false);
                    return;
                }

                string body = path switch
                {
                    StreamingLibraryRoutes.PaginaManagement => _templateManager.Replace("%CSRF_TOKEN%", EnsureCsrf(session)),
                    StreamingLibraryRoutes.PaginaMain => _templateMain.Replace("%CSRF_TOKEN%", EnsureCsrf(session)),
                    _ => _templateLogin.Replace("%ERROR%", session.Error ?? string.Empty)
                };

                await WriteHtmlAsync(context, body, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.WriteError("Streaming library page failed: " + Log.SafeException(ex));
                if (!context.Response.HasStarted)
                {
                    try
                    {
                        await StreamingMediaResponder
                            .WriteTextAsync(context, StatusCodes.Status500InternalServerError, "Internal server error", cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception sendEx)
                    {
                        Log.WriteError("Streaming library: could not send the error response: " + Log.SafeException(sendEx));
                    }
                }
            }
        }

        private async Task<bool> ProcessLoginAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            if (context.Request.Method != HttpMethods.Post)
                return true;

            IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            string password = form["Password"];
            if (password is null)
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
                if (Criptografia.FixedTimeEquals(
                        Criptografia.Utf8BytesOrNull(password),
                        Criptografia.Utf8BytesOrNull(_config.ServidorStreamingPassword)))
                {
                    session.LoggedIn = true;
                    session.LoginDate = DateTime.Now;
                    // Fresh token for the authenticated session.
                    RotateCsrf(session);
                    context.Response.Redirect(StreamingLibraryRoutes.PaginaMain);
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

        private async Task ProcessAjaxAsync(HttpContext context, Session session, CancellationToken cancellationToken)
        {
            string url = context.Request.Host.Value ?? string.Empty;
            string action = context.Request.Query["Action"];
            IFormCollection form = context.Request.HasFormContentType
                ? await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false)
                : FormCollection.Empty;
            if (string.IsNullOrEmpty(action))
                action = form["Action"];

            if (string.IsNullOrEmpty(action))
            {
                await WriteJsonAsync(context, JsonError("Action not specified"), cancellationToken).ConfigureAwait(false);
                return;
            }

            bool mutating = action is "Delete" or "Save" or "OpenVLC" or "ImportLinks" or "ExportLinks";
            if (mutating && (context.Request.Method != HttpMethods.Post || !ValidateCsrf(session, form["csrf"])))
            {
                await WriteJsonAsync(context, JsonError("Forbidden"), cancellationToken).ConfigureAwait(false);
                return;
            }

            string Parameter(string name)
            {
                string value = form[name];
                return value ?? context.Request.Query[name].ToString();
            }

            string json;
            switch (action)
            {
                case "Load":
                {
                    string elementId = Parameter("Element");
                    if (string.IsNullOrEmpty(elementId))
                    {
                        json = JsonError("Element not specified");
                        break;
                    }
                    LibraryElement element = StreamingLibraryManager.GetElementByID(elementId);
                    json = element is null ? JsonError("Element not found") : JsonData(url, element);
                    break;
                }

                case "Delete":
                {
                    string id = Parameter("ID");
                    if (string.IsNullOrEmpty(id) || !id.All(char.IsDigit))
                    {
                        json = JsonError("ID not specified");
                        break;
                    }
                    StreamingLibraryManager.RemoveElement(id);
                    json = JsonError(string.Empty);
                    break;
                }

                case "Save":
                {
                    json = Save(Parameter, url);
                    break;
                }

                case "LoadAll":
                    json = JsonData(url, StreamingLibraryManager.GetElements());
                    break;

                case "OpenVLC":
                {
                    string streamUrl = Parameter("URL");
                    if (string.IsNullOrEmpty(streamUrl))
                    {
                        json = JsonError("URL not specified");
                        break;
                    }
                    // The page script evaluates the body and reads .error off it, so a
                    // success answer must still be a JSON envelope.
                    json = StreamingHelper.WatchOnline(_config.VLCPath, streamUrl)
                        ? JsonError(string.Empty)
                        : JsonError("VLC could not be loaded");
                    break;
                }

                case "ImportLinks":
                {
                    string links = Parameter("Links");
                    if (links is null)
                    {
                        json = JsonError("Links not specified");
                        break;
                    }

                    if (StreamingLibraryManager.IsImportedLibrary(links))
                    {
                        json = JsonImport(-1, StreamingLibraryManager.ImportLibrary(links));
                        break;
                    }

                    string plain = links;
                    try
                    {
                        string unescaped = Uri.UnescapeDataString(links);
                        if (!string.IsNullOrEmpty(unescaped))
                            plain = unescaped;
                    }
                    catch (UriFormatException)
                    {
                        // A stray '%' is not an escape; import the raw text.
                    }
                    List<string> urls = URLExtractor.ExtraerURLs(plain);
                    json = JsonImport(urls.Count, StreamingLibraryManager.ImportLinks(ref _config, urls));
                    break;
                }

                case "ExportLinks":
                {
                    string ids = Parameter("IDs");
                    if (ids is null)
                    {
                        json = JsonError("IDs not specified");
                        break;
                    }
                    bool hideLinks = bool.TryParse(Parameter("HideLinks"), out bool hide) && hide;
                    bool plainText = bool.TryParse(Parameter("PlainText"), out bool plain) && plain;
                    json = "{\"error\":\"\",\"code\":" +
                           LibraryElement.JsonString(StreamingLibraryManager.ExportElements(ids.Split(';').ToList(), hideLinks, plainText)) +
                           "}";
                    break;
                }

                default:
                    json = JsonError("Action " + action + " not recognized");
                    break;
            }

            await WriteJsonAsync(context, json, cancellationToken).ConfigureAwait(false);
        }

        private string Save(Func<string, string> parameter, string url)
        {
            foreach (string required in new[] { "ID", "IMDB", "Allocine", "Filmaffinity", "Name", "Desc", "Poster", "Comments", "Link" })
            {
                if (parameter(required) is null)
                    return JsonError(required + " not specified");
            }

            string link = parameter("Link");
            if (string.IsNullOrEmpty(link))
                return JsonError("Link not specified");

            if (link != LibraryElement.HIDDEN_LINK_DESC && string.IsNullOrEmpty(Fichero.ExtraerFileID(link)))
                return JsonError("Invalid link");

            string id = parameter("ID");
            string name = parameter("Name");
            string description = parameter("Desc");
            string comments = parameter("Comments");
            string poster = parameter("Poster");
            string imdb = parameter("IMDB");
            string filmaffinity = parameter("Filmaffinity");
            string allocine = parameter("Allocine");

            LibraryElement element = id.All(char.IsDigit)
                ? StreamingLibraryManager.ModifyElement(id, name, description, comments, poster, link, true, imdb, allocine, filmaffinity)
                : StreamingLibraryManager.AddElement(name, description, comments, poster, link, true, imdb, allocine, filmaffinity);

            if (element is null)
                return JsonError(id.All(char.IsDigit) ? "Element not found" : "Element not created");

            return JsonData(url, element);
        }

        private string JsonData(string currentUrl, LibraryElement element) =>
            "{\"error\":\"\",\"Data\":[" + element.ToJSON(currentUrl, ref _config) + "]}";

        private string JsonData(string currentUrl, IEnumerable<LibraryElement> elements)
        {
            var text = new StringBuilder("{\"error\":\"\",\"Data\":[");
            bool first = true;
            foreach (LibraryElement element in elements)
            {
                if (!first)
                    text.Append(',');
                text.Append(element.ToJSON(currentUrl, ref _config));
                first = false;
            }
            return text.Append("]}").ToString();
        }

        private static string JsonError(string message) => "{\"error\":" + LibraryElement.JsonString(message) + "}";

        private static string JsonImport(int received, int imported) =>
            "{\"error\":\"\",\"numR\":" + LibraryElement.JsonString(received.ToString()) +
            ",\"numI\":" + LibraryElement.JsonString(imported.ToString()) + "}";

        private static async Task WriteJsonAsync(HttpContext context, string json, CancellationToken cancellationToken)
        {
            // UTF8Encoding(false) writes no BOM; strict JSON readers reject a preamble.
            byte[] bytes = new UTF8Encoding(false).GetBytes(json ?? string.Empty);
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

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
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

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        private Session GetSession(HttpContext context)
        {
            PruneSessions();

            string id = context.Request.Cookies[WebServerHost.StreamingCookieName];
            if (string.IsNullOrEmpty(id))
            {
                id = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
                context.Response.Cookies.Append(WebServerHost.StreamingCookieName, id, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
            }

            return _sessions.GetOrAdd(id, _ => new Session());
        }

        /// <summary>Drops sessions that timed out. Without this, every anonymous visit to
        /// the library page allocated a session that was never released.</summary>
        private void PruneSessions()
        {
            if (_sessions.Count == 0)
                return;

            DateTime cutoff = DateTime.Now.AddSeconds(-SessionTimeoutSeconds);
            foreach (KeyValuePair<string, Session> entry in _sessions.ToArray())
            {
                Session session = entry.Value;
                if (session.LoginDate != default && session.LoginDate.AddSeconds(SessionTimeoutSeconds) > DateTime.Now)
                    continue;
                if (session.LoginDate == default && session.LastSeenUtc > cutoff)
                    continue;
                if (_sessions.TryRemove(entry.Key, out _))
                    session.LoggedIn = false;
            }
        }

        private bool IsLoggedIn(Session session)
        {
            if (!session.LoggedIn)
                return false;
            if (session.LoginDate != default && session.LoginDate.AddSeconds(SessionTimeoutSeconds) <= DateTime.Now)
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

        /// <summary>Rotates the token so a pre-login token cannot be replayed.</summary>
        private static void RotateCsrf(Session session) =>
            session.Csrf = Guid.NewGuid().ToString("N");

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

        private sealed class Session
        {
            internal volatile bool LoggedIn;
            internal DateTime LoginDate;
            internal DateTime LastSeenUtc = DateTime.Now;
            internal string Csrf;
            internal volatile string Error;
        }
    }
}
