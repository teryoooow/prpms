using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Services;
using ResortSync.Sync;

namespace ResortSync.ViewModels;

/// <summary>
/// Application preferences: theme, resort name, booking defaults, and the
/// cloud-sync section. Persisted as JSON via <see cref="ISettingsService"/>.
/// Every save LOAD-then-MERGEs the file — constructing a fresh model here
/// would wipe device-local sync credentials on each visit.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, INavigationAware
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogs;
    private readonly SyncCoordinator _sync;

    /// <summary>
    /// Guards <see cref="OnThemeChanged"/>: the load step itself assigns
    /// Theme, which must not trigger a save before the other fields are
    /// hydrated (warm cache completes LoadAsync inline — a real race).
    /// </summary>
    private bool _hydrated;

    [ObservableProperty] private string theme = "System";
    [ObservableProperty] private string resortName = string.Empty;
    [ObservableProperty] private decimal downpaymentPercent;
    [ObservableProperty] private decimal defaultExtraGuestRate;
    [ObservableProperty] private int defaultBaseGuests;

    // ── Cloud sync (device-local credentials + enable switch) ──────────────
    [ObservableProperty] private bool syncEnabled;
    [ObservableProperty] private string firebaseApiKey = string.Empty;
    [ObservableProperty] private string firebaseProjectId = string.Empty;
    [ObservableProperty] private string syncEmail = string.Empty;
    [ObservableProperty] private string syncPassword = string.Empty;

    /// <summary>Cloud section status line ("Last sync…", "Not configured…").</summary>
    [ObservableProperty] private string cloudStatus = string.Empty;

    public IReadOnlyList<string> Themes { get; } = new[] { "System", "Light", "Dark" };

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IDialogService dialogs,
        SyncCoordinator sync)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _dialogs = dialogs;
        _sync = sync;
        Title = "Settings";
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
        => await RunBusyAsync(async () =>
        {
            _hydrated = false;
            var settings = await _settingsService.LoadAsync();

            Theme = settings.Theme;
            ResortName = settings.ResortName;
            DownpaymentPercent = settings.DownpaymentPercent * 100m; // UI shows percent
            DefaultExtraGuestRate = settings.DefaultExtraGuestRate;
            DefaultBaseGuests = settings.DefaultBaseGuests;

            SyncEnabled = settings.SyncEnabled;
            FirebaseApiKey = settings.FirebaseApiKey;
            FirebaseProjectId = settings.FirebaseProjectId;
            SyncEmail = settings.SyncEmail;
            SyncPassword = settings.SyncPassword;

            _hydrated = true;
            CloudStatus = DescribeSync();
            StatusMessage = $"Loaded from {_settingsService.GetType().Name}";
        });

    /// <summary>Theme combo changed → apply immediately (preview) and persist.</summary>
    partial void OnThemeChanged(string value)
    {
        _themeService.Apply(value);
        if (_hydrated)
        {
            _ = PersistAsync();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (DownpaymentPercent is < 0 or > 100)
        {
            _dialogs.ShowError("Invalid downpayment",
                "Downpayment percentage must be between 0 and 100.");
            return;
        }

        if (DefaultBaseGuests < 1)
        {
            _dialogs.ShowError("Invalid guests", "Default base guests must be at least 1.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            await PersistAsync();
            _dialogs.ShowInfo("Settings saved",
                "New defaults apply to bookings created from now on.");
        });
    }

    /// <summary>Save + push/pull once immediately (button in the cloud card).</summary>
    [RelayCommand]
    private async Task SyncNowAsync()
        => await RunBusyAsync(async () =>
        {
            await PersistAsync(); // persist what's on screen first

            var result = await _sync.SyncNowAsync();
            CloudStatus = DescribeSync(result);

            StatusMessage = result.State switch
            {
                SyncStatus.Idle => $"Sync complete — ↑{result.Pushed} ↓{result.Pulled}",
                SyncStatus.Error => $"Sync failed: {result.Error}",
                _ => "Sync did not run"
            };
        });

    private async Task PersistAsync()
    {
        var settings = await _settingsService.LoadAsync();

        var resort = string.IsNullOrWhiteSpace(ResortName) ? "My Private Resort" : ResortName.Trim();
        var downpayment = Math.Clamp(DownpaymentPercent / 100m, 0m, 1m);
        var extraRate = Math.Max(0m, DefaultExtraGuestRate);
        var baseGuests = Math.Max(1, DefaultBaseGuests);

        // Business fields travel through the cloud — bump the sync stamp only
        // when one of them actually changed (otherwise every save echoes).
        var businessChanged = settings.ResortName != resort
            || settings.DownpaymentPercent != downpayment
            || settings.DefaultExtraGuestRate != extraRate
            || settings.DefaultBaseGuests != baseGuests;

        settings.Theme = Theme;
        settings.ResortName = resort;
        settings.DownpaymentPercent = downpayment;
        settings.DefaultExtraGuestRate = extraRate;
        settings.DefaultBaseGuests = baseGuests;

        // Device-local sync config (never stamped, never compared).
        settings.SyncEnabled = SyncEnabled;
        settings.FirebaseApiKey = FirebaseApiKey.Trim();
        settings.FirebaseProjectId = FirebaseProjectId.Trim();
        settings.SyncEmail = SyncEmail.Trim();
        settings.SyncPassword = SyncPassword;

        if (businessChanged)
        {
            settings.SettingsUpdatedAt = FirestoreValues.TruncateMs(DateTime.UtcNow);
        }

        await _settingsService.SaveAsync(settings);

        if (businessChanged || SyncEnabled)
        {
            _sync.RequestSync(); // fire-and-forget; status lands on the next visit
        }

        CloudStatus = DescribeSync();
        StatusMessage = $"Saved {DateTime.Now:hh:mm:ss tt}";
    }

    private string DescribeSync(SyncResult? result = null)
    {
        result ??= _sync.LastResult;

        if (!SyncEnabled)
        {
            return "Cloud sync is off.";
        }

        if (string.IsNullOrWhiteSpace(FirebaseApiKey)
            || string.IsNullOrWhiteSpace(FirebaseProjectId)
            || string.IsNullOrWhiteSpace(SyncEmail))
        {
            return "Not configured — enter the Firebase project details.";
        }

        if (result is null)
        {
            return "Configured — waiting for the first sync.";
        }

        return result.State switch
        {
            SyncStatus.Idle when _sync.LastSyncAt is { } at =>
                $"Synced {at.ToLocalTime():hh:mm:ss tt} — ↑{result.Pushed} ↓{result.Pulled}",
            SyncStatus.Idle => "Waiting for the first sync.",
            SyncStatus.Error => $"Last sync failed: {result.Error}",
            SyncStatus.SignedOut => "Sign-in failed — check the email and password.",
            SyncStatus.Syncing => "Syncing…",
            _ => result.State.ToString()
        };
    }
}
