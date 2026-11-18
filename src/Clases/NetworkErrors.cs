using System;
using System.Net;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    /// <summary>
/// issue #6: shared classification of transport-level network failures.
/// TCP connect timeout, DNS pollution/hijack (e.g. the MEGA API host resolved
/// to a non-MEGA IP), firewall/VPN/proxy and TLS trust failures all happen
/// before any API logic runs, so the raw exception text ("Unable to connect
/// to the remote server", SocketException 10060, "The remote server returned
/// an error") tells the user nothing actionable. Surfaces that talk to the
/// network use this helper to show a friendly message while the technical
/// detail stays in the log.
/// </summary>
    public sealed class NetworkErrors
    {

        /// <summary>True when ex (or any inner exception, max depth 6) is a
    /// transport failure rather than an application/API-level error.</summary>
        public static bool IsTransportError(Exception ex)
        {
            var current = ex;
            int guard = 0;
            while (current is not null && guard < 6)
            {
                if (current is System.Net.Sockets.SocketException)
                    return true;
                if (current is System.IO.IOException)
                    return true;
                if (current is TimeoutException)
                    return true;
                WebException wex = current as WebException;
                if (wex is not null)
                {
                    switch (wex.Status)
                    {
                        case WebExceptionStatus.ConnectFailure:
                        case WebExceptionStatus.Timeout:
                        case WebExceptionStatus.NameResolutionFailure:
                        case WebExceptionStatus.ReceiveFailure:
                        case WebExceptionStatus.SendFailure:
                        case WebExceptionStatus.KeepAliveFailure:
                        case WebExceptionStatus.PipelineFailure:
                        case WebExceptionStatus.SecureChannelFailure:
                        case WebExceptionStatus.TrustFailure:
                            return true;
                    }
                }
                current = current.InnerException;
                guard += 1;
            }
            return false;
        }

        /// <summary>Actionable user message for a transport failure; %DETAIL% keeps
    /// the technical detail (the inner SocketException carries the failed
    /// IP:port, e.g. a polluted non-MEGA IP).</summary>
        public static string DescribeTransportError(Exception ex)
        {
            string template = Language.GetText("Network_Error");
            if (string.IsNullOrEmpty(template) || template == "Network_Error")
            {
                template = "Cannot connect to the server (network error)." + Constants.vbCrLf + "Check your internet connection, firewall/VPN/proxy, and try again." + " If the service is restricted in your region, configure a proxy in Configuration or use a VPN." + Constants.vbCrLf + "Technical detail: %DETAIL%";


            }
            // The outer WebException message alone ("unable to connect") hides the
            // useful part, so append the inner exception when present.
            string detail = Log.SafeException(ex);
            if (ex is not null && ex.InnerException is not null)
            {
                detail += " (" + Log.SafeException(ex.InnerException) + ")";
            }
            return template.Replace("%DETAIL%", detail);
        }

        /// <summary>Same friendly message when only a technical detail string is
    /// available (e.g. a short-link resolver that swallowed its exception).</summary>
        public static string DescribeTransportError(string detail)
        {
            return DescribeTransportError(new ApplicationException(detail));
        }

    }
}