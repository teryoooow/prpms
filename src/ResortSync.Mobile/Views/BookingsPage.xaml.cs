using System.Collections.ObjectModel;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Mobile.Views;

public partial class BookingsPage : ContentPage
{
    private const string StatusFilterAll = "All statuses";

    private readonly IBookingService _bookings;
    private readonly IDashboardInvalidator _invalidator;
    private readonly ObservableCollection<Booking> _rows = new();
    private int _listVersion;

    public BookingsPage()
        : this(
            IPlatformApplication.Current!.Services.GetRequiredService<IBookingService>(),
            IPlatformApplication.Current.Services.GetRequiredService<IDashboardInvalidator>())
    {
    }

    public BookingsPage(IBookingService bookings, IDashboardInvalidator invalidator)
    {
        InitializeComponent();
        _bookings = bookings;
        _invalidator = invalidator;
        BookingsList.ItemsSource = _rows;

        StatusPicker.ItemsSource = new[]
        {
            StatusFilterAll,
            nameof(BookingStatus.PendingDeposit),
            nameof(BookingStatus.Confirmed),
            nameof(BookingStatus.FullyPaid),
            nameof(BookingStatus.Completed),
            nameof(BookingStatus.Cancelled),
            nameof(BookingStatus.Expired)
        };
        StatusPicker.SelectedIndex = 0;

        // Background sync/expiry can move rows while this tab is already
        // visible — OnAppearing alone would leave a stale list.
        _invalidator.RefreshRequested += OnRefreshRequested;
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(async () => await SafeRefreshAsync());

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshListAsync();
    }

    // ── Filters (version-stamped so a slow older query cannot overwrite newer) ──
    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => _ = SafeRefreshAsync();

    private void OnStatusSelectedChanged(object? sender, EventArgs e) => _ = SafeRefreshAsync();

    private async Task SafeRefreshAsync()
    {
        try
        {
            await RefreshListAsync();
        }
        catch
        {
            // Best-effort background refresh; next change retries.
        }
    }

    private async Task RefreshListAsync()
    {
        var version = ++_listVersion;

        BookingStatus? status = StatusPicker.SelectedIndex <= 0
            ? null
            : Enum.Parse<BookingStatus>((string)StatusPicker.SelectedItem!);

        var rows = await _bookings.ListAsync(SearchEntry.Text, status);

        if (version != _listVersion)
        {
            return; // superseded
        }

        _rows.Clear();
        foreach (var row in rows)
        {
            _rows.Add(row);
        }

        CountLabel.Text = $"{_rows.Count} booking(s)";
    }

    // ── Navigation ──
    private async void OnBookingTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: Booking booking })
        {
            await Shell.Current.GoToAsync($"booking/detail?id={booking.Id}");
        }
    }

    private async void OnNewBookingClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("booking/new");
}
