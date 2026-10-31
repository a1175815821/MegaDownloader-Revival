Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class Configuration
    Private Sub InitConfigurationLayout()
        FormBorderStyle = FormBorderStyle.Sizable
        MaximizeBox = True
        MinimumSize = New Size(720, 540)
        ClientSize = New Size(880, 700)

        Dim shell As New TableLayoutPanel With {.ColumnCount = 1, .Margin = New Padding(0)}
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        UiStyles.SetRole(shell, UiStyles.Role.Surface)
        shell.Dock = DockStyle.Fill
        shell.AutoSize = False
        shell.Padding = New Padding(24)
        shell.RowCount = 3
        shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim heading As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .Margin = New Padding(0, 0, 0, 20)}
        heading.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        heading.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 300))
        UiStyles.SetRole(heading, UiStyles.Role.Surface)
        Dim title As New Label With {.Text = Text, .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0)}
        UiStyles.SetFont(title, 6, True, Me.Font)
        AddHandler Me.TextChanged, Sub() title.Text = Text
        Dim clear As New CommandButton With {.Text = "×", .TabStop = False, .AccessibleName = Language.GetText("UI_ClearSearch")}
        UiStyles.SetRole(clear, UiStyles.Role.QuietButton)
        Dim search As New SearchFrame(txtSearchConfig, clear) With {.Dock = DockStyle.Fill, .Height = 36, .Margin = New Padding(12, 0, 0, 0)}
        AddHandler clear.Click, Sub() txtSearchConfig.Clear()
        heading.Controls.Add(title, 0, 0)
        heading.Controls.Add(search, 1, 0)
        shell.Controls.Add(heading, 0, 0)

        Dim connectionPage As New TabPage With {.Name = "ConnectionPage", .Text = ConexionGroup.Text}
        AddHandler ConexionGroup.TextChanged, Sub() connectionPage.Text = ConexionGroup.Text
        TabControl1.TabPages.Clear()
        TabControl1.TabPages.AddRange(New TabPage() {TabPage1, connectionPage, TabPage2, TabPage3, TabPage4, TabPage5})
        TabControl1.Dock = DockStyle.Fill
        TabControl1.Margin = New Padding(0)
        TabControl1.Multiline = True
        TabControl1.Padding = New Point(16, 9)

        SettingsSection(GroupBox1, FieldRow(Label2, txtUsuario), FieldRow(Label3, txtPassword, chkShowPassword))
        Dim options As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 4, .Margin = New Padding(0)}
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        UiStyles.SetRole(options, UiStyles.Role.Surface)
        For index As Integer = 0 To 3
            options.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        Dim checks As Control() = {chkAnalisisPortapapeles, chkCrearDirectorio, chkComenzarPlay,
                                  InlineControls(chkApagarPC, linkApagarPC), chkStartWindows,
                                  InlineControls(chkUltimaConfig, linkUltConfig), chkCheckUpdates,
                                  InlineControls(chkUnZip, comboPrioridad)}
        For index As Integer = 0 To checks.Length - 1
            checks(index).Margin = New Padding(0, 6, 16, 6)
            options.Controls.Add(checks(index), index Mod 2, index \ 2)
        Next
        SettingsSection(GroupBox4, FieldRow(Label1, txtRuta, btnExaminar), options,
                        FieldRow(Label24, comboIdiomas), FieldRow(Label25, comboTheme))
        SettingsSection(ConexionGroup, InlineControls(chkLimitarVelocidad, txtLimiteVelocidadKBs, Label15),
                        InlineControls(chkReintentarError, txtPeriodoReintento, Label12), chkQuotaBreak,
                        FieldRow(Label10, txtDescSimult), FieldRow(Label9, txtConFic), LinkLabel1)
        SettingsSection(GroupBox2, chkProxy, FieldRow(Label13, txtProxyIP), FieldRow(Label14, txtProxyPort),
                        FieldRow(Label23, txtProxyName), FieldRow(Label22, txtProxyPassword))
        UiStyles.SetAppFont(Label8)
        UiStyles.SetRole(Label8, UiStyles.Role.Muted)
        SettingsSection(GroupBox3, Label8, FieldRow(Label4, txtTamanoPaquete, Label5),
                        FieldRow(Label6, txtTamanoBuffer, Label7), FieldRow(Label11, comboLog))
        SettingsSection(GroupBox5, chkServidorWeb, chkServidorWebLAN,
                        FieldRow(lblServidorWebBindIP, txtServidorWebBindIP), FieldRow(Label16, txtServidorWebPort),
                        FieldRow(Label18, txtServidorWebTimeout, Label19), FieldRow(Label17, txtServidorWebPassword),
                        FieldRow(Label20, txtServidorWebNombre), FieldRow(Label21, txtServidorWebTemplate, btnExaminarTemplate))
        txtKeyList.MinimumSize = New Size(0, 200)
        txtKeyList.Dock = DockStyle.Top
        SettingsSection(GroupBox, txtKeyList)
        SettingsSection(GroupBox6, chkStreamingServer, FieldRow(lblStreamingPort, txtStreamingPort), FieldRow(lblStreamingPassword, txtStreamingPassword))
        SettingsSection(GroupBox8, FieldRow(Label26, txtVLCPath, btnExaminarVLCPath), linkDownloadVLC)
        UiStyles.SetRole(lblInfoStreaming, UiStyles.Role.Muted)
        SettingsSection(GroupBox7, lblInfoStreaming)

        SettingsPage(TabPage1, GroupBox4, GroupBox1)
        SettingsPage(connectionPage, ConexionGroup, GroupBox2)
        SettingsPage(TabPage2, GroupBox3, GroupBox5)
        SettingsPage(TabPage3, lblDesc, GroupBox)
        SettingsPage(TabPage4, GroupBox6, GroupBox8, GroupBox7)
        ElcAccountControl.Dock = DockStyle.Fill
        TabPage5.Padding = New Padding(12)
        UiStyles.SetRole(TabPage5, UiStyles.Role.Surface)
        shell.Controls.Add(TabControl1, 0, 1)

        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .Margin = New Padding(0, 20, 0, 0)}
        For Each button As Button In New Button() {btnGuardar, btnCancel}
            button.AutoSize = True
            button.Padding = New Padding(16, 7, 16, 7)
            button.MinimumSize = New Size(100, 36)
            button.Margin = New Padding(8, 0, 0, 0)
        Next
        UiStyles.SetRole(btnGuardar, UiStyles.Role.PrimaryButton)
        UiStyles.SetRole(btnCancel, UiStyles.Role.SecondaryButton)
        actions.Controls.AddRange(New Control() {btnGuardar, btnCancel})
        UiStyles.SetRole(actions, UiStyles.Role.Surface)
        shell.Controls.Add(actions, 0, 2)
        Controls.Add(shell)
        shell.BringToFront()
        AcceptButton = btnGuardar
        CancelButton = btnCancel
        AddHandler Shown, Sub() FitConfigurationWindow()
    End Sub

    Private Sub FitConfigurationWindow()
        Dim area As Rectangle = Screen.FromControl(Me).WorkingArea
        MinimumSize = New Size(Math.Min(LogicalToDeviceUnits(640), area.Width), Math.Min(LogicalToDeviceUnits(480), area.Height))
        If WindowState <> FormWindowState.Normal Then Return
        Size = New Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height))
        Location = New Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)))
    End Sub

    Protected Overrides Sub OnDpiChanged(e As DpiChangedEventArgs)
        MinimumSize = Size.Empty
        MyBase.OnDpiChanged(e)
        FitConfigurationWindow()
    End Sub

    Private Shared Function StackLayout() As SettingsStackPanel
        Dim layout As New SettingsStackPanel With {.Dock = DockStyle.Top, .Margin = New Padding(0)}
        UiStyles.SetRole(layout, UiStyles.Role.Surface)
        Return layout
    End Function

    Private Shared Function InlineControls(ParamArray controls As Control()) As FlowLayoutPanel
        Dim line As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top, .WrapContents = True, .Margin = New Padding(0)}
        For Each control As Control In controls
            control.Anchor = AnchorStyles.Left
            control.Margin = New Padding(0, 3, 8, 3)
            If TypeOf control Is TextBox Then control.Width = 64
            If TypeOf control Is ComboBox Then control.Width = 110
            line.Controls.Add(control)
        Next
        UiStyles.SetRole(line, UiStyles.Role.Surface)
        Return line
    End Function

    Private Shared Function FieldRow(caption As Label, input As Control, ParamArray trailing As Control()) As SettingsFieldRow
        If TypeOf input Is TextBox Then DirectCast(input, TextBox).BorderStyle = BorderStyle.FixedSingle
        For Each control As Control In trailing
            If TypeOf control Is Button Then control.Padding = New Padding(12, 4, 12, 4)
        Next
        Dim row As New SettingsFieldRow(caption, input, trailing) With {.Margin = New Padding(0, 4, 0, 4)}
        UiStyles.SetRole(row, UiStyles.Role.Surface)
        Return row
    End Function

    Private Shared Sub SettingsSection(group As GroupBox, ParamArray rows As Control())
        group.Controls.Clear()
        group.AutoSize = False
        group.Dock = DockStyle.None
        group.Margin = New Padding(0, 0, 0, 24)
        group.Padding = New Padding(12, 16, 12, 12)
        Dim body = StackLayout()
        body.Dock = DockStyle.None
        body.SuspendLayout()
        For Each row As Control In rows
            row.Dock = DockStyle.None
            row.Margin = New Padding(0, 4, 0, 4)
            If TypeOf row Is Label Then row.AutoSize = True
            body.Controls.Add(row)
        Next
        DirectCast(group, Global.MegaDownloader.SettingsSection).SetBody(body)
        body.ResumeLayout(False)
        UiStyles.SetRole(group, UiStyles.Role.Surface)
    End Sub

    Private Shared Sub SettingsPage(page As TabPage, ParamArray sections As Control())
        page.Controls.Clear()
        page.Padding = New Padding(0)
        page.AutoScroll = True
        Dim body = StackLayout()
        body.Padding = New Padding(4, 20, 16, 0)
        body.SuspendLayout()
        For Each section As Control In sections
            section.Dock = DockStyle.None
            If TypeOf section Is Label Then section.AutoSize = True
            body.Controls.Add(section)
        Next
        page.Controls.Add(body)
        body.ResumeLayout(False)
        UiStyles.SetRole(page, UiStyles.Role.Surface)
    End Sub
End Class
