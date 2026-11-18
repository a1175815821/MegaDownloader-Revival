using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MegaDownloader
{

    // A 收口版:下载/解压失败的统一落点——持久化的失败标签页，带计数与一键重试。
    // 背景:此前只有配额横幅有用户可见的落点；下载/解压失败只进日志+红色态（列表行）。
    // 设计取舍:
    // - 不复用 Toast（Toast 是短命的 2-10s 提示，错误需要持久展示直至用户采取行动）。
    // - 失败 O(1) 快照由 Main 的 RefreshLista tick 认领：Fichero 只翻 Erroneo 态，
    // 横幅计数由 Main 轮询 ListaPaquetes，已有 quota 刷新协程同款编组：BeginInvoke 回 UI。
    // - 只有“首次发现”才弹托盘气球，避免 430ms 轮询的每次失效重复打扰；横幅本身常驻。
    public partial class Main
    {
        // 仅持有引用的  ID 单例； disposed guard 与 quotaBannerPanel 同款。
        private Panel failureBannerPanel = null;
        private Label failureBannerLabel = null;
        private Button failureRetryButton = null;
        private Button failureDismissButton = null;
        // Dismissed failure count: suppressed until a new failure (or a changed set) needs attention again.
        private int _failureBannerDismissedCount = 0;

        /// <summary>A 收口版：由 430ms UI 刷新 tick 认领 end-state 的 Erroneo 计数快照。
    /// 与 quota 同款：UI 线程常驻刷新 vs 线程池 worker 调用，故内部 BeginInvoke。 </summary>
        internal void RefreshFailureBanner()
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(RefreshFailureBanner));
                    return;
                }
                if (failureBannerPanel is null)
                    return;
                int count = 0;
                try
                {
                    if (ListaPaquetes is not null)
                    {
                        lock (Mutex.ListaDescargas)
                        {
                            foreach (Paquete paq in ListaPaquetes)
                            {
                                foreach (Fichero fic in paq.ListaFicheros)
                                {
                                    if (fic.DescargaEstado() == Estado.Erroneo)
                                        count += 1;
                                }
                            }
                        }
                    }
                }
                catch (ObjectDisposedException ex)
                {
                }
                if (count > 0)
                {
                    bool dismissed = count == _failureBannerDismissedCount;
                    if (!dismissed && !failureBannerPanel.Visible)
                    {
                        failureBannerPanel.Visible = true;
                        failureBannerPanel.BringToFront();
                        LayoutDownloadArea();
                    }
                    failureBannerLabel.Text = Language.GetText("Downloads_Failed_Banner").Replace("%N%", count.ToString());
                    if (dismissed && failureBannerPanel.Visible)
                    {
                        failureBannerPanel.Visible = false;
                        LayoutDownloadArea();
                    }
                }
                else if (failureBannerPanel.Visible)
                {
                    failureBannerPanel.Visible = false;
                    LayoutDownloadArea();
                }
            }
            catch
            {
            }
        }

        private void InitFailureBanner()
        {
            float sc = 1.0f;
            try
            {
                using (var g = CreateGraphics())
                {
                    sc = g.DpiY / 96.0f;
                }
            }
            catch
            {
            }
            if (sc < 1.0f)
                sc = 1.0f;
            int bannerH = (int)Math.Round(30f * sc);
            failureBannerPanel = new Panel();
            failureBannerPanel.Name = "failureBannerPanel";
            failureBannerPanel.Height = bannerH;
            failureBannerPanel.Left = 0;
            try
            {
                failureBannerPanel.Top = TableLayoutPanel1.Bottom + (quotaBannerPanel is not null && quotaBannerPanel.Visible ? quotaBannerPanel.Height : 0);
            }
            catch
            {
                failureBannerPanel.Top = 70;
            }
            failureBannerPanel.Width = ClientSize.Width;
            failureBannerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            failureBannerPanel.Visible = false;

            failureBannerLabel = new Label();
            failureBannerLabel.Name = "failureBannerLabel";
            failureBannerLabel.AutoSize = false;
            failureBannerLabel.Left = 12;
            failureBannerLabel.Top = 0;
            failureBannerLabel.Height = bannerH;
            failureBannerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            failureBannerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            failureRetryButton = new CommandButton();
            failureRetryButton.Name = "failureRetryButton";
            failureRetryButton.Height = (int)Math.Round(23f * sc);
            failureRetryButton.Width = (int)Math.Round(110f * sc);
            failureRetryButton.Top = (bannerH - failureRetryButton.Height) / 2;
            failureRetryButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            failureRetryButton.Click += FailureRetry_Click;

            failureDismissButton = new CommandButton();
            failureDismissButton.Name = "failureDismissButton";
            failureDismissButton.Text = "×";
            failureDismissButton.Height = (int)Math.Round(23f * sc);
            failureDismissButton.Width = (int)Math.Round(23f * sc);
            failureDismissButton.Top = (bannerH - failureDismissButton.Height) / 2;
            failureDismissButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            failureDismissButton.Click += (s, e) =>
                {
                    // “已看到”：记住当前失败数，下轮刷新不再弹出，
                    // 失败数变化（新增/减少）时重新提示。重试/删除仍是清零的途径。
                    int count = 0;
                    try
                    {
                        if (ListaPaquetes is not null)
                        {
                            lock (Mutex.ListaDescargas)
                            {
                                foreach (Paquete paq in ListaPaquetes)
                                {
                                    foreach (Fichero fic in paq.ListaFicheros)
                                    {
                                        if (fic.DescargaEstado() == Estado.Erroneo)
                                            count += 1;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                    _failureBannerDismissedCount = count;
                    failureBannerPanel.Visible = false;
                    LayoutDownloadArea();
                };

            failureBannerPanel.Controls.Add(failureBannerLabel);
            failureBannerPanel.Controls.Add(failureRetryButton);
            failureBannerPanel.Controls.Add(failureDismissButton);
            Controls.Add(failureBannerPanel);
            failureBannerPanel.BringToFront();
            LayoutFailureBanner();
            ApplyFailureBannerTheme();
            UpdateFailureBannerTexts();
        }

        private void LayoutFailureBanner()
        {
            if (failureBannerPanel is null || failureRetryButton is null || failureBannerLabel is null)
                return;
            try
            {
                int top = TableLayoutPanel1.Bottom;
                if (quotaBannerPanel is not null && quotaBannerPanel.Visible)
                    top += quotaBannerPanel.Height;
                failureBannerPanel.Top = top;
            }
            catch
            {
            }
            failureBannerPanel.Width = ClientSize.Width;
            failureDismissButton.Left = failureBannerPanel.Width - failureDismissButton.Width - 12;
            failureRetryButton.Left = failureDismissButton.Left - failureRetryButton.Width - 8;
            failureBannerLabel.Width = Math.Max(50, failureRetryButton.Left - 18);
        }

        private void ApplyFailureBannerTheme()
        {
            if (failureBannerPanel is null || failureBannerLabel is null)
                return;
            failureBannerPanel.BackColor = ThemeManager.GetColor("QuotaBack");
            failureBannerLabel.ForeColor = ThemeManager.GetColor("QuotaFore");
            try
            {
                failureRetryButton.BackColor = ThemeManager.GetColor("QuotaBack");
                failureRetryButton.ForeColor = ThemeManager.GetColor("QuotaFore");
                failureRetryButton.FlatStyle = FlatStyle.Flat;
                failureRetryButton.FlatAppearance.BorderColor = ThemeManager.GetColor("QuotaFore");
                failureDismissButton.BackColor = ThemeManager.GetColor("QuotaBack");
                failureDismissButton.ForeColor = ThemeManager.GetColor("QuotaFore");
                failureDismissButton.FlatStyle = FlatStyle.Flat;
                failureDismissButton.FlatAppearance.BorderColor = ThemeManager.GetColor("QuotaFore");
            }
            catch
            {
            }
        }

        private void UpdateFailureBannerTexts()
        {
            if (failureBannerLabel is null || failureRetryButton is null)
                return;
            failureRetryButton.Text = Language.GetText("Quota_RetryNow");
            if (string.IsNullOrEmpty(failureRetryButton.Text))
                failureRetryButton.Text = "Retry now";
        }

        /// <summary>失败横幅“重试”：与 Retry All Failed 菜单同语义——重置全部非永久 Erroneo 回 EnCola。
    /// 永久错误（EsErrorPermanente=True）不重试：链接已失效/密钥错误，自动重下只会烧配额（与自愈调度一致）。
    /// 配额失败保留断点（ResetearDescarga(preserveProgress:=True)），普通失败全量重置。 </summary>
        private void FailureRetry_Click(object sender, EventArgs e)
        {
            try
            {
                int retried = 0;
                var pend = new List<Fichero>();
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete paq in ListaPaquetes)
                    {
                        foreach (Fichero fic in paq.ListaFicheros)
                        {
                            if (fic.DescargaEstado() == Estado.Erroneo && !fic.EsErrorPermanente)
                            {
                                pend.Add(fic);
                            }
                        }
                    }
                }
                foreach (Fichero fic in pend)
                {
                    if (fic.FailedByQuota)
                    {
                        fic.ResetearDescarga(preserveProgress: true);
                    }
                    else
                    {
                        fic.ResetearDescarga();
                    }
                    fic.PausaIndividual = false;
                    fic.SetDescargaEstado = Estado.EnCola;
                    retried += 1;
                }
                if (retried > 0)
                    Log.WriteWarning("Failure banner retry: woke " + retried + " failed files.");
                RefreshListaDescargas(true);
                RefreshFailureBanner();
            }
            catch (Exception ex)
            {
                Log.WriteError("FailureRetry failed: " + Log.SafeException(ex));
            }
        }
    }
}