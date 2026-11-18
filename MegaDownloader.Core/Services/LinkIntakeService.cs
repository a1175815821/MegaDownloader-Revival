using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MegaDownloader
{
    /// <summary>Outcome of pushing clipboard or command-line text into the queue.</summary>
    public enum LinkIntakeStatus
    {
        /// <summary>Nothing link-shaped was found.</summary>
        NoLinks,
        /// <summary>Every link found is already queued.</summary>
        AllDuplicates,
        /// <summary>A package was appended to the queue.</summary>
        Added,
        /// <summary>The queue is not ready or the default folder is unusable.</summary>
        NotReady,
        Failed
    }

    public sealed class LinkIntakeResult
    {
        public LinkIntakeResult(LinkIntakeStatus status, int linkCount, int addedCount, string message)
        {
            Status = status;
            LinkCount = linkCount;
            AddedCount = addedCount;
            Message = message;
        }

        public LinkIntakeStatus Status { get; }

        /// <summary>Links recognised in the input, before duplicate filtering.</summary>
        public int LinkCount { get; }

        /// <summary>Files actually queued.</summary>
        public int AddedCount { get; }

        public string Message { get; }
    }

    /// <summary>
    /// Turns free text (clipboard contents, a <c>mega://</c> activation argument, a dropped
    /// string) into a queue entry. Shared by the clipboard monitor, the protocol handler
    /// and the command line so all three intake paths behave identically.
    /// </summary>
    public sealed class LinkIntakeService
    {
        private readonly DownloadEngineService _engine;

        public LinkIntakeService(DownloadEngineService engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>Links found in the text, without touching the queue.</summary>
        public static IReadOnlyList<string> ExtractLinks(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();
            return URLExtractor.ExtraerURLs(text);
        }

        /// <summary>
        /// Expands folder and encoded links, drops duplicates already in the queue and
        /// appends one package built from what is left. Safe to call from a background
        /// thread.
        /// </summary>
        public async Task<LinkIntakeResult> AddAsync(
            string? text,
            string? packageName = null,
            bool? createSubdirectory = null,
            CancellationToken cancellationToken = default)
        {
            if (!_engine.IsLoaded)
                return new LinkIntakeResult(LinkIntakeStatus.NotReady, 0, 0, "The download queue is still loading.");

            IReadOnlyList<string> links = ExtractLinks(text);
            if (links.Count == 0)
                return new LinkIntakeResult(LinkIntakeStatus.NoLinks, 0, 0, "No MEGA links found.");

            Configuracion config = _engine.Config;
            string destination = config.RutaDefecto;
            if (string.IsNullOrEmpty(destination) || !System.IO.Directory.Exists(destination))
            {
                return new LinkIntakeResult(LinkIntakeStatus.NotReady, links.Count, 0,
                    "Set a default download folder in Settings before adding links from the clipboard.");
            }

            // The clipboard path uses the same package defaults as the add-links
            // window, so a copied link lands in the same place as a pasted one.
            bool createDirectory = createSubdirectory ?? config.CrearDirectorioPaquete;

            try
            {
                var expanded = await Task.Run(
                    () => URLProcessor.ProcessURLs(new List<string>(links), ref config),
                    cancellationToken).ConfigureAwait(false);

                if (expanded is null || expanded.Count == 0)
                    return new LinkIntakeResult(LinkIntakeStatus.NoLinks, links.Count, 0, "No usable MEGA links found.");

                var queued = await Task.Run(() => _engine.GetQueuedLinkSnapshot(), cancellationToken).ConfigureAwait(false);
                var filtered = new List<URLProcessor.FileURL>(expanded.Count);
                foreach (URLProcessor.FileURL file in expanded)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string key = BatchLinkPreflight.Identity(file);
                    if (key is null || !queued.ContainsKey(key))
                        filtered.Add(file);
                }

                if (filtered.Count == 0)
                {
                    return new LinkIntakeResult(LinkIntakeStatus.AllDuplicates, links.Count, 0,
                        "Those links are already in the queue.");
                }

                Paquete package = await Task.Run(
                    () => DownloadPackageBuilder.Build(
                        filtered,
                        packageName ?? string.Empty,
                        destination,
                        createDirectory,
                        config.ExtraerAutomaticamente,
                        string.Empty,
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false);

                if (!_engine.AddPackage(package))
                {
                    foreach (Fichero file in package.SnapshotFicheros())
                        file.Dispose();
                    return new LinkIntakeResult(LinkIntakeStatus.NotReady, links.Count, 0, "The download queue is not ready.");
                }

                // The user asked for these links, so the transfer starts right away —
                // the same behaviour as the legacy shell, where the add-links dialog
                // had "start download" enabled by default.
                _engine.TryStartAll(out _);

                return new LinkIntakeResult(
                    LinkIntakeStatus.Added,
                    links.Count,
                    filtered.Count,
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Added {filtered.Count} file(s) from the clipboard."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.WriteError("Clipboard link intake failed: " + Log.SafeException(ex));
                return new LinkIntakeResult(LinkIntakeStatus.Failed, links.Count, 0,
                    DownloadAdvice.FirstLine(Log.Redact(ex.Message)));
            }
        }

        /// <summary>
        /// Arguments from a <c>mega://</c> activation or a command line: every link is
        /// collected and queued together, ignoring the executable path itself.
        /// </summary>
        public async Task<LinkIntakeResult> AddFromArgumentsAsync(
            IEnumerable<string> arguments,
            CancellationToken cancellationToken = default)
        {
            var links = new List<string>();
            foreach (string argument in arguments)
            {
                foreach (string link in ExtractLinks(argument))
                    links.Add(link);
            }

            if (links.Count == 0)
                return new LinkIntakeResult(LinkIntakeStatus.NoLinks, 0, 0, "No MEGA links found.");

            return await AddAsync(string.Join(Environment.NewLine, links), null, null, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
