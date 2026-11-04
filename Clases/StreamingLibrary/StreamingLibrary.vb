Imports System.IO
Imports System.Xml

Public Class StreamingLibrary

    Private _LibraryElementList As List(Of LibraryElement)
    Private _NextID As Integer
    Private ReadOnly _sync As New Object
    ''' <summary>加载时文件被瞬时占用(杀软/备份/同步盘)而不是真的损坏。磁盘上的库可能
    ''' 完好,此时内存里却是空库 —— 任何写盘都会用"空库+新增一条"覆盖掉用户的整库,
    ''' 原件还会被 File.Replace 挤进 .bak,下次保存连 .bak 一起毁掉。
    ''' 置位后 SaveXML 一律拒写,保住磁盘文件(与本轮 Configuracion/Paquete 的 P0-3 同策略)。</summary>
    Private _LoadTransientError As Boolean


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

    ' Elements() deliberately hands back a copy, so mutating through it is a silent no-op
    ' that SaveXML would then persist as "nothing changed". Callers must use these instead.
    Public Sub AddElement(ByVal element As LibraryElement)
        SyncLock _sync
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

    ''' <summary>公开的无参入口保持不变。</summary>
    Public Sub LoadXML()
        LoadXMLInterno(False)
    End Sub

    ''' <summary>soloRecuperar=True 时只从主文件重载(B2 自动恢复),不读 .bak。</summary>
    Private Sub LoadXMLInterno(ByVal soloRecuperar As Boolean)
        Dim Fichero As String = ObtenerRutaFicheroConfiguracion()

        Dim Xml As XmlDocument = Nothing
        Dim recovered As Boolean = False
        Dim transientFailure As Boolean = False

        If soloRecuperar Then
            If Not _LoadTransientError Then Return
            If Not AtomicFile.TryReloadPrimary(Fichero, Xml) Then Return
        End If

        If Not soloRecuperar Then
        SyncLock Mutex.GuardarConfig
            If Not AtomicFile.TryLoadXmlWithRetries(Fichero, Xml, recovered, transientFailure, AtomicFile.StartupAttempts, AtomicFile.StartupRetryDelayMs) Then
                ' 瞬时占用≠损坏:磁盘上的库可能完好,内存却是空库。禁写,别让一次"加片"
                ' 用空库覆盖掉整库(见 _LoadTransientError 声明处)。
                If transientFailure Then
                    _LoadTransientError = True
                    Log.WriteError("Streaming library is locked by another process (transient). Running with an empty library; writes are suspended to preserve the on-disk file: " & Fichero)
                End If
                Exit Sub
            End If
            If recovered Then
                Log.WriteWarning("Streaming library restored from backup.")
            End If
        End SyncLock
        End If

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

        If soloRecuperar Then
            _LoadTransientError = False
            Log.WriteInfo("Streaming library recovered after the on-disk file was unlocked.")
        End If

    End Sub

    ''' <summary>B2: 主窗体恢复计时器用来判断媒体库是否仍处于加载降级状态。</summary>
    Public ReadOnly Property LoadTransientError As Boolean
        Get
            Return _LoadTransientError
        End Get
    End Property

    ''' <summary>B2: 占用解除后重读主文件并解除写保护。降级期间用户新增的条目会
    ''' 重新分配 ID 后合并回来并立即落盘,不会被恢复动作静默丢弃。</summary>
    Public Function TryRecoverFromTransientLock() As Boolean
        If Not _LoadTransientError Then Return False
        Dim pending As List(Of LibraryElement) = Elements()
        Try
            LoadXMLInterno(True)
        Catch ex As Exception
            Log.WriteError("Streaming library recovery failed: " & Log.SafeException(ex))
            Return False
        End Try
        If _LoadTransientError Then Return False
        If pending.Count > 0 Then
            SyncLock _sync
                For Each ele As LibraryElement In pending
                    ele.ID = _NextID.ToString()
                    _NextID += 1
                    _LibraryElementList.Add(ele)
                Next
            End SyncLock
            SaveXML()
        End If
        Return True
    End Function


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

        ' B2: 加载期被瞬时占用 → 内存是空库,写盘会清空用户整库。
        ' 不再依赖 File.Exists,文件被移走时保护仍须生效。
        If _LoadTransientError Then
            Log.WriteWarning("Skipping streaming library save: the on-disk file could not be loaded earlier (locked). It is preserved untouched.")
            Exit Sub
        End If

        SyncLock Mutex.GuardarConfig
        Try
            AtomicFile.SaveXml(Xml, Fichero)
        Catch ex As Exception
            Log.WriteError("Error saving streaming library: " & Log.SafeException(ex))
        End Try
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