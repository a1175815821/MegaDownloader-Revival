Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Main
    Private workbenchHeader As Panel
    Private workbenchTitle As Label
    Private workbenchCount As Label
    Private workbenchSearchFrame As SearchFrame
    Private workbenchEmpty As Panel
    Private workbenchEmptyTitle As Label
    Private workbenchEmptyHint As Label
    Private workbenchEmptyAction As Button
    Private workbenchSearch As TextBox
    Private workbenchSearchLabel As Label
    Private workbenchClear As Button
    Private workbenchDetails As Button
    Private workbenchSchedule As Button
    Private workbenchSearchTimer As System.Windows.Forms.Timer
    Private workbenchReady As Boolean
    Private workbenchLayoutBusy As Boolean
    Private workbenchIconCaptionsCompact As Boolean?
    Private detailsRequested As Boolean = True
    Private navTitles As String() = {"", "", "", "", ""}
    Private navTotals As Integer() = New Integer(4) {}
    Private queueQuery As String = ""
    Private queueSearchGeneration As Integer
    Private queueMatches As New Generic.HashSet(Of Paquete)()
    Private queueFileMatches As New Generic.HashSet(Of Fichero)()
    Private queueWholeMatches As New Generic.HashSet(Of Paquete)()
    Private appliedScopeVersion As Long = -1
    Private requestedSearchVersion As Long = -1
    Private ReadOnly queueWaitMenu As New ToolStripMenuItem With {.Visible = False}
    Private detailQueueSummary As Label
    Private detailFirstFailed As CommandButton
    Private detailWaitActions As CommandButton
    Private detailHasFailedPackage As Boolean
    Private detailHasWaitActions As Boolean
    Private detailQueueMeasuredText As String
    Private detailQueueMeasuredSize As Size
    Private detailQueueMeasuredFont As Font

    Private Enum QueueWaitReason
        None
        ManualPause
        Schedule
        QueuePaused
        AnonymousQuota
        AccountQuota
        SessionExpired
        FixedAccountSignIn
    End Enum

    Private NotInheritable Class QueueSelection
        Friend ReadOnly WaitCounts(CInt(QueueWaitReason.FixedAccountSignIn)) As Integer
        Friend ReadOnly ManuallyPaused As New List(Of Fichero)()
        Friend ReadOnly QuotaAccounts As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Friend ReadOnly AccountsWaitingForSignIn As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Friend ShowFileCounts As Boolean
        Friend Total As Integer
        Friend Completed As Integer
        Friend Failed As Integer
        Friend FailedPackage As Paquete
        Friend FirstFailedFile As Fichero
        Friend AnonymousRetry As TimeSpan?
        Friend NextAccountRetryUtc As Date?
    End Class

    Private Structure QueueViewItem
        Friend Model As IDescarga
        Friend Scope As DownloadEstadoFilter.NavScope
        Friend Name As String
        Friend RelativePath As String
    End Structure

    ' Published once per existing refresh cycle. Painting and counter updates only read it.
    Private NotInheritable Class QueueOverview
        Friend ReadOnly Totals As Integer() = New Integer(4) {}
        Friend TotalBytes As Decimal
        Friend DoneBytes As Decimal
        Friend ReadOnly ViewItems As New Generic.List(Of QueueViewItem)()
        Friend ScopeVersion As Long
        Friend SearchVersion As Long
    End Class

    Private queueOverviewSnapshot As QueueOverview

    Private Sub InitWorkbench()
        Me.DoubleBuffered = True
        Me.KeyPreview = True
        btnUpdate.Visible = False
        workbenchSchedule = New CommandButton()
        AddHandler workbenchSchedule.Click, AddressOf OpenDownloadSchedule
        UiStyles.SetRole(btnAddLink, UiStyles.Role.PrimaryButton)

        TableLayoutPanel1.SuspendLayout()
        Try
            TableLayoutPanel1.Controls.Clear()
            ' Retain the old hidden container as a child so its resources still have an owner.
            PanelButtonsRight.Visible = False
            Me.Controls.Add(PanelButtonsRight)
            TableLayoutPanel1.ColumnStyles.Clear()
            TableLayoutPanel1.ColumnCount = 5
            Dim buttons As Button() = {btnPlay, btnPause, btnStop, workbenchSchedule, btnUpdate}
            For i As Integer = 0 To buttons.Length - 1
                TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
                If buttons(i) Is Nothing Then Continue For
                Dim button As Button = buttons(i)
                button.AutoSize = True
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink
                button.Margin = New Padding(0, 0, 8, 0)
                button.Padding = New Padding(10, 4, 10, 4)
                button.MinimumSize = New Size(0, Math.Max(34, Font.Height + 18))
                button.Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Bottom
                button.TextImageRelation = TextImageRelation.ImageBeforeText
                button.ImageAlign = ContentAlignment.MiddleCenter
                button.TextAlign = ContentAlignment.MiddleCenter
                If button IsNot btnAddLink Then UiStyles.SetRole(button, UiStyles.Role.QuietButton)
                TableLayoutPanel1.Controls.Add(button, i, 0)
                AddHandler button.Disposed, AddressOf DisposeWorkbenchButtonImage
            Next
            TableLayoutPanel1.Padding = New Padding(12, 8, 4, 8)
        Finally
            TableLayoutPanel1.ResumeLayout(True)
        End Try

        navListBox.DrawMode = DrawMode.OwnerDrawFixed
        navListBox.BorderStyle = BorderStyle.None
        navPanel.AutoScroll = True
        navListBox.ItemHeight = Math.Max(CInt(40 * GetUiScale()), Font.Height + 14)
        AddHandler navListBox.DrawItem, AddressOf DrawWorkbenchNav
        AddHandler navListBox.MouseMove, Sub(sender, e)
                                             Dim index As Integer = navListBox.IndexFromPoint(e.Location)
                                             If index = navHoverIndex Then Return
                                             navHoverIndex = index
                                             navListBox.Invalidate()
                                             ToolTipBotones.SetToolTip(navListBox, If(index >= 0, CStr(navListBox.Items(index)), ""))
                                         End Sub
        AddHandler navListBox.MouseLeave, Sub()
                                             navHoverIndex = -1
                                             navListBox.Invalidate()
                                         End Sub
        AddHandler btnUpdate.VisibleChanged, Sub() LayoutDownloadArea()

        workbenchHeader = New Panel With {.Name = "workbenchHeader"}
        workbenchTitle = New Label With {.AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft}
        workbenchCount = New Label With {.AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft}
        workbenchSearchLabel = New Label With {.AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft}
        workbenchSearch = New TextBox()
        workbenchClear = New CommandButton With {.Text = "×", .TabStop = False}
        workbenchDetails = New CommandButton With {.AutoEllipsis = True}
        workbenchSearchFrame = New SearchFrame(workbenchSearch, workbenchClear)
        UiStyles.SetRole(workbenchSearch, UiStyles.Role.InputSurface)
        AddHandler workbenchSearch.HandleCreated, Sub() UiStyles.SetSearchCue(workbenchSearch)
        workbenchSearchLabel.Visible = False
        TableLayoutPanel1.Dock = DockStyle.None
        TableLayoutPanel1.Anchor = AnchorStyles.Top Or AnchorStyles.Left
        UiStyles.SetRole(megaDriveButton, UiStyles.Role.SecondaryButton)
        AddHandler megaDriveButton.Click, AddressOf OpenMegaDrive
        workbenchHeader.Controls.AddRange(New Control() {workbenchTitle, workbenchCount, workbenchSearchLabel, workbenchSearchFrame, workbenchDetails, megaDriveButton, btnAddLink, TableLayoutPanel1})
        navPanel.Controls.Add(btnConfig)
        btnConfig.Anchor = AnchorStyles.Top Or AnchorStyles.Left
        UiStyles.SetRole(btnConfig, UiStyles.Role.QuietButton)
        AddHandler btnConfig.Disposed, AddressOf DisposeWorkbenchButtonImage
        AddHandler btnAddLink.Disposed, AddressOf DisposeWorkbenchButtonImage
        InitWorkbenchEmptyState()
        UiStyles.SetFont(workbenchTitle, 6, True, Me.Font)
        UiStyles.SetRole(workbenchCount, UiStyles.Role.Muted)
        UiStyles.SetRole(workbenchSearchLabel, UiStyles.Role.Muted)
        UiStyles.SetRole(workbenchClear, UiStyles.Role.QuietButton)
        UiStyles.SetRole(workbenchDetails, UiStyles.Role.QuietButton)
        UiStyles.SetRole(workbenchHeader, UiStyles.Role.Surface)
        UiStyles.SetRole(navPanel, UiStyles.Role.Sidebar)
        UiStyles.SetRole(detailPanel, UiStyles.Role.Surface)
        UiStyles.SetRole(detailGroup, UiStyles.Role.Surface)
        UiStyles.SetRole(TableLayoutPanel1, UiStyles.Role.Surface)
        UiStyles.SetRole(Me, UiStyles.Role.Surface)
        Controls.Add(workbenchHeader)
        AddHandler navPanel.Paint, Sub(sender, e)
                                       Using pen As New Pen(ThemeManager.GetColor("Border"))
                                           e.Graphics.DrawLine(pen, navPanel.Width - 1, 0, navPanel.Width - 1, navPanel.Height)
                                       End Using
                                   End Sub
        workbenchSearchTimer = New System.Windows.Forms.Timer(components) With {.Interval = 220}
        AddHandler workbenchSearch.TextChanged, Sub()
                                                   Interlocked.Increment(queueSearchGeneration)
                                                   workbenchSearchTimer.Stop()
                                                   If String.IsNullOrWhiteSpace(workbenchSearch.Text) Then
                                                       StartQueueSearch()
                                                   Else
                                                       workbenchSearchTimer.Start()
                                                   End If
                                               End Sub
        AddHandler workbenchSearchTimer.Tick, Sub()
                                                  workbenchSearchTimer.Stop()
                                                  StartQueueSearch()
                                              End Sub
        AddHandler workbenchClear.Click, Sub()
                                             workbenchSearch.Clear()
                                             workbenchSearch.Focus()
                                         End Sub
        AddHandler workbenchSearch.KeyDown, Sub(sender, e)
                                               If e.KeyCode = Keys.Escape Then
                                                   workbenchSearch.Clear()
                                                   e.SuppressKeyPress = True
                                               End If
                                           End Sub
        AddHandler workbenchDetails.Click, Sub()
                                               detailsRequested = Not detailsRequested
                                               LayoutDownloadArea()
                                               UpdateDetailPanel()
                                           End Sub
        AddHandler Me.KeyDown, Sub(sender, e)
                                   If e.Control AndAlso e.KeyCode = Keys.F Then
                                       workbenchSearch.Focus()
                                       workbenchSearch.SelectAll()
                                       e.SuppressKeyPress = True
                                   End If
                               End Sub
        StatusStrip1.SizingGrip = True
        RAMProcToolStripStatusLabel.ForeColor = ThemeManager.GetColor("Muted")
        InitMegaAccountUi()
        workbenchReady = True
        UpdateWorkbenchTexts()
    End Sub

    Private Sub UpdateWorkbenchTexts()
        If Not workbenchReady Then Return
        Dim keys As String() = {"Nav_All", "Nav_Downloading", "Nav_Waiting", "Nav_Failed", "Nav_Completed"}
        For i As Integer = 0 To 4
            navTitles(i) = Language.GetText(keys(i))
        Next
        workbenchSearchLabel.Text = Language.GetText("UI_Search")
        workbenchSearch.AccessibleName = workbenchSearchLabel.Text
        UiStyles.SetSearchCue(workbenchSearch)
        workbenchClear.AccessibleName = Language.GetText("UI_ClearSearch")
        btnAddLink.AccessibleName = Language.GetText("Add links")
        btnPlay.AccessibleName = Language.GetText("Start downloads")
        btnPause.AccessibleName = Language.GetText("Pause downloads")
        btnStop.AccessibleName = Language.GetText("Stop downloads")
        btnConfig.AccessibleName = Language.GetText("Configuration")
        btnUpdate.AccessibleName = Language.GetText("New version do you want to download it?")
        ToolTipBotones.SetToolTip(workbenchSearch, Language.GetText("UI_SearchHint"))
        ToolTipBotones.SetToolTip(workbenchClear, Language.GetText("UI_ClearSearch"))
        workbenchSchedule.Text = Language.GetText("UI_Schedule")
        ToolTipBotones.SetToolTip(workbenchSchedule, Language.GetText("Schedule_Title"))
        btnConfig.Text = Language.GetText("Configuration")
        btnAddLink.Text = Language.GetText("Add links")
        megaDriveButton.Text = Language.GetText("MegaDrive_Title")
        megaDriveButton.AccessibleName = Language.GetText("MegaDrive_Download")
        ToolTipBotones.SetToolTip(megaDriveButton, megaDriveButton.AccessibleName)
        LayoutWorkbenchToolbar()
        UpdateWorkbenchHeading()
        navListBox.Invalidate()
    End Sub

    Private Sub LayoutWorkbenchToolbar()
        If Not workbenchReady Then Return
        Dim scale As Single = GetUiScale()
        Dim compact As Boolean = ClientSize.Width < CInt(1060 * scale)
        btnPlay.Text = If(compact, "", Language.GetText("Start"))
        btnPause.Text = If(compact, "", Language.GetText("Pause"))
        btnStop.Text = Language.GetText("Stop")
        workbenchSchedule.Visible = ClientSize.Width >= CInt(1060 * scale)
        btnConfig.Text = Language.GetText("Configuration")
        For Each button As Button In New Button() {btnAddLink, btnPlay, btnPause, btnStop, workbenchSchedule, btnUpdate, btnConfig}
            button.ImageAlign = ContentAlignment.MiddleCenter
            button.TextImageRelation = TextImageRelation.ImageBeforeText
            button.Padding = New Padding(CInt(10 * scale), CInt(5 * scale), CInt(10 * scale), CInt(5 * scale))
            button.Margin = New Padding(0, 0, CInt(4 * scale), 0)
            button.MinimumSize = New Size(CInt(36 * scale), Math.Max(CInt(36 * scale), Font.Height + CInt(14 * scale)))
        Next
        btnAddLink.AutoSize = False
        btnConfig.AutoSize = False
        btnConfig.ImageAlign = ContentAlignment.MiddleLeft
        btnConfig.TextAlign = ContentAlignment.MiddleLeft
        TableLayoutPanel1.Padding = New Padding(0)
        TableLayoutPanel1.Height = Math.Max(CInt(36 * scale), Font.Height + CInt(14 * scale))
        If Not workbenchIconCaptionsCompact.HasValue OrElse workbenchIconCaptionsCompact.Value <> compact Then ApplyWorkbenchIcons()
    End Sub

    Private Sub InitWorkbenchEmptyState()
        workbenchEmpty = New Panel With {.Name = "workbenchEmpty", .Visible = False}
        workbenchEmptyTitle = New Label With {.TextAlign = ContentAlignment.MiddleCenter}
        workbenchEmptyHint = New Label With {.TextAlign = ContentAlignment.TopCenter}
        workbenchEmptyAction = New CommandButton With {.AutoSize = False}
        UiStyles.SetFont(workbenchEmptyTitle, 4, True, Me.Font)
        UiStyles.SetRole(workbenchEmpty, UiStyles.Role.Surface)
        UiStyles.SetRole(workbenchEmptyHint, UiStyles.Role.Muted)
        UiStyles.SetRole(workbenchEmptyAction, UiStyles.Role.SecondaryButton)
        workbenchEmpty.Controls.AddRange(New Control() {workbenchEmptyTitle, workbenchEmptyHint, workbenchEmptyAction})
        AddHandler workbenchEmptyAction.Click, Sub() btnAddLink.PerformClick()
        Controls.Add(workbenchEmpty)
        EnableLinkDrop(workbenchEmpty)
    End Sub

    Private Sub LayoutWorkbenchEmptyState()
        If workbenchEmpty Is Nothing Then Return
        workbenchEmpty.Bounds = ListaDescargas.Bounds
        Dim scale As Single = GetUiScale()
        Dim width As Integer = Math.Min(CInt(400 * scale), Math.Max(100, workbenchEmpty.Width - CInt(32 * scale)))
        Dim x As Integer = (workbenchEmpty.Width - width) \ 2
        Dim y As Integer = Math.Max(CInt(16 * scale), (workbenchEmpty.Height - CInt(170 * scale)) \ 2)
        workbenchEmptyTitle.SetBounds(x, y, width, CInt(38 * scale))
        workbenchEmptyHint.SetBounds(x, workbenchEmptyTitle.Bottom + CInt(8 * scale), width, CInt(64 * scale))
        Dim actionWidth As Integer = Math.Min(width, Math.Max(CInt(128 * scale), TextRenderer.MeasureText(workbenchEmptyAction.Text, Font).Width + CInt(32 * scale)))
        workbenchEmptyAction.SetBounds((workbenchEmpty.Width - actionWidth) \ 2, workbenchEmptyHint.Bottom + CInt(8 * scale), actionWidth, CInt(36 * scale))
    End Sub

    Private Sub UpdateWorkbenchHeading()
        If Not workbenchReady Then Return
        Dim scope As Integer = CInt(_navScope)
        UiStyles.SetText(workbenchTitle, navTitles(scope))
        Dim count As Integer = navTotals(scope)
        If queueQuery.Length > 0 Then count = queueVisibleCount
        UiStyles.SetText(workbenchCount, Language.GetText("UI_PackageCount").Replace("%N%", count.ToString()))
        workbenchClear.Enabled = workbenchSearch.TextLength > 0
        UiStyles.SetText(workbenchDetails, Language.GetText(If(detailsRequested AndAlso workbenchDetails.Enabled, "UI_HideDetails", "UI_ShowDetails")))
        workbenchDetails.AccessibleName = workbenchDetails.Text
        ' While the startup queue is loading or could not be loaded, the list's own
        ' "Loading please wait" / "Downloads could not be loaded" message must stay visible:
        ' the empty-queue prompt would cover it and wrongly claim the queue is simply empty.
        Dim queuePending As Boolean = System.Threading.Volatile.Read(_startupQueueLoading)
        Dim queueUnavailable As Boolean = Paquete.HasTransientLoadError OrElse Paquete.HasCorruptLoadError
        If Not queuePending AndAlso Not queueUnavailable Then
            If ListaDescargas.EmptyListMsg <> String.Empty Then ListaDescargas.EmptyListMsg = String.Empty
        End If
        If workbenchEmpty IsNot Nothing Then
            Dim filtered As Boolean = queueQuery.Length > 0 OrElse _navScope <> DownloadEstadoFilter.NavScope.All
            UiStyles.SetText(workbenchEmptyTitle, Language.GetText(If(filtered, "UI_NoMatches", "UI_EmptyTitle")))
            UiStyles.SetText(workbenchEmptyHint, Language.GetText(If(filtered, "UI_EmptyFilter", "UI_EmptyHint")))
            UiStyles.SetText(workbenchEmptyAction, Language.GetText("Add links"))
            workbenchEmptyAction.Visible = Not filtered
            Dim isEmpty As Boolean = Not queuePending AndAlso Not queueUnavailable AndAlso ListaDescargas.Items.Count = 0
            workbenchEmpty.Visible = isEmpty
            If isEmpty AndAlso Controls.GetChildIndex(workbenchEmpty) <> 0 Then workbenchEmpty.BringToFront()
        End If
    End Sub

    Private queueVisibleCount As Integer

    Private Sub LayoutWorkbenchArea()
        If workbenchLayoutBusy Then Return
        workbenchLayoutBusy = True
        SuspendLayout()
        Try
            LayoutMainMenu()
            EnsureMegaAccountMenu()
            LayoutWorkbenchToolbar()
            Dim scale As Single = GetUiScale()
            Dim pad As Integer = CInt(24 * scale)
            Dim top As Integer = LayoutDownloadBanners(MainMenuStrip.Bottom)
            Dim bottom As Integer = StatusStrip1.Top - pad
            Dim navWidth As Integer = CInt(192 * scale)
            Dim mainLeft As Integer = navWidth + pad
            Dim headerHeight As Integer = CInt(128 * scale)
            navPanel.SetBounds(0, top, navWidth, Math.Max(50, StatusStrip1.Top - top))
            workbenchHeader.SetBounds(mainLeft, top + pad, Math.Max(100, ClientSize.Width - pad - mainLeft), headerHeight)
            Dim commandHeight As Integer = TableLayoutPanel1.Height
            Dim addWidth As Integer = Math.Max(CInt(124 * scale), TextRenderer.MeasureText(btnAddLink.Text, Font).Width + CInt(52 * scale))
            btnAddLink.SetBounds(Math.Max(0, workbenchHeader.Width - addWidth), 0, addWidth, commandHeight)
            Dim driveWidth As Integer = TextRenderer.MeasureText(megaDriveButton.Text, Font).Width + CInt(26 * scale)
            Dim actionGap As Integer = CInt(8 * scale)
            megaDriveButton.SetBounds(Math.Max(0, btnAddLink.Left - actionGap - driveWidth), 0, driveWidth, commandHeight)
            Dim hasSelection As Boolean = ListaDescargas.SelectedObject IsNot Nothing
            workbenchDetails.Visible = hasSelection
            workbenchDetails.Enabled = bottom - workbenchHeader.Bottom >= CInt(260 * scale)
            Dim detailsWidth As Integer = CInt(112 * scale)
            Dim inlineDetails As Boolean = megaDriveButton.Left >= CInt(248 * scale)
            If inlineDetails Then
                workbenchDetails.SetBounds(megaDriveButton.Left - actionGap - detailsWidth, 0, detailsWidth, commandHeight)
            Else
                workbenchDetails.SetBounds(workbenchHeader.Width - detailsWidth, commandHeight, detailsWidth, CInt(72 * scale) - commandHeight)
            End If
            Dim titleRight As Integer = If(hasSelection AndAlso inlineDetails, workbenchDetails.Left, megaDriveButton.Left) - CInt(12 * scale)
            workbenchTitle.SetBounds(0, 0, Math.Max(80, titleRight), CInt(34 * scale))
            workbenchCount.SetBounds(0, workbenchTitle.Bottom + CInt(2 * scale), workbenchTitle.Width, CInt(24 * scale))
            Dim searchWidth As Integer = Math.Min(CInt(280 * scale), Math.Max(CInt(176 * scale), workbenchHeader.Width \ 3))
            Dim searchY As Integer = CInt(72 * scale)
            workbenchSearchFrame.SetBounds(workbenchHeader.Width - searchWidth, searchY, searchWidth, commandHeight)
            TableLayoutPanel1.SetBounds(0, searchY, Math.Max(80, workbenchSearchFrame.Left - CInt(12 * scale)), commandHeight)
            Dim contentTop As Integer = workbenchHeader.Bottom
            Dim listRight As Integer = ClientSize.Width - pad
            Dim listBottom As Integer = bottom
            Dim showDetails As Boolean = hasSelection AndAlso detailsRequested AndAlso workbenchDetails.Enabled
            detailPanel.Visible = showDetails
            If showDetails Then
                If ClientSize.Width >= CInt(1280 * scale) Then
                    Dim detailWidth As Integer = CInt(260 * scale)
                    detailPanel.SetBounds(ClientSize.Width - pad - detailWidth, contentTop, detailWidth, Math.Max(50, bottom - contentTop))
                    listRight = detailPanel.Left - pad
                Else
                    Dim detailHeight As Integer = Math.Min(CInt(220 * scale), (bottom - contentTop) * 2 \ 5)
                    detailPanel.SetBounds(mainLeft, bottom - detailHeight, Math.Max(100, listRight - mainLeft), detailHeight)
                    listBottom = detailPanel.Top - CInt(12 * scale)
                End If
            End If
            ListaDescargas.SetBounds(mainLeft, contentTop, Math.Max(100, listRight - mainLeft), Math.Max(50, listBottom - contentTop))
            ListaDescargas.BorderStyle = BorderStyle.None
            LayoutNavRail()
            LayoutDetailContent()
            LayoutQueueDetailContent()
            LayoutWorkbenchEmptyState()
            UpdateWorkbenchHeading()
        Finally
            ResumeLayout(False)
            workbenchLayoutBusy = False
        End Try
    End Sub

    Private navHoverIndex As Integer = -1

    Protected Overrides Sub OnDpiChanged(e As DpiChangedEventArgs)
        ' A minimum scaled on the previous screen can exceed the new working area.
        MinimumSize = Size.Empty
        MyBase.OnDpiChanged(e)
        If Not workbenchReady Then Return
        ClampMainWindowToWorkingArea(Screen.FromControl(Me).WorkingArea)
        navListBox.ItemHeight = Math.Max(CInt(40 * GetUiScale()), Font.Height + 14)
        ListaDescargas.RowHeight = Math.Max(CInt(40 * GetUiScale()), Font.Height + 16)
        detailLayoutSize = Size.Empty
        ApplyWorkbenchIcons()
        LayoutDownloadArea()
    End Sub

    Private Sub ClampMainWindowToWorkingArea(area As Rectangle)
        MinimumSize = New Size(Math.Min(LogicalToDeviceUnits(640), area.Width),
                               Math.Min(LogicalToDeviceUnits(440), area.Height))
        If WindowState <> FormWindowState.Normal Then Return
        Dim fitted As New Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height))
        Dim origin As New Point(Math.Max(area.Left, Math.Min(Left, area.Right - fitted.Width)),
                                Math.Max(area.Top, Math.Min(Top, area.Bottom - fitted.Height)))
        Bounds = New Rectangle(origin, fitted)
    End Sub

    Private Sub DisposeWorkbenchButtonImage(sender As Object, e As EventArgs)
        Dim button As Button = DirectCast(sender, Button)
        Dim owned As Image = button.Image
        button.Image = Nothing
        If owned IsNot Nothing Then owned.Dispose()
    End Sub

    Private Sub DrawWorkbenchNav(sender As Object, e As DrawItemEventArgs)
        If e.Index < 0 OrElse e.Index > 4 Then Return
        Dim selected As Boolean = (e.State And DrawItemState.Selected) <> 0
        Dim scale As Single = GetUiScale()
        Dim rect As Rectangle = Rectangle.Inflate(e.Bounds, 0, -CInt(2 * scale))
        Using brush As New SolidBrush(ThemeManager.GetColor(If(selected, "Selection", If(e.Index = navHoverIndex, "ButtonHover", "ControlBack"))))
            Using shape = CommandButton.Rounded(rect, CInt(6 * scale))
                e.Graphics.FillPath(brush, shape)
            End Using
        End Using
        If selected Then
            Using brush As New SolidBrush(ThemeManager.GetColor("Accent"))
                e.Graphics.FillRectangle(brush, rect.Left, rect.Top + 5, Math.Max(2, CInt(3 * scale)), Math.Max(2, rect.Height - 10))
            End Using
        End If
        Dim fore As Color = ThemeManager.GetColor(If(selected, "SelectionFore", "Fore"))
        Dim countText As String = navTotals(e.Index).ToString()
        Dim countWidth As Integer = TextRenderer.MeasureText(countText, e.Font).Width + CInt(10 * scale)
        Dim countRect As New Rectangle(rect.Right - countWidth - CInt(8 * scale), rect.Top, countWidth, rect.Height)
        DrawNavGlyph(e.Graphics, New Rectangle(rect.Left + CInt(12 * scale), rect.Top + (rect.Height - CInt(18 * scale)) \ 2, CInt(18 * scale), CInt(18 * scale)), e.Index, fore)
        Dim textRect As New Rectangle(rect.Left + CInt(40 * scale), rect.Top, Math.Max(15, countRect.Left - rect.Left - CInt(44 * scale)), rect.Height)
        TextRenderer.DrawText(e.Graphics, navTitles(e.Index), e.Font, textRect, fore, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix)
        TextRenderer.DrawText(e.Graphics, countText, e.Font, countRect, ThemeManager.GetColor(If(selected, "SelectionFore", "Muted")), TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        If (e.State And DrawItemState.Focus) <> 0 Then e.DrawFocusRectangle()
    End Sub

    Private Shared Sub DrawNavGlyph(graphics As Graphics, bounds As Rectangle, index As Integer, color As Color)
        Dim state = graphics.Save()
        Try
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            graphics.TranslateTransform(bounds.X, bounds.Y)
            graphics.ScaleTransform(bounds.Width / 20.0F, bounds.Height / 20.0F)
            Using pen As New Pen(color, 1.5F)
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                Select Case index
                    Case 0
                        For y As Integer = 2 To 11 Step 9
                            For x As Integer = 2 To 11 Step 9
                                graphics.DrawRectangle(pen, x, y, 6, 6)
                            Next
                        Next
                    Case 1
                        graphics.DrawLine(pen, 10, 2, 10, 13)
                        graphics.DrawLines(pen, New Point() {New Point(6, 9), New Point(10, 13), New Point(14, 9)})
                        graphics.DrawLines(pen, New Point() {New Point(3, 13), New Point(3, 17), New Point(17, 17), New Point(17, 13)})
                    Case 2
                        graphics.DrawEllipse(pen, 2, 2, 16, 16)
                        graphics.DrawLines(pen, New Point() {New Point(10, 5), New Point(10, 10), New Point(14, 12)})
                    Case 3
                        graphics.DrawEllipse(pen, 2, 2, 16, 16)
                        graphics.DrawLine(pen, 10, 5, 10, 11)
                        graphics.DrawLine(pen, 10, 14, 10, 14.5F)
                    Case 4
                        graphics.DrawEllipse(pen, 2, 2, 16, 16)
                        graphics.DrawLines(pen, New Point() {New Point(6, 10), New Point(9, 13), New Point(14, 7)})
                End Select
            End Using
        Finally
            graphics.Restore(state)
        End Try
    End Sub

    Private Async Sub StartQueueSearch()
        If Cerrando OrElse IsDisposed OrElse ListaPaquetes Is Nothing Then Return
        Dim generation As Integer = Interlocked.Increment(queueSearchGeneration)
        Dim overview As QueueOverview = Volatile.Read(queueOverviewSnapshot)
        requestedSearchVersion = If(overview Is Nothing, -1, overview.SearchVersion)
        Dim query As String = workbenchSearch.Text.Trim()
        If query.Length = 0 Then
            queueQuery = ""
            queueMatches.Clear()
            queueWholeMatches.Clear()
            queueFileMatches.Clear()
            RebuildWorkbenchRoots()
            Return
        End If
        Dim snapshot As New Generic.List(Of KeyValuePair(Of Paquete, Generic.List(Of Fichero)))()
        SyncLock Mutex.ListaDescargas
            For Each package As Paquete In ListaPaquetes
                snapshot.Add(New KeyValuePair(Of Paquete, Generic.List(Of Fichero))(package, package.SnapshotFicheros()))
            Next
        End SyncLock
        Try
            Dim result = Await Task.Run(Function()
                                            Dim packages As New Generic.HashSet(Of Paquete)()
                                            Dim whole As New Generic.HashSet(Of Paquete)()
                                            Dim files As New Generic.HashSet(Of Fichero)()
                                            For Each item In snapshot
                                                If generation <> Volatile.Read(queueSearchGeneration) Then Return Nothing
                                                If ContainsQueueText(item.Key.Nombre, query) Then
                                                    whole.Add(item.Key)
                                                    packages.Add(item.Key)
                                                Else
                                                    For Each file As Fichero In item.Value
                                                        If generation <> Volatile.Read(queueSearchGeneration) Then Return Nothing
                                                        If ContainsQueueText(file.NombreFichero, query) OrElse ContainsQueueText(file.RutaRelativa, query) Then
                                                            files.Add(file)
                                                            packages.Add(item.Key)
                                                        End If
                                                    Next
                                                End If
                                            Next
                                            Return Tuple.Create(packages, whole, files)
                                        End Function)
            If IsDisposed OrElse Cerrando OrElse generation <> queueSearchGeneration OrElse result Is Nothing OrElse
                Not String.Equals(query, workbenchSearch.Text.Trim(), StringComparison.Ordinal) Then Return
            queueQuery = query
            queueMatches = result.Item1
            queueWholeMatches = result.Item2
            queueFileMatches = result.Item3
            RebuildWorkbenchRoots()
        Catch ex As Exception
            Log.WriteDebug("Queue search failed: " & Log.SafeException(ex))
        End Try
    End Sub

    Private Shared Function ContainsQueueText(value As String, query As String) As Boolean
        Return value IsNot Nothing AndAlso value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function MatchesQueueFile(file As Fichero, package As Paquete) As Boolean
        Return DownloadEstadoFilter.MatchesScope(file, _navScope) AndAlso
            (queueQuery.Length = 0 OrElse queueWholeMatches.Contains(package) OrElse queueFileMatches.Contains(file))
    End Function

    Private Function MatchesQueuePackage(package As Paquete) As Boolean
        If queueQuery.Length > 0 AndAlso Not queueMatches.Contains(package) Then Return False
        If (queueQuery.Length = 0 OrElse queueWholeMatches.Contains(package)) AndAlso
           (_navScope = DownloadEstadoFilter.NavScope.All OrElse DownloadEstadoFilter.ScopeOf(package.DescargaEstado()) = _navScope) Then Return True
        For Each file As Fichero In GetQueueViewFiles(package)
            If MatchesQueueFile(file, package) Then Return True
        Next
        Return False
    End Function

    Private Sub RebuildWorkbenchRoots()
        Dim overview As QueueOverview = Volatile.Read(queueOverviewSnapshot)
        ListaDescargas.BeginUpdate()
        Try
            ApplyNavRoots()
        Finally
            ListaDescargas.EndUpdate()
        End Try
        If overview IsNot Nothing Then appliedScopeVersion = overview.ScopeVersion
        UpdateWorkbenchHeading()
        UpdateDetailPanel()
    End Sub

    Private Function ReadQueueSelection(selected As Collections.IList) As QueueSelection
        Dim result As New QueueSelection With {.ShowFileCounts = selected.Count > 1}
        Dim files As New HashSet(Of Fichero)()
        SyncLock Mutex.ListaDescargas
            For Each item As Object In selected
                Dim package As Paquete = TryCast(item, Paquete)
                If package IsNot Nothing Then
                    result.ShowFileCounts = True
                    If package.ListaFicheros Is Nothing Then Continue For
                    For Each file As Fichero In package.ListaFicheros
                        files.Add(file)
                        If result.FirstFailedFile Is Nothing AndAlso file.DescargaEstado() = Estado.Erroneo Then
                            result.FailedPackage = package
                            result.FirstFailedFile = file
                        End If
                    Next
                ElseIf TypeOf item Is Fichero Then
                    files.Add(DirectCast(item, Fichero))
                End If
            Next
        End SyncLock
        result.Total = files.Count
        If files.Count = 0 Then Return result

        Dim scheduleWaiting As Boolean = Not Config.DownloadSchedule.AllowsDownload(Date.Now)
        Dim queuePaused As Boolean = EstadoAplicacion = TipoEstadoAplicacion.Pausa
        Dim accounts As New Dictionary(Of String, MegaAccountService.Status)(StringComparer.OrdinalIgnoreCase)
        result.AnonymousRetry = MegaQuotaManager.GetRemaining()

        For Each file As Fichero In files
            Dim reason As QueueWaitReason = QueueWaitReason.None
            Dim state As Estado = file.DescargaEstado()
            Select Case state
                Case Estado.Completado
                    result.Completed += 1
                Case Estado.Erroneo
                    result.Failed += 1
                    If file.FailedByQuota OrElse (file.UsesMegaAccount AndAlso file.MegaAccountErrorCode.GetValueOrDefault() = MegaAccountService.SessionExpired) Then
                        reason = QueueTransferWaitReason(file, accounts, result)
                    End If
                Case Estado.EnCola, Estado.Pausado
                    If file.PausaIndividual Then
                        result.ManuallyPaused.Add(file)
                        reason = QueueWaitReason.ManualPause
                    ElseIf scheduleWaiting Then
                        reason = QueueWaitReason.Schedule
                    ElseIf queuePaused Then
                        reason = QueueWaitReason.QueuePaused
                    Else
                        reason = QueueTransferWaitReason(file, accounts, result)
                    End If
            End Select
            If reason <> QueueWaitReason.None Then result.WaitCounts(CInt(reason)) += 1
        Next
        Return result
    End Function

    Private Shared Function QueueTransferWaitReason(file As Fichero, accounts As Dictionary(Of String, MegaAccountService.Status), selection As QueueSelection) As QueueWaitReason
        If Not file.UsesMegaAccount Then Return If(selection.AnonymousRetry.HasValue OrElse file.FailedByQuota, QueueWaitReason.AnonymousQuota, QueueWaitReason.None)
        If file.HasRecordedMegaAccountOutput Then Return QueueWaitReason.None

        Dim email As String = file.EffectiveMegaAccountEmail
        Dim status As MegaAccountService.Status = Nothing
        If Not accounts.TryGetValue(email, status) Then
            status = MegaAccountService.Current.GetStatus(email)
            accounts.Add(email, status)
        End If
        If status.Connecting Then Return QueueWaitReason.None
        If Not status.SignedIn Then
            If file.FixedDownloadIdentity Then
                selection.AccountsWaitingForSignIn.Add(email)
                Return QueueWaitReason.FixedAccountSignIn
            End If
            If status.NeedsLogin OrElse file.MegaAccountErrorCode.GetValueOrDefault() = MegaAccountService.SessionExpired Then Return QueueWaitReason.SessionExpired
        End If
        If status.SignedIn AndAlso (status.QuotaReached OrElse (file.FailedByQuota AndAlso status.QuotaDelayUnknown)) Then
            selection.QuotaAccounts.Add(email)
            If status.QuotaRetryUtc.HasValue AndAlso status.QuotaRetryUtc.Value < selection.NextAccountRetryUtc.GetValueOrDefault(Date.MaxValue) Then
                selection.NextAccountRetryUtc = status.QuotaRetryUtc
            End If
            Return QueueWaitReason.AccountQuota
        End If
        Return QueueWaitReason.None
    End Function

    Private Shared Function QueueWaitCaption(key As String, count As Integer, showCount As Boolean) As String
        Dim caption As String = Language.GetText(key)
        If showCount Then caption = Language.GetText("QueueWait_Count").Replace("%TEXT%", caption).Replace("%N%", count.ToString())
        Return caption
    End Function

    Private Sub UpdateQueueDetail(selected As Collections.IList)
        Dim selection As QueueSelection = ReadQueueSelection(selected)
        Dim lines As New List(Of String)()
        If selection.ShowFileCounts Then
            lines.Add(Language.GetText(If(selected.Count > 1, "Detail_SelectedFiles", "Detail_PackageFiles")).
                Replace("%DONE%", selection.Completed.ToString()).Replace("%TOTAL%", selection.Total.ToString()).Replace("%FAILED%", selection.Failed.ToString()))
        End If
        Dim keys As String() = {"QueueWait_ManualPause", "Schedule_Waiting", "QueueWait_QueuePaused", "QueueWait_AnonymousQuota", "MegaDrive_WaitingQuota", "QueueWait_SessionExpired", "QueueWait_FixedAccountSignIn"}
        For i As Integer = 0 To keys.Length - 1
            Dim reason As QueueWaitReason = CType(i + 1, QueueWaitReason)
            Dim count As Integer = selection.WaitCounts(CInt(reason))
            If count = 0 Then Continue For
            Dim detail = QueueWaitCaption(keys(i), count, selection.ShowFileCounts)
            If reason = QueueWaitReason.FixedAccountSignIn AndAlso selection.AccountsWaitingForSignIn.Count = 1 Then
                detail &= " · " & QueueDownloadIdentityText(selection.AccountsWaitingForSignIn.Single())
            End If
            lines.Add(detail)
            Dim retryUtc As Date? = Nothing
            If reason = QueueWaitReason.AnonymousQuota AndAlso selection.AnonymousRetry.HasValue Then retryUtc = Date.UtcNow + selection.AnonymousRetry.Value
            If reason = QueueWaitReason.AccountQuota Then retryUtc = selection.NextAccountRetryUtc
            If retryUtc.HasValue Then lines.Add(Language.GetText("QueueWait_RetryAt").Replace("%T%", retryUtc.Value.ToLocalTime().ToString("g")))
        Next

        If detailQueueSummary Is Nothing Then
            If lines.Count = 0 Then Return
            detailQueueSummary = New Label With {.Name = "QueueDetailSummary", .UseMnemonic = False, .Visible = False}
            detailFirstFailed = New CommandButton With {.Name = "QueueDetailFirstFailed", .AutoEllipsis = True, .Visible = False}
            detailWaitActions = New CommandButton With {.Name = "QueueDetailWaitActions", .AutoEllipsis = True, .Visible = False}
            AddHandler detailFirstFailed.Click, AddressOf ShowFirstFailedFile
            AddHandler detailWaitActions.Click, AddressOf ShowQueueWaitActions
            detailContentPanel.Controls.AddRange(New Control() {detailQueueSummary, detailFirstFailed, detailWaitActions})
            UiStyles.SetRole(detailQueueSummary, UiStyles.Role.Muted)
            UiStyles.SetRole(detailFirstFailed, UiStyles.Role.SecondaryButton)
            UiStyles.SetRole(detailWaitActions, UiStyles.Role.SecondaryButton)
            EnableLinkDrop(detailQueueSummary)
            EnableLinkDrop(detailFirstFailed)
            EnableLinkDrop(detailWaitActions)
        End If
        UiStyles.SetText(detailQueueSummary, String.Join(Environment.NewLine, lines))
        detailHasFailedPackage = selection.FirstFailedFile IsNot Nothing
        detailHasWaitActions = selection.WaitCounts.Any(Function(count) count > 0)
        If detailHasFailedPackage Then UiStyles.SetText(detailFirstFailed, Language.GetText("Detail_FirstFailedFile"))
        If detailHasWaitActions Then UiStyles.SetText(detailWaitActions, Language.GetText("QueueWait_Actions"))
        LayoutQueueDetailContent()
    End Sub

    Private Sub LayoutQueueDetailContent()
        If detailQueueSummary Is Nothing OrElse Not detailContentPanel.Visible Then Return
        ' Base detail layout is cached independently. Insert the summary below its title.
        Dim scale As Single = GetUiScale()
        Dim y As Integer = detailTitleLabel.Bottom + CInt(8 * scale)
        Dim x As Integer = detailTitleLabel.Left
        Dim width As Integer = detailTitleLabel.Width
        Dim gap As Integer = CInt(4 * scale)
        Dim summaryText As String = detailQueueSummary.Text
        Dim showSummary As Boolean = summaryText.Length > 0
        Dim summaryBounds As Rectangle = Rectangle.Empty
        Dim actionBounds As Rectangle = Rectangle.Empty
        Dim waitBounds As Rectangle = Rectangle.Empty
        If showSummary Then
            Dim font As Font = detailQueueSummary.Font
            If summaryText <> detailQueueMeasuredText OrElse width <> detailQueueMeasuredSize.Width OrElse font IsNot detailQueueMeasuredFont Then
                Dim height As Integer = TextRenderer.MeasureText(summaryText, font, New Size(width, Integer.MaxValue), TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix).Height
                detailQueueMeasuredText = summaryText
                detailQueueMeasuredSize = New Size(width, height)
                detailQueueMeasuredFont = font
            End If
            summaryBounds = New Rectangle(x, y, width, detailQueueMeasuredSize.Height)
            y = summaryBounds.Bottom + gap
        End If
        If detailHasFailedPackage Then
            actionBounds = New Rectangle(x, y, width, Math.Max(CInt(30 * scale), detailFirstFailed.Font.Height + gap * 2))
            y = actionBounds.Bottom + gap
        End If

        If detailHasWaitActions Then
            waitBounds = New Rectangle(x, y, width, Math.Max(CInt(30 * scale), detailWaitActions.Font.Height + gap * 2))
            y = waitBounds.Bottom + gap
        End If
        Dim offset As Integer = y - detailRows(0).Key.Top
        If offset = 0 AndAlso detailQueueSummary.Bounds = summaryBounds AndAlso detailFirstFailed.Bounds = actionBounds AndAlso
            detailWaitActions.Bounds = waitBounds AndAlso detailWaitActions.Visible = detailHasWaitActions AndAlso
            detailQueueSummary.Visible = showSummary AndAlso detailFirstFailed.Visible = detailHasFailedPackage Then Return

        Dim scrollY As Integer = detailContentPanel.AutoScrollPosition.Y
        detailContentPanel.SuspendLayout()
        Try
            detailQueueSummary.Bounds = summaryBounds
            detailQueueSummary.Visible = showSummary
            detailFirstFailed.Bounds = actionBounds
            detailFirstFailed.Visible = detailHasFailedPackage
            detailWaitActions.Bounds = waitBounds
            detailWaitActions.Visible = detailHasWaitActions
            If offset <> 0 Then
                For Each row As KeyValuePair(Of Label, Label) In detailRows
                    row.Key.Top += offset
                    row.Value.Top += offset
                Next
                detailProgressBar.Top += offset
            End If
            detailContentPanel.AutoScrollMinSize = New Size(0, detailRows.Last().Value.Bottom - scrollY + gap + CInt(12 * scale))
        Finally
            detailContentPanel.ResumeLayout(True)
        End Try
    End Sub

    Private Sub ShowQueueWaitActions(sender As Object, e As EventArgs)
        PrepareQueueWaitActions(ListaDescargas.SelectedObjects)
        If queueWaitMenu.DropDownItems.Count > 0 Then queueWaitMenu.DropDown.Show(detailWaitActions, New Point(0, detailWaitActions.Height))
    End Sub

    Private Sub PrepareQueueWaitActions(selected As Collections.IList)
        While queueWaitMenu.DropDownItems.Count > 0
            queueWaitMenu.DropDownItems(0).Dispose()
        End While
        Dim selection As QueueSelection = ReadQueueSelection(selected)
        AddQueueWaitAction(selection, QueueWaitReason.ManualPause, "QueueWait_UnpauseSelected", AddressOf UnpauseSelectedFiles)
        AddQueueWaitAction(selection, QueueWaitReason.Schedule, "Schedule_Title", AddressOf OpenDownloadSchedule)
        AddQueueWaitAction(selection, QueueWaitReason.QueuePaused, "QueueWait_ResumeQueue", Sub(sender, e) StartDownload())
        AddQueueWaitAction(selection, QueueWaitReason.AnonymousQuota, "QueueWait_RetryAnonymous", AddressOf QuotaRetryNow_Click)
        AddQueueWaitAction(selection, QueueWaitReason.AccountQuota, "MegaAccount_RefreshQuota", Async Sub(sender, e) Await RefreshQueueQuotaAsync())
        Dim signInReason = If(selection.WaitCounts(CInt(QueueWaitReason.FixedAccountSignIn)) > 0,
            QueueWaitReason.FixedAccountSignIn, QueueWaitReason.SessionExpired)
        AddQueueWaitAction(selection, signInReason, "MegaAccount_Reconnect", AddressOf OpenMegaAccount)
        queueWaitMenu.Visible = queueWaitMenu.DropDownItems.Count > 0
        If queueWaitMenu.Available Then queueWaitMenu.Text = Language.GetText("QueueWait_Actions")
    End Sub

    Private Sub AddQueueWaitAction(selection As QueueSelection, reason As QueueWaitReason, key As String, handler As EventHandler)
        If selection.WaitCounts(CInt(reason)) = 0 Then Return
        Dim action As New ToolStripMenuItem(Language.GetText(key).Replace("&", "&&"))
        AddHandler action.Click, handler
        queueWaitMenu.DropDownItems.Add(action)
    End Sub

    Private Sub UnpauseSelectedFiles(sender As Object, e As EventArgs)
        SyncLock Mutex.ListaDescargas
            Dim selection As QueueSelection = ReadQueueSelection(ListaDescargas.SelectedObjects)
            If selection.ManuallyPaused.Count = 0 Then Return
            For Each file As Fichero In selection.ManuallyPaused
                file.PausaIndividual = False
            Next
            PeticionGuardadoFichero = Now
        End SyncLock
        RefreshListaDescargas(False)
    End Sub

    Private Async Function RefreshQueueQuotaAsync() As Task
        Dim selection As QueueSelection = ReadQueueSelection(ListaDescargas.SelectedObjects)
        Dim service As MegaAccountService = MegaAccountService.Current
        For Each email As String In selection.QuotaAccounts
            Dim result As Integer = Await service.RefreshQuotaAsync(email)
            If IsDisposed OrElse Cerrando Then Return
            Dim text As String = If(result = 0, MegaAccountForm.Summary(service.GetStatus(email), True), MegaAccountService.ErrorText(result, connecting:=True))
            ToastForm.ShowToast(Me, text)
        Next
        UpdateDetailPanel()
    End Function

    Private Sub ShowFirstFailedFile(sender As Object, e As EventArgs)
        Dim selection As QueueSelection = ReadQueueSelection(ListaDescargas.SelectedObjects)
        If selection.FirstFailedFile IsNot Nothing Then RevealQueuePackage(selection.FailedPackage, selection.FirstFailedFile)
    End Sub

    Private Sub RevealImportedPackage(package As Paquete)
        RevealQueuePackage(package)
    End Sub

    Private Sub RevealQueuePackage(package As Paquete, Optional file As Fichero = Nothing)
        If Cerrando OrElse IsDisposed OrElse Not workbenchReady Then Return
        ListaDescargas.BeginUpdate()
        Try
            If file Is Nothing OrElse Not MatchesQueueFile(file, package) Then
                _updatingNav = True
                Try
                    _navScope = DownloadEstadoFilter.NavScope.All
                    navListBox.SelectedIndex = CInt(_navScope)
                Finally
                    _updatingNav = False
                End Try
                If workbenchSearch.TextLength > 0 Then
                    workbenchSearch.Clear()
                Else
                    RebuildWorkbenchRoots()
                End If
            End If
            Dim target As IDescarga = package
            If file IsNot Nothing Then
                ListaDescargas.Expand(package)
                If ListaDescargas.IndexOf(file) < 0 Then ListaDescargas.RefreshObjects(New Object() {package})
                target = file
            End If
            ListaDescargas.SelectedObject = target
            ListaDescargas.EnsureModelVisible(target)
        Finally
            ListaDescargas.EndUpdate()
        End Try
        UpdateDetailPanel()
        ListaDescargas.Focus()
    End Sub

    Private Sub PublishQueueOverview(snapshot As Generic.List(Of KeyValuePair(Of Paquete, Generic.List(Of Fichero))))
        Dim value As New QueueOverview()
        Dim capacity As Integer = snapshot.Count
        For Each item In snapshot
            capacity += item.Value.Count
        Next
        value.ViewItems.Capacity = capacity
        For Each item In snapshot
            Dim present(4) As Boolean
            Dim packageScope As DownloadEstadoFilter.NavScope = DownloadEstadoFilter.ScopeOf(item.Key.DescargaEstado())
            present(CInt(packageScope)) = True
            value.ViewItems.Add(New QueueViewItem With {.Model = item.Key, .Scope = packageScope, .Name = item.Key.Nombre})
            For Each file As Fichero In item.Value
                Dim fileScope As DownloadEstadoFilter.NavScope = DownloadEstadoFilter.ScopeOf(file.DescargaEstado())
                present(CInt(fileScope)) = True
                value.ViewItems.Add(New QueueViewItem With {.Model = file, .Scope = fileScope, .Name = file.NombreFichero, .RelativePath = file.RutaRelativa})
            Next
            value.Totals(0) += 1
            For i As Integer = 1 To 4
                If present(i) Then value.Totals(i) += 1
            Next
            Dim size As Long = item.Key.DescargaTamanoBytes()
            If size > 0 Then
                value.TotalBytes += size
                value.DoneBytes += Math.Ceiling(item.Key.DescargaPorcentaje() * size / 100D)
            End If
        Next
        Dim previous As QueueOverview = Volatile.Read(queueOverviewSnapshot)
        Dim scopeChanged As Boolean = previous Is Nothing OrElse previous.ViewItems.Count <> value.ViewItems.Count
        Dim searchChanged As Boolean = scopeChanged
        If previous IsNot Nothing Then
            For index As Integer = 0 To Math.Min(previous.ViewItems.Count, value.ViewItems.Count) - 1
                Dim before As QueueViewItem = previous.ViewItems(index)
                Dim after As QueueViewItem = value.ViewItems(index)
                Dim different As Boolean = Not Object.ReferenceEquals(before.Model, after.Model)
                scopeChanged = scopeChanged OrElse different OrElse before.Scope <> after.Scope
                searchChanged = searchChanged OrElse different OrElse Not String.Equals(before.Name, after.Name, StringComparison.Ordinal) OrElse
                    Not String.Equals(before.RelativePath, after.RelativePath, StringComparison.Ordinal)
                If scopeChanged AndAlso searchChanged Then Exit For
            Next
            value.ScopeVersion = previous.ScopeVersion
            value.SearchVersion = previous.SearchVersion
        End If
        If scopeChanged Then value.ScopeVersion += 1
        If searchChanged Then value.SearchVersion += 1
        Volatile.Write(queueOverviewSnapshot, value)
    End Sub

    Private Sub ApplyWorkbenchIcons()
        Dim buttons As Button() = {btnAddLink, btnPlay, btnPause, btnStop, btnConfig, btnUpdate, workbenchSchedule}
        Dim side As Integer = Math.Max(16, CInt(18 * GetUiScale()))
        For i As Integer = 0 To buttons.Length - 1
            Dim button As Button = buttons(i)
            Dim color As Color = ThemeManager.GetColor(If(i = 0, "AccentFore", "Fore"))
            ' Native buttons center the font line box, which includes descender space.
            ' Lift captioned glyphs slightly to align their ink with the visible letters.
            Dim lift As Integer = If(button.Text.Length > 0, Math.Max(1, CInt(GetUiScale())), 0)
            Dim icon As Bitmap = CreateWorkbenchIcon(i, side, color, lift)
            Dim old As Image = button.Image
            button.Image = icon
            If old IsNot Nothing Then old.Dispose()
        Next
        workbenchIconCaptionsCompact = btnPlay.Text.Length = 0
    End Sub

    Private Shared Function CreateWorkbenchIcon(kind As Integer, side As Integer, color As Color, lift As Integer) As Bitmap
        ' A common 24-unit canvas and stroke keep glyphs balanced. Render large first:
        ' fractional display scales otherwise make opposite edges look uneven at 18px.
        Using source As New Bitmap(side * 4, side * 4, Imaging.PixelFormat.Format32bppPArgb)
            Using graphics As Graphics = Graphics.FromImage(source)
                graphics.SmoothingMode = SmoothingMode.AntiAlias
                graphics.ScaleTransform(source.Width / 24.0F, source.Height / 24.0F)
                Using pen As New Pen(color, 2.0F)
                    pen.StartCap = LineCap.Round
                    pen.EndCap = LineCap.Round
                    pen.LineJoin = LineJoin.Round
                    Select Case kind
                        Case 0
                            graphics.DrawLine(pen, 12, 5, 12, 19)
                            graphics.DrawLine(pen, 5, 12, 19, 12)
                        Case 1
                            ' A triangle needs an optical offset to balance its heavier left edge.
                            graphics.DrawPolygon(pen, New PointF() {New PointF(8, 5), New PointF(20, 12), New PointF(8, 19)})
                        Case 2
                            graphics.DrawLine(pen, 8, 5, 8, 19)
                            graphics.DrawLine(pen, 16, 5, 16, 19)
                        Case 3
                            graphics.DrawRectangle(pen, 5, 5, 14, 14)
                        Case 4
                            Dim gear As New Generic.List(Of PointF)(32)
                            For tooth As Integer = 0 To 7
                                For corner As Integer = 0 To 3
                                    Dim offset As Double = If(corner = 0, -17.5, If(corner = 1, -12.5, If(corner = 2, 12.5, 17.5)))
                                    Dim angle As Double = (tooth * 45 + offset) * Math.PI / 180
                                    Dim radius As Double = If(corner = 0 OrElse corner = 3, 6.5, 8.0)
                                    gear.Add(New PointF(CSng(12 + Math.Cos(angle) * radius), CSng(12 + Math.Sin(angle) * radius)))
                                Next
                            Next
                            graphics.DrawPolygon(pen, gear.ToArray())
                            graphics.DrawEllipse(pen, 9, 9, 6, 6)
                        Case 5
                            graphics.DrawLine(pen, 12, 5, 12, 14)
                            graphics.DrawLines(pen, New PointF() {New PointF(8, 10), New PointF(12, 14), New PointF(16, 10)})
                            graphics.DrawLines(pen, New PointF() {New PointF(5, 16), New PointF(5, 19), New PointF(19, 19), New PointF(19, 16)})
                        Case 6
                            graphics.DrawEllipse(pen, 4, 4, 16, 16)
                            graphics.DrawLine(pen, 12, 7, 12, 12)
                            graphics.DrawLine(pen, 12, 12, 16, 14)
                    End Select
                End Using
            End Using
            Dim result As New Bitmap(side, side, Imaging.PixelFormat.Format32bppPArgb)
            Try
                Using graphics As Graphics = Graphics.FromImage(result), attributes As New Imaging.ImageAttributes()
                    graphics.CompositingMode = CompositingMode.SourceCopy
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
                    attributes.SetWrapMode(WrapMode.TileFlipXY)
                    graphics.DrawImage(source, New Rectangle(0, -lift, side, side), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes)
                End Using
                Return result
            Catch
                result.Dispose()
                Throw
            End Try
        End Using
    End Function
End Class
