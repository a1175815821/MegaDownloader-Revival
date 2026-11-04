Imports System.IO
Imports System.Xml

Public Class StreamingLibrary

    Private _LibraryElementList As List(Of LibraryElement)
    Private _NextID As Integer
    Private ReadOnly _sync As New Object
    Private ReadOnly _loadSync As New Object
    Private ReadOnly _saveSync As New Object
    Private ReadOnly _sourcePath As String
    Private _transientBlockedSource As String
    ''' <summary>加载时文件被瞬时占用(杀软/备份/同步盘)而不是真的损坏。磁盘上的库可能
    ''' 完好,此时内存里却是空库 —— 任何写盘都会用"空库+新增一条"覆盖掉用户的整库,
    ''' 原件还会被 File.Replace 挤进 .bak,下次保存连 .bak 一起毁掉。
    ''' 置位后 SaveXML 一律拒写,保住磁盘文件(与本轮 Configuracion/Paquete 的 P0-3 同策略)。</summary>
    Private _LoadTransientError As Boolean


    Public Sub New()
        Me.New(Nothing)
    End Sub

    Friend Sub New(ByVal sourcePath As String)
        _sourcePath = If(sourcePath Is Nothing, Nothing, Path.GetFullPath(sourcePath))
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

    ' Elements() deliberately hands back a copy, so mutating through it is a silent no-op
    ' that SaveXML would then persist as "nothing changed". Callers must use these instead.
    Public Sub AddElement(ByVal element As LibraryElement)
        SyncLock _sync
            _LibraryElementList.Add(element)
        End SyncLock
    End Sub

    Public Sub AddNewElement(ByVal element As LibraryElement)
        SyncLock _sync
            element.ID = _NextID.ToString()
            _NextID += 1
            _LibraryElementList.Add(element)
        End SyncLock
    End Sub

    Public Function RemoveElement(ByVal element As LibraryElement) As Boolean
        SyncLock _sync
            Return _LibraryElementList.Remove(element)
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
        SyncLock _loadSync
            Dim filePath As String = If(_sourcePath, ObtenerRutaFicheroConfiguracion())
            Dim document As XmlDocument = Nothing
            Dim recovered As Boolean = False
            Dim transientFailure As Boolean = False
            SyncLock Mutex.GuardarConfig
                If Not AtomicFile.TryLoadXmlWithRecoveryInfo(filePath, document, recovered, transientFailure, _transientBlockedSource, AtomicFile.StartupAttempts, AtomicFile.StartupRetryDelayMs) Then
                    SyncLock _sync
                        System.Threading.Volatile.Write(_LoadTransientError, transientFailure)
                    End SyncLock
                    If transientFailure Then Log.WriteError("Streaming library is locked; writes are suspended: " & filePath)
                    Return
                End If
            End SyncLock
            If recovered Then
                Log.WriteWarning("Streaming library restored from backup.")
            End If
            Dim nextID As Integer
            Dim elements As List(Of LibraryElement) = ParseLibrary(document, nextID)
            SyncLock _sync
                _LibraryElementList = elements
                _NextID = nextID
                System.Threading.Volatile.Write(_LoadTransientError, False)
            End SyncLock
        End SyncLock
    End Sub

    Private Shared Function ParseLibrary(document As XmlDocument, ByRef nextID As Integer) As List(Of LibraryElement)
        Dim elements As New List(Of LibraryElement)
        nextID = 1
        Dim idAttribute As XmlNode = document.DocumentElement.SelectSingleNode("Elements/@nextID")
        If idAttribute IsNot Nothing AndAlso IsNumeric(idAttribute.Value) Then nextID = CInt(idAttribute.Value)
        For Each node As XmlNode In document.DocumentElement.SelectNodes("Elements/Element")
            Dim element As New LibraryElement
            element.LoadXML(node, False)
            elements.Add(element)
        Next
        Return elements
    End Function

    ''' <summary>B2: 主窗体恢复计时器用来判断媒体库是否仍处于加载降级状态。</summary>
    Public ReadOnly Property LoadTransientError As Boolean
        Get
            Return System.Threading.Volatile.Read(_LoadTransientError)
        End Get
    End Property

    Public Function TryRecoverFromTransientLock() As Boolean
        Return RecoverUsing(Function()
                                Dim document As XmlDocument = Nothing
                                Dim filePath As String = If(_sourcePath, ObtenerRutaFicheroConfiguracion())
                                AtomicFile.TryReloadAfterTransientFailure(filePath, _transientBlockedSource, document)
                                Return document
                            End Function)
    End Function

    Friend Function RecoverUsing(loadDocument As Func(Of XmlDocument)) As Boolean
        SyncLock _loadSync
            If Not LoadTransientError Then Return False
            Try
                Dim document As XmlDocument = loadDocument()
                If document Is Nothing Then Return False
                Dim nextID As Integer
                Dim elements As List(Of LibraryElement) = ParseLibrary(document, nextID)
                ' Merge the current live list at commit time. HTTP requests can add
                ' entries while the persisted document is being read and parsed.
                SyncLock _sync
                    For Each element As LibraryElement In _LibraryElementList
                        element.ID = nextID.ToString()
                        nextID += 1
                        elements.Add(element)
                    Next
                    _LibraryElementList = elements
                    _NextID = nextID
                    _transientBlockedSource = Nothing
                    System.Threading.Volatile.Write(_LoadTransientError, False)
                End SyncLock
            Catch ex As Exception
                Log.WriteError("Streaming library recovery failed: " & Log.SafeException(ex))
                Return False
            End Try
            SaveXML()
            Log.WriteInfo("Streaming library recovered after the on-disk file was unlocked.")
            Return True
        End SyncLock
    End Function

    Public Sub SaveXML()
        ' Serialize writers before taking a snapshot. A save prepared during
        ' recovery must never overwrite the merged collection with an older view.
        SyncLock _saveSync
            Dim document As New XmlDocument
            Dim root As XmlNode = document.AppendChild(document.CreateElement("XML"))
            Dim elementList As XmlNode = root.AppendChild(document.CreateElement("Elements"))
            SyncLock _sync
                If _LoadTransientError Then
                    Log.WriteWarning("Skipping streaming library save: the persisted library is still locked.")
                    Return
                End If
                elementList.Attributes.Append(document.CreateAttribute("nextID")).Value = _NextID.ToString()
                For Each element As LibraryElement In _LibraryElementList
                    element.SaveXML(elementList, False)
                Next
            End SyncLock
            SyncLock Mutex.GuardarConfig
                Try
                    AtomicFile.SaveXml(document, If(_sourcePath, ObtenerRutaFicheroConfiguracion()))
                Catch ex As Exception
                    Log.WriteError("Error saving streaming library: " & Log.SafeException(ex))
                End Try
            End SyncLock
        End SyncLock
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
