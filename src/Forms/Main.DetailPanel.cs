using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic;

namespace MegaDownloader
{
    /// <summary>
/// 主窗口详情栏：运行时构建，字段分段显示，布局缓存与定时数值刷新分离。
/// </summary>
    public partial class Main
    {

        #region v2.5: 右侧栏结构化详情

        // 结构化详情:标签列 + 值列,进度单独一条细进度条。替代原先"一个 Label 灌多行文本"
        // 的写法——长文件名/长路径无法换行对齐,标签与值也没有视觉区分。
        // 无选中时不建/不显示这些控件,仍由 detailLabel 承担空态引导(B 段)。
        private Panel detailContentPanel = default;
        private Label detailTitleLabel = default;
        private List<KeyValuePair<Label, Label>> detailRows = new List<KeyValuePair<Label, Label>>();
        private ProgressBar detailProgressBar = default;
        private System.Drawing.Size detailLayoutSize;
        private string detailLayoutCaptionKey = null;

        /// <summary>按需构建结构化详情面板(挂在 detailGroup 内,与空态 detailLabel 互斥显示)。
    /// 行数固定为 状态/进度/大小/速度/剩余/路径,内容在 UpdateDetailContent 里填。</summary>
        private void EnsureDetailContentPanel()
        {
            if (detailContentPanel is not null && !detailContentPanel.IsDisposed)
                return;
            if (detailGroup is null)
                return;

            detailContentPanel = new Panel();
            detailContentPanel.Name = "detailContentPanel";
            detailContentPanel.Dock = DockStyle.Fill;
            detailContentPanel.AutoScroll = true;
            UiStyles.SetRole(detailContentPanel, UiStyles.Role.Surface);
            detailContentPanel.Visible = false;

            detailTitleLabel = new Label();
            detailTitleLabel.Name = "detailTitleLabel";
            detailTitleLabel.AutoSize = false;
            detailTitleLabel.AutoEllipsis = true;
            detailTitleLabel.Height = 36;
            // 标题加粗,只在此处建一次 Font(换肤不重建,避免 GDI 对象堆积)。
            UiStyles.SetFont(detailTitleLabel, 1, true, Font);

            detailContentPanel.Controls.Add(detailTitleLabel);

            detailRows.Clear();
            for (int i = 0; i <= 5; i++)
            {
                var cap = new Label();
                cap.Name = "detailCap" + i.ToString();
                cap.AutoSize = false;
                cap.Width = 48;
                cap.Height = 20;
                cap.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

                var val = new Label();
                val.Name = "detailVal" + i.ToString();
                val.AutoSize = false;
                val.Height = 20;
                val.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                val.AutoEllipsis = true;

                detailContentPanel.Controls.Add(cap);
                detailContentPanel.Controls.Add(val);
                detailRows.Add(new KeyValuePair<Label, Label>(cap, val));
            }

            detailProgressBar = new SlimProgressBar();
            detailProgressBar.Name = "detailProgressBar";
            detailProgressBar.Minimum = 0;
            detailProgressBar.Maximum = 100;
            detailProgressBar.Visible = false;
            detailProgressBar.Height = 10;
            detailContentPanel.Controls.Add(detailProgressBar);

            detailGroup.Controls.Add(detailContentPanel);
            detailContentPanel.BringToFront();
            // 运行时创建的详情面板同样接受链接文本拖放(与 detailLabel 一致)。
            EnableLinkDrop(detailContentPanel);
            LayoutDetailContent();
            ApplyDetailContentTheme();
        }

