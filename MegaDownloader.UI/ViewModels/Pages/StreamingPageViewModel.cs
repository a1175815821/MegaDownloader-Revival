using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

/// <summary>Native management for the streaming library (Core StreamingLibraryManager).</summary>
public partial class StreamingPageViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly DownloadEngineService _engine;
    private string? _editingId;

    /// <summary>Set by the view so playback can open the stream URL.</summary>
    public Func<string, Task>? OpenStreamAsync { get; set; }

    [ObservableProperty]
    private StreamingRowViewModel? selectedItem;

    [ObservableProperty]
    private string editorTitle = "New entry";

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    private string poster = string.Empty;

    [ObservableProperty]
    private string link = string.Empty;

    [ObservableProperty]
    private bool linkVisible = true;

    [ObservableProperty]
    private string imdb = string.Empty;

    [ObservableProperty]
    private string allocine = string.Empty;

    [ObservableProperty]
    private string filmaffinity = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private bool statusIsError;

    [ObservableProperty]
    private bool hasSelection;

    public StreamingPageViewModel(IDialogService dialogs, DownloadEngineService engine)
    {
        _dialogs = dialogs;
        _engine = engine;
        Refresh();
    }

    public ObservableCollection<StreamingRowViewModel> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnSelectedItemChanged(StreamingRowViewModel? value)
    {
        HasSelection = value is not null;
        if (value is null)
            return;

        _editingId = value.Id;
        EditorTitle = "Edit entry";
        Name = value.Element.Name ?? string.Empty;
        Description = value.Element.Description ?? string.Empty;
        Comments = value.Element.Comments ?? string.Empty;
        Poster = value.Element.Poster ?? string.Empty;
        Link = SafeLink(value.Element);
        LinkVisible = value.Element.LinkVisible;
        Imdb = value.Element.IMDB ?? string.Empty;
        Allocine = value.Element.Allocine ?? string.Empty;
        Filmaffinity = value.Element.Filmaffinity ?? string.Empty;
    }

    [RelayCommand]
    private void New()
    {
        SelectedItem = null;
        _editingId = null;
        EditorTitle = "New entry";
        Name = string.Empty;
        Description = string.Empty;
        Comments = string.Empty;
        Poster = string.Empty;
        Link = string.Empty;
        LinkVisible = true;
        Imdb = string.Empty;
        Allocine = string.Empty;
        Filmaffinity = string.Empty;
        SetStatus(string.Empty, false);
    }

    [RelayCommand]
    private void Refresh()
    {
        string? selectedId = SelectedItem?.Id;
        Items.Clear();
        try
        {
            foreach (LibraryElement element in StreamingLibraryManager.GetElements())
                Items.Add(new StreamingRowViewModel(element));
        }
        catch (Exception ex)
        {
            SetStatus("Could not read the streaming library: " + ex.Message, true);
        }

        OnPropertyChanged(nameof(HasItems));
        SelectedItem = selectedId is null
            ? null
            : Items.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.Ordinal));
    }

    [RelayCommand]
    private void Save()
    {
        string entryName = Name.Trim();
        if (entryName.Length == 0)
        {
            SetStatus("Enter a name for the entry.", true);
            return;
        }

        try
        {
            if (_editingId is null)
            {
                LibraryElement created = StreamingLibraryManager.AddElement(
                    entryName,
                    Description,
                    Comments,
                    Poster.Trim(),
                    Link.Trim(),
                    LinkVisible,
                    Imdb.Trim(),
                    Allocine.Trim(),
                    Filmaffinity.Trim());
                _editingId = created?.ID;
                SetStatus("Entry added.", false);
            }
            else
            {
                LibraryElement updated = StreamingLibraryManager.ModifyElement(
                    _editingId,
                    entryName,
                    Description,
                    Comments,
                    Poster.Trim(),
                    Link.Trim(),
                    LinkVisible,
                    Imdb.Trim(),
                    Allocine.Trim(),
                    Filmaffinity.Trim());
                if (updated is null)
                {
                    SetStatus("The entry no longer exists. Refresh the list.", true);
                    return;
                }
                SetStatus("Entry saved.", false);
            }

            Refresh();
        }
        catch (Exception ex)
        {
            Log.WriteError("Saving a streaming library entry failed: " + Log.SafeException(ex));
            SetStatus("Could not save the entry: " + ex.Message, true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem is null)
            return;

        string entryName = SelectedItem.Name;
        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete entry",
            $"Remove “{entryName}” from the streaming library?",
            "Delete",
            "Cancel");
        if (!confirmed)
            return;

        try
        {
            StreamingLibraryManager.RemoveElement(SelectedItem.Id);
            New();
            Refresh();
            SetStatus("Entry deleted.", false);
        }
        catch (Exception ex)
        {
            Log.WriteError("Deleting a streaming library entry failed: " + Log.SafeException(ex));
            SetStatus("Could not delete the entry: " + ex.Message, true);
        }
    }

    /// <summary>
    /// Plays the selected entry: the MEGA link is resolved to a local streaming URL and
    /// handed to the player. The WinForms shell offers the same action.
    /// </summary>
    [RelayCommand]
    private async Task PlayAsync()
    {
        if (SelectedItem is null)
            return;

        string link = SafeLink(SelectedItem.Element);
        if (string.IsNullOrWhiteSpace(link))
        {
            SetStatus("This entry has no link to play.", true);
            return;
        }

        var config = _engine.Config;
        if (!config.ServidorStreamingActivo)
        {
            SetStatus("Enable the streaming server in Settings → Streaming to play this entry.", true);
            return;
        }

        SetStatus("Resolving the stream…", false);
        string streamUrl = string.Empty;
        try
        {
            // The resolution talks to the MEGA API, so it must not block the UI.
            streamUrl = await Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(SelectedItem?.Id))
                    return StreamingHelper.CreateStreamingLinkFromLibrary(SelectedItem.Id, link, ref config);
                return StreamingHelper.CreateStreamingLink(link, config.ServidorStreamingPuerto, ref config);
            });
        }
        catch (Exception ex)
        {
            Log.WriteError("Resolving the streaming URL failed: " + Log.SafeException(ex));
            SetStatus("The stream could not be resolved: " + DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
            return;
        }

        if (string.IsNullOrEmpty(streamUrl))
        {
            SetStatus("The stream could not be resolved for this link.", true);
            return;
        }

        if (OpenStreamAsync is null)
        {
            SetStatus(streamUrl, false);
            return;
        }

        try
        {
            await OpenStreamAsync(streamUrl);
            SetStatus("Playing " + streamUrl, false);
        }
        catch (Exception ex)
        {
            Log.WriteError("Starting the player failed: " + Log.SafeException(ex));
            SetStatus("The player could not be started: " + DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
        }
    }

    private static string SafeLink(LibraryElement element)
    {
        try
        {
            return Criptografia.ToInsecureString(element.Link) ?? string.Empty;
        }
        catch (ObjectDisposedException)
        {
            return string.Empty;
        }
    }

    private void SetStatus(string text, bool isError)
    {
        Status = text;
        StatusIsError = isError;
    }
}

public sealed class StreamingRowViewModel
{
    public StreamingRowViewModel(LibraryElement element)
    {
        Element = element;
        Id = element.ID ?? string.Empty;
        Name = string.IsNullOrWhiteSpace(element.Name) ? "(untitled)" : element.Name;
        Subtitle = string.IsNullOrWhiteSpace(element.Description)
            ? element.LastModification.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : element.Description;
        LinkText = element.LinkVisible ? "Link visible" : "Link hidden";
    }

    public LibraryElement Element { get; }

    public string Id { get; }

    public string Name { get; }

    public string Subtitle { get; }

    public string LinkText { get; }
}
