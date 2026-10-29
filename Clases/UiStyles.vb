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
        Muted
        PrimaryButton
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
            Case Role.BodyText
                control.ForeColor = ThemeManager.GetColor("Fore")
            Case Role.ErrorText
                control.ForeColor = ThemeManager.GetColor("ErrorFore")
            Case Role.Muted
                control.ForeColor = ThemeManager.GetColor("Muted")
            Case Role.PrimaryButton, Role.QuietButton
                Dim button As Button = TryCast(control, Button)
                If button Is Nothing Then Return
                Dim primary As Boolean = registration.Value = Role.PrimaryButton
                button.UseVisualStyleBackColor = False
                button.FlatStyle = FlatStyle.Flat
                button.BackColor = ThemeManager.GetColor(If(primary, "Accent", "ButtonBack"))
                button.ForeColor = ThemeManager.GetColor(If(primary, "AccentFore", "Fore"))
                button.FlatAppearance.BorderSize = If(primary, 0, 1)
                button.FlatAppearance.BorderColor = ThemeManager.GetColor("ButtonBorder")
                button.FlatAppearance.MouseOverBackColor = ThemeManager.GetColor(If(primary, "AccentHover", "ButtonHover"))
                button.FlatAppearance.MouseDownBackColor = ThemeManager.GetColor(If(primary, "AccentPressed", "ButtonPressed"))
        End Select
    End Sub

    ' Each font has a single control owner and is disposed with that control.
    Friend Shared Sub SetFont(control As Control, extraPoints As Single, Optional bold As Boolean = False, Optional baseFont As Font = Nothing)
        Dim basis As Font = If(baseFont, control.Font)
        Dim owned As New Font(basis.FontFamily, basis.SizeInPoints + extraPoints,
                              If(bold, FontStyle.Bold, FontStyle.Regular), GraphicsUnit.Point)
        control.Font = owned
        AddHandler control.Disposed, Sub() owned.Dispose()
    End Sub

    Friend Shared Sub SetAppFont(control As Control)
        Dim owned As New Font("Segoe UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
        control.Font = owned
        AddHandler control.Disposed, Sub() owned.Dispose()
    End Sub

    Friend Shared Sub SetText(control As Control, value As String)
        If control.Text <> value Then control.Text = value
    End Sub
End Class
