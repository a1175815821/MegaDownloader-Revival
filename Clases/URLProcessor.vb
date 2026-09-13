Public Class URLProcessor

    Public Class FileURL

        Public Sub New(pURL As String, pPath As String)
            Me.URL = pURL
            Me.Path = pPath
        End Sub

        Public URL As String
        Public Path As String

    End Class

    Public Shared Function ProcessURLs(ByVal URLs As Generic.List(Of String), ByRef Config As Configuracion, _
                                       Optional ByVal progress As IProgress(Of Integer) = Nothing, _
                                       Optional ByVal ct As System.Threading.CancellationToken = Nothing) As Generic.List(Of FileURL)

        ' Convertimos los links de MegaFolder a links individuales
        ' B1-⑤:per-URL 隔离——单个文件夹/ELC 失败(过期 ELC、服务端报错、断网)只跳过该条,
        ' 不再 Throw 中断整批(此前同次粘贴的好链接被一条坏 ELC 团灭,须人肉二分)。
        ' 全部失败时仍抛首个错误,保持单链调用方(CreateStreamingLink/错误弹窗)原有语义。
        Dim URLs3 As New Generic.List(Of FileURL)
        Dim firstError As Exception = Nothing
        For Each URL As String In URLs
            If ct.IsCancellationRequested Then ct.ThrowIfCancellationRequested()
            ' 同一条 URL 内部分成功后又失败(如 ELC 内第 2 个文件夹已挂),回滚该条已加入项,
            ' 做到"按条全有或全无",不留半截 ELC。
            Dim before As Integer = URLs3.Count
            Try
                If URLExtractor.IsMegaFolder(URL) Then
                    Dim FolderID As String = URLExtractor.ExtraerFileID(URL)
                    Dim FolderKey As String = URLExtractor.ExtraerFileKey(URL)
                    Dim SubFolderID As String = URLExtractor.ExtraerSubFolderID(URL)
                    Dim SubFileID As String = URLExtractor.ExtraerSubFileID(URL)
                    For Each FileURL In MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey, SubFolderID, SubFileID, progress, ct)
                        URLs3.Add(FileURL)
                    Next
                ElseIf URLExtractor.IsELC(URL) Then
                    Dim ELC_exc As Exception = Nothing
                    Dim decoded As Generic.List(Of String) = Nothing
                    Try
                        decoded = ServerEncoderLinkHelper.ServerDecode(URL, Config, ELC_exc)
                    Catch ex As Exception
                        ELC_exc = ex
                    End Try
                    If decoded IsNot Nothing Then
                        For Each FileURL As String In decoded
                            If URLExtractor.IsMegaFolder(FileURL) Then
                                Dim FolderID As String = URLExtractor.ExtraerFileID(FileURL)
                                Dim FolderKey As String = URLExtractor.ExtraerFileKey(FileURL)
                                Dim SubFolderID As String = URLExtractor.ExtraerSubFolderID(FileURL)
                                Dim SubFileID As String = URLExtractor.ExtraerSubFileID(FileURL)
                                For Each FileURL2 In MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey, SubFolderID, SubFileID, progress, ct)
                                    URLs3.Add(New FileURL(Fichero.HIDDEN_LINK & FileURL2.URL, FileURL2.Path))
                                Next
                            Else
                                URLs3.Add(New FileURL(Fichero.HIDDEN_LINK & FileURL, ""))
                            End If
                        Next
                    End If
                    If ELC_exc IsNot Nothing Then
                        Throw ELC_exc
                    End If
                Else
                    URLs3.Add(New FileURL(URL, ""))
                End If
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                While URLs3.Count > before
                    URLs3.RemoveAt(URLs3.Count - 1)
                End While
                Log.WriteWarning("ProcessURLs: skipping failed link, keeping the rest of the batch: " & Log.Redact(URL) & " - " & Log.SafeException(ex))
                If firstError Is Nothing Then firstError = ex
            End Try
        Next
        If URLs3.Count = 0 AndAlso firstError IsNot Nothing Then
            Throw firstError
        End If
        Return URLs3
    End Function

End Class
