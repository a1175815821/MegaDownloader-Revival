using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace MegaDownloader
{

    // Semantic roles survive theme changes without occupying application control metadata.
    internal sealed class UiStyles
    {
        // Link input accepts text only, so clipboard fonts/colors cannot override the theme.
        internal sealed class PlainTextRichTextBox : RichTextBox
        {

            private const int WM_PASTE = 0x302;

            internal PlainTextRichTextBox()
            {
                DetectUrls = false;
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (ShortcutsEnabled && (keyData == (Keys.Control | Keys.V) || keyData == (Keys.Control | Keys.Shift | Keys.V) || keyData == (Keys.Shift | Keys.Insert)))
                {
                    PastePlainText();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_PASTE)
                {
                    PastePlainText();
                    m.Result = IntPtr.Zero;
                    return;
                }
                base.WndProc(ref m);
            }

            private void PastePlainText()
            {
                if (ReadOnly || !Enabled)
                    return;
                var textFormat = DataFormats.GetFormat(DataFormats.UnicodeText);
                if (CanPaste(textFormat))
                    Paste(textFormat);
            }
        }

        internal sealed class BufferedGrid : DataGridView
        {

            internal BufferedGrid()
            {
                DoubleBuffered = !SystemInformation.TerminalServerSession;
            }
        }

        internal enum Role
        {
            Surface,
            Canvas,
            Sidebar,
            InputSurface,
            Muted,
            PrimaryButton,
            SecondaryButton,
            QuietButton,
            BodyText,
            ErrorText
        }

        private sealed class Registration
        {
            internal Role Value;
        }

        private static readonly ConditionalWeakTable<Control, Registration> roles = new ConditionalWeakTable<Control, Registration>();

        private UiStyles()
        {
        }

        internal static void SetRole(Control control, Role value)
        {
            roles.GetValue(control, key => new Registration()).Value = value;
            Apply(control);
        }

        internal static void Apply(Control control)
        {
            Registration registration = null;
            if (!roles.TryGetValue(control, out registration))
                return;
            switch (registration.Value)
            {
                case Role.Surface:
                    control.BackColor = ThemeManager.GetColor("Back");
                    control.ForeColor = ThemeManager.GetColor("Fore");
                    break;
                case Role.Canvas:
                    control.BackColor = ThemeManager.GetColor("Canvas");
                    control.ForeColor = ThemeManager.GetColor("Fore");
                    break;
                case Role.Sidebar:
                    control.BackColor = ThemeManager.GetColor("ControlBack");
                    control.ForeColor = ThemeManager.GetColor("Fore");
                    break;
                case Role.InputSurface:
                    control.BackColor = ThemeManager.GetColor("InputBack");
                    control.ForeColor = ThemeManager.GetColor("Fore");
                    break;
                case Role.BodyText:
                    control.ForeColor = ThemeManager.GetColor("Fore");
                    break;
                case Role.ErrorText:
                    control.ForeColor = ThemeManager.GetColor("ErrorFore");
                    break;
                case Role.Muted:
                    control.ForeColor = ThemeManager.GetColor("Muted");
                    break;
                case Role.PrimaryButton:
                case Role.SecondaryButton:
                case Role.QuietButton:
                    {
                        Button button = control as Button;
                        if (button is null)
                            return;
                        bool primary = registration.Value == Role.PrimaryButton;
                        bool quiet = registration.Value == Role.QuietButton;
                        button.UseVisualStyleBackColor = false;
                        button.FlatStyle = FlatStyle.Flat;
                        button.BackColor = quiet && button.Parent is not null ? button.Parent.BackColor : ThemeManager.GetColor(primary ? "Accent" : "ButtonBack");
                        button.ForeColor = ThemeManager.GetColor(primary ? "AccentFore" : "Fore");
                        button.FlatAppearance.BorderSize = primary || quiet ? 0 : 1;
                        button.FlatAppearance.BorderColor = ThemeManager.GetColor("ButtonBorder");
                        button.FlatAppearance.MouseOverBackColor = ThemeManager.GetColor(primary ? "AccentHover" : "ButtonHover");
                        button.FlatAppearance.MouseDownBackColor = ThemeManager.GetColor(primary ? "AccentPressed" : "ButtonPressed");
                        button.Invalidate();
                        break;
                    }
            }
        }

        internal static Role GetRole(Control control, Role fallback = Role.SecondaryButton)
        {
            Registration registration = null;
            return roles.TryGetValue(control, out registration) ? registration.Value : fallback;
        }

        internal static int Px(Control control, int value)
        {
            return (int)Math.Round(Math.Round(value * Math.Max(96, control.DeviceDpi) / 96.0d));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string text);

        internal static void SetSearchCue(TextBox input)
        {
            if (input.IsHandleCreated)
                SendMessage(input.Handle, 0x1501, IntPtr.Zero, input.AccessibleName);
        }

        internal static List<Control> SuspendLayouts(Control root)
        {
            var controls = new List<Control>();
            SuspendLayoutsRecursive(root, controls);
            return controls;
        }

        private static void SuspendLayoutsRecursive(Control control, List<Control> controls)
        {
            control.SuspendLayout();
            controls.Add(control);
            foreach (Control child in control.Controls)
                SuspendLayoutsRecursive(child, controls);
        }

        internal static void ResumeLayouts(List<Control> controls)
        {
            // P-19: ResumeLayout(False) already restores layout state bottom-up.
            // The old second pass called PerformLayout() on EVERY control in the tree
            // (267 layout calls when the settings window opens), each of which walks
            // that control's children again. One forced layout on the root is enough
            // to materialise the final geometry.
            for (int index = controls.Count - 1; index >= 0; index -= 1)
                controls[index].ResumeLayout(false);
            // Final review: a single PerformLayout on the root does not reach the
            // custom settings containers. SettingsStackPanel.OnLayout sizes each
            // child from the current width (long translations / high DPI need a
            // re-measure) and does not call MyBase, while SettingsSection.OnLayout
            // only calls body.SetBounds. Restrict the extra passes to those few
            // containers (bottom-up), not the whole tree, so the 267->62 saving
            // is largely kept. Re-layout a section's body after its SetBounds.
            for (int index = controls.Count - 1; index >= 0; index -= 1)
            {
                var current = controls[index];
                if (current is SettingsSection)
                {
                    current.PerformLayout();
                    if (current.Controls.Count > 0)
                        current.Controls[0].PerformLayout();
                }
                else if (current is SettingsStackPanel || current is SettingsFieldRow)
                {
                    current.PerformLayout();
                }
            }
            if (controls.Count > 0)
                controls[0].PerformLayout();
        }

        // Keep the designer geometry in logical pixels. Font selection is completed
        // before the first handle/layout, so font metrics and DPI never both scale it.
        internal static void InitializeForm(Form form)
        {
            form.AutoScaleMode = AutoScaleMode.None;
            SetAppFont(form);
            form.AutoScaleDimensions = new SizeF(96f, 96f);
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }

        // Each font has a single control owner and is disposed with that control.
        internal static void SetFont(Control control, float extraPoints, bool bold = false, Font baseFont = null)
        {
            var basis = baseFont ?? control.Font;
            var owned = new Font(basis.FontFamily, basis.SizeInPoints + extraPoints, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            control.Font = owned;
            control.Disposed += (s, e) => owned.Dispose();
        }

        internal static void SetAppFont(Control control, float points = 10.0f)
        {
            string code = Language.GetCurrentLanguageCode();
            if (string.IsNullOrEmpty(code))
                code = System.Globalization.CultureInfo.CurrentUICulture.Name;
            string[] candidates = new[] { "Segoe UI" };
            if (code.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) || code.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase))
            {
                candidates = new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Segoe UI" };
            }
            else if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                candidates = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            }
            else if (code.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                candidates = new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" };
            }
            else if (code.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            {
                candidates = new[] { "Malgun Gothic", "Segoe UI" };
            }
            string family = SystemFonts.MessageBoxFont.FontFamily.Name;
            foreach (string candidate in candidates)
            {
                using (var probe = new Font(candidate, 10.0f))
                {
                    if (string.Equals(probe.FontFamily.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        family = candidate;
                        break;
                    }
                }
            }
            var owned = new Font(family, points, FontStyle.Regular, GraphicsUnit.Point);
            control.Font = owned;
            control.Disposed += (s, e) => owned.Dispose();
        }

        internal static void SetText(Control control, string value)
        {
            if ((control.Text ?? "") != (value ?? ""))
                control.Text = value;
        }
    }
}