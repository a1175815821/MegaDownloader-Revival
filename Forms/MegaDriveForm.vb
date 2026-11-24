Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Friend NotInheritable Class MegaDriveForm
    Inherits Form

    Private NotInheritable Class AccountChoice
        Friend ReadOnly Status As MegaAccountService.Status

        Friend Sub New(status As MegaAccountService.Status)
            Me.Status = status
        End Sub

        Public Overrides Function ToString() As String
            Dim caption = If(Status.DisplayName = Status.Email, Status.Email, Status.DisplayName & " (" & Status.Email & ")")
            Return If(Status.NeedsLogin, caption & " · " & Language.GetText("MegaAccount_Reconnect"), caption)
        End Function
    End Class

    Private NotInheritable Class AccountDrive
        Friend Snapshot As MegaDriveSnapshot
        Friend ReadOnly Preferences As MegaDrivePreferences
        Private ReadOnly personalSelection As New HashSet(Of ULong)()
        Private ReadOnly sharedSelection As New HashSet(Of ULong)()
        Friend Folder As ULong
        Friend Incoming As Boolean

        Friend ReadOnly Property Selected As HashSet(Of ULong)
            Get
                Return If(Incoming, sharedSelection, personalSelection)
            End Get
        End Property

        Friend Sub New(preferences As MegaDrivePreferences)
            Me.Preferences = preferences
        End Sub
    End Class

    Private ReadOnly ownerMain As Main
    Private ReadOnly config As Configuracion
    Private ReadOnly service As MegaAccountService
    Private ReadOnly drives As New Dictionary(Of String, AccountDrive)(StringComparer.OrdinalIgnoreCase)
    Private Const BackgroundBrowseCount As Integer = 50000
    Private ReadOnly rowBuildGate As New SemaphoreSlim(1)
    Private currentEmail As String = ""
    Private drive As AccountDrive
    Private loadCancellation As CancellationTokenSource
    Private rowBuildCancellation As CancellationTokenSource
    Private changingAccount As Boolean
    Private showingSelected As Boolean
    Private submitting As Boolean
    Private sortByTime As Boolean
    Private descending As Boolean
    Private shownNodes As MegaDriveNode() = {}
    Private pendingFocusNode As ULong?
    Private selectionAnchor As Integer = -1
    Private loadMessage As String = ""

    Private ReadOnly accounts As New ComboBox With {.Name = "MegaDriveAccount", .DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
    Private ReadOnly driveSource As New ComboBox With {.Name = "MegaDriveSource", .DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
    Private ReadOnly scope As New ComboBox With {.Name = "MegaDriveScope", .DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
    Private ReadOnly typeFilter As New ComboBox With {.Name = "MegaDriveType", .DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
    Private ReadOnly search As New TextBox With {.Name = "MegaDriveSearch"}
    Private ReadOnly clearSearch As CommandButton = ButtonFor("UI_ClearSearch", UiStyles.Role.QuietButton)
    Private ReadOnly searchFrame As SearchFrame
    Private ReadOnly crumbs As New FlowLayoutPanel With {.Name = "MegaDriveBreadcrumbs", .Dock = DockStyle.Fill, .WrapContents = False}
    Private ReadOnly up As CommandButton = ButtonFor("MegaDrive_Up", UiStyles.Role.QuietButton)
    Private ReadOnly favorites As CommandButton = ButtonFor("MegaDrive_Favorites", UiStyles.Role.SecondaryButton)
    Private ReadOnly checkNew As CommandButton = ButtonFor("MegaDrive_CheckNew", UiStyles.Role.SecondaryButton)
    Private ReadOnly refreshButton As CommandButton = ButtonFor("MegaDrive_Refresh", UiStyles.Role.QuietButton)
    Private ReadOnly manage As CommandButton = ButtonFor("MegaAccount_Manage", UiStyles.Role.QuietButton)
    Private ReadOnly bulk As CommandButton = ButtonFor("MegaDrive_BulkSelection", UiStyles.Role.QuietButton)
    Private ReadOnly review As CommandButton = ButtonFor("MegaDrive_ViewSelected", UiStyles.Role.SecondaryButton)
    Private ReadOnly add As CommandButton = ButtonFor("MegaDrive_Download", UiStyles.Role.PrimaryButton)
    Private ReadOnly dismiss As CommandButton = ButtonFor("Close", UiStyles.Role.QuietButton)
    Private ReadOnly status As New Label With {.Name = "MegaDriveStatus", .Dock = DockStyle.Fill, .AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft}
    Private ReadOnly grid As New DataGridView With {.Name = "MegaDriveFiles", .Dock = DockStyle.Fill, .VirtualMode = True,
        .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .AllowUserToResizeRows = False, .RowHeadersVisible = False,
        .MultiSelect = False, .SelectionMode = DataGridViewSelectionMode.FullRowSelect, .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        .AutoGenerateColumns = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
    Private ReadOnly searchTimer As New Windows.Forms.Timer With {.Interval = 160}

    Friend Sub New(main As Main, configuration As Configuracion, accountService As MegaAccountService, Optional initialEmail As String = Nothing)
        ownerMain = main
        config = configuration
        service = accountService
        Text = Language.GetText("MegaDrive_Title")
        Name = "MegaDriveForm"
        StartPosition = FormStartPosition.CenterParent
        ShowInTaskbar = False
        MinimizeBox = False
        UiStyles.InitializeForm(Me)
        ClientSize = New Size(940, 626)
        MinimumSize = New Size(700, 460)

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(20)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For Each height As Integer In {42, 38, 42}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))

        accounts.AccessibleName = Language.GetText("MegaAccount_Title")
        Dim accountLabel As New Label With {.Text = accounts.AccessibleName, .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 0, 8, 0)}
        driveSource.Items.AddRange({Language.GetText("MegaDrive_Title"), Language.GetText("MegaDrive_IncomingShares")})
        driveSource.SelectedIndex = 0
        driveSource.AccessibleName = Language.GetText("MegaDrive_Location")
        layout.Controls.Add(Bar(accounts, accountLabel, accounts, driveSource, manage, refreshButton), 0, 0)
        layout.Controls.Add(Bar(crumbs, up, crumbs, checkNew, favorites), 0, 1)
        scope.Items.AddRange({Language.GetText("MegaDrive_CurrentFolder"), Language.GetText("MegaDrive_WholeDrive")})
        scope.AccessibleName = Language.GetText("MegaDrive_SearchScope")
        scope.SelectedIndex = 0
        For Each key In {"MegaDrive_AllTypes", "MegaDrive_Videos", "MegaDrive_Archives", "MegaDrive_Documents", "MegaDrive_Images", "MegaDrive_Audio", "MegaDrive_Other"}
            typeFilter.Items.Add(Language.GetText(key))
        Next
        typeFilter.AccessibleName = Language.GetText("MegaDrive_FileType")
        typeFilter.SelectedIndex = 0
        search.AccessibleName = Language.GetText("MegaDrive_Search")
        clearSearch.Text = "×"
        clearSearch.TabStop = False
        searchFrame = New SearchFrame(search, clearSearch) With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 4)}
        AddHandler search.HandleCreated, Sub() UpdateSearchCue()
        layout.Controls.Add(Bar(searchFrame, searchFrame, scope, typeFilter), 0, 2)

        grid.AccessibleName = Text
        grid.RowTemplate.Height = 30
        grid.ColumnHeadersHeight = 32
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        grid.Columns.Add(New DataGridViewCheckBoxColumn With {.Name = "Selected", .HeaderText = "✓", .ToolTipText = Language.GetText("MegaDrive_ViewSelected"),
                         .AutoSizeMode = DataGridViewAutoSizeColumnMode.None, .Width = 38, .Resizable = DataGridViewTriState.False})
        grid.Columns.Add(TextColumn("Name", "Name", 112, sortable:=True))
        grid.Columns.Add(TextColumn("Size", "Size", 32))
        grid.Columns.Add(TextColumn("Modified", "MegaDrive_Modified", 59, sortable:=True))
        grid.Columns.Add(TextColumn("Location", "MegaDrive_Location", 76))
        grid.Columns.Add(New DataGridViewButtonColumn With {.Name = "Jump", .HeaderText = "", .ReadOnly = True,
                         .Text = Language.GetText("MegaDrive_GoToFolder"), .UseColumnTextForButtonValue = True,
                         .AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                         .Width = TextRenderer.MeasureText(Language.GetText("MegaDrive_GoToFolder"), Font).Width + 24})
        grid.Columns("Size").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        grid.Margin = New Padding(0, 0, 0, 4)
        layout.Controls.Add(grid, 0, 3)
        status.Margin = New Padding(0, 0, 12, 0)
        UiStyles.SetRole(status, UiStyles.Role.Muted)
        layout.Controls.Add(Bar(status, status, bulk, review, add, dismiss), 0, 4)
        Controls.Add(layout)
        CancelButton = dismiss
        dismiss.DialogResult = DialogResult.Cancel
        ThemeManager.ApplyTheme(Me)

        AddHandler accounts.SelectedIndexChanged, Sub() ChooseAccount()
        AddHandler driveSource.SelectedIndexChanged, Sub()
                                                         showingSelected = False
                                                         search.Clear()
                                                         scope.SelectedIndex = 0
                                                         LoadDrive()
                                                     End Sub
        AddHandler checkNew.Click, AddressOf CheckBoundFolder
        AddHandler manage.Click, AddressOf ManageAccounts
        AddHandler refreshButton.Click, Sub() LoadDrive()
        AddHandler favorites.Click, AddressOf ShowFavorites
        AddHandler up.Click, Sub() Navigate(drive.Snapshot.Find(drive.Folder).ParentId)
        AddHandler clearSearch.Click, Sub() search.Clear()
        AddHandler search.TextChanged, Sub()
                                           searchTimer.Stop()
                                           searchTimer.Start()
                                       End Sub
        AddHandler searchTimer.Tick, Sub() RefreshRows()
        AddHandler typeFilter.SelectedIndexChanged, Sub() RefreshRows()
        AddHandler scope.SelectedIndexChanged, Sub()
                                                  UpdateSearchCue()
                                                  RefreshRows()
                                              End Sub
        AddHandler crumbs.SizeChanged, Sub() RefreshBreadcrumbs()
        AddHandler grid.CellValueNeeded, AddressOf ReadCell
        AddHandler grid.CellValuePushed, AddressOf UpdateSelection
        AddHandler grid.CurrentCellDirtyStateChanged, Sub()
                                                          If grid.IsCurrentCellDirty Then grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
                                                      End Sub
        AddHandler grid.ColumnHeaderMouseClick, AddressOf ChangeSort
        AddHandler grid.CellContentClick, Sub(sender, e)
                                              If e.RowIndex < 0 OrElse e.ColumnIndex <> grid.Columns("Jump").Index Then Return
                                              Dim node = shownNodes(e.RowIndex)
                                              Navigate(node.ParentId, node.Id)
                                          End Sub
        AddHandler grid.CellDoubleClick, Sub(sender, e)
                                             If e.RowIndex >= 0 AndAlso e.ColumnIndex > 0 AndAlso e.ColumnIndex <> grid.Columns("Jump").Index Then OpenNode(shownNodes(e.RowIndex))
                                         End Sub
        AddHandler grid.KeyDown, Sub(sender, e)
                                    If e.KeyCode <> Keys.Enter OrElse grid.CurrentCell Is Nothing OrElse grid.CurrentCell.ColumnIndex = grid.Columns("Jump").Index Then Return
                                    e.SuppressKeyPress = True
                                    OpenNode(shownNodes(grid.CurrentCell.RowIndex))
                                End Sub
        AddHandler bulk.Click, AddressOf ShowBulkActions
        AddHandler review.Click, Sub()
                                     showingSelected = Not showingSelected
                                     RefreshRows()
                                 End Sub
        AddHandler add.Click, Sub() ConfirmDownload(drive.Selected)
        AddHandler Shown, Sub() StartBrowsing(initialEmail)
        AddHandler FormClosing, Sub(sender, e) e.Cancel = submitting
        AddHandler FormClosed, Sub()
                                   If loadCancellation IsNot Nothing Then loadCancellation.Cancel()
                                   SavePreferences()
                                   searchTimer.Stop()
                               End Sub
        AddHandler Disposed, Sub()
                                 If rowBuildCancellation IsNot Nothing Then rowBuildCancellation.Cancel()
                                 searchTimer.Dispose()
                             End Sub
        RefreshRows()
    End Sub

    Private Shared Function ButtonFor(key As String, role As UiStyles.Role) As CommandButton
        Dim button As New CommandButton With {.Text = Language.GetText(key), .AutoSize = True, .AutoEllipsis = True,
            .Padding = New Padding(8, 3, 8, 3), .Margin = New Padding(3, 0, 3, 4), .Anchor = AnchorStyles.Left}
        UiStyles.SetRole(button, role)
        Return button
    End Function

    Private Shared Function Bar(stretch As Control, ParamArray controls() As Control) As TableLayoutPanel
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = controls.Length, .RowCount = 1, .Margin = Padding.Empty}
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        For index = 0 To controls.Length - 1
            Dim item = controls(index)
            layout.ColumnStyles.Add(If(item Is stretch, New ColumnStyle(SizeType.Percent, 100), New ColumnStyle(SizeType.AutoSize)))
            If TypeOf item Is ComboBox Then item.Anchor = AnchorStyles.Left Or AnchorStyles.Right
            layout.Controls.Add(item, index, 0)
        Next
        Return layout
    End Function

    Private Shared Function TextColumn(name As String, key As String, weight As Single, Optional sortable As Boolean = False) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn With {.Name = name, .HeaderText = Language.GetText(key), .FillWeight = weight,
            .SortMode = If(sortable, DataGridViewColumnSortMode.Programmatic, DataGridViewColumnSortMode.NotSortable), .ReadOnly = True}
    End Function

    Private Sub StartBrowsing(initialEmail As String)
        Dim ui = SynchronizationContext.Current
        Dim changed As EventHandler = Sub(sender, e)
                                          ui.Post(Sub(state)
                                                      If Not IsDisposed Then SetAccounts(If(currentEmail = "", service.DefaultEmail, currentEmail), reload:=False)
                                                  End Sub, Nothing)
                                      End Sub
        AddHandler service.Changed, changed
        AddHandler Disposed, Sub() RemoveHandler service.Changed, changed
        SetAccounts(If(initialEmail, service.DefaultEmail))
    End Sub

    Private Sub SetAccounts(preferred As String, Optional reload As Boolean = True)
        Dim roster = service.GetAccounts()
        Dim available = roster.Where(Function(item) item.SignedIn AndAlso Not item.Connecting).ToList()
        Dim expired = roster.Find(Function(item) item.NeedsLogin AndAlso String.Equals(item.Email, preferred, StringComparison.OrdinalIgnoreCase))
        If available.Count = 0 AndAlso String.IsNullOrEmpty(expired.Email) Then expired = roster.Find(Function(item) item.NeedsLogin)
        If Not String.IsNullOrEmpty(expired.Email) AndAlso (currentEmail <> "" OrElse available.Count = 0) Then available.Add(expired)
        Dim choices = available.Select(Function(item) New AccountChoice(item)).ToArray()
        Dim same = choices.Select(Function(item) item.ToString()).SequenceEqual(
                   accounts.Items.Cast(Of AccountChoice)().Select(Function(item) item.ToString()))
        If Not reload AndAlso same Then
            If drive Is Nothing Then LoadDrive()
            Return
        End If

        Dim previous = TryCast(accounts.SelectedItem, AccountChoice)
        changingAccount = True
        Try
            accounts.Items.Clear()
            For Each choice In choices
                Dim index = accounts.Items.Add(choice)
                If String.Equals(choice.Status.Email, preferred, StringComparison.OrdinalIgnoreCase) Then accounts.SelectedIndex = index
            Next
            If accounts.SelectedIndex < 0 AndAlso accounts.Items.Count > 0 Then accounts.SelectedIndex = 0
        Finally
            changingAccount = False
        End Try
        Dim selected = TryCast(accounts.SelectedItem, AccountChoice)
        ChooseAccount(reload OrElse (previous IsNot Nothing AndAlso selected IsNot Nothing AndAlso previous.Status.NeedsLogin <> selected.Status.NeedsLogin))
    End Sub

    Private Sub ChooseAccount(Optional reload As Boolean = True)
        If changingAccount Then Return
        Dim choice = TryCast(accounts.SelectedItem, AccountChoice)
        Dim email = If(choice Is Nothing, "", choice.Status.Email)
        If email <> currentEmail Then
            reload = True
            SavePreferences()
            If drive IsNot Nothing Then drive.Snapshot = Nothing
            currentEmail = email
            drive = Nothing
            If email <> "" AndAlso Not drives.TryGetValue(email, drive) Then
                drive = New AccountDrive(service.GetDrivePreferences(email))
                drives.Add(email, drive)
            End If
            showingSelected = False
            search.Clear()
            scope.SelectedIndex = 0
        End If
        If reload Then LoadDrive()
    End Sub

    Private Async Sub LoadDrive()
        If loadCancellation IsNot Nothing Then loadCancellation.Cancel()
        loadCancellation = Nothing
        loadMessage = ""
        If drive Is Nothing Then
            Dim accountStatus = service.GetStatus()
            If accountStatus.LastError = MegaAccountService.BackendMissing Then
                loadMessage = DriveLoadError(accountStatus.LastError)
            ElseIf accountStatus.Connecting OrElse accountStatus.NeedsLogin OrElse accountStatus.LastError <> 0 Then
                loadMessage = MegaAccountForm.Summary(accountStatus)
            Else
                loadMessage = Language.GetText(If(MegaAccountService.Availability() = 0, "MegaDrive_NoAccounts", "MegaDrive_BackendMissing"))
            End If
            RefreshRows()
            Return
        End If

        Dim access = service.GetAccessError(currentEmail)
        If access <> 0 Then
            drive.Snapshot = Nothing
            loadMessage = DriveLoadError(access)
            RefreshRows()
            Return
        End If

        Dim source As New CancellationTokenSource()
        loadCancellation = source
        Dim email = currentEmail
        Dim target = drive
        target.Snapshot = Nothing
        Dim incoming = driveSource.SelectedIndex = 1
        target.Incoming = incoming
        loadMessage = Language.GetText("MegaDrive_Loading")
        RefreshRows()
        Try
            Dim snapshot As MegaDriveSnapshot = Nothing
            Dim code = Await Task.Run(Function() service.ReadDrive(email, source.Token, snapshot, incomingShares:=incoming))
            If source.IsCancellationRequested OrElse IsDisposed Then Return
            If code = 0 Then
                target.Snapshot = snapshot
                Dim lastFolder = If(incoming, target.Preferences.LastSharedFolder, target.Preferences.LastFolder)
                Dim folder = lastFolder.GetValueOrDefault(snapshot.Root)
                target.Folder = If(snapshot.IsFolder(folder), folder, snapshot.Root)
                target.Selected.RemoveWhere(Function(id) snapshot.Find(id) Is Nothing)
            Else
                target.Snapshot = Nothing
                loadMessage = DriveLoadError(code)
            End If
        Catch ex As Exception
            If source.IsCancellationRequested OrElse IsDisposed Then Return
            Log.WriteWarning("MEGA drive list failed: " & Log.SafeException(ex))
            target.Snapshot = Nothing
            loadMessage = Language.GetText("MegaDrive_LoadFailed")
        Finally
            If loadCancellation Is source Then
                loadCancellation = Nothing
                If Not IsDisposed Then RefreshRows()
            End If
            source.Dispose()
        End Try
    End Sub

    Private Shared Function DriveLoadError(code As Integer) As String
        Select Case code
            Case MegaAccountService.SessionExpired : Return Language.GetText("MegaAccount_Reconnect")
            Case MegaAccountService.BackendMissing : Return Language.GetText("MegaDrive_BackendMissing")
            Case MegaAccountService.Busy : Return Language.GetText("MegaAccount_Connecting")
            Case Else : Return Language.GetText("MegaDrive_LoadFailed") & " " & MegaAccountService.ErrorText(code)
        End Select
    End Function

    Private Sub ManageAccounts(sender As Object, e As EventArgs)
        Dim preferred = currentEmail
        Using dialog As New MegaAccountForm()
            If dialog.ShowDialog(Me) = DialogResult.OK Then preferred = dialog.SignedInEmail
        End Using
        ' A re-login can replace the node tree for the selected account.
        SetAccounts(If(String.IsNullOrEmpty(preferred), service.DefaultEmail, preferred))
    End Sub

    Private Sub SavePreferences()
        If drive Is Nothing Then Return
        Dim result = service.SaveDrivePreferences(currentEmail, drive.Preferences)
        If result <> 0 AndAlso result <> MegaAccountService.SessionExpired Then
            MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub UpdateSearchCue()
        Dim key = If(scope.SelectedIndex = 1, "MegaDrive_SearchDrive", "MegaDrive_SearchHere")
        search.AccessibleName = Language.GetText(key)
        UiStyles.SetSearchCue(search)
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = (Keys.Control Or Keys.F) AndAlso search.Enabled Then
            search.Focus()
            search.SelectAll()
            Return True
        End If
        If grid.ContainsFocus AndAlso keyData = (Keys.Control Or Keys.A) AndAlso bulk.Enabled Then
            SelectVisible(True)
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub Navigate(folder As ULong, Optional focusNode As ULong? = Nothing)
        showingSelected = False
        drive.Folder = folder
        If drive.Incoming Then
            drive.Preferences.LastSharedFolder = folder
        Else
            drive.Preferences.LastFolder = folder
        End If
        pendingFocusNode = focusNode
        search.Clear()
        If scope.SelectedIndex = 0 Then
            RefreshRows()
        Else
            scope.SelectedIndex = 0
        End If
    End Sub

    Private Async Sub RefreshRows()
        searchTimer.Stop()
        If rowBuildCancellation IsNot Nothing Then rowBuildCancellation.Cancel()
        rowBuildCancellation = Nothing
        Dim focusNode = pendingFocusNode
        pendingFocusNode = Nothing
        selectionAnchor = -1
        Dim ready = loadCancellation Is Nothing AndAlso drive IsNot Nothing AndAlso drive.Snapshot IsNot Nothing
        Dim wholeDrive = scope.SelectedIndex = 1
        grid.RowCount = 0
        grid.Columns("Location").Visible = wholeDrive OrElse showingSelected
        grid.Columns("Jump").Visible = wholeDrive OrElse showingSelected
        shownNodes = {}
        UpdateBrowseControls(ready)
        If ready Then
            Dim snapshot = drive.Snapshot
            Dim folder = drive.Folder
            Dim byTime = sortByTime
            Dim reverse = descending
            Dim query = search.Text.Trim()
            Dim kind = CType(typeFilter.SelectedIndex, MegaDriveNode.FileKind)
            Dim selected = If(showingSelected, drive.Selected.ToArray(), Nothing)
            Dim candidateCount = If(selected Is Nothing, snapshot.BrowseCount(folder, wholeDrive), selected.Length)
            Dim build As Func(Of MegaDriveNode()) = Function()
                                                        If selected IsNot Nothing Then
                                                            Return MegaDriveSnapshot.Sort(selected.Select(AddressOf snapshot.Find), byTime, reverse)
                                                        End If
                                                        Return snapshot.Browse(folder, query, wholeDrive, kind, byTime, reverse)
                                                    End Function
            If candidateCount < BackgroundBrowseCount Then
                shownNodes = build()
            Else
                Dim source As New CancellationTokenSource()
                rowBuildCancellation = source
                UpdateSelectionActions()
                Try
                    ' Keep one sort active even when the user changes the search or account repeatedly.
                    Await rowBuildGate.WaitAsync(source.Token)
                    Dim rows As MegaDriveNode()
                    Try
                        rows = Await Task.Run(build, source.Token)
                    Finally
                        rowBuildGate.Release()
                    End Try
                    If source.IsCancellationRequested OrElse IsDisposed Then Return
                    shownNodes = rows
                Catch ex As OperationCanceledException When source.IsCancellationRequested
                    Return
                Finally
                    If rowBuildCancellation Is source Then rowBuildCancellation = Nothing
                    source.Dispose()
                End Try
            End If
        End If
        grid.RowCount = shownNodes.Length
        UpdateSelectionActions()
        If focusNode.HasValue Then
            Dim index = Array.FindIndex(shownNodes, Function(node) node.Id = focusNode.Value)
            If index >= 0 Then grid.CurrentCell = grid(grid.Columns("Name").Index, index)
        End If
    End Sub

    Private Sub UpdateBrowseControls(ready As Boolean)
        grid.Columns("Name").HeaderCell.SortGlyphDirection = If(sortByTime, SortOrder.None, If(descending, SortOrder.Descending, SortOrder.Ascending))
        grid.Columns("Modified").HeaderCell.SortGlyphDirection = If(sortByTime, If(descending, SortOrder.Descending, SortOrder.Ascending), SortOrder.None)
        searchFrame.Enabled = ready AndAlso Not showingSelected
        typeFilter.Enabled = searchFrame.Enabled
        scope.Enabled = searchFrame.Enabled
        up.Enabled = ready AndAlso drive.Folder <> drive.Snapshot.Root
        favorites.Enabled = ready
        checkNew.Visible = ready AndAlso drive.Preferences.FolderDestinations.ContainsKey(drive.Folder)
        driveSource.Enabled = drive IsNot Nothing
        refreshButton.Enabled = drive IsNot Nothing AndAlso loadCancellation Is Nothing
        manage.Enabled = loadCancellation Is Nothing
        accounts.Enabled = accounts.Items.Count > 0
        RefreshBreadcrumbs()
    End Sub

    Private Sub UpdateSelectionActions()
        Dim count = If(drive Is Nothing, 0, drive.Selected.Count)
        Dim ready = loadCancellation Is Nothing AndAlso rowBuildCancellation Is Nothing AndAlso drive IsNot Nothing AndAlso drive.Snapshot IsNot Nothing
        bulk.Enabled = ready AndAlso shownNodes.Length > 0
        review.Enabled = ready AndAlso (count > 0 OrElse showingSelected)
        review.Text = Language.GetText(If(showingSelected, "MegaDrive_BackToFiles", "MegaDrive_ViewSelected"))
        add.Enabled = ready AndAlso count > 0 AndAlso Not submitting
        If Not ready Then
            status.Text = If(rowBuildCancellation IsNot Nothing, Language.GetText("MegaDrive_PreparingList"), loadMessage)
        ElseIf shownNodes.Length = 0 AndAlso count = 0 Then
            Dim key = If(drive.Incoming AndAlso drive.Folder = drive.Snapshot.Root AndAlso Not drive.Snapshot.InDirectory(drive.Folder).Any(),
                         "MegaDrive_NoShares", "MegaDrive_EmptyFolder")
            status.Text = Language.GetText(If(showingSelected, "MegaDrive_NothingSelected", If(search.TextLength > 0 OrElse typeFilter.SelectedIndex > 0, "MegaDrive_NoMatches", key)))
        Else
            status.Text = Language.GetText("MegaDrive_Status").Replace("%ITEMS%", shownNodes.Length.ToString("N0")).Replace("%SELECTED%", count.ToString("N0"))
        End If
    End Sub

    Private Sub ReadCell(sender As Object, e As DataGridViewCellValueEventArgs)
        If e.RowIndex >= shownNodes.Length Then Return
        Dim node = shownNodes(e.RowIndex)
        Select Case grid.Columns(e.ColumnIndex).Name
            Case "Selected" : e.Value = drive.Selected.Contains(node.Id)
            Case "Name" : e.Value = If(node.IsFolder, "▸  " & node.Name, node.Name)
            Case "Size" : e.Value = If(node.IsFolder, "", FileDownloader.FormatSizeBinary(node.Bytes, 1))
            Case "Modified" : e.Value = If(node.Modified > 0, DateTimeOffset.FromUnixTimeSeconds(node.Modified).LocalDateTime.ToString("g"), "")
            Case "Location" : e.Value = FolderPath(node.ParentId)
        End Select
    End Sub

    Private Sub UpdateSelection(sender As Object, e As DataGridViewCellValueEventArgs)
        If e.ColumnIndex <> 0 OrElse e.RowIndex >= shownNodes.Length Then Return
        Dim row = e.RowIndex
        Dim anchor = If((ModifierKeys And Keys.Shift) <> Keys.None AndAlso selectionAnchor >= 0, selectionAnchor, row)
        selectionAnchor = row
        Dim first = Math.Min(row, anchor)
        Dim last = Math.Max(row, anchor)
        ApplySelection(shownNodes.Skip(first).Take(last - first + 1), CBool(e.Value))
    End Sub

    Private Sub ApplySelection(items As IEnumerable(Of MegaDriveNode), checked As Boolean)
        Dim ids = items.Select(Function(node) node.Id)
        If checked Then
            drive.Selected.UnionWith(ids)
        Else
            drive.Selected.ExceptWith(ids)
        End If
        If showingSelected Then
            BeginInvoke(New Action(AddressOf RefreshRows))
        Else
            grid.InvalidateColumn(0)
            UpdateSelectionActions()
        End If
    End Sub

    Private Sub SelectVisible(checked As Boolean)
        Dim items As IEnumerable(Of MegaDriveNode) = shownNodes
        If checked AndAlso Not showingSelected AndAlso typeFilter.SelectedIndex > 0 Then
            items = items.Where(Function(node) Not node.IsFolder)
        End If
        ApplySelection(items, checked)
    End Sub

    Private Sub ShowBulkActions(sender As Object, e As EventArgs)
        Dim menu As New ContextMenuStrip()
        menu.Items.Add(Language.GetText("MegaDrive_SelectAllVisible"), Nothing, Sub() SelectVisible(True)).Enabled = Not showingSelected
        menu.Items.Add(Language.GetText("MegaDrive_ClearVisible"), Nothing, Sub() SelectVisible(False))
        ThemeManager.ApplyThemeToControl(menu)
        AddHandler menu.Closed, Sub() menu.Dispose()
        menu.Show(bulk, New Point(0, -menu.GetPreferredSize(Size.Empty).Height))
    End Sub

    Private Sub ChangeSort(sender As Object, e As DataGridViewCellMouseEventArgs)
        Dim column = grid.Columns(e.ColumnIndex)
        If column.SortMode <> DataGridViewColumnSortMode.Programmatic Then Return
        Dim byTime = column.Name = "Modified"
        descending = If(byTime = sortByTime, Not descending, byTime)
        sortByTime = byTime
        RefreshRows()
    End Sub

    Private Sub OpenNode(node As MegaDriveNode)
        If node.IsFolder Then
            Navigate(node.Id)
        Else
            ConfirmDownload({node.Id})
        End If
    End Sub

    Private Function FolderPath(folder As ULong) As String
        Dim snapshot = drive.Snapshot
        Return String.Join(" / ", snapshot.Breadcrumbs(folder).Select(Function(node) If(node.Id = snapshot.Root, driveSource.Text, node.Name)))
    End Function

    Private Sub RefreshBreadcrumbs()
        crumbs.SuspendLayout()
        Try
            While crumbs.Controls.Count > 0
                crumbs.Controls(0).Dispose()
            End While
            If loadCancellation IsNot Nothing OrElse drive Is Nothing OrElse drive.Snapshot Is Nothing Then Return
            Dim trail = drive.Snapshot.Breadcrumbs(drive.Folder)
            Dim captions = trail.Select(Function(node) If(node.Id = drive.Snapshot.Root, driveSource.Text, node.Name)).ToArray()
            Dim widths = captions.Select(Function(caption) Math.Min(LogicalToDeviceUnits(180), TextRenderer.MeasureText(caption, Font).Width + LogicalToDeviceUnits(25))).ToArray()
            Dim separatorWidth = LogicalToDeviceUnits(14)
            Dim collapse = trail.Count > 2 AndAlso widths.Sum() + separatorWidth * (trail.Count - 1) > crumbs.ClientSize.Width
            For index As Integer = 0 To trail.Count - 1
                If collapse AndAlso index > 1 AndAlso index < trail.Count - 1 Then Continue For
                Dim crumbHeight = Math.Max(24, crumbs.ClientSize.Height - 4)
                If index > 0 Then
                    Dim separator As New Label With {.Text = "›", .TextAlign = ContentAlignment.MiddleCenter, .Margin = Padding.Empty,
                        .Size = New Size(separatorWidth, crumbHeight)}
                    crumbs.Controls.Add(separator)
                    UiStyles.SetRole(separator, UiStyles.Role.Muted)
                End If
                Dim folder = trail(index).Id
                Dim overflow = collapse AndAlso index = 1
                Dim button = ButtonFor("MegaDrive_Title", UiStyles.Role.QuietButton)
                button.AutoSize = False
                button.UseMnemonic = False
                button.Text = If(overflow, "…", captions(index))
                button.AccessibleName = If(overflow, Language.GetText("MegaDrive_ParentFolders"), captions(index))
                button.Margin = Padding.Empty
                button.SetBounds(0, 0, If(overflow, LogicalToDeviceUnits(36), widths(index)), crumbHeight)
                If overflow Then
                    AddHandler button.Click, Sub() ShowFolderMenu(button, trail.Skip(1).Take(trail.Count - 2))
                Else
                    AddHandler button.Click, Sub() Navigate(folder)
                End If
                crumbs.Controls.Add(button)
                ThemeManager.ApplyThemeToControl(button)
            Next
        Finally
            crumbs.ResumeLayout()
        End Try
    End Sub

    Private Sub ShowFolderMenu(anchor As Control, folders As IEnumerable(Of MegaDriveNode), Optional allowFavoriteChange As Boolean = False)
        Dim menu As New ContextMenuStrip()
        If allowFavoriteChange AndAlso drive.Folder <> drive.Snapshot.Root Then
            Dim folder = drive.Folder
            Dim marked = drive.Preferences.Favorites.Contains(folder)
            Dim bound = drive.Preferences.FolderDestinations.ContainsKey(folder)
            Dim toggle As New ToolStripMenuItem(Language.GetText(If(marked, "MegaDrive_RemoveFavorite", "MegaDrive_AddFavorite")))
            AddHandler toggle.Click, Sub()
                                         If marked Then
                                             drive.Preferences.Favorites.Remove(folder)
                                         Else
                                             drive.Preferences.Favorites.Add(folder)
                                         End If
                                         SavePreferences()
                                     End Sub
            menu.Items.Add(toggle)
            If marked OrElse bound Then
                menu.Items.Add(Language.GetText(If(bound, "DriveSubscription_EditSettings", "MegaDrive_BindFolder")), Nothing, Sub() BindFolder(folder))
            End If
            If bound Then
                menu.Items.Add(Language.GetText("MegaDrive_UnbindFolder"), Nothing,
                               Sub()
                                   drive.Preferences.FolderDestinations.Remove(folder)
                                   drive.Preferences.BoundFolderPaths.Remove(folder)
                                   SavePreferences()
                                   RefreshRows()
                               End Sub)
            End If
        End If
        Dim folderItems = folders.ToArray()
        If menu.Items.Count > 0 AndAlso folderItems.Length > 0 Then menu.Items.Add(New ToolStripSeparator())
        For Each node In folderItems
            Dim folder = node.Id
            Dim item As New ToolStripMenuItem(FolderPath(folder).Replace("&", "&&"))
            AddHandler item.Click, Sub() Navigate(folder)
            menu.Items.Add(item)
        Next
        If menu.Items.Count = 0 Then menu.Items.Add(New ToolStripMenuItem(Language.GetText("MegaDrive_NoFavorites")) With {.Enabled = False})
        ThemeManager.ApplyThemeToControl(menu)
        AddHandler menu.Closed, Sub() menu.Dispose()
        menu.Show(anchor, New Point(0, anchor.Height))
    End Sub

    Private Sub ShowFavorites(sender As Object, e As EventArgs)
        Dim snapshot = drive.Snapshot
        ShowFolderMenu(favorites, drive.Preferences.Favorites.Where(AddressOf snapshot.IsFolder).Select(AddressOf snapshot.Find), True)
    End Sub

    Private Sub BindFolder(folder As ULong)
        Dim destinations = drive.Preferences.FolderDestinations
        Dim current As String = Nothing
        destinations.TryGetValue(folder, current)
        Dim settings = service.GetDriveSubscriptions(currentEmail).FirstOrDefault(Function(item) item.Folder = folder)
        If settings Is Nothing Then
            settings = New MegaDriveSubscription With {.Folder = folder, .Destination = config.RutaDefecto}
            config.ApplyNewSubscriptionDefaults(settings)
        End If
        settings.Name = drive.Snapshot.Find(folder).Name
        settings.Incoming = drive.Incoming
        Using dialog As New MegaDriveSubscriptionSettingsForm(settings)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            settings = dialog.Settings
            Dim result = service.SaveDriveSubscriptionSettings(currentEmail, settings)
            If result <> 0 Then
                MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
        End Using
        Dim destination = PathGuard.GetSafePathUnderRoot(settings.Destination, "")
        If Not String.Equals(current, destination, StringComparison.OrdinalIgnoreCase) Then drive.Preferences.BoundFolderPaths.Remove(folder)
        destinations(folder) = destination
        RefreshRows()
    End Sub

    Private Sub CheckBoundFolder(sender As Object, e As EventArgs)
        If submitting OrElse loadCancellation IsNot Nothing Then Return
        Using dialog As New MegaDriveSubscriptionsForm(ownerMain, service, currentEmail, drive.Folder)
            dialog.ShowDialog(Me)
        End Using
        ' Subscription settings can change the local binding in the report window.
        Dim saved = service.GetDrivePreferences(currentEmail)
        drive.Preferences.FolderDestinations = saved.FolderDestinations
        drive.Preferences.BoundFolderPaths = saved.BoundFolderPaths
        RefreshRows()
    End Sub

    Private Async Sub ConfirmDownload(selection As IEnumerable(Of ULong))
        If submitting OrElse loadCancellation IsNot Nothing Then Return
        Dim ids = selection.ToArray()
        If ids.Length = 0 Then Return
        Dim selectedDrive = drive
        Dim snapshot = selectedDrive.Snapshot
        Dim email = currentEmail
        submitting = True
        Try
            Enabled = False
            Dim name = If(ids.Length = 1, snapshot.Find(ids(0)).Name, Text)
            Dim queued = ownerMain.SnapshotCloudQueue(email).Select(Function(file) file.Handle)
            Dim plan = Await Task.Run(Function() snapshot.PlanDownload(ids))
            Enabled = True
            Using dialog As New MegaDriveDownloadForm(plan, name, config.RutaDefecto, queued)
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim confirmed = dialog.ConfirmedPlan
                Dim destination = dialog.Destination
                Enabled = False
                status.Text = Language.GetText("MegaDrive_Adding")
                Dim package = Await Task.Run(Function() confirmed.Build(email, destination, name, config.ExtraerAutomaticamente, CancellationToken.None))
                If package.ListaFicheros.Count > 0 Then
                    Try
                        ownerMain.AgregarPaquete(package, False, True)
                    Catch ex As Exception
                        If Not ownerMain.IsPackageQueued(package) Then
                            For Each file In package.ListaFicheros
                                file.Dispose()
                            Next
                            Throw
                        End If
                        Log.WriteError("Cloud drive package was queued, but post-add updates failed: " & Log.SafeException(ex))
                        ownerMain.SaveImportedQueueInBackground()
                    End Try
                Else
                    MessageBox.Show(Me, Language.GetText("MegaDrive_FoldersCreated"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
                selectedDrive.Selected.ExceptWith(ids)
            End Using
        Catch ex As Exception
            Log.WriteWarning("Cloud drive queue creation failed: " & Log.SafeException(ex))
            MessageBox.Show(Me, Language.GetText("MegaDrive_AddFailed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Enabled = True
            submitting = False
            RefreshRows()
        End Try
    End Sub
End Class

Friend NotInheritable Class MegaDriveDownloadForm
    Inherits Form

    Private ReadOnly pathInput As New TextBox With {.Name = "MegaDriveDestination", .Dock = DockStyle.Fill}
    Private ReadOnly plan As MegaDriveDownload
    Private ReadOnly queued As HashSet(Of ULong)
    Private ReadOnly checkDuplicates As New CheckBox With {.Name = "MegaDriveSkipQueued", .AutoSize = True, .Anchor = AnchorStyles.Left}
    Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .AutoEllipsis = True}
    Private ReadOnly accept As New CommandButton With {.AutoSize = True, .Padding = New Padding(8, 4, 8, 4)}

    Friend Property ConfirmedPlan As MegaDriveDownload

    Friend ReadOnly Property Destination As String
        Get
            Return pathInput.Text.Trim()
        End Get
    End Property

    Friend Sub New(plan As MegaDriveDownload, title As String, initialPath As String, queuedHandles As IEnumerable(Of ULong))
        Me.plan = plan
        queued = New HashSet(Of ULong)(queuedHandles)
        Text = Language.GetText("MegaDrive_Confirm")
        Name = "MegaDriveConfirm"
        StartPosition = FormStartPosition.CenterParent
        ShowInTaskbar = False
        MinimizeBox = False
        UiStyles.InitializeForm(Me)
        ClientSize = New Size(760, 445)
        MinimumSize = New Size(570, 350)
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 7, .Padding = New Padding(20, 16, 20, 12)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        For Each rowHeight As Integer In {26, 27, 22, 34}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, rowHeight))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
        Dim selectionName As New Label With {.Text = title, .Dock = DockStyle.Fill, .AutoEllipsis = True, .UseMnemonic = False}
        UiStyles.SetFont(selectionName, 2, True, Font)
        UiStyles.SetRole(summary, UiStyles.Role.Muted)
        Dim pathLabel As New Label With {.Text = Language.GetText("MegaDrive_SavePath"), .AutoSize = True}
        layout.Controls.Add(selectionName, 0, 0)
        layout.SetColumnSpan(selectionName, 2)
        layout.Controls.Add(summary, 0, 1)
        layout.SetColumnSpan(summary, 2)
        layout.Controls.Add(pathLabel, 0, 2)
        pathInput.Text = initialPath
        pathInput.AccessibleName = pathLabel.Text
        layout.Controls.Add(pathInput, 0, 3)
        Dim browse As New CommandButton With {.Text = Language.GetText("Browse"), .AutoSize = True, .Anchor = AnchorStyles.Top Or AnchorStyles.Right}
        UiStyles.SetRole(browse, UiStyles.Role.SecondaryButton)
        AddHandler browse.Click, Sub()
                                     Using picker As New FolderBrowserDialog With {.SelectedPath = Destination}
                                         If picker.ShowDialog(Me) = DialogResult.OK Then pathInput.Text = picker.SelectedPath
                                     End Using
                                 End Sub
        layout.Controls.Add(browse, 1, 3)
        Dim fileList As New DataGridView With {.Name = "MegaDrivePreview", .Dock = DockStyle.Fill, .ReadOnly = True, .VirtualMode = True,
            .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .MultiSelect = False,
            .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal}
        fileList.RowTemplate.Height = 28
        fileList.Columns.Add(PreviewColumn("Name", 110))
        fileList.Columns.Add(PreviewColumn("MegaDrive_LocalFolder", 90))
        fileList.Columns.Add(PreviewColumn("Size", 35))
        fileList.Columns.Add(PreviewColumn("Status", 52))
        AddHandler fileList.CellValueNeeded, AddressOf ReadPreviewCell
        fileList.RowCount = plan.Files.Count
        layout.Controls.Add(fileList, 0, 4)
        layout.SetColumnSpan(fileList, 2)

        checkDuplicates.Text = Language.GetText("MegaDrive_SkipQueued")
        checkDuplicates.Visible = plan.Files.Any(Function(item) queued.Contains(item.Key.Id))
        AddHandler checkDuplicates.CheckedChanged, AddressOf UpdatePreview
        layout.Controls.Add(checkDuplicates, 0, 5)
        layout.SetColumnSpan(checkDuplicates, 2)
        UiStyles.SetRole(accept, UiStyles.Role.PrimaryButton)
        Dim dismiss As New CommandButton With {.Text = Language.GetText("Cancel"), .AutoSize = True, .DialogResult = DialogResult.Cancel, .Padding = New Padding(8, 4, 8, 4)}
        UiStyles.SetRole(dismiss, UiStyles.Role.QuietButton)
        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
        actions.Controls.AddRange({accept, dismiss})
        layout.Controls.Add(actions, 0, 6)
        layout.SetColumnSpan(actions, 2)
        AddHandler accept.Click, Sub()
                                     If Destination.Length = 0 OrElse Not Path.IsPathRooted(Destination) Then
                                         MessageBox.Show(Me, Language.GetText("MegaDrive_ChoosePath"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
                                         pathInput.Focus()
                                     Else
                                         DialogResult = DialogResult.OK
                                     End If
                                 End Sub
        Controls.Add(layout)
        AcceptButton = accept
        CancelButton = dismiss
        UpdatePreview(Me, EventArgs.Empty)
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Shared Function PreviewColumn(key As String, weight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn With {.HeaderText = Language.GetText(key), .FillWeight = weight,
                                                  .SortMode = DataGridViewColumnSortMode.NotSortable}
    End Function

    Private Sub ReadPreviewCell(sender As Object, e As DataGridViewCellValueEventArgs)
        Dim item = plan.Files(e.RowIndex)
        Select Case e.ColumnIndex
            Case 0 : e.Value = item.Key.Name
            Case 1 : e.Value = If(item.Value = "", ".", item.Value)
            Case 2 : e.Value = FileDownloader.FormatSizeBinary(item.Key.Bytes, 1)
            Case 3
                If queued.Contains(item.Key.Id) Then
                    e.Value = Language.GetText(If(checkDuplicates.Checked, "MegaDrive_SkippedQueued", "MegaDrive_AlreadyQueued"))
                End If
        End Select
    End Sub

    Private Sub UpdatePreview(sender As Object, e As EventArgs)
        ConfirmedPlan = If(checkDuplicates.Checked, plan.KeepFiles(Function(item) Not queued.Contains(item.Key.Id), preserveEmpty:=True), plan)
        Dim size = If(ConfirmedPlan.TotalBytes <= Long.MaxValue, FileDownloader.FormatSizeBinary(CLng(ConfirmedPlan.TotalBytes), 1), ConfirmedPlan.TotalBytes.ToString("N0") & " B")
        summary.Text = Language.GetText("MegaDrive_DownloadSummary").Replace("%FILES%", ConfirmedPlan.Files.Count.ToString("N0")).
            Replace("%FOLDERS%", ConfirmedPlan.Directories.Count.ToString("N0")).Replace("%SIZE%", size)
        accept.Text = Language.GetText(If(ConfirmedPlan.Files.Count > 0, "MegaDrive_AddToQueue", "MegaDrive_CreateFolders"))
        accept.Enabled = ConfirmedPlan.Files.Count > 0 OrElse ConfirmedPlan.Directories.Count > 0
        Controls.Find("MegaDrivePreview", True).Single().Invalidate()
    End Sub
End Class
