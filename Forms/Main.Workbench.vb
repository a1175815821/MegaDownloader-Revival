Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Main
    Private workbenchHeader As Panel
    Private workbenchTitle As Label
    Private workbenchCount As Label
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
        workbenchSchedule = New Button()
        AddHandler workbenchSchedule.Click, AddressOf OpenDownloadSchedule
        UiStyles.SetRole(btnAddLink, UiStyles.Role.PrimaryButton)

        TableLayoutPanel1.SuspendLayout()
        Try
            TableLayoutPanel1.Controls.Clear()
            ' Retain the old hidden container as a child so its resources still have an owner.
            PanelButtonsRight.Visible = False
            Me.Controls.Add(PanelButtonsRight)
            TableLayoutPanel1.ColumnStyles.Clear()
            TableLayoutPanel1.ColumnCount = 8
            Dim buttons As Button() = {btnAddLink, btnPlay, btnPause, btnStop, workbenchSchedule, btnUpdate, Nothing, btnConfig}
            For i As Integer = 0 To buttons.Length - 1
                TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(If(i = 6, SizeType.Percent, SizeType.AutoSize), If(i = 6, 100.0F, 0.0F)))
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
        navListBox.ItemHeight = Math.Max(CInt(34 * GetUiScale()), Font.Height + 14)
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
        workbenchSearch = New TextBox With {.BorderStyle = BorderStyle.FixedSingle}
        workbenchClear = New Button With {.Text = "×", .TabStop = False}
        workbenchDetails = New Button With {.AutoEllipsis = True}
        workbenchHeader.Controls.AddRange(New Control() {workbenchTitle, workbenchCount, workbenchSearchLabel, workbenchSearch, workbenchClear, workbenchDetails})
        UiStyles.SetFont(workbenchTitle, 3, True, Me.Font)
        UiStyles.SetRole(workbenchCount, UiStyles.Role.Muted)
        UiStyles.SetRole(workbenchSearchLabel, UiStyles.Role.Muted)
        UiStyles.SetRole(workbenchClear, UiStyles.Role.QuietButton)
        UiStyles.SetRole(workbenchDetails, UiStyles.Role.QuietButton)
        UiStyles.SetRole(workbenchHeader, UiStyles.Role.Surface)
        UiStyles.SetRole(navPanel, UiStyles.Role.Surface)
        UiStyles.SetRole(detailPanel, UiStyles.Role.Surface)
        UiStyles.SetRole(detailGroup, UiStyles.Role.Surface)
        UiStyles.SetRole(TableLayoutPanel1, UiStyles.Role.Surface)
        UiStyles.SetRole(Me, UiStyles.Role.Canvas)
        Controls.Add(workbenchHeader)
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
        LayoutWorkbenchToolbar()
        UpdateWorkbenchHeading()
        navListBox.Invalidate()
    End Sub

    Private Sub LayoutWorkbenchToolbar()
        If Not workbenchReady Then Return
        Dim scale As Single = GetUiScale()
        Dim compact As Boolean = ClientSize.Width < CInt(900 * scale)
        btnPlay.Text = If(compact, "", Language.GetText("Start"))
        btnPause.Text = If(compact, "", Language.GetText("Pause"))
        btnStop.Text = If(compact, "", Language.GetText("Stop"))
        ' All commands remain available from the menu at very narrow widths.
        workbenchSchedule.Visible = ClientSize.Width >= CInt(700 * scale)
        btnConfig.Text = If(compact, "", Language.GetText("Configuration"))
        For Each button As Button In New Button() {btnAddLink, btnPlay, btnPause, btnStop, workbenchSchedule, btnUpdate, btnConfig}
            ' Center the image/text group, including the compact icon-only buttons.
            button.ImageAlign = ContentAlignment.MiddleCenter
            button.TextAlign = ContentAlignment.MiddleCenter
            button.TextImageRelation = If(button.Text.Length = 0, TextImageRelation.Overlay, TextImageRelation.ImageBeforeText)
            button.Padding = New Padding(CInt(10 * scale), CInt(4 * scale), CInt(10 * scale), CInt(4 * scale))
            button.Margin = New Padding(0, 0, CInt(8 * scale), 0)
        Next
        TableLayoutPanel1.Height = Math.Max(CInt(54 * scale), Font.Height + CInt(34 * scale))
        TableLayoutPanel1.Padding = New Padding(CInt(12 * scale), CInt(8 * scale), CInt(4 * scale), CInt(8 * scale))
        If Me.MainMenuStrip IsNot Nothing Then TableLayoutPanel1.Top = Me.MainMenuStrip.Bottom
        If Not workbenchIconCaptionsCompact.HasValue OrElse workbenchIconCaptionsCompact.Value <> compact Then ApplyWorkbenchIcons()
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
        Dim emptyText As String = Language.GetText(If(queueQuery.Length > 0 OrElse _navScope <> DownloadEstadoFilter.NavScope.All, "UI_EmptyFilter", "OLV_EmptyList"))
        If ListaDescargas.EmptyListMsg <> emptyText Then ListaDescargas.EmptyListMsg = emptyText
    End Sub

    Private queueVisibleCount As Integer

    Private Sub LayoutWorkbenchArea()
        If workbenchLayoutBusy Then Return
        workbenchLayoutBusy = True
        SuspendLayout()
        Try
            LayoutWorkbenchToolbar()
            Dim scale As Single = GetUiScale()
            Dim gap As Integer = CInt(12 * scale)
            Dim top As Integer = TableLayoutPanel1.Bottom + gap
            If quotaBannerPanel IsNot Nothing AndAlso quotaBannerPanel.Visible Then top += quotaBannerPanel.Height
            Dim bottom As Integer = StatusStrip1.Top - gap
            Dim navWidth As Integer = CInt(184 * scale)
            Dim mainLeft As Integer = gap + navWidth + gap
            Dim headerHeight As Integer = CInt(82 * scale)
            navPanel.SetBounds(gap, top, navWidth, Math.Max(50, bottom - top))
            workbenchHeader.SetBounds(mainLeft, top, Math.Max(100, ClientSize.Width - gap - mainLeft), headerHeight)
            Dim titleHeight As Integer = CInt(36 * scale)
            Dim detailWidth As Integer = Math.Max(CInt(238 * scale), Font.Height * 14)
            Dim canShowDetails As Boolean = ClientSize.Width >= mainLeft + detailWidth + CInt(420 * scale) + gap * 2
            Dim showDetails As Boolean = detailsRequested AndAlso canShowDetails
            workbenchDetails.Enabled = canShowDetails
            workbenchDetails.SetBounds(Math.Max(0, workbenchHeader.Width - CInt(130 * scale)), 0, CInt(130 * scale), titleHeight)
            workbenchTitle.SetBounds(0, 0, Math.Max(60, workbenchDetails.Left - CInt(168 * scale)), titleHeight)
            workbenchCount.SetBounds(workbenchTitle.Right + gap, 0, Math.Max(35, workbenchDetails.Left - workbenchTitle.Right - gap), titleHeight)
            Dim searchY As Integer = titleHeight + CInt(8 * scale)
            Dim labelWidth As Integer = Math.Min(CInt(112 * scale), workbenchHeader.Width \ 3)
            Dim searchHeight As Integer = Math.Max(Font.Height + CInt(10 * scale), CInt(28 * scale))
            workbenchSearchLabel.SetBounds(0, searchY, labelWidth, searchHeight)
            workbenchClear.SetBounds(workbenchHeader.Width - searchHeight, searchY, searchHeight, searchHeight)
            workbenchSearch.SetBounds(labelWidth, searchY + 2, Math.Max(30, workbenchClear.Left - labelWidth - CInt(6 * scale)), searchHeight)
            Dim contentTop As Integer = top + headerHeight
            detailPanel.Visible = showDetails
            If showDetails Then detailPanel.SetBounds(ClientSize.Width - gap - detailWidth, contentTop, detailWidth, Math.Max(50, bottom - contentTop))
            Dim listRight As Integer = If(showDetails, detailPanel.Left - gap, ClientSize.Width - gap)
            ListaDescargas.SetBounds(mainLeft, contentTop, Math.Max(100, listRight - mainLeft), Math.Max(50, bottom - contentTop))
            ListaDescargas.BorderStyle = BorderStyle.None
            LayoutNavRail()
            LayoutDetailContent()
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
        navListBox.ItemHeight = Math.Max(CInt(34 * GetUiScale()), Font.Height + 14)
        ListaDescargas.RowHeight = Math.Max(CInt(32 * GetUiScale()), Font.Height + 12)
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
        Using brush As New SolidBrush(ThemeManager.GetColor(If(selected, "Selection", If(e.Index = navHoverIndex, "ButtonHover", "Back"))))
            e.Graphics.FillRectangle(brush, rect)
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
        Dim textRect As New Rectangle(rect.Left + CInt(14 * scale), rect.Top, Math.Max(15, countRect.Left - rect.Left - CInt(18 * scale)), rect.Height)
        TextRenderer.DrawText(e.Graphics, navTitles(e.Index), e.Font, textRect, fore, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix)
        TextRenderer.DrawText(e.Graphics, countText, e.Font, countRect, ThemeManager.GetColor(If(selected, "SelectionFore", "Muted")), TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        If (e.State And DrawItemState.Focus) <> 0 Then e.DrawFocusRectangle()
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
        If queueQuery.Length = 0 Then Return DownloadEstadoFilter.MatchesScope(package, _navScope)
        If Not queueMatches.Contains(package) Then Return False
        If queueWholeMatches.Contains(package) Then Return DownloadEstadoFilter.MatchesScope(package, _navScope)
        If package.ListaFicheros Is Nothing Then Return False
        For Each file As Fichero In package.ListaFicheros
            If MatchesQueueFile(file, package) Then Return True
        Next
        Return False
    End Function

    Private Sub RebuildWorkbenchRoots()
        Dim overview As QueueOverview = Volatile.Read(queueOverviewSnapshot)
        SyncLock Mutex.ListaDescargas
            ListaDescargas.BeginUpdate()
            Try
                ApplyNavRoots()
                ListaDescargas.BuildList()
            Finally
                ListaDescargas.EndUpdate()
            End Try
        End SyncLock
        If overview IsNot Nothing Then appliedScopeVersion = overview.ScopeVersion
        UpdateWorkbenchHeading()
        UpdateDetailPanel()
    End Sub

    Private Sub RevealImportedPackage(package As Paquete)
        If Cerrando OrElse IsDisposed OrElse Not workbenchReady Then Return
        ' Reveal the confirmed import even if an earlier search or status tab hid it.
        ListaDescargas.BeginUpdate()
        Try
            _updatingNav = True
            Try
                _navScope = DownloadEstadoFilter.NavScope.All
                navListBox.SelectedIndex = 0
            Finally
                _updatingNav = False
            End Try
            If workbenchSearch.TextLength > 0 Then
                workbenchSearch.Clear()
            Else
                RebuildWorkbenchRoots()
            End If
            ListaDescargas.SelectedObject = package
            ListaDescargas.EnsureModelVisible(package)
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
        Dim buttons As Button() = {btnAddLink, btnPlay, btnPause, btnStop, btnConfig, btnUpdate}
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
