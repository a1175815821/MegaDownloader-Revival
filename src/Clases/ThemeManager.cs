using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;

namespace MegaDownloader
{

    /// <summary>
/// 主题管理器:Auto 模式从注册表读取系统深/浅色偏好,Light/Dark 直接应用预设配色。
/// Auto 模式下监听系统主题变化并实时刷新已注册窗体。
/// 支持 ObjectListView (BrightIdeasSoftware.TreeListView)、ToolStrip 系列(MenuStrip/StatusStrip/ContextMenuStrip)、
/// 标准控件(TextBox/Button/Label 等)以及容器控件(Panel/GroupBox/TableLayoutPanel)。
/// </summary>
    public sealed class ThemeManager
    {

        private ThemeManager()
        {
        }

        public enum ResolvedTheme
        {
            Light,
            Dark
        }

        private static ResolvedTheme _current = ResolvedTheme.Light;
        private static ConfiguracionUI.ThemeModeType _currentMode = ConfiguracionUI.ThemeModeType.Auto;
        private static bool _systemWatchHooked = false;
        private static readonly List<WeakReference> _watchedForms = new List<WeakReference>();

        // Muted:次要说明文字专用色。此前这类文字取 "Border",而 Border 是边框色,
        // 浅色主题下与 ControlBack(240,240,240)对比度只有 1.38:1(深色 1.82:1),
        // 远低于正文 4.5:1 的下限,左栏总览说明几乎读不出来。
        // 现取值对 ControlBack 的对比度:浅色 5.16:1 / 深色 4.97:1。
        // 注意:注释不能写进 From { } 集合初始化器内部 —— VB 的隐式行继续会被注释行打断,
        // 右花括号随之提前闭合,其后每个元素都被判语法错误(BC30035)。
        // 浅色配色
        private static readonly Dictionary<string, Color> LightColors = new Dictionary<string, Color>() { { "Back", Color.White }, { "Fore", Color.FromArgb(35, 41, 52) }, { "AltBack", Color.FromArgb(251, 252, 253) }, { "ControlBack", Color.FromArgb(247, 248, 250) }, { "Canvas", Color.FromArgb(241, 243, 246) }, { "InputBack", Color.FromArgb(247, 248, 250) }, { "Border", Color.FromArgb(229, 232, 237) }, { "ButtonBack", Color.White }, { "ButtonBorder", Color.FromArgb(207, 213, 222) }, { "ButtonDisabledBack", Color.FromArgb(239, 242, 247) }, { "ButtonDisabledFore", Color.FromArgb(102, 113, 129) }, { "ButtonDisabledBorder", Color.FromArgb(193, 202, 214) }, { "Muted", Color.FromArgb(101, 113, 132) }, { "Selection", Color.FromArgb(251, 236, 239) }, { "SelectionFore", Color.FromArgb(158, 34, 51) }, { "Accent", Color.FromArgb(199, 53, 72) }, { "AccentFore", Color.White }, { "AccentHover", Color.FromArgb(179, 37, 51) }, { "AccentPressed", Color.FromArgb(154, 30, 43) }, { "Link", Color.FromArgb(36, 94, 173) }, { "ToolBack", Color.White }, { "ToolBorder", Color.FromArgb(216, 222, 231) }, { "ErrorFore", Color.FromArgb(192, 0, 0) }, { "QuotaBack", Color.FromArgb(178, 34, 34) }, { "QuotaFore", Color.White }, { "SuccessFore", Color.FromArgb(26, 121, 90) }, { "ProgressBack", Color.FromArgb(233, 237, 241) }, { "ProgressFill", Color.FromArgb(50, 110, 170) }, { "ProgressGradientStart", Color.FromArgb(40, 148, 113) }, { "ProgressGradientEnd", Color.FromArgb(40, 148, 113) }, { "ButtonHover", Color.FromArgb(240, 242, 246) }, { "ButtonPressed", Color.FromArgb(229, 233, 239) } };

