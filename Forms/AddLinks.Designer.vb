<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class AddLinks
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
        Me.FlowLayoutPanel1 = New System.Windows.Forms.FlowLayoutPanel()
        Me.OpcionesPaquete = New System.Windows.Forms.GroupBox()
        Me.LinkLabel2 = New System.Windows.Forms.LinkLabel()
        Me.chkStartDownload = New System.Windows.Forms.CheckBox()
        Me.lblPassword = New System.Windows.Forms.Label()
        Me.chkUnZip = New System.Windows.Forms.CheckBox()
        Me.txtPassword = New System.Windows.Forms.TextBox()
        Me.chkCrearDirectorio = New System.Windows.Forms.CheckBox()
        Me.btnExaminar = New System.Windows.Forms.Button()
        Me.txtRuta = New System.Windows.Forms.TextBox()
        Me.txtNombre = New System.Windows.Forms.TextBox()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.LinkLabel1 = New System.Windows.Forms.LinkLabel()
        Me.btnAgregar = New System.Windows.Forms.Button()
        Me.btnWatchOnline = New System.Windows.Forms.Button()
        Me.txtLinks = New System.Windows.Forms.RichTextBox()
        Me.linkStegano = New System.Windows.Forms.LinkLabel()
        Me.OpcionesPaquete.SuspendLayout()
        Me.SuspendLayout()
        '
        'FlowLayoutPanel1
        '
        Me.FlowLayoutPanel1.AutoSize = True
        Me.FlowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.FlowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown
        Me.FlowLayoutPanel1.Location = New System.Drawing.Point(12, 186)
        Me.FlowLayoutPanel1.Name = "FlowLayoutPanel1"
        Me.FlowLayoutPanel1.Size = New System.Drawing.Size(0, 0)
        Me.FlowLayoutPanel1.TabIndex = 1
        '
        'OpcionesPaquete
        '
        Me.OpcionesPaquete.Controls.Add(Me.LinkLabel2)
        Me.OpcionesPaquete.Controls.Add(Me.chkStartDownload)
        Me.OpcionesPaquete.Controls.Add(Me.lblPassword)
        Me.OpcionesPaquete.Controls.Add(Me.chkUnZip)
        Me.OpcionesPaquete.Controls.Add(Me.txtPassword)
        Me.OpcionesPaquete.Controls.Add(Me.chkCrearDirectorio)
        Me.OpcionesPaquete.Controls.Add(Me.btnExaminar)
        Me.OpcionesPaquete.Controls.Add(Me.txtRuta)
        Me.OpcionesPaquete.Controls.Add(Me.txtNombre)
        Me.OpcionesPaquete.Controls.Add(Me.Label2)
        Me.OpcionesPaquete.Controls.Add(Me.Label1)
        Me.OpcionesPaquete.Location = New System.Drawing.Point(12, 215)
        Me.OpcionesPaquete.Name = "OpcionesPaquete"
        Me.OpcionesPaquete.Size = New System.Drawing.Size(649, 111)
        Me.OpcionesPaquete.TabIndex = 4
        Me.OpcionesPaquete.TabStop = False
        Me.OpcionesPaquete.Text = "Opciones"
        '
        'LinkLabel2
        '
        Me.LinkLabel2.AutoSize = True
        Me.LinkLabel2.Location = New System.Drawing.Point(506, 86)
        Me.LinkLabel2.MinimumSize = New System.Drawing.Size(20, 0)
        Me.LinkLabel2.Name = "LinkLabel2"
        Me.LinkLabel2.Size = New System.Drawing.Size(20, 13)
        Me.LinkLabel2.TabIndex = 32
        Me.LinkLabel2.TabStop = True
        Me.LinkLabel2.Text = "[?]"
        Me.LinkLabel2.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkStartDownload
        '
        Me.chkStartDownload.AutoSize = True
        Me.chkStartDownload.Location = New System.Drawing.Point(59, 85)
        Me.chkStartDownload.MinimumSize = New System.Drawing.Size(97, 0)
        Me.chkStartDownload.Name = "chkStartDownload"
        Me.chkStartDownload.Size = New System.Drawing.Size(101, 17)
        Me.chkStartDownload.TabIndex = 7
        Me.chkStartDownload.Text = "Iniciar descarga"
        Me.chkStartDownload.UseVisualStyleBackColor = True
        '
        'lblPassword
        '
        Me.lblPassword.AutoSize = True
        Me.lblPassword.Location = New System.Drawing.Point(435, 86)
        Me.lblPassword.MinimumSize = New System.Drawing.Size(75, 0)
        Me.lblPassword.Name = "lblPassword"
        Me.lblPassword.Size = New System.Drawing.Size(75, 13)
        Me.lblPassword.TabIndex = 31
        Me.lblPassword.Text = "Password"
        Me.lblPassword.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'chkUnZip
        '
        Me.chkUnZip.AutoSize = True
        Me.chkUnZip.Location = New System.Drawing.Point(304, 85)
        Me.chkUnZip.Name = "chkUnZip"
        Me.chkUnZip.Size = New System.Drawing.Size(131, 17)
        Me.chkUnZip.TabIndex = 2
        Me.chkUnZip.Text = "Extracción automática"
        Me.chkUnZip.UseVisualStyleBackColor = True
        '
        'txtPassword
        '
        Me.txtPassword.Location = New System.Drawing.Point(532, 83)
        Me.txtPassword.MaxLength = 6
        Me.txtPassword.Name = "txtPassword"
        Me.txtPassword.Size = New System.Drawing.Size(102, 20)
        Me.txtPassword.TabIndex = 30
        Me.txtPassword.UseSystemPasswordChar = True
        '
        'chkCrearDirectorio
        '
        Me.chkCrearDirectorio.AutoSize = True
        Me.chkCrearDirectorio.Location = New System.Drawing.Point(178, 85)
        Me.chkCrearDirectorio.MinimumSize = New System.Drawing.Size(97, 0)
        Me.chkCrearDirectorio.Name = "chkCrearDirectorio"
        Me.chkCrearDirectorio.Size = New System.Drawing.Size(97, 17)
        Me.chkCrearDirectorio.TabIndex = 3
        Me.chkCrearDirectorio.Text = "Crear directorio"
        Me.chkCrearDirectorio.UseVisualStyleBackColor = True
        '
        'btnExaminar
        '
        Me.btnExaminar.Location = New System.Drawing.Point(559, 51)
        Me.btnExaminar.Name = "btnExaminar"
        Me.btnExaminar.Size = New System.Drawing.Size(75, 23)
        Me.btnExaminar.TabIndex = 6
        Me.btnExaminar.Text = "Examinar"
        Me.btnExaminar.UseVisualStyleBackColor = True
        '
        'txtRuta
        '
        Me.txtRuta.Location = New System.Drawing.Point(59, 53)
        Me.txtRuta.Name = "txtRuta"
        Me.txtRuta.Size = New System.Drawing.Size(494, 20)
        Me.txtRuta.TabIndex = 5
        '
        'txtNombre
        '
        Me.txtNombre.Location = New System.Drawing.Point(59, 24)
        Me.txtNombre.Name = "txtNombre"
        Me.txtNombre.Size = New System.Drawing.Size(575, 20)
        Me.txtNombre.TabIndex = 1
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(6, 27)
        Me.Label2.MinimumSize = New System.Drawing.Size(47, 0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(47, 13)
        Me.Label2.TabIndex = 0
        Me.Label2.Text = "Nombre:"
        Me.Label2.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(6, 56)
        Me.Label1.MinimumSize = New System.Drawing.Size(47, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(47, 13)
        Me.Label1.TabIndex = 4
        Me.Label1.Text = "Ruta:"
        Me.Label1.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'LinkLabel1
        '
        Me.LinkLabel1.AutoSize = True
        Me.LinkLabel1.Location = New System.Drawing.Point(310, 191)
        Me.LinkLabel1.Name = "LinkLabel1"
        Me.LinkLabel1.Size = New System.Drawing.Size(19, 13)
        Me.LinkLabel1.TabIndex = 11
        Me.LinkLabel1.TabStop = True
        Me.LinkLabel1.Text = "[?]"
        Me.LinkLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'btnAgregar
        '
        Me.btnAgregar.Location = New System.Drawing.Point(12, 186)
        Me.btnAgregar.Name = "btnAgregar"
        Me.btnAgregar.Size = New System.Drawing.Size(133, 23)
        Me.btnAgregar.TabIndex = 1
        Me.btnAgregar.Text = "Agregar links"
        Me.btnAgregar.UseVisualStyleBackColor = True
        '
        'btnWatchOnline
        '
        Me.btnWatchOnline.Location = New System.Drawing.Point(171, 186)
        Me.btnWatchOnline.Name = "btnWatchOnline"
        Me.btnWatchOnline.Size = New System.Drawing.Size(133, 23)
        Me.btnWatchOnline.TabIndex = 5
        Me.btnWatchOnline.Text = "Ver Online"
        Me.btnWatchOnline.UseVisualStyleBackColor = True
        '
        'txtLinks
        '
        Me.txtLinks.Location = New System.Drawing.Point(12, 12)
        Me.txtLinks.Name = "txtLinks"
        Me.txtLinks.Size = New System.Drawing.Size(649, 168)
        Me.txtLinks.TabIndex = 12
        Me.txtLinks.Text = ""
        '
        'linkStegano
        '
        Me.linkStegano.AutoSize = True
        Me.linkStegano.Location = New System.Drawing.Point(411, 191)
        Me.linkStegano.MinimumSize = New System.Drawing.Size(250, 0)
        Me.linkStegano.Name = "linkStegano"
        Me.linkStegano.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.linkStegano.Size = New System.Drawing.Size(250, 13)
        Me.linkStegano.TabIndex = 13
        Me.linkStegano.TabStop = True
        Me.linkStegano.Text = "Recuperar enlaces de una imagen"
        Me.linkStegano.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'AddLinks
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(675, 338)
        Me.Controls.Add(Me.linkStegano)
        Me.Controls.Add(Me.txtLinks)
        Me.Controls.Add(Me.LinkLabel1)
        Me.Controls.Add(Me.btnWatchOnline)
        Me.Controls.Add(Me.btnAgregar)
        Me.Controls.Add(Me.OpcionesPaquete)
        Me.Controls.Add(Me.FlowLayoutPanel1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.MaximizeBox = False
        Me.MaximumSize = New System.Drawing.Size(1500, 1500)
        Me.MinimizeBox = False
        Me.Name = "AddLinks"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "AddLinks"
        Me.OpcionesPaquete.ResumeLayout(False)
        Me.OpcionesPaquete.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents FlowLayoutPanel1 As System.Windows.Forms.FlowLayoutPanel
    Friend WithEvents OpcionesPaquete As System.Windows.Forms.GroupBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents txtNombre As System.Windows.Forms.TextBox
    Friend WithEvents txtRuta As System.Windows.Forms.TextBox
    Friend WithEvents chkCrearDirectorio As System.Windows.Forms.CheckBox
    Friend WithEvents btnExaminar As System.Windows.Forms.Button
    Friend WithEvents btnAgregar As System.Windows.Forms.Button
    Friend WithEvents chkUnZip As System.Windows.Forms.CheckBox
    Friend WithEvents btnWatchOnline As System.Windows.Forms.Button
    Friend WithEvents LinkLabel1 As System.Windows.Forms.LinkLabel
    Friend WithEvents chkStartDownload As System.Windows.Forms.CheckBox
    Friend WithEvents LinkLabel2 As System.Windows.Forms.LinkLabel
    Friend WithEvents lblPassword As System.Windows.Forms.Label
    Friend WithEvents txtPassword As System.Windows.Forms.TextBox
    Friend WithEvents txtLinks As System.Windows.Forms.RichTextBox
    Friend WithEvents linkStegano As System.Windows.Forms.LinkLabel
End Class
