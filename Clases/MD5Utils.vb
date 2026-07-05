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


    Public Shared Function MD5CalcString(ByVal str As String) As String
        str &= "" ' Evitamos nothing

        ' Create a new instance of the MD5CryptoServiceProvider object.
        Dim md5Hasher As Security.Cryptography.MD5 = Security.Cryptography.MD5.Create()

        ' Convert the input string to a byte array and compute the hash.
        Dim data As Byte() = md5Hasher.ComputeHash(System.Text.Encoding.[Default].GetBytes(str))

        ' Create a new Stringbuilder to collect the bytes
        ' and create a string.
        Dim sBuilder As New System.Text.StringBuilder()

        ' Loop through each byte of the hashed data 
        ' and format each one as a hexadecimal string.
        For i As Integer = 0 To data.Length - 1
            sBuilder.Append(data(i).ToString("x2"))
        Next

        ' Return the hexadecimal string.
        Return sBuilder.ToString()

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
