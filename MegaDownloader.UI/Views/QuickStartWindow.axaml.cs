using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.Views;

public partial class QuickStartWindow : Window
{
    private readonly QuickStartStep[] _steps;
    private int _current;

    public QuickStartWindow()
    {
        InitializeComponent();
        _steps =
        [
            Step(1, "Add links", "Link", "Bring your links",
                "Paste one or more MEGA file or folder links, choose a destination, and check what each link contains.",
                "Open Add links from Downloads or press Ctrl+N."),
            Step(2, "Transfer queue", "Downloads", "Stay in control",
                "Review the files you want, add them to the queue, then check progress, pause tasks, or resume where you left off.",
                "Use the menu on a task to inspect its properties or remove it."),
            Step(3, "Settings", "Settings", "Make it yours",
                "Set your download folder, language, and transfer limits in Settings. Use Quick start to reopen this guide whenever you need it.",
                "Changes apply when you save.")
        ];
        UpdateStep();
    }

    private QuickStartStep Step(int number, string label, string icon, string title, string description, string tip)
    {
        Avalonia.Application.Current!.Resources.TryGetResource("Icon." + icon, null, out object? resource);
        return new(number, UiText.Get(label), resource as Geometry, UiText.Get(title), UiText.Get(description), UiText.Get(tip));
    }

    private void UpdateStep()
    {
        Steps.ItemsSource = _steps.Select((step, index) => step with { IsCurrent = index == _current }).ToArray();
        StepContent.Content = _steps[_current];
        ProgressLabel.Text = UiText.Format("Step {0} of {1}", _current + 1, _steps.Length);
        BackButton.IsVisible = _current > 0;
        NextButton.Content = UiText.Get(_current == _steps.Length - 1 ? "Add links" : "Continue");
    }

    private void Back(object? sender, RoutedEventArgs e)
    {
        _current--;
        UpdateStep();
    }

    private void Next(object? sender, RoutedEventArgs e)
    {
        if (_current == _steps.Length - 1)
            Close(true);
        else
        {
            _current++;
            UpdateStep();
        }
    }

    private void Skip(object? sender, RoutedEventArgs e) => Close(false);
}

public sealed record QuickStartStep(int Number, string Label, Geometry? Icon, string Title, string Description, string Tip)
{
    public bool IsCurrent { get; init; }
}
