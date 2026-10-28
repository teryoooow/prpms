using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// All Bookings: searchable/status-filtered grid + detail pane with payments,
/// receipt printing, and lifecycle actions (confirm / complete / cancel).
/// </summary>
public partial class BookingsViewModel : ViewModelBase, INavigationAware
{
    private readonly IBookingService _bookingService;
    private readonly IPaymentService _paymentService;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    // ── Filters ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedStatusFilter = StatusFilterAll;

    public const string StatusFilterAll = "All statuses";

    public IReadOnlyList<string> StatusFilters { get; } = new[]
    {
        StatusFilterAll,
        nameof(BookingStatus.PendingDeposit),
        nameof(BookingStatus.Confirmed),
        nameof(BookingStatus.FullyPaid),
        nameof(BookingStatus.Completed),
        nameof(BookingStatus.Cancelled),
        nameof(BookingStatus.Expired)
    };

    // ── Master list ──────────────────────────────────────────────────────────
    public ObservableCollection<Booking> Bookings { get; } = new();

    [ObservableProperty] private Booking? selectedBooking;

    // ── Detail (loaded with full payments/addons graph) ──────────────────────
    [ObservableProperty] private Booking? detail;

    public ObservableCollection<PaymentTransaction> DetailPayments { get; } = new();

    [ObservableProperty] private decimal detailPaid;
    [ObservableProperty] private bool hasDetail;

    public BookingsViewModel(
        IBookingService bookingService,
        IPaymentService paymentService,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "All Bookings";
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
        => await RunBusyAsync(async () =>
        {
            await RefreshListAsync();

            // Arriving from a dashboard alert / post-save: preselect the booking.
            if (parameter is int bookingId && Bookings.FirstOrDefault(b => b.Id == bookingId) is { } match)
            {
                SelectedBooking = match;
                await LoadDetailAsync(match.Id);
            }
            else if (HasDetail && Detail is not null)
            {
                await LoadDetailAsync(Detail.Id);
            }
        });

    // ════════════════════════════════════════════════════════════════════════
    //  List
    // ════════════════════════════════════════════════════════════════════════

    partial void OnSearchTextChanged(string value) => _ = SafeRefreshAsync();
    partial void OnSelectedStatusFilterChanged(string value) => _ = SafeRefreshAsync();

    /// <summary>
    /// Filter changes bypass the busy guard (typing must never be dropped) but are
    /// version-stamped so a slow earlier query cannot overwrite a newer result.
    /// </summary>
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

    [RelayCommand]
    private Task RefreshAsync() => RunBusyAsync(RefreshListAsync);

    [RelayCommand]
    private Task ApplyFiltersAsync() => RunBusyAsync(RefreshListAsync);

    /// <summary>Grid selection → hydrate the detail pane.</summary>
    partial void OnSelectedBookingChanged(Booking? value)
    {
        if (value is null)
        {
            HasDetail = false;
            Detail = null;
            DetailPayments.Clear();
            return;
        }

        _ = RunBusyAsync(() => LoadDetailAsync(value.Id));
    }

    private int _listVersion;

    private async Task RefreshListAsync()
    {
        var version = ++_listVersion;

        BookingStatus? status = SelectedStatusFilter == StatusFilterAll
            ? null
            : Enum.Parse<BookingStatus>(SelectedStatusFilter);

        var rows = await _bookingService.ListAsync(SearchText, status);

        if (version != _listVersion)
        {
            return; // superseded by a newer refresh
        }

        Bookings.Clear();
        foreach (var row in rows)
        {
            Bookings.Add(row);
        }

        StatusMessage = $"{Bookings.Count} booking(s)";
    }

    private async Task LoadDetailAsync(int bookingId)
    {
        var booking = await _paymentService.GetBookingWithPaymentsAsync(bookingId);
        if (booking is null)
        {
            HasDetail = false;
            return;
        }

        Detail = booking;
        HasDetail = true;

        DetailPayments.Clear();
        foreach (var payment in booking.Payments.OrderByDescending(p => p.PaymentDate))
        {
            DetailPayments.Add(payment);
        }

        DetailPaid = booking.Payments
            .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
            .Sum(p => p.AmountPaid);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Actions
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Opens the partial-payment modal and records the result.</summary>
    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (Detail is null)
        {
            return;
        }

        var payment = _dialogs.ShowRecordPaymentDialog(Detail.Id, Detail.RemainingBalance);
        if (payment is null)
        {
            return; // cancelled
        }

        await RunBusyAsync(async () =>
        {
            try
            {
                var updated = await _paymentService.RecordPaymentAsync(Detail.Id, payment);

                StatusMessage =
                    $"Payment of ₱{payment.AmountPaid:N2} recorded. " +
                    $"Balance: ₱{updated.RemainingBalance:N2} ({updated.Status})";

                await RefreshListAsync();
                if (Bookings.FirstOrDefault(b => b.Id == updated.Id) is { } refreshed)
                {
                    SelectedBooking = refreshed;
                }
                await LoadDetailAsync(updated.Id);
            }
            catch (InvalidOperationException ex)
            {
                _dialogs.ShowError("Payment failed", ex.Message);
            }
        });
    }

    [RelayCommand]
    private async Task VoidPaymentAsync(PaymentTransaction? payment)
    {
        if (payment is null || Detail is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Void payment",
                $"Void the {payment.PaymentType} of ₱{payment.AmountPaid:N2} " +
                $"({payment.PaymentMethod}) on booking #{Detail.Id}?"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            try
            {
                await _paymentService.VoidPaymentAsync(payment.Id);
                StatusMessage = "Payment voided.";
                await RefreshListAsync();
                await LoadDetailAsync(Detail.Id);
            }
            catch (InvalidOperationException ex)
            {
                _dialogs.ShowError("Void failed", ex.Message);
            }
        });
    }

