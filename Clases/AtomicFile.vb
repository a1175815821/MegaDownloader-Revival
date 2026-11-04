Imports System.IO
Imports System.Text
Imports System.Xml

Friend NotInheritable Class AtomicFile
    Private Sub New()
    End Sub

    Public Shared Sub SaveXml(ByVal document As XmlDocument, ByVal destinationPath As String)
        If document Is Nothing Then Throw New ArgumentNullException("document")
        If String.IsNullOrWhiteSpace(destinationPath) Then Throw New ArgumentException("A destination path is required.", "destinationPath")

        Dim fullDestination As String = Path.GetFullPath(destinationPath)
        Dim directory As String = Path.GetDirectoryName(fullDestination)
        If String.IsNullOrEmpty(directory) Then Throw New InvalidOperationException("The destination directory could not be determined.")
        IO.Directory.CreateDirectory(directory)

        Dim tempPath As String = Path.Combine(directory, "." & Path.GetFileName(fullDestination) & "." & Guid.NewGuid().ToString("N") & ".tmp")
        Try
            Using stream As New FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)
                Dim settings As New XmlWriterSettings With {
                    .Encoding = New UTF8Encoding(False),
                    .Indent = False,
                    .CloseOutput = False
                }
                Using writer As XmlWriter = XmlWriter.Create(stream, settings)
                    document.Save(writer)
                    writer.Flush()
                End Using
                stream.Flush(True)
            End Using

            If File.Exists(fullDestination) Then
                File.Replace(tempPath, fullDestination, fullDestination & ".bak", True)
            Else
                File.Move(tempPath, fullDestination)
            End If
        Finally
            If File.Exists(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch
                End Try
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Loads an XML document, using ".bak" for a missing or corrupt primary.
    ''' A locked primary is left untouched instead of loading an older backup.
    ''' transientFailure reports that reading failed with IOException
    ''' (sharing violation: antivirus scan / backup / sync client) rather than a
    ''' corrupt document. Callers must treat that as "keep the on-disk file as-is":
    ''' loading defaults and then saving would replace a perfectly good file with
    ''' defaults and rotate it into ".bak", destroying user data.
    ''' </summary>
    Public Shared Function TryLoadXml(ByVal sourcePath As String, ByRef document As XmlDocument, ByRef recoveredFromBackup As Boolean, ByRef transientFailure As Boolean) As Boolean
        ' Original short retry window, kept for callers and regression tests.
        ' The name is deliberately unique: tests resolve "TryLoadXml" by name via reflection.
        Return TryLoadXmlWithRetries(sourcePath, document, recoveredFromBackup, transientFailure, 3, 100)
    End Function

    ''' <summary>Startup retry window (20 x 150ms = 3s). Antivirus / sync clients routinely
    ''' hold a config file for less than a second; waiting here avoids entering the degraded
    ''' "defaults + write-protected" state at all, which is the real B1/B2 root cause.</summary>
    Public Const StartupAttempts As Integer = 20
    Public Const StartupRetryDelayMs As Integer = 150

    Friend Shared Function TryLoadXmlWithRetries(ByVal sourcePath As String, ByRef document As XmlDocument, ByRef recoveredFromBackup As Boolean, ByRef transientFailure As Boolean, ByVal attempts As Integer, ByVal retryDelayMs As Integer) As Boolean
        recoveredFromBackup = False
        transientFailure = False
        document = Nothing
        Dim fullSource As String = Path.GetFullPath(sourcePath)
        If attempts < 1 Then attempts = 1
        If retryDelayMs < 0 Then retryDelayMs = 0

        If File.Exists(fullSource) Then
            Dim locked As Boolean = False
            For attempt As Integer = 1 To attempts
                Try
                    Dim primary As New XmlDocument
                    primary.Load(fullSource)
                    document = primary
                    Return True
                Catch ex As IOException
                    ' 瞬时占用 ≠ 文件损坏:重试窗口内吃掉杀软/同步盘扫描。
                    locked = True
                    If attempt < attempts AndAlso retryDelayMs > 0 Then System.Threading.Thread.Sleep(retryDelayMs)
                Catch
                    ' 真正的 XML 损坏等:与占用无关,走 .bak 恢复。
                    locked = False
                    Exit For
                End Try
            Next
            If locked Then
                ' A locked primary is not corrupt. An older backup must not become
                ' the writable state that replaces it when the lock is released.
                transientFailure = True
                Return False
            End If
        End If

        Dim backupPath As String = fullSource & ".bak"
        If File.Exists(backupPath) Then
            Dim locked As Boolean = False
            For attempt As Integer = 1 To attempts
                Try
                    Dim backup As New XmlDocument
                    backup.Load(backupPath)
                    document = backup
                    recoveredFromBackup = True
                    Return True
                Catch ex As IOException
                    locked = True
                    If attempt < attempts AndAlso retryDelayMs > 0 Then System.Threading.Thread.Sleep(retryDelayMs)
                Catch
                    locked = False
                    Exit For
                End Try
            Next
            If locked Then transientFailure = True
        End If

        Return False
    End Function

    ''' <summary>True when the file can be opened with an exclusive read share, i.e. the
    ''' process that held it during startup has released it.</summary>
    Public Shared Function CanReadExclusive(ByVal sourcePath As String) As Boolean
        Try
            Using stream As New FileStream(Path.GetFullPath(sourcePath), FileMode.Open, FileAccess.Read, FileShare.None)
            End Using
            Return True
        Catch ex As IOException
            Return False
        Catch ex As UnauthorizedAccessException
            Return False
        End Try
    End Function

    ''' <summary>Re-reads ONLY the primary file after a transient lock. Never falls back to
    ''' the .bak: a stale backup must not become the recovered writable state.</summary>
    Public Shared Function TryReloadPrimary(ByVal sourcePath As String, ByRef document As XmlDocument) As Boolean
        document = Nothing
        Dim fullSource As String = Path.GetFullPath(sourcePath)
        If Not File.Exists(fullSource) Then Return False
        Try
            Dim primary As New XmlDocument
            primary.Load(fullSource)
            document = primary
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function
End Class
