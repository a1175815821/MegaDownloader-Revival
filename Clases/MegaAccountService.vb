Imports System.Collections.Generic
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
    Friend Const Busy As Integer = -1002
    Friend Const LocalError As Integer = -1003

    Friend Shared ReadOnly Current As New MegaAccountService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader", "MEGA Account"))
    Friend Event Changed As EventHandler

    Friend Structure Status
        Friend Email As String
        Friend AliasName As String
        Friend SignedIn As Boolean
        Friend Connecting As Boolean
        Friend NeedsLogin As Boolean
        Friend Remember As Boolean
        Friend Remaining As Long?
        Friend QuotaReached As Boolean
        Friend Plan As Integer?
        Friend StorageUsed As Long?
        Friend StorageTotal As Long?
        Friend TransferUsed As Long?
        Friend TransferTotal As Long?

        Friend ReadOnly Property DisplayName As String
            Get
                Return If(String.IsNullOrEmpty(AliasName), Email, AliasName)
            End Get
        End Property
    End Structure

    Private Class SavedLogin
        Public Property Email As String
        Public Property AliasName As String
        Public Property Session As String
    End Class

    Private Class AccountStore
        Public Property DefaultEmail As String
        Public Property Accounts As List(Of SavedLogin)
    End Class

    Private Class AccountEntry
        Friend Value As Status
        Friend Session As String = ""
        Friend Saved As Boolean
        Friend Client As MegaAccountHandle
        Friend Operations As Integer
    End Class

    Private ReadOnly gate As New Object()
    Private ReadOnly accountFolder As String
    Private ReadOnly storePath As String
    Private ReadOnly accounts As New List(Of AccountEntry)()
    Private selectedDefault As String = ""
    Private restoring As Boolean

    Friend Sub New(folder As String)
        accountFolder = folder
        storePath = Path.Combine(folder, "accounts.dat")
    End Sub

    Friend ReadOnly Property DefaultEmail As String
        Get
            SyncLock gate
                Return selectedDefault
            End SyncLock
        End Get
    End Property

    Friend ReadOnly Property IsRestoring As Boolean
        Get
            SyncLock gate
                Return restoring
            End SyncLock
        End Get
    End Property

    Friend Function GetStatus() As Status
        SyncLock gate
            Dim entry = FindAccount(selectedDefault)
            Return If(entry Is Nothing, New Status With {.Email = "", .Connecting = restoring}, entry.Value)
        End SyncLock
    End Function

    Friend Function GetStatus(email As String) As Status
        SyncLock gate
            Dim entry = FindAccount(email)
            Return If(entry Is Nothing, New Status With {.Email = "", .Connecting = restoring}, entry.Value)
        End SyncLock
    End Function

    Friend Function GetAccounts() As List(Of Status)
        SyncLock gate
            Return accounts.ConvertAll(Function(entry) entry.Value)
        End SyncLock
    End Function

    Private Function FindAccount(email As String) As AccountEntry
        Return accounts.Find(Function(entry) String.Equals(entry.Value.Email, email, StringComparison.OrdinalIgnoreCase))
    End Function

    Friend Function NextTransferAccount(email As String, attempted As HashSet(Of String)) As String
        SyncLock gate
            Dim start As Integer = accounts.IndexOf(FindAccount(email))
            For offset As Integer = 1 To accounts.Count
                Dim status = accounts((start + offset) Mod accounts.Count).Value
                If status.SignedIn AndAlso Not status.Connecting AndAlso Not status.QuotaReached AndAlso Not attempted.Contains(status.Email) Then Return status.Email
            Next
            Return ""
        End SyncLock
    End Function

    Friend Shared Function Availability() As Integer
        If Not Environment.Is64BitProcess OrElse Not File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MegaAccountNative.Library)) Then Return BackendMissing
        Return 0
    End Function

    Friend Async Function RestoreAsync() As Task(Of Integer)
        Dim legacyPath As String = Path.Combine(accountFolder, "login.dat")
        Dim legacy As Boolean = Not File.Exists(storePath) AndAlso File.Exists(legacyPath)
        If Not legacy AndAlso Not File.Exists(storePath) Then Return 0
        SyncLock gate
            If restoring OrElse accounts.Count > 0 Then Return Busy
            restoring = True
        End SyncLock
        RaiseEvent Changed(Me, EventArgs.Empty)
        Try
            Return Await Task.Run(Async Function()
                                      Dim sessionAvailable As Boolean = Availability() = 0
                                      Dim store As AccountStore
                                      If legacy Then
                                          Dim saved = ReadProtected(Of SavedLogin)(legacyPath)
                                          store = New AccountStore With {.DefaultEmail = saved.Email, .Accounts = New List(Of SavedLogin) From {saved}}
                                      Else
                                          store = ReadProtected(Of AccountStore)(storePath)
                                      End If
                                      Dim pending As New List(Of AccountEntry)()
                                      SyncLock gate
                                          For Each saved In store.Accounts
                                              Dim entry As New AccountEntry With {
                                                  .Saved = True, .Session = If(saved.Session, ""),
                                                  .Value = New Status With {.Email = saved.Email, .AliasName = If(saved.AliasName, ""), .NeedsLogin = True}}
                                              entry.Value.Remember = entry.Session.Length > 0
                                              entry.Value.Connecting = sessionAvailable AndAlso entry.Value.Remember
                                              accounts.Add(entry)
                                              If entry.Value.Connecting Then pending.Add(entry)
                                          Next
                                          selectedDefault = If(store.DefaultEmail, "")
                                          If legacy AndAlso SaveStore() = 0 Then File.Delete(legacyPath)
                                      End SyncLock
                                      RaiseEvent Changed(Me, EventArgs.Empty)
                                      Dim results As New List(Of Task(Of Integer))()
                                      For Each entry In pending
                                          results.Add(RestoreAccountAsync(entry))
                                      Next
                                      For Each result In Await Task.WhenAll(results).ConfigureAwait(False)
                                          If result <> 0 Then Return result
                                      Next
                                      Return 0
                                  End Function).ConfigureAwait(False)
        Catch ex As Exception When IsStorageError(ex)
            Return LocalError
        Finally
            SyncLock gate
                restoring = False
            End SyncLock
            RaiseEvent Changed(Me, EventArgs.Empty)
        End Try
    End Function

    Private Async Function RestoreAccountAsync(entry As AccountEntry) As Task(Of Integer)
        Dim result As Integer = Await RunConnectionAsync(entry, Function() Connect(entry, "", "", entry.Session, True)).ConfigureAwait(False)
        If result = 0 Then Await RefreshQuotaAsync(entry.Value.Email).ConfigureAwait(False)
        Return result
    End Function

    Friend Function LoginAsync(email As String, password As String, pin As String, remember As Boolean, Optional aliasName As String = Nothing) As Task(Of Integer)
        Dim available As Integer = Availability()
        If available <> 0 Then Return Task.FromResult(available)
        email = email.Trim()
        Dim entry As AccountEntry
        SyncLock gate
            If restoring Then Return Task.FromResult(Busy)
            entry = FindAccount(email)
            If entry Is Nothing Then
                entry = New AccountEntry With {.Value = New Status With {.Email = email, .AliasName = ""}}
                accounts.Add(entry)
            ElseIf entry.Value.Connecting OrElse entry.Operations > 0 Then
                Return Task.FromResult(Busy)
            End If
            entry.Value.Connecting = True
        End SyncLock
        Return RunConnectionAsync(entry, Function() Connect(entry, password, pin, "", remember, aliasName))
    End Function

    Friend Function LogoutAsync(Optional email As String = Nothing) As Task(Of Integer)
        Return DisconnectAsync(If(email, DefaultEmail), remove:=False)
    End Function

    Friend Function RemoveAsync(email As String) As Task(Of Integer)
        Return DisconnectAsync(email, remove:=True)
    End Function

    Private Function DisconnectAsync(email As String, remove As Boolean) As Task(Of Integer)
        Dim entry As AccountEntry
        SyncLock gate
            If restoring Then Return Task.FromResult(Busy)
            entry = FindAccount(email)
            If entry Is Nothing Then Return Task.FromResult(SessionExpired)
            If entry.Value.Connecting OrElse entry.Operations > 0 Then Return Task.FromResult(Busy)
            entry.Value.Connecting = True
        End SyncLock
        Return RunConnectionAsync(entry, Function()
                                             SyncLock gate
                                                 Dim savedSession = entry.Session
                                                 Dim previousDefault = selectedDefault
                                                 entry.Session = ""
                                                 entry.Saved = Not remove
                                                 If remove AndAlso String.Equals(selectedDefault, email, StringComparison.OrdinalIgnoreCase) Then
                                                     Dim nextDefault = accounts.Find(Function(other) other.Saved AndAlso other.Value.SignedIn)
                                                     If nextDefault Is Nothing Then nextDefault = accounts.Find(Function(other) other.Saved)
                                                     selectedDefault = If(nextDefault Is Nothing, "", nextDefault.Value.Email)
                                                 End If
                                                 Dim stored = SaveStore()
                                                 If stored <> 0 Then
                                                     entry.Session = savedSession
                                                     entry.Saved = True
                                                     selectedDefault = previousDefault
                                                     Return stored
                                                 End If
                                                 entry.Value = New Status With {.Email = entry.Value.Email, .AliasName = entry.Value.AliasName, .NeedsLogin = True, .Connecting = True}
                                             End SyncLock
                                             If entry.Client Is Nothing Then Return 0
                                             Dim result As Integer
                                             Try
                                                 result = MegaAccountNative.md_sign_out(entry.Client)
                                             Finally
                                                 entry.Client.Dispose()
                                                 entry.Client = Nothing
                                             End Try
                                             Return If(result = SessionExpired, 0, result)
                                         End Function)
    End Function

    Friend Function SetAlias(email As String, aliasName As String) As Integer
        SyncLock gate
            Dim entry = FindAccount(email)
            If entry Is Nothing Then Return SessionExpired
            If entry.Value.Connecting Then Return Busy
            Dim previous = entry.Value.AliasName
            entry.Value.AliasName = aliasName.Trim()
            Dim result = SaveStore()
            If result <> 0 Then
                entry.Value.AliasName = previous
                Return result
            End If
        End SyncLock
        RaiseEvent Changed(Me, EventArgs.Empty)
        Return 0
    End Function

    Friend Function SetDefault(email As String) As Integer
        SyncLock gate
            Dim entry = FindAccount(email)
            If entry Is Nothing Then Return SessionExpired
            If entry.Value.Connecting Then Return Busy
            Dim previous = selectedDefault
            selectedDefault = entry.Value.Email
            Dim result = SaveStore()
            If result <> 0 Then
                selectedDefault = previous
                Return result
            End If
        End SyncLock
        RaiseEvent Changed(Me, EventArgs.Empty)
        Return 0
    End Function

    Private Function RunConnectionAsync(entry As AccountEntry, operation As Func(Of Integer)) As Task(Of Integer)
        RaiseEvent Changed(Me, EventArgs.Empty)
        Return Task.Run(Function()
                            Try
                                Return operation()
                            Catch ex As Exception When IsStorageError(ex)
                                Return LocalError
                            Catch ex As Exception When IsLibraryError(ex)
                                Return BackendMissing
                            Finally
                                SyncLock gate
                                    entry.Value.Connecting = False
                                    If Not entry.Saved Then accounts.Remove(entry)
                                End SyncLock
                                RaiseEvent Changed(Me, EventArgs.Empty)
                            End Try
                        End Function)
    End Function

    Private Function Connect(entry As AccountEntry, password As String, pin As String, savedSession As String, remember As Boolean,
                             Optional aliasName As String = Nothing) As Integer
        Dim email = entry.Value.Email
        Dim cacheName As String
        Using hash = SHA256.Create()
            cacheName = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(email.ToUpperInvariant()))).Replace("-", "").ToLowerInvariant()
        End Using
        Dim cache = Path.Combine(accountFolder, cacheName)
        Directory.CreateDirectory(cache)
        Dim proxyUrl As String = "", proxyUser As String = "", proxyPassword As String = ""
        Conexion.GetMegaAccountProxy(proxyUrl, proxyUser, proxyPassword)
        Dim appKey = If(Environment.GetEnvironmentVariable(AppKeyVariable), "").Trim()
        Dim opened As MegaAccountHandle = MegaAccountNative.md_open(appKey, cache, proxyUrl, proxyUser, proxyPassword)
        Try
            If opened.IsInvalid Then Return LocalError
            Dim result = MegaAccountNative.md_sign_in(opened, email, password, pin, savedSession)
            If result <> 0 Then Return result
            Dim session As String = savedSession
            If remember AndAlso savedSession = "" Then
                Dim value = MegaAccountNative.md_session(opened)
                Try
                    session = Marshal.PtrToStringAnsi(value)
                    If String.IsNullOrEmpty(session) Then Return LocalError
                Finally
                    MegaAccountNative.md_free_string(value)
                End Try
            End If
            SyncLock gate
                Dim previous = entry.Value
                Dim previousSession = entry.Session
                Dim previousDefault = selectedDefault
                Dim previouslySaved = entry.Saved
                entry.Value = New Status With {.Email = email, .AliasName = If(aliasName, previous.AliasName).Trim(),
                                               .SignedIn = True, .Connecting = True, .Remember = remember}
                entry.Session = session
                entry.Saved = True
                If selectedDefault = "" Then selectedDefault = email
                If savedSession = "" Then
                    result = SaveStore()
                    If result <> 0 Then
                        entry.Value = previous
                        entry.Session = previousSession
                        entry.Saved = previouslySaved
                        selectedDefault = previousDefault
                        Return result
                    End If
                End If
                If entry.Client IsNot Nothing Then entry.Client.Dispose()
                entry.Client = opened
                opened = Nothing
            End SyncLock
            Return 0
        Finally
            If opened IsNot Nothing Then opened.Dispose()
        End Try
    End Function

    Private Shared Function ReadProtected(Of T)(filePath As String) As T
        Dim bytes = ProtectedData.Unprotect(File.ReadAllBytes(filePath), Nothing, DataProtectionScope.CurrentUser)
        Try
            Return JsonConvert.DeserializeObject(Of T)(Encoding.UTF8.GetString(bytes))
        Finally
            Array.Clear(bytes, 0, bytes.Length)
        End Try
    End Function

    ' Called under gate so a saved default and its account records are published together.
    Private Function SaveStore() As Integer
        Dim store As New AccountStore With {.DefaultEmail = selectedDefault, .Accounts = New List(Of SavedLogin)()}
        For Each entry In accounts
            If entry.Saved Then store.Accounts.Add(New SavedLogin With {.Email = entry.Value.Email, .AliasName = entry.Value.AliasName, .Session = entry.Session})
        Next
        Dim bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(store))
        Dim tempPath = storePath & ".tmp"
        Try
            Directory.CreateDirectory(accountFolder)
            File.WriteAllBytes(tempPath, ProtectedData.Protect(bytes, Nothing, DataProtectionScope.CurrentUser))
            If File.Exists(storePath) Then
                File.Replace(tempPath, storePath, Nothing)
            Else
                File.Move(tempPath, storePath)
            End If
            Return 0
        Catch ex As Exception When IsStorageError(ex)
            Return LocalError
        Finally
            Array.Clear(bytes, 0, bytes.Length)
        End Try
    End Function

    Private Shared Function IsStorageError(ex As Exception) As Boolean
        Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is CryptographicException OrElse TypeOf ex Is JsonException
    End Function

    Private Shared Function IsLibraryError(ex As Exception) As Boolean
        Return TypeOf ex Is DllNotFoundException OrElse TypeOf ex Is EntryPointNotFoundException OrElse TypeOf ex Is BadImageFormatException
    End Function

    Private Function Acquire(email As String) As AccountEntry
        SyncLock gate
            Dim entry = FindAccount(email)
            If entry Is Nothing OrElse entry.Value.Connecting OrElse Not entry.Value.SignedIn Then Return Nothing
            entry.Operations += 1
            Return entry
        End SyncLock
    End Function

    Private Sub Release(entry As AccountEntry, result As Integer, Optional detailsChanged As Boolean = False)
        SyncLock gate
            entry.Operations -= 1
            If result = SessionExpired AndAlso entry.Value.SignedIn Then
                entry.Value.SignedIn = False
                entry.Value.NeedsLogin = True
                detailsChanged = True
            ElseIf result = OverQuota AndAlso Not entry.Value.QuotaReached Then
                entry.Value.QuotaReached = True
                detailsChanged = True
            End If
        End SyncLock
        If detailsChanged Then RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Friend Function RefreshQuotaAsync(Optional email As String = Nothing) As Task(Of Integer)
        Dim entry = Acquire(If(email, DefaultEmail))
        If entry Is Nothing Then Return Task.FromResult(SessionExpired)
        Return Task.Run(Function()
                            Dim result As Integer = LocalError
                            Try
                                Dim plan As Integer, storageUsed As Long, storageTotal As Long, transferUsed As Long, transferTotal As Long
                                result = MegaAccountNative.md_account_details(entry.Client, plan, storageUsed, storageTotal, transferUsed, transferTotal)
                                If result = 0 Then
                                    SyncLock gate
                                        entry.Value.Plan = If(plan >= 0, CType(plan, Integer?), Nothing)
                                        entry.Value.StorageUsed = If(storageUsed >= 0, CType(storageUsed, Long?), Nothing)
                                        entry.Value.StorageTotal = If(storageTotal > 0, CType(storageTotal, Long?), Nothing)
                                        entry.Value.TransferUsed = If(transferUsed >= 0, CType(transferUsed, Long?), Nothing)
                                        entry.Value.TransferTotal = If(transferTotal > 0, CType(transferTotal, Long?), Nothing)
                                        entry.Value.Remaining = If(transferTotal > 0 AndAlso transferUsed >= 0, CType(Math.Max(0L, transferTotal - transferUsed), Long?), Nothing)
                                        If entry.Value.Remaining.HasValue Then entry.Value.QuotaReached = entry.Value.Remaining.Value = 0
                                    End SyncLock
                                End If
                                Return result
                            Catch ex As Exception When IsLibraryError(ex)
                                Return BackendMissing
                            Finally
                                Release(entry, result, detailsChanged:=result = 0)
                            End Try
                        End Function)
    End Function

    Friend Function Inspect(email As String, link As String, ByRef name As String, ByRef size As Long) As Integer
        Dim entry = Acquire(email)
        If entry Is Nothing Then Return SessionExpired
        Dim result As Integer = LocalError
        Try
            Dim buffer(4095) As Byte
            result = MegaAccountNative.md_inspect(entry.Client, link, buffer, buffer.Length, size)
            If result = 0 Then name = Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, CByte(0)))
            Return result
        Finally
            Release(entry, result)
        End Try
    End Function

    Friend Function Download(email As String, link As String, destination As String, progress As Action(Of Long, Long, Long), ct As CancellationToken) As Integer
        Dim entry = Acquire(email)
        If entry Is Nothing Then Return SessionExpired
        Dim result As Integer = LocalError
        Try
            If GetStatus(email).QuotaReached Then
                result = OverQuota
            Else
                result = MegaAccountNative.md_download(entry.Client, link, destination,
                    Sub(received, total, speed) progress(received, total, speed), Function() If(ct.IsCancellationRequested, 1, 0))
            End If
            Return result
        Finally
            Release(entry, result)
        End Try
    End Function

    Friend Shared Function ErrorText(code As Integer, Optional signingIn As Boolean = False) As String
        Select Case code
            Case MfaRequired : Return Language.GetText("MegaAccount_MfaRequired")
            Case SessionExpired : Return Language.GetText("MegaAccount_SessionExpired")
            Case OverQuota : Return Language.GetText("MegaAccount_NoAvailableQuota")
            Case BackendMissing : Return Language.GetText("MegaAccount_BackendMissing")
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
    Friend Function md_account_details(handle As MegaAccountHandle, ByRef plan As Integer, ByRef storageUsed As Long, ByRef storageTotal As Long,
                                       ByRef transferUsed As Long, ByRef transferTotal As Long) As Integer
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
