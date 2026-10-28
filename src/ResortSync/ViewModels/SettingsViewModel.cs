using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// Application preferences: theme, resort name, downpayment %, and booking defaults.
/// Persisted as JSON via <see cref="ISettingsService"/>.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, INavigationAware
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string theme = "System";
    [ObservableProperty] private string resortName = string.Empty;
    [ObservableProperty] private decimal downpaymentPercent;
    [ObservableProperty] private decimal defaultExtraGuestRate;
    [ObservableProperty] private int defaultBaseGuests;

    public IReadOnlyList<string> Themes { get; } = new[] { "System", "Light", "Dark" };

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IDialogService dialogs)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _dialogs = dialogs;
        Title = "Settings";
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
        => await RunBusyAsync(async () =>
        {
            var settings = await _settingsService.LoadAsync();

            Theme = settings.Theme;
            ResortName = settings.ResortName;
            DownpaymentPercent = settings.DownpaymentPercent * 100m; // UI shows percent
            DefaultExtraGuestRate = settings.DefaultExtraGuestRate;
            DefaultBaseGuests = settings.DefaultBaseGuests;

            StatusMessage = $"Loaded from {_settingsService.GetType().Name}";
        });

    /// <summary>Theme combo changed → apply immediately (preview) and persist.</summary>
    partial void OnThemeChanged(string value)
    {
        _themeService.Apply(value);
        _ = PersistAsync();
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

    private async Task PersistAsync()
    {
        var settings = new SettingsModel
        {
            Theme = Theme,
            ResortName = string.IsNullOrWhiteSpace(ResortName) ? "My Private Resort" : ResortName.Trim(),
            DownpaymentPercent = Math.Clamp(DownpaymentPercent / 100m, 0m, 1m),
            DefaultExtraGuestRate = Math.Max(0m, DefaultExtraGuestRate),
            DefaultBaseGuests = Math.Max(1, DefaultBaseGuests)
        };

        await _settingsService.SaveAsync(settings);
        StatusMessage = $"Saved {DateTime.Now:hh:mm:ss tt}";
    }
}
