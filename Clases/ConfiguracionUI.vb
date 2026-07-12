Imports System.Xml

Public Class ConfiguracionUI

    Public Enum ThemeModeType
        Auto    ' 跟随系统
        Light   ' 浅色
        Dark    ' 深色
    End Enum

    Public AnchoVentanaPrincipal As Integer ' 0 = por defecto
    Public AltoVentanaPrincipal As Integer ' 0 = por defecto
    Public EstadoLista() As Byte
    Public RutaSkin As String

    Public Tema As ThemeModeType = ThemeModeType.Auto


    Public Sub ConfiguracionDefectoVacia()
        AnchoVentanaPrincipal = 0
        AltoVentanaPrincipal = 0
        Tema = ThemeModeType.Auto
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

        ' 主题模式:旧配置文件无此节点时默认 Auto
        Tema = ThemeModeType.Auto
        Select Case LeerNodo(XML, "ConfigUI/Tema", "Auto")
            Case "Light"
                Tema = ThemeModeType.Light
            Case "Dark"
                Tema = ThemeModeType.Dark
            Case Else
                Tema = ThemeModeType.Auto
        End Select
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
        NodoUI.AppendChild(XML.CreateElement("Tema")).InnerText = Tema.ToString

    End Sub
End Class
