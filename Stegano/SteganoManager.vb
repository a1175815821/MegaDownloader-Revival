Imports System.IO

Namespace Stegano
    Public Class SteganoManager


        Public Function CheckPassword(Password As String) As String
            If String.IsNullOrEmpty(Password) Then Password = URLExtractor.ENCODE_PASSWORD
            Return Password
        End Function

        Public Function CreateImage(Text As String, Input As String, Output As String, Quality As Integer, Password As String) As Boolean

            ' Cipher data
            Password = CheckPassword(Password)

            Dim AES As New Cryptography.AES
            Dim encryptedText As String = AES.Encrypt(Text, Password)

            Dim data As Byte() = System.Convert.FromBase64String(encryptedText)

            Dim img As Image

            If System.IO.File.Exists(Input) Then
                ' From file - use FileStream so the file is not locked after loading
                Dim fs As New IO.FileStream(Input, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
                Try
                    img = Image.FromStream(fs)
                Finally
                    fs.Close()
                End Try
            Else
                ' From URL
                Using webClient As New Net.WebClient()
                    Dim imgBytes = webClient.DownloadData(Input)
                    Dim mem As New MemoryStream(imgBytes)
                    img = Image.FromStream(mem) ' mem 必须在 img 使用期间保持打开
                End Using
            End If


            ' Save it into an image
            Using img
                Dim outStream As IO.FileStream = IO.File.OpenWrite(Output)
                Try
                    Using jpg As New F5.James.JpegEncoder(img, outStream, Nothing, Quality)

                        jpg.Compress(New IO.MemoryStream(data), System.Text.Encoding.Unicode.GetBytes(Password))

                        Dim MaxSize = jpg.MaxSizeToEmbed * 0.8 ' For security we consider 80% of capacity

                        Dim fileSize As Long = data.Length
                        Dim K_Used As Integer = jpg.K_Used

                        If MaxSize < fileSize Then
                            Throw New ApplicationException(Language.GetText("Warning: image too small, maybe the data is corrupted"))
                        End If

                    End Using
                Finally
                    outStream.Close()
                End Try
            End Using


            ' Check the file
            Using mem As New IO.MemoryStream

                Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                    extractor.Extract(IO.File.OpenRead(Output))
                End Using

                data = mem.ToArray

                Try
                    Dim CipheredText As String = AES.Decrypt(System.Convert.ToBase64String(data), Password)
                    If CipheredText <> Text Then
                        Throw New ApplicationException
                    End If
                Catch ex As Exception
                    Throw New ApplicationException(Language.GetText("Warning: The image output was created but the data verification failed. This may happen if the image is too small, try with a bigger image"))
                End Try


            End Using


            Return True
        End Function

        Public Function LoadImages(Input As String, Password As String, ByRef HiddenText As String) As Boolean
            Dim data As Byte()
            Dim AES As New Cryptography.AES
            Dim st As IO.Stream

            Password = CheckPassword(Password)

            If System.IO.File.Exists(Input) Then
                ' From file
                st = IO.File.OpenRead(Input)
            Else
                ' From URL
                Using webClient As New Net.WebClient()
                    data = webClient.DownloadData(Input)
                    st = New MemoryStream(data)
                End Using
            End If

            ' Retrieve data
            Using mem As New IO.MemoryStream
                Try
                    Using extractor As New F5.JpegExtract(mem, System.Text.Encoding.Unicode.GetBytes(Password))
                        extractor.Extract(st)
                    End Using

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

