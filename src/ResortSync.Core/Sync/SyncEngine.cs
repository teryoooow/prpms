using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResortSync.Data;
using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>
/// One full sync cycle: authenticate → PULL (remote wins when newer) → PUSH
/// (local rows newer than the watermark + pending tombstones) → settings doc.
///
/// Consistency model:
/// <list type="bullet">
/// <item>Last-write-wins on <c>updated_at</c> (UTC); equal stamps never
/// re-apply, so our own pushed rows echoed back are no-ops.</item>
/// <item>Pull runs before push: a pull can obsolete a pending tombstone
/// (remote row is newer than our delete → row is recreated and the tombstone
/// dropped), and push after pull shrinks the concurrent-edit race to seconds —
/// any residual overwrite self-heals on the next cycle because the losing
/// device keeps its newer local copy.</item>
/// <item>All apply writes run under <see cref="ResortDbContext.SyncApplyMode"/>
/// so remote timestamps survive verbatim and remote deletes do not spawn
/// fresh local tombstones.</item>
/// </list>
/// </summary>
public sealed class SyncEngine
{
    private readonly IDbContextFactory<ResortDbContext> _factory;
    private readonly IFirestoreTransport _transport;
    private readonly IFirebaseAuth _auth;
    private readonly ISettingsService _settings;
    private readonly ILogger<SyncEngine> _logger;

