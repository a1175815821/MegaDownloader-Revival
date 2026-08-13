Imports System.IO

Public Class Log

    Public Enum LevelLogType
        Minimal = 0 ' Only errors
        Normal = 1  ' Errors + warnings
        Info = 2    ' Errors + warnings + info
        Debug = 3   ' Errors + warnings + info + debug
    End Enum

    Private Shared _syncObject As Object = New Object
    Private Shared _LogLevel As LevelLogType = LevelLogType.Normal
    Private Shared _Buffer As System.Text.StringBuilder = Nothing
    Private Shared _LastWrite As Date = DateTime.UtcNow
    ' Log retention: files older than this many days are purged. Purge runs once
    ' per process session to avoid scanning the log folder on every flush.
    Private Const RetentionDays As Integer = 30
    Private Shared _purgePerformed As Boolean = False


    Public Shared WriteOnly Property SetLogLevel() As LevelLogType
        Set(value As LevelLogType)
            _LogLevel = value
        End Set
    End Property

    Public Shared Sub WriteError(Text As String)
        WriteLog(Redact(Text), LevelLogType.Minimal)
    End Sub

    ''' <summary>
    ''' Exception text without full URI / MEGA link material.
    ''' </summary>
    Public Shared Function SafeException(ByVal ex As Exception) As String
        If ex Is Nothing Then Return String.Empty
        Return Redact(ex.GetType().Name & ": " & ex.Message)
    End Function

    Public Shared Function Redact(ByVal text As String) As String
        If String.IsNullOrEmpty(text) Then Return text
        Dim t As String = text
        Try
            t = System.Text.RegularExpressions.Regex.Replace(t, "https?://[^\s""']+", "[url]")
            t = System.Text.RegularExpressions.Regex.Replace(t, "mega://[^\s""']+", "[mega-link]")
            t = System.Text.RegularExpressions.Regex.Replace(t, "(?i)(filekey|key|sid|password|token)\s*[=:]\s*[^\s,;]+", "$1=[redacted]")
        Catch
        End Try
        Return t
    End Function

    Public Shared Sub WriteWarning(Text As String)
        WriteLog(Text, LevelLogType.Normal)
    End Sub

    Public Shared Sub WriteInfo(Text As String)
        WriteLog(Text, LevelLogType.Info)
    End Sub

    Public Shared Sub WriteDebug(Text As String)
        WriteLog(Text, LevelLogType.Debug)
    End Sub


    Public Shared Sub WriteLog(Text As String, Level As LevelLogType)
        If _LogLevel < Level Then Exit Sub

        SyncLock (_syncObject)

            If _Buffer Is Nothing Then
                _Buffer = New System.Text.StringBuilder
            End If

            _Buffer.Append(DateTime.UtcNow.ToString("s"))
            _Buffer.Append(":")
            _Buffer.Append(DateTime.UtcNow.ToString("fff"))
            _Buffer.Append("Z [ID#")
            _Buffer.Append(System.Threading.Thread.CurrentThread.ManagedThreadId)
            _Buffer.Append("] >>> ")
            _Buffer.AppendLine(Text)

        End SyncLock

        Flush(False)
    End Sub

    Public Shared Sub Flush(forceFlush As Boolean)
        Dim DoFlush As Boolean = False

        SyncLock (_syncObject)
            DoFlush = (_LastWrite.AddSeconds(10) < DateTime.UtcNow)

            If (DoFlush Or forceFlush) And _Buffer IsNot Nothing AndAlso _Buffer.Length > 0 Then
                Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Log")

                If Not System.IO.Directory.Exists(PathLog) Then
                    System.IO.Directory.CreateDirectory(PathLog)
                End If
                Using t As New StreamWriter(PathLog & "\Log_" & DateTime.UtcNow.ToString("yyyyMMdd") & ".txt", True)
                    t.Write(_Buffer.ToString)
                End Using
                _Buffer = Nothing
                _LastWrite = DateTime.UtcNow

                ' Purge old log files once per process session.
                If Not _purgePerformed Then
                    _purgePerformed = True
                    PurgeOldLogs(PathLog, RetentionDays)
                End If
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' Deletes log files older than the retention period. Best-effort: any error
    ''' is swallowed so logging never fails because of cleanup issues.
    ''' </summary>
    Private Shared Sub PurgeOldLogs(logDir As String, days As Integer)
        Try
            Dim cutoff As Date = DateTime.UtcNow.AddDays(-days)
            For Each f As String In System.IO.Directory.GetFiles(logDir, "Log_*.txt")
                Try
                    Dim fi As New System.IO.FileInfo(f)
                    If fi.LastWriteTimeUtc < cutoff Then fi.Delete()
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub



End Class