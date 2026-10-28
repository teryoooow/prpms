using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>
/// Cross-booking payment ledger: filter by method/type/date/search, export CSV.
/// </summary>
public partial class PaymentsViewModel : ViewModelBase, INavigationAware
{
    private readonly IPaymentService _paymentService;
    private readonly IReceiptService _receiptService;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    // ── Filters ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedMethodFilter = FilterAll;
    [ObservableProperty] private string selectedTypeFilter = FilterAll;
    [ObservableProperty] private DateTime? fromDate;
    [ObservableProperty] private DateTime? toDate;

    public const string FilterAll = "All";

    public IReadOnlyList<string> MethodFilters { get; } = new[]
    {
        FilterAll, nameof(Models.PaymentMethod.GCash), nameof(Models.PaymentMethod.Maya),
        nameof(Models.PaymentMethod.BankTransfer), nameof(Models.PaymentMethod.Cash),
        nameof(Models.PaymentMethod.CreditCard)
    };

    public IReadOnlyList<string> TypeFilters { get; } = new[]
    {
        FilterAll, nameof(Models.PaymentType.Downpayment), nameof(Models.PaymentType.FinalBalance),
        nameof(Models.PaymentType.SecurityDeposit), nameof(Models.PaymentType.Refund)
    };

    // ── Results ──────────────────────────────────────────────────────────────
    public ObservableCollection<PaymentRow> Rows { get; } = new();

    [ObservableProperty] private decimal visibleTotal;

    private int _queryVersion;

    public PaymentsViewModel(
        IPaymentService paymentService,
        IReceiptService receiptService,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _paymentService = paymentService;
        _receiptService = receiptService;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "Payments";
    }

    public override Task OnNavigatedToAsync(object? parameter = null)
        => RunBusyAsync(LoadAsync);

    // ── Filter hooks (fire-and-forget, version-guarded) ─────────────────────
    partial void OnSearchTextChanged(string value) => _ = LoadSafeAsync();
    partial void OnSelectedMethodFilterChanged(string value) => _ = LoadSafeAsync();
    partial void OnSelectedTypeFilterChanged(string value) => _ = LoadSafeAsync();
    partial void OnFromDateChanged(DateTime? value) => _ = LoadSafeAsync();
    partial void OnToDateChanged(DateTime? value) => _ = LoadSafeAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunBusyAsync(LoadAsync);

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

        var filter = new PaymentFilter(
            Search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            Method: SelectedMethodFilter == FilterAll
                ? null
                : Enum.Parse<Models.PaymentMethod>(SelectedMethodFilter),
            Type: SelectedTypeFilter == FilterAll
                ? null
                : Enum.Parse<Models.PaymentType>(SelectedTypeFilter),
            From: FromDate is DateTime from ? from.Date : null,
            To: ToDate is DateTime to ? to.Date.AddDays(1) : null); // inclusive end date

        var rows = await _paymentService.QueryAsync(filter);

        if (version != _queryVersion)
        {
            return; // stale query result
        }

        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        VisibleTotal = Rows
            .Where(r => r.PaymentType is not Models.PaymentType.Refund)
            .Sum(r => r.AmountPaid)
            - Rows.Where(r => r.PaymentType == Models.PaymentType.Refund).Sum(r => r.AmountPaid);

        StatusMessage = $"{Rows.Count} transaction(s)  •  ₱{VisibleTotal:N2} shown";
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    /// <summary>Jump to the booking behind a ledger row.</summary>
    [RelayCommand]
    private Task OpenBookingAsync(PaymentRow? row)
        => row is null
            ? Task.CompletedTask
            : _navigation.NavigateAsync<BookingsViewModel>(row.BookingId);

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var path = _dialogs.PickSaveFilePath(
            "CSV file (*.csv)|*.csv",
            $"PRPMS-Payments-{DateTime.Now:yyyyMMdd}.csv");

        if (path is null)
        {
            return;
        }

        try
        {
            _receiptService.ExportPaymentsCsv(Rows, path);
            _dialogs.ShowInfo("Export complete", $"Payment ledger exported to:\n{path}");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Export failed", ex.Message);
        }

        await Task.CompletedTask;
    }
}
