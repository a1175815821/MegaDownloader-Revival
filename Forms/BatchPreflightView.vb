Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

''' <summary>Virtual grid backed by metadata, not one native row per imported file.</summary>
Public NotInheritable Class BatchPreflightView
    Inherits UserControl

    Private ReadOnly urls As List(Of String)
    Private ReadOnly queued As Dictionary(Of String, URLProcessor.FileURL)
    Private destination As String
    Private cancellation As CancellationTokenSource
    Private ReadOnly session As PreflightSession
    Private ReadOnly ownsSession As Boolean
    Private request As PreflightSession.Request
    Private ReadOnly grid As New UiStyles.BufferedGrid()
    Private ReadOnly summary As New Label()
    Private ReadOnly progressBar As New ProgressBar()
    Private ReadOnly progressTimer As New System.Windows.Forms.Timer With {.Interval = 150}
    Private displayedProgress As PreflightProgress
    Private displayedProgressSecond As Long = -1
    Private ReadOnly spaceLabel As New Label()
    Private ReadOnly search As New TextBox()
    Private ReadOnly category As New ComboBox()
    Private ReadOnly addButton As New Button()
    Private ReadOnly retryButton As New Button()
    Private ReadOnly backButton As New Button()
    Private ReadOnly cancelButton As New Button()
    Private ReadOnly stopButton As New Button()
    Private ReadOnly notice As New Label()
    Private ReadOnly targetLabel As New Label()
    Private ReadOnly filters As New FlowLayoutPanel()
    Private ReadOnly tools As New ToolStrip()
    Private ReadOnly targetTip As New ToolTip()
    Private ReadOnly filterTimer As New System.Windows.Forms.Timer With {.Interval = 180}
    Private ReadOnly selectionMenu As New ContextMenuStrip()
    Private ReadOnly statusLabels As New Dictionary(Of BatchLinkPreflight.LinkState, String)()
    Private ReadOnly folderSelectionLabel As String
    Private entries As New List(Of BatchLinkPreflight.Entry)()
    Private selection As PreflightSelection
    Private tree As PreflightTree
    Private boundRows As New List(Of PreflightTree.Node)()
    Private freeSpace As Long?
    Private loading As Boolean
    Private binding As Boolean
    Private disposeRequested As Boolean
    Private importing As Boolean
    Private checkingSpace As Boolean
    Private spaceGeneration As Integer
    Private ReadOnly queueProvider As Func(Of Dictionary(Of String, URLProcessor.FileURL))
    Public ReadOnly Property SelectedFiles As List(Of URLProcessor.FileURL)
    Public ReadOnly Property InspectionStopped As Boolean
    Public Event BackRequested As EventHandler
    Public Event CancelRequested As EventHandler
    Public Event ConfirmRequested As EventHandler

    Private ReadOnly Property SelectionLocked As Boolean
        Get
            Return loading OrElse importing OrElse disposeRequested
        End Get
    End Property

    Public Sub New(input As List(Of String), configuration As Configuracion, queueSnapshot As Dictionary(Of String, URLProcessor.FileURL), target As String,
                   Optional sharedSession As PreflightSession = Nothing, Optional prepared As PreflightSession.Request = Nothing,
                   Optional getQueue As Func(Of Dictionary(Of String, URLProcessor.FileURL)) = Nothing)
        urls = New List(Of String)(input)
        queued = queueSnapshot
        destination = target
        ownsSession = sharedSession Is Nothing
        session = If(sharedSession, New PreflightSession(configuration))
        request = prepared
        queueProvider = getQueue
        AutoScaleDimensions = New SizeF(7, 15)
        AutoScaleMode = AutoScaleMode.Font
        UiStyles.SetAppFont(Me)
        Size = New Size(960, 600)
        Dock = DockStyle.Fill
        Margin = New Padding(0)
        folderSelectionLabel = Language.GetText("Preflight_FolderSelection")
        For Each state As BatchLinkPreflight.LinkState In [Enum].GetValues(GetType(BatchLinkPreflight.LinkState))
            statusLabels(state) = Language.GetText("Preflight_State_" & state.ToString())
        Next

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18), .ColumnCount = 1, .RowCount = 7}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        UiStyles.SetRole(layout, UiStyles.Role.Surface)
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        For i As Integer = 0 To 3
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        summary.Dock = DockStyle.Fill
        summary.AutoSize = True
        summary.MinimumSize = New Size(0, 38)
        UiStyles.SetFont(summary, 0, True, Me.Font)
        summary.Text = Language.GetText("Preflight_Checking")
        Dim progressLayout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .RowCount = 2, .Margin = New Padding(0)}
        progressLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        progressLayout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        progressLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        progressLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 6))
        UiStyles.SetRole(progressLayout, UiStyles.Role.Surface)
        progressBar.Dock = DockStyle.Fill
        progressBar.Margin = New Padding(0, 0, 0, 2)
        progressBar.Style = ProgressBarStyle.Marquee
        progressLayout.Controls.Add(summary, 0, 0)
        stopButton.Text = Language.GetText("AddLinks_StopCheck")
        stopButton.AutoSize = True
        stopButton.Visible = False
        progressLayout.Controls.Add(stopButton, 1, 0)
        progressLayout.Controls.Add(progressBar, 0, 1)
        progressLayout.SetColumnSpan(progressBar, 2)
        layout.Controls.Add(progressLayout, 0, 0)
        filters.Dock = DockStyle.Fill
        filters.WrapContents = True
        filters.AutoSize = True
        filters.AutoSizeMode = AutoSizeMode.GrowAndShrink
        filters.MinimumSize = New Size(0, 34)
        filters.Controls.Add(New Label With {.Text = Language.GetText("Preflight_Search"), .AutoSize = True, .Padding = New Padding(0, 4, 0, 0)})
        search.Width = 185
        filters.Controls.Add(search)
        category.DropDownStyle = ComboBoxStyle.DropDownList
        category.Width = 125
        For Each key As String In New String() {"All", "Video", "Audio", "Archives", "Documents", "Other"}
            category.Items.Add(Language.GetText("Preflight_" & key))
        Next
        category.SelectedIndex = 0
        filters.Controls.Add(category)
        tools.GripStyle = ToolStripGripStyle.Hidden
        tools.Dock = DockStyle.Fill
        tools.Padding = New Padding(0, 4, 0, 4)
        Dim allFiles As New ToolStripDropDownButton(Language.GetText("UI_AllFiles"))
        AddSelectionAction(allFiles, "Preflight_SelectAll", Sub() ApplySelection(PreflightSelection.SelectionOperation.SelectAll, False))
        AddSelectionAction(allFiles, "Preflight_ClearAll", Sub() ApplySelection(PreflightSelection.SelectionOperation.Clear, False))
        Dim results As New ToolStripDropDownButton(Language.GetText("UI_CurrentResults"))
        AddSelectionAction(results, "Preflight_SelectVisible", Sub() SelectVisible(True))
        AddSelectionAction(results, "Preflight_InvertVisible", Sub() ApplySelection(PreflightSelection.SelectionOperation.Invert, True))
        AddSelectionAction(results, "Preflight_ClearVisible", Sub() SelectVisible(False))
        tools.Items.Add(allFiles)
        tools.Items.Add(results)
        tools.Items.Add(New ToolStripSeparator())
        Dim expand As New ToolStripButton(Language.GetText("Preflight_ExpandAll"))
        Dim collapse As New ToolStripButton(Language.GetText("Preflight_CollapseAll"))
        AddHandler expand.Click, Sub() ExpandFolders(True)
        AddHandler collapse.Click, Sub() ExpandFolders(False)
        tools.Items.Add(expand)
        tools.Items.Add(collapse)
        Dim filterArea As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 1, .RowCount = 3, .Margin = New Padding(0, 4, 0, 8)}
        For i As Integer = 0 To 2
            filterArea.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        Dim hint As New Label With {.Text = Language.GetText("Preflight_MultiSelectHint"), .AutoSize = True, .MaximumSize = New Size(900, 0), .Margin = New Padding(0, 4, 0, 8)}
        UiStyles.SetRole(hint, UiStyles.Role.Muted)
        UiStyles.SetRole(filters, UiStyles.Role.Surface)
        UiStyles.SetRole(filterArea, UiStyles.Role.Surface)
        filterArea.Controls.Add(filters, 0, 0)
        filterArea.Controls.Add(tools, 0, 1)
        filterArea.Controls.Add(hint, 0, 2)
        layout.Controls.Add(filterArea, 0, 1)

        grid.Dock = DockStyle.Fill
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = True
        grid.VirtualMode = True
        grid.AutoGenerateColumns = False
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        grid.RowTemplate.Height = Math.Max(30, Font.Height + 14)
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        grid.ColumnHeadersHeight = Math.Max(34, Font.Height + 16)
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
        grid.DefaultCellStyle.Padding = New Padding(4, 0, 4, 0)
        grid.Columns.Add(New DataGridViewCheckBoxColumn With {.Name = "Selected", .HeaderText = "", .Width = 34, .ThreeState = True, .ValueType = GetType(CheckState), .ReadOnly = True})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "FileName", .HeaderText = Language.GetText("Name"), .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 55, .ReadOnly = True})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "FileSize", .HeaderText = Language.GetText("Size"), .Width = 85, .ReadOnly = True})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "LinkStatus", .HeaderText = Language.GetText("Status"), .Width = 160, .ReadOnly = True})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "FilePath", .HeaderText = Language.GetText("Detail_Path"), .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 30, .ReadOnly = True})
        For Each column As DataGridViewColumn In grid.Columns
            column.SortMode = DataGridViewColumnSortMode.NotSortable
        Next
        AddContextAction("Preflight_CheckHighlighted", PreflightSelection.SelectionOperation.SelectAll)
        AddContextAction("Preflight_UncheckHighlighted", PreflightSelection.SelectionOperation.Clear)
        AddContextAction("Preflight_InvertHighlighted", PreflightSelection.SelectionOperation.Invert)
        grid.ContextMenuStrip = selectionMenu
        layout.Controls.Add(grid, 0, 2)
        spaceLabel.Dock = DockStyle.Fill
        spaceLabel.AutoSize = True
        spaceLabel.Padding = New Padding(0, 6, 0, 0)
        layout.Controls.Add(spaceLabel, 0, 3)
        targetLabel.Dock = DockStyle.Fill
        targetLabel.AutoEllipsis = True
        targetLabel.Height = 26
        targetLabel.Margin = New Padding(0, 2, 0, 4)
        UiStyles.SetRole(targetLabel, UiStyles.Role.Muted)
        layout.Controls.Add(targetLabel, 0, 4)
        notice.AutoSize = True
        notice.Dock = DockStyle.Fill
        notice.Margin = New Padding(0, 4, 0, 8)
        notice.Visible = False
        layout.Controls.Add(notice, 0, 5)
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = True, .Margin = New Padding(0)}
        cancelButton.Text = Language.GetText("Cancel")
        backButton.Text = Language.GetText("AddLinks_Back")
        addButton.AutoSize = True
        addButton.Enabled = False
        addButton.Text = Language.GetText("Add links")
        retryButton.AutoSize = True
        retryButton.Text = Language.GetText("Preflight_Retry")
        retryButton.Enabled = False
        buttons.Controls.Add(cancelButton)
        buttons.Controls.Add(addButton)
        buttons.Controls.Add(backButton)
        buttons.Controls.Add(retryButton)
        For Each button As Button In New Button() {cancelButton, addButton, backButton, retryButton, stopButton}
            button.AutoSize = True
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink
            button.Padding = New Padding(10, 4, 10, 4)
            button.MinimumSize = New Size(0, 34)
        Next
        UiStyles.SetRole(buttons, UiStyles.Role.Surface)
        UiStyles.SetRole(addButton, UiStyles.Role.PrimaryButton)
        layout.Controls.Add(buttons, 0, 6)
        Controls.Add(layout)
        AddHandler search.TextChanged, Sub()
                                           filterTimer.Stop()
                                           filterTimer.Start()
                                       End Sub
        AddHandler search.KeyDown, Sub(sender, e)
                                      If e.KeyCode <> Keys.Enter Then Return
                                      filterTimer.Stop()
                                      BindRows()
                                      e.Handled = True
                                      e.SuppressKeyPress = True
                                  End Sub
        AddHandler filterTimer.Tick, Sub()
                                         filterTimer.Stop()
                                         BindRows()
                                     End Sub
        AddHandler category.SelectedIndexChanged, Sub() BindRows()
        AddHandler grid.CellValueNeeded, AddressOf ValueNeeded
        AddHandler grid.CellValuePushed, AddressOf ValuePushed
        AddHandler grid.CellContentClick, Sub(sender, e)
                                             If e.ColumnIndex <> 0 OrElse SelectionLocked Then Return
                                             Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
                                             If node Is Nothing OrElse node.EligibleCount = 0 Then Return
                                             ChangeCheckbox(e.RowIndex, node.CheckState <> CheckState.Checked)
                                         End Sub
        AddHandler grid.CellBeginEdit, AddressOf BeginCellEdit
        AddHandler grid.CellFormatting, AddressOf FormatCell
        AddHandler grid.CellPainting, AddressOf PaintTreeCell
        AddHandler grid.CellMouseClick, AddressOf TreeCellClick
        AddHandler grid.CellMouseDoubleClick, Sub(sender, e)
                                                       If e.ColumnIndex <> 1 OrElse e.RowIndex < 0 Then Return
                                                       Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
                                                       If node IsNot Nothing AndAlso node.IsFolder AndAlso Not OnExpander(node, e.X) Then ToggleFolder(e.RowIndex)
                                                   End Sub
        AddHandler grid.CellToolTipTextNeeded, Sub(sender, e)
                                                  Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
                                                  If node Is Nothing Then Return
                                                  If node.IsFolder Then
                                                      e.ToolTipText = Language.GetText("Preflight_FolderHint")
                                                  ElseIf e.ColumnIndex = 3 Then
                                                      e.ToolTipText = If(node.Entry.Detail, "")
                                                  ElseIf e.ColumnIndex = 1 Then
                                                      e.ToolTipText = node.Entry.Name
                                                  End If
                                              End Sub
        AddHandler grid.CurrentCellDirtyStateChanged, Sub()
                                                         If grid.IsCurrentCellDirty Then grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
                                                     End Sub
        AddHandler grid.KeyDown, AddressOf GridKeyDown
        AddHandler grid.ColumnHeaderMouseClick, Sub(sender, e)
                                                   If e.ColumnIndex = 0 Then SelectVisible(True)
                                               End Sub
        AddHandler addButton.Click, AddressOf ConfirmSelection
        AddHandler retryButton.Click, Sub() BeginInspection(True)
        AddHandler backButton.Click, Sub() RaiseEvent BackRequested(Me, EventArgs.Empty)
        AddHandler cancelButton.Click, Sub() RaiseEvent CancelRequested(Me, EventArgs.Empty)
        AddHandler stopButton.Click, Sub() StopInspection()
        AddHandler progressTimer.Tick, Sub() RefreshProgress()
        UiStyles.SetRole(Me, UiStyles.Role.Surface)
        ThemeManager.ApplyThemeToContextMenuStrip(selectionMenu)
        ThemeManager.ApplyThemeToToolTip(targetTip)
        UpdateTargetLabel()
        ApplyGridTheme()
    End Sub

    Private Sub AddSelectionAction(parent As ToolStripDropDownButton, key As String, action As Action)
        Dim item As New ToolStripMenuItem(Language.GetText(key))
        AddHandler item.Click, Sub() action()
        parent.DropDownItems.Add(item)
    End Sub

    Private Sub AddContextAction(key As String, operation As PreflightSelection.SelectionOperation)
        Dim item As New ToolStripMenuItem(Language.GetText(key))
        AddHandler item.Click, Sub() ChangeHighlighted(operation)
        selectionMenu.Items.Add(item)
    End Sub

    Public Sub StartInspection()
        BeginInspection()
    End Sub

    Public ReadOnly Property ConfirmationButton As Button
        Get
            Return addButton
        End Get
    End Property

    Public ReadOnly Property DismissButton As Button
        Get
            Return cancelButton
        End Get
    End Property

    Public ReadOnly Property ActiveRequest As PreflightSession.Request
        Get
            Return request
        End Get
    End Property

    Public Sub ShowNotice(text As String, Optional isError As Boolean = False)
        notice.Text = text
        notice.Visible = Not String.IsNullOrEmpty(text)
        UiStyles.SetRole(notice, If(isError, UiStyles.Role.ErrorText, UiStyles.Role.BodyText))
    End Sub

    Private Sub StopInspection()
        If Not loading OrElse cancellation Is Nothing Then Return
        _InspectionStopped = True
        cancellation.Cancel()
        session.Invalidate()
        stopButton.Enabled = False
        summary.Text = Language.GetText("AddLinks_StoppingCheck")
    End Sub

    Public Sub SetImporting(value As Boolean)
        importing = value
        If value Then
            filterTimer.Stop()
            selectionMenu.Close()
        End If
        grid.Enabled = Not value AndAlso Not loading
        filters.Enabled = Not value AndAlso Not loading
        tools.Enabled = Not value AndAlso Not loading
        backButton.Enabled = Not value
        retryButton.Enabled = Not value AndAlso Not loading
        cancelButton.Enabled = True
        cancelButton.Text = Language.GetText(If(value, "AddLinks_CancelImport", "Cancel"))
        stopButton.Visible = loading
        If Not value Then
            progressBar.Style = ProgressBarStyle.Continuous
            progressBar.Maximum = 1000
            progressBar.Value = 1000
            BindRows()
        End If
    End Sub

    Public Sub ShowImportProgress(completed As Integer, total As Integer, committing As Boolean, stopping As Boolean)
        If Not importing OrElse disposeRequested Then Return
        Dim key As String = If(stopping, "AddLinks_StoppingImport", If(committing, "AddLinks_Committing", "AddLinks_ImportProgress"))
        summary.Text = Language.GetText(key).Replace("%N%", completed.ToString()).Replace("%T%", total.ToString())
        progressBar.Visible = True
        progressBar.Maximum = 1000
        If committing OrElse stopping Then
            progressBar.Style = ProgressBarStyle.Marquee
        Else
            progressBar.Style = ProgressBarStyle.Continuous
            progressBar.Value = CInt(Math.Min(1000.0R, CDbl(completed) * 1000 / Math.Max(1, total)))
        End If
        addButton.Enabled = False
        addButton.Text = Language.GetText("Loading...")
        cancelButton.Enabled = Not committing AndAlso Not stopping
    End Sub

    Public Async Sub UpdateDestination(target As String)
        If disposeRequested OrElse String.Equals(destination, target, StringComparison.Ordinal) Then Return
        destination = target
        UpdateTargetLabel()
        Dim generation As Integer = System.Threading.Interlocked.Increment(spaceGeneration)
        checkingSpace = True
        freeSpace = Nothing
        UpdateSummary()
        Try
            Dim available As Long? = Await Task.Run(Function() BatchLinkPreflight.AvailableSpace(target))
            If disposeRequested OrElse generation <> spaceGeneration Then Return
            freeSpace = available
        Catch ex As Exception
            If Not disposeRequested AndAlso generation = spaceGeneration Then
                Log.WriteWarning("Checking the import destination failed: " & Log.SafeException(ex))
                ShowNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), True)
            End If
        Finally
            If Not disposeRequested AndAlso generation = spaceGeneration Then
                checkingSpace = False
                UpdateSummary()
            End If
        End Try
    End Sub

    Private Sub UpdateTargetLabel()
        targetLabel.Text = Language.GetText("AddLinks_Target").Replace("%PATH%", destination)
        targetTip.SetToolTip(targetLabel, targetLabel.Text)
    End Sub

    Private Async Sub BeginInspection(Optional force As Boolean = False)
        If loading OrElse importing OrElse disposeRequested Then Return
        Dim source As New CancellationTokenSource()
        cancellation = source
        _InspectionStopped = False
        loading = True
        addButton.Enabled = False
        retryButton.Enabled = False
        grid.Enabled = False
        filters.Enabled = False
        tools.Enabled = False
        stopButton.Text = Language.GetText("AddLinks_StopCheck")
        stopButton.Enabled = True
        stopButton.Visible = True
        ShowNotice("")
        summary.Text = Language.GetText("Preflight_Checking")
        displayedProgress = Nothing
        progressBar.Visible = True
        progressBar.Style = ProgressBarStyle.Marquee
        Dim succeeded As Boolean = False
        Dim failed As Boolean = False
        Try
            Dim token As CancellationToken = source.Token
            Dim oldEntries As List(Of BatchLinkPreflight.Entry) = entries
            If force OrElse request Is Nothing Then request = session.Prepare(If(request Is Nothing, String.Join(vbLf, urls), request.Text), force)
            progressTimer.Start()
            RefreshProgress()
            Dim waiter As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim result As List(Of BatchLinkPreflight.Entry)
            Using registration As CancellationTokenRegistration = token.Register(Sub() waiter.TrySetCanceled())
                Await Task.WhenAny(request.Results, waiter.Task)
                token.ThrowIfCancellationRequested()
                result = Await request.Results
            End Using
            Dim currentQueue As Dictionary(Of String, URLProcessor.FileURL) = queued
            If queueProvider IsNot Nothing Then currentQueue = Await Task.Run(queueProvider, token)
            progressTimer.Stop()
            summary.Text = Language.GetText("Preflight_BuildingTree")
            progressBar.Style = ProgressBarStyle.Marquee
            Dim prepared As Tuple(Of PreflightSelection, PreflightTree) = Await Task.Run(Function()
                                                                    Dim previous As New Dictionary(Of String, Boolean)(StringComparer.Ordinal)
                                                                    For Each entry As BatchLinkPreflight.Entry In oldEntries
                                                                        Dim key As String = BatchLinkPreflight.Identity(entry.Source)
                                                                        If key IsNot Nothing AndAlso entry.CanAdd AndAlso Not previous.ContainsKey(key) Then previous.Add(key, entry.Selected)
                                                                    Next
                                                                    Dim updated As List(Of BatchLinkPreflight.Entry) = PreflightSession.Reconcile(result, currentQueue)
                                                                    For Each entry As BatchLinkPreflight.Entry In updated
                                                                        Dim key As String = BatchLinkPreflight.Identity(entry.Source)
                                                                        If key IsNot Nothing AndAlso previous.ContainsKey(key) Then entry.Selected = entry.CanAdd AndAlso previous(key)
                                                                    Next
                                                                    token.ThrowIfCancellationRequested()
                                                                    Dim model As New PreflightSelection(updated)
                                                                    Return Tuple.Create(model, New PreflightTree(model))
                                                                End Function, token)
            Dim target As String = destination
            Dim targetGeneration As Integer = spaceGeneration
            Dim available As Long? = Await Task.Run(Function() BatchLinkPreflight.AvailableSpace(target), token)
            If disposeRequested OrElse token.IsCancellationRequested Then Return
            selection = prepared.Item1
            tree = prepared.Item2
            entries = selection.Entries
            If targetGeneration = spaceGeneration Then freeSpace = available
            If entries.Count = 0 Then ShowNotice(Language.GetText("Links not valid"), True)
            succeeded = True
        Catch ex As OperationCanceledException
            Return
        Catch ex As Exception
            failed = True
            If Not disposeRequested Then ShowNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), True)
        Finally
            loading = False
            If Object.ReferenceEquals(cancellation, source) Then cancellation = Nothing
            source.Dispose()
            If Not disposeRequested Then
                progressTimer.Stop()
                progressBar.Style = ProgressBarStyle.Continuous
                progressBar.Maximum = 1000
                progressBar.Value = If(succeeded, 1000, 0)
                grid.Enabled = True
                filters.Enabled = True
                tools.Enabled = True
                stopButton.Visible = False
                retryButton.Enabled = True
                BindRows()
                If failed Then summary.Text = Language.GetText("AddLinks_CheckFailed")
                If InspectionStopped Then
                    summary.Text = Language.GetText("AddLinks_CheckStopped")
                    ShowNotice(Language.GetText("AddLinks_CheckStopped"))
                End If
            End If
        End Try
    End Sub

    Private Sub RefreshProgress()
        If disposeRequested OrElse Not loading OrElse request Is Nothing OrElse InspectionStopped Then Return
        Dim snapshot As PreflightProgress = request.Progress
        Dim second As Long = CLng(Math.Max(0, Math.Floor((DateTime.UtcNow - request.Started).TotalSeconds)))
        If Object.ReferenceEquals(snapshot, displayedProgress) AndAlso second = displayedProgressSecond Then Return
        displayedProgress = snapshot
        displayedProgressSecond = second
        PreflightProgress.Display(snapshot, summary, progressBar, request.Started)
    End Sub

    Private Sub ApplyGridTheme()
        If disposeRequested Then Return
        grid.EnableHeadersVisualStyles = False
        grid.BackgroundColor = ThemeManager.GetColor("Back")
        grid.GridColor = ThemeManager.GetColor("Border")
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.DefaultCellStyle.BackColor = ThemeManager.GetColor("Back")
        grid.DefaultCellStyle.ForeColor = ThemeManager.GetColor("Fore")
        grid.DefaultCellStyle.SelectionBackColor = ThemeManager.GetColor("Selection")
        grid.DefaultCellStyle.SelectionForeColor = ThemeManager.GetColor("SelectionFore")
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.GetColor("ControlBack")
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.GetColor("Fore")
        ThemeManager.ApplyThemeToContextMenuStrip(selectionMenu)
        ThemeManager.ApplyThemeToToolTip(targetTip)
    End Sub

    Protected Overrides Sub OnBackColorChanged(e As EventArgs)
        MyBase.OnBackColorChanged(e)
        If grid IsNot Nothing AndAlso grid.Columns.Count > 0 Then ApplyGridTheme()
    End Sub

    Private Sub BindRows()
        If SelectionLocked Then Return
        If selection Is Nothing OrElse Not Object.ReferenceEquals(selection.Entries, entries) Then
            selection = New PreflightSelection(entries)
            tree = Nothing
        End If
        If tree Is Nothing Then tree = New PreflightTree(selection)
        Dim categories As String() = {"", "Video", "Audio", "Archives", "Documents", "Other"}
        selection.Filter(search.Text, categories(Math.Max(0, category.SelectedIndex)))
        tree.Filter(search.Text.Length > 0 OrElse category.SelectedIndex > 0)
        BindTreeRows()
        UpdateSummary()
    End Sub

    Private Sub BindTreeRows(Optional currentNode As PreflightTree.Node = Nothing)
        ' The model projection may already have changed. Read previous indices from
        ' the last projection, not from the newly expanded/filtered tree.
        Dim highlighted As New HashSet(Of PreflightTree.Node)()
        Dim index As Integer = grid.Rows.GetFirstRow(DataGridViewElementStates.Selected)
        While index >= 0
            If index < boundRows.Count Then highlighted.Add(boundRows(index))
            index = grid.Rows.GetNextRow(index, DataGridViewElementStates.Selected)
        End While
        If currentNode Is Nothing AndAlso grid.CurrentCell IsNot Nothing AndAlso grid.CurrentCell.RowIndex < boundRows.Count Then
            currentNode = boundRows(grid.CurrentCell.RowIndex)
        End If
        Dim topIndex As Integer = If(grid.IsHandleCreated, grid.FirstDisplayedScrollingRowIndex, -1)
        Dim topNode As PreflightTree.Node = If(topIndex >= 0 AndAlso topIndex < boundRows.Count, boundRows(topIndex), Nothing)
        binding = True
        grid.SuspendLayout()
        Try
            grid.CurrentCell = Nothing
            grid.RowCount = tree.Rows.Count
            grid.ClearSelection()
            boundRows = New List(Of PreflightTree.Node)(tree.Rows)
            Dim currentIndex As Integer = -1
            Dim nextTop As Integer = Math.Min(topIndex, boundRows.Count - 1)
            For row As Integer = 0 To boundRows.Count - 1
                Dim node As PreflightTree.Node = boundRows(row)
                If Object.ReferenceEquals(node, currentNode) Then currentIndex = row
                If Object.ReferenceEquals(node, topNode) Then nextTop = row
            Next
            If currentIndex >= 0 Then grid.CurrentCell = grid(1, currentIndex)
            grid.ClearSelection()
            For row As Integer = 0 To boundRows.Count - 1
                If highlighted.Contains(boundRows(row)) Then grid.Rows(row).Selected = True
            Next
            If highlighted.Count = 0 AndAlso currentIndex >= 0 Then grid.Rows(currentIndex).Selected = True
            If grid.IsHandleCreated AndAlso grid.DisplayedRowCount(False) > 0 AndAlso nextTop >= 0 Then grid.FirstDisplayedScrollingRowIndex = nextTop
            grid.Invalidate()
        Finally
            grid.ResumeLayout()
            binding = False
        End Try
    End Sub

    Private Function NodeAt(index As Integer) As PreflightTree.Node
        If tree Is Nothing OrElse index < 0 OrElse index >= tree.Rows.Count Then Return Nothing
        Return tree.Rows(index)
    End Function

    Private Function TreeIndent(node As PreflightTree.Node) As Integer
        Return Math.Min(node.Depth, 16) * TreeStep()
    End Function

    Private Function TreeStep() As Integer
        Return Math.Max(CInt(20 * grid.DeviceDpi / 96.0R), grid.Font.Height + 4)
    End Function

    Private Function OnExpander(node As PreflightTree.Node, x As Integer) As Boolean
        Return x >= TreeIndent(node) AndAlso x < TreeIndent(node) + TreeStep()
    End Function

    Private Sub PaintTreeCell(sender As Object, e As DataGridViewCellPaintingEventArgs)
        If e.ColumnIndex <> 1 OrElse e.RowIndex < 0 Then Return
        Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
        If node Is Nothing OrElse Not node.IsFolder Then Return
        e.Paint(e.ClipBounds, e.PaintParts)
        Dim side As Integer = Math.Max(10, CInt(11 * grid.DeviceDpi / 96.0R))
        Dim glyph As New Rectangle(e.CellBounds.Left + TreeIndent(node) + CInt(3 * grid.DeviceDpi / 96.0R), e.CellBounds.Top + (e.CellBounds.Height - side) \ 2, side, side)
        Dim color As Color = If((e.State And DataGridViewElementStates.Selected) <> 0, e.CellStyle.SelectionForeColor, e.CellStyle.ForeColor)
        Dim state As Drawing2D.GraphicsState = e.Graphics.Save()
        Try
            e.Graphics.SetClip(Rectangle.Intersect(e.CellBounds, e.ClipBounds), Drawing2D.CombineMode.Intersect)
            Using pen As New Pen(color)
                e.Graphics.DrawRectangle(pen, glyph)
                e.Graphics.DrawLine(pen, glyph.Left + 2, glyph.Top + side \ 2, glyph.Right - 2, glyph.Top + side \ 2)
                If Not tree.IsExpanded(node) Then e.Graphics.DrawLine(pen, glyph.Left + side \ 2, glyph.Top + 2, glyph.Left + side \ 2, glyph.Bottom - 2)
            End Using
        Finally
            e.Graphics.Restore(state)
        End Try
        e.Handled = True
    End Sub

    Private Sub TreeCellClick(sender As Object, e As DataGridViewCellMouseEventArgs)
        If SelectionLocked OrElse e.Button <> MouseButtons.Left OrElse e.ColumnIndex <> 1 OrElse e.RowIndex < 0 OrElse e.Clicks > 1 Then Return
        Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
        If node IsNot Nothing AndAlso node.IsFolder AndAlso OnExpander(node, e.X) Then ToggleFolder(e.RowIndex)
    End Sub

    Private Sub ToggleFolder(index As Integer)
        If SelectionLocked OrElse tree Is Nothing Then Return
        Dim node As PreflightTree.Node = NodeAt(index)
        If node Is Nothing OrElse Not node.IsFolder Then Return
        tree.Toggle(node)
        BindTreeRows(node)
    End Sub

    Private Sub ExpandFolders(expand As Boolean)
        If SelectionLocked OrElse tree Is Nothing Then Return
        If filterTimer.Enabled Then
            filterTimer.Stop()
            BindRows()
        End If
        tree.ExpandAll(expand)
        BindTreeRows()
    End Sub

    Private Sub ValueNeeded(sender As Object, e As DataGridViewCellValueEventArgs)
        Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
        If node Is Nothing Then Return
        If node.IsFolder Then
            Select Case e.ColumnIndex
                Case 0 : e.Value = node.CheckState
                Case 1 : e.Value = node.Name
                Case 2 : e.Value = If(node.UnknownSizes = node.MatchCount, "—", FormatSize(node.SizeBytes) & If(node.UnknownSizes > 0, "+", ""))
                Case 3 : e.Value = folderSelectionLabel.Replace("%S%", node.SelectedCount.ToString()).Replace("%N%", node.MatchCount.ToString())
                Case 4 : e.Value = ""
            End Select
            Return
        End If
        Dim entry As BatchLinkPreflight.Entry = node.Entry
        Select Case e.ColumnIndex
            Case 0 : e.Value = If(entry.Selected, CheckState.Checked, CheckState.Unchecked)
            Case 1 : e.Value = entry.Name
            Case 2 : e.Value = If(entry.SizeBytes.HasValue, FormatSize(entry.SizeBytes.GetValueOrDefault()), "—")
            Case 3 : e.Value = statusLabels(entry.State)
            Case 4 : e.Value = If(entry.Source Is Nothing, "", entry.Source.Path)
        End Select
    End Sub

    Private Sub BeginCellEdit(sender As Object, e As DataGridViewCellCancelEventArgs)
        Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
        If SelectionLocked OrElse node Is Nothing OrElse node.EligibleCount = 0 Then e.Cancel = True
    End Sub

    Private Sub FormatCell(sender As Object, e As DataGridViewCellFormattingEventArgs)
        Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
        If node Is Nothing Then Return
        e.CellStyle.ForeColor = If(node.EligibleCount = 0, ThemeManager.GetColor("Muted"), ThemeManager.GetColor("Fore"))
        If e.ColumnIndex = 1 Then e.CellStyle.Padding = New Padding(TreeIndent(node) + TreeStep(), 0, 0, 0)
        If node.IsFolder Then
            e.CellStyle.BackColor = ThemeManager.GetColor("ControlBack")
        ElseIf e.ColumnIndex = 3 AndAlso node.Entry.State = BatchLinkPreflight.LinkState.Invalid Then
            e.CellStyle.ForeColor = ThemeManager.GetColor("ErrorFore")
        End If
    End Sub

    Private Sub ValuePushed(sender As Object, e As DataGridViewCellValueEventArgs)
        If binding OrElse SelectionLocked OrElse e.ColumnIndex <> 0 Then Return
        Dim node As PreflightTree.Node = NodeAt(e.RowIndex)
        If node Is Nothing Then Return
        ' A mixed folder becomes fully checked; a fully checked folder is cleared.
        Dim value As Boolean = If(TypeOf e.Value Is Boolean, CBool(e.Value), CType(e.Value, CheckState) <> CheckState.Unchecked)
        ChangeCheckbox(e.RowIndex, value)
    End Sub

    Private Sub ChangeCheckbox(index As Integer, value As Boolean)
        Dim node As PreflightTree.Node = NodeAt(index)
        If node Is Nothing OrElse SelectionLocked Then Return
        If (grid.Rows.GetRowState(index) And DataGridViewElementStates.Selected) <> 0 Then
            ChangeHighlighted(If(value, PreflightSelection.SelectionOperation.SelectAll, PreflightSelection.SelectionOperation.Clear))
        Else
            tree.Apply(New PreflightTree.Node() {node}, If(value, PreflightSelection.SelectionOperation.SelectAll, PreflightSelection.SelectionOperation.Clear))
            UpdateSummary()
        End If
        grid.Invalidate()
    End Sub

    Private Sub GridKeyDown(sender As Object, e As KeyEventArgs)
        If SelectionLocked Then Return
        If (e.KeyCode = Keys.Right OrElse e.KeyCode = Keys.Left) AndAlso grid.CurrentCell IsNot Nothing AndAlso grid.CurrentCell.ColumnIndex = 1 Then
            Dim node As PreflightTree.Node = NodeAt(grid.CurrentCell.RowIndex)
            If node Is Nothing Then Return
            If node.IsFolder AndAlso tree.IsExpanded(node) <> (e.KeyCode = Keys.Right) Then
                ToggleFolder(grid.CurrentCell.RowIndex)
            ElseIf e.KeyCode = Keys.Left AndAlso node.Parent IsNot Nothing Then
                Dim parentIndex As Integer = tree.Rows.IndexOf(node.Parent)
                If parentIndex < 0 Then Return
                grid.CurrentCell = grid(1, parentIndex)
            Else
                Return
            End If
        ElseIf e.KeyCode = Keys.Space Then
            ChangeHighlighted(PreflightSelection.SelectionOperation.Invert)
        ElseIf e.Control AndAlso e.KeyCode = Keys.I Then
            ApplySelection(PreflightSelection.SelectionOperation.Invert, True)
        Else
            Return
        End If
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub ChangeHighlighted(operation As PreflightSelection.SelectionOperation)
        If selection Is Nothing OrElse SelectionLocked Then Return
        Dim targets As New List(Of PreflightTree.Node)()
        Dim index As Integer = grid.Rows.GetFirstRow(DataGridViewElementStates.Selected)
        While index >= 0
            Dim node As PreflightTree.Node = NodeAt(index)
            If node IsNot Nothing Then targets.Add(node)
            index = grid.Rows.GetNextRow(index, DataGridViewElementStates.Selected)
        End While
        tree.Apply(targets, operation)
        grid.Invalidate()
        UpdateSummary()
    End Sub

    Private Sub SelectVisible(selected As Boolean)
        ApplySelection(If(selected, PreflightSelection.SelectionOperation.SelectAll, PreflightSelection.SelectionOperation.Clear), True)
    End Sub

    Private Sub ApplySelection(operation As PreflightSelection.SelectionOperation, visibleOnly As Boolean)
        If selection Is Nothing OrElse SelectionLocked Then Return
        If visibleOnly AndAlso filterTimer.Enabled Then
            filterTimer.Stop()
            BindRows()
        End If
        selection.Apply(operation, visibleOnly)
        grid.Invalidate()
        UpdateSummary()
    End Sub

    Private Sub UpdateSummary()
        If selection Is Nothing OrElse loading OrElse importing OrElse disposeRequested Then Return
        summary.Text = Language.GetText("Preflight_Summary").Replace("%N%", entries.Count.ToString()).Replace("%S%", selection.SelectedCount.ToString()).Replace("%D%", selection.DuplicateCount.ToString()).Replace("%E%", selection.InvalidCount.ToString()).Replace("%U%", selection.UnavailableCount.ToString())
        spaceLabel.Text = Language.GetText("Preflight_Total").Replace("%SIZE%", FormatSize(selection.SelectedBytes)).Replace("%FREE%", If(freeSpace.HasValue, FormatSize(freeSpace.GetValueOrDefault()), "—"))
        If selection.UnknownSizes > 0 Then spaceLabel.Text &= " · " & Language.GetText("Preflight_UnknownSizes").Replace("%N%", selection.UnknownSizes.ToString())
        Dim noSpace As Boolean = freeSpace.HasValue AndAlso selection.SelectedBytes > CDec(freeSpace.GetValueOrDefault())
        If noSpace Then spaceLabel.Text &= " · " & Language.GetText("Preflight_NoSpace")
        UiStyles.SetRole(spaceLabel, If(noSpace, UiStyles.Role.ErrorText, UiStyles.Role.BodyText))
        addButton.Enabled = Not checkingSpace AndAlso selection.SelectedCount > 0 AndAlso Not noSpace
        addButton.Text = Language.GetText("Preflight_AddSelected").Replace("%N%", selection.SelectedCount.ToString())
    End Sub

    Private Sub ConfirmSelection(sender As Object, e As EventArgs)
        If SelectionLocked OrElse Not addButton.Enabled Then Return
        If filterTimer.Enabled Then
            filterTimer.Stop()
            BindRows()
        End If
        If Not addButton.Enabled Then Return
        _SelectedFiles = entries.Where(Function(entry) entry.CanAdd AndAlso entry.Selected).Select(Function(entry) entry.Source).ToList()
        If _SelectedFiles.Count = 0 Then Return
        RaiseEvent ConfirmRequested(Me, EventArgs.Empty)
    End Sub

    Private Shared Function FormatSize(bytes As Decimal) As String
        Dim units As String() = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}
        Dim index As Integer
        While bytes >= 1024D AndAlso index < units.Length - 1
            bytes /= 1024D
            index += 1
        End While
        Return bytes.ToString(If(index = 0, "F0", "F2")) & " " & units(index)
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso Not disposeRequested Then
            disposeRequested = True
            filterTimer.Dispose()
            progressTimer.Dispose()
            selectionMenu.Dispose()
            targetTip.Dispose()
            If cancellation IsNot Nothing Then cancellation.Cancel()
            If ownsSession Then session.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
