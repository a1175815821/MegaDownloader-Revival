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
    ''' Loads an XML document, falling back to the ".bak" sibling on failure.
    ''' transientFailure reports that at least one attempt failed with IOException
    ''' (sharing violation: antivirus scan / backup / sync client) rather than a
    ''' corrupt document. Callers must treat that as "keep the on-disk file as-is":
    ''' loading defaults and then saving would replace a perfectly good file with
    ''' defaults and rotate it into ".bak", destroying user data.
    ''' </summary>
    Public Shared Function TryLoadXml(ByVal sourcePath As String, ByRef document As XmlDocument, ByRef recoveredFromBackup As Boolean, ByRef transientFailure As Boolean) As Boolean
        recoveredFromBackup = False
        transientFailure = False
        document = Nothing
        Dim fullSource As String = Path.GetFullPath(sourcePath)

        If File.Exists(fullSource) Then
            Dim locked As Boolean = False
            For attempt As Integer = 1 To 3
                Try
                    Dim primary As New XmlDocument
                    primary.Load(fullSource)
                    document = primary
                    Return True
                Catch ex As IOException
                    ' 瞬时占用 ≠ 文件损坏:短重试能吃掉绝大多数杀软/同步盘扫描窗口。
                    locked = True
                    If attempt < 3 Then System.Threading.Thread.Sleep(100)
                Catch
                    ' 真正的 XML 损坏等:与占用无关,走 .bak 恢复。
                    locked = False
                    Exit For
                End Try
            Next
            If locked Then transientFailure = True
        End If

        Dim backupPath As String = fullSource & ".bak"
        If File.Exists(backupPath) Then
            Dim locked As Boolean = False
            For attempt As Integer = 1 To 3
                Try
                    Dim backup As New XmlDocument
                    backup.Load(backupPath)
                    document = backup
                    recoveredFromBackup = True
                    Return True
                Catch ex As IOException
                    locked = True
                    If attempt < 3 Then System.Threading.Thread.Sleep(100)
                Catch
                    locked = False
                    Exit For
                End Try
            Next
            If locked Then transientFailure = True
        End If

        Return False
    End Function
End Class
