using ResortSync.Data;
using Microsoft.EntityFrameworkCore;
using ResortSync.Constants;
using ResortSync.Models;

namespace ResortSync.Services;

/// <summary>Result of pricing a booking: line breakdown plus deposit/balance figures.</summary>
public sealed record PricingResult(
    decimal ExtraGuestCharge,
    decimal Subtotal,
    decimal TotalAmount,
    decimal DepositRequired,
    decimal RemainingBalance);

/// <summary>
/// Pure, side-effect-free pricing math shared by the live form calculator and
/// the authoritative save path.
/// </summary>
public static class PricingCalculator
{
    /// <summary>
    /// (Base Amount) + (Additional Guests × Rate) + (Addons) = Total Amount,
    /// then Downpayment = Total × percent, Remaining = Total − Downpayment.
    /// </summary>
    public static PricingResult Calculate(
        decimal baseAmount,
        int additionalGuests,
        decimal pricePerExtraGuest,
        decimal addonsAmount,
        decimal downpaymentPercent)
    {
        // Guard against negative inputs slipping in from invalid UI states.
        baseAmount = Math.Max(0m, baseAmount);
        additionalGuests = Math.Max(0, additionalGuests);
        pricePerExtraGuest = Math.Max(0m, pricePerExtraGuest);
        addonsAmount = Math.Max(0m, addonsAmount);
        downpaymentPercent = Math.Clamp(downpaymentPercent, 0m, 1m);

        var extraGuestCharge = additionalGuests * pricePerExtraGuest;
        var total = baseAmount + extraGuestCharge + addonsAmount;
        var deposit = Math.Round(total * downpaymentPercent, 2, MidpointRounding.AwayFromZero);

        return new PricingResult(
            ExtraGuestCharge: extraGuestCharge,
            Subtotal: total,
            TotalAmount: total,
            DepositRequired: deposit,
            RemainingBalance: total - deposit);
    }
}

/// <summary>Thrown when a booking would double-book an occupied date range.</summary>
public sealed class BookingConflictException(IReadOnlyList<string> conflicts)
    : InvalidOperationException("The selected dates conflict with existing bookings.")
{
    public IReadOnlyList<string> Conflicts { get; } = conflicts;
}

/// <summary>Addon line captured from the booking form (catalog price snapshotted).</summary>
public sealed record BookingAddonInput(int AddonId, int Quantity, decimal UnitPrice);

/// <summary>Authoritative booking persistence rules (guest reuse, pricing, conflicts).</summary>
public interface IBookingService
{
    /// <summary>Type-ahead guest search over name/phone/email/ID number.</summary>
    Task<List<Guest>> SearchGuestsAsync(string query, int maxResults = 15);

    Task<List<Addon>> GetActiveAddonsAsync();

    /// <summary>Loads a booking with guest, payments, and addon lines (null when missing).</summary>
    Task<Booking?> GetAsync(int bookingId);

    /// <summary>Filtered booking list for the All Bookings grid (guest + payments included).</summary>
    Task<List<Booking>> ListAsync(string? search = null, BookingStatus? status = null, int maxResults = 500);

    /// <summary>
    /// Lifecycle transition (Confirm / Complete / Cancel / Reopen).
    /// Enforces legal state changes; returns the updated booking.
    /// </summary>
    Task<Booking> UpdateStatusAsync(int bookingId, BookingStatus status);

    /// <summary>
    /// Returns human-readable conflict descriptions when the given stay window
    /// overlaps an occupied booking; empty list means the window is free.
    /// </summary>
    Task<List<string>> ValidateDatesAsync(
        DateTime checkIn, DateTime checkOut, OvernightStayType stayType, int? excludeBookingId = null);

    /// <summary>
    /// Creates or updates the booking (and guest), recomputes all money fields
    /// server-side, applies the 24-hour deposit deadline, and saves atomically.
    /// Throws <see cref="BookingConflictException"/> on double-booking.
    /// </summary>
    Task<Booking> SaveBookingAsync(
        Guest guest,
        Booking booking,
        IReadOnlyList<BookingAddonInput> addons);
}

public sealed class BookingService : IBookingService
{
    private readonly IDbContextFactory<ResortDbContext> _factory;
    private readonly ISettingsService _settings;

    public BookingService(IDbContextFactory<ResortDbContext> factory, ISettingsService settings)
    {
        _factory = factory;
        _settings = settings;
    }

    public async Task<List<Guest>> SearchGuestsAsync(string query, int maxResults = 15)
    {
        await using var db = await _factory.CreateDbContextAsync();

        query = query?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return new List<Guest>();
        }

        var lowered = query.ToLower();

