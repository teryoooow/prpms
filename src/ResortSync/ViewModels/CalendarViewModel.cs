using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>Legend entry shown in the calendar header (Pending / Complete).</summary>
public sealed record CalendarLegend(string Label, BookingStatus Status, string Hint);

/// <summary>One booking rendered inside a day cell or the selected-day list.</summary>
public sealed record CalendarBookingChip(
    int BookingId,
    string GuestName,
    string StatusGroup,
    BookingStatus StatusColor,
    string DateRangeLabel,
    string StayLabel,
    string AmountLabel,
    string Tooltip);

/// <summary>A single date square in the month grid.</summary>
public partial class CalendarDayCell : ObservableObject
{
    private const int MaxVisibleChips = 3;

    public CalendarDayCell(
        DateTime date,
        bool isCurrentMonth,
        bool isToday,
        IReadOnlyList<CalendarBookingChip> bookings)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = isToday;
        Bookings = bookings;
        VisibleBookings = bookings.Count > MaxVisibleChips
            ? bookings.Take(MaxVisibleChips).ToList()
            : bookings.ToList();
        OverflowCount = Math.Max(0, bookings.Count - MaxVisibleChips);
    }

    public DateTime Date { get; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }

    /// <summary>Every booking on this date (drives the selected-day detail list).</summary>
    public IReadOnlyList<CalendarBookingChip> Bookings { get; }

    /// <summary>First chips shown inside the cell before the "+N more" line.</summary>
    public IReadOnlyList<CalendarBookingChip> VisibleBookings { get; }

    public int OverflowCount { get; }
    public bool HasBookings => Bookings.Count > 0;
    public bool HasOverflow => OverflowCount > 0;
    public string OverflowLabel => $"+{OverflowCount} more";

    [ObservableProperty] private bool isSelected;
}

/// <summary>
/// Month grid of bookings. Each occupied day shows its bookings as chips colored
/// Pending (amber) or Complete (brown); cancelled/expired bookings are hidden
/// because they release their dates. Clicking a date lists it on the right.
/// </summary>
public partial class CalendarViewModel : ViewModelBase, INavigationAware, IRefreshable
{
    private readonly IBookingService _bookingService;
    private readonly INavigationService _navigation;

    private List<Booking> _bookings = new();
    private DateTime _month = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private CalendarDayCell? _selectedCell;

    public ObservableCollection<CalendarDayCell> Days { get; } = new();

    [ObservableProperty] private string monthTitle = string.Empty;
    [ObservableProperty] private string monthSummary = string.Empty;
    [ObservableProperty] private string selectedDayTitle = "No date selected";
    [ObservableProperty] private string selectedDaySummary = string.Empty;
    [ObservableProperty] private CalendarDayCell? selectedDay;

    public IReadOnlyList<string> Weekdays { get; } =
        new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public IReadOnlyList<CalendarLegend> Legend { get; } = new[]
    {
        new CalendarLegend("Pending", BookingStatus.PendingDeposit,
            "Reserved — deposit pending, confirmed, or fully paid (stay not checked out)"),
        new CalendarLegend("Complete", BookingStatus.Completed,
            "Stay finished — guest has checked out")
    };

    public CalendarViewModel(IBookingService bookingService, INavigationService navigation)
    {
        _bookingService = bookingService;
        _navigation = navigation;
        Title = "Booking Calendar";
    }

    Task IRefreshable.RefreshAsync() => RefreshCommand.ExecuteAsync(null);

    public override async Task OnNavigatedToAsync(object? parameter = null)
        => await RunBusyAsync(async () =>
        {
            await LoadBookingsAsync();
            _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildMonth();
        });

