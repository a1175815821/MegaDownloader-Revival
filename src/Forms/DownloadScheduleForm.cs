using System;
using System.Drawing;
using System.Windows.Forms;

namespace MegaDownloader
{

    public sealed class DownloadScheduleForm : Form
    {

        private readonly CheckBox enabledBox = new CheckBox();
        private readonly RadioButton dailyButton = new RadioButton();
        private readonly RadioButton onceButton = new RadioButton();
        private readonly DateTimePicker startPicker = new DateTimePicker();
        private readonly DateTimePicker endPicker = new DateTimePicker();
        private bool modeWasDaily;
        private DateTime dailyStart;
        private DateTime dailyEnd;
        private DateTime onceStart;
        private DateTime onceEnd;
        public DownloadScheduleSettings Settings { get; private set; }

        public DownloadScheduleForm(DownloadScheduleSettings current)
        {
            Text = Language.GetText("Schedule_Title");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            UiStyles.InitializeForm(this);
            ClientSize = new Size(580, 340);

            var layout = new TableLayoutPanel() { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };
            UiStyles.SetRole(layout, UiStyles.Role.Surface);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            enabledBox.Text = Language.GetText("Schedule_Enable");
            enabledBox.AutoSize = true;
            UiStyles.SetFont(enabledBox, 1f, true, Font);
            enabledBox.Checked = current.Enabled;
            layout.Controls.Add(enabledBox, 0, 0);
            layout.SetColumnSpan(enabledBox, 2);

            var repeatPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, WrapContents = false };
            UiStyles.SetRole(repeatPanel, UiStyles.Role.Surface);
            dailyButton.Text = Language.GetText("Schedule_Daily");
            onceButton.Text = Language.GetText("Schedule_Once");
            dailyButton.AutoSize = true;
            onceButton.AutoSize = true;
            dailyButton.Checked = current.Daily;
            onceButton.Checked = !current.Daily;
            repeatPanel.Controls.Add(dailyButton);
            repeatPanel.Controls.Add(onceButton);
            layout.Controls.Add(repeatPanel, 0, 1);
            layout.SetColumnSpan(repeatPanel, 2);
            layout.Controls.Add(new Label() { Text = Language.GetText("Schedule_Start"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            layout.Controls.Add(startPicker, 1, 2);
            layout.Controls.Add(new Label() { Text = Language.GetText("Schedule_End"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            layout.Controls.Add(endPicker, 1, 3);
            startPicker.Dock = DockStyle.Fill;
            endPicker.Dock = DockStyle.Fill;
            modeWasDaily = current.Daily;
            dailyStart = DateTime.Today.AddMinutes(Math.Max(0, Math.Min(1439, current.StartMinute)));
            dailyEnd = DateTime.Today.AddMinutes(Math.Max(0, Math.Min(1439, current.EndMinute)));
            onceStart = ClampDate(current.OnceStart, startPicker);
            onceEnd = ClampDate(current.OnceEnd, endPicker);
            startPicker.Value = current.Daily ? dailyStart : onceStart;
            endPicker.Value = current.Daily ? dailyEnd : onceEnd;
            var note = new Label() { Text = Language.GetText("Schedule_Note"), Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            UiStyles.SetRole(note, UiStyles.Role.Muted);
            layout.Controls.Add(note, 0, 4);
            layout.SetColumnSpan(note, 2);
            var buttons = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new CommandButton() { Text = Language.GetText("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var save = new CommandButton() { Text = Language.GetText("Save"), AutoSize = true };
            UiStyles.SetRole(buttons, UiStyles.Role.Surface);
            UiStyles.SetRole(save, UiStyles.Role.PrimaryButton);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
            dailyButton.CheckedChanged += UpdatePickers;
            enabledBox.CheckedChanged += UpdatePickers;
            save.Click += SaveSettings;
            UpdatePickers(null, EventArgs.Empty);
            ThemeManager.ApplyTheme(this);
        }

        private void UpdatePickers(object sender, EventArgs e)
        {
            if (modeWasDaily != dailyButton.Checked)
            {
                if (modeWasDaily)
                {
                    dailyStart = startPicker.Value;
                    dailyEnd = endPicker.Value;
                    startPicker.Value = onceStart;
                    endPicker.Value = onceEnd;
                }
                else
                {
                    onceStart = startPicker.Value;
                    onceEnd = endPicker.Value;
                    startPicker.Value = dailyStart;
                    endPicker.Value = dailyEnd;
                }
                modeWasDaily = dailyButton.Checked;
            }
            dailyButton.Enabled = enabledBox.Checked;
            onceButton.Enabled = enabledBox.Checked;
            foreach (DateTimePicker picker in new DateTimePicker[] { startPicker, endPicker })
            {
                picker.Format = DateTimePickerFormat.Custom;
                picker.CustomFormat = dailyButton.Checked ? "HH:mm" : "yyyy-MM-dd HH:mm";
                picker.ShowUpDown = dailyButton.Checked;
                picker.Enabled = enabledBox.Checked;
            }
        }

        private static DateTime ClampDate(DateTime value, DateTimePicker picker)
        {
            if (value < picker.MinDate)
                return picker.MinDate;
            if (value > picker.MaxDate)
                return picker.MaxDate;
            return value;
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            var value = new DownloadScheduleSettings()
            {
                Enabled = enabledBox.Checked,
                Daily = dailyButton.Checked,
                StartMinute = dailyButton.Checked ? startPicker.Value.Hour * 60 + startPicker.Value.Minute : dailyStart.Hour * 60 + dailyStart.Minute,
                EndMinute = dailyButton.Checked ? endPicker.Value.Hour * 60 + endPicker.Value.Minute : dailyEnd.Hour * 60 + dailyEnd.Minute,
                OnceStart = dailyButton.Checked ? onceStart : new DateTime(startPicker.Value.Year, startPicker.Value.Month, startPicker.Value.Day, startPicker.Value.Hour, startPicker.Value.Minute, 0),
                OnceEnd = dailyButton.Checked ? onceEnd : new DateTime(endPicker.Value.Year, endPicker.Value.Month, endPicker.Value.Day, endPicker.Value.Hour, endPicker.Value.Minute, 0)
            };
            if (value.Enabled && !value.IsValid())
            {
                MessageBox.Show(this, Language.GetText("Schedule_Invalid"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Settings = value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}