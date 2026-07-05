Imports System.Collections.Specialized

Public Class Linkdecrypter

    Public Shared Function ExtraerURLs(ByVal URI As String) As Generic.List(Of String)
        Dim Lista As New Generic.List(Of String)


        Dim values As New NameValueCollection()
        values.Add("link_cache", "on")
        values.Add("modo_links", "text")
        values.Add("modo_recursivo", "on")
        values.Add("pro_links", URI)

        Dim RS As Conexion.Respuesta = Conexion.SendPOST("http://linkdecrypter.com", values, SendAppId:=False)

        Dim cookie As String = GetCookie(RS.Cookies)

        values = New NameValueCollection()
        values.Add("Cookie", cookie)


        Dim i As Integer = 0

        Do
            i += 1
            System.Threading.Thread.Sleep(300)
            RS = Conexion.LeerURL("http://linkdecrypter.com", Headers:=values)

            If RS.Mensaje.Contains("Loading CAPTCHA") Then
                ' No va :(
                Exit Do

            ElseIf RS.Mensaje.Contains("<textarea name=""links""") Then

                Dim URLs As Generic.List(Of String) = URLExtractor.ExtraerURLs(RS.Mensaje)
                For Each url As String In URLs
                    Lista.Add(url)
                Next

                Exit Do
            ElseIf i >= 5 Then
                Exit Do

            Else
                Dim sleepTime As Integer = CInt(i * 500 + ((New Random).NextDouble * 500))
                System.Threading.Thread.Sleep(sleepTime)
            End If
        Loop
     


        Return Lista

    End Function


    Private Shared Function GetCookie(cookies As String) As String

        If String.IsNullOrEmpty(cookies) Then
            Return String.Empty
        Else
            Dim str As New System.Text.StringBuilder

            For Each token As String In cookies.Split(";"c)
                Dim tokens As String() = token.Trim.Split("="c)
                If tokens.Length = 2 Then
                    Select Case tokens(0)
                        Case "__cfduid"
                            If str.Length > 0 Then str.Append("; ")
                            str.Append("__cfduid").Append("=").Append(tokens(1))
                        Case "HttpOnly,PHPSESSID", "HttpOnly, PHPSESSID"
                            If str.Length > 0 Then str.Append("; ")
                            str.Append("PHPSESSID").Append("=").Append(tokens(1))
                    End Select
                End If
            Next

            Return str.ToString

        End If

    End Function

End Class
