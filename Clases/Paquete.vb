Imports System.Xml
Imports System.IO

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
        Dim Fichero As String = ObtenerRutaFicheroDescargas()

        Log.WriteDebug("Loading download list XML")

        Dim XML As XmlDocument = Nothing
        Dim recoveredFromBackup As Boolean = False
        Dim transientFailure As Boolean = False
        SyncLock Mutex.GuardarDownloadList
            If Not AtomicFile.TryLoadXml(Fichero, XML, recoveredFromBackup, transientFailure) Then
                ' P0-3:被瞬时占用 ≠ 损坏:置标记让 GuardarEnFichero 拒写,否则周期性
                ' 落盘会把"空队列"写盘、把完好队列文件挤进 .bak 直至彻底覆盖。
                _LoadTransientError = transientFailure
                If transientFailure Then
                    Log.WriteError("Download list is locked by another process (transient). Running with an empty list; writes are suspended to preserve the on-disk file.")
                End If
                Return New Generic.List(Of Paquete)
            End If
            If recoveredFromBackup Then
                Log.WriteWarning("Download list restored from backup file.")
            End If
        End SyncLock

        Dim l As New Generic.List(Of Paquete)

        For Each NodoPaq As XmlNode In XML.DocumentElement.SelectNodes("Paquete")
            Dim p As New Paquete
            p.CargarXML(NodoPaq)
            l.Add(p)
        Next

        MarcarFicherosComoParados(l)
        Return l
    End Function


    Private Shared Sub MarcarFicherosComoParados(ByRef ListaPaquetes As Generic.List(Of Paquete))
        SyncLock Mutex.ListaDescargas
            For Each Paquete As Paquete In ListaPaquetes
                For Each Fichero As Fichero In Paquete.ListaFicheros
                    If Fichero.DescargaEstado = Estado.Descargando Or _
                       Fichero.DescargaEstado = Estado.Pausado Or _
                       Fichero.DescargaEstado = Estado.Verificando Or _
                       Fichero.DescargaEstado = Estado.Descomprimiendo Or _
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
    ''' <summary>P0-3:见 CargarDesdeFichero——加载因文件被瞬时占用而失败时,磁盘上的
    ''' 队列可能完好,写盘必须全部跳过(空列表落盘=用户队列被清空)。</summary>
    Private Shared _LoadTransientError As Boolean = False
    ' 同 Configuracion._TransientSkipLogged:GuardarEnFichero 每 5s 一次,只在首次拦截时记日志。
    Private Shared _TransientSkipLogged As Boolean = False
    Public Shared Sub GuardarEnFichero(ByVal ListaPaquetes As Generic.List(Of Paquete))

        Dim Fichero As String = ObtenerRutaFicheroDescargas()

        If _LoadTransientError AndAlso System.IO.File.Exists(Fichero) Then
            If Not _TransientSkipLogged Then
                _TransientSkipLogged = True
                Log.WriteWarning("Skipping download list save: the on-disk file could not be loaded earlier (locked). It is preserved untouched.")
            End If
            Exit Sub
        End If

        Dim XML As XmlDocument = GuardarXML(ListaPaquetes, False)

        If _LastSavedXML Is Nothing OrElse _LastSavedXML <> XML.DocumentElement.OuterXml Then

            Dim previousSaved As String = _LastSavedXML

            ' Como el usuario y password se guarda cifrado con entropia, cada vez tendrá un valor distinto, no podemos compararlos...
            XML = GuardarXML(ListaPaquetes, True)
            Log.WriteDebug("Saving download list")

            SyncLock Mutex.GuardarDownloadList
            Try
                AtomicFile.SaveXml(XML, Fichero)
                _LastSavedXML = previousSaved
                ' Update cache only after successful save (compare against unencrypted snapshot next time)
                Dim compareXml As XmlDocument = GuardarXML(ListaPaquetes, False)
                _LastSavedXML = compareXml.DocumentElement.OuterXml
            Catch ex As Exception
                Log.WriteError("Error saving download list XML: " & Log.SafeException(ex))
                _LastSavedXML = previousSaved
            End Try
            End SyncLock

        End If

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

        Me.ListaFicheros = New Generic.List(Of Fichero)
        For Each NodoFichero As XmlNode In XML.SelectNodes("ListaFicheros/Fichero")
            Dim fic As New Fichero
            fic.cargarxml(NodoFichero)
            Me.ListaFicheros.Add(fic)
        Next


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
        NodoPaq.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString
        NodoPaq.AppendChild(XML.CreateElement("Porcentaje")).InnerText = Porcentaje.ToString
        NodoPaq.AppendChild(XML.CreateElement("TamanoBytes")).InnerText = TamanoBytes.ToString
        If PendienteNombrePaquete Then NodoPaq.AppendChild(XML.CreateElement("PendienteNombrePaquete")).InnerText = PendienteNombrePaquete.ToString


        If Me.ListaFicheros IsNot Nothing Then
            Dim nodoListaFic As XmlNode = NodoPaq.AppendChild(XML.CreateElement("ListaFicheros"))
            For Each Fichero As Fichero In Me.ListaFicheros
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
