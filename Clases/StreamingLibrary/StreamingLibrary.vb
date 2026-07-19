Imports System.IO
Imports System.Xml

Public Class StreamingLibrary

    Private _LibraryElementList As List(Of LibraryElement)
    Private _NextID As Integer
    Private ReadOnly _sync As New Object


    Public Sub New()
        Init()
    End Sub

    Public Sub Init()
        SyncLock _sync
            _LibraryElementList = New List(Of LibraryElement)
            _NextID = 1
        End SyncLock
    End Sub

    Public Function Elements() As List(Of LibraryElement)
        SyncLock _sync
            Return New List(Of LibraryElement)(_LibraryElementList)
        End SyncLock
    End Function

    Public Function GetIDandIncrement() As Integer
        SyncLock _sync
            Dim n As Integer = _NextID
            _NextID += 1
            Return n
        End SyncLock
    End Function

    Public Sub LoadXML()
        Dim Fichero As String = ObtenerRutaFicheroConfiguracion()

        Dim Xml As XmlDocument = Nothing
        Dim recovered As Boolean = False
        Mutex.GuardarConfig.WaitOne()
        Try
            If Not AtomicFile.TryLoadXml(Fichero, Xml, recovered) Then
                Exit Sub
            End If
            If recovered Then
                Log.WriteWarning("Streaming library restored from backup.")
            End If
        Finally
            Mutex.GuardarConfig.ReleaseMutex()
        End Try

        SyncLock _sync
            _LibraryElementList = New List(Of LibraryElement)
            _NextID = 1

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
        End SyncLock

    End Sub


    Public Sub SaveXML()
        Dim Xml As New XmlDocument
        Dim Root As XmlNode = Xml.AppendChild(Xml.CreateElement("XML"))

        Dim ElementList As XmlNode = Root.AppendChild(Xml.CreateElement("Elements"))
        SyncLock _sync
            ElementList.Attributes.Append(Xml.CreateAttribute("nextID")).Value = _NextID.ToString
            For Each element As LibraryElement In _LibraryElementList
                element.SaveXML(ElementList, False)
            Next
        End SyncLock

        Dim Fichero As String = ObtenerRutaFicheroConfiguracion()

        Mutex.GuardarConfig.WaitOne()
        Try
            AtomicFile.SaveXml(Xml, Fichero)
        Catch ex As Exception
            Log.WriteError("Error saving streaming library: " & Log.SafeException(ex))
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