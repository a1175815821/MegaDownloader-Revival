using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using MegaDownloader.UI.Views;

namespace MegaDownloader.UI.Services;

public sealed class DialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message, string okText = "OK")
    {
        var window = BuildWindow(title, message, okText, null, null);
        return ShowAsync(window);
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string okText = "OK",
        string cancelText = "Cancel")
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = BuildWindow(title, message, okText, cancelText, result);
        window.Closed += (_, _) => result.TrySetResult(false);

        await ShowAsync(window);
        return await result.Task;
    }

    public async Task<bool> ShowAddLinksAsync()
    {
        var window = new AddLinksWindow();
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner is { IsVisible: true })
            return await window.ShowDialog<bool>(owner);

        window.Show();
        return false;
    }

    private static Window BuildWindow(
        string title,
        string message,
        string okText,
        string? cancelText,
        TaskCompletionSource<bool>? result)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(22, 20, 22, 18),
            Spacing = 18
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460
        });
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        var okButton = new Button
        {
            Content = okText,
            MinWidth = 88,
            IsDefault = true,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        okButton.Classes.Add("accent");
        okButton.Click += (_, _) =>
        {
            result?.TrySetResult(true);
            window.Close();
        };
        buttons.Children.Add(okButton);

        if (cancelText is not null)
        {
            var cancelButton = new Button
            {
                Content = cancelText,
                MinWidth = 88,
                IsCancel = true,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            cancelButton.Click += (_, _) =>
            {
                result?.TrySetResult(false);
                window.Close();
            };
            buttons.Children.Add(cancelButton);
        }

        return window;
    }

    private static async Task ShowAsync(Window window)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner is { IsVisible: true })
            await window.ShowDialog(owner);
        else
            window.Show();
    }
}
