Imports System.Collections.Generic
Imports System.IO
Imports System.Threading

''' <summary>Read-only metadata inspection; never creates a download or local file.</summary>
Public NotInheritable Class BatchLinkPreflight
    Public Enum LinkState
        Ready
        AlreadyQueued
        Duplicate
        Invalid
        Unavailable
    End Enum

    Public NotInheritable Class Entry
        Public Source As URLProcessor.FileURL
        Public OriginalUrl As String
        Public Name As String
        Public SizeBytes As Long?
        Public State As LinkState
        Public Detail As String
        Public Selected As Boolean
        Private categoryName As String
        Private categorySource As String

        Public ReadOnly Property CanAdd As Boolean
            Get
                Return Source IsNot Nothing AndAlso (State = LinkState.Ready OrElse State = LinkState.Unavailable)
            End Get
        End Property

        Public ReadOnly Property Category As String
            Get
                If categoryName IsNot Nothing AndAlso String.Equals(categorySource, Name, StringComparison.Ordinal) Then Return categoryName
                categorySource = Name
                Dim ext As String = Path.GetExtension(If(Name, "")).ToLowerInvariant()
                Select Case ext
                    Case ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".wmv", ".ts" : categoryName = "Video"
                    Case ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".opus" : categoryName = "Audio"
                    Case ".zip", ".rar", ".7z", ".gz", ".bz2", ".tar", ".xz" : categoryName = "Archives"
                    Case ".pdf", ".txt", ".md", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".epub" : categoryName = "Documents"
                    Case Else : categoryName = "Other"
                End Select
                Return categoryName
            End Get
        End Property
    End Class

    Public Shared Function Identity(fileId As String, fileKey As String) As String
        If String.IsNullOrEmpty(fileId) OrElse String.IsNullOrEmpty(fileKey) Then Return Nothing
        Return fileId & ChrW(0) & fileKey.TrimEnd("="c)
    End Function

    Public Shared Function Identity(source As URLProcessor.FileURL) As String
        If source Is Nothing OrElse String.IsNullOrEmpty(source.URL) Then Return Nothing
        If source.PreflightIdentity IsNot Nothing AndAlso String.Equals(source.PreflightIdentityUrl, source.URL, StringComparison.Ordinal) Then Return source.PreflightIdentity
        Dim url As String = source.URL.Replace(Fichero.HIDDEN_LINK, "")
        Dim value As String = Identity(Fichero.ExtraerFileID(url), Fichero.ExtraerFileKey(url))
        source.PreflightIdentity = value
        source.PreflightIdentityUrl = source.URL
        Return value
    End Function

    ''' <summary>Injectable metadata reader allows offline, deterministic regression tests.</summary>
    Public Shared Function InspectFiles(files As IEnumerable(Of URLProcessor.FileURL), queued As ISet(Of String),
                                        reader As Func(Of URLProcessor.FileURL, CancellationToken, Conexion.InformacionFichero),
                                        ct As CancellationToken, Optional progress As IProgress(Of Integer) = Nothing) As List(Of Entry)
        Dim result As New List(Of Entry)()
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
        For Each source As URLProcessor.FileURL In files
            ct.ThrowIfCancellationRequested()
            Dim entry As New Entry With {.Source = source, .State = LinkState.Unavailable, .Name = "#" & (result.Count + 1).ToString()}
            result.Add(entry)
            Try
                If source Is Nothing Then Throw New ArgumentException("Missing link")
                If Not String.IsNullOrEmpty(source.DisplayName) Then entry.Name = source.DisplayName
                entry.SizeBytes = source.SizeBytes
                Dim identityValue As String = Identity(source)
                If identityValue IsNot Nothing AndAlso queued IsNot Nothing AndAlso queued.Contains(identityValue) Then
                    entry.State = LinkState.AlreadyQueued
                ElseIf identityValue IsNot Nothing AndAlso Not seen.Add(identityValue) Then
                    entry.State = LinkState.Duplicate
                Else
                    Dim info As Conexion.InformacionFichero = Nothing
                    ' Folder listings already contain decrypted names and sizes. Do not
                    ' issue another API request for every node of a large folder.
                    If Not String.IsNullOrEmpty(source.DisplayName) AndAlso source.SizeBytes.HasValue Then
                        entry.State = LinkState.Ready
                    Else
                        info = reader(source, ct)
                        ct.ThrowIfCancellationRequested()
                        ApplyInformation(entry, info)
                    End If
                End If
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                entry.State = LinkState.Unavailable
                entry.Detail = DownloadAdvice.FirstLine(Log.Redact(ex.Message))
            End Try
            entry.Selected = entry.State = LinkState.Ready
            If progress IsNot Nothing Then progress.Report(result.Count)
        Next
        Dim originals As New Dictionary(Of String, Entry)(StringComparer.Ordinal)
        For Each entry As Entry In result
            Dim key As String = Identity(entry.Source)
            If key Is Nothing Then Continue For
            If entry.State = LinkState.Duplicate AndAlso originals.ContainsKey(key) Then
                entry.Name = originals(key).Name
                entry.SizeBytes = originals(key).SizeBytes
            ElseIf Not originals.ContainsKey(key) Then
                originals.Add(key, entry)
            End If
        Next
        Return result
    End Function

    Private Shared Sub ApplyInformation(entry As Entry, info As Conexion.InformacionFichero)
        If info IsNot Nothing AndAlso info.Err = Conexion.TipoError.SinErrores AndAlso Not String.IsNullOrEmpty(info.Nombre) Then
            entry.Name = PathGuard.SanitizeFileName(info.Nombre, "file")
            entry.SizeBytes = Math.Max(0L, info.Tamano)
            entry.Source.DisplayName = entry.Name
            entry.Source.SizeBytes = entry.SizeBytes
            entry.State = LinkState.Ready
        Else
            entry.Detail = If(info Is Nothing, "", DownloadAdvice.FirstLine(Log.Redact(info.Errtxt)))
            If info IsNot Nothing AndAlso DownloadAdvice.Classify(info.Errtxt, info.Err = Conexion.TipoError.QuotaExceeded, False) = DownloadAdvice.FailureKind.Permanent Then
                entry.State = LinkState.Invalid
            Else
                entry.State = LinkState.Unavailable
            End If
        End If
    End Sub

    Public Shared Function Resolve(urls As IEnumerable(Of String), config As Configuracion, queued As ISet(Of String),
                                    ct As CancellationToken, Optional progress As IProgress(Of Integer) = Nothing,
                                    Optional detailedProgress As IProgress(Of PreflightProgress) = Nothing) As List(Of Entry)
        Dim files As New List(Of URLProcessor.FileURL)()
        Dim failures As New List(Of Entry)()
        For Each url As String In urls
            ct.ThrowIfCancellationRequested()
            Try
                If detailedProgress IsNot Nothing Then detailedProgress.Report(New PreflightProgress(PreflightProgress.Phase.Reading))
                Dim folderProgress As IProgress(Of Integer) = progress
                If detailedProgress IsNot Nothing Then
                    folderProgress = New PreflightProgress.ItemReporter(Sub(count)
                                                                           If progress IsNot Nothing Then progress.Report(count)
                                                                           detailedProgress.Report(New PreflightProgress(PreflightProgress.Phase.Reading, processed:=count))
                                                                       End Sub)
                End If
                files.AddRange(URLProcessor.ProcessURLs(New List(Of String) From {url}, config, folderProgress, ct))
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                failures.Add(New Entry With {.OriginalUrl = url, .Name = "#" & (files.Count + failures.Count + 1).ToString(),
                    .State = If(DownloadAdvice.Classify(ex.Message, False, False) = DownloadAdvice.FailureKind.Permanent, LinkState.Invalid, LinkState.Unavailable),
                    .Detail = DownloadAdvice.FirstLine(Log.Redact(ex.Message))})
            End Try
        Next
        If detailedProgress IsNot Nothing Then detailedProgress.Report(New PreflightProgress(PreflightProgress.Phase.Checking, itemCount:=files.Count))
        Dim fileProgress As IProgress(Of Integer) = progress
        If detailedProgress IsNot Nothing Then
            fileProgress = New PreflightProgress.ItemReporter(Sub(count)
                                                                 If progress IsNot Nothing Then progress.Report(count)
                                                                 If count Mod 100 = 0 OrElse count = files.Count Then detailedProgress.Report(New PreflightProgress(PreflightProgress.Phase.Checking, processed:=count, itemCount:=files.Count))
                                                             End Sub)
        End If
        Dim results As List(Of Entry) = InspectFiles(files, queued,
            Function(source, token)
                If MegaQuotaManager.IsQuarantined() Then Return New Conexion.InformacionFichero With {.Err = Conexion.TipoError.QuotaExceeded, .Errtxt = Language.GetText("Advice_Quota")}
                Dim url As String = source.URL.Replace(Fichero.HIDDEN_LINK, "")
                Return Conexion.ObtenerInformacionFichero(config, Fichero.ExtraerFileID(url), Fichero.ExtraerFileKey(url), False, token)
            End Function, ct, fileProgress)
        results.AddRange(failures)
        Return results
    End Function

    Public Shared Function AvailableSpace(destination As String) As Long?
        Try
            Dim root As String = Path.GetPathRoot(Path.GetFullPath(destination))
            ' Do not block metadata inspection on an unavailable network share.
            If root.StartsWith("\\", StringComparison.Ordinal) Then Return Nothing
            Dim drive As New DriveInfo(root)
            If drive.IsReady Then Return drive.AvailableFreeSpace
        Catch ex As IOException
        Catch ex As ArgumentException
        Catch ex As UnauthorizedAccessException
        Catch ex As System.Security.SecurityException
        End Try
        Return Nothing
    End Function
End Class
