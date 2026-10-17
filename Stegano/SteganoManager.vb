Imports System.IO

Namespace Stegano
    Public Class SteganoManager


        Public Function CheckPassword(Password As String) As String
            If String.IsNullOrEmpty(Password) Then Password = URLExtractor.ENCODE_PASSWORD
            Return Password
        End Function

        ' P1-3: 向导前置校验 —— 本地文件(含 file:// URI)必须真实存在,或必须是 http(s) 远程 URL。
        ' 旧校验 IsValidUri 对 Windows 路径恒真(Uri 把 "C:\..." 解析成 scheme=file),
        ' 不存在的路径一路放行,最后只得到 "未找到隐藏链接" 的误导提示。
        Public Shared Function IsValidImageSource(ByVal Input As String) As Boolean
            If String.IsNullOrWhiteSpace(Input) Then Return False
            Dim s As String = Input.Trim()
            Dim uri As Uri = Nothing
            If Uri.TryCreate(s, UriKind.Absolute, uri) Then
                If uri.IsFile Then Return IO.File.Exists(uri.LocalPath)
                Return uri.Scheme = Uri.UriSchemeHttp OrElse uri.Scheme = Uri.UriSchemeHttps
            End If
            Return IO.File.Exists(s)
        End Function

        ' P1-3: 归一化输入来源 —— 本地路径(含 file:// URI 与 Windows 路径)返回
        ' LocalPath,远程 http(s) 返回 URL,二者恰好其一非 Nothing。本地路径不存在时
        ' 抛出明确的 ApplicationException("Input image does not exist")。
        ' 旧代码让 WebRequest.Create 把 Windows 路径解析成 FileWebRequest 再 CType 成
        ' HttpWebRequest(InvalidCastException 直接冒给用户),或被裸 Catch 压成
        ' "未找到隐藏链接" 的误导提示。
        Private Shared Function NormalizeInput(ByVal Input As String, ByRef LocalPath As String) As String
            LocalPath = Nothing
            Dim s As String = If(Input, String.Empty).Trim()
            Dim uri As Uri = Nothing
            If Uri.TryCreate(s, UriKind.Absolute, uri) Then
                If uri.IsFile Then
                    LocalPath = uri.LocalPath
                ElseIf uri.Scheme = Uri.UriSchemeHttp OrElse uri.Scheme = Uri.UriSchemeHttps Then
                    Return uri.AbsoluteUri
                Else
                    ' 其余 scheme 一律按本地路径处理,由下面的存在性检查给提示
                    LocalPath = s
                End If
            Else
                LocalPath = s
            End If
            If Not IO.File.Exists(LocalPath) Then
                Throw New ApplicationException(Language.GetText("Input image does not exist"))
            End If
            Return Nothing
        End Function

        ' B3:远端图片下载上限 64MB + 30s 超时。WebClient.DownloadData 无超时无上限,
        ' 误填/恶意的大文件 URL 直接整块进内存 OOM。超限抛 ApplicationException,
        ' 向导已有友好分支接住弹窗(不记为崩溃);超时/404 等 WebException 原样上抛,记日志。
        Private Const MaxRemoteImageBytes As Long = 64L * 1024L * 1024L

        Private Shared Function DownloadImageBytes(ByVal url As String) As Byte()
            ' file:// URI 不走 HTTP 栈(FileWebRequest 不支持 Timeout 等属性,此前 WebClient 兼容):
            ' 直接读本地文件,同样执行 64MB 上限。
            If url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) Then
                Dim localBytes As Byte() = IO.File.ReadAllBytes(New Uri(url).LocalPath)
                If localBytes.Length > MaxRemoteImageBytes Then
                    Throw New ApplicationException("Remote image is too large (over 64 MB).")
                End If
                Return localBytes
            End If
            ' P1-3: 不再 CType 强转 HttpWebRequest —— Windows 路径会被 WebRequest.Create
            ' 解析成 FileWebRequest,强转抛 InvalidCastException 原样冒给用户。
            ' HTTP 属性只在真正是 HttpWebRequest 时设置。
            Dim req As Net.WebRequest = Net.WebRequest.Create(url)
            Dim httpReq As Net.HttpWebRequest = TryCast(req, Net.HttpWebRequest)
            If httpReq IsNot Nothing Then
                httpReq.Method = "GET"
                httpReq.Timeout = 30000
                httpReq.ReadWriteTimeout = 60000
                httpReq.AllowAutoRedirect = True
                httpReq.MaximumAutomaticRedirections = 5
            End If
            ' issue #6: transport failures (no internet, DNS, firewall/VPN/proxy,
            ' timeout) become an actionable ApplicationException — the wizard's
            ' ApplicationException branch shows it as a warning. API-level
            ' failures (404 etc.) keep their original message.
            Try
                Using resp As Net.WebResponse = req.GetResponse()
                    If resp.ContentLength > MaxRemoteImageBytes Then
                        Throw New ApplicationException("Remote image is too large (over 64 MB).")
                    End If
                    Using src As IO.Stream = resp.GetResponseStream()
                        Using mem As New IO.MemoryStream()
                            Dim buf(81919) As Byte
                            Dim total As Long = 0
                            While True
                                Dim n As Integer = src.Read(buf, 0, buf.Length)
                                If n <= 0 Then Exit While
                                total += CLng(n)
                                If total > MaxRemoteImageBytes Then
                                    Throw New ApplicationException("Remote image is too large (over 64 MB).")
                                End If
                                mem.Write(buf, 0, n)
                            End While
                            Return mem.ToArray()
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                If NetworkErrors.IsTransportError(ex) Then
                    Log.WriteError("DownloadImageBytes: transport failure for [" & url & "] - " & Log.SafeException(ex))
                    Throw New ApplicationException(NetworkErrors.DescribeTransportError(ex), ex)
                End If
                Throw
            End Try
        End Function

        Public Function CreateImage(Text As String, Input As String, Output As String, Quality As Integer, Password As String) As Boolean

            ' Cipher data
            Password = CheckPassword(Password)

            Dim AES As New Cryptography.AES
            Dim encryptedText As String = AES.Encrypt(Text, Password)

            Dim data As Byte() = System.Convert.FromBase64String(encryptedText)

            Dim img As Image
            Dim backingStream As MemoryStream = Nothing
            ' 编码结果(在 Try 内生成,Try 外使用,故声明在块外)
            Dim encodedBytes As Byte() = Nothing

            ' P1-3: 统一解析来源(本地路径 / file:// / 远程 URL),不存在的本地路径
            ' 在此给出 "Input image does not exist",不再走到 DownloadImageBytes 的
            ' WebRequest 强转路径。
            Dim localPath As String = Nothing
            Dim remoteUrl As String = NormalizeInput(Input, localPath)

            Try
                If localPath IsNot Nothing Then
                    ' R-P4-1: a direct local path used to bypass the 64 MB cap that the
                    ' URL / file:// branches enforce — a huge image went fully into
                    ' memory (ReadAllBytes) and then through JPEG decode + re-encode,
                    ' an easy OOM. Same limit, same shape as DownloadImageBytes.
                    Dim fi As New IO.FileInfo(localPath)
                    If fi.Length > MaxRemoteImageBytes Then
                        Throw New ApplicationException("Image file is too large (over 64 MB).")
                    End If
                    ' Load file into memory first so the source file is not locked and
                    ' the backing stream stays open for the Image lifetime (FromStream requires it)
                    Dim fileBytes As Byte() = File.ReadAllBytes(localPath)
                    backingStream = New MemoryStream(fileBytes)
                    img = Image.FromStream(backingStream)
                Else
                    ' From URL
                    Dim imgBytes As Byte() = DownloadImageBytes(remoteUrl)
                    backingStream = New MemoryStream(imgBytes)
                    img = Image.FromStream(backingStream)
                End If

                ' 先在内存中编码与校验,全部通过后才落盘:
                ' 1) 容量不足时不再留下写坏的 .jpg(此前先写盘后检查,用户目录残留截断文件)
                ' 2) WriteAllBytes 截断覆盖,不会像 OpenWrite(OpenOrCreate)那样保留旧文件尾部字节
                Using img
                    Using memOut As New IO.MemoryStream()
                        Using jpg As New F5.James.JpegEncoder(img, memOut, Nothing, Quality)

                            jpg.Compress(New IO.MemoryStream(data), System.Text.Encoding.Unicode.GetBytes(Password))

                            Dim MaxSize = jpg.MaxSizeToEmbed * 0.8 ' For security we consider 80% of capacity

                            Dim fileSize As Long = data.Length
                            Dim K_Used As Integer = jpg.K_Used

                            If MaxSize < fileSize Then
                                Throw New ApplicationException(Language.GetText("Warning: image too small, maybe the data is corrupted"))
                            End If

                        End Using
                        encodedBytes = memOut.ToArray()
                    End Using
                End Using
            Finally
                If backingStream IsNot Nothing Then backingStream.Dispose()
            End Try

        ' Check the encoded data (in memory)
        Using mem As New IO.MemoryStream
            Using fsCheck As New IO.MemoryStream(encodedBytes)
                Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                    extractor.Extract(fsCheck)
                End Using
            End Using

            data = mem.ToArray

            Try
                Dim CipheredText As String = AES.Decrypt(System.Convert.ToBase64String(data), Password)
                If CipheredText <> Text Then
                    Throw New ApplicationException
                End If
            Catch ex As Exception
                Throw New ApplicationException(Language.GetText("Warning: data verification failed. The output file has not been created. This may happen if the image is too small, try with a bigger image"))
            End Try

        End Using

        ' 校验通过才写盘(截断覆盖)
        IO.File.WriteAllBytes(Output, encodedBytes)

        Return True
    End Function

        Public Function LoadImages(Input As String, Password As String, ByRef HiddenText As String) As Boolean
            Dim data As Byte()
            Dim AES As New Cryptography.AES

            Password = CheckPassword(Password)

            ' P1-3: 先解析来源(不存在的本地路径抛 "Input image does not exist"),
            ' 取数据/提取阶段的 ApplicationException(如网络失败)原样上抛,
            ' 只有"图片里提取不到隐藏数据"才压成 False —— 旧代码裸 Catch 把
            ' 路径写错、网络失败、无隐藏数据混成同一句误导提示。
            Dim localPath As String = Nothing
            Dim remoteUrl As String = NormalizeInput(Input, localPath)

            ' Retrieve data
            Using mem As New IO.MemoryStream
                Try
                    If localPath IsNot Nothing Then
                        Using st As Stream = File.OpenRead(localPath)
                            Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                                extractor.Extract(st)
                            End Using
                        End Using
                    Else
                        data = DownloadImageBytes(remoteUrl)
                        Using st As New MemoryStream(data)
                            Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                                extractor.Extract(st)
                            End Using
                        End Using
                    End If

                    data = mem.ToArray

                Catch ex As ApplicationException
                    ' 可定位的输入问题(路径不存在/网络失败等),向导按警告显示具体原因
                    Throw
                Catch ex As Exception
                    Return False
                End Try
            End Using

            ' Decrypt data
            Try
                Dim CipheredText As String = AES.Decrypt(System.Convert.ToBase64String(data), Password)
                HiddenText = CipheredText
            Catch ex As Exception
                Return False
            End Try

            Return True
        End Function



    End Class


End Namespace

