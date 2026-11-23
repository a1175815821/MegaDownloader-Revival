using System;
using System.Collections.Generic;
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
            string root = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "mega-core-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                PreflightTreeCases();
                PreflightSelectionCases();
                DestinationPreviewCases();
                PreflightSessionCacheCases();

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
    }
}
