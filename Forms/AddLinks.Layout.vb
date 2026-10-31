Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class AddLinks
    Private addLinksViewport As Panel
    Private addLinksContent As TableLayoutPanel
    Private addLinksLayoutReady As Boolean
    Private updatingAddLinksLayout As Boolean
    Private addLinksInputPage As TableLayoutPanel
    Private addLinksPageHost As Panel
    Private addLinksStepLabel As Label
    Private addLinksFeedback As Label
    Private addLinksCancel As Button

    Private Sub InitAddLinksLayout()
        ' Use the designer's Segoe UI 9 / 96-DPI baseline, before automatic scaling.
        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .Padding = New Padding(24),
            .ColumnCount = 1, .RowCount = 2, .TabIndex = 0}
        addLinksInputPage = layout
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        ' Keep confirmation outside the scroll area on short high-DPI screens.
        addLinksViewport = New Panel With {
            .Dock = DockStyle.Fill, .AutoScroll = True,
            .Margin = New Padding(0, 0, 0, 12), .TabIndex = 0}
        addLinksContent = New TableLayoutPanel With {
            .Dock = DockStyle.Top, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1, .RowCount = 5, .Margin = New Padding(0), .TabIndex = 0}
        addLinksContent.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        addLinksContent.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        addLinksContent.RowStyles.Add(New RowStyle(SizeType.Absolute, 160))
        addLinksContent.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        addLinksContent.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        addLinksContent.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Dim heading As New Label With {
            .Text = Language.GetText("AddLinks_InputHint"), .AutoSize = True, .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 0, 12)}
        UiStyles.SetRole(heading, UiStyles.Role.Muted)
        addLinksContent.Controls.Add(heading, 0, 0)
        txtLinks.Size = New Size(100, 160)
        txtLinks.Dock = DockStyle.Fill
        txtLinks.MinimumSize = New Size(0, 160)
        txtLinks.Margin = New Padding(0)
        txtLinks.BorderStyle = BorderStyle.FixedSingle
        txtLinks.TabIndex = 0
        addLinksContent.Controls.Add(txtLinks, 0, 1)

        Dim progress As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0, 4, 0, 10)}
        progress.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        progress.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        progress.RowStyles.Add(New RowStyle(SizeType.Absolute, 6))
        lblLinkCount.AutoSize = True
        lblLinkCount.MinimumSize = New Size(0, 36)
        lblLinkCount.Dock = DockStyle.Fill
        lblLinkCount.Margin = New Padding(0)
        preflightProgressBar.Dock = DockStyle.Fill
        preflightProgressBar.Margin = New Padding(0)
        progress.Controls.Add(lblLinkCount, 0, 0)
        progress.Controls.Add(preflightProgressBar, 0, 1)
        addLinksContent.Controls.Add(progress, 0, 2)

        OpcionesPaquete.Controls.Clear()
        OpcionesPaquete.Dock = DockStyle.Fill
        OpcionesPaquete.AutoSize = True
        OpcionesPaquete.AutoSizeMode = AutoSizeMode.GrowAndShrink
        OpcionesPaquete.Padding = New Padding(12, 16, 12, 12)
        OpcionesPaquete.Margin = New Padding(0, 0, 0, 12)
        OpcionesPaquete.TabIndex = 1
        Dim options As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 3, .RowCount = 4, .Margin = New Padding(0)}
        options.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        options.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        options.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        For row As Integer = 0 To 3
            options.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        For Each caption As Label In New Label() {Label2, Label1}
            caption.Anchor = AnchorStyles.Left
            caption.MinimumSize = Size.Empty
            caption.TextAlign = ContentAlignment.MiddleLeft
        Next
        ' Discard widths inherited from the old absolute-position layout.
        For Each input As TextBox In New TextBox() {txtNombre, txtRuta}
            input.Width = 100
            input.Dock = DockStyle.Fill
        Next
        options.Controls.Add(Label2, 0, 0)
        options.Controls.Add(txtNombre, 1, 0)
        options.SetColumnSpan(txtNombre, 2)
        options.Controls.Add(Label1, 0, 1)
        options.Controls.Add(txtRuta, 1, 1)
        btnExaminar.AutoSize = True
        btnExaminar.AutoSizeMode = AutoSizeMode.GrowAndShrink
        btnExaminar.Padding = New Padding(8, 2, 8, 2)
        options.Controls.Add(btnExaminar, 2, 1)
        Dim flags As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True, .Margin = New Padding(0)}
        flags.Controls.AddRange(New Control() {chkStartDownload, chkCrearDirectorio, chkUnZip})
        For Each flag As Control In flags.Controls
            flag.MinimumSize = Size.Empty
            flag.Margin = New Padding(0, 6, 16, 4)
        Next
        options.Controls.Add(flags, 0, 2)
        options.SetColumnSpan(flags, 3)
        Dim passwordRow As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True, .Margin = New Padding(0)}
        lblPassword.MinimumSize = Size.Empty
        lblPassword.Margin = New Padding(0, 6, 6, 0)
        lblPassword.TextAlign = ContentAlignment.MiddleLeft
        txtPassword.Width = 180
        txtPassword.Margin = New Padding(0, 3, 6, 3)
        LinkLabel2.Margin = New Padding(0, 6, 0, 0)
        passwordRow.Controls.AddRange(New Control() {lblPassword, txtPassword, LinkLabel2})
        options.Controls.Add(passwordRow, 0, 3)
        options.SetColumnSpan(passwordRow, 3)
        OpcionesPaquete.Controls.Add(options)
        addLinksContent.Controls.Add(OpcionesPaquete, 0, 3)

        linkStegano.MinimumSize = Size.Empty
        linkStegano.Margin = New Padding(0)
        linkStegano.Dock = DockStyle.Fill
        linkStegano.TextAlign = ContentAlignment.MiddleLeft
        linkStegano.TabIndex = 2
        addLinksContent.Controls.Add(linkStegano, 0, 4)
        addLinksViewport.Controls.Add(addLinksContent)
        layout.Controls.Add(addLinksViewport, 0, 0)

        Dim actions As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True, .FlowDirection = FlowDirection.RightToLeft, .Margin = New Padding(0), .TabIndex = 1}
        addLinksCancel = New CommandButton With {.Text = Language.GetText("Cancel")}
        AddHandler addLinksCancel.Click, Sub() Close()
        For Each button As Button In New Button() {btnAgregar, btnWatchOnline, addLinksCancel}
            button.AutoSize = True
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink
            button.Padding = New Padding(12, 5, 12, 5)
            button.MinimumSize = New Size(112, 34)
            button.Margin = New Padding(0, 0, 8, 0)
        Next
        btnAgregar.TabIndex = 0
        btnWatchOnline.TabIndex = 1
        LinkLabel1.Padding = New Padding(0, 8, 0, 0)
        LinkLabel1.TabIndex = 2
        actions.Controls.AddRange(New Control() {btnAgregar, addLinksCancel, btnWatchOnline, LinkLabel1})
        layout.Controls.Add(actions, 0, 1)
        FlowLayoutPanel1.Visible = False
        Dim shell As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Margin = New Padding(0)}
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        addLinksStepLabel = New Label With {
            .AutoSize = True, .Dock = DockStyle.Fill, .Text = Language.GetText("AddLinks_StepInput"),
            .Margin = New Padding(24, 20, 24, 0)}
        UiStyles.SetFont(addLinksStepLabel, 4, True, Me.Font)
        addLinksFeedback = New Label With {
            .AutoSize = True, .Dock = DockStyle.Fill, .Visible = False, .Margin = New Padding(24, 8, 24, 0)}
        addLinksPageHost = New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
        addLinksPageHost.Controls.Add(layout)
        shell.Controls.Add(addLinksStepLabel, 0, 0)
        shell.Controls.Add(addLinksFeedback, 0, 1)
        shell.Controls.Add(addLinksPageHost, 0, 2)
        Controls.Add(shell)
        shell.BringToFront()
        For Each surface As Control In New Control() {
                layout, addLinksViewport, addLinksContent, options, flags,
                passwordRow, actions, progress, OpcionesPaquete, shell, addLinksPageHost}
            UiStyles.SetRole(surface, UiStyles.Role.Surface)
        Next
        UiStyles.SetRole(btnAgregar, UiStyles.Role.PrimaryButton)
        UiStyles.SetRole(btnWatchOnline, UiStyles.Role.QuietButton)
        UiStyles.SetRole(addLinksCancel, UiStyles.Role.SecondaryButton)
        AcceptButton = btnAgregar
        CancelButton = addLinksCancel
        AddHandler addLinksViewport.ClientSizeChanged, AddressOf AddLinksViewportSizeChanged
        AddHandler addLinksContent.Layout, AddressOf AddLinksContentLayout
        AddHandler txtLinks.ClientSizeChanged, AddressOf AddLinksInputSizeChanged
    End Sub

    Private Sub AddLinksViewportSizeChanged(sender As Object, e As EventArgs)
        UpdateAddLinksInputHeight()
    End Sub

    Private Sub AddLinksContentLayout(sender As Object, e As LayoutEventArgs)
        UpdateAddLinksInputHeight()
    End Sub

    Private Sub UpdateAddLinksInputHeight()
        If Not addLinksLayoutReady OrElse updatingAddLinksLayout OrElse IsDisposed Then Return
        updatingAddLinksLayout = True
        Try
            Dim inputRow As RowStyle = addLinksContent.RowStyles(1)
            Dim preferred As Size = addLinksContent.GetPreferredSize(New Size(addLinksViewport.ClientSize.Width, 0))
            Dim otherHeight As Integer = Math.Max(0, preferred.Height - CInt(inputRow.Height))
            Dim inputHeight As Integer = Math.Max(txtLinks.MinimumSize.Height, addLinksViewport.ClientSize.Height - otherHeight)
            If CInt(inputRow.Height) <> inputHeight Then inputRow.Height = inputHeight
        Finally
            updatingAddLinksLayout = False
        End Try
    End Sub

    Private Sub FitAddLinksToWorkingArea(centerOnOwner As Boolean)
        Dim screenControl As Control = If(centerOnOwner AndAlso Owner IsNot Nothing, DirectCast(Owner, Control), Me)
        Dim area As Rectangle = Screen.FromControl(screenControl).WorkingArea
        ' Lower the minimum before clamping: Windows otherwise rejects the smaller size.
        MinimumSize = New Size(Math.Min(LogicalToDeviceUnits(640), area.Width),
                               Math.Min(LogicalToDeviceUnits(480), area.Height))
        Dim fitted As New Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height))
        Dim origin As Point = Location
        If centerOnOwner Then
            Dim anchor As Rectangle = If(Owner IsNot Nothing AndAlso Owner.WindowState <> FormWindowState.Minimized,
                                         Owner.Bounds, area)
            origin = New Point(anchor.Left + (anchor.Width - fitted.Width) \ 2,
                               anchor.Top + (anchor.Height - fitted.Height) \ 2)
            StartPosition = FormStartPosition.Manual
        End If
        origin.X = Math.Max(area.Left, Math.Min(origin.X, area.Right - fitted.Width))
        origin.Y = Math.Max(area.Top, Math.Min(origin.Y, area.Bottom - fitted.Height))
        Bounds = New Rectangle(origin, fitted)
    End Sub

    Protected Overrides Sub OnDpiChanged(e As DpiChangedEventArgs)
        Dim wasReady As Boolean = addLinksLayoutReady
        addLinksLayoutReady = False
        MinimumSize = Size.Empty
        Try
            MyBase.OnDpiChanged(e)
        Finally
            addLinksLayoutReady = wasReady
        End Try
        If wasReady Then
            FitAddLinksToWorkingArea(False)
            UpdateAddLinksInputHeight()
            UpdateAddLinksCueBounds()
        End If
    End Sub

    Private Sub AddLinksInputSizeChanged(sender As Object, e As EventArgs)
        UpdateAddLinksCueBounds()
    End Sub

    Private Sub UpdateAddLinksCueBounds()
        If _cueFallbackLabel Is Nothing OrElse _cueFallbackLabel.IsDisposed Then Return
        Dim inset As Integer = LogicalToDeviceUnits(4)
        _cueFallbackLabel.Location = New Point(inset, LogicalToDeviceUnits(3))
        _cueFallbackLabel.MaximumSize = New Size(Math.Max(1, txtLinks.ClientSize.Width - inset * 2), 0)
    End Sub
End Class
