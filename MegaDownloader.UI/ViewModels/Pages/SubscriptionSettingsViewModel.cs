using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels.Pages;

/// <summary>
/// Settings for one subscribed folder — either a folder bound inside a signed-in
/// cloud drive or an anonymous public folder link. Both store the same fields.
/// </summary>
public partial class SubscriptionSettingsViewModel : ObservableObject
{
    private readonly MegaFolderSubscriptionSettings _original;
    private readonly string _title;

    [ObservableProperty]
    private string destination;

    [ObservableProperty]
    private string extensions;

    [ObservableProperty]
    private bool dailyCheck;

    [ObservableProperty]
    private TimeSpan? checkTime;

    [ObservableProperty]
    private bool autoQueue;

    [ObservableProperty]
    private bool paused;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    public SubscriptionSettingsViewModel(MegaFolderSubscriptionSettings settings, string title)
    {
        _original = settings.Copy();
        _title = title;
        destination = settings.Destination ?? string.Empty;
        extensions = settings.Extensions ?? string.Empty;
        dailyCheck = settings.CheckMinutes.HasValue;
        int minutes = Math.Max(0, Math.Min(1439, settings.CheckMinutes.GetValueOrDefault(540)));
        checkTime = TimeSpan.FromMinutes(minutes);
        autoQueue = settings.AutoQueue;
        paused = settings.Paused;
    }

    public string FolderName => _title;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    partial void OnValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasValidationMessage));

    public bool IsReviewMode
    {
        get => !AutoQueue;
        set
        {
            if (value)
                AutoQueue = false;
        }
    }

    public bool IsAutomaticMode
    {
        get => AutoQueue;
        set
        {
            if (value)
                AutoQueue = true;
        }
    }

    partial void OnAutoQueueChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReviewMode));
        OnPropertyChanged(nameof(IsAutomaticMode));
    }

    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public event Action<MegaFolderSubscriptionSettings>? Confirmed;
    public event EventHandler? Cancelled;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (PickFolderAsync is null)
            return;
        string? folder = await PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
            Destination = folder;
    }

    [RelayCommand]
    private void Confirm()
    {
        string input = Destination.Trim();
        try
        {
            if (input.Length == 0 || !System.IO.Path.IsPathFullyQualified(input))
                throw new ArgumentException();
            PathGuard.GetSafePathUnderRoot(input, string.Empty);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.IO.IOException)
        {
            ValidationMessage = UiText.Get("Choose an absolute save path.");
            return;
        }

        MegaFolderSubscriptionSettings settings = _original.Copy();
        settings.Destination = input;
        settings.Extensions = MegaDriveSubscription.NormalizeExtensions(Extensions);
        settings.AutoQueue = AutoQueue;
        settings.Paused = Paused;
        settings.CheckMinutes = DailyCheck
            ? (int)(CheckTime ?? TimeSpan.FromHours(9)).TotalMinutes
            : null;
        ValidationMessage = string.Empty;
        Confirmed?.Invoke(settings);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);
}
