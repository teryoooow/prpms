using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Mobile.Views;

public partial class PaymentsPage : ContentPage
{
    private readonly IPaymentService _payments;
    private readonly IDashboardInvalidator _invalidator;
    private int _queryVersion;
    private bool _hydrated;

    public PaymentsPage()
        : this(
            IPlatformApplication.Current!.Services.GetRequiredService<IPaymentService>(),
            IPlatformApplication.Current.Services.GetRequiredService<IDashboardInvalidator>())
    {
    }

    public PaymentsPage(IPaymentService payments, IDashboardInvalidator invalidator)
    {
        InitializeComponent();

        _payments = payments;
        _invalidator = invalidator;

        MethodPicker.ItemsSource = new[]
        {
            "All",
            nameof(PaymentMethod.GCash),
            nameof(PaymentMethod.Maya),
            nameof(PaymentMethod.BankTransfer),
            nameof(PaymentMethod.Cash),
            nameof(PaymentMethod.CreditCard)
        };
        MethodPicker.SelectedIndex = 0;

        TypePicker.ItemsSource = new[]
        {
            "All",
            nameof(PaymentType.Downpayment),
            nameof(PaymentType.FinalBalance),
            nameof(PaymentType.SecurityDeposit),
            nameof(PaymentType.Refund)
        };
        TypePicker.SelectedIndex = 0;

        // DatePicker always holds a value — use a wide default range so the
        // ledger is unbounded until the user narrows it.
        FromPicker.Date = new DateTime(2000, 1, 1);
        ToPicker.Date = new DateTime(2100, 1, 1);

        _invalidator.RefreshRequested += OnRefreshRequested;
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(async () => await LoadSafeAsync());

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _hydrated = true;
        await LoadAsync();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        => await LoadSafeAsync();

    private async void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_hydrated) return;
        await LoadSafeAsync();
    }

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is PaymentRow row)
        {
            await Shell.Current.GoToAsync($"booking/detail?id={row.BookingId}");
        }
    }

    private async Task LoadSafeAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch
        {
            // Best-effort live filtering.
        }
    }

    private async Task LoadAsync()
    {
        var version = ++_queryVersion;

        var method = MethodPicker.SelectedItem as string;
        var type = TypePicker.SelectedItem as string;

        var filter = new PaymentFilter(
            Search: string.IsNullOrWhiteSpace(SearchEntry.Text) ? null : SearchEntry.Text,
            Method: method is null or "All"
                ? null
                : Enum.Parse<PaymentMethod>(method),
            Type: type is null or "All"
                ? null
                : Enum.Parse<PaymentType>(type),
            From: FromPicker.Date <= new DateTime(2000, 1, 2) ? null : FromPicker.Date.Date,
            To: ToPicker.Date >= new DateTime(2099, 12, 31) ? null : ToPicker.Date.Date.AddDays(1));

        var rows = await _payments.QueryAsync(filter);

        if (version != _queryVersion)
        {
            return;
        }

        PaymentsList.ItemsSource = rows;

        var total = rows
            .Where(r => r.PaymentType is not PaymentType.Refund)
            .Sum(r => r.AmountPaid)
            - rows.Where(r => r.PaymentType == PaymentType.Refund).Sum(r => r.AmountPaid);

        TotalLabel.Text = $"{rows.Count} transaction(s)  ·  ₱{total:N2} shown";
    }
}
