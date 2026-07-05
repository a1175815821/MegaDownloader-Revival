Public Partial Class ELCAccountControl
	Inherits System.Windows.Forms.UserControl
	
	Public Const PASSWORDDEFECTO As String = "*****"
	Private Const PREFIX_MAIN_ACCOUNT As String = "[*] "
	
	Public Config As Configuracion
	Private ELCAccountH As ELCAccountHelper
	Private t As ToolTip
	
	
	
	Public Sub New()
		' The Me.InitializeComponent call is required for Windows Forms designer support.
		Me.InitializeComponent()
		
	End Sub
	
	Public Sub Cerrar
		ELCAccountH.Dispose()
	End Sub
	
	Public Sub SaveToConfig(byref Config As Configuracion)
		ELCAccountH.SaveToConfig(Config)
	End Sub
	
	
	
	public Sub CargarDatos
		' SEL Accounts
		If Config IsNot Nothing Then 
			ELCAccountH = New ELCAccountHelper(Config)
			dgELCUsers.BackgroundColor = Color.Azure
			With dgELCUsers.ColumnHeadersDefaultCellStyle
				.BackColor = Color.Snow
				.Font = New Font(dgELCUsers.Font, FontStyle.Bold)
			End With
			With dgELCUsers.AlternatingRowsDefaultCellStyle
				.BackColor = Color.SeaShell
			End With
			FillELCAccountDataGridView()
			dgELCUsers.ClearSelection()
			
			txtELCAccountKey.Enabled = False
			txtELCAccountUser.Enabled = False
			txtELCAccountURL.Enabled = False
			txtELCAccountAlias.Enabled = False
			chkELCAccountShowPassword.Enabled = False
			chkELCAccountMain.Enabled = False
			btnELCAccountAddNew.Enabled = True
			btnELCAccountDelete.Enabled = False
			btnELCAccountModify.Enabled = False
			ELCURLBeingEdited = ""
			ELCAliasBeingEdited = ""
			Dim dbColumn2 As DataGridViewTextBoxColumn = CType(dgELCUsers.Columns(0), DataGridViewTextBoxColumn)
			dbColumn2.Name = "E-mail"
			dbColumn2.ReadOnly = True
			dbColumn2.Resizable = DataGridViewTriState.False
			dgELCUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
			dgELCUsers.RowHeadersVisible = False
			dgELCUsers.ColumnHeadersVisible = False
			
			Translate()
		End If
		
	End Sub
	
	Private Sub Translate()
		
		Me.chkELCAccountShowPassword.Text = Language.GetText("Show password")
		
		Me.Label7.Text = Language.GetText("User") & ":"
		Me.btnELCAccountDelete.Text = Language.GetText("Delete")
		Me.btnELCAccountModify.Text = Language.GetText("Modify")
		Me.chkELCAccountMain.Text = Language.GetText("Main account")
		Me.Label3.Text = Language.GetText("Alias") & ":"
		Me.btnELCAccountAddNew.Text = Language.GetText("Add new")
		Me.lblELCUrl.Text = Language.GetText("URL") & ":"
		Me.lblELCApiKey.Text = Language.GetText("Key") & ":"
		Me.GroupBox10.Text = Language.GetText("Information")
		Me.lblELCUrl.Text = Language.GetText("URL") & ":"
		Me.GroupBox8.Text =  Language.GetText("ELC Accounts") 
		Me.GroupBox9.Text = Language.GetText("ELC Account Info")
		Me.lblInfoELC.Text = Language.GetText("ELC Desc Info") 
		Me.lblELCApiKey.Text = Language.GetText("API-Key") 
		
	End Sub
	
	Private Sub chkELCAccountShowPassword_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkELCAccountShowPassword.CheckedChanged
		txtELCAccountKey.UseSystemPasswordChar = Not chkELCAccountShowPassword.Checked
	End Sub
	
	
	#Region "DataGridView SEL Accounts"
	
	
	
	Private ELCURLBeingEdited As String = ""
	Private ELCAliasBeingEdited As String = ""
	Private ELCAccountAction As String = ""
	Private Sub FillELCAccountDataGridView()
		dgELCUsers.DataSource = ELCAccountH.GetAccounts.OrderBy(Function(c) As String
																Dim t As String
																If c.DefaultAccount Then
																	t = "A" & c.Alias
																Else
																	t = "Z" & c.Alias
																End If
																Return t
															End Function _
																).Select(Function(c As ELCAccountHelper.Account) New With { _
																.Email = If(c.DefaultAccount, PREFIX_MAIN_ACCOUNT, "") & c.Alias} _
																).ToList()
	
	End Sub
	
	
	Private Sub dgELCUsers_Paint(ByVal sender As Object, ByVal e As PaintEventArgs) Handles dgELCUsers.Paint
		Dim sndr As DataGridView = DirectCast(sender, DataGridView)
		
		If Config IsNot Nothing AndAlso sndr IsNot Nothing AndAlso sndr.Rows.Count = 0 Then
			' <-- if there are no rows in the DataGridView when it paints, then it will create your message
			Using grfx As Graphics = e.Graphics
				' create a white rectangle so text will be easily readable
				'grfx.FillRectangle(Brushes.White, New Rectangle(New Point(), New Size(sndr.Width, 25)))
				' write text on top of the white rectangle just created
				grfx.DrawString(Language.GetText("Account list empty"), New Font(dgELCUsers.Font.FontFamily, dgELCUsers.Font.Size, FontStyle.Italic), Brushes.Black, New PointF(55, 10))
			End Using
		End If
	End Sub
	
	Private Sub dgSELUsers_CellClick(ByVal sender As Object, ByVal e As System.Windows.Forms.DataGridViewCellEventArgs) Handles dgELCUsers.CellClick	
		Dim aliasName As String = CStr(dgELCUsers.Rows(e.RowIndex).Cells(0).Value)
		'Private Sub dgSELUsers_CellClick(ByVal sender As Object, ByVal e As System.EventArgs) Handles dgELCUsers.SelectionChanged
		'	
		'	If dgELCUsers.SelectedCells Is Nothing OrElse dgELCUsers.SelectedCells.Count = 0 Then Exit Sub
		'	Dim aliasName As String = CStr(dgELCUsers.SelectedCells(0).value)
		
		If aliasName.StartsWith(PREFIX_MAIN_ACCOUNT) Then aliasName = aliasName.Substring(PREFIX_MAIN_ACCOUNT.Length)
		Dim account As ELCAccountHelper.Account = ELCAccountH.GetAccountDetailsByAlias(aliasName)
		If account IsNot Nothing Then
			txtELCAccountUser.Text = Criptografia.ToInsecureString(account.User)
			txtELCAccountAlias.Text = account.Alias
			txtELCAccountURL.Text = account.URL
			txtELCAccountKey.Text = PASSWORDDEFECTO
			ELCURLBeingEdited = txtELCAccountURL.Text
			ELCAliasBeingEdited = txtELCAccountAlias.Text
			chkELCAccountMain.Checked = account.DefaultAccount
			btnELCAccountAddNew.Enabled = True
			btnELCAccountDelete.Enabled = True
			btnELCAccountModify.Enabled = True
			ELCAccountAction = "EDIT"
			btnELCAccountAddNew.Text = Language.GetText("Add new")
			txtELCAccountKey.Enabled = True
			txtELCAccountUser.Enabled = True
			txtELCAccountURL.Enabled = True
			txtELCAccountAlias.Enabled = True
			chkELCAccountShowPassword.Enabled = True
			chkELCAccountShowPassword.Checked = False
			chkELCAccountMain.Enabled = True
		Else
			MessageBox.Show(Language.GetText("Account not found"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
			ELCURLBeingEdited = ""
			ELCAliasBeingEdited = ""
			btnELCAccountAddNew.Enabled = True
			btnELCAccountDelete.Enabled = False
			btnELCAccountModify.Enabled = False
		End If
	End Sub
	
	Private Sub btnELCAccountAddNew_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnELCAccountAddNew.Click
		Select Case ELCAccountAction
			Case "NEW"
				
				If String.IsNullOrEmpty(txtELCAccountURL.Text) Then
                    MessageBox.Show(Language.GetText("URL is mandatory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
					Exit Sub
				End If
				
				
				Dim account As New ELCAccountHelper.Account
				account.DefaultAccount = chkELCAccountMain.Checked
				account.User = Criptografia.ToSecureString(txtELCAccountUser.Text)
				account.Key = Criptografia.ToSecureString(txtELCAccountKey.Text)
				account.Alias = txtELCAccountAlias.Text
				account.URL = txtELCAccountURL.Text
				If String.IsNullOrEmpty(account.Alias) Then
					account.Alias = txtELCAccountURL.Text
				End If
				If ELCAccountH.AddNewAccount(account) Then
					FillELCAccountDataGridView()
					ELCAccountAction = "EDIT"
					btnELCAccountAddNew.Text = Language.GetText("Add new")
					ELCURLBeingEdited = txtELCAccountURL.Text
					ELCAliasBeingEdited = txtELCAccountAlias.Text
					btnELCAccountDelete.Enabled = True
					btnELCAccountModify.Enabled = True
					chkELCAccountShowPassword.Checked = False
					txtELCAccountKey.Text = PASSWORDDEFECTO
					dgELCUsers.ClearSelection()
					MessageBox.Show(Language.GetText("Account created correctly"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
				Else
					MessageBox.Show(Language.GetText("Account already exists"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
				End If
				
			Case Else
				
				ELCAccountAction = "NEW"
				btnELCAccountAddNew.Text = Language.GetText("Save")
				btnELCAccountModify.Enabled = False
				btnELCAccountDelete.Enabled = False
				ELCURLBeingEdited = ""
				ELCAliasBeingEdited = ""
				txtELCAccountUser.Text = ""
				txtELCAccountAlias.Text = ""
				txtELCAccountKey.Text = ""
				txtELCAccountURL.Text = ""
				txtELCAccountKey.Enabled = True
				txtELCAccountUser.Enabled = True
				txtELCAccountURL.Enabled = True
				txtELCAccountAlias.Enabled = True
				chkELCAccountShowPassword.Enabled = True
				chkELCAccountMain.Checked = False
				chkELCAccountMain.Enabled = True
				dgELCUsers.ClearSelection()
		End Select
	End Sub
	
	
	Private Sub btnELCAccountModify_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnELCAccountModify.Click
		
		If ELCAccountAction = "EDIT" And Not String.IsNullOrEmpty(ELCURLBeingEdited) And Not String.IsNullOrEmpty(ELCAliasBeingEdited) Then
			
			If String.IsNullOrEmpty(txtELCAccountURL.Text) Then
                MessageBox.Show(Language.GetText("URL is mandatory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
				Exit Sub
			End If
			
			Dim account As New ELCAccountHelper.Account
			account.DefaultAccount = chkELCAccountMain.Checked
			account.User = Criptografia.ToSecureString(txtELCAccountUser.Text)
			account.Alias = txtELCAccountAlias.Text
			account.URL = txtELCAccountURL.Text
			If String.IsNullOrEmpty(account.Alias) Then
				account.Alias = txtELCAccountURL.Text
			End If
			If txtELCAccountKey.Text = PASSWORDDEFECTO Then ' Password not changed
				Dim accAux As ELCAccountHelper.Account = ELCAccountH.GetAccountDetailsByURL(ELCURLBeingEdited)
				If accAux IsNot Nothing Then
					account.Key = accAux.Key.Copy
				End If
			Else
				account.Key = Criptografia.ToSecureString(txtELCAccountKey.Text)
			End If
			
			If ELCAccountH.ModifyAccountDetails(ELCURLBeingEdited, account) Then
				FillELCAccountDataGridView()
				ELCAccountAction = "EDIT"
				btnELCAccountAddNew.Text = Language.GetText("Add new")
				ELCURLBeingEdited = txtELCAccountURL.Text
				ELCAliasBeingEdited = txtELCAccountAlias.Text
				chkELCAccountShowPassword.Checked = False
				txtELCAccountKey.Text = PASSWORDDEFECTO
				dgELCUsers.ClearSelection()
				MessageBox.Show(Language.GetText("Account modified correctly"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
			Else
				MessageBox.Show(Language.GetText("Account already exists"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
			End If
			
		End If
	End Sub
	
	Private Sub btnELCAccountDelete_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnELCAccountDelete.Click
		If Not String.IsNullOrEmpty(ELCURLBeingEdited) And Not String.IsNullOrEmpty(ELCAliasBeingEdited) Then
			If MessageBox.Show(Language.GetText("Do you want to delete this account?"), _
				Language.GetText("Confirmation"), MessageBoxButtons.YesNo) = DialogResult.Yes Then
				
				If ELCAccountH.DeleteAccount(ELCURLBeingEdited) Then
					FillELCAccountDataGridView()
					dgELCUsers.ClearSelection()
					txtELCAccountKey.Text = ""
					txtELCAccountUser.Text = ""
					txtELCAccountAlias.Text = ""
					txtELCAccountURL.Text = ""
					txtELCAccountKey.Enabled = False
					txtELCAccountURL.Enabled = False
					txtELCAccountAlias.Enabled = False
					txtELCAccountUser.Enabled = False
					chkELCAccountShowPassword.Enabled = False
					chkELCAccountMain.Enabled = False
					btnELCAccountAddNew.Enabled = True
					btnELCAccountDelete.Enabled = False
					btnELCAccountModify.Enabled = False
					ELCURLBeingEdited = ""
					dgELCUsers.ClearSelection()
					MessageBox.Show(Language.GetText("Account deleted correctly"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
				Else
					MessageBox.Show(Language.GetText("Account could not be deleted"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error) '
				End If
			End If
			
		End If
	End Sub
	
	
	
	
	Private Sub lklELCUrl_MouseHover(sender As Object, e As System.EventArgs) Handles lklELCUrl.MouseHover
		t = New ToolTip
		t.SetToolTip(lklELCUrl, MsgUrlELCAccount)
	End Sub
	
	Private Sub lklELCUrl_MouseLeave(sender As Object, e As System.EventArgs) Handles lklELCUrl.MouseLeave
		If t IsNot Nothing Then t.Hide(lklELCUrl)
	End Sub
	
	Private Sub lklELCUrl_Click(sender As Object, e As System.EventArgs) Handles lklELCUrl.Click
		MessageBox.Show(MsgUrlELCAccount, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
	End Sub
	
	Private Function MsgUrlELCAccount() As String
		Return Language.GetText("URL ELC account explanation") 
	End Function
	
	
	Private Sub lklELCAliasAccount_MouseHover(sender As Object, e As System.EventArgs) Handles lklELCAliasAccount.MouseHover
		t = New ToolTip
		t.SetToolTip(lklELCAliasAccount, MsgAliasELCAccount)
	End Sub
	
	Private Sub lklELCAliasAccount_MouseLeave(sender As Object, e As System.EventArgs) Handles lklELCAliasAccount.MouseLeave
		If t IsNot Nothing Then t.Hide(lklELCAliasAccount)
	End Sub
	
	Private Sub lklELCAliasAccount_Click(sender As Object, e As System.EventArgs) Handles lklELCAliasAccount.Click
		MessageBox.Show(MsgAliasELCAccount, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
	End Sub
	
	Private Function MsgAliasELCAccount() As String
		Return Language.GetText("Alias ELC account explanation") 
	End Function
	
	
	Private Sub lklELCMainAccount_MouseHover(sender As Object, e As System.EventArgs) Handles lklELCMainAccount.MouseHover
		t = New ToolTip
		t.SetToolTip(lklELCMainAccount, MsgDefaultELCAccount)
	End Sub
	
	Private Sub lklELCMainAccount_MouseLeave(sender As Object, e As System.EventArgs) Handles lklELCMainAccount.MouseLeave
		If t IsNot Nothing Then t.Hide(lklELCMainAccount)
	End Sub
	
	Private Sub lklELCMainAccount_Click(sender As Object, e As System.EventArgs) Handles lklELCMainAccount.Click
		MessageBox.Show(MsgDefaultELCAccount, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
	End Sub
	
	Private Function MsgDefaultELCAccount() As String
		Return Language.GetText("Default ELC account explanation") 
	End Function
	
	
	Private Sub lklELCShowPassword_MouseHover(sender As Object, e As System.EventArgs) Handles lklELCShowPassword.MouseHover
		t = New ToolTip
		t.SetToolTip(lklELCShowPassword, MsgELCShowPassword)
	End Sub
	
	Private Sub lklELCShowPassword_MouseLeave(sender As Object, e As System.EventArgs) Handles lklELCShowPassword.MouseLeave
		If t IsNot Nothing Then t.Hide(lklELCShowPassword)
	End Sub
	
	Private Sub lklELCShowPassword_Click(sender As Object, e As System.EventArgs) Handles lklELCShowPassword.Click
		MessageBox.Show(MsgELCShowPassword, Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information)
	End Sub
	
	Private Function MsgELCShowPassword() As String
		Return Language.GetText("Show password explanation")
	End Function


#End Region

End Class
