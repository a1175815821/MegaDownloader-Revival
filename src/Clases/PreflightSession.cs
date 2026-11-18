using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MegaDownloader
{

    /// <summary>One add-links window owns a speculative, cancellable metadata session.
/// Results never enqueue a download. Finished sources are reused when more links arrive.</summary>
    public sealed class PreflightSession : IDisposable
    {

        public sealed class Request
        {
            public readonly string Text;
            public readonly Task<List<string>> Urls;
            public readonly Task<List<BatchLinkPreflight.Entry>> Results;
            internal readonly CancellationTokenSource Cancellation;
            public readonly DateTime Started = DateTime.UtcNow;
            private PreflightProgress latestProgress = new PreflightProgress(PreflightProgress.Phase.Parsing);

            public PreflightProgress Progress
            {
                get
                {
                    return Volatile.Read(ref latestProgress);
                }
            }

            private void Publish(PreflightProgress value)
            {
                Volatile.Write(ref latestProgress, value);
            }

            internal Request(string textValue, Func<List<string>> parser, Func<List<string>, CancellationToken, Action<PreflightProgress>, List<BatchLinkPreflight.Entry>> resolver, CancellationTokenSource cancellationValue)
            {
                Text = textValue;
                Cancellation = cancellationValue;
                var token = Cancellation.Token;
                Urls = Task.Run(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        var parsed = parser();
                        token.ThrowIfCancellationRequested();
                        return parsed;
                    }, token);
                Results = RunAsync(resolver, token);
                // Speculative work may finish after its window closed; always observe faults.
                Results.ContinueWith(t =>
                    {
                        var observed = t.Exception;
                    }, TaskContinuationOptions.OnlyOnFaulted);
            }

            private async Task<List<BatchLinkPreflight.Entry>> RunAsync(Func<List<string>, CancellationToken, Action<PreflightProgress>, List<BatchLinkPreflight.Entry>> resolver, CancellationToken token)
            {
                try
                {
                    var parsed = await Urls.ConfigureAwait(false);
                    Publish(new PreflightProgress(PreflightProgress.Phase.Waiting, total: parsed.Count));
                    var resolved = await Task.Run(() => resolver(parsed, token, Publish), token).ConfigureAwait(false);
                    Publish(new PreflightProgress(PreflightProgress.Phase.Completed, parsed.Count, parsed.Count, found: resolved.Count));
                    return resolved;
                }
                catch (OperationCanceledException ex)
                {
                    Publish(new PreflightProgress(PreflightProgress.Phase.Cancelled));
                    throw;
                }
                catch (Exception ex)
                {
                    Publish(new PreflightProgress(PreflightProgress.Phase.Failed));
                    throw;
                }
            }
        }

        private sealed class CachedSource
        {
            public List<BatchLinkPreflight.Entry> Entries;
            public DateTime Completed;
        }

        private readonly object gate = new object();
        private readonly SemaphoreSlim workerGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, CachedSource> cache = new Dictionary<string, CachedSource>(StringComparer.Ordinal);
        private readonly Func<string, CancellationToken, IProgress<PreflightProgress>, List<BatchLinkPreflight.Entry>> loader;
        private Request current;
        private bool disposed;

        public PreflightSession(Configuracion configuration)
        {
            loader = new Func<string, CancellationToken, IProgress<PreflightProgress>, List<BatchLinkPreflight.Entry>>((url, token, progress) => BatchLinkPreflight.Resolve(new List<string>() { url }, configuration, new HashSet<string>(StringComparer.Ordinal), token, detailedProgress: progress));
        }

        /// <summary>Injectable source loader for offline cache/cancellation regressions.</summary>
        public PreflightSession(Func<string, CancellationToken, List<BatchLinkPreflight.Entry>> sourceLoader)
        {
            loader = new Func<string, CancellationToken, IProgress<PreflightProgress>, List<BatchLinkPreflight.Entry>>((url, token, progress) => sourceLoader(url, token));
        }

        public Request Prepare(string text, bool force = false)
        {
            lock (gate)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(PreflightSession));
                if (!force && current is not null && !current.Cancellation.IsCancellationRequested && string.Equals(current.Text, text, StringComparison.Ordinal) && !current.Results.IsFaulted && !current.Results.IsCanceled && (!current.Results.IsCompleted || DateTime.UtcNow - current.Started < TimeSpan.FromMinutes(3d)))
                    return current;
                CancelCurrent();
                if (force)
                    cache.Clear();
                var source = new CancellationTokenSource();
                current = new Request(text, () => URLExtractor.ExtraerURLs(text), ResolveSources, source);
                return current;
            }
        }

        public void Invalidate(bool clearCache = false)
        {
            lock (gate)
            {
                CancelCurrent();
                if (clearCache)
                    cache.Clear();
            }
        }

        private void CancelCurrent()
        {
            var previous = current;
            current = null;
            if (previous is null)
                return;
            previous.Cancellation.Cancel();
            previous.Results.ContinueWith(t => previous.Cancellation.Dispose(), TaskScheduler.Default);
        }

        private List<BatchLinkPreflight.Entry> ResolveSources(List<string> urls, CancellationToken token, Action<PreflightProgress> report)
        {
            workerGate.Wait(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var sourceKeys = new Dictionary<string, string>(StringComparer.Ordinal);
                var active = new HashSet<string>(StringComparer.Ordinal);
                foreach (string url in urls)
                {
                    token.ThrowIfCancellationRequested();
                    string key = url;
                    if (!URLExtractor.IsMegaFolder(url) && !URLExtractor.IsELC(url))
                    {
                        key = BatchLinkPreflight.Identity(new URLProcessor.FileURL(url, "")) ?? url;
                    }
                    sourceKeys[url] = key;
                    active.Add(key);
                }
                lock (gate)
                {
                    foreach (string key in new List<string>(cache.Keys))
                    {
                        if (!active.Contains(key))
                            cache.Remove(key);
                    }
                }
                var result = new List<BatchLinkPreflight.Entry>();
                int completed = 0;
                foreach (string url in urls)
                {
                    token.ThrowIfCancellationRequested();
                    CachedSource known = null;
                    string sourceKey = sourceKeys[url];
                    lock (gate)
                        cache.TryGetValue(sourceKey, out known);
                    if (known is null || DateTime.UtcNow - known.Completed >= TimeSpan.FromMinutes(3d))
                    {
                        var sourceProgress = new PreflightProgress.Reporter(value => report(new PreflightProgress(value.Stage, completed, urls.Count, value.ItemsProcessed, value.ItemsTotal, result.Count)));
                        report(new PreflightProgress(PreflightProgress.Phase.Reading, completed, urls.Count, found: result.Count));
                        var inspected = loader(url, token, sourceProgress);
                        token.ThrowIfCancellationRequested();
                        known = new CachedSource() { Entries = inspected, Completed = DateTime.UtcNow };
                        // Transient failures are retried on the next generation, not cached.
                        if (!inspected.Exists(entry => entry.State == BatchLinkPreflight.LinkState.Unavailable))
                        {
                            lock (gate)
                            {
                                if (!disposed && !token.IsCancellationRequested)
                                    cache[sourceKey] = known;
                            }
                        }
                    }
                    result.AddRange(known.Entries);
                    completed += 1;
                    report(new PreflightProgress(PreflightProgress.Phase.Checking, completed - 1, urls.Count, known.Entries.Count, known.Entries.Count, result.Count));
                }
                return result;
            }
            finally
            {
                workerGate.Release();
            }
        }

        public static List<BatchLinkPreflight.Entry> Reconcile(List<BatchLinkPreflight.Entry> input, Dictionary<string, URLProcessor.FileURL> queued)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<BatchLinkPreflight.Entry>(input.Count);
            foreach (BatchLinkPreflight.Entry source in input)
            {
                var entry = new BatchLinkPreflight.Entry() { Source = source.Source, OriginalUrl = source.OriginalUrl, Name = source.Name, SizeBytes = source.SizeBytes, State = source.State, Detail = source.Detail };
                string key = BatchLinkPreflight.Identity(entry.Source);
                if (key is not null)
                {
                    URLProcessor.FileURL existing = null;
                    if (queued.TryGetValue(key, out existing))
                    {
                        entry.State = BatchLinkPreflight.LinkState.AlreadyQueued;
                        if (!string.IsNullOrEmpty(existing.DisplayName))
                            entry.Name = existing.DisplayName;
                        if (existing.SizeBytes.HasValue)
                            entry.SizeBytes = existing.SizeBytes;
                    }
                    else if (!seen.Add(key))
                    {
                        entry.State = BatchLinkPreflight.LinkState.Duplicate;
                    }
                }
                entry.Selected = entry.State == BatchLinkPreflight.LinkState.Ready;
                result.Add(entry);
            }
            return result;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                CancelCurrent();
                cache.Clear();
            }
            // workerGate can still have a cancelled worker unwinding. It owns no OS
            // wait handle unless requested, so let GC release it after that worker exits.
        }
    }
}