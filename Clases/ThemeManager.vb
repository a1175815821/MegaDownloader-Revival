Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.Win32

''' <summary>
''' 主题管理器:Auto 模式从注册表读取系统深/浅色偏好,Light/Dark 直接应用预设配色。
''' Auto 模式下监听系统主题变化并实时刷新已注册窗体。
''' 支持 ObjectListView (BrightIdeasSoftware.TreeListView)、ToolStrip 系列(MenuStrip/StatusStrip/ContextMenuStrip)、
''' 标准控件(TextBox/Button/Label 等)以及容器控件(Panel/GroupBox/TableLayoutPanel)。
''' </summary>
Public NotInheritable Class ThemeManager

    Private Sub New()
    End Sub

    Public Enum ResolvedTheme
        Light
        Dark
    End Enum

    Private Shared _current As ResolvedTheme = ResolvedTheme.Light
    Private Shared _currentMode As ConfiguracionUI.ThemeModeType = ConfiguracionUI.ThemeModeType.Auto
    Private Shared _systemWatchHooked As Boolean = False
    Private Shared ReadOnly _watchedForms As New Generic.List(Of WeakReference)

    ' Muted:次要说明文字专用色。此前这类文字取 "Border",而 Border 是边框色,
    ' 浅色主题下与 ControlBack(240,240,240)对比度只有 1.38:1(深色 1.82:1),
    ' 远低于正文 4.5:1 的下限,左栏总览说明几乎读不出来。
    ' 现取值对 ControlBack 的对比度:浅色 5.16:1 / 深色 4.97:1。
    ' 注意:注释不能写进 From { } 集合初始化器内部 —— VB 的隐式行继续会被注释行打断,
    ' 右花括号随之提前闭合,其后每个元素都被判语法错误(BC30035)。
    ' 浅色配色
    Private Shared ReadOnly LightColors As New Dictionary(Of String, Color) From
    {
        {"Back", Color.White},
        {"Fore", Color.FromArgb(32, 42, 58)},
        {"AltBack", Color.FromArgb(248, 250, 252)},
        {"ControlBack", Color.FromArgb(244, 246, 249)},
        {"Canvas", Color.FromArgb(231, 236, 243)},
        {"Border", Color.FromArgb(216, 222, 231)},
        {"ButtonBack", Color.FromArgb(246, 248, 251)},
        {"ButtonBorder", Color.FromArgb(164, 177, 194)},
        {"ButtonDisabledBack", Color.FromArgb(239, 242, 247)},
        {"ButtonDisabledFore", Color.FromArgb(102, 113, 129)},
        {"ButtonDisabledBorder", Color.FromArgb(193, 202, 214)},
        {"Muted", Color.FromArgb(92, 105, 124)},
        {"Selection", Color.FromArgb(225, 236, 252)},
        {"SelectionFore", Color.FromArgb(25, 58, 104)},
        {"Accent", Color.FromArgb(199, 45, 60)},
        {"AccentFore", Color.White},
        {"AccentHover", Color.FromArgb(179, 37, 51)},
        {"AccentPressed", Color.FromArgb(154, 30, 43)},
        {"Link", Color.FromArgb(36, 94, 173)},
        {"ToolBack", Color.White},
        {"ToolBorder", Color.FromArgb(216, 222, 231)},
        {"ErrorFore", Color.FromArgb(192, 0, 0)},
        {"QuotaBack", Color.FromArgb(178, 34, 34)},
        {"QuotaFore", Color.White},
        {"SuccessFore", Color.FromArgb(26, 121, 90)},
        {"ProgressBack", Color.White},
        {"ProgressFill", Color.FromArgb(40, 148, 113)},
        {"ProgressGradientStart", Color.FromArgb(40, 148, 113)},
        {"ProgressGradientEnd", Color.FromArgb(40, 148, 113)},
        {"ButtonHover", Color.FromArgb(235, 240, 248)},
        {"ButtonPressed", Color.FromArgb(221, 230, 243)}
    }

    ' Muted:深色主题下的次要说明文字色,对 ControlBack(51,51,55)对比度 4.97:1
    ' (此前误用 Border 色,仅 1.82:1)。
    ' 深色配色(VS Code Dark+ 风格)
    Private Shared ReadOnly DarkColors As New Dictionary(Of String, Color) From
    {
        {"Back", Color.FromArgb(30, 36, 47)},
        {"Fore", Color.FromArgb(230, 235, 243)},
        {"AltBack", Color.FromArgb(34, 42, 54)},
        {"ControlBack", Color.FromArgb(37, 45, 58)},
        {"Canvas", Color.FromArgb(17, 23, 33)},
        {"Border", Color.FromArgb(64, 77, 96)},
        {"ButtonBack", Color.FromArgb(46, 57, 73)},
        {"ButtonBorder", Color.FromArgb(100, 116, 139)},
        {"ButtonDisabledBack", Color.FromArgb(37, 45, 58)},
        {"ButtonDisabledFore", Color.FromArgb(153, 169, 191)},
        {"ButtonDisabledBorder", Color.FromArgb(73, 87, 106)},
        {"Muted", Color.FromArgb(166, 180, 199)},
        {"Selection", Color.FromArgb(46, 68, 99)},
        {"SelectionFore", Color.FromArgb(238, 245, 255)},
        {"Accent", Color.FromArgb(190, 46, 61)},
        {"AccentFore", Color.White},
        {"AccentHover", Color.FromArgb(211, 57, 73)},
        {"AccentPressed", Color.FromArgb(164, 35, 49)},
        {"Link", Color.FromArgb(135, 184, 250)},
        {"ToolBack", Color.FromArgb(30, 36, 47)},
        {"ToolBorder", Color.FromArgb(64, 77, 96)},
        {"ErrorFore", Color.FromArgb(244, 135, 113)},
        {"QuotaBack", Color.FromArgb(122, 28, 28)},
        {"QuotaFore", Color.White},
        {"SuccessFore", Color.FromArgb(78, 201, 176)},
        {"ProgressBack", Color.FromArgb(45, 45, 48)},
        {"ProgressFill", Color.FromArgb(67, 184, 146)},
        {"ProgressGradientStart", Color.FromArgb(67, 184, 146)},
        {"ProgressGradientEnd", Color.FromArgb(67, 184, 146)},
        {"ButtonHover", Color.FromArgb(62, 78, 100)},
        {"ButtonPressed", Color.FromArgb(77, 95, 119)}
    }

    Private Shared Function CurrentColors() As Dictionary(Of String, Color)
        Return If(_current = ResolvedTheme.Dark, DarkColors, LightColors)
    End Function

    ''' <summary>
    ''' 按 token 名取当前主题颜色。未知 key 返回 Fore。
    ''' </summary>
    Public Shared Function GetColor(key As String) As Color
        Dim colors = CurrentColors()
        If colors.ContainsKey(key) Then
            Return colors(key)
        End If
        Return colors("Fore")
    End Function

    ''' <summary>
    ''' 检测系统当前是否使用深色主题。
    ''' 通过读取注册表 HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme 实现。
    ''' 不支持深浅模式的旧系统(无该键)默认返回 False(浅色)。
    ''' </summary>
    Public Shared Function IsSystemDarkMode() As Boolean
        Try
            Using key As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", False)
                If key Is Nothing Then Return False
                Dim val As Object = key.GetValue("AppsUseLightTheme")
                If val Is Nothing Then Return False
                Return CInt(val) = 0 ' 0 = 深色,1 = 浅色
            End Using
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
            Return False
        End Try
    End Function

    Public Shared Function Resolve(mode As ConfiguracionUI.ThemeModeType) As ResolvedTheme
        Select Case mode
            Case ConfiguracionUI.ThemeModeType.Dark
                Return ResolvedTheme.Dark
            Case ConfiguracionUI.ThemeModeType.Light
                Return ResolvedTheme.Light
            Case Else ' Auto
                Return If(IsSystemDarkMode(), ResolvedTheme.Dark, ResolvedTheme.Light)
        End Select
    End Function

    ''' <summary>
    ''' 应用主题到指定窗体(递归所有子控件),并设置全局 ToolStrip 渲染器。
    ''' 应在窗体 Load/Shown 后调用,确保所有控件已创建。
    ''' </summary>
    Public Shared Sub ApplyTheme(form As Form, mode As ConfiguracionUI.ThemeModeType)
        Dim resolved As ResolvedTheme = Resolve(mode)
        _current = resolved
        _currentMode = mode
        Dim colors As Dictionary(Of String, Color) = If(resolved = ResolvedTheme.Dark, DarkColors, LightColors)

        ' 设置全局 ToolStrip 渲染器(影响 MenuStrip/StatusStrip/ContextMenuStrip 等)
        ToolStripManager.Renderer = New ToolStripProfessionalRenderer(New ThemeColorTable(colors))

        ApplyThemeRecursive(form, colors, resolved)

        ApplyThemeToFormContextMenus(form, colors)

        ' ToolTip 不在 Controls 树中,需从字段显式处理(RC:此前深色下仍浅黄底)
        ApplyThemeToFormToolTips(form, colors)

        RegisterFormForSystemThemeWatch(form)
        EnsureSystemThemeWatch()
    End Sub

    Private Shared Sub RegisterFormForSystemThemeWatch(form As Form)
        If form Is Nothing Then Return
        For Each wr As WeakReference In _watchedForms
            If wr.IsAlive AndAlso Object.ReferenceEquals(wr.Target, form) Then Return
        Next
        _watchedForms.Add(New WeakReference(form))
        AddHandler form.FormClosed, AddressOf WatchedForm_FormClosed
    End Sub

    Private Shared Sub WatchedForm_FormClosed(sender As Object, e As FormClosedEventArgs)
        Dim form = TryCast(sender, Form)
        If form Is Nothing Then Return
        RemoveHandler form.FormClosed, AddressOf WatchedForm_FormClosed
        For i As Integer = _watchedForms.Count - 1 To 0 Step -1
            If Not _watchedForms(i).IsAlive OrElse Object.ReferenceEquals(_watchedForms(i).Target, form) Then
                _watchedForms.RemoveAt(i)
            End If
        Next
    End Sub

    Private Shared Sub EnsureSystemThemeWatch()
        If _systemWatchHooked Then Return
        Try
            AddHandler SystemEvents.UserPreferenceChanged, AddressOf OnUserPreferenceChanged
            _systemWatchHooked = True
        Catch
        End Try
    End Sub

    Private Shared Sub OnUserPreferenceChanged(sender As Object, e As UserPreferenceChangedEventArgs)
        If _currentMode <> ConfiguracionUI.ThemeModeType.Auto Then Return
        If e.Category <> UserPreferenceCategory.Color AndAlso e.Category <> UserPreferenceCategory.General Then Return
        Dim nextTheme As ResolvedTheme = Resolve(ConfiguracionUI.ThemeModeType.Auto)
        If nextTheme = _current Then Return
        Try
            For Each form As Form In Application.OpenForms.Cast(Of Form)().ToList()
                ' N-P3-2: per-form guard. The loop body used to throw straight into the
                ' outer empty Catch (e.g. InvalidOperationException from BeginInvoke when
                ' a form's handle died between the IsHandleCreated check and the call),
                ' aborting the whole refresh so the remaining windows kept the old
                ' colors — light and dark mixed on screen after a system theme switch.
                ' One bad form must not skip the rest.
                Try
                    If form.IsHandleCreated AndAlso Not form.IsDisposed Then
                        If form.InvokeRequired Then
                            form.BeginInvoke(New Action(Of Form)(AddressOf RefreshFormThemeSafe), form)
                        Else
                            RefreshFormThemeSafe(form)
                        End If
                    End If
                Catch exForm As Exception
                    Log.WriteDebug("ThemeManager: one form could not be refreshed, continuing with the rest: " & Log.SafeException(exForm))
                End Try
            Next
        Catch ex As Exception
            Log.WriteDebug("ThemeManager: system theme change sweep failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Shared Sub RefreshFormThemeSafe(form As Form)
        Try
            If form Is Nothing OrElse form.IsDisposed Then Return
            ApplyTheme(form, ConfiguracionUI.ThemeModeType.Auto)
            Dim main = TryCast(form, Main)
            If main IsNot Nothing Then
                main.ApplyCurrentTheme()
            End If
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 使用上次保存的主题模式应用到指定窗体。供子窗体在 Load 事件中调用,
    ''' 无需自行访问 Main.Config。
    ''' </summary>
    Public Shared Sub ApplyTheme(form As Form)
        ApplyTheme(form, _currentMode)
    End Sub

    ''' <summary>
    ''' 显式主题化 ContextMenuStrip(不在 Form.Controls 树中)。
    ''' </summary>
    Public Shared Sub ApplyThemeToContextMenuStrip(cms As ContextMenuStrip)
        If cms Is Nothing Then Return
        ApplyThemeToToolStrip(cms, CurrentColors())
    End Sub

    Private Shared Sub ApplyThemeToFormContextMenus(form As Form, colors As Dictionary(Of String, Color))
        If form Is Nothing Then Return
        Try
            Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public Or Reflection.BindingFlags.NonPublic
            For Each fi As Reflection.FieldInfo In form.GetType().GetFields(flags)
                If GetType(ContextMenuStrip).IsAssignableFrom(fi.FieldType) Then
                    Dim cms = TryCast(fi.GetValue(form), ContextMenuStrip)
                    If cms IsNot Nothing Then
                        ApplyThemeToToolStrip(cms, colors)
                    End If
                End If
            Next
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try
    End Sub

    ''' N-P3-3: WEAK registry — see ApplyThemeToToolTip. A strong HashSet(Of ToolTip)
    ''' here rooted every themed tooltip for the process lifetime.
    Private Shared ReadOnly _themedToolTips As New Generic.HashSet(Of WeakReference)

    ''' <summary>主题化单个 ToolTip(含懒创建)。字段扫描扫不到的调用此方法。</summary>
    Public Shared Sub ApplyThemeToToolTip(tt As ToolTip)
        If tt Is Nothing Then Return
        ApplyThemeToToolTip(tt, CurrentColors())
    End Sub

    Private Shared Sub ApplyThemeToToolTip(tt As ToolTip, colors As Dictionary(Of String, Color))
        If tt Is Nothing Then Return
        Try
            tt.BackColor = colors("ControlBack")
            tt.ForeColor = colors("Fore")
            ' RC:非 OwnerDraw 下 Back/Fore 不生效,必须自绘才可见深色 tooltip。
            tt.OwnerDraw = True
            SyncLock _themedToolTips
                ' N-P3-3: the registry holds WEAK references on purpose. It used to
                ' root every ToolTip it ever themed for the whole process lifetime
                ' (entries were only removed on tt.Disposed, which never fired for the
                ' lazy-created "If t Is Nothing Then t = New ToolTip" help tips nobody
                ' disposed), so every open/close of the Configuration / Properties
                ' window leaked one ToolTip HWND + Draw handler pair, unbounded.
                ' Weak refs keep the "attach handlers once" semantics (an entry that
                ' was collected is re-attached on demand) without extending lifetime.
                PruneDeadToolTips()
                If Not IsToolTipRegistered(tt) Then
                    AddHandler tt.Draw, AddressOf ThemedToolTip_Draw
                    AddHandler tt.Disposed, AddressOf ThemedToolTip_Disposed
                    _themedToolTips.Add(New WeakReference(tt))
                End If
            End SyncLock
        Catch
        End Try
    End Sub

    Private Shared Function IsToolTipRegistered(ByVal tt As ToolTip) As Boolean
        For Each wr As WeakReference In _themedToolTips
            If wr.IsAlive AndAlso Object.ReferenceEquals(wr.Target, tt) Then Return True
        Next
        Return False
    End Function

    Private Shared Sub PruneDeadToolTips()
        If _themedToolTips.Count = 0 Then Return
        Dim dead As New Generic.List(Of WeakReference)
        For Each wr As WeakReference In _themedToolTips
            If Not wr.IsAlive Then dead.Add(wr)
        Next
        For Each wr As WeakReference In dead
            _themedToolTips.Remove(wr)
        Next
    End Sub

    Private Shared Sub ThemedToolTip_Disposed(sender As Object, e As EventArgs)
        Try
            Dim tt As ToolTip = TryCast(sender, ToolTip)
            If tt Is Nothing Then Return
            SyncLock _themedToolTips
                Dim toRemove As WeakReference = Nothing
                For Each wr As WeakReference In _themedToolTips
                    If wr.IsAlive AndAlso Object.ReferenceEquals(wr.Target, tt) Then
                        toRemove = wr
                        Exit For
                    End If
                Next
                If toRemove IsNot Nothing Then
                    RemoveHandler tt.Draw, AddressOf ThemedToolTip_Draw
                    RemoveHandler tt.Disposed, AddressOf ThemedToolTip_Disposed
                    _themedToolTips.Remove(toRemove)
                End If
            End SyncLock
        Catch
        End Try
    End Sub

    Private Shared Sub ApplyThemeToFormToolTips(form As Form, colors As Dictionary(Of String, Color))
        If form Is Nothing Then Return
        Try
            Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public Or Reflection.BindingFlags.NonPublic
            For Each fi As Reflection.FieldInfo In form.GetType().GetFields(flags)
                If GetType(ToolTip).IsAssignableFrom(fi.FieldType) Then
                    ApplyThemeToToolTip(TryCast(fi.GetValue(form), ToolTip), colors)
                End If
            Next
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Shared Sub ThemedToolTip_Draw(sender As Object, e As DrawToolTipEventArgs)
        Try
            Dim tt As ToolTip = TryCast(sender, ToolTip)
            Dim back As Color = CurrentColors()("ControlBack")
            Dim fore As Color = CurrentColors()("Fore")
            If tt IsNot Nothing Then
                Try
                    back = tt.BackColor
                    fore = tt.ForeColor
                Catch
                End Try
            End If
            Using br As New SolidBrush(back)
                e.Graphics.FillRectangle(br, e.Bounds)
            End Using
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, e.Bounds, fore,
                TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.WordBreak)
            Using pen As New Pen(CurrentColors()("Border"))
                e.Graphics.DrawRectangle(pen, New Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1))
            End Using
        Catch
            Try
                e.DrawBackground()
                e.DrawText()
                e.DrawBorder()
            Catch
            End Try
        End Try
    End Sub

    Private Shared Sub ApplyThemeToToolStrip(ts As ToolStrip, colors As Dictionary(Of String, Color))
        ts.BackColor = colors("ToolBack")
        ts.ForeColor = colors("Fore")
        ts.RenderMode = ToolStripRenderMode.ManagerRenderMode
        For Each item As ToolStripItem In ts.Items
            ApplyThemeToToolStripItem(item, colors)
        Next
    End Sub

    Private Shared Sub ApplyThemeRecursive(control As Control, colors As Dictionary(Of String, Color), theme As ResolvedTheme)

        ' ObjectListView (BrightIdeasSoftware.TreeListView) - 主窗口的下载列表
        If TypeOf control Is BrightIdeasSoftware.TreeListView Then
            ApplyThemeToObjectListView(CType(control, BrightIdeasSoftware.TreeListView), colors, theme)
            GoTo RecurseChildren
        End If

        ' ObjectListView 基类(ObjectListView, FastObjectListView 等)
        If TypeOf control Is BrightIdeasSoftware.ObjectListView Then
            ApplyThemeToObjectListView(CType(control, BrightIdeasSoftware.ObjectListView), colors, theme)
            GoTo RecurseChildren
        End If

        ' Form
        If TypeOf control Is Form Then
            CType(control, Form).BackColor = colors("Back")
            CType(control, Form).ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' GroupBox: Flat 避免系统 3D 浅色边框
        If TypeOf control Is GroupBox Then
            Dim gb As GroupBox = CType(control, GroupBox)
            gb.BackColor = colors("ControlBack")
            gb.ForeColor = colors("Fore")
            gb.FlatStyle = FlatStyle.Flat
            GoTo RecurseChildren
        End If

        ' 容器控件
        If TypeOf control Is Panel OrElse
           TypeOf control Is TableLayoutPanel OrElse TypeOf control Is FlowLayoutPanel OrElse
           TypeOf control Is SplitContainer OrElse TypeOf control Is SplitterPanel Then
            control.BackColor = colors("ControlBack")
            control.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' Button: Flat + 主题边框,避免 Visual Styles 的 3D 白边
        If TypeOf control Is Button Then
            ApplyThemeToButton(CType(control, Button), colors)
            GoTo RecurseChildren
        End If

        ' Label / LinkLabel
        If TypeOf control Is LinkLabel Then
            control.BackColor = Color.Transparent
            control.ForeColor = colors("Fore")
            CType(control, LinkLabel).LinkColor = colors("Link")
            CType(control, LinkLabel).VisitedLinkColor = colors("Link")
            CType(control, LinkLabel).ActiveLinkColor = colors("Link")
            CType(control, LinkLabel).DisabledLinkColor = colors("Muted")
            GoTo RecurseChildren
        End If
        If TypeOf control Is Label Then
            control.BackColor = Color.Transparent
            control.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' Native DropDownList painting can use a light system background in dark mode.
        ' Draw the item with theme colors, including the closed selected value.
        If TypeOf control Is ComboBox Then
            Dim combo As ComboBox = CType(control, ComboBox)
            combo.BackColor = colors("Back")
            combo.ForeColor = colors("Fore")
            If combo.DropDownStyle = ComboBoxStyle.DropDownList AndAlso combo.DrawMode = DrawMode.Normal Then
                combo.DrawMode = DrawMode.OwnerDrawFixed
                AddHandler combo.DrawItem, AddressOf DrawThemedComboItem
            End If
            combo.Invalidate()
            GoTo RecurseChildren
        End If

        ' 文本类控件
        If TypeOf control Is TextBox OrElse TypeOf control Is RichTextBox OrElse
           TypeOf control Is NumericUpDown OrElse
           TypeOf control Is MaskedTextBox Then
            control.BackColor = colors("Back")
            control.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' CheckBox / RadioButton
        If TypeOf control Is CheckBox OrElse TypeOf control Is RadioButton Then
            control.BackColor = Color.Transparent
            control.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' TabControl / TabPage
        If TypeOf control Is TabControl Then
            Dim tc As TabControl = CType(control, TabControl)
            tc.BackColor = colors("Back")
            tc.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If
        If TypeOf control Is TabPage Then
            Dim tp As TabPage = CType(control, TabPage)
            tp.BackColor = colors("Back")
            tp.ForeColor = colors("Fore")
            tp.UseVisualStyleBackColor = False
            GoTo RecurseChildren
        End If

        ' DataGridView
        If TypeOf control Is DataGridView Then
            Dim dgv As DataGridView = CType(control, DataGridView)
            dgv.BackgroundColor = colors("AltBack")
            dgv.DefaultCellStyle.BackColor = colors("Back")
            dgv.DefaultCellStyle.ForeColor = colors("Fore")
            dgv.DefaultCellStyle.SelectionBackColor = colors("Selection")
            dgv.DefaultCellStyle.SelectionForeColor = colors("SelectionFore")
            dgv.AlternatingRowsDefaultCellStyle.BackColor = colors("AltBack")
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = colors("Fore")
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = colors("Selection")
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = colors("SelectionFore")
            dgv.ColumnHeadersDefaultCellStyle.BackColor = colors("ControlBack")
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = colors("Fore")
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = colors("ControlBack")
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = colors("Fore")
            dgv.RowHeadersDefaultCellStyle.BackColor = colors("ControlBack")
            dgv.RowHeadersDefaultCellStyle.ForeColor = colors("Fore")
            dgv.GridColor = colors("Border")
            dgv.EnableHeadersVisualStyles = False
            GoTo RecurseChildren
        End If

        ' ListView (原生,非 OLV)
        If TypeOf control Is ListView Then
            Dim lv As ListView = CType(control, ListView)
            lv.BackColor = colors("Back")
            lv.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' TreeView
        If TypeOf control Is TreeView Then
            Dim tv As TreeView = CType(control, TreeView)
            tv.BackColor = colors("Back")
            tv.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' ListBox / CheckedListBox (RC:此前漏分支,靠各窗体手写补丁)
        If TypeOf control Is ListBox Then
            Dim lb As ListBox = CType(control, ListBox)
            lb.BackColor = colors("Back")
            lb.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If
        If TypeOf control Is CheckedListBox Then
            Dim clb As CheckedListBox = CType(control, CheckedListBox)
            clb.BackColor = colors("Back")
            clb.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' PictureBox - 透明背景
        If TypeOf control Is PictureBox Then
            CType(control, PictureBox).BackColor = Color.Transparent
            GoTo RecurseChildren
        End If

        ' ProgressBar
        If TypeOf control Is ProgressBar Then
            Dim pb As ProgressBar = CType(control, ProgressBar)
            pb.BackColor = colors("ControlBack")
            pb.ForeColor = colors("Selection")
            GoTo RecurseChildren
        End If

        ' ToolStrip 系列(MenuStrip/StatusStrip/ToolStrip - 不含 ContextMenuStrip,它不是 Control)
        If TypeOf control Is ToolStrip Then
            ApplyThemeToToolStrip(CType(control, ToolStrip), colors)
            Return ' ToolStrip 的 Items 不是 Controls.Controls,不要走默认递归
        End If

        ' 其他类型:若是容器,套用背景;否则保留默认
        If control.HasChildren Then
            control.BackColor = colors("Back")
            control.ForeColor = colors("Fore")
        End If

