Imports System.Xml
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Public Class Paquete
    Implements IDescarga

    Public Sub New()
    End Sub

    Public Nombre As String

    Public RutaLocal As String

	Public CrearSubdirectorio As Boolean

	Public PendienteNombrePaquete As Boolean

    Public ListaFicheros As Generic.List(Of Fichero)

    Public ExtraccionFicheroAutomatica As Boolean

    Public ExtraccionFicheroPassword As String

    Public Prioridad As Integer

    Private EstadoDescarga As Estado

    Public Porcentaje As Decimal

    Public TamanoBytes As Long

    Private VelocidadKBs As Decimal

    Private TiempoEstimadoDescarga As String

    Private Function HayFicheros() As Boolean
        SyncLock Mutex.ListaDescargas
            Return ListaFicheros IsNot Nothing AndAlso ListaFicheros.Count > 0
        End SyncLock
    End Function

    Public Sub AgregarFichero(ByVal Fichero As Fichero)
        SyncLock Mutex.ListaDescargas
            If ListaFicheros Is Nothing Then
                ListaFicheros = New Generic.List(Of Fichero)
            End If
            ListaFicheros.Add(Fichero)
        End SyncLock
    End Sub

    ''' <summary>
    ''' 返回本包文件引用的快照，供 430ms 后台刷新循环使用：短锁拷贝后，
    ''' 逐文件的重活（Downloader 属性读取、ETA 格式化）在锁外执行，
    ''' 不再把全局 ListaDescargas 锁 holds 住几百个文件的全程。
    ''' 快照与实时列表最多差一个刷新周期，计数类 UI 本就按节拍刷新，可接受。
    ''' </summary>
    Public Function SnapshotFicheros() As Generic.List(Of Fichero)
        SyncLock Mutex.ListaDescargas
            If ListaFicheros Is Nothing Then Return New Generic.List(Of Fichero)
            Return New Generic.List(Of Fichero)(ListaFicheros)
        End SyncLock
    End Function

    Public Sub ActualizarDatosDescarga()

        If Not HayFicheros() Then
            EstadoDescarga = Estado.EnCola
            Porcentaje = 0
            TamanoBytes = 0
            VelocidadKBs = 0
        Else

            Dim TotalVelocidad As Decimal = 0
            Dim HayDescargando As Boolean = False
            Dim HayCompletado As Boolean = False
            Dim HayEncola As Boolean = False
            Dim HayErroneos As Boolean = False
            Dim HayGenerandoLocal As Boolean = False
            Dim HayPausados As Boolean = False
            Dim HayDescomprimiendo As Boolean = False
            Dim HayEsperandoVolumenes As Boolean = False
            Dim HayComprobandoMD5 As Boolean = False
            Dim HayVerificando As Boolean = False
            Dim Total As Long = 0
            Dim TotalDesc As Long = 0

            SyncLock Mutex.ListaDescargas
                For Each fichero As Fichero In ListaFicheros
                    Total += fichero.DescargaTamanoBytes
                    TotalDesc += CLng(fichero.DescargaTamanoBytes * (fichero.DescargaPorcentaje / 100))
                    Select Case fichero.DescargaEstado
                        Case Estado.Descargando
                            TotalVelocidad += fichero.DescargaVelocidadKBs
                            HayDescargando = True
                        Case Estado.Completado
                            HayCompletado = True
                        Case Estado.EnCola
                            HayEncola = True
                        Case Estado.Pausado
                            HayPausados = True
                        Case Estado.ComprobandoMD5
                            HayComprobandoMD5 = True
                        Case Estado.Descomprimiendo
                            HayDescomprimiendo = True
                        Case Estado.EsperandoVolumenes
                            HayEsperandoVolumenes = True
                        Case Estado.Verificando
                            HayVerificando = True
                        Case Estado.CreandoLocal
                            HayGenerandoLocal = True
                        Case Else
                            HayErroneos = True
                    End Select
                Next
            End SyncLock
            If HayDescargando Then
                EstadoDescarga = Estado.Descargando
            ElseIf HayPausados Then
                EstadoDescarga = Estado.Pausado
            ElseIf HayErroneos Then
                EstadoDescarga = Estado.Erroneo
            ElseIf HayGenerandoLocal Then
                EstadoDescarga = Estado.CreandoLocal
            ElseIf HayEncola Then
                EstadoDescarga = Estado.EnCola
            ElseIf HayComprobandoMD5 Then
                EstadoDescarga = Estado.ComprobandoMD5
            ElseIf HayDescomprimiendo Then
                EstadoDescarga = Estado.Descomprimiendo
            ElseIf HayEsperandoVolumenes Then
                EstadoDescarga = Estado.EsperandoVolumenes
            ElseIf HayVerificando Then
                EstadoDescarga = Estado.Verificando
            Else
                EstadoDescarga = Estado.Completado
            End If

            TamanoBytes = Total
            VelocidadKBs = Math.Round(TotalVelocidad, 2)

            If TamanoBytes = 0 Then
                Porcentaje = 0
            Else
                Porcentaje = CDec(100 * TotalDesc / Total)
            End If
        End If

        If VelocidadKBs = 0 Then
            TiempoEstimadoDescarga = " --- "
        Else
            Dim BytesRestantes As Long = TamanoBytes - CLng(Porcentaje * TamanoBytes / 100)
            Dim SegundosRestantes As Double = BytesRestantes / (1024 * VelocidadKBs)
            Dim t As TimeSpan = TimeSpan.FromSeconds(SegundosRestantes)

            If SegundosRestantes > 3600 Then
                TiempoEstimadoDescarga = String.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                                            (t.Days * 24) + t.Hours,
                                            t.Minutes,
                                            t.Seconds)
            Else
                TiempoEstimadoDescarga = String.Format("{0:D2}m:{1:D2}s",
                                           t.Minutes,
                                           t.Seconds)
            End If
        End If

    End Sub

    Public Function DescargaPrioridad() As Integer Implements IDescarga.DescargaPrioridad
        Return Me.Prioridad
    End Function

    Public WriteOnly Property SetDescargaPrioridad As Integer
        Set(value As Integer)
            Me.Prioridad = value
        End Set
    End Property

    Public WriteOnly Property SetDescargaExtraccionAutomatica(Password As String) As Boolean
        Set(value As Boolean)
            Me.ExtraccionFicheroAutomatica = value
            Me.ExtraccionFicheroPassword = Password
        End Set
    End Property

    Public Function DescargaNombre() As String Implements IDescarga.DescargaNombre
        Return Nombre
    End Function

    Public Function DescargaEstado() As Estado Implements IDescarga.DescargaEstado
        Return EstadoDescarga
    End Function

    Public Function DescargaExtraccionAutomatica() As Boolean Implements IDescarga.DescargaExtraccionAutomatica
        Return Me.ExtraccionFicheroAutomatica
    End Function

    Public Function DescargaExtraccionPassword() As String Implements IDescarga.DescargaExtraccionPassword
        Return Me.ExtraccionFicheroPassword
    End Function

    Public Function DescargaTiempoEstimadoDescarga() As String Implements IDescarga.DescargaTiempoEstimadoDescarga
        Return TiempoEstimadoDescarga
    End Function

    Public Function DescargaPorcentaje() As Decimal Implements IDescarga.DescargaPorcentaje
        Return Porcentaje
    End Function

    Public Function DescargaTamanoBytes() As Long Implements IDescarga.DescargaTamanoBytes
        Return TamanoBytes
    End Function

    Public Function DescargaVelocidadKBs() As Decimal Implements IDescarga.DescargaVelocidadKBs
        Return VelocidadKBs
    End Function


    Public Shared Function CargarDesdeFichero() As Generic.List(Of Paquete)
        Return CargarDesdeRuta(ObtenerRutaFicheroDescargas())
    End Function

    Friend Shared Function CargarStartupQueue(cancellationToken As CancellationToken) As Generic.List(Of Paquete)
        Return CargarDesdeRutaCancelable(ObtenerRutaFicheroDescargas(), cancellationToken)
    End Function

    Friend Shared Function CargarDesdeRuta(ByVal Fichero As String) As Generic.List(Of Paquete)
        Return CargarDesdeRutaCancelable(Fichero, CancellationToken.None)
    End Function

    Friend Shared Function CargarDesdeRutaCancelable(ByVal Fichero As String, cancellationToken As CancellationToken) As Generic.List(Of Paquete)
        Return ReadQueue(Fichero, AtomicFile.StartupAttempts, AtomicFile.StartupRetryDelayMs, cancellationToken, recovering:=False)
    End Function

    Private Shared Function ReadQueue(filePath As String, attempts As Integer, delayMs As Integer,
                                      cancellationToken As CancellationToken, recovering As Boolean) As Generic.List(Of Paquete)
        Dim fullPath As String = Path.GetFullPath(filePath)
        Dim packages As Generic.List(Of Paquete) = Nothing
        Dim primary As AtomicFile.XmlReadState = ReadQueueFile(fullPath, attempts, delayMs, cancellationToken, packages)
        Dim backup As AtomicFile.XmlReadState = AtomicFile.XmlReadState.Missing
        If primary <> AtomicFile.XmlReadState.Loaded AndAlso primary <> AtomicFile.XmlReadState.Blocked Then
            backup = ReadQueueFile(fullPath & ".bak", attempts, delayMs, cancellationToken, packages)
        End If

        If cancellationToken.IsCancellationRequested Then
            DisposeLoadedQueue(packages)
            cancellationToken.ThrowIfCancellationRequested()
        End If
        Dim loadedBackup As Boolean = backup = AtomicFile.XmlReadState.Loaded
        Dim blocked As Boolean = primary = AtomicFile.XmlReadState.Blocked OrElse backup = AtomicFile.XmlReadState.Blocked
        Dim invalid As Boolean = primary = AtomicFile.XmlReadState.Invalid OrElse backup = AtomicFile.XmlReadState.Invalid
        SyncLock Mutex.GuardarDownloadList
            If packages IsNot Nothing Then
                _PreserveBackupOnNextSave = loadedBackup
            ElseIf blocked OrElse invalid OrElse recovering Then
                Dim corrupt As Boolean = invalid AndAlso Not blocked
                If Not _LoadTransientError OrElse corrupt <> _LoadCorruptError Then
                    Log.WriteWarning(If(corrupt, "Download list could not be read as a valid queue. Writes are suspended.",
                                        "Download list is unavailable. Writes are suspended."))
                End If
                Volatile.Write(_LoadCorruptError, corrupt)
                Volatile.Write(_LoadTransientError, True)
                Return If(recovering, Nothing, New Generic.List(Of Paquete))
            Else
                _PreserveBackupOnNextSave = False
            End If
            If Not recovering Then ClearQueueWriteProtection()
        End SyncLock

        If packages Is Nothing Then Return New Generic.List(Of Paquete)
        If loadedBackup Then Log.WriteWarning("Download list restored from backup file.")
        MarcarFicherosComoParados(packages)
        Return packages
    End Function

    Private Shared Function ReadQueueFile(filePath As String, attempts As Integer, delayMs As Integer,
                                          cancellationToken As CancellationToken, ByRef packages As Generic.List(Of Paquete)) As AtomicFile.XmlReadState
        Dim xml As XmlDocument = Nothing
        Dim state As AtomicFile.XmlReadState = AtomicFile.ReadXml(filePath, xml, attempts, delayMs, cancellationToken)
        If state <> AtomicFile.XmlReadState.Loaded Then Return state
        If xml.DocumentElement Is Nothing OrElse xml.DocumentElement.Name <> "ListaPaquetes" OrElse
           xml.DocumentElement.SelectSingleNode("*[not(self::Paquete)]") IsNot Nothing Then Return AtomicFile.XmlReadState.Invalid
        Dim loaded As New Generic.List(Of Paquete)
        Try
            For Each node As XmlNode In xml.DocumentElement.SelectNodes("Paquete")
                Dim package As New Paquete
                loaded.Add(package)
                package.CargarXMLCancelable(node, cancellationToken, 4)
            Next
            cancellationToken.ThrowIfCancellationRequested()
            packages = loaded
            Return AtomicFile.XmlReadState.Loaded
        Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is OverflowException OrElse
                                   TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidDataException
            DisposeLoadedQueue(loaded)
            Return AtomicFile.XmlReadState.Invalid
        Catch
            DisposeLoadedQueue(loaded)
            Throw
        End Try
    End Function

    Private Shared Sub MarcarFicherosComoParados(ByRef ListaPaquetes As Generic.List(Of Paquete))
        SyncLock Mutex.ListaDescargas
            For Each Paquete As Paquete In ListaPaquetes
                For Each Fichero As Fichero In Paquete.ListaFicheros
                    If Fichero.DescargaEstado = Estado.Descargando Or _
                       Fichero.DescargaEstado = Estado.Pausado Or _
                       Fichero.DescargaEstado = Estado.Verificando Or _
                       Fichero.DescargaEstado = Estado.Descomprimiendo Or _
                       Fichero.DescargaEstado = Estado.EsperandoVolumenes Or _
                       Fichero.DescargaEstado = Estado.CreandoLocal Or _
                       Fichero.DescargaEstado = Estado.ComprobandoMD5 Then
                        ' Verificando 是下载前的瞬态(EstadoAnterior 已丢失),Descomprimiendo 是下载完成但解压未完成:
                        ' 两者都不能标 Completado,否则“一字节没解也报成功”。统一回 EnCola,靠断点续传继续,
                        ' 已完整的文件只会做快速校验,不会从零重下。
                        ' CreandoLocal/ComprobandoMD5 同为瞬态且持久化后会被原样读回:漏掉它们时,
                        ' 关机窗口(API 验证 60s 超时/强杀/断电)内存盘的任务重启后既不会被调度
                        ' (调度只认 EnCola)又占着 DescargasSimultaneas 名额,ForzarDescarga 也救不回,
                        ' 队列永久停摆;ComprobandoMD5 还会被当成"已完成"。必须一并归一化。
                        Fichero.SetDescargaEstado = Estado.EnCola
                    End If
                    ' ③:存量 KB→控制器字节(×1024),否则历史限速全部 1/1024。
                    ThrottledStreamController.GetController.SetMaxSpeed(Fichero.FileID, CLng(Fichero.LimiteVelocidad) * 1024L)
                Next
            Next
        End SyncLock
    End Sub

    Private Shared _LastSavedXML As String = Nothing
    Private Shared _LoadTransientError As Boolean
    Private Shared _LoadCorruptError As Boolean
    Private Shared _TransientSkipLogged As Boolean
    Private Shared _PreserveBackupOnNextSave As Boolean

    Public Shared ReadOnly Property HasTransientLoadError As Boolean
        Get
            Return Volatile.Read(_LoadTransientError)
        End Get
    End Property

    Public Shared ReadOnly Property HasCorruptLoadError As Boolean
        Get
            Return Volatile.Read(_LoadCorruptError)
        End Get
    End Property

    Public Shared Function TryReloadAfterTransientLock() As Generic.List(Of Paquete)
        If Not HasTransientLoadError Then Return Nothing
        Return TryReloadFromPath(ObtenerRutaFicheroDescargas())
    End Function

    Friend Shared Function TryReloadFromPath(filePath As String) As Generic.List(Of Paquete)
        If Not HasTransientLoadError Then Return Nothing
        Return ReadQueue(filePath, 1, 0, CancellationToken.None, recovering:=True)
    End Function

    Public Shared Sub ArmTransientWriteProtection()
        SyncLock Mutex.GuardarDownloadList
            Volatile.Write(_LoadTransientError, True)
        End SyncLock
    End Sub

    Private Shared Sub ClearQueueWriteProtection()
        _LastSavedXML = Nothing
        _TransientSkipLogged = False
        Volatile.Write(_LoadCorruptError, False)
        Volatile.Write(_LoadTransientError, False)
    End Sub

    Public Shared Sub CommitTransientRecovery()
        SyncLock Mutex.GuardarDownloadList
            ClearQueueWriteProtection()
        End SyncLock
    End Sub

    Public Shared Sub GuardarEnFichero(ByVal ListaPaquetes As Generic.List(Of Paquete))
        GuardarEnRuta(ObtenerRutaFicheroDescargas(), ListaPaquetes)
    End Sub

    Friend Shared Sub GuardarEnRuta(filePath As String, packages As Generic.List(Of Paquete))
        If HasTransientLoadError Then
            SyncLock Mutex.GuardarDownloadList
                If Not _TransientSkipLogged Then
                    _TransientSkipLogged = True
                    Log.WriteWarning("Skipping download list save until the on-disk queue can be recovered.")
                End If
            End SyncLock
            Return
        End If

        Dim snapshot As String = GuardarXML(packages, False).DocumentElement.OuterXml
        SyncLock Mutex.GuardarDownloadList
            If _LoadTransientError OrElse _LastSavedXML = snapshot Then Return
        End SyncLock
        Dim xml As XmlDocument = GuardarXML(packages, True)

        SyncLock Mutex.GuardarDownloadList
            If _LoadTransientError OrElse _LastSavedXML = snapshot Then Return
            Try
                AtomicFile.SaveXml(xml, filePath, preserveBackup:=_PreserveBackupOnNextSave)
                _PreserveBackupOnNextSave = False
                _LastSavedXML = snapshot
            Catch ex As Exception
                Log.WriteError("Error saving download list XML: " & Log.SafeException(ex))
            End Try
        End SyncLock
    End Sub

    Private Shared Function GuardarXML(ByVal ListaPaquetes As Generic.List(Of Paquete), IncluirDatosCifrados As Boolean) As XmlDocument
        Dim XML As New XmlDocument
        XML.AppendChild(XML.CreateElement("ListaPaquetes"))

        If ListaPaquetes IsNot Nothing Then
            For Each Paquete As Paquete In ListaPaquetes
                XML.DocumentElement.AppendChild(Paquete.GuardarXML(XML, IncluirDatosCifrados))
            Next
        End If
        Return XML
    End Function


    Private Shared Function ObtenerRutaFicheroDescargas() As String

        Dim PathLog As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Config")

        If Not System.IO.Directory.Exists(PathLog) Then
            System.IO.Directory.CreateDirectory(PathLog)
        End If
        PathLog = Path.Combine(PathLog, "DownloadList.xml")
        Return PathLog
    End Function




    Public Sub CargarXML(ByVal XML As XmlNode)
        CargarXMLCancelable(XML, CancellationToken.None, 4)
    End Sub

    Friend Sub CargarXMLCancelable(ByVal XML As XmlNode, cancellationToken As CancellationToken, maxConcurrency As Integer)
        cancellationToken.ThrowIfCancellationRequested()
        Me.Nombre = LeerNodo(XML, "Nombre", "")
        Me.RutaLocal = LeerNodo(XML, "RutaLocal", "")
        Boolean.TryParse(LeerNodo(XML, "CrearSubdirectorio", "false"), CrearSubdirectorio)
        Boolean.TryParse(LeerNodo(XML, "PendienteNombrePaquete", "false"), PendienteNombrePaquete)
        Boolean.TryParse(LeerNodo(XML, "ExtraccionFicheroAutomatica", "false"), ExtraccionFicheroAutomatica)
        Dim tempPass As String = LeerNodo(XML, "ExtraccionFicheroPassword", "")
        If Not String.IsNullOrEmpty(tempPass) Then
            Me.ExtraccionFicheroPassword = Criptografia.AES_DecryptString(tempPass, "passZIP")
        End If


        Integer.TryParse(LeerNodo(XML, "Prioridad", "0"), Prioridad)
        Decimal.TryParse(LeerNodo(XML, "Porcentaje", "0"), Porcentaje)
        Long.TryParse(LeerNodo(XML, "TamanoBytes", "0"), TamanoBytes)

        Dim nodes As XmlNodeList = XML.SelectNodes("ListaFicheros/Fichero")
        Dim loaded(nodes.Count - 1) As Fichero
        Dim concurrency As Integer = Math.Max(1, Math.Min(4, Math.Min(Environment.ProcessorCount, maxConcurrency)))
        Try
            If nodes.Count < 128 OrElse concurrency = 1 Then
                For index As Integer = 0 To nodes.Count - 1
                    cancellationToken.ThrowIfCancellationRequested()
                    loaded(index) = New Fichero()
                    loaded(index).CargarXML(nodes(index))
                Next
            Else
                ' Each worker owns its DOM and secure buffers; preserve the original
                ' file order and publish the list only after every worker has finished.
                Dim isolated(nodes.Count - 1) As XmlNode
                For index As Integer = 0 To nodes.Count - 1
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim owner As New XmlDocument()
                    owner.AppendChild(owner.ImportNode(nodes(index), True))
                    isolated(index) = owner.DocumentElement
                Next
                Parallel.For(0, nodes.Count, New ParallelOptions With {.MaxDegreeOfParallelism = concurrency, .CancellationToken = cancellationToken},
                    Sub(index)
                        loaded(index) = New Fichero()
                        loaded(index).CargarXML(isolated(index))
                    End Sub)
            End If
            cancellationToken.ThrowIfCancellationRequested()
            Me.ListaFicheros = New Generic.List(Of Fichero)(loaded)
        Catch
            For Each file As Fichero In loaded
                DisposeLoadedFile(file)
            Next
            Throw
        End Try


    End Sub

    Friend Shared Sub DisposeLoadedQueue(packages As Generic.IEnumerable(Of Paquete))
        If packages Is Nothing Then Return
        For Each package As Paquete In packages
            If package.ListaFicheros Is Nothing Then Continue For
            For Each file As Fichero In package.ListaFicheros
                DisposeLoadedFile(file)
            Next
        Next
    End Sub

    Private Shared Sub DisposeLoadedFile(file As Fichero)
        If file Is Nothing Then Return
        file.Dispose()
        If file._FileID IsNot Nothing Then file._FileID.Dispose()
        If file._FileKey IsNot Nothing Then file._FileKey.Dispose()
        If file._URL IsNot Nothing Then file._URL.Dispose()
        If file._URLFichero IsNot Nothing Then file._URLFichero.Dispose()
    End Sub

    Public Function GuardarXML(ByVal XML As XmlDocument, IncluirDatosCifrados As Boolean) As XmlNode
        Dim NodoPaq As XmlNode = XML.CreateElement("Paquete")

        NodoPaq.AppendChild(XML.CreateElement("Nombre")).InnerText = Nombre
        NodoPaq.AppendChild(XML.CreateElement("RutaLocal")).InnerText = RutaLocal
        NodoPaq.AppendChild(XML.CreateElement("CrearSubdirectorio")).InnerText = CrearSubdirectorio.ToString
        NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroAutomatica")).InnerText = ExtraccionFicheroAutomatica.ToString
        ' 密文(随机 IV,每次不同)不得进入 IncluirDatosCifrados=False 的去重比对快照,
        ' 否则 _LastSavedXML 恒不相等,队列每 5 秒全量落盘(WriteThrough+fsync+Replace+.bak)。
        ' 与 Configuracion 的 B2-⑦/P0-4 处理一致:只写真盘,不参与比对。
        If IncluirDatosCifrados AndAlso Not String.IsNullOrEmpty(ExtraccionFicheroPassword) Then
            Dim encryptedZipPass As String = Criptografia.AES_EncryptString(ExtraccionFicheroPassword, "passZIP")
            If encryptedZipPass IsNot Nothing Then
                NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroPassword")).InnerText = encryptedZipPass
            End If
        End If
        If Not IncluirDatosCifrados Then
            NodoPaq.AppendChild(XML.CreateElement("ExtraccionFicheroPasswordHash")).InnerText = Utils.SecretFingerprint(ExtraccionFicheroPassword)
        End If
        NodoPaq.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString
        NodoPaq.AppendChild(XML.CreateElement("Porcentaje")).InnerText = Porcentaje.ToString
        NodoPaq.AppendChild(XML.CreateElement("TamanoBytes")).InnerText = TamanoBytes.ToString
        If PendienteNombrePaquete Then NodoPaq.AppendChild(XML.CreateElement("PendienteNombrePaquete")).InnerText = PendienteNombrePaquete.ToString


        ' P-01 follow-up: GuardarEnFichero now serializes outside Mutex.ListaDescargas.
        ' AgregarFichero mutates ListaFicheros under that lock, so a bare For Each here
        ' could throw "Collection was modified" while a package was being imported,
        ' killing the periodic disk-save worker for the rest of the session. Take a
        ' short locked reference snapshot; the (slower) per-file work stays outside.
        Dim ficherosSnapshot As Generic.List(Of Fichero) = Nothing
        SyncLock Mutex.ListaDescargas
            If Me.ListaFicheros IsNot Nothing Then
                ficherosSnapshot = New Generic.List(Of Fichero)(Me.ListaFicheros)
            End If
        End SyncLock

        If ficherosSnapshot IsNot Nothing Then
            Dim nodoListaFic As XmlNode = NodoPaq.AppendChild(XML.CreateElement("ListaFicheros"))
            For Each Fichero As Fichero In ficherosSnapshot
                nodoListaFic.AppendChild(Fichero.GuardarXML(XML, IncluirDatosCifrados))
            Next
        End If

        Return NodoPaq

    End Function

    Private Shared Function LeerNodo(ByRef NodoXML As XmlNode, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = NodoXML.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function

End Class
