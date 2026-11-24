using System;
using System.Globalization;
using MegaDownloader;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Services;

/// <summary>Shared account wording for the account, cloud-drive and queue pages.</summary>
internal static class MegaAccountText
{
    public static string Summary(MegaAccountService.Status status, bool showUnknown = false)
    {
        if (status.Connecting)
            return UiText.Get("Connecting to MEGA…");
        if (status.NeedsLogin)
            return UiText.Get("Sign in to MEGA again");
        if (status.LastError != 0)
            return UiText.Get("Could not connect to MEGA");
        if (!status.SignedIn)
            return UiText.Get("Signed out of MEGA");
        if (status.QuotaReached)
            return UiText.Get("MEGA transfer quota reached");
        if (status.Remaining.HasValue)
            return UiText.Format("{0} of MEGA transfer left", FormatBytes(status.Remaining.Value));
        return showUnknown
            ? UiText.Get("Signed in. MEGA did not report a transfer allowance for this account.")
            : UiText.Get("Signed in to MEGA");
    }

    public static string DisplayName(MegaAccountService.Status status) =>
        string.IsNullOrEmpty(status.AliasName) ? status.Email : status.AliasName;

    public static string AccountCaption(MegaAccountService.Status status) =>
        string.IsNullOrEmpty(status.AliasName)
            ? status.Email
            : status.AliasName + " (" + status.Email + ")";

    public static string FormatBytes(long value) =>
        DownloadsPageViewModel.FormatBytes(value);

    public static string Usage(long? used, long? total)
    {
        if (used.HasValue && total.HasValue)
            return UiText.Format("{0} used of {1}", FormatBytes(used.Value), FormatBytes(total.Value));
        if (used.HasValue)
            return UiText.Format("{0} used", FormatBytes(used.Value));
        if (total.HasValue)
            return UiText.Format("{0} total", FormatBytes(total.Value));
        return UiText.Get("Not reported");
    }

    public static string PlanName(int? plan)
    {
        if (!plan.HasValue)
            return UiText.Get("Not reported");
        return plan.Value switch
        {
            0 => UiText.Get("Free"),
            1 => "Pro I",
            2 => "Pro II",
            3 => "Pro III",
            4 => "Pro Lite",
            11 => "Starter",
            12 => "Basic",
            13 => "Essential",
            100 => "Business",
            101 => "Pro Flexi",
            _ => plan.Value.ToString(CultureInfo.InvariantCulture)
        };
    }

    public static string ErrorText(int code, bool signingIn = false, bool connecting = false)
    {
        // Account failures are shown through the same translated wording the engine
        // stores on the task.
        string text = MegaAccountService.ErrorText(code, signingIn, connecting);
        return text;
    }

    public static string IdentityCaption(string email)
    {
        if (string.IsNullOrEmpty(email))
            return UiText.Get("Anonymous");
        MegaAccountService.Status status = MegaAccountService.Current.GetStatus(email);
        string name = string.IsNullOrEmpty(status.DisplayName) ? email : status.DisplayName;
        string caption = "MEGA · " + name;
        if (status.Connecting)
            caption = UiText.Get("Connecting to MEGA…") + " · " + caption;
        else if (status.NeedsLogin || (!status.SignedIn && !string.IsNullOrEmpty(status.Email)))
            caption = Summary(status) + " · " + caption;
        return caption;
    }
}
