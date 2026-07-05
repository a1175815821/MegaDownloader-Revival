' Dejamos de usarla

'Public Class Serializer


'    Private Shared htCache As Generic.Dictionary(Of String, Xml.Serialization.XmlSerializer)

'    Private Shared Function GetSerializer(ByVal Tipo As Type) As Xml.Serialization.XmlSerializer
'        ' Cacheamos la instancia al xmlserializer, ya que cada nueva instancia
'        ' puede tardar hasta 100ms en crearse!!
'        ' http://robseder.wordpress.com/2010/03/18/the-deal-with-xmlserializer-being-so-slow-%E2%80%93-finally/


'        If htCache Is Nothing Then
'            htCache = New Generic.Dictionary(Of String, Xml.Serialization.XmlSerializer)
'        End If


'        Dim keyCache As String = Tipo.FullName

'        If htCache.ContainsKey(keyCache) Then
'            Return htCache(keyCache)
'        Else
'            Dim ser As New Xml.Serialization.XmlSerializer(Tipo)
'            htCache(keyCache) = ser
'            Return ser
'        End If
'    End Function

'    Public Shared Function SerializarObjeto(ByVal obj As Object) As String
'        Dim s As Xml.Serialization.XmlSerializer = GetSerializer(obj.GetType)
'        Dim w As IO.StringWriter = Nothing
'        Try
'            w = New IO.StringWriter()
'            s.Serialize(w, obj)

'            Return w.ToString
'        Finally
'            If w IsNot Nothing Then
'                w.Close()
'                w.Dispose()
'            End If
'        End Try
'    End Function

'    Public Shared Function DeserializarObjeto(Of T)(ByVal obj As String) As T
'        Dim s As Xml.Serialization.XmlSerializer = GetSerializer(GetType(T))
'        Dim w As IO.StringReader = Nothing
'        Try
'            w = New IO.StringReader(obj)
'            Return CType(s.Deserialize(w), T)
'        Finally
'            If w IsNot Nothing Then
'                w.Close()
'                w.Dispose()
'            End If
'        End Try
'    End Function

'    Public Shared Sub SerializarFichero(ByVal obj As Object, ByVal ruta As String)
'        Dim w As IO.StreamWriter = Nothing
'        Dim s As Xml.Serialization.XmlSerializer = GetSerializer(obj.GetType)
'        Try
'            w = New IO.StreamWriter(ruta)
'            s.Serialize(w, obj)
'            w.Flush()
'        Finally
'            If w IsNot Nothing Then
'                w.Close()
'                w.Dispose()
'            End If
'        End Try
'    End Sub

'    Public Shared Function DeserializarDesdeFichero(Of T)(ByVal ruta As String) As T
'        Dim s As Xml.Serialization.XmlSerializer = GetSerializer(GetType(T))

'        Dim r As New IO.StreamReader(ruta)
'        Try
'            Return CType(s.Deserialize(r), T)
'        Finally
'            r.Close()
'        End Try
'    End Function
'End Class
