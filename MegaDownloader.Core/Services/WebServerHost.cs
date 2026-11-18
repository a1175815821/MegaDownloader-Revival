using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MegaDownloader
{
    /// <summary>
    /// Owns the two optional HTTP servers: the browser remote control and the streaming
    /// library. This replaces the embedded <c>HttpServer.dll</c> with Kestrel so the
    /// feature works on macOS and Linux as well.
    /// </summary>
    public sealed class WebServerHost : IDisposable
    {
        internal const string RemoteControlCookieName = "Sd_session";
        internal const string StreamingCookieName = "Sd_streaming";

        private static readonly object Gate = new object();
        private static WebApplication _remote;
        private static WebApplication _streaming;
        private static int _remotePort;
        private static int _streamingPort;

        /// <summary>
        /// Starts the servers the configuration asks for and returns one message per
        /// failure. The two servers are independent: a busy remote-control port must not
        /// prevent the streaming server from starting.
        /// </summary>
        public static string Start(IDownloaderService downloader, Configuracion config)
        {
            if (config is null)
                return string.Empty;

            var errors = new List<string>();
            if (!config.ServidorWebActivo)
                StopRemoteControl();
            if (!config.ServidorStreamingActivo)
                StopStreaming();

            string remoteError = StartRemoteControl(downloader, config);
            if (!string.IsNullOrEmpty(remoteError))
                errors.Add(remoteError);

            string streamingError = StartStreaming(config);
            if (!string.IsNullOrEmpty(streamingError))
                errors.Add(streamingError);

            return string.Join(" ", errors);
        }

        public static void Stop()
        {
            StopRemoteControl();
            StopStreaming();
        }

        public static void StopRemoteControl()
        {
            lock (Gate)
            {
                if (_remote is null)
                    return;
                try
                {
                    RunBlocking(() => _remote.StopAsync(TimeSpan.FromSeconds(5)));
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error stopping the web server: " + Log.SafeException(ex));
                }
                finally
                {
                    RunBlocking(() => _remote.DisposeAsync().AsTask());
                    _remote = null;
                    _remotePort = 0;
                }
            }
        }

        public static void StopStreaming()
        {
            lock (Gate)
            {
                if (_streaming is null)
                    return;
                try
                {
                    RunBlocking(() => _streaming.StopAsync(TimeSpan.FromSeconds(5)));
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error stopping the streaming server: " + Log.SafeException(ex));
                }
                finally
                {
                    RunBlocking(() => _streaming.DisposeAsync().AsTask());
                    _streaming = null;
                    _streamingPort = 0;
                }
            }
        }

        /// <summary>Ports actually bound, for the UI (0 when the server is not running).</summary>
        public static int RemoteControlPort
        {
            get { lock (Gate) return _remotePort; }
        }

        public static int StreamingPort
        {
            get { lock (Gate) return _streamingPort; }
        }

        private static string StartRemoteControl(IDownloaderService downloader, Configuracion config)
        {
            lock (Gate)
            {
                if (_remote is not null || !config.ServidorWebActivo)
                    return string.Empty;

                if (config.ServidorWebPuerto < 1)
                    return "Server port is not configured (port must be between 1024 and 65535).";
                if (IsPortBusy(config.ServidorWebPuerto))
                    return "Port " + config.ServidorWebPuerto + " is not valid or is in use.";

                // Default posture: loopback only. LAN access is opt-in and requires a
                // password — an open listener would let anyone on the network drive the
                // downloader.
                IPAddress bindAddress = IPAddress.Loopback;
                if (config.ServidorWebPermitirLAN)
                {
                    if (string.IsNullOrEmpty(config.ServidorWebPassword))
                        return "A web server password is required to allow LAN access.";

                    if (!string.IsNullOrEmpty(config.ServidorWebBindIP))
                    {
                        if (!IPAddress.TryParse(config.ServidorWebBindIP, out bindAddress))
                            return "Invalid web server bind IP: " + config.ServidorWebBindIP;
                        Log.WriteWarning("Web server: LAN access enabled — binding to " + bindAddress + " (password required).");
                    }
                    else
                    {
                        bindAddress = IPAddress.Any;
                        Log.WriteWarning("Web server: LAN access enabled — listening on all interfaces (password required).");
                    }
                }

                try
                {
                    var responder = new RemoteControlResponder(
                        downloader,
                        config.ServidorWebRutaPlantilla,
                        config.ServidorWebPassword,
                        config.ServidorWebNombre,
                        config.ServidorWebTimeout * 60);

                    WebApplication app = BuildApp(bindAddress, config.ServidorWebPuerto,
                        context => responder.HandleAsync(context, context.RequestAborted));
                    RunBlocking(() => app.StartAsync());
                    _remote = app;
                    _remotePort = config.ServidorWebPuerto;
                }
                catch (Exception ex)
                {
                    _remote = null;
                    _remotePort = 0;
                    Log.WriteError("Error starting web server: " + Log.SafeException(ex));
                    return ex.Message;
                }

                return string.Empty;
            }
        }

        private static string StartStreaming(Configuracion config)
        {
            lock (Gate)
            {
                if (_streaming is not null || !config.ServidorStreamingActivo)
                    return string.Empty;

                if (config.ServidorStreamingPuerto < 1)
                    return "Server port is not configured (port must be between 1024 and 65535).";
                if (IsPortBusy(config.ServidorStreamingPuerto))
                    return "Port " + config.ServidorStreamingPuerto + " is not valid or is in use.";

                try
                {
                    var media = new StreamingMediaResponder(config);
                    var library = new StreamingLibraryResponder(config);

                    WebApplication app = BuildApp(IPAddress.Loopback, config.ServidorStreamingPuerto, context =>
                        (context.Request.Path.Value ?? string.Empty) == StreamingMediaResponder.PageStreaming
                            ? media.HandleAsync(context, context.RequestAborted)
                            : library.HandleAsync(context, context.RequestAborted));
                    RunBlocking(() => app.StartAsync());
                    _streaming = app;
                    _streamingPort = config.ServidorStreamingPuerto;
                }
                catch (Exception ex)
                {
                    _streaming = null;
                    _streamingPort = 0;
                    Log.WriteError("Error starting streaming web server: " + Log.SafeException(ex));
                    return ex.Message;
                }

                return string.Empty;
            }
        }

        private static WebApplication BuildApp(IPAddress address, int port, RequestDelegate handler)
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                // Kestrel only: no configuration files, no environment probing.
                ContentRootPath = AppContext.BaseDirectory
            });

            // The servers are an optional background feature; their request logging would
            // otherwise flood the application log.
            builder.Logging.ClearProviders();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Listen(address, port);
            });

            WebApplication app = builder.Build();
            app.Run(handler);
            return app;
        }

        /// <summary>
        /// Runs an async operation to completion without capturing the caller's
        /// synchronization context. Kestrel's start/stop continuations are posted back to
        /// the context they were awaited on, so a plain GetResult() from a UI thread (or
        /// the WinForms test harness) deadlocks.
        /// </summary>
        private static void RunBlocking(Func<Task> operation) =>
            Task.Run(operation).ConfigureAwait(false).GetAwaiter().GetResult();

        private static bool IsPortBusy(int port)
        {
            try
            {
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                foreach (IPEndPoint endpoint in properties.GetActiveTcpListeners())
                {
                    if (endpoint.Port == port)
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Port check failed for " + port + ": " + Log.SafeException(ex));
            }
            return false;
        }

        public void Dispose() => Stop();
    }
}
