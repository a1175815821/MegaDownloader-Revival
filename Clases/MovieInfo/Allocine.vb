Imports System.Xml
Imports System.Text

Public Class Allocine


    Public Class MovieInfo
        Public OK As Boolean
        Public Err As Exception
        Public Title As String
        Public Poster As String
        Public Plot As String
    End Class

    Public Shared Sub FillMissingFields(ByRef AllocineID As String, ByRef Name As String, ByRef Poster As String, ByRef Desc As String)
        If String.IsNullOrEmpty(AllocineID) Then Exit Sub
        If String.IsNullOrEmpty(Name) Or _
           String.IsNullOrEmpty(Poster) Or _
           String.IsNullOrEmpty(Desc) Then

            Dim info As MovieInfo = GetMovieInfo(AllocineID)
            If info.OK Then
                If String.IsNullOrEmpty(Name) Then Name = info.Title
                If String.IsNullOrEmpty(Poster) Then Poster = info.Poster
                If String.IsNullOrEmpty(Desc) Then Desc = info.Plot
            End If
        End If
    End Sub



    Public Shared Function GetMovieInfo(ByVal AllocineID As String) As MovieInfo
        Dim info As New MovieInfo

        ' https://github.com/gromez/allocine-api/blob/master/PHP/allocine.class.php

        Dim partnerKey As String = "100043982026"
        Dim secretKey As String = "29d185d98c984a359e6e6f26a0474269"

        Dim query As String = "partner=" & partnerKey & "&format=json&profile=small&code=" & AllocineID
        Dim sed As String = "&sed=" & Now.ToString("yyyyMMdd")

        Dim sig As String = "&sig=" & GetSHA1(secretKey & query & sed)

        Dim Res As Conexion.Respuesta = Conexion.LeerURL("http://api.allocine.fr/rest/v3/movie?" & query & sed & sig, _
                                                         userAgent:="Dalvik/1.6.0 (Linux; U; Android 4.2.2; Nexus 4 Build/JDQ39E)")

        If Res.Excepcion IsNot Nothing Then
            info.OK = False
            info.Err = Res.Excepcion
            Return info
        End If

        Dim JSON As Newtonsoft.Json.Linq.JObject

        Try
            JSON = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(Res.Mensaje & ""), Newtonsoft.Json.Linq.JObject)
        Catch ex As Exception
            info.OK = False
            info.Err = Res.Excepcion
            Return info
        End Try

        If JSON.Item("error") IsNot Nothing Then
            info.OK = False
            If JSON.Item("error").Item("$") IsNot Nothing Then
                info.Err = New ApplicationException(JSON.Item("error").Item("$").ToString())
            Else
                info.Err = New ApplicationException(JSON.Item("error").ToString())
            End If
            Return info
        End If

        If JSON.Item("movie") Is Nothing Then
            info.OK = False
            info.Err = New ApplicationException("Allocine movie node not found")
            Return info
        End If

        If JSON.Item("movie").Item("title") IsNot Nothing Then
            info.Title = JSON.Item("movie").Item("title").ToString
        End If

        If JSON.Item("movie").Item("productionYear") IsNot Nothing Then
            info.Title &= " (" & JSON.Item("movie").Item("productionYear").ToString & ")"
        End If

        If JSON.Item("movie").Item("synopsisShort") IsNot Nothing Then
            info.Plot = JSON.Item("movie").Item("synopsisShort").ToString
        End If

        If JSON.Item("movie").Item("poster") IsNot Nothing AndAlso JSON.Item("movie").Item("poster").Item("href") IsNot Nothing Then
            info.Poster = JSON.Item("movie").Item("poster").Item("href").ToString
        End If


        info.OK = True

        Return info
    End Function

    Public Shared Function GetSHA1(str As String) As String

        Dim sha1 As System.Security.Cryptography.SHA1 = System.Security.Cryptography.SHA1Managed.Create()

        Dim stream As Byte() = sha1.ComputeHash(Encoding.Default.GetBytes(str))

        Return Convert.ToBase64String(stream)

    End Function


End Class
