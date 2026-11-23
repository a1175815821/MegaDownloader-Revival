Option Strict On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms

Friend NotInheritable Class MegaDriveSubscriptionSettingsForm
    Inherits Form

    Private ReadOnly original As MegaDriveSubscription
    Private ReadOnly destination As New TextBox With {.Anchor = AnchorStyles.Left Or AnchorStyles.Right}
    Private ReadOnly extensions As New TextBox With {.Anchor = AnchorStyles.Left Or AnchorStyles.Right}
    Private ReadOnly daily As New CheckBox With {.AutoSize = True, .Anchor = AnchorStyles.Left}
    Private ReadOnly checkTime As New DateTimePicker With {.Format = DateTimePickerFormat.Custom,
        .CustomFormat = "HH:mm", .ShowUpDown = True, .Width = 84, .Anchor = AnchorStyles.Left}
    Private ReadOnly review As New RadioButton With {.AutoSize = True}
    Private ReadOnly automatic As New RadioButton With {.AutoSize = True}

    Friend ReadOnly Property Settings As MegaDriveSubscription

    Friend Sub New(settings As MegaDriveSubscription)
        original = settings
        Text = Language.GetText("DriveSubscription_SettingsTitle")
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        ShowInTaskbar = False
        MinimizeBox = False
        MaximizeBox = False
        UiStyles.InitializeForm(Me)
        ClientSize = New Size(580, 318)

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .Padding = New Padding(16)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        For Each height As Integer In {32, 21, 36, 21, 34, 37, 58, 44}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
        Next

        Dim folder As New Label With {.Text = If(String.IsNullOrEmpty(settings.Name), settings.Destination, settings.Name),
            .Dock = DockStyle.Fill, .AutoEllipsis = True, .UseMnemonic = False}
        UiStyles.SetFont(folder, 1, True, Font)
        layout.Controls.Add(folder, 0, 0)
        layout.SetColumnSpan(folder, 2)

        Dim destinationLabel As New Label With {.Text = Language.GetText("MegaDrive_SavePath"), .AutoSize = True}
        destination.AccessibleName = destinationLabel.Text
        destination.Text = settings.Destination
        Dim browse As New CommandButton With {.Text = Language.GetText("Browse"), .AutoSize = True, .Anchor = AnchorStyles.Left}
        UiStyles.SetRole(browse, UiStyles.Role.SecondaryButton)
        layout.Controls.Add(destinationLabel, 0, 1)
        layout.Controls.Add(destination, 0, 2)
        layout.Controls.Add(browse, 1, 2)

        Dim extensionsLabel As New Label With {.Text = Language.GetText("DriveSubscription_Extensions"), .AutoSize = True}
        extensions.AccessibleName = extensionsLabel.Text
        extensions.Text = settings.Extensions
        layout.Controls.Add(extensionsLabel, 0, 3)
        layout.SetColumnSpan(extensionsLabel, 2)
        layout.Controls.Add(extensions, 0, 4)
        layout.SetColumnSpan(extensions, 2)

        daily.Text = Language.GetText("DriveSubscription_DailyLocalTime")
        daily.Checked = settings.CheckMinutes.HasValue
        checkTime.AccessibleName = daily.Text
        checkTime.Value = Date.Today.AddMinutes(Math.Max(0, Math.Min(1439, settings.CheckMinutes.GetValueOrDefault(540))))
        checkTime.Enabled = daily.Checked
        Dim schedule As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Margin = Padding.Empty}
        schedule.Controls.AddRange({daily, checkTime})
        layout.Controls.Add(schedule, 0, 5)
        layout.SetColumnSpan(schedule, 2)

        review.Text = Language.GetText("DriveSubscription_ReviewFirst")
        automatic.Text = Language.GetText("DriveSubscription_AutoQueue")
        review.Checked = Not settings.AutoQueue
        automatic.Checked = settings.AutoQueue
        Dim queueMode As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False, .Margin = Padding.Empty}
        queueMode.Controls.AddRange({review, automatic})
        layout.Controls.Add(queueMode, 0, 6)
        layout.SetColumnSpan(queueMode, 2)

        Dim save As New CommandButton With {.Text = Language.GetText("Save"), .AutoSize = True, .TabIndex = 0}
        Dim cancel As New CommandButton With {.Text = Language.GetText("Cancel"), .DialogResult = DialogResult.Cancel,
            .AutoSize = True, .TabIndex = 1}
        UiStyles.SetRole(save, UiStyles.Role.PrimaryButton)
        UiStyles.SetRole(cancel, UiStyles.Role.QuietButton)
        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .Margin = Padding.Empty}
        actions.Controls.AddRange({cancel, save})
        layout.Controls.Add(actions, 0, 7)
        layout.SetColumnSpan(actions, 2)
        Controls.Add(layout)

        AcceptButton = save
        CancelButton = cancel
        AddHandler browse.Click, AddressOf BrowseDestination
        AddHandler daily.CheckedChanged, Sub() checkTime.Enabled = daily.Checked
        AddHandler save.Click, AddressOf SaveSettings
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Sub BrowseDestination(sender As Object, e As EventArgs)
        Using picker As New FolderBrowserDialog With {.Description = Language.GetText("MegaDrive_BindFolder"),
            .SelectedPath = destination.Text.Trim()}
            If picker.ShowDialog(Me) = DialogResult.OK Then destination.Text = picker.SelectedPath
        End Using
    End Sub

    Private Sub SaveSettings(sender As Object, e As EventArgs)
        Dim pathValue As String
        Try
            Dim input = destination.Text.Trim()
            Dim root = Path.GetPathRoot(input)
            If String.IsNullOrEmpty(root) OrElse root.Length = 1 OrElse root.EndsWith(":", StringComparison.Ordinal) Then
                Throw New ArgumentException()
            End If
            pathValue = PathGuard.GetSafePathUnderRoot(input, "")
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is NotSupportedException OrElse TypeOf ex Is IOException
            MessageBox.Show(Me, Language.GetText("MegaDrive_ChoosePath"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            destination.Focus()
            Return
        End Try

        Dim value = original.Copy()
        value.Destination = pathValue
        value.CheckMinutes = If(daily.Checked, New Integer?(checkTime.Value.Hour * 60 + checkTime.Value.Minute), Nothing)
        value.Extensions = MegaDriveSubscription.NormalizeExtensions(extensions.Text)
        value.AutoQueue = automatic.Checked
        _Settings = value
        DialogResult = DialogResult.OK
    End Sub
End Class

Friend NotInheritable Class MegaDriveSubscriptionsForm
    Inherits Form

    Private ReadOnly ownerMain As Main
    Private ReadOnly service As MegaAccountService
    Private ReadOnly accounts As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList,
        .FormattingEnabled = True, .Anchor = AnchorStyles.Left Or AnchorStyles.Right}
    Private ReadOnly folders As DataGridView = CreateGrid()
    Private ReadOnly report As DataGridView = CreateGrid()
    Private ReadOnly refreshButton As CommandButton = CreateButton("MegaDrive_Refresh", UiStyles.Role.QuietButton)
    Private ReadOnly checkFolder As CommandButton = CreateButton("DriveSubscription_CheckFolder", UiStyles.Role.SecondaryButton)
    Private ReadOnly checkAll As CommandButton = CreateButton("DriveSubscription_CheckAll", UiStyles.Role.SecondaryButton)
    Private ReadOnly edit As CommandButton = CreateButton("DriveSubscription_EditSettings", UiStyles.Role.QuietButton)
    Private ReadOnly selectPending As CommandButton = CreateButton("DriveSubscription_SelectPending", UiStyles.Role.QuietButton)
    Private ReadOnly clear As CommandButton = CreateButton("DriveSubscription_ClearSelection", UiStyles.Role.QuietButton)
    Private ReadOnly queue As CommandButton = CreateButton("MegaDrive_AddToQueue", UiStyles.Role.PrimaryButton)
    Private ReadOnly dismiss As CommandButton = CreateButton("Close", UiStyles.Role.QuietButton)
    Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft,
        .AutoEllipsis = True, .UseMnemonic = False}

    Private ReadOnly pendingText As String = Language.GetText("DriveSubscription_PendingReview")
    Private ReadOnly queuedText As String = Language.GetText("MegaDrive_AlreadyQueued")
    Private ReadOnly downloadedText As String = Language.GetText("Downloaded")
    Private ReadOnly newText As String = Language.GetText("MegaDrive_NewFile")
    Private ReadOnly updatedText As String = Language.GetText("MegaDrive_ChangedFile")
    Private ReadOnly checkedFiles As New HashSet(Of (Folder As ULong, Handle As ULong))()
    Private ReadOnly reportRows As New List(Of (Subscription As MegaDriveSubscription, FolderName As String, File As MegaDriveSubscriptionFile))()
    Private pendingCount As Integer
    Private currentEmail As String = ""
    Private checkOnOpen As ULong?
    Private busy As Boolean

    Friend Sub New(owner As Main, service As MegaAccountService, Optional initialEmail As String = Nothing, Optional initialFolder As ULong? = Nothing)
        ownerMain = owner
        Me.service = service
        checkOnOpen = initialFolder
        Text = Language.GetText("DriveSubscription_Title")
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.Sizable
        ShowInTaskbar = False
        MinimizeBox = False
        UiStyles.InitializeForm(Me)
        ClientSize = New Size(940, 654)
        MinimumSize = New Size(760, 490)

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6, .Padding = New Padding(16)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 36))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 64))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))

        Dim accountLabel = Caption("MegaAccount_Title")
        accounts.AccessibleName = accountLabel.Text
        layout.Controls.Add(Bar(accounts, accountLabel, accounts, refreshButton), 0, 0)

        Dim folderHeading = Caption("DriveSubscription_BoundFolders")
        UiStyles.SetFont(folderHeading, 1, True, Font)
        folders.AccessibleName = folderHeading.Text
        folders.ReadOnly = True
        folders.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        folders.Columns.Add(TextColumn("Name", 28, 110))
        folders.Columns.Add(TextColumn("MegaDrive_SavePath", 30, 130))
        folders.Columns.Add(TextColumn("DriveSubscription_LastCheck", 16, 112))
        folders.Columns.Add(TextColumn("DriveSubscription_NextCheck", 16, 112))
        folders.Columns.Add(TextColumn("Status", 15, 92))
        layout.Controls.Add(Bar(folderHeading, folderHeading, edit, checkFolder, checkAll), 0, 1)
        layout.Controls.Add(folders, 0, 2)

        Dim reportHeading = Caption("DriveSubscription_LatestReports")
        UiStyles.SetFont(reportHeading, 1, True, Font)
        report.AccessibleName = reportHeading.Text
        report.VirtualMode = True
        report.Columns.Add(New DataGridViewCheckBoxColumn With {.HeaderText = "✓", .ToolTipText = queue.Text,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.None, .Width = 38, .Resizable = DataGridViewTriState.False})
        report.Columns.Add(TextColumn("DriveSubscription_BoundFolder", 23, 100))
        report.Columns.Add(TextColumn("Path", 44, 145))
        report.Columns.Add(TextColumn("Size", 12, 76))
        report.Columns.Add(TextColumn("DriveSubscription_Change", 10, 76))
        report.Columns.Add(TextColumn("Status", 15, 100))
        report.Columns(3).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        layout.Controls.Add(Bar(reportHeading, reportHeading, selectPending, clear), 0, 3)
        layout.Controls.Add(report, 0, 4)

        UiStyles.SetRole(status, UiStyles.Role.Muted)
        layout.Controls.Add(Bar(status, status, queue, dismiss), 0, 5)
        Controls.Add(layout)
        dismiss.DialogResult = DialogResult.Cancel
        CancelButton = dismiss

        AddHandler Shown, Sub() LoadAccounts(If(initialEmail, service.DefaultEmail))
        AddHandler accounts.Format, AddressOf FormatAccount
        AddHandler accounts.SelectedIndexChanged, AddressOf ChooseAccount
        AddHandler folders.SelectionChanged, Sub() UpdateActions()
        AddHandler refreshButton.Click, Async Sub(sender, e) Await ReloadReportsAsync()
        AddHandler checkFolder.Click, Async Sub(sender, e) Await CheckAsync(SelectedFolder())
        AddHandler checkAll.Click, Async Sub(sender, e) Await CheckAsync(Nothing)
        AddHandler edit.Click, AddressOf EditSettings
        AddHandler report.CellValueNeeded, AddressOf ReadReportCell
        AddHandler report.CellValuePushed, AddressOf SetReportCheck
        AddHandler report.CellBeginEdit, AddressOf BeginReportEdit
        AddHandler report.CellPainting, AddressOf PaintReportCheck
        AddHandler report.CellToolTipTextNeeded, AddressOf ReadReportToolTip
        AddHandler report.CurrentCellDirtyStateChanged, Sub()
                                                            If report.IsCurrentCellDirty Then report.CommitEdit(DataGridViewDataErrorContexts.Commit)
                                                        End Sub
        AddHandler selectPending.Click, Sub() SetAllChecks(True)
        AddHandler clear.Click, Sub() SetAllChecks(False)
        AddHandler queue.Click, AddressOf QueueSelection
        UpdateActions()
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Shared Function CreateButton(key As String, role As UiStyles.Role) As CommandButton
        Dim button As New CommandButton With {.Text = Language.GetText(key), .AutoSize = True, .Anchor = AnchorStyles.Left}
        UiStyles.SetRole(button, role)
        Return button
    End Function

    Private Shared Function Caption(key As String) As Label
        Return New Label With {.Text = Language.GetText(key), .AutoSize = True, .Anchor = AnchorStyles.Left}
    End Function

    Private Shared Function Bar(stretch As Control, ParamArray controls As Control()) As TableLayoutPanel
        Dim panel As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = controls.Length,
            .RowCount = 1, .Margin = Padding.Empty}
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        For index = 0 To controls.Length - 1
            Dim item = controls(index)
            panel.ColumnStyles.Add(If(item Is stretch, New ColumnStyle(SizeType.Percent, 100), New ColumnStyle(SizeType.AutoSize)))
            panel.Controls.Add(item, index, 0)
        Next
        Return panel
    End Function

    Private Shared Function CreateGrid() As DataGridView
        Return New UiStyles.BufferedGrid With {.Dock = DockStyle.Fill, .Margin = Padding.Empty,
            .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .AllowUserToResizeRows = False,
            .MultiSelect = False, .RowHeadersVisible = False, .StandardTab = True,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, .ColumnHeadersHeight = 28,
            .RowTemplate = New DataGridViewRow With {.Height = 28}}
    End Function

    Private Shared Function TextColumn(key As String, weight As Single, minWidth As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn With {.HeaderText = Language.GetText(key), .FillWeight = weight,
            .MinimumWidth = minWidth, .ReadOnly = True, .SortMode = DataGridViewColumnSortMode.NotSortable}
    End Function

    Private Sub LoadAccounts(preferred As String)
        Dim index = -1
        For Each account In service.GetAccounts()
            Dim added = accounts.Items.Add(account)
            If String.Equals(account.Email, preferred, StringComparison.OrdinalIgnoreCase) Then index = added
        Next
        If accounts.Items.Count = 0 Then
            status.Text = Language.GetText("MegaDrive_NoAccounts")
        Else
            accounts.SelectedIndex = Math.Max(0, index)
        End If
    End Sub

    Private Sub FormatAccount(sender As Object, e As ListControlConvertEventArgs)
        If e.ListItem Is Nothing Then Return
        Dim account = DirectCast(e.ListItem, MegaAccountService.Status)
        Dim name = If(account.DisplayName = account.Email, account.Email, account.DisplayName & " (" & account.Email & ")")
        If account.NeedsLogin Then name &= " (" & Language.GetText("MegaAccount_SignInAgain") & ")"
        e.Value = name
    End Sub

    Private Async Sub ChooseAccount(sender As Object, e As EventArgs)
        If accounts.SelectedItem Is Nothing Then Return
        currentEmail = DirectCast(accounts.SelectedItem, MegaAccountService.Status).Email
        checkedFiles.Clear()
        ShowReports(New List(Of MegaDriveSubscription)(), Nothing)
        Dim initialFolder = checkOnOpen
        checkOnOpen = Nothing
        If initialFolder.HasValue Then
            Await CheckAsync(initialFolder)
        Else
            Await ReloadReportsAsync()
        End If
    End Sub

    Private Function SelectedFolder() As ULong?
        Dim row = folders.CurrentRow
        If row Is Nothing OrElse row.Tag Is Nothing Then Return Nothing
        Return DirectCast(row.Tag, MegaDriveSubscription).Folder
    End Function

    Private Function ReloadReportsAsync() As Task
        Return RunAndReloadAsync("DriveSubscription_Loading")
    End Function

    Private Function CheckAsync(folder As ULong?) As Task
        Return RunAndReloadAsync("MegaDrive_CheckingFolder", "DriveSubscription_CheckFailedMessage",
            Async Function()
                Await ownerMain.CheckDriveSubscriptionsAsync(currentEmail, folder)
                Return Nothing
            End Function, preferredFolder:=folder)
    End Function

    Private Async Function RunAndReloadAsync(progressKey As String, Optional failureKey As String = Nothing,
                                             Optional action As Func(Of Task(Of String)) = Nothing,
                                             Optional preferredFolder As ULong? = Nothing) As Task
        If busy OrElse currentEmail.Length = 0 Then Return
        Dim email = currentEmail
        busy = True
        status.Text = Language.GetText(progressKey)
        UpdateActions()
        Dim message As String = Nothing
        Try
            If action IsNot Nothing Then
                Try
                    message = Await action()
                Catch ex As Exception
                    Log.WriteWarning("MEGA drive subscription operation failed: " & Log.SafeException(ex))
                    message = Language.GetText(failureKey)
                End Try
            End If
            If IsDisposed OrElse Disposing OrElse Not Visible Then Return

            Dim reports = Await ownerMain.ReadDriveSubscriptionReportsAsync(email)
            If IsDisposed OrElse Disposing OrElse Not Visible Then Return
            ShowReports(reports, If(preferredFolder.HasValue, preferredFolder, SelectedFolder()))
            status.Text = If(message, ReportSummary(reports))
        Catch ex As Exception
            Log.WriteWarning("MEGA drive subscription reports could not be read: " & Log.SafeException(ex))
            If Not IsDisposed AndAlso Not Disposing AndAlso Visible Then
                Dim loadError = Language.GetText("DriveSubscription_LoadFailed")
                status.Text = If(message Is Nothing, loadError, message & Environment.NewLine & loadError)
            End If
        Finally
            busy = False
            If Not IsDisposed AndAlso Not Disposing AndAlso Visible Then UpdateActions()
        End Try
    End Function

    Private Sub ShowReports(subscriptions As List(Of MegaDriveSubscription), preferredFolder As ULong?)
        folders.Rows.Clear()
        reportRows.Clear()
        pendingCount = 0
        Dim pendingSelections As New HashSet(Of (Folder As ULong, Handle As ULong))()
        Dim preferredRow = -1
        For Each subscription In subscriptions
            Dim folderName = If(String.IsNullOrEmpty(subscription.Name), subscription.Destination, subscription.Name)
            Dim nextCheck = subscription.NextCheckLocal()
            Dim index = folders.Rows.Add(folderName, subscription.Destination,
                If(subscription.LastCheckedUtc.HasValue, subscription.LastCheckedUtc.GetValueOrDefault().ToLocalTime().ToString("g"), "—"),
                If(nextCheck.HasValue, nextCheck.GetValueOrDefault().ToString("g"), Language.GetText("Manual check only")),
                If(subscription.LastError = 0, "", Language.GetText("DriveSubscription_CheckFailed")))
            Dim row = folders.Rows(index)
            row.Tag = subscription
            If subscription.LastError <> 0 Then row.Cells(4).ToolTipText = MegaAccountService.ErrorText(subscription.LastError)
            If preferredFolder.HasValue AndAlso subscription.Folder = preferredFolder.Value Then preferredRow = index
            For Each file In subscription.Files
                reportRows.Add((subscription, folderName, file))
                If file.Status <> MegaDriveSubscriptionFileState.PendingReview Then Continue For
                pendingCount += 1
                Dim key = (subscription.Folder, file.Handle)
                If checkedFiles.Contains(key) Then pendingSelections.Add(key)
            Next
        Next
        If folders.Rows.Count > 0 Then folders.CurrentCell = folders.Rows(Math.Max(0, preferredRow)).Cells(0)
        checkedFiles.IntersectWith(pendingSelections)
        report.RowCount = reportRows.Count
        report.Invalidate()
    End Sub

    Private Function ReportSummary(subscriptions As List(Of MegaDriveSubscription)) As String
        If subscriptions.Count = 0 Then Return Language.GetText("DriveSubscription_NoBindings")
        Dim selected = SelectedFolder()
        If selected.HasValue Then
            Dim subscription = subscriptions.Find(Function(item) item.Folder = selected.Value)
            If subscription IsNot Nothing AndAlso subscription.LastError <> 0 Then Return MegaAccountService.ErrorText(subscription.LastError)
        End If
        If subscriptions.Any(Function(item) item.LastError <> 0) Then Return Language.GetText("DriveSubscription_SomeChecksFailed")
        If subscriptions.All(Function(item) Not item.LastCheckedUtc.HasValue) Then Return Language.GetText("DriveSubscription_NoChecks")
        If reportRows.Count = 0 Then Return Language.GetText("DriveSubscription_NoChanges")
        Return ""
    End Function

    Private Sub ReadReportCell(sender As Object, e As DataGridViewCellValueEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= reportRows.Count Then Return
        Dim row = reportRows(e.RowIndex)
        Dim file = row.File
        Select Case e.ColumnIndex
            Case 0 : e.Value = checkedFiles.Contains((row.Subscription.Folder, file.Handle))
            Case 1 : e.Value = row.FolderName
            Case 2 : e.Value = ReportPath(file)
            Case 3 : e.Value = FileDownloader.FormatSizeBinary(file.Bytes, 1)
            Case 4 : e.Value = If(file.Updated, updatedText, newText)
            Case 5
                Select Case file.Status
                    Case MegaDriveSubscriptionFileState.PendingReview : e.Value = pendingText
                    Case MegaDriveSubscriptionFileState.Queued : e.Value = queuedText
                    Case MegaDriveSubscriptionFileState.Downloaded : e.Value = downloadedText
                End Select
        End Select
    End Sub

    Private Shared Function ReportPath(file As MegaDriveSubscriptionFile) As String
        Return If(String.IsNullOrEmpty(file.RelativePath), file.Name, file.RelativePath & "\" & file.Name)
    End Function

    Private Sub ReadReportToolTip(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= reportRows.Count Then Return
        Dim row = reportRows(e.RowIndex)
        Select Case e.ColumnIndex
            Case 1 : e.ToolTipText = row.Subscription.Destination
            Case 2 : e.ToolTipText = ReportPath(row.File)
        End Select
    End Sub

    Private Sub PaintReportCheck(sender As Object, e As DataGridViewCellPaintingEventArgs)
        If e.ColumnIndex <> 0 OrElse e.RowIndex < 0 OrElse e.RowIndex >= reportRows.Count OrElse
            reportRows(e.RowIndex).File.Status = MegaDriveSubscriptionFileState.PendingReview Then Return
        e.Paint(e.ClipBounds, e.PaintParts And Not DataGridViewPaintParts.ContentForeground)
        e.Handled = True
    End Sub

    Private Sub BeginReportEdit(sender As Object, e As DataGridViewCellCancelEventArgs)
        e.Cancel = busy OrElse e.RowIndex < 0 OrElse e.RowIndex >= reportRows.Count OrElse
            reportRows(e.RowIndex).File.Status <> MegaDriveSubscriptionFileState.PendingReview
    End Sub

    Private Sub SetReportCheck(sender As Object, e As DataGridViewCellValueEventArgs)
        If busy OrElse e.ColumnIndex <> 0 OrElse e.RowIndex < 0 OrElse e.RowIndex >= reportRows.Count Then Return
        Dim row = reportRows(e.RowIndex)
        If row.File.Status <> MegaDriveSubscriptionFileState.PendingReview Then Return
        Dim key = (row.Subscription.Folder, row.File.Handle)
        If TypeOf e.Value Is Boolean AndAlso CBool(e.Value) Then
            checkedFiles.Add(key)
        Else
            checkedFiles.Remove(key)
        End If
        UpdateActions()
    End Sub

    Private Sub SetAllChecks(value As Boolean)
        checkedFiles.Clear()
        If value Then
            For Each row In reportRows
                If row.File.Status = MegaDriveSubscriptionFileState.PendingReview Then checkedFiles.Add((row.Subscription.Folder, row.File.Handle))
            Next
        End If
        report.Invalidate()
        UpdateActions()
    End Sub

    Private Sub UpdateActions()
        Dim hasAccount = currentEmail.Length > 0
        Dim hasFolder = SelectedFolder().HasValue
        accounts.Enabled = Not busy AndAlso accounts.Items.Count > 0
        refreshButton.Enabled = Not busy AndAlso hasAccount
        edit.Enabled = Not busy AndAlso hasFolder
        checkFolder.Enabled = Not busy AndAlso hasFolder
        checkAll.Enabled = Not busy AndAlso folders.Rows.Count > 0
        selectPending.Enabled = Not busy AndAlso checkedFiles.Count < pendingCount
        clear.Enabled = Not busy AndAlso checkedFiles.Count > 0
        queue.Enabled = clear.Enabled
        UiStyles.SetText(queue, If(checkedFiles.Count = 0, Language.GetText("MegaDrive_AddToQueue"),
            Language.GetText("Preflight_AddSelected").Replace("%N%", checkedFiles.Count.ToString("N0"))))
    End Sub

    Private Async Sub EditSettings(sender As Object, e As EventArgs)
        Dim folder = SelectedFolder()
        If busy OrElse Not folder.HasValue Then Return
        Dim email = currentEmail
        Dim subscription = service.GetDriveSubscriptions(email).Find(Function(item) item.Folder = folder.Value)
        If subscription Is Nothing Then
            Await ReloadReportsAsync()
            Return
        End If
        Using dialog As New MegaDriveSubscriptionSettingsForm(subscription)
            Do
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim result = service.SaveDriveSubscriptionSettings(email, dialog.Settings)
                If result = 0 Then Exit Do
                MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Loop
        End Using
        Await ReloadReportsAsync()
    End Sub

    Private Async Sub QueueSelection(sender As Object, e As EventArgs)
        If busy OrElse checkedFiles.Count = 0 Then Return
        Dim selection = checkedFiles.GroupBy(Function(item) item.Folder).
            ToDictionary(Function(group) group.Key, Function(group) New HashSet(Of ULong)(group.Select(Function(item) item.Handle)))
        Dim confirm = Language.GetText("DriveSubscription_QueueConfirm").Replace("%N%", checkedFiles.Count.ToString("N0"))
        If MessageBox.Show(Me, confirm, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

        Dim email = currentEmail
        Await RunAndReloadAsync("MegaDrive_Adding", "DriveSubscription_QueueFailed",
            Async Function()
                Dim result = Await ownerMain.QueueDriveSubscriptionFilesAsync(email, selection)
                If result < 0 Then Return MegaAccountService.ErrorText(result)
                If result = 0 Then Return Language.GetText("DriveSubscription_QueueNone")
                Return Language.GetText("DriveSubscription_QueueAdded").Replace("%N%", result.ToString("N0"))
            End Function)
    End Sub
End Class
