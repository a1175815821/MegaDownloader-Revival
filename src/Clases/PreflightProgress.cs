using System;
using System.Windows.Forms;

namespace MegaDownloader
{

    /// <summary>Immutable snapshots are polled by the UI; folder workers never post one UI event per file.</summary>
    public sealed class PreflightProgress
    {
        public enum Phase
        {
            Parsing,
            Waiting,
            Reading,
            Checking,
            Completed,
            Failed,
            Cancelled
        }

        public readonly Phase Stage;
        public readonly int LinksCompleted;
        public readonly int LinksTotal;
        public readonly int ItemsProcessed;
        public readonly int ItemsTotal;
        public readonly int FilesFound;

        public PreflightProgress(Phase value, int completed = 0, int total = 0, int processed = 0, int itemCount = 0, int found = 0)
        {
            Stage = value;
            LinksCompleted = completed;
            LinksTotal = total;
            ItemsProcessed = processed;
            ItemsTotal = itemCount;
            FilesFound = found;
        }

        public static void Display(PreflightProgress snapshot, Label label, ProgressBar bar, DateTime? started = default)
        {
            if (snapshot is null)
                return;
            string key;
            switch (snapshot.Stage)
            {
                case Phase.Parsing:
                    key = "Preflight_ProgressParsing";
                    break;
                case Phase.Waiting:
                    key = "Preflight_ProgressWaiting";
                    break;
                case Phase.Reading:
                    key = "Preflight_ProgressReading";
                    break;
                case Phase.Checking:
                    key = "Preflight_ProgressChecking";
                    break;
                case Phase.Completed:
                    key = "Preflight_Warmed";
                    break;
                case Phase.Failed:
                    key = "AddLinks_CheckFailed";
                    break;
                case Phase.Cancelled:
                    key = "Preflight_ProgressCancelled";
                    break;
                default:
                    key = "Preflight_Waiting";
                    break;
            }
            string text = Language.GetText(key).Replace("%N%", (snapshot.Stage == Phase.Completed ? snapshot.LinksTotal : snapshot.ItemsProcessed).ToString()).Replace("%F%", (snapshot.Stage == Phase.Completed ? snapshot.FilesFound : snapshot.ItemsTotal).ToString());
            if (snapshot.Stage == Phase.Reading || snapshot.Stage == Phase.Checking)
            {
                text += " · " + Language.GetText("Preflight_ProgressLinks").Replace("%D%", snapshot.LinksCompleted.ToString()).Replace("%T%", snapshot.LinksTotal.ToString());
            }
            if (started.HasValue && snapshot.Stage != Phase.Completed && snapshot.Stage != Phase.Failed && snapshot.Stage != Phase.Cancelled)
            {
                long seconds = (long)Math.Round(Math.Max(0d, Math.Floor((DateTime.UtcNow - started.Value).TotalSeconds)));
                text += " · " + Language.GetText("Preflight_Elapsed").Replace("%T%", seconds.ToString());
            }
            label.Text = text;
            bar.Visible = true;
            bar.Maximum = 1000;
            if (snapshot.Stage == Phase.Completed)
            {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = bar.Maximum;
            }
            else if (snapshot.Stage == Phase.Checking && snapshot.ItemsTotal > 0)
            {
                bar.Style = ProgressBarStyle.Continuous;
                double portion = Math.Min(1.0d, snapshot.ItemsProcessed / (double)snapshot.ItemsTotal);
                bar.Value = (int)Math.Round(Math.Min(999.0d, (snapshot.LinksCompleted + portion) * 1000.0d / Math.Max(1, snapshot.LinksTotal)));
            }
            else if (snapshot.Stage == Phase.Failed || snapshot.Stage == Phase.Cancelled)
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

        internal sealed class Reporter : IProgress<PreflightProgress>
        {
            private readonly Action<PreflightProgress> callback;
            public Reporter(Action<PreflightProgress> action)
            {
                callback = action;
            }
            public void Report(PreflightProgress value)
            {
                callback(value);
            }
        }

        internal sealed class ItemReporter : IProgress<int>
        {
            private readonly Action<int> callback;
            public ItemReporter(Action<int> action)
            {
                callback = action;
            }
            public void Report(int value)
            {
                callback(value);
            }
        }
    }
}