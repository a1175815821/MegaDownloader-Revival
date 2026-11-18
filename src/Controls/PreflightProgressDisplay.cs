using System;
using System.Windows.Forms;

namespace MegaDownloader
{
    /// <summary>
    /// WinForms renderer for the cross-platform <see cref="PreflightProgress"/> snapshots.
    /// The snapshot type itself lives in MegaDownloader.Core; this helper stays with the
    /// legacy WinForms UI.
    /// </summary>
    public static class PreflightProgressDisplay
    {
        public static void Display(PreflightProgress snapshot, Label label, ProgressBar bar, DateTime? started = default)
        {
            if (snapshot is null)
                return;
            string key;
            switch (snapshot.Stage)
            {
                case PreflightProgress.Phase.Parsing:
                    key = "Preflight_ProgressParsing";
                    break;
                case PreflightProgress.Phase.Waiting:
                    key = "Preflight_ProgressWaiting";
                    break;
                case PreflightProgress.Phase.Reading:
                    key = "Preflight_ProgressReading";
                    break;
                case PreflightProgress.Phase.Checking:
                    key = "Preflight_ProgressChecking";
                    break;
                case PreflightProgress.Phase.Completed:
                    key = "Preflight_Warmed";
                    break;
                case PreflightProgress.Phase.Failed:
                    key = "AddLinks_CheckFailed";
                    break;
                case PreflightProgress.Phase.Cancelled:
                    key = "Preflight_ProgressCancelled";
                    break;
                default:
                    key = "Preflight_Waiting";
                    break;
            }
            string text = Language.GetText(key).Replace("%N%", (snapshot.Stage == PreflightProgress.Phase.Completed ? snapshot.LinksTotal : snapshot.ItemsProcessed).ToString()).Replace("%F%", (snapshot.Stage == PreflightProgress.Phase.Completed ? snapshot.FilesFound : snapshot.ItemsTotal).ToString());
            if (snapshot.Stage == PreflightProgress.Phase.Reading || snapshot.Stage == PreflightProgress.Phase.Checking)
            {
                text += " · " + Language.GetText("Preflight_ProgressLinks").Replace("%D%", snapshot.LinksCompleted.ToString()).Replace("%T%", snapshot.LinksTotal.ToString());
            }
            if (started.HasValue && snapshot.Stage != PreflightProgress.Phase.Completed && snapshot.Stage != PreflightProgress.Phase.Failed && snapshot.Stage != PreflightProgress.Phase.Cancelled)
            {
                long seconds = (long)Math.Round(Math.Max(0d, Math.Floor((DateTime.UtcNow - started.Value).TotalSeconds)));
                text += " · " + Language.GetText("Preflight_Elapsed").Replace("%T%", seconds.ToString());
            }
            label.Text = text;
            bar.Visible = true;
            bar.Maximum = 1000;
            if (snapshot.Stage == PreflightProgress.Phase.Completed)
            {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = bar.Maximum;
            }
            else if (snapshot.Stage == PreflightProgress.Phase.Checking && snapshot.ItemsTotal > 0)
            {
                bar.Style = ProgressBarStyle.Continuous;
                double portion = Math.Min(1.0d, snapshot.ItemsProcessed / (double)snapshot.ItemsTotal);
                bar.Value = (int)Math.Round(Math.Min(999.0d, (snapshot.LinksCompleted + portion) * 1000.0d / Math.Max(1, snapshot.LinksTotal)));
            }
            else if (snapshot.Stage == PreflightProgress.Phase.Failed || snapshot.Stage == PreflightProgress.Phase.Cancelled)
            {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = 0;
            }
            else
            {
                // The server has not returned the folder size yet: no invented percentage.
                bar.Style = ProgressBarStyle.Marquee;
                bar.MarqueeAnimationSpeed = 30;
            }
        }
    }
}
