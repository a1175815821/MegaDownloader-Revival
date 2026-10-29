Imports System.Globalization
Imports System.Xml

''' <summary>Local-time download windows. No timers, I/O or queue mutations.</summary>
Public NotInheritable Class DownloadScheduleSettings
    Public Enabled As Boolean
    Public Daily As Boolean = True
    Public StartMinute As Integer
    Public EndMinute As Integer = 420
    Public OnceStart As DateTime = DateTime.Today.AddDays(1)
    Public OnceEnd As DateTime = DateTime.Today.AddDays(1).AddHours(7)

    Public Function IsValid() As Boolean
        If Daily Then Return StartMinute >= 0 AndAlso StartMinute < 1440 AndAlso EndMinute >= 0 AndAlso EndMinute < 1440 AndAlso StartMinute <> EndMinute
        Return OnceEnd > OnceStart
    End Function

    ''' <summary>Window identity stays constant across midnight; end is exclusive.</summary>
    Public Function ActiveWindow(now As DateTime) As String
        If Not Enabled OrElse Not IsValid() Then Return Nothing
        If Not Daily Then
            If now >= OnceStart AndAlso now < OnceEnd Then Return OnceStart.ToString("O", CultureInfo.InvariantCulture)
            Return Nothing
        End If
        Dim minute As Double = now.TimeOfDay.TotalMinutes
        Dim startDate As DateTime = now.Date
        If StartMinute < EndMinute Then
            If minute < StartMinute OrElse minute >= EndMinute Then Return Nothing
        Else
            If minute >= EndMinute AndAlso minute < StartMinute Then Return Nothing
            If minute < EndMinute Then startDate = startDate.AddDays(-1)
        End If
        Return startDate.AddMinutes(StartMinute).ToString("O", CultureInfo.InvariantCulture)
    End Function

    Public Function AllowsDownload(now As DateTime) As Boolean
        Return Not Enabled OrElse ActiveWindow(now) IsNot Nothing
    End Function

    Public Function ToXml(document As XmlDocument) As XmlElement
        Dim node As XmlElement = document.CreateElement("DownloadSchedule")
        node.SetAttribute("Enabled", Enabled.ToString())
        node.SetAttribute("Daily", Daily.ToString())
        node.SetAttribute("StartMinute", StartMinute.ToString(CultureInfo.InvariantCulture))
        node.SetAttribute("EndMinute", EndMinute.ToString(CultureInfo.InvariantCulture))
        node.SetAttribute("OnceStart", OnceStart.ToString("O", CultureInfo.InvariantCulture))
        node.SetAttribute("OnceEnd", OnceEnd.ToString("O", CultureInfo.InvariantCulture))
        Return node
    End Function

    Public Shared Function FromXml(node As XmlNode) As DownloadScheduleSettings
        Dim result As New DownloadScheduleSettings()
        If node Is Nothing OrElse node.Attributes Is Nothing Then Return result
        Dim enabled As Boolean, daily As Boolean, startMinute As Integer, endMinute As Integer
        Dim onceStart As DateTime, onceEnd As DateTime
        Dim getValue As Func(Of String, String) = Function(name) If(node.Attributes(name) Is Nothing, "", node.Attributes(name).Value)
        If Not Boolean.TryParse(getValue("Enabled"), enabled) OrElse
           Not Boolean.TryParse(getValue("Daily"), daily) Then Return result
        result.Daily = daily
        Dim dailyValid As Boolean = Integer.TryParse(getValue("StartMinute"), NumberStyles.Integer, CultureInfo.InvariantCulture, startMinute) AndAlso
            Integer.TryParse(getValue("EndMinute"), NumberStyles.Integer, CultureInfo.InvariantCulture, endMinute) AndAlso
            startMinute >= 0 AndAlso startMinute < 1440 AndAlso endMinute >= 0 AndAlso endMinute < 1440 AndAlso startMinute <> endMinute
        If dailyValid Then
            result.StartMinute = startMinute
            result.EndMinute = endMinute
        End If
        Dim onceValid As Boolean = DateTime.TryParseExact(getValue("OnceStart"), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, onceStart) AndAlso
            DateTime.TryParseExact(getValue("OnceEnd"), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, onceEnd) AndAlso onceEnd > onceStart
        If onceValid Then
            result.OnceStart = DateTime.SpecifyKind(onceStart, DateTimeKind.Unspecified)
            result.OnceEnd = DateTime.SpecifyKind(onceEnd, DateTimeKind.Unspecified)
        End If
        If (daily AndAlso Not dailyValid) OrElse (Not daily AndAlso Not onceValid) Then Return New DownloadScheduleSettings()
        result.Enabled = enabled AndAlso result.IsValid()
        Return result
    End Function
End Class

''' <summary>Edge-triggered actions; a manual pause lasts for the current window.</summary>
Public NotInheritable Class DownloadScheduleController
    Public Enum ScheduleAction
        None
        Start
        Pause
    End Enum

    Private _lastWindow As String
    Private _manualWindow As String
    Private ReadOnly stateLock As New Object()

    Public Sub Reset()
        SyncLock stateLock
            _lastWindow = Nothing
            _manualWindow = Nothing
        End SyncLock
    End Sub

    Public Sub MarkManualPause(settings As DownloadScheduleSettings, now As DateTime)
        SyncLock stateLock
            _manualWindow = settings.ActiveWindow(now)
        End SyncLock
    End Sub

    Public Sub MarkManualStart()
        SyncLock stateLock
            _manualWindow = Nothing
        End SyncLock
    End Sub

    Public Function Evaluate(settings As DownloadScheduleSettings, now As DateTime, isDownloading As Boolean) As ScheduleAction
        SyncLock stateLock
            If Not settings.Enabled Then
                Reset()
                Return ScheduleAction.None
            End If
            Dim window As String = settings.ActiveWindow(now)
            Dim changed As Boolean = Not String.Equals(window, _lastWindow, StringComparison.Ordinal)
            _lastWindow = window
            If window Is Nothing Then
                If isDownloading Then Return ScheduleAction.Pause
                Return ScheduleAction.None
            End If
            If changed AndAlso Not String.Equals(window, _manualWindow, StringComparison.Ordinal) AndAlso Not isDownloading Then Return ScheduleAction.Start
            Return ScheduleAction.None
        End SyncLock
    End Function
End Class
