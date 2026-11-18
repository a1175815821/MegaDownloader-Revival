using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public partial class Main
    {
        private Panel workbenchHeader;
        private Label workbenchTitle;
        private Label workbenchCount;
        private SearchFrame workbenchSearchFrame;
        private Panel workbenchEmpty;
        private Label workbenchEmptyTitle;
        private Label workbenchEmptyHint;
        private Button workbenchEmptyAction;
        private TextBox workbenchSearch;
        private Label workbenchSearchLabel;
        private Button workbenchClear;
        private Button workbenchDetails;
        private Button workbenchSchedule;
        private System.Windows.Forms.Timer workbenchSearchTimer;
        private bool workbenchReady;
        private bool workbenchLayoutBusy;
        private bool? workbenchIconCaptionsCompact;
        private bool detailsRequested = true;
        private string[] navTitles = new[] { "", "", "", "", "" };
        private int[] navTotals = new int[5];
        private string queueQuery = "";
        private int queueSearchGeneration;
        private HashSet<Paquete> queueMatches = new HashSet<Paquete>();
        private HashSet<Fichero> queueFileMatches = new HashSet<Fichero>();
        private HashSet<Paquete> queueWholeMatches = new HashSet<Paquete>();
        private long appliedScopeVersion = -1;
        private long requestedSearchVersion = -1;

        private struct QueueViewItem
        {
            internal IDescarga Model;
            internal DownloadEstadoFilter.NavScope Scope;
            internal string Name;
            internal string RelativePath;
        }

        // Published once per existing refresh cycle. Painting and counter updates only read it.
        private sealed class QueueOverview
        {
            internal readonly int[] Totals = new int[5];
            internal decimal TotalBytes;
            internal decimal DoneBytes;
            internal readonly List<QueueViewItem> ViewItems = new List<QueueViewItem>();
            internal long ScopeVersion;
            internal long SearchVersion;
        }

        private QueueOverview queueOverviewSnapshot;

        private void InitWorkbench()
        {
            DoubleBuffered = true;
            KeyPreview = true;
            btnUpdate.Visible = false;
            workbenchSchedule = new CommandButton();
            workbenchSchedule.Click += OpenDownloadSchedule;
            UiStyles.SetRole(btnAddLink, UiStyles.Role.PrimaryButton);

            TableLayoutPanel1.SuspendLayout();
            try
            {
                TableLayoutPanel1.Controls.Clear();
                // Retain the old hidden container as a child so its resources still have an owner.
                PanelButtonsRight.Visible = false;
                Controls.Add(PanelButtonsRight);
                TableLayoutPanel1.ColumnStyles.Clear();
                TableLayoutPanel1.ColumnCount = 5;
                Button[] buttons = new[] { btnPlay, btnPause, btnStop, workbenchSchedule, btnUpdate };
                for (int i = 0, loopTo = buttons.Length - 1; i <= loopTo; i++)
                {
                    TableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    if (buttons[i] is null)
                        continue;
                    var button = buttons[i];
                    button.AutoSize = true;
                    button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                    button.Margin = new Padding(0, 0, 8, 0);
                    button.Padding = new Padding(10, 4, 10, 4);
                    button.MinimumSize = new Size(0, Math.Max(34, Font.Height + 18));
                    button.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
                    button.TextImageRelation = TextImageRelation.ImageBeforeText;
                    button.ImageAlign = ContentAlignment.MiddleCenter;
                    button.TextAlign = ContentAlignment.MiddleCenter;
                    if (!ReferenceEquals(button, btnAddLink))
                        UiStyles.SetRole(button, UiStyles.Role.QuietButton);
                    TableLayoutPanel1.Controls.Add(button, i, 0);
                    button.Disposed += DisposeWorkbenchButtonImage;
                }
                TableLayoutPanel1.Padding = new Padding(12, 8, 4, 8);
            }
            finally
            {
                TableLayoutPanel1.ResumeLayout(true);
            }

            navListBox.DrawMode = DrawMode.OwnerDrawFixed;
            navListBox.BorderStyle = BorderStyle.None;
            navPanel.AutoScroll = true;
            navListBox.ItemHeight = Math.Max((int)Math.Round(40f * GetUiScale()), Font.Height + 14);
            navListBox.DrawItem += DrawWorkbenchNav;
            navListBox.MouseMove += (sender, e) =>
                {
                    int index = navListBox.IndexFromPoint(e.Location);
                    if (index == navHoverIndex)
                        return;
                    navHoverIndex = index;
                    navListBox.Invalidate();
                    ToolTipBotones.SetToolTip(navListBox, index >= 0 ? Conversions.ToString(navListBox.Items[index]) : "");
                };
            navListBox.MouseLeave += (s, e) =>
                {
                    navHoverIndex = -1;
                    navListBox.Invalidate();
                };
            btnUpdate.VisibleChanged += (s, e) => LayoutDownloadArea();

            workbenchHeader = new Panel() { Name = "workbenchHeader" };
            workbenchTitle = new Label() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            workbenchCount = new Label() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            workbenchSearchLabel = new Label() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            workbenchSearch = new TextBox();
            workbenchClear = new CommandButton() { Text = "×", TabStop = false };
            workbenchDetails = new CommandButton() { AutoEllipsis = true };
            workbenchSearchFrame = new SearchFrame(workbenchSearch, workbenchClear);
            UiStyles.SetRole(workbenchSearch, UiStyles.Role.InputSurface);
            workbenchSearch.HandleCreated += (s, e) => UiStyles.SetSearchCue(workbenchSearch);
            workbenchSearchLabel.Visible = false;
            TableLayoutPanel1.Dock = DockStyle.None;
            TableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            workbenchHeader.Controls.AddRange(new Control[] { workbenchTitle, workbenchCount, workbenchSearchLabel, workbenchSearchFrame, workbenchDetails, btnAddLink, TableLayoutPanel1 });
            navPanel.Controls.Add(btnConfig);
            btnConfig.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            UiStyles.SetRole(btnConfig, UiStyles.Role.QuietButton);
            btnConfig.Disposed += DisposeWorkbenchButtonImage;
            btnAddLink.Disposed += DisposeWorkbenchButtonImage;
            InitWorkbenchEmptyState();
            UiStyles.SetFont(workbenchTitle, 6f, true, Font);
            UiStyles.SetRole(workbenchCount, UiStyles.Role.Muted);
            UiStyles.SetRole(workbenchSearchLabel, UiStyles.Role.Muted);
            UiStyles.SetRole(workbenchClear, UiStyles.Role.QuietButton);
            UiStyles.SetRole(workbenchDetails, UiStyles.Role.QuietButton);
            UiStyles.SetRole(workbenchHeader, UiStyles.Role.Surface);
            UiStyles.SetRole(navPanel, UiStyles.Role.Sidebar);
            UiStyles.SetRole(detailPanel, UiStyles.Role.Surface);
            UiStyles.SetRole(detailGroup, UiStyles.Role.Surface);
            UiStyles.SetRole(TableLayoutPanel1, UiStyles.Role.Surface);
            UiStyles.SetRole(this, UiStyles.Role.Surface);
            Controls.Add(workbenchHeader);
            navPanel.Paint += (sender, e) =>
                {
                    using (var pen = new Pen(ThemeManager.GetColor("Border")))
                    {
                        e.Graphics.DrawLine(pen, navPanel.Width - 1, 0, navPanel.Width - 1, navPanel.Height);
                    }
                };
            workbenchSearchTimer = new System.Windows.Forms.Timer(components) { Interval = 220 };
            workbenchSearch.TextChanged += (s, e) =>
                {
                    Interlocked.Increment(ref queueSearchGeneration);
                    workbenchSearchTimer.Stop();
                    if (string.IsNullOrWhiteSpace(workbenchSearch.Text))
                    {
                        StartQueueSearch();
                    }
                    else
                    {
                        workbenchSearchTimer.Start();
                    }
                };
            workbenchSearchTimer.Tick += (s, e) =>
                {
                    workbenchSearchTimer.Stop();
                    StartQueueSearch();
                };
            workbenchClear.Click += (s, e) =>
                {
                    workbenchSearch.Clear();
                    workbenchSearch.Focus();
                };
            workbenchSearch.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        workbenchSearch.Clear();
                        e.SuppressKeyPress = true;
                    }
                };
            workbenchDetails.Click += (s, e) =>
                {
                    detailsRequested = !detailsRequested;
                    LayoutDownloadArea();
                    UpdateDetailPanel();
                };
            KeyDown += (sender, e) =>
                {
                    if (e.Control && e.KeyCode == Keys.F)
                    {
                        workbenchSearch.Focus();
                        workbenchSearch.SelectAll();
                        e.SuppressKeyPress = true;
                    }
                };
            StatusStrip1.SizingGrip = true;
            RAMProcToolStripStatusLabel.ForeColor = ThemeManager.GetColor("Muted");
            workbenchReady = true;
            UpdateWorkbenchTexts();
        }

        private void UpdateWorkbenchTexts()
        {
            if (!workbenchReady)
                return;
            string[] keys = new[] { "Nav_All", "Nav_Downloading", "Nav_Waiting", "Nav_Failed", "Nav_Completed" };
            for (int i = 0; i <= 4; i++)
                navTitles[i] = Language.GetText(keys[i]);
            workbenchSearchLabel.Text = Language.GetText("UI_Search");
            workbenchSearch.AccessibleName = workbenchSearchLabel.Text;
            UiStyles.SetSearchCue(workbenchSearch);
            workbenchClear.AccessibleName = Language.GetText("UI_ClearSearch");
            btnAddLink.AccessibleName = Language.GetText("Add links");
            btnPlay.AccessibleName = Language.GetText("Start downloads");
            btnPause.AccessibleName = Language.GetText("Pause downloads");
            btnStop.AccessibleName = Language.GetText("Stop downloads");
            btnConfig.AccessibleName = Language.GetText("Configuration");
            btnUpdate.AccessibleName = Language.GetText("New version do you want to download it?");
            ToolTipBotones.SetToolTip(workbenchSearch, Language.GetText("UI_SearchHint"));
            ToolTipBotones.SetToolTip(workbenchClear, Language.GetText("UI_ClearSearch"));
            workbenchSchedule.Text = Language.GetText("UI_Schedule");
            ToolTipBotones.SetToolTip(workbenchSchedule, Language.GetText("Schedule_Title"));
            btnConfig.Text = Language.GetText("Configuration");
            btnAddLink.Text = Language.GetText("Add links");
            LayoutWorkbenchToolbar();
            UpdateWorkbenchHeading();
            navListBox.Invalidate();
        }

        private void LayoutWorkbenchToolbar()
        {
            if (!workbenchReady)
                return;
            float scale = GetUiScale();
            bool compact = ClientSize.Width < (int)Math.Round(1060f * scale);
            btnPlay.Text = compact ? "" : Language.GetText("Start");
            btnPause.Text = compact ? "" : Language.GetText("Pause");
            btnStop.Text = Language.GetText("Stop");
            workbenchSchedule.Visible = ClientSize.Width >= (int)Math.Round(1060f * scale);
            btnConfig.Text = Language.GetText("Configuration");
            foreach (Button button in new Button[] { btnAddLink, btnPlay, btnPause, btnStop, workbenchSchedule, btnUpdate, btnConfig })
            {
                button.ImageAlign = ContentAlignment.MiddleCenter;
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
                button.Padding = new Padding((int)Math.Round(10f * scale), (int)Math.Round(5f * scale), (int)Math.Round(10f * scale), (int)Math.Round(5f * scale));
                button.Margin = new Padding(0, 0, (int)Math.Round(4f * scale), 0);
                button.MinimumSize = new Size((int)Math.Round(36f * scale), Math.Max((int)Math.Round(36f * scale), Font.Height + (int)Math.Round(14f * scale)));
            }
            btnAddLink.AutoSize = false;
            btnConfig.AutoSize = false;
            btnConfig.ImageAlign = ContentAlignment.MiddleLeft;
            btnConfig.TextAlign = ContentAlignment.MiddleLeft;
            TableLayoutPanel1.Padding = new Padding(0);
            TableLayoutPanel1.Height = Math.Max((int)Math.Round(36f * scale), Font.Height + (int)Math.Round(14f * scale));
            if (!workbenchIconCaptionsCompact.HasValue || workbenchIconCaptionsCompact.Value != compact)
                ApplyWorkbenchIcons();
        }

        private void InitWorkbenchEmptyState()
        {
            workbenchEmpty = new Panel() { Name = "workbenchEmpty", Visible = false };
            workbenchEmptyTitle = new Label() { TextAlign = ContentAlignment.MiddleCenter };
            workbenchEmptyHint = new Label() { TextAlign = ContentAlignment.TopCenter };
            workbenchEmptyAction = new CommandButton() { AutoSize = false };
            UiStyles.SetFont(workbenchEmptyTitle, 4f, true, Font);
            UiStyles.SetRole(workbenchEmpty, UiStyles.Role.Surface);
            UiStyles.SetRole(workbenchEmptyHint, UiStyles.Role.Muted);
            UiStyles.SetRole(workbenchEmptyAction, UiStyles.Role.SecondaryButton);
            workbenchEmpty.Controls.AddRange(new Control[] { workbenchEmptyTitle, workbenchEmptyHint, workbenchEmptyAction });
            workbenchEmptyAction.Click += (s, e) => btnAddLink.PerformClick();
            Controls.Add(workbenchEmpty);
            EnableLinkDrop(workbenchEmpty);
        }

        private void LayoutWorkbenchEmptyState()
        {
            if (workbenchEmpty is null)
                return;
            workbenchEmpty.Bounds = ListaDescargas.Bounds;
            float scale = GetUiScale();
            int width = Math.Min((int)Math.Round(400f * scale), Math.Max(100, workbenchEmpty.Width - (int)Math.Round(32f * scale)));
            int x = (workbenchEmpty.Width - width) / 2;
            int y = Math.Max((int)Math.Round(16f * scale), (workbenchEmpty.Height - (int)Math.Round(170f * scale)) / 2);
            workbenchEmptyTitle.SetBounds(x, y, width, (int)Math.Round(38f * scale));
            workbenchEmptyHint.SetBounds(x, workbenchEmptyTitle.Bottom + (int)Math.Round(8f * scale), width, (int)Math.Round(64f * scale));
            int actionWidth = Math.Min(width, Math.Max((int)Math.Round(128f * scale), TextRenderer.MeasureText(workbenchEmptyAction.Text, Font).Width + (int)Math.Round(32f * scale)));
            workbenchEmptyAction.SetBounds((workbenchEmpty.Width - actionWidth) / 2, workbenchEmptyHint.Bottom + (int)Math.Round(8f * scale), actionWidth, (int)Math.Round(36f * scale));
        }

        private void UpdateWorkbenchHeading()
        {
            if (!workbenchReady)
                return;
            int scope = (int)_navScope;
            UiStyles.SetText(workbenchTitle, navTitles[scope]);
            int count = navTotals[scope];
            if (queueQuery.Length > 0)
                count = queueVisibleCount;
            UiStyles.SetText(workbenchCount, Language.GetText("UI_PackageCount").Replace("%N%", count.ToString()));
            workbenchClear.Enabled = workbenchSearch.TextLength > 0;
            UiStyles.SetText(workbenchDetails, Language.GetText(detailsRequested && workbenchDetails.Enabled ? "UI_HideDetails" : "UI_ShowDetails"));
            workbenchDetails.AccessibleName = workbenchDetails.Text;
            if (!string.IsNullOrEmpty(ListaDescargas.EmptyListMsg))
                ListaDescargas.EmptyListMsg = string.Empty;
            if (workbenchEmpty is not null)
            {
                bool filtered = queueQuery.Length > 0 || _navScope != DownloadEstadoFilter.NavScope.All;
                UiStyles.SetText(workbenchEmptyTitle, Language.GetText(filtered ? "UI_NoMatches" : "UI_EmptyTitle"));
                UiStyles.SetText(workbenchEmptyHint, Language.GetText(filtered ? "UI_EmptyFilter" : "UI_EmptyHint"));
                UiStyles.SetText(workbenchEmptyAction, Language.GetText("Add links"));
                workbenchEmptyAction.Visible = !filtered;
                bool isEmpty = ListaDescargas.Items.Count == 0;
                workbenchEmpty.Visible = isEmpty;
                if (isEmpty && Controls.GetChildIndex(workbenchEmpty) != 0)
                    workbenchEmpty.BringToFront();
            }
        }

        private int queueVisibleCount;

        private void LayoutWorkbenchArea()
        {
            if (workbenchLayoutBusy)
                return;
            workbenchLayoutBusy = true;
            SuspendLayout();
            try
            {
                LayoutMainMenu();
                LayoutWorkbenchToolbar();
                float scale = GetUiScale();
                int pad = (int)Math.Round(24f * scale);
                int top = MainMenuStrip is null ? 0 : MainMenuStrip.Bottom;
                if (quotaBannerPanel is not null && quotaBannerPanel.Visible)
                    top += quotaBannerPanel.Height;
                if (failureBannerPanel is not null && failureBannerPanel.Visible)
                    top += failureBannerPanel.Height;
                int bottom = StatusStrip1.Top - pad;
                int navWidth = (int)Math.Round(192f * scale);
                int mainLeft = navWidth + pad;
                int headerHeight = (int)Math.Round(128f * scale);
                navPanel.SetBounds(0, top, navWidth, Math.Max(50, StatusStrip1.Top - top));
                workbenchHeader.SetBounds(mainLeft, top + pad, Math.Max(100, ClientSize.Width - pad - mainLeft), headerHeight);
                int commandHeight = TableLayoutPanel1.Height;
                int addWidth = Math.Max((int)Math.Round(124f * scale), TextRenderer.MeasureText(btnAddLink.Text, Font).Width + (int)Math.Round(52f * scale));
                btnAddLink.SetBounds(Math.Max(0, workbenchHeader.Width - addWidth), 0, addWidth, commandHeight);
                bool hasSelection = ListaDescargas.SelectedObject is not null;
                workbenchDetails.Visible = hasSelection;
                workbenchDetails.Enabled = bottom - workbenchHeader.Bottom >= (int)Math.Round(260f * scale);
                workbenchDetails.SetBounds(Math.Max(0, btnAddLink.Left - (int)Math.Round(120f * scale)), 0, (int)Math.Round(112f * scale), commandHeight);
                int titleRight = (hasSelection ? workbenchDetails.Left : btnAddLink.Left) - (int)Math.Round(12f * scale);
                workbenchTitle.SetBounds(0, 0, Math.Max(80, titleRight), (int)Math.Round(34f * scale));
                workbenchCount.SetBounds(0, workbenchTitle.Bottom + (int)Math.Round(2f * scale), workbenchTitle.Width, (int)Math.Round(24f * scale));
                int searchWidth = Math.Min((int)Math.Round(280f * scale), Math.Max((int)Math.Round(176f * scale), workbenchHeader.Width / 3));
                int searchY = (int)Math.Round(72f * scale);
                workbenchSearchFrame.SetBounds(workbenchHeader.Width - searchWidth, searchY, searchWidth, commandHeight);
                TableLayoutPanel1.SetBounds(0, searchY, Math.Max(80, workbenchSearchFrame.Left - (int)Math.Round(12f * scale)), commandHeight);
                int contentTop = workbenchHeader.Bottom;
                int listRight = ClientSize.Width - pad;
                int listBottom = bottom;
                bool showDetails = hasSelection && detailsRequested && workbenchDetails.Enabled;
                detailPanel.Visible = showDetails;
                if (showDetails)
                {
                    if (ClientSize.Width >= (int)Math.Round(1280f * scale))
                    {
                        int detailWidth = (int)Math.Round(260f * scale);
                        detailPanel.SetBounds(ClientSize.Width - pad - detailWidth, contentTop, detailWidth, Math.Max(50, bottom - contentTop));
                        listRight = detailPanel.Left - pad;
                    }
                    else
                    {
                        int detailHeight = Math.Min((int)Math.Round(220f * scale), (bottom - contentTop) * 2 / 5);
                        detailPanel.SetBounds(mainLeft, bottom - detailHeight, Math.Max(100, listRight - mainLeft), detailHeight);
                        listBottom = detailPanel.Top - (int)Math.Round(12f * scale);
                    }
                }
                ListaDescargas.SetBounds(mainLeft, contentTop, Math.Max(100, listRight - mainLeft), Math.Max(50, listBottom - contentTop));
                ListaDescargas.BorderStyle = BorderStyle.None;
                LayoutNavRail();
                LayoutDetailContent();
                LayoutWorkbenchEmptyState();
                UpdateWorkbenchHeading();
            }
            finally
            {
                ResumeLayout(false);
                workbenchLayoutBusy = false;
            }
        }

        private int navHoverIndex = -1;

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            // A minimum scaled on the previous screen can exceed the new working area.
            MinimumSize = Size.Empty;
            base.OnDpiChanged(e);
            if (!workbenchReady)
                return;
            ClampMainWindowToWorkingArea(Screen.FromControl(this).WorkingArea);
            navListBox.ItemHeight = Math.Max((int)Math.Round(40f * GetUiScale()), Font.Height + 14);
            ListaDescargas.RowHeight = Math.Max((int)Math.Round(40f * GetUiScale()), Font.Height + 16);
            detailLayoutSize = Size.Empty;
            ApplyWorkbenchIcons();
            LayoutDownloadArea();
        }

        private void ClampMainWindowToWorkingArea(Rectangle area)
        {
            MinimumSize = new Size(Math.Min(LogicalToDeviceUnits(640), area.Width), Math.Min(LogicalToDeviceUnits(440), area.Height));
            if (WindowState != FormWindowState.Normal)
                return;
            var fitted = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            var origin = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - fitted.Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - fitted.Height)));
            Bounds = new Rectangle(origin, fitted);
        }

        private void DisposeWorkbenchButtonImage(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            var owned = button.Image;
            button.Image = null;
            if (owned is not null)
                owned.Dispose();
        }

        private void DrawWorkbenchNav(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index > 4)
                return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            float scale = GetUiScale();
            var rect = Rectangle.Inflate(e.Bounds, 0, -(int)Math.Round(2f * scale));
            using (var brush = new SolidBrush(ThemeManager.GetColor(selected ? "Selection" : e.Index == navHoverIndex ? "ButtonHover" : "ControlBack")))
            {
                using (var shape = CommandButton.Rounded(rect, (int)Math.Round(6f * scale)))
                {
                    e.Graphics.FillPath(brush, shape);
                }
            }
            if (selected)
            {
                using (var brush = new SolidBrush(ThemeManager.GetColor("Accent")))
                {
                    e.Graphics.FillRectangle(brush, rect.Left, rect.Top + 5, Math.Max(2, (int)Math.Round(3f * scale)), Math.Max(2, rect.Height - 10));
                }
            }
            var fore = ThemeManager.GetColor(selected ? "SelectionFore" : "Fore");
            string countText = navTotals[e.Index].ToString();
            int countWidth = TextRenderer.MeasureText(countText, e.Font).Width + (int)Math.Round(10f * scale);
            var countRect = new Rectangle(rect.Right - countWidth - (int)Math.Round(8f * scale), rect.Top, countWidth, rect.Height);
            DrawNavGlyph(e.Graphics, new Rectangle(rect.Left + (int)Math.Round(12f * scale), rect.Top + (rect.Height - (int)Math.Round(18f * scale)) / 2, (int)Math.Round(18f * scale), (int)Math.Round(18f * scale)), e.Index, fore);
            var textRect = new Rectangle(rect.Left + (int)Math.Round(40f * scale), rect.Top, Math.Max(15, countRect.Left - rect.Left - (int)Math.Round(44f * scale)), rect.Height);
            TextRenderer.DrawText(e.Graphics, navTitles[e.Index], e.Font, textRect, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, countText, e.Font, countRect, ThemeManager.GetColor(selected ? "SelectionFore" : "Muted"), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if ((e.State & DrawItemState.Focus) != 0)
                e.DrawFocusRectangle();
        }

        private static void DrawNavGlyph(Graphics graphics, Rectangle bounds, int index, Color color)
        {
            var state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 20.0f, bounds.Height / 20.0f);
                using (var pen = new Pen(color, 1.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    switch (index)
                    {
                        case 0:
                            for (int y = 2; y <= 11; y += 9)
                            {
                                for (int x = 2; x <= 11; x += 9)
                                    graphics.DrawRectangle(pen, x, y, 6, 6);
                            }

                            break;
                        case 1:
                            graphics.DrawLine(pen, 10, 2, 10, 13);
                            graphics.DrawLines(pen, new Point[] { new Point(6, 9), new Point(10, 13), new Point(14, 9) });
                            graphics.DrawLines(pen, new Point[] { new Point(3, 13), new Point(3, 17), new Point(17, 17), new Point(17, 13) });
                            break;
                        case 2:
                            graphics.DrawEllipse(pen, 2, 2, 16, 16);
                            graphics.DrawLines(pen, new Point[] { new Point(10, 5), new Point(10, 10), new Point(14, 12) });
                            break;
                        case 3:
                            graphics.DrawEllipse(pen, 2, 2, 16, 16);
                            graphics.DrawLine(pen, 10, 5, 10, 11);
                            graphics.DrawLine(pen, 10f, 14f, 10f, 14.5f);
                            break;
                        case 4:
                            graphics.DrawEllipse(pen, 2, 2, 16, 16);
                            graphics.DrawLines(pen, new Point[] { new Point(6, 10), new Point(9, 13), new Point(14, 7) });
                            break;
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private async void StartQueueSearch()
        {
            if (Cerrando || IsDisposed || ListaPaquetes is null)
                return;
            int generation = Interlocked.Increment(ref queueSearchGeneration);
            var overview = Volatile.Read(ref queueOverviewSnapshot);
            requestedSearchVersion = overview is null ? -1 : overview.SearchVersion;
            string query = workbenchSearch.Text.Trim();
            if (query.Length == 0)
            {
                queueQuery = "";
                queueMatches.Clear();
                queueWholeMatches.Clear();
                queueFileMatches.Clear();
                RebuildWorkbenchRoots();
                return;
            }
            var snapshot = new List<KeyValuePair<Paquete, List<Fichero>>>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete package in ListaPaquetes)
                    snapshot.Add(new KeyValuePair<Paquete, List<Fichero>>(package, package.SnapshotFicheros()));
            }
            try
            {
                var result = await Task.Run(() =>
                    {
                        var packages = new HashSet<Paquete>();
                        var whole = new HashSet<Paquete>();
                        var files = new HashSet<Fichero>();
                        foreach (var item in snapshot)
                        {
                            if (generation != Volatile.Read(ref queueSearchGeneration))
                                return null;
                            if (ContainsQueueText(item.Key.Nombre, query))
                            {
                                whole.Add(item.Key);
                                packages.Add(item.Key);
                            }
                            else
                            {
                                foreach (Fichero @file in item.Value)
                                {
                                    if (generation != Volatile.Read(ref queueSearchGeneration))
                                        return null;
                                    if (ContainsQueueText(@file.NombreFichero, query) || ContainsQueueText(@file.RutaRelativa, query))
                                    {
                                        files.Add(@file);
                                        packages.Add(item.Key);
                                    }
                                }
                            }
                        }
                        return Tuple.Create(packages, whole, files);
                    });
                if (IsDisposed || Cerrando || generation != queueSearchGeneration || result is null || !string.Equals(query, workbenchSearch.Text.Trim(), StringComparison.Ordinal))
                    return;
                queueQuery = query;
                queueMatches = result.Item1;
                queueWholeMatches = result.Item2;
                queueFileMatches = result.Item3;
                RebuildWorkbenchRoots();
            }
            catch (Exception ex)
            {
                Log.WriteDebug("Queue search failed: " + Log.SafeException(ex));
            }
        }

        private static bool ContainsQueueText(string value, string query)
        {
            return value is not null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesQueueFile(Fichero @file, Paquete package)
        {
            return DownloadEstadoFilter.MatchesScope(@file, _navScope) && (queueQuery.Length == 0 || queueWholeMatches.Contains(package) || queueFileMatches.Contains(@file));
        }

        private bool MatchesQueuePackage(Paquete package)
        {
            if (queueQuery.Length == 0)
                return DownloadEstadoFilter.MatchesScope(package, _navScope);
            if (!queueMatches.Contains(package))
                return false;
            if (queueWholeMatches.Contains(package))
                return DownloadEstadoFilter.MatchesScope(package, _navScope);
            if (package.ListaFicheros is null)
                return false;
            foreach (Fichero @file in package.ListaFicheros)
            {
                if (MatchesQueueFile(@file, package))
                    return true;
            }
            return false;
        }

        private void RebuildWorkbenchRoots()
        {
            var overview = Volatile.Read(ref queueOverviewSnapshot);
            lock (Mutex.ListaDescargas)
            {
                ListaDescargas.BeginUpdate();
                try
                {
                    ApplyNavRoots();
                    ListaDescargas.BuildList();
                }
                finally
                {
                    ListaDescargas.EndUpdate();
                }
            }
            if (overview is not null)
                appliedScopeVersion = overview.ScopeVersion;
            UpdateWorkbenchHeading();
            UpdateDetailPanel();
        }

        private void RevealImportedPackage(Paquete package)
        {
            if (Cerrando || IsDisposed || !workbenchReady)
                return;
            // Reveal the confirmed import even if an earlier search or status tab hid it.
            ListaDescargas.BeginUpdate();
            try
            {
                _updatingNav = true;
                try
                {
                    _navScope = DownloadEstadoFilter.NavScope.All;
                    navListBox.SelectedIndex = 0;
                }
                finally
                {
                    _updatingNav = false;
                }
                if (workbenchSearch.TextLength > 0)
                {
                    workbenchSearch.Clear();
                }
                else
                {
                    RebuildWorkbenchRoots();
                }
                ListaDescargas.SelectedObject = package;
                ListaDescargas.EnsureModelVisible(package);
            }
            finally
            {
                ListaDescargas.EndUpdate();
            }
            UpdateDetailPanel();
            ListaDescargas.Focus();
        }

        private void PublishQueueOverview(List<KeyValuePair<Paquete, List<Fichero>>> snapshot)
        {
            var value = new QueueOverview();
            int capacity = snapshot.Count;
            foreach (var item in snapshot)
                capacity += item.Value.Count;
            value.ViewItems.Capacity = capacity;
            foreach (var item in snapshot)
            {
                var present = new bool[5];
                var packageScope = DownloadEstadoFilter.ScopeOf(item.Key.DescargaEstado());
                present[(int)packageScope] = true;
                value.ViewItems.Add(new QueueViewItem() { Model = item.Key, Scope = packageScope, Name = item.Key.Nombre });
                foreach (Fichero @file in item.Value)
                {
                    var fileScope = DownloadEstadoFilter.ScopeOf(@file.DescargaEstado());
                    present[(int)fileScope] = true;
                    value.ViewItems.Add(new QueueViewItem() { Model = @file, Scope = fileScope, Name = @file.NombreFichero, RelativePath = @file.RutaRelativa });
                }
                value.Totals[0] += 1;
                for (int i = 1; i <= 4; i++)
                {
                    if (present[i])
                        value.Totals[i] += 1;
                }
                long size = item.Key.DescargaTamanoBytes();
                if (size > 0L)
                {
                    value.TotalBytes += size;
                    value.DoneBytes += Math.Ceiling(item.Key.DescargaPorcentaje() * size / 100m);
                }
            }
            var previous = Volatile.Read(ref queueOverviewSnapshot);
            bool scopeChanged = previous is null || previous.ViewItems.Count != value.ViewItems.Count;
            bool searchChanged = scopeChanged;
            if (previous is not null)
            {
                for (int index = 0, loopTo = Math.Min(previous.ViewItems.Count, value.ViewItems.Count) - 1; index <= loopTo; index++)
                {
                    var before = previous.ViewItems[index];
                    var after = value.ViewItems[index];
                    bool different = !ReferenceEquals(before.Model, after.Model);
                    scopeChanged = scopeChanged || different || before.Scope != after.Scope;
                    searchChanged = searchChanged || different || !string.Equals(before.Name, after.Name, StringComparison.Ordinal) || !string.Equals(before.RelativePath, after.RelativePath, StringComparison.Ordinal);
                    if (scopeChanged && searchChanged)
                        break;
                }
                value.ScopeVersion = previous.ScopeVersion;
                value.SearchVersion = previous.SearchVersion;
            }
            if (scopeChanged)
                value.ScopeVersion += 1L;
            if (searchChanged)
                value.SearchVersion += 1L;
            Volatile.Write(ref queueOverviewSnapshot, value);
        }

        private void ApplyWorkbenchIcons()
        {
            Button[] buttons = new[] { btnAddLink, btnPlay, btnPause, btnStop, btnConfig, btnUpdate, workbenchSchedule };
            int side = Math.Max(16, (int)Math.Round(18f * GetUiScale()));
            for (int i = 0, loopTo = buttons.Length - 1; i <= loopTo; i++)
            {
                var button = buttons[i];
                var color = ThemeManager.GetColor(i == 0 ? "AccentFore" : "Fore");
                // Native buttons center the font line box, which includes descender space.
                // Lift captioned glyphs slightly to align their ink with the visible letters.
                int lift = button.Text.Length > 0 ? Math.Max(1, (int)Math.Round(GetUiScale())) : 0;
                var icon = CreateWorkbenchIcon(i, side, color, lift);
                var old = button.Image;
                button.Image = icon;
                if (old is not null)
                    old.Dispose();
            }
            workbenchIconCaptionsCompact = btnPlay.Text.Length == 0;
        }

        private static Bitmap CreateWorkbenchIcon(int kind, int side, Color color, int lift)
        {
            // A common 24-unit canvas and stroke keep glyphs balanced. Render large first:
            // fractional display scales otherwise make opposite edges look uneven at 18px.
            using (var source = new Bitmap(side * 4, side * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(source))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.ScaleTransform(source.Width / 24.0f, source.Height / 24.0f);
                    using (var pen = new Pen(color, 2.0f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;
                        switch (kind)
                        {
                            case 0:
                                graphics.DrawLine(pen, 12, 5, 12, 19);
                                graphics.DrawLine(pen, 5, 12, 19, 12);
                                break;
                            case 1:
                                // A triangle needs an optical offset to balance its heavier left edge.
                                graphics.DrawPolygon(pen, new PointF[] { new PointF(8f, 5f), new PointF(20f, 12f), new PointF(8f, 19f) });
                                break;
                            case 2:
                                graphics.DrawLine(pen, 8, 5, 8, 19);
                                graphics.DrawLine(pen, 16, 5, 16, 19);
                                break;
                            case 3:
                                graphics.DrawRectangle(pen, 5, 5, 14, 14);
                                break;
                            case 4:
                                {
                                    var gear = new List<PointF>(32);
                                    for (int tooth = 0; tooth <= 7; tooth++)
                                    {
                                        for (int corner = 0; corner <= 3; corner++)
                                        {
                                            double offset = corner == 0 ? -17.5d : corner == 1 ? -12.5d : corner == 2 ? 12.5d : 17.5d;
                                            double angle = (tooth * 45 + offset) * Math.PI / 180d;
                                            double radius = corner == 0 || corner == 3 ? 6.5d : 8.0d;
                                            gear.Add(new PointF((float)(12d + Math.Cos(angle) * radius), (float)(12d + Math.Sin(angle) * radius)));
                                        }
                                    }
                                    graphics.DrawPolygon(pen, gear.ToArray());
                                    graphics.DrawEllipse(pen, 9, 9, 6, 6);
                                    break;
                                }
                            case 5:
                                graphics.DrawLine(pen, 12, 5, 12, 14);
                                graphics.DrawLines(pen, new PointF[] { new PointF(8f, 10f), new PointF(12f, 14f), new PointF(16f, 10f) });
                                graphics.DrawLines(pen, new PointF[] { new PointF(5f, 16f), new PointF(5f, 19f), new PointF(19f, 19f), new PointF(19f, 16f) });
                                break;
                            case 6:
                                graphics.DrawEllipse(pen, 4, 4, 16, 16);
                                graphics.DrawLine(pen, 12, 7, 12, 12);
                                graphics.DrawLine(pen, 12, 12, 16, 14);
                                break;
                        }
                    }
                }
                var result = new Bitmap(side, side, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    using (var graphics = Graphics.FromImage(result))
                    using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        attributes.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(source, new Rectangle(0, -lift, side, side), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                    }
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }
    }
}