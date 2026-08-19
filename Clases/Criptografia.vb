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


    ' DPAPI application-specific entropy. Derived from the assembly identity so the
    ' literal is not present in source. Note: DPAPI security primarily relies on the
    ' CurrentUser scope; the entropy only isolates this app from other apps running
    ' under the same user. An attacker who can run code as the user can bypass DPAPI
    ' regardless of the entropy value.
    Shared entropy As Byte() = DeriveAppEntropy()
    ' Legacy entropy retained solely for decrypting data persisted by versions prior
    ' to the derivation change. New encryptions always use the derived entropy above.
    Private Shared legacyEntropy As Byte() = System.Text.Encoding.Unicode.GetBytes("G*SNAfhHW5A¿Amck+XMLCM6M#$xEK;9q")

    Private Shared Function DeriveAppEntropy() As Byte()
        Using sha As New SHA256Managed()
            Return sha.ComputeHash(System.Text.Encoding.Unicode.GetBytes(System.Reflection.Assembly.GetExecutingAssembly().GetName().FullName))
        End Using
    End Function

    Public Shared Function EncryptString_DPAPI(input As System.Security.SecureString) As String
        Dim encryptedData As Byte() = System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.Unicode.GetBytes(ToInsecureString(input)), entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser)
        Return Convert.ToBase64String(encryptedData)
    End Function

    Public Shared Function DecryptString_DPAPI(encryptedData As String) As SecureString
        Try
            Dim decryptedData As Byte() = System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser)
            Return ToSecureString(System.Text.Encoding.Unicode.GetString(decryptedData))
        Catch
            ' Fall back to the legacy entropy for data persisted by previous versions.
            Try
                Dim decryptedData As Byte() = System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), legacyEntropy, System.Security.Cryptography.DataProtectionScope.CurrentUser)
                Return ToSecureString(System.Text.Encoding.Unicode.GetString(decryptedData))
            Catch
                Return New SecureString()
            End Try
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

        vstrTextToBeEncrypted = StripNullCharacters(vstrTextToBeEncrypted & "") ' Evitamos nothing

        bytValue = Encoding.GetBytes(vstrTextToBeEncrypted.ToCharArray)

        Try
            Using objRijndaelManaged As New RijndaelManaged()
                Using objMemoryStream As New MemoryStream()
                    Using objCryptoStream As New CryptoStream(objMemoryStream, _
                          objRijndaelManaged.CreateEncryptor(bytKey, bytIV), _
                          CryptoStreamMode.Write, leaveOpen:=True)
                        objCryptoStream.Write(bytValue, 0, bytValue.Length)
                        objCryptoStream.FlushFinalBlock()
                    End Using
                    bytEncoded = objMemoryStream.ToArray
                End Using
            End Using
        Catch ex As CryptographicException
            Log.WriteError("AES_EncryptString failed: " & ex.ToString)
        Catch ex As Exception
            Log.WriteError("AES_EncryptString unexpected error: " & ex.ToString)
        End Try

        If bytEncoded Is Nothing Then Return String.Empty
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
        Dim bytPlain() As Byte = Nothing
        Dim bytIV() As Byte = {121, 241, 10, 1, 132, 74, 11, 39, 255, 91, 45, 78, 14, 211, 22, 62}

        '   ********************************************************************
        '   ******   Encryption Key must be 256 bits long (32 bytes)      ******
        '   ******   If it is longer than 32 bytes it will be truncated.  ******
        '   ******   If it is shorter than 32 bytes it will be padded     ******
        '   ******   with upper-case Xs.                                  ****** 
        '   ********************************************************************


        Try
            bytDataToBeDecrypted = Convert.FromBase64String(vstrStringToBeDecrypted)

            Using objRijndaelManaged As New RijndaelManaged()
                Using objMemoryStream As New MemoryStream(bytDataToBeDecrypted)
                    Using objCryptoStream As New CryptoStream(objMemoryStream, _
                           objRijndaelManaged.CreateDecryptor(bytDecryptionKey, bytIV), _
                           CryptoStreamMode.Read, leaveOpen:=True)
                        Using plainStream As New MemoryStream()
                            objCryptoStream.CopyTo(plainStream)
                            bytPlain = plainStream.ToArray
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As CryptographicException
            Log.WriteError("AES_DecryptString failed: " & ex.ToString)
        Catch ex As FormatException
            Log.WriteError("AES_DecryptString failed (invalid input): " & ex.ToString)
        Catch ex As Exception
            Log.WriteError("AES_DecryptString unexpected error: " & ex.ToString)
        End Try

        If bytPlain Is Nothing Then Return String.Empty
        Return StripNullCharacters(Encoding.GetString(bytPlain))

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

        ''' <summary>
        ''' Seeks CTR to the block that covers the given absolute file offset (floor division by block size).
        ''' </summary>
        Public Sub SeekToFileOffset(ByVal fileOffset As Long)
            If fileOffset < 0 Then Throw New ArgumentOutOfRangeException("fileOffset")
            Reset()
            Dim blockIndex As Long = fileOffset \ CLng(blockSize)
            IncrementCounter(blockIndex)
        End Sub

        Public Sub IncrementCounter(Optional ByVal NumberOfIncrements As Long = 1)
            If NumberOfIncrements <= 0 Then Return
            AddToCounter(NumberOfIncrements)
        End Sub

        Private Sub AddToCounter(ByVal value As Long)
            If value <= 0 Then Return
            Dim carry As Long = value
            For i As Integer = counter.Length - 1 To 0 Step -1
                If carry = 0 Then Exit For
                Dim sum As Long = CLng(counter(i)) + (carry And &HFFL)
                counter(i) = CByte(sum And &HFFL)
                carry = (carry >> 8) + (sum >> 8)
            Next
        End Sub

    End Class

#End Region

#Region "MEGA MetaMAC"

    ''' <summary>
    ''' Verifies MEGA file MetaMAC (words 6-7 of the 8-word file key) over plaintext on disk.
    ''' Public link keys carry only 4 words (16 bytes, the AES key itself) and do NOT
    ''' embed a MetaMAC — verification is impossible for them and is skipped (True).
    ''' Only 8-word (32-byte) node keys, obtained e.g. from folder API responses,
    ''' contain the nonce (words 4-5) and MetaMAC (words 6-7) needed to verify.
    ''' The chunk-size schedule is MEGA's ChunkedHash: 128 KiB * i for the first 8
    ''' chunks (128, 256, 384, 512, 640, 768, 896 KiB, then 1 MiB), and a fixed 1 MiB
    ''' for every chunk after that. This mirrors the MEGA SDK's
    ''' ChunkedHash::chunkfloor/chunkceil (SEGSIZE = 131072).
    ''' </summary>
    Friend Shared Function VerifyMegaMetaMac(ByVal filePath As String, ByVal pKey As String) As Boolean
        If String.IsNullOrEmpty(filePath) OrElse Not File.Exists(filePath) Then Return False
        If String.IsNullOrEmpty(pKey) Then Return False

        Dim keyWithoutN As String = pKey
        If keyWithoutN.Contains("=###n=") Then
            keyWithoutN = keyWithoutN.Substring(0, keyWithoutN.IndexOf("=###n="))
        End If

        Dim b64Dec As Byte() = B64Decode(keyWithoutN)
        Dim intKey As Integer() = ByteArrayToIntArray(b64Dec)
        If intKey Is Nothing OrElse intKey.Length < 4 Then Return False

        If intKey.Length < 8 Then
            ' 4-word public link key: no MetaMAC is embedded in the key, so there is
            ' nothing to verify against. Skipping must NOT be treated as a failure —
            ' treating it as one wrongly errored every completed public-link download.
            Log.WriteInfo("VerifyMegaMetaMac: key has " & intKey.Length & " words (public link key); no MetaMAC available, skipping verification for " & filePath)
            Return True
        End If

        Dim aesKey As Byte() = IntArrayToBytesArray(New Integer() {
            intKey(0) Xor intKey(4),
            intKey(1) Xor intKey(5),
            intKey(2) Xor intKey(6),
            intKey(3) Xor intKey(7)
        })
        Dim expectedMac0 As Integer = intKey(6)
        Dim expectedMac1 As Integer = intKey(7)

        Dim mac As Integer() = ComputeMegaFileMac(filePath, aesKey)
        Return mac(0) = expectedMac0 AndAlso mac(1) = expectedMac1
    End Function

    ''' <summary>
    ''' Computes the MEGA file MetaMAC using the SDK's ChunkedHash schedule: chunk sizes
    ''' are 128 KiB * i for the first 8 chunks (i = 1..8), then a fixed 1 MiB. Each chunk
    ''' contributes a CBC-MAC (AES, zero IV, zero-padded final block), folded into the
    ''' file MAC with fileMac = AES(fileMac XOR chunkMac). Returns the 2-word MetaMAC
    ''' (fileMac0^fileMac1, fileMac2^fileMac3).
    ''' </summary>
    Private Shared Function ComputeMegaFileMac(ByVal filePath As String, ByVal aesKey As Byte()) As Integer()
        Dim engine As New AesEngine()
        engine.Init(True, New KeyParameter(aesKey))

        Dim fileLen As Long = New FileInfo(filePath).Length

        ' An empty file has no chunks, so MEGA's MetaMAC for it is (0, 0).
        If fileLen = 0 Then Return New Integer() {0, 0}

        Dim fileMac As Integer() = New Integer() {0, 0, 0, 0}

        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
            Dim chunkStart As Long = 0
            Dim chunkNumber As Integer = 1 ' 1-based chunk index

            While chunkStart < fileLen
                ' ChunkedHash: 128 KiB * i for i = 1..8, then a fixed 1 MiB.
                Dim chunkSize As Long = If(chunkNumber <= 8, CLng(chunkNumber) * &H20000L, &H100000L)
                Dim thisChunk As Long = Math.Min(chunkSize, fileLen - chunkStart)

                ' CBC-MAC over this chunk (zero IV, zero-padded final partial block).
                Dim chunkMac As Integer() = New Integer() {0, 0, 0, 0}
                Dim remaining As Long = thisChunk
                Dim buffer(15) As Byte
                While remaining > 0
                    Dim toRead As Integer = CInt(Math.Min(16, remaining))
                    Dim read As Integer = 0
                    While read < toRead
                        Dim n As Integer = fs.Read(buffer, read, toRead - read)
                        If n = 0 Then Exit While
                        read += n
                    End While
                    If read < 16 Then
                        For i As Integer = read To 15
                            buffer(i) = 0
                        Next
                    End If

                    Dim block As Integer() = ByteArrayToIntArray(buffer)
                    chunkMac(0) = chunkMac(0) Xor block(0)
                    chunkMac(1) = chunkMac(1) Xor block(1)
                    chunkMac(2) = chunkMac(2) Xor block(2)
                    chunkMac(3) = chunkMac(3) Xor block(3)
                    chunkMac = AesEncryptBlock(engine, chunkMac)

                    remaining -= toRead
                End While

                ' Fold the chunk MAC into the file MAC.
                fileMac(0) = fileMac(0) Xor chunkMac(0)
                fileMac(1) = fileMac(1) Xor chunkMac(1)
                fileMac(2) = fileMac(2) Xor chunkMac(2)
                fileMac(3) = fileMac(3) Xor chunkMac(3)
                fileMac = AesEncryptBlock(engine, fileMac)

                chunkStart += thisChunk
                chunkNumber += 1
            End While
        End Using

        Return New Integer() {fileMac(0) Xor fileMac(1), fileMac(2) Xor fileMac(3)}
    End Function

    Private Shared Function AesEncryptBlock(ByVal engine As AesEngine, ByVal words As Integer()) As Integer()
        Dim input As Byte() = IntArrayToBytesArray(words)
        Dim output(15) As Byte
        engine.ProcessBlock(input, 0, output, 0)
        Return ByteArrayToIntArray(output)
    End Function

#End Region

End Class
