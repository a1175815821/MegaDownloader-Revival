Imports System.Security
Imports System.Security.Cryptography
Imports System.IO
Imports Org.BouncyCastle.Crypto.Modes
Imports Org.BouncyCastle.Crypto.Engines
Imports Org.BouncyCastle.Crypto.Parameters
Imports Org.BouncyCastle.Math
Imports Org.BouncyCastle.Crypto

Public Class Criptografia

#Region "Criptografía interna"


    Shared entropy As Byte() = System.Text.Encoding.Unicode.GetBytes("G*SNAfhHW5A¿Amck+XMLCM6M#$xEK;9q")
    ' Un salt para dificultar un posible hackeo, usando DPAPI.

    Public Shared Function EncryptString_DPAPI(input As System.Security.SecureString) As String
        Dim encryptedData As Byte() = System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.Unicode.GetBytes(ToInsecureString(input)), entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser)
        Return Convert.ToBase64String(encryptedData)
    End Function

    Public Shared Function DecryptString_DPAPI(encryptedData As String) As SecureString
        Try
            Dim decryptedData As Byte() = System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser)
            Return ToSecureString(System.Text.Encoding.Unicode.GetString(decryptedData))
        Catch
            Return New SecureString()
        End Try
    End Function

    Public Shared Function ToSecureString(input As String) As SecureString
        Dim secure As New SecureString()
        For Each c As Char In input
            secure.AppendChar(c)
        Next
        secure.MakeReadOnly()
        Return secure
    End Function

    Public Shared Function ToInsecureString(input As SecureString) As String
        Dim returnValue As String = String.Empty
        Dim ptr As IntPtr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input)
        Try
            returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr)
        Finally
            System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr)
        End Try
        Return returnValue
    End Function


    Public Shared Function AES_EncryptString(ByVal vstrTextToBeEncrypted As String, _
                                             ByVal vstrEncryptionKey As String) As String

        Return AES_EncryptString(vstrTextToBeEncrypted, vstrEncryptionKey, System.Text.Encoding.ASCII)

    End Function
    Public Shared Function AES_EncryptString(ByVal vstrTextToBeEncrypted As String, _
                                             ByVal vstrEncryptionKey As String, _
                                             ByVal Encoding As System.Text.Encoding) As String

        Dim intRemaining As Integer
        Dim intLength As Integer
        Dim bytKey() As Byte

        intLength = Len(vstrEncryptionKey)


        '   ********************************************************************
        '   ******   Encryption Key must be 256 bits long (32 bytes)      ******
        '   ******   If it is longer than 32 bytes it will be truncated.  ******
        '   ******   If it is shorter than 32 bytes it will be padded     ******
        '   ******   with upper-case Xs.                                  ****** 
        '   ********************************************************************

        If intLength >= 32 Then
            vstrEncryptionKey = Strings.Left(vstrEncryptionKey, 32)
        Else
            intLength = Len(vstrEncryptionKey)
            intRemaining = 32 - intLength
            vstrEncryptionKey = vstrEncryptionKey & Strings.StrDup(intRemaining, "X")
        End If

        bytKey = System.Text.Encoding.ASCII.GetBytes(vstrEncryptionKey.ToCharArray)

        Return AES_EncryptString(vstrTextToBeEncrypted, bytKey, Encoding)

    End Function

    Public Shared Function AES_EncryptString(ByVal vstrTextToBeEncrypted As String, _
                                             ByVal bytKey() As Byte, _
                                             ByVal Encoding As System.Text.Encoding) As String

        Dim bytValue() As Byte
        Dim bytEncoded() As Byte = Nothing
        Dim bytIV() As Byte = {121, 241, 10, 1, 132, 74, 11, 39, 255, 91, 45, 78, 14, 211, 22, 62}
        Dim objMemoryStream As New MemoryStream()
        Dim objCryptoStream As CryptoStream
        Dim objRijndaelManaged As RijndaelManaged

        vstrTextToBeEncrypted = StripNullCharacters(vstrTextToBeEncrypted & "") ' Evitamos nothing

        bytValue = Encoding.GetBytes(vstrTextToBeEncrypted.ToCharArray)


        objRijndaelManaged = New RijndaelManaged()

        Try

            objCryptoStream = New CryptoStream(objMemoryStream, _
              objRijndaelManaged.CreateEncryptor(bytKey, bytIV), _
              CryptoStreamMode.Write)
            objCryptoStream.Write(bytValue, 0, bytValue.Length)

            objCryptoStream.FlushFinalBlock()

            bytEncoded = objMemoryStream.ToArray
            objMemoryStream.Close()
            objCryptoStream.Close()
        Catch

        End Try

        Return Convert.ToBase64String(bytEncoded)

    End Function

    Public Shared Function AES_DecryptString(ByVal vstrStringToBeDecrypted As String, _
                                            ByVal vstrDecryptionKey As String) As String

        Return AES_DecryptString(vstrStringToBeDecrypted, vstrDecryptionKey, System.Text.Encoding.ASCII)

    End Function

    Public Shared Function AES_DecryptString(ByVal vstrStringToBeDecrypted As String, _
                                             ByVal vstrDecryptionKey As String, _
                                             ByVal Encoding As System.Text.Encoding) As String

        Dim intLength As Integer
        Dim intRemaining As Integer
        Dim bytDecryptionKey() As Byte

        intLength = Len(vstrDecryptionKey)

        If intLength >= 32 Then
            vstrDecryptionKey = Strings.Left(vstrDecryptionKey, 32)
        Else
            intLength = Len(vstrDecryptionKey)
            intRemaining = 32 - intLength
            vstrDecryptionKey = vstrDecryptionKey & Strings.StrDup(intRemaining, "X")
        End If

        bytDecryptionKey = System.Text.Encoding.ASCII.GetBytes(vstrDecryptionKey.ToCharArray)

        Return AES_DecryptString(vstrStringToBeDecrypted, bytDecryptionKey, Encoding)

    End Function

    Public Shared Function AES_DecryptString(ByVal vstrStringToBeDecrypted As String, _
                                             ByVal bytDecryptionKey() As Byte, _
                                             ByVal Encoding As System.Text.Encoding) As String

        Dim bytDataToBeDecrypted() As Byte
        Dim bytTemp() As Byte
        Dim bytIV() As Byte = {121, 241, 10, 1, 132, 74, 11, 39, 255, 91, 45, 78, 14, 211, 22, 62}
        Dim objRijndaelManaged As New RijndaelManaged()
        Dim objMemoryStream As MemoryStream
        Dim objCryptoStream As CryptoStream


        Dim strReturnString As String = String.Empty

        bytDataToBeDecrypted = Convert.FromBase64String(vstrStringToBeDecrypted)

        '   ********************************************************************
        '   ******   Encryption Key must be 256 bits long (32 bytes)      ******
        '   ******   If it is longer than 32 bytes it will be truncated.  ******
        '   ******   If it is shorter than 32 bytes it will be padded     ******
        '   ******   with upper-case Xs.                                  ****** 
        '   ********************************************************************


        ReDim bytTemp(bytDataToBeDecrypted.Length)

        objMemoryStream = New MemoryStream(bytDataToBeDecrypted)

        Try

            objCryptoStream = New CryptoStream(objMemoryStream, _
               objRijndaelManaged.CreateDecryptor(bytDecryptionKey, bytIV), _
               CryptoStreamMode.Read)

            objCryptoStream.Read(bytTemp, 0, bytTemp.Length)
            Try
                objCryptoStream.FlushFinalBlock()
            Catch ex As Exception
            End Try

            objMemoryStream.Close()
            objCryptoStream.Close()

        Catch

        End Try

        Return StripNullCharacters(Encoding.GetString(bytTemp))

    End Function


    Private Shared Function StripNullCharacters(ByVal vstrStringWithNulls As String) As String

        Dim intPosition As Integer
        Dim strStringWithOutNulls As String

        intPosition = 1
        strStringWithOutNulls = vstrStringWithNulls

        Do While intPosition > 0
            intPosition = InStr(intPosition, vstrStringWithNulls, vbNullChar)

            If intPosition > 0 Then
                strStringWithOutNulls = Left$(strStringWithOutNulls, intPosition - 1) & _
                                  Right$(strStringWithOutNulls, Len(strStringWithOutNulls) - intPosition)
            End If

            If intPosition > strStringWithOutNulls.Length Then
                Exit Do
            End If
        Loop

        Return strStringWithOutNulls

    End Function