    [RelayCommand]
    private Task PrintReceiptAsync()
    {
        if (Detail is not null)
        {
            _dialogs.ShowReceipt(Detail);
        }

        return Task.CompletedTask;
    }

    /// <summary>Mark the stay finished (asks for confirmation when money is outstanding).</summary>
    [RelayCommand]
    private Task MarkCompletedAsync() => TransitionAsync(BookingStatus.Completed, confirmOutstanding: true);

    [RelayCommand]
    private Task CancelBookingAsync() => TransitionAsync(BookingStatus.Cancelled);

    [RelayCommand]
    private Task ReopenBookingAsync() => TransitionAsync(BookingStatus.PendingDeposit);

    [RelayCommand]
    private async Task EditBookingAsync()
    {
        if (Detail is not null)
        {
            await _navigation.NavigateAsync<BookingFormViewModel>(Detail.Id);
        }
    }

    [RelayCommand]
    private Task NewBookingAsync() => _navigation.NavigateAsync<BookingFormViewModel>();

    private async Task TransitionAsync(BookingStatus target, bool confirmOutstanding = false)
    {
        if (Detail is null)
        {
            return;
        }

        var verb = target switch
        {
            BookingStatus.Completed => "mark as Completed (check-out done)",
            BookingStatus.Cancelled => "cancel this booking and release its dates",
            BookingStatus.PendingDeposit => "reopen with a fresh 24-hour deposit window",
            _ => $"set status to {target}"
        };

        if (confirmOutstanding && Detail.RemainingBalance > 0)
        {
            var proceed = _dialogs.Confirm("Outstanding balance",
                $"Booking #{Detail.Id} still has ₱{Detail.RemainingBalance:N2} outstanding.\n\n" +
                "Complete it anyway?");
            if (!proceed)
            {
                return;
            }
        }
        else if (!_dialogs.Confirm($"Confirm: {target}", $"Do you want to {verb}?"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            try
            {
                var updated = await _bookingService.UpdateStatusAsync(Detail.Id, target);
                StatusMessage = $"Booking #{updated.Id} → {updated.Status}";
                await RefreshListAsync();
                if (Bookings.FirstOrDefault(b => b.Id == updated.Id) is { } refreshed)
                {
                    SelectedBooking = refreshed;
                }
                await LoadDetailAsync(updated.Id);
            }
            catch (InvalidOperationException ex)
            {
                _dialogs.ShowError("Not allowed", ex.Message);
            }
        });
    }
}