        /// <summary>详情面板内部几何。宽度按 detailGroup 客户区算,避免 DPI 变化后错位。</summary>
        private void LayoutDetailContent()
        {
            try
            {
                if (detailContentPanel is null || detailContentPanel.IsDisposed)
                    return;
                // 缓存键同时看尺寸与字段名:定时数值刷新不重排,切换语言(字段名变化)才重排。
                string captionKey = "";
                foreach (KeyValuePair<Label, Label> kv in detailRows)
                    captionKey += kv.Key.Text + "|";
                if (detailLayoutSize == detailContentPanel.ClientSize && (detailLayoutCaptionKey ?? "") == (captionKey ?? ""))
                    return;
                System.Drawing.Point previousScroll = detailContentPanel.AutoScrollPosition;
                detailContentPanel.SuspendLayout();
                try
                {
                    // Bounds are defined in unscrolled coordinates; retain the user's reading position.
                    detailContentPanel.AutoScrollPosition = System.Drawing.Point.Empty;
                    float sc = GetUiScale();
                    int pad = (int)Math.Round(12f * sc);
                    int w = detailContentPanel.ClientSize.Width - (detailContentPanel.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth);
                    int innerW = Math.Max(60, w - pad * 2);
                    int rowH = Math.Max(detailContentPanel.Font.Height + 4, (int)Math.Round(20f * sc));
                    // 标题单行加粗,不再占 56px 高,消除标题下方的大块留白。
                    int titleH = Math.Max(detailContentPanel.Font.Height + 8, (int)Math.Round(26f * sc));
                    detailTitleLabel.SetBounds(pad, pad, innerW, titleH);
                    // 标签列 + 值列同行:标签列宽按当前语言字段名实测(避免翻译裁切),值列占剩余宽度。
                    // 原先标签/值各占一行上下堆叠,6 个字段叠得很高,底部详情区放不下而出滚动条、显空。
                    int gap = (int)Math.Round(10f * sc);
                    int labelW = (int)Math.Round(48f * sc);
                    foreach (KeyValuePair<Label, Label> kv in detailRows)
                        labelW = Math.Max(labelW, System.Windows.Forms.TextRenderer.MeasureText(kv.Key.Text, kv.Key.Font).Width);
                    labelW = Math.Min(labelW + (int)Math.Round(4f * sc), (int)Math.Round(innerW * 0.45d));
                    int valW = Math.Max(40, innerW - labelW - gap);
                    int y = detailTitleLabel.Bottom + (int)Math.Round(8f * sc);
                    for (int i = 0, loopTo = detailRows.Count - 1; i <= loopTo; i++)
                    {
                        var cap = detailRows[i].Key;
                        var val = detailRows[i].Value;
                        int valH = i == 5 ? rowH * 2 : rowH;
                        cap.SetBounds(pad, y, labelW, rowH);
                        val.SetBounds(pad + labelW + gap, y, valW, valH);
                        y += valH + (int)Math.Round(4f * sc);
                        if (i == 1 && detailProgressBar is not null)
                        {
                            detailProgressBar.SetBounds(pad, y, innerW, (int)Math.Round(6f * sc));
                            detailProgressBar.Visible = true;
                            y += detailProgressBar.Height + (int)Math.Round(4f * sc);
                        }
                    }
                    detailContentPanel.AutoScrollMinSize = new System.Drawing.Size(0, y + pad);
                }
                finally
                {
                    detailContentPanel.ResumeLayout(true);
                    detailContentPanel.AutoScrollPosition = new System.Drawing.Point(-previousScroll.X, -previousScroll.Y);
                }
                detailLayoutSize = detailContentPanel.ClientSize;
                detailLayoutCaptionKey = captionKey;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("LayoutDetailContent failed: " + Log.SafeException(ex));
            }
        }

