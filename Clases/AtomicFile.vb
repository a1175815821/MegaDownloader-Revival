Imports System.IO
Imports System.Text
Imports System.Xml

Friend NotInheritable Class AtomicFile
    Private Sub New()
    End Sub

    Public Shared Sub SaveXml(ByVal document As XmlDocument, ByVal destinationPath As String, Optional preserveBackup As Boolean = False)
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
                File.Replace(tempPath, fullDestination, If(preserveBackup, Nothing, fullDestination & ".bak"), True)
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
        Dim blockedSource As String = Nothing
        Return TryLoadXmlWithRecoveryInfo(sourcePath, document, recoveredFromBackup, transientFailure, blockedSource, attempts, retryDelayMs)
    End Function

    Friend Shared Function TryLoadXmlWithRecoveryInfo(ByVal sourcePath As String, ByRef document As XmlDocument, ByRef recoveredFromBackup As Boolean, ByRef transientFailure As Boolean, ByRef blockedSource As String, ByVal attempts As Integer, ByVal retryDelayMs As Integer) As Boolean
        Return TryLoadXmlCancelable(sourcePath, document, recoveredFromBackup, transientFailure, blockedSource, attempts, retryDelayMs, Threading.CancellationToken.None)
    End Function

    Friend Shared Function TryLoadXmlCancelable(ByVal sourcePath As String, ByRef document As XmlDocument, ByRef recoveredFromBackup As Boolean, ByRef transientFailure As Boolean, ByRef blockedSource As String, ByVal attempts As Integer, ByVal retryDelayMs As Integer, cancellationToken As Threading.CancellationToken) As Boolean
        recoveredFromBackup = False
        transientFailure = False
        blockedSource = Nothing
        Dim fullSource As String = Path.GetFullPath(sourcePath)
        Dim state As XmlReadState = ReadXml(fullSource, document, attempts, retryDelayMs, cancellationToken)
        If state = XmlReadState.Loaded Then Return True
        If state = XmlReadState.Blocked Then
            blockedSource = fullSource
        Else
            Dim backupPath As String = fullSource & ".bak"
            state = ReadXml(backupPath, document, attempts, retryDelayMs, cancellationToken)
            If state = XmlReadState.Loaded Then
                recoveredFromBackup = True
                Return True
            End If
            If state = XmlReadState.Blocked Then blockedSource = backupPath
        End If
        transientFailure = (state = XmlReadState.Blocked)
        Return False
    End Function

    Friend Enum XmlReadState
        Missing
        Loaded
        Invalid
        Blocked
    End Enum

    Friend Shared Function ReadXml(source As String, ByRef document As XmlDocument, attempts As Integer,
                                   delayMs As Integer, cancellationToken As Threading.CancellationToken) As XmlReadState
        document = Nothing
        For attempt As Integer = 1 To Math.Max(1, attempts)
            cancellationToken.ThrowIfCancellationRequested()
            Try
                Using stream As New FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read)
                    Dim xml As New XmlDocument With {.XmlResolver = Nothing}
                    xml.Load(stream)
                    document = xml
                    Return XmlReadState.Loaded
                End Using
            Catch ex As FileNotFoundException
                Return XmlReadState.Missing
            Catch ex As DirectoryNotFoundException
                Return XmlReadState.Missing
            Catch ex As XmlException
                Return XmlReadState.Invalid
            Catch ex As UnauthorizedAccessException
                Return XmlReadState.Blocked
            Catch ex As IOException
                If attempt < attempts AndAlso delayMs > 0 Then WaitForRetry(delayMs, cancellationToken)
            End Try
        Next
        Return XmlReadState.Blocked
    End Function

    Private Shared Sub WaitForRetry(delayMs As Integer, cancellationToken As Threading.CancellationToken)
        If cancellationToken.CanBeCanceled Then
            If cancellationToken.WaitHandle.WaitOne(delayMs) Then cancellationToken.ThrowIfCancellationRequested()
        Else
            Threading.Thread.Sleep(delayMs)
        End If
    End Sub

    Friend Shared Function TryReloadAfterTransientFailure(ByVal sourcePath As String, ByVal blockedSource As String, ByRef document As XmlDocument) As Boolean
        Dim fullSource As String = Path.GetFullPath(sourcePath)
        If String.Equals(blockedSource, fullSource & ".bak", StringComparison.OrdinalIgnoreCase) Then
            ' A backup lock was reached only after a missing/corrupt primary. Retry
            ' the normal preference order, still refusing fallback from a locked primary.
            Dim recovered As Boolean = False
            Dim transientFailure As Boolean = False
            Return TryLoadXmlWithRetries(fullSource, document, recovered, transientFailure, 1, 0)
        End If
        Return TryReloadPrimary(fullSource, document)
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
        Return ReadXml(Path.GetFullPath(sourcePath), document, 1, 0, Threading.CancellationToken.None) = XmlReadState.Loaded
    End Function
End Class
