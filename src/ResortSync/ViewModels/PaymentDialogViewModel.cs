using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// Modal for recording a partial payment against a booking.
/// The owning window subscribes to <see cref="RequestClose"/> and closes with
/// DialogResult = true when <see cref="Result"/> is populated.
/// </summary>
public partial class PaymentDialogViewModel : ObservableObject
{
    private readonly IPaymentService _paymentService;
    private readonly int _bookingId;

    public event EventHandler<bool>? RequestClose;

    /// <summary>The transaction to persist (null until saved).</summary>
    public PaymentTransaction? Result { get; private set; }

    // ── Form state ───────────────────────────────────────────────────────────
    [ObservableProperty] private string guestSummary = string.Empty;
    [ObservableProperty] private decimal outstandingBalance;
    [ObservableProperty] private decimal amountPaid;
    [ObservableProperty] private PaymentType paymentType = PaymentType.Downpayment;
    [ObservableProperty] private PaymentMethod paymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private string referenceNumber = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string validationMessage = string.Empty;

    public IReadOnlyList<PaymentType> PaymentTypes { get; } = Enum.GetValues<PaymentType>();
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>();

    public PaymentDialogViewModel(
        IServiceProvider services, int bookingId, decimal suggestedAmount)
    {
        _bookingId = bookingId;
        _paymentService = (IPaymentService)services.GetService(typeof(IPaymentService))!;

        _ = LoadAsync(suggestedAmount);
    }

    private async Task LoadAsync(decimal suggestedAmount)
    {
        var booking = await _paymentService.GetBookingWithPaymentsAsync(_bookingId);
        if (booking is null)
        {
            ValidationMessage = $"Booking #{_bookingId} was not found.";
            return;
        }

        GuestSummary =
            $"#{booking.Id}  •  {booking.Guest?.FullName}  •  Total ₱{booking.TotalAmount:N2}  •  " +
            $"Balance ₱{booking.RemainingBalance:N2}";

        OutstandingBalance = booking.RemainingBalance;
        AmountPaid = suggestedAmount > 0 ? suggestedAmount : booking.RemainingBalance;

        // Sensible default: first money in = downpayment, everything after = balance.
        PaymentType = booking.HasDownpayment()
            ? PaymentType.FinalBalance
            : PaymentType.Downpayment;
    }

    [RelayCommand]
    private void SetAmount(decimal value) => AmountPaid = Math.Max(0m, value);

    /// <summary>Quick fill: half of the outstanding balance (common split payment).</summary>
    [RelayCommand]
    private void SetHalfAmount()
        => AmountPaid = Math.Round(OutstandingBalance / 2m, 2, MidpointRounding.AwayFromZero);

    [RelayCommand]
    private void Save()
    {
        ValidationMessage = string.Empty;

        if (AmountPaid <= 0)
        {
            ValidationMessage = "Enter an amount greater than zero.";
            return;
        }

        if (PaymentMethod is not PaymentMethod.Cash
            && string.IsNullOrWhiteSpace(ReferenceNumber)
            && PaymentType is not PaymentType.Refund)
        {
            ValidationMessage = "Reference number is required for non-cash payments.";
            return;
        }

        Result = new PaymentTransaction
        {
            BookingId = _bookingId,
            AmountPaid = AmountPaid,
            PaymentType = PaymentType,
            PaymentMethod = PaymentMethod,
            ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? null : ReferenceNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            PaymentDate = DateTime.Now
        };

        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);
}

internal static class BookingPaymentExtensions
{
    public static bool HasDownpayment(this Booking booking)
        => booking.Payments.Any(p => p.PaymentType == PaymentType.Downpayment)
           || booking.Status is BookingStatus.Confirmed or BookingStatus.FullyPaid
               or BookingStatus.Completed;
}
