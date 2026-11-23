using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    /// <summary>Virtual grid backed by metadata, not one native row per imported file.</summary>
    public sealed class BatchPreflightView : UserControl
    {

        private readonly List<string> urls;
        private readonly Dictionary<string, URLProcessor.FileURL> queued;
        private string destination;
        private CancellationTokenSource cancellation;
        private readonly PreflightSession session;
        private readonly bool ownsSession;
        private PreflightSession.Request request;
        private readonly UiStyles.BufferedGrid grid = new UiStyles.BufferedGrid();
        private readonly Label summary = new Label();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly System.Windows.Forms.Timer progressTimer = new System.Windows.Forms.Timer() { Interval = 150 };
        private PreflightProgress displayedProgress;
        private long displayedProgressSecond = -1;
        private readonly Label spaceLabel = new Label();
        private readonly TextBox search = new TextBox();
        private readonly ComboBox category = new ComboBox();
        private readonly CommandButton addButton = new CommandButton();
        private readonly CommandButton retryButton = new CommandButton();
        private readonly CommandButton backButton = new CommandButton();
        private readonly CommandButton cancelButton = new CommandButton();
        private readonly CommandButton stopButton = new CommandButton();
        private readonly Label notice = new Label();
        private readonly Label targetLabel = new Label();
        private readonly FlowLayoutPanel filters = new FlowLayoutPanel();
        private readonly ToolStrip tools = new ToolStrip();
        private readonly ToolTip targetTip = new ToolTip();
        private readonly System.Windows.Forms.Timer filterTimer = new System.Windows.Forms.Timer() { Interval = 180 };
        private readonly ContextMenuStrip selectionMenu = new ContextMenuStrip();
        private readonly Dictionary<BatchLinkPreflight.LinkState, string> statusLabels = new Dictionary<BatchLinkPreflight.LinkState, string>();
        private readonly string folderSelectionLabel;
        private List<BatchLinkPreflight.Entry> entries = new List<BatchLinkPreflight.Entry>();
        private PreflightSelection selection;
        private PreflightTree tree;
        private List<PreflightTree.Node> boundRows = new List<PreflightTree.Node>();
        private long? freeSpace;
        private bool loading;
        private bool binding;
        private bool disposeRequested;
        private bool importing;
        private bool checkingSpace;
        private int spaceGeneration;
        private readonly Func<Dictionary<string, URLProcessor.FileURL>> queueProvider;
        public List<URLProcessor.FileURL> SelectedFiles { get; private set; }
        public bool InspectionStopped { get; private set; }
        public event EventHandler BackRequested;
        public event EventHandler CancelRequested;
        public event EventHandler ConfirmRequested;

        private bool SelectionLocked
        {
            get
            {
                return loading || importing || disposeRequested;
            }
        }

        public BatchPreflightView(List<string> input, Configuracion configuration, Dictionary<string, URLProcessor.FileURL> queueSnapshot, string target, PreflightSession sharedSession = null, PreflightSession.Request prepared = null, Func<Dictionary<string, URLProcessor.FileURL>> getQueue = null)
        {
            urls = new List<string>(input);
            queued = queueSnapshot;
            destination = target;
            ownsSession = sharedSession is null;
            session = sharedSession ?? new PreflightSession(configuration);
            request = prepared;
            queueProvider = getQueue;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            UiStyles.SetAppFont(this);
            Size = new Size(960, 600);
            Dock = DockStyle.Fill;
            Margin = new Padding(0);
            folderSelectionLabel = Language.GetText("Preflight_FolderSelection");
            foreach (BatchLinkPreflight.LinkState state in Enum.GetValues(typeof(BatchLinkPreflight.LinkState)))
                statusLabels[state] = Language.GetText("Preflight_State_" + state.ToString());

            var layout = new TableLayoutPanel() { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            UiStyles.SetRole(layout, UiStyles.Role.Surface);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i <= 3; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summary.Dock = DockStyle.Fill;
            summary.AutoSize = true;
            summary.MinimumSize = new Size(0, 38);
            UiStyles.SetFont(summary, 0f, true, Font);
            summary.Text = Language.GetText("Preflight_Checking");
            var progressLayout = new TableLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
            progressLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            progressLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            progressLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            progressLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 6f));
            UiStyles.SetRole(progressLayout, UiStyles.Role.Surface);
            progressBar.Dock = DockStyle.Fill;
            progressBar.Margin = new Padding(0, 0, 0, 2);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressLayout.Controls.Add(summary, 0, 0);
            stopButton.Text = Language.GetText("AddLinks_StopCheck");
            stopButton.AutoSize = true;
            stopButton.Visible = false;
            progressLayout.Controls.Add(stopButton, 1, 0);
            progressLayout.Controls.Add(progressBar, 0, 1);
            progressLayout.SetColumnSpan(progressBar, 2);
            layout.Controls.Add(progressLayout, 0, 0);
            filters.Dock = DockStyle.Fill;
            filters.WrapContents = true;
            filters.AutoSize = true;
            filters.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            filters.MinimumSize = new Size(0, 34);
            filters.Controls.Add(new Label() { Text = Language.GetText("Preflight_Search"), AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            search.Width = 185;
            filters.Controls.Add(search);
            category.DropDownStyle = ComboBoxStyle.DropDownList;
            category.Width = 125;
            foreach (string key in new string[] { "All", "Video", "Audio", "Archives", "Documents", "Other" })
                category.Items.Add(Language.GetText("Preflight_" + key));
            category.SelectedIndex = 0;
            filters.Controls.Add(category);
            tools.GripStyle = ToolStripGripStyle.Hidden;
            tools.Dock = DockStyle.Fill;
            tools.Padding = new Padding(0, 4, 0, 4);
            var allFiles = new ToolStripDropDownButton(Language.GetText("UI_AllFiles"));
            AddSelectionAction(allFiles, "Preflight_SelectAll", () => ApplySelection(PreflightSelection.SelectionOperation.SelectAll, false));
            AddSelectionAction(allFiles, "Preflight_ClearAll", () => ApplySelection(PreflightSelection.SelectionOperation.Clear, false));
            var results = new ToolStripDropDownButton(Language.GetText("UI_CurrentResults"));
            AddSelectionAction(results, "Preflight_SelectVisible", () => SelectVisible(true));
            AddSelectionAction(results, "Preflight_InvertVisible", () => ApplySelection(PreflightSelection.SelectionOperation.Invert, true));
            AddSelectionAction(results, "Preflight_ClearVisible", () => SelectVisible(false));
            tools.Items.Add(allFiles);
            tools.Items.Add(results);
            tools.Items.Add(new ToolStripSeparator());
            var expand = new ToolStripButton(Language.GetText("Preflight_ExpandAll"));
            var collapse = new ToolStripButton(Language.GetText("Preflight_CollapseAll"));
            expand.Click += (s, e) => ExpandFolders(true);
            collapse.Click += (s, e) => ExpandFolders(false);
            tools.Items.Add(expand);
            tools.Items.Add(collapse);
            var filterArea = new TableLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 4, 0, 8) };
            for (int i = 0; i <= 2; i++)
                filterArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var hint = new Label() { Text = Language.GetText("Preflight_MultiSelectHint"), AutoSize = true, MaximumSize = new Size(900, 0), Margin = new Padding(0, 4, 0, 8) };
            UiStyles.SetRole(hint, UiStyles.Role.Muted);
            UiStyles.SetRole(filters, UiStyles.Role.Surface);
            UiStyles.SetRole(filterArea, UiStyles.Role.Surface);
            filterArea.Controls.Add(filters, 0, 0);
            filterArea.Controls.Add(tools, 0, 1);
            filterArea.Controls.Add(hint, 0, 2);
            layout.Controls.Add(filterArea, 0, 1);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.VirtualMode = true;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = Math.Max(30, Font.Height + 14);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = Math.Max(34, Font.Height + 16);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            grid.Columns.Add(new DataGridViewCheckBoxColumn() { Name = "Selected", HeaderText = "", Width = 34, ThreeState = true, ValueType = typeof(CheckState), ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn() { Name = "FileName", HeaderText = Language.GetText("Name"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55f, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn() { Name = "FileSize", HeaderText = Language.GetText("Size"), Width = 85, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn() { Name = "LinkStatus", HeaderText = Language.GetText("Status"), Width = 160, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn() { Name = "FilePath", HeaderText = Language.GetText("Detail_Path"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30f, ReadOnly = true });
            foreach (DataGridViewColumn column in grid.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            AddContextAction("Preflight_CheckHighlighted", PreflightSelection.SelectionOperation.SelectAll);
            AddContextAction("Preflight_UncheckHighlighted", PreflightSelection.SelectionOperation.Clear);
            AddContextAction("Preflight_InvertHighlighted", PreflightSelection.SelectionOperation.Invert);
            grid.ContextMenuStrip = selectionMenu;
            layout.Controls.Add(grid, 0, 2);
            spaceLabel.Dock = DockStyle.Fill;
            spaceLabel.AutoSize = true;
            spaceLabel.Padding = new Padding(0, 6, 0, 0);
            layout.Controls.Add(spaceLabel, 0, 3);
            targetLabel.Dock = DockStyle.Fill;
            targetLabel.AutoEllipsis = true;
            targetLabel.Height = 26;
            targetLabel.Margin = new Padding(0, 2, 0, 4);
            UiStyles.SetRole(targetLabel, UiStyles.Role.Muted);
            layout.Controls.Add(targetLabel, 0, 4);
            notice.AutoSize = true;
            notice.Dock = DockStyle.Fill;
            notice.Margin = new Padding(0, 4, 0, 8);
            notice.Visible = false;
            layout.Controls.Add(notice, 0, 5);
            var buttons = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Margin = new Padding(0) };
            cancelButton.Text = Language.GetText("Cancel");
            backButton.Text = Language.GetText("AddLinks_Back");
            addButton.AutoSize = true;
            addButton.Enabled = false;
            addButton.Text = Language.GetText("Add links");
            retryButton.AutoSize = true;
            retryButton.Text = Language.GetText("Preflight_Retry");
            retryButton.Enabled = false;
            buttons.Controls.Add(addButton);
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(backButton);
            buttons.Controls.Add(retryButton);
            foreach (Button button in new Button[] { cancelButton, addButton, backButton, retryButton, stopButton })
            {
                button.AutoSize = true;
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                button.Padding = new Padding(10, 4, 10, 4);
                button.MinimumSize = new Size(0, 34);
            }
            UiStyles.SetRole(buttons, UiStyles.Role.Surface);
            UiStyles.SetRole(addButton, UiStyles.Role.PrimaryButton);
            layout.Controls.Add(buttons, 0, 6);
            Controls.Add(layout);
            search.TextChanged += (s, e) =>
                {
                    filterTimer.Stop();
                    filterTimer.Start();
                };
            search.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode != Keys.Enter)
                        return;
                    filterTimer.Stop();
                    BindRows();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                };
            filterTimer.Tick += (s, e) =>
                {
                    filterTimer.Stop();
                    BindRows();
                };
            category.SelectedIndexChanged += (s, e) => BindRows();
            grid.CellValueNeeded += ValueNeeded;
            grid.CellValuePushed += ValuePushed;
            grid.CellContentClick += (sender, e) =>
                {
                    if (e.ColumnIndex != 0 || SelectionLocked)
                        return;
                    var node = NodeAt(e.RowIndex);
                    if (node is null || node.EligibleCount == 0)
                        return;
                    ChangeCheckbox(e.RowIndex, node.CheckState != PreflightTree.TriState.Checked);
                };
            grid.CellBeginEdit += BeginCellEdit;
            grid.CellFormatting += FormatCell;
            grid.CellPainting += PaintTreeCell;
            grid.CellMouseClick += TreeCellClick;
            grid.CellMouseDoubleClick += (sender, e) =>
                {
                    if (e.ColumnIndex != 1 || e.RowIndex < 0)
                        return;
                    var node = NodeAt(e.RowIndex);
                    if (node is not null && node.IsFolder && !OnExpander(node, e.X))
                        ToggleFolder(e.RowIndex);
                };
            grid.CellToolTipTextNeeded += (sender, e) =>
                {
                    var node = NodeAt(e.RowIndex);
                    if (node is null)
                        return;
                    if (node.IsFolder)
                    {
                        e.ToolTipText = Language.GetText("Preflight_FolderHint");
                    }
                    else if (e.ColumnIndex == 3)
                    {
                        e.ToolTipText = node.Entry.Detail ?? "";
                    }
                    else if (e.ColumnIndex == 1)
                    {
                        e.ToolTipText = node.Entry.Name;
                    }
                };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
                {
                    if (grid.IsCurrentCellDirty)
                        grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                };
            grid.KeyDown += GridKeyDown;
            grid.ColumnHeaderMouseClick += (sender, e) =>
                {
                    if (e.ColumnIndex == 0)
                        SelectVisible(true);
                };
            addButton.Click += ConfirmSelection;
            retryButton.Click += (s, e) => BeginInspection(true);
            backButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
            cancelButton.Click += (s, e) => CancelRequested?.Invoke(this, EventArgs.Empty);
            stopButton.Click += (s, e) => StopInspection();
            progressTimer.Tick += (s, e) => RefreshProgress();
            UiStyles.SetRole(this, UiStyles.Role.Surface);
            ThemeManager.ApplyThemeToContextMenuStrip(selectionMenu);
            ThemeManager.ApplyThemeToToolTip(targetTip);
            UpdateTargetLabel();
            ApplyGridTheme();
        }

        private void AddSelectionAction(ToolStripDropDownButton parent, string key, Action action)
        {
            var item = new ToolStripMenuItem(Language.GetText(key));
            item.Click += (s, e) => action();
            parent.DropDownItems.Add(item);
        }

        private void AddContextAction(string key, PreflightSelection.SelectionOperation operation)
        {
            var item = new ToolStripMenuItem(Language.GetText(key));
            item.Click += (s, e) => ChangeHighlighted(operation);
            selectionMenu.Items.Add(item);
        }

        public void StartInspection()
        {
            BeginInspection();
        }

        public Button ConfirmationButton
        {
            get
            {
                return addButton;
            }
        }

        public Button DismissButton
        {
            get
            {
                return cancelButton;
            }
        }

        public PreflightSession.Request ActiveRequest
        {
            get
            {
                return request;
            }
        }

        public void ShowNotice(string text, bool isError = false)
        {
            notice.Text = text;
            notice.Visible = !string.IsNullOrEmpty(text);
            UiStyles.SetRole(notice, isError ? UiStyles.Role.ErrorText : UiStyles.Role.BodyText);
        }

        private void StopInspection()
        {
            if (!loading || cancellation is null)
                return;
            InspectionStopped = true;
            cancellation.Cancel();
            session.Invalidate();
            stopButton.Enabled = false;
            summary.Text = Language.GetText("AddLinks_StoppingCheck");
        }

        public void SetImporting(bool value)
        {
            importing = value;
            if (value)
            {
                filterTimer.Stop();
                selectionMenu.Close();
            }
            grid.Enabled = !value && !loading;
            filters.Enabled = !value && !loading;
            tools.Enabled = !value && !loading;
            backButton.Enabled = !value;
            retryButton.Enabled = !value && !loading;
            cancelButton.Enabled = true;
            cancelButton.Text = Language.GetText(value ? "AddLinks_CancelImport" : "Cancel");
            stopButton.Visible = loading;
            if (!value)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Maximum = 1000;
                progressBar.Value = 1000;
                BindRows();
            }
        }

        public void ShowImportProgress(int completed, int total, bool committing, bool stopping)
        {
            if (!importing || disposeRequested)
                return;
            string key = stopping ? "AddLinks_StoppingImport" : committing ? "AddLinks_Committing" : "AddLinks_ImportProgress";
            summary.Text = Language.GetText(key).Replace("%N%", completed.ToString()).Replace("%T%", total.ToString());
            progressBar.Visible = true;
            progressBar.Maximum = 1000;
            if (committing || stopping)
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = (int)Math.Round(Math.Min(1000.0d, completed * 1000d / Math.Max(1, total)));
            }
            addButton.Enabled = false;
            addButton.Text = Language.GetText("Loading...");
            cancelButton.Enabled = !committing && !stopping;
        }

        public async void UpdateDestination(string target)
        {
            if (disposeRequested || string.Equals(destination, target, StringComparison.Ordinal))
                return;
            destination = target;
            UpdateTargetLabel();
            int generation = Interlocked.Increment(ref spaceGeneration);
            checkingSpace = true;
            freeSpace = default;
            UpdateSummary();
            try
            {
                var available = await Task.Run(() => BatchLinkPreflight.AvailableSpace(target));
                if (disposeRequested || generation != spaceGeneration)
                    return;
                freeSpace = available;
            }
            catch (Exception ex)
            {
                if (!disposeRequested && generation == spaceGeneration)
                {
                    Log.WriteWarning("Checking the import destination failed: " + Log.SafeException(ex));
                    ShowNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
                }
            }
            finally
            {
                if (!disposeRequested && generation == spaceGeneration)
                {
                    checkingSpace = false;
                    UpdateSummary();
                }
            }
        }

        private void UpdateTargetLabel()
        {
            targetLabel.Text = Language.GetText("AddLinks_Target").Replace("%PATH%", destination);
            targetTip.SetToolTip(targetLabel, targetLabel.Text);
        }

        private async void BeginInspection(bool force = false)
        {
            if (loading || importing || disposeRequested)
                return;
            var source = new CancellationTokenSource();
            cancellation = source;
            InspectionStopped = false;
            loading = true;
            addButton.Enabled = false;
            retryButton.Enabled = false;
            grid.Enabled = false;
            filters.Enabled = false;
            tools.Enabled = false;
            stopButton.Text = Language.GetText("AddLinks_StopCheck");
            stopButton.Enabled = true;
            stopButton.Visible = true;
            ShowNotice("");
            summary.Text = Language.GetText("Preflight_Checking");
            displayedProgress = null;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            bool succeeded = false;
            bool failed = false;
            try
            {
                var token = source.Token;
                var oldEntries = entries;
                if (force || request is null)
                    request = session.Prepare(request is null ? string.Join(Constants.vbLf, urls) : request.Text, force);
                progressTimer.Start();
                RefreshProgress();
                var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                List<BatchLinkPreflight.Entry> result;
                using (var registration = token.Register(() => waiter.TrySetCanceled()))
                {
                    await Task.WhenAny(request.Results, waiter.Task);
                    token.ThrowIfCancellationRequested();
                    result = await request.Results;
                }
                var currentQueue = queued;
                if (queueProvider is not null)
                    currentQueue = await Task.Run(queueProvider, token);
                progressTimer.Stop();
                summary.Text = Language.GetText("Preflight_BuildingTree");
                progressBar.Style = ProgressBarStyle.Marquee;
                var prepared = await Task.Run(() =>
                    {
                        var previous = new Dictionary<string, bool>(StringComparer.Ordinal);
                        foreach (BatchLinkPreflight.Entry entry in oldEntries)
                        {
                            string key = BatchLinkPreflight.Identity(entry.Source);
                            if (key is not null && entry.CanAdd && !previous.ContainsKey(key))
                                previous.Add(key, entry.Selected);
                        }
                        var updated = PreflightSession.Reconcile(result, currentQueue);
                        foreach (BatchLinkPreflight.Entry entry in updated)
                        {
                            string key = BatchLinkPreflight.Identity(entry.Source);
                            if (key is not null && previous.ContainsKey(key))
                                entry.Selected = entry.CanAdd && previous[key];
                        }
                        token.ThrowIfCancellationRequested();
                        var model = new PreflightSelection(updated);
                        return Tuple.Create(model, new PreflightTree(model));
                    }, token);
                string target = destination;
                int targetGeneration = spaceGeneration;
                var available = await Task.Run(() => BatchLinkPreflight.AvailableSpace(target), token);
                if (disposeRequested || token.IsCancellationRequested)
                    return;
                selection = prepared.Item1;
                tree = prepared.Item2;
                entries = selection.Entries;
                if (targetGeneration == spaceGeneration)
                    freeSpace = available;
                if (entries.Count == 0)
                    ShowNotice(Language.GetText("Links not valid"), true);
                succeeded = true;
            }
            catch (OperationCanceledException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                failed = true;
                if (!disposeRequested)
                    ShowNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
            }
            finally
            {
                loading = false;
                if (ReferenceEquals(cancellation, source))
                    cancellation = null;
                source.Dispose();
                if (!disposeRequested)
                {
                    progressTimer.Stop();
                    progressBar.Style = ProgressBarStyle.Continuous;
                    progressBar.Maximum = 1000;
                    progressBar.Value = succeeded ? 1000 : 0;
                    grid.Enabled = true;
                    filters.Enabled = true;
                    tools.Enabled = true;
                    stopButton.Visible = false;
                    retryButton.Enabled = true;
                    BindRows();
                    if (failed)
                        summary.Text = Language.GetText("AddLinks_CheckFailed");
                    if (InspectionStopped)
                    {
                        summary.Text = Language.GetText("AddLinks_CheckStopped");
                        ShowNotice(Language.GetText("AddLinks_CheckStopped"));
                    }
                }
            }
        }

        private void RefreshProgress()
        {
            if (disposeRequested || !loading || request is null || InspectionStopped)
                return;
            var snapshot = request.Progress;
            long second = (long)Math.Round(Math.Max(0d, Math.Floor((DateTime.UtcNow - request.Started).TotalSeconds)));
            if (ReferenceEquals(snapshot, displayedProgress) && second == displayedProgressSecond)
                return;
            displayedProgress = snapshot;
            displayedProgressSecond = second;
            PreflightProgressDisplay.Display(snapshot, summary, progressBar, request.Started);
        }

        private void ApplyGridTheme()
        {
            if (disposeRequested)
                return;
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = ThemeManager.GetColor("Back");
            grid.GridColor = ThemeManager.GetColor("Border");
            grid.BorderStyle = BorderStyle.None;
            grid.DefaultCellStyle.BackColor = ThemeManager.GetColor("Back");
            grid.DefaultCellStyle.ForeColor = ThemeManager.GetColor("Fore");
            grid.DefaultCellStyle.SelectionBackColor = ThemeManager.GetColor("Selection");
            grid.DefaultCellStyle.SelectionForeColor = ThemeManager.GetColor("SelectionFore");
            grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.GetColor("ControlBack");
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.GetColor("Fore");
            ThemeManager.ApplyThemeToContextMenuStrip(selectionMenu);
            ThemeManager.ApplyThemeToToolTip(targetTip);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            if (grid is not null && grid.Columns.Count > 0)
                ApplyGridTheme();
        }

        private void BindRows()
        {
            if (SelectionLocked)
                return;
            if (selection is null || !ReferenceEquals(selection.Entries, entries))
            {
                selection = new PreflightSelection(entries);
                tree = null;
            }
            if (tree is null)
                tree = new PreflightTree(selection);
            string[] categories = new[] { "", "Video", "Audio", "Archives", "Documents", "Other" };
            selection.Filter(search.Text, categories[Math.Max(0, category.SelectedIndex)]);
            tree.Filter(search.Text.Length > 0 || category.SelectedIndex > 0);
            BindTreeRows();
            UpdateSummary();
        }

        private void BindTreeRows(PreflightTree.Node currentNode = null)
        {
            // The model projection may already have changed. Read previous indices from
            // the last projection, not from the newly expanded/filtered tree.
            var highlighted = new HashSet<PreflightTree.Node>();
            int index = grid.Rows.GetFirstRow(DataGridViewElementStates.Selected);
            while (index >= 0)
            {
                if (index < boundRows.Count)
                    highlighted.Add(boundRows[index]);
                index = grid.Rows.GetNextRow(index, DataGridViewElementStates.Selected);
            }
            if (currentNode is null && grid.CurrentCell is not null && grid.CurrentCell.RowIndex < boundRows.Count)
            {
                currentNode = boundRows[grid.CurrentCell.RowIndex];
            }
            int topIndex = grid.IsHandleCreated ? grid.FirstDisplayedScrollingRowIndex : -1;
            var topNode = topIndex >= 0 && topIndex < boundRows.Count ? boundRows[topIndex] : null;
            binding = true;
            grid.SuspendLayout();
            try
            {
                grid.CurrentCell = null;
                grid.RowCount = tree.Rows.Count;
                grid.ClearSelection();
                boundRows = new List<PreflightTree.Node>(tree.Rows);
                int currentIndex = -1;
                int nextTop = Math.Min(topIndex, boundRows.Count - 1);
                for (int row = 0, loopTo = boundRows.Count - 1; row <= loopTo; row++)
                {
                    var node = boundRows[row];
                    if (ReferenceEquals(node, currentNode))
                        currentIndex = row;
                    if (ReferenceEquals(node, topNode))
                        nextTop = row;
                }
                if (currentIndex >= 0)
                    grid.CurrentCell = grid[1, currentIndex];
                grid.ClearSelection();
                for (int row = 0, loopTo1 = boundRows.Count - 1; row <= loopTo1; row++)
                {
                    if (highlighted.Contains(boundRows[row]))
                        grid.Rows[row].Selected = true;
                }
                if (highlighted.Count == 0 && currentIndex >= 0)
                    grid.Rows[currentIndex].Selected = true;
                if (grid.IsHandleCreated && grid.DisplayedRowCount(false) > 0 && nextTop >= 0)
                    grid.FirstDisplayedScrollingRowIndex = nextTop;
                grid.Invalidate();
            }
            finally
            {
                grid.ResumeLayout();
                binding = false;
            }
        }

        private PreflightTree.Node NodeAt(int index)
        {
            if (tree is null || index < 0 || index >= tree.Rows.Count)
                return null;
            return tree.Rows[index];
        }

        private int TreeIndent(PreflightTree.Node node)
        {
            return Math.Min(node.Depth, 16) * TreeStep();
        }

        private int TreeStep()
        {
            return Math.Max((int)Math.Round(20 * grid.DeviceDpi / 96.0d), grid.Font.Height + 4);
        }

        private bool OnExpander(PreflightTree.Node node, int x)
        {
            return x >= TreeIndent(node) && x < TreeIndent(node) + TreeStep();
        }

        private void PaintTreeCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex != 1 || e.RowIndex < 0)
                return;
            var node = NodeAt(e.RowIndex);
            if (node is null || !node.IsFolder)
                return;
            e.Paint(e.ClipBounds, e.PaintParts);
            int side = Math.Max(10, (int)Math.Round(11 * grid.DeviceDpi / 96.0d));
            var glyph = new Rectangle(e.CellBounds.Left + TreeIndent(node) + (int)Math.Round(3 * grid.DeviceDpi / 96.0d), e.CellBounds.Top + (e.CellBounds.Height - side) / 2, side, side);
            var color = (e.State & DataGridViewElementStates.Selected) != 0 ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor;
            var state = e.Graphics.Save();
            try
            {
                e.Graphics.SetClip(Rectangle.Intersect(e.CellBounds, e.ClipBounds), System.Drawing.Drawing2D.CombineMode.Intersect);
                using (var pen = new Pen(color))
                {
                    e.Graphics.DrawRectangle(pen, glyph);
                    e.Graphics.DrawLine(pen, glyph.Left + 2, glyph.Top + side / 2, glyph.Right - 2, glyph.Top + side / 2);
                    if (!tree.IsExpanded(node))
                        e.Graphics.DrawLine(pen, glyph.Left + side / 2, glyph.Top + 2, glyph.Left + side / 2, glyph.Bottom - 2);
                }
            }
            finally
            {
                e.Graphics.Restore(state);
            }
            e.Handled = true;
        }

        private void TreeCellClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (SelectionLocked || e.Button != MouseButtons.Left || e.ColumnIndex != 1 || e.RowIndex < 0 || e.Clicks > 1)
                return;
            var node = NodeAt(e.RowIndex);
            if (node is not null && node.IsFolder && OnExpander(node, e.X))
                ToggleFolder(e.RowIndex);
        }

        private void ToggleFolder(int index)
        {
            if (SelectionLocked || tree is null)
                return;
            var node = NodeAt(index);
            if (node is null || !node.IsFolder)
                return;
            tree.Toggle(node);
            BindTreeRows(node);
        }

        private void ExpandFolders(bool expand)
        {
            if (SelectionLocked || tree is null)
                return;
            if (filterTimer.Enabled)
            {
                filterTimer.Stop();
                BindRows();
            }
            tree.ExpandAll(expand);
            BindTreeRows();
        }

        private void ValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var node = NodeAt(e.RowIndex);
            if (node is null)
                return;
            if (node.IsFolder)
            {
                switch (e.ColumnIndex)
                {
                    case 0:
                        e.Value = ToWinFormsState(node.CheckState);
                        break;
                    case 1:
                        e.Value = node.Name;
                        break;
                    case 2:
                        e.Value = node.UnknownSizes == node.MatchCount ? "—" : FormatSize(node.SizeBytes) + (node.UnknownSizes > 0 ? "+" : "");
                        break;
                    case 3:
                        e.Value = folderSelectionLabel.Replace("%S%", node.SelectedCount.ToString()).Replace("%N%", node.MatchCount.ToString());
                        break;
                    case 4:
                        e.Value = "";
                        break;
                }
                return;
            }
            var entry = node.Entry;
            switch (e.ColumnIndex)
            {
                case 0:
                    e.Value = entry.Selected ? CheckState.Checked : CheckState.Unchecked;
                    break;
                case 1:
                    e.Value = entry.Name;
                    break;
                case 2:
                    e.Value = entry.SizeBytes.HasValue ? FormatSize(entry.SizeBytes.GetValueOrDefault()) : "—";
                    break;
                case 3:
                    e.Value = statusLabels[entry.State];
                    break;
                case 4:
                    e.Value = entry.Source is null ? "" : entry.Source.Path;
                    break;
            }
        }

        private void BeginCellEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            var node = NodeAt(e.RowIndex);
            if (SelectionLocked || node is null || node.EligibleCount == 0)
                e.Cancel = true;
        }

        private void FormatCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var node = NodeAt(e.RowIndex);
            if (node is null)
                return;
            e.CellStyle.ForeColor = node.EligibleCount == 0 ? ThemeManager.GetColor("Muted") : ThemeManager.GetColor("Fore");
            if (e.ColumnIndex == 1)
                e.CellStyle.Padding = new Padding(TreeIndent(node) + TreeStep(), 0, 0, 0);
            if (node.IsFolder)
            {
                e.CellStyle.BackColor = ThemeManager.GetColor("ControlBack");
            }
            else if (e.ColumnIndex == 3 && node.Entry.State == BatchLinkPreflight.LinkState.Invalid)
            {
                e.CellStyle.ForeColor = ThemeManager.GetColor("ErrorFore");
            }
        }

        /// <summary>The core tree reports its own tri-state; the grid column needs the
        /// WinForms enum with the same three values.</summary>
        private static CheckState ToWinFormsState(PreflightTree.TriState state)
        {
            switch (state)
            {
                case PreflightTree.TriState.Checked:
                    return CheckState.Checked;
                case PreflightTree.TriState.Indeterminate:
                    return CheckState.Indeterminate;
                default:
                    return CheckState.Unchecked;
            }
        }

        private void ValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (binding || SelectionLocked || e.ColumnIndex != 0)
                return;
            var node = NodeAt(e.RowIndex);
            if (node is null)
                return;
            // A mixed folder becomes fully checked; a fully checked folder is cleared.
            bool value = e.Value is bool ? Conversions.ToBoolean(e.Value) : (CheckState)Conversions.ToInteger(e.Value) != CheckState.Unchecked;
            ChangeCheckbox(e.RowIndex, value);
        }

        private void ChangeCheckbox(int index, bool value)
        {
            var node = NodeAt(index);
            if (node is null || SelectionLocked)
                return;
            if ((grid.Rows.GetRowState(index) & DataGridViewElementStates.Selected) != 0)
            {
                ChangeHighlighted(value ? PreflightSelection.SelectionOperation.SelectAll : PreflightSelection.SelectionOperation.Clear);
            }
            else
            {
                tree.Apply(new PreflightTree.Node[] { node }, value ? PreflightSelection.SelectionOperation.SelectAll : PreflightSelection.SelectionOperation.Clear);
                UpdateSummary();
            }
            grid.Invalidate();
        }

        private void GridKeyDown(object sender, KeyEventArgs e)
        {
            if (SelectionLocked)
                return;
            if ((e.KeyCode == Keys.Right || e.KeyCode == Keys.Left) && grid.CurrentCell is not null && grid.CurrentCell.ColumnIndex == 1)
            {
                var node = NodeAt(grid.CurrentCell.RowIndex);
                if (node is null)
                    return;
                if (node.IsFolder && tree.IsExpanded(node) != (e.KeyCode == Keys.Right))
                {
                    ToggleFolder(grid.CurrentCell.RowIndex);
                }
                else if (e.KeyCode == Keys.Left && node.Parent is not null)
                {
                    int parentIndex = tree.Rows.IndexOf(node.Parent);
                    if (parentIndex < 0)
                        return;
                    grid.CurrentCell = grid[1, parentIndex];
                }
                else
                {
                    return;
                }
            }
            else if (e.KeyCode == Keys.Space)
            {
                ChangeHighlighted(PreflightSelection.SelectionOperation.Invert);
            }
            else if (e.Control && e.KeyCode == Keys.I)
            {
                ApplySelection(PreflightSelection.SelectionOperation.Invert, true);
            }
            else
            {
                return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void ChangeHighlighted(PreflightSelection.SelectionOperation operation)
        {
            if (selection is null || SelectionLocked)
                return;
            var targets = new List<PreflightTree.Node>();
            int index = grid.Rows.GetFirstRow(DataGridViewElementStates.Selected);
            while (index >= 0)
            {
                var node = NodeAt(index);
                if (node is not null)
                    targets.Add(node);
                index = grid.Rows.GetNextRow(index, DataGridViewElementStates.Selected);
            }
            tree.Apply(targets, operation);
            grid.Invalidate();
            UpdateSummary();
        }

        private void SelectVisible(bool selected)
        {
            ApplySelection(selected ? PreflightSelection.SelectionOperation.SelectAll : PreflightSelection.SelectionOperation.Clear, true);
        }

        private void ApplySelection(PreflightSelection.SelectionOperation operation, bool visibleOnly)
        {
            if (selection is null || SelectionLocked)
                return;
            if (visibleOnly && filterTimer.Enabled)
            {
                filterTimer.Stop();
                BindRows();
            }
            selection.Apply(operation, visibleOnly);
            grid.Invalidate();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (selection is null || loading || importing || disposeRequested)
                return;
            summary.Text = Language.GetText("Preflight_Summary").Replace("%N%", entries.Count.ToString()).Replace("%S%", selection.SelectedCount.ToString()).Replace("%D%", selection.DuplicateCount.ToString()).Replace("%E%", selection.InvalidCount.ToString()).Replace("%U%", selection.UnavailableCount.ToString());
            spaceLabel.Text = Language.GetText("Preflight_Total").Replace("%SIZE%", FormatSize(selection.SelectedBytes)).Replace("%FREE%", freeSpace.HasValue ? FormatSize(freeSpace.GetValueOrDefault()) : "—");
            if (selection.UnknownSizes > 0)
                spaceLabel.Text += " · " + Language.GetText("Preflight_UnknownSizes").Replace("%N%", selection.UnknownSizes.ToString());
            bool noSpace = freeSpace.HasValue && selection.SelectedBytes > freeSpace.GetValueOrDefault();
            if (noSpace)
                spaceLabel.Text += " · " + Language.GetText("Preflight_NoSpace");
            UiStyles.SetRole(spaceLabel, noSpace ? UiStyles.Role.ErrorText : UiStyles.Role.BodyText);
            addButton.Enabled = !checkingSpace && selection.SelectedCount > 0 && !noSpace;
            addButton.Text = Language.GetText("Preflight_AddSelected").Replace("%N%", selection.SelectedCount.ToString());
        }

        private void ConfirmSelection(object sender, EventArgs e)
        {
            if (SelectionLocked || !addButton.Enabled)
                return;
            if (filterTimer.Enabled)
            {
                filterTimer.Stop();
                BindRows();
            }
            if (!addButton.Enabled)
                return;
            SelectedFiles = entries.Where(entry => entry.CanAdd && entry.Selected).Select(entry => entry.Source).ToList();
            if (SelectedFiles.Count == 0)
                return;
            ConfirmRequested?.Invoke(this, EventArgs.Empty);
        }

        private static string FormatSize(decimal bytes)
        {
            string[] units = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            var index = default(int);
            while (bytes >= 1024m && index < units.Length - 1)
            {
                bytes /= 1024m;
                index += 1;
            }
            return bytes.ToString(index == 0 ? "F0" : "F2") + " " + units[index];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposeRequested)
            {
                disposeRequested = true;
                filterTimer.Dispose();
                progressTimer.Dispose();
                selectionMenu.Dispose();
                targetTip.Dispose();
                if (cancellation is not null)
                    cancellation.Cancel();
                if (ownsSession)
                    session.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}