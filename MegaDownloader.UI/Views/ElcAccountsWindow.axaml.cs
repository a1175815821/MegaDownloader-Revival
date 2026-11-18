using Avalonia.Controls;
using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Views;

public partial class ElcAccountsWindow : Window
{
    public ElcAccountsWindow()
    {
        InitializeComponent();

        var viewModel = new ElcAccountsViewModel(App.Services.GetRequiredService<DownloadEngineService>());
        viewModel.CloseRequested += Close;
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }
}