    // ════════════════════════════════════════════════════════════════════════
    //  Commands
    // ════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private Task RefreshAsync() => RunBusyAsync(async () =>
    {
        await LoadBookingsAsync();
        BuildMonth(SelectedDay?.Date);
    });

    [RelayCommand]
    private void PreviousMonth()
    {
        _month = _month.AddMonths(-1);
        BuildMonth();
    }

    [RelayCommand]
    private void NextMonth()
    {
        _month = _month.AddMonths(1);
        BuildMonth();
    }

    [RelayCommand]
    private void GoToday()
    {
        _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuildMonth(DateTime.Today);
    }

    /// <summary>Day cell clicked — jump to that month when it is dimmed out.</summary>
    [RelayCommand]
    private void SelectDay(CalendarDayCell? cell)
    {
        if (cell is null)
        {
            return;
        }

        if (cell.Date.Year != _month.Year || cell.Date.Month != _month.Month)
        {
            _month = new DateTime(cell.Date.Year, cell.Date.Month, 1);
            BuildMonth(cell.Date);
            return;
        }

        SelectedDay = cell;
    }

    /// <summary>Open the booking behind a chip / selected-day row.</summary>
    [RelayCommand]
    private Task OpenBookingAsync(int bookingId)
        => _navigation.NavigateAsync<BookingsViewModel>(bookingId);

    // ════════════════════════════════════════════════════════════════════════
    //  Grid construction
    // ════════════════════════════════════════════════════════════════════════

    private async Task LoadBookingsAsync()
    {
        var rows = await _bookingService.ListAsync(search: null, status: null, maxResults: 2000);

        // Cancelled/expired bookings release their dates — they never occupy the calendar.
        _bookings = rows
            .Where(b => b.Status is not (BookingStatus.Cancelled or BookingStatus.Expired))
            .ToList();
    }

    private void BuildMonth(DateTime? preferredDate = null)
    {
        var monthStart = new DateTime(_month.Year, _month.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var gridStart = monthStart.AddDays(-(int)monthStart.DayOfWeek); // weeks start Sunday
        var today = DateTime.Today;

        // Index each booking onto the days it occupies + collect month totals.
        var byDay = new Dictionary<DateTime, List<Booking>>();
        var inMonth = new List<Booking>();

        foreach (var booking in _bookings)
        {
            var (start, end) = StayWindow(booking);

            for (var day = start; day < end; day = day.AddDays(1))
            {
                if (!byDay.TryGetValue(day, out var list))
                {
                    byDay[day] = list = new List<Booking>();
                }

                list.Add(booking);
            }

            if (start < monthEnd && end > monthStart)
            {
                inMonth.Add(booking);
            }
        }

        Days.Clear();
        for (var i = 0; i < 42; i++) // fixed 6-week grid
        {
            var date = gridStart.AddDays(i);
            byDay.TryGetValue(date, out var dayList);

            var chips = (dayList ?? new List<Booking>())
                .OrderBy(b => b.CheckInDate)
                .ThenBy(b => b.Id)
                .Select(CreateChip)
                .ToList();

            Days.Add(new CalendarDayCell(
                date,
                isCurrentMonth: date.Month == _month.Month,
                isToday: date == today,
                bookings: chips));
        }

        MonthTitle = monthStart.ToString("MMMM yyyy");

        var pendingCount = inMonth.Count(b => b.Status != BookingStatus.Completed);
        MonthSummary = $"{inMonth.Count} booking(s) • {pendingCount} pending • " +
                       $"{inMonth.Count - pendingCount} complete";
        StatusMessage = $"{inMonth.Count} booking(s) in {MonthTitle}";

        SelectedDay = ResolveSelection(preferredDate);
    }

    /// <summary>Preferred date → today → first busy day → first of the month.</summary>
    private CalendarDayCell? ResolveSelection(DateTime? preferredDate)
    {
        if (preferredDate is DateTime preferred
            && Days.FirstOrDefault(c => c.Date == preferred.Date) is { } exact)
        {
            return exact;
        }

        if (Days.FirstOrDefault(c => c.IsCurrentMonth && c.Date == DateTime.Today) is { } todaysCell)
        {
            return todaysCell;
        }

        return Days.FirstOrDefault(c => c.IsCurrentMonth && c.HasBookings)
               ?? Days.FirstOrDefault(c => c.Date == new DateTime(_month.Year, _month.Month, 1));
    }

    /// <summary>
    /// Calendar window a booking holds: [check-in, check-out) for range stays —
    /// matching conflict semantics, so the checkout day is free — and a single
    /// day for tours.
    /// </summary>
    private static (DateTime Start, DateTime End) StayWindow(Booking booking)
    {
        var start = booking.CheckInDate.Date;

        if (booking.OvernightStayType is OvernightStayType.Overnight
            or OvernightStayType.TwentyTwoHourExclusive)
        {
            var end = booking.CheckOutDate.Date;
            return end > start ? (start, end) : (start, start.AddDays(1));
        }

        return (start, start.AddDays(1));
    }

    private static CalendarBookingChip CreateChip(Booking booking)
    {
        var isComplete = booking.Status == BookingStatus.Completed;

        var range = booking.OvernightStayType is OvernightStayType.Overnight
            or OvernightStayType.TwentyTwoHourExclusive
            ? $"{booking.CheckInDate:MMM d} → {booking.CheckOutDate:MMM d}"
            : booking.CheckInDate.ToString("MMM d");

        var guest = booking.Guest?.FullName ?? $"Booking #{booking.Id}";

        return new CalendarBookingChip(
            BookingId: booking.Id,
            GuestName: guest,
            StatusGroup: isComplete ? "Complete" : "Pending",
            StatusColor: isComplete ? BookingStatus.Completed : BookingStatus.PendingDeposit,
            DateRangeLabel: range,
            StayLabel: booking.StayLabel,
            AmountLabel: $"₱{booking.TotalAmount:N2}",
            Tooltip: $"{guest}\n" +
                     $"Booking #{booking.Id} • {booking.OvernightStayType}\n" +
                     $"{booking.CheckInDate:MMM d, yyyy hh:mm tt} → {booking.CheckOutDate:MMM d, yyyy hh:mm tt}\n" +
                     $"Status: {booking.Status} • Balance: ₱{booking.RemainingBalance:N2}");
    }

    partial void OnSelectedDayChanged(CalendarDayCell? value)
    {
        if (_selectedCell is not null)
        {
            _selectedCell.IsSelected = false;
        }

        _selectedCell = value;

        if (value is null)
        {
            SelectedDayTitle = "No date selected";
            SelectedDaySummary = string.Empty;
            return;
        }

        value.IsSelected = true;
        SelectedDayTitle = value.Date.ToString("dddd, MMMM d, yyyy");

        var pending = value.Bookings.Count(c => c.StatusColor == BookingStatus.PendingDeposit);
        var complete = value.Bookings.Count - pending;

        SelectedDaySummary = value.Bookings.Count == 0
            ? "No bookings on this date"
            : $"{value.Bookings.Count} booking(s) • {pending} pending • {complete} complete";
    }
}
