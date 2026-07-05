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
    Private Shared _LastWrite As Date = Now


    Public Shared WriteOnly Property SetLogLevel() As LevelLogType
        Set(value As LevelLogType)
            _LogLevel = value
        End Set
    End Property

    Public Shared Sub WriteError(Text As String)
        WriteLog(Text, LevelLogType.Minimal)
    End Sub

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

            _Buffer.Append(Now.ToString("s"))
            _Buffer.Append(":")
            _Buffer.Append(Now.ToString("fff"))
            _Buffer.Append(" [ID#")
            _Buffer.Append(System.Threading.Thread.CurrentThread.ManagedThreadId)
            _Buffer.Append("] >>> ")
            _Buffer.AppendLine(Text)

        End SyncLock

        Flush(False)
    End Sub

    Public Shared Sub Flush(forceFlush As Boolean)
        Dim DoFlush As Boolean = False

        SyncLock (_syncObject)
            DoFlush = (_LastWrite.AddSeconds(10) < Now)

            If (DoFlush Or forceFlush) And _Buffer IsNot Nothing AndAlso _Buffer.Length > 0 Then
                Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Log")

                If Not System.IO.Directory.Exists(PathLog) Then
                    System.IO.Directory.CreateDirectory(PathLog)
                End If
                Using t As New StreamWriter(PathLog & "\Log_" & Now.ToString("yyyyMMdd") & ".txt", True)
                    t.Write(_Buffer.ToString)
                End Using
                _Buffer = Nothing
                _LastWrite = Now
            End If
        End SyncLock
    End Sub



End Class