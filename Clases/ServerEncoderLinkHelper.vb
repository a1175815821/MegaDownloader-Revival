Imports System.Xml
Imports System.Security.Cryptography

Public Class ServerEncoderLinkHelper
	
	' Esta clase debe ser igual en MegaDownloader y MegaUploader!!
	Private Const ENCODE_PASSWORD As String = "tal.vApsg@i0smae"
	
	Public Class MegaLink
		Public FileID As String
		Public FileKey As String
        Public MegaFolder As Boolean
	End Class
	
	
	
	Public Shared Function ServerEncode(ByVal URL As String, byval LinkList As Generic.List(Of MegaLink), byref Config As Configuracion) As String
		
		Dim Helper As New ELCAccountHelper(Config)
		Try
			
			' mega://#!ABC!12345678900
			Dim strBuilderLinks As New System.Text.StringBuilder
			If LinkList IsNot Nothing then
				For Each Link As MegaLink In LinkList
					If strBuilderLinks.Length > 0 Then strBuilderLinks.Append("|")
					strBuilderLinks.Append("#" & If(Link.MegaFolder, "F", "") & "!" & Link.FileID & "!" & Link.FileKey)
				Next
			End if
			
			Dim linksToEncode As String = strBuilderLinks.ToString
			
			' 1) Generate random password
			Dim b As [Byte]() = New [Byte](23) {} 
			System.Security.Cryptography.RandomNumberGenerator.Create.GetBytes(b)
			Dim Password As String = Convert.ToBase64String(b)
			
			' 2) Encode data
			dim LinksBinary() As Byte = Cipher(False, System.Text.Encoding.UTF8.GetBytes(linksToEncode), b)
			dim URLBinary() As Byte = System.Text.Encoding.UTF8.GetBytes(URL)		
		
			
			' 3) Contact the server to encode the password
            If URL = "REVERSE" Or URL = "HIDDEN" Then ' For testing or hidden links
                Password = New String(Password.Reverse.ToArray)
            Else

                Dim Account As ELCAccountHelper.Account = Helper.GetAccountDetailsByURL(URL)
                If Account Is Nothing Then
                    Log.WriteDebug("ELC Account not found for [" & URL & "]")
                    Throw New ApplicationException(Language.GetText("ELC Account not found. You need a valid ELC Account configured for this ELC."))
                End If

                Dim param As New Specialized.NameValueCollection
                param.Add("OPERATION_TYPE", "E")
                param.Add("DATA", Password)
                param.Add("USER", Criptografia.ToInsecureString(Account.User))
                param.Add("APIKEY", Criptografia.ToInsecureString(Account.Key))

                Dim URL2 As String = URL
                If URLExtractor.EsUrlAcortador(URL2) Then
                    URL2 = Conexion.ObtenerUrlDesdeAcortador(URL2)
                End If
                Dim Res As Conexion.Respuesta = Conexion.SendPOST(URL2, param)

                If Res.Excepcion IsNot Nothing Then
                    Throw New ApplicationException("Error contacting the server to encode string [" & URL & "]", Res.Excepcion)
                End If

                Dim ht As Dictionary(Of String, Object)
                ' Example: {"e": "Error description", "d": "asdfjaoijeoiwajoiajsdf"}
                ht = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(Res.Mensaje, _
                    GetType(Generic.Dictionary(Of String, Object))),  _
                    Generic.Dictionary(Of String, Object))

                If ht.ContainsKey("e") AndAlso ht("e") IsNot Nothing AndAlso Not String.IsNullOrEmpty(CStr(ht("e"))) Then
                    Throw New ApplicationException("Server returned error - " & CStr(ht("e")))
                End If
                If Not ht.ContainsKey("d") Then
                    Return String.Empty
                End If

                Password = CStr(ht("d"))
            End If
			
			
			
			' 3) Create stream
			Dim PasswordBinary() As Byte = System.Text.Encoding.UTF8.GetBytes(Password)	
			Dim Length1 As Integer = LinksBinary.Length
			Dim Length2 As Short = CShort(URLBinary.length)
			Dim Length3 As Short = CShort(PasswordBinary.length)
			
			Dim ByteList As New Generic.List(Of Byte)
			
			ByteList.AddRange(System.BitConverter.GetBytes(Length1))
			ByteList.AddRange(LinksBinary)
			ByteList.AddRange(System.BitConverter.GetBytes(Length2))
			ByteList.AddRange(URLBinary)
			ByteList.AddRange(System.BitConverter.GetBytes(Length3))		
			ByteList.AddRange(PasswordBinary)
			
			' 4) Compress if necessary
			Dim l As Generic.List(Of Byte) = CompressBytes(ByteList.ToArray).ToList
			If l.count < ByteList.count Then
				ByteList = l
				ByteList.Insert(0, 112) ' Compressed - Hemos elegido 112 porque al pasarlo a base64 hace que la primera letra sea una "c"
			Else
				ByteList.Insert(0, 185) ' Uncompressed -  Hemos elegido 185 porque al pasarlo a base64 hace que la primera letra sea una "u"
			End If
			Return Convert.ToBase64String(ByteList.ToArray).Replace("+", "-").Replace("/", "_").Replace("=", "")
			
		Catch ex As Exception
			throw
		Finally
			Helper.Dispose 
		End Try
	End Function
	
	Public Shared Function ServerDecode(ByVal link As String, ByRef Config As Configuracion, byref Exc As Exception) As Generic.List(Of String)
		Dim Helper As New ELCAccountHelper(Config)
		Dim linkBackup As String = link
		Dim LinkList As New Generic.List(Of String)
		Try
			
					
			' 1) Decode string and uncompress
			If String.IsNullOrEmpty(link) Then Return LinkList
			If link.Contains(URLExtractor.SERVERENCODEDPREFIX) Then
				link = link.Substring(link.IndexOf(URLExtractor.SERVERENCODEDPREFIX) + URLExtractor.SERVERENCODEDPREFIX.Length)
            End If
            If link.Contains(URLExtractor.SERVERENCODEDPREFIX.Replace("?", "/?")) Then
                link = link.Substring(link.IndexOf(URLExtractor.SERVERENCODEDPREFIX.Replace("?", "/?")) + URLExtractor.SERVERENCODEDPREFIX.Replace("?", "/?").Length)
            End If
			
			
			link &= "==".Substring((2 - link.Length * 3) And 3)
			link = link.Replace("-", "+").Replace("_", "/").Replace(",", "")
			
			Dim ByteStream As Generic.List(Of Byte) = Convert.FromBase64String(link).ToList
			
			Dim Compressed As Boolean = False
			Select Case ByteStream(0)
				Case 112 ' Hemos elegido 112 porque al pasarlo a base64 hace que la primera letra sea una "c"
					ByteStream = ByteStream.GetRange(1, ByteStream.Count - 1) : Compressed = True
				Case 185 ' Hemos elegido 185 porque al pasarlo a base64 hace que la primera letra sea una "u"
					ByteStream = ByteStream.GetRange(1, ByteStream.Count - 1)
				Case Else
					Return LinkList
			End Select
			
			If Compressed Then
				ByteStream = UnCompressBytes(ByteStream.ToArray).ToList
			End If
			
			
			' 2) Read BinaryStream
			Dim Length1 As Integer = BitConverter.ToInt32(ByteStream.ToArray, 0)
			Dim LinksBinary As Generic.List(Of Byte) = ByteStream.GetRange(4, Length1) 
			Dim Length2 As Short = BitConverter.ToInt16(ByteStream.ToArray, 4 + Length1)
			Dim URLBinary As Generic.List(Of Byte) = ByteStream.GetRange(4 + Length1 + 2, Length2) 
			Dim Length3 As Short = BitConverter.ToInt16(ByteStream.ToArray, 4 + Length1 + 2 + Length2)
			Dim PasswordBinary As Generic.List(Of Byte) = ByteStream.GetRange(4 + Length1 + 2 + Length2 + 2, Length3) 
			
			Dim Password As String = System.Text.Encoding.UTF8.GetString(PasswordBinary.ToArray)
			Dim URL As String = System.Text.Encoding.UTF8.GetString(URLBinary.ToArray)
			
			' 3) Contact the server to decode the password
            If URL = "REVERSE" Or URL = "HIDDEN" Then ' For testing or hidden links
                Password = New String(Password.Reverse.ToArray)
            Else

                Dim Account As ELCAccountHelper.Account = Helper.GetAccountDetailsByURL(URL)
                If Account Is Nothing Then
                    Log.WriteDebug("ELC Account not found for [" & URL & "]")
                    Throw New ApplicationException(Language.GetText("ELC Account not found. You need a valid ELC Account configured for this ELC."))
                End If


                Dim param As New Specialized.NameValueCollection
                param.Add("OPERATION_TYPE", "D")
                param.Add("DATA", Password)
                param.Add("USER", Criptografia.ToInsecureString(Account.User))
                param.Add("APIKEY", Criptografia.ToInsecureString(Account.Key))

                Dim URL2 As String = URL
                If URLExtractor.EsUrlAcortador(URL2) Then
                    URL2 = Conexion.ObtenerUrlDesdeAcortador(URL2)
                End If
                Dim Res As Conexion.Respuesta = Conexion.SendPOST(URL2, param)

                If Res.Excepcion IsNot Nothing Then
                    Throw New ApplicationException("Error contacting the server to decode string [" & URL & "]", Res.Excepcion)
                End If

                Dim ht As Generic.Dictionary(Of String, Object)
                ' Example: {"e": "Error description", "d": "asdfjaoijeoiwajoiajsdf"}
                ht = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(Res.Mensaje, _
                    GetType(Generic.Dictionary(Of String, Object))),  _
                    Generic.Dictionary(Of String, Object))

                If ht.ContainsKey("e") AndAlso ht("e") IsNot Nothing AndAlso Not String.IsNullOrEmpty(CStr(ht("e"))) Then
                    Throw New ApplicationException("Server returned error - " & CStr(ht("e")))
                End If
                If Not ht.ContainsKey("d") Then
                    Return LinkList
                End If

                Password = CStr(ht("d"))
            End If
			
			
			' 4) Decode the links with the password
			LinksBinary = Trim0s(Cipher(True, LinksBinary.ToArray, Convert.FromBase64String(Password)).ToList)
			Dim Data As String = System.Text.Encoding.UTF8.GetString(LinksBinary.ToArray)
			
			For Each token As String In Data.Split("|"c)
				If Not String.IsNullOrEmpty (token) Then
					LinkList.Add("mega://" & token.trim)
				End If
			Next
			
			Return LinkList
			
		Catch ex As Exception
			Log.WriteError("Error trying to decrypt the server-encoded string " & linkBackup & " - Error: " & ex.ToString)
			Exc = ex
			Return LinkList
		Finally
			Helper.Dispose
		End Try
		
	End Function
	
	Private Shared Function Trim0s(BinaryList As Generic.List(Of Byte)) As Generic.List(Of Byte)
		Dim index As Integer = BinaryList.FindIndex(Function(x) x = 0 )
		If index > 0 Then
			BinaryList.RemoveRange(index, BinaryList.Count - index)
		End If
		return BinaryList
	End Function
	
	
	Private Shared Function Cipher(byval Decrypt As Boolean, byval Data As byte(), byval Key As byte()) As byte()
		
		Using aesAlg As New AesManaged
			
			aesAlg.KeySize = 128
			aesAlg.BlockSize = 128
			
			aesAlg.Key = Key.Take(16).toarray
			
			aesAlg.IV = New Byte() {Key(16), Key(17), Key(18), Key(19), Key(20), Key(21), Key(22), Key(23), 0, 0, 0, 0, 0, 0, 0, 0}
			
			aesAlg.Mode = System.Security.Cryptography.CipherMode.CBC
			aesAlg.Padding = System.Security.Cryptography.PaddingMode.Zeros
			
			
			Dim decryptor As System.Security.Cryptography.ICryptoTransform 
			If Decrypt Then
				decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV)
				Using ms As New IO.MemoryStream(Data)
					Using objCryptoStream As New CryptoStream(ms, decryptor, CryptoStreamMode.Read)
						Dim Response(Data.Length - 1) As Byte
						objCryptoStream.Read(Response, 0, Response.Length)
						Try
							objCryptoStream.FlushFinalBlock()
						Catch ex As Exception
						End Try
						
						Return Response
					End Using
					
				End Using
			Else
				decryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV)
				Using ms As New IO.MemoryStream()
					Using objCryptoStream As New CryptoStream(ms, decryptor, CryptoStreamMode.write)
						objCryptoStream.Write(Data, 0, Data.Length)
						objCryptoStream.FlushFinalBlock()
						Return ms.toArray
					End Using
				End Using
			End If
			
		End Using
		
	End Function
	
	Private Shared Function CompressBytes(ByVal bytes() As Byte) As Byte()
		
		Using msi = New IO.MemoryStream(bytes)
			Using mso = New IO.MemoryStream()
				Using gs = New IO.Compression.GZipStream(mso, IO.Compression.CompressionMode.Compress)
					CopyTo(msi, gs)
				End Using
				
				Return mso.ToArray()
			End Using
		End Using
	End Function
	
	Private Shared Function UnCompressBytes(ByVal bytes() As Byte) As byte()
		
		Using msi = New IO.MemoryStream(bytes)
			Using mso = New IO.MemoryStream()
				Using gs = New IO.Compression.GZipStream(msi, IO.Compression.CompressionMode.Decompress)
					CopyTo(gs, mso)
				End Using
				
				Return mso.ToArray()
			End Using
		End Using
	End Function
	
	private Shared Sub CopyTo(src As IO.Stream, dest As IO.Stream)
		Dim bytes As Byte() = New Byte(1023) {}
		
		Dim cnt As Integer
		Do
			cnt = src.Read(bytes, 0, bytes.Length)
			If cnt <> 0 Then
				dest.Write(bytes, 0, cnt)
			End If
		Loop While cnt > 0
		
	End Sub
End Class
