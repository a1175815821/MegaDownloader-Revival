Imports System.IO

Public Class Configuration

    Public Config As Configuracion
    Public MainForm As Main
    Public RequiereConfiguracion As Boolean = False

    Public Const PASSWORDDEFECTO As String = "*****"

    Private ListaPreSharedKey As List(Of String)

    Private Sub Configuration_Load(sender As Object, e As System.EventArgs) Handles Me.Load

        ElcAccountControl.Config = Config
        ElcAccountControl.CargarDatos()

        Translate()

        Dim ListaLogs As New Generic.Dictionary(Of String, String)
        ListaLogs(Log.LevelLogType.Minimal.ToString) = Language.GetText("Log_Minimum")
        ListaLogs(Log.LevelLogType.Normal.ToString) = Language.GetText("Log_Normal")
        ListaLogs(Log.LevelLogType.Info.ToString) = Language.GetText("Log_Informative")
        ListaLogs(Log.LevelLogType.Debug.ToString) = Language.GetText("Log_Debug")
        comboLog.DataSource = New BindingSource(ListaLogs, Nothing)
        comboLog.DisplayMember = "Value"
        comboLog.ValueMember = "Key"


        Dim ListaPrioridadExtraccion As New Generic.Dictionary(Of String, String)
        ListaPrioridadExtraccion(SharpCompress.PriorityExtension.Priority.PriorityType.Normal.ToString) = Language.GetText("Priority_Normal")
        ListaPrioridadExtraccion(SharpCompress.PriorityExtension.Priority.PriorityType.Low.ToString) = Language.GetText("Priority_Low")
        comboPrioridad.DataSource = New BindingSource(ListaPrioridadExtraccion, Nothing)
        comboPrioridad.DisplayMember = "Value"
        comboPrioridad.ValueMember = "Key"


        Dim ListaIdiomas As Generic.Dictionary(Of String, String) = Language.GetAvailableLanguages
        comboIdiomas.DataSource = New BindingSource(ListaIdiomas, Nothing)
        comboIdiomas.DisplayMember = "Value"
        comboIdiomas.ValueMember = "Key"


        txtRuta.Text = Config.RutaDefecto
        If String.IsNullOrEmpty(txtRuta.Text) OrElse Not System.IO.Directory.Exists(Config.RutaDefecto) Then
            txtRuta.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        End If
        txtUsuario.Text = Config.Usuario
        txtPassword.Text = PASSWORDDEFECTO

        txtVLCPath.Text = Config.VLCPath
        If String.IsNullOrEmpty(Config.VLCPath) Then
            Try
                Dim registryKey As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\VideoLan\VLC", False)
                If registryKey Is Nothing Then
                    registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\VideoLan\VLC", False)
                End If
                If registryKey IsNot Nothing AndAlso registryKey.GetValue("InstallDir") IsNot Nothing Then
                    Dim VLCInstallDir As Object = registryKey.GetValue("InstallDir")
                    txtVLCPath.Text = CStr(VLCInstallDir)
                End If
            Catch ex As Security.SecurityException
                Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry. Can't read VLC path.")
            End Try
        End If

        chkAnalisisPortapapeles.Checked = Config.AnalizarPortapapeles
        chkCrearDirectorio.Checked = Config.CrearDirectorioPaquete
        chkUnZip.Checked = Config.ExtraerAutomaticamente
        'chkSkins.Checked = Config.PermitirSkins
        chkApagarPC.Checked = Config.ApagarPC
        chkCheckUpdates.Checked = Config.CheckUpdates
        chkUltimaConfig.Checked = Config.MantenerUltimaConfiguracion
        chkComenzarPlay.Checked = Config.ComenzarDescargando
        chkStartWindows.Checked = Config.IniciarConWindows
        comboLog.SelectedValue = Config.NivelLog.ToString
        comboPrioridad.SelectedValue = Config.PrioridadDescompresion.ToString
        comboIdiomas.SelectedValue = Config.Idioma
        If String.IsNullOrEmpty(CStr(comboIdiomas.SelectedValue)) AndAlso comboIdiomas.Items.Count > 0 Then
            comboIdiomas.SelectedIndex = 0
        End If

        chkReintentarError.Checked = Config.ResetearErrores
        If Config.ResetearErrores Then
            txtPeriodoReintento.Text = Config.ResetearErroresPeriodoMinutos.ToString
        End If


        If Config.TamanoPaqueteKB > 0 Then
            txtTamanoPaquete.Text = Config.TamanoPaqueteKB.ToString
        End If
        If Config.TamanoBufferKB > 0 Then
            txtTamanoBuffer.Text = Config.TamanoBufferKB.ToString
        End If

        If Config.ConexionesPorFichero > 0 Then
            txtConFic.Text = Config.ConexionesPorFichero.ToString
        End If
        If Config.DescargasSimultaneas > 0 Then
            txtDescSimult.Text = Config.DescargasSimultaneas.ToString
        End If


        chkLimitarVelocidad.Checked = (Config.LimiteVelocidadKBs > 0)
        txtLimiteVelocidadKBs.Enabled = chkLimitarVelocidad.Checked
        If (Config.LimiteVelocidadKBs > 0) Then
            txtLimiteVelocidadKBs.Text = (Config.LimiteVelocidadKBs / 1024).ToString
        End If

        chkProxy.Checked = Config.UsarProxy
        txtProxyIP.Text = Config.ProxyIP
        txtProxyPort.Text = Config.ProxyPort.ToString
        txtProxyName.Text = Config.ProxyUser
        txtProxyPassword.Text = Config.ProxyPassword
        If Config.ProxyPort = 0 Then txtProxyPort.Text = ""

        txtStreamingPassword.Text = Config.ServidorStreamingPassword
        chkStreamingServer.Checked = Config.ServidorStreamingActivo
        txtStreamingPort.Text = Config.ServidorStreamingPuerto.ToString
        If Config.ServidorStreamingPuerto = 0 Then txtStreamingPort.Text = ""

        chkServidorWeb.Checked = Config.ServidorWebActivo
        txtServidorWebNombre.Text = Config.ServidorWebNombre
        txtServidorWebPassword.Text = Config.ServidorWebPassword
        txtServidorWebPort.Text = Config.ServidorWebPuerto.ToString
        txtServidorWebTemplate.Text = Config.ServidorWebRutaPlantilla
        txtServidorWebTimeout.Text = Config.ServidorWebTimeout.ToString
        If Config.ServidorWebPuerto = 0 Then txtServidorWebPort.Text = ""
        If Config.ServidorWebTimeout < 0 Or Config.ServidorWebTimeout > 60 Then txtServidorWebTimeout.Text = ""

        comboPrioridad.Enabled = chkUnZip.Checked


        Me.txtKeyList.Text = ""
        If Config.ListaPreSharedKeys IsNot Nothing Then
            For Each Str As Security.SecureString In Config.ListaPreSharedKeys
                Me.txtKeyList.Text &= Criptografia.ToInsecureString(Str) & vbNewLine
            Next
        End If

        chkProxy_CheckedChanged(Nothing, Nothing)
        chkServidorWeb_CheckedChanged(Nothing, Nothing)
        chkServidorStreaming_CheckedChanged(Nothing, Nothing)
        chkReintentarError_CheckedChanged(Nothing, Nothing)

    End Sub

    Private Sub Configuration_FormClosed(sender As Object, e As System.Windows.Forms.FormClosedEventArgs) Handles Me.FormClosed
        elcAccountControl.Cerrar()
    End Sub

    Private Sub Translate()
        Me.Text = Language.GetText("Configuration")
        Me.btnCancel.Text = Language.GetText("Cancel")
        Me.btnGuardar.Text = Language.GetText("Save")
        Me.GroupBox1.Text = Language.GetText("User data")
        Me.chkShowPassword.Text = Language.GetText("Show password")
        Me.Label3.Text = Language.GetText("Password") & ":"
        Me.Label24.Text = Language.GetText("Language") & ":"
        Me.Label2.Text = Language.GetText("E-mail") & ":"
        Me.chkComenzarPlay.Text = Language.GetText("Start downloading when application starts")
        Me.chkApagarPC.Text = Language.GetText("Turn off computer when finished")
        Me.Label12.Text = Language.GetText("minutes")
        Me.Label19.Text = Language.GetText("minutes")
        Me.chkReintentarError.Text = Language.GetText("In case of error, retry the download each")
        Me.LinkLabel1.Text = Language.GetText("Important note about connections")
        Me.Label10.Text = Language.GetText("Number of parallel downloads") & ":"
        Me.Label9.Text = Language.GetText("Number of connections per file") & ":"
        Me.chkAnalisisPortapapeles.Text = Language.GetText("Capture links from clipboard")
        Me.chkUnZip.Text = Language.GetText("Automatic extraction") & ". " & Language.GetText("Priority") & ":"
        Me.chkCheckUpdates.Text = Language.GetText("Check for updates")
        Me.chkCrearDirectorio.Text = Language.GetText("Create folder per package")
        Me.btnExaminar.Text = Language.GetText("Browse")
        Me.Label1.Text = Language.GetText("Download path") & ":"
        Me.GroupBox3.Text = Language.GetText("Advanced options")
        Me.Label11.Text = Language.GetText("Log level") & ":"
        Me.Label8.Text = Language.GetText("Note: for advanced users only. Dont touch anything if you arent sure")
        Me.Label6.Text = Language.GetText("Disk buffer size") & ":"
        Me.Label4.Text = Language.GetText("Package size") & ":"
        Me.Label14.Text = Language.GetText("Port") & ":"
        Me.Label16.Text = Language.GetText("Port") & ":"
        Me.Label13.Text = Language.GetText("Proxy address/IP") & ":"
        Me.chkProxy.Text = Language.GetText("Use proxy")
        Me.ConexionGroup.Text = Language.GetText("Connection")
        Me.chkLimitarVelocidad.Text = Language.GetText("Limit speed to")
        Me.GroupBox4.Text = Language.GetText("General options")
        Me.chkUltimaConfig.Text = Language.GetText("Save / use last used configuration")
        Me.chkStartWindows.Text = Language.GetText("Start with Windows")
        Me.TabPage1.Text = Language.GetText("General")
        Me.TabPage2.Text = Language.GetText("Advanced")
        Me.GroupBox5.Text = Language.GetText("Web server")
        Me.btnExaminarTemplate.Text = Language.GetText("Browse")
        Me.Label21.Text = Language.GetText("Template path") & ":"
        Me.Label20.Text = Language.GetText("Name (optional)") & ":"
        Me.Label18.Text = Language.GetText("Interrupt session") & ":"
        Me.Label17.Text = Language.GetText("Password") & ":"

        Me.chkServidorWeb.Text = Language.GetText("Use web server")
        Me.GroupBox2.Text = Language.GetText("Proxy")
        Me.Label23.Text = Language.GetText("User (optional)") & ":"
        Me.Label22.Text = Language.GetText("Password (optional)") & ":"
        Me.TabPage3.Text = Language.GetText("Pre-Shared Keys")
        Me.TabPage4.Text = Language.GetText("Streaming")
        Me.TabPage5.Text = Language.GetText("ELC Accounts")
        Me.lblDesc.Text = Language.GetText("Pre-Shared Key Manager Description")
        Me.GroupBox.Text = Language.GetText("Pre-Shared Key list")


        Me.GroupBox6.Text = Language.GetText("Streaming configuration")
        Me.GroupBox7.Text = Language.GetText("Information")
        Me.lblStreamingPort.Text = Language.GetText("Port") & ":"
        Me.chkStreamingServer.Text = Language.GetText("Use streaming server")
        Me.GroupBox8.Text = Language.GetText("VLC Configuration")
        Me.Label26.Text = Language.GetText("VLC Path") & ":"
        Me.btnExaminarVLCPath.Text = Language.GetText("Browse")
        Me.linkDownloadVLC.Text = Language.GetText("Download VLC")
        Me.lblInfoStreaming.Text = Language.GetText("Streaming configuration text")
        Me.lblStreamingPassword.Text = Language.GetText("Password") & " (" & Language.GetText("optional") & "):"
    End Sub

    Private Sub btnExaminar_Click(sender As System.Object, e As System.EventArgs) Handles btnExaminar.Click

        Dim ExaminarDirectorio As New FolderBrowserDialog
        ExaminarDirectorio.Description = Language.GetText("Select directory")

        If ExaminarDirectorio.ShowDialog = Windows.Forms.DialogResult.OK Then
            txtRuta.Text = ExaminarDirectorio.SelectedPath
        End If

        ExaminarDirectorio.Dispose()
    End Sub


    Private Sub btnCancel_Click(sender As System.Object, e As System.EventArgs) Handles btnCancel.Click
        If RequiereConfiguracion And String.IsNullOrEmpty(txtUsuario.Text) And String.IsNullOrEmpty(txtPassword.Text) Then
            MessageBox.Show(Language.GetText("You must configure user and password"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        Else
            Me.Close()
        End If

    End Sub

    Private Sub btnGuardar_Click(sender As System.Object, e As System.EventArgs) Handles btnGuardar.Click

        If String.IsNullOrEmpty(Config.Password) And txtPassword.Text = PASSWORDDEFECTO Then
            'MessageBox.Show("Debe configurar un usuario y contraseña", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            'Exit Sub
            txtPassword.Text = ""
        End If

        If String.IsNullOrEmpty(txtRuta.Text) Then
            If MessageBox.Show(Language.GetText("Do you want to leave the default path empty?"), Language.GetText("Save"), MessageBoxButtons.YesNo) = DialogResult.No Then
                Exit Sub
            End If
        End If

        Dim TamanoPaquete As Integer = 0
        Dim TamanoBuffer As Integer = 0
        Integer.TryParse(txtTamanoPaquete.Text, TamanoPaquete)
        Integer.TryParse(txtTamanoBuffer.Text, TamanoBuffer)

        If TamanoPaquete = 0 Or TamanoBuffer = 0 Then
            MessageBox.Show(Language.GetText("Disk buffer or package size values are not correct"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        ElseIf TamanoPaquete >= TamanoBuffer Then
            MessageBox.Show(Language.GetText("Disk buffer size must be greater than package size"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim DescSim As Integer = 0
        Dim ConFic As Integer = 0
        Integer.TryParse(txtDescSimult.Text, DescSim)
        Integer.TryParse(txtConFic.Text, ConFic)

        If DescSim <= 0 Or ConFic <= 0 OrElse DescSim * ConFic > 100 Then
            MessageBox.Show(Language.GetText("Connection number values are incorrect or too high"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim PeriodoReintentosError As Integer = 0
        Integer.TryParse(txtPeriodoReintento.Text, PeriodoReintentosError)

        If (PeriodoReintentosError < 1 Or PeriodoReintentosError > 999) Then
            If chkReintentarError.Checked Then
                MessageBox.Show(Language.GetText("Must specify the retry period, and this value must be between %A and %B").Replace("%A", "1").Replace("%B", "999"), _
                                Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Exit Sub
            Else
                PeriodoReintentosError = 15
            End If
        End If

        Dim ProxyPort As Integer = 0
        Integer.TryParse(txtProxyPort.Text, ProxyPort)

        If chkProxy.Checked And (ProxyPort = 0 Or ProxyPort > 65535 Or String.IsNullOrEmpty(txtProxyIP.Text)) Then
            MessageBox.Show(Language.GetText("Invalid proxy configuration"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim LimiteVelocidad As Integer = 0
        Integer.TryParse(txtLimiteVelocidadKBs.Text, LimiteVelocidad)
        If Not chkLimitarVelocidad.Checked Then
            LimiteVelocidad = 0
        ElseIf LimiteVelocidad <= 0 Or String.IsNullOrEmpty(txtLimiteVelocidadKBs.Text) Then
            MessageBox.Show(Language.GetText("Invalid speed limit"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If
        LimiteVelocidad *= 1024

        Dim servidorWebPuerto As Integer = 0
        Integer.TryParse(txtServidorWebPort.Text, servidorWebPuerto)

        Dim servidorStreamingPuerto As Integer = 0
        Integer.TryParse(txtStreamingPort.Text, servidorStreamingPuerto)

        Dim servidorWebTimeout As Integer = 5
        Integer.TryParse(txtServidorWebTimeout.Text, servidorWebTimeout)

        If Not chkServidorWeb.Checked Then
            If servidorWebPuerto < 1024 Or servidorWebPuerto > 65535 Then
                servidorWebPuerto = 0
            End If
            If servidorWebTimeout < 0 Or servidorWebTimeout > 99 Then
                servidorWebTimeout = 5
            End If
        ElseIf servidorWebPuerto < 1024 Or servidorWebPuerto > 65535 Or String.IsNullOrEmpty(txtServidorWebPort.Text) Then
            MessageBox.Show(Language.GetText("Invalid server port"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        ElseIf servidorWebTimeout < 0 Or servidorWebTimeout > 99 Or String.IsNullOrEmpty(txtServidorWebTimeout.Text) Then
            MessageBox.Show(Language.GetText("The value for interrumpting the session must be between 0 and %A. A value of 0 means that session is not interrupted").Replace("%A", "99"), _
                            Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        ElseIf Not String.IsNullOrEmpty(txtServidorWebTemplate.Text) AndAlso Not IO.File.Exists(txtServidorWebTemplate.Text) Then
            MessageBox.Show(Language.GetText("The template path is not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        ElseIf String.IsNullOrEmpty(txtServidorWebPassword.Text) AndAlso txtServidorWebPassword.Text.Length < 8 Then
            MessageBox.Show(Language.GetText("The web server password must have at least %A characters").Replace("%A", "8"), _
                            Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        If Not chkStreamingServer.Checked Then
            If servidorStreamingPuerto < 1024 Or servidorStreamingPuerto > 65535 Then
                servidorStreamingPuerto = 0
            End If
        ElseIf servidorStreamingPuerto < 1024 Or servidorStreamingPuerto > 65535 Or String.IsNullOrEmpty(txtStreamingPort.Text) Then
            MessageBox.Show(Language.GetText("Invalid server port"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If


        If chkServidorWeb.Checked And chkStreamingServer.Checked And servidorWebPuerto = servidorStreamingPuerto Then
            MessageBox.Show(Language.GetText("Web server and streaming server ports must be different"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If


        If txtPassword.Text <> PASSWORDDEFECTO Then
            'If txtPassword.Text.Length <> 35 Then
            '    MessageBox.Show("API Key no válida. Haga click en [?] para ver la página donde obtener la API Key.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            '    Exit Sub
            'End If
            Config.Password = txtPassword.Text
        End If

        Config.VLCPath = txtVLCPath.Text
        Config.Usuario = txtUsuario.Text
        Config.RutaDefecto = txtRuta.Text
        Config.ExtraerAutomaticamente = chkUnZip.Checked
        Config.AnalizarPortapapeles = chkAnalisisPortapapeles.Checked
        Config.CrearDirectorioPaquete = chkCrearDirectorio.Checked
        Config.ComenzarDescargando = chkComenzarPlay.Checked
        Config.TamanoBufferKB = TamanoBuffer
        Config.TamanoPaqueteKB = TamanoPaquete
        Config.ConexionesPorFichero = ConFic
        Config.DescargasSimultaneas = DescSim
        Config.ResetearErrores = chkReintentarError.Checked
        Config.ApagarPC = chkApagarPC.Checked
        Config.CheckUpdates = chkCheckUpdates.Checked
        Config.MantenerUltimaConfiguracion = chkUltimaConfig.Checked
        Config.IniciarConWindows = chkStartWindows.Checked
        Config.ResetearErroresPeriodoMinutos = PeriodoReintentosError
        Config.UsarProxy = chkProxy.Checked
        Config.ProxyPassword = txtProxyPassword.Text
        Config.ProxyUser = txtProxyName.Text
        Config.ProxyPort = ProxyPort
        Config.ProxyIP = txtProxyIP.Text
        Config.LimiteVelocidadKBs = LimiteVelocidad

        Config.ServidorStreamingActivo = chkStreamingServer.Checked
        Config.ServidorStreamingPuerto = servidorStreamingPuerto
        Config.ServidorStreamingPassword = txtStreamingPassword.Text


        Config.ServidorWebActivo = chkServidorWeb.Checked
        Config.ServidorWebNombre = txtServidorWebNombre.Text
        Config.ServidorWebPassword = txtServidorWebPassword.Text
        Config.ServidorWebPuerto = servidorWebPuerto
        Config.ServidorWebRutaPlantilla = txtServidorWebTemplate.Text
        Config.ServidorWebTimeout = servidorWebTimeout
        ElcAccountControl.SaveToConfig(Config)

        Dim AvisoReiniciarAppParaIdioma As Boolean = False
        If Language.IsValidLanguageCode(CStr(comboIdiomas.SelectedValue)) Then
            AvisoReiniciarAppParaIdioma = Config.Idioma <> CStr(comboIdiomas.SelectedValue)
            Config.Idioma = CStr(comboIdiomas.SelectedValue)
        End If


        If [Enum].IsDefined(GetType(Log.LevelLogType), comboLog.SelectedValue) Then
            Config.NivelLog = CType([Enum].Parse(GetType(Log.LevelLogType), CType(comboLog.SelectedItem, KeyValuePair(Of String, String)).Key), Log.LevelLogType)
            Log.SetLogLevel = Config.NivelLog
        End If

        If [Enum].IsDefined(GetType(SharpCompress.PriorityExtension.Priority.PriorityType), comboPrioridad.SelectedValue) Then
            Config.PrioridadDescompresion = CType([Enum].Parse(GetType(SharpCompress.PriorityExtension.Priority.PriorityType), CType(comboPrioridad.SelectedItem, KeyValuePair(Of String, String)).Key), SharpCompress.PriorityExtension.Priority.PriorityType)
            SharpCompress.PriorityExtension.Priority.DecompressionPriority = Config.PrioridadDescompresion
        End If

        Dim ListaSharedKeys As New List(Of Security.SecureString)
        Dim ListadoSharedKeysStr As String = Me.txtKeyList.Text
        If Not String.IsNullOrEmpty(ListadoSharedKeysStr) Then
            For Each Key As String In ListadoSharedKeysStr.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
                If Not String.IsNullOrEmpty(Key) Then
                    ListaSharedKeys.Add(Criptografia.ToSecureString(Key))
                End If
            Next
        End If
        Config.ListaPreSharedKeys = ListaSharedKeys

        Config.GuardarXML(True)

        Conexion.SetProxy(Config)
        Configuracion.RegisterInStartup(Config.IniciarConWindows)

        ThrottledStreamController.GetController.SetMaxGlobalSpeed(Config.LimiteVelocidadKBs)

        Main.Config = Config
        Main.NecesitaCambiarUsuarioYPassword = False


        ' Reiniciamos el servidor 
        ServidorWebController.StopWebServer()
        Dim strErrServidorWeb As String = ServidorWebController.StartWebServer(Main, Me.Config)
        If Not String.IsNullOrEmpty(strErrServidorWeb) Then
            MessageBox.Show(Language.GetText("Error starting web server") & ": " & strErrServidorWeb, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim AvisoUsuarioAnonimo As Boolean = String.IsNullOrEmpty(txtUsuario.Text) And String.IsNullOrEmpty(txtPassword.Text)
        Dim AvisoUsuarioRegistrado As Boolean = Not String.IsNullOrEmpty(txtUsuario.Text) And Not String.IsNullOrEmpty(txtPassword.Text)

        Dim avisos As String = ""
        If AvisoReiniciarAppParaIdioma Then
            avisos &= vbNewLine & " * " & Language.GetText("You have to restart the application for the language change to take effect")
        End If
        If AvisoUsuarioAnonimo Then
            ' Cuando sirva el user/password para descargar, mostraremos este mensaje
            'avisos &= vbNewLine & " * " & Language.GetText("If no user and password is specified, downloads will be made as an anonymous user")
        End If
        If AvisoUsuarioRegistrado Then
            avisos &= vbNewLine & " * " & Language.GetText("User and password is not used yet")
        End If

        If Not String.IsNullOrEmpty(avisos) Then
            avisos = vbNewLine & vbNewLine & Language.GetText("Please take into consideration the following") & ": " & avisos
        End If

        MessageBox.Show(Language.GetText("Data saved successfully") & avisos, Language.GetText("Save"), MessageBoxButtons.OK, MessageBoxIcon.Information)


        Me.Close()

    End Sub

    Private t As ToolTip
    Private Function MsgMaxConexiones() As String
        Return Language.GetText("Number of connection explanation")
    End Function
                        

    Private Sub LinkLabel1_MouseHover(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseHover
        t = New ToolTip
        t.SetToolTip(LinkLabel1, MsgMaxConexiones)
    End Sub

    Private Sub LinkLabel1_MouseLeave(sender As Object, e As System.EventArgs) Handles LinkLabel1.MouseLeave
        If t IsNot Nothing Then t.Hide(LinkLabel1)
    End Sub

    Private Sub LinkLabel1_Click(sender As Object, e As System.EventArgs) Handles LinkLabel1.Click
        MessageBox.Show(MsgMaxConexiones, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub


    Private Sub chkShowPassword_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkShowPassword.CheckedChanged
        txtPassword.UseSystemPasswordChar = Not chkShowPassword.Checked
    End Sub

    Private Sub chkProxy_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkProxy.CheckedChanged
        txtProxyIP.Enabled = chkProxy.Checked
        txtProxyPort.Enabled = chkProxy.Checked
        txtProxyName.Enabled = chkProxy.Checked
        txtProxyPassword.Enabled = chkProxy.Checked
    End Sub

    Private Sub chkServidorWeb_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkServidorWeb.CheckedChanged
        txtServidorWebNombre.Enabled = chkServidorWeb.Checked
        txtServidorWebPassword.Enabled = chkServidorWeb.Checked
        txtServidorWebPort.Enabled = chkServidorWeb.Checked
        txtServidorWebTemplate.Enabled = chkServidorWeb.Checked
        txtServidorWebTimeout.Enabled = chkServidorWeb.Checked
        btnExaminarTemplate.Enabled = chkServidorWeb.Checked
    End Sub

    Private Sub chkServidorStreaming_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkStreamingServer.CheckedChanged
        txtStreamingPort.Enabled = chkStreamingServer.Checked
    End Sub

    Private Function MsgApagarPC() As String
        Return Language.GetText("Turn off PC explanation")
    End Function


    Private Sub linkApagarPC_MouseHover(sender As Object, e As System.EventArgs) Handles linkApagarPC.MouseHover
        t = New ToolTip
        t.SetToolTip(linkApagarPC, MsgApagarPC)
    End Sub

    Private Sub linkApagarPC_MouseLeave(sender As Object, e As System.EventArgs) Handles linkApagarPC.MouseLeave
        If t IsNot Nothing Then t.Hide(linkApagarPC)
    End Sub

    Private Sub linkApagarPC_Click(sender As Object, e As System.EventArgs) Handles linkApagarPC.Click
        MessageBox.Show(MsgApagarPC, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub chkLimitarVelocidad_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkLimitarVelocidad.CheckedChanged
        txtLimiteVelocidadKBs.Enabled = chkLimitarVelocidad.Checked
    End Sub

    Private Sub btnVerDescompresor_Click(sender As System.Object, e As System.EventArgs)
        If Main.IsFormAlreadyOpen(GetType(Descompresor)) Is Nothing Then
            Dim frmName As New Descompresor
            frmName.Show()
        End If
    End Sub

    Private Sub chkUnZip_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkUnZip.CheckedChanged
        comboPrioridad.Enabled = chkUnZip.Checked
    End Sub




    Private Function MsgUltConfig() As String
        Return Language.GetText("Save and use last conf explanation")
    End Function

    Private Sub linkUltConfig_MouseHover(sender As Object, e As System.EventArgs) Handles linkUltConfig.MouseHover
        t = New ToolTip
        t.SetToolTip(linkUltConfig, MsgUltConfig)
    End Sub

    Private Sub linkUltConfig_MouseLeave(sender As Object, e As System.EventArgs) Handles linkUltConfig.MouseLeave
        If t IsNot Nothing Then t.Hide(linkUltConfig)
    End Sub

    Private Sub linkUltConfig_Click(sender As Object, e As System.EventArgs) Handles linkUltConfig.Click
        MessageBox.Show(MsgUltConfig, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub btnExaminarTemplate_Click(sender As System.Object, e As System.EventArgs) Handles btnExaminarTemplate.Click
        Dim Examinar As New OpenFileDialog
        Examinar.CheckFileExists = True
        Examinar.DefaultExt = "xml"
        Examinar.Filter = Language.GetText("Templates") & " (*.xml)|*.xml"
        Examinar.Multiselect = False

        If Examinar.ShowDialog = Windows.Forms.DialogResult.OK Then
            txtServidorWebTemplate.Text = Examinar.FileName
        End If

        Examinar.Dispose()
    End Sub

    Private Sub txtServidorWebTemplate_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles txtServidorWebTemplate.DragDrop
        Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
        For Each Fichero As String In ficheros
            If Fichero.ToUpper.EndsWith(".XML") Then
                txtServidorWebTemplate.Text = Fichero
            End If
        Next
    End Sub

    Private Sub txtServidorWebTemplate_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles txtServidorWebTemplate.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
            For Each Fichero As String In ficheros
                If Fichero.ToUpper.EndsWith(".XML") Then
                    e.Effect = DragDropEffects.Copy
                End If
            Next
        End If
    End Sub

    Private Sub HelpButtonPressed() Handles Me.HelpButtonClicked
        Main.FAQ_Click(Nothing, Nothing)
    End Sub

    Private Sub linkDownloadVLC_LinkClicked(sender As System.Object, e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles linkDownloadVLC.LinkClicked
        System.Diagnostics.Process.Start("http://www.videolan.org/vlc/")
    End Sub

    Private Sub btnExaminarVLCPath_Click(sender As System.Object, e As System.EventArgs) Handles btnExaminarVLCPath.Click

        Dim ExaminarDirectorio As New FolderBrowserDialog
        ExaminarDirectorio.Description = Language.GetText("Select directory")

        If ExaminarDirectorio.ShowDialog = Windows.Forms.DialogResult.OK Then
            txtVLCPath.Text = ExaminarDirectorio.SelectedPath
        End If

        ExaminarDirectorio.Dispose()
    End Sub

    Private Sub chkReintentarError_CheckedChanged(sender As Object, e As EventArgs) Handles chkReintentarError.CheckedChanged
        txtPeriodoReintento.Enabled = chkReintentarError.Checked
    End Sub

End Class