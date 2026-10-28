using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// Application shell: owns the drawer selection and swaps the content area's
/// ViewModel when navigation happens (including background-triggered refreshes).
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    /// <summary>The ViewModel rendered in the content area (bound by DataTemplates).</summary>
    [ObservableProperty]
    private ObservableObject? currentViewModel;

    /// <summary>Two-way bound to the drawer ListBox selection.</summary>
    [ObservableProperty]
    private NavigationItem? selectedNavItem;

    public ObservableCollection<NavigationItem> NavItems { get; }

    public MainViewModel(INavigationService navigation, IDashboardInvalidator invalidator)
    {
        _navigation = navigation;
        NavItems = new ObservableCollection<NavigationItem>(navigation.Items);
        Title = "PRPMS";

        // Sync shell state whenever the navigation service publishes a new page.
        _navigation.CurrentChanged += (_, _) =>
        {
            CurrentViewModel = _navigation.Current;
            SelectedNavItem = _navigation.SelectedItem;
            StatusMessage = _navigation.Current?.Title ?? string.Empty;
        };

        // The auto-expiry worker nudges us so the dashboard updates in place.
        // The worker runs on the thread pool → marshal onto the dispatcher before
        // touching any ObservableCollection-bound state.
        invalidator.RefreshRequested += (_, _) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (CurrentViewModel is DashboardViewModel dashboard)
                {
                    _ = dashboard.ReloadCommand.ExecuteAsync(null);
                }
            });
        };
    }

    /// <summary>User clicked a drawer entry.</summary>
    partial void OnSelectedNavItemChanged(NavigationItem? value)
    {
        if (value is null || value.ViewModelType == CurrentViewModel?.GetType())
        {
            return; // already showing this page (programmatic sync from CurrentChanged)
        }

        _ = NavigateSafeAsync(value.ViewModelType, null);
    }

    [RelayCommand]
    private Task NavigateAsync(NavigationItem? item)
        => item is null ? Task.CompletedTask : NavigateSafeAsync(item.ViewModelType, null);

    /// <summary>Navigate to the landing page (dashboard) — used by the logo/home button.</summary>
    [RelayCommand]
    private Task GoHomeAsync()
        => NavigateSafeAsync(typeof(DashboardViewModel), null);

    /// <summary>
    /// Navigation entry point that never lets a fault escape as an unobserved
    /// task exception (fire-and-forget from property setters).
    /// </summary>
    private async Task NavigateSafeAsync(Type viewModelType, object? parameter)
    {
        try
        {
            await _navigation.NavigateAsync(viewModelType, parameter);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Navigation failed: {ex.Message}";
        }
    }
}
