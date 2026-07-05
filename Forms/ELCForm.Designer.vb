'
' Created by SharpDevelop.
' User: asolino
' Date: 25/03/2013
' Time: 10:51
' 
' To change this template use Tools | Options | Coding | Edit Standard Headers.
'
Partial Class ELCForm
	Inherits System.Windows.Forms.Form
	
	''' <summary>
	''' Designer variable used to keep track of non-visual components.
	''' </summary>
	Private components As System.ComponentModel.IContainer
	
	''' <summary>
	''' Disposes resources used by the form.
	''' </summary>
	''' <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
	Protected Overrides Sub Dispose(ByVal disposing As Boolean)
		If disposing Then
			If components IsNot Nothing Then
				components.Dispose()
			End If
		End If
		MyBase.Dispose(disposing)
	End Sub
	
	''' <summary>
	''' This method is required for Windows Forms designer support.
	''' Do not change the method contents inside the source code editor. The Forms designer might
	''' not be able to load this method if it was changed manually.
	''' </summary>
	Private Sub InitializeComponent()
        Me.gbMEGAUrl = New System.Windows.Forms.GroupBox()
        Me.chkMultipleELCs = New System.Windows.Forms.CheckBox()
        Me.label3 = New System.Windows.Forms.Label()
        Me.btnCrearELC = New System.Windows.Forms.Button()
        Me.comboELCAccounts = New System.Windows.Forms.ComboBox()
        Me.txtMegaURLs = New System.Windows.Forms.TextBox()
        Me.lblExplanation = New System.Windows.Forms.Label()
        Me.gbELCLink = New System.Windows.Forms.GroupBox()
        Me.lblExplanation2 = New System.Windows.Forms.Label()
        Me.btnSaveFile = New System.Windows.Forms.Button()
        Me.btnExaminar = New System.Windows.Forms.Button()
        Me.lblSaveAsELC = New System.Windows.Forms.Label()
        Me.txtExaminar = New System.Windows.Forms.TextBox()
        Me.txtELCUrl = New System.Windows.Forms.TextBox()
        Me.btnCerrar = New System.Windows.Forms.Button()
        Me.gbMEGAUrl.SuspendLayout()
        Me.gbELCLink.SuspendLayout()
        Me.SuspendLayout()
        '
        'gbMEGAUrl
        '
        Me.gbMEGAUrl.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.gbMEGAUrl.Controls.Add(Me.chkMultipleELCs)
        Me.gbMEGAUrl.Controls.Add(Me.label3)
        Me.gbMEGAUrl.Controls.Add(Me.btnCrearELC)
        Me.gbMEGAUrl.Controls.Add(Me.comboELCAccounts)
        Me.gbMEGAUrl.Controls.Add(Me.txtMegaURLs)
        Me.gbMEGAUrl.Controls.Add(Me.lblExplanation)
        Me.gbMEGAUrl.Location = New System.Drawing.Point(12, 12)
        Me.gbMEGAUrl.Name = "gbMEGAUrl"
        Me.gbMEGAUrl.Size = New System.Drawing.Size(658, 181)
        Me.gbMEGAUrl.TabIndex = 0
        Me.gbMEGAUrl.TabStop = False
        Me.gbMEGAUrl.Text = "groupBox1"
        '
        'chkMultipleELCs
        '
        Me.chkMultipleELCs.AutoSize = True
        Me.chkMultipleELCs.Location = New System.Drawing.Point(322, 152)
        Me.chkMultipleELCs.Name = "chkMultipleELCs"
        Me.chkMultipleELCs.Size = New System.Drawing.Size(85, 17)
        Me.chkMultipleELCs.TabIndex = 5
        Me.chkMultipleELCs.Text = "Multiple ELC"
        Me.chkMultipleELCs.UseVisualStyleBackColor = True
        '
        'label3
        '
        Me.label3.Location = New System.Drawing.Point(6, 146)
        Me.label3.Name = "label3"
        Me.label3.Size = New System.Drawing.Size(100, 23)
        Me.label3.TabIndex = 4
        Me.label3.Text = "Cuenta ELC:"
        Me.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'btnCrearELC
        '
        Me.btnCrearELC.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnCrearELC.Location = New System.Drawing.Point(499, 148)
        Me.btnCrearELC.Name = "btnCrearELC"
        Me.btnCrearELC.Size = New System.Drawing.Size(144, 23)
        Me.btnCrearELC.TabIndex = 3
        Me.btnCrearELC.Text = "Generar ELC"
        Me.btnCrearELC.UseVisualStyleBackColor = True
        '
        'comboELCAccounts
        '
        Me.comboELCAccounts.FormattingEnabled = True
        Me.comboELCAccounts.Location = New System.Drawing.Point(112, 148)
        Me.comboELCAccounts.Name = "comboELCAccounts"
        Me.comboELCAccounts.Size = New System.Drawing.Size(195, 21)
        Me.comboELCAccounts.TabIndex = 2
        '
        'txtMegaURLs
        '
        Me.txtMegaURLs.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtMegaURLs.Location = New System.Drawing.Point(14, 42)
        Me.txtMegaURLs.Multiline = True
        Me.txtMegaURLs.Name = "txtMegaURLs"
        Me.txtMegaURLs.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtMegaURLs.Size = New System.Drawing.Size(629, 100)
        Me.txtMegaURLs.TabIndex = 1
        '
        'lblExplanation
        '
        Me.lblExplanation.Location = New System.Drawing.Point(6, 16)
        Me.lblExplanation.Name = "lblExplanation"
        Me.lblExplanation.Size = New System.Drawing.Size(495, 23)
        Me.lblExplanation.TabIndex = 0
        Me.lblExplanation.Text = "label1"
        Me.lblExplanation.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'gbELCLink
        '
        Me.gbELCLink.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.gbELCLink.Controls.Add(Me.lblExplanation2)
        Me.gbELCLink.Controls.Add(Me.btnSaveFile)
        Me.gbELCLink.Controls.Add(Me.btnExaminar)
        Me.gbELCLink.Controls.Add(Me.lblSaveAsELC)
        Me.gbELCLink.Controls.Add(Me.txtExaminar)
        Me.gbELCLink.Controls.Add(Me.txtELCUrl)
        Me.gbELCLink.Location = New System.Drawing.Point(12, 198)
        Me.gbELCLink.Name = "gbELCLink"
        Me.gbELCLink.Size = New System.Drawing.Size(658, 180)
        Me.gbELCLink.TabIndex = 1
        Me.gbELCLink.TabStop = False
        Me.gbELCLink.Text = "groupBox2"
        '
        'lblExplanation2
        '
        Me.lblExplanation2.Location = New System.Drawing.Point(6, 16)
        Me.lblExplanation2.Name = "lblExplanation2"
        Me.lblExplanation2.Size = New System.Drawing.Size(495, 23)
        Me.lblExplanation2.TabIndex = 4
        Me.lblExplanation2.Text = "label2"
        Me.lblExplanation2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'btnSaveFile
        '
        Me.btnSaveFile.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnSaveFile.Location = New System.Drawing.Point(499, 147)
        Me.btnSaveFile.Name = "btnSaveFile"
        Me.btnSaveFile.Size = New System.Drawing.Size(144, 23)
        Me.btnSaveFile.TabIndex = 4
        Me.btnSaveFile.Text = "Guardar fichero"
        Me.btnSaveFile.UseVisualStyleBackColor = True
        '
        'btnExaminar
        '
        Me.btnExaminar.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnExaminar.Location = New System.Drawing.Point(372, 147)
        Me.btnExaminar.Name = "btnExaminar"
        Me.btnExaminar.Size = New System.Drawing.Size(75, 23)
        Me.btnExaminar.TabIndex = 3
        Me.btnExaminar.Text = "Examinar"
        Me.btnExaminar.UseVisualStyleBackColor = True
        '
        'lblSaveAsELC
        '
        Me.lblSaveAsELC.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.lblSaveAsELC.Location = New System.Drawing.Point(6, 147)
        Me.lblSaveAsELC.Name = "lblSaveAsELC"
        Me.lblSaveAsELC.Size = New System.Drawing.Size(100, 23)
        Me.lblSaveAsELC.TabIndex = 4
        Me.lblSaveAsELC.Text = "Guardar ELC:"
        Me.lblSaveAsELC.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        'txtExaminar
        '
        Me.txtExaminar.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtExaminar.Location = New System.Drawing.Point(112, 149)
        Me.txtExaminar.Name = "txtExaminar"
        Me.txtExaminar.Size = New System.Drawing.Size(254, 20)
        Me.txtExaminar.TabIndex = 3
        '
        'txtELCUrl
        '
        Me.txtELCUrl.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.txtELCUrl.Location = New System.Drawing.Point(14, 42)
        Me.txtELCUrl.Multiline = True
        Me.txtELCUrl.Name = "txtELCUrl"
        Me.txtELCUrl.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.txtELCUrl.Size = New System.Drawing.Size(629, 99)
        Me.txtELCUrl.TabIndex = 2
        '
        'btnCerrar
        '
        Me.btnCerrar.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnCerrar.Location = New System.Drawing.Point(595, 386)
        Me.btnCerrar.Name = "btnCerrar"
        Me.btnCerrar.Size = New System.Drawing.Size(75, 23)
        Me.btnCerrar.TabIndex = 2
        Me.btnCerrar.Text = "Cerrar"
        Me.btnCerrar.UseVisualStyleBackColor = True
        '
        'ELCForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(682, 421)
        Me.Controls.Add(Me.btnCerrar)
        Me.Controls.Add(Me.gbELCLink)
        Me.Controls.Add(Me.gbMEGAUrl)
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.MinimumSize = New System.Drawing.Size(500, 400)
        Me.Name = "ELCForm"
        Me.Text = "ELCForm"
        Me.gbMEGAUrl.ResumeLayout(False)
        Me.gbMEGAUrl.PerformLayout()
        Me.gbELCLink.ResumeLayout(False)
        Me.gbELCLink.PerformLayout()
        Me.ResumeLayout(False)

    End Sub
	Private label3 As System.Windows.Forms.Label
	Private lblExplanation2 As System.Windows.Forms.Label
	Private WithEvents btnSaveFile As System.Windows.Forms.Button
	Private txtExaminar As System.Windows.Forms.TextBox
	Private lblSaveAsELC As System.Windows.Forms.Label
	Private WithEvents btnExaminar As System.Windows.Forms.Button
	Private WithEvents btnCerrar As System.Windows.Forms.Button
	Private comboELCAccounts As System.Windows.Forms.ComboBox
	Private WithEvents btnCrearELC As System.Windows.Forms.Button
    Private WithEvents txtELCUrl As System.Windows.Forms.TextBox
	Private gbELCLink As System.Windows.Forms.GroupBox
	Private lblExplanation As System.Windows.Forms.Label
    Private WithEvents txtMegaURLs As System.Windows.Forms.TextBox
    Private gbMEGAUrl As System.Windows.Forms.GroupBox
    Friend WithEvents chkMultipleELCs As System.Windows.Forms.CheckBox
	
End Class
