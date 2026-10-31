Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms

' Semantic roles survive theme changes without occupying application control metadata.
Friend NotInheritable Class UiStyles
    ' Link input accepts text only, so clipboard fonts/colors cannot override the theme.
    Friend NotInheritable Class PlainTextRichTextBox
        Inherits RichTextBox

        Private Const WM_PASTE As Integer = &H302

        Friend Sub New()
            DetectUrls = False
        End Sub

        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If ShortcutsEnabled AndAlso (keyData = (Keys.Control Or Keys.V) OrElse
                                         keyData = (Keys.Control Or Keys.Shift Or Keys.V) OrElse
                                         keyData = (Keys.Shift Or Keys.Insert)) Then
                PastePlainText()
                Return True
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WM_PASTE Then
                PastePlainText()
                m.Result = IntPtr.Zero
                Return
            End If
            MyBase.WndProc(m)
        End Sub

        Private Sub PastePlainText()
            If Me.ReadOnly OrElse Not Enabled Then Return
            Dim textFormat As DataFormats.Format = DataFormats.GetFormat(DataFormats.UnicodeText)
            If CanPaste(textFormat) Then Paste(textFormat)
        End Sub
    End Class

    Friend NotInheritable Class BufferedGrid
        Inherits DataGridView

        Friend Sub New()
            DoubleBuffered = Not SystemInformation.TerminalServerSession
        End Sub
    End Class

    Friend Enum Role
        Surface
        Canvas
        Sidebar
        InputSurface
        Muted
        PrimaryButton
        SecondaryButton
        QuietButton
        BodyText
        ErrorText
    End Enum

    Private NotInheritable Class Registration
        Friend Value As Role
    End Class

    Private Shared ReadOnly roles As New ConditionalWeakTable(Of Control, Registration)()

    Private Sub New()
    End Sub

    Friend Shared Sub SetRole(control As Control, value As Role)
        roles.GetValue(control, Function(key) New Registration()).Value = value
        Apply(control)
    End Sub

    Friend Shared Sub Apply(control As Control)
        Dim registration As Registration = Nothing
        If Not roles.TryGetValue(control, registration) Then Return
        Select Case registration.Value
            Case Role.Surface
                control.BackColor = ThemeManager.GetColor("Back")
                control.ForeColor = ThemeManager.GetColor("Fore")
            Case Role.Canvas
                control.BackColor = ThemeManager.GetColor("Canvas")
                control.ForeColor = ThemeManager.GetColor("Fore")
            Case Role.Sidebar
                control.BackColor = ThemeManager.GetColor("ControlBack")
                control.ForeColor = ThemeManager.GetColor("Fore")
            Case Role.InputSurface
                control.BackColor = ThemeManager.GetColor("InputBack")
                control.ForeColor = ThemeManager.GetColor("Fore")
            Case Role.BodyText
                control.ForeColor = ThemeManager.GetColor("Fore")
            Case Role.ErrorText
                control.ForeColor = ThemeManager.GetColor("ErrorFore")
            Case Role.Muted
                control.ForeColor = ThemeManager.GetColor("Muted")
            Case Role.PrimaryButton, Role.SecondaryButton, Role.QuietButton
                Dim button As Button = TryCast(control, Button)
                If button Is Nothing Then Return
                Dim primary As Boolean = registration.Value = Role.PrimaryButton
                Dim quiet As Boolean = registration.Value = Role.QuietButton
                button.UseVisualStyleBackColor = False
                button.FlatStyle = FlatStyle.Flat
                button.BackColor = If(quiet AndAlso button.Parent IsNot Nothing, button.Parent.BackColor,
                                      ThemeManager.GetColor(If(primary, "Accent", "ButtonBack")))
                button.ForeColor = ThemeManager.GetColor(If(primary, "AccentFore", "Fore"))
                button.FlatAppearance.BorderSize = If(primary OrElse quiet, 0, 1)
                button.FlatAppearance.BorderColor = ThemeManager.GetColor("ButtonBorder")
                button.FlatAppearance.MouseOverBackColor = ThemeManager.GetColor(If(primary, "AccentHover", "ButtonHover"))
                button.FlatAppearance.MouseDownBackColor = ThemeManager.GetColor(If(primary, "AccentPressed", "ButtonPressed"))
                button.Invalidate()
        End Select
    End Sub

    Friend Shared Function GetRole(control As Control, Optional fallback As Role = Role.SecondaryButton) As Role
        Dim registration As Registration = Nothing
        Return If(roles.TryGetValue(control, registration), registration.Value, fallback)
    End Function

    Friend Shared Function Px(control As Control, value As Integer) As Integer
        Return CInt(Math.Round(value * Math.Max(96, control.DeviceDpi) / 96.0R))
    End Function

    <Runtime.InteropServices.DllImport("user32.dll", CharSet:=Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function SendMessage(window As IntPtr, message As Integer, wParam As IntPtr, text As String) As IntPtr
    End Function

    Friend Shared Sub SetSearchCue(input As TextBox)
        If input.IsHandleCreated Then SendMessage(input.Handle, &H1501, IntPtr.Zero, input.AccessibleName)
    End Sub

    Friend Shared Function SuspendLayouts(root As Control) As List(Of Control)
        Dim controls As New List(Of Control)()
        SuspendLayoutsRecursive(root, controls)
        Return controls
    End Function

    Private Shared Sub SuspendLayoutsRecursive(control As Control, controls As List(Of Control))
        control.SuspendLayout()
        controls.Add(control)
        For Each child As Control In control.Controls
            SuspendLayoutsRecursive(child, controls)
        Next
    End Sub

    Friend Shared Sub ResumeLayouts(controls As List(Of Control))
        For index As Integer = controls.Count - 1 To 0 Step -1
            controls(index).ResumeLayout(False)
        Next
        For index As Integer = controls.Count - 1 To 0 Step -1
            controls(index).PerformLayout()
        Next
    End Sub

    ' Keep the designer geometry in logical pixels. Font selection is completed
    ' before the first handle/layout, so font metrics and DPI never both scale it.
    Friend Shared Sub InitializeForm(form As Form)
        form.AutoScaleMode = AutoScaleMode.None
        SetAppFont(form)
        form.AutoScaleDimensions = New SizeF(96, 96)
        form.AutoScaleMode = AutoScaleMode.Dpi
    End Sub

    ' Each font has a single control owner and is disposed with that control.
    Friend Shared Sub SetFont(control As Control, extraPoints As Single, Optional bold As Boolean = False, Optional baseFont As Font = Nothing)
        Dim basis As Font = If(baseFont, control.Font)
        Dim owned As New Font(basis.FontFamily, basis.SizeInPoints + extraPoints,
                              If(bold, FontStyle.Bold, FontStyle.Regular), GraphicsUnit.Point)
        control.Font = owned
        AddHandler control.Disposed, Sub() owned.Dispose()
    End Sub

    Friend Shared Sub SetAppFont(control As Control, Optional points As Single = 10.0F)
        Dim code As String = Language.GetCurrentLanguageCode()
        If String.IsNullOrEmpty(code) Then code = Globalization.CultureInfo.CurrentUICulture.Name
        Dim candidates As String() = {"Segoe UI"}
        If code.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) OrElse code.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) Then
            candidates = {"Microsoft JhengHei UI", "Microsoft JhengHei", "Segoe UI"}
        ElseIf code.StartsWith("zh", StringComparison.OrdinalIgnoreCase) Then
            candidates = {"Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI"}
        ElseIf code.StartsWith("ja", StringComparison.OrdinalIgnoreCase) Then
            candidates = {"Yu Gothic UI", "Meiryo UI", "Segoe UI"}
        ElseIf code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) Then
            candidates = {"Malgun Gothic", "Segoe UI"}
        End If
        Dim family As String = SystemFonts.MessageBoxFont.FontFamily.Name
        For Each candidate As String In candidates
            Using probe As New Font(candidate, 10.0F)
                If String.Equals(probe.FontFamily.Name, candidate, StringComparison.OrdinalIgnoreCase) Then
                    family = candidate
                    Exit For
                End If
            End Using
        Next
        Dim owned As New Font(family, points, FontStyle.Regular, GraphicsUnit.Point)
        control.Font = owned
        AddHandler control.Disposed, Sub() owned.Dispose()
    End Sub

    Friend Shared Sub SetText(control As Control, value As String)
        If control.Text <> value Then control.Text = value
    End Sub
End Class
