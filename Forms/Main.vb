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

    ''' <summary>
    ''' 进度条渲染器(主题切换时需同步颜色)。自定义绘制解决 OLV BarRenderer 三个固有问题:
    ''' 背景底色整条灰、渐变分支忽略 FillColor、小进度整型截断无填充。
    ''' </summary>
    Private progressBarRenderer As ThemeBarRenderer

    ''' <summary>RC:主题感知进度条。背景透明露出行底色,边框随主题,小进度保底 2px。</summary>
    Private Class ThemeBarRenderer
        Inherits BrightIdeasSoftware.BarRenderer

        Public Overrides Sub Render(g As Drawing.Graphics, r As Drawing.Rectangle)
            Try
                Dim minV As Double = Me.MinimumValue
                Dim maxV As Double = Me.MaximumValue
                Dim cur As Double = 0.0
                If Me.Aspect IsNot Nothing AndAlso IsNumeric(Me.Aspect) Then
                    cur = CDbl(Me.Aspect)
                End If
                Dim frac As Double = 0.0
                If maxV > minV Then
                    frac = (cur - minV) / (maxV - minV)
                End If
                If frac < 0.0 Then frac = 0.0
                If frac > 1.0 Then frac = 1.0
                Dim wantH As Integer = If(Me.MaximumHeight > 0, Me.MaximumHeight, 18)
                Dim barH As Integer = Math.Min(r.Height - 4, wantH)
                If barH < 6 Then barH = Math.Max(2, r.Height - 4)
                Dim barY As Integer = r.Y + (r.Height - barH) \ 2
                Dim barR As New Drawing.Rectangle(r.X + 2, barY, Math.Max(0, r.Width - 4), barH)
                If barR.Width <= 0 OrElse barR.Height <= 0 Then Return
                Dim fw As Single = If(Me.FrameWidth > 0, Me.FrameWidth, 1.0F)
                Using pen As New Drawing.Pen(Me.FrameColor, fw)
                    g.DrawRectangle(pen, barR)
                End Using
                Dim inner As Drawing.Rectangle = Drawing.Rectangle.Inflate(barR, -1, -1)
                If inner.Width > 0 AndAlso inner.Height > 0 AndAlso frac > 0.0 Then
                    Dim fillW As Integer = CInt(Math.Floor(inner.Width * frac))
                    If fillW < 2 Then fillW = 2
                    If fillW > inner.Width Then fillW = inner.Width
                    Using br As New Drawing.SolidBrush(Me.FillColor)
                        g.FillRectangle(br, New Drawing.Rectangle(inner.X, inner.Y, fillW, inner.Height))
                    End Using
                End If
            Catch ex As Exception
                MyBase.Render(g, r)
            End Try
        End Sub
    End Class


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

        ' Load embedded icon resources into independent MemoryStreams so the
        ' underlying manifest streams can be closed immediately. Image.FromStream
        ' requires the stream to remain open for the lifetime of the Image.
        btnConfig.Image = LoadEmbeddedImage(thisExe, "config.png")
        btnPause.Image = LoadEmbeddedImage(thisExe, "pause.png")
        btnPlay.Image = LoadEmbeddedImage(thisExe, "play.png")
        btnStop.Image = LoadEmbeddedImage(thisExe, "stop.png")
        btnAddLink.Image = LoadEmbeddedImage(thisExe, "addlink.png")
        btnUpdate.Image = LoadEmbeddedImage(thisExe, "download.png")
        btnCollaborate.Image = LoadEmbeddedImage(thisExe, "collaborate.png")

        CrearMenus()
        InitBatchMenu()
        InitQuotaBanner()
        InitDownloadAreaLayout()

        'btnCollaborate.Visible = Not Config.HideCollaborateButton
        btnCollaborate.Visible = False ' Lo quitamos... nadie lo usa...
        btnUpdate.Visible = False
        ' RC:右上协作按钮隐藏后回收 Panel 宽度+修正齿轮 1px 裁剪(80->45,46->11)。
        Try
            PanelButtonsRight.Width = 45
            btnConfig.Location = New Drawing.Point(10, 0)
        Catch
        End Try
        ' RC:状态栏 RAM/Proc 固定 90px 运行时必截断,改自动宽度。
        Try
            RAMProcToolStripStatusLabel.AutoSize = True
        Catch
        End Try

        If Me.WindowState <> FormWindowState.Minimized Then
            Me.IconoMinimizado.Visible = False
        End If

        ListaPaquetes = Paquete.CargarDesdeFichero

        ' Usamos esta librería para pintar la lista de descargas:
        ' http://objectlistview.sourceforge.net/cs/index.html
        DefinirColumnas()
        ListaDescargas.SetObjects(Me.ListaPaquetes)

        ' 左栏总览/快捷入口:必须在 DefinirColumnas→InitNavList 建好 5 个过滤器项之后再建,
        ' 否则 navListBox.Items.Count 还是 0,拿不到正确高度,总览会压在列表上。
        InitNavRail()
        UpdateNavRailOverview()

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

        ' 应用主题(在所有控件创建后、用户可见时应用)
        Try
            ApplyCurrentTheme()
        Catch ex As Exception
            Log.WriteError("Failed to apply theme: " & ex.ToString)
        End Try

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

        ' P0-1 UI:一次性列默认集(开 Progreso%、藏 Descargado、Estado 加宽)。只跑一次,不覆盖用户之后的手动调整。
        If Not Config.ColumnUIDefaultsMigratedV26 Then
            ApplyColumnDefaults()
            Config.ColumnUIDefaultsMigratedV26 = True
            Config.ConfigUI.EstadoLista = ListaDescargas.SaveState
            Config.GuardarXML(False)
        ElseIf RepairColumnStateIfCorrupted() Then
            ' 坏列状态自愈:持久化曾被拖成 0/上千像素(# 0 + 文件名 0 + 横向滚动条),
            ' 迁移已跑过不会重跑,此处按宽度 sanity 修一次并落盘,用户重启即恢复。
            Config.ConfigUI.EstadoLista = ListaDescargas.SaveState
            Config.GuardarXML(False)
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
        Me.ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList")
        Me.detailGroup.Text = Language.GetText("Detail_Title")
        ApplyNavListColors()
        UpdateNavRailTexts()
        UpdateNavCounts()
        UpdateDetailPanel()
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
        ' P0-2 UI:工具栏文字化,短键缺失时回退显示英文原文(Language.GetText 三级回退)
        Me.btnPlay.Text = Language.GetText("Start")
        Me.btnPause.Text = Language.GetText("Pause")
        Me.btnStop.Text = Language.GetText("Stop")
        Me.btnAddLink.Text = Language.GetText("Add")
        Me.btnUpdate.Text = Language.GetText("Update")
        Me.StatusToolStripStatusLabel.Text = Language.GetText("Status: -")
        Me.RAMProcToolStripStatusLabel.Text = Language.GetText("RAM Proc Empty")
        UpdateBatchMenuTexts()
        UpdateQuotaBannerTexts()
        ApplyQuotaBannerTheme()
    End Sub

    ''' <summary>
    ''' 从嵌入式资源加载图像,复制到独立 MemoryStream 后立即释放清单流。
    ''' Image.FromStream 要求 stream 在 Image 生命周期内保持打开,
    ''' 此方法返回的 MemoryStream 由 Image 持有,窗体关闭时由 Button.Dispose 间接释放。
    ''' </summary>
    Private Shared Function LoadEmbeddedImage(asm As System.Reflection.Assembly, resourceName As String) As Image
        Dim resName As String = asm.GetName.Name & "." & resourceName
        Using stream As System.IO.Stream = asm.GetManifestResourceStream(resName)
            If stream Is Nothing Then Return Nothing
            Dim buffer(CInt(stream.Length - 1)) As Byte
            stream.Read(buffer, 0, buffer.Length)
            Dim ms As New System.IO.MemoryStream(buffer)
            Return Image.FromStream(ms)
        End Using
    End Function

    Private Sub CrearMenus()

        Me.Menu = New MainMenu
        Dim Archivo As MenuItem = Menu.MenuItems.Add(Language.GetText("&File"))

        Dim OpenELC As MenuItem = New MenuItem(Language.GetText("Open &ELC"))
        AddHandler (OpenELC.Click), AddressOf OpenDLC_Click

        Dim Salir As MenuItem = New MenuItem(Language.GetText("E&xit"))
        AddHandler (Salir.Click), AddressOf CerrarToolStripMenuItem_Click

        Archivo.MenuItems.Add(OpenELC)
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

        Dim About As New MenuItem(Language.GetText("&About"))
        AddHandler (About.Click), AddressOf About_Click

        Dim CheckUpdates As New MenuItem(Language.GetText("Chec&k for updates"))
        AddHandler (CheckUpdates.Click), AddressOf CheckUpdates_Click

        Dim Ayuda As MenuItem = Menu.MenuItems.Add(Language.GetText("&Help"))
        Ayuda.MenuItems.Add(CheckUpdates)
        Ayuda.MenuItems.Add("-")
        Ayuda.MenuItems.Add(FAQ)
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

        ' 1) Stop accepting new work via Web/Streaming
        Log.WriteInfo("Stopping web server")
        ServidorWebController.StopWebServer()

        ' 2) Cancel background workers and extraction
        bgwActualizadorListaDescargas.CancelAsync()
        bgwComprobarMaxConexiones.CancelAsync()
        bgwActualizadorDatosDisco.CancelAsync()
        bgwDescompresor.CancelAsync()
        Try
            DescompresorController.GetController.RequestCancel()
        Catch ex As Exception
            Log.WriteError("Shutdown: DescompresorController.RequestCancel failed: " & ex.ToString)
        End Try

        ' 3) Stop downloads and wait
        Log.WriteInfo("Cancelling downloads")
        PararDescargaFicheros()
        EsperarParadaDescargasYWorkers()

        ' 4) Atomic-ish persistence before releasing UI lists
        Log.WriteInfo("Saving download list")
        GuardarFicheroDescargas()

        If Me.WindowState <> FormWindowState.Minimized Then
            Me.Config.ConfigUI.AnchoVentanaPrincipal = Me.Width
            Me.Config.ConfigUI.AltoVentanaPrincipal = Me.Height
        End If
        Me.Config.ConfigUI.EstadoLista = Me.ListaDescargas.SaveState
        Me.Config.GuardarXML(False)

        ' 5) Release list/UI resources
        ListaDescargas.ClearObjects()
        ListaDescargas.Dispose()

        ' Esto es solo para mostrar la imagen y que no parpadee xD
        If Now.Subtract(Crono).TotalMilliseconds < 500 Then
            System.Threading.Thread.Sleep(500)
        End If

        Log.WriteError("Closing MegaDownloader, bye bye!" & vbNewLine)

        Log.Flush(True)

        ' Liberamos resto de recursos
        IconoMinimizado.Dispose()
        ' 先 Uninstall 取消剪贴板监听,再 DestroyHandle 释放窗口句柄。
        ' 原顺序反了会导致监听器在句柄已销毁后仍触发一次回调引发异常。
        clipChange.Uninstall()
        clipChange.DestroyHandle()

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
        Examinar.Filter = Language.GetText("ELC") & " (*.elc)|*.elc|" & Language.GetText("DLC") & " (discontinued) (*.dlc)|*.dlc"
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
        LayoutQuotaBanner()
        LayoutDownloadArea()
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
                                                  Dim errBase As String = Language.GetText("Error capital leters")
                                                  Dim errReason As String = ShortErrorReason(ele)
                                                  If String.IsNullOrEmpty(errReason) Then Return errBase
                                                  Return errBase & ": " & errReason
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
                                         ' AspectGetter 在列表每次重绘时高频执行:只记日志并返回安全占位值。
                                         ' 此处弹窗/重新抛出会让每一行重绘都弹一次错误框并最终闪退。
                                         Log.WriteError("Error displaying download status: " & ex.ToString)
                                         Return "---"
                                     End Try
                                 End Function

        Dim ColPorcentaje As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaPorcentaje))
        ColPorcentaje.AspectGetter = Function(ele As IDescarga)
                                         Try
                                             Return ele.DescargaPorcentaje
                                         Catch ex As Exception
                                             Log.WriteError("Error displaying download %: " & ex.ToString)
                                             Return 0
                                         End Try
                                     End Function
        progressBarRenderer = New ThemeBarRenderer
        progressBarRenderer.UseStandardBar = False
        progressBarRenderer.MaximumWidth = 9999
        progressBarRenderer.MinimumValue = 0
        progressBarRenderer.MaximumValue = 100
        ApplyProgressBarThemeColors()
        ListaDescargas.AllColumns(IndiceColumnaPorcentaje).Renderer = progressBarRenderer

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
                                      Return "---"
                                  End Try
                              End Function
        ListaDescargas.AllColumns(IndiceColumnaEDT).TextAlign = HorizontalAlignment.Right

        Dim ColPorcentajeTexto As New BrightIdeasSoftware.TypedColumn(Of IDescarga)(ListaDescargas.AllColumns(IndiceColumnaPorcentajeTexto))
        ColPorcentajeTexto.AspectGetter = Function(ele As IDescarga)
                                              Try
                                                  Return ele.DescargaPorcentaje.ToString("F2") & "%"
                                              Catch ex As Exception
                                                  Log.WriteError("Error displaying download % (text): " & ex.ToString)
                                                  Return ""
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
                                            ' P2-11b:非 All 分组下只展开命中文件(包穿透规则与计数共用同一谓词)。
                                            ' 注意:不可用 OLV UseFiltering(启动期静默退出),此处包裹是唯一的过滤点。
                                            Try
                                                If Not TypeOf ele Is Paquete Then
                                                    Return Nothing
                                                End If
                                                Dim files As Generic.List(Of Fichero) = CType(ele, Paquete).ListaFicheros
                                                If files Is Nothing Then Return Nothing
                                                If _navScope = DownloadEstadoFilter.NavScope.All Then Return files
                                                Dim shown As New Generic.List(Of Fichero)()
                                                For Each f As Fichero In files
                                                    If DownloadEstadoFilter.MatchesScope(f, _navScope) Then shown.Add(f)
                                                Next
                                                Return shown
                                            Catch ex As Exception
                                                Log.WriteError("FilteredChildrenGetter failed: " & ex.ToString)
                                                Dim p0 As Paquete = TryCast(ele, Paquete)
                                                If p0 Is Nothing Then Return Nothing
                                                Return p0.ListaFicheros
                                            End Try
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


        ' 列宽上下限:WinForms 表头分栏线无最大宽度,OLV Minimum/MaximumWidth 只在代码赋值时生效,
        ' 拖动时靠 ColumnWidthChanging 事件手动夹取(见下方 handler)。
        ApplyColumnWidthLimits()

        ' P0-7 UI:行高 26px 留白;空列表引导(尺寸不受换肤影响,只设一次)
        ListaDescargas.RowHeight = 26
        ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList")
        ' P2-11b:导航初始化(计数+分组;过滤走 Roots 子集,禁用 OLV UseFiltering/ModelFilter)
        InitNavList()
    End Sub
    Private Sub ListaDescargas_FormatRow(sender As Object, e As BrightIdeasSoftware.FormatRowEventArgs) Handles ListaDescargas.FormatRow
        If e.DisplayIndex Mod 2 = 0 Then
            e.Item.BackColor = ThemeManager.GetColor("Back")
        Else
            e.Item.BackColor = ThemeManager.GetColor("AltBack")
        End If
        Dim desc As IDescarga = CType(e.Model, IDescarga)
        If desc.DescargaEstado = Estado.Erroneo Then
            e.Item.ForeColor = ThemeManager.GetColor("ErrorFore")
        ElseIf desc.DescargaEstado = Estado.Completado Then
            e.Item.ForeColor = ThemeManager.GetColor("SuccessFore")
        Else
            e.Item.ForeColor = ThemeManager.GetColor("Fore")
        End If
    End Sub

    ''' <summary>
    ''' 应用当前配置主题到主窗体,并同步列表进度条等非递归控件颜色。
    ''' </summary>
    Public Sub ApplyCurrentTheme()
        If Config Is Nothing OrElse Config.ConfigUI Is Nothing Then
            ThemeManager.ApplyTheme(Me)
        Else
            ThemeManager.ApplyTheme(Me, Config.ConfigUI.Tema)
        End If
        ApplyProgressBarThemeColors()
        ApplyQuotaBannerTheme()
        ApplyToolbarIconTheme()
        ApplyNavListColors()
        ApplyNavRailTheme()
        If ListaDescargas IsNot Nothing Then
            ListaDescargas.Invalidate()
        End If
    End Sub

    Private Sub ApplyProgressBarThemeColors()
        If progressBarRenderer Is Nothing Then Return
        ' RC:背景透明露出行底色(解决浅色整条发灰,0%行纯灰);边框随主题不再纯黑;
        ' 清空渐变走 FillColor 实色(解决 FillColor 死赋值);条高跟随行高。
        progressBarRenderer.BackgroundColor = Drawing.Color.Transparent
        progressBarRenderer.FillColor = ThemeManager.GetColor("ProgressFill")
        progressBarRenderer.GradientStartColor = Drawing.Color.Empty
        progressBarRenderer.GradientEndColor = Drawing.Color.Empty
        progressBarRenderer.FrameColor = ThemeManager.GetColor("Border")
        progressBarRenderer.FrameWidth = 1.0F
        progressBarRenderer.MaximumHeight = 18
    End Sub

    ''' <summary>
    ''' P0-5 UI:工具栏图标跟随主题。原图(40x40)首次调用时存入 Button.Tag,显示图统一缩到
    ''' 20px(88x34 文字钮放不下 40px 原图),深色再按 ColorMatrix 提亮;显示图每次重建,
    ''' 替换后 Dispose 上一张,原图与窗体同寿命,切换过程无句柄增长。
    ''' </summary>
    Private Sub ApplyToolbarIconTheme()
        Try
            Dim dark As Boolean = (ThemeManager.Current = ThemeManager.ResolvedTheme.Dark)
            ApplyIconThemeToButton(btnPlay, dark)
            ApplyIconThemeToButton(btnPause, dark)
            ApplyIconThemeToButton(btnStop, dark)
            ApplyIconThemeToButton(btnAddLink, dark)
            ApplyIconThemeToButton(btnUpdate, dark)
            ApplyIconThemeToButton(btnConfig, dark)
            ApplyIconThemeToButton(btnCollaborate, dark)
        Catch ex As Exception
            Log.WriteError("ApplyToolbarIconTheme failed: " & ex.ToString)
        End Try
    End Sub

    Private Shared Function GetToolbarIconSize() As Integer
        ' RC:高 DPI 下图标跟随放大(此前写死 20,150% 下显小)。
        Try
            Using g As Graphics = Graphics.FromHwnd(IntPtr.Zero)
                Dim s As Single = g.DpiX / 96.0F
                If s < 1.0F Then s = 1.0F
                If s > 2.5F Then s = 2.5F
                Return CInt(20 * s)
            End Using
        Catch
            Return 20
        End Try
    End Function

    Private Shared Sub ApplyIconThemeToButton(btn As Button, dark As Boolean)
        If btn Is Nothing OrElse btn.IsDisposed Then Return
        Dim original As Image = TryCast(btn.Tag, Image)
        If original Is Nothing Then
            If btn.Image Is Nothing Then Return
            original = btn.Image
            btn.Tag = original
        End If
        Dim previous As Image = btn.Image
        Dim scaled As Image = ScaleToolbarIcon(original, GetToolbarIconSize())
        Try
            If dark Then
                Dim tinted As Image = RecolorImageForDark(scaled)
                scaled.Dispose()
                scaled = tinted
            End If
            btn.Image = scaled
            scaled = Nothing
        Finally
            If scaled IsNot Nothing Then scaled.Dispose()
        End Try
        If previous IsNot Nothing AndAlso Not Object.ReferenceEquals(previous, original) Then
            previous.Dispose()
        End If
    End Sub

    Private Shared Function ScaleToolbarIcon(original As Image, size As Integer) As Image
        Dim bmp As New Bitmap(size, size)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
            g.DrawImage(original, 0, 0, size, size)
        End Using
        Return bmp
    End Function

    Private Shared Function RecolorImageForDark(original As Image) As Image
        Dim bmp As New Bitmap(original.Width, original.Height)
        Using g As Graphics = Graphics.FromImage(bmp)
            Dim m As New System.Drawing.Imaging.ColorMatrix(New Single()() {
                New Single() {1.35F, 0.0F, 0.0F, 0.0F, 0.0F},
                New Single() {0.0F, 1.35F, 0.0F, 0.0F, 0.0F},
                New Single() {0.0F, 0.0F, 1.35F, 0.0F, 0.0F},
                New Single() {0.0F, 0.0F, 0.0F, 1.0F, 0.0F},
                New Single() {0.08F, 0.08F, 0.08F, 0.0F, 1.0F}})
            Using attrs As New System.Drawing.Imaging.ImageAttributes()
                attrs.SetColorMatrix(m)
                g.DrawImage(original, New Rectangle(0, 0, bmp.Width, bmp.Height),
                            0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attrs)
            End Using
        End Using
        Return bmp
    End Function

