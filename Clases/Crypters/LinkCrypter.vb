Namespace Crypters

    Public Class LinkCrypter

        Public Shared Function IsLinkCrypter(FileID As String) As Boolean
            If String.IsNullOrEmpty(FileID) Then Return False
            Return FileID.ToLower.StartsWith(URLExtractor.LINKCRYPTERTOKEN.ToLower) And FileID.Split("$"c).Length = 3
        End Function

        Public Shared Function ObtenerInformacionFichero(ByVal Config As Configuracion, ByVal FileID As String, ByVal FileKey As String, ByVal ComprobacionAntesDescarga As Boolean) As Conexion.InformacionFichero
            If Not IsLinkCrypter(FileID) Then Throw New ApplicationException("Error, not LinkCrypter link")
            Dim LinkCrypter As String = "!" & FileID.Split("$"c)(1) & "!" & FileID.Split("$"c)(2)


            Dim Peticiones As New Generic.List(Of String)
            If ComprobacionAntesDescarga Then
                If String.IsNullOrEmpty(FileKey) Then Peticiones.Add("info")
                Peticiones.Add("dl")
            Else
                Peticiones.Add("info")
            End If

            Dim Info As New Conexion.InformacionFichero
            Info.FileID = FileID
            Info.FileKey = FileKey

            For Each Peticion As String In Peticiones
                Dim json As String = String.Format("{{""link"":""{0}"",""m"":""{1}""}}", LinkCrypter, Peticion)

                Dim Resultado As Conexion.Respuesta = Conexion.SendJSON("https://linkcrypter.net/api", json, SendAppId:=False)


                If Resultado.Excepcion Is Nothing Then

                    Dim FileInfoRS As Generic.Dictionary(Of String, Object)
                    Try
                        Dim str As String = Resultado.Mensaje
                        If str.StartsWith("[") Then str = str.Substring(1)
                        If str.EndsWith("]") Then str = str.Trim("]"c)

                        If IsNumeric(str) Then
                            Throw New ApplicationException("Invalid LinkCrypter link (" & str & ")")
                        End If

                        FileInfoRS = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(str, _
                            GetType(Generic.Dictionary(Of String, Object))),  _
                            Generic.Dictionary(Of String, Object))

                        ' DL: {"url":"http:\/\/gfs262n177.userstorage.mega.co.nz\/dl\/O3LMIM ... lUmO2g"}
                        ' INFO: {"name":"test.rar","size":14691675,"key":"-f_P0SJFJna0M5l ... 0xN5oytwew"}

                        If FileInfoRS.ContainsKey("error") Then
                            Throw New ApplicationException("Invalid LinkCrypter link (" & CStr(FileInfoRS.Item("error")) & ")")
                        End If

                    Catch excFileInfo As Exception
                        Log.WriteError("Error getting the info in LinkCrypter - Error: " & excFileInfo.ToString)
                        Info.Err = Conexion.TipoError.Otros
                        Info.Errtxt = "Error getting the info in LinkCrypter: " & excFileInfo.Message
                        Return Info
                    End Try

                    If FileInfoRS.ContainsKey("name") Then
                        Info.Nombre = CStr(FileInfoRS.Item("name"))
                    End If
                    If FileInfoRS.ContainsKey("key") Then
                        Info.FileKey = CStr(FileInfoRS.Item("key"))
                    End If
                    If FileInfoRS.ContainsKey("size") Then
                        Info.Tamano = 0
                        Long.TryParse(CStr(FileInfoRS.Item("size")), Info.Tamano)
                    End If
                    If FileInfoRS.ContainsKey("url") Then
                        Info.URL = CStr(FileInfoRS.Item("url"))
                    End If

                ElseIf TypeOf (Resultado.Excepcion) Is System.Net.WebException Then
                    Info.Err = Conexion.TipoError.ErrorConexion
                    Info.Errtxt = "Connection error: " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje
                    Log.WriteError("Error getting the info in LinkCrypter: " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje)
                    Return Info
                Else
                    Info.Err = Conexion.TipoError.Otros
                    Info.Errtxt = "Description: " & Resultado.Excepcion.ToString & " - Message received: " & Resultado.Mensaje
                    Log.WriteError("Error getting the info in LinkCrypter: " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje)
                    Return Info
                End If
            Next

            Return Info

        End Function


    End Class

End Namespace