using Avalonia;
using Avalonia.Controls;
using MegaDownloader.UI.ViewModels;
using MegaDownloader.UI.ViewModels.Pages;

namespace MegaDownloader.UI.Views;

public partial class IdentityPickerWindow : Window
{
    public IdentityPickerWindow()
    {
        InitializeComponent();
    }

    public static async Task<IdentityPickResult?> ShowAsync(Visual owner, string currentEmail)
    {
        Window? parent = TopLevel.GetTopLevel(owner) as Window;
        var model = new IdentityPickerViewModel(currentEmail);
        var window = new IdentityPickerWindow { DataContext = model };
        var completion = new TaskCompletionSource<IdentityPickResult?>();
        model.Confirmed += (_, _) =>
        {
            completion.TrySetResult(new IdentityPickResult(model.SelectedChoice?.Email ?? string.Empty, true));
            window.Close();
        };
        model.Cancelled += (_, _) =>
        {
            completion.TrySetResult(null);
            window.Close();
        };
        window.Closed += (_, _) => completion.TrySetResult(null);
        if (parent is not null)
            await window.ShowDialog(parent);
        else
            window.Show();
        return await completion.Task;
    }
}
