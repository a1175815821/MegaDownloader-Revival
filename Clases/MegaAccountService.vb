Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.Win32.SafeHandles
Imports Newtonsoft.Json

Friend NotInheritable Class MegaAccountService
    Friend Const AppKeyVariable As String = "MEGADOWNLOADER_MEGA_APP_KEY"
    Friend Const SessionExpired As Integer = -15
    Friend Const OverQuota As Integer = -17
    Friend Const MfaRequired As Integer = -26
    Friend Const BackendMissing As Integer = -1000
    Friend Const KeyMissing As Integer = -1001
    Friend Const Busy As Integer = -1002
    Friend Const LocalError As Integer = -1003

    Friend Shared ReadOnly Current As New MegaAccountService()
    Friend Event Changed As EventHandler

    Friend Structure Status
        Friend Email As String
        Friend Connecting As Boolean
        Friend NeedsLogin As Boolean
        Friend Remaining As Long?
        Friend QuotaReached As Boolean
    End Structure

    Private Class SavedLogin
        Public Property Email As String
        Public Property Session As String
    End Class

    Private ReadOnly gate As New Object()
    Private ReadOnly accountFolder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader", "MEGA Account")
    Private client As MegaAccountHandle
    Private currentStatus As Status = New Status With {.Email = ""}
    Private operations As Integer

    Friend ReadOnly Property Email As String
        Get
            Return GetStatus().Email
        End Get
    End Property

    Friend Function GetStatus() As Status
        SyncLock gate
            Return currentStatus
        End SyncLock
    End Function

    Friend Shared Function Availability() As Integer
        If Not Environment.Is64BitProcess OrElse Not File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MegaAccountNative.Library)) Then Return BackendMissing
        If String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppKeyVariable)) Then Return KeyMissing
        Return 0
    End Function

    Private ReadOnly Property SessionFile As String
        Get
            Return Path.Combine(accountFolder, "login.dat")
        End Get
    End Property

    Friend Function RestoreAsync() As Task(Of Integer)
        If Not File.Exists(SessionFile) OrElse Availability() <> 0 Then Return Task.FromResult(0)
        Return ChangeConnectionAsync(Function()
                                         Dim bytes = ProtectedData.Unprotect(File.ReadAllBytes(SessionFile), Nothing, DataProtectionScope.CurrentUser)
                                         Dim saved = JsonConvert.DeserializeObject(Of SavedLogin)(Encoding.UTF8.GetString(bytes))
                                         Array.Clear(bytes, 0, bytes.Length)
                                         If saved Is Nothing OrElse String.IsNullOrEmpty(saved.Email) OrElse String.IsNullOrEmpty(saved.Session) Then Return SessionExpired
                                         Return Connect(saved.Email, "", "", saved.Session, True)
                                     End Function, restoring:=True)
    End Function

    Friend Function LoginAsync(email As String, password As String, pin As String, remember As Boolean) As Task(Of Integer)
        Dim available As Integer = Availability()
        If available <> 0 Then Return Task.FromResult(available)
        If Email <> "" Then Return Task.FromResult(Busy)
        Return ChangeConnectionAsync(Function() Connect(email.Trim(), password, pin, "", remember))
    End Function

    Friend Function LogoutAsync() As Task(Of Integer)
        Return ChangeConnectionAsync(Function()
                                         Dim result As Integer = 0
                                         If client IsNot Nothing Then
                                             result = MegaAccountNative.md_sign_out(client)
                                             client.Dispose()
                                             client = Nothing
                                         End If
                                         SyncLock gate
                                             currentStatus = New Status With {.Email = "", .Connecting = True}
                                         End SyncLock
                                         File.Delete(SessionFile)
                                         Return If(result = SessionExpired, 0, result)
                                     End Function)
    End Function

    Private Function ChangeConnectionAsync(operation As Func(Of Integer), Optional restoring As Boolean = False) As Task(Of Integer)
        SyncLock gate
            If currentStatus.Connecting OrElse operations > 0 Then Return Task.FromResult(Busy)
            currentStatus.Connecting = True
        End SyncLock
        RaiseEvent Changed(Me, EventArgs.Empty)
        Return Task.Run(Function()
                            Dim result As Integer = LocalError
                            Try
                                result = operation()
                            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                                                      TypeOf ex Is CryptographicException OrElse TypeOf ex Is JsonException
                                result = LocalError
                            Catch ex As Exception When TypeOf ex Is DllNotFoundException OrElse TypeOf ex Is EntryPointNotFoundException OrElse TypeOf ex Is BadImageFormatException
                                result = BackendMissing
                            Finally
                                SyncLock gate
                                    currentStatus.Connecting = False
                                    If restoring AndAlso result <> 0 AndAlso currentStatus.Email.Length = 0 Then currentStatus.NeedsLogin = True
                                End SyncLock
                                RaiseEvent Changed(Me, EventArgs.Empty)
                            End Try
                            Return result
                        End Function)
    End Function

    Private Function Connect(email As String, password As String, pin As String, savedSession As String, remember As Boolean) As Integer
        Directory.CreateDirectory(accountFolder)
        Dim proxyUrl As String = "", proxyUser As String = "", proxyPassword As String = ""
        Conexion.GetMegaAccountProxy(proxyUrl, proxyUser, proxyPassword)
        If client IsNot Nothing Then
            client.Dispose()
            client = Nothing
        End If
        Dim opened As MegaAccountHandle = MegaAccountNative.md_open(Environment.GetEnvironmentVariable(AppKeyVariable).Trim(), accountFolder, proxyUrl, proxyUser, proxyPassword)
        Try
            If opened.IsInvalid Then Return LocalError
            Dim result = MegaAccountNative.md_sign_in(opened, email, password, pin, savedSession)
            If result <> 0 Then Return result
            client = opened
            opened = Nothing
            SyncLock gate
                currentStatus = New Status With {.Email = email, .Connecting = True}
            End SyncLock
            If remember Then
                Dim value = MegaAccountNative.md_session(client)
                Try
                    Dim saved = New SavedLogin With {.Email = email, .Session = Marshal.PtrToStringAnsi(value)}
                    Dim bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(saved))
                    Try
                        File.WriteAllBytes(SessionFile, ProtectedData.Protect(bytes, Nothing, DataProtectionScope.CurrentUser))
                    Finally
                        Array.Clear(bytes, 0, bytes.Length)
                    End Try
                Finally
                    MegaAccountNative.md_free_string(value)
                End Try
            Else
                File.Delete(SessionFile)
            End If
            Return 0
        Finally
            If opened IsNot Nothing Then opened.Dispose()
        End Try
    End Function

    Private Function Acquire(email As String) As MegaAccountHandle
        SyncLock gate
            If currentStatus.Connecting OrElse currentStatus.Email.Length = 0 OrElse
               Not String.Equals(currentStatus.Email, email, StringComparison.OrdinalIgnoreCase) Then Return Nothing
            operations += 1
            Return client
        End SyncLock
    End Function

    Private Sub Release(result As Integer)
        Dim changedStatus As Boolean = False
        SyncLock gate
            operations -= 1
            If result = SessionExpired AndAlso currentStatus.Email.Length > 0 Then
                currentStatus = New Status With {.Email = "", .NeedsLogin = True}
                changedStatus = True
            ElseIf result = OverQuota AndAlso Not currentStatus.QuotaReached Then
                currentStatus.QuotaReached = True
                currentStatus.Remaining = Nothing
                changedStatus = True
            End If
        End SyncLock
        If changedStatus Then RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Friend Function RefreshQuotaAsync() As Task(Of Integer)
        Dim connected = Acquire(Email)
        If connected Is Nothing Then Return Task.FromResult(SessionExpired)
        Return Task.Run(Function()
                            Dim result As Integer = LocalError
                            Try
                                Dim used As Long, total As Long
                                result = MegaAccountNative.md_quota(connected, used, total)
                                If result = 0 Then
                                    SyncLock gate
                                        If currentStatus.Email.Length > 0 Then
                                            currentStatus.Remaining = If(total > 0 AndAlso used >= 0, CType(Math.Max(0L, total - used), Long?), Nothing)
                                            currentStatus.QuotaReached = currentStatus.Remaining.HasValue AndAlso currentStatus.Remaining.Value = 0
                                        End If
                                    End SyncLock
                                End If
                                Return result
                            Finally
                                Release(result)
                                RaiseEvent Changed(Me, EventArgs.Empty)
                            End Try
                        End Function)
    End Function

    Friend Function Inspect(email As String, link As String, ByRef name As String, ByRef size As Long) As Integer
        Dim connected = Acquire(email)
        If connected Is Nothing Then Return SessionExpired
        Dim result As Integer = LocalError
        Try
            Dim buffer(4095) As Byte
            result = MegaAccountNative.md_inspect(connected, link, buffer, buffer.Length, size)
            If result = 0 Then name = Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, CByte(0)))
            Return result
        Finally
            Release(result)
        End Try
    End Function

    Friend Function Download(email As String, link As String, destination As String, progress As Action(Of Long, Long, Long), ct As CancellationToken) As Integer
        Dim connected = Acquire(email)
        If connected Is Nothing Then Return SessionExpired
        Dim result As Integer = LocalError
        Try
            result = MegaAccountNative.md_download(connected, link, destination,
                Sub(received, total, speed) progress(received, total, speed), Function() If(ct.IsCancellationRequested, 1, 0))
            Return result
        Finally
            Release(result)
        End Try
    End Function

    Friend Shared Function ErrorText(code As Integer, Optional signingIn As Boolean = False) As String
        Select Case code
            Case MfaRequired : Return Language.GetText("MegaAccount_MfaRequired")
            Case SessionExpired : Return Language.GetText("MegaAccount_SessionExpired")
            Case OverQuota : Return Language.GetText("MegaAccount_QuotaReached")
            Case BackendMissing : Return Language.GetText("MegaAccount_BackendMissing")
            Case KeyMissing : Return Language.GetText("MegaAccount_KeyMissing")
            Case Busy : Return Language.GetText("MegaAccount_Busy")
            Case LocalError : Return Language.GetText("MegaAccount_LocalError")
            Case -9, -11 : Return Language.GetText(If(signingIn, "MegaAccount_AuthenticationFailed", "MegaAccount_FileUnavailable"))
            Case -14, -2 : Return Language.GetText("MegaAccount_FileUnavailable")
            Case Else : Return Language.GetText("MegaAccount_RequestFailed").Replace("%N%", code.ToString(Globalization.CultureInfo.InvariantCulture))
        End Select
    End Function
