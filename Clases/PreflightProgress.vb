Imports System.Windows.Forms

''' <summary>Immutable snapshots are polled by the UI; folder workers never post one UI event per file.</summary>
Public NotInheritable Class PreflightProgress
    Public Enum Phase
        Parsing
        Waiting
        Reading
        Checking
        Completed
        Failed
        Cancelled
    End Enum

    Public ReadOnly Stage As Phase
    Public ReadOnly LinksCompleted As Integer
    Public ReadOnly LinksTotal As Integer
    Public ReadOnly ItemsProcessed As Integer
    Public ReadOnly ItemsTotal As Integer
    Public ReadOnly FilesFound As Integer

    Public Sub New(value As Phase, Optional completed As Integer = 0, Optional total As Integer = 0,
                   Optional processed As Integer = 0, Optional itemCount As Integer = 0, Optional found As Integer = 0)
        Stage = value
        LinksCompleted = completed
        LinksTotal = total
        ItemsProcessed = processed
        ItemsTotal = itemCount
        FilesFound = found
    End Sub

    Public Shared Sub Display(snapshot As PreflightProgress, label As Label, bar As ProgressBar, Optional started As DateTime? = Nothing)
        If snapshot Is Nothing Then Return
        Dim key As String
        Select Case snapshot.Stage
            Case Phase.Parsing : key = "Preflight_ProgressParsing"
            Case Phase.Waiting : key = "Preflight_ProgressWaiting"
            Case Phase.Reading : key = "Preflight_ProgressReading"
            Case Phase.Checking : key = "Preflight_ProgressChecking"
            Case Phase.Completed : key = "Preflight_Warmed"
            Case Phase.Failed : key = "AddLinks_CheckFailed"
            Case Phase.Cancelled : key = "Preflight_ProgressCancelled"
            Case Else : key = "Preflight_Waiting"
        End Select
        Dim text As String = Language.GetText(key).Replace("%N%", If(snapshot.Stage = Phase.Completed, snapshot.LinksTotal, snapshot.ItemsProcessed).ToString()).
            Replace("%F%", If(snapshot.Stage = Phase.Completed, snapshot.FilesFound, snapshot.ItemsTotal).ToString())
        If snapshot.Stage = Phase.Reading OrElse snapshot.Stage = Phase.Checking Then
            text &= " · " & Language.GetText("Preflight_ProgressLinks").Replace("%D%", snapshot.LinksCompleted.ToString()).Replace("%T%", snapshot.LinksTotal.ToString())
        End If
        If started.HasValue AndAlso snapshot.Stage <> Phase.Completed AndAlso snapshot.Stage <> Phase.Failed AndAlso snapshot.Stage <> Phase.Cancelled Then
            Dim seconds As Long = CLng(Math.Max(0, Math.Floor((DateTime.UtcNow - started.Value).TotalSeconds)))
            text &= " · " & Language.GetText("Preflight_Elapsed").Replace("%T%", seconds.ToString())
        End If
        label.Text = text
        bar.Visible = True
        bar.Maximum = 1000
        If snapshot.Stage = Phase.Completed Then
            bar.Style = ProgressBarStyle.Continuous
            bar.Value = bar.Maximum
        ElseIf snapshot.Stage = Phase.Checking AndAlso snapshot.ItemsTotal > 0 Then
            bar.Style = ProgressBarStyle.Continuous
            Dim portion As Double = Math.Min(1.0R, CDbl(snapshot.ItemsProcessed) / snapshot.ItemsTotal)
            bar.Value = CInt(Math.Min(999.0R, (snapshot.LinksCompleted + portion) * 1000.0R / Math.Max(1, snapshot.LinksTotal)))
        ElseIf snapshot.Stage = Phase.Failed OrElse snapshot.Stage = Phase.Cancelled Then
            bar.Style = ProgressBarStyle.Continuous
            bar.Value = 0
        Else
            ' The server has not returned the folder size yet: no invented percentage.
            bar.Style = ProgressBarStyle.Marquee
            bar.MarqueeAnimationSpeed = 30
        End If
    End Sub

    Friend NotInheritable Class Reporter
        Implements IProgress(Of PreflightProgress)
        Private ReadOnly callback As Action(Of PreflightProgress)
        Public Sub New(action As Action(Of PreflightProgress))
            callback = action
        End Sub
        Public Sub Report(value As PreflightProgress) Implements IProgress(Of PreflightProgress).Report
            callback(value)
        End Sub
    End Class

    Friend NotInheritable Class ItemReporter
        Implements IProgress(Of Integer)
        Private ReadOnly callback As Action(Of Integer)
        Public Sub New(action As Action(Of Integer))
            callback = action
        End Sub
        Public Sub Report(value As Integer) Implements IProgress(Of Integer).Report
            callback(value)
        End Sub
    End Class
End Class
