using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MegaDownloader.Core.Tests
{
    /// <summary>
    /// Cross-platform regression entry point. Runs on Windows, macOS and Linux against
    /// MegaDownloader.Core only (no WinForms), so the same checks can gate a build on
    /// every target platform.
    /// </summary>
    public static class Program
    {
        private static int _assertions;
        private static int _failures;

        public static int Main(string[] args)
        {
            // Second-process side of the single-instance case: hand the running test
            // instance this launch's arguments exactly as a second MegaDownloader would.
            if (args.Length >= 2 && args[0] == "--instance-child")
            {
                var child = new SingleInstanceCoordinator(args[1], "CoreTests");
                if (child.TryAcquire())
                {
                    child.Dispose();
                    return 3;
                }
                child.SendArguments(args.Skip(2).ToArray());
                child.Dispose();
                return 0;
            }

            string root = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "mega-core-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                PreflightTreeCases();
                PreflightSelectionCases();
                DestinationPreviewCases();
                PreflightSessionCacheCases();
                LinkContainerCases();
                SingleInstanceCases();
                DownloadScheduleCases();

                Console.WriteLine();
                Console.WriteLine("ASSERTIONS: " + _assertions);
                if (_failures > 0)
                {
                    Console.WriteLine("FAILED: " + _failures);
                    return 1;
                }
                Console.WriteLine("PASS: " + _assertions + " assertions.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FATAL: " + ex);
                return 2;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void Check(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                _failures++;
                Console.WriteLine("FAIL: " + message);
            }
        }

        private static BatchLinkPreflight.Entry Entry(string name, string folder, string path, long? size = 100L,
            BatchLinkPreflight.LinkState state = BatchLinkPreflight.LinkState.Ready)
        {
            var source = new URLProcessor.FileURL("https://mega.nz/file/" + Guid.NewGuid().ToString("N").Substring(0, 8) + "#key", path)
            {
                DisplayName = name,
                SizeBytes = size,
                FolderName = folder
            };
            return new BatchLinkPreflight.Entry
            {
                Source = source,
                OriginalUrl = source.URL,
                Name = name,
                SizeBytes = size,
                State = state,
                Selected = state == BatchLinkPreflight.LinkState.Ready
            };
        }

        private static void PreflightTreeCases()
        {
            var entries = new List<BatchLinkPreflight.Entry>
            {
                Entry("a.mp4", "Share", "Docs"),
                Entry("b.mp4", "Share", "Docs/Sub"),
                Entry("c.pdf", "Share", "Docs/Sub"),
                Entry("d.zip", "Share", ""),
            };
            var selection = new PreflightSelection(entries);
            var tree = new PreflightTree(selection);

            // Folders start collapsed: only the share root and top-level files are shown.
            var share = tree.Rows.First(node => node.IsFolder && node.Name == "Share");
            Check(tree.Rows.All(node => !(node.IsFolder && node.Name == "Docs")), "nested folders are collapsed at first");
            Check(share.MatchCount == 4, "share root counts every file");
            Check(share.CheckState == PreflightTree.TriState.Checked, "all selected reads as checked");
            Check(share.SizeBytes == 400m, "share size is the sum of its files");

            tree.ExpandAll(true);
            var docs = tree.Rows.First(node => node.IsFolder && node.Name == "Docs");
            var b = tree.LeafNode(entries[1]);
            Check(b is not null && b.Parent.Name == "Sub", "nested path becomes a nested folder");

            // Deselect one leaf: ancestors become indeterminate, siblings unaffected.
            selection.SetSelected(entries[1], false);
            tree.Filter(false);
            tree.ExpandAll(true);
            Check(docs.CheckState == PreflightTree.TriState.Indeterminate, "partial selection reads as indeterminate");
            Check(tree.Rows.First(node => node.Name == "Sub").CheckState == PreflightTree.TriState.Indeterminate,
                "nested folder aggregates too");

            // Collapsing hides rows but must not change what is selected.
            int selectedBefore = selection.SelectedCount;
            decimal bytesBefore = selection.SelectedBytes;
            tree.ExpandAll(true);
            var docsBefore = tree.Rows.First(node => node.Name == "Docs");
            tree.Toggle(docsBefore);
            Check(tree.Rows.All(node => !(node.IsFolder && node.Name == "Sub")), "collapse hides the nested folder");
            Check(selection.SelectedCount == selectedBefore, "collapse keeps the selection count");
            Check(selection.SelectedBytes == bytesBefore, "collapse keeps the selected size");

            // Selecting a folder selects everything underneath it, including hidden items.
            tree.ExpandAll(true);
            var docsNode = tree.Rows.First(node => node.Name == "Docs");
            tree.Apply(tree.NodesUnder(docsNode), PreflightSelection.SelectionOperation.SelectAll);
            tree.Filter(false);
            Check(docsNode.CheckState == PreflightTree.TriState.Checked, "folder select selects all descendants");
            Check(entries.Where(e => e.Source.Path is not null && e.Source.Path.StartsWith("Docs")).All(e => e.Selected),
                "every descendant entry is selected");

            // Clearing a folder clears its descendants.
            tree.Apply(tree.NodesUnder(docsNode), PreflightSelection.SelectionOperation.Clear);
            tree.Filter(false);
            Check(entries.Where(e => e.Source.Path is not null && e.Source.Path.StartsWith("Docs")).All(e => !e.Selected),
                "folder clear clears every descendant");
            Check(entries[3].Selected, "clearing one folder leaves other files alone");
            Console.WriteLine("preflight_tree: tri-state, aggregation, collapse and folder operations passed");
        }

        private static void PreflightSelectionCases()
        {
            var entries = new List<BatchLinkPreflight.Entry>
            {
                Entry("movie.mkv", "Share", "Video"),
                Entry("song.mp3", "Share", "Audio"),
                Entry("notes.txt", "Share", "Docs"),
                Entry("unknown.bin", "Share", "", size: null),
            };
            var selection = new PreflightSelection(entries);
            Check(selection.SelectedCount == 4, "ready entries start selected");
            Check(selection.UnknownSizes == 1, "unknown size counted separately");
            Check(selection.SelectedBytes == 300m, "known sizes summed");

            // Filtering only narrows what is visible; totals still count hidden rows.
            selection.Filter("mp3", "");
            Check(selection.Visible.Count == 1, "filter matches file names");
            Check(selection.SelectedCount == 4, "filter does not drop selection totals");
            Check(selection.SelectedBytes == 300m, "filter does not drop the selected size");

            selection.Filter("", "Video");
            Check(selection.Visible.Count == 1 && selection.Visible[0].Name == "movie.mkv", "category filter narrows rows");

            selection.Filter("Video", "");
            Check(selection.Visible.Count == 1, "filter matches the relative path");

            selection.Filter("share", "");
            Check(selection.Visible.Count == 4, "filter matches the share folder name");

            selection.Filter("", "");
            var categories = selection.Categories();
            Check(categories.Contains("Video") && categories.Contains("Audio") && categories.Contains("Documents"),
                "categories reflect the files present");

            selection.Apply(PreflightSelection.SelectionOperation.Clear, visibleOnly: false);
            Check(selection.SelectedCount == 0 && selection.SelectedBytes == 0m, "clear resets totals");
            selection.Apply(PreflightSelection.SelectionOperation.SelectAll, visibleOnly: false);
            Check(selection.SelectedCount == 4, "select all restores every entry");

            // A non-addable entry never becomes selected.
            var blocked = Entry("bad.mkv", "Share", "", state: BatchLinkPreflight.LinkState.Invalid);
            var blockedSelection = new PreflightSelection(new List<BatchLinkPreflight.Entry> { blocked });
            blockedSelection.SetSelected(blocked, true);
            Check(blockedSelection.SelectedCount == 0, "invalid entries cannot be selected");
            Console.WriteLine("preflight_selection: totals independent of filter, categories and clear/select-all passed");
        }

        private static void DestinationPreviewCases()
        {
            string root = Path.Combine(Path.GetTempPath(), "mega-preview-" + Guid.NewGuid().ToString("N"));

            var named = new List<URLProcessor.FileURL>
            {
                new URLProcessor.FileURL("https://mega.nz/file/AAAA#k", "Sub") { DisplayName = "a.bin" },
                new URLProcessor.FileURL("https://mega.nz/file/BBBB#k", "") { DisplayName = "b.bin" },
            };
            var preview = DownloadPackageBuilder.PreviewDestination(named, "MyPackage", root, createDirectory: true);
            Check(preview.PackageRoot == Path.Combine(root, "MyPackage"), "explicit package name becomes the folder");
            Check(preview.SampleFiles.Any(path => path.EndsWith(Path.Combine("Sub", "a.bin"))), "sample keeps the relative path");
            Check(!Directory.Exists(root), "preview writes nothing to disk");

            var shareNamed = new List<URLProcessor.FileURL>
            {
                new URLProcessor.FileURL("https://mega.nz/file/CCCC#k", "Docs") { DisplayName = "c.bin", FolderName = "RealShare" },
                new URLProcessor.FileURL("https://mega.nz/file/DDDD#k", "Docs") { DisplayName = "d.bin", FolderName = "RealShare" },
            };
            var sharePreview = DownloadPackageBuilder.PreviewDestination(shareNamed, "", root, createDirectory: true);
            Check(sharePreview.PackageRoot == Path.Combine(root, "RealShare"), "folder share uses its decrypted name");

            var single = new List<URLProcessor.FileURL>
            {
                new URLProcessor.FileURL("https://mega.nz/file/EEEE#k", "") { DisplayName = null }
            };
            var pending = DownloadPackageBuilder.PreviewDestination(single, "", root, createDirectory: true);
            Check(pending.PackageRoot is null && pending.NamePending, "a single unnamed link reports a pending folder");

            var noSubdir = DownloadPackageBuilder.PreviewDestination(named, "MyPackage", root, createDirectory: false);
            Check(noSubdir.PackageRoot == root, "without a package folder files land in the destination");
            Console.WriteLine("destination_preview: explicit, share, pending and no-subfolder cases passed");
        }

        private static void PreflightSessionCacheCases()
        {
            int loads = 0;
            var session = new PreflightSession((url, token) =>
            {
                loads++;
                return new List<BatchLinkPreflight.Entry> { Entry("cached.bin", "Share", "") };
            });

            var first = session.Prepare("https://mega.nz/file/FFFF#key");
            first.Results.Wait();
            int afterFirst = loads;

            // The same text reuses the in-flight/finished request without a new load.
            var second = session.Prepare("https://mega.nz/file/FFFF#key");
            second.Results.Wait();
            Check(loads == afterFirst, "same text reuses the prepared request");

            session.Invalidate(clearCache: true);
            var third = session.Prepare("https://mega.nz/file/FFFF#key");
            third.Results.Wait();
            Check(loads > afterFirst, "clearing the cache forces a new load");
            session.Dispose();
            Console.WriteLine("preflight_session: request reuse and cache clearing passed");
        }

        private static void LinkContainerCases()
        {
            string folder = Path.Combine(Path.GetTempPath(), "mega-container-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string elc = Path.Combine(folder, "links.ELC");
                File.WriteAllText(elc, "  serverEncodedPayload  ");
                Check(LinkContainer.IsContainerPath(elc), ".elc is recognised case-insensitively");
                Check(!LinkContainer.IsContainerPath(Path.Combine(folder, "plain.txt")), "other extensions are not containers");

                string link = LinkContainer.ReadAsync(elc).GetAwaiter().GetResult();
                Check(link == "mega://elc?serverEncodedPayload", "an .elc becomes a server-encoded link");
                Check(URLExtractor.ExtraerURLs(link).Count == 1, "the generated link is recognised by the extractor");

                string dlc = Path.Combine(folder, "old.dlc");
                File.WriteAllText(dlc, "payload");
                bool refused = false;
                try
                {
                    LinkContainer.ReadAsync(dlc).GetAwaiter().GetResult();
                }
                catch (NotSupportedException)
                {
                    refused = true;
                }
                Check(refused, ".dlc is refused with the discontinued reason");

                string empty = Path.Combine(folder, "empty.elc");
                File.WriteAllText(empty, "   ");
                bool emptyRefused = false;
                try
                {
                    LinkContainer.ReadAsync(empty).GetAwaiter().GetResult();
                }
                catch (InvalidDataException)
                {
                    emptyRefused = true;
                }
                Check(emptyRefused, "an empty container is reported");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
            Console.WriteLine("link_container: elc decoding, dlc refusal and empty files passed");
        }

        private static void SingleInstanceCases()
        {
            string folder = Path.Combine(Path.GetTempPath(), "mega-instance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var first = new SingleInstanceCoordinator(folder, "CoreTests");
            var second = new SingleInstanceCoordinator(folder, "CoreTests");
            try
            {
                Check(first.TryAcquire(), "the first process acquires the instance lock");
                Check(!second.TryAcquire(), "a second process is refused the lock");

                // Really spawn the second process: it must fail to take the lock and
                // leave its payload in the spool, with links and with no arguments.
                Check(RunInstanceChild(folder, "mega://elc?abc") == 0,
                    "a second process forwards its links instead of running");
                Check(RunInstanceChild(folder) == 0,
                    "a second process without arguments still reaches the owner");

                var pending = first.ReadPendingArguments();
                Check(pending.Count == 2, "both launches are forwarded in order");
                Check(pending[0].Count == 1 && pending[0][0] == "mega://elc?abc", "the link is forwarded intact");
                Check(pending[1].Count == 0, "a launch without arguments is forwarded as an empty batch");
                Check(first.ReadPendingArguments().Count == 0, "draining empties the spool");

                first.Dispose();
                Check(second.TryAcquire(), "the lock is released when its owner exits");
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                Directory.Delete(folder, true);
            }
            Console.WriteLine("single_instance: lock, argument spool and empty launch passed");
        }

        /// <summary>
        /// Runs this test binary as the second MegaDownloader process. Exit code 3 means
        /// it unexpectedly took the lock; 0 means it forwarded its payload.
        /// </summary>
        private static int RunInstanceChild(string folder, params string[] payload)
        {
            string host = Environment.ProcessPath ?? throw new InvalidOperationException("No process path available.");
            var start = new ProcessStartInfo(host) { UseShellExecute = false };
            if (string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--instance-child");
            start.ArgumentList.Add(folder);
            foreach (string argument in payload)
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start) ?? throw new InvalidOperationException("The second process did not start.");
            if (!process.WaitForExit(30000))
            {
                process.Kill();
                return -1;
            }
            return process.ExitCode;
        }

        private static void DownloadScheduleCases()
        {
            var crossMidnight = new DownloadScheduleSettings { Enabled = true, Daily = true, StartMinute = 23 * 60, EndMinute = 7 * 60 };
            var beforeMidnight = new DateTime(2026, 1, 1, 23, 30, 0);
            var afterMidnight = new DateTime(2026, 1, 2, 6, 0, 0);
            Check(crossMidnight.AllowsDownload(beforeMidnight), "a cross-midnight window is active before midnight");
            Check(crossMidnight.AllowsDownload(afterMidnight), "a cross-midnight window is active after midnight");
            Check(crossMidnight.ActiveWindow(beforeMidnight) == crossMidnight.ActiveWindow(afterMidnight),
                "the same window keeps its identity across midnight");
            Check(!crossMidnight.AllowsDownload(new DateTime(2026, 1, 2, 12, 0, 0)), "outside the window is refused");

            var once = new DownloadScheduleSettings
            {
                Enabled = true,
                Daily = false,
                OnceStart = new DateTime(2026, 1, 1, 9, 0, 0),
                OnceEnd = new DateTime(2026, 1, 1, 17, 0, 0)
            };
            Check(once.AllowsDownload(new DateTime(2026, 1, 1, 12, 0, 0)), "a one-time window allows downloads inside it");
            Check(!once.AllowsDownload(new DateTime(2026, 1, 1, 18, 0, 0)), "a one-time window stops after its end");
            Check(!once.AllowsDownload(new DateTime(2026, 1, 1, 8, 0, 0)), "a one-time window does not start early");
            Console.WriteLine("download_schedule: daily cross-midnight and one-time windows passed");
        }
    }
}
