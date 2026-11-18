using System;
using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    public partial class AddLinks
    {
        private PreflightSession preflightSession;
        private PreflightSession.Request warmRequest;
        private int inputGeneration;
        private bool importBusy;
        private bool preflightDisposed;
        private System.Threading.CancellationTokenSource importCancellation;
        private readonly List<Tuple<string, bool, bool, List<string>>> deferredInputs = new List<Tuple<string, bool, bool, List<string>>>();
        private readonly System.Windows.Forms.Timer preflightProgressTimer = new System.Windows.Forms.Timer() { Interval = 150 };
        private PreflightProgress displayedProgress;
        private long displayedProgressSecond = -1;

        private void RefreshPreflightProgress(object sender, EventArgs e)
        {
            if (preflightDisposed)
                return;
            if (importBusy)
            {
                RefreshImportProgress();
                return;
            }
            if (warmRequest is null)
                return;
            var snapshot = warmRequest.Progress;
            if (warmRequest.Results.IsCompleted)
                preflightProgressTimer.Stop();
            long second = (long)Math.Round(Math.Max(0d, Math.Floor((DateTime.UtcNow - warmRequest.Started).TotalSeconds)));
            if (ReferenceEquals(displayedProgress, snapshot) && second == displayedProgressSecond)
                return;
            displayedProgress = snapshot;
            displayedProgressSecond = second;
            PreflightProgressDisplay.Display(snapshot, lblLinkCount, preflightProgressBar, warmRequest.Started);
        }

        private PreflightSession.Request CurrentPreflightRequest()
        {
            if (preflightSession is null)
                preflightSession = new PreflightSession(Config);
            warmRequest = preflightSession.Prepare(txtLinks.Text + Constants.vbNewLine + HiddenLinks);
            return warmRequest;
        }

        private async void UpdateLinkCount()
        {
            if (Config is null || preflightDisposed || importBusy)
                return;
            int generation = inputGeneration;
            try
            {
                if (string.IsNullOrWhiteSpace(txtLinks.Text))
                {
                    HiddenLinks = string.Empty;
                    lblLinkCount.Text = "";
                    return;
                }
                var request = CurrentPreflightRequest();
                preflightProgressTimer.Tick -= RefreshPreflightProgress;
                preflightProgressTimer.Tick += RefreshPreflightProgress;
                preflightProgressTimer.Start();
                RefreshPreflightProgress(null, EventArgs.Empty);
                var parsed = await request.Urls;
                if (preflightDisposed || generation != inputGeneration || importBusy)
                    return;
                var result = await request.Results;
                if (preflightDisposed || generation != inputGeneration || importBusy)
                    return;
                RefreshPreflightProgress(null, EventArgs.Empty);
                preflightProgressTimer.Stop();
            }
            catch (OperationCanceledException ex)
            {
            }
            catch (Exception ex)
            {
                if (!preflightDisposed && generation == inputGeneration)
                {
                    RefreshPreflightProgress(null, EventArgs.Empty);
                    preflightProgressTimer.Stop();
                }
                Log.WriteWarning("Background preflight failed: " + Log.SafeException(ex));
            }
        }

        private void PreflightInputChanged()
        {
            inputGeneration += 1;
            InvalidateSelectionPage();
            bool empty = string.IsNullOrWhiteSpace(txtLinks.Text);
            warmRequest = null;
            displayedProgress = null;
            preflightProgressTimer.Stop();
            preflightProgressBar.Visible = !empty;
            preflightProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            if (preflightSession is not null && !importBusy)
                preflightSession.Invalidate(empty);
            if (_cueFallbackLabel is not null && !_cueFallbackLabel.IsDisposed)
                _cueFallbackLabel.Visible = txtLinks.TextLength == 0;
            lblLinkCount.Text = empty ? "" : Language.GetText("Preflight_Waiting");
        }

        private void RestoreDeferredInputs(bool clearOld)
        {
            if (clearOld)
            {
                HiddenLinks = string.Empty;
                txtLinks.Clear();
            }
            var pending = new List<Tuple<string, bool, bool, List<string>>>(deferredInputs);
            deferredInputs.Clear();
            foreach (Tuple<string, bool, bool, List<string>> item in pending)
                AgregarEnlaces(item.Item1, false, item.Item2, item.Item3, item.Item4);
            UpdateLinkCount();
        }

        private void AddLinks_Disposed(object sender, EventArgs e)
        {
            DisposePreflight();
        }

        private void DisposePreflight()
        {
            if (preflightDisposed)
                return;
            preflightDisposed = true;
            inputGeneration += 1;
            warmRequest = null;
            preflightProgressTimer.Dispose();
            if (t is not null)
                t.Dispose();
            if (preflightSession is not null)
                preflightSession.Dispose();
            if (importCancellation is not null)
                importCancellation.Cancel();
        }
    }
}