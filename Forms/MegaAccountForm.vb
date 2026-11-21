Imports System.Drawing
Imports System.Windows.Forms

Friend NotInheritable Class MegaAccountForm
    Inherits Form

    Private ReadOnly account As MegaAccountService = MegaAccountService.Current
    Private ReadOnly email As New TextBox With {.Name = "MegaAccountEmail"}
    Private ReadOnly password As New TextBox With {.Name = "MegaAccountPassword", .UseSystemPasswordChar = True}
    Private ReadOnly code As New TextBox With {.Name = "MegaAccountCode", .UseSystemPasswordChar = True}
    Private ReadOnly remember As New CheckBox With {.Name = "MegaAccountRemember", .AutoSize = True}
    Private ReadOnly message As New Label With {.Name = "MegaAccountMessage", .AutoSize = True, .MaximumSize = New Size(390, 0)}
    Private ReadOnly action As New CommandButton With {.Name = "MegaAccountAction", .AutoSize = True, .Padding = New Padding(10, 4, 10, 4)}
    Private ReadOnly dismiss As New CommandButton With {.AutoSize = True, .DialogResult = DialogResult.Cancel, .Padding = New Padding(10, 4, 10, 4)}
    Private ReadOnly refreshQuotaButton As New CommandButton With {.AutoSize = True, .Padding = New Padding(10, 4, 10, 4)}
    Private ReadOnly codeLabel As Label = New Label()
    Private working As Boolean
    Private mfa As Boolean

    Public Sub New()
        Text = Language.GetText("MegaAccount_Title")
        Name = "MegaAccountForm"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        UiStyles.InitializeForm(Me)

        Dim status = account.GetStatus()
        Dim signedIn As Boolean = status.Email <> ""
        ClientSize = New Size(438, If(signedIn, 192, 332))
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .Padding = New Padding(20, 14, 20, 16)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        UiStyles.SetRole(layout, UiStyles.Role.Surface)
        Dim heading As New Label With {.Text = If(signedIn, status.Email, Language.GetText("MegaAccount_Login")),
                                      .AutoSize = True, .MaximumSize = New Size(390, 0), .Margin = New Padding(3, 0, 3, 6)}
        UiStyles.SetFont(heading, 5, True, Font)
        AddRow(layout, heading)
        Dim hint As New Label With {.Text = If(signedIn, Summary(status, True), Language.GetText("MegaAccount_Hint")),
                                   .AutoSize = True, .MaximumSize = New Size(390, 0), .Margin = New Padding(3, 0, 3, 10)}
        UiStyles.SetRole(hint, UiStyles.Role.Muted)
        AddRow(layout, hint)

        If signedIn Then
            action.Text = Language.GetText("MegaAccount_Logout")
            AddHandler action.Click, AddressOf SignOut
            refreshQuotaButton.Text = Language.GetText("MegaAccount_RefreshQuota")
            UiStyles.SetRole(refreshQuotaButton, UiStyles.Role.QuietButton)
            AddHandler refreshQuotaButton.Click, Async Sub()
                                          SetWorking(True)
                                          Dim result = Await account.RefreshQuotaAsync()
                                          SetWorking(False)
                                          hint.Text = Summary(account.GetStatus(), True)
                                          If result <> 0 Then ShowProblem(MegaAccountService.ErrorText(result)) Else ShowProblem("")
                                          If account.Email = "" Then action.Enabled = False
                                      End Sub
        Else
            AddField(layout, New Label(), email, "E-mail")
            AddField(layout, New Label(), password, "Password")
            AddField(layout, codeLabel, code, "MegaAccount_Code")
            codeLabel.Visible = False
            code.Visible = False
            remember.Text = Language.GetText("MegaAccount_Remember")
            AddRow(layout, remember)
            action.Text = Language.GetText("MegaAccount_LoginButton")
            AddHandler action.Click, AddressOf SignIn
        End If
        UiStyles.SetRole(message, UiStyles.Role.Muted)
        AddRow(layout, message)
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowCount += 1
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = False,
                                                 .FlowDirection = FlowDirection.RightToLeft}
        UiStyles.SetRole(buttons, UiStyles.Role.Surface)
        dismiss.Text = Language.GetText(If(signedIn, "Close", "Cancel"))
        UiStyles.SetRole(dismiss, UiStyles.Role.QuietButton)
        UiStyles.SetRole(action, If(signedIn, UiStyles.Role.SecondaryButton, UiStyles.Role.PrimaryButton))
        buttons.Controls.Add(action)
        buttons.Controls.Add(dismiss)
        If signedIn Then buttons.Controls.Add(refreshQuotaButton)
        AddRow(layout, buttons)
        Controls.Add(layout)
        AcceptButton = If(signedIn, Nothing, action)
        CancelButton = dismiss
        AddHandler FormClosing, Sub(sender, e) e.Cancel = working
        ThemeManager.ApplyTheme(Me)
        If Not signedIn Then
            Dim available = MegaAccountService.Availability()
            If available <> 0 Then
                action.Enabled = False
                email.Enabled = False
                password.Enabled = False
                remember.Enabled = False
                ShowProblem(MegaAccountService.ErrorText(available))
            End If
        End If
    End Sub

    Private Shared Sub AddRow(layout As TableLayoutPanel, control As Control)
        Dim row = layout.RowCount
        layout.RowCount += 1
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.Controls.Add(control, 0, row)
    End Sub

    Private Shared Sub AddField(layout As TableLayoutPanel, label As Label, input As TextBox, key As String)
        label.Text = Language.GetText(key)
        label.AutoSize = True
        label.Margin = New Padding(3, 5, 3, 0)
        input.Dock = DockStyle.Top
        input.AccessibleName = label.Text
        UiStyles.SetRole(input, UiStyles.Role.InputSurface)
        AddRow(layout, label)
        AddRow(layout, input)
    End Sub

    Friend Shared Function Summary(status As MegaAccountService.Status, Optional showUnknown As Boolean = False) As String
        If status.Connecting Then Return Language.GetText("MegaAccount_Connecting")
        If status.NeedsLogin Then Return Language.GetText("MegaAccount_Reconnect")
        If status.Email = "" Then Return Language.GetText("MegaAccount_SignedOut")
        If status.QuotaReached Then Return Language.GetText("MegaAccount_QuotaReached")
        If status.Remaining.HasValue Then Return Language.GetText("MegaAccount_QuotaRemaining").Replace("%N%", FileDownloader.FormatSizeBinary(status.Remaining.Value, 1))
        Return Language.GetText(If(showUnknown, "MegaAccount_QuotaUnknown", "MegaAccount_SignedIn"))
    End Function

    Private Sub ShowProblem(text As String)
        message.ForeColor = ThemeManager.GetColor("ErrorFore")
        message.Text = text
    End Sub

    Private Sub SetWorking(value As Boolean)
        working = value
        action.Enabled = Not value
        dismiss.Enabled = Not value
        refreshQuotaButton.Enabled = Not value
        email.Enabled = Not value
        password.Enabled = Not value
        code.Enabled = Not value
        remember.Enabled = Not value
    End Sub

    Private Async Sub SignIn(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(email.Text) OrElse password.TextLength = 0 OrElse (mfa AndAlso String.IsNullOrWhiteSpace(code.Text)) Then
            ShowProblem(Language.GetText(If(mfa, "MegaAccount_MfaRequired", "MegaAccount_EnterCredentials")))
            Return
        End If
        ShowProblem("")
        SetWorking(True)
        Dim result = Await account.LoginAsync(email.Text, password.Text, code.Text.Trim(), remember.Checked)
        SetWorking(False)
        code.Clear()
        If result = MegaAccountService.MfaRequired Then
            If Not mfa Then
                mfa = True
                ClientSize = New Size(ClientSize.Width, ClientSize.Height + LogicalToDeviceUnits(50))
                codeLabel.Visible = True
                code.Visible = True
            End If
            code.Focus()
        Else
            password.Clear()
        End If
        If result = 0 OrElse account.Email <> "" Then
            If result <> 0 Then MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            DialogResult = DialogResult.OK
        Else
            ShowProblem(MegaAccountService.ErrorText(result, signingIn:=True))
        End If
    End Sub

    Private Async Sub SignOut(sender As Object, e As EventArgs)
        SetWorking(True)
        Dim result = Await account.LogoutAsync()
        SetWorking(False)
        If result = 0 Then
            DialogResult = DialogResult.OK
        ElseIf account.Email = "" Then
            MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            DialogResult = DialogResult.OK
        Else
            ShowProblem(MegaAccountService.ErrorText(result))
        End If
    End Sub
End Class
