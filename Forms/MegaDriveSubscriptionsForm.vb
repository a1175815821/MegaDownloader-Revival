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
    Private ReadOnly identity As New ComboBox With {.Anchor = AnchorStyles.Left Or AnchorStyles.Right,
        .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly includeExisting As New RadioButton With {.AutoSize = True}
    Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .UseMnemonic = False}
    Private ReadOnly save As New CommandButton With {.AutoSize = True, .TabIndex = 0}
    Private ReadOnly preview As Func(Of MegaDriveSubscription, (Matches As Integer, Queued As Integer, Downloaded As Integer, Adds As Integer))
    Private ReadOnly previewDraft As MegaDriveSubscription
    Private ReadOnly isPublic As Boolean

    Private NotInheritable Class IdentityOption
        Friend ReadOnly Email As String
        Private ReadOnly caption As String

        Friend Sub New(email As String, caption As String)
            Me.Email = email
            Me.caption = caption
        End Sub

        Public Overrides Function ToString() As String
            Return caption
        End Function
    End Class

    Friend ReadOnly Property Settings As MegaDriveSubscription

    Friend Sub New(settings As MegaDriveSubscription)
        Me.New(settings, Nothing)
    End Sub

    Friend Sub New(settings As MegaDriveSubscription,
                   preview As Func(Of MegaDriveSubscription, (Matches As Integer, Queued As Integer, Downloaded As Integer, Adds As Integer)))
        original = settings
        isPublic = Not String.IsNullOrEmpty(settings.PublicShareUrl)
        If isPublic Then Me.preview = preview
        Dim creating = Me.preview IsNot Nothing
        Text = Language.GetText(If(creating, "DriveSubscription_PublicCreateTitle", "DriveSubscription_SettingsTitle"))
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        ShowInTaskbar = False
        MinimizeBox = False
        MaximizeBox = False
        UiStyles.InitializeForm(Me)

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .Padding = New Padding(16)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

        Dim folder As New Label With {.Text = settings.Caption, .Dock = DockStyle.Fill, .AutoEllipsis = True, .UseMnemonic = False}
        UiStyles.SetFont(folder, 1, True, Font)
        AddRow(layout, If(isPublic, 44, 32), folder)
        If isPublic Then
            Dim scope As New Label With {.Text = Language.GetText("DriveSubscription_IncludesSubfolders"), .AutoSize = True}
            UiStyles.SetRole(scope, UiStyles.Role.Muted)
            AddRow(layout, 28, scope)
        End If

        Dim destinationLabel As New Label With {.Text = Language.GetText("MegaDrive_SavePath"), .AutoSize = True}
        destination.AccessibleName = destinationLabel.Text
        destination.Text = settings.Destination
        Dim browse As New CommandButton With {.Text = Language.GetText("Browse"), .AutoSize = True, .Anchor = AnchorStyles.Left}
        UiStyles.SetRole(browse, UiStyles.Role.SecondaryButton)
        AddRow(layout, 21, destinationLabel)
        AddRow(layout, 36, destination, browse)

        Dim extensionsLabel As New Label With {.Text = Language.GetText("DriveSubscription_Extensions"), .AutoSize = True}
        extensions.AccessibleName = extensionsLabel.Text
        extensions.Text = settings.Extensions
        AddRow(layout, 21, extensionsLabel)
        AddRow(layout, 34, extensions)

        If isPublic Then
            Dim identityLabel As New Label With {.Text = Language.GetText("DownloadIdentity"), .AutoSize = True}
            identity.AccessibleName = identityLabel.Text
            LoadIdentities(If(settings.DownloadEmail, ""))
            AddRow(layout, 21, identityLabel)
            AddRow(layout, 36, identity)
            If creating Then
                Dim initialHandling As New Label With {.Text = Language.GetText("DriveSubscription_InitialHandling"), .AutoSize = True}
                Dim futureOnly As New RadioButton With {.Text = Language.GetText("DriveSubscription_FutureOnly"),
                    .AutoSize = True, .Checked = Not settings.IncludeExisting}
                includeExisting.Text = Language.GetText("DriveSubscription_IncludeExisting")
                includeExisting.Checked = settings.IncludeExisting
                AddRow(layout, 21, initialHandling)
                AddRow(layout, 62, RadioGroup(futureOnly, includeExisting))
            Else
                Dim hint As New Label With {.Text = Language.GetText("DriveSubscription_IdentityChangeHint"), .AutoSize = True}
                UiStyles.SetRole(hint, UiStyles.Role.Muted)
                AddRow(layout, 33, hint)
            End If
        End If

        daily.Text = Language.GetText("DriveSubscription_DailyLocalTime")
        daily.Checked = settings.CheckMinutes.HasValue
        checkTime.AccessibleName = daily.Text
        checkTime.Value = Date.Today.AddMinutes(Math.Max(0, Math.Min(1439, settings.CheckMinutes.GetValueOrDefault(540))))
        checkTime.Enabled = daily.Checked
        Dim schedule As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Margin = Padding.Empty}
        schedule.Controls.AddRange({daily, checkTime})
        AddRow(layout, 37, schedule)

        review.Text = Language.GetText("DriveSubscription_ReviewFirst")
        automatic.Text = Language.GetText("DriveSubscription_AutoQueue")
        review.Checked = Not settings.AutoQueue
        automatic.Checked = settings.AutoQueue
        AddRow(layout, 58, RadioGroup(review, automatic))
        If creating Then
            UiStyles.SetRole(summary, UiStyles.Role.Muted)
            AddRow(layout, 69, summary)
        End If

        save.Text = Language.GetText(If(creating, "DriveSubscription_Create", "Save"))
        Dim cancel As New CommandButton With {.Text = Language.GetText("Cancel"), .DialogResult = DialogResult.Cancel,
            .AutoSize = True, .TabIndex = 1}
        UiStyles.SetRole(save, UiStyles.Role.PrimaryButton)
        UiStyles.SetRole(cancel, UiStyles.Role.QuietButton)
        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .Margin = Padding.Empty}
        actions.Controls.AddRange({cancel, save})
        AddRow(layout, 44, actions)
        ClientSize = New Size(If(isPublic, 620, 580), CInt(layout.RowStyles.Cast(Of RowStyle)().Sum(Function(row) row.Height)) + layout.Padding.Vertical + 3)
        Controls.Add(layout)

        AcceptButton = save
        CancelButton = cancel
        AddHandler browse.Click, AddressOf BrowseDestination
        AddHandler daily.CheckedChanged, Sub() checkTime.Enabled = daily.Checked
        AddHandler save.Click, AddressOf SaveSettings
        If creating Then
            previewDraft = settings.Copy()
            AddHandler destination.TextChanged, AddressOf UpdatePreview
            AddHandler extensions.TextChanged, AddressOf UpdatePreview
            AddHandler identity.SelectedIndexChanged, AddressOf UpdatePreview
            AddHandler includeExisting.CheckedChanged, AddressOf UpdatePreview
            UpdatePreview(Nothing, EventArgs.Empty)
        End If
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Shared Sub AddRow(layout As TableLayoutPanel, height As Integer, first As Control, Optional second As Control = Nothing)
        Dim index = layout.RowCount
        layout.RowCount += 1
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
        layout.Controls.Add(first, 0, index)
        If second Is Nothing Then
            layout.SetColumnSpan(first, 2)
        Else
            layout.Controls.Add(second, 1, index)
        End If
    End Sub

    Private Shared Function RadioGroup(ParamArray choices As RadioButton()) As FlowLayoutPanel
        Dim panel As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False, .Margin = Padding.Empty}
        panel.Controls.AddRange(choices)
        Return panel
    End Function

    Private Sub LoadIdentities(email As String)
        identity.Items.Add(New IdentityOption("", Language.GetText("MegaAccount_Anonymous")))
        Dim selectedIndex As Integer = 0
        For Each account In MegaAccountService.Current.GetAccounts()
            Dim index = identity.Items.Add(New IdentityOption(account.Email, IdentityCaption(account)))
            If String.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase) Then selectedIndex = index
        Next
        If email.Length > 0 AndAlso selectedIndex = 0 Then selectedIndex = identity.Items.Add(New IdentityOption(email, RemovedIdentity(email)))
        identity.SelectedIndex = selectedIndex
    End Sub

    Friend Shared Function AccountCaption(account As MegaAccountService.Status) As String
        Return If(account.DisplayName = account.Email, account.Email, account.DisplayName & " (" & account.Email & ")")
    End Function

    Private Shared Function IdentityCaption(account As MegaAccountService.Status) As String
        Return AccountCaption(account) & If(account.SignedIn, "", " · " & MegaAccountForm.Summary(account))
    End Function

    Private Shared Function RemovedIdentity(email As String) As String
        Return email & " · " & Language.GetText("MegaAccount_Removed")
    End Function

    Friend Shared Function IdentityCaption(service As MegaAccountService, email As String) As String
        If String.IsNullOrEmpty(email) Then Return Language.GetText("MegaAccount_Anonymous")
        Dim account = service.GetStatus(email)
        Return If(String.IsNullOrEmpty(account.Email), RemovedIdentity(email), IdentityCaption(account))
    End Function

    Private Sub BrowseDestination(sender As Object, e As EventArgs)
        Using picker As New FolderBrowserDialog With {.Description = Language.GetText("MegaDrive_BindFolder"),
            .SelectedPath = destination.Text.Trim()}
            If picker.ShowDialog(Me) = DialogResult.OK Then destination.Text = picker.SelectedPath
        End Using
    End Sub

    Private Function ReadDestination() As String
        Try
            Dim input = destination.Text.Trim()
            Dim root = Path.GetPathRoot(input)
            If String.IsNullOrEmpty(root) OrElse root.Length = 1 OrElse root.EndsWith(":", StringComparison.Ordinal) Then Return Nothing
            Return PathGuard.GetSafePathUnderRoot(input, "")
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is NotSupportedException OrElse TypeOf ex Is IOException
            Return Nothing
        End Try
    End Function

    Private Sub ReadSettings(value As MegaDriveSubscription, pathValue As String)
        value.Destination = pathValue
        value.CheckMinutes = If(daily.Checked, New Integer?(checkTime.Value.Hour * 60 + checkTime.Value.Minute), Nothing)
        value.Extensions = MegaDriveSubscription.NormalizeExtensions(extensions.Text)
        value.AutoQueue = automatic.Checked
        If isPublic Then
            value.DownloadEmail = DirectCast(identity.SelectedItem, IdentityOption).Email
            value.IncludeExisting = preview IsNot Nothing AndAlso includeExisting.Checked
        End If
    End Sub

    Private Sub UpdatePreview(sender As Object, e As EventArgs)
        Dim pathValue = ReadDestination()
        If pathValue Is Nothing Then
            summary.Text = Language.GetText("MegaDrive_ChoosePath")
            UiStyles.SetText(save, Language.GetText("DriveSubscription_Create"))
            Return
        End If

        ReadSettings(previewDraft, pathValue)
        Dim counts = preview(previewDraft)
        Dim identityName = identity.SelectedItem.ToString()
        Dim added = If(previewDraft.IncludeExisting, counts.Adds, 0).ToString("N0")
        summary.Text = Language.GetText("DriveSubscription_InitialPreview").Replace("%MATCHES%", counts.Matches.ToString("N0")).
            Replace("%QUEUED%", counts.Queued.ToString("N0")).Replace("%DOWNLOADED%", counts.Downloaded.ToString("N0")) & Environment.NewLine &
            Language.GetText(If(previewDraft.IncludeExisting, "DriveSubscription_InitialPreviewAdd", "DriveSubscription_InitialPreviewFuture")).
            Replace("%N%", added).Replace("%IDENTITY%", identityName)
        UiStyles.SetText(save, If(previewDraft.IncludeExisting AndAlso counts.Adds > 0,
            Language.GetText("DriveSubscription_CreateAndQueue").Replace("%N%", added), Language.GetText("DriveSubscription_Create")))
    End Sub

    Private Sub SaveSettings(sender As Object, e As EventArgs)
        Dim pathValue = ReadDestination()
        If pathValue Is Nothing Then
            MessageBox.Show(Me, Language.GetText("MegaDrive_ChoosePath"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            destination.Focus()
            Return
        End If

        Dim value = original.Copy()
        ReadSettings(value, pathValue)
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
    Private ReadOnly defaults As CommandButton = CreateButton("DriveSubscription_OpenDefaults", UiStyles.Role.QuietButton)
    Private ReadOnly checkFolder As CommandButton = CreateButton("DriveSubscription_CheckFolder", UiStyles.Role.SecondaryButton)
    Private ReadOnly checkAll As CommandButton = CreateButton("DriveSubscription_CheckAll", UiStyles.Role.SecondaryButton)
    Private ReadOnly edit As CommandButton = CreateButton("DriveSubscription_EditSettings", UiStyles.Role.QuietButton)
    Private ReadOnly remove As CommandButton = CreateButton("DriveSubscription_Unsubscribe", UiStyles.Role.QuietButton)
    Private ReadOnly openLocal As CommandButton = CreateButton("Open directory", UiStyles.Role.QuietButton)
    Private ReadOnly identityDetail As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft,
        .AutoEllipsis = True, .UseMnemonic = False}
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
    Private ReadOnly reportRows As New List(Of (Subscription As MegaDriveSubscription, File As MegaDriveSubscriptionFile))()
    Private subscriptions As New List(Of MegaDriveSubscription)()
    Private shownFolder As ULong?
    Private pendingCount As Integer
    Private currentEmail As String = ""
    Private folderOnOpen As ULong?
    Private loadingRows As Boolean
    Private busy As Boolean

    Private ReadOnly Property PublicSource As Boolean
        Get
            Return accounts.SelectedIndex = 0
        End Get
    End Property

    Friend Sub New(owner As Main, service As MegaAccountService, Optional initialEmail As String = Nothing, Optional initialFolder As ULong? = Nothing)
        ownerMain = owner
        Me.service = service
        folderOnOpen = initialFolder
        Text = Language.GetText("DriveSubscription_Title")
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.Sizable
        ShowInTaskbar = False
        MinimizeBox = False
        UiStyles.InitializeForm(Me)
        ClientSize = New Size(940, 654)
        MinimumSize = New Size(760, 490)

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 7, .Padding = New Padding(16)}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 36))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 64))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))

        Dim sourceLabel = Caption("DriveSubscription_Source")
        accounts.AccessibleName = sourceLabel.Text
        layout.Controls.Add(Bar(accounts, sourceLabel, accounts, refreshButton, defaults), 0, 0)

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
        layout.Controls.Add(Bar(folderHeading, folderHeading, edit, remove, checkFolder, checkAll), 0, 1)
        layout.Controls.Add(folders, 0, 2)
        layout.Controls.Add(identityDetail, 0, 3)

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
        layout.Controls.Add(Bar(reportHeading, reportHeading, openLocal, selectPending, clear), 0, 4)
        layout.Controls.Add(report, 0, 5)

        UiStyles.SetRole(status, UiStyles.Role.Muted)
        layout.Controls.Add(Bar(status, status, queue, dismiss), 0, 6)
        Controls.Add(layout)
        dismiss.DialogResult = DialogResult.Cancel
        CancelButton = dismiss

        AddHandler Shown, Sub() LoadAccounts(If(initialEmail, service.DefaultEmail))
        AddHandler accounts.Format, AddressOf FormatAccount
        AddHandler accounts.SelectedIndexChanged, AddressOf ChooseAccount
        AddHandler folders.CurrentCellChanged, AddressOf SelectFolder
        AddHandler report.CurrentCellChanged, Sub() If Not loadingRows Then UpdateActions()
        AddHandler refreshButton.Click, Async Sub(sender, e) Await ReloadReportsAsync()
        AddHandler defaults.Click, AddressOf OpenDefaults
        AddHandler checkFolder.Click, Async Sub(sender, e) Await CheckAsync(SelectedFolder())
        AddHandler checkAll.Click, Async Sub(sender, e) Await CheckAsync(Nothing)
        AddHandler edit.Click, AddressOf EditSettings
        AddHandler remove.Click, AddressOf RemoveSubscription
        AddHandler openLocal.Click, AddressOf OpenLocalFolder
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
        accounts.Items.Add(Language.GetText("DriveSubscription_PublicShares"))
        Dim index As Integer = 0
        For Each account In service.GetAccounts()
            Dim added = accounts.Items.Add(account)
            If String.Equals(account.Email, preferred, StringComparison.OrdinalIgnoreCase) Then index = added
        Next
        accounts.SelectedIndex = index
    End Sub

    Private Sub FormatAccount(sender As Object, e As ListControlConvertEventArgs)
        If Not TypeOf e.ListItem Is MegaAccountService.Status Then Return
        Dim account = DirectCast(e.ListItem, MegaAccountService.Status)
        Dim name = MegaDriveSubscriptionSettingsForm.AccountCaption(account)
        If account.NeedsLogin Then name &= " (" & Language.GetText("MegaAccount_SignInAgain") & ")"
        e.Value = name
    End Sub

    Private Async Sub ChooseAccount(sender As Object, e As EventArgs)
        If accounts.SelectedItem Is Nothing Then Return
        currentEmail = If(PublicSource, "", DirectCast(accounts.SelectedItem, MegaAccountService.Status).Email)
        remove.Visible = PublicSource
        openLocal.Visible = PublicSource
        folders.Columns(4).MinimumWidth = If(PublicSource, 216, 92)
        folders.Columns(4).FillWeight = If(PublicSource, 34, 15)
        report.Columns(1).Visible = Not PublicSource
        UiStyles.SetText(checkFolder, Language.GetText(If(PublicSource, "DriveSubscription_PublicCheckNow", "DriveSubscription_CheckFolder")))
        checkedFiles.Clear()
        ShowReports(New List(Of MegaDriveSubscription)(), Nothing)
        Dim initialFolder = folderOnOpen
        folderOnOpen = Nothing
        If initialFolder.HasValue AndAlso Not PublicSource Then
            Await CheckAsync(initialFolder)
        Else
            Await ReloadReportsAsync(initialFolder)
        End If
    End Sub

    Private Sub SelectFolder(sender As Object, e As EventArgs)
        If loadingRows Then Return
        If PublicSource Then
            If Not Nullable.Equals(shownFolder, SelectedFolder()) Then checkedFiles.Clear()
            ShowFileRows()
            If Not busy Then status.Text = ReportSummary()
        End If
        UpdateActions()
    End Sub

    Private Function SelectedSubscription() As MegaDriveSubscription
        Return TryCast(folders.CurrentRow?.Tag, MegaDriveSubscription)
    End Function

    Private Function SelectedFolder() As ULong?
        Return SelectedSubscription()?.Folder
    End Function

    Private Sub OpenDefaults(sender As Object, e As EventArgs)
        ownerMain.OpenSettings(Me, showSubscriptions:=True)
        For Each row As DataGridViewRow In folders.Rows
            row.Cells(3).Value = NextCheckCaption(DirectCast(row.Tag, MegaDriveSubscription))
        Next
    End Sub

    Private ReadOnly Property ChecksPaused As Boolean
        Get
            Return ownerMain IsNot Nothing AndAlso ownerMain.Config IsNot Nothing AndAlso ownerMain.Config.SubscriptionChecksPaused
        End Get
    End Property

    Private Function NextCheckCaption(subscription As MegaDriveSubscription) As String
        Dim nextCheck = subscription.NextCheckLocal()
        If Not nextCheck.HasValue Then Return Language.GetText("Manual check only")
        If ChecksPaused Then Return Language.GetText("Paused")
        Return nextCheck.Value.ToString("g")
    End Function

    Private Function ReloadReportsAsync(Optional preferredFolder As ULong? = Nothing) As Task
        Return RunAndReloadAsync("DriveSubscription_Loading", preferredFolder:=preferredFolder)
    End Function

    Private Function CheckAsync(folder As ULong?) As Task
        Dim email = currentEmail
        Dim checkPublic = PublicSource
        Return RunAndReloadAsync("MegaDrive_CheckingFolder", If(checkPublic, "DriveSubscription_PublicCheckFailed", "DriveSubscription_CheckFailedMessage"),
            Async Function()
                If checkPublic Then
                    Await ownerMain.CheckPublicSubscriptionsAsync(folder)
                Else
                    Await ownerMain.CheckDriveSubscriptionsAsync(email, folder)
                End If
                Return Nothing
            End Function, preferredFolder:=folder)
    End Function

    Private Async Function RunAndReloadAsync(progressKey As String, Optional failureKey As String = Nothing,
                                             Optional action As Func(Of Task(Of String)) = Nothing,
                                             Optional preferredFolder As ULong? = Nothing) As Task
        If busy OrElse accounts.SelectedIndex < 0 Then Return
        Dim email = currentEmail
        Dim readPublic = PublicSource
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

            Dim reports = Await If(readPublic, ownerMain.ReadPublicSubscriptionReportsAsync(), ownerMain.ReadDriveSubscriptionReportsAsync(email))
            If IsDisposed OrElse Disposing OrElse Not Visible Then Return
            ShowReports(reports, If(preferredFolder, SelectedFolder()))
            status.Text = If(message, ReportSummary())
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

    Private Sub ShowReports(reports As List(Of MegaDriveSubscription), preferredFolder As ULong?)
        subscriptions = reports
        loadingRows = True
        Try
            folders.Rows.Clear()
            Dim pendingSelections As New HashSet(Of (Folder As ULong, Handle As ULong))()
            Dim preferredRow = -1
            For Each subscription In reports
                Dim folderStatus = If(PublicSource, PublicFolderStatus(subscription),
                    If(subscription.LastError = 0, "", Language.GetText("DriveSubscription_CheckFailed")))
                Dim index = folders.Rows.Add(subscription.Caption, subscription.Destination,
                    If(subscription.LastCheckedUtc.HasValue, subscription.LastCheckedUtc.GetValueOrDefault().ToLocalTime().ToString("g"), "—"),
                    NextCheckCaption(subscription), folderStatus)
                Dim row = folders.Rows(index)
                row.Tag = subscription
                If subscription.LastError <> 0 Then row.Cells(4).ToolTipText = MegaAccountService.ErrorText(subscription.LastError)
                If preferredFolder.HasValue AndAlso subscription.Folder = preferredFolder.Value Then preferredRow = index
                If checkedFiles.Count = 0 Then Continue For
                For Each file In subscription.Files
                    Dim key = (subscription.Folder, file.Handle)
                    If file.Status = MegaDriveSubscriptionFileState.PendingReview AndAlso checkedFiles.Contains(key) Then pendingSelections.Add(key)
                Next
            Next
            If folders.Rows.Count > 0 Then folders.CurrentCell = folders.Rows(Math.Max(0, preferredRow)).Cells(0)
            checkedFiles.IntersectWith(pendingSelections)
        Finally
            loadingRows = False
        End Try
        ShowFileRows()
    End Sub

    Private Sub ShowFileRows()
        Dim visible As IEnumerable(Of MegaDriveSubscription) = subscriptions
        If PublicSource Then
            Dim selected = SelectedSubscription()
            visible = If(selected Is Nothing, Enumerable.Empty(Of MegaDriveSubscription)(), {selected})
        End If
        loadingRows = True
        Try
            reportRows.Clear()
            pendingCount = 0
            For Each subscription In visible
                For Each file In subscription.Files
                    reportRows.Add((subscription, file))
                    If file.Status = MegaDriveSubscriptionFileState.PendingReview Then pendingCount += 1
                Next
            Next
            shownFolder = SelectedFolder()
            report.RowCount = reportRows.Count
            report.Invalidate()
        Finally
            loadingRows = False
        End Try
    End Sub

    Private Shared Function PublicFolderStatus(subscription As MegaDriveSubscription) As String
        If subscription.LastError <> 0 Then Return Language.GetText(If(subscription.LastError = -9,
            "DriveSubscription_PublicFolderUnavailable", "DriveSubscription_PublicCheckFailed"))
        If subscription.Files.Count = 0 Then Return ""
        Dim updated As Integer
        Dim pending As Integer
        For Each file In subscription.Files
            If file.Updated Then updated += 1
            If file.Status = MegaDriveSubscriptionFileState.PendingReview Then pending += 1
        Next
        Return Language.GetText("DriveSubscription_PublicFolderSummary").Replace("%NEW%", (subscription.Files.Count - updated).ToString("N0")).
            Replace("%UPDATED%", updated.ToString("N0")).Replace("%PENDING%", pending.ToString("N0"))
    End Function

    Private Function ReportSummary() As String
        If subscriptions.Count = 0 Then Return Language.GetText(If(PublicSource, "DriveSubscription_NoPublicSubscriptions", "DriveSubscription_NoBindings"))
        Dim selected = SelectedSubscription()
        If PublicSource Then
            If selected IsNot Nothing Then
                If selected.LastError <> 0 Then Return PublicFolderStatus(selected)
                If Not selected.LastCheckedUtc.HasValue Then Return Language.GetText("DriveSubscription_PublicNoChecks")
            End If
        Else
            If selected IsNot Nothing AndAlso selected.LastError <> 0 Then Return MegaAccountService.ErrorText(selected.LastError)
            If subscriptions.Any(Function(item) item.LastError <> 0) Then Return Language.GetText("DriveSubscription_SomeChecksFailed")
            If subscriptions.All(Function(item) Not item.LastCheckedUtc.HasValue) Then Return Language.GetText("DriveSubscription_NoChecks")
        End If
        If reportRows.Count = 0 Then Return Language.GetText("DriveSubscription_NoChanges")
        Return ""
    End Function

    Private Sub ReadReportCell(sender As Object, e As DataGridViewCellValueEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= reportRows.Count Then Return
        Dim row = reportRows(e.RowIndex)
        Dim file = row.File
        Select Case e.ColumnIndex
            Case 0 : e.Value = checkedFiles.Contains((row.Subscription.Folder, file.Handle))
            Case 1 : e.Value = row.Subscription.Caption
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
        Dim selected = SelectedSubscription()
        Dim hasFolder = selected IsNot Nothing
        accounts.Enabled = Not busy AndAlso accounts.Items.Count > 0
        refreshButton.Enabled = Not busy AndAlso accounts.SelectedIndex >= 0
        edit.Enabled = Not busy AndAlso hasFolder
        remove.Enabled = Not busy AndAlso PublicSource AndAlso hasFolder
        openLocal.Enabled = Not busy AndAlso PublicSource AndAlso SelectedLocalPath() IsNot Nothing
        identityDetail.Text = If(hasFolder,
            Language.GetText("DownloadIdentity") & "  " & MegaDriveSubscriptionSettingsForm.IdentityCaption(service,
                If(PublicSource, selected?.DownloadEmail, currentEmail)), "")
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
        Dim editingPublic = PublicSource
        Dim subscription = If(editingPublic, SelectedSubscription(), service.GetDriveSubscriptions(email).Find(Function(item) item.Folder = folder.Value))
        If subscription Is Nothing Then
            Await ReloadReportsAsync()
            Return
        End If
        Using dialog As New MegaDriveSubscriptionSettingsForm(subscription)
            Do
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim result = If(editingPublic, ownerMain.SavePublicSubscriptionSettings(dialog.Settings), service.SaveDriveSubscriptionSettings(email, dialog.Settings))
                If result = 0 Then Exit Do
                MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Loop
        End Using
        Await ReloadReportsAsync()
    End Sub

    Private Function SelectedLocalPath() As String
        Dim index = If(report.CurrentCell?.RowIndex, -1)
        Return If(index >= 0 AndAlso index < reportRows.Count, reportRows(index).File.LocalPath, Nothing)
    End Function

    Private Sub OpenLocalFolder(sender As Object, e As EventArgs)
        Dim localPath = SelectedLocalPath()
        If localPath Is Nothing Then Return
        Try
            Diagnostics.Process.Start(New Diagnostics.ProcessStartInfo(IO.Path.GetDirectoryName(localPath)) With {.UseShellExecute = True})
        Catch ex As Exception When TypeOf ex Is System.ComponentModel.Win32Exception OrElse TypeOf ex Is IOException OrElse TypeOf ex Is ArgumentException
            MessageBox.Show(Me, Language.GetText("DriveSubscription_OpenLocalFolderFailed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Async Sub RemoveSubscription(sender As Object, e As EventArgs)
        Dim selected = SelectedSubscription()
        If busy OrElse Not PublicSource OrElse selected Is Nothing Then Return
        Using confirmation As New Form With {.Text = remove.Text, .StartPosition = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog, .ShowInTaskbar = False, .MinimizeBox = False, .MaximizeBox = False,
            .ClientSize = New Size(460, 174)}
            UiStyles.InitializeForm(confirmation)
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(16)}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            Dim question As New Label With {.Dock = DockStyle.Fill, .UseMnemonic = False,
                .Text = Language.GetText("DriveSubscription_UnsubscribeConfirm").Replace("%NAME%", selected.Caption)}
            Dim stopFollowing = CreateButton("DriveSubscription_Unsubscribe", UiStyles.Role.SecondaryButton)
            Dim keep = CreateButton("DriveSubscription_KeepSubscription", UiStyles.Role.PrimaryButton)
            stopFollowing.DialogResult = DialogResult.OK
            keep.DialogResult = DialogResult.Cancel
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft}
            actions.Controls.AddRange({keep, stopFollowing})
            layout.Controls.Add(question, 0, 0)
            layout.Controls.Add(actions, 0, 1)
            confirmation.Controls.Add(layout)
            confirmation.AcceptButton = keep
            confirmation.CancelButton = keep
            ThemeManager.ApplyTheme(confirmation)
            If confirmation.ShowDialog(Me) <> DialogResult.OK Then Return
        End Using
        Dim result = ownerMain.RemovePublicSubscription(selected.Folder)
        If result = 0 Then
            Await ReloadReportsAsync()
        Else
            MessageBox.Show(Me, MegaAccountService.ErrorText(result), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Async Sub QueueSelection(sender As Object, e As EventArgs)
        If busy OrElse checkedFiles.Count = 0 Then Return
        Dim selection = checkedFiles.GroupBy(Function(item) item.Folder).
            ToDictionary(Function(group) group.Key, Function(group) New HashSet(Of ULong)(group.Select(Function(item) item.Handle)))
        Dim confirm = Language.GetText("DriveSubscription_QueueConfirm").Replace("%N%", checkedFiles.Count.ToString("N0"))
        If MessageBox.Show(Me, confirm, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

        Dim email = currentEmail
        Dim publicSelection = PublicSource
        Await RunAndReloadAsync("MegaDrive_Adding", "DriveSubscription_QueueFailed",
            Async Function()
                Dim result = Await If(publicSelection, ownerMain.QueuePublicSubscriptionFilesAsync(selection), ownerMain.QueueDriveSubscriptionFilesAsync(email, selection))
                If result < 0 Then Return MegaAccountService.ErrorText(result)
                If result = 0 Then Return Language.GetText("DriveSubscription_QueueNone")
                Return Language.GetText("DriveSubscription_QueueAdded").Replace("%N%", result.ToString("N0"))
            End Function)
    End Sub
End Class
