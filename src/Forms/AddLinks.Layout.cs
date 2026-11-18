using System;
using System.Drawing;
using System.Windows.Forms;

namespace MegaDownloader
{

    public partial class AddLinks
    {
        private Panel addLinksViewport;
        private TableLayoutPanel addLinksContent;
        private bool addLinksLayoutReady;
        private bool updatingAddLinksLayout;
        private TableLayoutPanel addLinksInputPage;
        private Panel addLinksPageHost;
        private Label addLinksStepLabel;
        private Label addLinksFeedback;
        private Button addLinksCancel;

        private void InitAddLinksLayout()
        {
            // Use the designer's Segoe UI 9 / 96-DPI baseline, before automatic scaling.
            var layout = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 1,
                RowCount = 2,
                TabIndex = 0
            };
            addLinksInputPage = layout;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Keep confirmation outside the scroll area on short high-DPI screens.
            addLinksViewport = new Panel()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Margin = new Padding(0, 0, 0, 12),
                TabIndex = 0
            };
            addLinksContent = new TableLayoutPanel()
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 5,
                Margin = new Padding(0),
                TabIndex = 0
            };
            addLinksContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            addLinksContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            addLinksContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 160f));
            addLinksContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            addLinksContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            addLinksContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var heading = new Label()
            {
                Text = Language.GetText("AddLinks_InputHint"),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12)
            };
            UiStyles.SetRole(heading, UiStyles.Role.Muted);
            addLinksContent.Controls.Add(heading, 0, 0);
            txtLinks.Size = new Size(100, 160);
            txtLinks.Dock = DockStyle.Fill;
            txtLinks.MinimumSize = new Size(0, 160);
            txtLinks.Margin = new Padding(0);
            txtLinks.BorderStyle = BorderStyle.FixedSingle;
            txtLinks.TabIndex = 0;
            addLinksContent.Controls.Add(txtLinks, 0, 1);

            var progress = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 4, 0, 10)
            };
            progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            progress.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            progress.RowStyles.Add(new RowStyle(SizeType.Absolute, 6f));
            lblLinkCount.AutoSize = true;
            lblLinkCount.MinimumSize = new Size(0, 36);
            lblLinkCount.Dock = DockStyle.Fill;
            lblLinkCount.Margin = new Padding(0);
            preflightProgressBar.Dock = DockStyle.Fill;
            preflightProgressBar.Margin = new Padding(0);
            progress.Controls.Add(lblLinkCount, 0, 0);
            progress.Controls.Add(preflightProgressBar, 0, 1);
            addLinksContent.Controls.Add(progress, 0, 2);

            OpcionesPaquete.Controls.Clear();
            OpcionesPaquete.Dock = DockStyle.Fill;
            OpcionesPaquete.AutoSize = true;
            OpcionesPaquete.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            OpcionesPaquete.Padding = new Padding(12, 16, 12, 12);
            OpcionesPaquete.Margin = new Padding(0, 0, 0, 12);
            OpcionesPaquete.TabIndex = 1;
            var options = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 4,
                Margin = new Padding(0)
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int row = 0; row <= 3; row++)
                options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            foreach (Label caption in new Label[] { Label2, Label1 })
            {
                caption.Anchor = AnchorStyles.Left;
                caption.MinimumSize = Size.Empty;
                caption.TextAlign = ContentAlignment.MiddleLeft;
            }
            // Discard widths inherited from the old absolute-position layout.
            foreach (TextBox input in new TextBox[] { txtNombre, txtRuta })
            {
                input.Width = 100;
                input.Dock = DockStyle.Fill;
            }
            options.Controls.Add(Label2, 0, 0);
            options.Controls.Add(txtNombre, 1, 0);
            options.SetColumnSpan(txtNombre, 2);
            options.Controls.Add(Label1, 0, 1);
            options.Controls.Add(txtRuta, 1, 1);
            btnExaminar.AutoSize = true;
            btnExaminar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnExaminar.Padding = new Padding(8, 2, 8, 2);
            options.Controls.Add(btnExaminar, 2, 1);
            var flags = new FlowLayoutPanel()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0)
            };
            flags.Controls.AddRange(new Control[] { chkStartDownload, chkCrearDirectorio, chkUnZip });
            foreach (Control flag in flags.Controls)
            {
                flag.MinimumSize = Size.Empty;
                flag.Margin = new Padding(0, 6, 16, 4);
            }
            options.Controls.Add(flags, 0, 2);
            options.SetColumnSpan(flags, 3);
            var passwordRow = new FlowLayoutPanel()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0)
            };
            lblPassword.MinimumSize = Size.Empty;
            lblPassword.Margin = new Padding(0, 6, 6, 0);
            lblPassword.TextAlign = ContentAlignment.MiddleLeft;
            txtPassword.Width = 180;
            txtPassword.Margin = new Padding(0, 3, 6, 3);
            LinkLabel2.Margin = new Padding(0, 6, 0, 0);
            passwordRow.Controls.AddRange(new Control[] { lblPassword, txtPassword, LinkLabel2 });
            options.Controls.Add(passwordRow, 0, 3);
            options.SetColumnSpan(passwordRow, 3);
            OpcionesPaquete.Controls.Add(options);
            addLinksContent.Controls.Add(OpcionesPaquete, 0, 3);

            linkStegano.MinimumSize = Size.Empty;
            linkStegano.Margin = new Padding(0);
            linkStegano.Dock = DockStyle.Fill;
            linkStegano.TextAlign = ContentAlignment.MiddleLeft;
            linkStegano.TabIndex = 2;
            addLinksContent.Controls.Add(linkStegano, 0, 4);
            addLinksViewport.Controls.Add(addLinksContent);
            layout.Controls.Add(addLinksViewport, 0, 0);

            var actions = new FlowLayoutPanel()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                TabIndex = 1
            };
            addLinksCancel = new CommandButton() { Text = Language.GetText("Cancel") };
            addLinksCancel.Click += (s, e) => Close();
            foreach (Button button in new Button[] { btnAgregar, btnWatchOnline, addLinksCancel })
            {
                button.AutoSize = true;
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                button.Padding = new Padding(12, 5, 12, 5);
                button.MinimumSize = new Size(112, 34);
                button.Margin = new Padding(0, 0, 8, 0);
            }
            btnAgregar.TabIndex = 0;
            btnWatchOnline.TabIndex = 1;
            LinkLabel1.Padding = new Padding(0, 8, 0, 0);
            LinkLabel1.TabIndex = 2;
            actions.Controls.AddRange(new Control[] { btnAgregar, addLinksCancel, btnWatchOnline, LinkLabel1 });
            layout.Controls.Add(actions, 0, 1);
            FlowLayoutPanel1.Visible = false;
            var shell = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            addLinksStepLabel = new Label()
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Text = Language.GetText("AddLinks_StepInput"),
                Margin = new Padding(24, 20, 24, 0)
            };
            UiStyles.SetFont(addLinksStepLabel, 4f, true, Font);
            addLinksFeedback = new Label() { AutoSize = true, Dock = DockStyle.Fill, Visible = false, Margin = new Padding(24, 8, 24, 0) };
            addLinksPageHost = new Panel() { Dock = DockStyle.Fill, Margin = new Padding(0) };
            addLinksPageHost.Controls.Add(layout);
            shell.Controls.Add(addLinksStepLabel, 0, 0);
            shell.Controls.Add(addLinksFeedback, 0, 1);
            shell.Controls.Add(addLinksPageHost, 0, 2);
            Controls.Add(shell);
            shell.BringToFront();
            foreach (Control surface in new Control[] { layout, addLinksViewport, addLinksContent, options, flags, passwordRow, actions, progress, OpcionesPaquete, shell, addLinksPageHost })
                UiStyles.SetRole(surface, UiStyles.Role.Surface);
            UiStyles.SetRole(btnAgregar, UiStyles.Role.PrimaryButton);
            UiStyles.SetRole(btnWatchOnline, UiStyles.Role.QuietButton);
            UiStyles.SetRole(addLinksCancel, UiStyles.Role.SecondaryButton);
            AcceptButton = btnAgregar;
            CancelButton = addLinksCancel;
            addLinksViewport.ClientSizeChanged += AddLinksViewportSizeChanged;
            addLinksContent.Layout += AddLinksContentLayout;
            txtLinks.ClientSizeChanged += AddLinksInputSizeChanged;
        }

        private void AddLinksViewportSizeChanged(object sender, EventArgs e)
        {
            UpdateAddLinksInputHeight();
        }

        private void AddLinksContentLayout(object sender, LayoutEventArgs e)
        {
            UpdateAddLinksInputHeight();
        }

        private void UpdateAddLinksInputHeight()
        {
            if (!addLinksLayoutReady || updatingAddLinksLayout || IsDisposed)
                return;
            updatingAddLinksLayout = true;
            try
            {
                var inputRow = addLinksContent.RowStyles[1];
                var preferred = addLinksContent.GetPreferredSize(new Size(addLinksViewport.ClientSize.Width, 0));
                int otherHeight = Math.Max(0, preferred.Height - (int)Math.Round(inputRow.Height));
                int inputHeight = Math.Max(txtLinks.MinimumSize.Height, addLinksViewport.ClientSize.Height - otherHeight);
                if ((int)Math.Round(inputRow.Height) != inputHeight)
                    inputRow.Height = inputHeight;
            }
            finally
            {
                updatingAddLinksLayout = false;
            }
        }

        private void FitAddLinksToWorkingArea(bool centerOnOwner)
        {
            var screenControl = centerOnOwner && Owner is not null ? (Control)Owner : this;
            var area = Screen.FromControl(screenControl).WorkingArea;
            // Lower the minimum before clamping: Windows otherwise rejects the smaller size.
            MinimumSize = new Size(Math.Min(LogicalToDeviceUnits(640), area.Width), Math.Min(LogicalToDeviceUnits(480), area.Height));
            var fitted = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            var origin = Location;
            if (centerOnOwner)
            {
                var anchor = Owner is not null && Owner.WindowState != FormWindowState.Minimized ? Owner.Bounds : area;
                origin = new Point(anchor.Left + (anchor.Width - fitted.Width) / 2, anchor.Top + (anchor.Height - fitted.Height) / 2);
                StartPosition = FormStartPosition.Manual;
            }
            origin.X = Math.Max(area.Left, Math.Min(origin.X, area.Right - fitted.Width));
            origin.Y = Math.Max(area.Top, Math.Min(origin.Y, area.Bottom - fitted.Height));
            Bounds = new Rectangle(origin, fitted);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            bool wasReady = addLinksLayoutReady;
            addLinksLayoutReady = false;
            MinimumSize = Size.Empty;
            try
            {
                base.OnDpiChanged(e);
            }
            finally
            {
                addLinksLayoutReady = wasReady;
            }
            if (wasReady)
            {
                FitAddLinksToWorkingArea(false);
                UpdateAddLinksInputHeight();
                UpdateAddLinksCueBounds();
            }
        }

        private void AddLinksInputSizeChanged(object sender, EventArgs e)
        {
            UpdateAddLinksCueBounds();
        }

        private void UpdateAddLinksCueBounds()
        {
            if (_cueFallbackLabel is null || _cueFallbackLabel.IsDisposed)
                return;
            int inset = LogicalToDeviceUnits(4);
            _cueFallbackLabel.Location = new Point(inset, LogicalToDeviceUnits(3));
            _cueFallbackLabel.MaximumSize = new Size(Math.Max(1, txtLinks.ClientSize.Width - inset * 2), 0);
        }
    }
}