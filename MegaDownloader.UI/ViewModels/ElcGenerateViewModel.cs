using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// ELC link generation (WinForms ELCForm): pick an account, paste links, call the ELC
/// server through <see cref="ServerEncoderLinkHelper.ServerEncode"/> and show/copy/save
/// the resulting <c>mega://elc?</c> links.
/// </summary>
public partial class ElcGenerateViewModel : ViewModelBase
{
    private readonly DownloadEngineService _engine;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private ElcAccountOption? selectedAccount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string linksText = string.Empty;

    [ObservableProperty]
    private bool statusIsError;

    [ObservableProperty]
    private bool onePerLink;

    [ObservableProperty]
    private string resultText = string.Empty;

    [ObservableProperty]
    private string status = "Choose an account and paste links.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private bool isBusy;

    public ElcGenerateViewModel(DownloadEngineService engine)
    {
        _engine = engine;
        ReloadAccounts();
    }

    public ObservableCollection<ElcAccountOption> Accounts { get; } = new();

    public Func<string, Task>? CopyAsync { get; set; }

    public Func<string, Task>? SaveFileAsync { get; set; }

    public Func<Task>? OpenAccountsAsync { get; set; }

    public event Action? CloseRequested;

    public bool CanGenerate => !IsBusy && SelectedAccount is not null && !string.IsNullOrWhiteSpace(LinksText);

    partial void OnLinksTextChanged(string value) => OnPropertyChanged(nameof(CanGenerate));

    public void ReloadAccounts()
    {
        string? selectedUrl = SelectedAccount?.Url;
        Accounts.Clear();
        var config = _engine.Config;
        using (var helper = new ELCAccountHelper(ref config))
        {
            foreach (ELCAccountHelper.Account account in helper.GetAccounts())
                Accounts.Add(new ElcAccountOption(account.URL, account.Alias, account.DefaultAccount));
        }

        SelectedAccount = selectedUrl is null
            ? Accounts.FirstOrDefault(option => option.IsDefault) ?? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault(option => string.Equals(option.Url, selectedUrl, StringComparison.OrdinalIgnoreCase)) ??
              Accounts.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (SelectedAccount is null)
            return;

        var urls = URLExtractor.ExtraerURLs(LinksText);
        if (urls.Count == 0)
        {
            SetStatus("No Mega links found in the text.", true);
            return;
        }

        IsBusy = true;
        SetStatus("Contacting the ELC server…", false);
        string accountUrl = SelectedAccount.Url;
        bool onePer = OnePerLink;
        string input = LinksText;
        try
        {
            string result = await Task.Run(() => Generate(accountUrl, urls, onePer, input));
            ResultText = result;
            SetStatus($"Generated {urls.Count} link(s).", false);
        }
        catch (Exception ex)
        {
            Log.WriteError("ELC generation failed: " + Log.SafeException(ex));
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyResultAsync()
    {
        if (CopyAsync is null || string.IsNullOrEmpty(ResultText))
            return;
        await CopyAsync(ResultText);
        SetStatus("Result copied to the clipboard.", false);
    }

    [RelayCommand]
    private async Task SaveResultAsync()
    {
        if (SaveFileAsync is null || string.IsNullOrEmpty(ResultText))
            return;
        await SaveFileAsync(ResultText);
    }

    [RelayCommand]
    private async Task ManageAccountsAsync()
    {
        if (OpenAccountsAsync is null)
            return;
        await OpenAccountsAsync();
        ReloadAccounts();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private string Generate(string accountUrl, List<string> urls, bool onePerLinkMode, string input)
    {
        var config = _engine.Config;
        if (onePerLinkMode)
        {
            string text = input;
            foreach (string url in urls)
            {
                var single = new List<ServerEncoderLinkHelper.MegaLink> { ToMegaLink(url) };
                string encoded = ServerEncoderLinkHelper.ServerEncode(accountUrl, single, ref config);
                if (string.IsNullOrEmpty(encoded))
                    throw new ApplicationException("The server returned an empty ELC.");
                text = text.Replace(url, "mega://elc?" + encoded, StringComparison.Ordinal);
            }
            return text;
        }

        var links = new List<ServerEncoderLinkHelper.MegaLink>();
        foreach (string url in urls)
            links.Add(ToMegaLink(url));
        string result = ServerEncoderLinkHelper.ServerEncode(accountUrl, links, ref config);
        if (string.IsNullOrEmpty(result))
            throw new ApplicationException("The server returned an empty ELC.");
        return "mega://elc?" + result;
    }

    private static ServerEncoderLinkHelper.MegaLink ToMegaLink(string url)
    {
        return new ServerEncoderLinkHelper.MegaLink
        {
            FileID = URLExtractor.ExtraerFileID(url),
            FileKey = URLExtractor.ExtraerFileKey(url),
            MegaFolder = URLExtractor.IsMegaFolder(url),
            SubFolderID = URLExtractor.ExtraerSubFolderID(url),
            SubFileID = URLExtractor.ExtraerSubFileID(url)
        };
    }

    private void SetStatus(string text, bool isError)
    {
        Status = text;
        StatusIsError = isError;
    }
}

public sealed class ElcAccountOption
{
    public ElcAccountOption(string url, string alias, bool isDefault)
    {
        Url = url;
        Alias = string.IsNullOrWhiteSpace(alias) ? url : alias;
        IsDefault = isDefault;
    }

    public string Url { get; }

    public string Alias { get; }

    public bool IsDefault { get; }

    public override string ToString() => IsDefault ? Alias + " (default)" : Alias;
}