        private void ApplyDetailContentTheme()
        {
            try
            {
                if (detailContentPanel is null || detailContentPanel.IsDisposed)
                    return;
                // P2-8 同源问题:右栏详情左侧的"字段名"列也是次要说明文字,
                // 此前同样取 "Border" 当前景,挂在 detailContentPanel(ControlBack)上,
                // 对比度只有 1.38:1 / 1.82:1。改用 Muted 专用色。
                var subtle = ThemeManager.GetColor("Muted");
                var fore = ThemeManager.GetColor("Fore");
                if (detailTitleLabel is not null)
                {
                    detailTitleLabel.ForeColor = fore;
                }
                foreach (KeyValuePair<Label, Label> kv in detailRows)
                {
                    kv.Key.ForeColor = subtle;
                    kv.Value.ForeColor = fore;
                }
                if (detailProgressBar is not null)
                {
                    detailProgressBar.BackColor = ThemeManager.GetColor("ControlBack");
                    detailProgressBar.ForeColor = ThemeManager.GetColor("Selection");
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ApplyDetailContentTheme failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>把选中项的数据填进结构化详情。标签文案走语言系统,随语言切换同步。</summary>
        private void UpdateDetailContent(IDescarga ele)
        {
            try
            {
                EnsureDetailContentPanel();
                if (detailContentPanel is null)
                    return;

                UiStyles.SetText(detailTitleLabel, ele.DescargaNombre());
                ToolTipBotones.SetToolTip(detailTitleLabel, ele.DescargaNombre());

                var st = ele.DescargaEstado();
                decimal pct = ele.DescargaPorcentaje();
                long size = ele.DescargaTamanoBytes();
                string done = "-";
                if (size > 0L)
                {
                    done = PintarTamano(Math.Ceiling(pct * size / 100m)) + " / " + PintarTamano(size);
                }

                Fichero fic = ele as Fichero;
                string ruta = "-";
                if (fic is not null && !string.IsNullOrEmpty(fic.RutaRelativa))
                    ruta = fic.RutaRelativa;

                string[] captions = new string[] { Language.GetText("Status"), Language.GetText("Progress"), Language.GetText("Size"), Language.GetText("Speed"), Language.GetText("Remaining"), Language.GetText("Detail_Path") };
                // 空字符串统一显示为 "-",避免详情出现空白行(速度/剩余在未下载时本就无值)。
                string speedTxt = PintarVelocidadDescarga(ele);
                if (string.IsNullOrEmpty(speedTxt))
                    speedTxt = "-";
                string remainTxt = ele.DescargaTiempoEstimadoDescarga();
                if (string.IsNullOrEmpty(remainTxt))
                    remainTxt = "-";

                string[] values = new string[] { EstadoDisplayText(st), pct.ToString("F2") + "%", done, speedTxt, remainTxt, ruta };

                for (int i = 0, loopTo = Math.Min(captions.Length, detailRows.Count) - 1; i <= loopTo; i++)
                {
                    UiStyles.SetText(detailRows[i].Key, captions[i]);
                    if (detailRows[i].Value.Text != values[i])
                    {
                        detailRows[i].Value.Text = values[i];
                        if (i == 5)
                            ToolTipBotones.SetToolTip(detailRows[i].Value, values[i]);
                    }
                    // 失败态用语义色标注,与列表"状态"列的着色保持一致
                    if (i == 0 && st == Estado.Erroneo)
                    {
                        detailRows[i].Value.ForeColor = ThemeManager.GetColor("ErrorFore");
                    }
                    else if (i == 0 && st == Estado.Completado)
                    {
                        detailRows[i].Value.ForeColor = ThemeManager.GetColor("SuccessFore");
                    }
                    else
                    {
                        detailRows[i].Value.ForeColor = ThemeManager.GetColor("Fore");
                    }
                }

                if (detailProgressBar is not null)
                {
                    int p = (int)Math.Round(Math.Floor(pct));
                    if (p < 0)
                        p = 0;
                    if (p > 100)
                        p = 100;
                    try
                    {
                        if (detailProgressBar.Value != p)
                            detailProgressBar.Value = p;
                    }
                    catch
                    {
                    }
                }

                detailContentPanel.Visible = true;
                if (detailLabel is not null)
                    detailLabel.Visible = false;
                LayoutDetailContent();
            }
            catch (Exception ex)
            {
                Log.WriteDebug("UpdateDetailContent failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>回到空态:显示引导文案(Detail_Empty + Detail_EmptyHint),隐藏结构化面板。
    /// 主/副两行通过换行拼进同一个 detailLabel,不新增控件。</summary>
        private void ShowDetailEmptyState()
        {
            try
            {
                if (detailContentPanel is not null && !detailContentPanel.IsDisposed)
                {
                    detailContentPanel.Visible = false;
                }
                if (detailLabel is null || detailLabel.IsDisposed)
                    return;
                detailLabel.Visible = true;
                string main = Language.GetText("Detail_Empty");
                string hint = Language.GetText("Detail_EmptyHint");
                string text = main;
                if (!string.IsNullOrEmpty(hint) && !string.Equals(hint, "Detail_EmptyHint"))
                {
                    text = main + Constants.vbCrLf + Constants.vbCrLf + hint;
                }
                if ((detailLabel.Text ?? "") != (text ?? ""))
                    detailLabel.Text = text;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ShowDetailEmptyState failed: " + Log.SafeException(ex));
            }
        }

        #endregion

    }
}