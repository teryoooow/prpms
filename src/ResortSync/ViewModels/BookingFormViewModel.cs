using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.ViewModels;

/// <summary>Checkbox + quantity row for one catalog addon on the booking form.</summary>
public partial class AddonOption : ObservableObject
{
    public int AddonId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }

    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private int quantity = 1;

    public string PriceLabel => $"₱{UnitPrice:N2} / unit";

    /// <summary>Live line total — re-announced whenever Quantity changes.</summary>
    public decimal LineTotal => Quantity * UnitPrice;

    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(LineTotal));
}

/// <summary>
/// New / edit booking screen: guest reuse autocomplete, conflict-checked dates,
/// live pricing calculator, 50% downpayment preview, and addon selection.
/// </summary>
public partial class BookingFormViewModel : ViewModelBase, INavigationAware
{
    private readonly IBookingService _bookingService;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    /// <summary>Non-null while editing an existing booking.</summary>
    private Booking? _editing;

    /// <summary>Monotonic token so stale async conflict checks never overwrite fresh ones.</summary>
    private int _conflictCheckVersion;

    // ── Guest identity ───────────────────────────────────────────────────────
    [ObservableProperty] private string guestSearchQuery = string.Empty;
    [ObservableProperty] private Guest? selectedGuest;
    [ObservableProperty] private bool showSuggestions;
    public ObservableCollection<Guest> GuestSuggestions { get; } = new();

    [ObservableProperty] private string guestFullName = string.Empty;
    [ObservableProperty] private string guestPhone = string.Empty;
    [ObservableProperty] private string guestEmail = string.Empty;
    [ObservableProperty] private string guestIdType = string.Empty;
    [ObservableProperty] private string guestIdNumber = string.Empty;
    [ObservableProperty] private string guestNotes = string.Empty;

    // ── Stay window ──────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime checkInDate = DateTime.Today;
    [ObservableProperty] private DateTime checkOutDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private OvernightStayType stayType = OvernightStayType.Overnight;

    public Array StayTypes { get; } = Enum.GetValues(typeof(OvernightStayType));

    // ── Pricing inputs ───────────────────────────────────────────────────────
    [ObservableProperty] private int baseGuests = 2;
    [ObservableProperty] private int additionalGuests;
    [ObservableProperty] private decimal pricePerExtraGuest;
    [ObservableProperty] private decimal baseAmount;

    public ObservableCollection<AddonOption> Addons { get; } = new();

    // ── Live calculator output ───────────────────────────────────────────────
    [ObservableProperty] private decimal extraGuestCharge;
    [ObservableProperty] private decimal addonsTotal;
    [ObservableProperty] private decimal totalAmount;
    [ObservableProperty] private decimal depositRequired;
    [ObservableProperty] private decimal remainingBalance;
    private decimal _downpaymentPercent = Constants.AppConstants.DefaultDownpaymentPercent;

    /// <summary>Loaded from Settings (default 50%); drives deposit math.</summary>
    public decimal DownpaymentPercent
    {
        get => _downpaymentPercent;
        set
        {
            if (SetProperty(ref _downpaymentPercent, value))
            {
                OnPropertyChanged(nameof(DepositPercentLabel));
            }
        }
    }

    /// <summary>Live "Required downpayment (50%)" label tied to the configured rate.</summary>
    public string DepositPercentLabel => $"Required downpayment ({DownpaymentPercent:P0})";

    // ── Validation ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool hasConflicts;
    [ObservableProperty] private string conflictMessage = string.Empty;
    [ObservableProperty] private bool isEditMode;

