Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class AddLinks
    Private preflightSession As PreflightSession
    Private previewMegaAccountEmail As String = ""
    Private previewMegaAccountAccess As Integer
    Private accountIdentityChanged As EventHandler
    Private warmRequest As PreflightSession.Request
    Private inputGeneration As Integer
    Private importBusy As Boolean
    Private preflightDisposed As Boolean
    Private importCancellation As System.Threading.CancellationTokenSource
    Private ReadOnly deferredInputs As New List(Of Tuple(Of String, Boolean, Boolean, List(Of String)))()
    Private ReadOnly preflightProgressTimer As New System.Windows.Forms.Timer With {.Interval = 150}
    Private displayedProgress As PreflightProgress
    Private displayedProgressSecond As Long = -1

    Private Sub RefreshPreflightProgress(sender As Object, e As EventArgs)
        If preflightDisposed Then Return
        If importBusy Then
            RefreshImportProgress()
            Return
        End If
        If warmRequest Is Nothing Then Return
        Dim snapshot As PreflightProgress = warmRequest.Progress
        If warmRequest.Results.IsCompleted Then preflightProgressTimer.Stop()
        Dim second As Long = CLng(Math.Max(0, Math.Floor((DateTime.UtcNow - warmRequest.Started).TotalSeconds)))
        If Object.ReferenceEquals(displayedProgress, snapshot) AndAlso second = displayedProgressSecond Then Return
        displayedProgress = snapshot
        displayedProgressSecond = second
        PreflightProgress.Display(snapshot, lblLinkCount, preflightProgressBar, warmRequest.Started)
    End Sub

    Private Class IdentityOption
        Friend ReadOnly Email As String
        Private ReadOnly caption As String

        Friend Sub New(email As String, caption As String)
            Me.Email = email
            Me.caption = caption
        End Sub

        Public Overrides Function ToString() As String
            Return caption
        End Function
    End Class

    Private Sub LoadDownloadIdentities(selectedEmail As String)
        RemoveHandler downloadIdentity.SelectedIndexChanged, AddressOf MegaAccountChoiceChanged
        downloadIdentity.Items.Clear()
        downloadIdentity.Items.Add(New IdentityOption("", Language.GetText("MegaAccount_Anonymous")))
        Dim selectedIndex As Integer = 0
        For Each status In MegaAccountService.Current.GetAccounts()
            Dim caption = If(status.AliasName = "", status.Email, status.AliasName & " (" & status.Email & ")")
            If Not status.SignedIn Then caption &= " · " & MegaAccountForm.Summary(status)
            Dim index = downloadIdentity.Items.Add(New IdentityOption(status.Email, caption))
            If String.Equals(status.Email, selectedEmail, StringComparison.OrdinalIgnoreCase) Then selectedIndex = index
        Next
        If selectedEmail <> "" AndAlso selectedIndex = 0 Then
            selectedIndex = downloadIdentity.Items.Add(New IdentityOption(selectedEmail, selectedEmail & " · " & Language.GetText("MegaAccount_Reconnect")))
        End If
        downloadIdentity.SelectedIndex = selectedIndex
        AddHandler downloadIdentity.SelectedIndexChanged, AddressOf MegaAccountChoiceChanged
        NotifyDownloadIdentityChanged()
    End Sub

    Private Sub MegaAccountChoiceChanged(sender As Object, e As EventArgs)
        NotifyDownloadIdentityChanged()
    End Sub

    Private Sub ObserveDownloadIdentities()
        Dim context = SynchronizationContext.Current
        accountIdentityChanged = Sub(sender, e) context.Post(Sub(state)
                                                                  If Not preflightDisposed AndAlso Not importBusy Then LoadDownloadIdentities(previewMegaAccountEmail)
                                                              End Sub, Nothing)
        AddHandler MegaAccountService.Current.Changed, accountIdentityChanged
        LoadDownloadIdentities(MegaAccountService.Current.DefaultEmail)
        ShowIdentityNotice()
    End Sub

    Private Sub NotifyDownloadIdentityChanged()
        Dim email = DirectCast(downloadIdentity.SelectedItem, IdentityOption).Email
        Dim access = MegaAccountService.Current.GetAccessError(email)
        Dim identityChanged = Not String.Equals(email, previewMegaAccountEmail, StringComparison.OrdinalIgnoreCase)
        If Not identityChanged AndAlso access = previewMegaAccountAccess Then
            If access <> 0 Then ShowIdentityNotice()
            Return
        End If
        previewMegaAccountEmail = email
        previewMegaAccountAccess = access
        If onSelectionPage AndAlso Not identityChanged Then
            ' A reconnect during subscription setup must not discard the public preview.
            ShowIdentityNotice()
            Return
        End If
        PreflightInputChanged()
        If identityChanged AndAlso preflightSession IsNot Nothing Then
            preflightSession.Dispose()
            preflightSession = Nothing
        End If
        ShowIdentityNotice()
        UpdateLinkCount()
    End Sub

    Private Sub ShowIdentityNotice()
        Dim access = previewMegaAccountAccess
        ShowInputNotice(If(access = 0, "", AccountAccessNotice(previewMegaAccountEmail, access)), access <> 0)
    End Sub

    Private Shared Function AccountAccessNotice(email As String, access As Integer) As String
        If access = MegaAccountService.SessionExpired Then
            Dim status = MegaAccountService.Current.GetStatus(email)
            If status.LastError <> 0 AndAlso Not status.NeedsLogin Then
                Return MegaAccountForm.Summary(status) & " " & MegaAccountService.ErrorText(status.LastError, signingIn:=True)
            End If
        End If
        Return MegaAccountService.ErrorText(access, connecting:=True)
    End Function

    Private Function CurrentPreflightRequest() As PreflightSession.Request
        If preflightSession Is Nothing Then
            If previewMegaAccountEmail <> "" Then
                Dim email As String = previewMegaAccountEmail
                preflightSession = New PreflightSession(Config, Function(url, token) InspectMegaAccountLink(url, email, token))
            Else
                preflightSession = New PreflightSession(Config)
            End If
        End If
        warmRequest = preflightSession.Prepare(txtLinks.Text & vbNewLine & HiddenLinks)
        Return warmRequest
    End Function

    Private Shared Function InspectMegaAccountLink(url As String, email As String, token As CancellationToken) As List(Of BatchLinkPreflight.Entry)
        Dim entry As New BatchLinkPreflight.Entry With {.Name = "—", .State = BatchLinkPreflight.LinkState.Invalid}
        Dim link As String = Fichero.GetMegaAccountLink(url)
        If link Is Nothing Then
            entry.Detail = Language.GetText("AddLinks_MegaAccountFilesOnly")
        Else
            token.ThrowIfCancellationRequested()
            Dim name As String = Nothing
            Dim size As Long
            Dim code As Integer = MegaAccountService.Current.Inspect(email, link, name, size)
            token.ThrowIfCancellationRequested()
            entry.Name = Fichero.ExtraerFileID(url)
            entry.Source = New URLProcessor.FileURL(url, "")
            If code = 0 AndAlso Not String.IsNullOrEmpty(name) AndAlso size >= 0 Then
                entry.Name = PathGuard.SanitizeFileName(name, "file")
                entry.SizeBytes = size
                entry.Source.DisplayName = entry.Name
                entry.Source.SizeBytes = size
                entry.State = BatchLinkPreflight.LinkState.Ready
            Else
                entry.Detail = MegaAccountService.ErrorText(If(code = 0, -2, code), connecting:=True)
                If Not Fichero.IsPermanentMegaAccountError(code) Then entry.State = BatchLinkPreflight.LinkState.Unavailable
            End If
        End If
        Return New List(Of BatchLinkPreflight.Entry) From {entry}
    End Function

    Private Async Sub UpdateLinkCount()
        If Config Is Nothing OrElse preflightDisposed OrElse importBusy Then Return
        Dim generation As Integer = inputGeneration
        Try
            If String.IsNullOrWhiteSpace(txtLinks.Text) Then
                HiddenLinks = String.Empty
                lblLinkCount.Text = ""
                Return
            End If
            Dim request As PreflightSession.Request = CurrentPreflightRequest()
            RemoveHandler preflightProgressTimer.Tick, AddressOf RefreshPreflightProgress
            AddHandler preflightProgressTimer.Tick, AddressOf RefreshPreflightProgress
            preflightProgressTimer.Start()
            RefreshPreflightProgress(Nothing, EventArgs.Empty)
            Dim parsed As List(Of String) = Await request.Urls
            If preflightDisposed OrElse generation <> inputGeneration OrElse importBusy Then Return
            Dim result As List(Of BatchLinkPreflight.Entry) = Await request.Results
            If preflightDisposed OrElse generation <> inputGeneration OrElse importBusy Then Return
            RefreshPreflightProgress(Nothing, EventArgs.Empty)
            preflightProgressTimer.Stop()
        Catch ex As OperationCanceledException
        Catch ex As Exception
            If Not preflightDisposed AndAlso generation = inputGeneration Then
                RefreshPreflightProgress(Nothing, EventArgs.Empty)
                preflightProgressTimer.Stop()
            End If
            Log.WriteWarning("Background preflight failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub PreflightInputChanged()
        inputGeneration += 1
        InvalidateSelectionPage()
        Dim empty As Boolean = String.IsNullOrWhiteSpace(txtLinks.Text)
        warmRequest = Nothing
        displayedProgress = Nothing
        preflightProgressTimer.Stop()
        preflightProgressBar.Visible = Not empty
        preflightProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee
        If preflightSession IsNot Nothing AndAlso Not importBusy Then preflightSession.Invalidate(empty)
        If _cueFallbackLabel IsNot Nothing AndAlso Not _cueFallbackLabel.IsDisposed Then _cueFallbackLabel.Visible = txtLinks.TextLength = 0
        lblLinkCount.Text = If(empty, "", Language.GetText("Preflight_Waiting"))
    End Sub

    Private Sub RestoreDeferredInputs(clearOld As Boolean)
        If clearOld Then
            HiddenLinks = String.Empty
            txtLinks.Clear()
        End If
        Dim pending As New List(Of Tuple(Of String, Boolean, Boolean, List(Of String)))(deferredInputs)
        deferredInputs.Clear()
        For Each item As Tuple(Of String, Boolean, Boolean, List(Of String)) In pending
            AgregarEnlaces(item.Item1, False, item.Item2, item.Item3, item.Item4)
        Next
        UpdateLinkCount()
    End Sub

    Private Sub AddLinks_Disposed(sender As Object, e As EventArgs) Handles Me.Disposed
        DisposePreflight()
    End Sub

    Private Sub DisposePreflight()
        If preflightDisposed Then Return
        preflightDisposed = True
        If accountIdentityChanged IsNot Nothing Then RemoveHandler MegaAccountService.Current.Changed, accountIdentityChanged
        inputGeneration += 1
        warmRequest = Nothing
        preflightProgressTimer.Dispose()
        If t IsNot Nothing Then t.Dispose()
        If preflightSession IsNot Nothing Then preflightSession.Dispose()
        If importCancellation IsNot Nothing Then importCancellation.Cancel()
    End Sub
End Class
