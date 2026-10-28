using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using ResortSync.ViewModels;

namespace ResortSync.Services;

/// <summary>One entry in the navigation drawer.</summary>
public sealed record NavigationItem(string Title, PackIconKind Icon, Type ViewModelType);

/// <summary>
/// Resolves ViewModels from DI, runs their <see cref="INavigationAware"/> hook,
/// and publishes the active ViewModel to the shell (MainViewModel).
/// </summary>
public interface INavigationService
{
    ViewModelBase? Current { get; }
    IReadOnlyList<NavigationItem> Items { get; }
    NavigationItem? SelectedItem { get; }

    /// <summary>Raised after <see cref="Current"/> (or selection) changes.</summary>
    event EventHandler? CurrentChanged;

    Task NavigateAsync(Type viewModelType, object? parameter = null);
    Task NavigateAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
        // Drawer definitions live here so the shell stays presentation-only.
        Items = new List<NavigationItem>
        {
            new("Dashboard", PackIconKind.ViewDashboardOutline, typeof(DashboardViewModel)),
            new("New Booking", PackIconKind.PlusCircleOutline, typeof(BookingFormViewModel)),
            new("All Bookings", PackIconKind.CalendarMultiple, typeof(BookingsViewModel)),
            new("Payments", PackIconKind.Money, typeof(PaymentsViewModel)),
            new("Settings", PackIconKind.CogOutline, typeof(SettingsViewModel))
        };
    }

    public ViewModelBase? Current { get; private set; }
    public IReadOnlyList<NavigationItem> Items { get; }
    public NavigationItem? SelectedItem { get; private set; }
    public event EventHandler? CurrentChanged;

    public Task NavigateAsync(Type viewModelType, object? parameter = null)
    {
        // Transient activation: every visit gets a fresh, state-clean ViewModel.
        var viewModel = (ViewModelBase)ActivatorUtilities.CreateInstance(_services, viewModelType);

        Current = viewModel;
        SelectedItem = Items.FirstOrDefault(i => i.ViewModelType == viewModelType);
        CurrentChanged?.Invoke(this, EventArgs.Empty);

        return viewModel is INavigationAware aware
            ? aware.OnNavigatedToAsync(parameter)
            : Task.CompletedTask;
    }

    public Task NavigateAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
        => NavigateAsync(typeof(TViewModel), parameter);
}
