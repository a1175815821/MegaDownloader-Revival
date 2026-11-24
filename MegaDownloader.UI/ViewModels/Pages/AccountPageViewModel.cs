using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels.Pages;

/// <summary>
/// MEGA account roster and sign-in. One account can be the default for new public
/// file downloads; remembered sessions restore automatically at launch.
/// </summary>
public partial class AccountPageViewModel : ViewModelBase, IDisposable
{
    private readonly MegaAccountService _accounts;
    private readonly IDialogService _dialogs;
    private bool _disposed;
    private bool _refreshingSelection;
    private bool _manualLoginMode;

    [ObservableProperty]
    private ObservableCollection<AccountRowViewModel> accounts = new();

    [ObservableProperty]
    private AccountRowViewModel? selectedAccount;

    [ObservableProperty]
    private bool hasAccounts;

    [ObservableProperty]
    private bool isLoginMode;

    [ObservableProperty]
    private bool loginEmailReadOnly;

    [ObservableProperty]
    private string loginEmail = string.Empty;

    [ObservableProperty]
    private string loginPassword = string.Empty;

    [ObservableProperty]
    private string loginAlias = string.Empty;

    [ObservableProperty]
    private bool loginRemember;

    [ObservableProperty]
    private string mfaCode = string.Empty;

    [ObservableProperty]
    private bool showMfa;

    [ObservableProperty]
    private bool isWorking;

    [ObservableProperty]
    private string message = string.Empty;

    [ObservableProperty]
    private string notice = string.Empty;

    [ObservableProperty]
    private string selectedName = string.Empty;

    [ObservableProperty]
    private string selectedEmail = string.Empty;

    [ObservableProperty]
    private string selectedSummary = string.Empty;

    [ObservableProperty]
    private string selectedPlan = string.Empty;

    [ObservableProperty]
    private string selectedStorage = string.Empty;

    [ObservableProperty]
    private string selectedTransfer = string.Empty;

    [ObservableProperty]
    private string aliasInput = string.Empty;

    [ObservableProperty]
    private string loginHeading = UiText.Get("Sign in to MEGA");

    [ObservableProperty]
    private bool isBackendMissing;

    [ObservableProperty]
    private bool selectedIsError;

    public AccountPageViewModel(MegaAccountService accounts, IDialogService dialogs)
    {
        _accounts = accounts;
        _dialogs = dialogs;
        _accounts.Changed += OnAccountsChanged;
        Refresh();
        if (accounts.GetAccounts().Count == 0)
        {
            IsLoginMode = true;
            LoginHeading = UiText.Get("Sign in to MEGA");
        }
    }

    public bool HasSelection => SelectedAccount is not null;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    public bool CanSignOut => SelectedAccount?.SignedIn == true && !IsWorking;

    public bool CanSetDefault => SelectedAccount is { IsDefault: false, Connecting: false } && !IsWorking;

    public bool CanRemove => SelectedAccount is { Connecting: false } && !IsWorking;

    public bool CanSaveAlias => SelectedAccount is { Connecting: false } && !IsWorking &&
        AliasInput.Trim() != (SelectedAccount?.AliasName ?? string.Empty);

    public bool CanRefreshDetails => SelectedAccount is { SignedIn: true, Connecting: false } && !IsWorking;

    public string SignInButtonText => UiText.Get("Sign in");