    public BookingFormViewModel(
        IBookingService bookingService,
        ISettingsService settings,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _bookingService = bookingService;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "New Booking";
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════════════

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        await RunBusyAsync(async () =>
        {
            _editing = null;
            IsEditMode = false;
            Title = "New Booking";

            // Catalog + defaults.
            var settings = await _settings.LoadAsync();
            DownpaymentPercent = settings.DownpaymentPercent;
            PricePerExtraGuest = settings.DefaultExtraGuestRate;
            BaseGuests = settings.DefaultBaseGuests;

            // Always reload the addon catalog (prices may have changed).
            var catalog = await _bookingService.GetActiveAddonsAsync();
            var previousSelections = Addons.ToDictionary(a => a.AddonId);
            Addons.Clear();
            foreach (var addon in catalog)
            {
                var option = new AddonOption
                {
                    AddonId = addon.Id,
                    Name = addon.Name,
                    UnitPrice = addon.UnitPrice
                };

                // Preserve selections made before a navigation refresh.
                if (previousSelections.TryGetValue(addon.Id, out var old))
                {
                    option.IsSelected = old.IsSelected;
                    option.Quantity = old.Quantity;
                }

                option.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(AddonOption.IsSelected)
                        or nameof(AddonOption.Quantity))
                    {
                        Recalculate();
                    }
                };
                Addons.Add(option);
            }

            // Edit mode: parameter is a booking Id.
            if (parameter is int bookingId)
            {
                var booking = await _bookingService.GetAsync(bookingId);
                if (booking is null)
                {
                    _dialogs.ShowError("Not found", $"Booking #{bookingId} no longer exists.");
                    return;
                }

                await LoadEditableAsync(booking);
            }
            else
            {
                ResetForm();
            }

            Recalculate();
            await ValidateDatesAsync();
            StatusMessage = IsEditMode
                ? $"Editing booking #{_editing?.Id}"
                : $"Downpayment window: {AppConstants_DeadlineHours()}h from save";
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Guest autocomplete
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Bound to the search TextBox TextChanged behavior.</summary>
    [RelayCommand]
    private async Task SearchGuestsAsync()
    {
        // Picked (or loaded) guest whose name still matches the box → nothing to search.
        if (SelectedGuest is not null
            && string.Equals(GuestSearchQuery.Trim(), SelectedGuest.FullName.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        // The query diverged from the picked record: detach so Save creates/updates correctly.
        SelectedGuest = null;

        var results = await _bookingService.SearchGuestsAsync(GuestSearchQuery);

        GuestSuggestions.Clear();
        foreach (var guest in results)
        {
            GuestSuggestions.Add(guest);
        }

        ShowSuggestions = GuestSuggestions.Count > 0 && !string.IsNullOrWhiteSpace(GuestSearchQuery);
    }

    [RelayCommand]
    private void PickGuest(Guest? guest)
    {
        if (guest is null)
        {
            return;
        }

        SelectedGuest = guest;
        GuestSearchQuery = guest.FullName;

        GuestFullName = guest.FullName;
        GuestPhone = guest.Phone;
        GuestEmail = guest.Email;
        GuestIdType = guest.IdType;
        GuestIdNumber = guest.IdNumber;
        GuestNotes = guest.Notes ?? string.Empty;

        ShowSuggestions = false;
        StatusMessage = $"Returning guest loaded: {guest.FullName}";
    }

    [RelayCommand]
    private void ClearGuest()
    {
        SelectedGuest = null;
        GuestSearchQuery = string.Empty;
        GuestFullName = string.Empty;
        GuestPhone = string.Empty;
        GuestEmail = string.Empty;
        GuestIdType = string.Empty;
        GuestIdNumber = string.Empty;
        GuestNotes = string.Empty;
        ShowSuggestions = false;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Live pricing + conflict validation
    // ════════════════════════════════════════════════════════════════════════

    partial void OnBaseAmountChanged(decimal value) => Recalculate();
    partial void OnAdditionalGuestsChanged(int value) => Recalculate();
    partial void OnPricePerExtraGuestChanged(decimal value) => Recalculate();

    partial void OnCheckInDateChanged(DateTime value)
    {
        AlignCheckoutToStayType();
        _ = ValidateDatesAsync();
    }

    partial void OnCheckOutDateChanged(DateTime value) => _ = ValidateDatesAsync();

    partial void OnStayTypeChanged(OvernightStayType value)
    {
        AlignCheckoutToStayType();
        _ = ValidateDatesAsync();
    }

    /// <summary>Tours are single-day: check-out mirrors check-in.</summary>
    private void AlignCheckoutToStayType()
    {
        var isRange = StayType is OvernightStayType.Overnight
            or OvernightStayType.TwentyTwoHourExclusive;

        if (!isRange)
        {
            if (CheckOutDate != CheckInDate)
            {
                CheckOutDate = CheckInDate; // fires OnCheckOutDateChanged → validation
            }
        }
        else if (CheckOutDate <= CheckInDate)
        {
            CheckOutDate = CheckInDate.AddDays(1);
        }
    }

    /// <summary>Recomputes ExtraCharge/Addons/Total/Deposit/Remaining for display.</summary>
    private void Recalculate()
    {
        AddonsTotal = Addons
            .Where(a => a.IsSelected)
            .Sum(a => a.UnitPrice * Math.Max(0, a.Quantity));

        var result = PricingCalculator.Calculate(
            BaseAmount, AdditionalGuests, PricePerExtraGuest, AddonsTotal, DownpaymentPercent);

        ExtraGuestCharge = result.ExtraGuestCharge;
        TotalAmount = result.TotalAmount;
        DepositRequired = result.DepositRequired;
        RemainingBalance = result.RemainingBalance;
    }

    /// <summary>Async double-booking probe; results are versioned to drop stale replies.</summary>
    private async Task ValidateDatesAsync()
    {
        var version = ++_conflictCheckVersion;

        try
        {
            var conflicts = await _bookingService.ValidateDatesAsync(
                CheckInDate, CheckOutDate, StayType, _editing?.Id);

            if (version != _conflictCheckVersion)
            {
                return; // a newer check superseded this one
            }

            HasConflicts = conflicts.Count > 0;
            ConflictMessage = conflicts.Count == 0
                ? string.Empty
                : "Date conflicts:\n" + string.Join("\n", conflicts);
        }
        catch
        {
            // Validation is best-effort; save performs the authoritative check.
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Save / cancel
    // ════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SaveAsync()
    {
        // ── Client-side pre-flight ────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(GuestFullName))
        {
            _dialogs.ShowError("Missing guest", "Please enter the guest's full name.");
            return;
        }

        if (HasConflicts)
        {
            _dialogs.ShowError("Date conflict",
                "The selected dates overlap an existing booking:\n\n" + ConflictMessage);
            return;
        }

        var selectedAddons = Addons
            .Where(a => a.IsSelected && a.Quantity > 0)
            .Select(a => new BookingAddonInput(a.AddonId, a.Quantity, a.UnitPrice))
            .ToList();

        await RunBusyAsync(async () =>
        {
            try
            {
                // Reuse the picked guest row; otherwise start a fresh record.
                var guest = SelectedGuest ?? new Guest();
                guest.FullName = GuestFullName.Trim();
                guest.Phone = GuestPhone.Trim();
                guest.Email = GuestEmail.Trim();
                guest.IdType = GuestIdType.Trim();
                guest.IdNumber = GuestIdNumber.Trim();
                guest.Notes = string.IsNullOrWhiteSpace(GuestNotes) ? null : GuestNotes.Trim();

                var booking = _editing ?? new Booking();
                booking.CheckInDate = CheckInDate;
                booking.CheckOutDate = CheckOutDate;
                booking.OvernightStayType = StayType;
                booking.BaseGuests = BaseGuests;
                booking.AdditionalGuests = AdditionalGuests;
                booking.PricePerExtraGuest = PricePerExtraGuest;
                booking.BaseAmount = BaseAmount;

                var saved = await _bookingService.SaveBookingAsync(guest, booking, selectedAddons);

                if (_editing is null)
                {
                    _dialogs.ShowInfo("Booking saved",
                        $"Booking #{saved.Id} created for {saved.Guest?.FullName ?? guest.FullName}.\n\n" +
                        $"Total: ₱{saved.TotalAmount:N2}\n" +
                        $"Downpayment due (50%): ₱{saved.DepositRequired:N2}\n" +
                        $"Deadline: {saved.DepositDeadline:MMM dd, yyyy hh:mm tt} (24 hours)");
                }
                else
                {
                    _dialogs.ShowInfo("Booking updated",
                        $"Booking #{saved.Id} saved.\nTotal: ₱{saved.TotalAmount:N2}  •  " +
                        $"Balance: ₱{saved.RemainingBalance:N2}");
                }

                await _navigation.NavigateAsync<BookingsViewModel>(saved.Id);
            }
            catch (BookingConflictException ex)
            {
                HasConflicts = true;
                ConflictMessage = string.Join("\n", ex.Conflicts);
                _dialogs.ShowError("Date conflict",
                    "Another booking occupies these dates:\n\n" + ConflictMessage);
            }
            catch (InvalidOperationException ex)
            {
                _dialogs.ShowError("Cannot save", ex.Message);
            }
        });
    }

    [RelayCommand]
    private Task CancelAsync()
        => _navigation.NavigateAsync<DashboardViewModel>();

    // ════════════════════════════════════════════════════════════════════════
    //  Internals
    // ════════════════════════════════════════════════════════════════════════

    private void ResetForm()
    {
        ClearGuest();
        CheckInDate = DateTime.Today;
        CheckOutDate = DateTime.Today.AddDays(1);
        StayType = OvernightStayType.Overnight;
        AdditionalGuests = 0;
        BaseAmount = 0m;
        HasConflicts = false;
        ConflictMessage = string.Empty;
        foreach (var option in Addons)
        {
            option.IsSelected = false;
            option.Quantity = 1;
        }
    }

    private async Task LoadEditableAsync(Booking booking)
    {
        _editing = booking;
        IsEditMode = true;
        Title = $"Edit Booking #{booking.Id}";

        SelectedGuest = booking.Guest;
        GuestSearchQuery = booking.Guest?.FullName ?? string.Empty;
        GuestFullName = booking.Guest?.FullName ?? string.Empty;
        GuestPhone = booking.Guest?.Phone ?? string.Empty;
        GuestEmail = booking.Guest?.Email ?? string.Empty;
        GuestIdType = booking.Guest?.IdType ?? string.Empty;
        GuestIdNumber = booking.Guest?.IdNumber ?? string.Empty;
        GuestNotes = booking.Guest?.Notes ?? string.Empty;

        CheckInDate = booking.CheckInDate;
        CheckOutDate = booking.CheckOutDate;
        StayType = booking.OvernightStayType;
        BaseGuests = booking.BaseGuests;
        AdditionalGuests = booking.AdditionalGuests;
        PricePerExtraGuest = booking.PricePerExtraGuest;
        BaseAmount = booking.BaseAmount;

        // Apply saved addon lines onto the freshly loaded catalog options.
        var chosen = booking.BookingAddons.ToDictionary(x => x.AddonId);
        foreach (var option in Addons)
        {
            if (chosen.TryGetValue(option.AddonId, out var line))
            {
                option.IsSelected = true;
                option.Quantity = line.Quantity;
            }
        }

        if (booking.Payments.Count > 0)
        {
            StatusMessage =
                $"{booking.Payments.Count} payment(s) on file — balance recalculates on save.";
        }

        await Task.CompletedTask; // signature parity with async load paths
    }

    private static int AppConstants_DeadlineHours() => Constants.AppConstants.DepositDeadlineHours;
}
