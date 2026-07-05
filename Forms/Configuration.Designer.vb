<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Configuration
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Configuration))
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.chkShowPassword = New System.Windows.Forms.CheckBox()
        Me.txtPassword = New System.Windows.Forms.TextBox()
        Me.txtUsuario = New System.Windows.Forms.TextBox()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.chkComenzarPlay = New System.Windows.Forms.CheckBox()
        Me.linkApagarPC = New System.Windows.Forms.LinkLabel()
        Me.chkApagarPC = New System.Windows.Forms.CheckBox()
        Me.Label12 = New System.Windows.Forms.Label()
        Me.txtPeriodoReintento = New System.Windows.Forms.TextBox()
        Me.chkReintentarError = New System.Windows.Forms.CheckBox()
        Me.LinkLabel1 = New System.Windows.Forms.LinkLabel()
        Me.txtDescSimult = New System.Windows.Forms.TextBox()
        Me.Label10 = New System.Windows.Forms.Label()
        Me.txtConFic = New System.Windows.Forms.TextBox()
        Me.Label9 = New System.Windows.Forms.Label()
        Me.chkAnalisisPortapapeles = New System.Windows.Forms.CheckBox()
        Me.chkUnZip = New System.Windows.Forms.CheckBox()
        Me.chkCrearDirectorio = New System.Windows.Forms.CheckBox()
        Me.btnExaminar = New System.Windows.Forms.Button()
        Me.txtRuta = New System.Windows.Forms.TextBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.btnGuardar = New System.Windows.Forms.Button()
        Me.GroupBox3 = New System.Windows.Forms.GroupBox()
        Me.comboLog = New System.Windows.Forms.ComboBox()
        Me.Label11 = New System.Windows.Forms.Label()
        Me.Label8 = New System.Windows.Forms.Label()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.txtTamanoBuffer = New System.Windows.Forms.TextBox()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.txtTamanoPaquete = New System.Windows.Forms.TextBox()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.txtProxyPort = New System.Windows.Forms.TextBox()
        Me.Label14 = New System.Windows.Forms.Label()
        Me.txtProxyIP = New System.Windows.Forms.TextBox()
        Me.Label13 = New System.Windows.Forms.Label()
        Me.chkProxy = New System.Windows.Forms.CheckBox()
        Me.ConexionGroup = New System.Windows.Forms.GroupBox()
        Me.Label15 = New System.Windows.Forms.Label()
        Me.txtLimiteVelocidadKBs = New System.Windows.Forms.TextBox()
        Me.chkLimitarVelocidad = New System.Windows.Forms.CheckBox()
        Me.GroupBox4 = New System.Windows.Forms.GroupBox()
        Me.chkCheckUpdates = New System.Windows.Forms.CheckBox()
        Me.comboIdiomas = New System.Windows.Forms.ComboBox()
        Me.Label24 = New System.Windows.Forms.Label()
        Me.linkUltConfig = New System.Windows.Forms.LinkLabel()
        Me.chkUltimaConfig = New System.Windows.Forms.CheckBox()
        Me.comboPrioridad = New System.Windows.Forms.ComboBox()
        Me.chkStartWindows = New System.Windows.Forms.CheckBox()
        Me.TabControl1 = New System.Windows.Forms.TabControl()
        Me.TabPage1 = New System.Windows.Forms.TabPage()
        Me.TabPage2 = New System.Windows.Forms.TabPage()
        Me.GroupBox5 = New System.Windows.Forms.GroupBox()
        Me.btnExaminarTemplate = New System.Windows.Forms.Button()
        Me.txtServidorWebTemplate = New System.Windows.Forms.TextBox()
        Me.Label21 = New System.Windows.Forms.Label()
        Me.txtServidorWebNombre = New System.Windows.Forms.TextBox()
        Me.Label20 = New System.Windows.Forms.Label()
        Me.Label19 = New System.Windows.Forms.Label()
        Me.txtServidorWebTimeout = New System.Windows.Forms.TextBox()
        Me.Label18 = New System.Windows.Forms.Label()
        Me.Label17 = New System.Windows.Forms.Label()
        Me.txtServidorWebPassword = New System.Windows.Forms.TextBox()
        Me.txtServidorWebPort = New System.Windows.Forms.TextBox()
        Me.Label16 = New System.Windows.Forms.Label()
        Me.chkServidorWeb = New System.Windows.Forms.CheckBox()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.Label23 = New System.Windows.Forms.Label()
        Me.txtProxyName = New System.Windows.Forms.TextBox()
        Me.txtProxyPassword = New System.Windows.Forms.TextBox()
        Me.Label22 = New System.Windows.Forms.Label()
        Me.TabPage3 = New System.Windows.Forms.TabPage()
        Me.GroupBox = New System.Windows.Forms.GroupBox()
        Me.txtKeyList = New System.Windows.Forms.TextBox()
        Me.lblDesc = New System.Windows.Forms.Label()
        Me.TabPage4 = New System.Windows.Forms.TabPage()
        Me.GroupBox8 = New System.Windows.Forms.GroupBox()
        Me.btnExaminarVLCPath = New System.Windows.Forms.Button()
        Me.linkDownloadVLC = New System.Windows.Forms.LinkLabel()
        Me.txtVLCPath = New System.Windows.Forms.TextBox()
        Me.Label26 = New System.Windows.Forms.Label()
        Me.GroupBox7 = New System.Windows.Forms.GroupBox()
        Me.lblInfoStreaming = New System.Windows.Forms.Label()
        Me.GroupBox6 = New System.Windows.Forms.GroupBox()
        Me.txtStreamingPassword = New System.Windows.Forms.TextBox()
        Me.lblStreamingPassword = New System.Windows.Forms.Label()
        Me.txtStreamingPort = New System.Windows.Forms.TextBox()
        Me.lblStreamingPort = New System.Windows.Forms.Label()
        Me.chkStreamingServer = New System.Windows.Forms.CheckBox()
        Me.TabPage5 = New System.Windows.Forms.TabPage()
        Me.ElcAccountControl = New MegaDownloader.ELCAccountControl()
        Me.GroupBox1.SuspendLayout()
        Me.GroupBox3.SuspendLayout()
        Me.ConexionGroup.SuspendLayout()
        Me.GroupBox4.SuspendLayout()
        Me.TabControl1.SuspendLayout()
        Me.TabPage1.SuspendLayout()
        Me.TabPage2.SuspendLayout()
        Me.GroupBox5.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        Me.TabPage3.SuspendLayout()
        Me.GroupBox.SuspendLayout()
        Me.TabPage4.SuspendLayout()
        Me.GroupBox8.SuspendLayout()
        Me.GroupBox7.SuspendLayout()
        Me.GroupBox6.SuspendLayout()
        Me.TabPage5.SuspendLayout()
        Me.SuspendLayout()
        '
        'GroupBox1
        '
        Me.GroupBox1.Controls.Add(Me.chkShowPassword)
        Me.GroupBox1.Controls.Add(Me.txtPassword)
        Me.GroupBox1.Controls.Add(Me.txtUsuario)
        Me.GroupBox1.Controls.Add(Me.Label3)
        Me.GroupBox1.Controls.Add(Me.Label2)
        Me.GroupBox1.Location = New System.Drawing.Point(6, 6)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Size = New System.Drawing.Size(599, 62)
        Me.GroupBox1.TabIndex = 0
        Me.GroupBox1.TabStop = False
        Me.GroupBox1.Text = "Datos de usuario (opcionales)"
        '
        'chkShowPassword
        '
        Me.chkShowPassword.AutoSize = True
        Me.chkShowPassword.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkShowPassword.Location = New System.Drawing.Point(468, 30)
        Me.chkShowPassword.Name = "chkShowPassword"
        Me.chkShowPassword.Size = New System.Drawing.Size(117, 17)
        Me.chkShowPassword.TabIndex = 4
        Me.chkShowPassword.Text = "Mostrar contraseña"
        Me.chkShowPassword.UseVisualStyleBackColor = False
        '
        'txtPassword
        '
        Me.txtPassword.Location = New System.Drawing.Point(302, 28)
        Me.txtPassword.Name = "txtPassword"
        Me.txtPassword.Size = New System.Drawing.Size(156, 20)
        Me.txtPassword.TabIndex = 3
        Me.txtPassword.UseSystemPasswordChar = True
        '
        'txtUsuario
        '
        Me.txtUsuario.Location = New System.Drawing.Point(67, 28)
        Me.txtUsuario.Name = "txtUsuario"
        Me.txtUsuario.Size = New System.Drawing.Size(140, 20)
        Me.txtUsuario.TabIndex = 1
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label3.Location = New System.Drawing.Point(216, 31)
        Me.Label3.MinimumSize = New System.Drawing.Size(80, 0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(80, 13)
        Me.Label3.TabIndex = 2
        Me.Label3.Text = "Contraseña:"
        Me.Label3.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label2.Location = New System.Drawing.Point(9, 31)
        Me.Label2.MinimumSize = New System.Drawing.Size(55, 0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(55, 13)
        Me.Label2.TabIndex = 0
        Me.Label2.Text = "Usuario:"
        Me.Label2.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkComenzarPlay
        '
        Me.chkComenzarPlay.AutoSize = True
        Me.chkComenzarPlay.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkComenzarPlay.Location = New System.Drawing.Point(10, 81)
        Me.chkComenzarPlay.Name = "chkComenzarPlay"
        Me.chkComenzarPlay.Size = New System.Drawing.Size(282, 17)
        Me.chkComenzarPlay.TabIndex = 5
        Me.chkComenzarPlay.Text = "Comenzar a descargar en cuanto se inicie el programa"
        Me.chkComenzarPlay.UseVisualStyleBackColor = True
        '
        'linkApagarPC
        '
        Me.linkApagarPC.AutoSize = True
        Me.linkApagarPC.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.linkApagarPC.Location = New System.Drawing.Point(563, 82)
        Me.linkApagarPC.Name = "linkApagarPC"
        Me.linkApagarPC.Size = New System.Drawing.Size(19, 13)
        Me.linkApagarPC.TabIndex = 7
        Me.linkApagarPC.TabStop = True
        Me.linkApagarPC.Text = "[?]"
        '
        'chkApagarPC
        '
        Me.chkApagarPC.AutoSize = True
        Me.chkApagarPC.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkApagarPC.Location = New System.Drawing.Point(374, 81)
        Me.chkApagarPC.Name = "chkApagarPC"
        Me.chkApagarPC.Size = New System.Drawing.Size(178, 17)
        Me.chkApagarPC.TabIndex = 6
        Me.chkApagarPC.Text = "Apagar PC al finalizar descargas"
        Me.chkApagarPC.UseVisualStyleBackColor = True
        '
        'Label12
        '
        Me.Label12.AutoSize = True
        Me.Label12.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label12.Location = New System.Drawing.Point(283, 61)
        Me.Label12.Name = "Label12"
        Me.Label12.Size = New System.Drawing.Size(43, 13)
        Me.Label12.TabIndex = 7
        Me.Label12.Text = "minutos"
        '
        'txtPeriodoReintento
        '
        Me.txtPeriodoReintento.Location = New System.Drawing.Point(247, 58)
        Me.txtPeriodoReintento.MaxLength = 3
        Me.txtPeriodoReintento.Name = "txtPeriodoReintento"
        Me.txtPeriodoReintento.Size = New System.Drawing.Size(30, 20)
        Me.txtPeriodoReintento.TabIndex = 6
        '
        'chkReintentarError
        '
        Me.chkReintentarError.AutoSize = True
        Me.chkReintentarError.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkReintentarError.Location = New System.Drawing.Point(9, 60)
        Me.chkReintentarError.Name = "chkReintentarError"
        Me.chkReintentarError.Size = New System.Drawing.Size(239, 17)
        Me.chkReintentarError.TabIndex = 5
        Me.chkReintentarError.Text = "En caso de error, reintentar la descarga cada"
        Me.chkReintentarError.UseVisualStyleBackColor = True
        '
        'LinkLabel1
        '
        Me.LinkLabel1.AutoSize = True
        Me.LinkLabel1.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.LinkLabel1.Location = New System.Drawing.Point(350, 91)
        Me.LinkLabel1.MinimumSize = New System.Drawing.Size(230, 0)
        Me.LinkLabel1.Name = "LinkLabel1"
        Me.LinkLabel1.Size = New System.Drawing.Size(232, 13)
        Me.LinkLabel1.TabIndex = 10
        Me.LinkLabel1.TabStop = True
        Me.LinkLabel1.Text = "Nota importante sobre el número de conexiones"
        Me.LinkLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtDescSimult
        '
        Me.txtDescSimult.Location = New System.Drawing.Point(550, 26)
        Me.txtDescSimult.MaxLength = 2
        Me.txtDescSimult.Name = "txtDescSimult"
        Me.txtDescSimult.Size = New System.Drawing.Size(30, 20)
        Me.txtDescSimult.TabIndex = 4
        Me.txtDescSimult.Text = "3"
        '
        'Label10
        '
        Me.Label10.AutoSize = True
        Me.Label10.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label10.Location = New System.Drawing.Point(344, 29)
        Me.Label10.MinimumSize = New System.Drawing.Size(200, 0)
        Me.Label10.Name = "Label10"
        Me.Label10.Size = New System.Drawing.Size(200, 13)
        Me.Label10.TabIndex = 3
        Me.Label10.Text = "Número de descargas simultáneas:"
        Me.Label10.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtConFic
        '
        Me.txtConFic.Location = New System.Drawing.Point(550, 57)
        Me.txtConFic.MaxLength = 2
        Me.txtConFic.Name = "txtConFic"
        Me.txtConFic.Size = New System.Drawing.Size(30, 20)
        Me.txtConFic.TabIndex = 9
        Me.txtConFic.Text = "2"
        '
        'Label9
        '
        Me.Label9.AutoSize = True
        Me.Label9.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label9.Location = New System.Drawing.Point(344, 60)
        Me.Label9.MinimumSize = New System.Drawing.Size(200, 0)
        Me.Label9.Name = "Label9"
        Me.Label9.Size = New System.Drawing.Size(200, 13)
        Me.Label9.TabIndex = 8
        Me.Label9.Text = "Número de conexiones por fichero:"
        Me.Label9.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkAnalisisPortapapeles
        '
        Me.chkAnalisisPortapapeles.AutoSize = True
        Me.chkAnalisisPortapapeles.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkAnalisisPortapapeles.Location = New System.Drawing.Point(10, 58)
        Me.chkAnalisisPortapapeles.Name = "chkAnalisisPortapapeles"
        Me.chkAnalisisPortapapeles.Size = New System.Drawing.Size(171, 17)
        Me.chkAnalisisPortapapeles.TabIndex = 3
        Me.chkAnalisisPortapapeles.Text = "Capturar links del portapapeles"
        Me.chkAnalisisPortapapeles.UseVisualStyleBackColor = True
        '
        'chkUnZip
        '
        Me.chkUnZip.AutoSize = True
        Me.chkUnZip.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkUnZip.Location = New System.Drawing.Point(10, 127)
        Me.chkUnZip.Name = "chkUnZip"
        Me.chkUnZip.Size = New System.Drawing.Size(181, 17)
        Me.chkUnZip.TabIndex = 11
        Me.chkUnZip.Text = "Extracción automática. Prioridad:"
        Me.chkUnZip.UseVisualStyleBackColor = True
        '
        'chkCrearDirectorio
        '
        Me.chkCrearDirectorio.AutoSize = True
        Me.chkCrearDirectorio.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkCrearDirectorio.Location = New System.Drawing.Point(374, 58)
        Me.chkCrearDirectorio.Name = "chkCrearDirectorio"
        Me.chkCrearDirectorio.Size = New System.Drawing.Size(157, 17)
        Me.chkCrearDirectorio.TabIndex = 4
        Me.chkCrearDirectorio.Text = "Crear directorio por paquete"
        Me.chkCrearDirectorio.UseVisualStyleBackColor = True
        '
        'btnExaminar
        '
        Me.btnExaminar.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.btnExaminar.Location = New System.Drawing.Point(507, 27)
        Me.btnExaminar.Name = "btnExaminar"
        Me.btnExaminar.Size = New System.Drawing.Size(75, 23)
        Me.btnExaminar.TabIndex = 2
        Me.btnExaminar.Text = "Examinar"
        Me.btnExaminar.UseVisualStyleBackColor = True
        '
        'txtRuta
        '
        Me.txtRuta.Location = New System.Drawing.Point(97, 29)
        Me.txtRuta.Name = "txtRuta"
        Me.txtRuta.Size = New System.Drawing.Size(404, 20)
        Me.txtRuta.TabIndex = 1
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label1.Location = New System.Drawing.Point(6, 32)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(85, 13)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "Ruta descargas:"
        Me.Label1.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'btnCancel
        '
        Me.btnCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.btnCancel.Location = New System.Drawing.Point(556, 432)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(75, 23)
        Me.btnCancel.TabIndex = 1
        Me.btnCancel.Text = "Cancelar"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'btnGuardar
        '
        Me.btnGuardar.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.btnGuardar.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.btnGuardar.Location = New System.Drawing.Point(9, 432)
        Me.btnGuardar.Name = "btnGuardar"
        Me.btnGuardar.Size = New System.Drawing.Size(75, 23)
        Me.btnGuardar.TabIndex = 0
        Me.btnGuardar.Text = "Guardar"
        Me.btnGuardar.UseVisualStyleBackColor = True
        '
        'GroupBox3
        '
        Me.GroupBox3.Controls.Add(Me.comboLog)
        Me.GroupBox3.Controls.Add(Me.Label11)
        Me.GroupBox3.Controls.Add(Me.Label8)
        Me.GroupBox3.Controls.Add(Me.Label7)
        Me.GroupBox3.Controls.Add(Me.txtTamanoBuffer)
        Me.GroupBox3.Controls.Add(Me.Label6)
        Me.GroupBox3.Controls.Add(Me.Label5)
        Me.GroupBox3.Controls.Add(Me.txtTamanoPaquete)
        Me.GroupBox3.Controls.Add(Me.Label4)
        Me.GroupBox3.Location = New System.Drawing.Point(6, 6)
        Me.GroupBox3.Name = "GroupBox3"
        Me.GroupBox3.Size = New System.Drawing.Size(599, 107)
        Me.GroupBox3.TabIndex = 0
        Me.GroupBox3.TabStop = False
        Me.GroupBox3.Text = "Opciones avanzadas"
        '
        'comboLog
        '
        Me.comboLog.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.comboLog.FormattingEnabled = True
        Me.comboLog.Location = New System.Drawing.Point(432, 52)
        Me.comboLog.Name = "comboLog"
        Me.comboLog.Size = New System.Drawing.Size(153, 21)
        Me.comboLog.TabIndex = 5
        '
        'Label11
        '
        Me.Label11.AutoSize = True
        Me.Label11.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label11.Location = New System.Drawing.Point(306, 55)
        Me.Label11.MinimumSize = New System.Drawing.Size(120, 0)
        Me.Label11.Name = "Label11"
        Me.Label11.Size = New System.Drawing.Size(120, 13)
        Me.Label11.TabIndex = 4
        Me.Label11.Text = "Nivel de logs:"
        Me.Label11.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'Label8
        '
        Me.Label8.AutoSize = True
        Me.Label8.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Italic)
        Me.Label8.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label8.Location = New System.Drawing.Point(11, 27)
        Me.Label8.Name = "Label8"
        Me.Label8.Size = New System.Drawing.Size(397, 13)
        Me.Label8.TabIndex = 0
        Me.Label8.Text = "Nota: Para usuarios avanzados. Si no estás seguro de que poner, no toques nada."
        '
        'Label7
        '
        Me.Label7.AutoSize = True
        Me.Label7.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label7.Location = New System.Drawing.Point(205, 81)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(21, 13)
        Me.Label7.TabIndex = 8
        Me.Label7.Text = "KB"
        '
        'txtTamanoBuffer
        '
        Me.txtTamanoBuffer.Location = New System.Drawing.Point(167, 78)
        Me.txtTamanoBuffer.Name = "txtTamanoBuffer"
        Me.txtTamanoBuffer.Size = New System.Drawing.Size(32, 20)
        Me.txtTamanoBuffer.TabIndex = 7
        Me.txtTamanoBuffer.Text = "750"
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label6.Location = New System.Drawing.Point(12, 81)
        Me.Label6.MinimumSize = New System.Drawing.Size(150, 0)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(150, 13)
        Me.Label6.TabIndex = 6
        Me.Label6.Text = "Tamaño de buffer de disco:"
        Me.Label6.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label5.Location = New System.Drawing.Point(202, 55)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(21, 13)
        Me.Label5.TabIndex = 3
        Me.Label5.Text = "KB"
        '
        'txtTamanoPaquete
        '
        Me.txtTamanoPaquete.Location = New System.Drawing.Point(165, 52)
        Me.txtTamanoPaquete.Name = "txtTamanoPaquete"
        Me.txtTamanoPaquete.Size = New System.Drawing.Size(34, 20)
        Me.txtTamanoPaquete.TabIndex = 2
        Me.txtTamanoPaquete.Text = "50"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label4.Location = New System.Drawing.Point(12, 55)
        Me.Label4.MinimumSize = New System.Drawing.Size(150, 0)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(150, 13)
        Me.Label4.TabIndex = 1
        Me.Label4.Text = "Tamaño paquete conexión:"
        Me.Label4.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtProxyPort
        '
        Me.txtProxyPort.Location = New System.Drawing.Point(537, 43)
        Me.txtProxyPort.Name = "txtProxyPort"
        Me.txtProxyPort.Size = New System.Drawing.Size(46, 20)
        Me.txtProxyPort.TabIndex = 4
        '
        'Label14
        '
        Me.Label14.AutoSize = True
        Me.Label14.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label14.Location = New System.Drawing.Point(490, 46)
        Me.Label14.MinimumSize = New System.Drawing.Size(45, 0)
        Me.Label14.Name = "Label14"
        Me.Label14.Size = New System.Drawing.Size(45, 13)
        Me.Label14.TabIndex = 3
        Me.Label14.Text = "Puerto:"
        Me.Label14.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtProxyIP
        '
        Me.txtProxyIP.Location = New System.Drawing.Point(136, 43)
        Me.txtProxyIP.Name = "txtProxyIP"
        Me.txtProxyIP.Size = New System.Drawing.Size(322, 20)
        Me.txtProxyIP.TabIndex = 2
        '
        'Label13
        '
        Me.Label13.AutoSize = True
        Me.Label13.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label13.Location = New System.Drawing.Point(9, 46)
        Me.Label13.MinimumSize = New System.Drawing.Size(120, 0)
        Me.Label13.Name = "Label13"
        Me.Label13.Size = New System.Drawing.Size(121, 13)
        Me.Label13.TabIndex = 1
        Me.Label13.Text = "Dirección / IP del proxy:"
        Me.Label13.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkProxy
        '
        Me.chkProxy.AutoSize = True
        Me.chkProxy.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkProxy.Location = New System.Drawing.Point(12, 18)
        Me.chkProxy.Name = "chkProxy"
        Me.chkProxy.Size = New System.Drawing.Size(76, 17)
        Me.chkProxy.TabIndex = 0
        Me.chkProxy.Text = "Usar proxy"
        Me.chkProxy.UseVisualStyleBackColor = True
        '
        'ConexionGroup
        '
        Me.ConexionGroup.Controls.Add(Me.Label15)
        Me.ConexionGroup.Controls.Add(Me.txtLimiteVelocidadKBs)
        Me.ConexionGroup.Controls.Add(Me.chkLimitarVelocidad)
        Me.ConexionGroup.Controls.Add(Me.Label12)
        Me.ConexionGroup.Controls.Add(Me.Label10)
        Me.ConexionGroup.Controls.Add(Me.txtPeriodoReintento)
        Me.ConexionGroup.Controls.Add(Me.chkReintentarError)
        Me.ConexionGroup.Controls.Add(Me.Label9)
        Me.ConexionGroup.Controls.Add(Me.txtConFic)
        Me.ConexionGroup.Controls.Add(Me.txtDescSimult)
        Me.ConexionGroup.Controls.Add(Me.LinkLabel1)
        Me.ConexionGroup.Location = New System.Drawing.Point(6, 260)
        Me.ConexionGroup.Name = "ConexionGroup"
        Me.ConexionGroup.Size = New System.Drawing.Size(599, 116)
        Me.ConexionGroup.TabIndex = 2
        Me.ConexionGroup.TabStop = False
        Me.ConexionGroup.Text = "Conexión"
        '
        'Label15
        '
        Me.Label15.AutoSize = True
        Me.Label15.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label15.Location = New System.Drawing.Point(186, 30)
        Me.Label15.Name = "Label15"
        Me.Label15.Size = New System.Drawing.Size(31, 13)
        Me.Label15.TabIndex = 2
        Me.Label15.Text = "KB/s"
        '
        'txtLimiteVelocidadKBs
        '
        Me.txtLimiteVelocidadKBs.Location = New System.Drawing.Point(132, 27)
        Me.txtLimiteVelocidadKBs.MaxLength = 6
        Me.txtLimiteVelocidadKBs.Name = "txtLimiteVelocidadKBs"
        Me.txtLimiteVelocidadKBs.Size = New System.Drawing.Size(48, 20)
        Me.txtLimiteVelocidadKBs.TabIndex = 1
        '
        'chkLimitarVelocidad
        '
        Me.chkLimitarVelocidad.AutoSize = True
        Me.chkLimitarVelocidad.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkLimitarVelocidad.Location = New System.Drawing.Point(9, 29)
        Me.chkLimitarVelocidad.Name = "chkLimitarVelocidad"
        Me.chkLimitarVelocidad.Size = New System.Drawing.Size(128, 17)
        Me.chkLimitarVelocidad.TabIndex = 0
        Me.chkLimitarVelocidad.Text = "Limitar la velocidad a "
        Me.chkLimitarVelocidad.UseVisualStyleBackColor = True
        '
        'GroupBox4
        '
        Me.GroupBox4.Controls.Add(Me.chkCheckUpdates)
        Me.GroupBox4.Controls.Add(Me.comboIdiomas)
        Me.GroupBox4.Controls.Add(Me.Label24)
        Me.GroupBox4.Controls.Add(Me.linkUltConfig)
        Me.GroupBox4.Controls.Add(Me.chkUltimaConfig)
        Me.GroupBox4.Controls.Add(Me.comboPrioridad)
        Me.GroupBox4.Controls.Add(Me.chkStartWindows)
        Me.GroupBox4.Controls.Add(Me.btnExaminar)
        Me.GroupBox4.Controls.Add(Me.chkComenzarPlay)
        Me.GroupBox4.Controls.Add(Me.txtRuta)
        Me.GroupBox4.Controls.Add(Me.Label1)
        Me.GroupBox4.Controls.Add(Me.linkApagarPC)
        Me.GroupBox4.Controls.Add(Me.chkUnZip)
        Me.GroupBox4.Controls.Add(Me.chkApagarPC)
        Me.GroupBox4.Controls.Add(Me.chkAnalisisPortapapeles)
        Me.GroupBox4.Controls.Add(Me.chkCrearDirectorio)
        Me.GroupBox4.Location = New System.Drawing.Point(6, 74)
        Me.GroupBox4.Name = "GroupBox4"
        Me.GroupBox4.Size = New System.Drawing.Size(599, 180)
        Me.GroupBox4.TabIndex = 1
        Me.GroupBox4.TabStop = False
        Me.GroupBox4.Text = "Opciones generales"
        '
        'chkCheckUpdates
        '
        Me.chkCheckUpdates.AutoSize = True
        Me.chkCheckUpdates.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkCheckUpdates.Location = New System.Drawing.Point(10, 150)
        Me.chkCheckUpdates.Name = "chkCheckUpdates"
        Me.chkCheckUpdates.Size = New System.Drawing.Size(153, 17)
        Me.chkCheckUpdates.TabIndex = 15
        Me.chkCheckUpdates.Text = "Comprobar actualizaciones"
        Me.chkCheckUpdates.UseVisualStyleBackColor = True
        '
        'comboIdiomas
        '
        Me.comboIdiomas.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.comboIdiomas.FormattingEnabled = True
        Me.comboIdiomas.Location = New System.Drawing.Point(429, 128)
        Me.comboIdiomas.Name = "comboIdiomas"
        Me.comboIdiomas.Size = New System.Drawing.Size(152, 21)
        Me.comboIdiomas.TabIndex = 14
        '
        'Label24
        '
        Me.Label24.AutoSize = True
        Me.Label24.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label24.Location = New System.Drawing.Point(371, 131)
        Me.Label24.Name = "Label24"
        Me.Label24.Size = New System.Drawing.Size(41, 13)
        Me.Label24.TabIndex = 13
        Me.Label24.Text = "Idioma:"
        '
        'linkUltConfig
        '
        Me.linkUltConfig.AutoSize = True
        Me.linkUltConfig.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.linkUltConfig.Location = New System.Drawing.Point(563, 105)
        Me.linkUltConfig.Name = "linkUltConfig"
        Me.linkUltConfig.Size = New System.Drawing.Size(19, 13)
        Me.linkUltConfig.TabIndex = 10
        Me.linkUltConfig.TabStop = True
        Me.linkUltConfig.Text = "[?]"
        '
        'chkUltimaConfig
        '
        Me.chkUltimaConfig.AutoSize = True
        Me.chkUltimaConfig.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkUltimaConfig.Location = New System.Drawing.Point(374, 104)
        Me.chkUltimaConfig.Name = "chkUltimaConfig"
        Me.chkUltimaConfig.Size = New System.Drawing.Size(193, 17)
        Me.chkUltimaConfig.TabIndex = 9
        Me.chkUltimaConfig.Text = "Guardar última configuración usada"
        Me.chkUltimaConfig.UseVisualStyleBackColor = True
        '
        'comboPrioridad
        '
        Me.comboPrioridad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.comboPrioridad.FormattingEnabled = True
        Me.comboPrioridad.Location = New System.Drawing.Point(191, 125)
        Me.comboPrioridad.Name = "comboPrioridad"
        Me.comboPrioridad.Size = New System.Drawing.Size(67, 21)
        Me.comboPrioridad.TabIndex = 12
        '
        'chkStartWindows
        '
        Me.chkStartWindows.AutoSize = True
        Me.chkStartWindows.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkStartWindows.Location = New System.Drawing.Point(10, 104)
        Me.chkStartWindows.Name = "chkStartWindows"
        Me.chkStartWindows.Size = New System.Drawing.Size(122, 17)
        Me.chkStartWindows.TabIndex = 8
        Me.chkStartWindows.Text = "Iniciar con Windows"
        Me.chkStartWindows.UseVisualStyleBackColor = True
        '
        'TabControl1
        '
        Me.TabControl1.Controls.Add(Me.TabPage1)
        Me.TabControl1.Controls.Add(Me.TabPage2)
        Me.TabControl1.Controls.Add(Me.TabPage3)
        Me.TabControl1.Controls.Add(Me.TabPage4)
        Me.TabControl1.Controls.Add(Me.TabPage5)
        Me.TabControl1.Location = New System.Drawing.Point(10, 12)
        Me.TabControl1.Name = "TabControl1"
        Me.TabControl1.SelectedIndex = 0
        Me.TabControl1.Size = New System.Drawing.Size(621, 408)
        Me.TabControl1.TabIndex = 0
        '
        'TabPage1
        '
        Me.TabPage1.Controls.Add(Me.GroupBox1)
        Me.TabPage1.Controls.Add(Me.ConexionGroup)
        Me.TabPage1.Controls.Add(Me.GroupBox4)
        Me.TabPage1.Location = New System.Drawing.Point(4, 22)
        Me.TabPage1.Name = "TabPage1"
        Me.TabPage1.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage1.Size = New System.Drawing.Size(613, 382)
        Me.TabPage1.TabIndex = 0
        Me.TabPage1.Text = "General"
        Me.TabPage1.UseVisualStyleBackColor = True
        '
        'TabPage2
        '
        Me.TabPage2.Controls.Add(Me.GroupBox5)
        Me.TabPage2.Controls.Add(Me.GroupBox2)
        Me.TabPage2.Controls.Add(Me.GroupBox3)
        Me.TabPage2.Location = New System.Drawing.Point(4, 22)
        Me.TabPage2.Name = "TabPage2"
        Me.TabPage2.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage2.Size = New System.Drawing.Size(613, 382)
        Me.TabPage2.TabIndex = 1
        Me.TabPage2.Text = "Avanzado"
        Me.TabPage2.UseVisualStyleBackColor = True
        '
        'GroupBox5
        '
        Me.GroupBox5.Controls.Add(Me.btnExaminarTemplate)
        Me.GroupBox5.Controls.Add(Me.txtServidorWebTemplate)
        Me.GroupBox5.Controls.Add(Me.Label21)
        Me.GroupBox5.Controls.Add(Me.txtServidorWebNombre)
        Me.GroupBox5.Controls.Add(Me.Label20)
        Me.GroupBox5.Controls.Add(Me.Label19)
        Me.GroupBox5.Controls.Add(Me.txtServidorWebTimeout)
        Me.GroupBox5.Controls.Add(Me.Label18)
        Me.GroupBox5.Controls.Add(Me.Label17)
        Me.GroupBox5.Controls.Add(Me.txtServidorWebPassword)
        Me.GroupBox5.Controls.Add(Me.txtServidorWebPort)
        Me.GroupBox5.Controls.Add(Me.Label16)
        Me.GroupBox5.Controls.Add(Me.chkServidorWeb)
        Me.GroupBox5.Location = New System.Drawing.Point(6, 232)
        Me.GroupBox5.Name = "GroupBox5"
        Me.GroupBox5.Size = New System.Drawing.Size(599, 144)
        Me.GroupBox5.TabIndex = 2
        Me.GroupBox5.TabStop = False
        Me.GroupBox5.Text = "Servidor Web"
        '
        'btnExaminarTemplate
        '
        Me.btnExaminarTemplate.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.btnExaminarTemplate.Location = New System.Drawing.Point(510, 79)
        Me.btnExaminarTemplate.Name = "btnExaminarTemplate"
        Me.btnExaminarTemplate.Size = New System.Drawing.Size(75, 23)
        Me.btnExaminarTemplate.TabIndex = 10
        Me.btnExaminarTemplate.Text = "Examinar"
        Me.btnExaminarTemplate.UseVisualStyleBackColor = True
        '
        'txtServidorWebTemplate
        '
        Me.txtServidorWebTemplate.AllowDrop = True
        Me.txtServidorWebTemplate.Location = New System.Drawing.Point(114, 81)
        Me.txtServidorWebTemplate.Name = "txtServidorWebTemplate"
        Me.txtServidorWebTemplate.Size = New System.Drawing.Size(388, 20)
        Me.txtServidorWebTemplate.TabIndex = 9
        '
        'Label21
        '
        Me.Label21.AutoSize = True
        Me.Label21.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label21.Location = New System.Drawing.Point(12, 84)
        Me.Label21.Name = "Label21"
        Me.Label21.Size = New System.Drawing.Size(97, 13)
        Me.Label21.TabIndex = 8
        Me.Label21.Text = "Ruta de la plantilla:"
        '
        'txtServidorWebNombre
        '
        Me.txtServidorWebNombre.Location = New System.Drawing.Point(114, 115)
        Me.txtServidorWebNombre.Name = "txtServidorWebNombre"
        Me.txtServidorWebNombre.Size = New System.Drawing.Size(123, 20)
        Me.txtServidorWebNombre.TabIndex = 12
        '
        'Label20
        '
        Me.Label20.AutoSize = True
        Me.Label20.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label20.Location = New System.Drawing.Point(12, 118)
        Me.Label20.Name = "Label20"
        Me.Label20.Size = New System.Drawing.Size(96, 13)
        Me.Label20.TabIndex = 11
        Me.Label20.Text = "Nombre (opcional):"
        '
        'Label19
        '
        Me.Label19.AutoSize = True
        Me.Label19.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label19.Location = New System.Drawing.Point(414, 48)
        Me.Label19.Name = "Label19"
        Me.Label19.Size = New System.Drawing.Size(43, 13)
        Me.Label19.TabIndex = 5
        Me.Label19.Text = "minutos"
        '
        'txtServidorWebTimeout
        '
        Me.txtServidorWebTimeout.Location = New System.Drawing.Point(377, 45)
        Me.txtServidorWebTimeout.MaxLength = 2
        Me.txtServidorWebTimeout.Name = "txtServidorWebTimeout"
        Me.txtServidorWebTimeout.Size = New System.Drawing.Size(31, 20)
        Me.txtServidorWebTimeout.TabIndex = 4
        '
        'Label18
        '
        Me.Label18.AutoSize = True
        Me.Label18.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label18.Location = New System.Drawing.Point(251, 48)
        Me.Label18.MinimumSize = New System.Drawing.Size(120, 0)
        Me.Label18.Name = "Label18"
        Me.Label18.Size = New System.Drawing.Size(120, 13)
        Me.Label18.TabIndex = 3
        Me.Label18.Text = "Interrumpir sesión:"
        Me.Label18.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'Label17
        '
        Me.Label17.AutoSize = True
        Me.Label17.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label17.Location = New System.Drawing.Point(12, 48)
        Me.Label17.MinimumSize = New System.Drawing.Size(80, 0)
        Me.Label17.Name = "Label17"
        Me.Label17.Size = New System.Drawing.Size(80, 13)
        Me.Label17.TabIndex = 1
        Me.Label17.Text = "Contraseña:"
        Me.Label17.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtServidorWebPassword
        '
        Me.txtServidorWebPassword.Location = New System.Drawing.Point(93, 45)
        Me.txtServidorWebPassword.Name = "txtServidorWebPassword"
        Me.txtServidorWebPassword.Size = New System.Drawing.Size(144, 20)
        Me.txtServidorWebPassword.TabIndex = 2
        Me.txtServidorWebPassword.UseSystemPasswordChar = True
        '
        'txtServidorWebPort
        '
        Me.txtServidorWebPort.Location = New System.Drawing.Point(537, 45)
        Me.txtServidorWebPort.Name = "txtServidorWebPort"
        Me.txtServidorWebPort.Size = New System.Drawing.Size(46, 20)
        Me.txtServidorWebPort.TabIndex = 7
        '
        'Label16
        '
        Me.Label16.AutoSize = True
        Me.Label16.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label16.Location = New System.Drawing.Point(490, 48)
        Me.Label16.MinimumSize = New System.Drawing.Size(41, 0)
        Me.Label16.Name = "Label16"
        Me.Label16.Size = New System.Drawing.Size(41, 13)
        Me.Label16.TabIndex = 6
        Me.Label16.Text = "Puerto:"
        Me.Label16.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkServidorWeb
        '
        Me.chkServidorWeb.AutoSize = True
        Me.chkServidorWeb.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkServidorWeb.Location = New System.Drawing.Point(12, 19)
        Me.chkServidorWeb.Name = "chkServidorWeb"
        Me.chkServidorWeb.Size = New System.Drawing.Size(111, 17)
        Me.chkServidorWeb.TabIndex = 0
        Me.chkServidorWeb.Text = "Usar servidor web"
        Me.chkServidorWeb.UseVisualStyleBackColor = True
        '
        'GroupBox2
        '
        Me.GroupBox2.Controls.Add(Me.Label23)
        Me.GroupBox2.Controls.Add(Me.txtProxyName)
        Me.GroupBox2.Controls.Add(Me.txtProxyPassword)
        Me.GroupBox2.Controls.Add(Me.Label22)
        Me.GroupBox2.Controls.Add(Me.txtProxyPort)
        Me.GroupBox2.Controls.Add(Me.txtProxyIP)
        Me.GroupBox2.Controls.Add(Me.Label14)
        Me.GroupBox2.Controls.Add(Me.chkProxy)
        Me.GroupBox2.Controls.Add(Me.Label13)
        Me.GroupBox2.Location = New System.Drawing.Point(6, 119)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(599, 107)
        Me.GroupBox2.TabIndex = 1
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "Proxy"
        '
        'Label23
        '
        Me.Label23.AutoSize = True
        Me.Label23.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label23.Location = New System.Drawing.Point(10, 76)
        Me.Label23.MinimumSize = New System.Drawing.Size(120, 0)
        Me.Label23.Name = "Label23"
        Me.Label23.Size = New System.Drawing.Size(120, 13)
        Me.Label23.TabIndex = 5
        Me.Label23.Text = "Usuario (opcional):"
        Me.Label23.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtProxyName
        '
        Me.txtProxyName.Location = New System.Drawing.Point(135, 73)
        Me.txtProxyName.Name = "txtProxyName"
        Me.txtProxyName.Size = New System.Drawing.Size(144, 20)
        Me.txtProxyName.TabIndex = 6
        '
        'txtProxyPassword
        '
        Me.txtProxyPassword.Location = New System.Drawing.Point(428, 73)
        Me.txtProxyPassword.Name = "txtProxyPassword"
        Me.txtProxyPassword.Size = New System.Drawing.Size(155, 20)
        Me.txtProxyPassword.TabIndex = 8
        Me.txtProxyPassword.UseSystemPasswordChar = True
        '
        'Label22
        '
        Me.Label22.AutoSize = True
        Me.Label22.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label22.Location = New System.Drawing.Point(296, 76)
        Me.Label22.MinimumSize = New System.Drawing.Size(130, 0)
        Me.Label22.Name = "Label22"
        Me.Label22.Size = New System.Drawing.Size(130, 13)
        Me.Label22.TabIndex = 7
        Me.Label22.Text = "Contraseña (opcional):"
        Me.Label22.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'TabPage3
        '
        Me.TabPage3.Controls.Add(Me.GroupBox)
        Me.TabPage3.Controls.Add(Me.lblDesc)
        Me.TabPage3.Location = New System.Drawing.Point(4, 22)
        Me.TabPage3.Name = "TabPage3"
        Me.TabPage3.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage3.Size = New System.Drawing.Size(613, 382)
        Me.TabPage3.TabIndex = 2
        Me.TabPage3.Text = "Pre-Shared Keys"
        Me.TabPage3.UseVisualStyleBackColor = True
        '
        'GroupBox
        '
        Me.GroupBox.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBox.Controls.Add(Me.txtKeyList)
        Me.GroupBox.Location = New System.Drawing.Point(9, 65)
        Me.GroupBox.Name = "GroupBox"
        Me.GroupBox.Size = New System.Drawing.Size(598, 311)
        Me.GroupBox.TabIndex = 4
        Me.GroupBox.TabStop = False
        Me.GroupBox.Text = "Listado de claves"
        '
        'txtKeyList
        '
        Me.txtKeyList.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtKeyList.Location = New System.Drawing.Point(6, 19)
        Me.txtKeyList.Multiline = True
        Me.txtKeyList.Name = "txtKeyList"
        Me.txtKeyList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtKeyList.Size = New System.Drawing.Size(586, 286)
        Me.txtKeyList.TabIndex = 0
        '
        'lblDesc
        '
        Me.lblDesc.AutoSize = True
        Me.lblDesc.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.lblDesc.Location = New System.Drawing.Point(12, 12)
        Me.lblDesc.Name = "lblDesc"
        Me.lblDesc.Size = New System.Drawing.Size(473, 39)
        Me.lblDesc.TabIndex = 3
        Me.lblDesc.Text = resources.GetString("lblDesc.Text")
        '
        'TabPage4
        '
        Me.TabPage4.Controls.Add(Me.GroupBox8)
        Me.TabPage4.Controls.Add(Me.GroupBox7)
        Me.TabPage4.Controls.Add(Me.GroupBox6)
        Me.TabPage4.Location = New System.Drawing.Point(4, 22)
        Me.TabPage4.Name = "TabPage4"
        Me.TabPage4.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage4.Size = New System.Drawing.Size(613, 382)
        Me.TabPage4.TabIndex = 3
        Me.TabPage4.Text = "Streaming"
        Me.TabPage4.UseVisualStyleBackColor = True
        '
        'GroupBox8
        '
        Me.GroupBox8.Controls.Add(Me.btnExaminarVLCPath)
        Me.GroupBox8.Controls.Add(Me.linkDownloadVLC)
        Me.GroupBox8.Controls.Add(Me.txtVLCPath)
        Me.GroupBox8.Controls.Add(Me.Label26)
        Me.GroupBox8.Location = New System.Drawing.Point(6, 73)
        Me.GroupBox8.Name = "GroupBox8"
        Me.GroupBox8.Size = New System.Drawing.Size(601, 60)
        Me.GroupBox8.TabIndex = 2
        Me.GroupBox8.TabStop = False
        Me.GroupBox8.Text = "Configuración VLC"
        '
        'btnExaminarVLCPath
        '
        Me.btnExaminarVLCPath.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.btnExaminarVLCPath.Location = New System.Drawing.Point(404, 22)
        Me.btnExaminarVLCPath.Name = "btnExaminarVLCPath"
        Me.btnExaminarVLCPath.Size = New System.Drawing.Size(75, 23)
        Me.btnExaminarVLCPath.TabIndex = 11
        Me.btnExaminarVLCPath.Text = "Examinar"
        Me.btnExaminarVLCPath.UseVisualStyleBackColor = True
        '
        'linkDownloadVLC
        '
        Me.linkDownloadVLC.AutoSize = True
        Me.linkDownloadVLC.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.linkDownloadVLC.Location = New System.Drawing.Point(476, 27)
        Me.linkDownloadVLC.MinimumSize = New System.Drawing.Size(100, 0)
        Me.linkDownloadVLC.Name = "linkDownloadVLC"
        Me.linkDownloadVLC.Size = New System.Drawing.Size(100, 13)
        Me.linkDownloadVLC.TabIndex = 11
        Me.linkDownloadVLC.TabStop = True
        Me.linkDownloadVLC.Text = "Descargar VLC"
        Me.linkDownloadVLC.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtVLCPath
        '
        Me.txtVLCPath.Location = New System.Drawing.Point(87, 24)
        Me.txtVLCPath.Name = "txtVLCPath"
        Me.txtVLCPath.Size = New System.Drawing.Size(311, 20)
        Me.txtVLCPath.TabIndex = 10
        '
        'Label26
        '
        Me.Label26.AutoSize = True
        Me.Label26.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.Label26.Location = New System.Drawing.Point(6, 27)
        Me.Label26.MinimumSize = New System.Drawing.Size(75, 0)
        Me.Label26.Name = "Label26"
        Me.Label26.Size = New System.Drawing.Size(75, 13)
        Me.Label26.TabIndex = 10
        Me.Label26.Text = "Ruta VLC:"
        Me.Label26.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'GroupBox7
        '
        Me.GroupBox7.Controls.Add(Me.lblInfoStreaming)
        Me.GroupBox7.Location = New System.Drawing.Point(6, 139)
        Me.GroupBox7.Name = "GroupBox7"
        Me.GroupBox7.Size = New System.Drawing.Size(601, 237)
        Me.GroupBox7.TabIndex = 1
        Me.GroupBox7.TabStop = False
        Me.GroupBox7.Text = "Información"
        '
        'lblInfoStreaming
        '
        Me.lblInfoStreaming.AutoSize = True
        Me.lblInfoStreaming.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.lblInfoStreaming.Location = New System.Drawing.Point(6, 16)
        Me.lblInfoStreaming.MaximumSize = New System.Drawing.Size(570, 0)
        Me.lblInfoStreaming.MinimumSize = New System.Drawing.Size(570, 0)
        Me.lblInfoStreaming.Name = "lblInfoStreaming"
        Me.lblInfoStreaming.Size = New System.Drawing.Size(570, 273)
        Me.lblInfoStreaming.TabIndex = 0
        Me.lblInfoStreaming.Text = resources.GetString("lblInfoStreaming.Text")
        '
        'GroupBox6
        '
        Me.GroupBox6.Controls.Add(Me.txtStreamingPassword)
        Me.GroupBox6.Controls.Add(Me.lblStreamingPassword)
        Me.GroupBox6.Controls.Add(Me.txtStreamingPort)
        Me.GroupBox6.Controls.Add(Me.lblStreamingPort)
        Me.GroupBox6.Controls.Add(Me.chkStreamingServer)
        Me.GroupBox6.Location = New System.Drawing.Point(6, 6)
        Me.GroupBox6.Name = "GroupBox6"
        Me.GroupBox6.Size = New System.Drawing.Size(601, 61)
        Me.GroupBox6.TabIndex = 0
        Me.GroupBox6.TabStop = False
        Me.GroupBox6.Text = "Configuración de streaming"
        '
        'txtStreamingPassword
        '
        Me.txtStreamingPassword.Location = New System.Drawing.Point(447, 26)
        Me.txtStreamingPassword.Name = "txtStreamingPassword"
        Me.txtStreamingPassword.Size = New System.Drawing.Size(129, 20)
        Me.txtStreamingPassword.TabIndex = 11
        Me.txtStreamingPassword.UseSystemPasswordChar = True
        '
        'lblStreamingPassword
        '
        Me.lblStreamingPassword.AutoSize = True
        Me.lblStreamingPassword.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.lblStreamingPassword.Location = New System.Drawing.Point(316, 29)
        Me.lblStreamingPassword.MinimumSize = New System.Drawing.Size(125, 0)
        Me.lblStreamingPassword.Name = "lblStreamingPassword"
        Me.lblStreamingPassword.Size = New System.Drawing.Size(125, 13)
        Me.lblStreamingPassword.TabIndex = 10
        Me.lblStreamingPassword.Text = "Password (opcional):"
        Me.lblStreamingPassword.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtStreamingPort
        '
        Me.txtStreamingPort.Location = New System.Drawing.Point(253, 25)
        Me.txtStreamingPort.Name = "txtStreamingPort"
        Me.txtStreamingPort.Size = New System.Drawing.Size(40, 20)
        Me.txtStreamingPort.TabIndex = 9
        '
        'lblStreamingPort
        '
        Me.lblStreamingPort.AutoSize = True
        Me.lblStreamingPort.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.lblStreamingPort.Location = New System.Drawing.Point(206, 29)
        Me.lblStreamingPort.MinimumSize = New System.Drawing.Size(41, 0)
        Me.lblStreamingPort.Name = "lblStreamingPort"
        Me.lblStreamingPort.Size = New System.Drawing.Size(41, 13)
        Me.lblStreamingPort.TabIndex = 8
        Me.lblStreamingPort.Text = "Puerto:"
        Me.lblStreamingPort.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkStreamingServer
        '
        Me.chkStreamingServer.AutoSize = True
        Me.chkStreamingServer.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.chkStreamingServer.Location = New System.Drawing.Point(23, 28)
        Me.chkStreamingServer.Name = "chkStreamingServer"
        Me.chkStreamingServer.Size = New System.Drawing.Size(151, 17)
        Me.chkStreamingServer.TabIndex = 1
        Me.chkStreamingServer.Text = "Usar servidor de streaming"
        Me.chkStreamingServer.UseVisualStyleBackColor = True
        '
        'TabPage5
        '
        Me.TabPage5.Controls.Add(Me.ElcAccountControl)
        Me.TabPage5.Location = New System.Drawing.Point(4, 22)
        Me.TabPage5.Name = "TabPage5"
        Me.TabPage5.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage5.Size = New System.Drawing.Size(613, 382)
        Me.TabPage5.TabIndex = 4
        Me.TabPage5.Text = "Cuentas ELC"
        Me.TabPage5.UseVisualStyleBackColor = True
        '
        'ElcAccountControl
        '
        Me.ElcAccountControl.Location = New System.Drawing.Point(-2, 0)
        Me.ElcAccountControl.Name = "ElcAccountControl"
        Me.ElcAccountControl.Size = New System.Drawing.Size(615, 376)
        Me.ElcAccountControl.TabIndex = 0
        '
        'Configuration
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(643, 467)
        Me.Controls.Add(Me.TabControl1)
        Me.Controls.Add(Me.btnGuardar)
        Me.Controls.Add(Me.btnCancel)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.HelpButton = True
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.ImeMode = System.Windows.Forms.ImeMode.NoControl
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Configuration"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Configuration"
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
        Me.GroupBox3.ResumeLayout(False)
        Me.GroupBox3.PerformLayout()
        Me.ConexionGroup.ResumeLayout(False)
        Me.ConexionGroup.PerformLayout()
        Me.GroupBox4.ResumeLayout(False)
        Me.GroupBox4.PerformLayout()
        Me.TabControl1.ResumeLayout(False)
        Me.TabPage1.ResumeLayout(False)
        Me.TabPage2.ResumeLayout(False)
        Me.GroupBox5.ResumeLayout(False)
        Me.GroupBox5.PerformLayout()
        Me.GroupBox2.ResumeLayout(False)
        Me.GroupBox2.PerformLayout()
        Me.TabPage3.ResumeLayout(False)
        Me.TabPage3.PerformLayout()
        Me.GroupBox.ResumeLayout(False)
        Me.GroupBox.PerformLayout()
        Me.TabPage4.ResumeLayout(False)
        Me.GroupBox8.ResumeLayout(False)
        Me.GroupBox8.PerformLayout()
        Me.GroupBox7.ResumeLayout(False)
        Me.GroupBox7.PerformLayout()
        Me.GroupBox6.ResumeLayout(False)
        Me.GroupBox6.PerformLayout()
        Me.TabPage5.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents GroupBox1 As System.Windows.Forms.GroupBox
    Friend WithEvents btnExaminar As System.Windows.Forms.Button
    Friend WithEvents txtRuta As System.Windows.Forms.TextBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents chkAnalisisPortapapeles As System.Windows.Forms.CheckBox
    Friend WithEvents chkUnZip As System.Windows.Forms.CheckBox
    Friend WithEvents chkCrearDirectorio As System.Windows.Forms.CheckBox
    Friend WithEvents txtPassword As System.Windows.Forms.TextBox
    Friend WithEvents txtUsuario As System.Windows.Forms.TextBox
    Friend WithEvents Label3 As System.Windows.Forms.Label
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents btnCancel As System.Windows.Forms.Button
    Friend WithEvents btnGuardar As System.Windows.Forms.Button
    Friend WithEvents GroupBox3 As System.Windows.Forms.GroupBox
    Friend WithEvents Label7 As System.Windows.Forms.Label
    Friend WithEvents txtTamanoBuffer As System.Windows.Forms.TextBox
    Friend WithEvents Label6 As System.Windows.Forms.Label
    Friend WithEvents Label5 As System.Windows.Forms.Label
    Friend WithEvents txtTamanoPaquete As System.Windows.Forms.TextBox
    Friend WithEvents Label4 As System.Windows.Forms.Label
    Friend WithEvents Label8 As System.Windows.Forms.Label
    Friend WithEvents LinkLabel1 As System.Windows.Forms.LinkLabel
    Friend WithEvents txtDescSimult As System.Windows.Forms.TextBox
    Friend WithEvents Label10 As System.Windows.Forms.Label
    Friend WithEvents txtConFic As System.Windows.Forms.TextBox
    Friend WithEvents Label9 As System.Windows.Forms.Label
    Friend WithEvents comboLog As System.Windows.Forms.ComboBox
    Friend WithEvents Label11 As System.Windows.Forms.Label
    Friend WithEvents Label12 As System.Windows.Forms.Label
    Friend WithEvents txtPeriodoReintento As System.Windows.Forms.TextBox
    Friend WithEvents chkReintentarError As System.Windows.Forms.CheckBox
    Friend WithEvents chkShowPassword As System.Windows.Forms.CheckBox
    Friend WithEvents txtProxyPort As System.Windows.Forms.TextBox
    Friend WithEvents Label14 As System.Windows.Forms.Label
    Friend WithEvents txtProxyIP As System.Windows.Forms.TextBox
    Friend WithEvents Label13 As System.Windows.Forms.Label
    Friend WithEvents chkProxy As System.Windows.Forms.CheckBox
    Friend WithEvents chkApagarPC As System.Windows.Forms.CheckBox
    Friend WithEvents linkApagarPC As System.Windows.Forms.LinkLabel
    Friend WithEvents chkComenzarPlay As System.Windows.Forms.CheckBox
    Friend WithEvents ConexionGroup As System.Windows.Forms.GroupBox
    Friend WithEvents Label15 As System.Windows.Forms.Label
    Friend WithEvents txtLimiteVelocidadKBs As System.Windows.Forms.TextBox
    Friend WithEvents chkLimitarVelocidad As System.Windows.Forms.CheckBox
    Friend WithEvents GroupBox4 As System.Windows.Forms.GroupBox
    Friend WithEvents chkStartWindows As System.Windows.Forms.CheckBox
    Friend WithEvents comboPrioridad As System.Windows.Forms.ComboBox
    Friend WithEvents TabControl1 As System.Windows.Forms.TabControl
    Friend WithEvents TabPage1 As System.Windows.Forms.TabPage
    Friend WithEvents TabPage2 As System.Windows.Forms.TabPage
    Friend WithEvents GroupBox2 As System.Windows.Forms.GroupBox
    Friend WithEvents GroupBox5 As System.Windows.Forms.GroupBox
    Friend WithEvents btnExaminarTemplate As System.Windows.Forms.Button
    Friend WithEvents txtServidorWebTemplate As System.Windows.Forms.TextBox
    Friend WithEvents Label21 As System.Windows.Forms.Label
    Friend WithEvents txtServidorWebNombre As System.Windows.Forms.TextBox
    Friend WithEvents Label20 As System.Windows.Forms.Label
    Friend WithEvents Label19 As System.Windows.Forms.Label
    Friend WithEvents txtServidorWebTimeout As System.Windows.Forms.TextBox
    Friend WithEvents Label18 As System.Windows.Forms.Label
    Friend WithEvents Label17 As System.Windows.Forms.Label
    Friend WithEvents txtServidorWebPassword As System.Windows.Forms.TextBox
    Friend WithEvents txtServidorWebPort As System.Windows.Forms.TextBox
    Friend WithEvents Label16 As System.Windows.Forms.Label
    Friend WithEvents chkServidorWeb As System.Windows.Forms.CheckBox
    Friend WithEvents linkUltConfig As System.Windows.Forms.LinkLabel
    Friend WithEvents chkUltimaConfig As System.Windows.Forms.CheckBox
    Friend WithEvents Label23 As System.Windows.Forms.Label
    Friend WithEvents txtProxyName As System.Windows.Forms.TextBox
    Friend WithEvents txtProxyPassword As System.Windows.Forms.TextBox
    Friend WithEvents Label22 As System.Windows.Forms.Label
    Friend WithEvents comboIdiomas As System.Windows.Forms.ComboBox
    Friend WithEvents Label24 As System.Windows.Forms.Label
    Friend WithEvents TabPage3 As System.Windows.Forms.TabPage
    Friend WithEvents lblDesc As System.Windows.Forms.Label
    Friend WithEvents GroupBox As System.Windows.Forms.GroupBox
    Friend WithEvents txtKeyList As System.Windows.Forms.TextBox
    Friend WithEvents TabPage4 As System.Windows.Forms.TabPage
    Friend WithEvents GroupBox7 As System.Windows.Forms.GroupBox
    Friend WithEvents lblInfoStreaming As System.Windows.Forms.Label
    Friend WithEvents GroupBox6 As System.Windows.Forms.GroupBox
    Friend WithEvents txtStreamingPort As System.Windows.Forms.TextBox
    Friend WithEvents lblStreamingPort As System.Windows.Forms.Label
    Friend WithEvents chkStreamingServer As System.Windows.Forms.CheckBox
    Friend WithEvents GroupBox8 As System.Windows.Forms.GroupBox
    Friend WithEvents btnExaminarVLCPath As System.Windows.Forms.Button
    Friend WithEvents linkDownloadVLC As System.Windows.Forms.LinkLabel
    Friend WithEvents txtVLCPath As System.Windows.Forms.TextBox
    Friend WithEvents Label26 As System.Windows.Forms.Label
    Friend WithEvents TabPage5 As System.Windows.Forms.TabPage
    Friend WithEvents ElcAccountControl As MegaDownloader.ELCAccountControl
    Friend WithEvents txtStreamingPassword As System.Windows.Forms.TextBox
    Friend WithEvents lblStreamingPassword As System.Windows.Forms.Label
    Friend WithEvents chkCheckUpdates As System.Windows.Forms.CheckBox
End Class
