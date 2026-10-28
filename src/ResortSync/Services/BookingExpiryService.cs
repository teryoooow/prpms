using ResortSync.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResortSync.Constants;
using ResortSync.Models;

namespace ResortSync.Services;

/// <summary>
/// Background worker hosted by the Generic Host. Every <see cref="AppConstants.ExpiryCheckInterval"/>
/// it flips any <see cref="BookingStatus.PendingDeposit"/> booking whose 24-hour deposit
/// deadline has passed to <see cref="BookingStatus.Expired"/>.
///
/// Expired bookings release their dates: the conflict check in <see cref="IBookingService"/>
/// only treats PendingDeposit/Confirmed/FullyPaid/Completed as occupying inventory.
/// </summary>
public sealed class BookingExpiryService(
    IDbContextFactory<ResortDbContext> factory,
    ILogger<BookingExpiryService> logger,
    IServiceProvider services) : BackgroundService
{
    /// <remarks>
    /// The loop is deliberately started with <see cref="Task.Run"/>: the host is started
    /// on the WPF dispatcher thread, and a plain await inside would post continuations
    /// back to that thread — which would deadlock when <c>StopAsync</c> blocks the UI
    /// thread during shutdown. On the thread pool there is no SynchronizationContext.
    /// </remarks>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => RunLoopAsync(stoppingToken), CancellationToken.None);

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Booking expiry worker started (checks every {Interval}).",
            AppConstants.ExpiryCheckInterval);

        using var timer = new PeriodicTimer(AppConstants.ExpiryCheckInterval);

        // Run once immediately so stale rows from a previous session are handled at launch.
        await ExpireOverdueBookingsAsync(stoppingToken);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExpireOverdueBookingsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Booking expiry worker stopped.");
    }

    private async Task ExpireOverdueBookingsAsync(CancellationToken ct)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);

            var now = DateTime.Now;
            var overdue = await db.Bookings
                .Include(b => b.Guest)
                .Where(b => b.Status == BookingStatus.PendingDeposit
                            && b.DepositDeadline < now)
                .ToListAsync(ct);

            if (overdue.Count == 0)
            {
                return;
            }

            foreach (var booking in overdue)
            {
                booking.Status = BookingStatus.Expired;
            }

            await db.SaveChangesAsync(ct);

            logger.LogWarning(
                "Auto-expired {Count} booking(s): {Ids}",
                overdue.Count,
                string.Join(", ", overdue.Select(b => $"#{b.Id} ({b.Guest?.FullName})")));

            // Refresh the dashboard so the operator sees the change without switching tabs.
            if (services.GetService<IDashboardInvalidator>() is { } invalidator)
            {
                invalidator.RequestRefresh();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let a transient DB hiccup kill the worker.
            logger.LogError(ex, "Expiry check failed.");
        }
    }
}

/// <summary>
/// Minimal pub/sub so background code can poke the UI without a dependency on ViewModels.
/// <see cref="ViewModels.MainViewModel"/> subscribes at startup.
/// </summary>
public interface IDashboardInvalidator
{
    event EventHandler? RefreshRequested;
    void RequestRefresh();
}

public sealed class DashboardInvalidator : IDashboardInvalidator
{
    public event EventHandler? RefreshRequested;

    public void RequestRefresh() => RefreshRequested?.Invoke(this, EventArgs.Empty);
}
