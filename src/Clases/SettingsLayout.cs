using System;
using System.Drawing;
using System.Windows.Forms;

namespace MegaDownloader
{

    // Settings use a width-constrained, top-down layout. Stacking AutoSize
    // GroupBoxes and percentage TableLayoutPanels repeatedly measured the entire
    // descendant tree during each binding/text change and again on first show.
    internal class SettingsStackPanel : Panel
    {

        private bool layingOut;

        public SettingsStackPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            e.Control.SizeChanged += ChildLayoutChanged;
            e.Control.TextChanged += ChildLayoutChanged;
            e.Control.FontChanged += ChildLayoutChanged;
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            e.Control.SizeChanged -= ChildLayoutChanged;
            e.Control.TextChanged -= ChildLayoutChanged;
            e.Control.FontChanged -= ChildLayoutChanged;
            base.OnControlRemoved(e);
        }

        private void ChildLayoutChanged(object sender, EventArgs e)
        {
            if (!layingOut)
                PerformLayout();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
            int height = Padding.Vertical;
            foreach (Control child in Controls)
            {
                int available = Math.Max(1, width - Padding.Horizontal - child.Margin.Horizontal);
                height += child.GetPreferredSize(new Size(available, 0)).Height + child.Margin.Vertical;
            }
            return new Size(width, height);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (layingOut)
                return;
            layingOut = true;
            try
            {
                int top = Padding.Top;
                foreach (Control child in Controls)
                {
                    int available = Math.Max(1, ClientSize.Width - Padding.Horizontal - child.Margin.Horizontal);
                    if (child is Label && child.MaximumSize.Width != available)
                        child.MaximumSize = new Size(available, 0);
                    var wanted = child.GetPreferredSize(new Size(available, 0));
                    top += child.Margin.Top;
                    child.SetBounds(Padding.Left + child.Margin.Left, top, available, wanted.Height);
                    top += wanted.Height + child.Margin.Bottom;
                }
                int wantedHeight = top + Padding.Bottom;
                if (Height != wantedHeight)
                    Height = wantedHeight;
            }
            finally
            {
                layingOut = false;
            }
        }
    }

    internal class SettingsSection : SectionGroupBox
    {

        internal void SetBody(SettingsStackPanel body)
        {
            Controls.Add(body);
            body.SizeChanged += (s, e) => PerformLayout();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
            int height = Font.Height + Padding.Vertical;
            if (Controls.Count > 0)
                height += Controls[0].GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0)).Height;
            return new Size(width, height);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (Controls.Count == 0)
                return;
            var body = Controls[0];
            int available = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            int height = body.GetPreferredSize(new Size(available, 0)).Height;
            body.SetBounds(Padding.Left, Font.Height + Padding.Top, available, height);
            int wanted = Font.Height + Padding.Vertical + height;
            if (Height != wanted)
                Height = wanted;
        }
    }

    internal class SettingsFieldRow : Panel
    {

        private readonly Label caption;
        private readonly Control input;
        private readonly Control[] trailing;

        internal SettingsFieldRow(Label label, Control @field, Control[] extras)
        {
            caption = label;
            input = @field;
            trailing = extras;
            Controls.Add(caption);
            Controls.Add(input);
            Controls.AddRange(trailing);
            caption.AutoSize = false;
            caption.TextAlign = ContentAlignment.MiddleLeft;
            caption.MinimumSize = Size.Empty;
            caption.MaximumSize = Size.Empty;
            caption.TextChanged += PreferredSizeChanged;
            caption.FontChanged += PreferredSizeChanged;
            input.FontChanged += PreferredSizeChanged;
            // 翻译在 Load 时才写入控件 Text；末尾按钮的宽度必须跟随文本+字体重测，
            // 否则长文案（如各语言 Browse）会被裁或挤压 txtRuta。
            input.TextChanged += PreferredSizeChanged;
            input.Dock = DockStyle.None;
            input.MinimumSize = Size.Empty;
            foreach (Control control in trailing)
            {
                control.Dock = DockStyle.None;
                control.AutoSize = true;
                control.TextChanged += PreferredSizeChanged;
                control.FontChanged += PreferredSizeChanged;
            }
        }

        private void PreferredSizeChanged(object sender, EventArgs e)
        {
            if (Parent is not null)
                Parent.PerformLayout();
        }

        private int LabelWidth(int width)
        {
            return Math.Max(1, (int)Math.Round(width * 0.34d));
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
            var labelSize = TextRenderer.MeasureText(caption.Text, caption.Font, new Size(LabelWidth(width), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int height = Math.Max(labelSize.Height, input.GetPreferredSize(Size.Empty).Height);
            foreach (Control control in trailing)
                height = Math.Max(height, control.GetPreferredSize(Size.Empty).Height);
            return new Size(width, Math.Max(UiStyles.Px(this, 34), height + UiStyles.Px(this, 8)));
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (input is null)
                return;
            int gap = UiStyles.Px(this, 12);
            int left = LabelWidth(ClientSize.Width) + gap;
            int right = ClientSize.Width;
            for (int index = trailing.Length - 1; index >= 0; index -= 1)
            {
                var control = trailing[index];
                var wanted = control.GetPreferredSize(Size.Empty);
                wanted.Width = Math.Max(wanted.Width, UiStyles.Px(this, 92));
                wanted.Height = Math.Max(wanted.Height, UiStyles.Px(this, 28));
                right -= wanted.Width;
                control.SetBounds(right, Math.Max(0, (Height - wanted.Height) / 2), wanted.Width, wanted.Height);
                right -= gap;
            }
            caption.SetBounds(0, 0, Math.Max(1, left - gap), Height);
            int inputHeight = input.GetPreferredSize(Size.Empty).Height;
            inputHeight = Math.Max(inputHeight, UiStyles.Px(this, 28));
            input.SetBounds(left, Math.Max(0, (Height - inputHeight) / 2), Math.Max(1, right - left), inputHeight);
        }
    }

    // 设置分组内逻辑子段之间的细分割线。SectionGroupBox 只画标题下横线,子段之间原本
    // 直接堆叠留白、显得空而丑;用与标题横线同色(Border)的细线把子段分开,留白变得有结构。
    internal class SettingsDivider : Panel
    {

        public SettingsDivider()
        {
            DoubleBuffered = true;
            Margin = new Padding(0, 10, 0, 10);
            Height = 1;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size(Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width), 1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            using (var pen = new Pen(ThemeManager.GetColor("Border")))
            {
                int y = Height / 2;
                e.Graphics.DrawLine(pen, 0, y, Math.Max(0, Width - 1), y);
            }
        }
    }
}