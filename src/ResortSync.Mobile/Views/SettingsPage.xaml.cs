using ResortSync.Services;
using ResortSync.Sync;

namespace ResortSync.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ISettingsService _settings;
    private readonly SyncCoordinator _sync;

    public SettingsPage()
        : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ISettingsService>(),
            IPlatformApplication.Current.Services.GetRequiredService<SyncCoordinator>())
    {
    }

    public SettingsPage(ISettingsService settings, SyncCoordinator sync)
    {
        InitializeComponent();

        _settings = settings;
        _sync = sync;

        ThemePicker.ItemsSource = new[] { "System", "Light", "Dark" };
        ThemePicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        // LOAD-then-MERGE: never construct a fresh model here or device-local
        // sync credentials would be wiped on every visit.
        var s = await _settings.LoadAsync();

        ThemePicker.SelectedItem = s.Theme is "Light" or "Dark" ? s.Theme : "System";
        ResortNameEntry.Text = s.ResortName;
        DownpaymentEntry.Text = (s.DownpaymentPercent * 100m).ToString("0.##");
        ExtraRateEntry.Text = s.DefaultExtraGuestRate.ToString("0.##");
        BaseGuestsEntry.Text = s.DefaultBaseGuests.ToString();

        SyncEnabledCheck.IsChecked = s.SyncEnabled;
        ApiKeyEntry.Text = s.FirebaseApiKey;
        ProjectIdEntry.Text = s.FirebaseProjectId;
        EmailEntry.Text = s.SyncEmail;
        PasswordEntry.Text = s.SyncPassword;

        CloudStatusLabel.Text = DescribeSync(s);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
        => await SaveAsync(showConfirmation: true);

    private async Task SaveAsync(bool showConfirmation)
    {
        if (!decimal.TryParse(DownpaymentEntry.Text, out var downpaymentPercent)
            || downpaymentPercent is < 0 or > 100)
        {
            await DisplayAlert("Invalid downpayment",
                "Downpayment percentage must be between 0 and 100.", "OK");
            return;
        }

        if (!decimal.TryParse(ExtraRateEntry.Text, out var extraRate) || extraRate < 0)
        {
            await DisplayAlert("Invalid rate",
                "Extra guest rate must be zero or greater.", "OK");
            return;
        }

        if (!int.TryParse(BaseGuestsEntry.Text, out var baseGuests) || baseGuests < 1)
        {
            await DisplayAlert("Invalid guests", "Default base guests must be at least 1.", "OK");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var s = await _settings.LoadAsync();

            var resort = string.IsNullOrWhiteSpace(ResortNameEntry.Text)
                ? "My Private Resort"
                : ResortNameEntry.Text.Trim();
            var downpayment = Math.Clamp(downpaymentPercent / 100m, 0m, 1m);

            // Business fields travel through the cloud — bump the sync stamp only
            // when one of them actually changed (otherwise every save echoes).
            var businessChanged = s.ResortName != resort
                || s.DownpaymentPercent != downpayment
                || s.DefaultExtraGuestRate != extraRate
                || s.DefaultBaseGuests != baseGuests;

            s.Theme = ThemePicker.SelectedItem as string ?? "System";
            s.ResortName = resort;
            s.DownpaymentPercent = downpayment;
            s.DefaultExtraGuestRate = extraRate;
            s.DefaultBaseGuests = baseGuests;

            // Device-local sync config (never stamped).
            s.SyncEnabled = SyncEnabledCheck.IsChecked;
            s.FirebaseApiKey = (ApiKeyEntry.Text ?? string.Empty).Trim();
            s.FirebaseProjectId = (ProjectIdEntry.Text ?? string.Empty).Trim();
            s.SyncEmail = (EmailEntry.Text ?? string.Empty).Trim();
            s.SyncPassword = PasswordEntry.Text ?? string.Empty;

            if (businessChanged)
            {
                s.SettingsUpdatedAt = FirestoreValues.TruncateMs(DateTime.UtcNow);
            }

            await _settings.SaveAsync(s);

            if (businessChanged || s.SyncEnabled)
            {
                _sync.RequestSync();
            }

            CloudStatusLabel.Text = DescribeSync(s);
            if (showConfirmation)
            {
                await DisplayAlert("Settings saved",
                    "New defaults apply to bookings created from now on.", "OK");
            }
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        // Persist what's on screen first so the cycle picks up edits.
        if (SaveButton.IsEnabled)
        {
            await SaveAsync(showConfirmation: false);
        }

        SyncNowButton.IsEnabled = false;
        CloudStatusLabel.Text = "Syncing…";
        try
        {
            var result = await _sync.SyncNowAsync();
            var s = await _settings.LoadAsync();
            CloudStatusLabel.Text = DescribeSync(s, result);
        }
        catch (Exception ex)
        {
            CloudStatusLabel.Text = $"Sync failed: {ex.Message}";
        }
        finally
        {
            SyncNowButton.IsEnabled = true;
        }
    }

    private string DescribeSync(SettingsModel s, SyncResult? result = null)
    {
        result ??= _sync.LastResult;

        if (!s.SyncEnabled)
        {
            return "Cloud sync is off.";
        }

        if (string.IsNullOrWhiteSpace(s.FirebaseApiKey)
            || string.IsNullOrWhiteSpace(s.FirebaseProjectId)
            || string.IsNullOrWhiteSpace(s.SyncEmail)
            || string.IsNullOrWhiteSpace(s.SyncPassword))
        {
            return "Not configured — enter the Firebase project details.";
        }

        if (result is null)
        {
            return _sync.LastSyncAt is { } at
                ? $"Last sync {at.ToLocalTime():hh:mm:ss tt}."
                : "Configured — waiting for the first sync.";
        }

        return result.State switch
        {
            SyncStatus.Idle when _sync.LastSyncAt is { } at =>
                $"Synced {at.ToLocalTime():hh:mm:ss tt} — {result.Pushed} pushed, {result.Pulled} pulled.",
            SyncStatus.Idle => "Waiting for the first sync.",
            SyncStatus.Error => $"Last sync failed: {result.Error}",
            SyncStatus.SignedOut => "Sign-in failed — check the email and password.",
            SyncStatus.Syncing => "Syncing.",
            SyncStatus.Disabled => "Cloud sync is off.",
            _ => result.State.ToString()
        };
    }
}