    private void OnAccountsChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
                Refresh();
        });
    }

    public void ApplyLanguage()
    {
        LoginHeading = UiText.Get(LoginEmailReadOnly ? "Sign in to MEGA again" : HasAccounts ? "Add account" : "Sign in to MEGA");
        Refresh();
    }

    public void Refresh()
    {
        string? keepEmail = SelectedAccount?.Email;
        string defaultEmail = _accounts.DefaultEmail;
        var rows = new ObservableCollection<AccountRowViewModel>();
        foreach (MegaAccountService.Status status in _accounts.GetAccounts())
            rows.Add(new AccountRowViewModel(status, string.Equals(status.Email, defaultEmail, StringComparison.OrdinalIgnoreCase)));

        _refreshingSelection = true;
        try
        {
            Accounts = rows;
            HasAccounts = rows.Count > 0;
            SelectedAccount = rows.FirstOrDefault(row => string.Equals(row.Email, keepEmail, StringComparison.OrdinalIgnoreCase))
                ?? rows.FirstOrDefault(row => row.IsDefault)
                ?? (rows.Count > 0 ? rows[0] : null);
        }
        finally
        {
            _refreshingSelection = false;
        }

        if (!HasAccounts)
            IsLoginMode = true;
        else if (!_manualLoginMode && IsLoginMode)
            IsLoginMode = false;
        AliasInput = SelectedAccount?.AliasName ?? string.Empty;

        IsBackendMissing = MegaAccountService.Availability() == MegaAccountService.BackendMissing;
        Notice = IsBackendMissing
            ? UiText.Get("This build does not include the MEGA account module. Anonymous downloads are still available.")
            : string.Empty;
        UpdateSelectedDetails();
    }

    partial void OnSelectedAccountChanged(AccountRowViewModel? value)
    {
        if (_refreshingSelection)
            return;
        Message = string.Empty;
        AliasInput = value?.AliasName ?? string.Empty;
        UpdateSelectedDetails();
    }

    partial void OnAliasInputChanged(string value) => OnPropertyChanged(nameof(CanSaveAlias));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));

    partial void OnIsWorkingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSignOut));
        OnPropertyChanged(nameof(CanSetDefault));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanRefreshDetails));
        OnPropertyChanged(nameof(CanSaveAlias));
    }

    private void UpdateSelectedDetails()
    {
        AccountRowViewModel? row = SelectedAccount;
        if (row is null)
        {
            SelectedName = string.Empty;
            SelectedEmail = string.Empty;
            SelectedSummary = string.Empty;
            SelectedPlan = string.Empty;
            SelectedStorage = string.Empty;
            SelectedTransfer = string.Empty;
            SelectedIsError = false;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanSignOut));
            OnPropertyChanged(nameof(CanSetDefault));
            OnPropertyChanged(nameof(CanRemove));
            OnPropertyChanged(nameof(CanRefreshDetails));
            return;
        }

        SelectedName = row.DisplayName;
        SelectedEmail = string.IsNullOrEmpty(row.AliasName) ? string.Empty : row.Email;
        SelectedSummary = row.Summary;
        SelectedPlan = row.Plan;
        SelectedStorage = row.Storage;
        SelectedTransfer = row.Transfer;
        SelectedIsError = row.IsError;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanSignOut));
        OnPropertyChanged(nameof(CanSetDefault));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanRefreshDetails));
    }

    [RelayCommand]
    private void BeginAdd()
    {
        LoginEmailReadOnly = false;
        LoginEmail = string.Empty;
        LoginPassword = string.Empty;
        LoginAlias = string.Empty;
        LoginRemember = false;
        MfaCode = string.Empty;
        ShowMfa = false;
        Message = string.Empty;
        LoginHeading = UiText.Get("Add account");
        _manualLoginMode = true;
        IsLoginMode = true;
    }

    [RelayCommand]
    private void ReLogin()
    {
        if (SelectedAccount is not { } row)
            return;
        LoginEmailReadOnly = true;
        LoginEmail = row.Email;
        LoginPassword = string.Empty;
        LoginAlias = row.AliasName;
        LoginRemember = row.Remember;
        MfaCode = string.Empty;
        ShowMfa = false;
        Message = string.Empty;
        LoginHeading = UiText.Get("Sign in to MEGA again");
        _manualLoginMode = true;
        IsLoginMode = true;
    }

    [RelayCommand]
    private void CancelLogin()
    {
        if (!HasAccounts)
            return;
        _manualLoginMode = false;
        IsLoginMode = false;
        LoginPassword = string.Empty;
        MfaCode = string.Empty;
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsWorking)
            return;
        string email = LoginEmail.Trim();
        if (email.Length == 0 || LoginPassword.Length == 0)
        {
            Message = UiText.Get("Enter your MEGA email and password.");
            return;
        }
        if (ShowMfa && string.IsNullOrWhiteSpace(MfaCode))
        {
            Message = UiText.Get("Enter the code from your authenticator app.");
            return;
        }
        int available = MegaAccountService.Availability();
        if (available != 0)
        {
            Message = MegaAccountService.ErrorText(available);
            return;
        }

        IsWorking = true;
        Message = string.Empty;
        try
        {
            string? alias = string.IsNullOrWhiteSpace(LoginAlias) ? null : LoginAlias.Trim();
            int result = await _accounts.LoginAsync(email, LoginPassword, MfaCode.Trim(), LoginRemember, alias);
            if (result == MegaAccountService.MfaRequired)
            {
                ShowMfa = true;
                Message = MegaAccountService.ErrorText(MegaAccountService.MfaRequired, signingIn: true);
                return;
            }
            if (result == 0)
            {
                LoginPassword = string.Empty;
                MfaCode = string.Empty;
                ShowMfa = false;
                _manualLoginMode = false;
                IsLoginMode = false;
                Message = string.Empty;
                Refresh();
                SelectedAccount = Accounts.FirstOrDefault(row =>
                    string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase)) ?? SelectedAccount;
                int quota = await _accounts.RefreshQuotaAsync(email);
                if (quota != 0)
                    Message = MegaAccountService.ErrorText(quota, connecting: true);
                return;
            }
            Message = MegaAccountService.ErrorText(result, signingIn: true);
        }
        finally
        {
            IsWorking = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (SelectedAccount is not { } row || IsWorking)
            return;
        await RunAsync(() => _accounts.LogoutAsync(row.Email), connecting: true);
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedAccount is not { } row || IsWorking)
            return;
        bool confirmed = await _dialogs.ConfirmAsync(
            UiText.Get("MEGA account"),
            UiText.Format("Remove {0} from this app? Downloads assigned to this account will need a different identity or the account added again.", row.DisplayName),
            UiText.Get("Remove account"));
        if (!confirmed)
            return;
        await RunAsync(() => _accounts.RemoveAsync(row.Email), connecting: true);
    }

    [RelayCommand]
    private void SetDefault()
    {
        if (SelectedAccount is not { } row || IsWorking)
            return;
        int result = _accounts.SetDefault(row.Email);
        Message = result == 0 ? string.Empty : MegaAccountService.ErrorText(result);
        Refresh();
    }

    [RelayCommand]
    private void SaveAlias()
    {
        if (SelectedAccount is not { } row || IsWorking)
            return;
        int result = _accounts.SetAlias(row.Email, AliasInput);
        Message = result == 0 ? string.Empty : MegaAccountService.ErrorText(result);
        Refresh();
    }

    [RelayCommand]
    private async Task RefreshDetailsAsync()
    {
        if (SelectedAccount is not { } row || IsWorking)
            return;
        IsWorking = true;
        Message = string.Empty;
        try
        {
            int result = await _accounts.RefreshQuotaAsync(row.Email);
            if (result != 0)
                Message = MegaAccountService.ErrorText(result, connecting: true);
            Refresh();
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task RunAsync(Func<Task<int>> operation, bool connecting)
    {
        IsWorking = true;
        Message = string.Empty;
        try
        {
            int result = await operation();
            Message = result == 0 ? string.Empty : MegaAccountService.ErrorText(result, connecting: connecting);
            Refresh();
        }
        finally
        {
            IsWorking = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _accounts.Changed -= OnAccountsChanged;
    }
}

public sealed class AccountRowViewModel
{
    public AccountRowViewModel(MegaAccountService.Status status, bool isDefault)
    {
        Email = status.Email;
        AliasName = status.AliasName ?? string.Empty;
        SignedIn = status.SignedIn;
        Connecting = status.Connecting;
        NeedsLogin = status.NeedsLogin;
        Remember = status.Remember;
        QuotaReached = status.QuotaReached;
        IsDefault = isDefault;
        Error = status.LastError;
        Summary = MegaAccountText.Summary(status);
        Plan = MegaAccountText.PlanName(status.Plan);
        Storage = MegaAccountText.Usage(status.StorageUsed, status.StorageTotal);
        Transfer = MegaAccountText.Usage(status.TransferUsed, status.TransferTotal);
        Caption = MegaAccountText.AccountCaption(status) +
            (isDefault ? " · " + UiText.Get("default") : string.Empty) +
            (status.Remember ? " · " + UiText.Get("Session saved") : string.Empty);
        IsError = status.NeedsLogin || status.QuotaReached || status.LastError != 0;
    }

    public string Email { get; }
    public string AliasName { get; }
    public string DisplayName => string.IsNullOrEmpty(AliasName) ? Email : AliasName;
    public string Caption { get; }
    public string Summary { get; }
    public string Plan { get; }
    public string Storage { get; }
    public string Transfer { get; }
    public bool SignedIn { get; }
    public bool Connecting { get; }
    public bool NeedsLogin { get; }
    public bool Remember { get; }
    public bool QuotaReached { get; }
    public bool IsDefault { get; }
    public bool IsError { get; }
    public int Error { get; }
}
