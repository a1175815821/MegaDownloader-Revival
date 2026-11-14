Public Class MD5Utils
    ' specify the path to a file and this routine will calculate your hash
    Public Shared Function MD5CalcFile(ByVal filepath As String) As String

        ' open file (as read-only)
        'Using reader As New System.IO.FileStream(filepath, IO.FileMode.Open, IO.FileAccess.Read)
        Using reader As New IO.BufferedStream(IO.File.OpenRead(filepath), 1200000) ' http://stackoverflow.com/questions/1177607/what-is-the-fastest-way-to-create-a-checksum-for-large-files-in-c-sharp/1177744#1177744
            Using md5 As New System.Security.Cryptography.MD5CryptoServiceProvider

                ' hash contents of this stream
                Dim hash() As Byte = md5.ComputeHash(reader)

                ' return formatted hash
                Return ByteArrayToString(hash)

            End Using
        End Using

    End Function

    ' utility function to convert a byte array into a hex string
    Private Shared Function ByteArrayToString(ByVal arrInput() As Byte) As String

        Dim sb As New System.Text.StringBuilder(arrInput.Length * 2)

        For i As Integer = 0 To arrInput.Length - 1
            sb.Append(arrInput(i).ToString("X2"))
        Next

        Return sb.ToString().ToLower

    End Function
End Class
