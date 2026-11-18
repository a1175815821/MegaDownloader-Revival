using System;
using Microsoft.VisualBasic;

namespace MegaDownloader
{
    public class MEGA_ErrorHandler
    {

        // Requests can fail for a multitude of reasons. At this time, the following error codes are defined:

        // General errors:

        // EINTERNAL (-1): An internal error has occurred. Please submit a bug report, detailing the exact circumstances in which this error occurred.
        // EARGS (-2): You have passed invalid arguments to this command.
        // EAGAIN (-3) (always at the request level): A temporary congestion or server malfunction prevented your request from being processed. No data was altered. Retry. Retries must be spaced with exponential backoff.
        // ERATELIMIT (-4): You have exceeded your command weight per time quota. Please wait a few seconds, then try again (this should never happen in sane real-life applications).

        // Upload errors:

        // EFAILED (-5): The upload failed. Please restart it from scratch.
        // ETOOMANY (-6): Too many concurrent IP addresses are accessing this upload target URL.
        // ERANGE (-7): The upload file packet is out of range or not starting and ending on a chunk boundary.
        // EEXPIRED (-8): The upload target URL you are trying to access has expired. Please request a fresh one.

        // Filesystem/Account-level errors:

        // ENOENT (-9): Object (typically, node or user) not found
        // ECIRCULAR (-10): Circular linkage attempted
        // EACCESS (-11): Access violation (e.g., trying to write to a read-only share)
        // EEXIST (-12): Trying to create an object that already exists
        // EINCOMPLETE (-13): Trying to access an incomplete resource
        // EKEY (-14): A decryption operation failed (never returned by the API)
        // ESID (-15): Invalid or expired user session, please relogin
        // EBLOCKED (-16): User blocked
        // EOVERQUOTA (-17): Request over quota
        // ETEMPUNAVAIL (-18): Resource temporarily not available, please try again later


        public static Exception GetErrorFromMegaResponse(string MEGA_Response, string Context)
        {


            switch (MEGA_Response ?? "")
            {
                case "-1":
                    return CreateException("EINTERNAL - An internal error has occurred [-1].", Context);
                case "-2":
                    return CreateException("EARGS -  You have passed invalid arguments to this command [-2].", Context);
                case "-3":
                    return CreateException("EAGAIN - A temporary congestion or server malfunction prevented your request from being processed. No data was altered [-3].", Context);
                case "-4":
                    return CreateException("ERATELIMIT - You have exceeded your command weight per time quota. Please wait a few seconds, then try again [-4].", Context);
                case "-5":
                    return CreateException("EFAILED - The upload failed. Please restart it from scratch [-5].", Context);
                case "-6":
                    return CreateException("ETOOMANY - Too many concurrent IP addresses are accessing this upload target URL [-6].", Context);
                case "-7":
                    return CreateException("ERANGE - The upload file packet is out of range or not starting and ending on a chunk boundary [-7].", Context);
                case "-8":
                    return CreateException("EEXPIRED - The upload target URL you are trying to access has expired. Please request a fresh one [-8].", Context);
                case "-9":
                    return CreateException("ENOENT - Object (typically, node or user) not found [-9].", Context);
                case "-10":
                    return CreateException("ECIRCULAR - Circular linkage attempted [-10].", Context);
                case "-11":
                    return CreateException("EACCESS - Access violation (e.g., trying to write to a read-only share) [-11].", Context);
                case "-12":
                    return CreateException("EEXIST - Trying to create an object that already exists [-12].", Context);
                case "-13":
                    return CreateException("EINCOMPLETE - Trying to access an incomplete resource [-13].", Context);
                case "-14":
                    return CreateException("EKEY - A decryption operation failed [-14].", Context);
                case "-15":
                    return CreateException("ESID - Invalid or expired user session, please relogin [-15].", Context);
                case "-16":
                    return CreateException("EBLOCKED - User blocked [-16].", Context);
                case "-17":
                    return CreateException("EOVERQUOTA - Request over quota [-17].", Context);
                case "-18":
                    return CreateException("ETEMPUNAVAIL - Resource temporarily not available, please try again later [-18].", Context);
                default:
                    return CreateException(MEGA_Response, Context);
            }

        }

        private static Exception CreateException(string text, string Context)
        {
            if (!string.IsNullOrEmpty(Context))
            {
                Context = " " + Context;
            }
            return new ApplicationException("MEGA returned an error" + Context + ". " + Constants.vbNewLine + "Details: " + text);
        }

    }
}