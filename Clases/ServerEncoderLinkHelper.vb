Imports System.Xml
Imports System.Security.Cryptography

Public Class ServerEncoderLinkHelper
	
	' Esta clase debe ser igual en MegaDownloader y MegaUploader!!
	Private Const ENCODE_PASSWORD As String = "tal.vApsg@i0smae"
	
	Public Class MegaLink
		Public FileID As String
		Public FileKey As String
        Public MegaFolder As Boolean
        ' v2.4.4 子文件夹链接范围: mega.nz/folder/根#key/folder/子ID 或 /file/文件ID。
        ' 旧版解码器忽略 token 尾部的 /folder/... 后缀(退化为整文件夹=历史行为),新版解码器据此恢复子范围。
        Public SubFolderID As String
        Public SubFileID As String
	End Class

	''' <summary>issue #6: friendly message for an ELC URL shortener that could
	''' not be resolved (dead link or unreachable network).</summary>
	Private Shared Function GetShortLinkUnresolvedMessage() As String
		Dim msg As String = Language.GetText("ShortLink_Unresolved")
		If String.IsNullOrEmpty(msg) OrElse msg = "ShortLink_Unresolved" Then
			msg = "Could not resolve the shortened link. It may be dead, or the network is unreachable."
		End If
		Return msg
	End Function
	
	
	
	Public Shared Function ServerEncode(ByVal URL As String, byval LinkList As Generic.List(Of MegaLink), byref Config As Configuracion) As String
		
		Dim Helper As New ELCAccountHelper(Config)
		Try
			
			' mega://#!ABC!12345678900
			Dim strBuilderLinks As New System.Text.StringBuilder
			If LinkList IsNot Nothing then
				For Each Link As MegaLink In LinkList
					If strBuilderLinks.Length > 0 Then strBuilderLinks.Append("|")
					strBuilderLinks.Append("#" & If(Link.MegaFolder, "F", "") & "!" & Link.FileID & "!" & Link.FileKey)
                    If Link.MegaFolder Then
                        If Not String.IsNullOrEmpty(Link.SubFileID) Then
                            strBuilderLinks.Append("/file/" & Link.SubFileID)
                        ElseIf Not String.IsNullOrEmpty(Link.SubFolderID) Then
                            strBuilderLinks.Append("/folder/" & Link.SubFolderID)
                        End If
                    End If
				Next
			End if
			
			Dim linksToEncode As String = strBuilderLinks.ToString
			
			' 1) Generate random password
			Dim b As [Byte]() = New [Byte](23) {}
			Using rng As System.Security.Cryptography.RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator.Create()
				rng.GetBytes(b)
			End Using
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
                    If String.IsNullOrEmpty(URL2) Then
                        ' issue #6: the resolver swallows its own exception and returns
                        ' empty; without this guard SendPOST("") would surface a raw
                        ' UriFormatException ("Invalid URI") to the user.
                        Log.WriteError("ServerEncode: could not resolve shortened URL [" & URL & "]")
                        Throw New ApplicationException(GetShortLinkUnresolvedMessage())
                    End If
                End If
                Dim Res As Conexion.Respuesta = Conexion.SendPOST(URL2, param)

                If Res.Excepcion IsNot Nothing Then
                    ' issue #6: transport failures (no internet, DNS, firewall/VPN/proxy)
                    ' get an actionable message; API-level errors keep the short hint.
                    If NetworkErrors.IsTransportError(Res.Excepcion) Then
                        Log.WriteError("ServerEncode: transport failure contacting [" & URL & "] - " & Log.SafeException(Res.Excepcion))
                        Throw New ApplicationException(NetworkErrors.DescribeTransportError(Res.Excepcion), Res.Excepcion)
                    End If
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
	
	Public Shared Function ServerDecode(ByVal link As String, ByRef Config As Configuracion, byref Exc As Exception, Optional ByVal ct As System.Threading.CancellationToken = Nothing) As Generic.List(Of String)
		Dim Helper As New ELCAccountHelper(Config)
		Dim linkBackup As String = link
		Dim LinkList As New Generic.List(Of String)
		Try
			
					
			' 1) Decode string and uncompress
			If String.IsNullOrEmpty(link) Then Return LinkList
			' P3-3: prefix stripping must be case-insensitive like the detection side
			' (URLExtractor.vb regexes use RegexOptions.IgnoreCase and :239 compares
			' data.ToLower). "ELC?..." / "MEGA://CONFIGELC?..." links were accepted by
			' detection but their prefix was never stripped here, so the base64 step
			' failed and the link could not be decoded — while the lowercase form
			' worked. OrdinalIgnoreCase keeps the untrusted-substring semantics of the
			' old code (strip from the prefix onwards) — only the comparison changes.
			Dim pfxIdx As Integer = link.IndexOf(URLExtractor.SERVERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase)
			If pfxIdx >= 0 Then
				link = link.Substring(pfxIdx + URLExtractor.SERVERENCODEDPREFIX.Length)
			End If
			Dim prefixSlash As String = URLExtractor.SERVERENCODEDPREFIX.Replace("?", "/?")
			pfxIdx = link.IndexOf(prefixSlash, StringComparison.OrdinalIgnoreCase)
			If pfxIdx >= 0 Then
				link = link.Substring(pfxIdx + prefixSlash.Length)
			End If
			
			
			If (link.Length Mod 4) = 1 Then
				Throw New ApplicationException(Language.GetText("Invalid input data"))
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
                    If String.IsNullOrEmpty(URL2) Then
                        ' issue #6: same guard as ServerEncode — an unresolved short
                        ' link must not degrade into a raw UriFormatException.
                        Log.WriteError("ServerDecode: could not resolve shortened URL [" & URL & "]")
                        Throw New ApplicationException(GetShortLinkUnresolvedMessage())
                    End If
                End If
                Dim Res As Conexion.Respuesta = Conexion.SendPOST(URL2, param, ct:=ct)

                If Res.Excepcion IsNot Nothing Then
                    ' issue #6: transport failures (no internet, DNS, firewall/VPN/proxy)
                    ' get an actionable message; API-level errors keep the short hint.
                    If NetworkErrors.IsTransportError(Res.Excepcion) Then
                        Log.WriteError("ServerDecode: transport failure contacting [" & URL & "] - " & Log.SafeException(Res.Excepcion))
                        Throw New ApplicationException(NetworkErrors.DescribeTransportError(Res.Excepcion), Res.Excepcion)
                    End If
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
                    ' N-P2-5: a response without "d" used to return an empty list with no
                    ' exception, so the ELC link vanished silently (no log, no dialog) while
                    ' the rest of the batch succeeded. Fail loudly instead — the outer Catch
                    ' below turns this into Exc, which URLProcessor treats as a per-URL
                    ' failure (logged, and surfaced when the whole batch fails).
                    Throw New ApplicationException(Language.GetText( _
                        "The ELC link could not be decoded: the server did not return any data. The link may be corrupted or the account may be invalid."))
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
			
			' N-P2-5: an empty decode result is abnormal (a valid ELC always carries at
			' least one link). It used to be returned silently, dropping the link without
			' any log or dialog when other links in the same batch succeeded.
			If LinkList.Count = 0 Then
				Throw New ApplicationException(Language.GetText( _
					"The ELC link could not be decoded: no links were found inside. The link may be corrupted."))
			End If
			
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
							Log.WriteError("Cipher: FlushFinalBlock failed during AES decryption (PaddingMode.Zeros, data may be unaligned): " & ex.Message)
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
