using System;
using System.Collections.Generic;

namespace MegaDownloader
{
    public class ServidorWebController
    {

        private static HttpServer.HttpServer _WebServer = null;
        private static HttpServer.HttpServer _WebServerStreaming = null;


        public static string StartWebServer(IDownloaderService Downloader, Configuracion Config)
        {
            if (Config is null)
            {
                return "";
            }


            if (!Config.ServidorWebActivo)
            {
                StopWebServer_RemoteController();
            }
            if (!Config.ServidorStreamingActivo)
            {
                StopWebServer_Streaming();
            }

            // 两个服务器彼此独立:Web 端口被占用不得连带让流媒体也起不来(此前早 Return
            // 直接跳过了整个流媒体启动块)。分别启动,末尾汇总错误。
            var errores = new List<string>();
            string errWeb = StartRemoteController(Downloader, Config);
            if (!string.IsNullOrEmpty(errWeb))
                errores.Add(errWeb);
            string errStreaming = StartStreamingServer(Downloader, Config);
            if (!string.IsNullOrEmpty(errStreaming))
                errores.Add(errStreaming);
            return string.Join(" ", errores.ToArray());

        }

        private static string StartRemoteController(IDownloaderService Downloader, Configuracion Config)
        {
            if (_WebServer is not null || !Config.ServidorWebActivo)
                return "";

            if (Config.ServidorWebPuerto < 1)
            {
                return "Server port is not configured (port must be between 1024 and 65535).";
            }
            if (IsBusy(Config.ServidorWebPuerto))
            {
                return "Port " + Config.ServidorWebPuerto + " is not valid or is in use.";
            }

            // Default security posture: bind loopback only. Opt-in LAN access
            // binds all interfaces so phones/other PCs can push downloads, but a
            // password is MANDATORY — an open LAN listener would let anyone on
            // the network control the downloader.
            var bindAddress = System.Net.IPAddress.Loopback;
            if (Config.ServidorWebPermitirLAN)
            {
                if (string.IsNullOrEmpty(Config.ServidorWebPassword))
                {
                    return "A web server password is required to allow LAN access.";
                }
                if (!string.IsNullOrEmpty(Config.ServidorWebBindIP))
                {
                    try
                    {
                        bindAddress = System.Net.IPAddress.Parse(Config.ServidorWebBindIP);
                    }
                    catch (FormatException ex)
                    {
                        return "Invalid web server bind IP: " + Config.ServidorWebBindIP;
                    }
                    Log.WriteWarning("Web server: LAN access enabled — binding to " + bindAddress.ToString() + " (password required).");
                }
                else
                {
                    bindAddress = System.Net.IPAddress.Any;
                    Log.WriteWarning("Web server: LAN access enabled — listening on all interfaces (password required).");
                }
            }

            try
            {
                _WebServer = new HttpServer.HttpServer();
                _WebServer.ServerName = "Internal";
                _WebServer.SessionCookieName = "Sd_session";
                _WebServer.Add(new WebInterfaceModule(Downloader, Config.ServidorWebRutaPlantilla, Config.ServidorWebPassword, Config.ServidorWebNombre, Config.ServidorWebTimeout * 60, Language.GetCurrentLanguageCode()));




                _WebServer.Start(bindAddress, Config.ServidorWebPuerto);
            }
            catch (Exception ex)
            {
                _WebServer = null;
                Log.WriteError("Error starting web server: " + ex.ToString());
                return ex.Message;
            }

            return "";
        }

        private static string StartStreamingServer(IDownloaderService Downloader, Configuracion Config)
        {
            if (_WebServerStreaming is not null || !Config.ServidorStreamingActivo)
                return "";

            if (Config.ServidorStreamingPuerto < 1)
            {
                return "Server port is not configured (port must be between 1024 and 65535).";
            }
            if (IsBusy(Config.ServidorStreamingPuerto))
            {
                return "Port " + Config.ServidorStreamingPuerto + " is not valid or is in use.";
            }

            try
            {
                _WebServerStreaming = new HttpServer.HttpServer();
                _WebServerStreaming.ServerName = "Streaming";
                // Must differ from the remote controller's cookie name: both servers bind
                // 127.0.0.1 and browsers scope cookies by host, not by port, so a shared
                // name made the two sessions overwrite each other and forced re-login.
                _WebServerStreaming.SessionCookieName = "Sd_streaming";
                _WebServerStreaming.Add(new StreamingModule(ref Config));
                _WebServerStreaming.Add(new StreamingLibraryModule(Downloader, ref Config));
                _WebServerStreaming.Start(System.Net.IPAddress.Loopback, Config.ServidorStreamingPuerto);
            }
            catch (Exception ex)
            {
                _WebServerStreaming = null;
                Log.WriteError("Error starting streaming web server: " + ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public static void StopWebServer()
        {
            StopWebServer_RemoteController();
            StopWebServer_Streaming();
        }

        public static void StopWebServer_RemoteController()
        {
            try
            {
                if (_WebServer is not null)
                {
                    _WebServer.Stop();
                    _WebServer = null;
                }
            }

            catch (Exception ex)
            {
                Log.WriteError("Error stopping web server: " + ex.ToString());
            }

        }

        public static void StopWebServer_Streaming()
        {
            try
            {

                if (_WebServerStreaming is not null)
                {
                    _WebServerStreaming.Stop();
                    _WebServerStreaming = null;
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Error stopping streaming web server: " + ex.ToString());
            }

        }


        private static bool IsBusy(int port)
        {
            var ipGP = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            System.Net.IPEndPoint[] endpoints = ipGP.GetActiveTcpListeners();
            if (endpoints is null || endpoints.Length == 0)
            {
                return false;
            }
            for (int i = 0, loopTo = endpoints.Length - 1; i <= loopTo; i++)
            {
                if (endpoints[i].Port == port)
                {
                    return true;
                }
            }
            return false;
        }




    }
}