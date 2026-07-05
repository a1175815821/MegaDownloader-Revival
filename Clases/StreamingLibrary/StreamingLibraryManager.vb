Imports System.IO
Imports System.Security
Imports System.Xml
Imports System.Text.RegularExpressions

Public Class StreamingLibraryManager
	
	Private Shared _StreamingLibrary As StreamingLibrary
	
	Public Shared Function StreamingLibrary() As StreamingLibrary
		If _StreamingLibrary Is Nothing Then
			_StreamingLibrary = New StreamingLibrary
			_StreamingLibrary.LoadXML()
		End If
		Return _StreamingLibrary
	End Function
	
	Public Shared Sub SaveLibrary()
		If _StreamingLibrary IsNot Nothing Then
			_StreamingLibrary.SaveXML()
		End If
    End Sub

    Public Shared Function ProcessByNameAndAddElement(Name As String, Description As String, Comments As String, Poster As String, Link As String, LinkVisible As Boolean, IMDB As String, Allocine As String, Filmaffinity As String) As LibraryElement
        ' Fill automatically from imdb/filmaffinity/allocine by file name

        Dim regexList() As String = _
                {"\[film(?<FILMAFFINITY>[\d]*)\]", _
                 "\[tt(?<IMDB>[\d]*)\]", _
                 "\[allocine(?<ALLOCINE>[\d]*)\]"}

        If Not String.IsNullOrEmpty(Name) Then
            For Each pattern As String In regexList
                Dim regex = New System.Text.RegularExpressions.Regex(pattern)
                If regex.IsMatch(Name) Then
                    Dim match = regex.Match(Name)

                    If Not String.IsNullOrEmpty(match.Groups("FILMAFFINITY").Value) Then

                        Filmaffinity = match.Groups("FILMAFFINITY").Value

                    ElseIf Not String.IsNullOrEmpty(match.Groups("IMDB").Value) Then

                        IMDB = match.Groups("IMDB").Value

                    ElseIf Not String.IsNullOrEmpty(match.Groups("ALLOCINE").Value) Then

                        Allocine = match.Groups("ALLOCINE").Value
                    End If
                End If
            Next
        End If


        MegaDownloader.IMDB.FillMissingFields(IMDB, Name, Poster, Description)
        MegaDownloader.Allocine.FillMissingFields(Allocine, Name, Poster, Description)
        MegaDownloader.Filmaffinity.FillMissingFields(Filmaffinity, Name, Poster, Description)

        Return AddElement(Name, Description, Comments, Poster, Link, LinkVisible, IMDB, Allocine, Filmaffinity)
    End Function
	
	Public Shared Function AddElement(Name As String, Description As String, Comments As String, Poster As String, Link As String, LinkVisible As Boolean, IMDB As String, Allocine As String, Filmaffinity As String) As LibraryElement
		Dim e As New LibraryElement
		e.Name = Name
		e.Description = Description
		e.Comments = Comments
		e.IMDB = IMDB
		e.Allocine = Allocine
		e.Filmaffinity = Filmaffinity
		e.Poster = Poster
		e.LastModification = Now
		e.LinkVisible = LinkVisible
		If Link <> LibraryElement.HIDDEN_LINK_DESC Then
			e.Link = Criptografia.ToSecureString(Link)
		Else
			e.Link = New SecureString
		End If
		
		e.ID = StreamingLibrary.GetIDandIncrement.ToString
		StreamingLibrary.Elements.Add(e)
		StreamingLibrary.SaveXML()
		Return e
	End Function
	
	Public Shared Function ModifyElement(ID As String, Name As String, Description As String, Comments As String, Poster As String, Link As String, LinkVisible As Boolean, IMDB As String, Allocine As String, Filmaffinity As String) As LibraryElement
		
		Dim l As IEnumerable(Of LibraryElement) = From e As LibraryElement In StreamingLibrary.Elements Where e.ID = ID
		If l.Count > 0 Then
			Dim e As LibraryElement = l.ToArray(0)
			e.Name = Name
			e.Description = Description
			e.Comments = Comments
			e.IMDB = IMDB
			e.Allocine = Allocine
			e.Filmaffinity = Filmaffinity
			e.Poster = Poster
			e.LastModification = Now
			If Link <> LibraryElement.HIDDEN_LINK_DESC Then
				e.Link = Criptografia.ToSecureString(Link)
				e.LinkVisible = LinkVisible
			End If
			
			StreamingLibrary.SaveXML()
			
			Return e
		Else
			Return Nothing
		End If
		
	End Function
	
	Public Shared Sub RemoveElement(ID As String)
		Dim l As IEnumerable(Of LibraryElement) = From e As LibraryElement In StreamingLibrary.Elements Where e.ID = ID
		If l.Count > 0 Then
			StreamingLibrary.Elements.Remove(l.ToArray(0))
			StreamingLibrary.SaveXML()
		End If
	End Sub
	
	Public Shared Function GetElementByID(ID As String) As LibraryElement
		Dim l As IEnumerable(Of LibraryElement) = From e As LibraryElement In StreamingLibrary.Elements Where e.ID = ID
		If l.Count > 0 Then
			Return l.ToArray(0)
		End If
		Return Nothing
	End Function
	
	Public Shared Function GetElements() As List(Of LibraryElement)
		Return (From e As LibraryElement In StreamingLibrary.Elements Order By e.Name).ToList
	End Function
	
	Public Shared Function IsImportedLibrary(data As String) As Boolean
		Return GetImportedLibrary(data).Count > 0
	End Function
	
	Public Shared Function GetImportedLibrary(data As String) As List(Of String)
		' Detecta:
		' 1) el código entre "<ExportedXML>" Y "</ExportedXML>" (con los tags incluidos)
		' 2) el código entre "{MEGALIBRARY}" y "{:MEGALIBRARY}" (sin los tags)
		Dim Libraries As New List(Of String)
		
		
		Dim regx As New Regex("<(?<Tag>ExportedXML)>(?<Content>.*?)</\1>", RegexOptions.IgnoreCase)
		Dim matches As MatchCollection = regx.Matches(data)
		
		For Each match As Match In matches
			Dim valor As String = "<ExportedXML>" & match.Groups("Content").Value & "</ExportedXML>"
			Libraries.Add(valor)
		Next
		
		regx = New Regex("[<|{](?<Tag>MEGALIBRARY)[>|}](?<Content>.*?)[<|{][/|:]\1[>|}]", RegexOptions.IgnoreCase)
		matches = regx.Matches(data)
		
		For Each match As Match In matches
			Dim valor As String = match.Groups("Content").Value
			Try
				Dim bytes() As Byte = Criptografia.base64urldecodeBytes(valor)
				valor = UnCompressString(bytes)
				Libraries.Add(valor)
			Catch ex As Exception
				Log.WriteError("Error trying to import library [GetImportedLibrary] - error " & ex.ToString & " - Data: " & valor)
			End Try
			
		Next
		Return Libraries
	End Function
	
	Public Shared Function ImportLibrary(Data As String) As Integer
		Dim Imported As Integer = 0
		For Each library As String In GetImportedLibrary(Data)
			Try
				' Cada library es un nodo  "<ExportedXML>" ... "</ExportedXML>"
				Dim xml As New XmlDocument
				xml.LoadXml(library)
				
				For Each element As XmlNode In xml.DocumentElement.SelectNodes("Elements/Element")
					Dim ele As New LibraryElement
					ele.LoadXML(element, True)
					
					Dim l As IEnumerable(Of LibraryElement) = From e As LibraryElement In StreamingLibrary.Elements Where _
						Criptografia.ToInsecureString(e.Link).ToUpper.Trim = Criptografia.ToInsecureString(ele.Link).ToUpper.Trim
					If l.Count > 0 Then
						Continue For ' Already exists
					End If
					
					ele.ID = StreamingLibrary.GetIDandIncrement.ToString()
					ele.LastModification = Now
					StreamingLibrary.Elements.Add(ele)
					Imported += 1
				Next
				StreamingLibrary.SaveXML()
			Catch ex As Exception
				Log.WriteError("Error trying to import library [ImportLibrary] - error " & ex.ToString & " - Data: " & library)
			End Try
			
		Next
		Return Imported
	End Function
	
	Public Shared Function ImportLinks(ByRef Config As Configuracion, ByVal URLs As List(Of String)) As Integer
		
		' Convertimos los links de MegaFolder a links individuales
		Dim URLs2 As New Generic.List(Of String)
		For Each URL As String In URLs
			If URLExtractor.IsMegaFolder(URL) Then
				Dim FolderID As String = URLExtractor.ExtraerFileID(URL)
				Dim FolderKey As String = URLExtractor.ExtraerFileKey(URL)
                For Each FileURL In MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey)
                    URLs2.Add(FileURL.URL)
                Next
			ElseIf URLExtractor.IsELC(URL) Then
				Dim ELC_exc As Exception = Nothing
                For Each FileURL As String In ServerEncoderLinkHelper.ServerDecode(URL, Config, ELC_exc)
                    If URLExtractor.IsMegaFolder(FileURL) Then
                        Dim FolderID As String = URLExtractor.ExtraerFileID(FileURL)
                        Dim FolderKey As String = URLExtractor.ExtraerFileKey(FileURL)
                        For Each FileURL2 In MegaFolderHelper.RetrieveLinksFromFolder(FolderID, FolderKey)
                            URLs2.Add(Fichero.HIDDEN_LINK & FileURL2.URL)
                        Next
                    Else
                        URLs2.Add(Fichero.HIDDEN_LINK & FileURL)
                    End If
                Next
			Else
				URLs2.Add(URL)
			End If
		Next
		URLs = URLs2
		
		' Importamos
		Dim imported As Integer = 0
		For Each URL As String In URLs
						
			Dim URLFile As String = URL
			Dim Visible As Boolean = true
			If Not String.IsNullOrEmpty (URL) AndAlso URL.Contains(Fichero.HIDDEN_LINK) Then
				Visible = False
				URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "")
			End If
			
			If String.IsNullOrEmpty(URLFile) OrElse String.IsNullOrEmpty(Fichero.ExtraerFileID(URLFile)) Then
				Continue For
			End If
			
			Dim URL2 As String = URLFile
			Dim l As IEnumerable(Of LibraryElement) = From e As LibraryElement In StreamingLibrary.Elements Where Criptografia.ToInsecureString(e.Link).ToUpper.Trim = URL2.ToUpper.Trim
			If l.Count > 0 Then
				Continue For ' Already exists
			End If
			
			Dim FileID As String = Fichero.ExtraerFileID(URLFile)
			Dim FileKey As String = Fichero.ExtraerFileKey(URLFile)
			
			Dim info As Conexion.InformacionFichero = Conexion.ObtenerInformacionFichero(Config, FileID, FileKey, False)
			If info.Err = Conexion.TipoError.SinErrores Then
                ProcessByNameAndAddElement(info.Nombre, "", "", "", URLFile, Visible, "", "", "")
			Else
				AddElement("Imported element #" & imported, "", "", "", URLFile, Visible, "", "", "")
			End If
			imported += 1
			
		Next
		Return imported
    End Function



    Public Shared Function ExportElements(ByVal IDs As Generic.List(Of String), HideLinks As Boolean, ByVal PlainText As Boolean) As String

        Dim Xml As New XmlDocument
        Dim Root As XmlNode = Xml.AppendChild(Xml.CreateElement("ExportedXML"))
        Dim ElementList As XmlNode = Root.AppendChild(Xml.CreateElement("Elements"))

        For Each ele As LibraryElement In StreamingLibrary.Elements
            If IDs.Contains(ele.ID) Then
                Dim vis As Boolean = ele.LinkVisible
                If HideLinks Then ele.LinkVisible = False
                ele.SaveXML(ElementList, True)
                ele.LinkVisible = vis
            End If
        Next

        Dim str As String = Xml.DocumentElement.OuterXml
        If PlainText Then Return str

        Return "{MEGALIBRARY}" & Criptografia.base64urlencode(CompressString(str)) & "{:MEGALIBRARY}"

    End Function

    Private Shared Function CompressString(str As String) As Byte()
        Dim mem As New IO.MemoryStream
        Dim gz As New System.IO.Compression.GZipStream(mem, IO.Compression.CompressionMode.Compress)
        Dim sw As New IO.StreamWriter(gz)
        sw.Write(str)
        sw.Close()
        Return mem.ToArray
    End Function

    Private Shared Function UnCompressString(bytes() As Byte) As String
        Dim mem2 As New IO.MemoryStream(bytes.ToArray)
        Dim gz As New System.IO.Compression.GZipStream(mem2, IO.Compression.CompressionMode.Decompress)
        Dim sr As New IO.StreamReader(gz)
        Dim rs As String = sr.ReadToEnd
        sr.Close()
        Return rs
    End Function

End Class