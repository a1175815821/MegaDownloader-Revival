Imports System.Drawing
Imports System.Windows.Forms

Public NotInheritable Class DownloadScheduleForm
    Inherits Form

    Private ReadOnly enabledBox As New CheckBox()
    Private ReadOnly dailyButton As New RadioButton()
    Private ReadOnly onceButton As New RadioButton()
    Private ReadOnly startPicker As New DateTimePicker()
    Private ReadOnly endPicker As New DateTimePicker()
    Private modeWasDaily As Boolean
    Private dailyStart As DateTime
    Private dailyEnd As DateTime
    Private onceStart As DateTime
    Private onceEnd As DateTime
    Public ReadOnly Property Settings As DownloadScheduleSettings

    Public Sub New(current As DownloadScheduleSettings)
        Me.Text = Language.GetText("Schedule_Title")
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.ShowInTaskbar = False
        Me.AutoScaleMode = AutoScaleMode.Font
        UiStyles.SetAppFont(Me)
        Me.ClientSize = New Size(580, 340)

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(16), .ColumnCount = 2, .RowCount = 6}
        UiStyles.SetRole(layout, UiStyles.Role.Surface)
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 32))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 68))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        enabledBox.Text = Language.GetText("Schedule_Enable")
        enabledBox.AutoSize = True
        UiStyles.SetFont(enabledBox, 1, True, Me.Font)
        enabledBox.Checked = current.Enabled
        layout.Controls.Add(enabledBox, 0, 0)
        layout.SetColumnSpan(enabledBox, 2)

        Dim repeatPanel As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False}
        UiStyles.SetRole(repeatPanel, UiStyles.Role.Surface)
        dailyButton.Text = Language.GetText("Schedule_Daily")
        onceButton.Text = Language.GetText("Schedule_Once")
        dailyButton.AutoSize = True
        onceButton.AutoSize = True
        dailyButton.Checked = current.Daily
        onceButton.Checked = Not current.Daily
        repeatPanel.Controls.Add(dailyButton)
        repeatPanel.Controls.Add(onceButton)
        layout.Controls.Add(repeatPanel, 0, 1)
        layout.SetColumnSpan(repeatPanel, 2)
        layout.Controls.Add(New Label With {.Text = Language.GetText("Schedule_Start"), .AutoSize = True, .Anchor = AnchorStyles.Left}, 0, 2)
        layout.Controls.Add(startPicker, 1, 2)
        layout.Controls.Add(New Label With {.Text = Language.GetText("Schedule_End"), .AutoSize = True, .Anchor = AnchorStyles.Left}, 0, 3)
        layout.Controls.Add(endPicker, 1, 3)
        startPicker.Dock = DockStyle.Fill
        endPicker.Dock = DockStyle.Fill
        modeWasDaily = current.Daily
        dailyStart = DateTime.Today.AddMinutes(Math.Max(0, Math.Min(1439, current.StartMinute)))
        dailyEnd = DateTime.Today.AddMinutes(Math.Max(0, Math.Min(1439, current.EndMinute)))
        onceStart = ClampDate(current.OnceStart, startPicker)
        onceEnd = ClampDate(current.OnceEnd, endPicker)
        startPicker.Value = If(current.Daily, dailyStart, onceStart)
        endPicker.Value = If(current.Daily, dailyEnd, onceEnd)
        Dim note As New Label With {.Text = Language.GetText("Schedule_Note"), .Dock = DockStyle.Fill, .Padding = New Padding(0, 8, 0, 0)}
        UiStyles.SetRole(note, UiStyles.Role.Muted)
        layout.Controls.Add(note, 0, 4)
        layout.SetColumnSpan(note, 2)
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
        Dim cancel As New Button With {.Text = Language.GetText("Cancel"), .DialogResult = DialogResult.Cancel, .AutoSize = True}
        Dim save As New Button With {.Text = Language.GetText("Save"), .AutoSize = True}
        UiStyles.SetRole(buttons, UiStyles.Role.Surface)
        UiStyles.SetRole(save, UiStyles.Role.PrimaryButton)
        buttons.Controls.Add(cancel)
        buttons.Controls.Add(save)
        layout.Controls.Add(buttons, 0, 5)
        layout.SetColumnSpan(buttons, 2)
        Me.Controls.Add(layout)
        Me.AcceptButton = save
        Me.CancelButton = cancel
        AddHandler dailyButton.CheckedChanged, AddressOf UpdatePickers
        AddHandler enabledBox.CheckedChanged, AddressOf UpdatePickers
        AddHandler save.Click, AddressOf SaveSettings
        UpdatePickers(Nothing, EventArgs.Empty)
        ThemeManager.ApplyTheme(Me)
    End Sub

    Private Sub UpdatePickers(sender As Object, e As EventArgs)
        If modeWasDaily <> dailyButton.Checked Then
            If modeWasDaily Then
                dailyStart = startPicker.Value
                dailyEnd = endPicker.Value
                startPicker.Value = onceStart
                endPicker.Value = onceEnd
            Else
                onceStart = startPicker.Value
                onceEnd = endPicker.Value
                startPicker.Value = dailyStart
                endPicker.Value = dailyEnd
            End If
            modeWasDaily = dailyButton.Checked
        End If
        dailyButton.Enabled = enabledBox.Checked
        onceButton.Enabled = enabledBox.Checked
        For Each picker As DateTimePicker In New DateTimePicker() {startPicker, endPicker}
            picker.Format = DateTimePickerFormat.Custom
            picker.CustomFormat = If(dailyButton.Checked, "HH:mm", "yyyy-MM-dd HH:mm")
            picker.ShowUpDown = dailyButton.Checked
            picker.Enabled = enabledBox.Checked
        Next
    End Sub

    Private Shared Function ClampDate(value As DateTime, picker As DateTimePicker) As DateTime
        If value < picker.MinDate Then Return picker.MinDate
        If value > picker.MaxDate Then Return picker.MaxDate
        Return value
    End Function

    Private Sub SaveSettings(sender As Object, e As EventArgs)
        Dim value As New DownloadScheduleSettings With {
            .Enabled = enabledBox.Checked, .Daily = dailyButton.Checked,
            .StartMinute = If(dailyButton.Checked, startPicker.Value.Hour * 60 + startPicker.Value.Minute, dailyStart.Hour * 60 + dailyStart.Minute),
            .EndMinute = If(dailyButton.Checked, endPicker.Value.Hour * 60 + endPicker.Value.Minute, dailyEnd.Hour * 60 + dailyEnd.Minute),
            .OnceStart = If(dailyButton.Checked, onceStart, New DateTime(startPicker.Value.Year, startPicker.Value.Month, startPicker.Value.Day, startPicker.Value.Hour, startPicker.Value.Minute, 0)),
            .OnceEnd = If(dailyButton.Checked, onceEnd, New DateTime(endPicker.Value.Year, endPicker.Value.Month, endPicker.Value.Day, endPicker.Value.Hour, endPicker.Value.Minute, 0))}
        If value.Enabled AndAlso Not value.IsValid() Then
            MessageBox.Show(Me, Language.GetText("Schedule_Invalid"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        _Settings = value
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub
End Class
