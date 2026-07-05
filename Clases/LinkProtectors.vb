Public Class LinkProtectors

    Public Shared Function ExtraerURLs(ByVal LixInURI As String) As Generic.List(Of String)

        If Not IsLinkProtector(LixInURI) Then
            Return New Generic.List(Of String)
        Else
            Return Linkdecrypter.ExtraerURLs(LixInURI)
        End If

    End Function

    Friend Shared Function IsLinkProtector(ByVal URI As String) As Boolean
        Return IsLixIn(URI) OrElse IsAdfly(URI) 'OrElse IsShSt(URI)
    End Function

    Private Shared Function IsLixIn(ByVal URI As String) As Boolean
        If String.IsNullOrEmpty(URI) Then Return False
        Return URI.ToLower.Contains("lix.in/")
    End Function

    Private Shared Function IsAdfly(ByVal URI As String) As Boolean
        If String.IsNullOrEmpty(URI) Then Return False
        Return URI.ToLower.Contains("j.gs/") Or URI.ToLower.Contains("q.gs/") Or URI.ToLower.Contains("adf.ly/")
    End Function

    'Private Shared Function IsShSt(ByVal URI As String) As Boolean
    '    If String.IsNullOrEmpty(URI) Then Return False
    '    Return URI.ToLower.Contains("sh.st/")
    'End Function


End Class
