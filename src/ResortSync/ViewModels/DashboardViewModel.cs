using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// Landing page: four stat cards + live downpayment-expiry alerts.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, INavigationAware
{
    private readonly IDashboardService _dashboard;
    private readonly INavigationService _navigation;

    // ── Stat cards ───────────────────────────────────────────────────────────
    [ObservableProperty] private decimal monthlyRevenue;
    [ObservableProperty] private decimal pendingBalanceTotal;
    [ObservableProperty] private int todayCheckIns;
    [ObservableProperty] private int overdueDownpayments;
    [ObservableProperty] private int activeBookings;

    /// <summary>Bookings inside (or past) the 24-hour deposit window.</summary>
    public ObservableCollection<Booking> DepositAlerts { get; } = new();

    public DashboardViewModel(IDashboardService dashboard, INavigationService navigation)
    {
        _dashboard = dashboard;
        _navigation = navigation;
        Title = "Dashboard";
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
        => await ReloadAsync();

    /// <summary>Bound to the refresh button and the shell's expiry-broadcast hook.</summary>
    [RelayCommand]
    private Task ReloadAsync() => RunBusyAsync(LoadCoreAsync);

    /// <summary>Alert row clicked → open the booking in All Bookings.</summary>
    [RelayCommand]
    private Task OpenAlertAsync(Booking? booking)
        => booking is null
            ? Task.CompletedTask
            : _navigation.NavigateAsync<BookingsViewModel>(booking.Id);

    /// <summary>Card "collect" button → jump to the payment ledger.</summary>
    [RelayCommand]
    private Task ViewPaymentsAsync() => _navigation.NavigateAsync<PaymentsViewModel>();

    [RelayCommand]
    private Task NewBookingAsync() => _navigation.NavigateAsync<BookingFormViewModel>();

    private async Task LoadCoreAsync()
    {
        var stats = await _dashboard.GetStatsAsync();
        var alerts = await _dashboard.GetDepositAlertsAsync();

        MonthlyRevenue = stats.MonthlyRevenue;
        PendingBalanceTotal = stats.PendingBalanceTotal;
        TodayCheckIns = stats.TodayCheckIns;
        OverdueDownpayments = stats.OverdueDownpayments;
        ActiveBookings = stats.ActiveBookings;

        DepositAlerts.Clear();
        foreach (var alert in alerts)
        {
            DepositAlerts.Add(alert);
        }

        StatusMessage = alerts.Count > 0
            ? $"{alerts.Count} downpayment alert(s) — updated {DateTime.Now:hh:mm tt}"
            : $"No deposit alerts — updated {DateTime.Now:hh:mm tt}";
    }
}
