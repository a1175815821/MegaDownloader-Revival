using System;
using System.Drawing;
using System.Windows.Forms;

namespace MegaDownloader
{

    public partial class Configuration
    {
        private void InitConfigurationLayout()
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(720, 540);
            ClientSize = new Size(880, 700);

            var shell = new TableLayoutPanel() { ColumnCount = 1, Margin = new Padding(0) };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            UiStyles.SetRole(shell, UiStyles.Role.Surface);
            shell.Dock = DockStyle.Fill;
            shell.AutoSize = false;
            shell.Padding = new Padding(24);
            shell.RowCount = 3;
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var heading = new TableLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 20) };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f));
            UiStyles.SetRole(heading, UiStyles.Role.Surface);
            var title = new Label() { Text = Text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
            UiStyles.SetFont(title, 6f, true, Font);
            TextChanged += (s, e) => title.Text = Text;
            var clear = new CommandButton() { Text = "×", TabStop = false, AccessibleName = Language.GetText("UI_ClearSearch") };
            UiStyles.SetRole(clear, UiStyles.Role.QuietButton);
            var search = new SearchFrame(txtSearchConfig, clear) { Dock = DockStyle.Fill, Height = 36, Margin = new Padding(12, 0, 0, 0) };
            clear.Click += (s, e) => txtSearchConfig.Clear();
            heading.Controls.Add(title, 0, 0);
            heading.Controls.Add(search, 1, 0);
            shell.Controls.Add(heading, 0, 0);

            var connectionPage = new TabPage() { Name = "ConnectionPage", Text = ConexionGroup.Text };
            ConexionGroup.TextChanged += (s, e) => connectionPage.Text = ConexionGroup.Text;
            TabControl1.TabPages.Clear();
            TabControl1.TabPages.AddRange(new TabPage[] { TabPage1, connectionPage, TabPage2, TabPage3, TabPage4, TabPage5 });
            TabControl1.Dock = DockStyle.Fill;
            TabControl1.Margin = new Padding(0);
            TabControl1.Multiline = true;
            TabControl1.Padding = new Point(16, 9);

            SettingsSection(GroupBox1, FieldRow(Label2, txtUsuario), FieldRow(Label3, txtPassword, chkShowPassword));
            var options = new TableLayoutPanel() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 4, Margin = new Padding(0) };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            UiStyles.SetRole(options, UiStyles.Role.Surface);
            for (int index = 0; index <= 3; index++)
                options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            Control[] checks = new Control[] { chkAnalisisPortapapeles, chkCrearDirectorio, chkComenzarPlay, InlineControls(chkApagarPC, linkApagarPC), chkStartWindows, InlineControls(chkUltimaConfig, linkUltConfig), chkCheckUpdates, InlineControls(chkUnZip, comboPrioridad) };
            // 6: 更新检查行：间隔（小时） + 仅手动。Interval>0 有意义，ManualOnly 独立于 CheckUpdates。
            // “h” 在区域设置下无意义：翻译后长尾巴会把复选框挤出可视区，沿用本语言的 hours 词。
            var updateIntervalLabel = new Label() { Text = Language.GetText("hours"), AutoSize = true };
            var updateRow = new FlowLayoutPanel() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0) };
            updateRow.Controls.AddRange(new Control[] { chkUpdateManualOnly, txtUpdateCheckInterval, updateIntervalLabel });
            UiStyles.SetRole(updateRow, UiStyles.Role.Surface);
            foreach (Control c in updateRow.Controls)
            {
                c.Margin = new Padding(0, 3, 8, 3);
                if (c is TextBox)
                    c.Width = 44;
            }

            for (int index = 0, loopTo = checks.Length - 1; index <= loopTo; index++)
            {
                checks[index].Margin = new Padding(0, 6, 16, 6);
                options.Controls.Add(checks[index], index % 2, index / 2);
            }
            SettingsSection(GroupBox4, FieldRow(Label1, txtRuta, btnExaminar), Divider(), options, updateRow, Divider(), FieldRow(Label24, comboIdiomas), FieldRow(Label25, comboTheme));
            SettingsSection(ConexionGroup, InlineControls(chkLimitarVelocidad, txtLimiteVelocidadKBs, Label15), InlineControls(chkReintentarError, txtPeriodoReintento, Label12), chkQuotaBreak, FieldRow(Label10, txtDescSimult), FieldRow(Label9, txtConFic), LinkLabel1);
            SettingsSection(GroupBox2, chkProxy, Divider(), FieldRow(Label13, txtProxyIP), FieldRow(Label14, txtProxyPort), FieldRow(Label23, txtProxyName), FieldRow(Label22, txtProxyPassword));
            UiStyles.SetAppFont(Label8);
            UiStyles.SetRole(Label8, UiStyles.Role.Muted);
            SettingsSection(GroupBox3, Label8, FieldRow(Label4, txtTamanoPaquete, Label5), FieldRow(Label6, txtTamanoBuffer, Label7), FieldRow(Label11, comboLog));
            SettingsSection(GroupBox5, chkServidorWeb, chkServidorWebLAN, Divider(), FieldRow(lblServidorWebBindIP, txtServidorWebBindIP), FieldRow(Label16, txtServidorWebPort), FieldRow(Label18, txtServidorWebTimeout, Label19), FieldRow(Label17, txtServidorWebPassword), FieldRow(Label20, txtServidorWebNombre), FieldRow(Label21, txtServidorWebTemplate, btnExaminarTemplate));
            txtKeyList.MinimumSize = new Size(0, 200);
            txtKeyList.Dock = DockStyle.Top;
            SettingsSection(GroupBox, txtKeyList);
            SettingsSection(GroupBox6, chkStreamingServer, FieldRow(lblStreamingPort, txtStreamingPort), FieldRow(lblStreamingPassword, txtStreamingPassword));
            SettingsSection(GroupBox8, FieldRow(Label26, txtVLCPath, btnExaminarVLCPath), linkDownloadVLC);
            UiStyles.SetRole(lblInfoStreaming, UiStyles.Role.Muted);
            SettingsSection(GroupBox7, lblInfoStreaming);

            SettingsPage(TabPage1, GroupBox4, GroupBox1);
            SettingsPage(connectionPage, ConexionGroup, GroupBox2);
            SettingsPage(TabPage2, GroupBox3, GroupBox5);
            SettingsPage(TabPage3, lblDesc, GroupBox);
            SettingsPage(TabPage4, GroupBox6, GroupBox8, GroupBox7);
            ElcAccountControl.Dock = DockStyle.Fill;
            TabPage5.Padding = new Padding(12);
            UiStyles.SetRole(TabPage5, UiStyles.Role.Surface);
            shell.Controls.Add(TabControl1, 0, 1);

            var actions = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 20, 0, 0) };
            foreach (Button button in new Button[] { btnGuardar, btnCancel })
            {
                button.AutoSize = true;
                button.Padding = new Padding(16, 7, 16, 7);
                button.MinimumSize = new Size(100, 36);
                button.Margin = new Padding(8, 0, 0, 0);
            }
            UiStyles.SetRole(btnGuardar, UiStyles.Role.PrimaryButton);
            UiStyles.SetRole(btnCancel, UiStyles.Role.SecondaryButton);
            actions.Controls.AddRange(new Control[] { btnGuardar, btnCancel });
            UiStyles.SetRole(actions, UiStyles.Role.Surface);
            shell.Controls.Add(actions, 0, 2);
            Controls.Add(shell);
            shell.BringToFront();
            AcceptButton = btnGuardar;
            CancelButton = btnCancel;
            Shown += (s, e) => FitConfigurationWindow();
        }

        private void FitConfigurationWindow()
        {
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(LogicalToDeviceUnits(640), area.Width), Math.Min(LogicalToDeviceUnits(480), area.Height));
            if (WindowState != FormWindowState.Normal)
                return;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            MinimumSize = Size.Empty;
            base.OnDpiChanged(e);
            FitConfigurationWindow();
        }

        private static SettingsStackPanel StackLayout()
        {
            var layout = new SettingsStackPanel() { Dock = DockStyle.Top, Margin = new Padding(0) };
            UiStyles.SetRole(layout, UiStyles.Role.Surface);
            return layout;
        }

        private static FlowLayoutPanel InlineControls(params Control[] controls)
        {
            var line = new FlowLayoutPanel() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0) };
            foreach (Control control in controls)
            {
                control.Anchor = AnchorStyles.Left;
                control.Margin = new Padding(0, 3, 8, 3);
                if (control is TextBox)
                    control.Width = 64;
                if (control is ComboBox)
                    control.Width = 110;
                line.Controls.Add(control);
            }
            UiStyles.SetRole(line, UiStyles.Role.Surface);
            return line;
        }

        private static SettingsFieldRow FieldRow(Label caption, Control input, params Control[] trailing)
        {
            if (input is TextBox)
                ((TextBox)input).BorderStyle = BorderStyle.FixedSingle;
            foreach (Control control in trailing)
            {
                if (control is Button)
                    control.Padding = new Padding(12, 4, 12, 4);
            }
            var row = new SettingsFieldRow(caption, input, trailing) { Margin = new Padding(0, 4, 0, 4) };
            UiStyles.SetRole(row, UiStyles.Role.Surface);
            return row;
        }

        private static SettingsDivider Divider()
        {
            var d = new SettingsDivider();
            UiStyles.SetRole(d, UiStyles.Role.Surface);
            return d;
        }

        private static void SettingsSection(GroupBox @group, params Control[] rows)
        {
            group.Controls.Clear();
            group.AutoSize = false;
            group.Dock = DockStyle.None;
            group.Margin = new Padding(0, 0, 0, 24);
            group.Padding = new Padding(12, 16, 12, 12);
            var body = StackLayout();
            body.Dock = DockStyle.None;
            body.SuspendLayout();
            foreach (Control row in rows)
            {
                row.Dock = DockStyle.None;
                row.Margin = new Padding(0, 4, 0, 4);
                if (row is Label)
                    row.AutoSize = true;
                body.Controls.Add(row);
            } ((SettingsSection)group).SetBody(body);
            body.ResumeLayout(false);
            UiStyles.SetRole(group, UiStyles.Role.Surface);
        }

        private static void SettingsPage(TabPage page, params Control[] sections)
        {
            page.Controls.Clear();
            page.Padding = new Padding(0);
            page.AutoScroll = true;
            var body = StackLayout();
            body.Padding = new Padding(4, 20, 16, 0);
            body.SuspendLayout();
            foreach (Control section in sections)
            {
                section.Dock = DockStyle.None;
                if (section is Label)
                    section.AutoSize = true;
                body.Controls.Add(section);
            }
            page.Controls.Add(body);
            body.ResumeLayout(false);
            UiStyles.SetRole(page, UiStyles.Role.Surface);
        }

        private void HelpButtonPressed(object sender, System.ComponentModel.CancelEventArgs e) => HelpButtonPressed();
    }
}