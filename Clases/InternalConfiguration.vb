Imports System.Xml

Public Class InternalConfiguration

    Private Shared XML_CONFIG As XmlDocument

    Public Shared Function ObtenerValueFromInternalConfig(Key As String) As String
        Try
            Dim XmlConfig As XmlDocument = XML_CONFIG

            If XmlConfig Is Nothing Then

                XmlConfig = GetXmlConfig()
                XML_CONFIG = XmlConfig

            End If

            Dim Nodo As XmlNode = XmlConfig.DocumentElement.SelectSingleNode(Key)
            If Nodo Is Nothing Then Return ""
            Return Nodo.InnerText

        Catch ex As Exception
            Throw
        End Try
    End Function

    Public Shared Function ObtenerValuesFromInternalConfig(Key As String) As Generic.List(Of KeyValuePair(Of String, String))
        Try
            Dim XmlConfig As XmlDocument = XML_CONFIG

            If XmlConfig Is Nothing Then

                XmlConfig = GetXmlConfig()
                XML_CONFIG = XmlConfig

            End If


            Dim l As New Generic.List(Of KeyValuePair(Of String, String))
            For Each n As XmlNode In XmlConfig.DocumentElement.SelectNodes(Key)
                If n IsNot Nothing AndAlso n.Attributes("key") IsNot Nothing Then
                    Dim k As New KeyValuePair(Of String, String)(n.Attributes("key").Value, n.InnerText)
                    l.Add(k)
                End If
            Next
            Return l

        Catch ex As Exception
            Throw
        End Try
    End Function

    Private Shared Function GetXmlConfig() As XmlDocument
        Dim ResName As String = ResourceHelper.GetResourceName(".InternalConfig.xml")
        If String.IsNullOrEmpty(ResName) Then
            Throw New ApplicationException("InternalConfig could not be loaded")
        End If
        Dim file As System.IO.Stream = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(ResName)

        Dim xml As String = String.Empty
        Using reader As New IO.StreamReader(file)
            xml = reader.ReadToEnd()
        End Using

        If Not xml.StartsWith("<") Then
            xml = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(xml))
        End If

        Dim XmlConfig As New XmlDocument
        XmlConfig.LoadXml(xml)
        Return XmlConfig
    End Function

 

    Public Shared Function ObtenerNombreApp() As String
#If MSD Then
        Return ObtenerValueFromInternalConfig("TITULO_MAIN_MSD")
#Else
        Return ObtenerValueFromInternalConfig("TITULO_MAIN")
#End If
    End Function
End Class
