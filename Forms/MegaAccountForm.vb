Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Friend NotInheritable Class MegaAccountForm
    Inherits Form

    Private ReadOnly account As MegaAccountService = MegaAccountService.Current
    Private ReadOnly message As New Label With {.Name = "MegaAccountMessage", .AutoSize = True}
    Private ReadOnly dismiss As CommandButton = MakeButton("Close", UiStyles.Role.QuietButton)
    Private working As Boolean

    Private email As TextBox
    Private password As TextBox
    Private code As TextBox
    Private codeLabel As Label
    Private loginAlias As TextBox
    Private remember As CheckBox
    Private action As CommandButton
    Private mfa As Boolean

    Private roster As ListBox
    Private accounts As New List(Of MegaAccountService.Status)
    Private ReadOnly refreshingDetails As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private updatingRoster As Boolean
    Private selectedEmail As String
    Private details As TableLayoutPanel
    Private emptyRoster As Label
    Private accountName As Label
    Private accountEmail As Label
    Private accountSummary As Label
    Private planValue As Label
    Private storageValue As Label
    Private transferValue As Label
    Private aliasInput As TextBox
    Private saveAlias As CommandButton
    Private addAccount As CommandButton
    Private makeDefault As CommandButton
    Private signInAgain As CommandButton
    Private refreshDetails As CommandButton
    Private signOut As CommandButton
    Private removeAccount As CommandButton

    Friend Property SignedInEmail As String

    Public Sub New()
        Me.New(False, Nothing)
    End Sub

    Private Sub New(loginOnly As Boolean, existing As MegaAccountService.Status)
        Text = Language.GetText("MegaAccount_Title")
        Name = "MegaAccountForm"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        UiStyles.InitializeForm(Me)
        dismiss.DialogResult = DialogResult.Cancel
        CancelButton = dismiss
        AddHandler FormClosing, Sub(sender, e) e.Cancel = working
        UiStyles.SetRole(message, UiStyles.Role.ErrorText)

        If loginOnly OrElse account.GetAccounts().Count = 0 Then
            BuildLogin(existing, loginOnly)
        Else
            BuildManager()
        End If
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Sub BuildLogin(existing As MegaAccountService.Status, adding As Boolean)
        Dim fixedEmail = Not String.IsNullOrEmpty(existing.Email)
        Dim titleKey = If(fixedEmail, "MegaAccount_Reconnect", If(adding, "MegaAccount_Add", "MegaAccount_Login"))
        Text = Language.GetText(titleKey)
        ClientSize = New Size(438, If(fixedEmail, 346, 402))
        message.MaximumSize = New Size(390, 0)
        Dim layout = CreateLayout()
        Dim heading As New Label With {.Text = Text, .AutoSize = True, .Margin = New Padding(3, 0, 3, 6)}
        UiStyles.SetFont(heading, 5, True, Font)
        AddRow(layout, heading)
        If Not fixedEmail Then
            Dim hint As New Label With {.Text = Language.GetText("MegaAccount_Hint"), .AutoSize = True,
                                       .MaximumSize = New Size(390, 0), .Margin = New Padding(3, 0, 3, 10)}
            UiStyles.SetRole(hint, UiStyles.Role.Muted)
            AddRow(layout, hint)
        End If

        email = New TextBox With {.Name = "MegaAccountEmail", .Text = If(existing.Email, ""), .ReadOnly = fixedEmail}
        password = New TextBox With {.Name = "MegaAccountPassword", .UseSystemPasswordChar = True}
        code = New TextBox With {.Name = "MegaAccountCode", .UseSystemPasswordChar = True}
        codeLabel = New Label()
        AddField(layout, New Label(), email, "E-mail")
        AddField(layout, New Label(), password, "Password")
        AddField(layout, codeLabel, code, "MegaAccount_Code")
        codeLabel.Visible = False
        code.Visible = False
        If Not fixedEmail Then
            loginAlias = New TextBox With {.Name = "MegaAccountAlias"}
            AddField(layout, New Label(), loginAlias, "MegaAccount_OptionalAlias")
            AddHandler email.TextChanged, Sub() SetMfa(False)
        End If
        remember = New CheckBox With {.Name = "MegaAccountRemember", .AutoSize = True, .Checked = existing.Remember,
                                      .Text = Language.GetText("MegaAccount_Remember")}
        AddRow(layout, remember)
        AddRow(layout, message)
        AddRow(layout, New Panel(), SizeType.Percent, 100)

        action = MakeButton("MegaAccount_LoginButton", UiStyles.Role.PrimaryButton)
        action.Name = "MegaAccountAction"
        AddHandler action.Click, AddressOf SignIn
        dismiss.Text = Language.GetText("Cancel")
        AddRow(layout, MakeButtons(FlowDirection.RightToLeft, action, dismiss))
        Controls.Add(layout)
        AcceptButton = action
        ActiveControl = If(fixedEmail, password, email)
        SetLoginWorking(False)
    End Sub

    Private Sub BuildManager()
        Text = Language.GetText("MegaAccount_Manage")
        ClientSize = New Size(724, 410)
        message.MaximumSize = New Size(676, 0)
        Dim layout = CreateLayout()
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = Padding.Empty}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 222))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        roster = New ListBox With {.Name = "MegaAccountRoster", .Dock = DockStyle.Fill, .IntegralHeight = False,
                                   .HorizontalScrollbar = True, .AccessibleName = Text, .Margin = New Padding(3, 3, 0, 3)}
        UiStyles.SetRole(roster, UiStyles.Role.InputSurface)
        AddHandler roster.SelectedIndexChanged, AddressOf AccountSelected
        body.Controls.Add(roster, 0, 0)

        details = New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .Margin = New Padding(16, 0, 0, 0)}
        details.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        accountName = New Label With {.Dock = DockStyle.Fill, .AutoEllipsis = True, .UseMnemonic = False}
        UiStyles.SetFont(accountName, 5, True, Font)
        AddRow(details, accountName, SizeType.Absolute, 28)
        accountEmail = New Label With {.Dock = DockStyle.Fill, .AutoEllipsis = True, .UseMnemonic = False}
        UiStyles.SetRole(accountEmail, UiStyles.Role.Muted)
        AddRow(details, accountEmail, SizeType.Absolute, 22)
        accountSummary = New Label With {.AutoSize = True, .MaximumSize = New Size(420, 0), .Margin = New Padding(3, 0, 3, 8)}
        AddRow(details, accountSummary)

        Dim usage As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .Margin = New Padding(0, 0, 0, 8)}
        usage.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        usage.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        planValue = AddDetail(usage, "MegaAccount_Plan")
        storageValue = AddDetail(usage, "MegaAccount_Storage")
        transferValue = AddDetail(usage, "MegaAccount_Transfer")
        AddRow(details, usage)

        Dim aliasRow As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 3, .RowCount = 1, .Margin = Padding.Empty}
        aliasRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        aliasRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        aliasRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        Dim aliasCaption As New Label With {.Text = Language.GetText("MegaAccount_Alias"), .AutoSize = True, .Anchor = AnchorStyles.Left}
        aliasInput = New TextBox With {.Name = "MegaAccountAlias", .Dock = DockStyle.Fill, .AccessibleName = aliasCaption.Text}
        UiStyles.SetRole(aliasInput, UiStyles.Role.InputSurface)
        saveAlias = MakeButton("Save", UiStyles.Role.SecondaryButton)
        AddHandler aliasInput.TextChanged, Sub() UpdateManagerButtons()
        AddHandler saveAlias.Click, Sub() ShowResult(account.SetAlias(selectedEmail, aliasInput.Text))
        aliasRow.Controls.Add(aliasCaption, 0, 0)
        aliasRow.Controls.Add(aliasInput, 1, 0)
        aliasRow.Controls.Add(saveAlias, 2, 0)
        AddRow(details, aliasRow)

        makeDefault = MakeButton("MegaAccount_SetDefault", UiStyles.Role.SecondaryButton)
        signInAgain = MakeButton("MegaAccount_SignInAgain", UiStyles.Role.SecondaryButton)
        refreshDetails = MakeButton("MegaAccount_RefreshDetails", UiStyles.Role.SecondaryButton)
        signOut = MakeButton("MegaAccount_Logout", UiStyles.Role.QuietButton)
        removeAccount = MakeButton("MegaAccount_Remove", UiStyles.Role.QuietButton)
        AddHandler makeDefault.Click, Sub() ShowResult(account.SetDefault(selectedEmail))
        AddHandler signInAgain.Click, Sub() OpenSignIn(SelectedAccount())
        AddHandler refreshDetails.Click, Async Sub() Await RefreshDetailsAsync(selectedEmail)
        AddHandler signOut.Click, Async Sub() Await RunAccountOperationAsync(account.LogoutAsync(selectedEmail))
        AddHandler removeAccount.Click, AddressOf RemoveSelected
        AddRow(details, MakeButtons(FlowDirection.LeftToRight, makeDefault, signInAgain, refreshDetails))
        AddRow(details, MakeButtons(FlowDirection.LeftToRight, signOut, removeAccount))
        body.Controls.Add(details, 1, 0)
        emptyRoster = New Label With {.Text = Language.GetText("MegaAccount_NoAccounts"), .AutoSize = True, .Visible = False}
        UiStyles.SetRole(emptyRoster, UiStyles.Role.Muted)
        AddRow(layout, emptyRoster)
        AddRow(layout, body, SizeType.Percent, 100)
        AddRow(layout, message)

        addAccount = MakeButton("MegaAccount_Add", UiStyles.Role.PrimaryButton)
        AddHandler addAccount.Click, Sub() OpenSignIn(Nothing)
        AddRow(layout, MakeButtons(FlowDirection.RightToLeft, dismiss, addAccount))
        Controls.Add(layout)
        RefreshAccounts()
        AddHandler Shown, Sub()
                              Dim context = SynchronizationContext.Current
                              Dim changed As EventHandler = Sub(sender, e) context.Post(Sub(state)
                                                                                             If Not IsDisposed Then RefreshAccounts()
                                                                                         End Sub, Nothing)
                              AddHandler account.Changed, changed
                              AddHandler Disposed, Sub() RemoveHandler account.Changed, changed
                              RefreshAccounts()
                          End Sub
    End Sub

    Private Shared Function CreateLayout() As TableLayoutPanel
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .Padding = New Padding(20, 14, 20, 16)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        UiStyles.SetRole(layout, UiStyles.Role.Surface)
        Return layout
    End Function

    Private Shared Sub AddRow(layout As TableLayoutPanel, control As Control, Optional kind As SizeType = SizeType.AutoSize, Optional height As Single = 0)
        Dim row = layout.RowCount
        layout.RowCount += 1
        layout.RowStyles.Add(New RowStyle(kind, height))
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

    Private Shared Function AddDetail(layout As TableLayoutPanel, key As String) As Label
        Dim caption As New Label With {.Text = Language.GetText(key), .AutoSize = True, .Margin = New Padding(3, 3, 14, 3)}
        Dim value As New Label With {.AutoSize = True}
        Dim row = layout.RowCount
        layout.RowCount += 1
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.Controls.Add(caption, 0, row)
        layout.Controls.Add(value, 1, row)
        UiStyles.SetRole(caption, UiStyles.Role.Muted)
        Return value
    End Function

    Private Shared Function MakeButton(key As String, role As UiStyles.Role) As CommandButton
        Dim button As New CommandButton With {.Text = Language.GetText(key), .AutoSize = True, .Padding = New Padding(10, 4, 10, 4)}
        UiStyles.SetRole(button, role)
        Return button
    End Function

    Private Shared Function MakeButtons(direction As FlowDirection, ParamArray buttons() As CommandButton) As FlowLayoutPanel
        Dim bar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = False, .FlowDirection = direction}
        UiStyles.SetRole(bar, UiStyles.Role.Surface)
        For Each button In buttons
            bar.Controls.Add(button)
        Next
        Return bar
    End Function

    Friend Shared Function Summary(status As MegaAccountService.Status, Optional showUnknown As Boolean = False) As String
        If status.Connecting Then Return Language.GetText("MegaAccount_Connecting")
        If status.NeedsLogin Then Return Language.GetText("MegaAccount_Reconnect")
        If status.LastError <> 0 Then Return Language.GetText("MegaAccount_ConnectionFailed")
        If Not status.SignedIn Then Return Language.GetText("MegaAccount_SignedOut")
        If status.QuotaReached Then Return Language.GetText("MegaAccount_QuotaReached")
        If status.Remaining.HasValue Then Return Language.GetText("MegaAccount_QuotaRemaining").Replace("%N%", FormatBytes(status.Remaining.Value))
        Return Language.GetText(If(showUnknown, "MegaAccount_QuotaUnknown", "MegaAccount_SignedIn"))
    End Function

    Private Shared Function FormatBytes(value As Long) As String
        Return FileDownloader.FormatSizeBinary(value, 1)
    End Function

    Private Shared Function FormatUsage(used As Long?, total As Long?) As String
        If used.HasValue AndAlso total.HasValue Then
            Return Language.GetText("MegaAccount_Usage").Replace("%USED%", FormatBytes(used.Value)).Replace("%TOTAL%", FormatBytes(total.Value))
        End If
        If used.HasValue Then Return Language.GetText("MegaAccount_Used").Replace("%N%", FormatBytes(used.Value))
        If total.HasValue Then Return Language.GetText("MegaAccount_Total").Replace("%N%", FormatBytes(total.Value))
        Return Language.GetText("MegaAccount_NotReported")
    End Function

    Private Shared Function PlanName(plan As Integer?) As String
        If Not plan.HasValue Then Return Language.GetText("MegaAccount_NotReported")
        ' MegaAccountDetails::getProLevel() in the pinned MEGA SDK.
        Select Case plan.Value
            Case 0 : Return Language.GetText("MegaAccount_FreePlan")
            Case 1 : Return "Pro I"
            Case 2 : Return "Pro II"
            Case 3 : Return "Pro III"
            Case 4 : Return "Pro Lite"
            Case 11 : Return "Starter"
            Case 12 : Return "Basic"
            Case 13 : Return "Essential"
            Case 100 : Return "Business"
            Case 101 : Return "Pro Flexi"
            Case Else : Return plan.Value.ToString(Globalization.CultureInfo.InvariantCulture)
        End Select
    End Function

    Private Sub RefreshAccounts(Optional preferredEmail As String = Nothing)
        Dim keep = If(preferredEmail, selectedEmail)
        Dim defaultEmail = account.DefaultEmail
        accounts = account.GetAccounts()
        updatingRoster = True
        roster.BeginUpdate()
        Try
            roster.Items.Clear()
            For Each status In accounts
                Dim caption = If(SameEmail(status.Email, defaultEmail),
                                 Language.GetText("MegaAccount_DefaultEntry").Replace("%NAME%", status.DisplayName), status.DisplayName)
                If status.Remember Then caption &= " · " & Language.GetText("MegaAccount_Remembered")
                roster.Items.Add(caption)
            Next
            Dim index = accounts.FindIndex(Function(status) SameEmail(status.Email, keep))
            If index < 0 Then index = accounts.FindIndex(Function(status) SameEmail(status.Email, defaultEmail))
            roster.SelectedIndex = If(index < 0 AndAlso accounts.Count > 0, 0, index)
        Finally
            roster.EndUpdate()
            updatingRoster = False
        End Try
        UpdateDetails()
    End Sub

    Private Shared Function SameEmail(left As String, right As String) As Boolean
        Return String.Equals(left, right, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function SelectedAccount() As MegaAccountService.Status
        If roster.SelectedIndex < 0 Then Return Nothing
        Return accounts(roster.SelectedIndex)
    End Function

    Private Sub AccountSelected(sender As Object, e As EventArgs)
        If updatingRoster Then Return
        message.Text = ""
        UpdateDetails()
    End Sub

    Private Sub UpdateDetails()
        Dim status = SelectedAccount()
        Dim hasSelection = Not String.IsNullOrEmpty(status.Email)
        Dim thisAlias = If(status.AliasName, "")
        If Not SameEmail(selectedEmail, status.Email) Then aliasInput.Text = thisAlias
        selectedEmail = status.Email
        details.Visible = hasSelection
        roster.Visible = hasSelection
        emptyRoster.Visible = Not hasSelection
        accountName.Text = status.DisplayName
        accountEmail.Text = If(thisAlias.Length > 0, status.Email, "")
        accountEmail.Visible = thisAlias.Length > 0
        accountSummary.Text = Summary(status)
        If Not status.Connecting AndAlso status.LastError <> 0 AndAlso Not status.NeedsLogin Then
            accountSummary.Text &= vbNewLine & MegaAccountService.ErrorText(status.LastError, signingIn:=True)
        End If
        UiStyles.SetRole(accountSummary, If(status.NeedsLogin OrElse status.QuotaReached OrElse status.LastError <> 0, UiStyles.Role.ErrorText, UiStyles.Role.Muted))
        planValue.Text = PlanName(status.Plan)
        storageValue.Text = FormatUsage(status.StorageUsed, status.StorageTotal)
        transferValue.Text = FormatUsage(status.TransferUsed, status.TransferTotal)
        UpdateManagerButtons()
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.Enter AndAlso aliasInput IsNot Nothing AndAlso aliasInput.Focused Then
            saveAlias.PerformClick()
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub UpdateManagerButtons()
        Dim status = SelectedAccount()
        Dim canEdit = Not String.IsNullOrEmpty(status.Email) AndAlso Not status.Connecting
        makeDefault.Enabled = canEdit AndAlso Not SameEmail(status.Email, account.DefaultEmail)
        signInAgain.Enabled = canEdit
        refreshDetails.Enabled = canEdit AndAlso status.SignedIn AndAlso Not refreshingDetails.Contains(status.Email)
        signOut.Enabled = canEdit AndAlso status.SignedIn
        removeAccount.Enabled = canEdit
        aliasInput.Enabled = canEdit
        saveAlias.Enabled = canEdit AndAlso aliasInput.Text.Trim() <> If(status.AliasName, "")
    End Sub

    Private Sub ShowResult(result As Integer)
        message.Text = If(result = 0, "", MegaAccountService.ErrorText(result))
        RefreshAccounts()
    End Sub

    Private Async Function RunAccountOperationAsync(operation As Task(Of Integer)) As Task
        message.Text = ""
        Dim result = Await operation
        If Not IsDisposed Then ShowResult(result)
    End Function

    Private Async Function RefreshDetailsAsync(email As String) As Task
        If Not refreshingDetails.Add(email) Then Return
        message.Text = ""
        UpdateManagerButtons()
        Try
            Dim result = Await account.RefreshQuotaAsync(email)
            If Not IsDisposed AndAlso SameEmail(email, selectedEmail) Then
                message.Text = If(result = 0, "", MegaAccountService.ErrorText(result, connecting:=True))
            End If
        Finally
            refreshingDetails.Remove(email)
            If Not IsDisposed Then UpdateManagerButtons()
        End Try
    End Function

    Private Async Sub RemoveSelected(sender As Object, e As EventArgs)
        Dim selected = SelectedAccount()
        If MessageBox.Show(Me, Language.GetText("MegaAccount_RemoveConfirm").Replace("%NAME%", selected.DisplayName),
                           Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return
        Await RunAccountOperationAsync(account.RemoveAsync(selected.Email))
    End Sub

    Private Async Sub OpenSignIn(existing As MegaAccountService.Status)
        message.Text = ""
        Dim signedInEmail As String
        Using dialog As New MegaAccountForm(True, existing)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            signedInEmail = dialog.SignedInEmail
        End Using
        RefreshAccounts(signedInEmail)
        Await RefreshDetailsAsync(signedInEmail)
    End Sub

    Private Sub SetLoginWorking(value As Boolean)
        working = value
        Dim available = MegaAccountService.Availability()
        Dim enabled = Not value AndAlso available = 0
        action.Enabled = enabled
        dismiss.Enabled = Not value
        email.Enabled = enabled
        password.Enabled = enabled
        code.Enabled = enabled
        remember.Enabled = enabled
        If loginAlias IsNot Nothing Then loginAlias.Enabled = enabled
        If available <> 0 Then message.Text = MegaAccountService.ErrorText(available)
    End Sub

    Private Sub SetMfa(value As Boolean)
        If mfa = value Then Return
        mfa = value
        code.Clear()
        Dim extra = LogicalToDeviceUnits(50)
        ClientSize = New Size(ClientSize.Width, ClientSize.Height + If(value, extra, -extra))
        codeLabel.Visible = value
        code.Visible = value
        If Not value Then message.Text = ""
    End Sub

    Private Async Sub SignIn(sender As Object, e As EventArgs)
        Dim accountEmail = email.Text.Trim()
        If accountEmail.Length = 0 OrElse password.TextLength = 0 Then
            message.Text = Language.GetText("MegaAccount_EnterCredentials")
            Return
        End If
        If mfa AndAlso String.IsNullOrWhiteSpace(code.Text) Then
            message.Text = Language.GetText("MegaAccount_MfaRequired")
            code.Focus()
            Return
        End If
        message.Text = ""
        SetLoginWorking(True)
        Dim aliasName = If(loginAlias Is Nothing, "", loginAlias.Text.Trim())
        Dim result = Await account.LoginAsync(accountEmail, password.Text, code.Text.Trim(), remember.Checked, If(aliasName.Length = 0, Nothing, aliasName))
        SetLoginWorking(False)
        code.Clear()
        If result = MegaAccountService.MfaRequired Then
            SetMfa(True)
            code.Focus()
        Else
            password.Clear()
        End If
        If result = 0 Then
            SignedInEmail = accountEmail
            DialogResult = DialogResult.OK
        Else
            message.Text = MegaAccountService.ErrorText(result, signingIn:=True)
        End If
    End Sub
End Class
