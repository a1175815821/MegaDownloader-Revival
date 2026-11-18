using System;
using System.Collections.Generic;

namespace MegaDownloader
{

    /// <summary>Selection and totals independent of native grid rows; O(1) per checkbox.</summary>
    public sealed class PreflightSelection
    {
        public enum SelectionOperation
        {
            SelectAll,
            Clear,
            Invert
        }

        public readonly List<BatchLinkPreflight.Entry> Entries;
        public readonly List<BatchLinkPreflight.Entry> Visible = new List<BatchLinkPreflight.Entry>();
        public int SelectedCount { get; private set; }
        public decimal SelectedBytes { get; private set; }
        public int UnknownSizes { get; private set; }
        public int DuplicateCount { get; private set; }
        public int InvalidCount { get; private set; }
        public int UnavailableCount { get; private set; }
        public event SelectionChangedEventHandler SelectionChanged;

        public delegate void SelectionChangedEventHandler(BatchLinkPreflight.Entry entry, int delta);

        public PreflightSelection(List<BatchLinkPreflight.Entry> input)
        {
            Entries = input;
            foreach (BatchLinkPreflight.Entry entry in Entries)
            {
                switch (entry.State)
                {
                    case BatchLinkPreflight.LinkState.AlreadyQueued:
                    case BatchLinkPreflight.LinkState.Duplicate:
                        DuplicateCount += 1;
                        break;
                    case BatchLinkPreflight.LinkState.Invalid:
                        InvalidCount += 1;
                        break;
                    case BatchLinkPreflight.LinkState.Unavailable:
                        UnavailableCount += 1;
                        break;
                }
                bool selected = entry.Selected && entry.CanAdd;
                entry.Selected = false;
                SetSelected(entry, selected);
            }
            Filter("", "");
        }

        public void Filter(string text, string category)
        {
            Visible.Clear();
            foreach (BatchLinkPreflight.Entry entry in Entries)
            {
                if (text.Length > 0 && (entry.Name ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0 && (entry.Source is null || (entry.Source.FolderName ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0 && (entry.Source.Path ?? "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0))
                    continue;
                if (category.Length > 0 && (entry.Category ?? "") != (category ?? ""))
                    continue;
                Visible.Add(entry);
            }
        }

        public void SetSelected(BatchLinkPreflight.Entry entry, bool selected)
        {
            selected = selected && entry.CanAdd;
            if (entry.Selected == selected)
                return;
            int delta = selected ? 1 : -1;
            entry.Selected = selected;
            SelectedCount += delta;
            if (entry.SizeBytes.HasValue)
            {
                SelectedBytes += entry.SizeBytes.Value * (decimal)delta;
            }
            else
            {
                UnknownSizes += delta;
            }
            SelectionChanged?.Invoke(entry, delta);
        }

        public void Apply(SelectionOperation operation, bool visibleOnly = true)
        {
            foreach (BatchLinkPreflight.Entry entry in visibleOnly ? Visible : Entries)
                SetSelected(entry, operation == SelectionOperation.Invert ? !entry.Selected : operation == SelectionOperation.SelectAll);
        }
    }
}