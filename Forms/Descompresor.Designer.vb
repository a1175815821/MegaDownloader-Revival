<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Descompresor
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
        Me.gbEstado = New System.Windows.Forms.GroupBox()
        Me.lblTamano = New System.Windows.Forms.Label()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.lblActual = New System.Windows.Forms.Label()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.lblFichero = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.txtCola = New System.Windows.Forms.TextBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.gbEstado.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        Me.SuspendLayout()
        '
        'gbEstado
        '
        Me.gbEstado.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.gbEstado.Controls.Add(Me.lblTamano)
        Me.gbEstado.Controls.Add(Me.Label6)
        Me.gbEstado.Controls.Add(Me.lblActual)
        Me.gbEstado.Controls.Add(Me.Label4)
        Me.gbEstado.Controls.Add(Me.lblFichero)
        Me.gbEstado.Controls.Add(Me.Label2)
        Me.gbEstado.Location = New System.Drawing.Point(12, 48)
        Me.gbEstado.Name = "gbEstado"
        Me.gbEstado.Size = New System.Drawing.Size(504, 81)
        Me.gbEstado.TabIndex = 0
        Me.gbEstado.TabStop = False
        Me.gbEstado.Text = "Estado"
        '
        'lblTamano
        '
        Me.lblTamano.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblTamano.AutoSize = True
        Me.lblTamano.Location = New System.Drawing.Point(390, 50)
        Me.lblTamano.MinimumSize = New System.Drawing.Size(100, 0)
        Me.lblTamano.Name = "lblTamano"
        Me.lblTamano.Size = New System.Drawing.Size(100, 13)
        Me.lblTamano.TabIndex = 5
        Me.lblTamano.Text = "  -"
        '
        'Label6
        '
        Me.Label6.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(343, 50)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(49, 13)
        Me.Label6.TabIndex = 4
        Me.Label6.Text = "Tamaño:"
        '
        'lblActual
        '
        Me.lblActual.AutoSize = True
        Me.lblActual.Location = New System.Drawing.Point(57, 50)
        Me.lblActual.Name = "lblActual"
        Me.lblActual.Size = New System.Drawing.Size(10, 13)
        Me.lblActual.TabIndex = 3
        Me.lblActual.Text = "-"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(6, 50)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(40, 13)
        Me.Label4.TabIndex = 2
        Me.Label4.Text = "Actual:"
        '
        'lblFichero
        '
        Me.lblFichero.AutoSize = True
        Me.lblFichero.Location = New System.Drawing.Point(57, 25)
        Me.lblFichero.Name = "lblFichero"
        Me.lblFichero.Size = New System.Drawing.Size(10, 13)
        Me.lblFichero.TabIndex = 1
        Me.lblFichero.Text = "-"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(6, 25)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(45, 13)
        Me.Label2.TabIndex = 0
        Me.Label2.Text = "Fichero:"
        '
        'GroupBox2
        '
        Me.GroupBox2.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBox2.Controls.Add(Me.txtCola)
        Me.GroupBox2.Location = New System.Drawing.Point(12, 135)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(504, 88)
        Me.GroupBox2.TabIndex = 1
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "Cola de descompresión"
        '
        'txtCola
        '
        Me.txtCola.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtCola.Enabled = False
        Me.txtCola.Location = New System.Drawing.Point(6, 19)
        Me.txtCola.Multiline = True
        Me.txtCola.Name = "txtCola"
        Me.txtCola.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtCola.Size = New System.Drawing.Size(492, 63)
        Me.txtCola.TabIndex = 0
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 9)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(388, 26)
        Me.Label1.TabIndex = 2
        Me.Label1.Text = "Esta pantalla le permite ver el estado de los ficheros pendientes de descomprimir" & _
    " " & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "(si ha activado la opción de extraer automáticamente)."
        '
        'Descompresor
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(528, 235)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.GroupBox2)
        Me.Controls.Add(Me.gbEstado)
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.MinimumSize = New System.Drawing.Size(400, 250)
        Me.Name = "Descompresor"
        Me.Text = "Descompresor de ficheros"
        Me.gbEstado.ResumeLayout(False)
        Me.gbEstado.PerformLayout()
        Me.GroupBox2.ResumeLayout(False)
        Me.GroupBox2.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents gbEstado As System.Windows.Forms.GroupBox
    Friend WithEvents lblTamano As System.Windows.Forms.Label
    Friend WithEvents Label6 As System.Windows.Forms.Label
    Friend WithEvents lblActual As System.Windows.Forms.Label
    Friend WithEvents Label4 As System.Windows.Forms.Label
    Friend WithEvents lblFichero As System.Windows.Forms.Label
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents GroupBox2 As System.Windows.Forms.GroupBox
    Friend WithEvents txtCola As System.Windows.Forms.TextBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
End Class