    public SyncEngine(
        IDbContextFactory<ResortDbContext> factory,
        IFirestoreTransport transport,
        IFirebaseAuth auth,
        ISettingsService settings,
        ILogger<SyncEngine>? logger = null)
    {
        _factory = factory;
        _transport = transport;
        _auth = auth;
        _settings = settings;
        _logger = logger ?? NullLogger<SyncEngine>.Instance;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken ct = default)
    {
        var settings = await _settings.LoadAsync();
        if (!settings.SyncEnabled)
        {
            return SyncResult.Disabled;
        }

        var config = settings.ToSyncConfig();
        if (!config.IsConfigured)
        {
            return SyncResult.NotConfigured;
        }

        try
        {
            await _auth.GetIdTokenAsync(ct); // sign-in / refresh up front

            await using var db = await _factory.CreateDbContextAsync(ct);

            var (pulled, deleted) = await PullAsync(db, ct);
            var (pushed, tombstoned) = await PushAsync(db, ct);

            if (pulled + deleted + pushed + tombstoned > 0)
            {
                _logger.LogInformation(
                    "Sync cycle: {Pulled} pulled, {Pushed} pushed, {Deleted} deleted locally, {Tombstones} tombstones sent.",
                    pulled, pushed, deleted, tombstoned);
            }

            return new SyncResult(SyncStatus.Idle,
                Pushed: pushed + tombstoned, Pulled: pulled, Deleted: deleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync cycle failed.");
            return new SyncResult(SyncStatus.Error, Error: ex.Message);
        }
    }

    // ── Pull ─────────────────────────────────────────────────────────────────

    private async Task<(int Applied, int Deleted)> PullAsync(ResortDbContext db, CancellationToken ct)
    {
        var applied = 0;
        var deleted = 0;

        // Remote rows must keep their stamps verbatim through SaveChanges —
        // stamping is suppressed for the WHOLE pull, not just the mapping step.
        db.SyncApplyMode = true;
        try
        {
            foreach (var collection in SyncCollections.PullOrder)
            {
                var state = await StateAsync(db, collection, ct);

                // Full collection pull every cycle — never filter by Cursor.
                // An incremental `updated_at >= cursor` watermark skips remote
                // rows older than our local max even when we have never seen
                // them (classic first-sync skew: desktop rows stamped hours
                // before this device's own writes). Resort-scale collections
                // are tiny; LWW makes re-applying already-known docs a no-op.
                var docs = await _transport.QueryAsync(collection, minUpdatedAt: null, ct);
                var (a, d) = await ApplyCollectionAsync(db, collection, docs, ct);
                applied += a;
                deleted += d;

                if (docs.Count > 0)
                {
                    state.Cursor = FormatStamp(docs.Max(d => d.UpdatedAt));
                    state.LastSyncAt = DateTime.UtcNow;
                }

                // Persist per collection: bookings/payments resolve their FKs
                // against rows applied earlier in THIS cycle, and AsNoTracking
                // lookups only see committed data.
                await db.SaveChangesAsync(ct);
            }

            // ── Resort settings doc (single "app" document) ──────────────────
            applied += await PullSettingsAsync(db, ct);

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.SyncApplyMode = false;
        }

        return (applied, deleted);
    }

    private async Task<int> PullSettingsAsync(ResortDbContext db, CancellationToken ct)
    {
        var state = await StateAsync(db, SyncCollections.Settings, ct);
        // Always fetch the full settings collection (single "app" doc) so a
        // remote stamp older than our Cursor can never be skipped.
        var docs = await _transport.QueryAsync(
            SyncCollections.Settings, minUpdatedAt: null, ct);

        if (docs.Count == 0)
        {
            var local = await _settings.LoadAsync();
            if (local.SettingsUpdatedAt == default)
            {
            // Bootstrap: first device to ever sync seeds the cloud with its
            // current business settings so a fresh device inherits them.
            // (ms-truncated — see ResortDbContext.StampSyncFields.)
            local.SettingsUpdatedAt = FirestoreValues.TruncateMs(DateTime.UtcNow);
                await _settings.SaveAsync(local);
            }

            return 0;
        }

        var doc = docs.Single(d => d.Id == "app");
        var localSettings = await _settings.LoadAsync();
        var applied = false;

        if (doc.UpdatedAt > localSettings.SettingsUpdatedAt)
        {
            applied = true;
            localSettings.ResortName = Str(doc, "resort_name", localSettings.ResortName);
            localSettings.DownpaymentPercent = Dec(doc, "downpayment_percent", localSettings.DownpaymentPercent);
            localSettings.DefaultExtraGuestRate = Dec(doc, "default_extra_guest_rate", localSettings.DefaultExtraGuestRate);
            localSettings.DefaultBaseGuests = (int)Dec(doc, "default_base_guests", localSettings.DefaultBaseGuests);
            localSettings.SettingsUpdatedAt = doc.UpdatedAt;
            await _settings.SaveAsync(localSettings);
        }

        state.Cursor = FormatStamp(docs.Max(d => d.UpdatedAt));
        state.LastSyncAt = DateTime.UtcNow;
        return applied ? 1 : 0;
    }

    private Task<(int Applied, int Deleted)> ApplyCollectionAsync(
        ResortDbContext db, string collection, IReadOnlyList<RemoteDoc> docs, CancellationToken ct)
    {
        if (docs.Count == 0)
        {
            return Task.FromResult<(int, int)>((0, 0));
        }

        var result = collection switch
        {
            SyncCollections.Guests => ApplySimple<Guest>(db, docs,
                DocMapper.ToGuest,
                (local, remote) =>
                {
                    local.FullName = remote.FullName;
                    local.Phone = remote.Phone;
                    local.Email = remote.Email;
                    local.IdType = remote.IdType;
                    local.IdNumber = remote.IdNumber;
                    local.Notes = remote.Notes;
                },
                db.Guests, g => g.CloudId!, g => g.UpdatedAt),

            SyncCollections.Addons => ApplySimple<Addon>(db, docs,
                DocMapper.ToAddon,
                (local, remote) =>
                {
                    local.Name = remote.Name;
                    local.UnitPrice = remote.UnitPrice;
                    local.IsActive = remote.IsActive;
                },
                db.Addons, a => a.CloudId!, a => a.UpdatedAt),

            SyncCollections.Bookings => ApplyBookingsAsync(db, docs, ct),

            SyncCollections.Payments => ApplyPaymentsAsync(db, docs, ct),

            SyncCollections.BookingAddons => ApplyBookingAddonsAsync(db, docs, ct),

            _ => (0, 0)
        };
        return Task.FromResult(result);

    }

    private static (int Applied, int Deleted) ApplySimple<T>(
        ResortDbContext db,
        IReadOnlyList<RemoteDoc> docs,
        Func<RemoteDoc, T> parse,
        Action<T, T> copyScalars,
        Microsoft.EntityFrameworkCore.DbSet<T> set,
        Func<T, string> cloudId,
        Func<T, DateTime> updatedAt)
        where T : class, ISyncable
    {
        var locals = set.AsNoTracking().ToList()
            .Where(x => x.CloudId is not null)
            .GroupBy(cloudId)
            .ToDictionary(g => g.Key, g => g.First());

        var pendingDeletes = PendingDeletes(db, typeof(T) == typeof(Guest)
            ? SyncCollections.Guests
            : SyncCollections.Addons);

        var applied = 0;
        var deleted = 0;

        foreach (var doc in docs)
        {
            if (doc.IsDeleted)
            {
                if (locals.TryGetValue(doc.Id, out var dying)
                    && updatedAt(dying) <= doc.UpdatedAt)
                {
                    set.Remove(dying); // detached Remove() tracks as Deleted
                    deleted++;
                }

                continue;
            }

            var remote = parse(doc);

            if (locals.TryGetValue(doc.Id, out var local))
            {
                if (remote.UpdatedAt > updatedAt(local))
                {
                    copyScalars(local, remote);
                    local.UpdatedAt = remote.UpdatedAt;
                    set.Attach(local); // AsNoTracking instance - re-attach to modify
                    db.Entry(local).State = EntityState.Modified;
                    applied++;
                }
            }
            else
            {
                // A pending local delete wins against an equal-or-older remote row;
                // a newer remote row wins and retires the tombstone.
                if (pendingDeletes.TryGetValue(doc.Id, out var deletedAt)
                    && deletedAt >= remote.UpdatedAt)
                {
                    continue;
                }

                set.Add(remote);
                DropObsoleteTombstones(db, doc.Id, remote.UpdatedAt);
                applied++;
            }
        }

        return (applied, deleted);
    }

    private (int Applied, int Deleted) ApplyBookingsAsync(
        ResortDbContext db, IReadOnlyList<RemoteDoc> docs, CancellationToken ct)
    {
        var guestByCloud = db.Guests.AsNoTracking()
            .Where(g => g.CloudId != null)
            .ToList()
            .GroupBy(g => g.CloudId!)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var locals = db.Bookings.AsNoTracking()
            .Where(b => b.CloudId != null)
            .ToList()
            .GroupBy(b => b.CloudId!)
            .ToDictionary(g => g.Key, g => g.First());

        var pendingDeletes = PendingDeletes(db, SyncCollections.Bookings);

        var applied = 0;
        var deleted = 0;
        var skipped = 0;

        foreach (var doc in docs)
        {
            if (doc.IsDeleted)
            {
                if (locals.TryGetValue(doc.Id, out var dying) && dying.UpdatedAt <= doc.UpdatedAt)
                {
                    db.Bookings.Remove(FetchTracked(db, dying));
                    deleted++;
                }

                continue;
            }

            if (!guestByCloud.TryGetValue(Str(doc, "guest_cloud_id"), out var guestId))
            {
                skipped++; // parent not synced yet — next cycle resolves it
                continue;
            }

            var remote = DocMapper.ToBooking(doc);

            if (locals.TryGetValue(doc.Id, out var local))
            {
                if (remote.UpdatedAt > local.UpdatedAt)
                {
                    var tracked = FetchTracked(db, local);
                    CopyBooking(tracked, remote);
                    tracked.GuestId = guestId;
                    tracked.UpdatedAt = remote.UpdatedAt;
                    applied++;
                }
            }
            else
            {
                if (pendingDeletes.TryGetValue(doc.Id, out var deletedAt)
                    && deletedAt >= remote.UpdatedAt)
                {
                    continue;
                }

                remote.GuestId = guestId;
                db.Bookings.Add(remote);
                DropObsoleteTombstones(db, doc.Id, remote.UpdatedAt);
                applied++;
            }
        }

        if (skipped > 0)
        {
            _logger.LogWarning("{Count} booking(s) skipped pending their guest.", skipped);
        }

        return (applied, deleted);

        static Booking FetchTracked(ResortDbContext context, Booking detached)
            => context.Bookings.Local.FirstOrDefault(b => b.Id == detached.Id)
               ?? context.Bookings.Attach(detached).Entity;
    }

    private (int Applied, int Deleted) ApplyPaymentsAsync(
        ResortDbContext db, IReadOnlyList<RemoteDoc> docs, CancellationToken ct)
    {
        var bookingByCloud = db.Bookings.AsNoTracking()
            .Where(b => b.CloudId != null)
            .ToList()
            .GroupBy(b => b.CloudId!)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var locals = db.PaymentTransactions.AsNoTracking()
            .Where(p => p.CloudId != null)
            .ToList()
            .GroupBy(p => p.CloudId!)
            .ToDictionary(g => g.Key, g => g.First());

        var pendingDeletes = PendingDeletes(db, SyncCollections.Payments);

        var applied = 0;
        var deleted = 0;

        foreach (var doc in docs)
        {
            if (doc.IsDeleted)
            {
                if (locals.TryGetValue(doc.Id, out var dying) && dying.UpdatedAt <= doc.UpdatedAt)
                {
                    db.PaymentTransactions.Remove(
                        db.PaymentTransactions.Local.FirstOrDefault(p => p.Id == dying.Id)
                        ?? db.PaymentTransactions.Attach(dying).Entity);
                    deleted++;
                }

                continue;
            }

            if (!bookingByCloud.TryGetValue(Str(doc, "booking_cloud_id"), out var bookingId))
            {
                continue; // booking not synced yet — next cycle
            }

            var remote = DocMapper.ToPayment(doc);

            if (locals.TryGetValue(doc.Id, out var local))
            {
                if (remote.UpdatedAt > local.UpdatedAt)
                {
                    var tracked = db.PaymentTransactions.Local.FirstOrDefault(p => p.Id == local.Id)
                                  ?? db.PaymentTransactions.Attach(local).Entity;
                    tracked.PaymentDate = remote.PaymentDate;
                    tracked.AmountPaid = remote.AmountPaid;
                    tracked.PaymentType = remote.PaymentType;
                    tracked.PaymentMethod = remote.PaymentMethod;
                    tracked.ReferenceNumber = remote.ReferenceNumber;
                    tracked.Notes = remote.Notes;
                    tracked.UpdatedAt = remote.UpdatedAt;
                    applied++;
                }
            }
            else
            {
                if (pendingDeletes.TryGetValue(doc.Id, out var deletedAt)
                    && deletedAt >= remote.UpdatedAt)
                {
                    continue;
                }

                remote.BookingId = bookingId;
                db.PaymentTransactions.Add(remote);
                DropObsoleteTombstones(db, doc.Id, remote.UpdatedAt);
                applied++;
            }
        }

        return (applied, deleted);
    }

    private (int Applied, int Deleted) ApplyBookingAddonsAsync(
        ResortDbContext db, IReadOnlyList<RemoteDoc> docs, CancellationToken ct)
    {
        var bookingByCloud = db.Bookings.AsNoTracking()
            .Where(b => b.CloudId != null)
            .ToList()
            .GroupBy(b => b.CloudId!)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var addonByCloud = db.Addons.AsNoTracking()
            .Where(a => a.CloudId != null)
            .ToList()
            .GroupBy(a => a.CloudId!)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var locals = db.BookingAddons.AsNoTracking()
            .Where(x => x.CloudId != null)
            .ToList()
            .GroupBy(x => x.CloudId!)
            .ToDictionary(g => g.Key, g => g.First());

        var pendingDeletes = PendingDeletes(db, SyncCollections.BookingAddons);

        var applied = 0;
        var deleted = 0;

        foreach (var doc in docs)
        {
            if (doc.IsDeleted)
            {
                if (locals.TryGetValue(doc.Id, out var dying) && dying.UpdatedAt <= doc.UpdatedAt)
                {
                    db.BookingAddons.Remove(
                        db.BookingAddons.Local.FirstOrDefault(x => x.Id == dying.Id)
                        ?? db.BookingAddons.Attach(dying).Entity);
                    deleted++;
                }

                continue;
            }

            if (!bookingByCloud.TryGetValue(Str(doc, "booking_cloud_id"), out var bookingId)
                || !addonByCloud.TryGetValue(Str(doc, "addon_cloud_id"), out var addonId))
            {
                continue;
            }

            var remote = DocMapper.ToBookingAddon(doc);

            if (locals.TryGetValue(doc.Id, out var local))
            {
                if (remote.UpdatedAt > local.UpdatedAt)
                {
                    var tracked = db.BookingAddons.Local.FirstOrDefault(x => x.Id == local.Id)
                                  ?? db.BookingAddons.Attach(local).Entity;
                    tracked.Quantity = remote.Quantity;
                    tracked.UnitPrice = remote.UnitPrice;
                    tracked.UpdatedAt = remote.UpdatedAt;
                    applied++;
                }
            }
            else
            {
                if (pendingDeletes.TryGetValue(doc.Id, out var deletedAt)
                    && deletedAt >= remote.UpdatedAt)
                {
                    continue;
                }

                remote.BookingId = bookingId;
                remote.AddonId = addonId;
                db.BookingAddons.Add(remote);
                DropObsoleteTombstones(db, doc.Id, remote.UpdatedAt);
                applied++;
            }
        }

        return (applied, deleted);
    }

    // ── Push ─────────────────────────────────────────────────────────────────

    private async Task<(int Pushed, int Tombstones)> PushAsync(
        ResortDbContext db, CancellationToken ct)
    {
        var pushed = 0;
        var tombstoned = 0;

        // Settings doc first (cheap; lets other devices price correctly).
        pushed += await PushSettingsAsync(db, ct);

        foreach (var collection in SyncCollections.PullOrder)
        {
            var state = await StateAsync(db, collection, ct);
            var since = ParseStamp(state.PushedThrough) ?? DateTime.MinValue;
            var writes = new List<CloudWrite>();

            switch (collection)
            {
                case SyncCollections.Guests:
                {
                    var rows = await db.Guests.AsNoTracking()
                        .Where(g => g.UpdatedAt > since)
                        .OrderBy(g => g.UpdatedAt)
                        .ToListAsync(ct);
                    writes.AddRange(rows.Select(g => new CloudWrite(
                        collection, g.CloudId!, DocMapper.Fields(g))));
                    pushed += rows.Count;
                    if (rows.Count > 0) state.PushedThrough = FormatStamp(rows[^1].UpdatedAt);
                    break;
                }

                case SyncCollections.Addons:
                {
                    var rows = await db.Addons.AsNoTracking()
                        .Where(a => a.UpdatedAt > since)
                        .OrderBy(a => a.UpdatedAt)
                        .ToListAsync(ct);
                    writes.AddRange(rows.Select(a => new CloudWrite(
                        collection, a.CloudId!, DocMapper.Fields(a))));
                    pushed += rows.Count;
                    if (rows.Count > 0) state.PushedThrough = FormatStamp(rows[^1].UpdatedAt);
                    break;
                }

                case SyncCollections.Bookings:
                {
                    var rows = await db.Bookings.AsNoTracking()
                        .Include(b => b.Guest)
                        .Where(b => b.UpdatedAt > since)
                        .OrderBy(b => b.UpdatedAt)
                        .ToListAsync(ct);
                    writes.AddRange(rows.Select(b => new CloudWrite(
                        collection, b.CloudId!, DocMapper.Fields(b, b.Guest.CloudId!))));
                    pushed += rows.Count;
                    if (rows.Count > 0) state.PushedThrough = FormatStamp(rows[^1].UpdatedAt);
                    break;
                }

                case SyncCollections.BookingAddons:
                {
                    var rows = await db.BookingAddons.AsNoTracking()
                        .Include(x => x.Booking).ThenInclude(b => b.Guest)
                        .Include(x => x.Addon)
                        .Where(x => x.UpdatedAt > since)
                        .OrderBy(x => x.UpdatedAt)
                        .ToListAsync(ct);
                    writes.AddRange(rows.Select(x => new CloudWrite(
                        collection, x.CloudId!,
                        DocMapper.Fields(x, x.Booking.CloudId!, x.Addon.CloudId!))));
                    pushed += rows.Count;
                    if (rows.Count > 0) state.PushedThrough = FormatStamp(rows[^1].UpdatedAt);
                    break;
                }

                case SyncCollections.Payments:
                {
                    var rows = await db.PaymentTransactions.AsNoTracking()
                        .Include(p => p.Booking).ThenInclude(b => b.Guest)
                        .Where(p => p.UpdatedAt > since)
                        .OrderBy(p => p.UpdatedAt)
                        .ToListAsync(ct);
                    writes.AddRange(rows.Select(p => new CloudWrite(
                        collection, p.CloudId!, DocMapper.Fields(p, p.Booking.CloudId!))));
                    pushed += rows.Count;
                    if (rows.Count > 0) state.PushedThrough = FormatStamp(rows[^1].UpdatedAt);
                    break;
                }
            }

            // Tombstones: overwrite the cloud row as an is_deleted marker.
            var tombstones = await db.Tombstones.AsNoTracking()
                .Where(t => t.Collection == collection)
                .ToListAsync(ct);

            foreach (var tombstone in tombstones)
            {
                writes.Add(new CloudWrite(collection, tombstone.CloudId, new Dictionary<string, object?>
                {
                    ["cloud_id"] = tombstone.CloudId,
                    ["is_deleted"] = true,
                    ["updated_at"] = FirestoreValues.ToFirestoreUtc(tombstone.DeletedAt)
                }));
                tombstoned++;
            }

            if (writes.Count > 0)
            {
                await _transport.CommitAsync(writes, ct);
            }

            if (tombstones.Count > 0)
            {
                db.Tombstones.RemoveRange(tombstones);
                await db.SaveChangesAsync(ct);
            }
            else if (writes.Count > 0)
            {
                await db.SaveChangesAsync(ct); // persist watermark
            }
        }

        return (pushed, tombstoned);
    }

    private async Task<int> PushSettingsAsync(ResortDbContext db, CancellationToken ct)
    {
        var state = await StateAsync(db, SyncCollections.Settings, ct);
        var since = ParseStamp(state.PushedThrough) ?? DateTime.MinValue;
        var settings = await _settings.LoadAsync();

        if (settings.SettingsUpdatedAt == default || settings.SettingsUpdatedAt <= since)
        {
            return 0;
        }

        await _transport.CommitAsync(new[]
        {
            new CloudWrite(SyncCollections.Settings, "app", DocMapper.Fields(settings))
        }, ct);

        state.PushedThrough = FormatStamp(settings.SettingsUpdatedAt);
        await db.SaveChangesAsync(ct);
        return 1;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Pending local deletes for a collection, keyed by CloudId.</summary>
    private static Dictionary<string, DateTime> PendingDeletes(ResortDbContext db, string collection)
        => db.Tombstones.AsNoTracking()
            .Where(t => t.Collection == collection)
            .ToList()
            .ToDictionary(t => t.CloudId, t => t.DeletedAt);

    /// <summary>Retires tombstones older than a remote row that just won LWW.</summary>
    private static void DropObsoleteTombstones(ResortDbContext db, string cloudId, DateTime remoteUpdatedAt)
    {
        var stale = db.Tombstones
            .Where(t => t.CloudId == cloudId && t.DeletedAt < remoteUpdatedAt)
            .ToList();
        if (stale.Count > 0)
        {
            db.Tombstones.RemoveRange(stale);
        }
    }
    private static async Task<SyncState> StateAsync(
        ResortDbContext db, string collection, CancellationToken ct)
    {
        var state = await db.SyncStates
            .FirstOrDefaultAsync(s => s.Collection == collection, ct);

        if (state is null)
        {
            state = new SyncState { Collection = collection };
            db.SyncStates.Add(state);
        }

        return state;
    }

    /// <summary>Wire/RFC-ish stamp stored in SyncState cursors (UTC, sortable).</summary>
    internal static string FormatStamp(DateTime utc)
        => FirestoreValues.FormatUtc(utc);

    internal static DateTime? ParseStamp(string? stamp)
        => string.IsNullOrEmpty(stamp) ? null : FirestoreValues.ParseTimestamp(stamp);

    private static string Str(RemoteDoc doc, string key, string fallback = "")
        => doc.Fields.TryGetValue(key, out var v) && v is string s ? s : fallback;

    private static decimal Dec(RemoteDoc doc, string key, decimal fallback)
        => doc.Fields.TryGetValue(key, out var v)
            ? v switch { double d => (decimal)d, decimal m => m, long l => l, _ => fallback }
            : fallback;

    private static void CopyBooking(Booking local, Booking remote)
    {
        local.CheckInDate = remote.CheckInDate;
        local.CheckOutDate = remote.CheckOutDate;
        local.OvernightStayType = remote.OvernightStayType;
        local.BaseGuests = remote.BaseGuests;
        local.AdditionalGuests = remote.AdditionalGuests;
        local.PricePerExtraGuest = remote.PricePerExtraGuest;
        local.BaseAmount = remote.BaseAmount;
        local.AddonsAmount = remote.AddonsAmount;
        local.TotalAmount = remote.TotalAmount;
        local.DepositRequired = remote.DepositRequired;
        local.RemainingBalance = remote.RemainingBalance;
        local.Status = remote.Status;
        local.CreatedAt = remote.CreatedAt;
        local.DepositDeadline = remote.DepositDeadline;
        local.CompletedAt = remote.CompletedAt;
        local.Notes = remote.Notes;
    }
}
