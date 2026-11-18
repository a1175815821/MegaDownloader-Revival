using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MegaDownloader
{

    // These retain the native button/textbox behaviours (keyboard, accessibility,
    // IME, mnemonics and DialogResult). Only their presentation is custom drawn.
    internal class CommandButton : Button
    {

        private bool hovered;
        private bool pressed;

        public CommandButton()
        {
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            // 宽度必须只由内容决定:AutoSize 按钮的基类 GetPreferredSize 会回显当前宽度,
            // 若与当前值取 Max,任何一次瞬态布局把按钮拉宽后就永远缩不回来(设置页浏览按钮
            // 曾因此胀到 593px 压住输入框)。高度同理按字体+内边距计算。
            int textWidth = Text.Length == 0 ? 0 : TextRenderer.MeasureText(Text, Font).Width;
            int imageWidth = Image is null ? 0 : Image.Width;
            int gap = textWidth > 0 && imageWidth > 0 ? UiStyles.Px(this, 7) : 0;
            int contentWidth = textWidth + imageWidth + gap + Math.Max(Padding.Horizontal, UiStyles.Px(this, 20));
            int contentHeight = Math.Max(Image is null ? 0 : Image.Height, Font.Height) + Padding.Vertical + UiStyles.Px(this, 6);
            // 按 MinimumSize 钳制:AutoSize 实际渲染会取 max(内容, MinimumSize),preferred 若只报
            // 内容宽,RTL 流式面板 / TableLayoutPanel 的 AutoSize 列会按偏小值分配槽位,被 MinimumSize
            // 撑宽的按钮溢出邻位 → 相邻按钮重叠(添加链接/设置/预检/定时对话框曾重叠 43px)。
            // MinimumSize 是固定下限而非当前宽度,因此不会复发浏览按钮被瞬态布局拉宽后缩不回的膨胀。
            return new Size(Math.Max(contentWidth, MinimumSize.Width), Math.Max(Math.Max(contentHeight, MinimumSize.Height), UiStyles.Px(this, 28)));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            base.OnMouseLeave(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = e.Button == MouseButtons.Left;
            base.OnMouseDown(e);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            base.OnMouseUp(e);
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture)
                pressed = false;
            base.OnMouseCaptureChanged(e);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
                pressed = true;
            base.OnKeyDown(e);
            Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            pressed = false;
            base.OnKeyUp(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            pressed = false;
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
                return;
            var role = UiStyles.GetRole(this);
            bool primary = role == UiStyles.Role.PrimaryButton;
            bool quiet = role == UiStyles.Role.QuietButton;
            var parentBack = Parent is null ? ThemeManager.GetColor("Back") : Parent.BackColor;
            e.Graphics.Clear(parentBack);
            var fill = BackColor;
            var ink = ForeColor;
            if (!Enabled)
            {
                fill = quiet ? parentBack : ThemeManager.GetColor("ButtonDisabledBack");
                ink = ThemeManager.GetColor("ButtonDisabledFore");
            }
            else if (pressed && (hovered || Focused))
            {
                fill = ThemeManager.GetColor(primary ? "AccentPressed" : "ButtonPressed");
                // 换成主题底色就必须配套换成主题字色。自定义深底白字按钮(如配额横幅的
                // "立即重试")只换底色会变成白字白底:浅色主题下对比度仅 1.12:1,文字看不见。
                ink = ThemeManager.GetColor(primary ? "AccentFore" : "Fore");
            }
            else if (hovered)
            {
                fill = ThemeManager.GetColor(primary ? "AccentHover" : "ButtonHover");
                ink = ThemeManager.GetColor(primary ? "AccentFore" : "Fore");
            }
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Rounded(bounds, UiStyles.Px(this, 6)))
            {
                using (var brush = new SolidBrush(fill))
                {
                    e.Graphics.FillPath(brush, shape);
                }
                if (!quiet && !primary)
                {
                    using (var pen = new Pen(ThemeManager.GetColor(Enabled ? "ButtonBorder" : "ButtonDisabledBorder")))
                    {
                        e.Graphics.DrawPath(pen, shape);
                    }
                }
            }
            var content = Rectangle.Inflate(ClientRectangle, -UiStyles.Px(this, 10), -2);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (!UseMnemonic)
                flags = flags | TextFormatFlags.NoPrefix;
            int imageWidth = Image is null ? 0 : Image.Width;
            int gap = imageWidth > 0 && Text.Length > 0 ? UiStyles.Px(this, 7) : 0;
            int textWidth = Text.Length == 0 ? 0 : TextRenderer.MeasureText(Text, Font).Width;
            int groupWidth = Math.Min(content.Width, imageWidth + gap + textWidth);
            int left = TextAlign == ContentAlignment.MiddleLeft ? content.Left : content.Left + (content.Width - groupWidth) / 2;
            if (RightToLeft == RightToLeft.Yes)
                flags = flags | TextFormatFlags.RightToLeft;
            if (Image is not null)
            {
                int top = (Height - Image.Height) / 2;
                if (Enabled)
                {
                    e.Graphics.DrawImage(Image, left, top, Image.Width, Image.Height);
                }
                else
                {
                    ControlPaint.DrawImageDisabled(e.Graphics, Image, left, top, fill);
                }
                left += imageWidth + gap;
            }
            if (Text.Length > 0)
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(left, content.Top, Math.Max(0, content.Right - left), content.Height), ink, flags);
            }
            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ink, fill);
            }
        }

        internal static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }

    internal class NavigationTabs : TabControl
    {

        public NavigationTabs()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            ItemSize = new Size(0, 38);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ThemeManager.GetColor("Back"));
            for (int index = 0, loopTo = TabPages.Count - 1; index <= loopTo; index++)
            {
                var state = index == SelectedIndex ? DrawItemState.Selected : DrawItemState.None;
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(index), index, state));
            }
            using (var pen = new Pen(ThemeManager.GetColor("Border")))
            {
                e.Graphics.DrawLine(pen, 0, DisplayRectangle.Top - 1, Width, DisplayRectangle.Top - 1);
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            bool selected = e.Index == SelectedIndex;
            using (var brush = new SolidBrush(ThemeManager.GetColor("Back")))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds, ThemeManager.GetColor(selected ? "SelectionFore" : "Muted"), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (selected)
            {
                using (var brush = new SolidBrush(ThemeManager.GetColor("Accent")))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds.Left + 12, e.Bounds.Bottom - 3, Math.Max(1, e.Bounds.Width - 24), 3);
                }
            }
            if (Focused && ShowFocusCues && selected)
                e.DrawFocusRectangle();
        }
    }

    internal class SlimProgressBar : ProgressBar
    {

        public SlimProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ThemeManager.GetColor("ProgressBack"));
            double fraction = (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum);
            using (var brush = new SolidBrush(ThemeManager.GetColor("ProgressFill")))
            {
                e.Graphics.FillRectangle(brush, 0, 0, (int)Math.Round(ClientSize.Width * fraction), ClientSize.Height);
            }
        }
    }

    internal class SectionGroupBox : GroupBox
    {

        public SectionGroupBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int inset = UiStyles.Px(this, 12);
            var title = new Rectangle(inset, 0, Math.Max(0, Width - inset * 2), Font.Height + 8);
            TextRenderer.DrawText(e.Graphics, Text, Font, title, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(ThemeManager.GetColor("Border")))
            {
                e.Graphics.DrawLine(pen, inset, title.Bottom, Math.Max(inset, Width - inset), title.Bottom);
            }
        }
    }

    internal class SearchFrame : Panel
    {

        private readonly TextBox input;
        private readonly Button clearButton;

        internal SearchFrame(TextBox textBox, Button clear)
        {
            DoubleBuffered = true;
            input = textBox;
            clearButton = clear;
            input.BorderStyle = BorderStyle.None;
            Controls.AddRange(new Control[] { input, clearButton });
            input.GotFocus += (s, e) => Invalidate();
            input.LostFocus += (s, e) => Invalidate();
            input.TextChanged += (s, e) =>
                {
                    clearButton.Visible = input.TextLength > 0;
                    PerformLayout();
                };
            UiStyles.SetRole(this, UiStyles.Role.InputSurface);
            clearButton.Visible = false;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (input is null)
                return;
            int inset = UiStyles.Px(this, 10);
            int side = Math.Max(22, Height - 8);
            clearButton.SetBounds(Width - side - 4, 4, side, side);
            int right = clearButton.Visible ? clearButton.Left : Width - inset;
            input.SetBounds(UiStyles.Px(this, 32), Math.Max(2, (Height - input.PreferredHeight) / 2), Math.Max(8, right - UiStyles.Px(this, 32)), input.PreferredHeight);
            input.BackColor = BackColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = CommandButton.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), UiStyles.Px(this, 6)))
            {
                using (var pen = new Pen(ThemeManager.GetColor(input.Focused ? "Link" : "Border")))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
            int side = UiStyles.Px(this, 11);
            int x = UiStyles.Px(this, 11);
            int y = (Height - side) / 2 - 1;
            using (var pen = new Pen(ThemeManager.GetColor("Muted"), Math.Max(1.0f, DeviceDpi / 96.0f)))
            {
                e.Graphics.DrawEllipse(pen, x, y, side, side);
                e.Graphics.DrawLine(pen, x + side - 1, y + side - 1, x + side + 4, y + side + 4);
            }
        }
    }
}