using Avalonia.Controls;
using MegaDownloader.UI.Services;
using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (!App.Services.GetRequiredService<ISettingsService>().Current.HasSeenGuide &&
                DataContext is MainWindowViewModel model && model.ShowGuideCommand.CanExecute(null))
                model.ShowGuideCommand.Execute(null);
        };
    }
}
