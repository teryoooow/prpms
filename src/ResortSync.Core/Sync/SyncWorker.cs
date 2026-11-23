using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResortSync.Constants;
using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>
/// Background sync loop hosted by the Generic Host. Every
/// <see cref="AppConstants.SyncInterval"/> it runs one full sync cycle when
/// cloud sync is configured, and pokes <see cref="IDashboardInvalidator"/>
/// when the cycle actually moved data (the coordinator does the poking —
/// this class only owns the timer).
/// </summary>
public sealed class SyncWorker(
    SyncCoordinator coordinator,
    ILogger<SyncWorker> logger) : BackgroundService
{
    /// <remarks>
    /// Started with <see cref="Task.Run"/> for the same reason as the expiry
    /// worker: the host starts on the WPF dispatcher thread, and awaiting
    /// there would deadlock when StopAsync blocks the UI during shutdown.
    /// </remarks>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => RunLoopAsync(stoppingToken), CancellationToken.None);

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Cloud sync worker started (interval {Interval}).", AppConstants.SyncInterval);

        // Run once immediately so a session opened after downtime catches up.
        await TickAsync(stoppingToken);

        using var timer = new PeriodicTimer(AppConstants.SyncInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Cloud sync worker stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            if (!await coordinator.IsActiveAsync())
            {
                return; // switched off or not configured — stay quiet
            }

            var result = await coordinator.SyncNowAsync(ct);
            if (result.State == SyncStatus.Error)
            {
                logger.LogWarning("Sync cycle failed: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let a transient network/DB hiccup kill the worker.
            logger.LogError(ex, "Sync tick failed.");
        }
    }
}
