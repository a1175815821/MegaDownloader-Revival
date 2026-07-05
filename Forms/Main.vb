Imports BrightIdeasSoftware
Imports System.ComponentModel
Imports System.Runtime.InteropServices

Public Class Main

#Region "Variables internas"

    ''' <summary>
    ''' Configuración de la aplicación (usuario, password, configuración por defecto, etc)
    ''' </summary>
    ''' <remarks>Es pública para que se pueda modificar desde la pantalla "Configuration".</remarks>
    Public Config As Configuracion

    ''' <summary>
    ''' Indica si ha fallado el usuario y password. Se usa para evitar peticiones innecesarias 
    ''' (si falla la identificacion no tiene sentido seguir intentandolo).
    ''' </summary>
    ''' <remarks>Es pública para que se pueda modificar desde la pantalla "Configuration".</remarks>
    Public NecesitaCambiarUsuarioYPassword As Boolean = False

    ''' <summary>
    ''' Listado de paquetes y descargas
    ''' </summary>
    ''' <remarks></remarks>
    Private ListaPaquetes As Generic.List(Of Paquete)

    ''' <summary>
    ''' Objeto que se ocupa de mirar en el portapapeles si hay links
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents clipChange As ClipboardViewer

    ''' <summary>
    ''' Objeto interno que se encarga de gestionar el "drag and drop" del listado.
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents dropSink As SimpleDropSink


    ' WORKERS

    ''' <summary>
    '''  Se ocupa de comprobar periodicamente el número de conexiones máximas
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents bgwComprobarMaxConexiones As New BackgroundWorker

    ''' <summary>
    ''' Se ocupa de refrescar el listado de descarga
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents bgwActualizadorListaDescargas As New BackgroundWorker

    ''' <summary>
    ''' Se ocupa de ir actualizando la información de ficheros en cola y de configuracion
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents bgwActualizadorDatosDisco As New BackgroundWorker

    ''' <summary>
    ''' Se ocupa de descomprimir los ficheros en segundo plano
    ''' </summary>
    ''' <remarks></remarks>
    Private WithEvents bgwDescompresor As New BackgroundWorker

    Private bgwComprobarMaxConexionesCompleted As Boolean = False
    Private bgwActualizadorListaDescargasCompleted As Boolean = False
    Private bgwActualizadorDatosDiscoCompleted As Boolean = False
    Private bgwDescompresorCompleted As Boolean = False

    ' ACCIONES

    ''' <summary>
    ''' Indica cuando se ha solicitado guardar la configuración en disco
    ''' </summary>
    ''' <remarks></remarks>
    Private PeticionGuardadoConfig As Date = Date.MinValue

    ''' <summary>
    ''' Indica cuando se ha guardado la configuración en disco
    ''' </summary>
    ''' <remarks></remarks>
    Private UltimoGuardadoConfig As Date = Date.MinValue

    ''' <summary>
    ''' Indica cuando se ha solicitado guardar el listado de paquetes en disco
    ''' </summary>
    ''' <remarks></remarks>
    Private PeticionGuardadoFichero As Date = Date.MinValue

    ''' <summary>
    ''' Indica cuando se ha guardado el listado de paquetes en disco
    ''' </summary>
    ''' <remarks></remarks>
    Private UltimoGuardadoFichero As Date = Date.MinValue

    ''' <summary>
    ''' Indica cuando se ha de hacer un flush de memoria
    ''' </summary>
    ''' <remarks></remarks>
    Private ProximoFlushMemoria As Date = Date.MinValue

    ''' <summary>
    ''' Indica la próxima comprobación del número máximo de conexiones
    ''' </summary>
    ''' <remarks></remarks>
    Private ProximaComprobacionMaxConexiones As Date = Now

    ''' <summary>
    ''' Velocidad global de todas las descargas en conjunto
    ''' </summary>
    ''' <remarks></remarks>
    Private VelocidadGlobalDescarga As Decimal? = Nothing

    ''' <summary>
    ''' Número de descarga activas (bajando)
    ''' </summary>
    ''' <remarks></remarks>
    Private NumDescargasActivas As Integer? = Nothing

    ''' <summary>
    ''' Número de descargas en cola
    ''' </summary>
    ''' <remarks></remarks>
    Private NumDescargasEnCola As Integer? = Nothing

    ''' <summary>
    ''' Número de descargas que han dado error
    ''' </summary>
    ''' <remarks></remarks>
    Private NumDescargasErroneas As Integer? = Nothing

    ''' <summary>
    ''' Número de descargas que se han completado
    ''' </summary>
    ''' <remarks></remarks>
    Private NumDescargasCompletadas As Integer? = Nothing

    ''' <summary>
    ''' Indica el número máximo de conexiones 
    ''' </summary>
    ''' <remarks></remarks>
    Private NumeroConexionesMaxima As Integer

    ''' <summary>
    ''' Nueva versión (si está disponible) del Megadowloader
    ''' </summary>
    ''' <remarks></remarks>
    Private UrlNuevaVersionMegadownloader As String
    Private VersionNuevaVersionMegadownloader As String


    ' Un par de monitores...
    Private ProcesadorCounter As PerformanceCounter
    Private RAMCounter As PerformanceCounter
    Private NumCores As Integer


    ''' <summary>
    ''' Indica el estado de la aplicación (descargando, pausa, parado)
    ''' </summary>
    ''' <remarks></remarks>
    Private EstadoAplicacion As TipoEstadoAplicacion = TipoEstadoAplicacion.Parado

    Friend Enum TipoEstadoAplicacion
        Descargando
        Pausa
        Parado
    End Enum

#End Region

#Region "Carga del formulario"


    Private Sub Main_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load

        My.MyApplication.Main_Form = Me

        'Language.SaveTranslationReport()

        Log.WriteError("Starting Megadownloader")

        Log.WriteError("Version: " & InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER"))

        Config = New Configuracion
        Log.SetLogLevel = Config.NivelLog
        Language.InitLanguage(Config.Idioma)

        Dim Silent As Boolean = IsSilent()

        Dim s As SplashScreen = Nothing
        If Not Silent Then
            s = New SplashScreen()
            s.Show()
        End If

        Me.Visible = False
        Me.StartPosition = FormStartPosition.CenterScreen

        Me.Translate()

        InicializarMonitores()

        clipChange = New ClipboardViewer
        clipChange.AssignHandle(Handle)
        clipChange.Install()

        Dim thisExe As System.Reflection.Assembly
        thisExe = System.Reflection.Assembly.GetExecutingAssembly()
        Dim file As System.IO.Stream

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".config.png")
        btnConfig.Image = Image.FromStream(file)

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".pause.png")
        btnPause.Image = Image.FromStream(file)

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".play.png")
        btnPlay.Image = Image.FromStream(file)

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".stop.png")
        btnStop.Image = Image.FromStream(file)

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".addlink.png")
        btnAddLink.Image = Image.FromStream(file)

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".download.png")
        btnUpdate.Image = Image.FromStream(file)

        file = thisExe.GetManifestResourceStream(thisExe.GetName.Name & ".collaborate.png")
        btnCollaborate.Image = Image.FromStream(file)

        CrearMenus()

        'btnCollaborate.Visible = Not Config.HideCollaborateButton
        btnCollaborate.Visible = False ' Lo quitamos... nadie lo usa... 
        btnUpdate.Visible = False

        If Me.WindowState <> FormWindowState.Minimized Then
            Me.IconoMinimizado.Visible = False
        End If

        ListaPaquetes = Paquete.CargarDesdeFichero

        ' Usamos esta librería para pintar la lista de descargas:
        ' http://objectlistview.sourceforge.net/cs/index.html
        DefinirColumnas()
        ListaDescargas.SetObjects(Me.ListaPaquetes)

        SharpCompress.PriorityExtension.Priority.DecompressionPriority = Config.PrioridadDescompresion

        Conexion.PingMega()

        Me.NumeroConexionesMaxima = Config.MaxConexionesGuardadas

        Log.WriteInfo("Starting workers")

        bgwComprobarMaxConexiones.WorkerReportsProgress = True
        bgwComprobarMaxConexiones.WorkerSupportsCancellation = True
        bgwComprobarMaxConexiones.RunWorkerAsync()

        bgwActualizadorListaDescargas.WorkerReportsProgress = True
        bgwActualizadorListaDescargas.WorkerSupportsCancellation = True
        bgwActualizadorListaDescargas.RunWorkerAsync()

        bgwActualizadorDatosDisco.WorkerReportsProgress = True
        bgwActualizadorDatosDisco.WorkerSupportsCancellation = True
        bgwActualizadorDatosDisco.RunWorkerAsync()


        AddHandler bgwDescompresor.DoWork, AddressOf DescompresorController.DescompresorController_DoWork
        bgwDescompresor.WorkerReportsProgress = True
        bgwDescompresor.WorkerSupportsCancellation = True
        bgwDescompresor.RunWorkerAsync()

        AddHandler DescompresorController.GetController.DescompresionFinalizada, AddressOf DescompresionFinalizada_EventHandler

        'If Me.Config.PermitirSkins And Not String.IsNullOrEmpty(Me.Config.ConfigUI.RutaSkin) AndAlso System.IO.File.Exists(Me.Config.ConfigUI.RutaSkin) Then
        '    SkinEngine.SkinFile = Me.Config.ConfigUI.RutaSkin
        '    If Not SkinEngine.Active Then
        '        SkinEngine.Active = True
        '    End If
        'End If

        If Config.IniciarConWindows Then
            Configuracion.RegisterInStartup(True) ' Actualizamos con la ruta actual (por si hemos movido el ejecutable de sitio)
        End If

        If Not Silent Then
            s.Close()
            Me.Visible = True
        End If

        ' Autodetect mega:// parameters from browser
        MegaURIProtocol.RegisterUrlProtocol()


        If Config.ComenzarDescargando Then
            QuitarPausasIndividuales()
            Me.EstadoAplicacion = TipoEstadoAplicacion.Descargando
        End If

        Log.WriteInfo("Start process finished")


    End Sub

    Private Sub Main_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown

        Me.Text = InternalConfiguration.ObtenerNombreApp & InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER")

        If Config.ConfigUI.AltoVentanaPrincipal > 0 And Config.ConfigUI.AnchoVentanaPrincipal > 0 Then
            Log.WriteDebug("Window size - X: " & Config.ConfigUI.AnchoVentanaPrincipal & " Y:" & Config.ConfigUI.AltoVentanaPrincipal)
            Me.Size = New System.Drawing.Size(Config.ConfigUI.AnchoVentanaPrincipal, _
                                              Config.ConfigUI.AltoVentanaPrincipal)
            Me.StartPosition = FormStartPosition.CenterScreen
            Main_Resize(Nothing, Nothing)
        End If

        ' Centramos la pantalla
        ' http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
        Dim scr = Screen.FromPoint(Me.Location)
        Me.Location = New Point(CInt((scr.WorkingArea.Right - Me.Width) / 2), CInt((scr.WorkingArea.Bottom - Me.Height) / 2))


        If Config.ConfigUI.EstadoLista IsNot Nothing Then
            Log.WriteDebug("Restoring columns")
            ListaDescargas.RestoreState(Config.ConfigUI.EstadoLista)
        End If

        If Not CheckMEGAConditions() Then Exit Sub
        CheckVersionStatistics()

        If Config.ErrorConfig <> Configuracion.ErrorConfigClass.SinErrores Then
            Log.WriteWarning("Invalid configuration, opening configuration form.")
            Dim frmName As New Configuration
            frmName.MainForm = Me
            frmName.Config = Config
            frmName.RequiereConfiguracion = True
            frmName.ShowDialog()
            ' Hasta que no se cierre la ventana no continuamos la ejecución
            frmName.Dispose()
        End If

        Log.WriteDebug("App render finished")

        If IsSilent() Then
            Me.IconoMinimizado.Text = "MegaDownloader started"
            Me.WindowState = FormWindowState.Minimized
        End If


        Dim strErrServidorWeb As String = ServidorWebController.StartWebServer(Me, Me.Config)
        If Not String.IsNullOrEmpty(strErrServidorWeb) Then
            MessageBox.Show("Error starting web server: " & strErrServidorWeb, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If


        ProcessArgs(Environment.GetCommandLineArgs)

    End Sub

    Private Sub Translate()
        Me.Text = "MegaDownloader"
        Me.OlvColumnVelocidad.Text = Language.GetText("Speed")
        Me.OlvColumnEDT.Text = Language.GetText("Estimated")
        Me.OlvColumnProgreso.Text = Language.GetText("Progress")
        Me.OlvColumnProgresoPorc.Text = Language.GetText("Progress %")
        Me.OlvColumnEstado.Text = Language.GetText("Status")
        Me.OlvColumnTamano.Text = Language.GetText("Size")
        Me.OlvColumnDescargado.Text = Language.GetText("Downloaded")
        Me.OlvColumnNombre.Text = Language.GetText("Name")
        Me.OlvColumnRestante.Text = Language.GetText("Remaining")
        Me.AbrirEnCarpetaToolStripMenuItem.Text = Language.GetText("Open directory")
        Me.SubirPrioridadMenuItem.Text = Language.GetText("Increase priority")
        Me.BajarPrioridadMenuItem.Text = Language.GetText("Decrease priority")
        Me.PausarStripMenuItem.Text = Language.GetText("Pause")
        Me.ForceDownloadStripMenuItem.Text = Language.GetText("Force download")
        Me.EliminarMenuItem.Text = Language.GetText("Delete from list")
        Me.EliminarYBorrarMenuItem.Text = Language.GetText("Delete from list and disk")
        Me.VerErrorToolStripMenuItem.Text = Language.GetText("See error")
        Me.VerLinksToolStripMenuItem.Text = Language.GetText("See links")
        Me.VerLinksDescToolStripMenuItem.Text = Language.GetText("See links + desc")
        Me.OcultarEnlacesImagenMenuItem.Text = Language.GetText("Hide links inside an image")
        Me.ResetToolStripMenuItem.Text = Language.GetText("Reset")
        Me.VerProgresoDescompresionToolStripMenuItem.Text = Language.GetText("See decompression progress")
        Me.PropiedadesToolStripMenuItem.Text = Language.GetText("Properties")
        Me.AgregarLinksToolStripMenuItem.Text = Language.GetText("Add links")
        Me.LimpiarCompletados2ToolStripMenuItem.Text = Language.GetText("Clean completed")
        Me.LimpiarCompletadosToolStripMenuItem.Text = Language.GetText("Clean completed")
        Me.AbrirToolStripMenuItem1.Text = Language.GetText("Open")
        Me.AgregarLinkStripMenuItem.Text = Language.GetText("Add link")
        Me.CerrarToolStripMenuItem.Text = Language.GetText("Close")
        Me.ToolTipBotones.SetToolTip(Me.btnConfig, Language.GetText("Configuration"))
        Me.ToolTipBotones.SetToolTip(Me.btnPlay, Language.GetText("Start downloads"))
        Me.ToolTipBotones.SetToolTip(Me.btnCollaborate, Language.GetText("Collaborate"))
        Me.ToolTipBotones.SetToolTip(Me.btnPause, Language.GetText("Pause downloads"))
        Me.ToolTipBotones.SetToolTip(Me.btnStop, Language.GetText("Stop downloads"))
        Me.ToolTipBotones.SetToolTip(Me.btnAddLink, Language.GetText("Add links"))
        Me.ToolTipBotones.SetToolTip(Me.btnUpdate, Language.GetText("New version do you want to download it?"))
        Me.StatusToolStripStatusLabel.Text = Language.GetText("Status: -")
        Me.RAMProcToolStripStatusLabel.Text = Language.GetText("RAM Proc Empty")
    End Sub

    Private Sub CrearMenus()

        Me.Menu = New MainMenu
        Dim Archivo As MenuItem = Menu.MenuItems.Add(Language.GetText("&File"))

        Dim OpenDLC As MenuItem = New MenuItem(Language.GetText("Open &DLC"))
        AddHandler (OpenDLC.Click), AddressOf OpenDLC_Click

        Dim Salir As MenuItem = New MenuItem(Language.GetText("E&xit"))
        AddHandler (Salir.Click), AddressOf CerrarToolStripMenuItem_Click

        Archivo.MenuItems.Add(OpenDLC)
        Archivo.MenuItems.Add("-")
        Archivo.MenuItems.Add(Salir)

        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        Dim ColaExtraccion As MenuItem = New MenuItem(Language.GetText("See extraction &queue"))
        AddHandler (ColaExtraccion.Click), AddressOf VerDescompresor_Click

        Dim Configuracion As MenuItem = New MenuItem(Language.GetText("&Configuration"))
        AddHandler (Configuracion.Click), AddressOf btnConfig_Click

        Dim VerLogs As MenuItem = New MenuItem(Language.GetText("See lo&gs"))
        AddHandler (VerLogs.Click), AddressOf VerLogs_Click

        Dim CodificarEnlaces As MenuItem = New MenuItem(Language.GetText("Encode lin&ks"))
        AddHandler (CodificarEnlaces.Click), AddressOf CodificarEnlaces_Click

        Dim GenerateELC As MenuItem = New MenuItem(Language.GetText("Generat&e ELC"))
        AddHandler (GenerateELC.Click), AddressOf GenerateELC_Click

        Dim Stegano As MenuItem = New MenuItem(Language.GetText("Steganograph&y"))

        Dim Opciones As MenuItem = Menu.MenuItems.Add(Language.GetText("&Options"))


        Dim searcherList = InternalConfiguration.ObtenerValuesFromInternalConfig("SEARCH_LIST/ELEMENT")
        If searcherList.Count > 0 Then
            Dim Buscadores As MenuItem = Opciones.MenuItems.Add(Language.GetText("Searc&h"))
            Opciones.MenuItems.Add("-")
            For Each searcher In searcherList
                Dim Buscador As MenuItem = New MenuItem(searcher.Key)
                AddHandler (Buscador.Click), AddressOf Buscador_Click
                Buscadores.MenuItems.Add(Buscador)
            Next
        End If

        Opciones.MenuItems.Add(CodificarEnlaces)
        Opciones.MenuItems.Add(GenerateELC)
        Opciones.MenuItems.Add(Stegano)
        Opciones.MenuItems.Add("-")
        Opciones.MenuItems.Add(ColaExtraccion)
        Opciones.MenuItems.Add(VerLogs)
        Opciones.MenuItems.Add("-")
        Opciones.MenuItems.Add(Configuracion)

        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        Dim CreateStegano As MenuItem = New MenuItem(Language.GetText("&Hide links inside an image"))
        AddHandler (CreateStegano.Click), AddressOf CreateStegano_Click
        Dim UseStegano As MenuItem = New MenuItem(Language.GetText("&Retrieve links from an image"))
        AddHandler (UseStegano.Click), AddressOf UseStegano_Click

        Stegano.MenuItems.Add(CreateStegano)
        Stegano.MenuItems.Add(UseStegano)

        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        Dim VerStreaming As MenuItem = New MenuItem(Language.GetText("Watch &Online"))
        AddHandler (VerStreaming.Click), AddressOf VerStreaming_Click

        Dim LibraryManager As MenuItem = New MenuItem(Language.GetText("Manage Streaming &Library"))
        AddHandler (LibraryManager.Click), AddressOf LibraryManager_Click

        Dim SeeLibraryManager As MenuItem = New MenuItem(Language.GetText("See Streaming &Library"))
        AddHandler (SeeLibraryManager.Click), AddressOf SeeLibraryManager_Click

        Dim Streaming As MenuItem = Menu.MenuItems.Add(Language.GetText("&Streaming"))
        Streaming.MenuItems.Add(VerStreaming)
        Streaming.MenuItems.Add("-")
        Streaming.MenuItems.Add(SeeLibraryManager)
        Streaming.MenuItems.Add(LibraryManager)


        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        Dim FAQ As New MenuItem(Language.GetText("FA&Q"))
        AddHandler (FAQ.Click), AddressOf FAQ_Click

        Dim GetMegaUploader As New MenuItem(Language.GetText("Get MegaUploa&der"))
        AddHandler (GetMegaUploader.Click), AddressOf GetMegaUploader_Click
        Dim About As New MenuItem(Language.GetText("&About"))
        AddHandler (About.Click), AddressOf About_Click
        'Dim Colabora As New MenuItem(Language.GetText("&Collaborate"))
        'AddHandler (Colabora.Click), AddressOf Collaborate_Click

        Dim CheckUpdates As New MenuItem(Language.GetText("Chec&k for updates"))
        AddHandler (CheckUpdates.Click), AddressOf CheckUpdates_Click

        Dim Ayuda As MenuItem = Menu.MenuItems.Add(Language.GetText("&Help"))
        Ayuda.MenuItems.Add(CheckUpdates)
        Ayuda.MenuItems.Add("-")
        Ayuda.MenuItems.Add(FAQ)
        Ayuda.MenuItems.Add(GetMegaUploader)
        Ayuda.MenuItems.Add("-")
        Ayuda.MenuItems.Add(About)

    End Sub

    Private Sub InicializarMonitores()
        Try
            Dim p As Process = Process.GetCurrentProcess

            ProcesadorCounter = New PerformanceCounter("Process", "% Processor Time", p.ProcessName)
            RAMCounter = New PerformanceCounter("Process", "Working Set", p.ProcessName)

            NumCores = 0
            For Each item As System.Management.ManagementBaseObject In New System.Management.ManagementObjectSearcher("Select * from Win32_Processor").[Get]()
                NumCores += Integer.Parse(item("NumberOfCores").ToString())
            Next
            p.Dispose()
        Catch ex As Exception
            Log.WriteError("Error in InicializarMonitores: " & ex.ToString)
        End Try
    End Sub

    Private Function IsSilent() As Boolean
        Dim args As String() = Environment.GetCommandLineArgs()
        If args IsNot Nothing Then
            For Each arg As String In args
                If arg = "-silent" Then
                    Return True
                End If
            Next
        End If
        Return False
    End Function

    Private Sub CheckVersionStatistics()
        Dim VersionActual As Double = 0
        Double.TryParse(InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_UPDATE"), Globalization.NumberStyles.Number, New Globalization.CultureInfo("en-GB"), VersionActual)

        Dim UltimaVersionConfig As Double = 0
        Double.TryParse(Config.VersionConfig, Globalization.NumberStyles.Number, New Globalization.CultureInfo("en-GB"), UltimaVersionConfig)
        If VersionActual > UltimaVersionConfig Then
            Config.VersionConfig = VersionActual.ToString(New Globalization.CultureInfo("en-GB"))

            ' Hitcount new user of this version
            Conexion.PingNewVersion()
        End If
    End Sub

    Private Function CheckMEGAConditions() As Boolean
        If Not Config.CondicionesAceptadas Then
            If MessageBox.Show(
                Language.GetText("Accept terms of use"),
                Language.GetText("Terms of use"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button3,
                0,
                InternalConfiguration.ObtenerValueFromInternalConfig("MEGA_TERMS"),
                "") <> Windows.Forms.DialogResult.Yes Then
                _ForzarCierre = True
                Me.Close()

                Return False
            Else
                ' Hitcount new user app
                Conexion.PingNewUser()
            End If
        End If
        Config.CondicionesAceptadas = True
        Return True
    End Function

#End Region

#Region "Cierre del formulario"
    Private Cerrando As Boolean = False
    Private Sub Main_FormClosed(sender As Object, e As System.Windows.Forms.FormClosedEventArgs) Handles Me.FormClosed
        'SkinEngine.Dispose()
        Application.DoEvents()

        Cerrando = True

        Dim Crono As Date = Now

        Log.WriteInfo("Closing, cancelling workers")

        ' Abortamos workers

        bgwActualizadorListaDescargas.CancelAsync()
        bgwComprobarMaxConexiones.CancelAsync()
        bgwActualizadorDatosDisco.CancelAsync()
        bgwDescompresor.CancelAsync()

        ' Paramos descargas
        Log.WriteInfo("Cancelling downloads")
        PararDescargaFicheros()
        EsperarParadaDescargasYWorkers()

        Log.WriteInfo("Saving download list")
        ' Guardamos lista descargas
        GuardarFicheroDescargas()

        ' Guardamos configuracion
        If Me.WindowState <> FormWindowState.Minimized Then
            Me.Config.ConfigUI.AnchoVentanaPrincipal = Me.Width
            Me.Config.ConfigUI.AltoVentanaPrincipal = Me.Height
        End If
        Me.Config.ConfigUI.EstadoLista = Me.ListaDescargas.SaveState
        Me.Config.GuardarXML(False)

        ' Borramos lista descargas
        ListaDescargas.ClearObjects()
        ListaDescargas.Dispose()

        ' Detenemos el servidor web
        Log.WriteInfo("Stopping web server")
        ServidorWebController.StopWebServer()

        ' Esto es solo para mostrar la imagen y que no parpadee xD
        If Now.Subtract(Crono).TotalMilliseconds < 500 Then
            System.Threading.Thread.Sleep(500)
        End If

        Log.WriteError("Closing MegaDownloader, bye bye!" & vbNewLine)

        Log.Flush(True)

        ' Liberamos resto de recursos
        IconoMinimizado.Dispose()
        clipChange.DestroyHandle()
        clipChange.Uninstall()

        System.Windows.Forms.Application.Exit()

    End Sub

    Private _ForzarCierre As Boolean = False
    Private Sub Main_FormClosing(sender As Object, e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing

        Dim msjCerrar As String = Language.GetText("Do you want to exit?")
        Dim comp As DescompresorController = DescompresorController.GetController
        If comp.Ocupado Then
            msjCerrar = Language.GetText("Files extracting, corruption danger") & vbNewLine & _
                        msjCerrar
        End If

        If e.CloseReason = CloseReason.UserClosing And Not _ForzarCierre AndAlso MessageBox.Show(msjCerrar, Language.GetText("Close"), MessageBoxButtons.YesNo) = DialogResult.No Then
            e.Cancel = True
        Else
            Me.Visible = False
            Dim ventanaCerrando As New Cerrando
            ventanaCerrando.lblMensaje.Text = Language.GetText("Closing please wait")
            ventanaCerrando.Show()
        End If
    End Sub
    Private Sub CerrarToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles CerrarToolStripMenuItem.Click
        Me.Close()
    End Sub

    Private Sub VerDescompresor_Click(sender As System.Object, e As System.EventArgs)
        If Main.IsFormAlreadyOpen(GetType(Descompresor)) Is Nothing Then
            Dim frmName As New Descompresor
            frmName.Show()
        End If
    End Sub


    Private Sub OpenDLC_Click(sender As System.Object, e As System.EventArgs)

        If DLCProcessing Then
            MessageBox.Show(Language.GetText("There is a DLC being processed, please wait"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim Examinar As New OpenFileDialog
        Examinar.CheckFileExists = True
        Examinar.Filter = Language.GetText("DLC") & " / " & Language.GetText("ELC") & " (*.dlc, *.elc)|*.dlc;*.elc"
        Examinar.Multiselect = False

        Dim DLCPath As String = String.Empty
        If Examinar.ShowDialog = Windows.Forms.DialogResult.OK Then
            DLCPath = Examinar.FileName
        End If
        Examinar.Dispose()

        AddDLC(DLCPath)

    End Sub

#End Region

#Region "Pintado del formulario"



    Private Sub Main_Resize(sender As Object, e As System.EventArgs) Handles Me.Resize

        ' Minimizamos
        If Me.WindowState = FormWindowState.Minimized Then
            Me.IconoMinimizado.Visible = True
            Me.Visible = False
            Me.IconoMinimizado.ShowBalloonTip(5000, Me.Text, Me.IconoMinimizado.Text, Nothing)
        Else
            Me.IconoMinimizado.Visible = False
            Me.Visible = True
        End If
    End Sub

    Private Sub RestaurarVentana()
        If Me.WindowState = FormWindowState.Minimized Then
            Me.Visible = True
            Me.IconoMinimizado.Visible = False
            Me.WindowState = FormWindowState.Normal
        End If
    End Sub


    Private Sub AbrirToolStripMenuItem1_Click(sender As System.Object, e As System.EventArgs) Handles AbrirToolStripMenuItem1.Click
        RestaurarVentana()
    End Sub


    Private Sub IconoMinimizado_DoubleClick(sender As Object, e As System.EventArgs) Handles IconoMinimizado.DoubleClick
        RestaurarVentana()
    End Sub
    Private Sub IconoMinimizado_Click(sender As Object, e As System.EventArgs) Handles IconoMinimizado.Click
        If TypeOf (e) Is System.Windows.Forms.MouseEventArgs Then
            If CType(e, System.Windows.Forms.MouseEventArgs).Button = Windows.Forms.MouseButtons.Left Then
                RestaurarVentana()
            End If
        End If
    End Sub


#End Region

#Region "Columnas del ListView"

    ''' <summary>
    ''' Define el comportamiento de las columnas del listview
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub DefinirColumnas()


        Dim IndiceColumnaPrioridad As Integer = 0
        Dim IndiceColumnaNombre As Integer = 1
        Dim IndiceColumnaDescargado As Integer = 2
        Dim IndiceColumnaTamano As Integer = 3
        Dim IndiceColumnaEstado As Integer = 4
        Dim IndiceColumnaPorcentajeTexto As Integer = 5
        Dim IndiceColumnaPorcentaje As Integer = 6
        Dim IndiceColumnaVelocidad As Integer = 7
        Dim IndiceColumnaEDT As Integer = 8
        Dim IndiceColumnaRestante As Integer = 9



        ListaDescargas.PrimarySortColumn = ListaDescargas.AllColumns(IndiceColumnaPrioridad)

        ListaDescargas.SortGroupItemsByPrimaryColumn = True


        Dim ColPrioridad As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaPrioridad))
        ColPrioridad.AspectGetter = Function(ele As IDescarga)
                                        Return ele.DescargaPrioridad
                                    End Function

        Dim ColNombre As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaNombre))
        ColNombre.AspectGetter = Function(ele As IDescarga)
                                     If TypeOf ele Is Fichero Then
                                         Return System.IO.Path.Combine(CType(ele, Fichero).RutaRelativa, ele.DescargaNombre)
                                     Else
                                         Return ele.DescargaNombre
                                     End If

                                 End Function

        Dim ColDescargado As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaDescargado))
        ColDescargado.AspectGetter = Function(ele As IDescarga)
                                         Dim tamano As Decimal = ele.DescargaTamanoBytes
                                         If tamano = 0 Then Return "-"
                                         Dim descargado As Decimal = Math.Ceiling(ele.DescargaPorcentaje * tamano / 100)
                                         Return PintarTamano(descargado)
                                     End Function
        ListaDescargas.AllColumns(IndiceColumnaDescargado).TextAlign = HorizontalAlignment.Right


        Dim ColRestante As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaRestante))
        ColRestante.AspectGetter = Function(ele As IDescarga)
                                       Dim tamano As Decimal = ele.DescargaTamanoBytes
                                       If tamano = 0 Then Return "-"
                                       Dim descargado As Decimal = Math.Ceiling(ele.DescargaPorcentaje * tamano / 100)
                                       Return PintarTamano(tamano - descargado)
                                   End Function
        ListaDescargas.AllColumns(IndiceColumnaRestante).TextAlign = HorizontalAlignment.Right


        Dim ColTamano As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaTamano))
        ColTamano.AspectGetter = Function(ele As IDescarga)
                                     Dim tamano As Decimal = ele.DescargaTamanoBytes
                                     If tamano = 0 Then Return "-"
                                     Return PintarTamano(tamano)

                                 End Function
        ListaDescargas.AllColumns(IndiceColumnaTamano).TextAlign = HorizontalAlignment.Right

        Dim ColEstado As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaEstado))
        ColEstado.AspectGetter = Function(ele As IDescarga)
                                     Try
                                         Dim Estado As Estado = ele.DescargaEstado()
                                         Select Case Estado
                                             Case MegaDownloader.Estado.EnCola
                                                 Return Language.GetText("In queue")
                                             Case MegaDownloader.Estado.CreandoLocal
                                                 Return Language.GetText("Creating files")
                                             Case MegaDownloader.Estado.Verificando
                                                 Return Language.GetText("Verifying")
                                             Case MegaDownloader.Estado.Erroneo
                                                 Return Language.GetText("Error capital leters")
                                             Case MegaDownloader.Estado.Pausado
                                                 Return Language.GetText("Paused")
                                             Case MegaDownloader.Estado.Descomprimiendo
                                                 Return Language.GetText("Extracting")
                                             Case MegaDownloader.Estado.Descargando
                                                 Return Language.GetText("Downloading")
                                             Case MegaDownloader.Estado.ComprobandoMD5
                                                 Return Language.GetText("Hashing MD5")
                                             Case MegaDownloader.Estado.Completado
                                                 Return Language.GetText("Completed")
                                             Case Else
                                                 Return "---"
                                         End Select
                                     Catch ex As Exception
                                         MsgBox("Error displaying download status: " & ex.ToString, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                                         Throw
                                     End Try
                                 End Function

        Dim ColPorcentaje As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaPorcentaje))
        ColPorcentaje.AspectGetter = Function(ele As IDescarga)
                                         Try
                                             Return ele.DescargaPorcentaje
                                         Catch ex As Exception
                                             Log.WriteError("Error displaying download %: " & ex.ToString)
                                             MsgBox("Error displaying download %: " & ex.ToString, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                                             Throw
                                         End Try
                                     End Function
        Dim barRender As New BrightIdeasSoftware.BarRenderer
        barRender.UseStandardBar = False
        barRender.BackgroundColor = System.Drawing.Color.Azure
        barRender.FillColor = System.Drawing.Color.MediumTurquoise
        barRender.GradientStartColor = System.Drawing.Color.SpringGreen
        barRender.GradientEndColor = System.Drawing.Color.MediumTurquoise
        barRender.MaximumWidth = 9999
        ListaDescargas.AllColumns(IndiceColumnaPorcentaje).Renderer = barRender

        Dim ColVelocidad As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaVelocidad))
        ColVelocidad.AspectGetter = Function(ele As IDescarga)
                                        Return PintarVelocidadDescarga(ele)
                                    End Function
        ListaDescargas.AllColumns(IndiceColumnaVelocidad).TextAlign = HorizontalAlignment.Right


        Dim ColEDT As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaEDT))
        ColEDT.AspectGetter = Function(ele As IDescarga)
                                  Try
                                      Return ele.DescargaTiempoEstimadoDescarga
                                  Catch ex As Exception
                                      Log.WriteError("Error displaying download time: " & ex.ToString)
                                      MsgBox("Error displaying download time: " & ex.ToString, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                      Throw
                                  End Try
                              End Function
        ListaDescargas.AllColumns(IndiceColumnaEDT).TextAlign = HorizontalAlignment.Right

        Dim ColPorcentajeTexto As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaPorcentajeTexto))
        ColPorcentajeTexto.AspectGetter = Function(ele As IDescarga)
                                              Try
                                                  Return ele.DescargaPorcentaje.ToString("F2") & "%"
                                              Catch ex As Exception
                                                  Log.WriteError("Error displaying download % (text): " & ex.ToString)
                                                  MsgBox("Error displaying download % (text): " & ex.ToString, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                  Throw
                                              End Try
                                          End Function
        ListaDescargas.AllColumns(IndiceColumnaPorcentajeTexto).TextAlign = HorizontalAlignment.Right



        ListaDescargas.AllowColumnReorder = True

        ListaDescargas.CanExpandGetter = Function(ele As Object)
                                             Return TypeOf (ele) Is Paquete AndAlso _
                                               CType(ele, Paquete).ListaFicheros IsNot Nothing AndAlso _
                                               CType(ele, Paquete).ListaFicheros.Count > 0
                                         End Function

        ListaDescargas.ChildrenGetter = Function(ele As Object)
                                            If Not TypeOf (ele) Is Paquete Then
                                                Return Nothing
                                            Else
                                                Return CType(ele, Paquete).ListaFicheros
                                            End If
                                        End Function


        ListaDescargas.SelectColumnsOnRightClickBehaviour = ObjectListView.ColumnSelectBehaviour.InlineMenu

        ListaDescargas.FullRowSelect = True

        ListaDescargas.DragSource = New SimpleDragSource
        dropSink = New SimpleDropSink
        ListaDescargas.DropSink = dropSink
        dropSink.CanDropBetween = True
        dropSink.CanDropOnBackground = False
        dropSink.CanDropOnItem = False
        dropSink.CanDropOnSubItem = False


        InitializeColumnWidths()
    End Sub
    Private Sub ListaDescargas_FormatRow(sender As Object, e As BrightIdeasSoftware.FormatRowEventArgs) Handles ListaDescargas.FormatRow
        If e.DisplayIndex Mod 2 = 0 Then
            e.Item.BackColor = Color.White
        Else
            e.Item.BackColor = Color.Honeydew
        End If
        Dim desc As IDescarga = CType(e.Model, IDescarga)
        If desc.DescargaEstado = Estado.Erroneo Then
            e.Item.ForeColor = Color.Red
        ElseIf desc.DescargaEstado = Estado.Completado Then
            e.Item.ForeColor = Color.Green
        End If
    End Sub
    Private Function PintarVelocidadDescarga(ele As IDescarga) As String
        If ele.DescargaEstado = Estado.Descargando Then
            Return PintarVelocidadDescarga(ele.DescargaVelocidadKBs)
        Else
            Return ""
        End If
    End Function
    Private Function PintarVelocidadDescarga(vel As Decimal) As String
        Dim Dato As String = "KB/s"
        If vel > 1024 Then
            Dato = "MB/s"
            vel = vel / 1024
        End If
        Return vel.ToString("F2") & " " & Dato

    End Function

    Private Function PintarTamano(numBytes As Decimal) As String
        Dim Dato As String = "B"
        If numBytes > 1024 Then
            Dato = "KB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "MB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "GB"
            numBytes = numBytes / 1024
        End If
        If numBytes > 1024 Then
            Dato = "TB"
            numBytes = numBytes / 1024
        End If
        Return numBytes.ToString("F2") & " " & Dato

    End Function


    Protected Overridable Sub InitializeColumnWidths()
        ' Al final no hacemos nada aquí...
    End Sub
#End Region

#Region "Gestion lista paquetes y descargas"


    ''' <summary>
    ''' Agregamos paquetes desde la pantalla "Addlinks" o desde la carga inicial
    ''' </summary>
    ''' <param name="Paquete"></param>
    ''' <remarks></remarks>
    Friend Sub AgregarPaquete(ByVal Paquete As Paquete, AgregadoDesdeServidorWeb As Boolean)

        Mutex.ListaDescargas.WaitOne()
        Me.ListaPaquetes.Add(Paquete)
        If String.IsNullOrEmpty(Paquete.Nombre) Then
            Paquete.Nombre = Language.GetText("Package") & " #" & Me.ListaPaquetes.Count.ToString
        End If
        Mutex.ListaDescargas.ReleaseMutex()
        Log.WriteError("Package added: " & Paquete.Nombre)

        ReordenarPrioridadPaquetes(True)

        GuardarFicheroDescargas()
        If Not AgregadoDesdeServidorWeb Then RestaurarVentana()
   
    End Sub

    ''' <summary>
    ''' Guarda en disco el listado de paquetes y descargas
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub GuardarFicheroDescargas()
        Mutex.ListaDescargas.WaitOne()
        Try
            Paquete.GuardarEnFichero(Me.ListaPaquetes)
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try

        UltimoGuardadoFichero = Now
    End Sub


