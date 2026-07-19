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
        Dim linkStr As String = HIDDEN_LINK_DESC
        If LinkVisible Then
            linkStr = Criptografia.ToInsecureString(Link)
        End If
        Dim vlc As String = StreamingHelper.CreateStreamingLinkFromLibrary(ID, CurrentURL, Config)
        Dim str As New System.Text.StringBuilder
        str.Append("{")
        str.Append("""ID"":").Append(JsonString(ID)).Append(","c)
        str.Append("""Name"":").Append(JsonString(Name)).Append(","c)
        str.Append("""Desc"":").Append(JsonString(Description)).Append(","c)
        str.Append("""Com"":").Append(JsonString(Comments)).Append(","c)
        str.Append("""Poster"":").Append(JsonString(Poster)).Append(","c)
        str.Append("""IMDB"":").Append(JsonString(IMDB)).Append(","c)
        str.Append("""Filmaffinity"":").Append(JsonString(Filmaffinity)).Append(","c)
        str.Append("""Allocine"":").Append(JsonString(Allocine)).Append(","c)
        str.Append("""Date"":").Append(JsonString(LastModification.ToString("yyyy-MM-dd HH:mm"))).Append(","c)
        str.Append("""Link"":").Append(JsonString(linkStr)).Append(","c)
        str.Append("""VlcLink"":").Append(JsonString(vlc))
        str.Append("}")
        Return str.ToString
    End Function

    Friend Shared Function JsonString(ByVal value As String) As String
        If value Is Nothing Then value = ""
        Dim sb As New System.Text.StringBuilder(value.Length + 2)
        sb.Append(""""c)
        For Each ch As Char In value
            Select Case ch
                Case """"c
                    sb.Append("\""")
                Case "\"c
                    sb.Append("\\")
                Case "/"c
                    sb.Append("\/")
                Case ChrW(8)
                    sb.Append("\b")
                Case ChrW(12)
                    sb.Append("\f")
                Case ChrW(10)
                    sb.Append("\n")
                Case ChrW(13)
                    sb.Append("\r")
                Case ChrW(9)
                    sb.Append("\t")
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("\u").Append(AscW(ch).ToString("x4"))
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next
        sb.Append(""""c)
        Return sb.ToString()
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