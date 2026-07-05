Namespace Crypters

    Public Class EncrypterMega

        Public Shared Function IsEncrypterMega(FileID As String) As Boolean
            If String.IsNullOrEmpty(FileID) Then Return False
            Return FileID.ToLower.StartsWith(URLExtractor.ENCRYPTERMEGATOKEN.ToLower) And FileID.Split("$"c).Length = 3
        End Function

        Public Shared Function ObtenerInformacionFichero(ByVal Config As Configuracion, ByVal FileID As String, ByVal FileKey As String, ByVal ComprobacionAntesDescarga As Boolean) As Conexion.InformacionFichero
            If Not IsEncrypterMega(FileID) Then Throw New ApplicationException("Error, not EncrypterMe.ga link")
            Dim LinkMegaCrypter As String = "!" & FileID.Split("$"c)(1) & "!" & FileID.Split("$"c)(2)


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
                Dim json As String = String.Format("{{""link"":""{0}"",""m"":""{1}""}}", LinkMegaCrypter, Peticion)

                Dim Resultado As Conexion.Respuesta = Conexion.SendJSON("http://encrypterme.ga/api", json, SendAppId:=False)


                If Resultado.Excepcion Is Nothing Then

                    Dim FileInfoRS As Generic.Dictionary(Of String, Object)
                    Try
                        Dim str As String = Resultado.Mensaje
                        If str.StartsWith("[") Then str = str.Substring(1)
                        If str.EndsWith("]") Then str = str.Trim("]"c)

                        If IsNumeric(str) Then
                            Throw New ApplicationException("Invalid EncrypterMe.ga link (" & str & ")")
                        End If

                        FileInfoRS = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(str, _
                            GetType(Generic.Dictionary(Of String, Object))),  _
                            Generic.Dictionary(Of String, Object))

                        ' DL: {"url":"http:\/\/gfs262n177.userstorage.mega.co.nz\/dl\/O3LMIM ... lUmO2g"}
                        ' INFO: {"name":"test.rar","size":14691675,"key":"-f_P0SJFJna0M5l ... 0xN5oytwew"}

                        If FileInfoRS.ContainsKey("error") Then
                            Throw New ApplicationException("Invalid EncrypterMe.ga link (" & CStr(FileInfoRS.Item("error")) & ")")
                        End If

                    Catch excFileInfo As Exception
                        Log.WriteError("Error getting the info in EncrypterMe.ga - Error: " & excFileInfo.ToString)
                        Info.Err = Conexion.TipoError.Otros
                        Info.Errtxt = "Error getting the info in EncrypterMe.ga: " & excFileInfo.Message
                        Return Info
                    End Try

                    If FileInfoRS.ContainsKey("pass") AndAlso FileInfoRS.Item("pass").ToString.ToLower <> "false" Then
                        ' Password!!

                        'Info -response
                        '{"name": "FILE_NAME" OR "CRYPTED_FILE_NAME", 
                        '"size": FILE_SIZE, 
                        '"key": "FILE_KEY" OR "CRYPTED_FILE_KEY",
                        '"extra": "EXTRA_INFO" OR "CRYPTED_EXTRA_INFO",
                        '"expire": false OR EXPIRE_TIMESTAMP,
                        '"pass": false OR "ITER_LOG2#KEY_CHECK_HASH#SALT"}

                        ' About password protected files: File name, file key, and extra-info will be returned crypted using AES CBC (PKCS7) with 256 bits key derivated 
                        ' from pass and zero-iv. Moreover, SHA-256 key hash will be returned to allow you checking it. 


                        Dim dataPass As String() = FileInfoRS.Item("pass").ToString.Split("#"c)
                        If dataPass.Length <> 3 Then
                            Info.Err = Conexion.TipoError.Otros
                            Info.Errtxt = "Error getting the info in EncrypterMe.ga: pass data not defined"
                            Return Info
                        Else

                            Info.Err = Conexion.TipoError.Otros
                            Info.Errtxt = "EncrypterMe.ga with password not supported yet"
                            Return Info
                        End If


                    End If


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
                    Log.WriteError("Error getting the info in EncrypterMe.ga: " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje)
                    Return Info
                Else
                    Info.Err = Conexion.TipoError.Otros
                    Info.Errtxt = "Description: " & Resultado.Excepcion.ToString & " - Message received: " & Resultado.Mensaje
                    Log.WriteError("Error getting the info in EncrypterMe.ga: " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje)
                    Return Info
                End If
            Next

            Return Info

        End Function


    End Class

End Namespace