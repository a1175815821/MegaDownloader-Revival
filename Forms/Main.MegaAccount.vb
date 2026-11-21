Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms

Partial Public Class Main
    Private megaAccountMenu As ToolStripMenuItem
    Private megaAccountStatus As ToolStripStatusLabel
    Private megaAccountContext As SynchronizationContext

    Private Sub InitMegaAccountUi()
        megaAccountContext = SynchronizationContext.Current
        megaAccountStatus = New ToolStripStatusLabel With {.Name = "MegaAccountStatus", .Overflow = ToolStripItemOverflow.AsNeeded}
        StatusStrip1.Items.Insert(1, megaAccountStatus)
        AddHandler MegaAccountService.Current.Changed, AddressOf MegaAccountChanged
        AddHandler Disposed, Sub() RemoveHandler MegaAccountService.Current.Changed, AddressOf MegaAccountChanged
        EnsureMegaAccountMenu()
        UpdateMegaAccountUi()
    End Sub

    Private Sub EnsureMegaAccountMenu()
        If MainMenuStrip Is Nothing OrElse megaAccountStatus Is Nothing Then Return
        If megaAccountMenu IsNot Nothing AndAlso megaAccountMenu.Owner Is MainMenuStrip Then Return
        megaAccountMenu = New ToolStripMenuItem With {.Name = "MegaAccountMenu", .Alignment = ToolStripItemAlignment.Right,
                                                       .Overflow = ToolStripItemOverflow.Never, .Padding = New Padding(8, 0, 8, 0)}
        AddHandler megaAccountMenu.Click, AddressOf OpenMegaAccount
        MainMenuStrip.Items.Add(megaAccountMenu)
        UpdateMegaAccountUi()
    End Sub

    Private Sub MegaAccountChanged(sender As Object, e As EventArgs)
        megaAccountContext.Post(Sub(state)
                                    If Not IsDisposed Then UpdateMegaAccountUi()
                                End Sub, Nothing)
    End Sub

    Private Sub UpdateMegaAccountUi()
        If megaAccountStatus Is Nothing Then Return
        Dim service = MegaAccountService.Current
        Dim account = service.GetStatus()
        Dim summary As String = MegaAccountForm.Summary(account)
        If megaAccountStatus.Text <> summary Then megaAccountStatus.Text = summary
        megaAccountStatus.ForeColor = ThemeManager.GetColor(If(account.QuotaReached OrElse account.NeedsLogin, "ErrorFore", "Muted"))
        If megaAccountMenu Is Nothing Then Return
        Dim hasDefault = Not String.IsNullOrEmpty(account.Email)
        Dim caption = Language.GetText(If(account.NeedsLogin, "MegaAccount_Reconnect", "MegaAccount_Login"))
        Dim fullCaption = caption
        If hasDefault Then
            fullCaption = "MEGA: " & account.DisplayName
            caption = fullCaption
            If TextRenderer.MeasureText(caption, megaAccountMenu.Font, Size.Empty, TextFormatFlags.NoPrefix).Width > LogicalToDeviceUnits(220) Then
                caption = Language.GetText("MegaAccount_Manage")
            End If
        ElseIf service.GetAccounts().Count > 0 Then
            caption = Language.GetText("MegaAccount_Manage")
            fullCaption = caption
        End If
        caption = caption.Replace("&", "&&")
        If megaAccountMenu.Text <> caption Then megaAccountMenu.Text = caption
        megaAccountMenu.ToolTipText = If(hasDefault AndAlso account.DisplayName <> account.Email, fullCaption & " (" & account.Email & ")", fullCaption)
        megaAccountMenu.AccessibleName = If(hasDefault, Language.GetText("MegaAccount_Manage") & ": " & account.DisplayName, fullCaption)
        megaAccountMenu.Enabled = Not service.IsRestoring
    End Sub

    Private Async Sub RestoreMegaAccount()
        Await MegaAccountService.Current.RestoreAsync()
    End Sub

    Private Async Sub OpenMegaAccount(sender As Object, e As EventArgs)
        Dim signedInEmail As String
        Using dialog As New MegaAccountForm()
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            signedInEmail = dialog.SignedInEmail
        End Using
        Await MegaAccountService.Current.RefreshQuotaAsync(signedInEmail)
    End Sub
End Class
