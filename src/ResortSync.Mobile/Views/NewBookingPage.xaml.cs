using System.Collections.ObjectModel;
using ResortSync.Constants;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Mobile.Views;

[QueryProperty(nameof(EditId), "id")]
public partial class NewBookingPage : ContentPage
{
    private readonly IBookingService _bookings;
    private readonly ISettingsService _settings;

    private Booking? _editing;
    private Guest? _selectedGuest;
    private int _conflictCheckVersion;
    private bool _aligning;
    private bool _loaded;
    private int? _pendingEditId;

    private decimal _downpaymentPercent = AppConstants.DefaultDownpaymentPercent;

    private sealed class AddonRow
    {
        public required int AddonId { get; init; }
        public required decimal UnitPrice { get; init; }
        public required CheckBox Check { get; init; }
        public required Stepper Qty { get; init; }
        public required Label Total { get; init; }
    }

    private readonly List<AddonRow> _addonRows = new();
    private readonly ObservableCollection<Guest> _suggestions = new();

    public string EditId
    {
        set
        {
            if (int.TryParse(value, out var id) && id > 0)
            {
                _pendingEditId = id;
            }
        }
    }

    public NewBookingPage()
        : this(IPlatformApplication.Current!.Services.GetRequiredService<IBookingService>(),
               IPlatformApplication.Current!.Services.GetRequiredService<ISettingsService>())
    {
    }