End Class

Friend NotInheritable Class MegaAccountHandle
    Inherits SafeHandleZeroOrMinusOneIsInvalid

    Private Sub New()
        MyBase.New(True)
    End Sub

    Protected Overrides Function ReleaseHandle() As Boolean
        MegaAccountNative.md_close(handle)
        Return True
    End Function
End Class

Friend Module MegaAccountNative
    Friend Const Library As String = "MegaAccountBridge.dll"

    <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
    Friend Delegate Sub Progress(received As Long, total As Long, speed As Long)
    <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
    Friend Delegate Function Cancelled() As Integer

    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl),
     DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory Or DllImportSearchPath.System32)>
    Friend Function md_open(<MarshalAs(UnmanagedType.LPUTF8Str)> key As String, <MarshalAs(UnmanagedType.LPUTF8Str)> cache As String,
                            <MarshalAs(UnmanagedType.LPUTF8Str)> proxy As String, <MarshalAs(UnmanagedType.LPUTF8Str)> user As String,
                            <MarshalAs(UnmanagedType.LPUTF8Str)> password As String) As MegaAccountHandle
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Sub md_close(handle As IntPtr)
    End Sub
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Sub md_free_string(value As IntPtr)
    End Sub
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_sign_in(handle As MegaAccountHandle, <MarshalAs(UnmanagedType.LPUTF8Str)> email As String,
                               <MarshalAs(UnmanagedType.LPUTF8Str)> password As String, <MarshalAs(UnmanagedType.LPUTF8Str)> pin As String,
                               <MarshalAs(UnmanagedType.LPUTF8Str)> session As String) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_session(handle As MegaAccountHandle) As IntPtr
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_sign_out(handle As MegaAccountHandle) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_quota(handle As MegaAccountHandle, ByRef used As Long, ByRef total As Long) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_inspect(handle As MegaAccountHandle, <MarshalAs(UnmanagedType.LPUTF8Str)> link As String,
                                <Out> name As Byte(), capacity As Integer, ByRef size As Long) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_download(handle As MegaAccountHandle, <MarshalAs(UnmanagedType.LPUTF8Str)> link As String,
                                 <MarshalAs(UnmanagedType.LPUTF8Str)> path As String, progressCallback As Progress, cancelCallback As Cancelled) As Integer
    End Function
End Module
