using Microsoft.Maui.Controls.Shapes;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Mobile.Views;

[QueryProperty(nameof(BookingId), "id")]
public partial class BookingDetailPage : ContentPage
{
    private readonly IBookingService _bookings;
    private readonly IPaymentService _payments;
    private readonly StatusColorConverter _statusColor = new();

    private int _id;
    private Booking? _detail;

    public string BookingId
    {
        set => int.TryParse(value, out _id);
    }

    public BookingDetailPage()
        : this(IPlatformApplication.Current!.Services.GetRequiredService<IBookingService>(),
               IPlatformApplication.Current!.Services.GetRequiredService<IPaymentService>())
    {
    }

    public BookingDetailPage(IBookingService bookings, IPaymentService payments)
    {
        InitializeComponent();
        _bookings = bookings;
        _payments = payments;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var booking = await _bookings.GetAsync(_id);
        if (booking is null)
        {
            await DisplayAlert("Not found", $"Booking #{_id} no longer exists.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        _detail = booking;

        IdLabel.Text = $"#{booking.Id}";
        StatusLabel.Text = booking.Status.ToString();
        StatusLabel.BackgroundColor = (Color)_statusColor.Convert(booking.Status, typeof(Color), null!, null!);
        GuestLabel.Text = booking.Guest.FullName;

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(booking.Guest.Phone)) contact.Add(booking.Guest.Phone);
        if (!string.IsNullOrWhiteSpace(booking.Guest.Email)) contact.Add(booking.Guest.Email);
        ContactLabel.Text = string.Join("  ·  ", contact);

        DatesLabel.Text = $"{booking.CheckInDate:MMM dd, yyyy}  →  {booking.CheckOutDate:MMM dd, yyyy}";
        TypeLabel.Text = $"Type: {booking.StayLabel}";
        GuestsLabel.Text = $"Guests: {booking.BaseGuests} base + {booking.AdditionalGuests} extra × ₱{booking.PricePerExtraGuest:N2}";

        DeadlineLabel.Text = booking.Status switch
        {
            BookingStatus.PendingDeposit => booking.IsDepositOverdue
                ? $"Deposit overdue (deadline {booking.DepositDeadline:MMM dd, h:mm tt})"
                : $"Deposit deadline: {booking.DepositDeadline:MMM dd, h:mm tt}",
            BookingStatus.Cancelled => "Cancelled — dates released",
            BookingStatus.Expired => "Expired — deposit window lapsed",
            _ => string.Empty
        };
        NotesLabel.Text = string.IsNullOrWhiteSpace(booking.Notes) ? string.Empty : $"Notes: {booking.Notes}";

        TotalLabel.Text = $"₱{booking.TotalAmount:N2}";
        DepositLabel.Text = $"₱{booking.DepositRequired:N2}";
        var paid = booking.Payments
            .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
            .Sum(p => p.AmountPaid);
        PaidLabel.Text = $"₱{paid:N2}";
        BalanceLabel.Text = $"₱{booking.RemainingBalance:N2}";
        BalanceLabel.TextColor = booking.RemainingBalance > 0 ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32");

        // Add-ons
        AddonsStack.Clear();
        if (booking.BookingAddons.Count == 0)
        {
            AddonsStack.Children.Add(new Label { Text = "None", TextColor = Colors.Gray, FontSize = 13 });
        }
        else
        {
            foreach (var line in booking.BookingAddons)
            {
                AddonsStack.Children.Add(new Label
                {
                    FontSize = 13,
                    TextColor = Color.FromArgb("#212121"),
                    Text = $"{line.Addon.Name}  ×{line.Quantity}   ₱{line.LineTotal:N2}"
                });
            }
        }

        // Payments
        PaymentsStack.Clear();
        var payments = booking.Payments.OrderByDescending(p => p.PaymentDate).ToList();
        if (payments.Count == 0)
        {
            PaymentsStack.Children.Add(new Label { Text = "No payments yet", TextColor = Colors.Gray, FontSize = 13 });
        }
        else
        {
            foreach (var payment in payments)
            {
                PaymentsStack.Children.Add(BuildPaymentRow(payment));
            }
        }
    }

    private View BuildPaymentRow(PaymentTransaction payment)
    {
        var info = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#212121"),
                    Text = $"{payment.PaymentType}  ₱{payment.AmountPaid:N2}"
                },
                new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#777777"),
                    Text = $"{payment.PaymentDate:MMM dd, yyyy} · {payment.PaymentMethod}" +
                           (string.IsNullOrWhiteSpace(payment.ReferenceNumber) ? "" : $" · ref {payment.ReferenceNumber}")
                }
            }
        };

        var voidButton = new Button
        {
            Text = "Void",
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White
        };
        voidButton.Clicked += async (_, _) => await VoidPaymentAsync(payment);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Children.Add(info);
        Grid.SetColumn(voidButton, 1);
        grid.Children.Add(voidButton);

        return new Border
        {
            Padding = new Thickness(10, 8),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = grid
        };
    }

    // -- Actions -------------------------------------------------------------

    private async void OnRecordPaymentClicked(object? sender, EventArgs e)
    {
        if (_detail is null) return;
        await Shell.Current.GoToAsync($"payment/record?id={_detail.Id}&bal={_detail.RemainingBalance}");
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_detail is null) return;
        await Shell.Current.GoToAsync($"booking/new?id={_detail.Id}");
    }

    private async void OnMarkCompletedClicked(object? sender, EventArgs e)
        => await TransitionAsync(BookingStatus.Completed, confirmOutstanding: true);

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await TransitionAsync(BookingStatus.Cancelled);

    private async void OnReopenClicked(object? sender, EventArgs e)
    {
        if (_detail is null) return;

        // Domain rules: Cancelled/Expired reopen as PendingDeposit (fresh deposit window);
        // Completed reopens as FullyPaid or Confirmed (money already settled).
        var target = _detail.Status switch
        {
            BookingStatus.Completed when _detail.RemainingBalance <= 0m => BookingStatus.FullyPaid,
            BookingStatus.Completed => BookingStatus.Confirmed,
            _ => BookingStatus.PendingDeposit
        };
        await TransitionAsync(target);
    }

    private async Task TransitionAsync(BookingStatus target, bool confirmOutstanding = false)
    {
        if (_detail is null) return;

        var verb = target switch
        {
            BookingStatus.Completed => "mark as Completed (check-out done)?",
            BookingStatus.Cancelled => "cancel this booking and release its dates?",
            BookingStatus.PendingDeposit => "reopen with a fresh 24-hour deposit window?",
            BookingStatus.FullyPaid when _detail.Status == BookingStatus.Completed => "reopen as FullyPaid?",
            BookingStatus.Confirmed when _detail.Status == BookingStatus.Completed => "reopen as Confirmed?",
            _ => $"set status to {target}?"
        };

        if (confirmOutstanding && _detail.RemainingBalance > 0)
        {
            var proceed = await DisplayAlert("Outstanding balance",
                $"Booking #{_detail.Id} still has ₱{_detail.RemainingBalance:N2} outstanding.\n\nComplete it anyway?",
                "Complete", "Back");
            if (!proceed) return;
        }
        else if (!await DisplayAlert($"Confirm: {target}", $"Do you want to {verb}", "Yes", "No"))
        {
            return;
        }

        try
        {
            var updated = await _bookings.UpdateStatusAsync(_detail.Id, target);
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("Not allowed", ex.Message, "OK");
        }
    }

    private async Task VoidPaymentAsync(PaymentTransaction payment)
    {
        if (_detail is null) return;

        var confirmed = await DisplayAlert("Void payment",
            $"Void the {payment.PaymentType} of ₱{payment.AmountPaid:N2} ({payment.PaymentMethod}) on booking #{_detail.Id}?",
            "Void", "Back");
        if (!confirmed) return;

        try
        {
            await _payments.VoidPaymentAsync(payment.Id);
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("Void failed", ex.Message, "OK");
        }
    }
}
