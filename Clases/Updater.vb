Imports System.Xml
Imports Microsoft.Win32

Public Class Updater

    Private Enum Version
        Binary ' Just the EXE
        Installer ' Installer
        MSD ' Mega Search Desktop - installer
    End Enum

    Public Shared Sub ComprobarVersionMegadownloader(ByRef UrlNuevaVersion As String, ByRef Version As String)
        If String.IsNullOrEmpty(Conexion.GetUpdateCheckURL) Then Exit Sub
        Dim URL As String = Conexion.GetUpdateCheckURL()
        Dim Resultado As Conexion.Respuesta = Conexion.LeerURL(URL)
        If Resultado.Excepcion Is Nothing Then
            Dim XML As New XmlDocument
            Try
                XML.LoadXml(Resultado.Mensaje)
            Catch ex As Exception
                Log.WriteError("Error loading the version check XML: " & ex.ToString)
                Exit Sub
            End Try
            Dim UltimaVersion As System.Version = Nothing
            Dim VersionActual As System.Version = Nothing

            Dim ultimaVersionStr As String = NormalizeVersionString(LeerNodo(XML, "Version", ""))
            Dim actualVersionStr As String = NormalizeVersionString(InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_UPDATE"))

            If System.Version.TryParse(ultimaVersionStr, UltimaVersion) AndAlso _
               System.Version.TryParse(actualVersionStr, VersionActual) Then
                If UltimaVersion > VersionActual Then


                    Dim NodeToCheck As String = "Link"
                    Select Case GetVersion()
                        Case Updater.Version.MSD
                            NodeToCheck = "LinkMSD"
                        Case Updater.Version.Installer
                            NodeToCheck = "LinkInstaller"
                        Case Else
                            NodeToCheck = "Link"
                    End Select

                    UrlNuevaVersion = LeerNodo(XML, NodeToCheck, "")
                    If String.IsNullOrEmpty(UrlNuevaVersion) Then
                        UrlNuevaVersion = LeerNodo(XML, "Link", "")
                    End If

                    Version = LeerNodo(XML, "Version", "")
                    Log.WriteInfo("There is a new version of MegaDownloader: " & Version & " - " & UrlNuevaVersion)
                End If
            End If
        End If
    End Sub

    Private Shared Function GetVersion() As Version
#If MSD Then
        Return Version.MSD
#Else
        Try
            Dim rKey As RegistryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\MegaDownloader", False)
            If rKey IsNot Nothing _
               AndAlso rKey.GetValue("Installer") IsNot Nothing _
               AndAlso CStr(rKey.GetValue("Installer")) = "1" Then
                Return Version.Installer
            Else
                Return Version.Binary
            End If
        Catch ex As Security.SecurityException
            Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry. Installation check not possible, assume binaries.")
            Return Version.Binary

        Catch ex As Exception
            Log.WriteError("Error accessing the registry for checking installation. Error: " & ex.ToString)
            Return Version.Binary

        End Try
#End If
    End Function

    Private Shared Function LeerNodo(ByRef DocumentoXML As XmlDocument, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = DocumentoXML.DocumentElement.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function

    ''' <summary>
    ''' Normalizes "2.0" and "2.0.0.0" to the same four-part version string for comparisons.
    ''' </summary>
    Private Shared Function NormalizeVersionString(ByVal value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "0.0.0.0"
        Dim parts = value.Trim().Split("."c).ToList()
        While parts.Count < 4
            parts.Add("0")
        End While
        If parts.Count > 4 Then parts = parts.Take(4).ToList()
        For i As Integer = 0 To parts.Count - 1
            Dim n As Integer
            If Not Integer.TryParse(parts(i), n) OrElse n < 0 Then parts(i) = "0"
        Next
        Return String.Join(".", parts)
    End Function
End Class