#End Region

#Region "Criptografía MEGA"


    Friend Shared Function GetFileKeyFromPreSharedKey(ByVal PreSharedKey As String) As String
        Dim PSK As String = PreSharedKey.PadRight(24, "#"c).Substring(0, 24)

        Dim bitArray() As Byte = GetBytes(PSK)

        Dim hashKey As Integer() = {-1815844893, _
                                    2108737444, _
                                    -776061055, _
                                    22203222, _
                                    1885434739, _
                                    2003792484}

        Dim hasharray As Byte() = IntArrayToBytesArray(hashKey)
        If bitArray.Length = hasharray.Length Then
            For i As Integer = 0 To bitArray.Length - 1
                bitArray(i) = bitArray(i) Xor CByte(i)
                bitArray(i) = bitArray(i) Xor hasharray(i)
            Next
        End If

        Dim intKey As Integer() = ByteArrayToIntArray(bitArray)

        Dim Key As Integer() = New Integer() {intKey(0) Xor intKey(4), intKey(1) Xor intKey(5), intKey(2), intKey(3), intKey(4), intKey(5), 0, 0}
        Return a32_to_base64(Key)
    End Function

    Private Shared Function GetBytes(ByVal str As String) As Byte()
        Dim l As New List(Of Byte)
        For Each c As Char In str
            l.Add(BitConverter.GetBytes(c)(0))
        Next
        Return l.ToArray
    End Function

    Private Shared Function a32_to_str(a As Integer()) As String
        Dim b As String = ""
        For i As Integer = 0 To (a.Length * 4) - 1
            Dim val As Integer = a(i >> 2)
            Dim val2 As Integer = (24 - (i And 3) * 8)
            Dim CodCharacter As Integer = ZFRS(val, val2) And 255
            Select Case CodCharacter
                'Case 156
                ' No hacemos nada, en javascript "String.fromCharCode(156)" no devuelve nada
                Case Else
                    b &= ChrW(CodCharacter)
            End Select
        Next
        Return b
    End Function
    Private Shared Function ZFRS(i As Integer, j As Integer) As Integer
        Dim maskIt As Boolean = (i < 0)
        i = i >> j
        If maskIt Then
            i = i And &H7FFFFFFF
        End If
        Return i
    End Function

    Friend Shared Function GetInstaceCipher(ByVal pKey As String) As SicSeekableBlockCipher
        Dim b64Dec As Byte() = B64Decode(pKey)
        Dim intKey As Integer() = ByteArrayToIntArray(b64Dec)
        Dim keyNOnce As Integer() = New Integer() {intKey(0) Xor intKey(4), intKey(1) Xor intKey(5), intKey(2) Xor intKey(6), intKey(3) Xor intKey(7), intKey(4), intKey(5)}
        Dim key As Byte() = IntArrayToBytesArray(New Integer() {keyNOnce(0), keyNOnce(1), keyNOnce(2), keyNOnce(3)})
        Dim iv As Byte() = IntArrayToBytesArray(New Integer() {keyNOnce(4), keyNOnce(5), 0, 0})
        Dim cipher As SicSeekableBlockCipher = Nothing
        cipher = New SicSeekableBlockCipher(New AesEngine())
        Dim ivAndKey As New ParametersWithIV(New KeyParameter(key), iv)
        cipher.Init(False, ivAndKey)
        Return cipher
    End Function

    Friend Shared Function AES_MEGA_DecryptString(ByVal pEnc As String, ByVal pKey As String) As String
        Dim b64Dec As Byte() = B64Decode(pKey)
        Dim intKey As Integer() = ByteArrayToIntArray(b64Dec)
        Dim key As Byte()
        If intKey.Length = 4 Then
            key = IntArrayToBytesArray(New Integer() {intKey(0), intKey(1), intKey(2), intKey(3)})
        Else
            key = IntArrayToBytesArray(New Integer() {intKey(0) Xor intKey(4), intKey(1) Xor intKey(5), intKey(2) Xor intKey(6), intKey(3) Xor intKey(7)})
        End If

        Dim iv As Byte() = IntArrayToBytesArray(New Integer() {0, 0, 0, 0})

        Dim unPadded As Byte() = B64Decode(pEnc)
        Dim len As Integer = 16 - ((unPadded.Length - 1) And 15) - 1
        Dim payLoadBytes As Byte() = New Byte(unPadded.Length + (len - 1)) {}
        Array.Copy(unPadded, 0, payLoadBytes, 0, unPadded.Length)

        Return DecryptStringFromBytesAes(payLoadBytes, key, iv)
    End Function

    Private Shared Function DecryptStringFromBytesAes(ByVal pCipherText As Byte(), ByVal pKey As Byte(), ByVal pIV As Byte()) As String
        ' Check arguments.
        If pCipherText Is Nothing OrElse pCipherText.Length <= 0 Then
            Throw New ArgumentNullException("pCipherText")
        End If
        If pKey Is Nothing OrElse pKey.Length <= 0 Then
            Throw New ArgumentNullException("pKey")
        End If
        If pIV Is Nothing OrElse pIV.Length <= 0 Then
            Throw New ArgumentNullException("pIV")
        End If

        ' Declare the string used to hold
        ' the decrypted text.
        Dim plaintext As String = Nothing

        ' Create an Aes object
        ' with the specified key and IV.
        Using aesAlg As Aes = Aes.Create()
            aesAlg.Mode = CipherMode.CBC
            aesAlg.Padding = PaddingMode.None

            aesAlg.Key = pKey
            aesAlg.IV = pIV

            ' Create a decrytor to perform the stream transform.
            Dim decryptor As ICryptoTransform = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV)
            ' Create the streams used for decryption.
            Using msDecrypt As New MemoryStream(pCipherText)
                Using csDecrypt As New CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
                    Using srDecrypt As New StreamReader(csDecrypt)
                        ' Read the decrypted bytes from the decrypting stream
                        ' and place them in a string.
                        plaintext = srDecrypt.ReadToEnd()
                    End Using
                End Using

            End Using
        End Using

        Return plaintext
    End Function

    Friend Shared Function decrypt_key(ByVal Data As Integer(), ByVal keyhash As Integer()) As Integer()

        Using aesAlg As New AesManaged

            aesAlg.KeySize = 128
            aesAlg.BlockSize = 128


            aesAlg.Key = IntArrayToBytesArrayREVERSE(keyhash)
            aesAlg.IV = New Byte() {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}

            aesAlg.Mode = CipherMode.CBC
            aesAlg.Padding = PaddingMode.Zeros


            Dim Buffer As Byte() = IntArrayToBytesArrayREVERSE(Data)

            Dim buffer2(Buffer.Length - 1) As Byte
            For i As Integer = 0 To Buffer.Length - 1 Step aesAlg.CreateDecryptor.InputBlockSize
                Dim dec = aesAlg.CreateDecryptor
                dec.TransformBlock(Buffer, i, dec.InputBlockSize, buffer2, i)
            Next
            Dim l() As Integer = ByteArrayToIntArrayREVERSE(buffer2)
            Return l

        End Using

    End Function

    Friend Shared Function ByteArrayToIntArrayREVERSE(ByVal pBytes As Byte()) As Integer()
        Dim b(CInt(Math.Ceiling(pBytes.Count / 4)) - 1) As Integer
        Dim x As Integer = 0

        For i As Integer = 0 To CInt(pBytes.Length / 4) - 1
            If 4 * (i + 1) <= pBytes.Length Then
                Dim l As New List(Of Byte)()
                For z As Integer = i * 4 + 3 To i * 4 Step -1
                    l.Add(pBytes(z))
                Next

                b(x) = BitConverter.ToInt32(l.ToArray, 0)
                x += 1
            End If
        Next
        Return b

    End Function
    Private Shared Function IntArrayToBytesArrayREVERSE(ByVal pInts As IEnumerable(Of Integer)) As Byte()
        Dim res(pInts.Count * 4 - 1) As Byte
        Dim i As Integer = 0
        For Each t As Integer In pInts
            Dim b() As Byte = BitConverter.GetBytes(t)
            For j As Integer = b.Length - 1 To 0 Step -1
                res(i) = b(j)
                i += 1
            Next
        Next
        Return res
    End Function


    Friend Shared Function base64_to_a32(ByVal pData As String) As Integer()
        Return str_to_a32(base64urldecode(pData))
    End Function

    Friend Shared Function a32_to_base64(ByVal a As Integer()) As String
        Dim str As String = a32_to_str(a)
        str = EncodeTo64(str)
        str = str.Replace("+", "-").Replace("/", "_").Replace("=", "")
        Return str
    End Function

    Private Shared Function EncodeTo64(ByVal toEncode As String) As String
        Dim returnValue As String = System.Convert.ToBase64String(GetBytes(toEncode))
        Return returnValue
    End Function

    Private Shared Function base64urldecode(ByVal pData As String) As String
        Dim bytes() As Byte = base64urldecodeBytes(pData)
        Dim RS As String = ""
        For Each b As Byte In bytes
            RS &= ChrW(b)
        Next
        Return RS
    End Function

    Friend Shared Function str_to_a32(ByVal b As String) As Integer()
        Dim a = New Integer(((b.Length + 3) >> 2) - 1) {}

        For i As Integer = 0 To b.Length - 1
            a(i >> 2) = a(i >> 2) Or (CInt(AscW(Convert.ToChar(b.Substring(i, 1)))) << (24 - (i And 3) * 8))
        Next

        Return a
    End Function

    Private Shared Function B64Decode(pData As String) As Byte()
        pData &= "==".Substring((2 - pData.Length * 3) And 3)
        pData = pData.Replace("-", "+").Replace("_", "/").Replace(",", "")
        Return Convert.FromBase64String(pData)
    End Function

    Private Shared Function ByteArrayToIntArray(pBytes As Byte()) As Integer()
        Dim b = New List(Of Integer)()
        For i As Integer = 0 To CInt(pBytes.Length / 4) - 1
            If 4 * (i + 1) <= pBytes.Length Then b.Add(BitConverter.ToInt32(pBytes, i * 4))
        Next
        Return b.ToArray()
    End Function

    Private Shared Function IntArrayToBytesArray(pInts As IEnumerable(Of Integer)) As Byte()
        Dim res = New List(Of Byte)()
        For Each t As Integer In pInts
            res.AddRange(BitConverter.GetBytes(t))
        Next
        Return res.ToArray()
    End Function

    'Public Shared Sub DecryptFile(pEncFile As String, pOutDecFile As String, pKey As String)
    '    Dim cipher As SicBlockCipher = GetInstaceCipher(pKey)

    '    Using fs = New FileStream(pEncFile, FileMode.Open)
    '        If System.IO.File.Exists(pOutDecFile) Then
    '            System.IO.File.Delete(pOutDecFile)
    '        End If
    '        Using fw = New FileStream(pOutDecFile, FileMode.CreateNew)
    '            Dim i = 0
    '            While i < fs.Length
    '                Dim buff As Byte() = New Byte(CInt(If(fs.Length - i > cipher.GetBlockSize(), cipher.GetBlockSize(), fs.Length - i) - 1)) {}
    '                fs.Read(buff, 0, buff.Length)
    '                Dim result = cipherData(cipher, buff)
    '                fw.Write(result, 0, result.Length)
    '                i += cipher.GetBlockSize()
    '            End While
    '        End Using
    '    End Using
    'End Sub

