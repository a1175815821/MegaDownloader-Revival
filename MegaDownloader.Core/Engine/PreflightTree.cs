using System;
using System.Collections.Generic;

namespace MegaDownloader
{

    /// <summary>
    /// Folder hierarchy over a <see cref="PreflightSelection"/> with a virtual row
    /// projection. Collapsing or filtering never changes what is selected; folder
    /// tri-state is always derived from the leaf entries underneath it.
    /// </summary>
    public sealed class PreflightTree
    {
        public enum TriState
        {
            Unchecked,
            Checked,
            Indeterminate
        }

        public sealed class Node
        {
            public string Name;
            public Node Parent;
            public BatchLinkPreflight.Entry Entry;
            public bool Expanded;
            public int Depth;
            public int MatchCount;
            public int EligibleCount;
            public int SelectedCount;
            public decimal SizeBytes;
            public int UnknownSizes;
            internal bool Matches;
            internal bool SearchCollapsed;
            internal readonly Dictionary<string, Node> Folders;
            internal readonly List<Node> FolderChildren;
            internal readonly List<Node> FileChildren;

            public Node(BatchLinkPreflight.Entry file = null)
            {
                Entry = file;
                if (file is null)
                {
                    Folders = new Dictionary<string, Node>(StringComparer.Ordinal);
                    FolderChildren = new List<Node>();
                    FileChildren = new List<Node>();
                }
            }

            public bool IsFolder => Entry is null;

            public TriState CheckState
            {
                get
                {
                    if (EligibleCount == 0 || SelectedCount == 0)
                        return TriState.Unchecked;
                    return SelectedCount == EligibleCount ? TriState.Checked : TriState.Indeterminate;
                }
            }
        }

        private readonly Node root = new Node() { Depth = -1 };
        private readonly List<Node> nodes = new List<Node>();
        private readonly Dictionary<BatchLinkPreflight.Entry, Node> leaves = new Dictionary<BatchLinkPreflight.Entry, Node>();
        private readonly PreflightSelection selection;
        private bool filtered;

        /// <summary>Visible rows in display order (the list the UI binds to).</summary>
        public readonly List<Node> Rows = new List<Node>();

        public PreflightTree(PreflightSelection model)
        {
            selection = model;
            foreach (BatchLinkPreflight.Entry entry in selection.Entries)
            {
                var parent = root;
                var source = entry.Source;
                if (source is not null)
                {
                    if (!string.IsNullOrEmpty(source.FolderName))
                    {
                        string groupKey = "share:" + (source.PreflightOrigin ?? "") + '\0' + source.FolderName;
                        parent = Folder(parent, groupKey, source.FolderName);
                    }
                    foreach (string segment in (source.Path ?? "").Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
                        parent = Folder(parent, "path:" + segment, segment);
                }
                var leaf = new Node(entry) { Name = entry.Name, Parent = parent, Depth = parent.Depth + 1 };
                parent.FileChildren.Add(leaf);
                nodes.Add(leaf);
                leaves[entry] = leaf;
            }
            selection.SelectionChanged += SelectionChanged;
            Filter(false);
        }

        public Node Root => root;

        public bool IsFiltered => filtered;

        private Node Folder(Node parent, string key, string name)
        {
            Node result = null;
            if (!parent.Folders.TryGetValue(key, out result))
            {
                result = new Node() { Name = name, Parent = parent, Depth = parent.Depth + 1 };
                parent.Folders.Add(key, result);
                parent.FolderChildren.Add(result);
                nodes.Add(result);
            }
            return result;
        }

        /// <summary>Recomputes match and selection counts from the selection's visible
        /// entries and rebuilds <see cref="Rows"/>. Call after the selection filter
        /// changed.</summary>
        public void Filter(bool isFiltered)
        {
            filtered = isFiltered;
            foreach (Node node in nodes)
            {
                node.SearchCollapsed = false;
                node.Matches = false;
                node.MatchCount = 0;
                node.EligibleCount = 0;
                node.SelectedCount = 0;
                node.SizeBytes = 0m;
                node.UnknownSizes = 0;
            }
            foreach (BatchLinkPreflight.Entry entry in selection.Visible)
            {
                if (!leaves.TryGetValue(entry, out Node leaf))
                    continue;
                leaf.Matches = true;
                var node = leaf;
                while (!ReferenceEquals(node, root))
                {
                    node.MatchCount += 1;
                    if (entry.CanAdd)
                        node.EligibleCount += 1;
                    if (entry.Selected)
                        node.SelectedCount += 1;
                    if (entry.SizeBytes.HasValue)
                        node.SizeBytes += entry.SizeBytes.Value;
                    else
                        node.UnknownSizes += 1;
                    node = node.Parent;
                }
            }
            RebuildRows();
        }

        private void SelectionChanged(BatchLinkPreflight.Entry entry, int delta)
        {
            if (!leaves.TryGetValue(entry, out Node node) || !node.Matches)
                return;
            while (!ReferenceEquals(node, root))
            {
                node.SelectedCount += delta;
                node = node.Parent;
            }
        }

        public void Toggle(Node node)
        {
            if (node is null || !node.IsFolder)
                return;
            if (filtered)
                node.SearchCollapsed = !node.SearchCollapsed;
            else
                node.Expanded = !node.Expanded;
            RebuildRows();
        }

        public void ExpandAll(bool expand)
        {
            foreach (Node node in nodes)
            {
                if (!node.IsFolder)
                    continue;
                if (filtered)
                    node.SearchCollapsed = !expand;
                else
                    node.Expanded = expand;
            }
            RebuildRows();
        }

        public bool IsExpanded(Node node) => filtered ? !node.SearchCollapsed : node.Expanded;

        /// <summary>Row projection. Rows are rebuilt, but the Node objects (and therefore
        /// expansion and selection state) are stable.</summary>
        public void RebuildRows()
        {
            Rows.Clear();
            var pending = new Stack<Node>();
            PushChildren(root, pending);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node.MatchCount == 0)
                    continue;
                Rows.Add(node);
                if (node.IsFolder && IsExpanded(node))
                    PushChildren(node, pending);
            }
        }