RecurseChildren:
        UiStyles.Apply(control)
        For Each child As Control In control.Controls
            ApplyThemeRecursive(child, colors, theme)
        Next
    End Sub

    Private Shared Sub ApplyThemeToObjectListView(olv As BrightIdeasSoftware.ObjectListView,
                                                  colors As Dictionary(Of String, Color),
                                                  theme As ResolvedTheme)
        RemoveHandler olv.HandleCreated, AddressOf ThemedList_HandleCreated
        AddHandler olv.HandleCreated, AddressOf ThemedList_HandleCreated
        ' 关键:关闭 Explorer 主题,否则自定义颜色不生效
        Try
            olv.UseExplorerTheme = False
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try
        Try
            olv.UseTranslucentSelection = False
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try

        olv.BackColor = colors("Back")
        olv.ForeColor = colors("Fore")
        olv.UseAlternatingBackColors = True
        Dim emptyOverlay As BrightIdeasSoftware.TextOverlay = TryCast(olv.EmptyListMsgOverlay, BrightIdeasSoftware.TextOverlay)
        If emptyOverlay IsNot Nothing Then
            emptyOverlay.BackColor = colors("Back")
            emptyOverlay.TextColor = colors("Muted")
            emptyOverlay.BorderColor = colors("Border")
            emptyOverlay.BorderWidth = 0
        End If
        Try
            olv.AlternateRowBackColor = colors("AltBack")
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try

        ' 选中行的颜色(失焦/聚焦)- 通过反射访问,因不同 OLV 版本属性名不同
        TrySetProperty(olv, "SelectedBackColor", colors("Selection"))
        TrySetProperty(olv, "SelectedForeColor", colors("SelectionFore"))
        TrySetProperty(olv, "UnfocusedSelectedBackColor", colors("Selection"))
        TrySetProperty(olv, "UnfocusedSelectedForeColor", colors("SelectionFore"))
        TrySetProperty(olv, "UseHotControls", False)

        ' 表头样式
        Try
            Dim headerStyle As New BrightIdeasSoftware.HeaderFormatStyle()
            headerStyle.Normal.BackColor = colors("ControlBack")
            headerStyle.Normal.ForeColor = colors("Fore")
            headerStyle.Normal.FrameColor = colors("Border")
            headerStyle.Hot.BackColor = colors("AltBack")
            headerStyle.Hot.ForeColor = colors("Fore")
            headerStyle.Hot.FrameColor = colors("Border")
            headerStyle.Pressed.BackColor = colors("ControlBack")
            headerStyle.Pressed.ForeColor = colors("Fore")
            headerStyle.Pressed.FrameColor = colors("Border")
            olv.HeaderFormatStyle = headerStyle
            olv.HeaderUsesThemes = False
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try

        ' TreeColumnRenderer (树列的展开/折叠图标所在列) - 仅 TreeListView 有此属性
        Try
            Dim renderer As Object = olv.GetType().GetProperty("TreeColumnRenderer").GetValue(olv, Nothing)
            If renderer IsNot Nothing Then
                TrySetProperty(renderer, "BranchColor", colors("Border"))
                TrySetProperty(renderer, "LineColor", colors("Border"))
            End If
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try
        ' Reuse the virtual model; discard only cached row formatting from the prior palette.
        Dim virtualList As BrightIdeasSoftware.VirtualObjectListView = TryCast(olv, BrightIdeasSoftware.VirtualObjectListView)
        If virtualList IsNot Nothing Then virtualList.ClearCachedInfo()
        SyncNativeListColors(olv)
        olv.Invalidate()
    End Sub

    Friend Shared Sub RefreshListTheme(list As BrightIdeasSoftware.ObjectListView)
        If list Is Nothing OrElse list.IsDisposed Then Return
        ApplyThemeToObjectListView(list, CurrentColors(), _current)
    End Sub

    <System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="SendMessageW", ExactSpelling:=True)>
    Private Shared Function SendListViewMessage(window As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Shared Sub SyncNativeListColors(list As ListView)
        If list.IsDisposed OrElse Not list.IsHandleCreated OrElse list.InvokeRequired Then Return
        ' ListView's native colors can diverge from its managed properties after handle recreation.
        ' LVM_SETBKCOLOR, LVM_SETTEXTCOLOR and LVM_SETTEXTBKCOLOR also cover unused row space.
        Dim back As New IntPtr(ColorTranslator.ToWin32(list.BackColor))
        Dim fore As New IntPtr(ColorTranslator.ToWin32(list.ForeColor))
        SendListViewMessage(list.Handle, &H1001, IntPtr.Zero, back)
        SendListViewMessage(list.Handle, &H1024, IntPtr.Zero, fore)
        SendListViewMessage(list.Handle, &H1026, IntPtr.Zero, back)
    End Sub

    Private Shared Sub ThemedList_HandleCreated(sender As Object, e As EventArgs)
        Dim list As ListView = DirectCast(sender, ListView)
        SyncNativeListColors(list)
        Try
            ' ObjectListView applies its own native styles after the base HandleCreated event.
            list.BeginInvoke(New MethodInvoker(Sub()
                                                  If list.IsDisposed OrElse Not list.IsHandleCreated Then Return
                                                  SyncNativeListColors(list)
                                                  list.Invalidate()
                                              End Sub))
        Catch ex As InvalidOperationException
            ' The window may be closing while the deferred update is queued.
        End Try
    End Sub

    Private Shared Sub TrySetProperty(target As Object, propertyName As String, value As Object)
        Try
            Dim prop As System.Reflection.PropertyInfo = target.GetType().GetProperty(propertyName)
            If prop IsNot Nothing AndAlso prop.CanWrite Then
                prop.SetValue(target, value, Nothing)
            End If
        Catch ex As Exception
            Log.WriteDebug("ThemeManager reflection/theme apply failed once: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Shared Sub DrawThemedComboItem(sender As Object, e As DrawItemEventArgs)
        Dim combo As ComboBox = DirectCast(sender, ComboBox)
        Dim selected As Boolean = (e.State And DrawItemState.Selected) <> 0 AndAlso (e.State And DrawItemState.ComboBoxEdit) = 0
        Dim background As Color = GetColor(If(selected, "Selection", "Back"))
        Dim foreground As Color = GetColor(If(Not combo.Enabled, "Muted", If(selected, "SelectionFore", "Fore")))
        Using brush As New SolidBrush(background)
            e.Graphics.FillRectangle(brush, e.Bounds)
        End Using
        If e.Index >= 0 AndAlso e.Index < combo.Items.Count Then
            TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items(e.Index)), e.Font, e.Bounds, foreground,
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        End If
        If (e.State And DrawItemState.Focus) <> 0 Then e.DrawFocusRectangle()
    End Sub

    Private Shared Sub ApplyThemeToButton(btn As Button, colors As Dictionary(Of String, Color))
        btn.UseVisualStyleBackColor = False
        btn.FlatStyle = FlatStyle.Flat
        btn.BackColor = colors("ButtonBack")
        btn.ForeColor = colors("Fore")
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.BorderColor = colors("ButtonBorder")
        btn.FlatAppearance.MouseOverBackColor = colors("ButtonHover")
        btn.FlatAppearance.MouseDownBackColor = colors("ButtonPressed")
        ' 有图标的工具栏按钮禁用时保持边框可见
        btn.FlatAppearance.CheckedBackColor = colors("Selection")
        RemoveHandler btn.Paint, AddressOf DrawDisabledButton
        AddHandler btn.Paint, AddressOf DrawDisabledButton
    End Sub

    Private Shared Sub DrawDisabledButton(sender As Object, e As PaintEventArgs)
        Dim btn As Button = DirectCast(sender, Button)
        ' Native disabled text is nearly black on dark surfaces. Preserve native
        ' image layout for image buttons; text-only buttons get legible theme ink.
        If btn.Enabled OrElse btn.Image IsNot Nothing OrElse btn.ClientSize.Width < 2 OrElse btn.ClientSize.Height < 2 Then Return
        Dim bounds As Rectangle = btn.ClientRectangle
        Using brush As New SolidBrush(GetColor("ButtonDisabledBack"))
            e.Graphics.FillRectangle(brush, bounds)
        End Using
        Using pen As New Pen(GetColor("ButtonDisabledBorder"))
            e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1)
        End Using
        Dim flags As TextFormatFlags = If(btn.Text.Contains(vbLf), TextFormatFlags.WordBreak, TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
        Select Case btn.TextAlign
            Case ContentAlignment.TopLeft, ContentAlignment.MiddleLeft, ContentAlignment.BottomLeft
                flags = flags Or TextFormatFlags.Left
            Case ContentAlignment.TopRight, ContentAlignment.MiddleRight, ContentAlignment.BottomRight
                flags = flags Or TextFormatFlags.Right
            Case Else
                flags = flags Or TextFormatFlags.HorizontalCenter
        End Select
        Select Case btn.TextAlign
            Case ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight
                flags = flags Or TextFormatFlags.Top
            Case ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight
                flags = flags Or TextFormatFlags.Bottom
            Case Else
                flags = flags Or TextFormatFlags.VerticalCenter
        End Select
        If Not btn.UseMnemonic Then flags = flags Or TextFormatFlags.NoPrefix
        If btn.RightToLeft = RightToLeft.Yes Then flags = flags Or TextFormatFlags.RightToLeft
        bounds = New Rectangle(2 + btn.Padding.Left, 1 + btn.Padding.Top,
                               Math.Max(0, bounds.Width - 4 - btn.Padding.Horizontal),
                               Math.Max(0, bounds.Height - 2 - btn.Padding.Vertical))
        TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, bounds, GetColor("ButtonDisabledFore"), flags)
    End Sub

    Private Shared Sub ApplyThemeToToolStripItem(item As ToolStripItem, colors As Dictionary(Of String, Color))
        item.BackColor = colors("ToolBack")
        item.ForeColor = colors("Fore")

        If TypeOf item Is ToolStripDropDownItem Then
            Dim dropDown As ToolStripDropDownItem = CType(item, ToolStripDropDownItem)
            If dropDown.HasDropDownItems Then ApplyThemeToToolStrip(dropDown.DropDown, colors)
        ElseIf TypeOf item Is ToolStripControlHost Then
            ApplyThemeRecursive(CType(item, ToolStripControlHost).Control, colors, _current)
        ElseIf TypeOf item Is ToolStripLabel Then
            CType(item, ToolStripLabel).BackColor = colors("ToolBack")
        End If
    End Sub

    Public Shared ReadOnly Property Current As ResolvedTheme
        Get
            Return _current
        End Get
    End Property

    Public Shared ReadOnly Property CurrentMode As ConfiguracionUI.ThemeModeType
        Get
            Return _currentMode
        End Get
    End Property

