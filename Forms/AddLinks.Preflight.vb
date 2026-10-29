Imports System.Collections.Generic
Imports System.Threading.Tasks

Partial Public Class AddLinks
    Private preflightSession As PreflightSession
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

    Private Function CurrentPreflightRequest() As PreflightSession.Request
        If preflightSession Is Nothing Then preflightSession = New PreflightSession(Config)
        warmRequest = preflightSession.Prepare(txtLinks.Text & vbNewLine & HiddenLinks)
        Return warmRequest
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
        inputGeneration += 1
        warmRequest = Nothing
        preflightProgressTimer.Dispose()
        If t IsNot Nothing Then t.Dispose()
        If preflightSession IsNot Nothing Then preflightSession.Dispose()
        If importCancellation IsNot Nothing Then importCancellation.Cancel()
    End Sub
End Class
