using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class Configuration : System.Windows.Forms.Form
    {

        // Form overrides dispose to clean up the component list.
        [DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components is not null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(Configuration));
            GroupBox1 = new SettingsSection();
            chkShowPassword = new System.Windows.Forms.CheckBox();
            chkShowPassword.CheckedChanged += new EventHandler(chkShowPassword_CheckedChanged);
            txtPassword = new System.Windows.Forms.TextBox();
            txtUsuario = new System.Windows.Forms.TextBox();
            Label3 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            chkComenzarPlay = new System.Windows.Forms.CheckBox();
            linkApagarPC = new System.Windows.Forms.LinkLabel();
            linkApagarPC.MouseHover += new EventHandler(linkApagarPC_MouseHover);
            linkApagarPC.MouseLeave += new EventHandler(linkApagarPC_MouseLeave);
            linkApagarPC.Click += new EventHandler(linkApagarPC_Click);
            chkApagarPC = new System.Windows.Forms.CheckBox();
            Label12 = new System.Windows.Forms.Label();
            txtPeriodoReintento = new System.Windows.Forms.TextBox();
            chkReintentarError = new System.Windows.Forms.CheckBox();
            chkReintentarError.CheckedChanged += new EventHandler(chkReintentarError_CheckedChanged);
            chkQuotaBreak = new System.Windows.Forms.CheckBox();
            LinkLabel1 = new System.Windows.Forms.LinkLabel();
            LinkLabel1.MouseHover += new EventHandler(LinkLabel1_MouseHover);
            LinkLabel1.MouseLeave += new EventHandler(LinkLabel1_MouseLeave);
            LinkLabel1.Click += new EventHandler(LinkLabel1_Click);
            txtDescSimult = new System.Windows.Forms.TextBox();
            Label10 = new System.Windows.Forms.Label();
            txtConFic = new System.Windows.Forms.TextBox();
            Label9 = new System.Windows.Forms.Label();
            chkAnalisisPortapapeles = new System.Windows.Forms.CheckBox();
            chkUnZip = new System.Windows.Forms.CheckBox();
            chkUnZip.CheckedChanged += new EventHandler(chkUnZip_CheckedChanged);
            chkCrearDirectorio = new System.Windows.Forms.CheckBox();
            btnExaminar = new CommandButton();
            btnExaminar.Click += new EventHandler(btnExaminar_Click);
            txtRuta = new System.Windows.Forms.TextBox();
            Label1 = new System.Windows.Forms.Label();
            btnCancel = new CommandButton();
            btnCancel.Click += new EventHandler(btnCancel_Click);
            btnGuardar = new CommandButton();
            btnGuardar.Click += new EventHandler(btnGuardar_Click);
            GroupBox3 = new SettingsSection();
            comboLog = new System.Windows.Forms.ComboBox();
            Label11 = new System.Windows.Forms.Label();
            Label8 = new System.Windows.Forms.Label();
            Label7 = new System.Windows.Forms.Label();
            txtTamanoBuffer = new System.Windows.Forms.TextBox();
            Label6 = new System.Windows.Forms.Label();
            Label5 = new System.Windows.Forms.Label();
            txtTamanoPaquete = new System.Windows.Forms.TextBox();
            Label4 = new System.Windows.Forms.Label();
            txtProxyPort = new System.Windows.Forms.TextBox();
            Label14 = new System.Windows.Forms.Label();
            txtProxyIP = new System.Windows.Forms.TextBox();
            Label13 = new System.Windows.Forms.Label();
            chkProxy = new System.Windows.Forms.CheckBox();
            chkProxy.CheckedChanged += new EventHandler(chkProxy_CheckedChanged);
            ConexionGroup = new SettingsSection();
            Label15 = new System.Windows.Forms.Label();
            txtLimiteVelocidadKBs = new System.Windows.Forms.TextBox();
            chkLimitarVelocidad = new System.Windows.Forms.CheckBox();
            chkLimitarVelocidad.CheckedChanged += new EventHandler(chkLimitarVelocidad_CheckedChanged);
            GroupBox4 = new SettingsSection();
            chkCheckUpdates = new System.Windows.Forms.CheckBox();
            chkUpdateManualOnly = new System.Windows.Forms.CheckBox();
            txtUpdateCheckInterval = new System.Windows.Forms.TextBox();
            comboIdiomas = new System.Windows.Forms.ComboBox();
            Label24 = new System.Windows.Forms.Label();
            comboTheme = new System.Windows.Forms.ComboBox();
            Label25 = new System.Windows.Forms.Label();
            linkUltConfig = new System.Windows.Forms.LinkLabel();
            linkUltConfig.MouseHover += new EventHandler(linkUltConfig_MouseHover);
            linkUltConfig.MouseLeave += new EventHandler(linkUltConfig_MouseLeave);
            linkUltConfig.Click += new EventHandler(linkUltConfig_Click);
            chkUltimaConfig = new System.Windows.Forms.CheckBox();
            comboPrioridad = new System.Windows.Forms.ComboBox();
            chkStartWindows = new System.Windows.Forms.CheckBox();
            TabControl1 = new NavigationTabs();
            TabPage1 = new System.Windows.Forms.TabPage();
            TabPage2 = new System.Windows.Forms.TabPage();
            GroupBox5 = new SettingsSection();
            btnExaminarTemplate = new CommandButton();
            btnExaminarTemplate.Click += new EventHandler(btnExaminarTemplate_Click);
            txtServidorWebTemplate = new System.Windows.Forms.TextBox();
            txtServidorWebTemplate.DragDrop += new System.Windows.Forms.DragEventHandler(txtServidorWebTemplate_DragDrop);
            txtServidorWebTemplate.DragEnter += new System.Windows.Forms.DragEventHandler(txtServidorWebTemplate_DragEnter);
            Label21 = new System.Windows.Forms.Label();
            txtServidorWebNombre = new System.Windows.Forms.TextBox();
            Label20 = new System.Windows.Forms.Label();
            Label19 = new System.Windows.Forms.Label();
            txtServidorWebTimeout = new System.Windows.Forms.TextBox();
            Label18 = new System.Windows.Forms.Label();
            Label17 = new System.Windows.Forms.Label();
            txtServidorWebPassword = new System.Windows.Forms.TextBox();
            txtServidorWebPort = new System.Windows.Forms.TextBox();
            Label16 = new System.Windows.Forms.Label();
            chkServidorWeb = new System.Windows.Forms.CheckBox();
            chkServidorWeb.CheckedChanged += new EventHandler(chkServidorWeb_CheckedChanged);
            chkServidorWebLAN = new System.Windows.Forms.CheckBox();
            chkServidorWebLAN.CheckedChanged += new EventHandler(chkServidorWebLAN_CheckedChanged);
            lblServidorWebBindIP = new System.Windows.Forms.Label();
            txtServidorWebBindIP = new System.Windows.Forms.TextBox();
            GroupBox2 = new SettingsSection();
            Label23 = new System.Windows.Forms.Label();
            txtProxyName = new System.Windows.Forms.TextBox();
            txtProxyPassword = new System.Windows.Forms.TextBox();
            Label22 = new System.Windows.Forms.Label();
            TabPage3 = new System.Windows.Forms.TabPage();
            GroupBox = new SettingsSection();
            txtKeyList = new System.Windows.Forms.TextBox();
            lblDesc = new System.Windows.Forms.Label();
            TabPage4 = new System.Windows.Forms.TabPage();
            GroupBox8 = new SettingsSection();
            btnExaminarVLCPath = new CommandButton();
            btnExaminarVLCPath.Click += new EventHandler(btnExaminarVLCPath_Click);
            linkDownloadVLC = new System.Windows.Forms.LinkLabel();
            linkDownloadVLC.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkDownloadVLC_LinkClicked);
            txtVLCPath = new System.Windows.Forms.TextBox();
            Label26 = new System.Windows.Forms.Label();
            GroupBox7 = new SettingsSection();
            lblInfoStreaming = new System.Windows.Forms.Label();
            GroupBox6 = new SettingsSection();
            txtStreamingPassword = new System.Windows.Forms.TextBox();
            lblStreamingPassword = new System.Windows.Forms.Label();
            txtStreamingPort = new System.Windows.Forms.TextBox();
            lblStreamingPort = new System.Windows.Forms.Label();
            chkStreamingServer = new System.Windows.Forms.CheckBox();
            chkStreamingServer.CheckedChanged += new EventHandler(chkServidorStreaming_CheckedChanged);
            TabPage5 = new System.Windows.Forms.TabPage();
            ElcAccountControl = new ELCAccountControl();
            txtSearchConfig = new System.Windows.Forms.TextBox();
            txtSearchConfig.KeyDown += new System.Windows.Forms.KeyEventHandler(txtSearchConfig_KeyDown);
            GroupBox1.SuspendLayout();
            GroupBox3.SuspendLayout();
            ConexionGroup.SuspendLayout();
            GroupBox4.SuspendLayout();
            TabControl1.SuspendLayout();
            TabPage1.SuspendLayout();
            TabPage2.SuspendLayout();
            GroupBox5.SuspendLayout();
            GroupBox2.SuspendLayout();
            TabPage3.SuspendLayout();
            GroupBox.SuspendLayout();
            TabPage4.SuspendLayout();
            GroupBox8.SuspendLayout();
            GroupBox7.SuspendLayout();
            GroupBox6.SuspendLayout();
            TabPage5.SuspendLayout();
            SuspendLayout();
            // 
            // GroupBox1
            // 
            GroupBox1.Controls.Add(chkShowPassword);
            GroupBox1.Controls.Add(txtPassword);
            GroupBox1.Controls.Add(txtUsuario);
            GroupBox1.Controls.Add(Label3);
            GroupBox1.Controls.Add(Label2);
            GroupBox1.Location = new System.Drawing.Point(6, 6);
            GroupBox1.Name = "GroupBox1";
            GroupBox1.Size = new System.Drawing.Size(599, 62);
            GroupBox1.TabIndex = 0;
            GroupBox1.TabStop = false;
            GroupBox1.Text = "Datos de usuario (opcionales)";
            // 
            // chkShowPassword
            // 
            chkShowPassword.AutoSize = true;
            chkShowPassword.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkShowPassword.Location = new System.Drawing.Point(468, 30);
            chkShowPassword.Name = "chkShowPassword";
            chkShowPassword.Size = new System.Drawing.Size(117, 17);
            chkShowPassword.TabIndex = 4;
            chkShowPassword.Text = "Mostrar contraseña";
            chkShowPassword.UseVisualStyleBackColor = false;
            // 
            // txtPassword
            // 
            txtPassword.Location = new System.Drawing.Point(302, 28);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new System.Drawing.Size(156, 20);
            txtPassword.TabIndex = 3;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // txtUsuario
            // 
            txtUsuario.Location = new System.Drawing.Point(67, 28);
            txtUsuario.Name = "txtUsuario";
            txtUsuario.Size = new System.Drawing.Size(140, 20);
            txtUsuario.TabIndex = 1;
            // 
            // Label3
            // 
            Label3.AutoSize = true;
            Label3.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label3.Location = new System.Drawing.Point(216, 31);
            Label3.MinimumSize = new System.Drawing.Size(80, 0);
            Label3.Name = "Label3";
            Label3.Size = new System.Drawing.Size(80, 13);
            Label3.TabIndex = 2;
            Label3.Text = "Contraseña:";
            Label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label2.Location = new System.Drawing.Point(9, 31);
            Label2.MinimumSize = new System.Drawing.Size(55, 0);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(55, 13);
            Label2.TabIndex = 0;
            Label2.Text = "Usuario:";
            Label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkComenzarPlay
            // 
            chkComenzarPlay.AutoSize = true;
            chkComenzarPlay.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkComenzarPlay.Location = new System.Drawing.Point(10, 81);
            chkComenzarPlay.Name = "chkComenzarPlay";
            chkComenzarPlay.Size = new System.Drawing.Size(282, 17);
            chkComenzarPlay.TabIndex = 5;
            chkComenzarPlay.Text = "Comenzar a descargar en cuanto se inicie el programa";
            chkComenzarPlay.UseVisualStyleBackColor = true;
            // 
            // linkApagarPC
            // 
            linkApagarPC.AutoSize = true;
            linkApagarPC.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            linkApagarPC.Location = new System.Drawing.Point(563, 82);
            linkApagarPC.Name = "linkApagarPC";
            linkApagarPC.Size = new System.Drawing.Size(19, 13);
            linkApagarPC.TabIndex = 7;
            linkApagarPC.TabStop = true;
            linkApagarPC.Text = "[?]";
            // 
            // chkApagarPC
            // 
            chkApagarPC.AutoSize = true;
            chkApagarPC.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkApagarPC.Location = new System.Drawing.Point(374, 81);
            chkApagarPC.Name = "chkApagarPC";
            chkApagarPC.Size = new System.Drawing.Size(178, 17);
            chkApagarPC.TabIndex = 6;
            chkApagarPC.Text = "Apagar PC al finalizar descargas";
            chkApagarPC.UseVisualStyleBackColor = true;
            // 
            // Label12
            // 
            Label12.AutoSize = true;
            Label12.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label12.Location = new System.Drawing.Point(283, 61);
            Label12.Name = "Label12";
            Label12.Size = new System.Drawing.Size(43, 13);
            Label12.TabIndex = 7;
            Label12.Text = "minutos";
            // 
            // txtPeriodoReintento
            // 
            txtPeriodoReintento.Location = new System.Drawing.Point(247, 58);
            txtPeriodoReintento.MaxLength = 3;
            txtPeriodoReintento.Name = "txtPeriodoReintento";
            txtPeriodoReintento.Size = new System.Drawing.Size(30, 20);
            txtPeriodoReintento.TabIndex = 6;
            // 
            // chkReintentarError
            // 
            chkReintentarError.AutoSize = true;
            chkReintentarError.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkReintentarError.Location = new System.Drawing.Point(9, 60);
            chkReintentarError.Name = "chkReintentarError";
            chkReintentarError.Size = new System.Drawing.Size(239, 17);
            chkReintentarError.TabIndex = 5;
            chkReintentarError.Text = "En caso de error, reintentar la descarga cada";
            chkReintentarError.UseVisualStyleBackColor = true;
            // 
            // chkQuotaBreak
            // 
            chkQuotaBreak.AutoSize = true;
            chkQuotaBreak.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkQuotaBreak.Location = new System.Drawing.Point(9, 83);
            chkQuotaBreak.Name = "chkQuotaBreak";
            chkQuotaBreak.Size = new System.Drawing.Size(250, 17);
            chkQuotaBreak.TabIndex = 11;
            chkQuotaBreak.Text = "Superar cuota (reintento clásico)";
            chkQuotaBreak.UseVisualStyleBackColor = true;
            // 
            // LinkLabel1
            // 
            LinkLabel1.AutoSize = true;
            LinkLabel1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            LinkLabel1.Location = new System.Drawing.Point(350, 91);
            LinkLabel1.MinimumSize = new System.Drawing.Size(230, 0);
            LinkLabel1.Name = "LinkLabel1";
            LinkLabel1.Size = new System.Drawing.Size(232, 13);
            LinkLabel1.TabIndex = 10;
            LinkLabel1.TabStop = true;
            LinkLabel1.Text = "Nota importante sobre el número de conexiones";
            LinkLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtDescSimult
            // 
            txtDescSimult.Location = new System.Drawing.Point(550, 26);
            txtDescSimult.MaxLength = 2;
            txtDescSimult.Name = "txtDescSimult";
            txtDescSimult.Size = new System.Drawing.Size(30, 20);
            txtDescSimult.TabIndex = 4;
            txtDescSimult.Text = "3";
            // 
            // Label10
            // 
            Label10.AutoSize = true;
            Label10.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label10.Location = new System.Drawing.Point(344, 29);
            Label10.MinimumSize = new System.Drawing.Size(200, 0);
            Label10.Name = "Label10";
            Label10.Size = new System.Drawing.Size(200, 13);
            Label10.TabIndex = 3;
            Label10.Text = "Número de descargas simultáneas:";
            Label10.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtConFic
            // 
            txtConFic.Location = new System.Drawing.Point(550, 57);
            txtConFic.MaxLength = 2;
            txtConFic.Name = "txtConFic";
            txtConFic.Size = new System.Drawing.Size(30, 20);
            txtConFic.TabIndex = 9;
            txtConFic.Text = "2";
            // 
            // Label9
            // 
            Label9.AutoSize = true;
            Label9.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label9.Location = new System.Drawing.Point(344, 60);
            Label9.MinimumSize = new System.Drawing.Size(200, 0);
            Label9.Name = "Label9";
            Label9.Size = new System.Drawing.Size(200, 13);
            Label9.TabIndex = 8;
            Label9.Text = "Número de conexiones por fichero:";
            Label9.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkAnalisisPortapapeles
            // 
            chkAnalisisPortapapeles.AutoSize = true;
            chkAnalisisPortapapeles.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkAnalisisPortapapeles.Location = new System.Drawing.Point(10, 58);
            chkAnalisisPortapapeles.Name = "chkAnalisisPortapapeles";
            chkAnalisisPortapapeles.Size = new System.Drawing.Size(171, 17);
            chkAnalisisPortapapeles.TabIndex = 3;
            chkAnalisisPortapapeles.Text = "Capturar links del portapapeles";
            chkAnalisisPortapapeles.UseVisualStyleBackColor = true;
            // 
            // chkUnZip
            // 
            chkUnZip.AutoSize = true;
            chkUnZip.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkUnZip.Location = new System.Drawing.Point(10, 127);
            chkUnZip.Name = "chkUnZip";
            chkUnZip.Size = new System.Drawing.Size(181, 17);
            chkUnZip.TabIndex = 11;
            chkUnZip.Text = "Extracción automática. Prioridad:";
            chkUnZip.UseVisualStyleBackColor = true;
            // 
            // chkCrearDirectorio
            // 
            chkCrearDirectorio.AutoSize = true;
            chkCrearDirectorio.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkCrearDirectorio.Location = new System.Drawing.Point(374, 58);
            chkCrearDirectorio.Name = "chkCrearDirectorio";
            chkCrearDirectorio.Size = new System.Drawing.Size(157, 17);
            chkCrearDirectorio.TabIndex = 4;
            chkCrearDirectorio.Text = "Crear directorio por paquete";
            chkCrearDirectorio.UseVisualStyleBackColor = true;
            // 
            // btnExaminar
            // 
            btnExaminar.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            btnExaminar.Location = new System.Drawing.Point(507, 27);
            btnExaminar.Name = "btnExaminar";
            btnExaminar.Size = new System.Drawing.Size(75, 23);
            btnExaminar.TabIndex = 2;
            btnExaminar.Text = "Examinar";
            btnExaminar.UseVisualStyleBackColor = true;
            // 
            // txtRuta
            // 
            txtRuta.Location = new System.Drawing.Point(97, 29);
            txtRuta.Name = "txtRuta";
            txtRuta.Size = new System.Drawing.Size(404, 20);
            txtRuta.TabIndex = 1;
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label1.Location = new System.Drawing.Point(6, 32);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(85, 13);
            Label1.TabIndex = 0;
            Label1.Text = "Ruta descargas:";
            Label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            btnCancel.Location = new System.Drawing.Point(556, 458);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 23);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Cancelar";
            btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnGuardar
            // 
            btnGuardar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnGuardar.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            btnGuardar.Location = new System.Drawing.Point(9, 458);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new System.Drawing.Size(75, 23);
            btnGuardar.TabIndex = 0;
            btnGuardar.Text = "Guardar";
            btnGuardar.UseVisualStyleBackColor = true;
            // 
            // GroupBox3
            // 
            GroupBox3.Controls.Add(comboLog);
            GroupBox3.Controls.Add(Label11);
            GroupBox3.Controls.Add(Label8);
            GroupBox3.Controls.Add(Label7);
            GroupBox3.Controls.Add(txtTamanoBuffer);
            GroupBox3.Controls.Add(Label6);
            GroupBox3.Controls.Add(Label5);
            GroupBox3.Controls.Add(txtTamanoPaquete);
            GroupBox3.Controls.Add(Label4);
            GroupBox3.Location = new System.Drawing.Point(6, 6);
            GroupBox3.Name = "GroupBox3";
            GroupBox3.Size = new System.Drawing.Size(599, 107);
            GroupBox3.TabIndex = 0;
            GroupBox3.TabStop = false;
            GroupBox3.Text = "Opciones avanzadas";
            // 
            // comboLog
            // 
            comboLog.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboLog.FormattingEnabled = true;
            comboLog.Location = new System.Drawing.Point(432, 52);
            comboLog.Name = "comboLog";
            comboLog.Size = new System.Drawing.Size(153, 21);
            comboLog.TabIndex = 5;
            // 
            // Label11
            // 
            Label11.AutoSize = true;
            Label11.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label11.Location = new System.Drawing.Point(306, 55);
            Label11.MinimumSize = new System.Drawing.Size(120, 0);
            Label11.Name = "Label11";
            Label11.Size = new System.Drawing.Size(120, 13);
            Label11.TabIndex = 4;
            Label11.Text = "Nivel de logs:";
            Label11.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label8
            // 
            Label8.AutoSize = true;
            Label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Italic);
            Label8.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label8.Location = new System.Drawing.Point(11, 27);
            Label8.Name = "Label8";
            Label8.Size = new System.Drawing.Size(397, 13);
            Label8.TabIndex = 0;
            Label8.Text = "Nota: Para usuarios avanzados. Si no estás seguro de que poner, no toques nada.";
            // 
            // Label7
            // 
            Label7.AutoSize = true;
            Label7.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label7.Location = new System.Drawing.Point(205, 81);
            Label7.Name = "Label7";
            Label7.Size = new System.Drawing.Size(21, 13);
            Label7.TabIndex = 8;
            Label7.Text = "KB";
            // 
            // txtTamanoBuffer
            // 
            txtTamanoBuffer.Location = new System.Drawing.Point(167, 78);
            txtTamanoBuffer.Name = "txtTamanoBuffer";
            txtTamanoBuffer.Size = new System.Drawing.Size(32, 20);
            txtTamanoBuffer.TabIndex = 7;
            txtTamanoBuffer.Text = "750";
            // 
            // Label6
            // 
            Label6.AutoSize = true;
            Label6.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label6.Location = new System.Drawing.Point(12, 81);
            Label6.MinimumSize = new System.Drawing.Size(150, 0);
            Label6.Name = "Label6";
            Label6.Size = new System.Drawing.Size(150, 13);
            Label6.TabIndex = 6;
            Label6.Text = "Tamaño de buffer de disco:";
            Label6.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label5
            // 
            Label5.AutoSize = true;
            Label5.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label5.Location = new System.Drawing.Point(202, 55);
            Label5.Name = "Label5";
            Label5.Size = new System.Drawing.Size(21, 13);
            Label5.TabIndex = 3;
            Label5.Text = "KB";
            // 
            // txtTamanoPaquete
            // 
            txtTamanoPaquete.Location = new System.Drawing.Point(165, 52);
            txtTamanoPaquete.Name = "txtTamanoPaquete";
            txtTamanoPaquete.Size = new System.Drawing.Size(34, 20);
            txtTamanoPaquete.TabIndex = 2;
            txtTamanoPaquete.Text = "50";
            // 
            // Label4
            // 
            Label4.AutoSize = true;
            Label4.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label4.Location = new System.Drawing.Point(12, 55);
            Label4.MinimumSize = new System.Drawing.Size(150, 0);
            Label4.Name = "Label4";
            Label4.Size = new System.Drawing.Size(150, 13);
            Label4.TabIndex = 1;
            Label4.Text = "Tamaño paquete conexión:";
            Label4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtProxyPort
            // 
            txtProxyPort.Location = new System.Drawing.Point(537, 43);
            txtProxyPort.Name = "txtProxyPort";
            txtProxyPort.Size = new System.Drawing.Size(46, 20);
            txtProxyPort.TabIndex = 4;
            // 
            // Label14
            // 
            Label14.AutoSize = true;
            Label14.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label14.Location = new System.Drawing.Point(490, 46);
            Label14.MinimumSize = new System.Drawing.Size(45, 0);
            Label14.Name = "Label14";
            Label14.Size = new System.Drawing.Size(45, 13);
            Label14.TabIndex = 3;
            Label14.Text = "Puerto:";
            Label14.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtProxyIP
            // 
            txtProxyIP.Location = new System.Drawing.Point(136, 43);
            txtProxyIP.Name = "txtProxyIP";
            txtProxyIP.Size = new System.Drawing.Size(322, 20);
            txtProxyIP.TabIndex = 2;
            // 
            // Label13
            // 
            Label13.AutoSize = true;
            Label13.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label13.Location = new System.Drawing.Point(9, 46);
            Label13.MinimumSize = new System.Drawing.Size(120, 0);
            Label13.Name = "Label13";
            Label13.Size = new System.Drawing.Size(121, 13);
            Label13.TabIndex = 1;
            Label13.Text = "Dirección / IP del proxy:";
            Label13.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkProxy
            // 
            chkProxy.AutoSize = true;
            chkProxy.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkProxy.Location = new System.Drawing.Point(12, 18);
            chkProxy.Name = "chkProxy";
            chkProxy.Size = new System.Drawing.Size(76, 17);
            chkProxy.TabIndex = 0;
            chkProxy.Text = "Usar proxy";
            chkProxy.UseVisualStyleBackColor = true;
            // 
            // ConexionGroup
            // 
            ConexionGroup.Controls.Add(Label15);
            ConexionGroup.Controls.Add(txtLimiteVelocidadKBs);
            ConexionGroup.Controls.Add(chkLimitarVelocidad);
            ConexionGroup.Controls.Add(Label12);
            ConexionGroup.Controls.Add(Label10);
            ConexionGroup.Controls.Add(txtPeriodoReintento);
            ConexionGroup.Controls.Add(chkReintentarError);
            ConexionGroup.Controls.Add(chkQuotaBreak);
            ConexionGroup.Controls.Add(Label9);
            ConexionGroup.Controls.Add(txtConFic);
            ConexionGroup.Controls.Add(txtDescSimult);
            ConexionGroup.Controls.Add(LinkLabel1);
            ConexionGroup.Location = new System.Drawing.Point(6, 260);
            ConexionGroup.Name = "ConexionGroup";
            ConexionGroup.Size = new System.Drawing.Size(599, 116);
            ConexionGroup.TabIndex = 2;
            ConexionGroup.TabStop = false;
            ConexionGroup.Text = "Conexión";
            // 
            // Label15
            // 
            Label15.AutoSize = true;
            Label15.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label15.Location = new System.Drawing.Point(186, 30);
            Label15.Name = "Label15";
            Label15.Size = new System.Drawing.Size(31, 13);
            Label15.TabIndex = 2;
            Label15.Text = "MB/s";
            // 
            // txtLimiteVelocidadKBs
            // 
            txtLimiteVelocidadKBs.Location = new System.Drawing.Point(132, 27);
            txtLimiteVelocidadKBs.MaxLength = 10;
            txtLimiteVelocidadKBs.Name = "txtLimiteVelocidadKBs";
            txtLimiteVelocidadKBs.Size = new System.Drawing.Size(48, 20);
            txtLimiteVelocidadKBs.TabIndex = 1;
            // 
            // chkLimitarVelocidad
            // 
            chkLimitarVelocidad.AutoSize = true;
            chkLimitarVelocidad.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkLimitarVelocidad.Location = new System.Drawing.Point(9, 29);
            chkLimitarVelocidad.Name = "chkLimitarVelocidad";
            chkLimitarVelocidad.Size = new System.Drawing.Size(128, 17);
            chkLimitarVelocidad.TabIndex = 0;
            chkLimitarVelocidad.Text = "Limitar la velocidad a ";
            chkLimitarVelocidad.UseVisualStyleBackColor = true;
            // 
            // GroupBox4
            // 
            GroupBox4.Controls.Add(chkCheckUpdates);
            GroupBox4.Controls.Add(chkUpdateManualOnly);
            GroupBox4.Controls.Add(txtUpdateCheckInterval);
            GroupBox4.Controls.Add(comboTheme);
            GroupBox4.Controls.Add(Label25);
            GroupBox4.Controls.Add(comboIdiomas);
            GroupBox4.Controls.Add(Label24);
            GroupBox4.Controls.Add(linkUltConfig);
            GroupBox4.Controls.Add(chkUltimaConfig);
            GroupBox4.Controls.Add(comboPrioridad);
            GroupBox4.Controls.Add(chkStartWindows);
            GroupBox4.Controls.Add(btnExaminar);
            GroupBox4.Controls.Add(chkComenzarPlay);
            GroupBox4.Controls.Add(txtRuta);
            GroupBox4.Controls.Add(Label1);
            GroupBox4.Controls.Add(linkApagarPC);
            GroupBox4.Controls.Add(chkUnZip);
            GroupBox4.Controls.Add(chkApagarPC);
            GroupBox4.Controls.Add(chkAnalisisPortapapeles);
            GroupBox4.Controls.Add(chkCrearDirectorio);
            GroupBox4.Location = new System.Drawing.Point(6, 74);
            GroupBox4.Name = "GroupBox4";
            GroupBox4.Size = new System.Drawing.Size(599, 180);
            GroupBox4.TabIndex = 1;
            GroupBox4.TabStop = false;
            GroupBox4.Text = "Opciones generales";
            // 
            // chkCheckUpdates
            // 
            chkCheckUpdates.AutoSize = true;
            chkCheckUpdates.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkCheckUpdates.Location = new System.Drawing.Point(10, 150);
            chkCheckUpdates.Name = "chkCheckUpdates";
            chkCheckUpdates.Size = new System.Drawing.Size(153, 17);
            chkCheckUpdates.TabIndex = 15;
            chkCheckUpdates.Text = "Comprobar actualizaciones";
            chkCheckUpdates.UseVisualStyleBackColor = true;
            // 
            // chkUpdateManualOnly
            // 
            chkUpdateManualOnly.AutoSize = true;
            chkUpdateManualOnly.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkUpdateManualOnly.Location = new System.Drawing.Point(10, 168);
            chkUpdateManualOnly.Name = "chkUpdateManualOnly";
            chkUpdateManualOnly.Size = new System.Drawing.Size(220, 17);
            chkUpdateManualOnly.TabIndex = 17;
            chkUpdateManualOnly.Text = "Manual check only";
            chkUpdateManualOnly.UseVisualStyleBackColor = true;
            // 
            // txtUpdateCheckInterval
            // 
            txtUpdateCheckInterval.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            txtUpdateCheckInterval.Location = new System.Drawing.Point(180, 148);
            txtUpdateCheckInterval.MaxLength = 4;
            txtUpdateCheckInterval.Name = "txtUpdateCheckInterval";
            txtUpdateCheckInterval.Size = new System.Drawing.Size(40, 20);
            txtUpdateCheckInterval.TabIndex = 18;
            txtUpdateCheckInterval.Text = "1";
            // 
            // comboIdiomas
            // 
            comboIdiomas.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboIdiomas.FormattingEnabled = true;
            comboIdiomas.Location = new System.Drawing.Point(429, 128);
            comboIdiomas.Name = "comboIdiomas";
            comboIdiomas.Size = new System.Drawing.Size(152, 21);
            comboIdiomas.TabIndex = 14;
            // 
            // Label24
            // 
            Label24.AutoSize = true;
            Label24.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label24.Location = new System.Drawing.Point(371, 131);
            Label24.Name = "Label24";
            Label24.Size = new System.Drawing.Size(41, 13);
            Label24.TabIndex = 13;
            Label24.Text = "Idioma:";
            // 
            // comboTheme
            // 
            comboTheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboTheme.FormattingEnabled = true;
            comboTheme.Location = new System.Drawing.Point(429, 152);
            comboTheme.Name = "comboTheme";
            comboTheme.Size = new System.Drawing.Size(152, 21);
            comboTheme.TabIndex = 17;
            // 
            // Label25
            // 
            Label25.AutoSize = true;
            Label25.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label25.Location = new System.Drawing.Point(371, 155);
            Label25.Name = "Label25";
            Label25.Size = new System.Drawing.Size(31, 13);
            Label25.TabIndex = 16;
            Label25.Text = "Tema:";
            // 
            // linkUltConfig
            // 
            linkUltConfig.AutoSize = true;
            linkUltConfig.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            linkUltConfig.Location = new System.Drawing.Point(563, 105);
            linkUltConfig.Name = "linkUltConfig";
            linkUltConfig.Size = new System.Drawing.Size(19, 13);
            linkUltConfig.TabIndex = 10;
            linkUltConfig.TabStop = true;
            linkUltConfig.Text = "[?]";
            // 
            // chkUltimaConfig
            // 
            chkUltimaConfig.AutoSize = true;
            chkUltimaConfig.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkUltimaConfig.Location = new System.Drawing.Point(374, 104);
            chkUltimaConfig.Name = "chkUltimaConfig";
            chkUltimaConfig.Size = new System.Drawing.Size(193, 17);
            chkUltimaConfig.TabIndex = 9;
            chkUltimaConfig.Text = "Guardar última configuración usada";
            chkUltimaConfig.UseVisualStyleBackColor = true;
            // 
            // comboPrioridad
            // 
            comboPrioridad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboPrioridad.FormattingEnabled = true;
            comboPrioridad.Location = new System.Drawing.Point(191, 125);
            comboPrioridad.Name = "comboPrioridad";
            comboPrioridad.Size = new System.Drawing.Size(67, 21);
            comboPrioridad.TabIndex = 12;
            // 
            // chkStartWindows
            // 
            chkStartWindows.AutoSize = true;
            chkStartWindows.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkStartWindows.Location = new System.Drawing.Point(10, 104);
            chkStartWindows.Name = "chkStartWindows";
            chkStartWindows.Size = new System.Drawing.Size(122, 17);
            chkStartWindows.TabIndex = 8;
            chkStartWindows.Text = "Iniciar con Windows";
            chkStartWindows.UseVisualStyleBackColor = true;
            // 
            // txtSearchConfig
            // 
            txtSearchConfig.Location = new System.Drawing.Point(10, 12);
            txtSearchConfig.Name = "txtSearchConfig";
            txtSearchConfig.Size = new System.Drawing.Size(621, 20);
            txtSearchConfig.TabIndex = 2;
            // 
            // TabControl1
            // 
            TabControl1.Controls.Add(TabPage1);
            TabControl1.Controls.Add(TabPage2);
            TabControl1.Controls.Add(TabPage3);
            TabControl1.Controls.Add(TabPage4);
            TabControl1.Controls.Add(TabPage5);
            TabControl1.Location = new System.Drawing.Point(10, 38);
            TabControl1.Name = "TabControl1";
            TabControl1.SelectedIndex = 0;
            TabControl1.Size = new System.Drawing.Size(621, 408);
            TabControl1.TabIndex = 0;
            // 
            // TabPage1
            // 
            TabPage1.Controls.Add(GroupBox1);
            TabPage1.Controls.Add(ConexionGroup);
            TabPage1.Controls.Add(GroupBox4);
            TabPage1.Location = new System.Drawing.Point(4, 22);
            TabPage1.Name = "TabPage1";
            TabPage1.Padding = new System.Windows.Forms.Padding(3);
            TabPage1.Size = new System.Drawing.Size(613, 382);
            TabPage1.TabIndex = 0;
            TabPage1.Text = "General";
            TabPage1.UseVisualStyleBackColor = true;
            // 
            // TabPage2
            // 
            TabPage2.Controls.Add(GroupBox5);
            TabPage2.Controls.Add(GroupBox2);
            TabPage2.Controls.Add(GroupBox3);
            TabPage2.Location = new System.Drawing.Point(4, 22);
            TabPage2.Name = "TabPage2";
            TabPage2.Padding = new System.Windows.Forms.Padding(3);
            TabPage2.Size = new System.Drawing.Size(613, 382);
            TabPage2.TabIndex = 1;
            TabPage2.Text = "Avanzado";
            TabPage2.UseVisualStyleBackColor = true;
            // 
            // GroupBox5
            // 
            GroupBox5.Controls.Add(btnExaminarTemplate);
            GroupBox5.Controls.Add(txtServidorWebTemplate);
            GroupBox5.Controls.Add(Label21);
            GroupBox5.Controls.Add(txtServidorWebNombre);
            GroupBox5.Controls.Add(Label20);
            GroupBox5.Controls.Add(Label19);
            GroupBox5.Controls.Add(txtServidorWebTimeout);
            GroupBox5.Controls.Add(Label18);
            GroupBox5.Controls.Add(Label17);
            GroupBox5.Controls.Add(txtServidorWebPassword);
            GroupBox5.Controls.Add(txtServidorWebPort);
            GroupBox5.Controls.Add(Label16);
            GroupBox5.Controls.Add(chkServidorWeb);
            GroupBox5.Controls.Add(chkServidorWebLAN);
            GroupBox5.Controls.Add(lblServidorWebBindIP);
            GroupBox5.Controls.Add(txtServidorWebBindIP);
            GroupBox5.Location = new System.Drawing.Point(6, 232);
            GroupBox5.Name = "GroupBox5";
            GroupBox5.Size = new System.Drawing.Size(599, 144);
            GroupBox5.TabIndex = 2;
            GroupBox5.TabStop = false;
            GroupBox5.Text = "Servidor Web";
            // 
            // btnExaminarTemplate
            // 
            btnExaminarTemplate.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            btnExaminarTemplate.Location = new System.Drawing.Point(510, 79);
            btnExaminarTemplate.Name = "btnExaminarTemplate";
            btnExaminarTemplate.Size = new System.Drawing.Size(75, 23);
            btnExaminarTemplate.TabIndex = 10;
            btnExaminarTemplate.Text = "Examinar";
            btnExaminarTemplate.UseVisualStyleBackColor = true;
            // 
            // txtServidorWebTemplate
            // 
            txtServidorWebTemplate.AllowDrop = true;
            txtServidorWebTemplate.Location = new System.Drawing.Point(114, 81);
            txtServidorWebTemplate.Name = "txtServidorWebTemplate";
            txtServidorWebTemplate.Size = new System.Drawing.Size(388, 20);
            txtServidorWebTemplate.TabIndex = 9;
            // 
            // Label21
            // 
            Label21.AutoSize = true;
            Label21.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label21.Location = new System.Drawing.Point(12, 84);
            Label21.Name = "Label21";
            Label21.Size = new System.Drawing.Size(97, 13);
            Label21.TabIndex = 8;
            Label21.Text = "Ruta de la plantilla:";
            // 
            // txtServidorWebNombre
            // 
            txtServidorWebNombre.Location = new System.Drawing.Point(114, 115);
            txtServidorWebNombre.Name = "txtServidorWebNombre";
            txtServidorWebNombre.Size = new System.Drawing.Size(123, 20);
            txtServidorWebNombre.TabIndex = 12;
            // 
            // Label20
            // 
            Label20.AutoSize = true;
            Label20.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label20.Location = new System.Drawing.Point(12, 118);
            Label20.Name = "Label20";
            Label20.Size = new System.Drawing.Size(96, 13);
            Label20.TabIndex = 11;
            Label20.Text = "Nombre (opcional):";
            // 
            // Label19
            // 
            Label19.AutoSize = true;
            Label19.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label19.Location = new System.Drawing.Point(414, 48);
            Label19.Name = "Label19";
            Label19.Size = new System.Drawing.Size(43, 13);
            Label19.TabIndex = 5;
            Label19.Text = "minutos";
            // 
            // txtServidorWebTimeout
            // 
            txtServidorWebTimeout.Location = new System.Drawing.Point(377, 45);
            txtServidorWebTimeout.MaxLength = 2;
            txtServidorWebTimeout.Name = "txtServidorWebTimeout";
            txtServidorWebTimeout.Size = new System.Drawing.Size(31, 20);
            txtServidorWebTimeout.TabIndex = 4;
            // 
            // Label18
            // 
            Label18.AutoSize = true;
            Label18.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label18.Location = new System.Drawing.Point(251, 48);
            Label18.MinimumSize = new System.Drawing.Size(120, 0);
            Label18.Name = "Label18";
            Label18.Size = new System.Drawing.Size(120, 13);
            Label18.TabIndex = 3;
            Label18.Text = "Interrumpir sesión:";
            Label18.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Label17
            // 
            Label17.AutoSize = true;
            Label17.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label17.Location = new System.Drawing.Point(12, 48);
            Label17.MinimumSize = new System.Drawing.Size(80, 0);
            Label17.Name = "Label17";
            Label17.Size = new System.Drawing.Size(80, 13);
            Label17.TabIndex = 1;
            Label17.Text = "Contraseña:";
            Label17.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtServidorWebPassword
            // 
            txtServidorWebPassword.Location = new System.Drawing.Point(93, 45);
            txtServidorWebPassword.Name = "txtServidorWebPassword";
            txtServidorWebPassword.Size = new System.Drawing.Size(144, 20);
            txtServidorWebPassword.TabIndex = 2;
            txtServidorWebPassword.UseSystemPasswordChar = true;
            // 
            // txtServidorWebPort
            // 
            txtServidorWebPort.Location = new System.Drawing.Point(537, 45);
            txtServidorWebPort.Name = "txtServidorWebPort";
            txtServidorWebPort.Size = new System.Drawing.Size(46, 20);
            txtServidorWebPort.TabIndex = 7;
            // 
            // Label16
            // 
            Label16.AutoSize = true;
            Label16.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label16.Location = new System.Drawing.Point(490, 48);
            Label16.MinimumSize = new System.Drawing.Size(41, 0);
            Label16.Name = "Label16";
            Label16.Size = new System.Drawing.Size(41, 13);
            Label16.TabIndex = 6;
            Label16.Text = "Puerto:";
            Label16.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkServidorWeb
            // 
            chkServidorWeb.AutoSize = true;
            chkServidorWeb.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkServidorWeb.Location = new System.Drawing.Point(12, 19);
            chkServidorWeb.Name = "chkServidorWeb";
            chkServidorWeb.Size = new System.Drawing.Size(111, 17);
            chkServidorWeb.TabIndex = 0;
            chkServidorWeb.Text = "Usar servidor web";
            chkServidorWeb.UseVisualStyleBackColor = true;
            // 
            // chkServidorWebLAN
            // 
            chkServidorWebLAN.AutoSize = true;
            chkServidorWebLAN.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkServidorWebLAN.Location = new System.Drawing.Point(140, 19);
            chkServidorWebLAN.Name = "chkServidorWebLAN";
            chkServidorWebLAN.Size = new System.Drawing.Size(160, 17);
            chkServidorWebLAN.TabIndex = 8;
            chkServidorWebLAN.Text = "Permitir acceso LAN";
            chkServidorWebLAN.UseVisualStyleBackColor = true;
            // 
            // lblServidorWebBindIP
            // 
            lblServidorWebBindIP.AutoSize = true;
            lblServidorWebBindIP.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            lblServidorWebBindIP.Location = new System.Drawing.Point(310, 21);
            lblServidorWebBindIP.Name = "lblServidorWebBindIP";
            lblServidorWebBindIP.Size = new System.Drawing.Size(112, 13);
            lblServidorWebBindIP.TabIndex = 13;
            lblServidorWebBindIP.Text = "Bind IP (empty = all):";
            // 
            // txtServidorWebBindIP
            // 
            txtServidorWebBindIP.Location = new System.Drawing.Point(428, 18);
            txtServidorWebBindIP.Name = "txtServidorWebBindIP";
            txtServidorWebBindIP.Size = new System.Drawing.Size(120, 20);
            txtServidorWebBindIP.TabIndex = 14;
            // 
            // GroupBox2
            // 
            GroupBox2.Controls.Add(Label23);
            GroupBox2.Controls.Add(txtProxyName);
            GroupBox2.Controls.Add(txtProxyPassword);
            GroupBox2.Controls.Add(Label22);
            GroupBox2.Controls.Add(txtProxyPort);
            GroupBox2.Controls.Add(txtProxyIP);
            GroupBox2.Controls.Add(Label14);
            GroupBox2.Controls.Add(chkProxy);
            GroupBox2.Controls.Add(Label13);
            GroupBox2.Location = new System.Drawing.Point(6, 119);
            GroupBox2.Name = "GroupBox2";
            GroupBox2.Size = new System.Drawing.Size(599, 107);
            GroupBox2.TabIndex = 1;
            GroupBox2.TabStop = false;
            GroupBox2.Text = "Proxy";
            // 
            // Label23
            // 
            Label23.AutoSize = true;
            Label23.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label23.Location = new System.Drawing.Point(10, 76);
            Label23.MinimumSize = new System.Drawing.Size(120, 0);
            Label23.Name = "Label23";
            Label23.Size = new System.Drawing.Size(120, 13);
            Label23.TabIndex = 5;
            Label23.Text = "Usuario (opcional):";
            Label23.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtProxyName
            // 
            txtProxyName.Location = new System.Drawing.Point(135, 73);
            txtProxyName.Name = "txtProxyName";
            txtProxyName.Size = new System.Drawing.Size(144, 20);
            txtProxyName.TabIndex = 6;
            // 
            // txtProxyPassword
            // 
            txtProxyPassword.Location = new System.Drawing.Point(428, 73);
            txtProxyPassword.Name = "txtProxyPassword";
            txtProxyPassword.Size = new System.Drawing.Size(155, 20);
            txtProxyPassword.TabIndex = 8;
            txtProxyPassword.UseSystemPasswordChar = true;
            // 
            // Label22
            // 
            Label22.AutoSize = true;
            Label22.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label22.Location = new System.Drawing.Point(296, 76);
            Label22.MinimumSize = new System.Drawing.Size(130, 0);
            Label22.Name = "Label22";
            Label22.Size = new System.Drawing.Size(130, 13);
            Label22.TabIndex = 7;
            Label22.Text = "Contraseña (opcional):";
            Label22.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // TabPage3
            // 
            TabPage3.Controls.Add(GroupBox);
            TabPage3.Controls.Add(lblDesc);
            TabPage3.Location = new System.Drawing.Point(4, 22);
            TabPage3.Name = "TabPage3";
            TabPage3.Padding = new System.Windows.Forms.Padding(3);
            TabPage3.Size = new System.Drawing.Size(613, 382);
            TabPage3.TabIndex = 2;
            TabPage3.Text = "Pre-Shared Keys";
            TabPage3.UseVisualStyleBackColor = true;
            // 
            // GroupBox
            // 
            GroupBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            GroupBox.Controls.Add(txtKeyList);
            GroupBox.Location = new System.Drawing.Point(9, 65);
            GroupBox.Name = "GroupBox";
            GroupBox.Size = new System.Drawing.Size(598, 311);
            GroupBox.TabIndex = 4;
            GroupBox.TabStop = false;
            GroupBox.Text = "Listado de claves";
            // 
            // txtKeyList
            // 
            txtKeyList.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            txtKeyList.Location = new System.Drawing.Point(6, 19);
            txtKeyList.Multiline = true;
            txtKeyList.Name = "txtKeyList";
            txtKeyList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtKeyList.Size = new System.Drawing.Size(586, 286);
            txtKeyList.TabIndex = 0;
            // 
            // lblDesc
            // 
            lblDesc.AutoSize = true;
            lblDesc.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            lblDesc.Location = new System.Drawing.Point(12, 12);
            lblDesc.Name = "lblDesc";
            lblDesc.Size = new System.Drawing.Size(473, 39);
            lblDesc.TabIndex = 3;
            lblDesc.Text = resources.GetString("lblDesc.Text");
            // 
            // TabPage4
            // 
            TabPage4.Controls.Add(GroupBox8);
            TabPage4.Controls.Add(GroupBox7);
            TabPage4.Controls.Add(GroupBox6);
            TabPage4.Location = new System.Drawing.Point(4, 22);
            TabPage4.Name = "TabPage4";
            TabPage4.Padding = new System.Windows.Forms.Padding(3);
            TabPage4.Size = new System.Drawing.Size(613, 382);
            TabPage4.TabIndex = 3;
            TabPage4.Text = "Streaming";
            TabPage4.UseVisualStyleBackColor = true;
            // 
            // GroupBox8
            // 
            GroupBox8.Controls.Add(btnExaminarVLCPath);
            GroupBox8.Controls.Add(linkDownloadVLC);
            GroupBox8.Controls.Add(txtVLCPath);
            GroupBox8.Controls.Add(Label26);
            GroupBox8.Location = new System.Drawing.Point(6, 73);
            GroupBox8.Name = "GroupBox8";
            GroupBox8.Size = new System.Drawing.Size(601, 60);
            GroupBox8.TabIndex = 2;
            GroupBox8.TabStop = false;
            GroupBox8.Text = "Configuración VLC";
            // 
            // btnExaminarVLCPath
            // 
            btnExaminarVLCPath.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            btnExaminarVLCPath.Location = new System.Drawing.Point(404, 22);
            btnExaminarVLCPath.Name = "btnExaminarVLCPath";
            btnExaminarVLCPath.Size = new System.Drawing.Size(75, 23);
            btnExaminarVLCPath.TabIndex = 11;
            btnExaminarVLCPath.Text = "Examinar";
            btnExaminarVLCPath.UseVisualStyleBackColor = true;
            // 
            // linkDownloadVLC
            // 
            linkDownloadVLC.AutoSize = true;
            linkDownloadVLC.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            linkDownloadVLC.Location = new System.Drawing.Point(476, 27);
            linkDownloadVLC.MinimumSize = new System.Drawing.Size(100, 0);
            linkDownloadVLC.Name = "linkDownloadVLC";
            linkDownloadVLC.Size = new System.Drawing.Size(100, 13);
            linkDownloadVLC.TabIndex = 11;
            linkDownloadVLC.TabStop = true;
            linkDownloadVLC.Text = "Descargar VLC";
            linkDownloadVLC.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtVLCPath
            // 
            txtVLCPath.Location = new System.Drawing.Point(87, 24);
            txtVLCPath.Name = "txtVLCPath";
            txtVLCPath.Size = new System.Drawing.Size(311, 20);
            txtVLCPath.TabIndex = 10;
            // 
            // Label26
            // 
            Label26.AutoSize = true;
            Label26.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            Label26.Location = new System.Drawing.Point(6, 27);
            Label26.MinimumSize = new System.Drawing.Size(75, 0);
            Label26.Name = "Label26";
            Label26.Size = new System.Drawing.Size(75, 13);
            Label26.TabIndex = 10;
            Label26.Text = "Ruta VLC:";
            Label26.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // GroupBox7
            // 
            GroupBox7.Controls.Add(lblInfoStreaming);
            GroupBox7.Location = new System.Drawing.Point(6, 139);
            GroupBox7.Name = "GroupBox7";
            GroupBox7.Size = new System.Drawing.Size(601, 237);
            GroupBox7.TabIndex = 1;
            GroupBox7.TabStop = false;
            GroupBox7.Text = "Información";
            // 
            // lblInfoStreaming
            // 
            lblInfoStreaming.AutoSize = true;
            lblInfoStreaming.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            lblInfoStreaming.Location = new System.Drawing.Point(6, 16);
            lblInfoStreaming.MaximumSize = new System.Drawing.Size(570, 0);
            lblInfoStreaming.MinimumSize = new System.Drawing.Size(570, 0);
            lblInfoStreaming.Name = "lblInfoStreaming";
            lblInfoStreaming.Size = new System.Drawing.Size(570, 273);
            lblInfoStreaming.TabIndex = 0;
            lblInfoStreaming.Text = resources.GetString("lblInfoStreaming.Text");
            // 
            // GroupBox6
            // 
            GroupBox6.Controls.Add(txtStreamingPassword);
            GroupBox6.Controls.Add(lblStreamingPassword);
            GroupBox6.Controls.Add(txtStreamingPort);
            GroupBox6.Controls.Add(lblStreamingPort);
            GroupBox6.Controls.Add(chkStreamingServer);
            GroupBox6.Location = new System.Drawing.Point(6, 6);
            GroupBox6.Name = "GroupBox6";
            GroupBox6.Size = new System.Drawing.Size(601, 61);
            GroupBox6.TabIndex = 0;
            GroupBox6.TabStop = false;
            GroupBox6.Text = "Configuración de streaming";
            // 
            // txtStreamingPassword
            // 
            txtStreamingPassword.Location = new System.Drawing.Point(447, 26);
            txtStreamingPassword.Name = "txtStreamingPassword";
            txtStreamingPassword.Size = new System.Drawing.Size(129, 20);
            txtStreamingPassword.TabIndex = 11;
            txtStreamingPassword.UseSystemPasswordChar = true;
            // 
            // lblStreamingPassword
            // 
            lblStreamingPassword.AutoSize = true;
            lblStreamingPassword.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            lblStreamingPassword.Location = new System.Drawing.Point(316, 29);
            lblStreamingPassword.MinimumSize = new System.Drawing.Size(125, 0);
            lblStreamingPassword.Name = "lblStreamingPassword";
            lblStreamingPassword.Size = new System.Drawing.Size(125, 13);
            lblStreamingPassword.TabIndex = 10;
            lblStreamingPassword.Text = "Password (opcional):";
            lblStreamingPassword.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtStreamingPort
            // 
            txtStreamingPort.Location = new System.Drawing.Point(253, 25);
            txtStreamingPort.Name = "txtStreamingPort";
            txtStreamingPort.Size = new System.Drawing.Size(40, 20);
            txtStreamingPort.TabIndex = 9;
            // 
            // lblStreamingPort
            // 
            lblStreamingPort.AutoSize = true;
            lblStreamingPort.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            lblStreamingPort.Location = new System.Drawing.Point(206, 29);
            lblStreamingPort.MinimumSize = new System.Drawing.Size(41, 0);
            lblStreamingPort.Name = "lblStreamingPort";
            lblStreamingPort.Size = new System.Drawing.Size(41, 13);
            lblStreamingPort.TabIndex = 8;
            lblStreamingPort.Text = "Puerto:";
            lblStreamingPort.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // chkStreamingServer
            // 
            chkStreamingServer.AutoSize = true;
            chkStreamingServer.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            chkStreamingServer.Location = new System.Drawing.Point(23, 28);
            chkStreamingServer.Name = "chkStreamingServer";
            chkStreamingServer.Size = new System.Drawing.Size(151, 17);
            chkStreamingServer.TabIndex = 1;
            chkStreamingServer.Text = "Usar servidor de streaming";
            chkStreamingServer.UseVisualStyleBackColor = true;
            // 
            // TabPage5
            // 
            TabPage5.Controls.Add(ElcAccountControl);
            TabPage5.Location = new System.Drawing.Point(4, 22);
            TabPage5.Name = "TabPage5";
            TabPage5.Padding = new System.Windows.Forms.Padding(3);
            TabPage5.Size = new System.Drawing.Size(613, 382);
            TabPage5.TabIndex = 4;
            TabPage5.Text = "Cuentas ELC";
            TabPage5.UseVisualStyleBackColor = true;
            // 
            // ElcAccountControl
            // 
            ElcAccountControl.Location = new System.Drawing.Point(-2, 0);
            ElcAccountControl.Name = "ElcAccountControl";
            ElcAccountControl.Size = new System.Drawing.Size(615, 376);
            ElcAccountControl.TabIndex = 0;
            // 
            // Configuration
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(643, 493);
            Controls.Add(txtSearchConfig);
            Controls.Add(TabControl1);
            Controls.Add(btnGuardar);
            Controls.Add(btnCancel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            HelpButton = true;
            Icon = My.Resources.Resources.icono;
            ImeMode = System.Windows.Forms.ImeMode.NoControl;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Configuration";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Configuration";
            GroupBox1.ResumeLayout(false);
            GroupBox1.PerformLayout();
            GroupBox3.ResumeLayout(false);
            GroupBox3.PerformLayout();
            ConexionGroup.ResumeLayout(false);
            ConexionGroup.PerformLayout();
            GroupBox4.ResumeLayout(false);
            GroupBox4.PerformLayout();
            TabControl1.ResumeLayout(false);
            TabPage1.ResumeLayout(false);
            TabPage2.ResumeLayout(false);
            GroupBox5.ResumeLayout(false);
            GroupBox5.PerformLayout();
            GroupBox2.ResumeLayout(false);
            GroupBox2.PerformLayout();
            TabPage3.ResumeLayout(false);
            TabPage3.PerformLayout();
            GroupBox.ResumeLayout(false);
            GroupBox.PerformLayout();
            TabPage4.ResumeLayout(false);
            GroupBox8.ResumeLayout(false);
            GroupBox8.PerformLayout();
            GroupBox7.ResumeLayout(false);
            GroupBox7.PerformLayout();
            GroupBox6.ResumeLayout(false);
            GroupBox6.PerformLayout();
            TabPage5.ResumeLayout(false);
            Load += new EventHandler(Configuration_Load);
            FormClosed += new System.Windows.Forms.FormClosedEventHandler(Configuration_FormClosed);
            HelpButtonClicked += new System.ComponentModel.CancelEventHandler(HelpButtonPressed);
            ResumeLayout(false);

        }
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.Button btnExaminar;
        internal System.Windows.Forms.TextBox txtRuta;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.CheckBox chkAnalisisPortapapeles;
        internal System.Windows.Forms.CheckBox chkUnZip;
        internal System.Windows.Forms.CheckBox chkCrearDirectorio;
        internal System.Windows.Forms.TextBox txtPassword;
        internal System.Windows.Forms.TextBox txtUsuario;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Button btnCancel;
        internal System.Windows.Forms.Button btnGuardar;
        internal System.Windows.Forms.GroupBox GroupBox3;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.TextBox txtTamanoBuffer;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.TextBox txtTamanoPaquete;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.Label Label8;
        internal System.Windows.Forms.LinkLabel LinkLabel1;
        internal System.Windows.Forms.TextBox txtDescSimult;
        internal System.Windows.Forms.Label Label10;
        internal System.Windows.Forms.TextBox txtConFic;
        internal System.Windows.Forms.Label Label9;
        internal System.Windows.Forms.ComboBox comboLog;
        internal System.Windows.Forms.Label Label11;
        internal System.Windows.Forms.Label Label12;
        internal System.Windows.Forms.TextBox txtPeriodoReintento;
        internal System.Windows.Forms.CheckBox chkReintentarError;
        internal System.Windows.Forms.CheckBox chkQuotaBreak;
        internal System.Windows.Forms.CheckBox chkShowPassword;
        internal System.Windows.Forms.TextBox txtProxyPort;
        internal System.Windows.Forms.Label Label14;
        internal System.Windows.Forms.TextBox txtProxyIP;
        internal System.Windows.Forms.Label Label13;
        internal System.Windows.Forms.CheckBox chkProxy;
        internal System.Windows.Forms.CheckBox chkApagarPC;
        internal System.Windows.Forms.LinkLabel linkApagarPC;
        internal System.Windows.Forms.CheckBox chkComenzarPlay;
        internal System.Windows.Forms.GroupBox ConexionGroup;
        internal System.Windows.Forms.Label Label15;
        internal System.Windows.Forms.TextBox txtLimiteVelocidadKBs;
        internal System.Windows.Forms.CheckBox chkLimitarVelocidad;
        internal System.Windows.Forms.GroupBox GroupBox4;
        internal System.Windows.Forms.CheckBox chkStartWindows;
        internal System.Windows.Forms.ComboBox comboPrioridad;
        internal System.Windows.Forms.TabControl TabControl1;
        internal System.Windows.Forms.TabPage TabPage1;
        internal System.Windows.Forms.TabPage TabPage2;
        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.GroupBox GroupBox5;
        internal System.Windows.Forms.Button btnExaminarTemplate;
        internal System.Windows.Forms.TextBox txtServidorWebTemplate;
        internal System.Windows.Forms.Label Label21;
        internal System.Windows.Forms.TextBox txtServidorWebNombre;
        internal System.Windows.Forms.Label Label20;
        internal System.Windows.Forms.Label Label19;
        internal System.Windows.Forms.TextBox txtServidorWebTimeout;
        internal System.Windows.Forms.Label Label18;
        internal System.Windows.Forms.Label Label17;
        internal System.Windows.Forms.TextBox txtServidorWebPassword;
        internal System.Windows.Forms.TextBox txtServidorWebPort;
        internal System.Windows.Forms.Label Label16;
        internal System.Windows.Forms.CheckBox chkServidorWeb;
        internal System.Windows.Forms.CheckBox chkServidorWebLAN;
        internal System.Windows.Forms.Label lblServidorWebBindIP;
        internal System.Windows.Forms.TextBox txtServidorWebBindIP;
        internal System.Windows.Forms.LinkLabel linkUltConfig;
        internal System.Windows.Forms.CheckBox chkUltimaConfig;
        internal System.Windows.Forms.Label Label23;
        internal System.Windows.Forms.TextBox txtProxyName;
        internal System.Windows.Forms.TextBox txtProxyPassword;
        internal System.Windows.Forms.Label Label22;
        internal System.Windows.Forms.ComboBox comboIdiomas;
        internal System.Windows.Forms.Label Label24;
        internal System.Windows.Forms.ComboBox comboTheme;
        internal System.Windows.Forms.Label Label25;
        internal System.Windows.Forms.TabPage TabPage3;
        internal System.Windows.Forms.Label lblDesc;
        internal System.Windows.Forms.GroupBox GroupBox;
        internal System.Windows.Forms.TextBox txtKeyList;
        internal System.Windows.Forms.TabPage TabPage4;
        internal System.Windows.Forms.GroupBox GroupBox7;
        internal System.Windows.Forms.Label lblInfoStreaming;
        internal System.Windows.Forms.GroupBox GroupBox6;
        internal System.Windows.Forms.TextBox txtStreamingPort;
        internal System.Windows.Forms.Label lblStreamingPort;
        internal System.Windows.Forms.CheckBox chkStreamingServer;
        internal System.Windows.Forms.GroupBox GroupBox8;
        internal System.Windows.Forms.Button btnExaminarVLCPath;
        internal System.Windows.Forms.LinkLabel linkDownloadVLC;
        internal System.Windows.Forms.TextBox txtVLCPath;
        internal System.Windows.Forms.Label Label26;
        internal System.Windows.Forms.TabPage TabPage5;
        internal ELCAccountControl ElcAccountControl;
        internal System.Windows.Forms.TextBox txtStreamingPassword;
        internal System.Windows.Forms.Label lblStreamingPassword;
        internal System.Windows.Forms.CheckBox chkCheckUpdates;
        internal System.Windows.Forms.CheckBox chkUpdateManualOnly;
        internal System.Windows.Forms.TextBox txtUpdateCheckInterval;
        internal System.Windows.Forms.TextBox txtSearchConfig;
    }
}