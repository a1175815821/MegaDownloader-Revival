Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' These retain the native button/textbox behaviours (keyboard, accessibility,
' IME, mnemonics and DialogResult). Only their presentation is custom drawn.
Friend Class CommandButton
    Inherits Button

    Private hovered As Boolean
    Private pressed As Boolean

    Public Sub New()
        FlatStyle = FlatStyle.Flat
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
    End Sub

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim result As Size = MyBase.GetPreferredSize(proposedSize)
        Dim textWidth As Integer = If(Text.Length = 0, 0, TextRenderer.MeasureText(Text, Font).Width)
        Dim imageWidth As Integer = If(Image Is Nothing, 0, Image.Width)
        Dim gap As Integer = If(textWidth > 0 AndAlso imageWidth > 0, UiStyles.Px(Me, 7), 0)
        result.Width = Math.Max(result.Width, textWidth + imageWidth + gap + Math.Max(Padding.Horizontal, UiStyles.Px(Me, 20)))
        Return result
    End Function

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        hovered = True
        MyBase.OnMouseEnter(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        hovered = False
        MyBase.OnMouseLeave(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        pressed = e.Button = MouseButtons.Left
        MyBase.OnMouseDown(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        pressed = False
        MyBase.OnMouseUp(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseCaptureChanged(e As EventArgs)
        If Not Capture Then pressed = False
        MyBase.OnMouseCaptureChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Space Then pressed = True
        MyBase.OnKeyDown(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
        pressed = False
        MyBase.OnKeyUp(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        pressed = False
        MyBase.OnLostFocus(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Width < 2 OrElse Height < 2 Then Return
        Dim role = UiStyles.GetRole(Me)
        Dim primary As Boolean = role = UiStyles.Role.PrimaryButton
        Dim quiet As Boolean = role = UiStyles.Role.QuietButton
        Dim parentBack As Color = If(Parent Is Nothing, ThemeManager.GetColor("Back"), Parent.BackColor)
        e.Graphics.Clear(parentBack)
        Dim fill As Color = BackColor
        Dim ink As Color = ForeColor
        If Not Enabled Then
            fill = If(quiet, parentBack, ThemeManager.GetColor("ButtonDisabledBack"))
            ink = ThemeManager.GetColor("ButtonDisabledFore")
        ElseIf pressed AndAlso (hovered OrElse Focused) Then
            fill = ThemeManager.GetColor(If(primary, "AccentPressed", "ButtonPressed"))
        ElseIf hovered Then
            fill = ThemeManager.GetColor(If(primary, "AccentHover", "ButtonHover"))
        End If
        Dim bounds As New Rectangle(0, 0, Width - 1, Height - 1)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using shape As GraphicsPath = Rounded(bounds, UiStyles.Px(Me, 6))
            Using brush As New SolidBrush(fill)
                e.Graphics.FillPath(brush, shape)
            End Using
            If Not quiet AndAlso Not primary Then
                Using pen As New Pen(ThemeManager.GetColor(If(Enabled, "ButtonBorder", "ButtonDisabledBorder")))
                    e.Graphics.DrawPath(pen, shape)
                End Using
            End If
        End Using
        Dim content As Rectangle = Rectangle.Inflate(ClientRectangle, -UiStyles.Px(Me, 10), -2)
        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
        If Not UseMnemonic Then flags = flags Or TextFormatFlags.NoPrefix
        Dim imageWidth As Integer = If(Image Is Nothing, 0, Image.Width)
        Dim gap As Integer = If(imageWidth > 0 AndAlso Text.Length > 0, UiStyles.Px(Me, 7), 0)
        Dim textWidth As Integer = If(Text.Length = 0, 0, TextRenderer.MeasureText(Text, Font).Width)
        Dim groupWidth As Integer = Math.Min(content.Width, imageWidth + gap + textWidth)
        Dim left As Integer = If(TextAlign = ContentAlignment.MiddleLeft, content.Left, content.Left + (content.Width - groupWidth) \ 2)
        If RightToLeft = RightToLeft.Yes Then flags = flags Or TextFormatFlags.RightToLeft
        If Image IsNot Nothing Then
            Dim top As Integer = (Height - Image.Height) \ 2
            If Enabled Then
                e.Graphics.DrawImage(Image, left, top, Image.Width, Image.Height)
            Else
                ControlPaint.DrawImageDisabled(e.Graphics, Image, left, top, fill)
            End If
            left += imageWidth + gap
        End If
        If Text.Length > 0 Then
            TextRenderer.DrawText(e.Graphics, Text, Font, New Rectangle(left, content.Top, Math.Max(0, content.Right - left), content.Height), ink, flags)
        End If
        If Focused AndAlso ShowFocusCues Then
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ink, fill)
        End If
    End Sub

    Friend Shared Function Rounded(bounds As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim diameter As Integer = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)))
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90)
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90)
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90)
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90)
        path.CloseFigure()
        Return path
    End Function
End Class

