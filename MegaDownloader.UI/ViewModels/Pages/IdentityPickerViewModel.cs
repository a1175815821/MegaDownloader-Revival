using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels.Pages;

public sealed class IdentityChoice
{
    public IdentityChoice(string email, string caption, bool available)
    {
        Email = email;
        Caption = caption;
        Available = available;
    }

    public string Email { get; }
    public string Caption { get; }
    public bool Available { get; }
}

public partial class IdentityPickerViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<IdentityChoice> choices = new();

    [ObservableProperty]
    private IdentityChoice? selectedChoice;

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public IdentityPickerViewModel(string currentEmail)
    {
        Choices.Add(new IdentityChoice(string.Empty, UiText.Get("Anonymous"), true));
        foreach (MegaAccountService.Status status in MegaAccountService.Current.GetAccounts())
        {
            bool available = status.SignedIn && !status.Connecting;
            string caption = MegaAccountText.AccountCaption(status);
            if (!available)
                caption += " · " + MegaAccountText.Summary(status);
            Choices.Add(new IdentityChoice(status.Email, caption, available));
        }
        if (currentEmail.Length > 0 && !Choices.Any(choice =>
            string.Equals(choice.Email, currentEmail, StringComparison.OrdinalIgnoreCase)))
        {
            Choices.Add(new IdentityChoice(currentEmail,
                currentEmail + " · " + UiText.Get("Sign in to MEGA again"), false));
        }
        SelectedChoice = Choices.FirstOrDefault(choice =>
            string.Equals(choice.Email, currentEmail, StringComparison.OrdinalIgnoreCase)) ?? Choices[0];
    }

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedChoice is null || !SelectedChoice.Available)
            return;
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);
}