'    Private Shared Function cipherData(cipher As SicBlockCipher, data As Byte()) As Byte()
'        Dim originalLength = data.Length
'        Dim outBuff As Byte() = New Byte(data.Length - 1) {}
'        Dim i = 0
'        While i < data.Length
'            If i + cipher.GetBlockSize() < data.Length Then
'                cipher.ProcessBlock(data, i, outBuff, i)
'            Else
'                Dim diff = (i + cipher.GetBlockSize()) - data.Length
'                Dim tmp = data.ToList()
'                tmp.AddRange(New Byte(diff - 1) {})
'                data = tmp.ToArray()
'
'                tmp = outBuff.ToList()
'                tmp.AddRange(New Byte(diff - 1) {})
'                outBuff = tmp.ToArray()
'
'                cipher.ProcessBlock(data, i, outBuff, i)
'            End If
'            i += cipher.GetBlockSize()
'        End While
'        Return outBuff.Take(originalLength).ToArray()
'    End Function


   
    Public Shared Function base64urlencode(pData() As Byte) As String
        Dim d As String = System.Convert.ToBase64String(pData)
        d = d.Replace("+", "-").Replace("/", "_").Replace("=", "")
        Return d
    End Function

    Public Shared Function base64urldecodeBytes(ByVal pData As String) As Byte()
        pData &= "==".Substring((2 - pData.Length * 3) And 3)
        pData = pData.Replace("-", "+").Replace("_", "/").Replace(",", "")
        Dim bytes() As Byte = Convert.FromBase64String(pData)
        Return bytes
    End Function

