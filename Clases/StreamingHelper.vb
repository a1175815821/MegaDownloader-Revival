Public Class StreamingHelper
	
	
	
	Private shared TempStreamingCache As New Generic.Dictionary(Of String, String)
	
	Public Shared Function WatchOnline(VLCPath As String, URLStreamning As String) As Boolean
		If String.IsNullOrEmpty(VLCPath) Then Return False
		Dim exe As String = "vlc.exe"
		If Not System.IO.File.Exists(System.IO.Path.Combine(VLCPath, exe)) Then exe = "vlcportable.exe"
		If Not System.IO.File.Exists(System.IO.Path.Combine(VLCPath, exe)) Then Return False
		
		Dim p As New Process
		p.StartInfo.FileName = System.IO.Path.Combine(VLCPath, exe)
		p.StartInfo.Arguments = URLStreamning
		p.Start()
		Return True
	End Function
	
	
	Public Shared Function GetFileDataFromTempID(TempID As String, ByRef FileID As String, ByRef FileKey As String) As Boolean
		If Not TempStreamingCache.ContainsKey (TempID) Then Return False
		Dim Key As String = TempStreamingCache(TempID) 
		If String.IsNullOrEmpty(Key) OrElse Not Key.Contains ("|") Then Return False
		FileID = Key.Split("|"c)(0)
		FileKey = Key.Split("|"c)(1)
		Return true
	End Function
	
	Public Shared Function CreateStreamingLink(ByVal URLMega As String, ByVal StreamingPort As Integer, byref Config As Configuracion) As String

        Dim listaURLs As New Generic.List(Of String)
        listaURLs.Add(URLMega)
        Dim listaURLs2 = URLProcessor.ProcessURLs(listaURLs, Config)
        If listaURLs2.Count > 1 Then
            MessageBox.Show(Language.GetText("The link is a folder with %NUM files. Now only the first will be used, if you want to use all, import the link into the streaming library." _
                                             ).Replace("%NUM", listaURLs2.Count.ToString), _
                Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        ElseIf listaURLs2.Count = 0 Then
            Return String.Empty
        End If

        Dim FileID As String = Fichero.ExtraerFileID(listaURLs2(0).URL)
        Dim FileKey As String = Fichero.ExtraerFileKey(listaURLs2(0).URL)

        'Dim FileID As String = Fichero.ExtraerFileID(URLMega)
        'Dim FileKey As String = Fichero.ExtraerFileKey(URLMega)

        'If URLExtractor.IsMegaFolder(URLMega) Then
        '	Dim l = MegaFolderHelper.RetrieveLinksFromFolder(FileID, FileKey)
        '	If l.Count > 0 Then
        '		FileID = Fichero.ExtraerFileID(l(0))
        '		FileKey = Fichero.ExtraerFileKey(l(0))
        '		If l.Count > 1 Then
        '                  MessageBox.Show(Language.GetText("The link is a folder with %NUM files. Now only the first will be used, if you want to use all, import the link into the streaming library.").Replace("%NUM", l.Count.ToString), _
        '                      Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        '		End if
        '	Else
        '		Return String.Empty
        '	End If
        'ElseIf URLExtractor.IsELC (URLMega) Then
        '	Dim ELC_exc As Exception = Nothing
        '	Dim l = ServerEncoderLinkHelper.ServerDecode (URLMega, Config, ELC_exc)
        '	If l.Count> 0 Then
        '		FileID = Fichero.ExtraerFileID(l(0))
        '		FileKey = Fichero.ExtraerFileKey(l(0))
        '		If l.Count > 1 Then
        '                  MessageBox.Show(Language.GetText("The ELC contains %NUM files. Now only the first will be used, if you want to use all, import the ELC into the streaming library.").Replace("%NUM", l.Count.ToString), _
        '                      Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        '		End if
        '	End If
        '	If ELC_exc IsNot Nothing Then
        '		MessageBox.Show(Language.GetText("Error") & ": " & ELC_exc.Message, _
        '				Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        '	End If
        'End If
		
		If String.IsNullOrEmpty(FileID) Then Return String.Empty
		
		Dim key As String =	FileID & "|" & FileKey 
		Dim TempID As String = ""
		If TempStreamingCache.ContainsKey(key) Then
			TempID = TempStreamingCache(key)
		Else
			TempID = ((TempStreamingCache.Keys.Count / 2) + 1).ToString
			TempStreamingCache(key) = TempID
			TempStreamingCache(TempID) = key
		End If
		
		
		Dim URLStreaming As String = "http://localhost:"
		
		URLStreaming &= StreamingPort
        URLStreaming &= "/streaming?t=" & TempID
        If Not String.IsNullOrEmpty(Config.ServidorStreamingPassword) Then
            URLStreaming &= "&p=" & Config.ServidorStreamingPassword
        End If
		
		Return URLStreaming
		
	End Function
	
    Public Shared Function CreateStreamingLinkFromLibrary(ID As String, CurrentURL As String, ByRef Config As Configuracion) As String
        If StreamingLibraryManager.GetElementByID(ID) Is Nothing Then
            Return String.Empty
        End If

        'Dim URLStreaming As String = "http://localhost:"
        'URLStreaming &= Config.ServidorStreamingPuerto

        Dim URLStreaming As String = "http://" & CurrentURL
        URLStreaming &= "/streaming?id=" & ID
        If Not String.IsNullOrEmpty(Config.ServidorStreamingPassword) Then
            URLStreaming &= "&p=" & Config.ServidorStreamingPassword
        End If

        Return URLStreaming
    End Function
	
	Public Shared Function LibraryManagerURL(StreamingPort As Integer, Manage As Boolean) As String
		Dim URL As String = "http://localhost:"
		URL &= StreamingPort
		If Manage Then
			URL &= StreamingLibraryModule.PaginaManagement
		Else
			URL &= StreamingLibraryModule.PaginaMain
		End If
		Return URL
	End Function
	
End Class
