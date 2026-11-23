using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using MegaDownloader.UI.Views;
using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Services;

public sealed class DialogService : IDialogService
{
    public async Task<bool> ShowGuideAsync()
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not { IsVisible: true })
            return false;

        var settings = App.Services.GetRequiredService<ISettingsService>();
        var window = new QuickStartWindow();
        bool addLinks = await window.ShowDialog<bool>(owner);
        if (!settings.Current.HasSeenGuide)
        {
            settings.Current.HasSeenGuide = true;
            settings.Save();
        }
        return addLinks;
    }

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

    private AddLinksWindow? _addLinksWindow;

    public async Task<string?> ShowAddLinksAsync(string? initialText = null)
    {
        var window = new AddLinksWindow { InitialText = initialText };
        _addLinksWindow = window;
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        try
        {
            if (owner is { IsVisible: true })
                return await window.ShowDialog<bool>(owner) ? window.Outcome : null;

            window.Show();
            return null;
        }
        finally
        {
            _addLinksWindow = null;
        }
    }

    public void ShowPendingBatch(int count) => _addLinksWindow?.ShowPendingBatch(count);

    public async Task<bool> ShowPropertiesAsync(object item)
    {
        var window = new PropertiesWindow(item);
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner is { IsVisible: true })
            return await window.ShowDialog<bool>(owner);

        window.Show();
        return false;
    }

    public async Task ShowEncodeLinksAsync()
    {
        var window = new EncodeLinksWindow();
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner is { IsVisible: true })
            await window.ShowDialog(owner);
        else
            window.Show();
    }

    public async Task ShowElcGenerateAsync()
    {
        var window = new ElcGenerateWindow();
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (owner is { IsVisible: true })
            await window.ShowDialog(owner);
        else
            window.Show();
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
            Text = UiText.Get(message),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460
        });
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = UiText.Get(title),
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border { Classes = { "card" }, Margin = new Thickness(12), Padding = new Thickness(0), Child = panel }
        };

        var okButton = new Button
        {
            Content = UiText.Get(okText),
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
                Content = UiText.Get(cancelText),
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
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.Windows.LastOrDefault(candidate => candidate.IsVisible && candidate.IsActive) ?? desktop?.MainWindow;

        if (owner is { IsVisible: true })
            await window.ShowDialog(owner);
        else
            window.Show();
    }
}
