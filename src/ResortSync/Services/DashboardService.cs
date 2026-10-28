using ResortSync.Data;
using Microsoft.EntityFrameworkCore;
using ResortSync.Models;

namespace ResortSync.Services;

/// <summary>Aggregated figures shown on the dashboard stat cards.</summary>
public sealed record DashboardStats(
    decimal MonthlyRevenue,
    decimal PendingBalanceTotal,
    int TodayCheckIns,
    int OverdueDownpayments,
    int ActiveBookings);

/// <summary>Read-side queries for the dashboard.</summary>
public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync();

    /// <summary>
    /// PendingDeposit bookings whose deadline expired or is within the alert window,
    /// sorted by urgency (most overdue first).
    /// </summary>
    Task<List<Booking>> GetDepositAlertsAsync();
}

public sealed class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<ResortDbContext> _factory;

    public DashboardService(IDbContextFactory<ResortDbContext> factory) => _factory = factory;

    public async Task<DashboardStats> GetStatsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        // SQLite stores decimal as text — materialize payments before summing.
        var monthPayments = await db.PaymentTransactions.AsNoTracking()
            .Where(p => p.PaymentDate >= monthStart)
            .Select(p => new { p.AmountPaid, p.PaymentType })
            .ToListAsync();

        // Revenue = settled room money this month (refunds deducted, security
        // deposits excluded because they are refundable holds).
        var revenue = monthPayments
            .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
            .Sum(p => p.AmountPaid)
            - monthPayments.Where(p => p.PaymentType == PaymentType.Refund).Sum(p => p.AmountPaid);

        // Outstanding balances across bookings that can still be collected.
        var pendingBookings = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.PendingDeposit
                        || b.Status == BookingStatus.Confirmed
                        || b.Status == BookingStatus.FullyPaid)
            .Select(b => b.RemainingBalance)
            .ToListAsync();
        var pendingBalance = pendingBookings.Sum();

        var today = DateTime.Today;
        var todayCheckIns = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.CheckInDate >= today
                             && b.CheckInDate < today.AddDays(1)
                             && b.Status != BookingStatus.Cancelled
                             && b.Status != BookingStatus.Expired);

        var overdue = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.Status == BookingStatus.PendingDeposit
                             && b.DepositDeadline < DateTime.Now);

        var active = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.Status == BookingStatus.PendingDeposit
                             || b.Status == BookingStatus.Confirmed
                             || b.Status == BookingStatus.FullyPaid);

        return new DashboardStats(revenue, pendingBalance, todayCheckIns, overdue, active);
    }

    public async Task<List<Booking>> GetDepositAlertsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var soon = DateTime.Now.AddHours(Constants.AppConstants.ExpiringSoonAlertHours);

        return await db.Bookings
            .Include(b => b.Guest)
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.PendingDeposit && b.DepositDeadline <= soon)
            .OrderBy(b => b.DepositDeadline)
            .ToListAsync();
    }
}
