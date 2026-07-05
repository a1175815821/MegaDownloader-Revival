Imports System.Xml

Public Class ConfiguracionUI

    Public AnchoVentanaPrincipal As Integer ' 0 = por defecto
    Public AltoVentanaPrincipal As Integer ' 0 = por defecto
    Public EstadoLista() As Byte
    Public RutaSkin As String


    Public Sub ConfiguracionDefectoVacia()
        AnchoVentanaPrincipal = 0
        AltoVentanaPrincipal = 0
    End Sub


    Public Sub CargarXML(ByVal XML As XmlDocument)
        AnchoVentanaPrincipal = 0
        Integer.TryParse(LeerNodo(XML, "ConfigUI/AnchoVentanaPrincipal", "0"), AnchoVentanaPrincipal)
        AltoVentanaPrincipal = 0
        Integer.TryParse(LeerNodo(XML, "ConfigUI/AltoVentanaPrincipal", "0"), AltoVentanaPrincipal)

        RutaSkin = LeerNodo(XML, "ConfigUI/RutaSkin", "")
        Dim strEstadoLista As String = LeerNodo(XML, "ConfigUI/ConfigListaDescargas", "")

        Try
            Me.EstadoLista = System.Convert.FromBase64String(strEstadoLista)
        Catch ex As Exception
            Me.EstadoLista = Nothing
        End Try
    End Sub

    Private Function LeerNodo(ByRef DocumentoXML As XmlDocument, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = DocumentoXML.DocumentElement.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function

    Public Sub GuardarXML(ByRef XML As XmlDocument)

        Dim strEstadoLista As String = ""
        If EstadoLista IsNot Nothing Then
            strEstadoLista = System.Convert.ToBase64String(EstadoLista)
        End If

        Dim NodoUI As XmlNode = XML.DocumentElement.AppendChild(XML.CreateElement("ConfigUI"))

        NodoUI.AppendChild(XML.CreateElement("AnchoVentanaPrincipal")).InnerText = AnchoVentanaPrincipal.ToString
        NodoUI.AppendChild(XML.CreateElement("AltoVentanaPrincipal")).InnerText = AltoVentanaPrincipal.ToString
        NodoUI.AppendChild(XML.CreateElement("ConfigListaDescargas")).InnerText = strEstadoLista
        NodoUI.AppendChild(XML.CreateElement("RutaSkin")).InnerText = RutaSkin

    End Sub
End Class
