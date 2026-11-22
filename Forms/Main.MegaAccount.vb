Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Main
    Private ReadOnly megaDriveButton As New CommandButton With {.Name = "OpenMegaDrive"}
    Private openingMegaDrive As Boolean
    Private megaAccountMenu As ToolStripMenuItem
    Private megaAccountStatus As ToolStripStatusLabel
    Private megaAccountContext As SynchronizationContext
    Private ReadOnly refreshCloudQuota As New ToolStripMenuItem With {.Name = "RefreshCloudQuota"}
    Private ReadOnly retryCloudDownload As New ToolStripMenuItem With {.Name = "RetryCloudDownload"}

    Private Sub InitCloudQuotaActions()
        Dim position = MenuDescarga.Items.IndexOf(queueIdentityMenu) + 1
        MenuDescarga.Items.Insert(position, refreshCloudQuota)
        MenuDescarga.Items.Insert(position + 1, retryCloudDownload)
        AddHandler refreshCloudQuota.Click, Async Sub() Await RefreshSelectedCloudQuotaAsync(False)
        AddHandler retryCloudDownload.Click, Async Sub() Await RefreshSelectedCloudQuotaAsync(True)
    End Sub

    Private Function SelectedCloudFiles() As Fichero()
        Dim files As New HashSet(Of Fichero)()
        SyncLock Mutex.ListaDescargas
            For Each selected In ListaDescargas.SelectedObjects
                Dim file = TryCast(selected, Fichero)
                Dim package = TryCast(selected, Paquete)
                If file IsNot Nothing Then files.Add(file)
                If package IsNot Nothing Then files.UnionWith(package.ListaFicheros)
            Next
        End SyncLock
        Return files.Where(Function(file) file.MegaDriveNodeHandle.HasValue AndAlso file.DescargaEstado() <> Estado.Completado).ToArray()
    End Function

    Private Sub PrepareCloudQuotaActions()
        Dim files = SelectedCloudFiles()
        refreshCloudQuota.Text = Language.GetText("MegaAccount_RefreshQuota")
        retryCloudDownload.Text = Language.GetText("MegaDrive_RetryDownload")
        refreshCloudQuota.Visible = files.Length > 0
        retryCloudDownload.Visible = files.Any(AddressOf CanRetryCloudDownload)
    End Sub

    Private Shared Function CanRetryCloudDownload(file As Fichero) As Boolean
        Dim state = file.DescargaEstado()
        Return state = Estado.EnCola OrElse state = Estado.Pausado OrElse (state = Estado.Erroneo AndAlso Not file.EsErrorPermanente)
    End Function

    Private Async Function RefreshSelectedCloudQuotaAsync(retry As Boolean) As Task
        Dim files = SelectedCloudFiles()
        Dim service = MegaAccountService.Current
        For Each account In files.GroupBy(Function(file) file.MegaAccountEmail, StringComparer.OrdinalIgnoreCase)
            Dim result = Await service.RefreshQuotaAsync(account.Key)
            If IsDisposed OrElse Cerrando Then Return
            If result <> 0 Then
                ToastForm.ShowToast(Me, MegaAccountService.ErrorText(result, connecting:=True))
                Continue For
            End If
            Dim info = service.GetStatus(account.Key)
            If info.QuotaReached Then
                ToastForm.ShowToast(Me, CloudQuotaText(info))
            ElseIf retry Then
                SyncLock Mutex.ListaDescargas
                    For Each file In account
                        If Not CanRetryCloudDownload(file) OrElse Not ListaPaquetes.Any(Function(package) package.ListaFicheros.Contains(file)) Then Continue For
                        If file.DescargaEstado() = Estado.Erroneo Then
                            file.ResetearDescarga(preserveProgress:=True)
                            file.SetDescargaEstado = Estado.EnCola
                        End If
                        file.PausaIndividual = False
                    Next
                    PeticionGuardadoFichero = Now
                End SyncLock
            Else
                ToastForm.ShowToast(Me, MegaAccountForm.Summary(info, True))
            End If
        Next
        RefreshListaDescargas(False)
        If retry AndAlso EstadoAplicacion = TipoEstadoAplicacion.Descargando Then PonerFicherosADescargar()
    End Function

    Private Shared Function CloudQuotaText(account As MegaAccountService.Status) As String
        Dim caption = Language.GetText("MegaDrive_WaitingQuota")
        If account.QuotaRetryUtc.HasValue Then caption &= " · " & FormatQuotaRemaining(account.QuotaRetryUtc.Value - Date.UtcNow)
        Return caption
    End Function

    Private Shared Function CloudQueueStatus(item As IDescarga) As String
        Dim file = TryCast(item, Fichero)
        If file Is Nothing OrElse Not file.MegaDriveNodeHandle.HasValue Then Return Nothing
        If file.DescargaEstado() <> Estado.EnCola AndAlso file.DescargaEstado() <> Estado.Pausado Then Return Nothing
        Dim account = MegaAccountService.Current.GetStatus(file.MegaAccountEmail)
        Return If(account.SignedIn AndAlso account.QuotaReached, CloudQuotaText(account), Nothing)
    End Function

    Private Shared Function CanRetryMegaAccountFailure(file As Fichero, retryDue As Boolean, autoRetry As Boolean) As Boolean
        If file.PausaIndividual Then Return False
        Dim status = MegaAccountService.Current.GetStatus(file.EffectiveMegaAccountEmail)
        If Not status.SignedIn OrElse status.Connecting OrElse status.QuotaReached Then Return False
        If file.FailedByQuota Then Return Not status.QuotaDelayUnknown OrElse retryDue
        Dim code = file.MegaAccountErrorCode.GetValueOrDefault()
        Return code = MegaAccountService.SessionExpired OrElse code = MegaAccountService.Busy OrElse
            (autoRetry AndAlso retryDue AndAlso Fichero.IsTransientMegaAccountError(code))
    End Function

    Private Shared Function CloudQuotaBlocks(file As Fichero) As Boolean
        If Not file.MegaDriveNodeHandle.HasValue Then Return False
        Dim account = MegaAccountService.Current.GetStatus(file.MegaAccountEmail)
        Return account.SignedIn AndAlso account.QuotaReached
    End Function

    Private Sub InitMegaAccountUi()
        megaAccountContext = SynchronizationContext.Current
        megaAccountStatus = New ToolStripStatusLabel With {.Name = "MegaAccountStatus", .Overflow = ToolStripItemOverflow.AsNeeded}
        StatusStrip1.Items.Insert(1, megaAccountStatus)
        AddHandler MegaAccountService.Current.Changed, AddressOf MegaAccountChanged
        AddHandler Disposed, Sub() RemoveHandler MegaAccountService.Current.Changed, AddressOf MegaAccountChanged
        EnsureMegaAccountMenu()
        UpdateMegaAccountUi()
    End Sub

    Private Sub EnsureMegaAccountMenu()
        If MainMenuStrip Is Nothing OrElse megaAccountStatus Is Nothing Then Return
        If megaAccountMenu IsNot Nothing AndAlso megaAccountMenu.Owner Is MainMenuStrip Then Return
        megaAccountMenu = New ToolStripMenuItem With {.Name = "MegaAccountMenu", .Alignment = ToolStripItemAlignment.Right,
                                                       .Overflow = ToolStripItemOverflow.Never, .Padding = New Padding(8, 0, 8, 0)}
        AddHandler megaAccountMenu.Click, AddressOf OpenMegaAccount
        MainMenuStrip.Items.Add(megaAccountMenu)
        UpdateMegaAccountUi()
    End Sub

    Private Sub MegaAccountChanged(sender As Object, e As EventArgs)
        megaAccountContext.Post(Sub(state)
                                    If Not IsDisposed Then UpdateMegaAccountUi()
                                End Sub, Nothing)
    End Sub

    Private Sub UpdateMegaAccountUi()
        If megaAccountStatus Is Nothing Then Return
        Dim service = MegaAccountService.Current
        Dim account = service.GetStatus()
        Dim summary As String = MegaAccountForm.Summary(account)
        If megaAccountStatus.Text <> summary Then megaAccountStatus.Text = summary
        megaAccountStatus.ForeColor = ThemeManager.GetColor(If(account.QuotaReached OrElse account.NeedsLogin, "ErrorFore", "Muted"))
        If megaAccountMenu Is Nothing Then Return
        Dim hasDefault = Not String.IsNullOrEmpty(account.Email)
        Dim caption = Language.GetText(If(account.NeedsLogin, "MegaAccount_Reconnect", "MegaAccount_Login"))
        Dim fullCaption = caption
        If hasDefault Then
            fullCaption = "MEGA: " & account.DisplayName
            caption = fullCaption
            If TextRenderer.MeasureText(caption, megaAccountMenu.Font, Size.Empty, TextFormatFlags.NoPrefix).Width > LogicalToDeviceUnits(220) Then
                caption = Language.GetText("MegaAccount_Manage")
            End If
        ElseIf service.GetAccounts().Count > 0 Then
            caption = Language.GetText("MegaAccount_Manage")
            fullCaption = caption
        End If
        caption = caption.Replace("&", "&&")
        If megaAccountMenu.Text <> caption Then megaAccountMenu.Text = caption
        megaAccountMenu.ToolTipText = If(hasDefault AndAlso account.DisplayName <> account.Email, fullCaption & " (" & account.Email & ")", fullCaption)
        megaAccountMenu.AccessibleName = If(hasDefault, Language.GetText("MegaAccount_Manage") & ": " & account.DisplayName, fullCaption)
        megaAccountMenu.Enabled = Not service.IsRestoring
    End Sub

    Private Async Sub RestoreMegaAccount()
        Await MegaAccountService.Current.RestoreAsync()
    End Sub

    Private Async Sub OpenMegaDrive(sender As Object, e As EventArgs)
        If openingMegaDrive Then Return
        openingMegaDrive = True
        Try
            If Not Await WaitForStartupQueueAsync() Then Return
            Using dialog As New MegaDriveForm(Me, Config, MegaAccountService.Current)
                dialog.ShowDialog(Me)
            End Using
        Finally
            openingMegaDrive = False
        End Try
    End Sub

    Friend Function SnapshotCloudQueue(email As String) As List(Of MegaDriveQueueItem)
        Dim files As New List(Of MegaDriveQueueItem)()
        SyncLock Mutex.ListaDescargas
            If ListaPaquetes Is Nothing Then Return files
            SyncLock Mutex.FicheroDownloader
                For Each package In ListaPaquetes
                    For Each file In package.ListaFicheros
                        If file.MegaDriveNodeHandle.HasValue AndAlso String.Equals(file.MegaAccountEmail, email, StringComparison.OrdinalIgnoreCase) Then
                            files.Add(New MegaDriveQueueItem(file.MegaDriveNodeHandle.Value, file.RutaLocal, file.NombreFichero, file.TamanoBytes,
                                                             file.DescargaEstado() = Estado.Completado))
                        End If
                    Next
                Next
            End SyncLock
        End SyncLock
        Return files
    End Function

    Private Async Sub OpenMegaAccount(sender As Object, e As EventArgs)
        Dim signedInEmail As String
        Using dialog As New MegaAccountForm()
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            signedInEmail = dialog.SignedInEmail
        End Using
        Await MegaAccountService.Current.RefreshQuotaAsync(signedInEmail)
    End Sub
End Class
