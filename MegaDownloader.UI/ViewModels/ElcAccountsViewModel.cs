using MegaDownloader.UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// ELC account editor over <see cref="ELCAccountHelper"/>. Credentials are copied into
/// the helper, persisted back to <see cref="Configuracion.ELCAccounts"/> and saved to
/// Configuration.xml on every successful change.
/// </summary>
public partial class ElcAccountsViewModel : ViewModelBase, IDisposable
{
    private Configuracion _config;
    private readonly ELCAccountHelper _helper;
    private string? _editingUrl;

    [ObservableProperty]
    private ElcAccountRow? selectedAccount;

    [ObservableProperty]
    private string editorTitle = UiText.Get("New account");

    [ObservableProperty]
    private string alias = string.Empty;

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private string user = string.Empty;

    [ObservableProperty]
    private string key = string.Empty;

    [ObservableProperty]
    private bool showKey;

    [ObservableProperty]
    private bool isDefault;

    [ObservableProperty]
    private bool hasSelection;

    [ObservableProperty]
    private string importText = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private bool statusIsError;

    public ElcAccountsViewModel(DownloadEngineService engine)
    {
        _config = engine.Config;
        _helper = new ELCAccountHelper(ref _config);
        Reload();
    }

    public ObservableCollection<ElcAccountRow> Accounts { get; } = new();

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    public event Action? CloseRequested;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnSelectedAccountChanged(ElcAccountRow? value)
    {
        HasSelection = value is not null;
        if (value is null)
            return;

        var account = _helper.GetAccountDetailsByURL(value.Url);
        if (account is null)
            return;

        _editingUrl = account.URL;
        EditorTitle = UiText.Get("Edit account");
        Alias = account.Alias ?? string.Empty;
        Url = account.URL ?? string.Empty;
        User = SafePlain(account.User);
        Key = SafePlain(account.Key);
        IsDefault = account.DefaultAccount;
        SetStatus(string.Empty, false);
    }

    [RelayCommand]
    private void New()
    {
        SelectedAccount = null;
        _editingUrl = null;
        EditorTitle = UiText.Get("New account");
        Alias = string.Empty;
        Url = string.Empty;
        User = string.Empty;
        Key = string.Empty;
        ShowKey = false;
        IsDefault = false;
        SetStatus(string.Empty, false);
    }

    [RelayCommand]
    private void Save()
    {
        string accountAlias = Alias.Trim();
        string accountUrl = Url.Trim();
        string accountUser = User.Trim();
        string accountKey = Key.Trim();
        if (accountAlias.Length == 0 || accountUrl.Length == 0 || accountUser.Length == 0 || accountKey.Length == 0)
        {
            SetStatus(UiText.Get("Alias, URL, user and API key are all required."), true);
            return;
        }

        var account = new ELCAccountHelper.Account
        {
            Alias = accountAlias,
            URL = accountUrl,
            User = Criptografia.ToSecureString(accountUser),
            Key = Criptografia.ToSecureString(accountKey),
            DefaultAccount = IsDefault
        };

        bool saved = _editingUrl is null
            ? _helper.AddNewAccount(account)
            : _helper.ModifyAccountDetails(_editingUrl, account);
        if (!saved)
        {
            DisposeAccount(account);
            SetStatus(UiText.Get("An account with the same alias or URL already exists."), true);
            return;
        }

        if (!Persist())
            return;
        Reload();
        SetStatus(UiText.Get("Account saved."), false);
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedAccount is null)
            return;

        if (!_helper.DeleteAccount(SelectedAccount.Url))
        {
            SetStatus(UiText.Get("The account could not be removed."), true);
            return;
        }

        if (!Persist())
            return;
        New();
        Reload();
        SetStatus(UiText.Get("Account deleted."), false);
    }

    [RelayCommand]
    private void Import()
    {
        var lines = new List<string>();
        foreach (string line in ImportText.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }

        if (!_helper.ImportConfig(lines))
        {
            SetStatus(UiText.Get("No valid account lines found. Expected url:nick:apikey[:alias]."), true);
            return;
        }

        if (!Persist())
            return;
        Reload();
        ImportText = string.Empty;
        SetStatus(UiText.Get("Accounts imported."), false);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private void Reload()
    {
        string? selectedUrl = SelectedAccount?.Url;
        Accounts.Clear();
        foreach (ELCAccountHelper.Account account in _helper.GetAccounts())
            Accounts.Add(new ElcAccountRow(account.Alias, account.URL, account.DefaultAccount));

        SelectedAccount = selectedUrl is null
            ? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault(row => string.Equals(row.Url, selectedUrl, StringComparison.OrdinalIgnoreCase)) ??
              Accounts.FirstOrDefault();
    }

    /// <summary>Writes the accounts to disk. Returns false when the file could not be
    /// written; the accounts are then only in memory and the caller must say so.</summary>
    private bool Persist()
    {
        _helper.SaveToConfig(ref _config);
        _config.GuardarXML(true);
        if (_config.ErrorConfig != Configuracion.ErrorConfigClass.SinErrores)
        {
            // The accounts are in memory but not on disk. Saying "saved" here would
            // lose them on the next restart without a word of warning.
            SetStatus(UiText.Format("The accounts could not be written to disk: {0}.", _config.ErrorConfig), true);
            return false;
        }
        return true;
    }

    private static string SafePlain(System.Security.SecureString value)
    {
        try
        {
            return value is null ? string.Empty : Criptografia.ToInsecureString(value);
        }
        catch (ObjectDisposedException)
        {
            return string.Empty;
        }
    }

    private static void DisposeAccount(ELCAccountHelper.Account account)
    {
        account.User?.Dispose();
        account.Key?.Dispose();
    }

    private void SetStatus(string text, bool isError)
    {
        Status = text;
        StatusIsError = isError;
    }

    public void Dispose() => _helper.Dispose();
}

public sealed class ElcAccountRow
{
    public ElcAccountRow(string alias, string url, bool isDefault)
    {
        Alias = string.IsNullOrWhiteSpace(alias) ? url : alias;
        Url = url;
        IsDefault = isDefault;
    }

    public string Alias { get; }

    public string Url { get; }

    public bool IsDefault { get; }

    public string Display => IsDefault ? UiText.Format("{0} (default)", Alias) : Alias;
}