#Region "v2.5 beta: 配额横幅 + 批量失败操作"

    Private quotaBannerPanel As Panel = Nothing
    Private quotaBannerLabel As Label = Nothing
    Private quotaRetryNowButton As Button = Nothing
    Private quotaNotified As Boolean = False
    Private WithEvents RetryAllFailedMenuItem As New ToolStripMenuItem()
    Private WithEvents RemoveAllFailedMenuItem As New ToolStripMenuItem()
    Private WithEvents ResetColumnsMenuItem As New ToolStripMenuItem()
    Private ReadOnly sepBatchMenuItem As New ToolStripSeparator()
    Private ReadOnly sepColumnsMenuItem As New ToolStripSeparator()

#End Region

#Region "v2.5: 左侧栏任务总览 + 快捷入口"

    ' 左栏纵向扩展后的分段控件。navListBox 仍是过滤器本体(0..4),下方依次是
    ' 任务总览(只读)与快捷入口(复用既有菜单命令)。
    Private navOverviewHeader As Label = Nothing
    Private navOverviewSpeedValue As Label = Nothing
    Private navOverviewCountsValue As Label = Nothing
    Private navOverviewProgressBar As ProgressBar = Nothing
    Private navOverviewProgressText As Label = Nothing
    Private navOverviewRemainingValue As Label = Nothing
    Private navQuickHeader As Label = Nothing
    Private WithEvents navQuickExtractButton As Button = Nothing
    Private WithEvents navQuickLibraryButton As Button = Nothing
    Private WithEvents navQuickLogsButton As Button = Nothing
    Private navRailInitialized As Boolean = False

    ' 左栏宽度:130 太窄放不下"总速度 12.4 MB/s"这类文案,加宽到 168。
    ' 中部列表 FillsFreeSpace 列会自动吸收差额,1024 宽下仍有约 640px。
    ' 注意:做成字段而非常量——过滤器文本超宽(大计数/长译文)时 FitNavListBoxNoScroll
    ' 会按实测加宽到 220,免横滚;Const 写死就加不动了。
    Private NavRailWidth As Integer = 168
    ' 过滤器文本超宽时面板最多加到该宽度,再宽就吃掉中部列表,改出横滚兜底。
    Private Const NavRailMaxWidth As Integer = 220

#End Region

