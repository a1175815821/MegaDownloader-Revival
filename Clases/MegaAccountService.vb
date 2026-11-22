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
        Friend LastError As Integer
        Friend Remember As Boolean
        Friend Remaining As Long?
        Friend QuotaReached As Boolean
        Friend QuotaRetryUtc As Date?
        Friend QuotaDelayUnknown As Boolean
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
        Public Property DrivePreferences As MegaDrivePreferences
        Public Property QuotaRetryUtc As Date?
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
        Friend AppliedProxy As Conexion.ProxySettings
        Friend ReadOnly ProxyGate As New Object()
        Friend ReadOnly DownloadBudget As New ThrottledStreamController.AccountDownloadBudget()
        Friend Operations As Integer
        Friend ZeroBalanceExpiresUtc As Date
        Friend ReadOnly StreamingNodes As New HashSet(Of ULong)()
        Friend DrivePreferences As New MegaDrivePreferences()
    End Class

    Private ReadOnly gate As New Object()
    Private ReadOnly accountFolder As String
    Private ReadOnly storePath As String
    Private ReadOnly accounts As New List(Of AccountEntry)()
    Private selectedDefault As String = ""
    Private restoring As Boolean

    Friend ReadOnly DriveHistory As MegaDriveHistory

    Friend Sub New(folder As String)
        accountFolder = folder
        storePath = Path.Combine(folder, "accounts.dat")
        DriveHistory = New MegaDriveHistory(Path.Combine(folder, "Drive history"))
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
            Return SnapshotStatus(entry)
        End SyncLock
    End Function

    Friend Function GetStatus(email As String) As Status
        SyncLock gate
            Dim entry = FindAccount(email)
            Return SnapshotStatus(entry)
        End SyncLock
    End Function

    Friend Function GetAccounts() As List(Of Status)
        SyncLock gate
            Return accounts.ConvertAll(AddressOf SnapshotStatus)
        End SyncLock
    End Function

    Private Function SnapshotStatus(entry As AccountEntry) As Status
        If entry Is Nothing Then Return New Status With {.Email = "", .Connecting = restoring}
        Dim status = entry.Value
        Dim utcNow = Date.UtcNow
        If status.QuotaRetryUtc.HasValue AndAlso status.QuotaRetryUtc.Value <= utcNow Then
            status.QuotaRetryUtc = Nothing
            status.Remaining = Nothing
        ElseIf status.Remaining.GetValueOrDefault(1L) = 0 AndAlso entry.ZeroBalanceExpiresUtc <= utcNow Then
            status.Remaining = Nothing
        End If
        status.QuotaReached = status.QuotaRetryUtc.HasValue OrElse status.Remaining.GetValueOrDefault(1L) = 0
        Return status
    End Function

    Private Function FindAccount(email As String) As AccountEntry
        Return accounts.Find(Function(entry) String.Equals(entry.Value.Email, email, StringComparison.OrdinalIgnoreCase))
    End Function

    Friend Function NextTransferAccount(email As String, attempted As HashSet(Of String), Optional allowUnknownDelay As Boolean = True) As String
        SyncLock gate
            Dim start As Integer = accounts.IndexOf(FindAccount(email))
            For offset As Integer = 1 To accounts.Count
                Dim status = SnapshotStatus(accounts((start + offset) Mod accounts.Count))
                If status.SignedIn AndAlso Not status.Connecting AndAlso Not status.QuotaReached AndAlso
                   (allowUnknownDelay OrElse Not status.QuotaDelayUnknown) AndAlso Not attempted.Contains(status.Email) Then Return status.Email
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
        Dim pending As List(Of AccountEntry)
        Try
            pending = Await Task.Run(Function() LoadSavedAccounts(legacyPath, legacy)).ConfigureAwait(False)
        Catch ex As Exception When IsStorageError(ex)
            Return LocalError
        Finally
            SyncLock gate
                restoring = False
            End SyncLock
            RaiseEvent Changed(Me, EventArgs.Empty)
        End Try

        Dim results = Await Task.WhenAll(pending.Select(AddressOf RestoreAccountAsync)).ConfigureAwait(False)
        Return results.FirstOrDefault(Function(result) result <> 0)
    End Function

    Private Function LoadSavedAccounts(legacyPath As String, legacy As Boolean) As List(Of AccountEntry)
        Dim store As AccountStore
        If legacy Then
            Dim saved = ReadProtected(Of SavedLogin)(legacyPath)
            store = New AccountStore With {.DefaultEmail = saved.Email, .Accounts = New List(Of SavedLogin) From {saved}}
        Else
            store = ReadProtected(Of AccountStore)(storePath)
        End If
        Dim sessionAvailable As Boolean = Availability() = 0
        Dim pending As New List(Of AccountEntry)()
        SyncLock gate
            For Each saved In store.Accounts
                Dim entry As New AccountEntry With {
                    .Saved = True, .Session = If(saved.Session, ""),
                    .DrivePreferences = If(saved.DrivePreferences, New MegaDrivePreferences()),
                    .Value = New Status With {.Email = saved.Email, .AliasName = If(saved.AliasName, ""),
                                              .QuotaRetryUtc = saved.QuotaRetryUtc}}
                entry.Value.Remember = entry.Session.Length > 0
                entry.Value.Connecting = sessionAvailable AndAlso entry.Value.Remember
                If entry.Value.Remember AndAlso Not sessionAvailable Then entry.Value.LastError = BackendMissing
                accounts.Add(entry)
                If entry.Value.Connecting Then pending.Add(entry)
            Next
            selectedDefault = If(store.DefaultEmail, "")
            If legacy AndAlso SaveStore() = 0 Then
                Try
                    File.Delete(legacyPath)
                Catch ex As Exception When IsStorageError(ex)
                    Log.WriteWarning("Could not delete the migrated MEGA account store: " & Log.SafeException(ex))
                End Try
            End If
        End SyncLock
        Return pending
    End Function

    Private Async Function RestoreAccountAsync(entry As AccountEntry) As Task(Of Integer)
        Dim result As Integer = Await RunConnectionAsync(entry, Function() Connect(entry, "", "", entry.Session, True), restoringSession:=True).ConfigureAwait(False)
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
                                                 entry.Value = New Status With {.Email = entry.Value.Email, .AliasName = entry.Value.AliasName,
                                                                                .Connecting = True, .QuotaRetryUtc = entry.Value.QuotaRetryUtc}
                                             End SyncLock
                                             Dim result As Integer
                                             Dim cleanup As Integer
                                             Try
                                                 If entry.Client IsNot Nothing Then
                                                     Try
                                                         result = MegaAccountNative.md_sign_out(entry.Client)
                                                     Finally
                                                         entry.Client.Dispose()
                                                         entry.Client = Nothing
                                                     End Try
                                                 End If
                                             Finally
                                                 If remove Then cleanup = RemoveAccountData(entry.Value.Email)
                                             End Try
                                             Return If(result = 0 OrElse result = SessionExpired, cleanup, result)
                                         End Function)
    End Function

    Private Function RemoveAccountData(email As String) As Integer
        Dim result As Integer
        Try
            Directory.Delete(AccountCachePath(email), recursive:=True)
        Catch ex As DirectoryNotFoundException
        Catch ex As Exception When IsStorageError(ex)
            Log.WriteWarning("Could not remove the MEGA SDK cache: " & Log.SafeException(ex))
            result = LocalError
        End Try
        Try
            DriveHistory.RemoveAccount(email)
        Catch ex As Exception When IsStorageError(ex)
            Log.WriteWarning("Could not remove MEGA cloud drive history: " & Log.SafeException(ex))
            result = LocalError
        End Try
        Return result
    End Function

    Friend Sub RecordDriveDelivery(email As String, node As ULong, output As String)
        SyncLock gate
            Dim entry = FindAccount(email)
            If entry IsNot Nothing AndAlso entry.Saved Then DriveHistory.Record(email, node, output)
        End SyncLock
    End Sub

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

    Private Function RunConnectionAsync(entry As AccountEntry, operation As Func(Of Integer), Optional restoringSession As Boolean = False) As Task(Of Integer)
        RaiseEvent Changed(Me, EventArgs.Empty)
        Return Task.Run(Function()
                            Dim result As Integer = LocalError
                            Try
                                result = operation()
                            Catch ex As Exception When IsStorageError(ex)
                                result = LocalError
                            Catch ex As Exception When IsLibraryError(ex)
                                result = BackendMissing
                            Finally
                                SyncLock gate
                                    If restoringSession Then
                                        entry.Value.LastError = result
                                        entry.Value.NeedsLogin = result = SessionExpired OrElse result = -9 OrElse result = -11
                                    End If
                                    entry.Value.Connecting = False
                                    If Not entry.Saved Then accounts.Remove(entry)
                                End SyncLock
                                RaiseEvent Changed(Me, EventArgs.Empty)
                            End Try
                            Return result
                        End Function)
    End Function

    Private Function AccountCachePath(email As String) As String
        Using hash = SHA256.Create()
            Dim cacheName = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(email.ToUpperInvariant()))).Replace("-", "").ToLowerInvariant()
            Return Path.Combine(accountFolder, cacheName)
        End Using
    End Function

    Private Function Connect(entry As AccountEntry, password As String, pin As String, savedSession As String, remember As Boolean,
                             Optional aliasName As String = Nothing) As Integer
        Dim email = entry.Value.Email
        Dim cache = AccountCachePath(email)
        Directory.CreateDirectory(cache)
        Dim proxy = Conexion.GetMegaAccountProxy()
        If proxy IsNot Nothing AndAlso proxy.Address Is Nothing Then Return LocalError
        Dim appKey = If(Environment.GetEnvironmentVariable(AppKeyVariable), "").Trim()
        Dim opened As MegaAccountHandle = MegaAccountNative.md_open(appKey, cache, If(proxy?.Address.AbsoluteUri, ""),
                                                                   If(proxy?.Username, ""), If(proxy?.Password, ""))
        Dim replaced As MegaAccountHandle = Nothing
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
                                               .SignedIn = True, .Connecting = True, .Remember = remember,
                                               .QuotaRetryUtc = previous.QuotaRetryUtc}
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
                replaced = entry.Client
                entry.Client = opened
                entry.AppliedProxy = proxy
                opened = Nothing
            End SyncLock
            Return 0
        Finally
            If replaced IsNot Nothing Then replaced.Dispose()
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
            If entry.Saved Then
                store.Accounts.Add(New SavedLogin With {.Email = entry.Value.Email, .AliasName = entry.Value.AliasName,
                                                        .Session = entry.Session, .DrivePreferences = entry.DrivePreferences,
                                                        .QuotaRetryUtc = SnapshotStatus(entry).QuotaRetryUtc})
            End If
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

    Friend Function GetAccessError(email As String) As Integer
        If String.IsNullOrEmpty(email) Then Return 0
        Return AccessError(GetStatus(email))
    End Function

    Private Shared Function AccessError(status As Status) As Integer
        If status.Connecting Then Return Busy
        Return If(status.SignedIn, 0, SessionExpired)
    End Function

    Friend Sub UpdateProxies()
        For Each status In GetAccounts()
            If Not status.SignedIn OrElse status.Connecting Then Continue For
            Dim entry As AccountEntry = Nothing
            Dim result = Acquire(status.Email, entry)
            If result = 0 Then
                Release(entry, result)
            ElseIf result <> Busy AndAlso result <> SessionExpired Then
                Log.WriteWarning("Could not update the MEGA account proxy: " & result.ToString())
            End If
        Next
    End Sub

    Private Function Acquire(email As String, ByRef entry As AccountEntry) As Integer
        SyncLock gate
            entry = FindAccount(email)
            Dim access = AccessError(SnapshotStatus(entry))
            If access <> 0 Then Return access
            entry.Operations += 1
        End SyncLock
        Dim result As Integer = LocalError
        Try
            SyncLock entry.ProxyGate
                Dim proxy = Conexion.GetMegaAccountProxy()
                If proxy Is entry.AppliedProxy Then
                    result = 0
                ElseIf proxy Is Nothing OrElse proxy.Address IsNot Nothing Then
                    result = MegaAccountNative.md_set_proxy(entry.Client, If(proxy?.Address.AbsoluteUri, ""),
                                                            If(proxy?.Username, ""), If(proxy?.Password, ""))
                    If result = 0 Then entry.AppliedProxy = proxy
                End If
            End SyncLock
            Return result
        Catch ex As Exception When IsLibraryError(ex)
            Return BackendMissing
        Finally
            If result <> 0 Then Release(entry, result)
        End Try
    End Function

    Private Sub Release(entry As AccountEntry, result As Integer, Optional detailsChanged As Boolean = False, Optional quotaWaitSeconds As Long = 0)
        SyncLock gate
            entry.Operations -= 1
            Dim utcNow = Date.UtcNow
            Dim previous = entry.Value.QuotaRetryUtc
            If previous.HasValue AndAlso previous.Value <= utcNow Then
                entry.Value.QuotaRetryUtc = Nothing
                entry.Value.Remaining = Nothing
                previous = Nothing
                detailsChanged = True
            End If
            If result = SessionExpired AndAlso entry.Value.SignedIn Then
                entry.Value.SignedIn = False
                entry.Value.NeedsLogin = True
                entry.Value.LastError = result
                detailsChanged = True
            ElseIf result = OverQuota Then
                If quotaWaitSeconds > 0 Then
                    Dim deadline = utcNow.AddSeconds(Math.Min(CDbl(quotaWaitSeconds), (Date.MaxValue - utcNow.AddDays(1)).TotalSeconds))
                    If Not previous.HasValue OrElse deadline > previous.Value.AddSeconds(1) Then
                        entry.Value.QuotaRetryUtc = deadline
                        entry.Value.QuotaDelayUnknown = False
                        detailsChanged = True
                        If SaveStore() <> 0 Then Log.WriteWarning("Could not save MEGA transfer quota deadline.")
                    End If
                ElseIf Not previous.HasValue AndAlso Not entry.Value.QuotaDelayUnknown Then
                    ' With no SDK deadline, use the queue's retry interval for failed tasks.
                    entry.Value.QuotaDelayUnknown = True
                    detailsChanged = True
                End If
            End If
        End SyncLock
        If detailsChanged Then RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Friend Function RefreshQuotaAsync(Optional email As String = Nothing) As Task(Of Integer)
        Dim entry As AccountEntry = Nothing
        Dim access = Acquire(If(email, DefaultEmail), entry)
        If access <> 0 Then Return Task.FromResult(access)
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
                                        Dim utcNow = Date.UtcNow
                                        If entry.Value.Remaining.GetValueOrDefault(1L) = 0 Then entry.ZeroBalanceExpiresUtc = utcNow.AddMinutes(5)
                                        If entry.Value.QuotaRetryUtc.GetValueOrDefault() <= utcNow Then entry.Value.QuotaRetryUtc = Nothing
                                        entry.Value.QuotaDelayUnknown = False
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

    Friend Function GetDrivePreferences(email As String) As MegaDrivePreferences
        SyncLock gate
            Dim entry = FindAccount(email)
            Return If(entry Is Nothing, New MegaDrivePreferences(), entry.DrivePreferences.Copy())
        End SyncLock
    End Function

    Friend Function SaveDrivePreferences(email As String, preferences As MegaDrivePreferences) As Integer
        SyncLock gate
            Dim entry = FindAccount(email)
            If entry Is Nothing OrElse Not entry.Saved Then Return SessionExpired
            Dim previous = entry.DrivePreferences
            If previous.LastFolder.Equals(preferences.LastFolder) AndAlso previous.LastSharedFolder.Equals(preferences.LastSharedFolder) AndAlso
               previous.Favorites.SequenceEqual(preferences.Favorites) AndAlso previous.FolderDestinations.Count = preferences.FolderDestinations.Count AndAlso
               previous.FolderDestinations.All(Function(item)
                                                   Dim path As String = Nothing
                                                   Return preferences.FolderDestinations.TryGetValue(item.Key, path) AndAlso path = item.Value
                                               End Function) Then Return 0
            entry.DrivePreferences = preferences.Copy()
            Dim result = SaveStore()
            If result <> 0 Then entry.DrivePreferences = previous
            Return result
        End SyncLock
    End Function

    Friend Function ReadDrive(email As String, ct As CancellationToken, ByRef snapshot As MegaDriveSnapshot,
                              Optional incomingShares As Boolean = False) As Integer
        Dim entry As AccountEntry = Nothing
        Dim access = Acquire(email, entry)
        If access <> 0 Then Return access
        Dim result As Integer = LocalError
        Try
            Dim nodes As New List(Of MegaDriveNode)()
            Dim root As ULong
            Dim visit As MegaAccountNative.DriveNode =
                Function(id, parent, name, folder, bytes, modified)
                    If ct.IsCancellationRequested Then Return 0
                    nodes.Add(New MegaDriveNode(id, parent, name, folder <> 0, bytes, modified))
                    Return 1
                End Function
            result = If(incomingShares, MegaAccountNative.md_list_shares(entry.Client, visit, root),
                        MegaAccountNative.md_list_drive(entry.Client, visit, root))
            If result = 0 Then snapshot = New MegaDriveSnapshot(root, nodes)
            Return result
        Catch ex As Exception When IsLibraryError(ex)
            Return BackendMissing
        Finally
            Release(entry, result)
        End Try
    End Function

    Friend Function Inspect(email As String, link As String, ByRef name As String, ByRef size As Long, Optional node As ULong? = Nothing) As Integer
        Dim entry As AccountEntry = Nothing
        Dim access = Acquire(email, entry)
        If access <> 0 Then Return access
        Dim result As Integer = LocalError
        Try
            Dim buffer(4095) As Byte
            result = If(node.HasValue, MegaAccountNative.md_inspect_node(entry.Client, node.GetValueOrDefault(), buffer, buffer.Length, size),
                        MegaAccountNative.md_inspect(entry.Client, link, buffer, buffer.Length, size))
            If result = 0 Then name = Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, CByte(0)))
            Return result
        Catch ex As Exception When node.HasValue AndAlso IsLibraryError(ex)
            Return BackendMissing
        Finally
            Release(entry, result)
        End Try
    End Function

    Friend Function Download(email As String, link As String, destination As String, progress As Action(Of Long, Long, Long), ct As CancellationToken) As Integer
        Dim entry As AccountEntry = Nothing
        Dim access = Acquire(email, entry)
        If access <> 0 Then Return access
        Dim result As Integer = LocalError
        Dim quotaWaitSeconds As Long
        Dim controller = ThrottledStreamController.GetController()
        Dim budget = entry.DownloadBudget
        Dim registered As Boolean = False
        Try
            If GetStatus(email).QuotaReached Then Return OverQuota
            Dim report As MegaAccountNative.Progress = Sub(received, total, speed) progress(received, total, speed)
            Dim cancelled As MegaAccountNative.Cancelled = Function() If(ct.IsCancellationRequested, 1, 0)
            controller.BeginAccountDownload(budget)
            registered = True
            result = MegaAccountNative.md_download_with_limit(entry.Client, link, destination, report, cancelled, quotaWaitSeconds,
                                                               Function() budget.BytesPerSecond)
            Return result
        Catch ex As Exception When IsLibraryError(ex)
            Return BackendMissing
        Finally
            If registered Then controller.EndAccountDownload(budget)
            Release(entry, result, quotaWaitSeconds:=quotaWaitSeconds)
        End Try
    End Function

    Friend Function InspectPrivateDownload(email As String, node As ULong, ByRef name As String, ByRef size As Long,
                                           ByRef key As String) As Integer
        Dim entry As AccountEntry = Nothing
        Dim access = Acquire(email, entry)
        If access <> 0 Then Return access
        Dim result As Integer = LocalError
        Try
            Dim nameBuffer(4095) As Byte
            Dim keyBuffer(43) As Byte
            result = MegaAccountNative.md_inspect_node_for_resume(entry.Client, node, nameBuffer, nameBuffer.Length,
                                                                   size, keyBuffer, keyBuffer.Length)
            If result = 0 Then
                name = Encoding.UTF8.GetString(nameBuffer, 0, Array.IndexOf(nameBuffer, CByte(0)))
                key = Encoding.ASCII.GetString(keyBuffer, 0, Array.IndexOf(keyBuffer, CByte(0)))
                ' VerifyMegaMetaMac also accepts public keys that do not contain a MAC.
                ' A private transfer must always supply a full 32-byte file key.
                If key.Length <> 43 OrElse Not System.Text.RegularExpressions.Regex.IsMatch(key, "\A[A-Za-z0-9_-]{43}\z") OrElse
                   Criptografia.base64urldecodeBytes(key).Length <> 32 Then result = -14
            End If
            Return result
        Catch ex As Exception When IsLibraryError(ex)
            Return BackendMissing
        Finally
            Release(entry, result)
        End Try
    End Function

    Friend Function StreamPrivateDownload(email As String, node As ULong, key As String, size As Long, offset As Long,
                                          write As MegaAccountNative.StreamData, ct As CancellationToken) As Integer
        Dim entry As AccountEntry = Nothing
        Dim access = Acquire(email, entry)
        If access <> 0 Then Return access
        Dim result As Integer = LocalError
        Dim started As Boolean = False
        Dim quotaWaitSeconds As Long
        Dim controller = ThrottledStreamController.GetController()
        Dim budget = entry.DownloadBudget
        Dim registered As Boolean = False
        Try
            ' SDK streaming cancellation is by node and range, not by listener. Do not
            ' let a second task for the same node be stopped by the first task's pause.
            SyncLock entry.StreamingNodes
                While entry.StreamingNodes.Contains(node)
                    ct.ThrowIfCancellationRequested()
                    Monitor.Wait(entry.StreamingNodes, 100)
                End While
                ct.ThrowIfCancellationRequested()
                started = entry.StreamingNodes.Add(node)
            End SyncLock
            If offset < size AndAlso GetStatus(email).QuotaReached Then Return OverQuota
            Dim cancelled As MegaAccountNative.Cancelled = Function() If(ct.IsCancellationRequested, 1, 0)
            If offset < size Then
                controller.BeginAccountDownload(budget)
                registered = True
            End If
            result = MegaAccountNative.md_stream_node_with_limit(entry.Client, node, key, size, offset, write, cancelled, quotaWaitSeconds,
                                                                 Function() budget.BytesPerSecond)
            Return result
        Catch ex As Exception When IsLibraryError(ex)
            Return BackendMissing
        Finally
            If registered Then controller.EndAccountDownload(budget)
            If started Then
                SyncLock entry.StreamingNodes
                    entry.StreamingNodes.Remove(node)
                    Monitor.PulseAll(entry.StreamingNodes)
                End SyncLock
            End If
            Release(entry, result, quotaWaitSeconds:=quotaWaitSeconds)
        End Try
    End Function

    Friend Shared Function ErrorText(code As Integer, Optional signingIn As Boolean = False, Optional connecting As Boolean = False) As String
        Select Case code
            Case MfaRequired : Return Language.GetText("MegaAccount_MfaRequired")
            Case SessionExpired : Return Language.GetText("MegaAccount_SessionExpired")
            Case OverQuota : Return Language.GetText("MegaAccount_NoAvailableQuota")
            Case BackendMissing : Return Language.GetText("MegaAccount_BackendMissing")
            Case Busy : Return Language.GetText(If(connecting, "MegaAccount_Connecting", "MegaAccount_Busy"))
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
    <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
    Friend Delegate Function DownloadLimit() As Long

    <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
    Friend Delegate Function DriveNode(id As ULong, parent As ULong, <MarshalAs(UnmanagedType.LPUTF8Str)> name As String,
                                       folder As Integer, bytes As Long, modified As Long) As Integer

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
    Friend Function md_set_proxy(handle As MegaAccountHandle, <MarshalAs(UnmanagedType.LPUTF8Str)> url As String,
                                 <MarshalAs(UnmanagedType.LPUTF8Str)> user As String, <MarshalAs(UnmanagedType.LPUTF8Str)> password As String) As Integer
    End Function
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
    Friend Function md_list_drive(handle As MegaAccountHandle, visit As DriveNode, ByRef root As ULong) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_list_shares(handle As MegaAccountHandle, visit As DriveNode, ByRef root As ULong) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_inspect_node(handle As MegaAccountHandle, node As ULong, <Out> name As Byte(), capacity As Integer, ByRef size As Long) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_download_with_limit(handle As MegaAccountHandle, <MarshalAs(UnmanagedType.LPUTF8Str)> link As String,
                                           <MarshalAs(UnmanagedType.LPUTF8Str)> path As String, progressCallback As Progress,
                                           cancelCallback As Cancelled, ByRef quotaWaitSeconds As Long, readLimit As DownloadLimit) As Integer
    End Function
    <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
    Friend Delegate Function StreamData(buffer As IntPtr, count As Integer, speed As Long) As Integer
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_inspect_node_for_resume(handle As MegaAccountHandle, node As ULong, <Out> name As Byte(), nameCapacity As Integer,
                                               ByRef size As Long, <Out> key As Byte(), keyCapacity As Integer) As Integer
    End Function
    <DllImport(Library, CallingConvention:=CallingConvention.Cdecl)>
    Friend Function md_stream_node_with_limit(handle As MegaAccountHandle, node As ULong, <MarshalAs(UnmanagedType.LPUTF8Str)> key As String,
                                              size As Long, offset As Long, write As StreamData, cancelCallback As Cancelled,
                                              ByRef quotaWaitSeconds As Long, readLimit As DownloadLimit) As Integer
    End Function
End Module
