using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MegaDownloader
{

    /// <summary>Folder hierarchy with a virtual projection; collapsing a folder never changes its selection.</summary>
    public sealed class PreflightTree
    {
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

            public Node(BatchLinkPreflight.Entry @file = null)
            {
                Entry = @file;
                if (@file is null)
                {
                    Folders = new Dictionary<string, Node>(StringComparer.Ordinal);
                    FolderChildren = new List<Node>();
                    FileChildren = new List<Node>();
                }
            }

            public bool IsFolder
            {
                get
                {
                    return Entry is null;
                }
            }

            public CheckState CheckState
            {
                get
                {
                    if (EligibleCount == 0 || SelectedCount == 0)
                        return CheckState.Unchecked;
                    return SelectedCount == EligibleCount ? CheckState.Checked : CheckState.Indeterminate;
                }
            }
        }

        private readonly Node root = new Node() { Depth = -1 };
        private readonly List<Node> nodes = new List<Node>();
        private readonly Dictionary<BatchLinkPreflight.Entry, Node> leaves = new Dictionary<BatchLinkPreflight.Entry, Node>();
        private readonly PreflightSelection selection;
        private bool filtered;
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
                var leaf = leaves[entry];
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
                    {
                        node.SizeBytes += entry.SizeBytes.Value;
                    }
                    else
                    {
                        node.UnknownSizes += 1;
                    }
                    node = node.Parent;
                }
            }
            RebuildRows();
        }

        private void SelectionChanged(BatchLinkPreflight.Entry entry, int delta)
        {
            Node node = null;
            if (!leaves.TryGetValue(entry, out node) || !node.Matches)
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
            {
                node.SearchCollapsed = !node.SearchCollapsed;
            }
            else
            {
                node.Expanded = !node.Expanded;
            }
            RebuildRows();
        }

        public void ExpandAll(bool expand)
        {
            foreach (Node node in nodes)
            {
                if (!node.IsFolder)
                    continue;
                if (filtered)
                {
                    node.SearchCollapsed = !expand;
                }
                else
                {
                    node.Expanded = expand;
                }
            }
            RebuildRows();
        }

        public bool IsExpanded(Node node)
        {
            return filtered ? !node.SearchCollapsed : node.Expanded;
        }

        private void RebuildRows()
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

        public void Apply(IEnumerable<Node> targets, PreflightSelection.SelectionOperation operation)
        {
            // A folder and a highlighted child must never invert the same file twice.
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
    }
}