#Region "v2.5: 左栏任务总览 + 快捷入口 —— 实现"

    ''' <summary>
    ''' 左栏下半部分此前 70% 留白(5 项过滤器约 130px,面板 429px)。此处补两段:
    ''' B 任务总览——速度/计数/队列进度/剩余时间,数据全部复用既有字段与 430ms 刷新循环,
    ''' 不新增计时器;C 快捷入口——把埋在二级菜单里的解压队列/流媒体库/日志提到首屏。
    ''' 所有控件挂到 navPanel 内并置于 navListBox 之下,由 LayoutNavRail 统一定位,
    ''' 主题走 ThemeManager 递归(navListBox 除外,仍需 ApplyNavListColors 手动同步)。
    ''' </summary>
    Private Sub InitNavRail()
        Try
            If navPanel Is Nothing Then Return

            Dim sc As Single = GetUiScale()
            Dim pad As Integer = CInt(6 * sc)
            Dim innerW As Integer = NavRailWidth - pad * 2
            Dim rowH As Integer = CInt(20 * sc)

            ' ---- B 段:任务总览 ----
            navOverviewHeader = CreateNavSectionHeader(Language.GetText("Overview_Title"), sc)

            Dim speedLabel As New Label()
            speedLabel.Name = "navOverviewSpeedLabel"
            speedLabel.AutoSize = False
            speedLabel.TextAlign = Drawing.ContentAlignment.MiddleLeft
            speedLabel.Text = Language.GetText("Overview_Speed")
            speedLabel.Height = rowH

            navOverviewSpeedValue = New Label()
            navOverviewSpeedValue.Name = "navOverviewSpeedValue"
            navOverviewSpeedValue.AutoSize = False
            navOverviewSpeedValue.TextAlign = Drawing.ContentAlignment.MiddleRight
            navOverviewSpeedValue.Height = rowH
            navOverviewSpeedValue.Text = "-"

            navOverviewCountsValue = New Label()
            navOverviewCountsValue.Name = "navOverviewCountsValue"
            navOverviewCountsValue.AutoSize = False
            navOverviewCountsValue.TextAlign = Drawing.ContentAlignment.MiddleLeft
            navOverviewCountsValue.Height = CInt(34 * sc)
            navOverviewCountsValue.Text = "-"

            Dim progressCaption As New Label()
            progressCaption.Name = "navOverviewProgressCaption"
            progressCaption.AutoSize = False
            progressCaption.TextAlign = Drawing.ContentAlignment.MiddleLeft
            progressCaption.Text = Language.GetText("Overview_Progress")
            progressCaption.Height = rowH

            navOverviewProgressText = New Label()
            navOverviewProgressText.Name = "navOverviewProgressText"
            navOverviewProgressText.AutoSize = False
            navOverviewProgressText.TextAlign = Drawing.ContentAlignment.MiddleRight
            navOverviewProgressText.Height = rowH
            navOverviewProgressText.Text = "0%"

            navOverviewProgressBar = New ProgressBar()
            navOverviewProgressBar.Name = "navOverviewProgressBar"
            navOverviewProgressBar.Minimum = 0
            navOverviewProgressBar.Maximum = 100
            navOverviewProgressBar.Value = 0
            navOverviewProgressBar.Height = CInt(10 * sc)

            Dim remainingCaption As New Label()
            remainingCaption.Name = "navOverviewRemainingCaption"
            remainingCaption.AutoSize = False
            remainingCaption.TextAlign = Drawing.ContentAlignment.MiddleLeft
            remainingCaption.Text = Language.GetText("Overview_Remaining")
            remainingCaption.Height = rowH

            navOverviewRemainingValue = New Label()
            navOverviewRemainingValue.Name = "navOverviewRemainingValue"
            navOverviewRemainingValue.AutoSize = False
            navOverviewRemainingValue.TextAlign = Drawing.ContentAlignment.MiddleRight
            navOverviewRemainingValue.Height = rowH
            navOverviewRemainingValue.Text = "-"

            ' 控件登记到 navPanel,Text 属性留待 UpdateNavRailTexts 统一本地化(切换语言时重建)。
            navPanel.Controls.Add(navOverviewHeader)
            navPanel.Controls.Add(speedLabel)
            navPanel.Controls.Add(navOverviewSpeedValue)
            navPanel.Controls.Add(navOverviewCountsValue)
            navPanel.Controls.Add(progressCaption)
            navPanel.Controls.Add(navOverviewProgressText)
            navPanel.Controls.Add(navOverviewProgressBar)
            navPanel.Controls.Add(remainingCaption)
            navPanel.Controls.Add(navOverviewRemainingValue)

            ' 说明性 Label 用 Tag 登记,供 FindNavLabel 在换语言/换肤时按语义定位(它们不是交互控件)。
            speedLabel.Tag = "overviewSpeedLabel"
            progressCaption.Tag = "overviewProgressCaption"
            remainingCaption.Tag = "overviewRemainingCaption"

            ' ---- C 段:快捷入口 ----
            navQuickHeader = CreateNavSectionHeader(Language.GetText("Quick_Title"), sc)

            navQuickExtractButton = CreateNavQuickButton("navQuickExtractButton",
                Language.GetText("Quick_ExtractQueue"), innerW, sc)
            navQuickLibraryButton = CreateNavQuickButton("navQuickLibraryButton",
                Language.GetText("Quick_StreamingLibrary"), innerW, sc)
            navQuickLogsButton = CreateNavQuickButton("navQuickLogsButton",
                Language.GetText("Quick_Logs"), innerW, sc)

            navPanel.Controls.Add(navQuickHeader)
            navPanel.Controls.Add(navQuickExtractButton)
            navPanel.Controls.Add(navQuickLibraryButton)
            navPanel.Controls.Add(navQuickLogsButton)

            ' 复用的既有命令,通过 ToolTip 说明书与菜单入口一致
            ToolTipBotones.SetToolTip(navQuickExtractButton, Language.GetText("See extraction queue"))
            ToolTipBotones.SetToolTip(navQuickLibraryButton, Language.GetText("Manage Streaming Library"))
            ToolTipBotones.SetToolTip(navQuickLogsButton, Language.GetText("See logs"))

            ' 侧栏整体接受链接文本拖放(含递归到的 Label/按钮等子控件),转发到 Main_DragDrop。
            EnableLinkDrop(navPanel)
            If detailGroup IsNot Nothing AndAlso Not detailGroup.IsDisposed Then EnableLinkDrop(detailGroup)

            navRailInitialized = True
            LayoutNavRail()
            ApplyNavRailTheme()
            UpdateNavRailTexts()
        Catch ex As Exception
            Log.WriteError("InitNavRail failed: " & ex.ToString)
        End Try
    End Sub

    Private Function CreateNavSectionHeader(text As String, sc As Single) As Label
        Dim lbl As New Label()
        lbl.Name = "navSectionHeader"
        lbl.AutoSize = False
        lbl.TextAlign = Drawing.ContentAlignment.MiddleLeft
        lbl.Height = CInt(20 * sc)
        lbl.Text = text
        ' 分节标题用加粗主色(与说明性灰字区分)。Font 只在此处建一次,
        ' 换肤只改颜色——避免每次 ApplyTheme 都 New Font 造成 GDI 对象堆积。
        lbl.Font = New Drawing.Font(lbl.Font, Drawing.FontStyle.Bold)
        Return lbl
    End Function

    Private Function CreateNavQuickButton(name As String, text As String, width As Integer, sc As Single) As Button
        Dim btn As New Button()
        btn.Name = name
        btn.Text = text
        btn.TextAlign = Drawing.ContentAlignment.MiddleLeft
        btn.Height = CInt(26 * sc)
        btn.Width = width
        btn.UseVisualStyleBackColor = False
        btn.FlatStyle = FlatStyle.Flat
        Return btn
    End Function

    ''' <summary>左栏两段的所有几何一次算死,不依赖 Anchor 累加,避免 resize/DPI 变化漂移。
    ''' 总览与快捷入口占据列表区下方,过滤器本体高度保持不变。</summary>
    Private Sub LayoutNavRail()
        Try
            If Not navRailInitialized OrElse navPanel Is Nothing Then Return
            Dim sc As Single = GetUiScale()
            Dim pad As Integer = CInt(6 * sc)
            Dim innerW As Integer = NavRailWidth - pad * 2
            Dim rowH As Integer = CInt(20 * sc)

            ' 过滤器本体高度 = 5 项 × 真实行高(见 GetNavActualItemHeight);它不再 Dock=Fill,必须显式给定,
            ' 否则默认 130 在字体/DPI 变化时会截断第 5 项。Normal 模式 ItemHeight 无效,不可直接用它。
            If navListBox IsNot Nothing AndAlso Not navListBox.IsDisposed AndAlso navListBox.Items.Count > 0 Then
                FitNavListBoxNoScroll()
            End If

            Dim y As Integer = navListBox.Bottom + CInt(8 * sc)

            ' 纵向空间不足时(最小窗口 280 高 → 内容区仅约 209px)整段隐藏,
            ' 避免总览/快捷入口溢出到状态栏外面。阈值 = 总览+快捷入口两段所需高度。
            Dim needH As Integer = CInt(8 * sc) + CInt(20 * sc) + rowH * 3 + CInt(34 * sc) +
                                   CInt(10 * sc) + CInt(4 * sc) + CInt(10 * sc) +
                                   CInt(20 * sc) + CInt(26 * sc) * 3 + CInt(3 * sc) * 3
            Dim showRail As Boolean = (navPanel.ClientSize.Height - navListBox.Bottom) >= needH
            SetNavRailSectionVisible(showRail, False)
            If Not showRail Then Return
            SetNavRailSectionVisible(True, True)

            ' --- B 段:任务总览 ---
            navOverviewHeader.SetBounds(pad, y, innerW, CInt(20 * sc))
            y += navOverviewHeader.Height + CInt(2 * sc)

            Dim speedLbl As Label = FindNavLabel("overviewSpeedLabel")
            If speedLbl IsNot Nothing Then speedLbl.SetBounds(pad, y, CInt(66 * sc), rowH)
            navOverviewSpeedValue.SetBounds(pad + CInt(66 * sc), y, innerW - CInt(66 * sc), rowH)
            y += rowH

            navOverviewCountsValue.SetBounds(pad, y, innerW, CInt(34 * sc))
            y += navOverviewCountsValue.Height + CInt(2 * sc)

            Dim progCap As Label = FindNavLabel("overviewProgressCaption")
            If progCap IsNot Nothing Then progCap.SetBounds(pad, y, CInt(66 * sc), rowH)
            navOverviewProgressText.SetBounds(pad + CInt(66 * sc), y, innerW - CInt(66 * sc), rowH)
            y += rowH

            navOverviewProgressBar.SetBounds(pad, y, innerW, navOverviewProgressBar.Height)
            y += navOverviewProgressBar.Height + CInt(4 * sc)

            Dim remCap As Label = FindNavLabel("overviewRemainingCaption")
            If remCap IsNot Nothing Then remCap.SetBounds(pad, y, CInt(66 * sc), rowH)
            navOverviewRemainingValue.SetBounds(pad + CInt(66 * sc), y, innerW - CInt(66 * sc), rowH)
            y += rowH + CInt(10 * sc)

            ' --- C 段:快捷入口 ---
            navQuickHeader.SetBounds(pad, y, innerW, CInt(20 * sc))
            y += navQuickHeader.Height + CInt(2 * sc)

            For Each btn As Button In New Button() {navQuickExtractButton, navQuickLibraryButton, navQuickLogsButton}
                If btn IsNot Nothing Then
                    btn.SetBounds(pad, y, innerW, btn.Height)
                    y += btn.Height + CInt(3 * sc)
                End If
            Next
        Catch ex As Exception
            Log.WriteDebug("LayoutNavRail failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>按 Tag 找 navPanel 内的说明性 Label(它们不参与交互,无需字段)。</summary>
    Private Function FindNavLabel(tag As String) As Label
        If navPanel Is Nothing Then Return Nothing
        For Each c As Control In navPanel.Controls
            If TypeOf c Is Label AndAlso String.Equals(TryCast(c.Tag, String), tag) Then Return CType(c, Label)
        Next
        Return Nothing
    End Function

    ''' <summary>整体显隐左栏两段。overview 与 quick 分开控制,便于未来只保留总览。</summary>
    Private Sub SetNavRailSectionVisible(overviewVisible As Boolean, quickVisible As Boolean)
        Try
            If navOverviewHeader IsNot Nothing Then navOverviewHeader.Visible = overviewVisible
            If navOverviewSpeedValue IsNot Nothing Then navOverviewSpeedValue.Visible = overviewVisible
            If navOverviewCountsValue IsNot Nothing Then navOverviewCountsValue.Visible = overviewVisible
            If navOverviewProgressBar IsNot Nothing Then navOverviewProgressBar.Visible = overviewVisible
            If navOverviewProgressText IsNot Nothing Then navOverviewProgressText.Visible = overviewVisible
            If navOverviewRemainingValue IsNot Nothing Then navOverviewRemainingValue.Visible = overviewVisible
            For Each tag As String In New String() {"overviewSpeedLabel", "overviewProgressCaption", "overviewRemainingCaption"}
                Dim l As Label = FindNavLabel(tag)
                If l IsNot Nothing Then l.Visible = overviewVisible
            Next

            If navQuickHeader IsNot Nothing Then navQuickHeader.Visible = quickVisible
            If navQuickExtractButton IsNot Nothing Then navQuickExtractButton.Visible = quickVisible
            If navQuickLibraryButton IsNot Nothing Then navQuickLibraryButton.Visible = quickVisible
            If navQuickLogsButton IsNot Nothing Then navQuickLogsButton.Visible = quickVisible
        Catch ex As Exception
            Log.WriteDebug("SetNavRailSectionVisible failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Shared Function GetUiScale() As Single
        Dim sc As Single = 1.0F
        Try
            Using g As Drawing.Graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)
                sc = g.DpiY / 96.0F
            End Using
        Catch
        End Try
        If sc < 1.0F Then sc = 1.0F
        Return sc
    End Function

    ''' <summary>切换语言后重建左栏所有文案。计数/速度等动态值由 UpdateNavRailOverview 刷新。</summary>
    Private Sub UpdateNavRailTexts()
        Try
            If Not navRailInitialized Then Return
            navOverviewHeader.Text = Language.GetText("Overview_Title")
            navQuickHeader.Text = Language.GetText("Quick_Title")
            Dim speedLbl As Label = FindNavLabel("overviewSpeedLabel")
            If speedLbl IsNot Nothing Then speedLbl.Text = Language.GetText("Overview_Speed")
            Dim progCap As Label = FindNavLabel("overviewProgressCaption")
            If progCap IsNot Nothing Then progCap.Text = Language.GetText("Overview_Progress")
            Dim remCap As Label = FindNavLabel("overviewRemainingCaption")
            If remCap IsNot Nothing Then remCap.Text = Language.GetText("Overview_Remaining")
            If navQuickExtractButton IsNot Nothing Then navQuickExtractButton.Text = Language.GetText("Quick_ExtractQueue")
            If navQuickLibraryButton IsNot Nothing Then navQuickLibraryButton.Text = Language.GetText("Quick_StreamingLibrary")
            If navQuickLogsButton IsNot Nothing Then navQuickLogsButton.Text = Language.GetText("Quick_Logs")
        Catch ex As Exception
            Log.WriteDebug("UpdateNavRailTexts failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>
    ''' 任务总览数值刷新。在 430ms 刷新循环里调用(UI 线程),速度直接取该循环算好的
    ''' VelocidadGlobalDescarga;计数复用 UpdateNavCounts 的同一判定 MatchesScope,
    ''' 保证与过滤器后缀括号里的数字完全一致;进度与剩余时间按字节加权。
    ''' </summary>
    Private Sub UpdateNavRailOverview()
        ' P1(审查):本函数在 430ms 后台循环里被直接调用,此前直接写控件属性=
        ' 跨线程违规(与 UpdateQuotaUI/SetStatusBar 的编组惯例相悖),且无锁遍历
        ' ListaPaquetes(他处持 Mutex.ListaDescargas 增删,foreach 可抛)。
        ' 修法:先编组回 UI(同 UpdateQuotaUI 的 BeginInvoke 模式),计数快照持锁。
        Try
            If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
            If Me.InvokeRequired Then
                Try
                    Me.BeginInvoke(New Action(AddressOf UpdateNavRailOverview))
                Catch
                End Try
                Return
            End If
            If Not navRailInitialized Then Return
            If navPanel Is Nothing OrElse navPanel.IsDisposed Then Return

            ' 速度(与状态栏同源,单位换算复用 PintarVelocidadDescarga)
            Dim speedTxt As String = "-"
            If VelocidadGlobalDescarga.HasValue AndAlso VelocidadGlobalDescarga.Value > 0 Then
                speedTxt = PintarVelocidadDescarga(VelocidadGlobalDescarga.Value)
            End If
            navOverviewSpeedValue.Text = speedTxt
            Try
                TintNavSpeedValue()
            Catch
            End Try

            ' 计数:一次遍历同时累加总量/已完成量,再算整体进度。
            ' 快照持 Mutex.ListaDescargas(只包遍历,不包控件赋值,锁序与 RefreshListaDescargas 一致)。
            Dim total As Integer = 0
            Dim active As Integer = 0
            Dim queued As Integer = 0
            Dim failed As Integer = 0
            Dim totalBytes As Decimal = 0D
            Dim doneBytes As Decimal = 0D

            If ListaPaquetes IsNot Nothing Then
                Mutex.ListaDescargas.WaitOne()
                Try
                    For Each p As Paquete In ListaPaquetes
                        total += 1
                        If DownloadEstadoFilter.MatchesScope(p, DownloadEstadoFilter.NavScope.Downloading) Then active += 1
                        If DownloadEstadoFilter.MatchesScope(p, DownloadEstadoFilter.NavScope.Waiting) Then queued += 1
                        If DownloadEstadoFilter.MatchesScope(p, DownloadEstadoFilter.NavScope.Failed) Then failed += 1

                        Dim st As Long = p.DescargaTamanoBytes()
                        Dim pct As Decimal = p.DescargaPorcentaje()
                        If st > 0 Then
                            totalBytes += st
                            doneBytes += Math.Ceiling(pct * st / 100D)
                        End If
                    Next
                Finally
                    Mutex.ListaDescargas.ReleaseMutex()
                End Try
            End If

            navOverviewCountsValue.Text = Language.GetText("Overview_Active") & " " & active.ToString() &
                "   " & Language.GetText("Overview_Queued") & " " & queued.ToString() &
                "   " & Language.GetText("Overview_Failed") & " " & failed.ToString()

            Dim pctOverall As Integer = 0
            If totalBytes > 0 Then pctOverall = CInt(Math.Floor(doneBytes * 100D / totalBytes))
            If pctOverall < 0 Then pctOverall = 0
            If pctOverall > 100 Then pctOverall = 100

            ' ProgressBar.Value 越界会抛异常(高 DPI/极小文件时 pct 计算可能溢出)
            Try
                If navOverviewProgressBar.Value <> pctOverall Then navOverviewProgressBar.Value = pctOverall
            Catch
            End Try
            navOverviewProgressText.Text = pctOverall.ToString() & "%"

            ' 剩余:总剩余字节 / 当前总速度(KB/s)。速度缺失或无剩余时不显示估算,避免编造数字。
            Dim remainTxt As String = "-"
            If totalBytes > 0 AndAlso doneBytes < totalBytes AndAlso
               VelocidadGlobalDescarga.HasValue AndAlso VelocidadGlobalDescarga.Value > 0 Then
                Dim remainKB As Decimal = (totalBytes - doneBytes) / 1024D
                Dim secs As Decimal = remainKB / VelocidadGlobalDescarga.Value
                If secs > 0 AndAlso secs < 86400 * 7 Then
                    remainTxt = FormatNavRemaining(CInt(Math.Ceiling(secs)))
                End If
            End If
            navOverviewRemainingValue.Text = remainTxt
        Catch ex As Exception
            Log.WriteDebug("UpdateNavRailOverview failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>剩余时间简写:&lt;1min 读秒,否则分/时。与配额倒计时风格保持一致。</summary>
    Private Shared Function FormatNavRemaining(totalSec As Integer) As String
        If totalSec < 60 Then Return totalSec.ToString() & " s"
        Dim totalMin As Integer = totalSec \ 60
        If totalMin < 60 Then Return totalMin.ToString() & " min"
        Dim h As Integer = totalMin \ 60
        Dim m As Integer = totalMin Mod 60
        If h >= 24 Then
            Dim d As Integer = h \ 24
            Return d.ToString() & " d " & (h Mod 24).ToString() & " h"
        End If
        Return h.ToString() & " h " & m.ToString() & " min"
    End Function

    ''' <summary>左栏总览/快捷入口换肤。分节标题用主题强调色(深浅主题下分别是深蓝/浅蓝),
    ''' 说明文字用次要灰,数值用主色/语义色区分——避免之前全取 Border 一片灰、换肤也看不出变化。</summary>
    Private Sub ApplyNavRailTheme()
        Try
            If Not navRailInitialized Then Return
            Dim subtle As Drawing.Color = ThemeManager.GetColor("Border")
            Dim fore As Drawing.Color = ThemeManager.GetColor("Fore")
            Dim accent As Drawing.Color = ThemeManager.GetColor("Link")

            ' 分节标题:强调色+加粗(换肤可见),不再用灰。
            For Each hdr As Label In New Label() {navOverviewHeader, navQuickHeader}
                If hdr IsNot Nothing Then
                    hdr.ForeColor = accent
                End If
            Next

            ' 计数行:主色保证可读(之前全灰,数字看不清)。
            If navOverviewCountsValue IsNot Nothing Then navOverviewCountsValue.ForeColor = fore

            ' 左列说明(label)次要灰,右列数值主色/语义色,两列一眼区分。
            Dim speedLbl As Label = FindNavLabel("overviewSpeedLabel")
            If speedLbl IsNot Nothing Then speedLbl.ForeColor = subtle
            Dim progCap As Label = FindNavLabel("overviewProgressCaption")
            If progCap IsNot Nothing Then progCap.ForeColor = subtle
            Dim remCap As Label = FindNavLabel("overviewRemainingCaption")
            If remCap IsNot Nothing Then remCap.ForeColor = subtle

            ' 总速度:有速度时语义绿,无速度(-)时主色(由 UpdateNavRailOverview 每次刷新后重调)。
            If navOverviewSpeedValue IsNot Nothing Then navOverviewSpeedValue.ForeColor = fore
            If navOverviewProgressText IsNot Nothing Then navOverviewProgressText.ForeColor = fore
            If navOverviewRemainingValue IsNot Nothing Then navOverviewRemainingValue.ForeColor = fore
            Try
                TintNavSpeedValue()
            Catch
            End Try

            ' 快捷入口按钮:沿用主题按钮配色(与工具栏一致的 Flat + 边框)
            Dim btnBack As Drawing.Color = ThemeManager.GetColor("ControlBack")
            Dim btnBorder As Drawing.Color = ThemeManager.GetColor("Border")
            Dim btnHover As Drawing.Color = ThemeManager.GetColor("ButtonHover")
            Dim btnPressed As Drawing.Color = ThemeManager.GetColor("ButtonPressed")
            For Each btn As Button In New Button() {navQuickExtractButton, navQuickLibraryButton, navQuickLogsButton}
                If btn IsNot Nothing Then
                    btn.BackColor = btnBack
                    btn.ForeColor = fore
                    btn.FlatStyle = FlatStyle.Flat
                    btn.FlatAppearance.BorderSize = 1
                    btn.FlatAppearance.BorderColor = btnBorder
                    btn.FlatAppearance.MouseOverBackColor = btnHover
                    btn.FlatAppearance.MouseDownBackColor = btnPressed
                    btn.UseVisualStyleBackColor = False
                End If
            Next

            ' 进度条走主题 token(与列表内进度条同色系)
            ' 注意:系统 VisualStyles 下 ProgressBar 会忽略 Back/Fore,此处尽力而为。
            If navOverviewProgressBar IsNot Nothing Then
                navOverviewProgressBar.BackColor = ThemeManager.GetColor("ControlBack")
                navOverviewProgressBar.ForeColor = ThemeManager.GetColor("Selection")
            End If
        Catch ex As Exception
            Log.WriteDebug("ApplyNavRailTheme failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>总速度数值语义色:下载中绿色强调, idle 显示 - 时回主色,换肤/每次刷新都重调。</summary>
    Private Sub TintNavSpeedValue()
        If navOverviewSpeedValue Is Nothing OrElse navOverviewSpeedValue.IsDisposed Then Return
        Dim hasSpeed As Boolean = VelocidadGlobalDescarga.HasValue AndAlso VelocidadGlobalDescarga.Value > 0
        If hasSpeed Then
            navOverviewSpeedValue.ForeColor = ThemeManager.GetColor("SuccessFore")
        Else
            navOverviewSpeedValue.ForeColor = ThemeManager.GetColor("Fore")
        End If
    End Sub

#End Region

#Region "v2.5: 右侧栏结构化详情"

    ' 结构化详情:标签列 + 值列,进度单独一条细进度条。替代原先"一个 Label 灌多行文本"
    ' 的写法——长文件名/长路径无法换行对齐,标签与值也没有视觉区分。
    ' 无选中时不建/不显示这些控件,仍由 detailLabel 承担空态引导(B 段)。
    Private detailContentPanel As Panel = Nothing
    Private detailTitleLabel As Label = Nothing
    Private detailRows As New Generic.List(Of KeyValuePair(Of Label, Label))()
    Private detailProgressBar As ProgressBar = Nothing

    ''' <summary>按需构建结构化详情面板(挂在 detailGroup 内,与空态 detailLabel 互斥显示)。
    ''' 行数固定为 状态/进度/大小/速度/剩余/路径,内容在 UpdateDetailContent 里填。</summary>
    Private Sub EnsureDetailContentPanel()
        If detailContentPanel IsNot Nothing AndAlso Not detailContentPanel.IsDisposed Then Return
        If detailGroup Is Nothing Then Return

        detailContentPanel = New Panel()
        detailContentPanel.Name = "detailContentPanel"
        detailContentPanel.Dock = DockStyle.Fill
        detailContentPanel.Visible = False

        detailTitleLabel = New Label()
        detailTitleLabel.Name = "detailTitleLabel"
        detailTitleLabel.AutoSize = False
        detailTitleLabel.AutoEllipsis = True
        detailTitleLabel.Height = 36
        ' 标题加粗,只在此处建一次 Font(换肤不重建,避免 GDI 对象堆积)。
        detailTitleLabel.Font = New Drawing.Font(detailTitleLabel.Font, Drawing.FontStyle.Bold)

        detailContentPanel.Controls.Add(detailTitleLabel)

        detailRows.Clear()
        For i As Integer = 0 To 5
            Dim cap As New Label()
            cap.Name = "detailCap" & i.ToString()
            cap.AutoSize = False
            cap.Width = 48
            cap.Height = 20
            cap.TextAlign = Drawing.ContentAlignment.MiddleLeft

            Dim val As New Label()
            val.Name = "detailVal" & i.ToString()
            val.AutoSize = False
            val.Height = 20
            val.TextAlign = Drawing.ContentAlignment.MiddleLeft
            val.AutoEllipsis = True

            detailContentPanel.Controls.Add(cap)
            detailContentPanel.Controls.Add(val)
            detailRows.Add(New KeyValuePair(Of Label, Label)(cap, val))
        Next

        detailProgressBar = New ProgressBar()
        detailProgressBar.Name = "detailProgressBar"
        detailProgressBar.Minimum = 0
        detailProgressBar.Maximum = 100
        detailProgressBar.Visible = False
        detailProgressBar.Height = 10
        detailContentPanel.Controls.Add(detailProgressBar)

        detailGroup.Controls.Add(detailContentPanel)
        detailContentPanel.BringToFront()
        ' 运行时创建的详情面板同样接受链接文本拖放(与 detailLabel 一致)。
        EnableLinkDrop(detailContentPanel)
        LayoutDetailContent()
        ApplyDetailContentTheme()
    End Sub

    ''' <summary>详情面板内部几何。宽度按 detailGroup 客户区算,避免 DPI 变化后错位。</summary>
    Private Sub LayoutDetailContent()
        Try
            If detailContentPanel Is Nothing OrElse detailContentPanel.IsDisposed Then Return
            Dim sc As Single = GetUiScale()
            Dim pad As Integer = CInt(8 * sc)
            Dim w As Integer = detailContentPanel.ClientSize.Width
            If w <= 0 Then w = detailGroup.ClientSize.Width - pad * 2
            Dim innerW As Integer = Math.Max(60, w - pad * 2)
            Dim capW As Integer = CInt(46 * sc)
            Dim rowH As Integer = Math.Max(18, CInt(20 * sc))

            detailTitleLabel.SetBounds(pad, pad, innerW, CInt(34 * sc))

            Dim y As Integer = detailTitleLabel.Bottom + CInt(4 * sc)
            For i As Integer = 0 To detailRows.Count - 1
                Dim cap As Label = detailRows(i).Key
                Dim val As Label = detailRows(i).Value
                cap.SetBounds(pad, y, capW, rowH)
                val.SetBounds(pad + capW, y, innerW - capW, rowH)
                y += rowH

                ' 第 2 行(进度)下方插一条进度条
                If i = 1 AndAlso detailProgressBar IsNot Nothing Then
                    detailProgressBar.SetBounds(pad + capW, y, innerW - capW, CInt(10 * sc))
                    detailProgressBar.Visible = True
                    y += detailProgressBar.Height + CInt(2 * sc)
                End If
            Next
        Catch ex As Exception
            Log.WriteDebug("LayoutDetailContent failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub ApplyDetailContentTheme()
        Try
            If detailContentPanel Is Nothing OrElse detailContentPanel.IsDisposed Then Return
            Dim subtle As Drawing.Color = ThemeManager.GetColor("Border")
            Dim fore As Drawing.Color = ThemeManager.GetColor("Fore")
            If detailTitleLabel IsNot Nothing Then
                detailTitleLabel.ForeColor = fore
            End If
            For Each kv As KeyValuePair(Of Label, Label) In detailRows
                kv.Key.ForeColor = subtle
                kv.Value.ForeColor = fore
            Next
            If detailProgressBar IsNot Nothing Then
                detailProgressBar.BackColor = ThemeManager.GetColor("ControlBack")
                detailProgressBar.ForeColor = ThemeManager.GetColor("Selection")
            End If
        Catch ex As Exception
            Log.WriteDebug("ApplyDetailContentTheme failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>把选中项的数据填进结构化详情。标签文案走语言系统,随语言切换同步。</summary>
    Private Sub UpdateDetailContent(ele As IDescarga)
        Try
            EnsureDetailContentPanel()
            If detailContentPanel Is Nothing Then Return

            detailTitleLabel.Text = ele.DescargaNombre

            Dim st As Estado = ele.DescargaEstado()
            Dim pct As Decimal = ele.DescargaPorcentaje()
            Dim size As Long = ele.DescargaTamanoBytes()
            Dim done As String = "-"
            If size > 0 Then
                done = PintarTamano(Math.Ceiling(pct * size / 100)) & " / " & PintarTamano(size)
            End If

            Dim fic As Fichero = TryCast(ele, Fichero)
            Dim ruta As String = "-"
            If fic IsNot Nothing AndAlso Not String.IsNullOrEmpty(fic.RutaRelativa) Then ruta = fic.RutaRelativa

            Dim captions() As String = {
                Language.GetText("Status"),
                Language.GetText("Progress"),
                Language.GetText("Size"),
                Language.GetText("Speed"),
                Language.GetText("Remaining"),
                Language.GetText("Detail_Path")
            }
            ' 空字符串统一显示为 "-",避免详情出现空白行(速度/剩余在未下载时本就无值)。
            Dim speedTxt As String = PintarVelocidadDescarga(ele)
            If String.IsNullOrEmpty(speedTxt) Then speedTxt = "-"
            Dim remainTxt As String = ele.DescargaTiempoEstimadoDescarga()
            If String.IsNullOrEmpty(remainTxt) Then remainTxt = "-"

            Dim values() As String = {
                EstadoDisplayText(st),
                pct.ToString("F2") & "%",
                done,
                speedTxt,
                remainTxt,
                ruta
            }

            For i As Integer = 0 To Math.Min(captions.Length, detailRows.Count) - 1
                detailRows(i).Key.Text = captions(i)
                detailRows(i).Value.Text = values(i)
                ' 失败态用语义色标注,与列表"状态"列的着色保持一致
                If i = 0 AndAlso st = Estado.Erroneo Then
                    detailRows(i).Value.ForeColor = ThemeManager.GetColor("ErrorFore")
                ElseIf i = 0 AndAlso st = Estado.Completado Then
                    detailRows(i).Value.ForeColor = ThemeManager.GetColor("SuccessFore")
                Else
                    detailRows(i).Value.ForeColor = ThemeManager.GetColor("Fore")
                End If
            Next

            If detailProgressBar IsNot Nothing Then
                Dim p As Integer = CInt(Math.Floor(pct))
                If p < 0 Then p = 0
                If p > 100 Then p = 100
                Try
                    detailProgressBar.Value = p
                Catch
                End Try
            End If

            detailContentPanel.Visible = True
            If detailLabel IsNot Nothing Then detailLabel.Visible = False
            LayoutDetailContent()
        Catch ex As Exception
            Log.WriteDebug("UpdateDetailContent failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>回到空态:显示引导文案(Detail_Empty + Detail_EmptyHint),隐藏结构化面板。
    ''' 主/副两行通过换行拼进同一个 detailLabel,不新增控件。</summary>
    Private Sub ShowDetailEmptyState()
        Try
            If detailContentPanel IsNot Nothing AndAlso Not detailContentPanel.IsDisposed Then
                detailContentPanel.Visible = False
            End If
            If detailLabel Is Nothing OrElse detailLabel.IsDisposed Then Return
            detailLabel.Visible = True
            Dim main As String = Language.GetText("Detail_Empty")
            Dim hint As String = Language.GetText("Detail_EmptyHint")
            Dim text As String = main
            If Not String.IsNullOrEmpty(hint) AndAlso Not String.Equals(hint, "Detail_EmptyHint") Then
                text = main & vbCrLf & vbCrLf & hint
            End If
            If detailLabel.Text <> text Then detailLabel.Text = text
        Catch ex As Exception
            Log.WriteDebug("ShowDetailEmptyState failed: " & Log.SafeException(ex))
        End Try
    End Sub

#End Region

    ''' <summary>配额横幅:Anchor 布局,显示/隐藏时整体下移下载列表,无 Dock 冲突。
    ''' RC:Top 跟随工具栏底部(不再写死 40),高度按 DPI 缩放,防 125%/150% 下压住工具栏。</summary>
    Private Sub InitQuotaBanner()
        Dim sc As Single = 1.0F
        Try
            Using g As Drawing.Graphics = Me.CreateGraphics()
                sc = g.DpiY / 96.0F
            End Using
        Catch
        End Try
        If sc < 1.0F Then sc = 1.0F
        Dim bannerH As Integer = CInt(30 * sc)
        quotaBannerPanel = New Panel()
        quotaBannerPanel.Name = "quotaBannerPanel"
        quotaBannerPanel.Height = bannerH
        quotaBannerPanel.Left = 0
        Try
            quotaBannerPanel.Top = TableLayoutPanel1.Bottom
        Catch
            quotaBannerPanel.Top = 40
        End Try
        quotaBannerPanel.Width = Me.ClientSize.Width
        quotaBannerPanel.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        quotaBannerPanel.Visible = False

        quotaBannerLabel = New Label()
        quotaBannerLabel.Name = "quotaBannerLabel"
        quotaBannerLabel.AutoSize = False
        quotaBannerLabel.Left = 12
        quotaBannerLabel.Top = 0
        quotaBannerLabel.Height = bannerH
        quotaBannerLabel.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        quotaBannerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft

        quotaRetryNowButton = New Button()
        quotaRetryNowButton.Name = "quotaRetryNowButton"
        quotaRetryNowButton.Height = CInt(23 * sc)
        quotaRetryNowButton.Width = CInt(110 * sc)
        quotaRetryNowButton.Top = (bannerH - quotaRetryNowButton.Height) \ 2
        quotaRetryNowButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        AddHandler quotaRetryNowButton.Click, AddressOf QuotaRetryNow_Click

        quotaBannerPanel.Controls.Add(quotaBannerLabel)
        quotaBannerPanel.Controls.Add(quotaRetryNowButton)
        Me.Controls.Add(quotaBannerPanel)
        quotaBannerPanel.BringToFront()
        LayoutQuotaBanner()
        ApplyQuotaBannerTheme()
        UpdateQuotaBannerTexts()
    End Sub

    Private Sub LayoutQuotaBanner()
        If quotaBannerPanel Is Nothing OrElse quotaRetryNowButton Is Nothing OrElse quotaBannerLabel Is Nothing Then Return
        Try
            quotaBannerPanel.Top = TableLayoutPanel1.Bottom
        Catch
        End Try
        quotaBannerPanel.Width = Me.ClientSize.Width
        quotaRetryNowButton.Left = quotaBannerPanel.Width - quotaRetryNowButton.Width - 12
        quotaBannerLabel.Width = Math.Max(50, quotaRetryNowButton.Left - 18)
    End Sub

    Private Sub ApplyQuotaBannerTheme()
        If quotaBannerPanel Is Nothing OrElse quotaBannerLabel Is Nothing Then Return
        quotaBannerPanel.BackColor = ThemeManager.GetColor("QuotaBack")
        quotaBannerLabel.ForeColor = ThemeManager.GetColor("QuotaFore")
        Try
            quotaRetryNowButton.BackColor = ThemeManager.GetColor("QuotaBack")
            quotaRetryNowButton.ForeColor = ThemeManager.GetColor("QuotaFore")
            quotaRetryNowButton.FlatStyle = FlatStyle.Flat
            quotaRetryNowButton.FlatAppearance.BorderColor = ThemeManager.GetColor("QuotaFore")
        Catch
        End Try
    End Sub

    Private Sub UpdateQuotaBannerTexts()
        If quotaBannerLabel Is Nothing OrElse quotaRetryNowButton Is Nothing Then Return
        quotaRetryNowButton.Text = Language.GetText("Quota_RetryNow")
        If String.IsNullOrEmpty(quotaRetryNowButton.Text) Then quotaRetryNowButton.Text = "Retry now"
    End Sub

    ''' <summary>配额倒计时文案。Ceiling 分钟在最后 60 秒会卡住"1 min"不动,
    ''' 且 1h59m30s 会进位成"2 h 0 min":120 秒内直接读秒,以上向下取整。</summary>
    Private Shared Function FormatQuotaRemaining(span As TimeSpan) As String
        Dim totalSec As Integer = Math.Max(1, CInt(Math.Ceiling(span.TotalSeconds)))
        If totalSec < 120 Then
            If totalSec < 60 Then Return totalSec.ToString() & " s"
            Return "1 min " & (totalSec - 60).ToString() & " s"
        End If
        Dim totalMin As Integer = totalSec \ 60
        Dim h As Integer = totalMin \ 60
        Dim m As Integer = totalMin Mod 60
        If h > 0 Then Return h.ToString() & " h " & m.ToString() & " min"
        Return m.ToString() & " min"
    End Function

    ''' <summary>后台线程每 430ms 调用,内部编组回 UI。配额进入/解除时各弹一次气泡。</summary>
    Private Sub UpdateQuotaUI()
        Try
            If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
            If Me.InvokeRequired Then
                Me.BeginInvoke(New Action(AddressOf UpdateQuotaUI))
                Return
            End If
            If quotaBannerPanel Is Nothing Then Return
            Dim quotaRem As TimeSpan? = MegaQuotaManager.GetRemaining()
            If quotaRem.HasValue Then
                quotaBannerPanel.Visible = True
                LayoutQuotaBanner()
                LayoutDownloadArea()
                quotaBannerLabel.Text = Language.GetText("Quota_Banner").Replace("%T%", FormatQuotaRemaining(quotaRem.Value))
                If Not quotaNotified Then
                    quotaNotified = True
                    Log.WriteWarning("Showing MEGA quota banner, remaining " & quotaRem.Value.ToString())
                    Try
                        IconoMinimizado.ShowBalloonTip(5000, "MegaDownloader", quotaBannerLabel.Text, ToolTipIcon.Warning)
                    Catch
                    End Try
                End If
            Else
                If quotaBannerPanel.Visible Then
                    quotaBannerPanel.Visible = False
                    LayoutDownloadArea()
                End If
                If quotaNotified Then
                    quotaNotified = False
                    Log.WriteWarning("MEGA quota cleared; queue auto-resumes.")
                    Try
                        IconoMinimizado.ShowBalloonTip(5000, "MegaDownloader", Language.GetText("Quota_Recovered"), ToolTipIcon.Info)
                    Catch
                    End Try
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub QuotaRetryNow_Click(sender As Object, e As EventArgs)
        Try
            MegaQuotaManager.ClearQuota()
            WakeQuotaFailedItems()
            UpdateQuotaUI()
            RefreshListaDescargas(True)
        Catch ex As Exception
            Log.WriteError("QuotaRetryNow failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>把配额失败项全部唤回 EnCola(手动出口与到期自动恢复共用)。
    ''' 收集用局部表:调用方横跨 UI 线程(立即重试)与调度线程(到期边沿),共享字段会竞态重复唤醒。</summary>
    Private Sub WakeQuotaFailedItems()
        Dim woken As Integer = 0
        Dim pend As New Generic.List(Of Fichero)
        Mutex.ListaDescargas.WaitOne()
        Try
            For Each paq As Paquete In Me.ListaPaquetes
                For Each fic As Fichero In paq.ListaFicheros
                    If fic.DescargaEstado = Estado.Erroneo AndAlso fic.FailedByQuota Then
                        pend.Add(fic)
                    End If
                Next
            Next
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try
        For Each fic As Fichero In pend
            ' P0-1:配额唤醒保留断点,不删 .part。
            fic.ResetearDescarga(True)
            fic.SetDescargaEstado = Estado.EnCola
            woken += 1
        Next
        If woken > 0 Then Log.WriteWarning("Woke " & woken & " quota-failed files back to queue.")
    End Sub

    Private Sub InitBatchMenu()
        RetryAllFailedMenuItem.Name = "RetryAllFailedMenuItem"
        RemoveAllFailedMenuItem.Name = "RemoveAllFailedMenuItem"
        ResetColumnsMenuItem.Name = "ResetColumnsMenuItem"
        sepBatchMenuItem.Name = "sepBatchMenuItem"
        sepColumnsMenuItem.Name = "sepColumnsMenuItem"
        MenuDescarga.Items.Add(sepBatchMenuItem)
        MenuDescarga.Items.Add(RetryAllFailedMenuItem)
        MenuDescarga.Items.Add(RemoveAllFailedMenuItem)
        MenuDescarga.Items.Add(sepColumnsMenuItem)
        MenuDescarga.Items.Add(ResetColumnsMenuItem)
        UpdateBatchMenuTexts()
    End Sub

    Private Sub UpdateBatchMenuTexts()
        RetryAllFailedMenuItem.Text = Language.GetText("Retry all failed")
        If String.IsNullOrEmpty(RetryAllFailedMenuItem.Text) Then RetryAllFailedMenuItem.Text = "Retry all failed"
        RemoveAllFailedMenuItem.Text = Language.GetText("Remove all failed")
        If String.IsNullOrEmpty(RemoveAllFailedMenuItem.Text) Then RemoveAllFailedMenuItem.Text = "Remove all failed"
        ResetColumnsMenuItem.Text = Language.GetText("Reset column widths")
        If String.IsNullOrEmpty(ResetColumnsMenuItem.Text) Then ResetColumnsMenuItem.Text = "Reset column widths"
    End Sub

    Private Sub ResetColumnsMenuItem_Click(sender As Object, e As EventArgs) Handles ResetColumnsMenuItem.Click
        ' 程序内回到默认的入口:此前 # 列 Hideable=False + 迁移已跑过,坏状态只能手改配置。
        Try
            ApplyColumnDefaults()
            Try
                Config.ConfigUI.EstadoLista = ListaDescargas.SaveState
                Config.GuardarXML(False)
            Catch ex As Exception
                Log.WriteError("ResetColumns persist failed: " & ex.ToString)
            End Try
            RefreshListaDescargas(True)
            ToastForm.ShowToast(Me, ResetColumnsMenuItem.Text)
        Catch ex As Exception
            Log.WriteError("ResetColumns failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Function CollectErroneoFiles() As Generic.List(Of Fichero)
        Dim res As New Generic.List(Of Fichero)
        Mutex.ListaDescargas.WaitOne()
        Try
            For Each paq As Paquete In Me.ListaPaquetes
                For Each fic As Fichero In paq.ListaFicheros
                    If fic.DescargaEstado = Estado.Erroneo Then res.Add(fic)
                Next
            Next
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try
        Return res
    End Function

    Private Sub RetryAllFailedMenuItem_Click(sender As Object, e As EventArgs) Handles RetryAllFailedMenuItem.Click
        Try
            Dim lista As Generic.List(Of Fichero) = CollectErroneoFiles()
            For Each fic As Fichero In lista
                fic.ResetearDescarga()
                fic.SetDescargaEstado = Estado.EnCola
            Next
            Log.WriteWarning("Retrying all failed files: " & lista.Count)
            RefreshListaDescargas(True)
        Catch ex As Exception
            Log.WriteError("RetryAllFailed failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub RemoveAllFailedMenuItem_Click(sender As Object, e As EventArgs) Handles RemoveAllFailedMenuItem.Click
        Try
            Dim lista As Generic.List(Of Fichero) = CollectErroneoFiles()
            If lista.Count = 0 Then Return
            Dim confirm As String = Language.GetText("Remove all failed confirm").Replace("%N%", lista.Count.ToString())
            If MessageBox.Show(confirm, Language.GetText("Confirmation"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) <> DialogResult.OK Then Return
            Dim deletePart As Boolean = MessageBox.Show(Language.GetText("Remove all failed delete part"), Language.GetText("Confirmation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
            For Each fic As Fichero In lista
                Eliminar(fic, deletePart, False)
            Next
            RefreshListaDescargas(True)
        Catch ex As Exception
            Log.WriteError("RemoveAllFailed failed: " & Log.SafeException(ex))
        End Try
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


    ''' <summary>
    ''' P0-1 UI:应用一次性列默认集。AllColumns 顺序固定 = Designer Add 顺序:
    ''' 0 # / 1 Nombre / 2 Descargado / 3 Tamaño / 4 Estado / 5 Progreso% / 6 Progreso / 7 Velocidad / 8 EDT / 9 Restante。
    ''' RC:升级为全量默认(含顺序/显隐/宽度),复用为右键"恢复默认列宽"与坏状态自愈的唯一入口。
    ''' </summary>
    Private Sub ApplyColumnUIDefaultsV26()
        ApplyColumnDefaults()
    End Sub

    ''' <summary>列宽上下限(拖拽"无限长"的根因:此前所有列都无 Minimum/MaximumWidth)。</summary>
    Private Sub ApplyColumnWidthLimits()
        Try
            If ListaDescargas Is Nothing OrElse ListaDescargas.AllColumns Is Nothing Then Return
            If ListaDescargas.AllColumns.Count < 10 Then Return
            SetColumnWidthLimit(0, 20, 40)
            SetColumnWidthLimit(1, 150, 700)
            SetColumnWidthLimit(2, 60, 120)
            SetColumnWidthLimit(3, 60, 120)
            SetColumnWidthLimit(4, 90, 320)
            SetColumnWidthLimit(5, 45, 80)
            SetColumnWidthLimit(6, 60, 200)
            SetColumnWidthLimit(7, 55, 120)
            SetColumnWidthLimit(8, 55, 120)
            SetColumnWidthLimit(9, 55, 120)
            ' 文件名列保持自动吃剩余空间(窗口拉宽不留白);"反向"手感由上下限兜底:它永远不会被压成 0。
            ListaDescargas.AllColumns(1).FillsFreeSpace = True
        Catch ex As Exception
            Log.WriteError("ApplyColumnWidthLimits failed: " & ex.ToString)
        End Try
    End Sub

    Private Sub SetColumnWidthLimit(idx As Integer, minW As Integer, maxW As Integer)
        Try
            Dim col As BrightIdeasSoftware.OLVColumn = ListaDescargas.AllColumns(idx)
            If col Is Nothing Then Return
            col.MinimumWidth = minW
            col.MaximumWidth = maxW
        Catch ex As Exception
            Log.WriteError("SetColumnWidthLimit failed: " & ex.ToString)
        End Try
    End Sub

    ''' <summary>全量列默认:顺序/显隐/宽度。右键恢复与自愈共用,保持行为一致。</summary>
    Private Sub ApplyColumnDefaults()
        Try
            If ListaDescargas Is Nothing OrElse ListaDescargas.AllColumns Is Nothing Then Return
            If ListaDescargas.AllColumns.Count < 10 Then Return
            ApplyColumnWidthLimits()
            Dim widths() As Integer = {20, 185, 70, 70, 90, 55, 80, 77, 70, 60}
            For i As Integer = 0 To 9
                Dim col As BrightIdeasSoftware.OLVColumn = ListaDescargas.AllColumns(i)
                col.Width = widths(i)
                Try
                    col.DisplayIndex = i
                Catch
                End Try
            Next
            ' Descargado 可由 Tamaño×% 推算,默认隐藏降噪(用户可从列菜单恢复);Restante 默认隐藏
            ListaDescargas.AllColumns(2).IsVisible = False
            ListaDescargas.AllColumns(5).IsVisible = True
            ListaDescargas.AllColumns(9).IsVisible = False
            ListaDescargas.AllColumns(1).FillsFreeSpace = True
            For i As Integer = 0 To 9
                If i <> 1 Then
                    Try
                        ListaDescargas.AllColumns(i).FillsFreeSpace = False
                    Catch
                    End Try
                End If
            Next
            ListaDescargas.RebuildColumns()
            Log.WriteWarning("Applied column defaults (widths/order/visibility).")
        Catch ex As Exception
            Log.WriteError("ApplyColumnDefaults failed: " & ex.ToString)
        End Try
    End Sub

    ''' <summary>
    ''' 坏列状态自愈:RestoreState 读回的持久化宽度若出现关键列被压 0 / 单列上千像素 /
    ''' 可见列总宽远超窗口,即判 corruption。返回 True 表示已重置,调用方负责落盘。
    ''' </summary>
    Private Function RepairColumnStateIfCorrupted() As Boolean
        Try
            If ListaDescargas Is Nothing OrElse ListaDescargas.AllColumns Is Nothing Then Return False
            If ListaDescargas.AllColumns.Count < 10 Then Return False
            Dim hashCol As BrightIdeasSoftware.OLVColumn = ListaDescargas.AllColumns(0)
            Dim nameCol As BrightIdeasSoftware.OLVColumn = ListaDescargas.AllColumns(1)
            If hashCol.Width < 20 OrElse nameCol.Width < 50 Then
                Log.WriteWarning("Column state corrupted (#=" & hashCol.Width & ", Nombre=" & nameCol.Width & "); resetting to defaults.")
                ApplyColumnDefaults()
                Return True
            End If
            Dim total As Integer = 0
            For Each c As BrightIdeasSoftware.OLVColumn In ListaDescargas.AllColumns
                If c.Width < 0 OrElse c.Width > 800 Then
                    Log.WriteWarning("Column state corrupted (col '" & c.Text & "' width=" & c.Width & "); resetting to defaults.")
                    ApplyColumnDefaults()
                    Return True
                End If
                Try
                    If c.IsVisible Then total += c.Width
                Catch
                    total += c.Width
                End Try
            Next
            ' 总宽阈值跟窗口走:上限夹取后合法最大约 2120,固定 2000 会误伤宽屏手动布局。
            Dim totalLimit As Integer = Math.Max(2000, Me.ClientSize.Width * 2)
            If total > totalLimit Then
                Log.WriteWarning("Column state corrupted (visible total=" & total & "); resetting to defaults.")
                ApplyColumnDefaults()
                Return True
            End If
            Return False
        Catch ex As Exception
            Log.WriteError("RepairColumnStateIfCorrupted failed: " & ex.ToString)
            Return False
        End Try
    End Function

    Private Sub ListaDescargas_ColumnWidthChanging(sender As Object, e As ColumnWidthChangingEventArgs) Handles ListaDescargas.ColumnWidthChanging
        ' OLV Minimum/MaximumWidth 不拦截表头拖拽,此处手动夹取,防止再次拖出 0 / 上千像素。
        Try
            Dim olv As BrightIdeasSoftware.ObjectListView = TryCast(sender, BrightIdeasSoftware.ObjectListView)
            If olv Is Nothing OrElse olv.Columns Is Nothing Then Return
            If e.ColumnIndex < 0 OrElse e.ColumnIndex >= olv.Columns.Count Then Return
            Dim col As BrightIdeasSoftware.OLVColumn = TryCast(olv.Columns(e.ColumnIndex), BrightIdeasSoftware.OLVColumn)
            If col Is Nothing Then Return
            If col.MinimumWidth > 0 AndAlso e.NewWidth < col.MinimumWidth Then e.NewWidth = col.MinimumWidth
            If col.MaximumWidth > 0 AndAlso e.NewWidth > col.MaximumWidth Then e.NewWidth = col.MaximumWidth
        Catch ex As Exception
            Log.WriteError("ColumnWidthChanging clamp failed: " & ex.ToString)
        End Try
    End Sub

    ''' <summary>
    ''' P1-10 UI:错误行内联原因首行(截断 40 字)。只读 Fichero.DescripcionError 字段,O(1),
    ''' 可安全跑在 2.3Hz 列表刷新热路径;包级别不扫子文件(展开看各文件原因),外层 Try 已兜底。
    ''' </summary>
    Private Shared Function ShortErrorReason(ele As IDescarga) As String
        Dim fic As Fichero = TryCast(ele, Fichero)
        If fic Is Nothing Then Return Nothing
        Dim msg As String = fic.DescripcionError
        If String.IsNullOrEmpty(msg) Then Return Nothing
        Dim cut As Integer = msg.IndexOf(vbLf)
        If cut >= 0 Then msg = msg.Substring(0, cut)
        msg = msg.Trim().Replace(vbCr, " ").Replace(vbTab, " ")
        If msg.Length > 40 Then msg = msg.Substring(0, 40) & "..."
        If msg.Length = 0 Then Return Nothing
        Return msg
    End Function

    ' P2-11b:左导航分组 + 计数。计数与过滤共用 DownloadEstadoFilter.MatchesScope,所见即所数。
    ' 计数单位是包(列表顶层行),不是文件:此前连包带文件各算一遍,一个 12 文件的完成包
    ' 就贡献 13,导航"已完成(25)"而列表只有 2 个包——口径与所见不一致,现只数包。
    Private _navScope As DownloadEstadoFilter.NavScope = DownloadEstadoFilter.NavScope.All
    Private _updatingNav As Boolean = False

    Private Sub InitNavList()
        ' RC:ItemHeight 跟随 DPI/字体(此前写死 20,大字体下截断)。
        ' 注意:DrawMode=Normal 时 ItemHeight 被忽略,真实行高由字体决定,
        ' 高度必须按 GetItemHeight 实测(见 GetNavActualItemHeight),否则 5 项放不下出竖滚。
        Try
            navListBox.IntegralHeight = False
            navListBox.HorizontalScrollbar = False
            navListBox.ScrollAlwaysVisible = False
        Catch
        End Try
        Try
            Dim sc As Single = 1.0F
            Using g As Drawing.Graphics = Me.CreateGraphics()
                sc = g.DpiY / 96.0F
            End Using
            If sc < 1.0F Then sc = 1.0F
            navListBox.ItemHeight = Math.Max(20, CInt((navListBox.Font.Height + 6) * sc))
        Catch
        End Try
        UpdateNavCounts()
        If navListBox.SelectedIndex < 0 Then navListBox.SelectedIndex = 0
        ApplyNavListColors()
    End Sub

    ''' <summary>左导航真实行高。Normal 模式下 ItemHeight 属性无效,必须用 GetItemHeight 实测,
    ''' 否则按 ItemHeight×5 算出的高度偏小,第 5 项被截断需要手动滚动。</summary>
    Private Function GetNavActualItemHeight() As Integer
        Try
            If navListBox IsNot Nothing AndAlso Not navListBox.IsDisposed AndAlso navListBox.Items.Count > 0 Then
                Dim h As Integer = navListBox.GetItemHeight(0)
                If h > 0 Then Return h
            End If
        Catch
        End Try
        Try
            If navListBox IsNot Nothing AndAlso navListBox.Font IsNot Nothing Then
                Return Math.Max(20, navListBox.Font.Height + 6)
            End If
        Catch
        End Try
        Return 20
    End Function

    ''' <summary>左导航免滚动适配:高度按真实行高×项数一次给够(无竖滚),
    ''' 宽度占满 navPanel;文本超宽时优先加宽面板(≤220),平时无任何滚动条,
    ''' 窄窗口实在加不动才出横滚兜底(此时高度补上横滚条高度,横滚不再顶出竖滚)。</summary>
    Private Sub FitNavListBoxNoScroll()
        Try
            If navListBox Is Nothing OrElse navListBox.IsDisposed Then Return
            If navPanel Is Nothing OrElse navPanel.IsDisposed Then Return
            If navListBox.Items.Count <= 0 Then Return
            navListBox.IntegralHeight = False
            navListBox.ScrollAlwaysVisible = False
            ' 先关横滚:开着时 ClientSize.Height 被压缩,边框与宽度都会误算,
            ' 且横滚条会盖住最后一项反过来顶出竖滚。
            If navListBox.HorizontalScrollbar Then
                Try
                    navListBox.HorizontalScrollbar = False
                    navListBox.HorizontalExtent = 0
                Catch
                End Try
            End If
            Dim itemH As Integer = GetNavActualItemHeight()
            ' 边框实测(自适应 Fixed3D/FixedSingle),失败时按 Fixed3D 取 4。
            Dim borderH As Integer = 4
            Try
                Dim bh As Integer = navListBox.Height - navListBox.ClientSize.Height
                If bh >= 2 AndAlso bh <= 10 Then borderH = bh
            Catch
            End Try
            ' GDI 量字(与 ListBox 渲染同管线,比 GDI+ 的 MeasureString 准)。
            Dim maxW As Integer = 0
            Try
                For Each o As Object In navListBox.Items
                    Dim s As String = If(o Is Nothing, String.Empty, o.ToString())
                    If String.IsNullOrEmpty(s) Then Continue For
                    Dim sz As Drawing.Size = Windows.Forms.TextRenderer.MeasureText(
                        s, navListBox.Font, New Drawing.Size(Integer.MaxValue, Integer.MaxValue),
                        Windows.Forms.TextFormatFlags.SingleLine Or Windows.Forms.TextFormatFlags.NoPadding)
                    Dim w As Integer = sz.Width + 10
                    If w > maxW Then maxW = w
                Next
            Catch
            End Try
            ' 超宽则加宽面板而不是出横滚(上限 220,避免吃掉中部列表)。
            If maxW > 0 Then
                Dim chromeW As Integer = 0
                Try
                    chromeW = (navPanel.Width - navPanel.ClientSize.Width) +
                              (navListBox.Width - navListBox.ClientSize.Width) + 4
                    If chromeW < 4 OrElse chromeW > 30 Then chromeW = 8
                Catch
                    chromeW = 8
                End Try
                Dim needPanelW As Integer = maxW + chromeW
                If needPanelW > NavRailWidth AndAlso needPanelW <= NavRailMaxWidth Then
                    NavRailWidth = needPanelW
                    ApplyNavRailWidth()
                ElseIf needPanelW > NavRailMaxWidth AndAlso NavRailWidth < NavRailMaxWidth Then
                    NavRailWidth = NavRailMaxWidth
                    ApplyNavRailWidth()
                End If
            End If
            Dim fullW As Integer = navPanel.ClientSize.Width
            If fullW > 0 AndAlso navListBox.Width <> fullW Then navListBox.Width = fullW
            Dim listH As Integer = itemH * navListBox.Items.Count + borderH
            ' 面板实在加不动(窄窗口被 ApplyNavRailWidth 钳住)才出横滚,
            ' 高度必须补上横滚条高度,否则横滚盖住最后一项又顶出竖滚。
            Dim needH As Boolean = False
            If maxW > 0 Then
                Try
                    needH = maxW > navListBox.ClientSize.Width
                Catch
                End Try
            End If
            If needH Then
                Try
                    navListBox.HorizontalScrollbar = True
                    navListBox.HorizontalExtent = maxW
                Catch
                End Try
                listH += Windows.Forms.SystemInformation.HorizontalScrollBarHeight
            End If
            If navListBox.Height <> listH Then navListBox.Height = listH
        Catch ex As Exception
            Log.WriteDebug("FitNavListBoxNoScroll failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub ApplyNavListColors()
        Try
            If navListBox Is Nothing OrElse navListBox.IsDisposed Then Return
            ' ListBox 不在 ThemeManager 递归覆盖范围,手动同步
            navListBox.BackColor = ThemeManager.GetColor("Back")
            navListBox.ForeColor = ThemeManager.GetColor("Fore")
        Catch ex As Exception
            Log.WriteDebug("ApplyNavListColors failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub navListBox_SelectedIndexChanged(sender As Object, e As EventArgs) Handles navListBox.SelectedIndexChanged
        If _updatingNav Then Return
        Try
            Dim scope As DownloadEstadoFilter.NavScope = DownloadEstadoFilter.NavScope.All
            Select Case navListBox.SelectedIndex
                Case 1
                    scope = DownloadEstadoFilter.NavScope.Downloading
                Case 2
                    scope = DownloadEstadoFilter.NavScope.Waiting
                Case 3
                    scope = DownloadEstadoFilter.NavScope.Failed
                Case 4
                    scope = DownloadEstadoFilter.NavScope.Completed
            End Select
            If scope = _navScope Then Return
            _navScope = scope
            ApplyNavRoots()
            ListaDescargas.BuildList()
        Catch ex As Exception
            Log.WriteError("navListBox_SelectedIndexChanged failed: " & ex.ToString)
        End Try
    End Sub

    Private Sub UpdateNavCounts()
        Try
            If navListBox Is Nothing OrElse navListBox.IsDisposed Then Return
            _updatingNav = True
            Try
                While navListBox.Items.Count < 5
                    navListBox.Items.Add("")
                End While
                Dim totals(4) As Integer
                If ListaPaquetes IsNot Nothing Then
                    For Each p As Paquete In ListaPaquetes
                        CountNavObject(p, totals)
                    Next
                End If
                Dim keys() As String = {"Nav_All", "Nav_Downloading", "Nav_Waiting", "Nav_Failed", "Nav_Completed"}
                For i As Integer = 0 To 4
                    Dim t As String = Language.GetText(keys(i)) & " (" & totals(i).ToString() & ")"
                    If Not Object.Equals(navListBox.Items(i), t) Then navListBox.Items(i) = t
                Next
                If navListBox.SelectedIndex < 0 OrElse navListBox.SelectedIndex > 4 Then navListBox.SelectedIndex = 0
            Finally
                _updatingNav = False
            End Try
            ' 计数后缀变宽(如 9→10000)时刷新横向范围;高度按真实行高重算,保证 5 项免竖滚。
            FitNavListBoxNoScroll()
        Catch ex As Exception
            Log.WriteDebug("UpdateNavCounts failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>顶层包计数。包命中判定本身已含子文件穿透(MatchesScope),此处不再递归进文件,
    ''' 否则包与文件重复累加,计数与列表行数对不上。</summary>
    Private Shared Sub CountNavObject(obj As Object, totals() As Integer)
        totals(0) += 1
        For s As Integer = 1 To 4
            If DownloadEstadoFilter.MatchesScope(obj, CType(s, DownloadEstadoFilter.NavScope)) Then totals(s) += 1
        Next
    End Sub

    ''' <summary>
    ''' P2-11b:按当前分组重设 Roots(包命中或子文件穿透才留)。SetObjects 会重置 Roots
    ''' 为全量,故每次重建后调用;False 刷新沿用当前 Roots。禁用 OLV UseFiltering。
    ''' </summary>
    Private Sub ApplyNavRoots()
        Try
            If ListaDescargas Is Nothing OrElse ListaPaquetes Is Nothing Then Return
            If _navScope = DownloadEstadoFilter.NavScope.All Then
                ListaDescargas.Roots = Me.ListaPaquetes
            Else
                Dim shown As New Generic.List(Of Paquete)()
                For Each p As Paquete In ListaPaquetes
                    If DownloadEstadoFilter.MatchesScope(p, _navScope) Then shown.Add(p)
                Next
                ListaDescargas.Roots = shown
            End If
        Catch ex As Exception
            Log.WriteError("ApplyNavRoots failed: " & ex.ToString)
        End Try
    End Sub

    ''' <summary>下载区绝对布局:列表+两侧栏 Top/Height 永远由工具栏底+横幅显隐重算。
    ''' 此前相对位移(Top+=h)叠加 Anchor Top|Bottom,在 resize/最大化/DPI 变化时漂移累积,
    ''' 横幅解除后底部压住状态栏直到下次 resize。现所有几何一次算死,不再累加。</summary>
    Private Sub LayoutDownloadArea()
        Try
            If ListaDescargas Is Nothing OrElse ListaDescargas.IsDisposed Then Return
            If navPanel Is Nothing OrElse detailPanel Is Nothing Then Return
            If TableLayoutPanel1 Is Nothing OrElse StatusStrip1 Is Nothing Then Return
            Dim top As Integer = TableLayoutPanel1.Bottom + 2
            If quotaBannerPanel IsNot Nothing AndAlso quotaBannerPanel.Visible Then
                top += quotaBannerPanel.Height
            End If
            Dim bottom As Integer = StatusStrip1.Top - 4
            Dim h As Integer = Math.Max(50, bottom - top)
            ListaDescargas.Top = top
            ListaDescargas.Height = h
            navPanel.Top = top
            navPanel.Height = h
            detailPanel.Top = top
            detailPanel.Height = h

            ' 左栏加宽:列表 Left/Width 同步跟随,否则列表会压到加宽后的侧栏上。
            ' 窄窗口(接近 MinimumSize 640)时按可用空间回退,保证中部列表不被挤没。
            ApplyNavRailWidth()

            ' 左栏纵向分段定位(总览/快捷入口贴在过滤器下方,不随窗口高度拉伸)
            LayoutNavRail()

            ' 右栏详情内部几何跟随宽度变化(标签/值两列按客户区宽度算)
            If detailContentPanel IsNot Nothing AndAlso Not detailContentPanel.IsDisposed AndAlso
               detailContentPanel.Visible Then
                LayoutDetailContent()
            End If
        Catch ex As Exception
            Log.WriteDebug("LayoutDownloadArea failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>左栏宽度单一来源。保证最小宽度下中部列表仍有可用空间(≥320px)。</summary>
    Private Sub ApplyNavRailWidth()
        Try
            If navPanel Is Nothing OrElse ListaDescargas Is Nothing Then Return
            Dim desired As Integer = NavRailWidth
            ' 右侧 detailPanel 与各边距固定占用约 210px(见 Designer 的 x 布局),留 320px 给列表。
            Dim maxAllowed As Integer = Me.ClientSize.Width - 210 - 320
            If maxAllowed < 130 Then maxAllowed = 130
            Dim finalWidth As Integer = Math.Min(desired, maxAllowed)

            If navPanel.Width <> finalWidth Then navPanel.Width = finalWidth
            Dim listLeft As Integer = navPanel.Left + finalWidth + 6
            If ListaDescargas.Left <> listLeft Then ListaDescargas.Left = listLeft
            ListaDescargas.Width = Math.Max(50, detailPanel.Left - 6 - listLeft)
        Catch ex As Exception
            Log.WriteDebug("ApplyNavRailWidth failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Sub InitDownloadAreaLayout()
        Try
            ' 去掉 Bottom 锚点:高度只由 LayoutDownloadArea 决定,Anchor 不再插手 Height,从根上杜绝漂移。
            ListaDescargas.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            navPanel.Anchor = AnchorStyles.Top Or AnchorStyles.Left
            detailPanel.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        Catch ex As Exception
            Log.WriteDebug("InitDownloadAreaLayout anchor failed: " & Log.SafeException(ex))
        End Try
        LayoutDownloadArea()
    End Sub

    Private Sub ListaDescargas_SelectionChanged(sender As Object, e As EventArgs) Handles ListaDescargas.SelectionChanged
        UpdateDetailPanel()
    End Sub

    Private Sub UpdateDetailPanel()
        Try
            Dim sel As IDescarga = Nothing
            If ListaDescargas IsNot Nothing AndAlso ListaDescargas.SelectedObjects IsNot Nothing AndAlso
               ListaDescargas.SelectedObjects.Count > 0 Then
                sel = TryCast(ListaDescargas.SelectedObjects(0), IDescarga)
            End If

            If sel Is Nothing Then
                ShowDetailEmptyState()
            Else
                UpdateDetailContent(sel)
            End If
        Catch ex As Exception
            Log.WriteDebug("UpdateDetailPanel failed: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' <summary>状态本地化文本(ColEstado AspectGetter 的只读镜像,供详情面板复用;改键时两处同步,热路径本身不动)。</summary>
    Private Shared Function EstadoDisplayText(st As Estado) As String
        Select Case st
            Case Estado.EnCola
                Return Language.GetText("In queue")
            Case Estado.CreandoLocal
                Return Language.GetText("Creating files")
            Case Estado.Verificando
                Return Language.GetText("Verifying")
            Case Estado.Erroneo
                Return Language.GetText("Error capital leters")
            Case Estado.Pausado
                Return Language.GetText("Paused")
            Case Estado.Descomprimiendo
                Return Language.GetText("Extracting")
            Case Estado.Descargando
                Return Language.GetText("Downloading")
            Case Estado.ComprobandoMD5
                Return Language.GetText("Hashing MD5")
            Case Estado.Completado
                Return Language.GetText("Completed")
            Case Else
                Return "---"
        End Select
    End Function
#End Region

#Region "Gestion lista paquetes y descargas"


    ''' <summary>
    ''' Agregamos paquetes desde la pantalla "Addlinks" o desde la carga inicial
    ''' </summary>
    ''' <param name="Paquete"></param>
    ''' <remarks></remarks>
    Friend Sub AgregarPaquete(ByVal Paquete As Paquete, AgregadoDesdeServidorWeb As Boolean)

        Mutex.ListaDescargas.WaitOne()
        Try
            Me.ListaPaquetes.Add(Paquete)
            If String.IsNullOrEmpty(Paquete.Nombre) Then
                Paquete.Nombre = Language.GetText("Package") & " #" & Me.ListaPaquetes.Count.ToString
            End If
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try
        Log.WriteError("Package added: " & Paquete.Nombre)

        ReordenarPrioridadPaquetes(True)

        GuardarFicheroDescargas()
        ' P0-4 UI:任务入库后给一次非打断确认(之前只有日志,用户不知道"加上没")。
        ' Web 推送且主窗最小化时不弹(用户不在跟前,状态栏/托盘已有其它反馈位)。
        If Not (AgregadoDesdeServidorWeb AndAlso Me.WindowState = FormWindowState.Minimized) Then
            ToastForm.ShowToast(Me, Language.GetText("Toast_PackageAdded").Replace("%N%", Paquete.Nombre))
        End If
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


    ''' <summary>
    ''' Marshals an error dialog to the UI thread. BackgroundWorker.DoWork handlers run on
    ''' thread-pool threads; calling MsgBox directly can throw InvalidOperationException
    ''' if the form is closing/closed, and produces a parentless dialog. This helper
    ''' checks IsDisposed/IsHandleCreated and uses Invoke; if marshalling is impossible
    ''' (form gone or InvokeRequired check itself fails) the error is logged only.
    ''' </summary>
    Private Sub SafeShowError(message As String)
        Try
            If Me.IsDisposed OrElse Not Me.IsHandleCreated Then
                Log.WriteError("SafeShowError: form not available; message was: " & message)
                Return
            End If
            If Me.InvokeRequired Then
                Me.Invoke(New Action(Of String)(AddressOf SafeShowError), message)
                Return
            End If
            MessageBox.Show(Me, message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As InvalidOperationException
            Log.WriteError("SafeShowError: could not marshal to UI thread: " & ex.ToString & " (original message: " & message & ")")
        Catch ex As Exception
            Log.WriteError("SafeShowError: unexpected failure: " & ex.ToString & " (original message: " & message & ")")
        End Try
    End Sub

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
                        Try
                            Updater.ComprobarVersionMegadownloader(UrlNuevaVersionMegadownloader, VersionNuevaVersionMegadownloader)
                        Finally
                            Mutex.NumeroConexionesMaxima.ReleaseMutex()
                        End Try
                        If Not String.IsNullOrEmpty(UrlNuevaVersionMegadownloader) Then
                            ActivarUpdateButton()
                            ' "永不提醒"只针对特定版本:已跳过的版本不再弹窗,新版本仍会提醒
                            If String.IsNullOrEmpty(Config.UpdateSkipVersion) OrElse Config.UpdateSkipVersion <> VersionNuevaVersionMegadownloader Then
                                ProximoAvisoActualizacion = Now.AddSeconds(15)
                            End If
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
            SafeShowError(ex.Message) ' 完整堆栈已写入上一行日志,给用户只看消息
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

                ' v2.5 beta: 配额期内不做新文件信息校验(每次校验=一次 API 调用,会延长惩罚窗口)。
                Dim quotaHoldVerify As Boolean = MegaQuotaManager.IsQuarantined()
                If quotaHoldVerify Then TiempoDormir = 5000

                If Not NecesitaCambiarUsuarioYPassword AndAlso Not quotaHoldVerify Then

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
	                            	' 后台线程(DoWork)不能直接弹窗:无属主窗体会藏到主窗体后面,用户会以为卡死。
	                            	' 走 SafeShowError 编组回 UI 线程显示
	                            	SafeShowError("Error creating directory: " & ex.Message)
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
            SafeShowError(ex.Message) ' 完整堆栈已写入上一行日志,给用户只看消息
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
                ' 单点故障防护:单次迭代异常只记日志继续循环,不再整条 worker 死亡
                ' (此前 Try 包在 While 外面,一次异常=列表刷新+状态栏+横幅+倒计时+自动恢复全停摆,且无重启)。
                Try

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

                ' 左栏任务总览:与状态栏同频(430ms)刷新,数据源一致,不额外起线程。
                UpdateNavRailOverview()

                UpdateQuotaUI()

                If worker.CancellationPending Then Exit While

                ' Dirty trick!!
                Flujo &= "Getting mega:// parameters" & vbNewLine
                ApplicationInstanceManager.GetParameters()

                Flujo &= "Sleeping" & vbNewLine

                System.Threading.Thread.Sleep(430)

                Flujo &= "Finishing cycle" & vbNewLine

                sw.Stop()
                sw.Reset()

                Catch exIter As Exception
                    Log.WriteError("bgwActualizadorListaDescargas iteration failed, continuing (" & Flujo.Replace(vbNewLine, " ").Trim() & "): " & Log.SafeException(exIter))
                    Try
                        System.Threading.Thread.Sleep(1000)
                    Catch
                    End Try
                End Try

            End While
            Log.WriteWarning("Stopping worker bgwActualizadorListaDescargas")
        Catch ex As Exception
            Log.WriteError("Error in worker bgwActualizadorListaDescargas: " & ex.ToString)
            SafeShowError(ex.Message) ' 完整堆栈已写入上一行日志,给用户只看消息
        Finally
            If sw.ElapsedMilliseconds > 5000 Then
                Log.WriteError("bgwActualizadorListaDescargas was too slow (" & sw.ElapsedMilliseconds & " ms): " & vbNewLine & Flujo)
            End If
            bgwActualizadorListaDescargasCompleted = True
        End Try
    End Sub




    Private Sub bgwActualizadorListaDescargas_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwActualizadorListaDescargas.RunWorkerCompleted
        bgwActualizadorListaDescargasCompleted = True
        ' 看门狗:非关闭流程中的 worker 结束=异常穿透(内层已尽力自保),3 秒后自救重启。
        ' 关闭时 Cerrando/_ForzarCierre/Disposed 必有一真,不重启,不卡退出流程。
        Try
            If e.Cancelled Then Return
            If Cerrando OrElse _ForzarCierre OrElse Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Return
            Log.WriteError("bgwActualizadorListaDescargas ended unexpectedly; restarting in 3s.")
            System.Threading.Tasks.Task.Run(Sub()
                                                System.Threading.Thread.Sleep(3000)
                                                Try
                                                    If Cerrando OrElse _ForzarCierre OrElse Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Return
                                                    If bgwActualizadorListaDescargas.IsBusy Then Return
                                                    bgwActualizadorListaDescargasCompleted = False
                                                    bgwActualizadorListaDescargas.RunWorkerAsync()
                                                Catch ex As Exception
                                                    Log.WriteError("bgwActualizadorListaDescargas restart failed: " & Log.SafeException(ex))
                                                End Try
                                            End Sub)
        Catch ex As Exception
            Log.WriteError("bgwActualizadorListaDescargas watchdog failed: " & Log.SafeException(ex))
        End Try
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
        ' v2.5 beta: 配额熔断状态。配额期内不唤醒配额失败项、不开新任务；到期后配额失败项立即唤醒。
        Dim quotaHold As Boolean = MegaQuotaManager.IsQuarantined()
        ' P1:配额到期唤醒必须独立于自愈开关。WakeQuotaFailedItems 的唯一旧出口藏在下面
        ' If ResetearErrores 分支里,用户关掉"失败自愈"后横幅消失、倒计时归零,失败项却永久停在
        ' Erroneo——发布说明承诺的行为静默失效。此处熔断解除边沿直接复用 WakeQuotaFailedItems。
        Static quotaHoldPrev As Boolean = False
        Try
            If quotaHoldPrev AndAlso Not quotaHold Then WakeQuotaFailedItems()
        Catch ex As Exception
            Log.WriteError("Quota auto-wake failed: " & Log.SafeException(ex))
        End Try
        quotaHoldPrev = quotaHold

        ' Reset de descargas erroneas
        If ResetearErrores Then
            Mutex.ListaDescargas.WaitOne()
            For Each paq As Paquete In Me.ListaPaquetes
                For Each Fichero As Fichero In paq.ListaFicheros
                    If Fichero.DescargaEstado = Estado.Erroneo AndAlso Not Fichero.EsErrorPermanente Then
                        If Fichero.FailedByQuota Then
                            If Not quotaHold Then ColaReseteo.Add(Fichero)
                        ElseIf Not Fichero.FechaUltimoError.HasValue OrElse Fichero.FechaUltimoError.Value.AddMinutes(ResetearErroresPeriodo) < Now Then
                            ColaReseteo.Add(Fichero)
                        End If
                    End If
                Next
            Next
            Mutex.ListaDescargas.ReleaseMutex()
            For Each Fichero In ColaReseteo
                ' P0-1:自愈保留断点续传,不删 .part/不清分块(否则大文件超时后无限重下)。
                Fichero.ResetearDescarga(True)
                Fichero.SetDescargaEstado = Estado.EnCola
                Log.WriteInfo("Reseting file " & Fichero.FileID & " automatically.")
            Next
        End If


        Mutex.ListaDescargas.WaitOne()
        For Each paq As Paquete In Me.ListaPaquetes
            For Each Fichero As Fichero In paq.ListaFicheros

                If Fichero.DescargaEstado = Estado.EnCola And Fichero.DescargaProcesada AndAlso Not quotaHold Then
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
            ElseIf Fichero.DescargaEstado = Estado.Erroneo AndAlso Not Fichero.EsErrorPermanente Then
                ' ⑧:红字强制=手动全量重置后按单个强制起(删 .part 从头来,顺带修尺寸不符类错误)。
                Fichero.ResetearDescarga()
                Fichero.SetDescargaEstado = Estado.EnCola
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
                        ' B2-⑩:关闭必须等瞬态(此前只等 Descargando/Pausado,CreandoLocal/
                        ' Verificando/解压/MD5 回补中的任务会被撕裂存盘且关后复活)。
                        ' Pausado 为稳态,照旧等待以保持原有关机语义(有暂停项时走足超时,不在此批改动)。
                        If Fichero.DescargaEstado = Estado.Descargando Or _
                           Fichero.DescargaEstado = Estado.Pausado Or _
                           Fichero.DescargaEstado = Estado.CreandoLocal Or _
                           Fichero.DescargaEstado = Estado.Verificando Or _
                           Fichero.DescargaEstado = Estado.Descomprimiendo Or _
                           Fichero.DescargaEstado = Estado.ComprobandoMD5 Then
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
            If Not bgwDescompresorCompleted Then TodosFinalizados = False
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
        Try
            For Each paq As Paquete In Me.ListaPaquetes
                For Each Fichero As Fichero In paq.ListaFicheros

                    HayCola = True

                    ' Only fully successful tasks count as completed for auto-shutdown
                    If Fichero.DescargaEstado <> Estado.Completado Then
                        TodosCompletadosOErroneos = False
                    End If
                    If Fichero.DescargaEstado = Estado.Erroneo Then
                        HayErroneos = True
                        TodosCompletadosOErroneos = False
                    End If

                Next
            Next
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try

        If HayCola And TodosCompletadosOErroneos And Not HayErroneos Then
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
        If ficheroAEliminar Is Nothing AndAlso paqueteAEliminar Is Nothing Then
            ' 选中"包及其子文件"时子项会走到这里:它已随包被一并移除,
            ' 跳过后续 Stop/Dispose,避免 CancellationComplete 双挂导致 BorrarFicheroLocal/Dispose 执行两遍
            Exit Sub
        End If
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
            ' B1-①:Web 推送与手动加链走同链路——先经 URLProcessor 展开文件夹/ELC
            ' (此前逐 URL 直建 Fichero,文件夹链变成单个 FileID=根ID 的坏任务,API 报 ENOENT 永久错误);
            ' pckname 强净化(此前字符串拼接 .RutaLocal & "\" & .Nombre,已认证任意目录创建+下载落盘越狱)。
            Dim expanded As Generic.List(Of URLProcessor.FileURL) = Nothing
            Try
                expanded = URLProcessor.ProcessURLs(URLs, Config)
            Catch ex As Exception
                Log.WriteError("ControlRemotoAgregarLinks: failed to resolve links: " & Log.SafeException(ex))
                Return ex.Message
            End Try
            If expanded Is Nothing OrElse expanded.Count = 0 Then
                Return Language.GetText("No valid URLs have been inserted")
            End If
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
                    Dim packageSegment As String = PathGuard.SanitizeFileName(.Nombre, Language.GetText("New package"))
                    Try
                        .RutaLocal = PathGuard.GetSafePathUnderRoot(.RutaLocal, packageSegment, allowRoot:=False)
                    Catch ex As Exception
                        Log.WriteError("ControlRemotoAgregarLinks: invalid package name: " & Log.SafeException(ex))
                        Return Language.GetText("Invalid package name")
                    End Try
                    System.IO.Directory.CreateDirectory(.RutaLocal)
                End If

                Log.WriteWarning("Adding package in " & .RutaLocal)

                .SetDescargaExtraccionAutomatica(Nothing) = Config.ExtraerAutomaticamente
                For Each FileURL As URLProcessor.FileURL In expanded

                    Dim ruta As String = .RutaLocal
                    If Not String.IsNullOrEmpty(FileURL.Path) Then
                        Try
                            ruta = PathGuard.GetSafePathUnderRoot(.RutaLocal, FileURL.Path, allowRoot:=True)
                        Catch ex As Exception
                            Log.WriteError("ControlRemotoAgregarLinks: invalid subfolder path, using package root: " & Log.SafeException(ex))
                            ruta = .RutaLocal
                        End Try
                        System.IO.Directory.CreateDirectory(ruta)
                    End If

                    Dim URLFile As String = FileURL.URL
                    Dim Visible As Boolean = True
                    If Not String.IsNullOrEmpty(URLFile) AndAlso URLFile.Contains(Fichero.HIDDEN_LINK) Then
                        Visible = False
                        URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "")
                    End If

                    Dim oFichero As New Fichero(URLFile)
                    With oFichero
                        .LinkVisible = Visible
                        .RutaLocal = ruta
                        .RutaRelativa = If(FileURL.Path, String.Empty)
                        .NombreFichero = If(Visible, URLFile, Fichero.HIDDEN_LINK_DESC)
                        .FileID = Fichero.ExtraerFileID(URLFile)
                        .FileKey = Fichero.ExtraerFileKey(URLFile)
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
            LeerPortapapelesYAgregarLinks()
        End If
    End Sub

    ''' <summary>
    ''' Reads the clipboard text and pushes it through ComprobarYAgregarLinks.
    ''' Web browsers (Chrome/Edge/Firefox) use DELAYED RENDERING: when the clipboard
    ''' change notification arrives the data has not been written yet, and an
    ''' immediate read returns empty or throws CLIPBRD_E_CANT_OPEN because the
    ''' source process still owns the clipboard. Retry for a short window before
    ''' giving up (fixes "clipboard monitoring misses copies from web pages").
    ''' </summary>
    Private Async Sub LeerPortapapelesYAgregarLinks()
        Const MaxIntentos As Integer = 5
        Const RetrasoMs As Integer = 150

        For intento As Integer = 1 To MaxIntentos
            Dim textoPortapapeles As String = Nothing
            Try
                Dim data As IDataObject = Clipboard.GetDataObject()
                If data IsNot Nothing Then
                    textoPortapapeles = TryCast(data.GetData(GetType(String)), String)
                End If
            Catch ex As System.Runtime.InteropServices.ExternalException
                ' CLIPBRD_E_CANT_OPEN: another process holds the clipboard open — retry shortly
                Log.WriteWarning("Clipboard busy on change notification (attempt " & intento & "): " & Log.SafeException(ex))
            Catch ex As Exception
                Log.WriteWarning("Clipboard read failed (attempt " & intento & "): " & Log.SafeException(ex))
            End Try

            If Not String.IsNullOrEmpty(textoPortapapeles) Then
                ComprobarYAgregarLinks(textoPortapapeles, True, False)
                Return
            End If

            ' Empty read: either delayed rendering (data not materialized yet) or
            ' a non-text copy. Wait briefly and try again before discarding.
            If intento < MaxIntentos Then
                Await Threading.Tasks.Task.Delay(RetrasoMs)
            End If
        Next
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
                Dim quotaSuffix As String = ""
                Try
                    Dim qrem As TimeSpan? = MegaQuotaManager.GetRemaining()
                    If qrem.HasValue Then
                        quotaSuffix = "    " & Language.GetText("Quota_Status").Replace("%T%", FormatQuotaRemaining(qrem.Value))
                    End If
                Catch
                End Try
                Me.StatusToolStripStatusLabel.Text = Language.GetText("Status") & ": " & Estado & velocidad & "    " & Language.GetText("Connection conf") & ": " & configuracionConexiones & quotaSuffix
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
                    ApplyNavRoots()
                    ListaDescargas.BuildList()
                End If

                ListaDescargas.RefreshObjects(CType(ListaDescargas.Roots, Collections.IList))
                UpdateNavCounts()
                UpdateDetailPanel()
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


    ''' <summary>
    ''' P2(审查):空态 hint 承诺"拖入链接",但此前只收 FileDrop(.dlc/.elc),
    ''' 浏览器拖过来的链接文本被静默丢弃;且侧栏子控件 AllowDrop=False 会吞事件。
    ''' 此处补文本分支(复用 ComprobarYAgregarLinks,与剪贴板同一入口),
    ''' 子面板由 EnableLinkDrop 递归打开并转发到这两个处理器(文件逻辑不动)。
    ''' </summary>
    Private Sub EnableLinkDrop(target As Control)
        If target Is Nothing OrElse target.IsDisposed Then Return
        Try
            target.AllowDrop = True
            RemoveHandler target.DragEnter, AddressOf Main_DragEnter
            RemoveHandler target.DragDrop, AddressOf Main_DragDrop
            AddHandler target.DragEnter, AddressOf Main_DragEnter
            AddHandler target.DragDrop, AddressOf Main_DragDrop
            For Each c As Control In target.Controls
                EnableLinkDrop(c)
            Next
        Catch
        End Try
    End Sub

    Private Sub Main_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragDrop
        Dim dropText As String = TryCast(e.Data.GetData(DataFormats.UnicodeText), String)
        If String.IsNullOrEmpty(dropText) Then dropText = TryCast(e.Data.GetData(DataFormats.Text), String)
        If Not String.IsNullOrEmpty(dropText) Then
            ComprobarYAgregarLinks(dropText, True, False)
            Return
        End If
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

                ' ⑥:多个 .elc/.dlc 逐个排队导入,此前 Exit Sub 只进第一个。
                Dim handled As Integer = 0
                For Each Fichero As String In ficheros
                    If Fichero.ToUpper.EndsWith(".DLC") Or Fichero.ToUpper.EndsWith(".ELC") Then
                        AddDLC(Fichero)
                        handled += 1
                    End If
                Next
                If handled > 0 Then Return

                ' 拖入的是其它类型文件:给出提示而不是静默丢弃
                MessageBox.Show(Language.GetText("Only ELC and DLC files are supported by drag and drop"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End If
    End Sub

    Private Sub Main_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter
        If e.Data.GetDataPresent(DataFormats.UnicodeText) OrElse e.Data.GetDataPresent(DataFormats.Text) Then
            e.Effect = DragDropEffects.Copy
            Return
        End If
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
                ' 三选:是=现在更新 / 否=稍后提醒(3小时) / 取消=此版本永不提醒
                Dim Mensaje As String = Language.GetText("New version do you want to download it? Recommended!") & _
                    vbNewLine & vbNewLine & "Version: " & VersionNuevaVersionMegadownloader & vbNewLine & _
                    Language.GetText("Update prompt hint")
                Dim Respuesta As Windows.Forms.DialogResult = MessageBox.Show(Mensaje, _
                                   Language.GetText("New version available"), MessageBoxButtons.YesNoCancel)
                If Respuesta = Windows.Forms.DialogResult.Yes Then
                    btnUpdate_Click(Nothing, Nothing)
                    ' Ya no avisamos más al usuario y dejamos Date.MaxValue
                ElseIf Respuesta = Windows.Forms.DialogResult.No Then
                    ProximoAvisoActualizacion = Now.AddHours(3) ' Cada 3 horas se lo recordamos
                ElseIf Respuesta = Windows.Forms.DialogResult.Cancel Then
                    ' 此版本永不提醒:记录跳过的版本号并保存,新版本仍会正常提醒
                    Config.UpdateSkipVersion = VersionNuevaVersionMegadownloader
                    Config.GuardarXML(False)
                    Log.WriteInfo("User skipped version reminder: " & VersionNuevaVersionMegadownloader)
                    ' 保持 Date.MaxValue,本版本不再弹窗
                Else
                    ProximoAvisoActualizacion = Now.AddHours(3)
                End If
            End If
        End If
    End Sub



    Private Sub DescompresionFinalizada_EventHandler(ByVal Code As String, ByVal Success As Boolean, ByVal ErrorMessage As String)
        Mutex.ListaDescargas.WaitOne()
        Try
            For Each paq As Paquete In Me.ListaPaquetes
                For Each fic As Fichero In paq.ListaFicheros
                    If fic.FileID = Code Then
                        fic.DescompresionFinalizada(Success, ErrorMessage)
                    End If
                Next
            Next
        Finally
            Mutex.ListaDescargas.ReleaseMutex()
        End Try
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
            RetryAllFailedMenuItem.Visible = False
            RemoveAllFailedMenuItem.Visible = False
            If CollectErroneoFiles().Count > 0 Then
                RetryAllFailedMenuItem.Visible = True
                RemoveAllFailedMenuItem.Visible = True
            End If
            ' 批量组隐藏时连带藏起它的分隔线,避免"恢复列宽"上方出现双线。
            sepBatchMenuItem.Visible = RetryAllFailedMenuItem.Visible
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

    ''' <summary>左栏快捷入口:直接复用既有菜单命令的事件处理器,不复制业务逻辑,
    ''' 保证侧栏与菜单两条路径行为完全一致(避免逻辑分叉)。</summary>
    Private Sub navQuickExtractButton_Click(sender As Object, e As EventArgs) Handles navQuickExtractButton.Click
        VerDescompresor_Click(sender, e)
    End Sub

    Private Sub navQuickLibraryButton_Click(sender As Object, e As EventArgs) Handles navQuickLibraryButton.Click
        SeeLibraryManager_Click(sender, e)
    End Sub

    Private Sub navQuickLogsButton_Click(sender As Object, e As EventArgs) Handles navQuickLogsButton.Click
        VerLogs_Click(sender, e)
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
        ' 熔断期点开始此前零反馈(调度器静默不开新任务)。给明确提示,不改状态。
        If MegaQuotaManager.IsQuarantined() Then
            Dim qrem As TimeSpan? = MegaQuotaManager.GetRemaining()
            Dim msg As String = Language.GetText("Quota_Error")
            If qrem.HasValue Then
                msg &= " (" & Language.GetText("Quota_Status").Replace("%T%", FormatQuotaRemaining(qrem.Value)) & ")"
            End If
            ToastForm.ShowToast(Me, msg)
            Return
        End If
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
        ' B3:更新 URL 只允许 https。version.xml 若被投毒/中间人替换,http 明文链接
        ' 会把用户送到钓鱼站;Process.Start 直接打开,必須掐掉降级。
        If Not Me.UrlNuevaVersionMegadownloader.StartsWith("https", StringComparison.OrdinalIgnoreCase) Then
            Log.WriteError("Update URL rejected (not https): " & Log.Redact(Me.UrlNuevaVersionMegadownloader))
            Exit Sub
        End If
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
        ' 未选中任何条目(或条目尚无本地路径)时直接退出,避免弹出误导性的空目录错误
        If String.IsNullOrEmpty(Ruta) Then Exit Sub
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
                        fic.ResetearDescarga()
                        fic.SetDescargaEstado = Estado.EnCola
                    End If
                Next
            ElseIf TypeOf (obj) Is Fichero Then
                Dim fic As Fichero = CType(obj, Fichero)
                If fic.DescargaEstado = Estado.Erroneo Then
                    Log.WriteDebug("Reseting file " & fic.NombreFichero)
                    ' 与包分支保持一致:单文件也必须 ResetearDescarga 清理分块/已下字节/错误描述,
                    ' 否则只改回 EnCola,残留的 .part 与错误状态会让它很快又变回 Erroneo
                    fic.ResetearDescarga()
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
                    If fic.DescargaEstado = Estado.Erroneo AndAlso Not String.IsNullOrWhiteSpace(fic.DescripcionError) Then
                        ht.Add(fic.DescripcionError)
                    End If
                Next
            ElseIf TypeOf (obj) Is Fichero Then
                Dim fic As Fichero = CType(obj, Fichero)
                If fic.DescargaEstado = Estado.Erroneo AndAlso Not String.IsNullOrWhiteSpace(fic.DescripcionError) Then
                    ht.Add(fic.DescripcionError)
                End If

            End If
        Next
        Dim msg As String = ""
        For Each Str As String In ht
            msg &= Str & vbNewLine & vbNewLine
        Next
        msg = msg.Trim
        ' RC:老队列重启后描述为空时不再弹空白窗,给可操作 fallback。
        If String.IsNullOrWhiteSpace(msg) Then
            msg = Language.GetText("Quota_Error")
            If String.IsNullOrWhiteSpace(msg) OrElse msg = "Quota_Error" Then
                msg = "No error details available (e.g. queue saved by an older version). Please retry the download; a fresh error will include details."
            End If
        End If


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
            ' ⑧:红字(Erroneo)此前被拦,点 Force 零反馈。放行并由 ForzarDescarga 重置后强制起。
            If TypeOf (o) Is Fichero _
                AndAlso (CType(o, Fichero).DescargaEstado = Estado.Pausado _
                         Or CType(o, Fichero).DescargaEstado = Estado.EnCola _
                         Or CType(o, Fichero).DescargaEstado = Estado.Erroneo) Then
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
            formsDiscarded.Add(GetType(ToastForm))
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
            ' ⑦:双击/命令行 .elc 此前无分支静默无操作;多 .dlc 只取 [0]。统一排队逐个导入。
            If IO.File.Exists(arg) AndAlso (arg.ToUpper.EndsWith(".DLC") OrElse arg.ToUpper.EndsWith(".ELC")) Then
                DLCList.Add(arg)
            End If
        Next
        If URLlist.Count > 0 Then
            ComprobarYAgregarLinks(String.Join(vbNewLine, URLlist.ToArray), True, False)
        End If
        For Each dlcPath As String In DLCList
            AddDLC(dlcPath)
        Next

    End Sub



#End Region

#Region "Agregar DLCs"

    Private DLCProcessing As Boolean = False ' Indica si ya hay un proceso tratando el DLC
    Private DLCPath As String = String.Empty
    Private DLCResults As Generic.List(Of String) = Nothing
    Private DLCErrorProcessing As Exception = Nothing
    ' ⑥⑦:多文件排队。AddDLC 原来 DLCProcessing=True 时直接丢弃,拖多个/传多个只进第一个。
    Private DLCQueue As New Generic.Queue(Of String)


    Private Sub AddDLC(ByVal DLCFilePath As String)
        ' 用户在文件对话框点了"取消"时传空串:静默退出,不弹"The path is not valid"假错误
        If String.IsNullOrWhiteSpace(DLCFilePath) Then Exit Sub
        DLCQueue.Enqueue(DLCFilePath)
        PumpDLCQueue()
    End Sub

    ''' <summary>⑥⑦:空闲且队列非空时取下一个开工;保证多 .elc/.dlc 逐个导入,不再吞文件。</summary>
    Private Sub PumpDLCQueue()
        If DLCProcessing Then Exit Sub
        If DLCQueue.Count = 0 Then Exit Sub
        DLCProcessing = True
        Dim Thread As New System.Threading.Thread(AddressOf StartProcessDLC)
        DLCPath = DLCQueue.Dequeue()
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
        ' ⑥⑦:当前处理完顺手起下一个排队文件。
        PumpDLCQueue()
    End Sub
    Private Sub StartProcessDLC()
        Dim Thread As New System.Threading.Thread(AddressOf ProcessDLC)
        Thread.IsBackground = True
        Thread.Start()
        Dim d As Action(Of Boolean)

        If Not Thread.Join(New TimeSpan(0, 0, 0, 30)) Then ' 30 seconds timeout
            ' Never call Thread.Abort(): it can interrupt I/O mid-flight and corrupt state.
            ' Mark the result as failed; the worker (a local file read) finishes on its own.
            DLCProcessing = False
            DLCResults = Nothing
            DLCErrorProcessing = New ApplicationException("30s timeout")
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

            If Not System.IO.File.Exists(Path) Then
                Throw New ApplicationException(Language.GetText("The path is not valid"))
            End If

            If Path.ToLower.EndsWith(".elc") Then
                Dim ELC As New System.Text.StringBuilder
                Using t As New System.IO.StreamReader(Path)
                    ELC.Append(t.ReadToEnd())
                End Using
                If ELC.Length = 0 Then
                    Throw New ApplicationException(Language.GetText("ELC file is empty"))
                End If
                DLCResults = New Generic.List(Of String)
                DLCResults.Add("mega://" & URLExtractor.SERVERENCODEDPREFIX & ELC.ToString())
            Else
                ' DLC decryption service (dcrypt.it) has been discontinued.
                Throw New ApplicationException(Language.GetText("DLC format is no longer supported (dcrypt.it service discontinued). Please use .elc files instead."))
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

