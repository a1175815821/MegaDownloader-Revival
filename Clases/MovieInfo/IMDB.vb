Public Class IMDB


    Public Class MovieInfo
        Public OK As Boolean
        Public Err As Exception
        Public Title As String
        Public Poster As String
        Public Plot As String
    End Class

    Public Shared Sub FillMissingFields(ByRef IMDB As String, ByRef Name As String, ByRef Poster As String, ByRef Desc As String)
        If String.IsNullOrEmpty(IMDB) Then Exit Sub
        If String.IsNullOrEmpty(Name) Or _
           String.IsNullOrEmpty(Poster) Or _
           String.IsNullOrEmpty(Desc) Then

            Dim info As MovieInfo = GetMovieInfo(IMDB)
            If info.OK Then
                If String.IsNullOrEmpty(Name) Then Name = info.Title
                If String.IsNullOrEmpty(Poster) Then Poster = info.Poster
                If String.IsNullOrEmpty(Desc) Then Desc = info.Plot
            End If
        End If
    End Sub



    Public Shared Function GetMovieInfo(ByVal IMDB As String) As MovieInfo
        Dim info As New MovieInfo

        IMDB = IMDB.ToLower.Replace("tt", "")

        Dim Res As Conexion.Respuesta = Conexion.LeerURL("http://www.omdbapi.com/?i=tt" & IMDB)

        If Res.Excepcion IsNot Nothing Then
            info.OK = False
            info.Err = Res.Excepcion
            Return info
        End If

        Dim ht As Dictionary(Of String, Object)
        Try
            ht = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(Res.Mensaje, _
                                                          GetType(Generic.Dictionary(Of String, Object))),  _
                                                          Generic.Dictionary(Of String, Object))
        Catch ex As Exception
            info.OK = False
            info.Err = Res.Excepcion
            Return info
        End Try

        If ht.ContainsKey("Error") AndAlso Not String.IsNullOrEmpty(CStr(ht("Error"))) Then
            info.OK = False
            info.Err = New ApplicationException(CStr(ht("Error")))
            Return info
        End If

        If ht.ContainsKey("Title") Then
            info.Title = CStr(ht("Title"))
        End If
        If ht.ContainsKey("Year") Then
            info.Title &= " (" & CStr(ht("Year")) & ")"
        End If
        If ht.ContainsKey("Poster") Then
            info.Poster = CStr(ht("Poster"))
        End If
        If ht.ContainsKey("Plot") Then
            info.Plot = CStr(ht("Plot"))
        End If
        info.OK = True

        Return info
    End Function

End Class