        // Muted:深色主题下的次要说明文字色,对 ControlBack(51,51,55)对比度 4.97:1
        // (此前误用 Border 色,仅 1.82:1)。
        // 深色配色(VS Code Dark+ 风格)
        private static readonly Dictionary<string, Color> DarkColors = new Dictionary<string, Color>() { { "Back", Color.FromArgb(32, 37, 45) }, { "Fore", Color.FromArgb(231, 235, 241) }, { "AltBack", Color.FromArgb(34, 40, 49) }, { "ControlBack", Color.FromArgb(26, 31, 38) }, { "Canvas", Color.FromArgb(21, 25, 31) }, { "InputBack", Color.FromArgb(26, 31, 38) }, { "Border", Color.FromArgb(52, 60, 72) }, { "ButtonBack", Color.FromArgb(38, 44, 54) }, { "ButtonBorder", Color.FromArgb(73, 83, 99) }, { "ButtonDisabledBack", Color.FromArgb(37, 45, 58) }, { "ButtonDisabledFore", Color.FromArgb(153, 169, 191) }, { "ButtonDisabledBorder", Color.FromArgb(73, 87, 106) }, { "Muted", Color.FromArgb(164, 175, 191) }, { "Selection", Color.FromArgb(56, 39, 46) }, { "SelectionFore", Color.FromArgb(244, 155, 166) }, { "Accent", Color.FromArgb(228, 92, 108) }, { "AccentFore", Color.FromArgb(21, 25, 31) }, { "AccentHover", Color.FromArgb(238, 122, 135) }, { "AccentPressed", Color.FromArgb(220, 86, 103) }, { "Link", Color.FromArgb(135, 184, 250) }, { "ToolBack", Color.FromArgb(32, 37, 45) }, { "ToolBorder", Color.FromArgb(52, 60, 72) }, { "ErrorFore", Color.FromArgb(244, 135, 113) }, { "QuotaBack", Color.FromArgb(122, 28, 28) }, { "QuotaFore", Color.White }, { "SuccessFore", Color.FromArgb(78, 201, 176) }, { "ProgressBack", Color.FromArgb(51, 60, 72) }, { "ProgressFill", Color.FromArgb(144, 185, 230) }, { "ProgressGradientStart", Color.FromArgb(67, 184, 146) }, { "ProgressGradientEnd", Color.FromArgb(67, 184, 146) }, { "ButtonHover", Color.FromArgb(42, 49, 60) }, { "ButtonPressed", Color.FromArgb(51, 61, 75) } };

        private static Dictionary<string, Color> CurrentColors()
        {
            return _current == ResolvedTheme.Dark ? DarkColors : LightColors;
        }

        /// <summary>
    /// 按 token 名取当前主题颜色。未知 key 返回 Fore。
    /// </summary>
        public static Color GetColor(string key)
        {
            var colors = CurrentColors();
            if (colors.ContainsKey(key))
            {
                return colors[key];
            }
            return colors["Fore"];
        }

