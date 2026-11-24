using Avalonia.Controls;
using MegaDownloader.UI.ViewModels.Pages;

namespace MegaDownloader.UI.Views;

public partial class SubscriptionsPageView : UserControl
{
    public SubscriptionsPageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SubscriptionsPageViewModel model)
        {
            model.EditSubscriptionAsync = settings => SubscriptionSettingsWindow.ShowAsync(this, settings);
            model.PublicFolders.EditSubscriptionAsync =
                settings => SubscriptionSettingsWindow.ShowAsync(this, settings);
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is SubscriptionsPageViewModel model)
            model.Initialize();
    }
}
