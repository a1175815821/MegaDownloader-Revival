Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' 简易主题管理器:Auto 模式从注册表读取系统深/浅色偏好,Light/Dark 直接应用预设配色。
''' 仅修改背景色和前景色,避免与控件的 ForeColor/BackColor 默认值冲突;不改变控件尺寸或布局。
''' </summary>
Public NotInheritable Class ThemeManager

    Private Sub New()
    End Sub

    ' 当前进程实际生效的主题(Light 或 Dark,不含 Auto)
    Public Enum ResolvedTheme
        Light
        Dark
    End Enum

    Private Shared _current As ResolvedTheme = ResolvedTheme.Light

    ' 浅色配色
    Private Shared ReadOnly LightColors As New Dictionary(Of String, Color) From
    {
        {"Back", Color.White},
        {"Fore", Color.Black},
        {"AltBack", Color.FromArgb(245, 245, 245)},
        {"ControlBack", Color.FromArgb(240, 240, 240)},
        {"Border", Color.FromArgb(204, 206, 209)}
    }

    ' 深色配色(VS Code Dark+ 风格)
    Private Shared ReadOnly DarkColors As New Dictionary(Of String, Color) From
    {
        {"Back", Color.FromArgb(30, 30, 30)},
        {"Fore", Color.FromArgb(241, 241, 241)},
        {"AltBack", Color.FromArgb(45, 45, 48)},
        {"ControlBack", Color.FromArgb(51, 51, 55)},
        {"Border", Color.FromArgb(90, 90, 90)}
    }

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
            ' 任何读取失败都安全降级为浅色
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 根据 ConfiguracionUI.ThemeMode 解析为实际主题(Light 或 Dark)。
    ''' </summary>
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
    ''' 应用主题到指定窗体(递归所有子控件)。
    ''' 应在窗体 Load/Shown 后调用,确保所有控件已创建。
    ''' </summary>
    Public Shared Sub ApplyTheme(form As Form, mode As ConfiguracionUI.ThemeModeType)
        Dim resolved As ResolvedTheme = Resolve(mode)
        _current = resolved
        ApplyThemeRecursive(form, resolved)
    End Sub

    Private Shared Sub ApplyThemeRecursive(control As Control, theme As ResolvedTheme)
        Dim colors As Dictionary(Of String, Color) = If(theme = ResolvedTheme.Dark, DarkColors, LightColors)

        ' 对支持 BackColor/ForeColor 的控件统一设置
        ' ListBox/ListView/DataGridView 等 ItemsControl 默认背景在 SystemColor,需要显式覆盖
        If TypeOf control Is Form Then
            CType(control, Form).BackColor = colors("Back")
            CType(control, Form).ForeColor = colors("Fore")
        ElseIf TypeOf control Is GroupBox OrElse TypeOf control Is Panel Then
            control.BackColor = colors("ControlBack")
            control.ForeColor = colors("Fore")
        ElseIf TypeOf control Is Button Then
            control.BackColor = colors("ControlBack")
            control.ForeColor = colors("Fore")
            CType(control, Button).FlatStyle = FlatStyle.Standard
        ElseIf TypeOf control Is Label OrElse TypeOf control Is LinkLabel Then
            control.BackColor = Color.Transparent
            control.ForeColor = colors("Fore")
        ElseIf TypeOf control Is TextBox OrElse TypeOf control Is ComboBox OrElse
               TypeOf control Is NumericUpDown OrElse TypeOf control Is MaskedTextBox Then
            control.BackColor = colors("Back")
            control.ForeColor = colors("Fore")
        ElseIf TypeOf control Is CheckBox OrElse TypeOf control Is RadioButton Then
            control.BackColor = Color.Transparent
            control.ForeColor = colors("Fore")
        ElseIf TypeOf control Is TabPage Then
            control.BackColor = colors("Back")
            control.ForeColor = colors("Fore")
        ElseIf TypeOf control Is DataGridView Then
            Dim dgv As DataGridView = CType(control, DataGridView)
            dgv.BackgroundColor = colors("AltBack")
            dgv.DefaultCellStyle.BackColor = colors("Back")
            dgv.DefaultCellStyle.ForeColor = colors("Fore")
            dgv.ColumnHeadersDefaultCellStyle.BackColor = colors("ControlBack")
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = colors("Fore")
            dgv.GridColor = colors("Border")
        Else
            ' 其他类型按容器背景处理
            If control.HasChildren Then
                control.BackColor = colors("Back")
                control.ForeColor = colors("Fore")
            End If
        End If

        ' 递归子控件
        For Each child As Control In control.Controls
            ApplyThemeRecursive(child, theme)
        Next
    End Sub

    Public Shared ReadOnly Property Current As ResolvedTheme
        Get
            Return _current
        End Get
    End Property

End Class
