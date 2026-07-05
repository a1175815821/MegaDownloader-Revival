Public Class PreSharedKeyManager

    Public Shared Function GetFileKeyFromPreSharedKeys(ByRef Config As Configuracion) As List(Of String)


        Dim list2 As New List(Of String)
        For Each key As Security.SecureString In Config.ListaPreSharedKeys
            list2.Add(Criptografia.GetFileKeyFromPreSharedKey(Criptografia.ToInsecureString(key)))
        Next

        Return list2
    End Function

    Public Shared Function DecryptFileInfo(ByVal EncryptedFileInfo As String, ByVal FileKey As String) As String
        Dim FileInfoDec As String = Nothing

        If FileKey.Contains("=###n=") Then
            FileKey = FileKey.Substring(0, FileKey.IndexOf("=###n="))
        End If

        Try
            FileInfoDec = "" & Criptografia.AES_MEGA_DecryptString(EncryptedFileInfo, FileKey)
        Catch exc As Exception
            Return Nothing
        End Try

        If Not FileInfoDec.ToUpperInvariant().StartsWith("MEGA") Then
            Return Nothing
        Else
            Return FileInfoDec
        End If
    End Function

End Class
