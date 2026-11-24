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

            if (args.Length >= 2 && args[0] == "--write-test-data")
            {
                TestDataSets.WriteQueueProfile(args[1], Path.Combine(args[1], "Downloads"));
                Console.WriteLine("Wrote the fixed queue profile to " + args[1]);
                return 0;
            }
            if (args.Length >= 2 && args[0] == "--write-empty-profile")
            {
                TestDataSets.WriteEmptyProfile(args[1]);
                Console.WriteLine("Wrote the empty profile to " + args[1]);
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--describe-test-data")
            {
                Console.WriteLine(TestDataSets.ShareShapeDescription());
                Console.WriteLine();
                Console.WriteLine(TestDataSets.TransferCaseDescription());
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
                MegaDriveCases(root);
                MegaDriveHistoryCases(root);
                MegaDriveSubscriptionCases();
                PublicFolderSubscriptionCases(root);
                AutoStartEntryCases(root);
                MegaAccountStoreCases(root);
                FicheroAccountXmlCases(root);
                ThrottledAccountBudgetCases();

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

        private static MegaDriveSnapshot DriveSnapshot()
        {
            var nodes = new List<MegaDriveNode>
            {
                new MegaDriveNode(1, 0, "Root", true, 0, 0),
                new MegaDriveNode(2, 1, "Docs", true, 0, 10),
                new MegaDriveNode(3, 2, "Sub", true, 0, 11),
                new MegaDriveNode(4, 2, "a.mp4", false, 100, 12),
                new MegaDriveNode(5, 3, "b.pdf", false, 200, 13),
                new MegaDriveNode(6, 1, "c.zip", false, 300, 14)
            };
            return new MegaDriveSnapshot(1, nodes);
        }

        private static void MegaDriveCases(string root)
        {
            MegaDriveSnapshot snapshot = DriveSnapshot();
            Check(snapshot.IsFolder(2) && !snapshot.IsFolder(4), "drive: folder detection");
            Check(snapshot.Breadcrumbs(3).Select(node => node.Name).SequenceEqual(new[] { "Root", "Docs", "Sub" }),
                "drive: breadcrumbs follow parents");

            MegaDriveDownload full = snapshot.PlanDownload(new ulong[] { 2 });
            Check(full.Files.Count == 2 && full.Directories.Count == 2,
                "drive: selecting a folder includes its files and subfolders");
            Check(full.Files.Select(file => file.Key.Name).OrderBy(name => name).SequenceEqual(new[] { "a.mp4", "b.pdf" }),
                "drive: plan keeps every descendant file");

            var paths = new Dictionary<ulong, string>();
            MegaDriveDownload bound = snapshot.PlanBoundDownload(2, paths);
            Check(bound.Files.Count == 2 && bound.Directories.SequenceEqual(new[] { "Sub" }),
                "drive: a bound folder is relative to itself");
            Check(bound.FolderPaths.TryGetValue(3, out string subPath) && subPath == "Sub",
                "drive: bound folder paths are recorded for later checks");

            MegaDriveDownload flat = snapshot.PlanDownload(new ulong[] { 4, 5 });
            Check(flat.Files.Count == 2 && flat.Directories.Count == 0,
                "drive: selecting files only does not invent folders");

            MegaDriveDownload kept = full.KeepFiles(file => file.Key.Name != "a.mp4", preserveEmpty: false);
            Check(kept.Files.Count == 1 && kept.TotalBytes == 200,
                "drive: KeepFiles filters files and totals");
            Check(kept.Directories.Contains(Path.Combine("Docs", "Sub")) && !kept.Directories.Contains("Other"),
                "drive: KeepFiles keeps only directories used by the selected files");

            string duplicateRoot = Path.Combine(root, "drive-duplicate");
            Directory.CreateDirectory(duplicateRoot);
            var duplicate = new MegaDriveSnapshot(1, new[]
            {
                new MegaDriveNode(1, 0, "Root", true, 0, 0),
                new MegaDriveNode(2, 1, "Same", true, 0, 1),
                new MegaDriveNode(3, 1, "Same", true, 0, 2),
                new MegaDriveNode(4, 2, "x", false, 1, 3)
            });
            MegaDriveDownload collision = duplicate.PlanDownload(new ulong[] { 2, 3 });
            Check(collision.Directories.Contains("Same") && collision.Directories.Contains("Same (2)"),
                "drive: sibling folders with the same name get distinct paths");

            var package = flat.Build("user@example.com", duplicateRoot, "Drive package", false,
                System.Threading.CancellationToken.None);
            try
            {
                Check(package.ListaFicheros.Count == 2, "drive: build creates one task per file");
                Check(package.ListaFicheros.All(file => file.MegaDriveNodeHandle.HasValue &&
                    file.MegaAccountEmail == "user@example.com" && !file.LinkVisible),
                    "drive: tasks keep the account and node handle");
            }
            finally
            {
                foreach (Fichero file in package.SnapshotFicheros())
                    file.Dispose();
            }
        }

        private static void MegaDriveHistoryCases(string root)
        {
            string destination = Path.Combine(root, "history-destination");
            Directory.CreateDirectory(destination);
            string local = Path.Combine(destination, "a.mp4");
            File.WriteAllBytes(local, new byte[100]);

            var history = new MegaDriveHistory(Path.Combine(root, "history"));
            history.Record("user@example.com", 4, "a.mp4", local);

            MegaDriveSnapshot snapshot = DriveSnapshot();
            MegaDriveDownload plan = snapshot.PlanBoundDownload(2, new Dictionary<ulong, string>());
            MegaDriveChanges changes = history.FindBoundChanges("user@example.com", snapshot, 2, destination,
                Array.Empty<MegaDriveQueueItem>(), new Dictionary<ulong, string>());
            Check(!changes.Plan.Files.Any(file => file.Key.Id == 4),
                "history: a delivered file is not offered again");
            Check(changes.Plan.Files.Any(file => file.Key.Id == 5),
                "history: an undelivered file remains in the plan");

            File.WriteAllBytes(local, new byte[101]);
            MegaDriveChanges updated = history.FindBoundChanges("user@example.com", snapshot, 2, destination,
                Array.Empty<MegaDriveQueueItem>(), new Dictionary<ulong, string>());
            Check(updated.Plan.Files.Any(file => file.Key.Id == 4),
                "history: a file changed after delivery is offered again");

            history.RemoveAccount("user@example.com");
            Check(!Directory.EnumerateFiles(Path.Combine(root, "history"), "*.jsonl").Any(),
                "history: removing the account deletes its delivery log");
        }

        private static void MegaDriveSubscriptionCases()
        {
            Check(MegaDriveSubscription.NormalizeExtensions(" .MP4, mkv;;mp4 ") == "mp4, mkv",
                "subscription: extensions are normalized and deduplicated");

            var settings = new MegaDriveSubscription
            {
                Folder = 2,
                Destination = Path.GetTempPath(),
                CheckMinutes = 540,
                ScheduleFromUtc = DateTime.UtcNow.AddDays(-1)
            };
            DateTime? next = settings.NextCheckLocal();
            Check(next.HasValue && next.Value > DateTime.Now, "subscription: next check is in the future");
            settings.Paused = true;
            Check(!settings.NextCheckLocal().HasValue, "subscription: a paused subscription has no next check");

            MegaDriveSnapshot snapshot = DriveSnapshot();
            var changes = new MegaDriveChanges(snapshot.PlanBoundDownload(2, new Dictionary<ulong, string>()),
                new HashSet<ulong> { 4 }, new HashSet<ulong> { 5 });
            var check = new MegaDriveSubscription
            {
                Folder = 2,
                Destination = Path.GetTempPath(),
                Extensions = "mp4",
                ScheduleFromUtc = DateTime.UtcNow
            };
            check.ApplyCheck(snapshot, changes);
            Check(check.Files.Count == 1 && check.Files[0].Handle == 4 && !check.Files[0].Updated,
                "subscription: extension rules keep matching files only");
            MegaDriveDownload selected = check.PlanSelected(check.Files);
            Check(selected.Files.Count == 1 && selected.Files[0].Key.Id == 4,
                "subscription: selected files build a download plan");
            Check(check.MatchesSettings(check.Copy()), "subscription: settings comparison is stable");
        }

        private static void PublicFolderSubscriptionCases(string root)
        {
            Check(MegaPublicFolderSubscription.TryParseLink(
                    "https://mega.nz/folder/AbCdEf12#Key-_123", out string id, out string key, out string link),
                "public folder: a folder link with its key is accepted");
            Check(id == "AbCdEf12" && key == "Key-_123" && link == "https://mega.nz/folder/AbCdEf12#Key-_123",
                "public folder: the link is normalized to the folder form");
            Check(!MegaPublicFolderSubscription.TryParseLink("https://mega.nz/folder/AbCdEf12", out _, out _, out _),
                "public folder: a link without its key is rejected");
            Check(!MegaPublicFolderSubscription.TryParseLink("https://mega.nz/file/AbCdEf12#Key-_123", out _, out _, out _),
                "public folder: a single-file link is not a folder subscription");
            Check(MegaPublicFolderSubscription.TryParseLink(
                    "text https://mega.nz/#F!AbCdEf12!Key-_123 more", out id, out key, out _),
                "public folder: the legacy #F! form is accepted");

            var settings = new MegaPublicFolderSubscription
            {
                FolderId = "AbCdEf12",
                FolderKey = "Key-_123",
                Destination = Path.GetTempPath(),
                CheckMinutes = 540,
                ScheduleFromUtc = DateTime.UtcNow.AddDays(-1)
            };
            DateTime? next = settings.NextCheckLocal();
            Check(next.HasValue && next.Value > DateTime.Now, "public folder: next check is in the future");
            settings.Paused = true;
            Check(!settings.NextCheckLocal().HasValue, "public folder: a paused subscription has no next check");
            settings.Paused = false;

            MegaFolderSubscriptionSettings edit = settings.ReadSettings();
            edit.Extensions = " .MP4, mkv ";
            edit.AutoQueue = true;
            settings.ApplySettings(edit, destinationChanged: false);
            Check(settings.Extensions == "mp4, mkv" && settings.AutoQueue,
                "public folder: edited settings are normalized");

            settings.Files.Add(new MegaPublicFolderFile
            {
                FileId = "N?1234",
                Name = "clip.mp4",
                RelativePath = "season 1",
                Bytes = 10
            });
            settings.ApplySettings(edit, destinationChanged: true);
            Check(settings.Files.Count == 0, "public folder: changing the destination drops the old report");

            Check(MegaPublicFolderSubscription.TryReadHandle("N?1234", out ulong handle) && handle == 1234,
                "public folder: a numeric handle is read from the task id");
            Check(!MegaPublicFolderSubscription.TryReadHandle("https://mega.nz/file/AbCdEf12#k", out _),
                "public folder: a plain link has no folder handle");

            var store = new MegaPublicFolderStore(Path.Combine(root, "public-folders"));
            settings.Files.Add(new MegaPublicFolderFile { FileId = "N?9", Name = "a.mkv", Bytes = 4 });
            Check(store.Upsert(settings) == 0, "public folder: a subscription is saved");
            MegaPublicFolderSubscription loaded = store.Find("AbCdEf12");
            Check(loaded is not null && loaded.Files.Count == 1 && loaded.Files[0].Name == "a.mkv",
                "public folder: the saved subscription reads back with its report");
            Check(MegaPublicFolderStore.SameSettings(settings, loaded),
                "public folder: settings comparison survives a save");
            loaded.AutoQueue = !loaded.AutoQueue;
            Check(store.SaveCheck(loaded) == MegaAccountService.Busy,
                "public folder: a check is rejected when the settings changed meanwhile");
            Check(store.Remove("AbCdEf12") == 0 && store.Find("AbCdEf12") is null,
                "public folder: removing a subscription deletes it");
        }

        private static void AutoStartEntryCases(string root)
        {
            string installed = "/usr/lib/megadownloader/MegaDownloader";
            Check(Platform.UnixPlatformServices.StableExecutable(installed) == "/usr/bin/megadownloader",
                "autostart: an installed Linux binary starts through /usr/bin/megadownloader");
            Check(Platform.UnixPlatformServices.StableExecutable("/home/u/apps/MegaDownloader") ==
                "/home/u/apps/MegaDownloader", "autostart: a development build keeps its own path");
            Check(Platform.UnixPlatformServices.StableExecutable("/opt/megadownloader/MegaDownloader") ==
                "/usr/bin/megadownloader", "autostart: an /opt install also uses the launcher");

            string linux = Platform.UnixPlatformServices.BuildAutoStartEntry(
                Platform.PlatformKind.Linux, installed, "--minimized");
            Check(linux.Contains("Exec=\"/usr/bin/megadownloader\" --minimized"),
                "autostart: the Linux entry runs the launcher with its arguments");
            Check(linux.Contains("X-GNOME-Autostart-enabled=true"),
                "autostart: the Linux entry is enabled for the desktop session");

            string mac = Platform.UnixPlatformServices.BuildAutoStartEntry(
                Platform.PlatformKind.MacOS, "/Applications/Mega Downloader.app/Contents/MacOS/MegaDownloader", "");
            Check(mac.Contains("<key>RunAtLoad</key><true/>"),
                "autostart: the macOS agent runs at login");
            Check(mac.Contains("/Applications/Mega Downloader.app/Contents/MacOS/MegaDownloader"),
                "autostart: the macOS agent names the bundle executable");
            Check(mac.Contains("<array><string>") && !mac.Contains("&amp;lt;"),
                "autostart: the macOS agent keeps one unescaped program argument");

            // launchd refuses a plist it cannot parse, and the failure only appears at
            // the next login, so the generated document is parsed here.
            var plist = new System.Xml.XmlDocument();
            plist.LoadXml(mac);
            System.Xml.XmlElement dictionary = plist.DocumentElement!["dict"];
            Check(dictionary is not null, "autostart: the macOS agent is a well-formed plist");
            Check(dictionary!["string"]?.InnerText == "com.megadownloader",
                "autostart: the macOS agent carries its label");

            string[] lines = linux.Split('\n');
            Check(lines[0].Trim() == "[Desktop Entry]", "autostart: the Linux entry starts with its group header");
            Check(lines.Any(line => line.Trim() == "Type=Application"), "autostart: the Linux entry is an application");
            Check(lines.Any(line => line.Trim() == "Name=Mega Downloader"), "autostart: the Linux entry has a name");
            Check(lines.Any(line => line.Trim() == "Exec=\"/usr/bin/megadownloader\" --minimized"),
                "autostart: the Linux entry keeps the launcher and its arguments");

            // Enable, re-enable and disable, the way the settings page drives it.
            string home = Path.Combine(root, "autostart-home");
            Directory.CreateDirectory(home);
            Platform.UnixPlatformServices.ApplyAutoStart(Platform.PlatformKind.Linux, home, true,
                installed, string.Empty);
            string file = Platform.UnixPlatformServices.AutoStartFile(Platform.PlatformKind.Linux, home);
            Check(Platform.UnixPlatformServices.IsAutoStartEnabled(Platform.PlatformKind.Linux, home) &&
                File.Exists(file), "autostart: enabling writes the login entry");
            Platform.UnixPlatformServices.ApplyAutoStart(Platform.PlatformKind.Linux, home, true,
                installed, string.Empty);
            Check(File.ReadAllText(file).Contains("Exec=\"/usr/bin/megadownloader\""),
                "autostart: enabling twice leaves one valid entry");
            Platform.UnixPlatformServices.ApplyAutoStart(Platform.PlatformKind.Linux, home, false,
                installed, string.Empty);
            Check(!Platform.UnixPlatformServices.IsAutoStartEnabled(Platform.PlatformKind.Linux, home),
                "autostart: disabling removes the login entry");
            Platform.UnixPlatformServices.ApplyAutoStart(Platform.PlatformKind.Linux, home, false,
                installed, string.Empty);
            Check(!File.Exists(file), "autostart: disabling twice is harmless");

            Platform.UnixPlatformServices.ApplyAutoStart(Platform.PlatformKind.MacOS, home, true,
                "/Applications/MegaDownloader.app/Contents/MacOS/MegaDownloader", string.Empty);
            string agent = Platform.UnixPlatformServices.AutoStartFile(Platform.PlatformKind.MacOS, home);
            Check(File.Exists(agent) &&
                agent.EndsWith("Library/LaunchAgents/com.megadownloader.plist".Replace('/', Path.DirectorySeparatorChar),
                    StringComparison.Ordinal),
                "autostart: the macOS agent lands in Library/LaunchAgents");
            Platform.UnixPlatformServices.ApplyAutoStart(Platform.PlatformKind.MacOS, home, false,
                "/Applications/MegaDownloader.app/Contents/MacOS/MegaDownloader", string.Empty);
            Check(!File.Exists(agent), "autostart: disabling removes the macOS agent");
        }

        private static void MegaAccountStoreCases(string root)
        {
            string folder = Path.Combine(root, "account-store");
            Directory.CreateDirectory(folder);
            var store = new
            {
                DefaultEmail = "one@example.com",
                Accounts = new object[]
                {
                    new { Email = "one@example.com", AliasName = "One", Session = "", QuotaRetryUtc = (DateTime?)null },
                    new { Email = "two@example.com", AliasName = "", Session = "", QuotaRetryUtc = (DateTime?)null }
                }
            };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(store);
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] protectedBytes;
            if (OperatingSystem.IsWindows())
            {
                // The WinForms 2.6.5 store used DPAPI with no entropy.
                protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
                    raw, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            }
            else
            {
                protectedBytes = System.Text.Encoding.UTF8.GetBytes(
                    Criptografia.EncryptString_DPAPI(Criptografia.ToSecureString(json)));
            }
            File.WriteAllBytes(Path.Combine(folder, "accounts.dat"), protectedBytes);

            var service = new MegaAccountService(folder);
            int result = service.RestoreAsync().GetAwaiter().GetResult();
            Check(result == 0, "account store: the saved account list restores");
            Check(service.DefaultEmail == "one@example.com", "account store: the saved default is kept");
            Check(service.GetAccounts().Count == 2, "account store: both accounts are read");
            Check(service.GetStatus("one@example.com").AliasName == "One", "account store: aliases are read");
            Check(!service.GetStatus("one@example.com").SignedIn,
                "account store: an empty session does not sign in");
        }

        private static void FicheroAccountXmlCases(string root)
        {
            string taskFolder = Path.Combine(root, "account-task");
            string key = new string('A', 43);
            Check(Fichero.GetMegaAccountLink("https://mega.nz/file/abcdefgh#" + key) ==
                "https://mega.nz/file/abcdefgh#" + key,
                "account link: a direct public file link is normalized");
            Check(Fichero.GetMegaAccountLink("https://mega.nz/folder/abcdefgh#" + key) is null,
                "account link: folder links are not account downloads");
            Check(Fichero.GetMegaAccountLink("https://mega.nz/file/abcdefgh#" + key + "=###n=extra") is null,
                "account link: encoded link payloads are rejected");
            Directory.CreateDirectory(taskFolder);

            var privateTask = new Fichero("https://mega.nz/file/abcdefgh#AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            {
                MegaAccountEmail = "user@example.com",
                MegaDriveNodeHandle = 42,
                MegaDriveWaitingForQuota = true,
                MegaAccountErrorCode = MegaAccountService.OverQuota,
                NombreFichero = "private.bin",
                RutaLocal = taskFolder,
                TamanoBytes = 10,
                DescripcionError = "quota",
                FailedByQuota = true,
                DescargaComenzada = true,
                EstadoDescarga = Estado.Erroneo
            };
            try
            {
                var document = new System.Xml.XmlDocument();
                System.Xml.XmlNode node = privateTask.GuardarXML(document, true);
                var loaded = new Fichero();
                try
                {
                    loaded.CargarXML(node);
                    Check(loaded.MegaAccountEmail == "user@example.com",
                        "account xml: the download identity is persisted");
                    Check(loaded.MegaDriveNodeHandle == 42, "account xml: a private node handle is persisted");
                    Check(loaded.MegaDriveWaitingForQuota, "account xml: the quota wait is persisted");
                    Check(loaded.MegaAccountErrorCode == MegaAccountService.OverQuota,
                        "account xml: the account error code is persisted");
                    Check(!loaded.LinkVisible, "account xml: private tasks hide their link");
                }
                finally
                {
                    loaded.Dispose();
                }
            }
            finally
            {
                privateTask.Dispose();
            }

            var publicTask = new Fichero("https://mega.nz/file/abcdefgh#AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            {
                MegaAccountEmail = "user@example.com",
                MegaAccountTransferEmail = "other@example.com",
                NombreFichero = "public.bin",
                RutaLocal = taskFolder,
                TamanoBytes = 5
            };
            try
            {
                var document = new System.Xml.XmlDocument();
                System.Xml.XmlNode node = publicTask.GuardarXML(document, true);
                var loaded = new Fichero();
                try
                {
                    loaded.CargarXML(node);
                    Check(loaded.EffectiveMegaAccountEmail == "other@example.com",
                        "account xml: a quota-fallback transfer identity is persisted");
                }
                finally
                {
                    loaded.Dispose();
                }
            }
            finally
            {
                publicTask.Dispose();
            }
        }

        private static void ThrottledAccountBudgetCases()
        {
            ThrottledStreamController controller = ThrottledStreamController.GetController();
            var first = new ThrottledStreamController.AccountDownloadBudget();
            var second = new ThrottledStreamController.AccountDownloadBudget();
            controller.SetMaxGlobalSpeed(1024 * 1024);
            controller.BeginAccountDownload(first);
            Check(first.BytesPerSecond == 1024 * 1024,
                "budget: one SDK transfer receives the full global limit");
            controller.BeginAccountDownload(second);
            Check(first.BytesPerSecond == 512 * 1024 && second.BytesPerSecond == 512 * 1024,
                "budget: two SDK transfers split the global limit");
            controller.EndAccountDownload(second);
            Check(first.BytesPerSecond == 1024 * 1024,
                "budget: releasing one transfer restores the other allocation");
            controller.EndAccountDownload(first);
            controller.SetMaxGlobalSpeed(0);
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
