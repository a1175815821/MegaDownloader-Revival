<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Credits
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
        Me.Button1 = New System.Windows.Forms.Button()
        Me.lblTitle = New System.Windows.Forms.LinkLabel()
        Me.lblAutor = New System.Windows.Forms.Label()
        Me.lblEmail = New System.Windows.Forms.Label()
        Me.lblGraciasA = New System.Windows.Forms.Label()
        Me.lblListaColaboradores = New System.Windows.Forms.Label()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.PictureBox1 = New System.Windows.Forms.PictureBox()
        Me.Label2 = New System.Windows.Forms.Label()
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Button1
        '
        Me.Button1.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Button1.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.Button1.Location = New System.Drawing.Point(314, 267)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(75, 23)
        Me.Button1.TabIndex = 0
        Me.Button1.Text = "Cerrar"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'lblTitle
        '
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Font = New System.Drawing.Font("Microsoft Sans Serif", 10.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblTitle.Location = New System.Drawing.Point(149, 25)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(190, 17)
        Me.lblTitle.TabIndex = 1
        Me.lblTitle.TabStop = True
        Me.lblTitle.Text = "MegaDownloader BETA v"
        '
        'lblAutor
        '
        Me.lblAutor.AutoSize = True
        Me.lblAutor.Location = New System.Drawing.Point(149, 74)
        Me.lblAutor.Name = "lblAutor"
        Me.lblAutor.Size = New System.Drawing.Size(173, 13)
        Me.lblAutor.TabIndex = 2
        Me.lblAutor.Text = "Creado y diseñado por Andres_age"
        '
        'lblEmail
        '
        Me.lblEmail.AutoSize = True
        Me.lblEmail.Location = New System.Drawing.Point(194, 96)
        Me.lblEmail.Name = "lblEmail"
        Me.lblEmail.Size = New System.Drawing.Size(118, 13)
        Me.lblEmail.TabIndex = 3
        Me.lblEmail.Text = "andres.age@gmail.com"
        '
        'lblGraciasA
        '
        Me.lblGraciasA.AutoSize = True
        Me.lblGraciasA.Location = New System.Drawing.Point(9, 197)
        Me.lblGraciasA.Name = "lblGraciasA"
        Me.lblGraciasA.Size = New System.Drawing.Size(55, 13)
        Me.lblGraciasA.TabIndex = 4
        Me.lblGraciasA.Text = "Gracias a:"
        '
        'lblListaColaboradores
        '
        Me.lblListaColaboradores.AutoSize = True
        Me.lblListaColaboradores.Location = New System.Drawing.Point(20, 219)
        Me.lblListaColaboradores.Name = "lblListaColaboradores"
        Me.lblListaColaboradores.Size = New System.Drawing.Size(62, 39)
        Me.lblListaColaboradores.TabIndex = 5
        Me.lblListaColaboradores.Text = "* ------------" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "* ----------------" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "* ----------------" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10)
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 130)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(350, 13)
        Me.Label1.TabIndex = 6
        Me.Label1.Text = "Gracias por usar esta aplicación no oficial de descarga de MEGA.CO.NZ"
        '
        'PictureBox1
        '
        Me.PictureBox1.Image = Global.MegaDownloader.My.Resources.Resources.mega
        Me.PictureBox1.Location = New System.Drawing.Point(15, 25)
        Me.PictureBox1.Name = "PictureBox1"
        Me.PictureBox1.Size = New System.Drawing.Size(128, 84)
        Me.PictureBox1.TabIndex = 7
        Me.PictureBox1.TabStop = False
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(12, 159)
        Me.Label2.MaximumSize = New System.Drawing.Size(350, 0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(325, 26)
        Me.Label2.TabIndex = 8
        Me.Label2.Text = "MegaDownloader no está relacionado con Mega.co.nz - Todas las marcas y logos son " & _
    "propiedad de sus respectivos dueños."
        '
        'Credits
        '
        Me.AcceptButton = Me.lblTitle
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoSize = True
        Me.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.CancelButton = Me.Button1
        Me.ClientSize = New System.Drawing.Size(401, 302)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.PictureBox1)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.lblListaColaboradores)
        Me.Controls.Add(Me.lblGraciasA)
        Me.Controls.Add(Me.lblEmail)
        Me.Controls.Add(Me.lblAutor)
        Me.Controls.Add(Me.lblTitle)
        Me.Controls.Add(Me.Button1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Credits"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Información"
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents Button1 As System.Windows.Forms.Button
    Friend WithEvents lblAutor As System.Windows.Forms.Label
    Friend WithEvents lblEmail As System.Windows.Forms.Label
    Friend WithEvents lblGraciasA As System.Windows.Forms.Label
    Friend WithEvents lblListaColaboradores As System.Windows.Forms.Label
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents PictureBox1 As System.Windows.Forms.PictureBox
    Friend WithEvents lblTitle As System.Windows.Forms.LinkLabel
    Friend WithEvents Label2 As System.Windows.Forms.Label
End Class