    public NewBookingPage(IBookingService bookings, ISettingsService settings)
    {
        InitializeComponent();
        _bookings = bookings;
        _settings = settings;

        SuggestionsList.ItemsSource = _suggestions;

        TypePicker.ItemsSource = Enum.GetValues<OvernightStayType>();
        TypePicker.SelectedIndex = (int)OvernightStayType.Overnight;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded && _pendingEditId is null)
        {
            return; // keep state when returning without a new edit request
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loaded = false;
        SaveButton.IsEnabled = false;

        var s = await _settings.LoadAsync();
        _downpaymentPercent = s.DownpaymentPercent;
        DepositCaption.Text = $"Required downpayment ({_downpaymentPercent:P0})";
        DeadlineCaption.Text = $"Deposit window: {AppConstants.DepositDeadlineHours}h from save";

        if (_pendingEditId is int editId)
        {
            var booking = await _bookings.GetAsync(editId);
            if (booking is null)
            {
                await DisplayAlert("Not found", $"Booking #{editId} no longer exists.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Title = $"Edit booking #{booking.Id}";
            Prefill(booking);
        }
        else
        {
            Title = "New booking";
            ResetForm();
            ExtraRateEntry.Text = s.DefaultExtraGuestRate.ToString("0.00");
            BaseGuestsStepper.Value = Math.Clamp(s.DefaultBaseGuests, 1, 20);
        }

        await LoadAddonsAsync();

        // Edit mode: apply the booking's saved addon lines onto the catalog rows.
        if (_editing is { BookingAddons.Count: > 0 })
        {
            var chosen = _editing.BookingAddons.ToDictionary(x => x.AddonId);
            foreach (var row in _addonRows)
            {
                if (chosen.TryGetValue(row.AddonId, out var line))
                {
                    row.Check.IsChecked = true;
                    row.Qty.Value = Math.Clamp(line.Quantity, 1, 99);
                }
            }
        }

        Recalculate();
        await ValidateDatesAsync();
        SaveButton.IsEnabled = true;
        _loaded = true;
    }

    // -- Add-on catalog ------------------------------------------------------

    private async Task LoadAddonsAsync()
    {
        var previous = _addonRows.ToDictionary(
            r => r.AddonId,
            r => (r.Check.IsChecked, r.Qty.Value));

        _addonRows.Clear();
        AddonsStack.Clear();

        var catalog = await _bookings.GetActiveAddonsAsync();
        if (catalog.Count == 0)
        {
            AddonsStack.Children.Add(new Label { Text = "No add-ons available", TextColor = Colors.Gray, FontSize = 13 });
            return;
        }

        foreach (var addon in catalog)
        {
            var check = new CheckBox { IsChecked = false, VerticalOptions = LayoutOptions.Center };
            var qty = new Stepper { Minimum = 1, Maximum = 99, Increment = 1, Value = 1, HorizontalOptions = LayoutOptions.End };
            var total = new Label { FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#212121"), VerticalOptions = LayoutOptions.Center };

            if (previous.TryGetValue(addon.Id, out var old))
            {
                check.IsChecked = old.IsChecked;
                qty.Value = old.Value;
            }

            var nameBlock = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    new Label { FontSize = 14, TextColor = Color.FromArgb("#212121"), Text = addon.Name },
                    new Label { FontSize = 12, TextColor = Color.FromArgb("#777777"), Text = $"₱{addon.UnitPrice:N2} / unit" }
                }
            };

            var grid = new Grid
            {
                ColumnDefinitions = new()
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto),
                    new(GridLength.Auto)
                },
                ColumnSpacing = 8
            };
            Grid.SetColumn(check, 0);
            Grid.SetColumn(nameBlock, 1);
            Grid.SetColumn(qty, 2);
            Grid.SetColumn(total, 3);
            grid.Children.Add(check);
            grid.Children.Add(nameBlock);
            grid.Children.Add(qty);
            grid.Children.Add(total);

            var row = new AddonRow { AddonId = addon.Id, UnitPrice = addon.UnitPrice, Check = check, Qty = qty, Total = total };
            _addonRows.Add(row);

            check.CheckedChanged += (_, _) => Recalculate();
            qty.ValueChanged += (_, _) => Recalculate();

            AddonsStack.Children.Add(grid);
            UpdateRowTotal(row);
        }
    }

    private void UpdateRowTotal(AddonRow row)
    {
        row.Total.Text = row.Check.IsChecked
            ? $"₱{row.UnitPrice * (int)row.Qty.Value:N2}"
            : "—";
    }

    // -- Guest search --------------------------------------------------------

    private async void OnGuestSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_selectedGuest is not null
            && string.Equals(GuestSearch.Text?.Trim(), _selectedGuest.FullName.Trim(), StringComparison.Ordinal))
        {
            return; // picked guest still matches the box
        }

        _selectedGuest = null; // query diverged: detach so Save creates a fresh record
        ReturningBadge.IsVisible = false;

        var results = await _bookings.SearchGuestsAsync(GuestSearch.Text ?? string.Empty);

        _suggestions.Clear();
        foreach (var guest in results)
        {
            _suggestions.Add(guest);
        }

        SuggestionsList.IsVisible = _suggestions.Count > 0 && !string.IsNullOrWhiteSpace(GuestSearch.Text);
    }

    private void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: Guest guest })
        {
            return;
        }

        _selectedGuest = guest;
        GuestSearch.Text = guest.FullName;
        NameEntry.Text = guest.FullName;
        PhoneEntry.Text = guest.Phone;
        EmailEntry.Text = guest.Email;
        IdTypeEntry.Text = guest.IdType;
        IdNumberEntry.Text = guest.IdNumber;
        GuestNotesEntry.Text = guest.Notes ?? string.Empty;

        SuggestionsList.IsVisible = false;
        ReturningBadge.Text = $"Returning guest loaded: {guest.FullName}";
        ReturningBadge.IsVisible = true;
    }

    private void OnClearGuestClicked(object? sender, EventArgs e)
    {
        _selectedGuest = null;
        GuestSearch.Text = string.Empty;
        NameEntry.Text = string.Empty;
        PhoneEntry.Text = string.Empty;
        EmailEntry.Text = string.Empty;
        IdTypeEntry.Text = string.Empty;
        IdNumberEntry.Text = string.Empty;
        GuestNotesEntry.Text = string.Empty;
        SuggestionsList.IsVisible = false;
        ReturningBadge.IsVisible = false;
    }

    // -- Stay dates & type ---------------------------------------------------

    private void OnStayTypeChanged(object? sender, EventArgs e)
    {
        AlignCheckoutToStayType();
        _ = ValidateDatesAsync();
    }

    private void OnDatesChanged(object? sender, EventArgs e)
    {
        if (_aligning)
        {
            return;
        }

        AlignCheckoutToStayType();
        _ = ValidateDatesAsync();
    }

    private void AlignCheckoutToStayType()
    {
        if (TypePicker.SelectedItem is not OvernightStayType stayType)
        {
            return;
        }

        var isRange = stayType is OvernightStayType.Overnight or OvernightStayType.TwentyTwoHourExclusive;
        _aligning = true;
        try
        {
            if (!isRange)
            {
                if (CheckOutPicker.Date != CheckInPicker.Date)
                {
                    CheckOutPicker.Date = CheckInPicker.Date;
                }
            }
            else if (CheckOutPicker.Date <= CheckInPicker.Date)
            {
                CheckOutPicker.Date = CheckInPicker.Date.AddDays(1);
            }
        }
        finally
        {
            _aligning = false;
        }
    }

    private async Task ValidateDatesAsync()
    {
        var version = ++_conflictCheckVersion;
        if (TypePicker.SelectedItem is not OvernightStayType stayType)
        {
            return;
        }

        try
        {
            var conflicts = await _bookings.ValidateDatesAsync(
                CheckInPicker.Date, CheckOutPicker.Date, stayType, _editing?.Id);

            if (version != _conflictCheckVersion)
            {
                return;
            }

            ConflictLabel.IsVisible = conflicts.Count > 0;
            ConflictLabel.Text = conflicts.Count == 0
                ? string.Empty
                : "Date conflicts:\n" + string.Join("\n", conflicts);
        }
        catch
        {
            // Best-effort; save runs the authoritative check.
        }
    }

    // -- Pricing -------------------------------------------------------------

    private void OnStepperChanged(object? sender, ValueChangedEventArgs e) => Recalculate();

    private void OnFormFieldChanged(object? sender, TextChangedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        foreach (var row in _addonRows)
        {
            UpdateRowTotal(row);
        }

        var addonsTotal = _addonRows
            .Where(r => r.Check.IsChecked)
            .Sum(r => r.UnitPrice * (int)r.Qty.Value);

        var baseAmount = ParseDecimal(BaseAmountEntry.Text);
        var extraRate = ParseDecimal(ExtraRateEntry.Text);
        var additional = (int)AdditionalStepper.Value;

        var result = PricingCalculator.Calculate(
            baseAmount, additional, extraRate, addonsTotal, _downpaymentPercent);

        ExtraChargeCaption.Text = $"Extra guests ({additional} × ₱{extraRate:N2})";
        ExtraChargeLabel.Text = $"₱{result.ExtraGuestCharge:N2}";
        AddonsTotalLabel.Text = $"₱{addonsTotal:N2}";
        TotalLabel.Text = $"₱{result.TotalAmount:N2}";
        DepositLabel.Text = $"₱{result.DepositRequired:N2}";
        RemainingLabel.Text = $"₱{result.RemainingBalance:N2}";
    }

    private static decimal ParseDecimal(string? text)
        => decimal.TryParse(text, out var value) && value >= 0 ? value : 0m;

    // -- Save / cancel -------------------------------------------------------

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text))
        {
            await DisplayAlert("Missing guest", "Please enter the guest's full name.", "OK");
            return;
        }

        if (ConflictLabel.IsVisible)
        {
            await DisplayAlert("Date conflict",
                "The selected dates overlap an existing booking:\n\n" + ConflictLabel.Text, "OK");
            return;
        }

        if (TypePicker.SelectedItem is not OvernightStayType stayType)
        {
            await DisplayAlert("Missing type", "Please choose a stay type.", "OK");
            return;
        }

        var selectedAddons = _addonRows
            .Where(r => r.Check.IsChecked && r.Qty.Value > 0)
            .Select(r => new BookingAddonInput(r.AddonId, (int)r.Qty.Value, r.UnitPrice))
            .ToList();

        SaveButton.IsEnabled = false;
        try
        {
            var guest = _selectedGuest ?? new Guest();
            guest.FullName = NameEntry.Text.Trim();
            guest.Phone = PhoneEntry.Text?.Trim() ?? string.Empty;
            guest.Email = EmailEntry.Text?.Trim() ?? string.Empty;
            guest.IdType = IdTypeEntry.Text?.Trim() ?? string.Empty;
            guest.IdNumber = IdNumberEntry.Text?.Trim() ?? string.Empty;
            guest.Notes = string.IsNullOrWhiteSpace(GuestNotesEntry.Text) ? null : GuestNotesEntry.Text.Trim();

            var booking = _editing ?? new Booking();
            booking.CheckInDate = CheckInPicker.Date;
            booking.CheckOutDate = CheckOutPicker.Date;
            booking.OvernightStayType = stayType;
            booking.BaseGuests = (int)BaseGuestsStepper.Value;
            booking.AdditionalGuests = (int)AdditionalStepper.Value;
            booking.PricePerExtraGuest = ParseDecimal(ExtraRateEntry.Text);
            booking.BaseAmount = ParseDecimal(BaseAmountEntry.Text);

            var saved = await _bookings.SaveBookingAsync(guest, booking, selectedAddons);

            if (_editing is null)
            {
                await DisplayAlert("Booking saved",
                    $"Booking #{saved.Id} created for {saved.Guest?.FullName ?? guest.FullName}.\n\n" +
                    $"Total: ₱{saved.TotalAmount:N2}\n" +
                    $"Downpayment due ({_downpaymentPercent:P0}): ₱{saved.DepositRequired:N2}\n" +
                    $"Deadline: {saved.DepositDeadline:MMM dd, yyyy hh:mm tt} ({AppConstants.DepositDeadlineHours} hours)",
                    "OK");
                await Shell.Current.GoToAsync($"../booking/detail?id={saved.Id}");
            }
            else
            {
                await DisplayAlert("Booking updated",
                    $"Booking #{saved.Id} saved.\nTotal: ₱{saved.TotalAmount:N2}  •  " +
                    $"Balance: ₱{saved.RemainingBalance:N2}", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (BookingConflictException ex)
        {
            ConflictLabel.IsVisible = true;
            ConflictLabel.Text = string.Join("\n", ex.Conflicts);
            await DisplayAlert("Date conflict",
                "Another booking occupies these dates:\n\n" + ConflictLabel.Text, "OK");
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("Cannot save", ex.Message, "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    // -- Prefill / reset -----------------------------------------------------

    private void ResetForm()
    {
        _editing = null;
        _selectedGuest = null;
        GuestSearch.Text = string.Empty;
        NameEntry.Text = string.Empty;
        PhoneEntry.Text = string.Empty;
        EmailEntry.Text = string.Empty;
        IdTypeEntry.Text = string.Empty;
        IdNumberEntry.Text = string.Empty;
        GuestNotesEntry.Text = string.Empty;
        ReturningBadge.IsVisible = false;
        SuggestionsList.IsVisible = false;

        CheckInPicker.Date = DateTime.Today;
        CheckOutPicker.Date = DateTime.Today.AddDays(1);
        TypePicker.SelectedIndex = (int)OvernightStayType.Overnight;
        AdditionalStepper.Value = 0;
        BaseAmountEntry.Text = "0.00";
        ConflictLabel.IsVisible = false;
        ConflictLabel.Text = string.Empty;
    }

    private void Prefill(Booking booking)
    {
        _editing = booking;
        _selectedGuest = booking.Guest;

        GuestSearch.Text = booking.Guest?.FullName ?? string.Empty;
        NameEntry.Text = booking.Guest?.FullName ?? string.Empty;
        PhoneEntry.Text = booking.Guest?.Phone ?? string.Empty;
        EmailEntry.Text = booking.Guest?.Email ?? string.Empty;
        IdTypeEntry.Text = booking.Guest?.IdType ?? string.Empty;
        IdNumberEntry.Text = booking.Guest?.IdNumber ?? string.Empty;
        GuestNotesEntry.Text = booking.Guest?.Notes ?? string.Empty;
        ReturningBadge.Text = $"Editing guest: {booking.Guest?.FullName}";
        ReturningBadge.IsVisible = true;

        CheckInPicker.Date = booking.CheckInDate;
        CheckOutPicker.Date = booking.CheckOutDate;
        TypePicker.SelectedIndex = (int)booking.OvernightStayType;
        BaseGuestsStepper.Value = Math.Clamp(booking.BaseGuests, 1, 20);
        AdditionalStepper.Value = Math.Clamp(booking.AdditionalGuests, 0, 50);
        ExtraRateEntry.Text = booking.PricePerExtraGuest.ToString("0.00");
        BaseAmountEntry.Text = booking.BaseAmount.ToString("0.00");
    }
}
