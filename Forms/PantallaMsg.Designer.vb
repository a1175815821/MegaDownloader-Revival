<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class PantallaMsg
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
        Me.txtDatos = New System.Windows.Forms.RichTextBox()
        Me.chkCodificarEnlaces = New System.Windows.Forms.CheckBox()
        Me.lklGenerarELC = New System.Windows.Forms.LinkLabel()
        Me.SuspendLayout()
        '
        'Button1
        '
        Me.Button1.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Button1.Location = New System.Drawing.Point(638, 234)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(75, 23)
        Me.Button1.TabIndex = 0
        Me.Button1.Text = "Cerrar"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'txtDatos
        '
        Me.txtDatos.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtDatos.Location = New System.Drawing.Point(12, 12)
        Me.txtDatos.Name = "txtDatos"
        Me.txtDatos.ReadOnly = True
        Me.txtDatos.Size = New System.Drawing.Size(701, 216)
        Me.txtDatos.TabIndex = 1
        Me.txtDatos.Text = ""
        '
        'chkCodificarEnlaces
        '
        Me.chkCodificarEnlaces.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.chkCodificarEnlaces.AutoSize = True
        Me.chkCodificarEnlaces.Location = New System.Drawing.Point(13, 239)
        Me.chkCodificarEnlaces.Name = "chkCodificarEnlaces"
        Me.chkCodificarEnlaces.Size = New System.Drawing.Size(107, 17)
        Me.chkCodificarEnlaces.TabIndex = 2
        Me.chkCodificarEnlaces.Text = "Codificar enlaces"
        Me.chkCodificarEnlaces.UseVisualStyleBackColor = True
        '
        'lklGenerarELC
        '
        Me.lklGenerarELC.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.lklGenerarELC.Location = New System.Drawing.Point(143, 240)
        Me.lklGenerarELC.Name = "lklGenerarELC"
        Me.lklGenerarELC.Size = New System.Drawing.Size(100, 23)
        Me.lklGenerarELC.TabIndex = 3
        Me.lklGenerarELC.TabStop = True
        Me.lklGenerarELC.Text = "Generar ELC"
        '
        'PantallaMsg
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(725, 269)
        Me.Controls.Add(Me.lklGenerarELC)
        Me.Controls.Add(Me.chkCodificarEnlaces)
        Me.Controls.Add(Me.txtDatos)
        Me.Controls.Add(Me.Button1)
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.Name = "PantallaMsg"
        Me.Text = "Información"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents lklGenerarELC As System.Windows.Forms.LinkLabel
    Friend WithEvents Button1 As System.Windows.Forms.Button
    Friend WithEvents txtDatos As System.Windows.Forms.RichTextBox
    Friend WithEvents chkCodificarEnlaces As System.Windows.Forms.CheckBox
End Class
