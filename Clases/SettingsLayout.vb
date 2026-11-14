Imports System.Drawing
Imports System.Windows.Forms

' Settings use a width-constrained, top-down layout. Stacking AutoSize
' GroupBoxes and percentage TableLayoutPanels repeatedly measured the entire
' descendant tree during each binding/text change and again on first show.
Friend Class SettingsStackPanel
    Inherits Panel

    Private layingOut As Boolean

    Public Sub New()
        DoubleBuffered = True
    End Sub

    Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
        MyBase.OnControlAdded(e)
        AddHandler e.Control.SizeChanged, AddressOf ChildLayoutChanged
        AddHandler e.Control.TextChanged, AddressOf ChildLayoutChanged
        AddHandler e.Control.FontChanged, AddressOf ChildLayoutChanged
    End Sub

    Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
        RemoveHandler e.Control.SizeChanged, AddressOf ChildLayoutChanged
        RemoveHandler e.Control.TextChanged, AddressOf ChildLayoutChanged
        RemoveHandler e.Control.FontChanged, AddressOf ChildLayoutChanged
        MyBase.OnControlRemoved(e)
    End Sub

    Private Sub ChildLayoutChanged(sender As Object, e As EventArgs)
        If Not layingOut Then PerformLayout()
    End Sub

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim width As Integer = Math.Max(1, If(proposedSize.Width > 0, proposedSize.Width, Me.Width))
        Dim height As Integer = Padding.Vertical
        For Each child As Control In Controls
            Dim available As Integer = Math.Max(1, width - Padding.Horizontal - child.Margin.Horizontal)
            height += child.GetPreferredSize(New Size(available, 0)).Height + child.Margin.Vertical
        Next
        Return New Size(width, height)
    End Function

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        If layingOut Then Return
        layingOut = True
        Try
            Dim top As Integer = Padding.Top
            For Each child As Control In Controls
                Dim available As Integer = Math.Max(1, ClientSize.Width - Padding.Horizontal - child.Margin.Horizontal)
                If TypeOf child Is Label AndAlso child.MaximumSize.Width <> available Then child.MaximumSize = New Size(available, 0)
                Dim wanted As Size = child.GetPreferredSize(New Size(available, 0))
                top += child.Margin.Top
                child.SetBounds(Padding.Left + child.Margin.Left, top, available, wanted.Height)
                top += wanted.Height + child.Margin.Bottom
            Next
            Dim wantedHeight As Integer = top + Padding.Bottom
            If Height <> wantedHeight Then Height = wantedHeight
        Finally
            layingOut = False
        End Try
    End Sub
End Class

Friend Class SettingsSection
    Inherits SectionGroupBox

    Friend Sub SetBody(body As SettingsStackPanel)
        Controls.Add(body)
        AddHandler body.SizeChanged, Sub() PerformLayout()
    End Sub

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim width As Integer = Math.Max(1, If(proposedSize.Width > 0, proposedSize.Width, Me.Width))
        Dim height As Integer = Font.Height + Padding.Vertical
        If Controls.Count > 0 Then height += Controls(0).GetPreferredSize(New Size(Math.Max(1, width - Padding.Horizontal), 0)).Height
        Return New Size(width, height)
    End Function

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        If Controls.Count = 0 Then Return
        Dim body = Controls(0)
        Dim available As Integer = Math.Max(1, ClientSize.Width - Padding.Horizontal)
        Dim height As Integer = body.GetPreferredSize(New Size(available, 0)).Height
        body.SetBounds(Padding.Left, Font.Height + Padding.Top, available, height)
        Dim wanted As Integer = Font.Height + Padding.Vertical + height
        If Me.Height <> wanted Then Me.Height = wanted
    End Sub
End Class

