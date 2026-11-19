using MegaDownloader.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// Offline link encoder (WinForms EncodeLinksForm): rewrites official Mega links in the
/// pasted text as their encoded <c>mega://enc</c> form. Sub-folder/sub-file links keep
/// their original text because the encoded form cannot carry that scope.
/// </summary>
public partial class EncodeLinksViewModel : ViewModelBase
{
    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyResultCommand))]
    private string outputText = string.Empty;

    [ObservableProperty]
    private string status = UiText.Get("Paste links, then press Encode.");

    public Func<string, Task>? CopyAsync { get; set; }

    public event Action? CloseRequested;

    [RelayCommand]
    private void Encode()
    {
        string text = InputText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            Status = UiText.Get("Paste at least one link.");
            return;
        }

        int encoded = 0;
        int skipped = 0;
        foreach (string url in URLExtractor.ExtraerSoloURLsOficiales(text))
        {
            string fileId = URLExtractor.ExtraerFileID(url);
            string fileKey = URLExtractor.ExtraerFileKey(url);
            if (string.IsNullOrEmpty(fileId))
                continue;
            if (!string.IsNullOrEmpty(URLExtractor.ExtraerSubFolderID(url)) ||
                !string.IsNullOrEmpty(URLExtractor.ExtraerSubFileID(url)))
            {
                skipped++;
                continue;
            }

            string encodedUrl = URLExtractor.GenerateEncodedURILink(
                fileId,
                fileKey,
                URLExtractor.IsMegaFolder(url),
                false);
            if (string.Equals(encodedUrl, url, StringComparison.Ordinal))
                continue;
            text = text.Replace(url, encodedUrl, StringComparison.Ordinal);
            encoded++;
        }

        OutputText = text;
        Status = skipped > 0
            ? UiText.Format("Encoded {0} link(s); {1} sub-folder/sub-file link(s) kept unchanged.", encoded, skipped)
            : UiText.Format("Encoded {0} link(s).", encoded);
    }

    private bool CanCopyResult => !string.IsNullOrEmpty(OutputText);

    [RelayCommand(CanExecute = nameof(CanCopyResult))]
    private async Task CopyResultAsync()
    {
        if (CopyAsync is null || string.IsNullOrEmpty(OutputText))
            return;
        try
        {
            await CopyAsync(OutputText);
            Status = UiText.Get("Encoded links copied to the clipboard.");
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Copying encoded links failed: " + Log.SafeException(ex));
            Status = UiText.Get("Could not copy the result to the clipboard.");
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
