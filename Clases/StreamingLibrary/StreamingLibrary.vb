Imports System.IO
Imports System.Xml

Public Class StreamingLibrary

    Private _LibraryElementList As List(Of LibraryElement)
    Private _NextID As Integer


    Public Sub New()
        Init()
    End Sub

    Public Sub Init()
        _LibraryElementList = New List(Of LibraryElement)
        _NextID = 1
    End Sub

    Public Function Elements() As List(Of LibraryElement)
        Return _LibraryElementList
    End Function

    Public Function GetIDandIncrement() As Integer
        Dim n As Integer = _NextID
        _NextID += 1
        Return n
    End Function

    Public Sub LoadXML()
        Dim Fichero As String = ObtenerRutaFicheroConfiguracion()

        If Not System.IO.File.Exists(Fichero) Then
            Exit Sub
        End If


        Dim Xml As New XmlDocument
        Mutex.GuardarConfig.WaitOne()
        Try
            Xml.Load(Fichero)
        Catch ex As Exception
            Log.WriteError("Error loading streaming library: " & ex.ToString)
            Exit Sub
        Finally
            Mutex.GuardarConfig.ReleaseMutex()
        End Try

        Init()

        If Xml.DocumentElement.SelectSingleNode("Elements") IsNot Nothing AndAlso _
           Xml.DocumentElement.SelectSingleNode("Elements").Attributes("nextID") IsNot Nothing AndAlso _
           IsNumeric(Xml.DocumentElement.SelectSingleNode("Elements").Attributes("nextID").Value) Then
            Me._NextID = CInt(Xml.DocumentElement.SelectSingleNode("Elements").Attributes("nextID").Value)
        End If

        For Each eleNode As XmlNode In Xml.DocumentElement.SelectNodes("Elements/Element")
            Dim Ele As New LibraryElement
            Ele.LoadXML(eleNode, False)
            Me._LibraryElementList.Add(Ele)
        Next

    End Sub


    Public Sub SaveXML()

        Dim Xml As New XmlDocument
        Dim Root As XmlNode = Xml.AppendChild(Xml.CreateElement("XML"))

        Dim ElementList As XmlNode = Root.AppendChild(Xml.CreateElement("Elements"))
        ElementList.Attributes.Append(Xml.CreateAttribute("nextID")).Value = _NextID.ToString

        For Each element As LibraryElement In Elements()
            element.SaveXML(ElementList, False)
        Next

        Dim Fichero As String = ObtenerRutaFicheroConfiguracion()

        Mutex.GuardarConfig.WaitOne()
        Try
            Xml.Save(Fichero)
        Catch ex As Exception
            Log.WriteError("Error saving streaming library: " & ex.ToString)
        Finally
            Mutex.GuardarConfig.ReleaseMutex()
        End Try

    End Sub

    Private Shared Function ObtenerRutaFicheroConfiguracion() As String

        Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Library")

        If Not System.IO.Directory.Exists(PathLog) Then
            System.IO.Directory.CreateDirectory(PathLog)
        End If
        PathLog = Path.Combine(PathLog, "StreamingLibrary.xml")
        Return PathLog
    End Function


End Class