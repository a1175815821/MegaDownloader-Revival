Imports System.IO
Imports System.Xml
Imports System.Security

Public Class LibraryElement


    Public Const HIDDEN_LINK As String = "{HIDDEN}"
    Public Const HIDDEN_LINK_DESC As String = "** LINK NOT VISIBLE **"

    Public ID As String
    Public Name As String
    Public Description As String
    Public Comments As String
    Public Poster As String
    Public LastModification As Date
    Public Link As SecureString
    Public LinkVisible As Boolean

    ' Movie info
    Public IMDB As String
    Public Allocine As String
    Public Filmaffinity As String


    Public Sub New()
        Me.LastModification = Now
    End Sub

    Public Function ToJSON(CurrentURL As String, ByRef Config As Configuracion) As String
        Dim str As New System.Text.StringBuilder
        str.Append("{")
        str.Append("""ID"":")
        str.Append("""" & (ID & "").Replace("""", "\""") & """,")
        str.Append("""Name"":")
        str.Append("""" & (Name & "").Replace("""", "\""") & """,")
        str.Append("""Desc"":")
        str.Append("""" & (Description & "").Replace("""", "\""") & """,")
        str.Append("""Com"":")
        str.Append("""" & (Comments & "").Replace("""", "\""") & """,")
        str.Append("""Poster"":")
        str.Append("""" & (Poster & "").Replace("""", "\""") & """,")
        str.Append("""IMDB"":")
        str.Append("""" & (IMDB & "").Replace("""", "\""") & """,")
        str.Append("""Filmaffinity"":")
        str.Append("""" & (Filmaffinity & "").Replace("""", "\""") & """,")
        str.Append("""Allocine"":")
        str.Append("""" & (Allocine & "").Replace("""", "\""") & """,")
        str.Append("""Date"":")
        str.Append("""" & LastModification.ToString("yyyy-MM-dd HH:mm") & """,")
        str.Append("""Link"":")
        Dim linkStr As String = HIDDEN_LINK_DESC
        If LinkVisible Then
            linkStr = Criptografia.ToInsecureString(Link)
        End If
        str.Append("""" & linkStr & """,")
        str.Append("""VlcLink"":")
        str.Append("""" & StreamingHelper.CreateStreamingLinkFromLibrary(ID, CurrentURL, Config).Replace("""", "\""") & """")
        str.Append("}")
        Return str.ToString
    End Function


    Public Sub LoadXML(ByVal XML As XmlNode, Import As Boolean)
        ID = LeerNodo(XML, "ID", "")
        Name = LeerNodo(XML, "Name", "")
        Description = LeerNodo(XML, "Desc", "")
        Comments = LeerNodo(XML, "Com", "")
        Poster = LeerNodo(XML, "Post", "")
        IMDB = LeerNodo(XML, "IMDB", "")
        Filmaffinity = LeerNodo(XML, "Filmaffinity", "")
        Allocine = LeerNodo(XML, "Allocine", "")
        Dim str As String = LeerNodo(XML, "Link", "")
        If Not String.IsNullOrEmpty(str) Then
            If Import Then
                str = Criptografia.AES_DecryptString(str, ExportPassword)
            Else
                Link = Criptografia.DecryptString_DPAPI(str)
                str = Criptografia.ToInsecureString(Link)
            End If

            LinkVisible = Not str.StartsWith(HIDDEN_LINK)
            str = str.Replace(HIDDEN_LINK, "")
            Link = Criptografia.ToSecureString(str)
        Else
            Link = New SecureString()
        End If

        Me.LastModification = Date.MinValue
        str = LeerNodo(XML, "Date", "")
        If IsDate(str) Then
            Me.LastModification = CDate(str)
        End If
    End Sub

    Private Function LeerNodo(ByRef NodoXML As XmlNode, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = NodoXML.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function

    Public Sub SaveXML(ByRef XML As XmlNode, Export As Boolean)

        Dim ElementNode As XmlNode = XML.AppendChild(XML.OwnerDocument.CreateElement("Element"))
        If Not Export Then
            ElementNode.AppendChild(XML.OwnerDocument.CreateElement("ID")).InnerText = ID
            ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Date")).InnerText = LastModification.ToString("s")
        End If

        If Not String.IsNullOrEmpty(Name) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Name")).InnerText = Name
        If Not String.IsNullOrEmpty(Description) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Desc")).InnerText = Description
        If Not String.IsNullOrEmpty(Comments) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Com")).InnerText = Comments
        If Not String.IsNullOrEmpty(Poster) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Post")).InnerText = Poster
        If Not String.IsNullOrEmpty(IMDB) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("IMDB")).InnerText = IMDB
        If Not String.IsNullOrEmpty(Filmaffinity) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Filmaffinity")).InnerText = Filmaffinity
        If Not String.IsNullOrEmpty(Allocine) Then ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Allocine")).InnerText = Allocine

        Dim link2 As String = If(LinkVisible, "", HIDDEN_LINK) & Criptografia.ToInsecureString(Link)
        If Export Then
            ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Link")).InnerText = Criptografia.AES_EncryptString(link2, ExportPassword)
        Else
            Dim link3 As SecureString = Criptografia.ToSecureString(link2)
            ElementNode.AppendChild(XML.OwnerDocument.CreateElement("Link")).InnerText = Criptografia.EncryptString_DPAPI(link3)
        End If


    End Sub

    Private Const ExportPassword As String = "ae7}Kazdje/twiev"

End Class