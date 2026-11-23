using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Mobile.Views;

[QueryProperty(nameof(BookingId), "id")]
[QueryProperty(nameof(SuggestedBalance), "bal")]
public partial class RecordPaymentPage : ContentPage
{
    private readonly IPaymentService _payments;

    private int _bookingId;
    private decimal _suggestedBalance;

    public string BookingId
    {
        set => int.TryParse(value, out _bookingId);
    }

    public string SuggestedBalance
    {
        set
        {
            if (decimal.TryParse(value, out var parsed))
            {
                _suggestedBalance = parsed;
            }
        }
    }

    public RecordPaymentPage()
        : this(IPlatformApplication.Current!.Services.GetRequiredService<IPaymentService>())
    {
    }

    public RecordPaymentPage(IPaymentService payments)
    {
        InitializeComponent();

        _payments = payments;

        TypePicker.ItemsSource = new[]
        {
            nameof(PaymentType.Downpayment),
            nameof(PaymentType.FinalBalance),
            nameof(PaymentType.SecurityDeposit)
        };
        TypePicker.SelectedIndex = 0;

        MethodPicker.ItemsSource = new[]
        {
            nameof(PaymentMethod.GCash),
            nameof(PaymentMethod.Maya),
            nameof(PaymentMethod.BankTransfer),
            nameof(PaymentMethod.Cash),
            nameof(PaymentMethod.CreditCard)
        };
        MethodPicker.SelectedIndex = 0;

        DatePicker.Date = DateTime.Today;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        HeadingLabel.Text = $"Booking #{_bookingId} — remaining balance ₱{_suggestedBalance:N2}";
        if (string.IsNullOrWhiteSpace(AmountEntry.Text))
        {
            AmountEntry.Text = _suggestedBalance > 0 ? _suggestedBalance.ToString("0.00") : string.Empty;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(AmountEntry.Text, out var amount) || amount <= 0)
        {
            await DisplayAlert("Invalid amount", "Enter an amount greater than zero.", "OK");
            return;
        }

        var payment = new PaymentTransaction
        {
            BookingId = _bookingId,
            AmountPaid = amount,
            PaymentType = Enum.Parse<PaymentType>((string)TypePicker.SelectedItem!),
            PaymentMethod = Enum.Parse<PaymentMethod>((string)MethodPicker.SelectedItem!),
            PaymentDate = DatePicker.Date,
            ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceEntry.Text) ? null : ReferenceEntry.Text.Trim(),
            Notes = string.IsNullOrWhiteSpace(NotesEntry.Text) ? null : NotesEntry.Text.Trim()
        };

        SaveButton.IsEnabled = false;
        try
        {
            await _payments.RecordPaymentAsync(_bookingId, payment);
            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("Payment failed", ex.Message, "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }
}
