using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace MegaDownloader
{

    /// <summary>
/// P0-4 UI:轻量 Toast 提示。None 边框 / TopMost / 不进任务栏,按文案长度停留 2~10 秒,点击即关。
/// 与 PantallaMsg 互不复用(后者是带文本框的错误窗,且被 ComprobarYAgregarLinks 的
/// formsDiscarded 判据依赖);本窗体已加入该判据名单,避免干扰剪贴板监控。
/// ShowToast 内部编组回 UI 线程,后台线程可直接调用,失败只记日志不抛错。
/// </summary>
    public class ToastForm : Form
    {

        private static ToastForm _current = null;

        private const int MinWidth = 300;
        private const int MaxWidth = 420;
        private const int MinHeight = 60;
        private const int MaxHeight = 240;

        private Timer __timer;

        private Timer _timer
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return __timer;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (__timer != null)
                {
                    __timer.Tick -= _timer_Tick;
                }

                __timer = value;
                if (__timer != null)
                {
                    __timer.Tick += _timer_Tick;
                }
            }
        }
        private Label _label;

        public ToastForm(string text)
        {
            _timer = new Timer();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(MinWidth, MinHeight);
            Padding = new Padding(1);
            BackColor = ThemeManager.GetColor("Border");
            ForeColor = ThemeManager.GetColor("Fore");

            _label = new Label();
            _label.Dock = DockStyle.Fill;
            _label.BackColor = ThemeManager.GetColor("AltBack");
            _label.ForeColor = ForeColor;
            _label.TextAlign = ContentAlignment.MiddleLeft;
            _label.Padding = new Padding(12, 6, 12, 6);
            _label.UseMnemonic = false;
            _label.AutoEllipsis = true;
            _label.Text = text;
            _label.Click += ToastForm_Click;
            Controls.Add(_label);
            Click += ToastForm_Click;

            FitToText();
        }

        // 固定 300x60 配 AutoEllipsis 会把长文案(Config_LoadRecovered 有 170+ 字符)截成
        // 省略号,2 秒也读不完。按实测文本尺寸自适应,停留时间随长度增长。
        private void FitToText()
        {
            string text = _label.Text + "";
            int chromeW = Padding.Horizontal + _label.Padding.Horizontal;
            int chromeH = Padding.Vertical + _label.Padding.Vertical;
            var measured = TextRenderer.MeasureText(text, _label.Font, new Size(MaxWidth - chromeW, 10000), TextFormatFlags.WordBreak);
            Size = new Size(Math.Max(MinWidth, measured.Width + chromeW), Math.Max(MinHeight, Math.Min(MaxHeight, measured.Height + chromeH)));
            _timer.Interval = Math.Min(10000, Math.Max(2000, 1000 + text.Length * 60));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        private void SetText(string text)
        {
            if (_label is not null && !_label.IsDisposed)
            {
                _label.Text = text;
                FitToText();
            }
            _timer.Stop();
            _timer.Start();
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            Close();
        }

        private void ToastForm_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_timer is not null)
                    {
                        _timer.Dispose();
                        _timer = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static void Current_FormClosed(object sender, FormClosedEventArgs e)
        {
            ToastForm f = sender as ToastForm;
            if (f is not null)
            {
                f.FormClosed -= Current_FormClosed;
            }
            _current = null;
        }

        // 定位 Toast:主窗右下,四边夹取在工作区内。新建/复用共用,行为一致。
        // 此前新建分支只夹左/上,主窗贴屏幕右/下边缘时首弹一半在屏外,复用反而对的。
        private static void PlaceToast(ToastForm f, Form owner)
        {
            try
            {
                var area = Screen.PrimaryScreen.WorkingArea;
                if (owner is not null && owner.Visible)
                {
                    area = Screen.FromControl(owner).WorkingArea;
                    f.Location = new Point(owner.Right - f.Width - 16, owner.Bottom - f.Height - 48);
                }
                else
                {
                    f.Location = new Point(area.Right - f.Width - 16, area.Bottom - f.Height - 16);
                }
                if (f.Left < area.Left)
                    f.Left = area.Left + 16;
                if (f.Top < area.Top)
                    f.Top = area.Top + 16;
                if (f.Right > area.Right)
                    f.Left = area.Right - f.Width - 16;
                if (f.Bottom > area.Bottom)
                    f.Top = area.Bottom - f.Height - 16;
            }
            catch
            {
            }
        }
        /// <summary>显示 Toast。若已有 Toast 打开则刷新文本并重计时,不会堆叠。</summary>
        public static void ShowToast(Form owner, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return;
                if (owner is not null && owner.InvokeRequired)
                {
                    owner.BeginInvoke(new Action<Form, string>(ShowToast), owner, text);
                    return;
                }
                if (_current is not null && !_current.IsDisposed)
                {
                    _current.SetText(text);
                    // RC:复用也必须重算位置(主窗跨屏移动/尺寸变化后旧坐标过期)。
                    PlaceToast(_current, owner);
                    if (!_current.Visible)
                    {
                        if (owner is not null && owner.Visible)
                        {
                            _current.Show(owner);
                        }
                        else
                        {
                            _current.Show();
                        }
                    }
                    return;
                }
                _current = new ToastForm(text);
                _current.FormClosed += Current_FormClosed;
                PlaceToast(_current, owner);
                // 有主窗时做 owned 显示:主窗最小化/关闭时 Toast 跟着走,不留孤儿窗体挡关机流程
                if (owner is not null && owner.Visible)
                {
                    _current.Show(owner);
                }
                else
                {
                    _current.Show();
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("ShowToast failed: " + ex.ToString());
            }
        }

    }
}