#End Region

#Region "Background Workers"


    Private Sub bgwComprobarMaxConexiones_DoWork(ByVal sender As Object, ByVal e As DoWorkEventArgs) Handles bgwComprobarMaxConexiones.DoWork
        Dim worker As BackgroundWorker = DirectCast(sender, BackgroundWorker)

        Try
            Log.WriteWarning("Starting worker bgwComprobarMaxConexiones")
            While Not worker.CancellationPending

                ' Comprobamos conexiones máximas
                If Now >= ProximaComprobacionMaxConexiones Then
                    'Log.WriteWarning("Checking max connection number")

                    ' Lo cogemos de configuracion, pero por defecto ponemos cada hora
                    Dim SegundosProximaActualizacion As Integer = 3600
                    If Not Integer.TryParse( _
                                            InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_PERIODO_REFRESCO_SEG"), _
                                            SegundosProximaActualizacion) Then
                        SegundosProximaActualizacion = 3600
                    End If

                    ProximaComprobacionMaxConexiones = Now.AddSeconds(SegundosProximaActualizacion)

                    ' Aprovechamos para ver si hay una nueva versión

                    If Config.CheckUpdates Then

                        Mutex.NumeroConexionesMaxima.WaitOne()
                        Updater.ComprobarVersionMegadownloader(UrlNuevaVersionMegadownloader, VersionNuevaVersionMegadownloader)
                        Mutex.NumeroConexionesMaxima.ReleaseMutex()
                        If Not String.IsNullOrEmpty(UrlNuevaVersionMegadownloader) Then
                            ActivarUpdateButton()
                            ProximoAvisoActualizacion = Now.AddSeconds(15)
                        End If


                        Log.WriteWarning("Version checked; next check in " & SegundosProximaActualizacion & " seconds")
                    End If

                End If
                If ProximoAvisoActualizacion.HasValue AndAlso ProximoAvisoActualizacion.Value < Now Then
                    MostrarMensajeActualizacion()
                End If


                ' Aprovechamos y hacemos un flush de memoria ya que este worker no hace mucho trabajo
                If Me.ProximoFlushMemoria < Now Then
                    Dim FrecFlush As Integer = 60
                    If Not Integer.TryParse( _
                                            InternalConfiguration.ObtenerValueFromInternalConfig("FLUSH_MEMORY_PERIODO_REFRESCO_SEG"), _
                                            FrecFlush) Then
                        FrecFlush = 60
                    End If
                    Me.ProximoFlushMemoria = Now.AddSeconds(FrecFlush)
                    FlushMemory()
                    Log.WriteDebug("Flush memory, next flush in " & FrecFlush & " seconds")
                End If

                ' Y de paso hacemos un flush de logs
                Log.Flush(False)

                System.Threading.Thread.Sleep(1000)
            End While

            Log.WriteWarning("Stopping worker bgwComprobarMaxConexiones")
        Catch ex As Exception
            Log.WriteError("Error in worker bgwComprobarMaxConexiones: " & ex.ToString)
            MsgBox(ex.ToString, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            bgwComprobarMaxConexionesCompleted = True
        End Try
    End Sub

    Private Sub bgwActualizadorDatosDisco_DoWork(ByVal sender As Object, ByVal e As DoWorkEventArgs) Handles bgwActualizadorDatosDisco.DoWork
        Dim worker As BackgroundWorker = DirectCast(sender, BackgroundWorker)

        Try

            Log.WriteWarning("Starting worker bgwActualizadorDatosDisco")
            While Not worker.CancellationPending

				Dim FicheroActualizar As Fichero = Nothing
				Dim PaqueteDelFicheroActualizar As Paquete = Nothing
                Dim TiempoDormir As Integer = 250

                If Not NecesitaCambiarUsuarioYPassword Then

                    Mutex.ListaDescargas.WaitOne()
                    Try
                        For Each paq As Paquete In Me.ListaPaquetes
                            For Each fic As Fichero In paq.ListaFicheros
                                If fic.DescargaProcesada = False And fic.DescargaEstado <> Estado.Erroneo Then
                                	FicheroActualizar = fic
                                	PaqueteDelFicheroActualizar = paq
                                    Exit Try
                                End If
                            Next
                        Next
                    Finally
                        Mutex.ListaDescargas.ReleaseMutex()
                    End Try

                    If FicheroActualizar IsNot Nothing Then
                        Log.WriteInfo("Updating file info " & FicheroActualizar.FileID)
                        TiempoDormir = 50
                        Dim Err As Conexion.TipoError = Conexion.TipoError.SinErrores
                        FicheroActualizar.ActualizarInformacionFichero(Config, Err, False)
                        If Err = Conexion.TipoError.UsuarioInvalido Then
                            ' Si el usuario está mal no vamos a pedir lo mismo 1000 veces seguidas, 
                            ' abortamos hasta que se cambie el usuario
                            NecesitaCambiarUsuarioYPassword = True
                            Log.WriteWarning("Error retrieving file info " & FicheroActualizar.FileID & ": " & Err.ToString)
                        ElseIf Err = Conexion.TipoError.SinErrores Then
                            ' Guardamos!!
                            PeticionGuardadoFichero = Now
                            
                        	If FicheroActualizar.DescargaProcesada And PaqueteDelFicheroActualizar.PendienteNombrePaquete Then
                        		PaqueteDelFicheroActualizar.PendienteNombrePaquete = false
                        		PaqueteDelFicheroActualizar.Nombre = FicheroActualizar.ObtenerNombreSinExtension
                        		If PaqueteDelFicheroActualizar.CrearSubdirectorio Then
                        			Try
                        				PaqueteDelFicheroActualizar.RutaLocal = System.IO.Path.Combine(PaqueteDelFicheroActualizar.RutaLocal, PaqueteDelFicheroActualizar.Nombre)
                        				System.IO.Directory.CreateDirectory(PaqueteDelFicheroActualizar.RutaLocal)
		                            	For Each fic As Fichero In PaqueteDelFicheroActualizar.ListaFicheros
		                            		If Not fic.DescargaComenzada Then
		                            			fic.RutaLocal = PaqueteDelFicheroActualizar.RutaLocal
		                            		End If
		                            	Next
		                            Catch ex As Exception
		                            	Log.WriteError("Error while creating directory for package " & PaqueteDelFicheroActualizar.Nombre & ": " & ex.ToString)
		                            	MessageBox.Show("Error creating directory: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
		                            End Try			                            	
                            	End if
                            End If                            
                        Else
                            Log.WriteWarning("Error retrieving file info " & FicheroActualizar.FileID & ": " & Err.ToString)
                        End If
                    End If


                    ' Miramos si hay que guardar el fichero de downloads
                    ' Se guarda cada 5 segundos siempre O
                    ' Si al menos han pasado 400ms sin peticiones de guardado 
                    ' (si un proceso pide 10 veces seguidas que se guarde, solo se guardará una vez, 
                    ' no 10 veces, lo cual es innecesario)

                    If UltimoGuardadoFichero.AddSeconds(5) < Now Or ( _
                       UltimoGuardadoFichero < PeticionGuardadoFichero And _
                       PeticionGuardadoFichero.AddMilliseconds(400) < Now) Then

                        GuardarFicheroDescargas()

                    End If

                    If UltimoGuardadoConfig.AddSeconds(5) < Now Or ( _
                      UltimoGuardadoConfig < PeticionGuardadoConfig And _
                      PeticionGuardadoConfig.AddMilliseconds(400) < Now) Then

                        Me.Config.GuardarXML(False)
                        UltimoGuardadoConfig = Now

                    End If
                End If
                System.Threading.Thread.Sleep(TiempoDormir)
            End While
            Log.WriteWarning("Stopping worker bgwActualizadorDatosDisco")
        Catch ex As Exception
            Log.WriteError("Error in worker bgwActualizadorDatosDisco: " & ex.ToString)
            MsgBox(ex.ToString, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            bgwActualizadorDatosDiscoCompleted = True
        End Try
    End Sub

    Private Sub bgwActualizadorListaDescargas_DoWork(ByVal sender As Object, ByVal e As DoWorkEventArgs) Handles bgwActualizadorListaDescargas.DoWork
        Dim worker As BackgroundWorker = DirectCast(sender, BackgroundWorker)
        Dim sw As New System.Diagnostics.Stopwatch
        Dim Flujo As String = ""

        Try
            Log.WriteWarning("Starting worker bgwActualizadorListaDescargas")
            While Not worker.CancellationPending

                sw.Start()
                Flujo = "Checking status" & vbNewLine

                ' Cambio estado
                If EstadoAplicacion = TipoEstadoAplicacion.Descargando Then
                    PonerFicherosADescargar()
                ElseIf EstadoAplicacion = TipoEstadoAplicacion.Pausa Then
                    PonerFicherosEnPausa()
                ElseIf EstadoAplicacion = TipoEstadoAplicacion.Parado Then
                    PararDescargaFicheros()
                End If

                Flujo &= "Calculating speed, state, etc" & vbNewLine

                ' Calculo de velocidad, estado y demás datos
                Dim VelocidadGlobal As Decimal = 0

                If worker.CancellationPending Then Exit While
                Mutex.ListaDescargas.WaitOne()
                Try
                    Me.NumDescargasActivas = 0
                    Me.NumDescargasEnCola = 0
                    Me.NumDescargasErroneas = 0
                    Me.NumDescargasCompletadas = 0
                    For Each paq As Paquete In Me.ListaPaquetes
                        For Each fic As Fichero In paq.ListaFicheros

                            fic.ActualizarDatosDescarga()
                            If fic.EstadoDescarga = Estado.Descargando Or _
                               fic.EstadoDescarga = Estado.CreandoLocal Or _
                               fic.EstadoDescarga = Estado.Verificando Then
                                Me.NumDescargasActivas += 1
                            ElseIf fic.EstadoDescarga = Estado.EnCola Or _
                                   fic.EstadoDescarga = Estado.Pausado Then
                                Me.NumDescargasEnCola += 1
                            ElseIf fic.EstadoDescarga = Estado.Erroneo Then
                                Me.NumDescargasErroneas += 1
                            ElseIf fic.EstadoDescarga = Estado.Completado Or _
                                   fic.EstadoDescarga = Estado.ComprobandoMD5 Or _
                                   fic.EstadoDescarga = Estado.Descomprimiendo Then
                                Me.NumDescargasCompletadas += 1

                            End If
                        Next
                        paq.ActualizarDatosDescarga()
                        VelocidadGlobal += paq.DescargaVelocidadKBs()
                    Next
                Finally
                    Mutex.ListaDescargas.ReleaseMutex()
                End Try

                If worker.CancellationPending Then Exit While

                Flujo &= "Updating download list" & vbNewLine

                RefreshListaDescargas(False)

                VelocidadGlobalDescarga = VelocidadGlobal
                TextoIconoMinimizado(Me.NumDescargasActivas & " " & Language.GetText("active downloads") & " - " & PintarVelocidadDescarga(VelocidadGlobal))


                If worker.CancellationPending Then Exit While

                Flujo &= "Checking if we have finished and we have to turn off the PC" & vbNewLine

                ' Revisamos si hemos terminado y hay que apagar el PC
                RevisarSiHayQueApagarPC()


                Flujo &= "Calculating data (speed, processor and RAM usage, etc)" & vbNewLine
                Dim EstadoTxt As String = Language.GetText("Stopped")
                Dim VelocidadTxt As String = ""
                Select Case EstadoAplicacion
                    Case TipoEstadoAplicacion.Descargando
                        EstadoTxt = Language.GetText("Downloading")
                        If VelocidadGlobalDescarga.HasValue AndAlso VelocidadGlobalDescarga.Value > 0 Then
                            VelocidadTxt = " " & PintarVelocidadDescarga(VelocidadGlobalDescarga.Value)
                        End If
                    Case TipoEstadoAplicacion.Pausa
                        EstadoTxt = Language.GetText("Paused")
                End Select


                ' Calculo procesador y RAM
                Dim RAMStr As String = "-"
                Dim ProcesadorStr As String = "-"
                If RAMCounter IsNot Nothing And ProcesadorCounter IsNot Nothing Then
                    Dim RAM As Double = RAMCounter.NextValue / 1024 / 1024 ' MB
                    Dim Procesador As Double = ProcesadorCounter.NextValue / (If(Me.NumCores = 0, 1, Me.NumCores))  ' %
                    RAMStr = RAM.ToString("F2")
                    ProcesadorStr = Procesador.ToString("F2")
                End If

                Flujo &= "Displaying data in status bar" & vbNewLine

                SetStatusBar(RAMStr & "MB", ProcesadorStr & "%", EstadoTxt, VelocidadTxt, Config.ConexionesPorFichero & "/" & Config.DescargasSimultaneas)

                If worker.CancellationPending Then Exit While

                ' Dirty trick!!
                Flujo &= "Getting mega:// parameters" & vbNewLine
                ApplicationInstanceManager.GetParameters()

                Flujo &= "Sleeping" & vbNewLine

                System.Threading.Thread.Sleep(430)

                Flujo &= "Finishing cycle" & vbNewLine

                sw.Stop()
                sw.Reset()

            End While
            Log.WriteWarning("Stopping worker bgwActualizadorListaDescargas")
        Catch ex As Exception
            Log.WriteError("Error in worker bgwActualizadorListaDescargas: " & ex.ToString)
            MsgBox(ex.ToString, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            If sw.ElapsedMilliseconds > 5000 Then
                Log.WriteError("bgwActualizadorListaDescargas was too slow (" & sw.ElapsedMilliseconds & " ms): " & vbNewLine & Flujo)
            End If
            bgwActualizadorListaDescargasCompleted = True
        End Try
    End Sub




    Private Sub bgwActualizadorListaDescargas_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwActualizadorListaDescargas.RunWorkerCompleted
        bgwActualizadorListaDescargasCompleted = True
    End Sub
    Private Sub bgwActualizadorDatosDisco_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwActualizadorDatosDisco.RunWorkerCompleted
        bgwActualizadorDatosDiscoCompleted = True
    End Sub
    Private Sub bgwComprobarMaxConexiones_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwComprobarMaxConexiones.RunWorkerCompleted
        bgwComprobarMaxConexionesCompleted = True
    End Sub

    Private Sub bgwDescompresor_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwDescompresor.RunWorkerCompleted
        bgwDescompresorCompleted = True
    End Sub

    Private Sub PonerFicherosADescargar()
        Dim ColaDescarga As New Generic.List(Of Fichero) ' Lista de ficheros en cola
        Dim ColaDescargaPausa As New Generic.List(Of Fichero) ' Lista de ficheros pausados en cola
        Dim ColaReseteo As New Generic.List(Of Fichero) ' Lista de ficheros a resetear
        Dim NumFicherosDescargando As Integer = 0
        Dim NumConexionesAbiertas As Integer = 0

        Dim configConexionesPorFichero As Integer = Config.ConexionesPorFichero
        Dim ResetearErrores As Boolean = Config.ResetearErrores
        Dim ResetearErroresPeriodo As Integer = Config.ResetearErroresPeriodoMinutos

        ' Reset de descargas erroneas
        If ResetearErrores Then
            Mutex.ListaDescargas.WaitOne()
            For Each paq As Paquete In Me.ListaPaquetes
                For Each Fichero As Fichero In paq.ListaFicheros
                    If Fichero.DescargaEstado = Estado.Erroneo AndAlso _
                       (Not Fichero.FechaUltimoError.HasValue OrElse Fichero.FechaUltimoError.Value.AddMinutes(ResetearErroresPeriodo) < Now) Then
                        ColaReseteo.Add(Fichero)
                    End If
                Next
            Next
            Mutex.ListaDescargas.ReleaseMutex()
            For Each Fichero In ColaReseteo
                Fichero.SetDescargaEstado = Estado.EnCola
                Log.WriteInfo("Reseting file " & Fichero.FileID & " automatically.")
            Next
        End If


        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros

                If Fichero.DescargaEstado = Estado.EnCola And Fichero.DescargaProcesada Then
                    ColaDescarga.Add(Fichero)
                ElseIf Fichero.DescargaEstado = Estado.Pausado And Not Fichero.PausaIndividual Then
                    ColaDescargaPausa.Add(Fichero)
                ElseIf Fichero.DescargaEstado = Estado.Descargando Or (Fichero.DescargaEstado = Estado.Pausado And Fichero.PausaIndividual) Then
                    ' Las pausas individuales significa que el resto de ficheros está descargando, pero el fichero pausado no, por tanto no abrimos nuevas conexiones
                    NumFicherosDescargando += 1
                    Dim NumConAbiertas As Integer = Fichero.NumeroConexionesAbiertas
                    If NumConAbiertas = 0 Then
                        NumConAbiertas = configConexionesPorFichero ' Todavía no está descargando pero empezará en breve
                    End If
                    NumConexionesAbiertas += NumConAbiertas
                ElseIf Fichero.DescargaEstado = Estado.CreandoLocal Or Fichero.DescargaEstado = Estado.Verificando Then
                    NumFicherosDescargando += 1
                    NumConexionesAbiertas += configConexionesPorFichero
                End If

            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()

        Dim ConexionesPorAbrir As Integer
        Mutex.NumeroConexionesMaxima.WaitOne()
        ConexionesPorAbrir = Me.NumeroConexionesMaxima - NumConexionesAbiertas
        Mutex.NumeroConexionesMaxima.ReleaseMutex()
        Dim FicherosPorAbrir As Integer = Config.DescargasSimultaneas - NumFicherosDescargando

        'If (ColaDescargaPausa.Count > 0 Or ColaDescarga.Count > 0) And ConexionesPorAbrir > 0 Then
        '    Log.WriteInfo("We have " & ConexionesPorAbrir & " connections left")
        'End If

        'Mutex.ListaDescargas.WaitOne()
        Try
            For Each Fichero As Fichero In ColaDescargaPausa ' Primero reanudamos los ficheros en pausa y luego el resto
                If ConexionesPorAbrir > 0 And FicherosPorAbrir > 0 Then
                    Log.WriteInfo("Continuing paused file download " & Fichero.NombreFichero)
                    Fichero.[Resume]()
                    ConexionesPorAbrir -= Fichero.NumeroConexionesAbiertas
                    FicherosPorAbrir -= 1
                End If
            Next
            For Each Fichero As Fichero In ColaDescarga
                If ConexionesPorAbrir > 0 And FicherosPorAbrir > 0 Then
                    Dim ConexionesParaEstaDescarga As Integer = configConexionesPorFichero
                    If ConexionesPorAbrir < configConexionesPorFichero Then ConexionesParaEstaDescarga = ConexionesPorAbrir
                    Log.WriteInfo("Starting file download " & Fichero.NombreFichero)
                    Fichero.Start(Me.Config, ConexionesParaEstaDescarga)
                    ConexionesPorAbrir -= ConexionesParaEstaDescarga
                    FicherosPorAbrir -= 1
                End If
            Next
        Finally
            'Mutex.ListaDescargas.ReleaseMutex()
        End Try
    End Sub

    Private Sub ForzarDescarga(Fichero As Fichero)
        Log.WriteInfo("Forcing download" & Fichero.NombreFichero)
        Mutex.ListaDescargas.WaitOne()
        Try
            If Fichero.DescargaEstado = Estado.Pausado Then
                Fichero.Resume()
            ElseIf Fichero.DescargaEstado = Estado.EnCola And Fichero.DescargaProcesada Then
                Fichero.DescargaIndividual = True
                Fichero.Start(Me.Config, Me.Config.ConexionesPorFichero)
            End If
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try
    End Sub
    Private Sub QuitarDescargasIndividuales()
        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros
                Fichero.DescargaIndividual = False
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub

    Private Sub PonerFicherosEnPausa()
        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros
                If (Fichero.DescargaEstado = Estado.Descargando And Not Fichero.DescargaIndividual) Or Fichero.DescargaEstado = Estado.CreandoLocal Then
                    Log.WriteInfo("Pausing file " & Fichero.NombreFichero)
                    Fichero.Pause()
                End If
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub
    Private Sub QuitarPausasIndividuales()
        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros
                Fichero.PausaIndividual = False
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub
    Private Sub PonerFicheroEnPausa(ByVal Fichero As Fichero)
        Mutex.ListaDescargas.WaitOne()
        If Fichero.DescargaEstado = Estado.Descargando Or Fichero.DescargaEstado = Estado.CreandoLocal Then
            Log.WriteInfo("Pausing file " & Fichero.NombreFichero)
            Fichero.PausaIndividual = True
            Fichero.Pause()
            ThrottledStreamController.GetController.Abortar(Fichero.FileID)
        End If
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub

    Private Sub PararDescargaFicheros()
        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros
                If (Fichero.DescargaEstado = Estado.Descargando And Not Fichero.DescargaIndividual) Or _
                   Fichero.DescargaEstado = Estado.Pausado Or _
                   Fichero.DescargaEstado = Estado.CreandoLocal Then
                    Log.WriteInfo("Stopping file " & Fichero.NombreFichero)
                    Fichero.Stop()
                End If
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub

    Private Sub EsperarParadaDescargasYWorkers()
        Dim TodosFinalizados As Boolean = False
        Dim TimeoutSeg As Integer = 15
        Integer.TryParse(InternalConfiguration.ObtenerValueFromInternalConfig("TIMEOUT_CLOSE"), TimeoutSeg)
        Dim Ini As Date = Now
        While Not TodosFinalizados

            TodosFinalizados = True
            Mutex.ListaDescargas.WaitOne()
            Try
                For Each paq As Paquete In Me.ListaPaquetes
                    For Each Fichero As Fichero In paq.ListaFicheros
                        If Fichero.DescargaEstado = Estado.Descargando Or _
                           Fichero.DescargaEstado = Estado.Pausado Then
                            TodosFinalizados = False
                        End If
                    Next
                Next
            Finally
                Mutex.ListaDescargas.ReleaseMutex()
            End Try

            If Not bgwActualizadorDatosDiscoCompleted Then TodosFinalizados = False
            If Not bgwComprobarMaxConexionesCompleted Then TodosFinalizados = False
            If Not bgwActualizadorListaDescargasCompleted Then TodosFinalizados = False
            System.Threading.Thread.Sleep(100)

            If Ini.AddSeconds(TimeoutSeg) < Now Then TodosFinalizados = True
        End While
        If Ini.AddSeconds(TimeoutSeg) < Now Then
            Log.WriteInfo("We have waited " & TimeoutSeg & " seconds... we can't wait more!")
        End If


    End Sub

    Private Sub RevisarSiHayQueApagarPC()
        If Not Config.ApagarPC Or _Timer IsNot Nothing Then Exit Sub


        Dim TodosCompletadosOErroneos As Boolean = True
        Dim HayErroneos As Boolean = False
        Dim HayCola As Boolean = False

        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros

                HayCola = True

                If Fichero.DescargaEstado <> Estado.Completado And Fichero.DescargaEstado <> Estado.Erroneo Then
                    TodosCompletadosOErroneos = False
                End If
                If Fichero.DescargaEstado = Estado.Erroneo Then
                    HayErroneos = True
                End If

            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()

        If HayCola And TodosCompletadosOErroneos Then
            If Not Config.ResetearErrores Or Not HayErroneos Then
                ' Apagamos el PC!!!

                Dim AbortClose As Boolean = False
                For Each frm As Form In My.Application.OpenForms
                    If TypeOf frm Is Configuration Then ' No cerramos hasta que el usuario quite la pantalla de configuración
                        AbortClose = True
                    End If
                Next

                If Not AbortClose Then
                    _Timer = New System.Timers.Timer ' Creamos un timer para que este worker no se quede bloqueado
                    AddHandler _Timer.Elapsed, AddressOf ApagarPC_Event
                    _Timer.Interval = 500
                    _Timer.Enabled = True
                End If
            End If
        End If

    End Sub

    Private _Timer As System.Timers.Timer ' Temporizador que apaga el PC
    Private Sub ApagarPC_Event(source As Object, e As Timers.ElapsedEventArgs)
        If _Timer IsNot Nothing Then
            _Timer.Enabled = False
            _Timer = Nothing
            _ForzarCierre = True

            Log.WriteWarning("Turning off PC automatically")

            Process.Start("shutdown", "/s /t 60 /c """ & Language.GetText("Turning off in 60 seconds") & """")

            CerrarAplicacion()
        End If
    End Sub

#End Region

#Region "Manejo orden y prioridad de cola"

    ''' <summary>
    ''' Reordena la prioridad en base al orden interno (el orden de la lista de paquetes y descarga se toma como 
    ''' referencia a la hora de pintar la prioridad)
    ''' </summary>
    ''' <remarks>Se debe llamar a esta función cada vez que se modifica la lista de paquetes y descarga</remarks>
    Private Sub ReordenarPrioridadPaquetes(RefrescarFicheros As Boolean)
        Dim i As Integer = 0
        Mutex.ListaDescargas.WaitOne()
        For Each paquete As Paquete In Me.ListaPaquetes
            i += 1
            paquete.SetDescargaPrioridad = i
            If paquete.ListaFicheros IsNot Nothing Then
                For Each fichero As Fichero In paquete.ListaFicheros
                    i += 1
                    fichero.SetDescargaPrioridad = i
                Next
            End If
        Next
        Mutex.ListaDescargas.ReleaseMutex()
        If RefrescarFicheros Then
            RefreshListaDescargas(RefrescarFicheros)
        End If
    End Sub



    ''' <summary>
    ''' Define el comportamiento del listView cuando se arrastra un elemento (drag and drop)
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub ListaDescargas_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles ListaDescargas.DragDrop
        If e.Data.GetData("BrightIdeasSoftware.OLVListItem") IsNot Nothing Then
            ' Nothing, dropSink_ModelDropped will handle
        Else
            Main_DragDrop(sender, e) ' File drag & drop
        End If
    End Sub

    ''' <summary>
    ''' Define el comportamiento del listView cuando se arrastra un elemento (drag and drop)
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub ListaDescargas_CanDrop(sender As Object, e As BrightIdeasSoftware.OlvDropEventArgs) Handles ListaDescargas.CanDrop

        e.Effect = DragDropEffects.Move

        If e.DragEventArgs.Data.GetData(DataFormats.FileDrop) IsNot Nothing Then
            ' File drag & drop
            Dim ficheros() As String = CType(e.DragEventArgs.Data.GetData(DataFormats.FileDrop), String())
            Dim TodosExisten As Boolean = True
            For Each Fichero As String In ficheros
                If Not IO.File.Exists(Fichero) Then
                    TodosExisten = False
                End If
            Next
            If TodosExisten Then
                e.Effect = DragDropEffects.Copy
            End If
        End If

    End Sub


    ''' <summary>
    ''' Define el comportamiento del listView cuando se arrastra un elemento (drag and drop)
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub dropSink_ModelDropped(sender As Object, e As BrightIdeasSoftware.ModelDropEventArgs) Handles dropSink.ModelDropped

        For Each Source As IDescarga In e.SourceModels
            RealizarMovimiento(Source, e.TargetModel, e.DropTargetLocation)
        Next
        ReordenarPrioridadPaquetes(True)
    End Sub

    ''' <summary>
    ''' Realiza un movimiento dentro de la lista de paquetes
    ''' </summary>
    ''' <param name="Source"></param>
    ''' <param name="Target"></param>
    ''' <param name="TargetLocation"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function RealizarMovimiento(Source As Object, Target As Object, ByVal TargetLocation As DropTargetLocation) As Boolean
        If TypeOf (Source) Is Fichero And TypeOf (Target) Is Fichero Then

            Dim sourcet As Fichero = CType(Source, Fichero)
            Dim targett As Fichero = CType(Target, Fichero)

            ' Comprobamos que forman parte del mismo paquete
            Mutex.ListaDescargas.WaitOne()
            Try
                For Each paq As Paquete In Me.ListaPaquetes

                    Dim indSource As Integer = paq.ListaFicheros.FindIndex(Function(x)
                                                                               Return x.DescargaPrioridad = sourcet.DescargaPrioridad
                                                                           End Function)
                    Dim indTarget As Integer = paq.ListaFicheros.FindIndex(Function(x)
                                                                               Return x.DescargaPrioridad = targett.DescargaPrioridad
                                                                           End Function)
                    If indSource >= 0 And indTarget >= 0 And indTarget <> indSource Then
                        paq.ListaFicheros.RemoveRange(indSource, 1)
                        ' Recalculamos el target (puede haber cambiado al eliminar el source!)
                        indTarget = paq.ListaFicheros.FindIndex(Function(x)
                                                                    Return x.DescargaPrioridad = targett.DescargaPrioridad
                                                                End Function)
                        paq.ListaFicheros.Insert(If(TargetLocation = DropTargetLocation.BelowItem, indTarget + 1, indTarget), sourcet)
                        Return True
                    End If

                Next
            Finally
                Mutex.ListaDescargas.ReleaseMutex()
            End Try

        ElseIf TypeOf (Source) Is Paquete And TypeOf (Target) Is Paquete Then

            Dim sourcet As Paquete = CType(Source, Paquete)
            Dim targett As Paquete = CType(Target, Paquete)

            Mutex.ListaDescargas.WaitOne()
            Try
                Dim indSource As Integer = Me.ListaPaquetes.FindIndex(Function(x)
                                                                          Return x.DescargaPrioridad = sourcet.DescargaPrioridad
                                                                      End Function)
                Dim indTarget As Integer = Me.ListaPaquetes.FindIndex(Function(x)
                                                                          Return x.DescargaPrioridad = targett.DescargaPrioridad
                                                                      End Function)
                If indSource >= 0 And indTarget >= 0 And indTarget <> indSource Then
                    Me.ListaPaquetes.RemoveRange(indSource, 1)
                    ' Recalculamos el target (puede haber cambiado al eliminar el source!)
                    indTarget = Me.ListaPaquetes.FindIndex(Function(x)
                                                               Return x.DescargaPrioridad = targett.DescargaPrioridad
                                                           End Function)
                    Me.ListaPaquetes.Insert(If(TargetLocation = DropTargetLocation.BelowItem, indTarget + 1, indTarget), sourcet)
                    Return True
                End If
            Finally
                Mutex.ListaDescargas.ReleaseMutex()
            End Try

        End If
        Return False
    End Function


    ''' <summary>
    ''' Sube la prioridad de un elemento en un punto
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub SubirPrioridad_Click(sender As System.Object, e As System.EventArgs) Handles SubirPrioridadMenuItem.Click

        If ListaDescargas.SelectedObjects Is Nothing Then Exit Sub
        Dim ObjetoASeleccionar As Object = Nothing
        For Each selobject As Object In ListaDescargas.SelectedObjects


            If TypeOf (selobject) Is Paquete Then
                Dim Prioridad As Integer = CType(selobject, Paquete).DescargaPrioridad
                Dim i As Integer = 0
                Dim Encontrado As Boolean = False

                Mutex.ListaDescargas.WaitOne()
                For Each paq As Paquete In Me.ListaPaquetes
                    If paq.DescargaPrioridad = Prioridad And i > 0 Then
                        Encontrado = True
                        Exit For
                    End If
                    i += 1
                Next
                If Encontrado Then
                    Dim paqTemp As Paquete = Me.ListaPaquetes(i - 1)
                    Me.ListaPaquetes(i - 1) = CType(selobject, Paquete)
                    Me.ListaPaquetes(i) = paqTemp
                    ReordenarPrioridadPaquetes(True)
                    ObjetoASeleccionar = Me.ListaPaquetes(i - 1)
                End If
                Mutex.ListaDescargas.ReleaseMutex()

            ElseIf TypeOf (selobject) Is Fichero Then
                Dim Prioridad As Integer = CType(selobject, Fichero).DescargaPrioridad

                Mutex.ListaDescargas.WaitOne()
                For Each paq As Paquete In Me.ListaPaquetes
                    Dim i As Integer = 0
                    Dim Encontrado As Boolean = False
                    For Each file As Fichero In paq.ListaFicheros
                        If file.DescargaPrioridad = Prioridad And i > 0 Then
                            Encontrado = True
                            Exit For
                        End If
                        i += 1
                    Next
                    If Encontrado Then
                        Dim ficTemp As Fichero = paq.ListaFicheros(i - 1)
                        paq.ListaFicheros(i - 1) = CType(selobject, Fichero)
                        paq.ListaFicheros(i) = ficTemp
                        ReordenarPrioridadPaquetes(True)
                        ObjetoASeleccionar = paq.ListaFicheros(i - 1)
                        Exit For
                    End If
                Next
                Mutex.ListaDescargas.ReleaseMutex()

            End If
        Next
        If ObjetoASeleccionar IsNot Nothing Then ListaDescargas.SelectedObject = ObjetoASeleccionar
    End Sub

    ''' <summary>
    ''' Baja la prioridad de un elemento en un punto
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub BajarPrioridadMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles BajarPrioridadMenuItem.Click

        If ListaDescargas.SelectedObjects Is Nothing Then Exit Sub
        For Each selobject As Object In ListaDescargas.SelectedObjects


            If TypeOf (selobject) Is Paquete Then
                Dim Prioridad As Integer = CType(selobject, Paquete).DescargaPrioridad

                Mutex.ListaDescargas.WaitOne()
                Dim numPaquetes As Integer = Me.ListaPaquetes.Count - 1
                Dim i As Integer = 0
                Dim Encontrado As Boolean = False
                For Each paq As Paquete In Me.ListaPaquetes
                    If paq.DescargaPrioridad = Prioridad And i < numPaquetes Then
                        Encontrado = True
                        Exit For
                    End If
                    i += 1
                Next
                If Encontrado Then
                    Dim paqTemp As Paquete = Me.ListaPaquetes(i + 1)
                    Me.ListaPaquetes(i + 1) = CType(selobject, Paquete)
                    Me.ListaPaquetes(i) = paqTemp
                    Dim paqSeleccionar As Paquete = Me.ListaPaquetes(i + 1)
                    Mutex.ListaDescargas.ReleaseMutex()
                    ReordenarPrioridadPaquetes(True)
                    ListaDescargas.SelectedObject = paqSeleccionar
                Else
                    Mutex.ListaDescargas.ReleaseMutex()
                End If


            ElseIf TypeOf (selobject) Is Fichero Then
                Dim Prioridad As Integer = CType(selobject, Fichero).DescargaPrioridad

                Dim FicheroASeleccionar As Fichero = Nothing
                Mutex.ListaDescargas.WaitOne()
                For Each paq As Paquete In Me.ListaPaquetes
                    Dim numFicheros As Integer = paq.ListaFicheros.Count - 1
                    Dim i As Integer = 0
                    Dim Encontrado As Boolean = False
                    For Each file As Fichero In paq.ListaFicheros
                        If file.DescargaPrioridad = Prioridad And i < numFicheros Then
                            Encontrado = True
                            Exit For
                        End If
                        i += 1
                    Next
                    If Encontrado Then
                        Dim ficTemp As Fichero = paq.ListaFicheros(i + 1)
                        paq.ListaFicheros(i + 1) = CType(selobject, Fichero)
                        paq.ListaFicheros(i) = ficTemp
                        FicheroASeleccionar = paq.ListaFicheros(i + 1)
                        Exit For
                    End If
                Next
                Mutex.ListaDescargas.ReleaseMutex()

                If FicheroASeleccionar IsNot Nothing Then
                    ReordenarPrioridadPaquetes(True)
                    ListaDescargas.SelectedObject = FicheroASeleccionar
                End If
            End If
        Next
    End Sub

#End Region

#Region "Eliminar descarga"


    Private Sub EliminarMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles EliminarMenuItem.Click
        If ListaDescargas.SelectedObjects Is Nothing Then Exit Sub

        If MessageBox.Show(Language.GetText("Do you want to delete the element(s)?") & vbNewLine & Language.GetText("Note: files will NOT be deleted"), _
                           Language.GetText("Confirmation"), MessageBoxButtons.YesNo) = DialogResult.No Then
            Exit Sub
        End If

        For Each selobject As Object In ListaDescargas.SelectedObjects
            If TypeOf (selobject) Is IDescarga Then
                Log.WriteError("Deleting from list " & CType(selobject, IDescarga).DescargaNombre)
                Eliminar(CType(selobject, IDescarga), False, False)
            End If
        Next
        RefreshListaDescargas(True)
    End Sub

    Private Sub EliminarYBorrarMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles EliminarYBorrarMenuItem.Click
        If ListaDescargas.SelectedObjects Is Nothing Then Exit Sub

        If MessageBox.Show(Language.GetText("Do you want to delete the element(s)?") & vbNewLine & Language.GetText("Note: files will BE deleted"), _
                         Language.GetText("Confirmation"), MessageBoxButtons.YesNo) = DialogResult.No Then
            Exit Sub
        End If

        For Each selobject As Object In ListaDescargas.SelectedObjects
            If TypeOf (selobject) Is IDescarga Then
                Log.WriteError("Deleting from list and disk " & CType(selobject, IDescarga).DescargaNombre)
                Eliminar(CType(selobject, IDescarga), True, False)
            End If
        Next
        RefreshListaDescargas(True)
    End Sub

    Private Sub Eliminar(Objeto As IDescarga, ByVal BorrarFicheros As Boolean, ByVal RefrescarFicheros As Boolean)

        ' Quitamos el objeto de la lista y así no lo pintamos ni procesamos más
        Dim paqueteAEliminar As Paquete = Nothing
        Dim ficheroAEliminar As Fichero = Nothing

        Mutex.ListaDescargas.WaitOne()
        Try
            For Each paq As Paquete In Me.ListaPaquetes
                If paq.DescargaPrioridad = Objeto.DescargaPrioridad Then
                    paqueteAEliminar = paq
                    Exit Try
                End If
                For Each fic As Fichero In paq.ListaFicheros
                    If Objeto.DescargaPrioridad = fic.DescargaPrioridad Then
                        ficheroAEliminar = fic
                        paqueteAEliminar = paq
                        Exit Try
                    End If
                Next
            Next
        Finally
        End Try
        If ficheroAEliminar IsNot Nothing Then
            paqueteAEliminar.ListaFicheros.Remove(ficheroAEliminar)
            ThrottledStreamController.GetController.RemoveId(ficheroAEliminar.FileID)
            If paqueteAEliminar.ListaFicheros.Count = 0 Then
                ListaPaquetes.Remove(paqueteAEliminar)
            End If
        ElseIf paqueteAEliminar IsNot Nothing Then
            ListaPaquetes.Remove(paqueteAEliminar)
            For Each fic As Fichero In paqueteAEliminar.ListaFicheros
                ThrottledStreamController.GetController.RemoveId(fic.FileID)
            Next
        End If
        PeticionGuardadoFichero = Now
        Mutex.ListaDescargas.ReleaseMutex()
        ReordenarPrioridadPaquetes(RefrescarFicheros)


        ' Ya podemos trabajar con el objeto "tranquilamente"
        If TypeOf (Objeto) Is Fichero Then
            Dim fic As Fichero = CType(Objeto, Fichero)
            Select Case Objeto.DescargaEstado
                Case Estado.ComprobandoMD5, Estado.Descargando, Estado.Pausado, Estado.Descomprimiendo
                    AddHandler fic.CancellationComplete, AddressOf DisposeFichero
                    fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros
                    fic.Stop()
                    Log.WriteDebug("File download stopped " & fic.NombreFichero)
                Case Else
                    fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros
                    fic.BorrarFicheroLocal()
                    fic.Dispose()
            End Select

        ElseIf TypeOf (Objeto) Is Paquete Then

            For Each fic As Fichero In CType(Objeto, Paquete).ListaFicheros
                Select Case fic.DescargaEstado
                    Case Estado.ComprobandoMD5, Estado.Descargando, Estado.Pausado, Estado.Descomprimiendo
                        AddHandler fic.CancellationComplete, AddressOf DisposeFichero
                        fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros
                        fic.Stop()
                        Log.WriteDebug("File download stopped " & fic.NombreFichero)
                    Case Else
                        fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros
                        fic.BorrarFicheroLocal()
                        fic.Dispose()
                End Select
            Next
            CType(Objeto, Paquete).ListaFicheros.Clear()
        End If

    End Sub

    Private Sub DisposeFichero(ByVal sender As System.Object, ByVal e As System.EventArgs)
        If TypeOf (sender) Is Fichero Then
            Dim fic As Fichero = CType(sender, Fichero)
            If fic IsNot Nothing Then
                Log.WriteDebug("Download file stopped " & fic.NombreFichero & ", pending delete")
                If fic.MarcadoParaBorrarFicheroLocal Then
                    fic.BorrarFicheroLocal()
                End If
                fic.Dispose()
            End If
        End If
    End Sub

#End Region

#Region "Funciones control remoto"

    Friend Function ControlRemotoObtenerVelocidad() As Decimal?
        Return Me.VelocidadGlobalDescarga
    End Function

    Friend Function ControlRemotoObtenerDescargasActivas() As Integer?
        Return Me.NumDescargasActivas
    End Function

    Friend Function ControlRemotoObtenerDescargasCompletadas() As Integer?
        Return Me.NumDescargasCompletadas
    End Function

    Friend Function ControlRemotoObtenerDescargasErroneas() As Integer?
        Return Me.NumDescargasErroneas
    End Function

    Friend Function ControlRemotoObtenerDescargasEnCola() As Integer?
        Return Me.NumDescargasEnCola
    End Function

    Friend Function ControlRemotoObtenerEstado() As TipoEstadoAplicacion
        Return Me.EstadoAplicacion
    End Function

    Friend Sub ControlRemotoDescargar()
        btnPlay_Click(Nothing, Nothing)
    End Sub
    Friend Sub ControlRemotoParar()
        btnStop_Click(Nothing, Nothing)
    End Sub

    Friend Function ControlRemotoAgregarLinks(ByVal Links As String, ByVal NombrePaquete As String, ByVal CrearDirectorio As Boolean) As String
        Dim URLs As Generic.List(Of String) = URLExtractor.ExtraerURLs(Links)
        If URLs.Count = 0 Then
            Return Language.GetText("No valid URLs have been inserted")
        ElseIf String.IsNullOrEmpty(Config.RutaDefecto) OrElse Not System.IO.Directory.Exists(Config.RutaDefecto) Then
            Return Language.GetText("Can not add remote links without a default path")
        Else
            Dim oPaquete As New Paquete
            With oPaquete
                .Nombre = NombrePaquete
                If String.IsNullOrEmpty(.Nombre) Then
                    .Nombre = Language.GetText("New package")
                End If
                .RutaLocal = Config.RutaDefecto
                .CrearSubdirectorio = CrearDirectorio

                ' Creamos el directorio
                If .CrearSubdirectorio Then
                    .RutaLocal = .RutaLocal.Trim("\"c) & "\" & .Nombre
                    System.IO.Directory.CreateDirectory(.RutaLocal)
                End If

                Log.WriteWarning("Adding package in " & .RutaLocal)

                .SetDescargaExtraccionAutomatica(Nothing) = Config.ExtraerAutomaticamente
                For Each URL As String In URLs

                    Dim oFichero As New Fichero(URL)
                    With oFichero
                        .RutaLocal = oPaquete.RutaLocal
                        .NombreFichero = URL
                        .FileID = Fichero.ExtraerFileID(URL)
                        .FileKey = Fichero.ExtraerFileKey(URL)
                        .SetDescargaExtraccionAutomatica(Nothing) = Config.ExtraerAutomaticamente
                        Log.WriteWarning("Adding files to package: " & .FileID)
                    End With
                    .AgregarFichero(oFichero)

                Next
            End With
            Me.AgregarPaquete(oPaquete, True)

            Return ""
        End If
    End Function


#End Region

#Region "Eventos varios"

    ''' <summary>
    ''' Indica si la versión del SO es superior a XP (Vista para delante)
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function VersionMayorWindowsXP() As Boolean
        'http://stackoverflow.com/questions/2819934/detect-windows-7-in-net
        If System.Environment.OSVersion.Platform = PlatformID.Win32NT Then
            Return Environment.OSVersion.Version.Major > 5 ' XP es la 5, Vista es la 6
        Else
            Return False
        End If
    End Function


    ''' <summary>
    ''' Define el comportamiento del evento del portapapeles
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub clipChange_ClipboardChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles clipChange.ClipboardChanged
        'Application.DoEvents()

        If Config IsNot Nothing AndAlso Config.AnalizarPortapapeles Then
            Dim TextoPortapapeles As String = CStr(Clipboard.GetDataObject.GetData(GetType(String)))
            ComprobarYAgregarLinks(TextoPortapapeles, True, False)
        End If
    End Sub

    Public Shared Function IsFormAlreadyOpen(FormType As Type) As Form
        For Each OpenForm As Form In Application.OpenForms
            If OpenForm.GetType.FullName = FormType.FullName Then
                Return OpenForm
            End If
        Next
        Return Nothing
    End Function



    ' Hace un "trim" de la memoria, equivalente a minimizar la ventana... muy útil cuando
    ' en .NET la memoria empieza a crecer y crecer y la máquina virtual no suelta la memoria
    ' no usada sino que la acapara...
    Public Shared Sub FlushMemory()
        GC.Collect()
        GC.WaitForPendingFinalizers()
        If (Environment.OSVersion.Platform = PlatformID.Win32NT) Then
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1)
        End If
    End Sub
    Private Declare Function SetProcessWorkingSetSize Lib "kernel32.dll" ( _
     ByVal process As IntPtr, _
     ByVal minimumWorkingSetSize As Integer, _
     ByVal maximumWorkingSetSize As Integer) As Integer


    ' This delegate enables asynchronous calls for setting
    ' the text property on a TextBox control.
    Delegate Sub MsgBoxCallback(text As String, caption As String, buttons As System.Windows.Forms.MessageBoxButtons, icon As System.Windows.Forms.MessageBoxIcon)
    Private Sub MsgBox(text As String, caption As String, buttons As System.Windows.Forms.MessageBoxButtons, icon As System.Windows.Forms.MessageBoxIcon)
        ' InvokeRequired required compares the thread ID of the
        ' calling thread to the thread ID of the creating thread.
        ' If these threads are different, it returns true.
        If Me.StatusStrip1.InvokeRequired Then
            Dim d As New MsgBoxCallback(AddressOf MsgBox)
            Me.Invoke(d, New Object() {text, caption, buttons, icon})
        Else
            MessageBox.Show(text, caption, buttons, icon)
        End If
    End Sub

    ' This delegate enables asynchronous calls for setting
    ' the text property on a TextBox control.
    Delegate Sub SetStatusBarCallback(RAM As String, Proc As String, Estado As String, velocidad As String, configuracionConexiones As String)
    Private Sub SetStatusBar(RAM As String, Proc As String, Estado As String, velocidad As String, configuracionConexiones As String)
        ' InvokeRequired required compares the thread ID of the
        ' calling thread to the thread ID of the creating thread.
        ' If these threads are different, it returns true.
        Try
            If Cerrando Then Exit Sub
            If Me.StatusStrip1.InvokeRequired Then
                Dim d As New SetStatusBarCallback(AddressOf SetStatusBar)
                Me.Invoke(d, New Object() {RAM, Proc, Estado, velocidad, configuracionConexiones})
            Else
                Me.RAMProcToolStripStatusLabel.Text = Language.GetText("RAM") & ": " & RAM & " / " & Language.GetText("Proc") & ": " & Proc
                Me.StatusToolStripStatusLabel.Text = Language.GetText("Status") & ": " & Estado & velocidad & "    " & Language.GetText("Connection conf") & ": " & configuracionConexiones
            End If
        Catch ex As Exception
            ' No hacemos nada
        End Try
    End Sub

    ' Permite refrescar el listado de descargas
    ' This delegate enables asynchronous calls for refreshing ListaDescargas
    Delegate Sub RefreshListaDescargasCallback(ByVal SetObjects As Boolean)
    Private Sub RefreshListaDescargas(ByVal SetObjects As Boolean)
        ' InvokeRequired required compares the thread ID of the
        ' calling thread to the thread ID of the creating thread.
        ' If these threads are different, it returns true.
        If Me.ListaDescargas.InvokeRequired Then
            Dim d As New RefreshListaDescargasCallback(AddressOf RefreshListaDescargas)
            Me.Invoke(d, New Object() {SetObjects})
        Else
            Mutex.ListaDescargas.WaitOne()
            Try
                If SetObjects Then
                    ListaDescargas.SetObjects(Me.ListaPaquetes)
                    ListaDescargas.BuildList()
                End If

                ListaDescargas.RefreshObjects(CType(ListaDescargas.Roots, Collections.IList))
            Finally
                Mutex.ListaDescargas.ReleaseMutex()
            End Try

        End If
    End Sub

    ' This delegate enables asynchronous calls for setting
    ' the text property on a TextBox control.
    Delegate Sub TextoIconoMinimizadoCallback(txt As String)
    Private Sub TextoIconoMinimizado(txt As String)
        ' InvokeRequired required compares the thread ID of the
        ' calling thread to the thread ID of the creating thread.
        ' If these threads are different, it returns true.
        If Me.btnPause.InvokeRequired Then
            Dim d As New TextoIconoMinimizadoCallback(AddressOf TextoIconoMinimizado)
            Me.Invoke(d, New Object() {txt})
        Else
            Me.IconoMinimizado.Text = txt
        End If
    End Sub

    ' This delegate enables asynchronous calls for setting
    ' the text property on a TextBox control.
    Delegate Sub ActivarUpdateButtonCallback()
    Private Sub ActivarUpdateButton()
        ' InvokeRequired required compares the thread ID of the
        ' calling thread to the thread ID of the creating thread.
        ' If these threads are different, it returns true.
        If Me.btnPause.InvokeRequired Then
            Dim d As New ActivarUpdateButtonCallback(AddressOf ActivarUpdateButton)
            Me.Invoke(d, New Object() {})
        Else
            If Not Me.btnUpdate.Visible Then
                Me.btnUpdate.Visible = True
            End If
        End If
    End Sub

    Delegate Sub CerrarAplicacionCallback()
    Private Sub CerrarAplicacion()
        ' InvokeRequired required compares the thread ID of the
        ' calling thread to the thread ID of the creating thread.
        ' If these threads are different, it returns true.
        If Me.btnPause.InvokeRequired Then
            Dim d As New CerrarAplicacionCallback(AddressOf CerrarAplicacion)
            Me.Invoke(d, New Object() {})
        Else
            Me.Close()
        End If
    End Sub


    Private Sub Main_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragDrop
        If e.Data.GetData(DataFormats.FileDrop) IsNot Nothing Then
            ' File drag & drop

            Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
            Dim TodosExisten As Boolean = True
            For Each Fichero As String In ficheros
                If Not IO.File.Exists(Fichero) Then ' No permitimos directorios
                    TodosExisten = False
                End If
            Next
            If TodosExisten Then

                For Each Fichero As String In ficheros
                    If Fichero.ToUpper.EndsWith(".DLC") Or Fichero.ToUpper.EndsWith(".ELC") Then
                        AddDLC(Fichero)
                        Exit Sub
                    End If
                Next
            End If
        End If
    End Sub

    Private Sub Main_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter
        If e.Data.GetData(DataFormats.FileDrop) IsNot Nothing Then
            ' File drag & drop
            Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
            Dim TodosExisten As Boolean = True
            For Each Fichero As String In ficheros
                If Not IO.File.Exists(Fichero) Then  ' No permitimos directorios
                    TodosExisten = False
                End If
            Next
            If TodosExisten Then
                e.Effect = DragDropEffects.Copy
            End If
        End If
    End Sub

    'Private Sub Main_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragDrop
    '    Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
    '    For Each Fichero As String In ficheros
    '        If Fichero.ToUpper.EndsWith(".SSK") And Me.Config.PermitirSkins Then
    '            SkinEngine.SkinFile = Fichero
    '            Me.Config.ConfigUI.RutaSkin = Fichero
    '            If Not SkinEngine.Active Then
    '                SkinEngine.Active = True
    '            End If
    '        End If
    '    Next
    'End Sub

    'Private Sub Main_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter
    '    If e.Data.GetDataPresent(DataFormats.FileDrop) And Me.Config.PermitirSkins Then
    '        Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
    '        For Each Fichero As String In ficheros
    '            If Fichero.ToUpper.EndsWith(".SSK") Then
    '                e.Effect = DragDropEffects.Copy
    '            End If
    '        Next
    '    End If
    'End Sub

    Private ProximoAvisoActualizacion As Date? = Nothing
    Delegate Sub MostrarMensajeActualizacionCallback()
    Private Sub MostrarMensajeActualizacion()
        If Me.StatusStrip1.InvokeRequired Then
            Dim d As New MostrarMensajeActualizacionCallback(AddressOf MostrarMensajeActualizacion)
            Me.Invoke(d, New Object() {})
        Else
            If Form.ActiveForm IsNot Nothing AndAlso Form.ActiveForm.Equals(Me) Then
                ProximoAvisoActualizacion = Date.MaxValue  ' Evitamos que si el usuario no cierra el mensaje vuelva a salir
                If MessageBox.Show(Language.GetText("New version do you want to download it? Recommended!"), _
                                   Language.GetText("New version available"), MessageBoxButtons.YesNo) = Windows.Forms.DialogResult.Yes Then
                    btnUpdate_Click(Nothing, Nothing)
                    ' Ya no avisamos más al usuario y dejamos Date.MaxValue  
                Else
                    ProximoAvisoActualizacion = Now.AddHours(3) ' Cada 3 horas se lo recordamos
                End If
            End If
        End If
    End Sub



    Private Sub DescompresionFinalizada_EventHandler(ByVal Code As String)
        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each fic As Fichero In paq.ListaFicheros
                If fic.FileID = Code Then
                    fic.DescompresionFinalizada()
                End If
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
    End Sub

#End Region

#Region "Botones y menús"

    ''' <summary>
    ''' Definimos el menú que saldrá según donde se haga click derecho
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub ListaDescargas_CellRightClick(sender As Object, e As BrightIdeasSoftware.CellRightClickEventArgs) Handles ListaDescargas.CellRightClick

        If ListaDescargas.SelectedObjects Is Nothing OrElse ListaDescargas.SelectedObjects.Count = 0 Then
            e.MenuStrip = MenuPanel
        Else
            VerErrorToolStripMenuItem.Visible = False
            VerProgresoDescompresionToolStripMenuItem.Visible = False
            ResetToolStripMenuItem.Visible = False
            PropiedadesToolStripMenuItem.Enabled = False
            PausarStripMenuItem.Visible = False
            ForceDownloadStripMenuItem.Visible = False
            If ListaDescargas.SelectedObjects.Count = 1 Then
                PropiedadesToolStripMenuItem.Enabled = True
            End If
            For Each o As Object In ListaDescargas.SelectedObjects
                If TypeOf (o) Is IDescarga AndAlso CType(o, IDescarga).DescargaEstado = Estado.Erroneo Then
                    VerErrorToolStripMenuItem.Visible = True
                    ResetToolStripMenuItem.Visible = True
                ElseIf TypeOf (o) Is Fichero AndAlso CType(o, Fichero).DescargaEstado = Estado.Descargando Then
                    PausarStripMenuItem.Visible = True
                ElseIf TypeOf (o) Is Fichero AndAlso CType(o, Fichero).DescargaEstado = Estado.EnCola Then
                    ForceDownloadStripMenuItem.Visible = True
                ElseIf TypeOf (o) Is Fichero AndAlso CType(o, Fichero).DescargaEstado = Estado.Pausado Then
                    ForceDownloadStripMenuItem.Visible = True
                ElseIf TypeOf (o) Is Fichero AndAlso CType(o, Fichero).DescargaEstado = Estado.Descomprimiendo Then
                    VerProgresoDescompresionToolStripMenuItem.Visible = True
                End If
            Next
            e.MenuStrip = MenuDescarga
        End If
    End Sub


    ''' <summary>
    ''' Agregamos links con el menú contextual
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub AgregarLinksToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles AgregarLinksToolStripMenuItem.Click
        AgregarLink()
    End Sub

    Private Sub AgregarLinkStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles AgregarLinkStripMenuItem.Click
        AgregarLink()
    End Sub


    Private Sub btnAddLink_Click(sender As System.Object, e As System.EventArgs) Handles btnAddLink.Click
        AgregarLink()
    End Sub


    Private Sub LimpiarCompletados2ToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles LimpiarCompletados2ToolStripMenuItem.Click
        LimpiarCompletados()
    End Sub
    Private Sub LimpiarCompletadosToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles LimpiarCompletadosToolStripMenuItem.Click
        LimpiarCompletados()
    End Sub

    Private Sub LimpiarCompletados()
        Mutex.ListaDescargas.WaitOne()
        Dim listaFicherosEliminar As New Generic.List(Of Fichero)
        For Each paq As Paquete In Me.ListaPaquetes
            For Each fic As Fichero In paq.ListaFicheros
                If fic.EstadoDescarga = Estado.Completado Then
                    listaFicherosEliminar.Add(fic)
                End If
            Next
        Next
        Mutex.ListaDescargas.ReleaseMutex()
        For Each fic As Fichero In listaFicherosEliminar
            Log.WriteDebug("Deleting file " & fic.NombreFichero)
            Eliminar(fic, False, False)
        Next
        RefreshListaDescargas(True)
    End Sub


    Private Sub About_Click(sender As System.Object, e As System.EventArgs)

        Dim ventanaError As New Credits
        ventanaError.Text = Language.GetText("About")
        ventanaError.ShowDialog()
        ventanaError.Dispose()

    End Sub

    Friend Sub FAQ_Click(sender As System.Object, e As System.EventArgs)
        Dim codIdi As String = Language.GetCurrentLanguageCode.ToUpperInvariant
        If codIdi.StartsWith("ES") Then
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("FAQ_LINK_ES"))
        ElseIf codIdi.StartsWith("FR") Then
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("FAQ_LINK_FR"))
        Else
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("FAQ_LINK_EN"))
        End If
    End Sub

    Private Sub CheckUpdates_Click(sender As System.Object, e As System.EventArgs)
        Dim codIdi As String = Language.GetCurrentLanguageCode.ToUpperInvariant
        If codIdi.StartsWith("ES") Then
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("DOWNLOAD_LINK_ES"))
        Else
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("DOWNLOAD_LINK_EN"))
        End If
    End Sub


    Private Sub Collaborate_Click(sender As System.Object, e As System.EventArgs) Handles btnCollaborate.Click
        Dim codIdi As String = Language.GetCurrentLanguageCode.ToUpperInvariant
        If codIdi.StartsWith("ES") Then
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("COLLABORATE_LINK_ES"))
        Else
            System.Diagnostics.Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("COLLABORATE_LINK_EN"))
        End If
    End Sub

    Private Sub GetMegaUploader_Click(sender As System.Object, e As System.EventArgs)
        Dim codIdi As String = Language.GetCurrentLanguageCode.ToUpperInvariant
        Dim MegaUploaderLink As String
        If codIdi.StartsWith("ES") Then
            MegaUploaderLink = InternalConfiguration.ObtenerValueFromInternalConfig("MEGAUPLOADER_LINK_ES")
        Else
            MegaUploaderLink = InternalConfiguration.ObtenerValueFromInternalConfig("MEGAUPLOADER_LINK_EN")
        End If


        Dim Key As String = Fichero.ExtraerFileKey(MegaUploaderLink)
        'If String.IsNullOrEmpty(Key) AndAlso Fichero.EsUrlAcortador(MegaUploaderLink) Then
        '    Dim url As String = Conexion.ObtenerUrlDesdeAcortador(MegaUploaderLink)
        '    Key = Fichero.ExtraerFileKey(MegaUploaderLink)
        'End If

        If String.IsNullOrEmpty(Key) Then
            ' Parece que no es un link de mega, será un link directo al ejecutable...
            System.Diagnostics.Process.Start(MegaUploaderLink)
        Else
            AgregarLink(MegaUploaderLink, "MegaUploader", True, False)
        End If

    End Sub


    Private Sub Buscador_Click(sender As System.Object, e As System.EventArgs)
        Dim tag = CType(sender, System.Windows.Forms.MenuItem).Text

        Dim element = (From n In InternalConfiguration.ObtenerValuesFromInternalConfig("SEARCH_LIST/ELEMENT") Where n.Key = tag).FirstOrDefault
        System.Diagnostics.Process.Start(element.Value)
    End Sub


    ''' <summary>
    ''' Botón de "Configuración"
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnConfig_Click(sender As System.Object, e As System.EventArgs) Handles btnConfig.Click
        Dim frmName As New Configuration
        frmName.MainForm = Me
        frmName.Config = Config
        frmName.RequiereConfiguracion = False
        frmName.ShowDialog()
        ' Hasta que no se cierre la ventana no continuamos la ejecución
        frmName.Dispose()
    End Sub


    Private Sub VerStreaming_Click(sender As System.Object, e As System.EventArgs)
        If Main.IsFormAlreadyOpen(GetType(StreamingForm)) Is Nothing Then
            Dim frmName As New StreamingForm
            frmName.MainForm = Me
            frmName.Config = Me.Config
            frmName.Show()
        End If
    End Sub
    Private Sub CreateStegano_Click(sender As System.Object, e As System.EventArgs)
        If Main.IsFormAlreadyOpen(GetType(Stegano.SteganoWizardSave)) Is Nothing Then
            Dim frmName As New Stegano.SteganoWizardSave
            frmName.MainForm = Me
            frmName.Config = Me.Config
            frmName.Show()
        End If
    End Sub

    Private Sub UseStegano_Click(sender As System.Object, e As System.EventArgs)
        OpenSteganoWizard()
    End Sub

    Public Function OpenSteganoWizard() As Stegano.SteganoWizardLoad
        Dim f As Stegano.SteganoWizardLoad = CType(Main.IsFormAlreadyOpen(GetType(Stegano.SteganoWizardLoad)), Stegano.SteganoWizardLoad)
        If f Is Nothing Then
            f = New Stegano.SteganoWizardLoad
            f.MainForm = Me
            f.Config = Me.Config
            f.Show()
        End If
        Return f
    End Function


    Private Sub LibraryManager_Click(sender As System.Object, e As System.EventArgs)
        If Not Config.ServidorStreamingActivo Then
            MessageBox.Show(Language.GetText("Streaming server not activated"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Else
            System.Diagnostics.Process.Start(StreamingHelper.LibraryManagerURL(Config.ServidorStreamingPuerto, True))
        End If
    End Sub

    Private Sub SeeLibraryManager_Click(sender As System.Object, e As System.EventArgs)
        If Not Config.ServidorStreamingActivo Then
            MessageBox.Show(Language.GetText("Streaming server not activated"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Else
            System.Diagnostics.Process.Start(StreamingHelper.LibraryManagerURL(Config.ServidorStreamingPuerto, False))
        End If
    End Sub

    Private Sub VerLogs_Click(sender As System.Object, e As System.EventArgs)
        Dim PathLog As String = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader")

        If Not System.IO.Directory.Exists(PathLog) Then
            System.IO.Directory.CreateDirectory(PathLog)
        End If

        System.Diagnostics.Process.Start(PathLog)
    End Sub

    Private Sub CodificarEnlaces_Click(sender As System.Object, e As System.EventArgs)
        If Main.IsFormAlreadyOpen(GetType(EncodeLinksForm)) Is Nothing Then
            Dim frmName As New EncodeLinksForm
            frmName.MainForm = Me
            frmName.Show()
        End If
    End Sub

    Private Sub GenerateELC_Click(sender As System.Object, e As System.EventArgs)
        If Main.IsFormAlreadyOpen(GetType(EncodeLinksForm)) Is Nothing Then
            Dim frmName As New ELCForm
            frmName.MainForm = Me
            frmName.Show()
        End If
    End Sub

    Public Sub StartDownload()
        QuitarPausasIndividuales()
        Me.EstadoAplicacion = TipoEstadoAplicacion.Descargando
        ThrottledStreamController.GetController.Continuar()
    End Sub
    Public Sub PauseDownload()
        QuitarDescargasIndividuales()
        Me.EstadoAplicacion = TipoEstadoAplicacion.Pausa
        ThrottledStreamController.GetController.Abortar()
    End Sub
    Public Sub StopDownload()
        QuitarDescargasIndividuales()
        Me.EstadoAplicacion = TipoEstadoAplicacion.Parado
        ThrottledStreamController.GetController.Abortar()
    End Sub

    Private Sub btnPlay_Click(sender As System.Object, e As System.EventArgs) Handles btnPlay.Click
        StartDownload()
    End Sub

    Private Sub btnPause_Click(sender As System.Object, e As System.EventArgs) Handles btnPause.Click
        PauseDownload()
    End Sub

    Private Sub btnStop_Click(sender As System.Object, e As System.EventArgs) Handles btnStop.Click
        StopDownload()
    End Sub


    Private Sub btnUpdate_Click(sender As System.Object, e As System.EventArgs) Handles btnUpdate.Click
        If String.IsNullOrEmpty(Me.UrlNuevaVersionMegadownloader) Then Exit Sub
        If Not Me.UrlNuevaVersionMegadownloader.StartsWith("http") Then Exit Sub
        Dim Key As String = Fichero.ExtraerFileKey(UrlNuevaVersionMegadownloader)
        If String.IsNullOrEmpty(Key) AndAlso URLExtractor.EsUrlAcortador(UrlNuevaVersionMegadownloader) Then
            Dim url As String = Conexion.ObtenerUrlDesdeAcortador(UrlNuevaVersionMegadownloader)
            Key = Fichero.ExtraerFileKey(UrlNuevaVersionMegadownloader)
        End If

        If String.IsNullOrEmpty(Key) Then
            ' Parece que no es un link de mega, será un link directo al ejecutable...
            System.Diagnostics.Process.Start(UrlNuevaVersionMegadownloader)
        Else
            AgregarLink(UrlNuevaVersionMegadownloader, "MegaDownloader v" & VersionNuevaVersionMegadownloader, True, False)
        End If
    End Sub

    Private Sub AbrirEnCarpetaToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles AbrirEnCarpetaToolStripMenuItem.Click
        If ListaDescargas.SelectedObjects Is Nothing Then Exit Sub
        Dim Ruta As String = ""
        For Each selobject As Object In ListaDescargas.SelectedObjects
            If TypeOf (selobject) Is Fichero Then
                Ruta = CType(selobject, Fichero).RutaLocal
            ElseIf TypeOf (selobject) Is Paquete Then
                Ruta = CType(selobject, Paquete).RutaLocal
            End If
        Next
        If System.IO.Directory.Exists(Ruta) Then
            System.Diagnostics.Process.Start(Ruta)
        Else
            MessageBox.Show(Language.GetText("Directory %D% does not exist").Replace("%D%", Ruta), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If

    End Sub

    Private Sub ResetToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles ResetToolStripMenuItem.Click
        For Each obj As Object In ListaDescargas.SelectedObjects
            If TypeOf (obj) Is Paquete Then
                For Each fic As Fichero In CType(obj, Paquete).ListaFicheros
                    If fic.DescargaEstado = Estado.Erroneo Then
                        Log.WriteDebug("Reseting file " & fic.NombreFichero)
                        fic.SetDescargaEstado = Estado.EnCola
                    End If
                Next
            ElseIf TypeOf (obj) Is Fichero Then
                Dim fic As Fichero = CType(obj, Fichero)
                If fic.DescargaEstado = Estado.Erroneo Then
                    Log.WriteDebug("Reseting file " & fic.NombreFichero)
                    fic.SetDescargaEstado = Estado.EnCola
                End If
            End If
        Next

    End Sub


    Private Sub VerProgresoDescompresionToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles VerProgresoDescompresionToolStripMenuItem.Click
        If Main.IsFormAlreadyOpen(GetType(Descompresor)) Is Nothing Then
            Dim frmName As New Descompresor
            frmName.Show()
        End If
    End Sub

    Private Sub VerErrorToolStripMenuItem_Click(sender As Object, e As System.EventArgs) Handles VerErrorToolStripMenuItem.Click

        Dim ht As New HashSet(Of String) ' Evitamos repetidos (por ejemplo si seleccionamos un paquete y sus ficheros, saldrían los errores 2 veces)
        For Each obj As Object In ListaDescargas.SelectedObjects
            If TypeOf (obj) Is Paquete Then
                For Each fic As Fichero In CType(obj, Paquete).ListaFicheros
                    If fic.DescargaEstado = Estado.Erroneo Then
                        ht.Add(fic.DescripcionError)
                    End If
                Next
            ElseIf TypeOf (obj) Is Fichero Then
                Dim fic As Fichero = CType(obj, Fichero)
                If fic.DescargaEstado = Estado.Erroneo Then
                    ht.Add(fic.DescripcionError)
                End If

            End If
        Next
        Dim msg As String = ""
        For Each Str As String In ht
            msg &= Str & vbNewLine & vbNewLine
        Next
        msg = msg.Trim


        Dim ventanaError As New PantallaMsg
        ventanaError.Text = Language.GetText("Error information")
        ventanaError.TextoError = msg
        ventanaError.ShowDialog()
        ventanaError.Dispose()
    End Sub

    Private Sub VerLinksToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles VerLinksToolStripMenuItem.Click
        VerLinks(False, False)
    End Sub
    Private Sub VerLinksDescToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles VerLinksDescToolStripMenuItem.Click
        VerLinks(True, False)
    End Sub
    Private Sub OcultarEnlacesImagenMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles OcultarEnlacesImagenMenuItem.Click
        VerLinks(True, True)
    End Sub

    Private Sub VerLinks(DescripcionFichero As Boolean, stegano As Boolean)
        Dim ht As New HashSet(Of String) ' Evitamos repetidos (por ejemplo si seleccionamos un paquete y sus ficheros, saldrían los links 2 veces)
        For Each obj As Object In ListaDescargas.SelectedObjects
            If TypeOf (obj) Is Paquete Then
                For Each fic As Fichero In CType(obj, Paquete).ListaFicheros
                    If DescripcionFichero Then
                        ht.Add(GetFullFileDesc(fic))
                    Else
                        ht.Add(If(fic.LinkVisible, fic.URL, Fichero.HIDDEN_LINK_DESC))
                    End If
                Next
            ElseIf TypeOf (obj) Is Fichero Then
                Dim fic As Fichero = CType(obj, Fichero)
                If DescripcionFichero Then
                    ht.Add(GetFullFileDesc(fic))
                Else
                    ht.Add(If(fic.LinkVisible, fic.URL, Fichero.HIDDEN_LINK_DESC))
                End If
            End If
        Next
        Dim msg As New System.Text.StringBuilder

        Dim order As Boolean = DescripcionFichero

        If order Then
            For Each Str As String In (From s In ht Order By s)
                msg.Append(Str & vbNewLine & If(DescripcionFichero, vbNewLine, String.Empty))
            Next
        Else
            For Each Str As String In ht
                msg.Append(Str & vbNewLine & If(DescripcionFichero, vbNewLine, String.Empty))
            Next
        End If

        If stegano Then

            Dim frm As Form = IsFormAlreadyOpen(GetType(Stegano.SteganoWizardSave))
            ' Si ya está abierta la pantalla no la volvemos a abrir
            If (frm Is Nothing) Then
                Dim frmStegano As New Stegano.SteganoWizardSave
                frmStegano.MainForm = Me
                frmStegano.Config = Me.Config
                frmStegano.txtLinks.Text = msg.ToString.Trim
                frmStegano.Show()
            Else
                Dim frmStegano As Stegano.SteganoWizardSave = CType(frm, MegaDownloader.Stegano.SteganoWizardSave)
                If String.IsNullOrEmpty(frmStegano.txtLinks.Text) Then
                    frmStegano.txtLinks.Text = msg.ToString.Trim
                Else
                    frmStegano.txtLinks.Text &= vbNewLine & msg.ToString.Trim
                End If
                frmStegano.Focus()
            End If

        Else

            Dim ventanaError As New PantallaMsg
            ventanaError.Text = Language.GetText("Links")
            ventanaError.TextoError = msg.ToString.Trim
            ventanaError.MostrarCodificarEnlaces = True
            ventanaError.ShowDialog(Me)
            ventanaError.Dispose()

        End If

    End Sub

    Private Function GetFullFileDesc(ByRef fic As Fichero) As String
        Dim str As New System.Text.StringBuilder
        str.Append(fic.DescargaNombre)
        If fic.TamanoBytes > 0 Then
            str.Append(" (").Append(PintarTamano(fic.TamanoBytes)).Append(")")
        End If
        str.Append(vbNewLine)
        If fic.LinkVisible Then
            str.Append(fic.URL)
        Else
            str.Append(Fichero.HIDDEN_LINK_DESC)
        End If
        Return str.ToString
    End Function

    Private Sub PropiedadesToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles PropiedadesToolStripMenuItem.Click
        If TypeOf (ListaDescargas.SelectedObject) Is IDescarga Then
            Dim v As New PropiedadesDescarga
            v.Descarga = CType(ListaDescargas.SelectedObject, IDescarga)
            v.ShowDialog()

            v.Dispose()
        End If
    End Sub

    Private Sub PausarStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles PausarStripMenuItem.Click
        For Each o As Object In ListaDescargas.SelectedObjects
            If TypeOf (o) Is Fichero AndAlso CType(o, Fichero).DescargaEstado = Estado.Descargando Then
                PonerFicheroEnPausa(CType(o, Fichero))
            End If
        Next
    End Sub

    Private Sub ForceDownloadStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles ForceDownloadStripMenuItem.Click
        For Each o As Object In ListaDescargas.SelectedObjects
            If TypeOf (o) Is Fichero _
                AndAlso (CType(o, Fichero).DescargaEstado = Estado.Pausado _
                         Or CType(o, Fichero).DescargaEstado = Estado.EnCola) Then
                ForzarDescarga(CType(o, Fichero))
            End If
        Next
    End Sub

#End Region

#Region "Agregar enlaces"

    Public Sub ComprobarYAgregarLinks(ByVal Texto As String, ExtraerURLs As Boolean, EsconderLinks As Boolean)

        Dim frm As Form = IsFormAlreadyOpen(GetType(AddLinks))
        ' Si ya está abierta la pantalla de agregar link no la volvemos a abrir
        If (frm Is Nothing) Then

            ' Si tenemos abierta la pantalla de "ver links", seguramente copiemos los links así que no queremos que salte
            Dim formsDiscarded As New Generic.List(Of Type)
            formsDiscarded.Add(GetType(PantallaMsg))
            formsDiscarded.Add(GetType(ELCForm))
            formsDiscarded.Add(GetType(EncodeLinksForm))
            For Each t As Type In formsDiscarded
                frm = IsFormAlreadyOpen(t)
                If (frm IsNot Nothing) Then
                    Exit Sub
                End If
            Next

            Dim ConfigsELC As Generic.List(Of String) = URLExtractor.ExtraerConfiguracionELC(Texto)
            If ConfigsELC IsNot Nothing AndAlso ConfigsELC.Count > 0 Then
                Dim Helper As New ELCAccountHelper(Me.Config)
                If Helper.ImportConfig(ConfigsELC, Me) Then
                    Helper.SaveToConfig(Me.Config)
                End If
                Helper.Dispose()
            End If


            Dim URLs As Generic.List(Of String) = URLExtractor.ExtraerURLs(Texto)
            If URLs IsNot Nothing AndAlso URLs.Count > 0 Then

                ' Damos foco a la ventana
                Me.Activate()

                AgregarLink(Texto, String.Empty, ExtraerURLs, EsconderLinks)
            End If

        ElseIf frm.GetType.FullName = GetType(AddLinks).FullName Then
            ' Añadimos links a la ventana ya abierta
            Dim frmName As AddLinks = CType(frm, AddLinks)
            If Not frmName.ContainsFocus Then ' Evitamos que si estamos en la ventana y hacemos un cortar, vuelva a pegarse los links

                If frmName.AgregarEnlaces(Texto, False, ExtraerURLs, EsconderLinks) Then
                    frmName.PonerFoco()
                End If

            End If
        End If
    End Sub

    Private Sub AgregarLink()
        AgregarLink(String.Empty, String.Empty, True, False)
    End Sub

    Private Sub AgregarLink(Url As String, ByVal NombrePaquete As String, ByVal ExtraerURLs As Boolean, EsconderLinks As Boolean)
        Dim frmName As New AddLinks
        frmName.Main = Me

        frmName.Config = Me.Config
        If Not String.IsNullOrEmpty(Url) Then
            frmName.AgregarEnlaces(Url, True, ExtraerURLs, EsconderLinks)
        End If
        If Not String.IsNullOrEmpty(NombrePaquete) Then
            frmName.txtNombre.Text = NombrePaquete
        End If

        frmName.ShowDialog(Me)

        Dim openStegano As Boolean =   frmName.OpenSteganoLoadOnExit
        ' Hasta que no se cierre la ventana no continuamos la ejecución
        frmName.Dispose()


        ' Ñapa para abrir el cuadro de carga esteganografica... necesario porque AddLinks contiene un botón a ese cuadro :/
        If openStegano Then OpenSteganoWizard()
    End Sub


    ' Append params
    Public Sub ProcessArgs(args As String())
        If args Is Nothing OrElse args.Length = 0 Then Exit Sub

        Dim assemblyName As String = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name
        If args(0).ToUpper.Contains(assemblyName.ToUpper) Then
            ' First argument is app, ignore
            args = args.ToList.Skip(1).ToArray
        End If


        Dim URLlist As New Generic.HashSet(Of String)
        Dim DLCList As New Generic.HashSet(Of String)
        For Each arg As String In args

            Dim ConfigsELC As Generic.List(Of String) = URLExtractor.ExtraerConfiguracionELC(arg)
            If ConfigsELC IsNot Nothing AndAlso ConfigsELC.Count > 0 Then
                Dim Helper As New ELCAccountHelper(Me.Config)
                If Helper.ImportConfig(ConfigsELC, Me) Then
                    Helper.SaveToConfig(Me.Config)
                End If
                Helper.Dispose()
            End If

            For Each url As String In URLExtractor.ExtraerURLs(arg)
                URLlist.Add(url)
            Next
            If arg.ToUpper.EndsWith(".DLC") AndAlso IO.File.Exists(arg) Then
                DLCList.Add(arg)
            End If
        Next
        If URLlist.Count > 0 Then
            ComprobarYAgregarLinks(String.Join(vbNewLine, URLlist.ToArray), True, False)
        End If
        If DLCList.Count > 0 Then
            AddDLC(DLCList(0))
        End If

    End Sub



#End Region

#Region "Agregar DLCs"

    Private DLCProcessing As Boolean = False ' Indica si ya hay un proceso tratando el DLC
    Private DLCPath As String = String.Empty
    Private DLCResults As Generic.List(Of String) = Nothing
    Private DLCErrorProcessing As Exception = Nothing


    Private Sub AddDLC(ByVal DLCFilePath As String)
        If DLCProcessing Then Exit Sub

        DLCProcessing = True
        Dim Thread As New System.Threading.Thread(AddressOf StartProcessDLC)
        DLCPath = DLCFilePath
        Thread.Start()
    End Sub

    Private Sub FinishProcessing()

        Dim ErrorProcessing As Exception = DLCErrorProcessing
        Dim LocalURLList As Generic.List(Of String) = DLCResults
        DLCResults = Nothing
        DLCErrorProcessing = Nothing

        If ErrorProcessing IsNot Nothing Then
            MessageBox.Show(Language.GetText("The DLC could not be loaded. Reason: %REASON").Replace("%REASON", ErrorProcessing.Message), _
                                  Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If

        If LocalURLList IsNot Nothing Then
            Dim URLstr As String = ""
            For Each url As String In LocalURLList
                If URLstr.Length > 0 Then
                    URLstr &= vbNewLine
                End If
                URLstr &= url
            Next
            If Not String.IsNullOrEmpty(URLstr) Then
                AgregarLink(URLstr, String.Empty, True, False)
            ElseIf ErrorProcessing IsNot Nothing Then ' Si hay excepcion, ya hemos enseñado un error antes
                MessageBox.Show(Language.GetText("The DLC has no valid Mega links"), Language.GetText("Note"), _
                       MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End If
    End Sub
    Private Sub StartProcessDLC()
        Dim Thread As New System.Threading.Thread(AddressOf ProcessDLC)
        Thread.Start()
        Dim d As Action(Of Boolean)

        If Not Thread.Join(New TimeSpan(0, 0, 0, 30)) Then ' 30 seconds timeout
            DLCProcessing = False
            Thread.Abort()
            DLCResults = Nothing

            d = Sub(x As Boolean)
                    MessageBox.Show(Language.GetText("The DLC could not be loaded. Reason: %REASON").Replace("%REASON", "30s timeout"), _
                                    Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Sub
            Me.Invoke(d, True)
        End If
        DLCPath = String.Empty
        d = Sub(x As Boolean)
                FinishProcessing()
            End Sub
        Me.Invoke(d, True)
    End Sub
    Private Sub ProcessDLC()
        Try
            Dim Path As String = DLCPath

            If String.IsNullOrEmpty(Path) Then
                Throw New ApplicationException(Language.GetText("The path is not valid"))
            End If

            If Path.ToLower.EndsWith(".elc") Then
                Dim ELC As String = DLCHelper.ReadELC_File(Path)
                DLCResults = New Generic.List(Of String)
                DLCResults.Add("mega://" & URLExtractor.SERVERENCODEDPREFIX & ELC)
            Else
                DLCResults = DLCHelper.DecryptDLC_File(Path)
            End If

        Catch ex As Exception
            Log.WriteError("Error processing DLC: " & ex.ToString)
            DLCErrorProcessing = ex
        Finally
            If DLCProcessing Then DLCProcessing = False
        End Try
    End Sub

#End Region


End Class