End Class

''' <summary>
''' 自定义 ToolStrip 配色表,使 MenuStrip/StatusStrip/ContextMenuStrip 跟随深/浅色主题。
''' </summary>
Friend Class ThemeColorTable
    Inherits ProfessionalColorTable

    Private ReadOnly _colors As Dictionary(Of String, Color)

    Public Sub New(colors As Dictionary(Of String, Color))
        _colors = colors
    End Sub

    Public Overrides ReadOnly Property ToolStripBorder As Color
        Get
            Return _colors("ToolBorder")
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripGradientMiddle As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripGradientEnd As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ToolStripDropDownBackground As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuStripGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuStripGradientEnd As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemBorder As Color
        Get
            Return _colors("Border")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuBorder As Color
        Get
            Return _colors("Border")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemSelected As Color
        Get
            Return _colors("AltBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemSelectedGradientBegin As Color
        Get
            Return _colors("AltBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemSelectedGradientEnd As Color
        Get
            Return _colors("AltBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemPressedGradientBegin As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemPressedGradientMiddle As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property MenuItemPressedGradientEnd As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonSelectedHighlight As Color
        Get
            Return _colors("AltBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonSelectedGradientBegin As Color
        Get
            Return _colors("AltBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonSelectedGradientEnd As Color
        Get
            Return _colors("AltBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonPressedHighlight As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonPressedGradientBegin As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonPressedGradientEnd As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ButtonCheckedHighlight As Color
        Get
            Return _colors("Selection")
        End Get
    End Property

    Public Overrides ReadOnly Property CheckBackground As Color
        Get
            Return _colors("Selection")
        End Get
    End Property

    Public Overrides ReadOnly Property CheckSelectedBackground As Color
        Get
            Return _colors("Selection")
        End Get
    End Property

    Public Overrides ReadOnly Property CheckPressedBackground As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property SeparatorDark As Color
        Get
            Return _colors("Border")
        End Get
    End Property

    Public Overrides ReadOnly Property SeparatorLight As Color
        Get
            Return _colors("Border")
        End Get
    End Property

    Public Overrides ReadOnly Property StatusStripGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property StatusStripGradientEnd As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginRevealedGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginRevealedGradientMiddle As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property ImageMarginRevealedGradientEnd As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property RaftingContainerGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property RaftingContainerGradientEnd As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property OverflowButtonGradientBegin As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property OverflowButtonGradientMiddle As Color
        Get
            Return _colors("ToolBack")
        End Get
    End Property

    Public Overrides ReadOnly Property OverflowButtonGradientEnd As Color
        Get
            Return _colors("ControlBack")
        End Get
    End Property

    Public Overrides ReadOnly Property GripDark As Color
        Get
            Return _colors("Border")
        End Get
    End Property

    Public Overrides ReadOnly Property GripLight As Color
        Get
            Return _colors("Border")
        End Get
    End Property

End Class
