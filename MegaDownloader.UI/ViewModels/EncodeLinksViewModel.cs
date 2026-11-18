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
    private string outputText = string.Empty;

    [ObservableProperty]
    private string status = "Paste links, then press Encode.";

    public Func<string, Task>? CopyAsync { get; set; }

    public event Action? CloseRequested;

    [RelayCommand]
    private void Encode()
    {
        string text = InputText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            Status = "Paste at least one link.";
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
            ? $"Encoded {encoded} link(s); {skipped} sub-folder/sub-file link(s) kept unchanged."
            : $"Encoded {encoded} link(s).";
    }

    [RelayCommand]
    private async Task CopyResultAsync()
    {
        if (CopyAsync is null || string.IsNullOrEmpty(OutputText))
            return;
        await CopyAsync(OutputText);
        Status = "Encoded links copied to the clipboard.";
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