Friend Class SettingsFieldRow
    Inherits Panel

    Private ReadOnly caption As Label
    Private ReadOnly input As Control
    Private ReadOnly trailing As Control()

    Friend Sub New(label As Label, field As Control, extras As Control())
        caption = label
        input = field
        trailing = extras
        Controls.Add(caption)
        Controls.Add(input)
        Controls.AddRange(trailing)
        caption.AutoSize = False
        caption.TextAlign = ContentAlignment.MiddleLeft
        caption.MinimumSize = Size.Empty
        caption.MaximumSize = Size.Empty
        AddHandler caption.TextChanged, AddressOf PreferredSizeChanged
        AddHandler caption.FontChanged, AddressOf PreferredSizeChanged
        AddHandler input.FontChanged, AddressOf PreferredSizeChanged
        ' 翻译在 Load 时才写入控件 Text；末尾按钮的宽度必须跟随文本+字体重测，
        ' 否则长文案（如各语言 Browse）会被裁或挤压 txtRuta。
        AddHandler input.TextChanged, AddressOf PreferredSizeChanged
        input.Dock = DockStyle.None
        input.MinimumSize = Size.Empty
        For Each control As Control In trailing
            control.Dock = DockStyle.None
            control.AutoSize = True
            AddHandler control.TextChanged, AddressOf PreferredSizeChanged
            AddHandler control.FontChanged, AddressOf PreferredSizeChanged
        Next
    End Sub

    Private Sub PreferredSizeChanged(sender As Object, e As EventArgs)
        If Parent IsNot Nothing Then Parent.PerformLayout()
    End Sub

    Private Function LabelWidth(width As Integer) As Integer
        Return Math.Max(1, CInt(width * 0.34))
    End Function

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Dim width As Integer = Math.Max(1, If(proposedSize.Width > 0, proposedSize.Width, Me.Width))
        Dim labelSize = TextRenderer.MeasureText(caption.Text, caption.Font, New Size(LabelWidth(width), 0), TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix)
        Dim height As Integer = Math.Max(labelSize.Height, input.GetPreferredSize(Size.Empty).Height)
        For Each control As Control In trailing
            height = Math.Max(height, control.GetPreferredSize(Size.Empty).Height)
        Next
        Return New Size(width, Math.Max(UiStyles.Px(Me, 34), height + UiStyles.Px(Me, 8)))
    End Function

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        If input Is Nothing Then Return
        Dim gap As Integer = UiStyles.Px(Me, 12)
        Dim left As Integer = LabelWidth(ClientSize.Width) + gap
        Dim right As Integer = ClientSize.Width
        For index As Integer = trailing.Length - 1 To 0 Step -1
            Dim control = trailing(index)
            Dim wanted = control.GetPreferredSize(Size.Empty)
            wanted.Width = Math.Max(wanted.Width, UiStyles.Px(Me, 92))
            wanted.Height = Math.Max(wanted.Height, UiStyles.Px(Me, 28))
            right -= wanted.Width
            control.SetBounds(right, Math.Max(0, (Height - wanted.Height) \ 2), wanted.Width, wanted.Height)
            right -= gap
        Next
        caption.SetBounds(0, 0, Math.Max(1, left - gap), Height)
        Dim inputHeight As Integer = input.GetPreferredSize(Size.Empty).Height
        inputHeight = Math.Max(inputHeight, UiStyles.Px(Me, 28))
        input.SetBounds(left, Math.Max(0, (Height - inputHeight) \ 2), Math.Max(1, right - left), inputHeight)
    End Sub
End Class

' 设置分组内逻辑子段之间的细分割线。SectionGroupBox 只画标题下横线,子段之间原本
' 直接堆叠留白、显得空而丑;用与标题横线同色(Border)的细线把子段分开,留白变得有结构。
Friend Class SettingsDivider
    Inherits Panel

    Public Sub New()
        DoubleBuffered = True
        Margin = New Padding(0, 10, 0, 10)
        Height = 1
    End Sub

    Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
        Return New Size(Math.Max(1, If(proposedSize.Width > 0, proposedSize.Width, Width)), 1)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.Clear(BackColor)
        Using pen As New Pen(ThemeManager.GetColor("Border"))
            Dim y As Integer = Height \ 2
            e.Graphics.DrawLine(pen, 0, y, Math.Max(0, Width - 1), y)
        End Using
    End Sub
End Class
