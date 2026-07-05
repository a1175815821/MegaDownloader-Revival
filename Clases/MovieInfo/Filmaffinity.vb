Public Class Filmaffinity


    Public Class MovieInfo
        Public OK As Boolean
        Public Err As Exception
        Public Title As String
        Public Poster As String
        Public Plot As String
    End Class

    Public Shared Sub FillMissingFields(ByRef FilmAffinityID As String, ByRef Name As String, ByRef Poster As String, ByRef Desc As String)
        If String.IsNullOrEmpty(FilmAffinityID) Then Exit Sub
        If String.IsNullOrEmpty(Name) Or _
           String.IsNullOrEmpty(Poster) Or _
           String.IsNullOrEmpty(Desc) Then

            Dim info As MovieInfo = GetMovieInfo(FilmAffinityID)
            If info.OK Then
                If String.IsNullOrEmpty(Name) Then Name = info.Title
                If String.IsNullOrEmpty(Poster) Then Poster = info.Poster
                If String.IsNullOrEmpty(Desc) Then Desc = info.Plot
            End If
        End If
    End Sub



    Public Shared Function GetMovieInfo(ByVal FilmAffinityID As String) As MovieInfo
        Dim info As New MovieInfo

        FilmAffinityID = FilmAffinityID.ToLower.Replace("film", "")

        Dim Res As Conexion.Respuesta = Conexion.LeerURL("http://www.filmaffinity.com/es/film" & FilmAffinityID & ".html", _
                                                         System.Text.Encoding.GetEncoding("UTF-8"), _
                                                         referer:="http://www.filmaffinity.com", _
                                                         userAgent:="Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36")

        If Res.Excepcion IsNot Nothing Then
            info.OK = False
            info.Err = Res.Excepcion
            Return info
        End If


        Dim moviegif As String = "<img alt=""logo"" src=""/images/logo4.png""></a>"

        If Res.Mensaje.IndexOf(moviegif) <= 0 Then
            info.OK = False
            info.Err = New ApplicationException("Could not scrap website")
            Return info
        End If

        Dim Ini As Integer
        Dim Fin As Integer

        ' Titulo
        Ini = Res.Mensaje.IndexOf("<title>") + 7
        Fin = Res.Mensaje.IndexOf("</title>", Ini)

        If Fin > 0 And Ini > 0 And Fin - Ini > 0 Then
            info.Title = Res.Mensaje.Substring(Ini, Fin - Ini).Replace(" - FilmAffinity", "")
        End If

        '' Año
        'Ini = Res.Mensaje.IndexOf("<th>A&Ntilde;O</th>") + 1
        'If Ini > 0 Then Ini = Res.Mensaje.IndexOf("</a></div>", Ini) + 10
        'Fin = Res.Mensaje.IndexOf("</td>", Ini)

        'If Fin > 0 And Ini > 0 And Fin - Ini > 0 Then
        '    info.Title &= " (" & Res.Mensaje.Substring(Ini, Fin - Ini).Trim & ")"
        'End If

        ' Sinopsis

        ' Año
        Ini = Res.Mensaje.IndexOf("<dt>Sinopsis</dt>") + 1
        If Ini > 0 Then Ini = Res.Mensaje.IndexOf("<dd>", Ini) + 4
        Fin = Res.Mensaje.IndexOf("</dd>", Ini)

        If Fin > 0 And Ini > 0 And Fin - Ini > 0 Then
            info.Plot = Res.Mensaje.Substring(Ini, Fin - Ini).Replace("(FILMAFFINITY)", "").Trim.Replace(vbNewLine, " ")
        End If


        ' Imagen
        Ini = Res.Mensaje.IndexOf("<a class=""lightbox"" href=""") + 26
        If Ini > 0 Then Fin = Res.Mensaje.IndexOf(""" title", Ini)

        If Fin > 0 And Ini > 0 And Fin - Ini > 0 Then
            info.Poster = Res.Mensaje.Substring(Ini, Fin - Ini).Trim
        End If

        info.OK = True

        Return info
    End Function

End Class
