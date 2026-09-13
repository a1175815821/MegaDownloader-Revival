Imports System.IO

Namespace Stegano
    Public Class SteganoManager


        Public Function CheckPassword(Password As String) As String
            If String.IsNullOrEmpty(Password) Then Password = URLExtractor.ENCODE_PASSWORD
            Return Password
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
            Dim req As Net.HttpWebRequest = CType(Net.WebRequest.Create(url), Net.HttpWebRequest)
            req.Method = "GET"
            req.Timeout = 30000
            req.ReadWriteTimeout = 60000
            req.AllowAutoRedirect = True
            req.MaximumAutomaticRedirections = 5
            Using resp As Net.HttpWebResponse = CType(req.GetResponse(), Net.HttpWebResponse)
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

            Try
                If System.IO.File.Exists(Input) Then
                    ' Load file into memory first so the source file is not locked and
                    ' the backing stream stays open for the Image lifetime (FromStream requires it)
                    Dim fileBytes As Byte() = File.ReadAllBytes(Input)
                    backingStream = New MemoryStream(fileBytes)
                    img = Image.FromStream(backingStream)
                Else
                    ' From URL
                    Dim imgBytes As Byte() = DownloadImageBytes(Input)
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

            ' Retrieve data
            Using mem As New IO.MemoryStream
                Try
                    If System.IO.File.Exists(Input) Then
                        Using st As Stream = File.OpenRead(Input)
                            Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                                extractor.Extract(st)
                            End Using
                        End Using
                    Else
                        data = DownloadImageBytes(Input)
                        Using st As New MemoryStream(data)
                            Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                                extractor.Extract(st)
                            End Using
                        End Using
                    End If

                    data = mem.ToArray

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

