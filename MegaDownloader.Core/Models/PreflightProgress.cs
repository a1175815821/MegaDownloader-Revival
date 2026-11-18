namespace MegaDownloader;

/// <summary>
/// Immutable snapshots are polled by the UI; folder workers never post one UI event per file.
/// The cross-platform UI binds to these values; the legacy WinForms Display helper stays in
/// the WinForms compatibility layer (Forms/AddLinks.Preflight.cs, BatchPreflightView.cs).
/// </summary>
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

    public sealed class Reporter : IProgress<PreflightProgress>
    {
        private readonly Action<PreflightProgress> callback;
        public Reporter(Action<PreflightProgress> action) => callback = action;
        public void Report(PreflightProgress value) => callback(value);
    }

    public sealed class ItemReporter : IProgress<int>
    {
        private readonly Action<int> callback;
        public ItemReporter(Action<int> action) => callback = action;
        public void Report(int value) => callback(value);
    }
}
