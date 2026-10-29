Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class AddLinks
    Private preflightView As BatchPreflightView
    Private onSelectionPage As Boolean
    Private closeAfterImport As Boolean
    Private importCompleted As Integer
    Private importTotal As Integer
    Private importCommitting As Boolean

    Private Sub ShowSelectionPage()
        If importBusy OrElse _watchResolving OrElse preflightDisposed Then Return
        If String.IsNullOrWhiteSpace(txtLinks.Text) Then
            ShowInputNotice(Language.GetText("Links not valid"), True)
            txtLinks.Focus()
            Return
        End If
        If String.IsNullOrWhiteSpace(txtRuta.Text) Then
            ShowInputNotice(Language.GetText("AddLinks_PathRequired"), True)
            txtRuta.Focus()
            Return
        End If
        ShowInputNotice("")
        If _linkCountTimer IsNot Nothing Then _linkCountTimer.Stop()
        preflightProgressTimer.Stop()
        Dim created As Boolean = preflightView Is Nothing
        If created Then
            Dim prepared As PreflightSession.Request = CurrentPreflightRequest()
            Dim mainForm As Main = Main
            preflightView = New BatchPreflightView(New List(Of String)(), Config,
                New Dictionary(Of String, URLProcessor.FileURL)(StringComparer.Ordinal), txtRuta.Text,
                preflightSession, prepared, Function() mainForm.GetQueuedLinkSnapshot())
            AddHandler preflightView.BackRequested, AddressOf BackFromSelection
            AddHandler preflightView.CancelRequested, AddressOf DismissSelection
            AddHandler preflightView.ConfirmRequested, AddressOf SubmitSelection
            addLinksPageHost.Controls.Add(preflightView)
        Else
            preflightView.UpdateDestination(txtRuta.Text)
        End If
        onSelectionPage = True
        addLinksInputPage.Visible = False
        preflightView.Visible = True
        preflightView.BringToFront()
        addLinksStepLabel.Text = Language.GetText("AddLinks_StepSelection")
        AcceptButton = preflightView.ConfirmationButton
        CancelButton = preflightView.DismissButton
        ' Form.CancelButton assigns DialogResult.Cancel by default; this button
        ' cancels the build in place while an import is running.
        preflightView.DismissButton.DialogResult = DialogResult.None
        If created Then
            ThemeManager.ApplyTheme(Me)
            preflightView.StartInspection()
        End If
        preflightView.Focus()
    End Sub

    Private Sub BackFromSelection(sender As Object, e As EventArgs)
        If Not importBusy Then ShowInputPage()
    End Sub

    Private Sub ShowInputPage(Optional mergeDeferred As Boolean = True)
        onSelectionPage = False
        If preflightView IsNot Nothing Then preflightView.Visible = False
        addLinksInputPage.Visible = True
        addLinksInputPage.BringToFront()
        addLinksStepLabel.Text = Language.GetText("AddLinks_StepInput")
        AcceptButton = btnAgregar
        CancelButton = addLinksCancel
        UpdateAddLinksInputHeight()
        If mergeDeferred AndAlso deferredInputs.Count > 0 Then
            RestoreDeferredInputs(False)
            ShowInputNotice(Language.GetText("AddLinks_DeferredMerged"))
        ElseIf mergeDeferred Then
            If preflightView IsNot Nothing AndAlso preflightView.InspectionStopped Then
                preflightProgressTimer.Stop()
                lblLinkCount.Text = Language.GetText("AddLinks_CheckStopped")
                preflightProgressBar.Style = ProgressBarStyle.Continuous
                preflightProgressBar.Value = 0
            ElseIf preflightView IsNot Nothing Then
                ' Returning is navigation, not a new request (including after
                ' the short metadata cache would otherwise have expired).
                warmRequest = preflightView.ActiveRequest
                displayedProgress = Nothing
                RemoveHandler preflightProgressTimer.Tick, AddressOf RefreshPreflightProgress
                AddHandler preflightProgressTimer.Tick, AddressOf RefreshPreflightProgress
                preflightProgressTimer.Start()
                RefreshPreflightProgress(Nothing, EventArgs.Empty)
            Else
                UpdateLinkCount()
            End If
        End If
        txtLinks.Focus()
    End Sub

    Private Sub InvalidateSelectionPage()
        If preflightView Is Nothing Then Return
        If onSelectionPage Then ShowInputPage(False)
        Dim obsolete As BatchPreflightView = preflightView
        preflightView = Nothing
        addLinksPageHost.Controls.Remove(obsolete)
        obsolete.Dispose()
    End Sub

    Private Sub ShowInputNotice(text As String, Optional isError As Boolean = False)
        If addLinksFeedback Is Nothing Then Return
        addLinksFeedback.Text = text
        addLinksFeedback.Visible = Not String.IsNullOrEmpty(text)
        UiStyles.SetRole(addLinksFeedback, If(isError, UiStyles.Role.ErrorText, UiStyles.Role.BodyText))
    End Sub

    Private Sub DismissSelection(sender As Object, e As EventArgs)
        If importBusy Then
            CancelImport()
        Else
            Close()
        End If
    End Sub

    Private Sub CancelImport()
        If Not importBusy OrElse importCommitting OrElse importCancellation Is Nothing Then Return
        importCancellation.Cancel()
        RefreshImportProgress()
    End Sub

    Private Sub AddLinks_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If Not importBusy OrElse preflightDisposed Then Return
        If e.CloseReason <> CloseReason.UserClosing Then
            ' App/system shutdown must not wait for the UI continuation to run.
            If importCancellation IsNot Nothing Then importCancellation.Cancel()
            Return
        End If
        ' Never close between building a package and publishing it to the queue.
        e.Cancel = True
        closeAfterImport = True
        CancelImport()
    End Sub

    Private Sub RefreshImportProgress()
        If preflightView Is Nothing OrElse preflightView.IsDisposed Then Return
        preflightView.ShowImportProgress(Volatile.Read(importCompleted), importTotal, importCommitting,
            importCancellation IsNot Nothing AndAlso importCancellation.IsCancellationRequested)
    End Sub

    Private Async Sub SubmitSelection(sender As Object, e As EventArgs)
        If importBusy OrElse preflightDisposed OrElse preflightView Is Nothing Then Return
        Dim selected As List(Of URLProcessor.FileURL) = preflightView.SelectedFiles
        If selected Is Nothing OrElse selected.Count = 0 Then Return
        importBusy = True
        importCancellation = New CancellationTokenSource()
        Dim source As CancellationTokenSource = importCancellation
        Dim token As CancellationToken = source.Token
        importTotal = selected.Count
        Volatile.Write(importCompleted, 0)
        importCommitting = False
        Dim committed As Boolean = False
        preflightView.ShowNotice("")
        preflightView.SetImporting(True)
        btnAgregar.Enabled = False
        btnWatchOnline.Enabled = False
        txtLinks.ReadOnly = True
        OpcionesPaquete.Enabled = False
        RemoveHandler preflightProgressTimer.Tick, AddressOf RefreshPreflightProgress
        AddHandler preflightProgressTimer.Tick, AddressOf RefreshPreflightProgress
        preflightProgressTimer.Start()
        RefreshImportProgress()
        Try
            Dim latest = Await Task.Run(Function() Main.GetQueuedLinkSnapshot(), token)
            selected = Await Task.Run(Function()
                                          Dim known As New HashSet(Of String)(latest.Keys, StringComparer.Ordinal)
                                          Dim result As New List(Of URLProcessor.FileURL)()
                                          For Each file As URLProcessor.FileURL In selected
                                              token.ThrowIfCancellationRequested()
                                              Dim key As String = BatchLinkPreflight.Identity(file)
                                              If key Is Nothing OrElse known.Add(key) Then result.Add(file)
                                          Next
                                          Return result
                                      End Function, token)
            token.ThrowIfCancellationRequested()
            If preflightDisposed Then Return
            If selected.Count = 0 Then
                preflightView.ShowNotice(Language.GetText("Preflight_AllQueued"))
                Return
            End If
            importTotal = selected.Count
            committed = Await FinishAddPackageAsync(selected)
            If committed Then
                UltimaConfiguracionUsada.ExisteUltimaConfiguracion = True
                UltimaConfiguracionUsada.CrearDirectorioPaquete = chkCrearDirectorio.Checked
                UltimaConfiguracionUsada.ExtraerAutomaticamente = chkUnZip.Checked
                UltimaConfiguracionUsada.RutaDescarga = txtRuta.Text
                UltimaConfiguracionUsada.IniciarDescarga = chkStartDownload.Checked
                If chkStartDownload.Checked Then
                    Try
                        Main.StartDownload()
                    Catch ex As Exception
                        ' The tasks are already queued; a start failure must not
                        ' put the same import back into the uncommitted state.
                        Log.WriteError("Starting the imported tasks failed: " & Log.SafeException(ex))
                        ToastForm.ShowToast(Main, Language.GetText("AddLinks_AddedPaused"))
                    End Try
                End If
            End If
        Catch ex As OperationCanceledException
            If Not preflightDisposed Then preflightView.ShowNotice(Language.GetText("AddLinks_ImportCancelled"))
        Catch ex As Exception
            Log.WriteError("Error while adding links: " & Log.SafeException(ex))
            If Not preflightDisposed Then preflightView.ShowNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), True)
        Finally
            importBusy = False
            importCommitting = False
            importCancellation = Nothing
            source.Dispose()
            If Not preflightDisposed Then
                preflightProgressTimer.Stop()
                txtLinks.ReadOnly = False
                OpcionesPaquete.Enabled = True
                ResetResolveButtons()
                preflightView.SetImporting(False)
                If closeAfterImport OrElse (committed AndAlso deferredInputs.Count = 0) Then
                    Close()
                ElseIf committed Then
                    ShowInputPage(False)
                    RestoreDeferredInputs(True)
                    ShowInputNotice(Language.GetText("AddLinks_AddedMore").Replace("%N%", importTotal.ToString()))
                End If
            End If
        End Try
    End Sub
End Class