        /// <summary>
    /// 检测系统当前是否使用深色主题。
    /// 通过读取注册表 HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme 实现。
    /// 不支持深浅模式的旧系统(无该键)默认返回 False(浅色)。
    /// </summary>
        public static bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key is null)
                        return false;
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is null)
                        return false;
                    return Conversions.ToInteger(val) == 0; // 0 = 深色,1 = 浅色
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
                return false;
            }
        }

        public static ResolvedTheme Resolve(ConfiguracionUI.ThemeModeType mode)
        {
            switch (mode)
            {
                case ConfiguracionUI.ThemeModeType.Dark:
                    return ResolvedTheme.Dark;
                case ConfiguracionUI.ThemeModeType.Light:
                    return ResolvedTheme.Light; // Auto
                default:
                    return IsSystemDarkMode() ? ResolvedTheme.Dark : ResolvedTheme.Light;
            }
        }

        /// <summary>
    /// 应用主题到指定窗体(递归所有子控件),并设置全局 ToolStrip 渲染器。
    /// 应在窗体 Load/Shown 后调用,确保所有控件已创建。
    /// </summary>
        public static void ApplyTheme(Form form, ConfiguracionUI.ThemeModeType mode)
        {
            var resolved = Resolve(mode);
            _current = resolved;
            _currentMode = mode;
            form.HandleCreated -= ThemedForm_HandleCreated;
            form.HandleCreated += ThemedForm_HandleCreated;
            ApplyWindowFrame(form);
            var colors = resolved == ResolvedTheme.Dark ? DarkColors : LightColors;

            // 设置全局 ToolStrip 渲染器(影响 MenuStrip/StatusStrip/ContextMenuStrip 等)
            ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable(colors));

            ApplyThemeRecursive(form, colors, resolved);

            ApplyThemeToFormContextMenus(form, colors);

            // ToolTip 不在 Controls 树中,需从字段显式处理(RC:此前深色下仍浅黄底)
            ApplyThemeToFormToolTips(form, colors);

            RegisterFormForSystemThemeWatch(form);
            EnsureSystemThemeWatch();
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        private static void ThemedForm_HandleCreated(object sender, EventArgs e)
        {
            ApplyWindowFrame((Form)sender);
        }

        private static void ApplyWindowFrame(Form form)
        {
            if (!form.IsHandleCreated || form.IsDisposed)
                return;
            try
            {
                int dark = _current == ResolvedTheme.Dark ? 1 : 0;
                DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                // Newer Windows versions accept explicit caption colours. Older
                // systems reject unsupported attributes and retain their native frame.
                int caption = ColorTranslator.ToWin32(GetColor("Back"));
                int text = ColorTranslator.ToWin32(GetColor("Fore"));
                DwmSetWindowAttribute(form.Handle, 35, ref caption, 4);
                DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
            }
            catch (DllNotFoundException ex)
            {
            }
            catch (EntryPointNotFoundException ex)
            {
            }
        }

        private static void RegisterFormForSystemThemeWatch(Form form)
        {
            if (form is null)
                return;
            foreach (WeakReference wr in _watchedForms)
            {
                if (wr.IsAlive && ReferenceEquals(wr.Target, form))
                    return;
            }
            _watchedForms.Add(new WeakReference(form));
            form.FormClosed += WatchedForm_FormClosed;
        }

        private static void WatchedForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form form = sender as Form;
            if (form is null)
                return;
            form.FormClosed -= WatchedForm_FormClosed;
            for (int i = _watchedForms.Count - 1; i >= 0; i -= 1)
            {
                if (!_watchedForms[i].IsAlive || ReferenceEquals(_watchedForms[i].Target, form))
                {
                    _watchedForms.RemoveAt(i);
                }
            }
        }

        private static void EnsureSystemThemeWatch()
        {
            if (_systemWatchHooked)
                return;
            try
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _systemWatchHooked = true;
            }
            catch
            {
            }
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (_currentMode != ConfiguracionUI.ThemeModeType.Auto)
                return;
            if (e.Category != UserPreferenceCategory.Color && e.Category != UserPreferenceCategory.General)
                return;
            var nextTheme = Resolve(ConfiguracionUI.ThemeModeType.Auto);
            if (nextTheme == _current)
                return;
            try
            {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
                {
                    // N-P3-2: per-form guard. The loop body used to throw straight into the
                    // outer empty Catch (e.g. InvalidOperationException from BeginInvoke when
                    // a form's handle died between the IsHandleCreated check and the call),
                    // aborting the whole refresh so the remaining windows kept the old
                    // colors — light and dark mixed on screen after a system theme switch.
                    // One bad form must not skip the rest.
                    try
                    {
                        if (form.IsHandleCreated && !form.IsDisposed)
                        {
                            if (form.InvokeRequired)
                            {
                                form.BeginInvoke(new Action<Form>(RefreshFormThemeSafe), form);
                            }
                            else
                            {
                                RefreshFormThemeSafe(form);
                            }
                        }
                    }
                    catch (Exception exForm)
                    {
                        Log.WriteDebug("ThemeManager: one form could not be refreshed, continuing with the rest: " + Log.SafeException(exForm));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager: system theme change sweep failed: " + Log.SafeException(ex));
            }
        }

        private static void RefreshFormThemeSafe(Form form)
        {
            try
            {
                if (form is null || form.IsDisposed)
                    return;
                ApplyTheme(form, ConfiguracionUI.ThemeModeType.Auto);
                Main main = form as Main;
                if (main is not null)
                {
                    main.ApplyCurrentTheme();
                }
            }
            catch
            {
            }
        }

        /// <summary>
    /// 使用上次保存的主题模式应用到指定窗体。供子窗体在 Load 事件中调用,
    /// 无需自行访问 Main.Config。
    /// </summary>
        public static void ApplyTheme(Form form)
        {
            ApplyTheme(form, _currentMode);
        }

        /// <summary>
    /// 显式主题化 ContextMenuStrip(不在 Form.Controls 树中)。
    /// </summary>
        public static void ApplyThemeToContextMenuStrip(ContextMenuStrip cms)
        {
            if (cms is null)
                return;
            ApplyThemeToToolStrip(cms, CurrentColors());
        }

        /// <summary>P-12: GetFields() over a whole form type on every theme apply is pure
    /// reflection overhead; the field layout of a given Form type never changes.
    /// Cache by type; the caller still filters by field type.</summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]> _formFieldsCache = new System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]>();

        private static System.Reflection.FieldInfo[] GetCachedFormFields(Form form)
        {
            var t = form.GetType();
            System.Reflection.FieldInfo[] fields = null;
            if (!_formFieldsCache.TryGetValue(t, out fields))
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                fields = t.GetFields(flags);
                fields = _formFieldsCache.GetOrAdd(t, fields);
            }
            return fields;
        }

        private static void ApplyThemeToFormContextMenus(Form form, Dictionary<string, Color> colors)
        {
            if (form is null)
                return;
            try
            {
                foreach (System.Reflection.FieldInfo fi in GetCachedFormFields(form))
                {
                    if (typeof(ContextMenuStrip).IsAssignableFrom(fi.FieldType))
                    {
                        ContextMenuStrip cms = fi.GetValue(form) as ContextMenuStrip;
                        if (cms is not null)
                        {
                            ApplyThemeToToolStrip(cms, colors);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }
        }

        /// N-P3-3: WEAK registry — see ApplyThemeToToolTip. A strong HashSet(Of ToolTip)
    /// here rooted every themed tooltip for the process lifetime.
        private static readonly HashSet<WeakReference> _themedToolTips = new HashSet<WeakReference>();

        /// <summary>主题化单个 ToolTip(含懒创建)。字段扫描扫不到的调用此方法。</summary>
        public static void ApplyThemeToToolTip(ToolTip tt)
        {
            if (tt is null)
                return;
            ApplyThemeToToolTip(tt, CurrentColors());
        }

        private static void ApplyThemeToToolTip(ToolTip tt, Dictionary<string, Color> colors)
        {
            if (tt is null)
                return;
            try
            {
                tt.BackColor = colors["ControlBack"];
                tt.ForeColor = colors["Fore"];
                // RC:非 OwnerDraw 下 Back/Fore 不生效,必须自绘才可见深色 tooltip。
                tt.OwnerDraw = true;
                lock (_themedToolTips)
                {
                    // N-P3-3: the registry holds WEAK references on purpose. It used to
                    // root every ToolTip it ever themed for the whole process lifetime
                    // (entries were only removed on tt.Disposed, which never fired for the
                    // lazy-created "If t Is Nothing Then t = New ToolTip" help tips nobody
                    // disposed), so every open/close of the Configuration / Properties
                    // window leaked one ToolTip HWND + Draw handler pair, unbounded.
                    // Weak refs keep the "attach handlers once" semantics (an entry that
                    // was collected is re-attached on demand) without extending lifetime.
                    PruneDeadToolTips();
                    if (!IsToolTipRegistered(tt))
                    {
                        tt.Draw += ThemedToolTip_Draw;
                        tt.Disposed += ThemedToolTip_Disposed;
                        _themedToolTips.Add(new WeakReference(tt));
                    }
                }
            }
            catch
            {
            }
        }

        private static bool IsToolTipRegistered(ToolTip tt)
        {
            foreach (WeakReference wr in _themedToolTips)
            {
                if (wr.IsAlive && ReferenceEquals(wr.Target, tt))
                    return true;
            }
            return false;
        }

        private static void PruneDeadToolTips()
        {
            if (_themedToolTips.Count == 0)
                return;
            var dead = new List<WeakReference>();
            foreach (WeakReference wr in _themedToolTips)
            {
                if (!wr.IsAlive)
                    dead.Add(wr);
            }
            foreach (WeakReference wr in dead)
                _themedToolTips.Remove(wr);
        }

        private static void ThemedToolTip_Disposed(object sender, EventArgs e)
        {
            try
            {
                ToolTip tt = sender as ToolTip;
                if (tt is null)
                    return;
                lock (_themedToolTips)
                {
                    WeakReference toRemove = null;
                    foreach (WeakReference wr in _themedToolTips)
                    {
                        if (wr.IsAlive && ReferenceEquals(wr.Target, tt))
                        {
                            toRemove = wr;
                            break;
                        }
                    }
                    if (toRemove is not null)
                    {
                        tt.Draw -= ThemedToolTip_Draw;
                        tt.Disposed -= ThemedToolTip_Disposed;
                        _themedToolTips.Remove(toRemove);
                    }
                }
            }
            catch
            {
            }
        }

        private static void ApplyThemeToFormToolTips(Form form, Dictionary<string, Color> colors)
        {
            if (form is null)
                return;
            try
            {
                foreach (System.Reflection.FieldInfo fi in GetCachedFormFields(form))
                {
                    if (typeof(ToolTip).IsAssignableFrom(fi.FieldType))
                    {
                        ApplyThemeToToolTip(fi.GetValue(form) as ToolTip, colors);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }
        }

        private static void ThemedToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            try
            {
                ToolTip tt = sender as ToolTip;
                var back = CurrentColors()["ControlBack"];
                var fore = CurrentColors()["Fore"];
                if (tt is not null)
                {
                    try
                    {
                        back = tt.BackColor;
                        fore = tt.ForeColor;
                    }
                    catch
                    {
                    }
                }
                using (var br = new SolidBrush(back))
                {
                    e.Graphics.FillRectangle(br, e.Bounds);
                }
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, e.Bounds, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak);
                using (var pen = new Pen(CurrentColors()["Border"]))
                {
                    e.Graphics.DrawRectangle(pen, new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1));
                }
            }
            catch
            {
                try
                {
                    e.DrawBackground();
                    e.DrawText();
                    e.DrawBorder();
                }
                catch
                {
                }
            }
        }

        private static void ApplyThemeToToolStrip(ToolStrip ts, Dictionary<string, Color> colors)
        {
            var form = ts.FindForm();
            if (form is not null)
                ts.Font = form.Font;
            ts.BackColor = colors["ToolBack"];
            ts.ForeColor = colors["Fore"];
            ts.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            foreach (ToolStripItem item in ts.Items)
                ApplyThemeToToolStripItem(item, colors);
        }

        private static void ApplyThemeRecursive(Control control, Dictionary<string, Color> colors, ResolvedTheme theme)
        {

            // ObjectListView (BrightIdeasSoftware.TreeListView) - 主窗口的下载列表
            if (control is BrightIdeasSoftware.TreeListView)
            {
                ApplyThemeToObjectListView((BrightIdeasSoftware.TreeListView)control, colors, theme);
                goto RecurseChildren;
            }

            // ObjectListView 基类(ObjectListView, FastObjectListView 等)
            if (control is BrightIdeasSoftware.ObjectListView)
            {
                ApplyThemeToObjectListView((BrightIdeasSoftware.ObjectListView)control, colors, theme);
                goto RecurseChildren;
            }

            // Form
            if (control is Form)
            {
                ((Form)control).BackColor = colors["Back"];
                ((Form)control).ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // GroupBox: Flat 避免系统 3D 浅色边框
            if (control is GroupBox)
            {
                GroupBox gb = (GroupBox)control;
                gb.BackColor = colors["Back"];
                gb.ForeColor = colors["Fore"];
                gb.FlatStyle = FlatStyle.Flat;
                goto RecurseChildren;
            }

            // 容器控件
            if (control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel || control is SplitContainer || control is SplitterPanel)
            {
                control.BackColor = colors["ControlBack"];
                control.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // Button: Flat + 主题边框,避免 Visual Styles 的 3D 白边
            if (control is Button)
            {
                ApplyThemeToButton((Button)control, colors);
                goto RecurseChildren;
            }

            // Label / LinkLabel
            if (control is LinkLabel)
            {
                control.BackColor = Color.Transparent;
                control.ForeColor = colors["Fore"];
                ((LinkLabel)control).LinkColor = colors["Link"];
                ((LinkLabel)control).VisitedLinkColor = colors["Link"];
                ((LinkLabel)control).ActiveLinkColor = colors["Link"];
                ((LinkLabel)control).DisabledLinkColor = colors["Muted"];
                goto RecurseChildren;
            }
            if (control is Label)
            {
                control.BackColor = Color.Transparent;
                control.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // Native DropDownList painting can use a light system background in dark mode.
            // Draw the item with theme colors, including the closed selected value.
            if (control is ComboBox)
            {
                ComboBox combo = (ComboBox)control;
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = colors["Back"];
                combo.ForeColor = colors["Fore"];
                if (combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.DrawMode == DrawMode.Normal)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += DrawThemedComboItem;
                }
                combo.Invalidate();
                goto RecurseChildren;
            }

            // 文本类控件
            if (control is TextBox || control is RichTextBox || control is NumericUpDown || control is MaskedTextBox)
            {
                control.BackColor = colors["Back"];
                control.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // CheckBox / RadioButton
            if (control is CheckBox || control is RadioButton)
            {
                control.BackColor = Color.Transparent;
                control.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // TabControl / TabPage
            if (control is TabControl)
            {
                TabControl tc = (TabControl)control;
                tc.BackColor = colors["Back"];
                tc.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }
            if (control is TabPage)
            {
                TabPage tp = (TabPage)control;
                tp.BackColor = colors["Back"];
                tp.ForeColor = colors["Fore"];
                tp.UseVisualStyleBackColor = false;
                goto RecurseChildren;
            }

            // DataGridView
            if (control is DataGridView)
            {
                DataGridView dgv = (DataGridView)control;
                dgv.BackgroundColor = colors["AltBack"];
                dgv.DefaultCellStyle.BackColor = colors["Back"];
                dgv.DefaultCellStyle.ForeColor = colors["Fore"];
                dgv.DefaultCellStyle.SelectionBackColor = colors["Selection"];
                dgv.DefaultCellStyle.SelectionForeColor = colors["SelectionFore"];
                dgv.AlternatingRowsDefaultCellStyle.BackColor = colors["AltBack"];
                dgv.AlternatingRowsDefaultCellStyle.ForeColor = colors["Fore"];
                dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = colors["Selection"];
                dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = colors["SelectionFore"];
                dgv.ColumnHeadersDefaultCellStyle.BackColor = colors["ControlBack"];
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = colors["Fore"];
                dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = colors["ControlBack"];
                dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = colors["Fore"];
                dgv.RowHeadersDefaultCellStyle.BackColor = colors["ControlBack"];
                dgv.RowHeadersDefaultCellStyle.ForeColor = colors["Fore"];
                dgv.GridColor = colors["Border"];
                dgv.EnableHeadersVisualStyles = false;
                goto RecurseChildren;
            }

            // ListView (原生,非 OLV)
            if (control is ListView)
            {
                ListView lv = (ListView)control;
                lv.BackColor = colors["Back"];
                lv.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // TreeView
            if (control is TreeView)
            {
                TreeView tv = (TreeView)control;
                tv.BackColor = colors["Back"];
                tv.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // ListBox / CheckedListBox (RC:此前漏分支,靠各窗体手写补丁)
            if (control is ListBox)
            {
                ListBox lb = (ListBox)control;
                lb.BackColor = colors["Back"];
                lb.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }
            if (control is CheckedListBox)
            {
                CheckedListBox clb = (CheckedListBox)control;
                clb.BackColor = colors["Back"];
                clb.ForeColor = colors["Fore"];
                goto RecurseChildren;
            }

            // PictureBox - 透明背景
            if (control is PictureBox)
            {
                ((PictureBox)control).BackColor = Color.Transparent;
                goto RecurseChildren;
            }

            // ProgressBar
            if (control is ProgressBar)
            {
                ProgressBar pb = (ProgressBar)control;
                pb.BackColor = colors["ControlBack"];
                pb.ForeColor = colors["Selection"];
                goto RecurseChildren;
            }

            // ToolStrip 系列(MenuStrip/StatusStrip/ToolStrip - 不含 ContextMenuStrip,它不是 Control)
            if (control is ToolStrip)
            {
                ApplyThemeToToolStrip((ToolStrip)control, colors);
                return; // ToolStrip 的 Items 不是 Controls.Controls,不要走默认递归
            }

            // 其他类型:若是容器,套用背景;否则保留默认
            if (control.HasChildren)
            {
                control.BackColor = colors["Back"];
                control.ForeColor = colors["Fore"];
            }

        RecurseChildren:
            ;

            UiStyles.Apply(control);
            foreach (Control child in control.Controls)
                ApplyThemeRecursive(child, colors, theme);
        }

        private static void ApplyThemeToObjectListView(BrightIdeasSoftware.ObjectListView olv, Dictionary<string, Color> colors, ResolvedTheme theme)
        {
            olv.HandleCreated -= ThemedList_HandleCreated;
            olv.HandleCreated += ThemedList_HandleCreated;
            // 关键:关闭 Explorer 主题,否则自定义颜色不生效
            try
            {
                olv.UseExplorerTheme = false;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }
            try
            {
                olv.UseTranslucentSelection = false;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }

            olv.BackColor = colors["Back"];
            olv.ForeColor = colors["Fore"];
            olv.UseAlternatingBackColors = true;
            BrightIdeasSoftware.TextOverlay emptyOverlay = olv.EmptyListMsgOverlay as BrightIdeasSoftware.TextOverlay;
            if (emptyOverlay is not null)
            {
                emptyOverlay.BackColor = colors["Back"];
                emptyOverlay.TextColor = colors["Muted"];
                emptyOverlay.BorderColor = colors["Border"];
                emptyOverlay.BorderWidth = 0f;
            }
            try
            {
                olv.AlternateRowBackColor = colors["AltBack"];
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }

            // 选中行的颜色(失焦/聚焦)- 通过反射访问,因不同 OLV 版本属性名不同
            TrySetProperty(olv, "SelectedBackColor", colors["Selection"]);
            TrySetProperty(olv, "SelectedForeColor", colors["SelectionFore"]);
            TrySetProperty(olv, "UnfocusedSelectedBackColor", colors["Selection"]);
            TrySetProperty(olv, "UnfocusedSelectedForeColor", colors["SelectionFore"]);
            TrySetProperty(olv, "UseHotControls", false);

            // 表头样式
            try
            {
                var headerStyle = new BrightIdeasSoftware.HeaderFormatStyle();
                headerStyle.Normal.BackColor = colors["ControlBack"];
                headerStyle.Normal.ForeColor = colors["Fore"];
                headerStyle.Normal.FrameColor = colors["Border"];
                headerStyle.Hot.BackColor = colors["AltBack"];
                headerStyle.Hot.ForeColor = colors["Fore"];
                headerStyle.Hot.FrameColor = colors["Border"];
                headerStyle.Pressed.BackColor = colors["ControlBack"];
                headerStyle.Pressed.ForeColor = colors["Fore"];
                headerStyle.Pressed.FrameColor = colors["Border"];
                olv.HeaderFormatStyle = headerStyle;
                olv.HeaderUsesThemes = false;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }

            // TreeColumnRenderer (树列的展开/折叠图标所在列) - 仅 TreeListView 有此属性
            try
            {
                var renderer = olv.GetType().GetProperty("TreeColumnRenderer").GetValue(olv, null);
                if (renderer is not null)
                {
                    TrySetProperty(renderer, "IsShowLines", false);
                    TrySetProperty(renderer, "UseTriangles", true);
                    TrySetProperty(renderer, "BranchColor", colors["Border"]);
                    TrySetProperty(renderer, "LineColor", colors["Border"]);
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }
            // Reuse the virtual model; discard only cached row formatting from the prior palette.
            BrightIdeasSoftware.VirtualObjectListView virtualList = olv as BrightIdeasSoftware.VirtualObjectListView;
            if (virtualList is not null)
                virtualList.ClearCachedInfo();
            SyncNativeListColors(olv);
            olv.Invalidate();
        }

        internal static void RefreshListTheme(BrightIdeasSoftware.ObjectListView list)
        {
            if (list is null || list.IsDisposed)
                return;
            ApplyThemeToObjectListView(list, CurrentColors(), _current);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        private static extern IntPtr SendListViewMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static void SyncNativeListColors(ListView list)
        {
            if (list.IsDisposed || !list.IsHandleCreated || list.InvokeRequired)
                return;
            // ListView's native colors can diverge from its managed properties after handle recreation.
            // LVM_SETBKCOLOR, LVM_SETTEXTCOLOR and LVM_SETTEXTBKCOLOR also cover unused row space.
            var back = new IntPtr(ColorTranslator.ToWin32(list.BackColor));
            var fore = new IntPtr(ColorTranslator.ToWin32(list.ForeColor));
            SendListViewMessage(list.Handle, 0x1001, IntPtr.Zero, back);
            SendListViewMessage(list.Handle, 0x1024, IntPtr.Zero, fore);
            SendListViewMessage(list.Handle, 0x1026, IntPtr.Zero, back);
        }

        private static void ThemedList_HandleCreated(object sender, EventArgs e)
        {
            ListView list = (ListView)sender;
            SyncNativeListColors(list);
            try
            {
                // ObjectListView applies its own native styles after the base HandleCreated event.
                list.BeginInvoke(new MethodInvoker(() =>
                    {
                        if (list.IsDisposed || !list.IsHandleCreated)
                            return;
                        SyncNativeListColors(list);
                        list.Invalidate();
                    }));
            }
            catch (InvalidOperationException ex)
            {
                // The window may be closing while the deferred update is queued.
            }
        }

        private static void TrySetProperty(object target, string propertyName, object value)
        {
            try
            {
                var prop = target.GetType().GetProperty(propertyName);
                if (prop is not null && prop.CanWrite)
                {
                    prop.SetValue(target, value, null);
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ThemeManager reflection/theme apply failed once: " + Log.SafeException(ex));
            }
        }

        private static void DrawThemedComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            var background = GetColor(selected ? "Selection" : "Back");
            var foreground = GetColor(!combo.Enabled ? "Muted" : selected ? "SelectionFore" : "Fore");
            using (var brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            if (e.Index >= 0 && e.Index < combo.Items.Count)
            {
                TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items[e.Index]), e.Font, e.Bounds, foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if ((e.State & DrawItemState.Focus) != 0)
                e.DrawFocusRectangle();
        }

        private static void ApplyThemeToButton(Button btn, Dictionary<string, Color> colors)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = colors["ButtonBack"];
            btn.ForeColor = colors["Fore"];
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = colors["ButtonBorder"];
            btn.FlatAppearance.MouseOverBackColor = colors["ButtonHover"];
            btn.FlatAppearance.MouseDownBackColor = colors["ButtonPressed"];
            // 有图标的工具栏按钮禁用时保持边框可见
            btn.FlatAppearance.CheckedBackColor = colors["Selection"];
            btn.Paint -= DrawDisabledButton;
            if (!(btn is CommandButton))
                btn.Paint += DrawDisabledButton;
        }

        private static void DrawDisabledButton(object sender, PaintEventArgs e)
        {
            Button btn = (Button)sender;
            // Native disabled text is nearly black on dark surfaces. Preserve native
            // image layout for image buttons; text-only buttons get legible theme ink.
            if (btn.Enabled || btn.Image is not null || btn.ClientSize.Width < 2 || btn.ClientSize.Height < 2)
                return;
            var bounds = btn.ClientRectangle;
            using (var brush = new SolidBrush(GetColor("ButtonDisabledBack")))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }
            using (var pen = new Pen(GetColor("ButtonDisabledBorder")))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }
            var flags = btn.Text.Contains(Constants.vbLf) ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            switch (btn.TextAlign)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.MiddleLeft:
                case ContentAlignment.BottomLeft:
                    flags = flags | TextFormatFlags.Left;
                    break;
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    flags = flags | TextFormatFlags.Right;
                    break;
                default:
                    flags = flags | TextFormatFlags.HorizontalCenter;
                    break;
            }
            switch (btn.TextAlign)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.TopCenter:
                case ContentAlignment.TopRight:
                    flags = flags | TextFormatFlags.Top;
                    break;
                case ContentAlignment.BottomLeft:
                case ContentAlignment.BottomCenter:
                case ContentAlignment.BottomRight:
                    flags = flags | TextFormatFlags.Bottom;
                    break;
                default:
                    flags = flags | TextFormatFlags.VerticalCenter;
                    break;
            }
            if (!btn.UseMnemonic)
                flags = flags | TextFormatFlags.NoPrefix;
            if (btn.RightToLeft == RightToLeft.Yes)
                flags = flags | TextFormatFlags.RightToLeft;
            bounds = new Rectangle(2 + btn.Padding.Left, 1 + btn.Padding.Top, Math.Max(0, bounds.Width - 4 - btn.Padding.Horizontal), Math.Max(0, bounds.Height - 2 - btn.Padding.Vertical));
            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, bounds, GetColor("ButtonDisabledFore"), flags);
        }

        private static void ApplyThemeToToolStripItem(ToolStripItem item, Dictionary<string, Color> colors)
        {
            item.BackColor = colors["ToolBack"];
            item.ForeColor = colors["Fore"];

            if (item is ToolStripDropDownItem)
            {
                ToolStripDropDownItem dropDown = (ToolStripDropDownItem)item;
                if (dropDown.HasDropDownItems)
                    ApplyThemeToToolStrip(dropDown.DropDown, colors);
            }
            else if (item is ToolStripControlHost)
            {
                ApplyThemeRecursive(((ToolStripControlHost)item).Control, colors, _current);
            }
            else if (item is ToolStripLabel)
            {
                ((ToolStripLabel)item).BackColor = colors["ToolBack"];
            }
        }

        public static ResolvedTheme Current
        {
            get
            {
                return _current;
            }
        }

    }

    /// <summary>
/// 自定义 ToolStrip 配色表,使 MenuStrip/StatusStrip/ContextMenuStrip 跟随深/浅色主题。
/// </summary>
    internal class ThemeColorTable : ProfessionalColorTable
    {

        private readonly Dictionary<string, Color> _colors;

        public ThemeColorTable(Dictionary<string, Color> colors)
        {
            _colors = colors;
        }

        public override Color ToolStripBorder
        {
            get
            {
                return _colors["ToolBorder"];
            }
        }

        public override Color ToolStripGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ToolStripGradientMiddle
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ToolStripGradientEnd
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ToolStripDropDownBackground
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color MenuStripGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color MenuStripGradientEnd
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color MenuItemBorder
        {
            get
            {
                return _colors["Border"];
            }
        }

        public override Color MenuBorder
        {
            get
            {
                return _colors["Border"];
            }
        }

        public override Color MenuItemSelected
        {
            get
            {
                return _colors["AltBack"];
            }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get
            {
                return _colors["AltBack"];
            }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get
            {
                return _colors["AltBack"];
            }
        }

        public override Color MenuItemPressedGradientBegin
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color MenuItemPressedGradientMiddle
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color MenuItemPressedGradientEnd
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color ButtonSelectedHighlight
        {
            get
            {
                return _colors["AltBack"];
            }
        }

        public override Color ButtonSelectedGradientBegin
        {
            get
            {
                return _colors["AltBack"];
            }
        }

        public override Color ButtonSelectedGradientEnd
        {
            get
            {
                return _colors["AltBack"];
            }
        }

        public override Color ButtonPressedHighlight
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color ButtonPressedGradientBegin
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color ButtonPressedGradientEnd
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color ButtonCheckedHighlight
        {
            get
            {
                return _colors["Selection"];
            }
        }

        public override Color CheckBackground
        {
            get
            {
                return _colors["Selection"];
            }
        }

        public override Color CheckSelectedBackground
        {
            get
            {
                return _colors["Selection"];
            }
        }

        public override Color CheckPressedBackground
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color SeparatorDark
        {
            get
            {
                return _colors["Border"];
            }
        }

        public override Color SeparatorLight
        {
            get
            {
                return _colors["Border"];
            }
        }

        public override Color StatusStripGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color StatusStripGradientEnd
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ImageMarginGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ImageMarginGradientMiddle
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ImageMarginGradientEnd
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ImageMarginRevealedGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ImageMarginRevealedGradientMiddle
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color ImageMarginRevealedGradientEnd
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color RaftingContainerGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color RaftingContainerGradientEnd
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color OverflowButtonGradientBegin
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color OverflowButtonGradientMiddle
        {
            get
            {
                return _colors["ToolBack"];
            }
        }

        public override Color OverflowButtonGradientEnd
        {
            get
            {
                return _colors["ControlBack"];
            }
        }

        public override Color GripDark
        {
            get
            {
                return _colors["Border"];
            }
        }

        public override Color GripLight
        {
            get
            {
                return _colors["Border"];
            }
        }

    }
}