        return await db.Guests
            .Where(g => g.FullName.ToLower().Contains(lowered)
                        || g.Phone.Contains(query)
                        || g.Email.ToLower().Contains(lowered)
                        || g.IdNumber.ToLower().Contains(lowered))
            .OrderBy(g => g.FullName)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<List<Addon>> GetActiveAddonsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Addons.Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
    }

    public async Task<Booking?> GetAsync(int bookingId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Bookings
            .Include(b => b.Guest)
            .Include(b => b.Payments)
            .Include(b => b.BookingAddons).ThenInclude(x => x.Addon)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    public async Task<List<Booking>> ListAsync(
        string? search = null, BookingStatus? status = null, int maxResults = 500)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Bookings
            .Include(b => b.Guest)
            .Include(b => b.Payments)
            .AsNoTracking()
            .AsQueryable();

        if (status is BookingStatus filterStatus)
        {
            query = query.Where(b => b.Status == filterStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b =>
                b.Guest.FullName.ToLower().Contains(s)
                || b.Guest.Phone.Contains(s)
                || b.Id.ToString().Contains(s));
        }

        return await query
            .OrderByDescending(b => b.CheckInDate)
            .ThenByDescending(b => b.Id)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<Booking> UpdateStatusAsync(int bookingId, BookingStatus status)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var booking = await db.Bookings
            .Include(b => b.Guest)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new InvalidOperationException($"Booking #{bookingId} was not found.");

        var allowed = booking.Status switch
        {
            // Cancelled and Expired release inventory — reopen before any other change.
            BookingStatus.Cancelled or BookingStatus.Expired =>
                status is BookingStatus.PendingDeposit,

            BookingStatus.PendingDeposit =>
                status is BookingStatus.Confirmed or BookingStatus.Cancelled
                    or BookingStatus.Expired or BookingStatus.FullyPaid,

            BookingStatus.Confirmed =>
                status is BookingStatus.FullyPaid or BookingStatus.Completed
                    or BookingStatus.Cancelled,

            BookingStatus.FullyPaid =>
                status is BookingStatus.Completed or BookingStatus.Cancelled,

            BookingStatus.Completed =>
                status is BookingStatus.Confirmed or BookingStatus.FullyPaid, // reopen

            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Transition {booking.Status} → {status} is not allowed.");
        }

        booking.Status = status;
        if (status == BookingStatus.Completed)
        {
            booking.CompletedAt = DateTime.Now;
        }

        if (status == BookingStatus.PendingDeposit)
        {
            // Reopening an expired/cancelled booking restarts the deposit clock.
            booking.CreatedAt = DateTime.Now;
            booking.DepositDeadline = booking.CreatedAt.AddHours(AppConstants.DepositDeadlineHours);
            booking.CompletedAt = null;
        }

        await db.SaveChangesAsync();
        return booking;
    }

    public async Task<List<string>> ValidateDatesAsync(
        DateTime checkIn, DateTime checkOut, OvernightStayType stayType, int? excludeBookingId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var candidates = await LoadOccupiedBookingsAsync(db);
        return FindConflicts(candidates, checkIn, checkOut, stayType, excludeBookingId);
    }

    public async Task<Booking> SaveBookingAsync(
        Guest guest, Booking booking, IReadOnlyList<BookingAddonInput> addons)
    {
        if (string.IsNullOrWhiteSpace(guest.FullName))
        {
            throw new InvalidOperationException("Guest name is required.");
        }

        if (IsRangeStay(booking.OvernightStayType) && booking.CheckOutDate <= booking.CheckInDate)
        {
            throw new InvalidOperationException("Check-out must be after check-in.");
        }

        await using var db = await _factory.CreateDbContextAsync();

        // ── 1. Double-booking guard (authoritative DB check) ──────────────────
        var conflicts = FindConflicts(
            await LoadOccupiedBookingsAsync(db),
            booking.CheckInDate, booking.CheckOutDate,
            booking.OvernightStayType, booking.Id);

        if (conflicts.Count > 0)
        {
            throw new BookingConflictException(conflicts);
        }

        // ── 2. Upsert the guest ───────────────────────────────────────────────
        if (guest.Id == 0)
        {
            db.Guests.Add(guest);
        }
        else
        {
            db.Guests.Attach(guest);
            db.Entry(guest).State = EntityState.Modified;
        }

        // ── 3. Recompute money from source inputs (never trust the client) ────
        var addonsAmount = addons.Where(a => a.Quantity > 0).Sum(a => a.Quantity * a.UnitPrice);
        var settings = await _settings.LoadAsync();
        var pricing = PricingCalculator.Calculate(
            booking.BaseAmount, booking.AdditionalGuests, booking.PricePerExtraGuest,
            addonsAmount, settings.DownpaymentPercent);

        booking.Guest = guest;
        booking.AddonsAmount = addonsAmount;
        booking.TotalAmount = pricing.TotalAmount;
        booking.DepositRequired = pricing.DepositRequired;

        var isEdit = booking.Id != 0;

        if (!isEdit)
        {
            // New booking: reserve the 24-hour downpayment window.
            booking.CreatedAt = DateTime.Now;
            booking.DepositDeadline = booking.CreatedAt.AddHours(AppConstants.DepositDeadlineHours);
            booking.Status = BookingStatus.PendingDeposit;
            booking.RemainingBalance = pricing.TotalAmount; // no payments yet

            foreach (var input in addons.Where(a => a.Quantity > 0))
            {
                booking.BookingAddons.Add(new BookingAddon
                {
                    AddonId = input.AddonId,
                    Quantity = input.Quantity,
                    UnitPrice = input.UnitPrice
                });
            }

            db.Bookings.Add(booking);
        }
        else
        {
            // Edit: never Update() a disconnected graph — its navigation entities
            // (guest, addon lines) collide with instances the fresh context already
            // tracks ("same key already being tracked"). Copy scalars onto the
            // tracked instance and rebuild addon lines with shared instances.
            var tracked = await db.Bookings
                .FirstOrDefaultAsync(b => b.Id == booking.Id)
                ?? throw new InvalidOperationException($"Booking #{booking.Id} was not found.");

            db.Entry(tracked).CurrentValues.SetValues(booking);

            // Re-point the relationship: supports "pick a different guest while editing"
            // (guest.Id == 0 means a brand-new guest record was created).
            tracked.Guest = guest;

            // Keep the balance coherent when totals changed after money already moved.
            var paid = (await db.PaymentTransactions
                    .Where(p => p.BookingId == tracked.Id
                                && (p.PaymentType == PaymentType.Downpayment
                                    || p.PaymentType == PaymentType.FinalBalance))
                    .ToListAsync())
                .Sum(p => p.AmountPaid);
            tracked.RemainingBalance = Math.Max(0m, tracked.TotalAmount - paid);
            booking.RemainingBalance = tracked.RemainingBalance;

            // Rebuild addon lines (the form owns its full selection).
            var oldLines = await db.BookingAddons
                .Where(x => x.BookingId == tracked.Id)
                .ToListAsync();
            db.BookingAddons.RemoveRange(oldLines);

            foreach (var input in addons.Where(a => a.Quantity > 0))
            {
                db.BookingAddons.Add(new BookingAddon
                {
                    BookingId = tracked.Id,
                    AddonId = input.AddonId,
                    Quantity = input.Quantity,
                    UnitPrice = input.UnitPrice
                });
            }
        }

        await db.SaveChangesAsync();
        return booking;
    }

    /// <summary>Only range-style stays produce overlap conflicts; tours conflict on the same date.</summary>
    private static bool IsRangeStay(OvernightStayType type)
        => type is OvernightStayType.Overnight or OvernightStayType.TwentyTwoHourExclusive;

    /// <summary>Bookings that still hold inventory (expired/cancelled release their dates).</summary>
    /// <remarks>
    /// AsNoTracking is load-bearing: SaveBookingAsync runs this first, then tracks
    /// the guest it was handed. If this query tracked the same Guest instance
    /// (e.g. the booking being edited holds it), the Attach in step 2 would collide
    /// with it — "another instance with the same key value ... is already tracked".
    /// The result only feeds conflict detection, so nothing needs to be tracked.
    /// </remarks>
    private static async Task<List<Booking>> LoadOccupiedBookingsAsync(ResortDbContext db)
        => await db.Bookings
            .AsNoTracking()
            .Include(b => b.Guest)
            .Where(b => b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Expired)
            .ToListAsync();

    /// <summary>Shared overlap logic used by both validation and save.</summary>
    private static List<string> FindConflicts(
        List<Booking> candidates, DateTime checkIn, DateTime checkOut,
        OvernightStayType stayType, int? excludeBookingId)
    {
        var results = new List<string>();

        foreach (var other in candidates)
        {
            if (excludeBookingId is int id && other.Id == id)
            {
                continue;
            }

            var overlaps = IsRangeStay(stayType)
                // Range overlap: [in, out) intersects [otherIn, otherOut)
                ? checkIn < other.CheckOutDate && checkOut > other.CheckInDate
                // Tours: same calendar day is a conflict.
                : checkIn.Date == other.CheckInDate.Date;

            if (overlaps)
            {
                results.Add(
                    $"{other.Guest?.FullName ?? "Unknown"} — " +
                    $"{other.CheckInDate:MMM dd, yyyy hh:mm tt} → {other.CheckOutDate:MMM dd, yyyy hh:mm tt} " +
                    $"({other.OvernightStayType}, {other.Status})");
            }
        }

        return results;
    }
}
