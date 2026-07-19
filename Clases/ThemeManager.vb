Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' 主题管理器:Auto 模式从注册表读取系统深/浅色偏好,Light/Dark 直接应用预设配色。
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

    ' 浅色配色
    Private Shared ReadOnly LightColors As New Dictionary(Of String, Color) From
    {
        {"Back", Color.White},
        {"Fore", Color.Black},
        {"AltBack", Color.FromArgb(245, 245, 245)},
        {"ControlBack", Color.FromArgb(240, 240, 240)},
        {"Border", Color.FromArgb(204, 206, 209)},
        {"Selection", Color.FromArgb(0, 120, 215)},
        {"SelectionFore", Color.White},
        {"Link", Color.FromArgb(0, 102, 204)},
        {"ToolBack", Color.FromArgb(240, 240, 240)},
        {"ToolBorder", Color.FromArgb(204, 206, 209)},
        {"ErrorFore", Color.FromArgb(192, 0, 0)},
        {"SuccessFore", Color.FromArgb(0, 128, 0)},
        {"ProgressBack", Color.Azure},
        {"ProgressFill", Color.MediumTurquoise},
        {"ProgressGradientStart", Color.SpringGreen},
        {"ProgressGradientEnd", Color.MediumTurquoise},
        {"ButtonHover", Color.FromArgb(229, 241, 251)},
        {"ButtonPressed", Color.FromArgb(204, 228, 247)}
    }

    ' 深色配色(VS Code Dark+ 风格)
    Private Shared ReadOnly DarkColors As New Dictionary(Of String, Color) From
    {
        {"Back", Color.FromArgb(30, 30, 30)},
        {"Fore", Color.FromArgb(241, 241, 241)},
        {"AltBack", Color.FromArgb(45, 45, 48)},
        {"ControlBack", Color.FromArgb(51, 51, 55)},
        {"Border", Color.FromArgb(90, 90, 90)},
        {"Selection", Color.FromArgb(38, 79, 120)},
        {"SelectionFore", Color.FromArgb(241, 241, 241)},
        {"Link", Color.FromArgb(86, 156, 214)},
        {"ToolBack", Color.FromArgb(45, 45, 48)},
        {"ToolBorder", Color.FromArgb(90, 90, 90)},
        {"ErrorFore", Color.FromArgb(244, 135, 113)},
        {"SuccessFore", Color.FromArgb(78, 201, 176)},
        {"ProgressBack", Color.FromArgb(45, 45, 48)},
        {"ProgressFill", Color.FromArgb(38, 79, 120)},
        {"ProgressGradientStart", Color.FromArgb(14, 99, 156)},
        {"ProgressGradientEnd", Color.FromArgb(38, 79, 120)},
        {"ButtonHover", Color.FromArgb(62, 62, 66)},
        {"ButtonPressed", Color.FromArgb(38, 79, 120)}
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

        ' ContextMenuStrip 不在 Controls 树中,需从 components / 字段显式处理
        ApplyThemeToFormContextMenus(form, colors)
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
            GoTo RecurseChildren
        End If
        If TypeOf control Is Label Then
            control.BackColor = Color.Transparent
            control.ForeColor = colors("Fore")
            GoTo RecurseChildren
        End If

        ' 文本类控件
        If TypeOf control Is TextBox OrElse TypeOf control Is RichTextBox OrElse
           TypeOf control Is ComboBox OrElse TypeOf control Is NumericUpDown OrElse
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
        For Each child As Control In control.Controls
            ApplyThemeRecursive(child, colors, theme)
        Next
    End Sub

    Private Shared Sub ApplyThemeToObjectListView(olv As BrightIdeasSoftware.ObjectListView,
                                                  colors As Dictionary(Of String, Color),
                                                  theme As ResolvedTheme)
        ' 关键:关闭 Explorer 主题,否则自定义颜色不生效
        Try
            olv.UseExplorerTheme = False
        Catch ex As Exception
        End Try
        Try
            olv.UseTranslucentSelection = False
        Catch ex As Exception
        End Try

        olv.BackColor = colors("Back")
        olv.ForeColor = colors("Fore")
        Try
            olv.AlternateRowBackColor = colors("AltBack")
        Catch ex As Exception
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
        End Try

        ' TreeColumnRenderer (树列的展开/折叠图标所在列) - 仅 TreeListView 有此属性
        Try
            Dim renderer As Object = olv.GetType().GetProperty("TreeColumnRenderer").GetValue(olv, Nothing)
            If renderer IsNot Nothing Then
                TrySetProperty(renderer, "BranchColor", colors("Border"))
                TrySetProperty(renderer, "LineColor", colors("Border"))
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Shared Sub TrySetProperty(target As Object, propertyName As String, value As Object)
        Try
            Dim prop As System.Reflection.PropertyInfo = target.GetType().GetProperty(propertyName)
            If prop IsNot Nothing AndAlso prop.CanWrite Then
                prop.SetValue(target, value, Nothing)
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Shared Sub ApplyThemeToButton(btn As Button, colors As Dictionary(Of String, Color))
        btn.UseVisualStyleBackColor = False
        btn.FlatStyle = FlatStyle.Flat
        btn.BackColor = colors("ControlBack")
        btn.ForeColor = colors("Fore")
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.BorderColor = colors("Border")
        btn.FlatAppearance.MouseOverBackColor = colors("ButtonHover")
        btn.FlatAppearance.MouseDownBackColor = colors("ButtonPressed")
        ' 有图标的工具栏按钮禁用时保持边框可见
        btn.FlatAppearance.CheckedBackColor = colors("Selection")
    End Sub

    Private Shared Sub ApplyThemeToToolStripItem(item As ToolStripItem, colors As Dictionary(Of String, Color))
        item.BackColor = colors("ToolBack")
        item.ForeColor = colors("Fore")

        If TypeOf item Is ToolStripMenuItem Then
            Dim mi As ToolStripMenuItem = CType(item, ToolStripMenuItem)
            For Each child As ToolStripItem In mi.DropDownItems
                ApplyThemeToToolStripItem(child, colors)
            Next
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
