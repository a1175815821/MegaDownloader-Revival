'
' Created by SharpDevelop.
' User: asolino
' Date: 25/03/2013
' Time: 9:18
' 
' To change this template use Tools | Options | Coding | Edit Standard Headers.
'
Partial Class ELCAccountControl
	''' <summary>
	''' Designer variable used to keep track of non-visual components.
	''' </summary>
	Private components As System.ComponentModel.IContainer
	
	''' <summary>
	''' Disposes resources used by the control.
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(ELCAccountControl))
        Me.GroupBox10 = New System.Windows.Forms.GroupBox()
        Me.lblInfoELC = New System.Windows.Forms.Label()
        Me.GroupBox9 = New System.Windows.Forms.GroupBox()
        Me.lklELCUrl = New System.Windows.Forms.LinkLabel()
        Me.txtELCAccountURL = New System.Windows.Forms.TextBox()
        Me.lblELCUrl = New System.Windows.Forms.Label()
        Me.lklELCMainAccount = New System.Windows.Forms.LinkLabel()
        Me.chkELCAccountMain = New System.Windows.Forms.CheckBox()
        Me.btnELCAccountAddNew = New System.Windows.Forms.Button()
        Me.btnELCAccountModify = New System.Windows.Forms.Button()
        Me.btnELCAccountDelete = New System.Windows.Forms.Button()
        Me.lklELCAliasAccount = New System.Windows.Forms.LinkLabel()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.txtELCAccountAlias = New System.Windows.Forms.TextBox()
        Me.lklELCShowPassword = New System.Windows.Forms.LinkLabel()
        Me.chkELCAccountShowPassword = New System.Windows.Forms.CheckBox()
        Me.txtELCAccountKey = New System.Windows.Forms.TextBox()
        Me.txtELCAccountUser = New System.Windows.Forms.TextBox()
        Me.lblELCApiKey = New System.Windows.Forms.Label()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.GroupBox8 = New System.Windows.Forms.GroupBox()
        Me.dgELCUsers = New System.Windows.Forms.DataGridView()
        Me.GroupBox10.SuspendLayout()
        Me.GroupBox9.SuspendLayout()
        Me.GroupBox8.SuspendLayout()
        CType(Me.dgELCUsers, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'GroupBox10
        '
        Me.GroupBox10.Controls.Add(Me.lblInfoELC)
        Me.GroupBox10.Location = New System.Drawing.Point(3, 3)
        Me.GroupBox10.Name = "GroupBox10"
        Me.GroupBox10.Size = New System.Drawing.Size(607, 99)
        Me.GroupBox10.TabIndex = 5
        Me.GroupBox10.TabStop = False
        Me.GroupBox10.Text = "Información"
        '
        'lblInfoELC
        '
        Me.lblInfoELC.AutoSize = True
        Me.lblInfoELC.Location = New System.Drawing.Point(7, 20)
        Me.lblInfoELC.MaximumSize = New System.Drawing.Size(590, 0)
        Me.lblInfoELC.Name = "lblInfoELC"
        Me.lblInfoELC.Size = New System.Drawing.Size(573, 65)
        Me.lblInfoELC.TabIndex = 0
        Me.lblInfoELC.Text = resources.GetString("lblInfoELC.Text")
        '
        'GroupBox9
        '
        Me.GroupBox9.Controls.Add(Me.lklELCUrl)
        Me.GroupBox9.Controls.Add(Me.txtELCAccountURL)
        Me.GroupBox9.Controls.Add(Me.lblELCUrl)
        Me.GroupBox9.Controls.Add(Me.lklELCMainAccount)
        Me.GroupBox9.Controls.Add(Me.chkELCAccountMain)
        Me.GroupBox9.Controls.Add(Me.btnELCAccountAddNew)
        Me.GroupBox9.Controls.Add(Me.btnELCAccountModify)
        Me.GroupBox9.Controls.Add(Me.btnELCAccountDelete)
        Me.GroupBox9.Controls.Add(Me.lklELCAliasAccount)
        Me.GroupBox9.Controls.Add(Me.Label3)
        Me.GroupBox9.Controls.Add(Me.txtELCAccountAlias)
        Me.GroupBox9.Controls.Add(Me.lklELCShowPassword)
        Me.GroupBox9.Controls.Add(Me.chkELCAccountShowPassword)
        Me.GroupBox9.Controls.Add(Me.txtELCAccountKey)
        Me.GroupBox9.Controls.Add(Me.txtELCAccountUser)
        Me.GroupBox9.Controls.Add(Me.lblELCApiKey)
        Me.GroupBox9.Controls.Add(Me.Label7)
        Me.GroupBox9.Location = New System.Drawing.Point(261, 108)
        Me.GroupBox9.Name = "GroupBox9"
        Me.GroupBox9.Size = New System.Drawing.Size(349, 264)
        Me.GroupBox9.TabIndex = 4
        Me.GroupBox9.TabStop = False
        Me.GroupBox9.Text = "Datos de cuenta ELC"
        '
        'lklELCUrl
        '
        Me.lklELCUrl.AutoSize = True
        Me.lklELCUrl.Location = New System.Drawing.Point(103, 48)
        Me.lklELCUrl.Name = "lklELCUrl"
        Me.lklELCUrl.Size = New System.Drawing.Size(19, 13)
        Me.lklELCUrl.TabIndex = 27
        Me.lklELCUrl.TabStop = True
        Me.lklELCUrl.Text = "[?]"
        '
        'txtELCAccountURL
        '
        Me.txtELCAccountURL.Location = New System.Drawing.Point(128, 45)
        Me.txtELCAccountURL.Name = "txtELCAccountURL"
        Me.txtELCAccountURL.Size = New System.Drawing.Size(215, 20)
        Me.txtELCAccountURL.TabIndex = 26
        '
        'lblELCUrl
        '
        Me.lblELCUrl.AutoSize = True
        Me.lblELCUrl.Location = New System.Drawing.Point(39, 48)
        Me.lblELCUrl.MinimumSize = New System.Drawing.Size(65, 0)
        Me.lblELCUrl.Name = "lblELCUrl"
        Me.lblELCUrl.Size = New System.Drawing.Size(65, 13)
        Me.lblELCUrl.TabIndex = 25
        Me.lblELCUrl.Text = "URL:"
        Me.lblELCUrl.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'lklELCMainAccount
        '
        Me.lklELCMainAccount.AutoSize = True
        Me.lklELCMainAccount.Location = New System.Drawing.Point(148, 123)
        Me.lklELCMainAccount.Name = "lklELCMainAccount"
        Me.lklELCMainAccount.Size = New System.Drawing.Size(19, 13)
        Me.lklELCMainAccount.TabIndex = 24
        Me.lklELCMainAccount.TabStop = True
        Me.lklELCMainAccount.Text = "[?]"
        '
        'chkELCAccountMain
        '
        Me.chkELCAccountMain.AutoSize = True
        Me.chkELCAccountMain.CheckAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.chkELCAccountMain.ImageAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.chkELCAccountMain.Location = New System.Drawing.Point(22, 122)
        Me.chkELCAccountMain.MinimumSize = New System.Drawing.Size(120, 0)
        Me.chkELCAccountMain.Name = "chkELCAccountMain"
        Me.chkELCAccountMain.Size = New System.Drawing.Size(120, 17)
        Me.chkELCAccountMain.TabIndex = 23
        Me.chkELCAccountMain.Text = "Cuenta principal"
        Me.chkELCAccountMain.TextAlign = System.Drawing.ContentAlignment.TopRight
        Me.chkELCAccountMain.UseVisualStyleBackColor = False
        '
        'btnELCAccountAddNew
        '
        Me.btnELCAccountAddNew.Location = New System.Drawing.Point(8, 235)
        Me.btnELCAccountAddNew.Name = "btnELCAccountAddNew"
        Me.btnELCAccountAddNew.Size = New System.Drawing.Size(96, 23)
        Me.btnELCAccountAddNew.TabIndex = 20
        Me.btnELCAccountAddNew.Text = "Add new"
        Me.btnELCAccountAddNew.UseVisualStyleBackColor = True
        '
        'btnELCAccountModify
        '
        Me.btnELCAccountModify.Location = New System.Drawing.Point(128, 235)
        Me.btnELCAccountModify.Name = "btnELCAccountModify"
        Me.btnELCAccountModify.Size = New System.Drawing.Size(96, 23)
        Me.btnELCAccountModify.TabIndex = 21
        Me.btnELCAccountModify.Text = "Modify"
        Me.btnELCAccountModify.UseVisualStyleBackColor = True
        '
        'btnELCAccountDelete
        '
        Me.btnELCAccountDelete.Location = New System.Drawing.Point(247, 235)
        Me.btnELCAccountDelete.Name = "btnELCAccountDelete"
        Me.btnELCAccountDelete.Size = New System.Drawing.Size(96, 23)
        Me.btnELCAccountDelete.TabIndex = 22
        Me.btnELCAccountDelete.Text = "Delete"
        Me.btnELCAccountDelete.UseVisualStyleBackColor = True
        '
        'lklELCAliasAccount
        '
        Me.lklELCAliasAccount.AutoSize = True
        Me.lklELCAliasAccount.Location = New System.Drawing.Point(103, 22)
        Me.lklELCAliasAccount.Name = "lklELCAliasAccount"
        Me.lklELCAliasAccount.Size = New System.Drawing.Size(19, 13)
        Me.lklELCAliasAccount.TabIndex = 12
        Me.lklELCAliasAccount.TabStop = True
        Me.lklELCAliasAccount.Text = "[?]"
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(39, 22)
        Me.Label3.MinimumSize = New System.Drawing.Size(65, 0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(65, 13)
        Me.Label3.TabIndex = 11
        Me.Label3.Text = "Alias:"
        Me.Label3.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'txtELCAccountAlias
        '
        Me.txtELCAccountAlias.Location = New System.Drawing.Point(128, 19)
        Me.txtELCAccountAlias.Name = "txtELCAccountAlias"
        Me.txtELCAccountAlias.Size = New System.Drawing.Size(215, 20)
        Me.txtELCAccountAlias.TabIndex = 13
        '
        'lklELCShowPassword
        '
        Me.lklELCShowPassword.AutoSize = True
        Me.lklELCShowPassword.Location = New System.Drawing.Point(320, 123)
        Me.lklELCShowPassword.Name = "lklELCShowPassword"
        Me.lklELCShowPassword.Size = New System.Drawing.Size(19, 13)
        Me.lklELCShowPassword.TabIndex = 19
        Me.lklELCShowPassword.TabStop = True
        Me.lklELCShowPassword.Text = "[?]"
        '
        'chkELCAccountShowPassword
        '
        Me.chkELCAccountShowPassword.AutoSize = True
        Me.chkELCAccountShowPassword.CheckAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.chkELCAccountShowPassword.ImageAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.chkELCAccountShowPassword.Location = New System.Drawing.Point(198, 123)
        Me.chkELCAccountShowPassword.MinimumSize = New System.Drawing.Size(120, 0)
        Me.chkELCAccountShowPassword.Name = "chkELCAccountShowPassword"
        Me.chkELCAccountShowPassword.Size = New System.Drawing.Size(120, 17)
        Me.chkELCAccountShowPassword.TabIndex = 18
        Me.chkELCAccountShowPassword.Text = "Mostrar contraseña"
        Me.chkELCAccountShowPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        Me.chkELCAccountShowPassword.UseVisualStyleBackColor = False
        '
        'txtELCAccountKey
        '
        Me.txtELCAccountKey.Location = New System.Drawing.Point(128, 97)
        Me.txtELCAccountKey.MaxLength = 128
        Me.txtELCAccountKey.Name = "txtELCAccountKey"
        Me.txtELCAccountKey.Size = New System.Drawing.Size(215, 20)
        Me.txtELCAccountKey.TabIndex = 17
        Me.txtELCAccountKey.UseSystemPasswordChar = True
        '
        'txtELCAccountUser
        '
        Me.txtELCAccountUser.Location = New System.Drawing.Point(128, 71)
        Me.txtELCAccountUser.Name = "txtELCAccountUser"
        Me.txtELCAccountUser.Size = New System.Drawing.Size(215, 20)
        Me.txtELCAccountUser.TabIndex = 15
        '
        'lblELCApiKey
        '
        Me.lblELCApiKey.AutoSize = True
        Me.lblELCApiKey.Location = New System.Drawing.Point(47, 100)
        Me.lblELCApiKey.MinimumSize = New System.Drawing.Size(75, 0)
        Me.lblELCApiKey.Name = "lblELCApiKey"
        Me.lblELCApiKey.Size = New System.Drawing.Size(75, 13)
        Me.lblELCApiKey.TabIndex = 16
        Me.lblELCApiKey.Text = "API-Key:"
        Me.lblELCApiKey.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'Label7
        '
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(57, 74)
        Me.Label7.MinimumSize = New System.Drawing.Size(65, 0)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(65, 13)
        Me.Label7.TabIndex = 14
        Me.Label7.Text = "Usuario:"
        Me.Label7.TextAlign = System.Drawing.ContentAlignment.TopRight
        '
        'GroupBox8
        '
        Me.GroupBox8.Controls.Add(Me.dgELCUsers)
        Me.GroupBox8.Location = New System.Drawing.Point(3, 108)
        Me.GroupBox8.Name = "GroupBox8"
        Me.GroupBox8.Size = New System.Drawing.Size(252, 264)
        Me.GroupBox8.TabIndex = 3
        Me.GroupBox8.TabStop = False
        Me.GroupBox8.Text = "Cuentas ELC"
        '
        'dgELCUsers
        '
        Me.dgELCUsers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.dgELCUsers.Location = New System.Drawing.Point(7, 19)
        Me.dgELCUsers.Name = "dgELCUsers"
        Me.dgELCUsers.Size = New System.Drawing.Size(239, 239)
        Me.dgELCUsers.TabIndex = 1
        '
        'ELCAccountControl
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.Controls.Add(Me.GroupBox10)
        Me.Controls.Add(Me.GroupBox9)
        Me.Controls.Add(Me.GroupBox8)
        Me.Name = "ELCAccountControl"
        Me.Size = New System.Drawing.Size(615, 375)
        Me.GroupBox10.ResumeLayout(False)
        Me.GroupBox10.PerformLayout()
        Me.GroupBox9.ResumeLayout(False)
        Me.GroupBox9.PerformLayout()
        Me.GroupBox8.ResumeLayout(False)
        CType(Me.dgELCUsers, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)

    End Sub
	Friend WithEvents dgELCUsers As System.Windows.Forms.DataGridView
	Friend GroupBox8 As System.Windows.Forms.GroupBox
	Friend Label7 As System.Windows.Forms.Label
	Friend lblELCApiKey As System.Windows.Forms.Label
	Friend txtELCAccountUser As System.Windows.Forms.TextBox
	Friend txtELCAccountKey As System.Windows.Forms.TextBox
	Friend WithEvents chkELCAccountShowPassword As System.Windows.Forms.CheckBox
	Friend WithEvents lklELCShowPassword As System.Windows.Forms.LinkLabel
	Friend txtELCAccountAlias As System.Windows.Forms.TextBox
	Friend Label3 As System.Windows.Forms.Label
	Friend WithEvents lklELCAliasAccount As System.Windows.Forms.LinkLabel
	Friend WithEvents btnELCAccountDelete As System.Windows.Forms.Button
	Friend WithEvents btnELCAccountModify As System.Windows.Forms.Button
	Friend WithEvents btnELCAccountAddNew As System.Windows.Forms.Button
	Friend chkELCAccountMain As System.Windows.Forms.CheckBox
	Friend WithEvents lklELCMainAccount As System.Windows.Forms.LinkLabel
	Friend lblELCUrl As System.Windows.Forms.Label
	Friend txtELCAccountURL As System.Windows.Forms.TextBox
	Friend WithEvents lklELCUrl As System.Windows.Forms.LinkLabel
	Friend GroupBox9 As System.Windows.Forms.GroupBox
	Friend lblInfoELC As System.Windows.Forms.Label
	Friend GroupBox10 As System.Windows.Forms.GroupBox
End Class
