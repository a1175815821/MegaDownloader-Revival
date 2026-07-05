Public Class MEGA_ErrorHandler

    'Requests can fail for a multitude of reasons. At this time, the following error codes are defined:

    'General errors:

    '    EINTERNAL (-1): An internal error has occurred. Please submit a bug report, detailing the exact circumstances in which this error occurred.
    '    EARGS (-2): You have passed invalid arguments to this command.
    '    EAGAIN (-3) (always at the request level): A temporary congestion or server malfunction prevented your request from being processed. No data was altered. Retry. Retries must be spaced with exponential backoff.
    '    ERATELIMIT (-4): You have exceeded your command weight per time quota. Please wait a few seconds, then try again (this should never happen in sane real-life applications).

    'Upload errors:

    '    EFAILED (-5): The upload failed. Please restart it from scratch.
    '    ETOOMANY (-6): Too many concurrent IP addresses are accessing this upload target URL.
    '    ERANGE (-7): The upload file packet is out of range or not starting and ending on a chunk boundary.
    '    EEXPIRED (-8): The upload target URL you are trying to access has expired. Please request a fresh one.

    'Filesystem/Account-level errors:

    '    ENOENT (-9): Object (typically, node or user) not found
    '    ECIRCULAR (-10): Circular linkage attempted
    '    EACCESS (-11): Access violation (e.g., trying to write to a read-only share)
    '    EEXIST (-12): Trying to create an object that already exists
    '    EINCOMPLETE (-13): Trying to access an incomplete resource
    '    EKEY (-14): A decryption operation failed (never returned by the API)
    '    ESID (-15): Invalid or expired user session, please relogin
    '    EBLOCKED (-16): User blocked
    '    EOVERQUOTA (-17): Request over quota
    '    ETEMPUNAVAIL (-18): Resource temporarily not available, please try again later


    Public Shared Function GetErrorFromMegaResponse(ByVal MEGA_Response As String, ByVal Context As String) As Exception


        Select Case MEGA_Response
            Case "-1"
                Return CreateException("EINTERNAL - An internal error has occurred [-1].", Context)
            Case "-2"
                Return CreateException("EARGS -  You have passed invalid arguments to this command [-2].", Context)
            Case "-3"
                Return CreateException("EAGAIN - A temporary congestion or server malfunction prevented your request from being processed. No data was altered [-3].", Context)
            Case "-4"
                Return CreateException("ERATELIMIT - You have exceeded your command weight per time quota. Please wait a few seconds, then try again [-4].", Context)
            Case "-5"
                Return CreateException("EFAILED - The upload failed. Please restart it from scratch [-5].", Context)
            Case "-6"
                Return CreateException("ETOOMANY - Too many concurrent IP addresses are accessing this upload target URL [-6].", Context)
            Case "-7"
                Return CreateException("ERANGE - The upload file packet is out of range or not starting and ending on a chunk boundary [-7].", Context)
            Case "-8"
                Return CreateException("EEXPIRED - The upload target URL you are trying to access has expired. Please request a fresh one [-8].", Context)
            Case "-9"
                Return CreateException("ENOENT - Object (typically, node or user) not found [-9].", Context)
            Case "-10"
                Return CreateException("ECIRCULAR - Circular linkage attempted [-10].", Context)
            Case "-11"
                Return CreateException("EACCESS - Access violation (e.g., trying to write to a read-only share) [-11].", Context)
            Case "-12"
                Return CreateException("EEXIST - Trying to create an object that already exists [-12].", Context)
            Case "-13"
                Return CreateException("EINCOMPLETE - Trying to access an incomplete resource [-13].", Context)
            Case "-14"
                Return CreateException("EKEY - A decryption operation failed [-14].", Context)
            Case "-15"
                Return CreateException("ESID - Invalid or expired user session, please relogin [-15].", Context)
            Case "-16"
                Return CreateException("EBLOCKED - User blocked [-16].", Context)
            Case "-17"
                Return CreateException("EOVERQUOTA - Request over quota [-17].", Context)
            Case "-18"
                Return CreateException("ETEMPUNAVAIL - Resource temporarily not available, please try again later [-18].", Context)

            Case Else
                Return CreateException(MEGA_Response, Context)
        End Select

    End Function

    Private Shared Function CreateException(text As String, ByVal Context As String) As Exception
        If Not String.IsNullOrEmpty(Context) Then
            Context = " " & Context
        End If
        Return New ApplicationException("MEGA returned an error" & Context & ". " & vbNewLine & "Details: " & text)
    End Function

End Class
