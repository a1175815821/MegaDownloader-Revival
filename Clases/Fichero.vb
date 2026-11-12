Imports System.Collections.Specialized
Imports System.Text.RegularExpressions
Imports System.ComponentModel
Imports System.Xml
Imports System.Security

Public Class Fichero
	Implements IDescarga, IDisposable
	
	
	Public Const HIDDEN_LINK As String = "{HIDDEN}"
	Public Const HIDDEN_LINK_DESC As String = "** LINK NOT VISIBLE **"
	
	Public _FileID As SecureString ' Codificado!!
	
	Public _FileKey As SecureString ' Codificado!!
	
	Public _URL As SecureString ' Codificado!!
	
	Public _URLFichero As SecureString ' Codificado!!

    Public NombreFichero As String ' Solo el nombre del fichero, sin ruta

    Public PartialFileName As String = FileDownloader.CreatePartialFileName()

    ' Empty means no delivered output; Nothing identifies an older queue entry.
    Private completedFileName As String = ""

    ''' <summary>B3: 本会话内已由下载器校验并交付的成品。删除时按"路径+大小+写入时间"
    ''' 复核未被外部替换即可复用,不必对 GB 级文件重算一次全文件 MetaMAC。</summary>
    Private _verifiedOutputPath As String = Nothing
    Private _verifiedOutputLength As Long = -1
    Private _verifiedOutputWriteUtc As Long = 0

    ''' <summary>B4: 16 字节(4 词)公链 key 没有内嵌 MetaMAC。预热时标记出来,
    ''' 由界面一次性请求用户显式确认,而不是静默把成品留在磁盘上。</summary>
    Public OutputNeedsConfirmation As Boolean = False
    Public OutputConfirmationPath As String = Nothing
    Public ConfirmUnverifiedOutputDeletion As Boolean = False

    ''' <summary>B7: CancellationComplete 的一次性门闩,以及 Main.DisposeFichero 的幂等标记。</summary>
    Private _cancellationNotified As Integer = 0
    Public DisposalHandled As Integer = 0
	
    Public RutaLocal As String

    Public RutaRelativa As String
	
	Public TamanoBytes As Long
	
	Public NumeroConexionesAbiertas As Integer
	
	Public NumeroChunksAsignados As Integer
	
	Public MD5 As String
	
	Public LinkVisible As Boolean
	
	Public DescripcionError As String

	''' <summary>v2.5 beta: 永久失败(链接失效/key错/无权限/被封),自愈跳过,需手动处理。</summary>
	Public EsErrorPermanente As Boolean = False

	''' <summary>v2.5 RC: 配额失败,配额期内自愈跳过,到期由配额恢复统一唤醒。RC起持久化,重启后立即重试仍可捞回。</summary>
	Public FailedByQuota As Boolean = False

	Public FechaUltimoError As Date?
	
	Public Porcentaje As Decimal
	
	Public DescargaComenzada As Boolean ' Variable auxiliar que indica si la descarga ya ha comenzado (y no se puede cambiar su configuración de ruta y demás)
	
	Public DescargaProcesada As Boolean ' Indica si la descarga ya ha sido procesada (se ha verificado la URL del fichero, su tamaño, etc)
	
    Public ExtraccionFicheroAutomatica As Boolean

    Public ExtraccionFicheroPassword As String
	
	Public Prioridad As Integer
	
	Public VelocidadKBs As Decimal
	
	Public BytesDescargados As Long
	
	Public EstadoDescarga As Estado
	
	Public MarcadoParaBorrarFicheroLocal As Boolean

	''' <summary>删盘时顺带清理空目录用的包上下文(Eliminar 在 Stop 前写入,
	''' DisposeFichero 在异步删盘后消费一次即清)。平时始终为 Nothing。</summary>
	Public LimpiezaPaquete As Paquete = Nothing
	
	Private WithEvents Downloader As FileDownloader
	
	Public Event CancellationComplete As EventHandler
	
	Public TiempoEstimadoDescarga As String
	
	Public PausaIndividual As Boolean
	
	Public DescargaIndividual As Boolean
	
	Public DatosPartes As FileDownloader.DataPart
	
	Private NumErroresChunk As Integer
	Private FechaUltimoErrorChunk As Date?
	
	Public LimiteVelocidad As Integer
	
    Private UltimoErrorChunk As Exception

    Private Version As Integer

    Private Const NUM_MAX_ERRORES_CHUNK As Integer = 30
    ' 必须显著大于分块退避上限(约 16.5 秒),否则真正持续失败的重试会被误判为"已恢复",
    ' 保护永不触发。取 120 秒:持续失败约 8 分钟内累计到上限,几小时里的零星抖动不累积。
    Private Const CHUNK_ERROR_COOLDOWN_SECONDS As Integer = 120
	
	Public Sub New()
		Me.New("")
	End Sub
	
    Public Sub New(ByVal Url As String)
        Me.Version = 2
        Me.LinkVisible = True
        Me.URL = Url
        Me.URLFichero = ""
        Me.FileID = ""
        Me.FileKey = ""
        Me.NombreFichero = ""
        Me.RutaLocal = ""
        Me.RutaRelativa = ""
        Me.TamanoBytes = 0
        Me.MD5 = ""
        Me.Porcentaje = 0
        Me.DescargaComenzada = False
        Me.DescargaProcesada = False
        Me.ExtraccionFicheroAutomatica = False
        Me.ExtraccionFicheroPassword = Nothing
        Me.Prioridad = 0
        Me.VelocidadKBs = 0
        Me.EstadoDescarga = Estado.EnCola
        Me.MarcadoParaBorrarFicheroLocal = False
        Me.TamanoBytes = 0
        Me.BytesDescargados = 0
        Me.NumErroresChunk = 0
        Me.PausaIndividual = False
        Me.DescargaIndividual = False

    End Sub
	
	
    Private Const keyUrl As String = "xmpcphVkCTJ2unykcwRMBecz3jEnTX93Nv5KZGrtYK6LE3WE"
	Public Property URL As String
		Get
			Dim str As String = Criptografia.ToInsecureString(Me._URL)
            If String.IsNullOrEmpty(str) Or Me.Version > 1 Then
                Return str
            Else
                Return Criptografia.AES_DecryptString(str, keyUrl)
            End If
        End Get
        Set(value As String)
            If Me.Version > 1 Then
                Me._URL = Criptografia.ToSecureString(value)
            Else
                Me._URL = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl))
            End If
            CacheSecureData = Nothing
        End Set
	End Property
	
	Public Property URLFichero As String
		Get
			Dim str As String = Criptografia.ToInsecureString(Me._URLFichero)
            If String.IsNullOrEmpty(str) Or Me.Version > 1 Then
                Return str
            Else
                Return Criptografia.AES_DecryptString(str, keyUrl)
            End If
		End Get
        Set(value As String)
            If Me.Version > 1 Then
                Me._URLFichero = Criptografia.ToSecureString(value)
            Else
                Me._URLFichero = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl))
            End If
            CacheSecureData = Nothing
        End Set
    End Property


	Public Property FileID As String
		Get
			Dim str As String = Criptografia.ToInsecureString(Me._FileID)
            If String.IsNullOrEmpty(str) Or Me.Version > 1 Then
                Return str
            Else
                Return Criptografia.AES_DecryptString(str, keyUrl)
            End If
		End Get
        Set(value As String)
            If Me.Version > 1 Then
                Me._FileID = Criptografia.ToSecureString(value)
            Else
                Me._FileID = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl))
            End If
            CacheSecureData = Nothing
        End Set
	End Property
	
	
	Public Property FileKey As String
		Get
			Dim str As String = Criptografia.ToInsecureString(Me._FileKey)
            If String.IsNullOrEmpty(str) Or Me.Version > 1 Then
                Return str
            Else
                Return Criptografia.AES_DecryptString(str, keyUrl)
            End If
		End Get
        Set(value As String)
            If Me.Version > 1 Then
                Me._FileKey = Criptografia.ToSecureString(value)
            Else
                Me._FileKey = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl))
            End If
            CacheSecureData = Nothing
        End Set
    End Property

    Private Class Cache
        Public FileKey As String
        Public FileID As String
        Public URLFichero As String
        Public URL As String
        Public FileIDSource As SecureString
        Public FileKeySource As SecureString
        Public URLSource As SecureString
        Public URLFicheroSource As SecureString
        Public LinkVisible As Boolean

        Public Function Matches(file As Fichero) As Boolean
            Return Object.ReferenceEquals(FileIDSource, file._FileID) AndAlso
                   Object.ReferenceEquals(FileKeySource, file._FileKey) AndAlso
                   Object.ReferenceEquals(URLSource, file._URL) AndAlso
                   Object.ReferenceEquals(URLFicheroSource, file._URLFichero) AndAlso
                   LinkVisible = file.LinkVisible
        End Function
    End Class
    Private CacheSecureData As Cache = Nothing

    Private Sub CaptureCacheSources(cache As Cache)
        cache.FileIDSource = _FileID
        cache.FileKeySource = _FileKey
        cache.URLSource = _URL
        cache.URLFicheroSource = _URLFichero
        cache.LinkVisible = LinkVisible
    End Sub
    Private Sub RegenerateCacheSecureData()
        Dim c As New Cache
        c.FileID = Criptografia.EncryptString_DPAPI(_FileID)
        c.FileKey = Criptografia.EncryptString_DPAPI(_FileKey)
        Using secureUrl As SecureString = Criptografia.ToSecureString(If(LinkVisible, "", HIDDEN_LINK) & Criptografia.ToInsecureString(Me._URL))
            c.URL = Criptografia.EncryptString_DPAPI(secureUrl)
        End Using
        c.URLFichero = Criptografia.EncryptString_DPAPI(_URLFichero)
        CaptureCacheSources(c)
        CacheSecureData = c
    End Sub

	Public Function DescargaNombre() As String Implements IDescarga.DescargaNombre
		If String.IsNullOrEmpty(NombreFichero) Then
			Return FileID
        Else
            Return NombreFichero
        End If
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
	
	Public Function DescargaEstado() As Estado Implements IDescarga.DescargaEstado
		Return Me.EstadoDescarga
	End Function
	
	Public WriteOnly Property SetDescargaEstado As Estado
		Set(value As Estado)
			Me.EstadoDescarga = value
		End Set
	End Property
	
	
	Public Function DescargaPrioridad() As Integer Implements IDescarga.DescargaPrioridad
		Return Me.Prioridad
	End Function
	
	Public WriteOnly Property SetDescargaPrioridad As Integer
		Set(value As Integer)
			Me.Prioridad = value
		End Set
	End Property
	
	Public Function DescargaExtraccionAutomatica() As Boolean Implements IDescarga.DescargaExtraccionAutomatica
		Return Me.ExtraccionFicheroAutomatica
    End Function

    Public Function DescargaExtraccionPassword() As String Implements IDescarga.DescargaExtraccionPassword
        Return Me.ExtraccionFicheroPassword
    End Function
	
    Public WriteOnly Property SetDescargaExtraccionAutomatica(Password As String) As Boolean
        Set(value As Boolean)
            Me.ExtraccionFicheroAutomatica = value
            Me.ExtraccionFicheroPassword = Password
        End Set
    End Property
	
	Public Function DescargaTiempoEstimadoDescarga() As String Implements IDescarga.DescargaTiempoEstimadoDescarga
		Return TiempoEstimadoDescarga
	End Function
	
	Private _Actualizando As Boolean = False
	Public Sub ActualizarInformacionFichero(ByVal Config As Configuracion, ByRef ErrorObtenido As Conexion.TipoError, ByVal ComprobacionAntesDescarga As boolean, Optional ByVal ct As System.Threading.CancellationToken = Nothing)
		If Not _Actualizando Then
			_Actualizando = True
			Try
				Dim EstadoAnterior As Estado = Me.DescargaEstado
				Me.SetDescargaEstado = Estado.Verificando
				
				Dim Info As Conexion.InformacionFichero = Conexion.ObtenerInformacionFichero(Config, Me.FileID, Me.FileKey, ComprobacionAntesDescarga, ct)
				' B2-⑩:字段批量回写必须持 FicheroDownloader 锁,与 GuardarXML/ActualizarDatosDescarga
				' 同锁互斥,否则关机存盘会读到半新半旧的撕裂对象(新 Key+旧 Size+旧分片表)。
				' 网络 IO 已在锁外完成,此处仅内存赋值,持锁极短无性能影响。
				SyncLock Mutex.FicheroDownloader
				If Info IsNot Nothing AndAlso Info.Err = Conexion.TipoError.SinErrores Then
					If Not String.IsNullOrEmpty(Info.URL) then Me.URLFichero = Info.URL
					If Info.Tamano  > 0 Then Me.TamanoBytes = Info.Tamano
					If Not String.IsNullOrEmpty(Info.Nombre) AndAlso
					   (Me.DatosPartes Is Nothing OrElse Not Me.DatosPartes.AllFinished OrElse String.IsNullOrEmpty(Me.NombreFichero)) Then
						' A completed task may already use a collision suffix on disk.
						' Keep that name while recovering extraction/verification.
						Me.NombreFichero = PathGuard.SanitizeFileName(Info.Nombre, "file")
					End If
					Me.MD5 = Info.MD5
					Me.DescargaProcesada = True
					Me.SetDescargaEstado = EstadoAnterior
					Me.FileKey = Info.FileKey
					Me.FileID = Info.FileID
				ElseIf Info IsNot Nothing AndAlso Info.Err <> Conexion.TipoError.SinErrores Then
					ErrorObtenido = Info.Err
					If Info.Err = Conexion.TipoError.QuotaExceeded Then
						Me.EstablecerError("MEGA transfer quota exceeded (EOVERQUOTA / HTTP 509)." & vbNewLine & _
							" * File code: " & Me.FileID & vbNewLine & _
							" * Internal info: " & Info.Errtxt, False, True)
					Else
						Me.EstablecerError("The file could not be verified." & vbNewLine & _
							" * File code: " & Me.FileID & vbNewLine & _
							" * Error type: " & Info.Err.ToString & vbNewLine & _
							" * Internal info: " & Info.Errtxt)
					End If
				End If
				End SyncLock
			Finally
				_Actualizando = False
			End Try
		End If
	End Sub
	
	
	
	Public Shared Function ExtraerFileKey(ByVal URL As String) As String
		Return URLExtractor.ExtraerFileKey(URL)
	End Function
	
	Public Shared Function ExtraerFileID(ByVal URL As String) As String
		Return URLExtractor.ExtraerFileID(URL)
	End Function
	
	Public Sub BorrarFicheroLocal()
		If Not Me.MarcadoParaBorrarFicheroLocal Then Return
		Dim Ruta As String = String.Empty
		Dim errorException As Exception = Nothing
		Try
			If String.IsNullOrWhiteSpace(Me.RutaLocal) OrElse String.IsNullOrWhiteSpace(Me.NombreFichero) Then
				Throw New InvalidOperationException("Local path or file name is missing.")
			End If
			' B3: 归属判定可能要读整个文件算 MetaMAC(GB 级可达数十秒),必须放在
			' Mutex.DeletingFiles 之外,否则一次删除会阻塞所有其它删除。
			' 界面入口会先在后台线程 PrepareOutputOwnership() 预热,这里通常命中缓存。
			Dim completado As String = GetCompletedFileForDeletion()
			Ruta = If(completado, String.Empty)
			Dim parcial As String = FileDownloader.GetPartialFilePath(Me.RutaLocal, Me.NombreFichero, Me.PartialFileName)
			SyncLock Mutex.DeletingFiles
				Try
					If Not String.IsNullOrEmpty(completado) Then
						If System.IO.File.Exists(completado) Then System.IO.File.Delete(completado)
						completedFileName = ""
					End If
					If System.IO.File.Exists(parcial) Then
						Ruta = parcial
						System.IO.File.Delete(parcial)
					End If
				Catch e As Exception
					errorException = e
				End Try
			End SyncLock
			If errorException IsNot Nothing Then
				Log.WriteError("Error deleting local files - Path: " & Ruta & " - Error: " & errorException.ToString)
				MessageBox.Show(Language.GetText("Error deleting local files - Path: %PATH% - Error: %ERROR%").Replace("%PATH%", Ruta).Replace("%ERROR%", errorException.Message), _
					Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
			End If
		Catch e As Exception
			Log.WriteError("Error deleting local files - Path: " & Ruta & " - Error: " & e.ToString)
		End Try
	End Sub

    Private Function CompletedOutputPath(fileName As String) As String
        If String.IsNullOrEmpty(fileName) OrElse System.IO.Path.GetFileName(fileName) <> fileName OrElse
           fileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 Then
            Throw New ArgumentException("Invalid completed file name.")
        End If
        ' Do not sanitize an actual collision-suffixed name into a different path.
        Dim result As String = System.IO.Path.GetFullPath(System.IO.Path.Combine(RutaLocal, fileName))
        PathGuard.EnsurePathUnderRoot(RutaLocal, result, allowRoot:=False)
        Return result
    End Function

    Private Function GetCompletedFileForDeletion() As String
        If Not String.IsNullOrEmpty(completedFileName) Then Return CompletedOutputPath(completedFileName)

        ' B3: 本会话内下载器已完成校验的成品,只要路径/大小/写入时间未变就直接复用,
        ' 不再对同一个 GB 级文件重算一次全文件 MetaMAC。必须放在旧队列守卫之前:
        ' 新建任务的 completedFileName 是 ""(非 Nothing),否则会被提前拦掉。
        If Not String.IsNullOrEmpty(_verifiedOutputPath) Then
            Try
                Dim verifiedPath As String = CompletedOutputPath(System.IO.Path.GetFileName(_verifiedOutputPath))
                If String.Equals(verifiedPath, _verifiedOutputPath, StringComparison.OrdinalIgnoreCase) AndAlso
                   System.IO.File.Exists(verifiedPath) Then
                    Dim verifiedInfo As New System.IO.FileInfo(verifiedPath)
                    If verifiedInfo.Length = _verifiedOutputLength AndAlso
                       verifiedInfo.LastWriteTimeUtc.Ticks = _verifiedOutputWriteUtc Then
                        completedFileName = System.IO.Path.GetFileName(verifiedPath)
                        Return verifiedPath
                    End If
                End If
            Catch ex As Exception
                Log.WriteWarning("Verified output can no longer be reused for " & Me.FileID & ": " & Log.SafeException(ex))
            End Try
        End If

        If completedFileName IsNot Nothing OrElse Not DescargaComenzada Then Return Nothing
        ' Older queues have no output marker. Require a real MetaMAC match before
        ' treating a same-named existing file as their completed output.
        If EstadoDescarga <> Estado.Completado AndAlso EstadoDescarga <> Estado.Descomprimiendo AndAlso
           EstadoDescarga <> Estado.ComprobandoMD5 AndAlso (DatosPartes Is Nothing OrElse Not DatosPartes.AllFinished) Then Return Nothing

        OutputNeedsConfirmation = False
        OutputConfirmationPath = Nothing
        Try
            Dim candidate As String = CompletedOutputPath(NombreFichero)
            If Not System.IO.File.Exists(candidate) OrElse New System.IO.FileInfo(candidate).Length <> TamanoBytes Then Return Nothing
            Dim key As String = Me.FileKey
            If key.Contains("=###n=") Then key = key.Substring(0, key.IndexOf("=###n="))
            If Criptografia.base64urldecodeBytes(key).Length <> 32 Then
                ' B4: 4 词公链 key(16 字节)没有内嵌 MetaMAC,原实现在此静默返回 Nothing,
                ' 用户以为删掉了成品,文件却一直留在磁盘上。前置条件已经限定
                ' "任务确实完成 + 同名 + 大小精确一致 + 下载已开始",交由用户显式确认。
                If Not Me.ConfirmUnverifiedOutputDeletion Then
                    OutputNeedsConfirmation = True
                    OutputConfirmationPath = candidate
                    Log.WriteWarning("Legacy link has no MetaMAC; keeping output pending explicit confirmation: " & candidate)
                    Return Nothing
                End If
                completedFileName = NombreFichero
                Return candidate
            End If
            If Not Criptografia.VerifyMegaMetaMac(candidate, key) Then Return Nothing
            completedFileName = NombreFichero
            Return candidate
        Catch ex As Exception
            Log.WriteWarning("Keeping unverified legacy output for " & Me.FileID & ": " & Log.SafeException(ex))
            Return Nothing
        End Try
    End Function

    ''' <summary>B3: 界面在执行删除前,先在后台线程预热归属判定(可能整文件读),
    ''' 校验结果写入 completedFileName 缓存,随后 UI 线程的删除就是 O(1)。</summary>
    Public Sub PrepareOutputOwnership()
        Try
            GetCompletedFileForDeletion()
        Catch ex As Exception
            Log.WriteWarning("Output ownership precheck failed for " & Me.FileID & ": " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>B3/B6: 记录下载器已验证并交付的成品元数据,供删除时复用。</summary>
    Public Sub RecordVerifiedOutput(ByVal path As String)
        Try
            If String.IsNullOrEmpty(path) Then Return
            Dim full As String = System.IO.Path.GetFullPath(path)
            Dim info As New System.IO.FileInfo(full)
            _verifiedOutputPath = full
            _verifiedOutputLength = info.Length
            _verifiedOutputWriteUtc = info.LastWriteTimeUtc.Ticks
        Catch ex As Exception
            _verifiedOutputPath = Nothing
            Log.WriteWarning("Could not cache verified output: " & Log.SafeException(ex))
        End Try
    End Sub
	
	Public Function ObtenerNombreSinExtension As String
		
		If Not String.IsNullOrEmpty(Me.NombreFichero) AndAlso Me.NombreFichero.Contains(".") Then
			Dim str As String = Me.NombreFichero.Substring(0, Me.NombreFichero.LastIndexOf("."))
			If str.LastIndexOf(".part") >= 0 AndAlso IsNumeric(str.Substring(str.LastIndexOf(".part") + 5)) Then
				' Ficheros .part01.rar, .part02.rar, etc
				str = str.Substring(0, str.LastIndexOf(".part"))
			End If
			Return str
		Else 
			Return Me.NombreFichero
		End If

	End Function
	
#Region "Downloader"
	
	Private Class DownloadWorker
		Inherits BackgroundWorker
		
		
		Public Sub New(ByRef Configuration As Configuracion, NumeroConexionesDisponibles As Integer)
			Me.NumConexionesDisponibles = NumeroConexionesDisponibles
			Me.Config = Configuration
		End Sub
		Public Config As Configuracion
		Public NumConexionesDisponibles As Integer
	End Class
	
	' Usamos un worker para iniciar la descarga en segundo plano, ya que es necesario llamar a ActualizarInformacionFichero pero esta función
	' puede tardar mucho y así no bloqueamos el UI
	Private WithEvents bgArranque As DownloadWorker
	' B1-④:验证阶段取消标志。bgArranque.WorkerSupportsCancellation=False,Stop/Dispose
	' 拦不住 threadpool 上的验证;无此标志时 Stop 后 DoWork 照样 New FileDownloader+Start() 复活。
	' Boolean 读写原子,Stop 还有 5s 轮询,可见性足够,无需加锁。
	Private _StartupCancelled As Boolean = False
	Private Sub bgArranque_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles bgArranque.DoWork
		Try
			
			
			Dim worker As DownloadWorker = CType(sender, DownloadWorker)
            CaptureDownloaderProgress()
			
			Dim ErrAct As Conexion.TipoError = Conexion.TipoError.SinErrores
			Me.ActualizarInformacionFichero(worker.Config, ErrAct, True)
			
			If ErrAct <> Conexion.TipoError.SinErrores Then
				' La función ActualizarInformacionFichero ya ha actualizado el estado de la descarga, no hacemos nada
				Exit Sub
			End If

			' B1-④:验证耗时(60s API 超时/大文件夹解密)期间用户已 Stop/关闭:直接退出,
			' 不得再建下载器(此前必复活)。
			If Me._StartupCancelled Then
				Log.WriteWarning("Startup cancelled during file info verification, not starting downloader for " & Me.FileID)
                Me.EstadoDescarga = Estado.EnCola
				Exit Sub
			End If

			Me.NumErroresChunk = 0
            ' B8: "清空 + 装配"必须在同一把锁下完成,后续全程使用局部引用 dl。
            ' downloader_Canceled 会在 Mutex.FicheroDownloader 内把 Me.Downloader 置 Nothing;
            ' 若上一轮 Canceled 恰好落在本轮 ActualizarInformacionFichero(最长 60s)之后、
            ' 装配之前,旧代码从 :478 起就会 NullReferenceException 并被引擎转成 Erroneo。
            Dim dl As FileDownloader = Nothing
            SyncLock Mutex.FicheroDownloader
                dl = Me.Downloader
                If dl Is Nothing Then
                    dl = New FileDownloader(True)
                    AddHandler dl.Resumed, AddressOf downloader_Started
                    AddHandler dl.Started, AddressOf downloader_Started
                    AddHandler dl.Paused, AddressOf downloader_Paused
                    AddHandler dl.CancelRequested, AddressOf downloader_CancelRequested
                    AddHandler dl.Canceled, AddressOf downloader_Canceled
                    AddHandler dl.Completed, AddressOf downloader_Completed
                    AddHandler dl.FileDownloadSucceeded, AddressOf downloader_FileDownloadSucceeded
                    AddHandler dl.FileDownloadFailed, AddressOf downloader_FileDownloadFailed
                    AddHandler dl.ChunkDownloadFailed, AddressOf downloader_ChunkDownloadFailed
                    AddHandler dl.FileLocalCreated, AddressOf downloader_FileLocalCreated
                    AddHandler dl.FileRenamed, AddressOf downloader_FileRenamed
                    Me.Downloader = dl
                End If
            End SyncLock

            dl.setBufferAndPackageSize(worker.Config.TamanoBufferKB * 1024, worker.Config.TamanoPaqueteKB * 1024)

            'dl.PackageSize = worker.Config.TamanoPaqueteKB * 1024
            'dl.BufferSize = worker.Config.TamanoBufferKB * 1024
            dl.StopWatchCyclesAmount = 30
			dl.LocalDirectory = Me.RutaLocal
			dl.DeleteFilesAfterCancel = False
			dl.DeleteCompletedFilesAfterCancel = False
			dl.PartsPerFile = worker.Config.ConexionesPorFichero
			dl.NumConnections = If(worker.Config.ConexionesPorFichero < worker.NumConexionesDisponibles, worker.Config.ConexionesPorFichero, worker.NumConexionesDisponibles)
			dl.AddFileInfo(Me.FileID, Me.FileKey, Me.URLFichero, Me.DescargaNombre, Me.DatosPartes, Me.PartialFileName)
			Me.PartialFileName = dl.File.PartialFileName

			' B1-④:装配完成到 Start 之间的取消窗口同样要拦,否则建完即启动,前功尽弃。
			If Me._StartupCancelled Then
				Log.WriteWarning("Startup cancelled just before downloader start, disposing it for " & Me.FileID)
				Try
					dl.Dispose()
				Catch ex As Exception
					Log.WriteError("Error disposing cancelled startup downloader: " & Log.SafeException(ex))
				End Try
				' B8: 只清自己装配的实例,别把取消窗口里刚换上的新 downloader 抹掉。
				SyncLock Mutex.FicheroDownloader
					If Object.ReferenceEquals(Me.Downloader, dl) Then Me.Downloader = Nothing
				End SyncLock
                Me.EstadoDescarga = Estado.EnCola
				Exit Sub
			End If
			If dl.CanStart Then
				dl.Start()
			End If
		Catch ex As Exception
			Log.WriteError("Error on bgArranque_DoWork: " & Log.SafeException(ex))
			Me.EstablecerError("The download could not be started." & vbNewLine & _
				" * File code: " & Me.FileID & vbNewLine & _
				" * Internal info: " & Log.SafeException(ex))
		End Try
	End Sub
	Private Sub bgArranque_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgArranque.RunWorkerCompleted
		If e.Error IsNot Nothing Then
			Log.WriteError("bgArranque completed with error: " & Log.SafeException(e.Error))
			If Me.EstadoDescarga <> Estado.Erroneo Then
				Me.EstablecerError("The download could not be started." & vbNewLine & _
					" * File code: " & Me.FileID & vbNewLine & _
					" * Internal info: " & Log.SafeException(e.Error))
			End If
		End If
        Dim worker As DownloadWorker = TryCast(sender, DownloadWorker)
        If worker IsNot Nothing Then worker.Dispose()
        If Object.ReferenceEquals(bgArranque, worker) Then bgArranque = Nothing
	End Sub
	
	
	Public Sub Start(ByRef Config As Configuracion, NumConexionesDisponibles As Integer)
		If disposedValue OrElse Estado.Erroneo = Me.DescargaEstado Then Exit Sub

		If bgArranque Is Nothing AndAlso (Me.Downloader Is Nothing OrElse Me.Downloader.CanStart) Then
			Me.EstadoDescarga = Estado.CreandoLocal
			Me.DescargaComenzada = True
			Me._StartupCancelled = False
			' B7: 新一轮下载开始,允许后续取消再通知一次删除回调。
			System.Threading.Interlocked.Exchange(_cancellationNotified, 0)
			bgArranque = New DownloadWorker(Config, NumConexionesDisponibles)
			bgArranque.WorkerSupportsCancellation = False
			bgArranque.WorkerReportsProgress = False
			bgArranque.RunWorkerAsync()
		End If
	End Sub

    Private Sub CaptureDownloaderProgress()
        SyncLock Mutex.FicheroDownloader
            If Me.Downloader Is Nothing OrElse Me.Downloader.File Is Nothing Then Return
            Dim file As FileDownloader.FileInfo = Me.Downloader.File
            If file.DataPartInitialized AndAlso file.Size > 0 Then Me.DatosPartes = file.GetDataPart
            Me.PartialFileName = file.PartialFileName
        End SyncLock
    End Sub
	
	Public Sub [Resume]()
		If Me.Downloader IsNot Nothing AndAlso Me.Downloader.CanResume Then
            ThrottledStreamController.GetController.Continuar(Me.FileID)
			Me.Downloader.Resume()
		End If
	End Sub
	
	Public Sub Pause()
		If Me.Downloader IsNot Nothing AndAlso Me.Downloader.CanPause Then
			Me.Downloader.Pause()
		End If
	End Sub
	
	Public Sub [Stop]()
		' B1-④:先置验证取消标志,再等。bgArranque 不可 CancelAsync,只能靠标志自停。
		Me._StartupCancelled = True
		' B7: 每次显式 Stop 都开启一个新的通知周期。同一次取消里随后到达的
		' downloader_Canceled 会被门闩挡住,但下一次 Stop(例如删除任务)仍能通知到。
		System.Threading.Interlocked.Exchange(_cancellationNotified, 0)
		Dim CiclosMax As Integer = 100 ' 100*50 = 5 segundos
		Dim Ciclos As Integer = 0
		While Me._Actualizando And Ciclos < CiclosMax
			System.Threading.Thread.Sleep(50)
			Ciclos += 1
		End While
		If Me.Downloader Is Nothing OrElse Me.Downloader.CanStart Then
            Me.EstadoDescarga = Estado.EnCola
			NotifyCancellationComplete()
		ElseIf Me.Downloader.CanStop Then
			Me.Downloader.Stop()
		End If
	End Sub

	''' <summary>Deliver at most one completion notification per cancellation cycle.</summary>
	Private Sub NotifyCancellationComplete()
		If System.Threading.Interlocked.Exchange(_cancellationNotified, 1) = 0 Then
			RaiseEvent CancellationComplete(Me, New EventArgs)
		End If
	End Sub
	
	Public Sub DescompresionFinalizada(Optional ByVal success As Boolean = True, Optional ByVal errorMessage As String = "")
		If Me.EstadoDescarga = Estado.Descomprimiendo Then
			If success Then
				Me.EstadoDescarga = Estado.Completado
			Else
				Dim detail As String = If(String.IsNullOrEmpty(errorMessage), "", vbNewLine & " * Reason: " & errorMessage)
				Me.EstablecerError("Automatic extraction failed or was cancelled." & vbNewLine & " * File code: " & Me.FileID & detail)
			End If
		End If
	End Sub
	
	
	Private Sub downloader_Started(ByVal sender As System.Object, ByVal e As System.EventArgs)
		Me.EstadoDescarga = Estado.Descargando
		Me.DescargaComenzada = True
	End Sub
	
	Private Sub downloader_Paused(ByVal sender As System.Object, ByVal e As System.EventArgs)
		Me.EstadoDescarga = Estado.Pausado
	End Sub
	
	Private Sub downloader_CancelRequested(ByVal sender As System.Object, ByVal e As System.EventArgs)
		Me.EstadoDescarga = Estado.Pausado
	End Sub
	
	Private Sub downloader_FileLocalCreated(ByVal sender As System.Object, ByVal e As System.EventArgs)
		Me.EstadoDescarga = Estado.Descargando
	End Sub

	''' <summary>下载器落盘终名回写(重名时为 "name (2).ext" 这类真实名)。
	''' 故意不取任何锁:事件在 FileDownloader.MutexFile 锁外同步触发,而
	''' ActualizarDatosDescarga 的锁序是反方向(FicheroDownloader -&gt; 下载器内部锁),
	''' 取锁即 AB-BA 死锁;字符串引用赋值原子,与其它 handler 直接赋值同规矩。</summary>
	Private Sub downloader_FileRenamed(ByVal sender As System.Object, ByVal e As FileRenamedEventArgs)
		Try
			If e Is Nothing OrElse String.IsNullOrEmpty(e.FileName) Then Return
            CompletedOutputPath(e.FileName)
            completedFileName = e.FileName
			If Not String.Equals(Me.NombreFichero, e.FileName, StringComparison.Ordinal) Then
				Log.WriteInfo("Final file name for " & Me.FileID & ": " & e.FileName)
				Me.NombreFichero = e.FileName
			End If
		Catch ex As Exception
			Log.WriteError("downloader_FileRenamed failed: " & Log.SafeException(ex))
		End Try
	End Sub
	
	Private Sub downloader_FileDownloadFailed(ByVal sender As System.Object, ByVal e As System.Exception)
		' 过期失败免疫：成功与失败经不同 worker 编组，到达顺序不定；文件已交付成功后，
		' 任何后到的失败都是过期事件，直接丢弃。注意与成功侧对称：成功守卫 <>Erroneo
		' （失败优先），失败侧守卫 <>Completado（成功终态优先）——两者都不翻转对方的终态。
		If Me.EstadoDescarga = Estado.Completado Then
			Log.WriteWarning("downloader_FileDownloadFailed: stale failure after success ignored for " & Me.FileID & ": " & Log.SafeException(e))
			Return
		End If
		Me.EstadoDescarga = Estado.Erroneo
		Dim Mensaje As String = ""
		Try
			' Si es un webexception intentamos sacar lo que ha devuelto el servidor...
			If TypeOf (e) Is Net.WebException AndAlso CType(e, Net.WebException).Response IsNot Nothing Then
				Using Stream = CType(e, Net.WebException).Response.GetResponseStream()
					Using reader = New IO.StreamReader(Stream)
						Mensaje = reader.ReadToEnd()
					End Using
				End Using
			End If
		Catch ex As Exception
			Log.WriteError("downloader_FileDownloadFailed: could not read server error response: " & Log.SafeException(ex))
		End Try
		Dim esQuota As Boolean = (TypeOf e Is MegaQuotaExceededException) OrElse FileDownloader.ReportQuotaIfMatch(e)
		If esQuota Then
			Me.EstablecerError("MEGA transfer quota exceeded (EOVERQUOTA / HTTP 509)." & vbNewLine & _
				" * File code: " & Me.FileID & vbNewLine & _
				" * Server response: " & Mensaje & vbNewLine & _
				" * Internal info: " & e.ToString, False, True)
		Else
			Me.EstablecerError("File download failed." & vbNewLine & _
				" * File code: " & Me.FileID & vbNewLine & _
				" * Error type: An error occurred while trying to download the file." & vbNewLine & _
				" * Server response: " & Mensaje & vbNewLine & _
				" * Internal info: " & e.ToString)
		End If
		Log.WriteError(Me.DescripcionError)
	End Sub
	
	Private Sub downloader_ChunkDownloadFailed(ByVal sender As System.Object, ByVal e As System.Exception)
		Dim Mensaje As String = ""
		Try
			' Si es un webexception intentamos sacar lo que ha devuelto el servidor...
			If TypeOf (e) Is Net.WebException AndAlso CType(e, Net.WebException).Response IsNot Nothing Then
				Using Stream = CType(e, Net.WebException).Response.GetResponseStream()
					Using reader = New IO.StreamReader(Stream)
						Mensaje = reader.ReadToEnd()
					End Using
				End Using
			End If
		Catch ex As Exception
			Log.WriteError("downloader_ChunkDownloadFailed: could not read server error response: " & Log.SafeException(ex))
		End Try
		Dim descError As String = "Chunk download failed. Reconnecting. " & vbNewLine & _
			" * File code: " & Me.FileID & vbNewLine & _
			" * Error type: An error occurred while trying to download the file." & vbNewLine & _
			" * Server response: " & Mensaje & vbNewLine & _
			" * Internal info: " & e.ToString
		Log.WriteError(descError)
		Me.UltimoErrorChunk = e
		'  No hacemos nada, dejamos que vuelva a reconectarse, excepto si ha habido demasiados errores
		' 终身计数器会把几小时里 31 次各自都自动重连成功的瞬时抖动判成"错误过多"而停掉任务。
		' 改为连续失败语义:距上次分块错误超过冷却窗口即视为已恢复,重新计数。
		If Me.FechaUltimoErrorChunk.HasValue AndAlso
		   (Now - Me.FechaUltimoErrorChunk.Value).TotalSeconds > Fichero.CHUNK_ERROR_COOLDOWN_SECONDS Then
			Me.NumErroresChunk = 0
		End If
		Me.FechaUltimoErrorChunk = Now
		Me.NumErroresChunk += 1
		If Me.NumErroresChunk > Fichero.NUM_MAX_ERRORES_CHUNK Then
			Me.Stop()
		End If
	End Sub
	
	Private Sub downloader_FileDownloadSucceeded(ByVal sender As System.Object, ByVal e As System.EventArgs)
		' B6: EnsurePathUnderRoot 会抛 UnauthorizedAccessException,GetFullPath 会抛
		' ArgumentException。原实现整个方法无 Catch,而 RaiseEvent 侧也没有,异常一旦
		' 逸出就会连下面的"完成态赋值"一起跳过 —— 真正下载完成却状态错乱、成品归属丢失。
		' 与兄弟处理器 downloader_FileRenamed 保持一致,在处理器内兜住并只记录日志。
        Try
            Dim source As FileDownloader = TryCast(sender, FileDownloader)
            If source IsNot Nothing AndAlso Not String.IsNullOrEmpty(source.CompletedFilePath) Then
                PathGuard.EnsurePathUnderRoot(Me.RutaLocal, source.CompletedFilePath, allowRoot:=False)
                completedFileName = System.IO.Path.GetFileName(source.CompletedFilePath)
                RecordVerifiedOutput(source.CompletedFilePath)
            End If
        Catch ex As Exception
            Log.WriteError("Completed output path could not be verified for " & Me.FileID & ": " & Log.SafeException(ex))
        End Try
		' This event fires as soon as the file is verified and renamed — before the Completed event.
		' Setting Completado here ensures the UI reflects the true state immediately, even if the
		' Completed event is delayed or the background worker's RunWorkerCompleted is slow to fire.
		' A delayed notification must also preserve extraction/verification already in progress.
		If Me.EstadoDescarga <> Estado.Erroneo AndAlso
		   Me.EstadoDescarga <> Estado.Descomprimiendo AndAlso
		   Me.EstadoDescarga <> Estado.ComprobandoMD5 Then
			Me.EstadoDescarga = Estado.Completado
		End If
	End Sub

	Private Sub downloader_Completed(ByVal sender As System.Object, ByVal e As System.EventArgs)
		If Me.EstadoDescarga <> Estado.Erroneo Then
			Log.WriteWarning("File " & Me.FileID & " downloaded.")
			Me.EstadoDescarga = Estado.Completado
			
			' Obsolete, MEGA doesn't check MD5
			If Not String.IsNullOrEmpty(Me.MD5) Then
				Log.WriteInfo("Verifying MD5...")
				Me.EstadoDescarga = Estado.ComprobandoMD5
				Dim Hash As String = ""
				Try
					Hash = MD5Utils.MD5CalcFile(PathGuard.GetSafeFilePathUnderRoot(Me.RutaLocal, Me.NombreFichero))
					If Hash.ToUpper = Me.MD5.ToUpper Then
						Me.EstadoDescarga = Estado.Completado
						Log.WriteInfo("MD5 file " & Me.FileID & " verified.")
					Else
						Me.EstablecerError("File downloaded correctly (aparently), but the MD5 verification has failed." & vbNewLine & _
							"This mean that the file is corrupted. Try opening and if it fails, download it again. " & _
							"After few tries if it is still corrupted, it could mean that it is corrupted on the server." & vbNewLine & _
							" * File code: " & Me.FileID & vbNewLine & _
							" * Error type: Error verifying MD5" & vbNewLine & _
							" * Internal info: calculated MD5 [" & Hash & "]; expected MD5 [" & Me.MD5 & "]")
						Log.WriteInfo("File " & Me.FileID & " checked, MD5 does not match.")
					End If
				Catch ex As Exception
					Log.WriteError("Error verifying MD5: " & Log.SafeException(ex))
					Me.EstablecerError("MD5 verification threw an exception; file cannot be marked complete." & vbNewLine & _
						" * File code: " & Me.FileID & vbNewLine & _
						" * Internal info: " & Log.SafeException(ex))
				End Try
				
			End If
		End If
		
		If Me.EstadoDescarga <> Estado.Erroneo Then
			' Descomprimimos fichero
			' Crear directorio al descomprimir?? De momento ponemos siempre que sí...
            If Me.ExtraccionFicheroAutomatica AndAlso _
                DescompresorController.GetController.AgregarElemento(Me.FileID, PathGuard.GetSafeFilePathUnderRoot(Me.RutaLocal, Me.NombreFichero), True, Me.DescargaExtraccionPassword) Then
                Me.EstadoDescarga = Estado.Descomprimiendo
            End If
		End If
		
	End Sub
	
	
	Private Sub downloader_Canceled(ByVal sender As System.Object, ByVal e As System.EventArgs)
		' B8: 上一轮取消的迟到事件不能把新一轮刚装配好的 downloader 清成 Nothing,
		' 也不能把正在进行的下载状态改回 EnCola。
		SyncLock Mutex.FicheroDownloader
			If Not Object.ReferenceEquals(Me.Downloader, sender) Then
				Log.WriteWarning("Ignored a late cancellation from a replaced downloader for " & Me.FileID)
				Return
			End If
			If Me.Downloader IsNot Nothing Then
				
				RemoveHandler Me.Downloader.Resumed, AddressOf downloader_Started
				RemoveHandler Me.Downloader.Started, AddressOf downloader_Started
				RemoveHandler Me.Downloader.Paused, AddressOf downloader_Paused
				RemoveHandler Me.Downloader.CancelRequested, AddressOf downloader_CancelRequested
				RemoveHandler Me.Downloader.Canceled, AddressOf downloader_Canceled
				RemoveHandler Me.Downloader.Completed, AddressOf downloader_Completed
				RemoveHandler Me.Downloader.FileDownloadSucceeded, AddressOf downloader_FileDownloadSucceeded
				RemoveHandler Me.Downloader.FileDownloadFailed, AddressOf downloader_FileDownloadFailed
			RemoveHandler Me.Downloader.ChunkDownloadFailed, AddressOf downloader_ChunkDownloadFailed
			RemoveHandler Me.Downloader.FileLocalCreated, AddressOf downloader_FileLocalCreated
			RemoveHandler Me.Downloader.FileRenamed, AddressOf downloader_FileRenamed
				
				Me.Downloader.Dispose()
				
			End If
			Me.Downloader = Nothing
		End SyncLock
		
		If Me.MarcadoParaBorrarFicheroLocal Then
			Me.BorrarFicheroLocal()
		End If
		
		If Me.NumErroresChunk > Fichero.NUM_MAX_ERRORES_CHUNK Then
			Dim lastIsQuota As Boolean = (TypeOf Me.UltimoErrorChunk Is MegaQuotaExceededException) OrElse IsQuotaErrorText(If(Me.UltimoErrorChunk Is Nothing, "", Me.UltimoErrorChunk.ToString))
			If lastIsQuota Then FileDownloader.ReportQuotaIfMatch(Me.UltimoErrorChunk)
			Dim descError As String = "Download stopped because there were too many connection errors (" & Me.NumErroresChunk & "). " & vbNewLine & _
				"Last error: " & vbNewLine & _
				" * File code: " & Me.FileID & vbNewLine & _
				" * Error type: Connection error." & vbNewLine & _
				" * Internal info: " & Me.UltimoErrorChunk.ToString
			Me.EstablecerError(descError, False, lastIsQuota)
			Log.WriteError(descError)
		Else
			' Publish the queueable state only after the old instance is detached
			' and disposed; a new startup must not reassemble that same instance.
			Me.EstadoDescarga = Estado.EnCola
		End If
		
		NotifyCancellationComplete()
	End Sub
	
	
	Public Sub ActualizarDatosDescarga()
		' B2-⑥:100% 回补的完整流程(MD5 全文件读+解压入队)不得在 FicheroDownloader/
		' ListaDescargas 锁内同步执行,否则 GB 级文件撞线时冻结 UI/调度数秒~数分钟。
		' 这里只在锁内预定(置 ComprobandoMD5 防重复投递),锁外投线程池真正执行。
		Dim needDeferredCompletion As Boolean = False
		SyncLock Mutex.FicheroDownloader
			If Me.Downloader IsNot Nothing Then
				
				Me.NumeroConexionesAbiertas = Me.Downloader.OpenConnections
				
				If Me.Downloader.File IsNot Nothing AndAlso Me.Downloader.File.Size > 0 Then
					Me.DatosPartes = Me.Downloader.File.GetDataPart
				End If
				
				Me.VelocidadKBs = CDec(Me.Downloader.DownloadSpeed / 1024)
				If Me.Downloader.CurrentFileSize > 0 Or Me.TamanoBytes = 0 Then
					' Si paramos y arrancamos de nuevo, CurrentFileSize tarda en inicializarse.
					Me.TamanoBytes = Me.Downloader.CurrentFileSize
				End If
				If Me.Downloader.CurrentFileProgress > 0 Or Me.BytesDescargados = 0 Then
					' Si paramos y arrancamos de nuevo, CurrentFileSize tarda en inicializarse.
					Me.BytesDescargados = Me.Downloader.CurrentFileProgress
					If Me.BytesDescargados > Me.TamanoBytes Then Me.BytesDescargados = Me.TamanoBytes ' Evitamos mostrar más de un 100%
				End If
				If Me.TamanoBytes > 0 Then
					Me.Porcentaje = CDec(100 * Me.BytesDescargados / Me.TamanoBytes)
				End If

				' Fallback: if the download is at 100% and AllFinished is set, but the state
				' is still Descargando (because the Completed/FileDownloadSucceeded event was
				' delayed or missed), correct it here. This is a safety net for Bug 2.
				' NOTE: we must run the full completion flow (MD5 verification + automatic
				' extraction), not just flip the state — otherwise the UI shows "complete"
				' while integrity is never checked and auto-extract never fires.
				' B2-⑥:不得在此(双全局锁内)同步跑 MD5,只做预定并延迟到线程池执行。
				If Me.EstadoDescarga = Estado.Descargando AndAlso
				   Me.TamanoBytes > 0 AndAlso
				   Me.BytesDescargados >= Me.TamanoBytes AndAlso
				   Me.Downloader.File IsNot Nothing AndAlso
				   Me.Downloader.File.DataPartInitialized AndAlso
				   Me.Downloader.File.GetDataPart.AllFinished AndAlso
				   Not Me.Downloader.IsBusy Then
					Log.WriteWarning("ActualizarDatosDescarga: download is 100% and AllFinished but state was still Descargando. Queueing deferred completion flow. File: " & Me.FileID)
					Me.EstadoDescarga = Estado.ComprobandoMD5
					needDeferredCompletion = True
				End If
			End If
			If EstadoDescarga = Estado.Descargando Then
				If VelocidadKBs = 0 Then
					TiempoEstimadoDescarga = " --- "
				Else
					Dim BytesRestantes As Decimal = TamanoBytes - (Porcentaje * TamanoBytes / 100)
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
			Else
				TiempoEstimadoDescarga = ""
			End If
		End SyncLock
		If needDeferredCompletion Then
			Try
				System.Threading.ThreadPool.QueueUserWorkItem(AddressOf RunDeferredCompletion)
			Catch ex As Exception
				Log.WriteError("ActualizarDatosDescarga: failed to queue deferred completion: " & Log.SafeException(ex))
			End Try
		End If
	End Sub

	''' <summary>
	''' B2-⑥:锁外执行的延迟补完流程(对应 ActualizarDatosDescarga 内预定的回补)。
	''' 在线程池运行,可安全做 MD5 全文件读与解压入队,不阻塞 ListaDescargas 调度与 UI 刷新。
	''' downloader_Completed 内部自带 Estado 非 Erroneo 守卫,重复投递 harmless(第二次 MD5 命中已 Completado 可接受)。
	''' </summary>
	Private Sub RunDeferredCompletion(ByVal state As Object)
		Try
			Me.downloader_Completed(Nothing, EventArgs.Empty)
		Catch ex As Exception
			Log.WriteError("RunDeferredCompletion failed: " & Log.SafeException(ex))
		End Try
	End Sub
	
	Friend Sub EstablecerError(ByVal msj As String)
		EstablecerError(msj, IsPermanentErrorText(msj), IsQuotaErrorText(msj))
	End Sub

	Private Sub EstablecerError(ByVal msj As String, ByVal esPermanente As Boolean, ByVal esQuota As Boolean)
		Me.EstadoDescarga = Estado.Erroneo
		Me.DescripcionError = msj
		Me.FechaUltimoError = Now
		Me.EsErrorPermanente = esPermanente
		Me.FailedByQuota = esQuota
		If esQuota Then Me.EsErrorPermanente = False
	End Sub

	''' <summary>v2.5 beta: 永久失败码 -9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED。仅匹配错误码语义,不做泛文本匹配。</summary>
	Friend Shared Function IsPermanentErrorText(s As String) As Boolean
		If String.IsNullOrEmpty(s) Then Return False
		If s.IndexOf("-9", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("ENOENT", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		If s.IndexOf("-11", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("EACCESS", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		If s.IndexOf("-14", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("EKEY", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		If s.IndexOf("-16", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("EBLOCKED", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		' 第11项(唯一的文本匹配例外):MEGA MetaMAC 校验失败(算法修正后 mismatch 即真损坏)
		' 必须按永久失败处理 —— 否则自愈(默认开、15 分钟周期)每轮把整个文件重下一遍、
		' 烧等量 MEGA 配额,而旧提示语承诺的 "从上次有效分块继续" 并不存在(分块被整体清零)。
		' 用户仍可右键 "重试全部失败" 手动重试。
		If s.IndexOf("MetaMAC verification failed", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		Return False
	End Function

	Friend Shared Function IsQuotaErrorText(s As String) As Boolean
		If String.IsNullOrEmpty(s) Then Return False
		If s.IndexOf("EOVERQUOTA", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		If s.IndexOf("MegaQuotaExceededException", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		If s.IndexOf("transfer quota exceeded", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
		Return False
	End Function

	''' <summary>
	''' 重设下载状态：清理 DatosPartes/BytesDescargados/Porcentaje 并删除残留的 .part 文件。
	''' 用于从错误状态恢复，确保下次能从头下载而不是卡在"100% 但验证失败"的死循环。
	''' P0-1:preserveProgress=True 时保留分块进度与 .part(自愈/配额唤醒用),只清错误记账,
	''' 避免"超时→删.part→重下→再超时"无限循环;手动重置仍用默认 False 从头来。
	''' </summary>
	Public Sub ResetearDescarga(Optional ByVal preserveProgress As Boolean = False)
        CaptureDownloaderProgress()
		If preserveProgress Then
			Me.NumErroresChunk = 0
			Me.UltimoErrorChunk = Nothing
			Me.DescripcionError = Nothing
			Me.FechaUltimoError = Nothing
			Me.EsErrorPermanente = False
			Me.FailedByQuota = False
			Me.TiempoEstimadoDescarga = ""
			Return
		End If
		' 重置分块状态：AllFinished=False 且所有 chunk 回到未下载
		If Me.DatosPartes IsNot Nothing Then
			Me.DatosPartes.AllFinished = False
			If Me.DatosPartes.ChunkList IsNot Nothing Then
				For Each c As FileDownloader.DataPart.Chunk In Me.DatosPartes.ChunkList
					c.Index = 0
					c.SyncedIndex = 0
					c.Available = True
				Next
			End If
		End If
		Me.BytesDescargados = 0
		Me.Porcentaje = 0
		Me.NumErroresChunk = 0
		Me.UltimoErrorChunk = Nothing
		Me.DescripcionError = Nothing
		Me.FechaUltimoError = Nothing
		Me.EsErrorPermanente = False
		Me.FailedByQuota = False
		Me.TiempoEstimadoDescarga = ""

		' 删除可能残留的 .part 文件（可能已损坏或处于不一致状态）
		If Not String.IsNullOrWhiteSpace(Me.RutaLocal) AndAlso Not String.IsNullOrWhiteSpace(Me.NombreFichero) Then
			Try
				Dim partPath As String = FileDownloader.GetPartialFilePath(Me.RutaLocal, Me.NombreFichero, Me.PartialFileName)
				If System.IO.File.Exists(partPath) Then
					System.IO.File.Delete(partPath)
					Log.WriteInfo("Deleted .part file during reset: " & partPath)
				End If
			Catch ex As Exception
				Log.WriteWarning("Could not delete .part file during reset: " & Log.SafeException(ex))
			End Try
		End If
	End Sub
	
	
	#End Region
	
	
	
	#Region "IDisposable Support"
	Private disposedValue As Boolean ' To detect redundant calls
	
	' IDisposable
	Protected Overridable Sub Dispose(disposing As Boolean)
		If Not Me.disposedValue Then
			' B1-④:释放即取消验证,防关机后复活。
			Me._StartupCancelled = True
			If disposing Then
				Try
					If Me.Downloader IsNot Nothing Then
						Me.Downloader.Dispose()
						Me.Downloader = Nothing
					End If
				Catch ex As Exception
					Log.WriteError("Error disposing downloader: " & Log.SafeException(ex))
				End Try
				' bgArranque is a BackgroundWorker that is only disposed in its
				' RunWorkerCompleted handler. If the Fichero is disposed while the
				' startup worker is still running (e.g. app closed during link
				' resolution), RunWorkerCompleted never fires and the worker leaks.
				Try
					If Me.bgArranque IsNot Nothing Then
						' bgArranque.WorkerSupportsCancellation=False,调 CancelAsync 必抛
						' InvalidOperationException;取消靠入口处的 _StartupCancelled 标志,
						' 这里只释放不取消。
						Me.bgArranque.Dispose()
						Me.bgArranque = Nothing
					End If
				Catch ex As Exception
					Log.WriteError("Error disposing bgArranque: " & Log.SafeException(ex))
				End Try
			End If
		End If
		Me.disposedValue = True
	End Sub
	
	' This code added by Visual Basic to correctly implement the disposable pattern.
	Public Sub Dispose() Implements IDisposable.Dispose
		' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
		Dispose(True)
		GC.SuppressFinalize(Me)
	End Sub
	#End Region
	
	
	
	
    Public Sub CargarXML(ByVal XML As XmlNode)

        ' IsNumeric 放行超出 Int32 的值(如 "2147483648"/"1e10"),CInt 会抛 OverflowException
        ' 并让整个下载列表加载失败。与下面日期字段同样按"单个坏字段不拖垮整份队列"处理。
        Dim versionAttr As Integer = 1
        If XML.Attributes("v") IsNot Nothing AndAlso Integer.TryParse(XML.Attributes("v").Value, versionAttr) Then
            Version = versionAttr
        Else
            Version = 1
        End If

        CacheSecureData = Nothing
        Dim loadedCache As New Cache()
        Me._FileID = Criptografia.DecryptStringWithCacheInfo(LeerNodo(XML, "FileID", ""), loadedCache.FileID)
        Me._FileKey = Criptografia.DecryptStringWithCacheInfo(LeerNodo(XML, "FileKey", ""), loadedCache.FileKey)
        Me._URL = Criptografia.DecryptStringWithCacheInfo(LeerNodo(XML, "URL", ""), loadedCache.URL)
        Me._URLFichero = Criptografia.DecryptStringWithCacheInfo(LeerNodo(XML, "URLFichero", ""), loadedCache.URLFichero)

        ' Hacemos el link no visible
        Dim UrlPlain As String = Criptografia.ToInsecureString(Me._URL)
        Me.LinkVisible = Not UrlPlain.StartsWith(HIDDEN_LINK, StringComparison.Ordinal)
        If Not Me.LinkVisible Then
            Me._URL.Dispose()
            Me._URL = Criptografia.ToSecureString(UrlPlain.Replace(HIDDEN_LINK, ""))
        End If
        ' Reuse only successfully decrypted current-format ciphertext. Historical
        ' entropy values and failed/missing fields must still be migrated on save.
        If loadedCache.FileID IsNot Nothing AndAlso loadedCache.FileKey IsNot Nothing AndAlso
           loadedCache.URL IsNot Nothing AndAlso loadedCache.URLFichero IsNot Nothing Then
            CaptureCacheSources(loadedCache)
            CacheSecureData = loadedCache
        End If


        Me.NombreFichero = LeerNodo(XML, "NombreFichero", "")
        Me.PartialFileName = LeerNodo(XML, "PartialFileName", "")
        If Not String.IsNullOrEmpty(Me.PartialFileName) Then
            Try
                FileDownloader.ValidatePartialFileName(Me.PartialFileName)
            Catch ex As ArgumentException
                Log.WriteWarning("Invalid persisted partial file name; assigning a new task file.")
                Me.PartialFileName = FileDownloader.CreatePartialFileName()
            End Try
        End If
        Me.RutaRelativa = LeerNodo(XML, "RutaRelativa", "")
        Me.RutaLocal = LeerNodo(XML, "RutaLocal", "")
        Dim completedNode As XmlNode = XML.SelectSingleNode("CompletedFileName")
        completedFileName = If(completedNode Is Nothing, Nothing, completedNode.InnerText)
        If Not String.IsNullOrEmpty(completedFileName) Then
            Try
                CompletedOutputPath(completedFileName)
            Catch ex As ArgumentException
                completedFileName = ""
                Log.WriteWarning("Invalid persisted completed file name; keeping existing files.")
            Catch ex As UnauthorizedAccessException
                completedFileName = ""
                Log.WriteWarning("Completed file path is outside the task directory; keeping existing files.")
            Catch ex As System.IO.IOException
                completedFileName = ""
                Log.WriteWarning("Unreadable persisted completed file path; keeping existing files.")
            End Try
        End If
        Long.TryParse(LeerNodo(XML, "TamanoBytes", "0"), TamanoBytes)
        Integer.TryParse(LeerNodo(XML, "NumeroConexionesAbiertas", "0"), NumeroConexionesAbiertas)
        Integer.TryParse(LeerNodo(XML, "NumeroChunksAsignados", "0"), NumeroChunksAsignados)
        Dim strFecha As String = LeerNodo(XML, "FechaUltimoError", "")
        If Not String.IsNullOrEmpty(strFecha) Then
            ' CDate 区域性相关且会抛异常:单个损坏的队列文件不应导致整个下载列表加载失败(启动即崩)
            Dim fecha As Date
            If Date.TryParse(strFecha, fecha) OrElse Date.TryParse(strFecha, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, fecha) Then
                FechaUltimoError = fecha
            Else
                Log.WriteWarning("Invalid FechaUltimoError in queue XML, ignored: " & strFecha)
            End If
        End If
        Decimal.TryParse(LeerNodo(XML, "Porcentaje", "0"), Porcentaje)
        Boolean.TryParse(LeerNodo(XML, "DescargaComenzada", "false"), DescargaComenzada)
        Boolean.TryParse(LeerNodo(XML, "DescargaProcesada", "false"), DescargaProcesada)
        Boolean.TryParse(LeerNodo(XML, "ExtraccionFicheroAutomatica", "false"), ExtraccionFicheroAutomatica)
        Dim tempPass As String = LeerNodo(XML, "ExtraccionFicheroPassword", "")
        If Not String.IsNullOrEmpty(tempPass) Then
            ' Decrypt with DPAPI (current scheme, machine/user bound).
            Dim decrypted As String = ""
            Try
                decrypted = Criptografia.ToInsecureString(Criptografia.DecryptString_DPAPI(tempPass))
            Catch ex As Exception
                Log.WriteWarning("ExtraccionFicheroPassword: DPAPI decrypt failed, trying legacy AES fallback: " & ex.Message)
            End Try
            ' Legacy fallback for queue files saved before the DPAPI migration.
            ' Kept for backward compatibility only; new saves always use DPAPI.
            If String.IsNullOrEmpty(decrypted) Then
                decrypted = Criptografia.AES_DecryptString(tempPass, "passZIP")
            End If
            If Not String.IsNullOrEmpty(decrypted) Then
                Me.ExtraccionFicheroPassword = decrypted
            End If
        End If


        Integer.TryParse(LeerNodo(XML, "Prioridad", "0"), Prioridad)



        Decimal.TryParse(LeerNodo(XML, "VelocidadKBs", "0"), VelocidadKBs)
        Long.TryParse(LeerNodo(XML, "BytesDescargados", "0"), BytesDescargados)

        EstadoDescarga = Estado.EnCola
        If [Enum].IsDefined(GetType(Estado), LeerNodo(XML, "EstadoDescarga", "")) Then
            EstadoDescarga = CType([Enum].Parse(GetType(Estado), LeerNodo(XML, "EstadoDescarga", "")), Estado)
        End If
        ' RC:重启后查看错误空白/永久失败诈尸的根因——此前只存 Estado,不存描述与标记。
        Me.DescripcionError = LeerNodo(XML, "DescripcionError", "")
        If String.IsNullOrEmpty(Me.DescripcionError) Then Me.DescripcionError = Nothing
        Boolean.TryParse(LeerNodo(XML, "EsErrorPermanente", "false"), EsErrorPermanente)
        Boolean.TryParse(LeerNodo(XML, "FailedByQuota", "false"), FailedByQuota)
        ' 兼容旧版派生标记:老队列无标记时按描述文本回推,避免重启后永久失败被自愈捞起。
        If Not EsErrorPermanente AndAlso Not FailedByQuota AndAlso Not String.IsNullOrEmpty(Me.DescripcionError) Then
            EsErrorPermanente = IsPermanentErrorText(Me.DescripcionError)
            If Not EsErrorPermanente Then FailedByQuota = IsQuotaErrorText(Me.DescripcionError)
            If FailedByQuota Then EsErrorPermanente = False
        End If
        Boolean.TryParse(LeerNodo(XML, "MarcadoParaBorrarFicheroLocal", "false"), MarcadoParaBorrarFicheroLocal)
        Boolean.TryParse(LeerNodo(XML, "DescargaIndividual", "false"), DescargaIndividual)
        Boolean.TryParse(LeerNodo(XML, "PausaIndividual", "false"), PausaIndividual)
        Me.TiempoEstimadoDescarga = LeerNodo(XML, "TiempoEstimadoDescarga", "")
        Integer.TryParse(LeerNodo(XML, "LimiteVelocidad", "0"), LimiteVelocidad)

        If XML.SelectSingleNode("DatosPartes") IsNot Nothing Then
            Dim NodoDatosPartes As XmlNode = XML.SelectSingleNode("DatosPartes")
            Me.DatosPartes = New FileDownloader.DataPart
            Boolean.TryParse(LeerNodo(NodoDatosPartes, "AllFinished", "false"), Me.DatosPartes.AllFinished)
            If NodoDatosPartes.SelectSingleNode("ChunkList") IsNot Nothing Then
                Dim NodoChunkList As XmlNode = NodoDatosPartes.SelectSingleNode("ChunkList")
                Me.DatosPartes.ChunkList = New Generic.List(Of FileDownloader.DataPart.Chunk)
                For Each NodoChunk As XmlNode In NodoChunkList.SelectNodes("Chunk")
                    Dim Chunk As New FileDownloader.DataPart.Chunk
                    Long.TryParse(LeerNodo(NodoChunk, "Index", "0"), Chunk.Index)
                    Long.TryParse(LeerNodo(NodoChunk, "Size", "0"), Chunk.Size)
                    Long.TryParse(LeerNodo(NodoChunk, "StartIndex", "0"), Chunk.StartIndex)
                    Boolean.TryParse(LeerNodo(NodoChunk, "Available", "false"), Chunk.Available)
                    ' 方案B：旧版本 XML 无 SyncedIndex 记账，但历史进度在旧版每次落盘都 fsync，
                    ' 直接继承为已同步边界，避免升级后重启把存量进度钳到 0 重下。
                    Chunk.SyncedIndex = Chunk.Index
                    Me.DatosPartes.ChunkList.Add(Chunk)
                Next
            End If
            ' Do not trust AllFinished without a consistent layout / full progress
            If Me.DatosPartes.ChunkList Is Nothing OrElse Me.DatosPartes.ChunkList.Count = 0 Then
                Me.DatosPartes.AllFinished = False
            ElseIf Me.TamanoBytes > 0 Then
                Me.DatosPartes.ValidateAndNormalize(Me.TamanoBytes)
            Else
                Dim anyIncomplete As Boolean = False
                For Each c As FileDownloader.DataPart.Chunk In Me.DatosPartes.ChunkList
                    If c.Index <> c.Size Then anyIncomplete = True
                Next
                If anyIncomplete Then Me.DatosPartes.AllFinished = False
            End If
        End If

    End Sub



    Public Function GuardarXML(ByVal XML As XmlDocument, IncluirDatosCifrados As Boolean) As XmlNode
        ' B2-⑩:序列化与 ActualizarInformacionFichero 回写同锁,防关机撕裂(半新半旧队列)。
        ' 锁序与 ActualizarDatosDescarga 一致(ListaDescargas->FicheroDownloader),无反转死锁。
        SyncLock Mutex.FicheroDownloader
        Dim NodoFic As XmlNode = XML.CreateElement("Fichero")
        NodoFic.Attributes.Append(XML.CreateAttribute("v")).Value = Me.Version.ToString

        If IncluirDatosCifrados Then
            If CacheSecureData Is Nothing OrElse Not CacheSecureData.Matches(Me) Then RegenerateCacheSecureData()

            NodoFic.AppendChild(XML.CreateElement("FileID")).InnerText = CacheSecureData.FileID
            NodoFic.AppendChild(XML.CreateElement("FileKey")).InnerText = CacheSecureData.FileKey
            NodoFic.AppendChild(XML.CreateElement("URL")).InnerText = CacheSecureData.URL
            NodoFic.AppendChild(XML.CreateElement("URLFichero")).InnerText = CacheSecureData.URLFichero
        Else
            NodoFic.AppendChild(XML.CreateElement("FileID")).InnerText = Criptografia.ToInsecureString(_FileID)
        End If
        NodoFic.AppendChild(XML.CreateElement("NombreFichero")).InnerText = NombreFichero
        NodoFic.AppendChild(XML.CreateElement("PartialFileName")).InnerText = PartialFileName
        If completedFileName IsNot Nothing Then NodoFic.AppendChild(XML.CreateElement("CompletedFileName")).InnerText = completedFileName
        NodoFic.AppendChild(XML.CreateElement("RutaLocal")).InnerText = RutaLocal
        NodoFic.AppendChild(XML.CreateElement("RutaRelativa")).InnerText = RutaRelativa
        NodoFic.AppendChild(XML.CreateElement("TamanoBytes")).InnerText = TamanoBytes.ToString
        NodoFic.AppendChild(XML.CreateElement("NumeroConexionesAbiertas")).InnerText = NumeroConexionesAbiertas.ToString
        NodoFic.AppendChild(XML.CreateElement("NumeroChunksAsignados")).InnerText = NumeroChunksAsignados.ToString
        If FechaUltimoError.HasValue Then
            NodoFic.AppendChild(XML.CreateElement("FechaUltimoError")).InnerText = FechaUltimoError.Value.ToString("s")
        End If
        NodoFic.AppendChild(XML.CreateElement("Porcentaje")).InnerText = Porcentaje.ToString
        NodoFic.AppendChild(XML.CreateElement("DescargaComenzada")).InnerText = DescargaComenzada.ToString
        NodoFic.AppendChild(XML.CreateElement("DescargaProcesada")).InnerText = DescargaProcesada.ToString
        NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroAutomatica")).InnerText = ExtraccionFicheroAutomatica.ToString

        ' DPAPI 密文(随机盐,每次不同)只写真盘、不进比对快照,理由同 Paquete.GuardarXML。
        If IncluirDatosCifrados AndAlso Not String.IsNullOrEmpty(ExtraccionFicheroPassword) Then
            NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroPassword")).InnerText = Criptografia.EncryptString_DPAPI(Criptografia.ToSecureString(ExtraccionFicheroPassword))
        End If
        If Not IncluirDatosCifrados Then
            NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroPasswordHash")).InnerText = Utils.SecretFingerprint(ExtraccionFicheroPassword)
        End If

        NodoFic.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString
        NodoFic.AppendChild(XML.CreateElement("VelocidadKBs")).InnerText = VelocidadKBs.ToString
        NodoFic.AppendChild(XML.CreateElement("BytesDescargados")).InnerText = BytesDescargados.ToString

        NodoFic.AppendChild(XML.CreateElement("EstadoDescarga")).InnerText = [Enum].GetName(GetType(Estado), EstadoDescarga)
        ' RC:持久化错误描述与标记(重启后查看错误不再空白,永久/配额标记不再丢失)。
        ' 描述含内部堆栈,截断到 4000 字防队列文件膨胀;标记缺省 false 兼容旧版。
        Dim descToSave As String = If(DescripcionError, "")
        If descToSave.Length > 4000 Then descToSave = descToSave.Substring(0, 4000)
        NodoFic.AppendChild(XML.CreateElement("DescripcionError")).InnerText = descToSave
        NodoFic.AppendChild(XML.CreateElement("EsErrorPermanente")).InnerText = EsErrorPermanente.ToString
        NodoFic.AppendChild(XML.CreateElement("FailedByQuota")).InnerText = FailedByQuota.ToString
        NodoFic.AppendChild(XML.CreateElement("MarcadoParaBorrarFicheroLocal")).InnerText = MarcadoParaBorrarFicheroLocal.ToString
        NodoFic.AppendChild(XML.CreateElement("TiempoEstimadoDescarga")).InnerText = TiempoEstimadoDescarga
        NodoFic.AppendChild(XML.CreateElement("PausaIndividual")).InnerText = PausaIndividual.ToString
        NodoFic.AppendChild(XML.CreateElement("DescargaIndividual")).InnerText = DescargaIndividual.ToString
        NodoFic.AppendChild(XML.CreateElement("LimiteVelocidad")).InnerText = LimiteVelocidad.ToString

        If Me.DatosPartes IsNot Nothing Then
            Dim NodoDatosPartes As XmlNode = NodoFic.AppendChild(XML.CreateElement("DatosPartes"))
            NodoDatosPartes.AppendChild(XML.CreateElement("AllFinished")).InnerText = Me.DatosPartes.AllFinished.ToString
            If Me.DatosPartes.ChunkList IsNot Nothing Then
                Dim NodoChunkList As XmlNode = NodoDatosPartes.AppendChild(XML.CreateElement("ChunkList"))
                For Each chunk As FileDownloader.DataPart.Chunk In Me.DatosPartes.ChunkList
                    Dim NodoChunk As XmlNode = NodoChunkList.AppendChild(XML.CreateElement("Chunk"))
                    NodoChunk.AppendChild(XML.CreateElement("StartIndex")).InnerText = chunk.StartIndex.ToString
                    NodoChunk.AppendChild(XML.CreateElement("Size")).InnerText = chunk.Size.ToString
                    ' 方案B 红线：只持久化已 fsync 的边界。内存 Index 可能超前（节流窗口内），
                    ' 取 Min 后断电重启最多重下窗口内数据，永不出现空洞。
                    Dim persistedIndex As Long = Math.Min(chunk.Index, chunk.SyncedIndex)
                    If persistedIndex < 0 Then persistedIndex = 0
                    NodoChunk.AppendChild(XML.CreateElement("Index")).InnerText = persistedIndex.ToString
                    NodoChunk.AppendChild(XML.CreateElement("Available")).InnerText = chunk.Available.ToString
                Next
            End If

        End If

        Return NodoFic
        End SyncLock

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
