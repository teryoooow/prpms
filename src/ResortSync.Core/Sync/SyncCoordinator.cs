using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>
/// Thin single-flight wrapper around <see cref="SyncEngine"/>: serializes
/// cycles, exposes last-known status for Settings UIs, refreshes the UI when
/// a cycle moved data, and offers a fire-and-forget <see cref="RequestSync"/>
/// hook for "save then sync" flows.
/// </summary>
public sealed class SyncCoordinator
{
    private readonly SyncEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IDashboardInvalidator _invalidator;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SyncCoordinator(
        SyncEngine engine,
        ISettingsService settings,
        IDashboardInvalidator invalidator)
    {
        _engine = engine;
        _settings = settings;
        _invalidator = invalidator;
    }

    /// <summary>Result of the most recent cycle (null before the first).</summary>
    public SyncResult? LastResult { get; private set; }

    public DateTime? LastSyncAt { get; private set; }

    /// <summary>Raised after every attempted cycle (from a thread-pool thread).</summary>
    public event EventHandler? SyncCompleted;

    /// <summary>True when sync is enabled and credentials are present.</summary>
    public async Task<bool> IsActiveAsync()
    {
        var s = await _settings.LoadAsync();
        return s.SyncEnabled && s.ToSyncConfig().IsConfigured;
    }

    /// <summary>
    /// Runs one cycle. Concurrent callers coalesce: if a cycle is already
    /// running they await its completion instead of starting another.
    /// </summary>
    public async Task<SyncResult> SyncNowAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var result = await _engine.SyncAsync(ct);
            LastResult = result;

            if (result.State == SyncStatus.Idle)
            {
                LastSyncAt = DateTime.UtcNow;

                if (result.Pushed + result.Pulled + result.Deleted > 0)
                {
                    // Data moved — nudge whatever page is open (marshaled by
                    // MainViewModel; no-op when Settings/booking form is open).
                    _invalidator.RequestRefresh();
                }
            }

            return result;
        }
        finally
        {
            _gate.Release();
            SyncCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Queues a cycle without blocking the caller (never throws).</summary>
    public void RequestSync()
        => _ = Task.Run(async () =>
        {
            try
            {
                await SyncNowAsync();
            }
            catch
            {
                // Status is surfaced through LastResult; background trigger stays silent.
            }
        });
}
