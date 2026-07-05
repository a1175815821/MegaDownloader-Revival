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
	
    Public RutaLocal As String

    Public RutaRelativa As String
	
	Public TamanoBytes As Long
	
	Public NumeroConexionesAbiertas As Integer
	
	Public NumeroChunksAsignados As Integer
	
	Public MD5 As String
	
	Public LinkVisible As Boolean
	
	Public DescripcionError As String
	
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
	
	Private WithEvents Downloader As FileDownloader
	
	Public Event CancellationComplete As EventHandler
	
	Public TiempoEstimadoDescarga As String
	
	Public PausaIndividual As Boolean
	
	Public DescargaIndividual As Boolean
	
	Public DatosPartes As FileDownloader.DataPart
	
	Private NumErroresChunk As Integer
	
	Public LimiteVelocidad As Integer
	
    Private UltimoErrorChunk As Exception

    Private Version As Integer

    Private Const NUM_MAX_ERRORES_CHUNK As Integer = 100
	
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
        Public OptionalPassword As String
    End Class
    Private CacheSecureData As Cache = Nothing
    Private Sub RegenerateCacheSecureData()
        Dim c As New Cache
        c.FileID = Criptografia.EncryptString_DPAPI(_FileID)
        c.FileKey = Criptografia.EncryptString_DPAPI(_FileKey)
        c.URL = Criptografia.EncryptString_DPAPI(Criptografia.ToSecureString(If(LinkVisible, "", HIDDEN_LINK) & Criptografia.ToInsecureString(Me._URL)))
        c.URLFichero = Criptografia.EncryptString_DPAPI(_URLFichero)
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
	Public Sub ActualizarInformacionFichero(ByVal Config As Configuracion, ByRef ErrorObtenido As Conexion.TipoError, ByVal ComprobacionAntesDescarga As boolean)
		If Not _Actualizando Then
			_Actualizando = True
			Try
				Dim EstadoAnterior As Estado = Me.DescargaEstado
				Me.SetDescargaEstado = Estado.Verificando
				
				Dim Info As Conexion.InformacionFichero = Conexion.ObtenerInformacionFichero(Config, Me.FileID, Me.FileKey, ComprobacionAntesDescarga)
				If Info IsNot Nothing AndAlso Info.Err = Conexion.TipoError.SinErrores Then
					If Not String.IsNullOrEmpty(Info.URL) then Me.URLFichero = Info.URL
					If Info.Tamano  > 0 Then Me.TamanoBytes = Info.Tamano
					If Not String.IsNullOrEmpty(Info.Nombre) then Me.NombreFichero = Info.Nombre
					Me.MD5 = Info.MD5
					Me.DescargaProcesada = True
					Me.SetDescargaEstado = EstadoAnterior
					Me.FileKey = Info.FileKey
					Me.FileID = Info.FileID
				ElseIf Info IsNot Nothing AndAlso Info.Err <> Conexion.TipoError.SinErrores Then
					ErrorObtenido = Info.Err
					Me.EstablecerError("The file could not be verified." & vbNewLine & _
						" * File code: " & Me.FileID & vbNewLine & _
						" * Error type: " & Info.Err.ToString & vbNewLine & _
						" * Internal info: " & Info.Errtxt)
				End If
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
		If Me.MarcadoParaBorrarFicheroLocal Then
			Mutex.DeletingFiles.WaitOne()
			Dim Ruta As String = String.Empty
			Try
				Ruta = System.IO.Path.Combine(Me.RutaLocal, Me.NombreFichero)
				If System.IO.File.Exists(Ruta) Then
					System.IO.File.Delete(Ruta)
				End If
				Ruta = System.IO.Path.Combine(Me.RutaLocal, Me.NombreFichero & ".part")
				If System.IO.File.Exists(Ruta) Then
					System.IO.File.Delete(Ruta)
				End If
			Catch e As Exception
				Log.WriteError("Error deleting local files - Path: " & Ruta & " - Error: " & e.ToString)
                MessageBox.Show(Language.GetText("Error deleting local files - Path: %PATH% - Error: %ERROR%").Replace("%PATH%", Ruta).Replace("%ERROR%", e.Message), _
                    Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
			Finally
				Mutex.DeletingFiles.ReleaseMutex()
			End Try
		End If
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
	Private Sub bgArranque_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles bgArranque.DoWork
		Try
			
			
			Dim worker As DownloadWorker = CType(sender, DownloadWorker)
			
			Dim ErrAct As Conexion.TipoError = Conexion.TipoError.SinErrores
			Me.ActualizarInformacionFichero(worker.Config, ErrAct, True)
			
			If ErrAct <> Conexion.TipoError.SinErrores Then
				' La función ActualizarInformacionFichero ya ha actualizado el estado de la descarga, no hacemos nada
				Exit Sub
			End If
			
			Me.NumErroresChunk = 0
			Me.Downloader = New FileDownloader(True)
			
			AddHandler Me.Downloader.Resumed, AddressOf downloader_Started
			AddHandler Me.Downloader.Started, AddressOf downloader_Started
			AddHandler Me.Downloader.Paused, AddressOf downloader_Paused
			AddHandler Me.Downloader.CancelRequested, AddressOf downloader_CancelRequested
			AddHandler Me.Downloader.Canceled, AddressOf downloader_Canceled
			AddHandler Me.Downloader.Completed, AddressOf downloader_Completed
			AddHandler Me.Downloader.FileDownloadFailed, AddressOf downloader_FileDownloadFailed
			AddHandler Me.Downloader.ChunkDownloadFailed, AddressOf downloader_ChunkDownloadFailed
			AddHandler Me.Downloader.FileLocalCreated, AddressOf downloader_FileLocalCreated

            Me.Downloader.setBufferAndPackageSize(worker.Config.TamanoBufferKB * 1024, worker.Config.TamanoPaqueteKB * 1024)

            'Me.Downloader.PackageSize = worker.Config.TamanoPaqueteKB * 1024
            'Me.Downloader.BufferSize = worker.Config.TamanoBufferKB * 1024
            Me.Downloader.StopWatchCyclesAmount = 30
			Me.Downloader.LocalDirectory = Me.RutaLocal
			Me.Downloader.DeleteFilesAfterCancel = False
			Me.Downloader.DeleteCompletedFilesAfterCancel = False
			Me.Downloader.PartsPerFile = worker.Config.ConexionesPorFichero
			Me.Downloader.NumConnections = If(worker.Config.ConexionesPorFichero < worker.NumConexionesDisponibles, worker.Config.ConexionesPorFichero, worker.NumConexionesDisponibles)
			Me.Downloader.AddFileInfo(Me.FileID, Me.FileKey, Me.URLFichero, Me.DescargaNombre, Me.DatosPartes)
			
			If Me.Downloader.CanStart Then
				Me.Downloader.Start()
			End If
		Catch ex As Exception
			Log.WriteError("Error on bgArranque_DoWork: " & ex.ToString)
			bgArranque = Nothing
		End Try
	End Sub
	Private Sub bgArranque_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgArranque.RunWorkerCompleted
		bgArranque.Dispose()
		bgArranque = Nothing
	End Sub
	
	
	Public Sub Start(ByRef Config As Configuracion, NumConexionesDisponibles As Integer)
		If Estado.Erroneo = Me.DescargaEstado Then Exit Sub
		
		If Me.Downloader Is Nothing And bgArranque Is Nothing Then
			Me.EstadoDescarga = Estado.CreandoLocal
			Me.DescargaComenzada = True
			bgArranque = New DownloadWorker(Config, NumConexionesDisponibles)
			bgArranque.WorkerSupportsCancellation = False
			bgArranque.WorkerReportsProgress = False
			bgArranque.RunWorkerAsync()
			
		ElseIf Me.Downloader IsNot Nothing AndAlso Me.Downloader.CanStart Then
			Me.EstadoDescarga = Estado.CreandoLocal
			Me.DescargaComenzada = True
			Me.Downloader.Start()
		End If
	End Sub
	
	Public Sub [Resume]()
		If Me.Downloader IsNot Nothing AndAlso Me.Downloader.CanResume Then
			Me.Downloader.Resume()
		End If
	End Sub
	
	Public Sub Pause()
		If Me.Downloader IsNot Nothing AndAlso Me.Downloader.CanPause Then
			Me.Downloader.Pause()
		End If
	End Sub
	
	Public Sub [Stop]()
		Dim CiclosMax As Integer = 100 ' 100*50 = 5 segundos
		Dim Ciclos As Integer = 0
		While Me._Actualizando And Ciclos < CiclosMax
			System.Threading.Thread.Sleep(50)
			Ciclos += 1
		End While
		If Me.Downloader Is Nothing Then
			RaiseEvent CancellationComplete(Me, New EventArgs)
		ElseIf Me.Downloader.CanStop Then
			Me.Downloader.Stop()
		End If
	End Sub
	
	Public Sub DescompresionFinalizada()
		If Me.EstadoDescarga = Estado.Descomprimiendo Then
			Me.EstadoDescarga = Estado.Completado
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
	
	Private Sub downloader_FileDownloadFailed(ByVal sender As System.Object, ByVal e As System.Exception)
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
		End Try
		Me.EstablecerError("File download failed." & vbNewLine & _
			" * File code: " & Me.FileID & vbNewLine & _
			" * Error type: An error occurred while trying to download the file." & vbNewLine & _
			" * Server response: " & Mensaje & vbNewLine & _
			" * Internal info: " & e.ToString)
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
		End Try
		Dim descError As String = "Chunk download failed. Reconnecting. " & vbNewLine & _
			" * File code: " & Me.FileID & vbNewLine & _
			" * Error type: An error occurred while trying to download the file." & vbNewLine & _
			" * Server response: " & Mensaje & vbNewLine & _
			" * Internal info: " & e.ToString
		Log.WriteError(descError)
		Me.UltimoErrorChunk = e
		'  No hacemos nada, dejamos que vuelva a reconectarse, excepto si ha habido demasiados errores
		Me.NumErroresChunk += 1
		If Me.NumErroresChunk > Fichero.NUM_MAX_ERRORES_CHUNK Then
			Me.Stop()
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
					Hash = MD5Utils.MD5CalcFile(IO.Path.Combine(Me.RutaLocal, Me.NombreFichero))
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
					Log.WriteError("Error verifying MD5: " & ex.ToString)
					' Que hacemos??
					' De momento decimos que está ok...
					Me.EstadoDescarga = Estado.Completado
				End Try
				
			End If
		End If
		
		If Me.EstadoDescarga <> Estado.Erroneo Then
			' Descomprimimos fichero
			' Crear directorio al descomprimir?? De momento ponemos siempre que sí...
            If Me.ExtraccionFicheroAutomatica AndAlso _
                DescompresorController.GetController.AgregarElemento(Me.FileID, IO.Path.Combine(Me.RutaLocal, Me.NombreFichero), True, Me.DescargaExtraccionPassword) Then
                Me.EstadoDescarga = Estado.Descomprimiendo
            End If
		End If
		
	End Sub
	
	
	Private Sub downloader_Canceled(ByVal sender As System.Object, ByVal e As System.EventArgs)
		Me.EstadoDescarga = Estado.EnCola
		
		Mutex.FicheroDownloader.WaitOne()
		Try
			If Me.Downloader IsNot Nothing Then
				
				RemoveHandler Me.Downloader.Resumed, AddressOf downloader_Started
				RemoveHandler Me.Downloader.Started, AddressOf downloader_Started
				RemoveHandler Me.Downloader.Paused, AddressOf downloader_Paused
				RemoveHandler Me.Downloader.CancelRequested, AddressOf downloader_CancelRequested
				RemoveHandler Me.Downloader.Canceled, AddressOf downloader_Canceled
				RemoveHandler Me.Downloader.Completed, AddressOf downloader_Completed
				RemoveHandler Me.Downloader.FileDownloadFailed, AddressOf downloader_FileDownloadFailed
				RemoveHandler Me.Downloader.ChunkDownloadFailed, AddressOf downloader_ChunkDownloadFailed
				RemoveHandler Me.Downloader.FileLocalCreated, AddressOf downloader_FileLocalCreated
				
				Me.Downloader.Dispose()
				
			End If
			Me.Downloader = Nothing
		Finally
			Mutex.FicheroDownloader.ReleaseMutex()
		End Try
		
		If Me.MarcadoParaBorrarFicheroLocal Then
			Me.BorrarFicheroLocal()
		End If
		
		If Me.NumErroresChunk > Fichero.NUM_MAX_ERRORES_CHUNK Then
			Dim descError As String = "Download stopped because there were too many connection errors (" & Me.NumErroresChunk & "). " & vbNewLine & _
				"Last error: " & vbNewLine & _
				" * File code: " & Me.FileID & vbNewLine & _
				" * Error type: Connection error." & vbNewLine & _
				" * Internal info: " & Me.UltimoErrorChunk.ToString
			Me.EstablecerError(descError)
			Log.WriteError(descError)
		End If
		
		RaiseEvent CancellationComplete(Me, New EventArgs)
	End Sub
	
	
	Public Sub ActualizarDatosDescarga()
		Mutex.FicheroDownloader.WaitOne()
		Try
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
		Finally
			Mutex.FicheroDownloader.ReleaseMutex()
		End Try
	End Sub
	
	Private Sub EstablecerError(ByVal msj As String)
		Me.EstadoDescarga = Estado.Erroneo
		Me.DescripcionError = msj
		Me.FechaUltimoError = Now
	End Sub
	
	
	#End Region
	
	
	
	#Region "IDisposable Support"
	Private disposedValue As Boolean ' To detect redundant calls
	
	' IDisposable
	Protected Overridable Sub Dispose(disposing As Boolean)
		If Not Me.disposedValue Then
			If disposing Then
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

        If XML.Attributes("v") IsNot Nothing AndAlso IsNumeric(XML.Attributes("v").Value) Then
            Version = CInt(XML.Attributes("v").Value)
        Else
            Version = 1
        End If

        Me._FileID = Criptografia.DecryptString_DPAPI(LeerNodo(XML, "FileID", ""))
        Me._FileKey = Criptografia.DecryptString_DPAPI(LeerNodo(XML, "FileKey", ""))
        Me._URL = Criptografia.DecryptString_DPAPI(LeerNodo(XML, "URL", ""))
        Me._URLFichero = Criptografia.DecryptString_DPAPI(LeerNodo(XML, "URLFichero", ""))

        ' Hacemos el link no visible
        Dim UrlPlain As String = Criptografia.ToInsecureString(Me._URL)
        If UrlPlain.StartsWith(HIDDEN_LINK) Then
            Me._URL = Criptografia.ToSecureString(UrlPlain.Replace(HIDDEN_LINK, ""))
            Me.LinkVisible = False
        End If


        Me.NombreFichero = LeerNodo(XML, "NombreFichero", "")
        Me.RutaRelativa = LeerNodo(XML, "RutaRelativa", "")
        Me.RutaLocal = LeerNodo(XML, "RutaLocal", "")
        Long.TryParse(LeerNodo(XML, "TamanoBytes", "0"), TamanoBytes)
        Integer.TryParse(LeerNodo(XML, "NumeroConexionesAbiertas", "0"), NumeroConexionesAbiertas)
        Integer.TryParse(LeerNodo(XML, "NumeroChunksAsignados", "0"), NumeroChunksAsignados)
        Dim strFecha As String = LeerNodo(XML, "FechaUltimoError", "")
        If Not String.IsNullOrEmpty(strFecha) Then
            FechaUltimoError = CDate(strFecha)
        End If
        Decimal.TryParse(LeerNodo(XML, "Porcentaje", "0"), Porcentaje)
        Boolean.TryParse(LeerNodo(XML, "DescargaComenzada", "false"), DescargaComenzada)
        Boolean.TryParse(LeerNodo(XML, "DescargaProcesada", "false"), DescargaProcesada)
        Boolean.TryParse(LeerNodo(XML, "ExtraccionFicheroAutomatica", "false"), ExtraccionFicheroAutomatica)
        Dim tempPass As String = LeerNodo(XML, "ExtraccionFicheroPassword", "")
        If Not String.IsNullOrEmpty(tempPass) Then
            Me.ExtraccionFicheroPassword = Criptografia.AES_DecryptString(tempPass, "passZIP")
        End If


        Integer.TryParse(LeerNodo(XML, "Prioridad", "0"), Prioridad)



        Decimal.TryParse(LeerNodo(XML, "VelocidadKBs", "0"), VelocidadKBs)
        Long.TryParse(LeerNodo(XML, "BytesDescargados", "0"), BytesDescargados)

        EstadoDescarga = Estado.EnCola
        If [Enum].IsDefined(GetType(Estado), LeerNodo(XML, "EstadoDescarga", "")) Then
            EstadoDescarga = CType([Enum].Parse(GetType(Estado), LeerNodo(XML, "EstadoDescarga", "")), Estado)
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
                    Me.DatosPartes.ChunkList.Add(Chunk)
                Next
            End If
        End If

    End Sub



    Public Function GuardarXML(ByVal XML As XmlDocument, IncluirDatosCifrados As Boolean) As XmlNode
        Dim NodoFic As XmlNode = XML.CreateElement("Fichero")
        NodoFic.Attributes.Append(XML.CreateAttribute("v")).Value = Me.Version.ToString

        If IncluirDatosCifrados Then
            If CacheSecureData Is Nothing Then RegenerateCacheSecureData()

            NodoFic.AppendChild(XML.CreateElement("FileID")).InnerText = CacheSecureData.FileID
            NodoFic.AppendChild(XML.CreateElement("FileKey")).InnerText = CacheSecureData.FileKey
            NodoFic.AppendChild(XML.CreateElement("URL")).InnerText = CacheSecureData.URL
            NodoFic.AppendChild(XML.CreateElement("URLFichero")).InnerText = CacheSecureData.URLFichero
            NodoFic.AppendChild(XML.CreateElement("OptionalPassword")).InnerText = CacheSecureData.OptionalPassword
        Else
            NodoFic.AppendChild(XML.CreateElement("FileID")).InnerText = Criptografia.ToInsecureString(_FileID)
            'NodoFic.AppendChild(XML.CreateElement("FileKey")).InnerText = Criptografia.ToInsecureString(_FileKey)
            'NodoFic.AppendChild(XML.CreateElement("URL")).InnerText = If(LinkVisible, "", HIDDEN_LINK) & Criptografia.ToInsecureString(Me._URL)
            'NodoFic.AppendChild(XML.CreateElement("URLFichero")).InnerText = Criptografia.ToInsecureString(_URLFichero)
        End If
        NodoFic.AppendChild(XML.CreateElement("NombreFichero")).InnerText = NombreFichero
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

        If Not String.IsNullOrEmpty(ExtraccionFicheroPassword) Then
            NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroPassword")).InnerText = Criptografia.AES_EncryptString(ExtraccionFicheroPassword, "passZIP")
        End If

        NodoFic.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString
        NodoFic.AppendChild(XML.CreateElement("VelocidadKBs")).InnerText = VelocidadKBs.ToString
        NodoFic.AppendChild(XML.CreateElement("BytesDescargados")).InnerText = BytesDescargados.ToString

        NodoFic.AppendChild(XML.CreateElement("EstadoDescarga")).InnerText = [Enum].GetName(GetType(Estado), EstadoDescarga)
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
                    NodoChunk.AppendChild(XML.CreateElement("Index")).InnerText = chunk.Index.ToString
                    NodoChunk.AppendChild(XML.CreateElement("Available")).InnerText = chunk.Available.ToString
                Next
            End If

        End If

        Return NodoFic

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
