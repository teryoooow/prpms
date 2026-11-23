using ResortSync.Data;
using Microsoft.EntityFrameworkCore;
using ResortSync.Models;

namespace ResortSync.Services;

/// <summary>Filters supported by the payments ledger view.</summary>
public sealed record PaymentFilter(
    string? Search = null,
    PaymentMethod? Method = null,
    PaymentType? Type = null,
    DateTime? From = null,
    DateTime? To = null);

public sealed record PaymentRow(
    int PaymentId,
    int BookingId,
    string GuestName,
    DateTime PaymentDate,
    decimal AmountPaid,
    PaymentType PaymentType,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes);

/// <summary>Records partial payments and keeps booking balances/statuses in sync.</summary>
public interface IPaymentService
{
    /// <summary>Booking payments ordered newest-first (includes guest + addon lines).</summary>
    Task<Booking?> GetBookingWithPaymentsAsync(int bookingId);

    /// <summary>
    /// Appends a payment and recomputes the booking balance/status:
    /// downpayment → Confirmed, balance zero → FullyPaid.
    /// Security deposits are held separately and never reduce the total.
    /// </summary>
    Task<Booking> RecordPaymentAsync(int bookingId, PaymentTransaction payment);

    /// <summary>Reverse a mistaken payment (records an offsetting audit-safe deletion).</summary>
    Task VoidPaymentAsync(int paymentId);

    Task<List<PaymentRow>> QueryAsync(PaymentFilter filter, int maxResults = 500);
}

public sealed class PaymentService : IPaymentService
{
    private readonly IDbContextFactory<ResortDbContext> _factory;

    public PaymentService(IDbContextFactory<ResortDbContext> factory) => _factory = factory;

    public async Task<Booking?> GetBookingWithPaymentsAsync(int bookingId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Bookings
            .Include(b => b.Guest)
            .Include(b => b.Payments)
            .Include(b => b.BookingAddons).ThenInclude(x => x.Addon)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    public async Task<Booking> RecordPaymentAsync(int bookingId, PaymentTransaction payment)
    {
        if (payment.AmountPaid <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        await using var db = await _factory.CreateDbContextAsync();

        var booking = await db.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new InvalidOperationException($"Booking #{bookingId} was not found.");

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Expired or BookingStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Cannot record a payment against a {booking.Status} booking.");
        }

        payment.BookingId = booking.Id;
        payment.PaymentDate = DateTime.Now;
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync();

        // ── Recompute balance (security deposits & refunds excluded) ──────────
        var towardTotal = booking.Payments
            .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
            .Sum(p => p.AmountPaid);
        var refunded = booking.Payments
            .Where(p => p.PaymentType == PaymentType.Refund)
            .Sum(p => p.AmountPaid);

        booking.RemainingBalance = Math.Max(0m, booking.TotalAmount - towardTotal + refunded);

        // ── Status transitions ────────────────────────────────────────────────
        var madeProgress = payment.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance;
        if (madeProgress)
        {
            booking.Status = booking.RemainingBalance <= 0m
                ? BookingStatus.FullyPaid
                : BookingStatus.Confirmed; // deposit window satisfied
        }

        await db.SaveChangesAsync();
        return booking;
    }

    public async Task VoidPaymentAsync(int paymentId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var payment = await db.PaymentTransactions
            .Include(p => p.Booking).ThenInclude(b => b.Payments)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new InvalidOperationException("Payment not found.");

        if (payment.PaymentType == PaymentType.Downpayment
            && payment.Booking.Status is not (BookingStatus.PendingDeposit or BookingStatus.Confirmed or BookingStatus.FullyPaid))
        {
            throw new InvalidOperationException(
                "Only payments on active bookings can be voided. Cancel/complete handling requires supervisor review.");
        }

        db.PaymentTransactions.Remove(payment);
        await db.SaveChangesAsync();

        // Recompute after void.
        var booking = payment.Booking;
        var towardTotal = booking.Payments
            .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
            .Sum(p => p.AmountPaid);
        var refunded = booking.Payments
            .Where(p => p.PaymentType == PaymentType.Refund)
            .Sum(p => p.AmountPaid);

        booking.RemainingBalance = Math.Max(0m, booking.TotalAmount - towardTotal + refunded);

        if (booking.Status == BookingStatus.FullyPaid && booking.RemainingBalance > 0m)
        {
            booking.Status = BookingStatus.Confirmed;
        }
        else if (booking.Status == BookingStatus.Confirmed && towardTotal == 0m)
        {
            booking.Status = BookingStatus.PendingDeposit;
            booking.DepositDeadline = DateTime.Now.AddHours(Constants.AppConstants.DepositDeadlineHours);
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<PaymentRow>> QueryAsync(PaymentFilter filter, int maxResults = 500)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.PaymentTransactions
            .Include(p => p.Booking).ThenInclude(b => b.Guest)
            .AsNoTracking()
            .AsQueryable();

        if (filter.Method is PaymentMethod method)
        {
            query = query.Where(p => p.PaymentMethod == method);
        }

        if (filter.Type is PaymentType type)
        {
            query = query.Where(p => p.PaymentType == type);
        }

        if (filter.From is DateTime from)
        {
            query = query.Where(p => p.PaymentDate >= from);
        }

        if (filter.To is DateTime to)
        {
            query = query.Where(p => p.PaymentDate < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Booking.Guest.FullName.ToLower().Contains(s)
                || (p.ReferenceNumber != null && p.ReferenceNumber.ToLower().Contains(s))
                || p.BookingId.ToString().Contains(s));
        }

        var rows = await query
            .OrderByDescending(p => p.PaymentDate)
            .Take(maxResults)
            .Select(p => new PaymentRow(
                p.Id,
                p.BookingId,
                p.Booking.Guest.FullName,
                p.PaymentDate,
                p.AmountPaid,
                p.PaymentType,
                p.PaymentMethod,
                p.ReferenceNumber,
                p.Notes))
            .ToListAsync();

        return rows;
    }
}
