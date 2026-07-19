Imports System.Collections.Generic
Imports System.Threading

Public Class StreamingHelper

    Private Const MaxTempCacheEntries As Integer = 500
    Private Shared ReadOnly CacheLock As New Object
    Private Shared ReadOnly TempStreamingCache As New Dictionary(Of String, String)(StringComparer.Ordinal)
    Private Shared ReadOnly TempIdCreatedUtc As New Dictionary(Of String, DateTime)(StringComparer.Ordinal)
    Private Shared NextTempId As Integer = 1

    Public Shared Function WatchOnline(VLCPath As String, URLStreamning As String) As Boolean
        If String.IsNullOrEmpty(VLCPath) Then Return False
        If String.IsNullOrEmpty(URLStreamning) Then Return False
        ' Only allow local streaming URLs to avoid argument injection / remote launch abuse
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(URLStreamning, UriKind.Absolute, uri) Then Return False
        If uri.Scheme <> Uri.UriSchemeHttp AndAlso uri.Scheme <> Uri.UriSchemeHttps Then Return False
        If Not (uri.Host = "127.0.0.1" OrElse uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) Then Return False

        Dim exe As String = "vlc.exe"
        If Not System.IO.File.Exists(System.IO.Path.Combine(VLCPath, exe)) Then exe = "vlcportable.exe"
        If Not System.IO.File.Exists(System.IO.Path.Combine(VLCPath, exe)) Then Return False

        Dim p As New Process
        p.StartInfo.FileName = System.IO.Path.Combine(VLCPath, exe)
        p.StartInfo.Arguments = """" & URLStreamning.Replace("""", "") & """"
        p.Start()
        Return True
    End Function

    Public Shared Function GetFileDataFromTempID(TempID As String, ByRef FileID As String, ByRef FileKey As String) As Boolean
        SyncLock CacheLock
            PruneTempCacheUnlocked()
            If Not TempStreamingCache.ContainsKey(TempID) Then Return False
            Dim Key As String = TempStreamingCache(TempID)
            If String.IsNullOrEmpty(Key) OrElse Not Key.Contains("|"c) Then Return False
            FileID = Key.Split("|"c)(0)
            FileKey = Key.Split("|"c)(1)
            Return True
        End SyncLock
    End Function

    Public Shared Function CreateStreamingLink(ByVal URLMega As String, ByVal StreamingPort As Integer, ByRef Config As Configuracion) As String

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

        If String.IsNullOrEmpty(FileID) Then Return String.Empty

        Dim key As String = FileID & "|" & FileKey
        Dim TempID As String = ""
        SyncLock CacheLock
            PruneTempCacheUnlocked()
            If TempStreamingCache.ContainsKey(key) Then
                TempID = TempStreamingCache(key)
            Else
                TempID = Interlocked.Increment(NextTempId).ToString()
                TempStreamingCache(key) = TempID
                TempStreamingCache(TempID) = key
                TempIdCreatedUtc(TempID) = DateTime.UtcNow
            End If
        End SyncLock

        ' Fixed loopback origin — never trust Host / Authority from clients
        Dim URLStreaming As String = "http://127.0.0.1:" & StreamingPort.ToString() & "/streaming?t=" & TempID
        If Not String.IsNullOrEmpty(Config.ServidorStreamingPassword) Then
            URLStreaming &= "&p=" & Uri.EscapeDataString(Config.ServidorStreamingPassword)
        End If

        Return URLStreaming
    End Function

    Public Shared Function CreateStreamingLinkFromLibrary(ID As String, CurrentURL As String, ByRef Config As Configuracion) As String
        If StreamingLibraryManager.GetElementByID(ID) Is Nothing Then
            Return String.Empty
        End If

        ' Ignore client-supplied Host; always bind media URLs to loopback
        Dim port As Integer = Config.ServidorStreamingPuerto
        Dim URLStreaming As String = "http://127.0.0.1:" & port.ToString() & "/streaming?id=" & Uri.EscapeDataString(ID)
        If Not String.IsNullOrEmpty(Config.ServidorStreamingPassword) Then
            URLStreaming &= "&p=" & Uri.EscapeDataString(Config.ServidorStreamingPassword)
        End If

        Return URLStreaming
    End Function

    Public Shared Function LibraryManagerURL(StreamingPort As Integer, Manage As Boolean) As String
        Dim URL As String = "http://127.0.0.1:" & StreamingPort.ToString()
        If Manage Then
            URL &= StreamingLibraryModule.PaginaManagement
        Else
            URL &= StreamingLibraryModule.PaginaMain
        End If
        Return URL
    End Function

    Private Shared Sub PruneTempCacheUnlocked()
        If TempStreamingCache.Count <= MaxTempCacheEntries Then Return
        Dim ordered = TempIdCreatedUtc.OrderBy(Function(kv) kv.Value).ToList()
        Dim toRemove As Integer = Math.Max(1, ordered.Count \ 4)
        For i As Integer = 0 To Math.Min(toRemove, ordered.Count) - 1
            Dim tempId As String = ordered(i).Key
            Dim fileKey As String = Nothing
            If TempStreamingCache.TryGetValue(tempId, fileKey) Then
                TempStreamingCache.Remove(tempId)
                If fileKey IsNot Nothing Then TempStreamingCache.Remove(fileKey)
            End If
            TempIdCreatedUtc.Remove(tempId)
        Next
    End Sub

End Class