Friend Class NavigationTabs
    Inherits TabControl

    Public Sub New()
        DrawMode = TabDrawMode.OwnerDrawFixed
        ItemSize = New Size(0, 38)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.Clear(ThemeManager.GetColor("Back"))
        For index As Integer = 0 To TabPages.Count - 1
            Dim state As DrawItemState = If(index = SelectedIndex, DrawItemState.Selected, DrawItemState.None)
            OnDrawItem(New DrawItemEventArgs(e.Graphics, Font, GetTabRect(index), index, state))
        Next
        Using pen As New Pen(ThemeManager.GetColor("Border"))
            e.Graphics.DrawLine(pen, 0, DisplayRectangle.Top - 1, Width, DisplayRectangle.Top - 1)
        End Using
    End Sub

    Protected Overrides Sub OnDrawItem(e As DrawItemEventArgs)
        If e.Index < 0 Then Return
        Dim selected As Boolean = e.Index = SelectedIndex
        Using brush As New SolidBrush(ThemeManager.GetColor("Back"))
            e.Graphics.FillRectangle(brush, e.Bounds)
        End Using
        TextRenderer.DrawText(e.Graphics, TabPages(e.Index).Text, Font, e.Bounds,
                              ThemeManager.GetColor(If(selected, "SelectionFore", "Muted")),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        If selected Then
            Using brush As New SolidBrush(ThemeManager.GetColor("Accent"))
                e.Graphics.FillRectangle(brush, e.Bounds.Left + 12, e.Bounds.Bottom - 3, Math.Max(1, e.Bounds.Width - 24), 3)
            End Using
        End If
        If Focused AndAlso ShowFocusCues AndAlso selected Then e.DrawFocusRectangle()
    End Sub
End Class

Friend Class SlimProgressBar
    Inherits ProgressBar

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.Clear(ThemeManager.GetColor("ProgressBack"))
        Dim fraction As Double = (Value - Minimum) / CDbl(Math.Max(1, Maximum - Minimum))
        Using brush As New SolidBrush(ThemeManager.GetColor("ProgressFill"))
            e.Graphics.FillRectangle(brush, 0, 0, CInt(ClientSize.Width * fraction), ClientSize.Height)
        End Using
    End Sub
End Class

Friend Class SectionGroupBox
    Inherits GroupBox

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.Clear(BackColor)
        Dim inset As Integer = UiStyles.Px(Me, 12)
        Dim title As New Rectangle(inset, 0, Math.Max(0, Width - inset * 2), Font.Height + 8)
        TextRenderer.DrawText(e.Graphics, Text, Font, title, ForeColor,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        Using pen As New Pen(ThemeManager.GetColor("Border"))
            e.Graphics.DrawLine(pen, inset, title.Bottom, Math.Max(inset, Width - inset), title.Bottom)
        End Using
    End Sub
End Class

Friend Class SearchFrame
    Inherits Panel

    Private ReadOnly input As TextBox
    Private ReadOnly clearButton As Button

    Friend Sub New(textBox As TextBox, clear As Button)
        DoubleBuffered = True
        input = textBox
        clearButton = clear
        input.BorderStyle = BorderStyle.None
        Controls.AddRange(New Control() {input, clearButton})
        AddHandler input.GotFocus, Sub() Invalidate()
        AddHandler input.LostFocus, Sub() Invalidate()
        AddHandler input.TextChanged, Sub()
                                          clearButton.Visible = input.TextLength > 0
                                          PerformLayout()
                                      End Sub
        UiStyles.SetRole(Me, UiStyles.Role.InputSurface)
        clearButton.Visible = False
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        If input Is Nothing Then Return
        Dim inset As Integer = UiStyles.Px(Me, 10)
        Dim side As Integer = Math.Max(22, Height - 8)
        clearButton.SetBounds(Width - side - 4, 4, side, side)
        Dim right As Integer = If(clearButton.Visible, clearButton.Left, Width - inset)
        input.SetBounds(UiStyles.Px(Me, 32), Math.Max(2, (Height - input.PreferredHeight) \ 2),
                        Math.Max(8, right - UiStyles.Px(Me, 32)), input.PreferredHeight)
        input.BackColor = BackColor
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using path = CommandButton.Rounded(New Rectangle(0, 0, Width - 1, Height - 1), UiStyles.Px(Me, 6))
            Using pen As New Pen(ThemeManager.GetColor(If(input.Focused, "Link", "Border")))
                e.Graphics.DrawPath(pen, path)
            End Using
        End Using
        Dim side As Integer = UiStyles.Px(Me, 11)
        Dim x As Integer = UiStyles.Px(Me, 11)
        Dim y As Integer = (Height - side) \ 2 - 1
        Using pen As New Pen(ThemeManager.GetColor("Muted"), Math.Max(1.0F, DeviceDpi / 96.0F))
            e.Graphics.DrawEllipse(pen, x, y, side, side)
            e.Graphics.DrawLine(pen, x + side - 1, y + side - 1, x + side + 4, y + side + 4)
        End Using
    End Sub
End Class
