Imports System.Collections.Specialized
Imports System.IO

Public Class DLCHelper

    Public Class DLCFileNotFound
        Inherits ApplicationException

        Public Sub New()
            MyBase.New("DLC/ELC file does not exists")
        End Sub
    End Class

    Public Class DLCFileIsEmpty
        Inherits ApplicationException

        Public Sub New()
            MyBase.New("DLC/ELC file is empty")
        End Sub
    End Class

    Public Shared Function ReadELC_File(filePath As String) As String

        If Not System.IO.File.Exists(filePath) Then
            Throw New DLCFileNotFound
        End If

        Dim text As New System.Text.StringBuilder
        Using t As New StreamReader(filePath)
            While Not t.EndOfStream
                text.Append(t.ReadToEnd)
            End While
        End Using

        If text.Length = 0 Then
            Throw New DLCFileIsEmpty
        End If

        Return text.ToString

    End Function

    Public Shared Function DecryptDLC_File(filePath As String) As Generic.List(Of String)

        If Not System.IO.File.Exists(filePath) Then
            Throw New DLCFileNotFound
        End If

        Dim text As New System.Text.StringBuilder
        Using t As New StreamReader(filePath)
            While Not t.EndOfStream
                text.Append(t.ReadToEnd)
            End While
        End Using

        If text.Length = 0 Then
            Throw New DLCFileIsEmpty
        End If

        Return DecryptDLC_Content(text.ToString)

    End Function


    Public Shared Function DecryptDLC_Content(dlc_content As String) As Generic.List(Of String)
        Dim values As New NameValueCollection()
        values.Add("content", dlc_content)
        Dim res As Conexion.Respuesta = Conexion.SendPOST("http://dcrypt.it/decrypt/paste", values, SendAppId:=False)

        If res.Excepcion IsNot Nothing Then Throw res.Excepcion
        If Not String.IsNullOrEmpty(res.Mensaje) AndAlso res.Mensaje.Contains("container is corrupted") Then Throw New ApplicationException("DLC corrupted")


        Dim l As Generic.List(Of String) = URLExtractor.ExtraerSoloURLsOficiales(res.Mensaje)
        For Each Url As String In l
            Dim FileID As String = URLExtractor.ExtraerFileID(Url)
            Dim FileKey As String = URLExtractor.ExtraerFileKey(Url)
            If Not String.IsNullOrEmpty(FileID) Then
                Dim NewUrl As String = URLExtractor.GenerateEncodedURILink(FileID, FileKey, URLExtractor.IsMegaFolder(Url), False)
                res.Mensaje = res.Mensaje.Replace(Url, NewUrl)
            End If
        Next

        Return URLExtractor.ExtraerURLs(res.Mensaje)

    End Function

End Class
