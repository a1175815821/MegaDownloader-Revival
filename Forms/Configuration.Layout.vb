Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class Configuration
    ' 首次打开只物化可见的“一般”页；其余页面的控件仍在字段里存活，
    ' 但暂时离开布局/句柄树，等第一次选中该页时再构建（见 EnsureSettingsPage）。
    ' 实测这能把首次打开的构造期从约 1.0 秒降到约 0.67 秒。
    Private connectionPage As TabPage
    Private settingsLayoutReady As Boolean
    Private ReadOnly settingsPagesBuilt As New HashSet(Of TabPage)
    ' 懒建页第一次选中时必须强制布局一次：页面的布局引擎不会在
    ' “页面原本为空、随后在 SelectedIndexChanged 里加入 body”时自动重跑，
    ' 否则 body 会停在默认高度 20px，整页看起来是空白。
    Private ReadOnly settingsPagesLaidOut As New HashSet(Of TabPage)

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

        connectionPage = New TabPage With {.Name = "ConnectionPage", .Text = ConexionGroup.Text}
        AddHandler ConexionGroup.TextChanged, Sub() connectionPage.Text = ConexionGroup.Text
        ' 只移动“连接”页后面的 4 个页；原先的 Clear + AddRange 会把 6 个页连同
        ' 174 个子控件全部拆掉重加，单是这一步就要约 50 ms。TabPages.Insert 在
        ' 这个窗体上不可靠(页会进入 Controls 却不进入 TabPages)，故用移除/重加。
        TabControl1.TabPages.Add(connectionPage)
        Dim trailingPages As TabPage() = {TabPage2, TabPage3, TabPage4, TabPage5}
        For Each trailing As TabPage In trailingPages
            TabControl1.TabPages.Remove(trailing)
        Next
        For Each trailing As TabPage In trailingPages
            TabControl1.TabPages.Add(trailing)
        Next
        TabControl1.Dock = DockStyle.Fill
        TabControl1.Margin = New Padding(0)
        TabControl1.Multiline = True
        TabControl1.Padding = New Point(16, 9)

        ' 未选中的页面先清空。控件本身仍由窗体字段持有，Configuration_Load
        ' 照旧给它们赋值；等页面第一次被选中时再重新放进分区布局。
        TabPage2.Controls.Clear()
        TabPage3.Controls.Clear()
        TabPage4.Controls.Clear()
        TabPage5.Controls.Clear()

        EnsureSettingsPage(TabPage1)
        ' 一般页由外壳的首次布局负责，避免在 Shown 里再强制排一遍。
        settingsPagesLaidOut.Add(TabPage1)
        settingsLayoutReady = True

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

    ''' <summary>
    ''' 第一次选中某个标签页时才构建它的分区布局。外壳、标签页和控件字段
    ''' 在构造期已经存在，所以这里不需要再创建控件，只做分区/页面重排。
    ''' </summary>
    Private Sub EnsureSettingsPage(page As TabPage)
        If page Is Nothing OrElse settingsPagesBuilt.Contains(page) Then Return
        settingsPagesBuilt.Add(page)
        If page Is TabPage1 Then
            BuildGeneralPage()
        ElseIf page Is connectionPage Then
            BuildConnectionPage()
        ElseIf page Is TabPage2 Then
            BuildAdvancedPage()
        ElseIf page Is TabPage3 Then
            BuildPresharedKeysPage()
        ElseIf page Is TabPage4 Then
            BuildStreamingPage()
        ElseIf page Is TabPage5 Then
            BuildElcPage()
        End If
        ' 页面构建发生在 Load 的主题应用之后时，需要单独补一次主题；否则
        ' 未参与首次 ApplyTheme 递归的这棵子树会保留默认浅色。
        If themeApplied Then ThemeManager.ApplyThemeToControl(page)
        ' 窗体还隐藏时不强制布局（此时宽度可能不是最终值）；Shown 时会补。
        If settingsLayoutReady AndAlso Me.Visible AndAlso page Is TabControl1.SelectedTab Then ForceSettingsPageLayout(page)
    End Sub

    ''' <summary>
    ''' 懒建页面第一次显示时补一次真实布局。SettingsPage 里的
    ''' body.ResumeLayout(False) 只恢复挂起计数，不执行布局；页面从空变有内容时
    ''' WinForms 也不会替我们重排这棵树，必须显式 PerformLayout。
    ''' </summary>
    Private Sub ForceSettingsPageLayout(page As TabPage)
        If page Is Nothing OrElse settingsPagesLaidOut.Contains(page) Then Return
        settingsPagesLaidOut.Add(page)
        If page.Controls.Count = 0 Then Return
        page.Controls(0).PerformLayout()
        page.Invalidate(True)
    End Sub

    Private Sub BuildGeneralPage()
        Dim options As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 4, .Margin = New Padding(0)}
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        UiStyles.SetRole(options, UiStyles.Role.Surface)
        For index As Integer = 0 To 3
            options.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        Dim intervalCaption As New Label With {.Text = Language.GetText("Check interval"), .AutoSize = True}
        Dim hours As New Label With {.Text = Language.GetText("hours"), .AutoSize = True}
        Dim intervalFields = InlineControls(intervalCaption, txtUpdateCheckInterval, hours)
        intervalFields.WrapContents = False
        txtUpdateCheckInterval.Width = 44
        txtUpdateCheckInterval.AccessibleName = intervalCaption.Text & " (" & hours.Text & ")"
        For Each field As Control In intervalFields.Controls
            field.Anchor = AnchorStyles.None
            field.Margin = New Padding(0, 0, 6, 0)
        Next
        Dim updatePreferences = InlineControls(chkUpdateManualOnly, intervalFields)
        updatePreferences.Padding = New Padding(20, 0, 0, 0)
        updatePreferences.Margin = New Padding(0)
        updatePreferences.TabIndex = 1
        chkUpdateManualOnly.Anchor = AnchorStyles.None
        chkUpdateManualOnly.Margin = New Padding(0, 4, 12, 4)
        chkUpdateManualOnly.TabIndex = 0
        intervalFields.Anchor = AnchorStyles.None
        intervalFields.Margin = New Padding(0, 4, 0, 4)
        intervalFields.TabIndex = 1
        Dim updateOptions = StackLayout()
        updateOptions.TabIndex = chkCheckUpdates.TabIndex
        chkCheckUpdates.Margin = New Padding(0)
        chkCheckUpdates.TabIndex = 0
        updateOptions.Controls.AddRange(New Control() {chkCheckUpdates, updatePreferences})
        Dim refreshUpdateOptions As Action =
            Sub()
                chkUpdateManualOnly.Enabled = chkCheckUpdates.Checked
                intervalFields.Enabled = chkCheckUpdates.Checked AndAlso Not chkUpdateManualOnly.Checked
            End Sub
        AddHandler chkCheckUpdates.CheckedChanged, Sub() refreshUpdateOptions()
        AddHandler chkUpdateManualOnly.CheckedChanged, Sub() refreshUpdateOptions()
        refreshUpdateOptions()

        Dim checks As Control() = {chkAnalisisPortapapeles, chkCrearDirectorio, chkComenzarPlay,
                                  InlineControls(chkApagarPC, linkApagarPC), chkStartWindows,
                                  InlineControls(chkUltimaConfig, linkUltConfig), updateOptions,
                                  InlineControls(chkUnZip, comboPrioridad)}
        For index As Integer = 0 To checks.Length - 1
            checks(index).Margin = New Padding(0, 6, 16, 6)
            options.Controls.Add(checks(index), index Mod 2, index \ 2)
        Next
        SettingsSection(GroupBox4, FieldRow(Label1, txtRuta, btnExaminar), Divider(),
                        options, Divider(), FieldRow(Label24, comboIdiomas), FieldRow(Label25, comboTheme))
        SettingsPage(TabPage1, GroupBox4)
        ' Keep the historical fields owned by the form, but sign in from the main window.
        GroupBox1.Visible = False
        TabPage1.Controls.Add(GroupBox1)
    End Sub

    Private Sub BuildConnectionPage()
        SettingsSection(ConexionGroup, InlineControls(chkLimitarVelocidad, txtLimiteVelocidadKBs, Label15),
                        InlineControls(chkReintentarError, txtPeriodoReintento, Label12), chkQuotaBreak,
                        FieldRow(Label10, txtDescSimult), FieldRow(Label9, txtConFic), LinkLabel1)
        SettingsSection(GroupBox2, chkProxy, Divider(), FieldRow(Label13, txtProxyIP), FieldRow(Label14, txtProxyPort),
                        FieldRow(Label23, txtProxyName), FieldRow(Label22, txtProxyPassword))
        SettingsPage(connectionPage, ConexionGroup, GroupBox2)
    End Sub

    Private Sub BuildAdvancedPage()
        UiStyles.SetAppFont(Label8)
        UiStyles.SetRole(Label8, UiStyles.Role.Muted)
        SettingsSection(GroupBox3, Label8, FieldRow(Label4, txtTamanoPaquete, Label5),
                        FieldRow(Label6, txtTamanoBuffer, Label7), FieldRow(Label11, comboLog))
        SettingsSection(GroupBox5, chkServidorWeb, chkServidorWebLAN, Divider(),
                        FieldRow(lblServidorWebBindIP, txtServidorWebBindIP), FieldRow(Label16, txtServidorWebPort),
                        FieldRow(Label18, txtServidorWebTimeout, Label19), FieldRow(Label17, txtServidorWebPassword),
                        FieldRow(Label20, txtServidorWebNombre), FieldRow(Label21, txtServidorWebTemplate, btnExaminarTemplate))
        SettingsPage(TabPage2, GroupBox3, GroupBox5)
    End Sub

    Private Sub BuildPresharedKeysPage()
        txtKeyList.MinimumSize = New Size(0, 200)
        txtKeyList.Dock = DockStyle.Top
        SettingsSection(GroupBox, txtKeyList)
        SettingsPage(TabPage3, lblDesc, GroupBox)
    End Sub

    Private Sub BuildStreamingPage()
        SettingsSection(GroupBox6, chkStreamingServer, FieldRow(lblStreamingPort, txtStreamingPort), FieldRow(lblStreamingPassword, txtStreamingPassword))
        SettingsSection(GroupBox8, FieldRow(Label26, txtVLCPath, btnExaminarVLCPath), linkDownloadVLC)
        UiStyles.SetRole(lblInfoStreaming, UiStyles.Role.Muted)
        SettingsSection(GroupBox7, lblInfoStreaming)
        SettingsPage(TabPage4, GroupBox6, GroupBox8, GroupBox7)
    End Sub

    Private Sub BuildElcPage()
        EnsureElcControl()
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

    Private Shared Function Divider() As SettingsDivider
        Dim d As New SettingsDivider()
        UiStyles.SetRole(d, UiStyles.Role.Surface)
        Return d
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