        private static void PushChildren(Node parent, Stack<Node> pending)
        {
            for (int index = parent.FileChildren.Count - 1; index >= 0; index -= 1)
                pending.Push(parent.FileChildren[index]);
            for (int index = parent.FolderChildren.Count - 1; index >= 0; index -= 1)
                pending.Push(parent.FolderChildren[index]);
        }

        /// <summary>Applies an operation to the given nodes (folders expand to their
        /// leaves). A folder and one of its highlighted descendants never touch the
        /// same file twice.</summary>
        public void Apply(IEnumerable<Node> targets, PreflightSelection.SelectionOperation operation)
        {
            var highlighted = new HashSet<Node>(targets);
            var pending = new Stack<Node>();
            foreach (Node node in highlighted)
            {
                var ancestor = node.Parent;
                while (ancestor is not null && !highlighted.Contains(ancestor))
                    ancestor = ancestor.Parent;
                if (ancestor is null)
                    pending.Push(node);
            }
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node.MatchCount == 0)
                    continue;
                if (node.IsFolder)
                {
                    PushChildren(node, pending);
                }
                else if (node.Matches)
                {
                    var entry = node.Entry;
                    selection.SetSelected(entry, operation == PreflightSelection.SelectionOperation.Invert ? !entry.Selected : operation == PreflightSelection.SelectionOperation.SelectAll);
                }
            }
        }

        /// <summary>Every leaf entry below a node, whether or not it is currently visible.</summary>
        public IEnumerable<BatchLinkPreflight.Entry> LeavesUnder(Node node)
        {
            foreach (Node leaf in NodesUnder(node))
                yield return leaf.Entry;
        }

        /// <summary>Every leaf node below a node, visible or not.</summary>
        public IEnumerable<Node> NodesUnder(Node node)
        {
            var pending = new Stack<Node>();
            pending.Push(node);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current.IsFolder)
                {
                    for (int index = current.FileChildren.Count - 1; index >= 0; index -= 1)
                        pending.Push(current.FileChildren[index]);
                    for (int index = current.FolderChildren.Count - 1; index >= 0; index -= 1)
                        pending.Push(current.FolderChildren[index]);
                }
                else
                {
                    yield return current;
                }
            }
        }

        /// <summary>The leaf node that owns an entry, or null when it is not in this tree.</summary>
        public Node LeafNode(BatchLinkPreflight.Entry entry) =>
            entry is not null && leaves.TryGetValue(entry, out Node node) ? node : null;
    }
}