#End Region

#Region "SicSeekableBlockCipher"

    ' Copied from SICBlockCipher.cs (BouncyCastle C#) with a new "IncrementCounter" property 
    Public Class SicSeekableBlockCipher
        Implements IBlockCipher

        Private ReadOnly cipher As IBlockCipher
        Private ReadOnly blockSize As Integer
        Private ReadOnly IV As Byte()
        Private ReadOnly counter As Byte()
        Private ReadOnly counterOut As Byte()

        '*
        '		* Basic constructor.
        '		*
        '		* @param c the block cipher to be used.
        '		

        Public Sub New(cipher As IBlockCipher)
            Me.cipher = cipher
            Me.blockSize = cipher.GetBlockSize()
            Me.IV = New Byte(blockSize - 1) {}
            Me.counter = New Byte(blockSize - 1) {}
            Me.counterOut = New Byte(blockSize - 1) {}
        End Sub

        '*
        '		* return the underlying block cipher that we are wrapping.
        '		*
        '		* @return the underlying block cipher that we are wrapping.
        '		

        Public Function GetUnderlyingCipher() As IBlockCipher
            Return cipher
        End Function

        'ignored by this CTR mode
        Public Sub Init(forEncryption As Boolean, parameters As ICipherParameters) Implements Org.BouncyCastle.Crypto.IBlockCipher.Init
            If TypeOf parameters Is ParametersWithIV Then
                Dim ivParam As ParametersWithIV = DirectCast(parameters, ParametersWithIV)
                Dim iv__1 As Byte() = ivParam.GetIV()
                Array.Copy(iv__1, 0, IV, 0, IV.Length)

                Reset()
                cipher.Init(True, ivParam.Parameters)
            Else
                Throw New ArgumentException("SIC mode requires ParametersWithIV", "parameters")
            End If
        End Sub

        Public ReadOnly Property AlgorithmName() As String Implements Org.BouncyCastle.Crypto.IBlockCipher.AlgorithmName
            Get
                Return cipher.AlgorithmName + "/SIC"
            End Get
        End Property

        Public ReadOnly Property IsPartialBlockOkay() As Boolean Implements Org.BouncyCastle.Crypto.IBlockCipher.IsPartialBlockOkay
            Get
                Return True
            End Get
        End Property

        Public Function GetBlockSize() As Integer Implements Org.BouncyCastle.Crypto.IBlockCipher.GetBlockSize
            Return cipher.GetBlockSize()
        End Function

        Public Function ProcessBlock(input As Byte(), inOff As Integer, output As Byte(), outOff As Integer) As Integer Implements Org.BouncyCastle.Crypto.IBlockCipher.ProcessBlock
            cipher.ProcessBlock(counter, 0, counterOut, 0)

            '
            ' XOR the counterOut with the plaintext producing the cipher text
            '
            For i As Integer = 0 To counterOut.Length - 1
                output(outOff + i) = CByte(counterOut(i) Xor input(inOff + i))
            Next

            IncrementCounter()

            Return counter.Length
        End Function

        Public Sub Reset() Implements Org.BouncyCastle.Crypto.IBlockCipher.Reset
            Array.Copy(IV, 0, counter, 0, counter.Length)
            cipher.Reset()
        End Sub

        Public Sub IncrementCounter(Optional ByVal NumberOfIncrements As Integer = 1)
            ' Increment the counter
            If NumberOfIncrements < 16 Then
                For aux As Integer = 1 To NumberOfIncrements

                    Dim j As Integer = counter.Length - 1

                    If j >= 0 Then counter(j) = CByte(If(counter(j) + 1 > Byte.MaxValue, 0, counter(j) + 1))
                    While j >= 0 AndAlso 0 = counter(j)
                        j -= 1
                        If j >= 0 Then counter(j) = CByte(If(counter(j) + 1 > Byte.MaxValue, 0, counter(j) + 1))
                    End While
                Next
            Else
                Dim bi As New Org.BouncyCastle.Math.BigInteger(counter)
                bi = bi.Add(New Org.BouncyCastle.Math.BigInteger(NumberOfIncrements.ToString))
                Dim l As List(Of Byte) = bi.ToByteArray.ToList
                If l.Count < 16 Then
                    For i As Integer = l.Count + 1 To 16
                        l.Insert(0, 0)
                    Next
                End If
                Array.Copy(l.ToArray, 0, counter, 0, counter.Length)
            End If

        End Sub

    End Class

#End Region

End Class
