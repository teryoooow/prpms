using Microsoft.EntityFrameworkCore;
using ResortSync.Models;

namespace ResortSync.Data;

/// <summary>
/// EF Core context for the local SQLite database.
/// Registered via <c>AddDbContextFactory</c> — WPF creates short-lived contexts per
/// operation (one context per ViewModel action) which is the recommended pattern
/// for single-threaded UI apps.
/// </summary>
public class ResortDbContext : DbContext
{
    public ResortDbContext(DbContextOptions<ResortDbContext> options) : base(options) { }

    /// <summary>
    /// While true, <see cref="SaveChanges"/> performs no sync stamping: the sync
    /// engine is applying rows exactly as the cloud sent them (CloudId and
    /// UpdatedAt must survive untouched or devices would ping-pong timestamps).
    /// </summary>
    internal bool SyncApplyMode { get; set; }

    // ── DbSets ───────────────────────────────────────────────────────────────
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Addon> Addons => Set<Addon>();
    public DbSet<BookingAddon> BookingAddons => Set<BookingAddon>();
    public DbSet<Tombstone> Tombstones => Set<Tombstone>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampSyncFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampSyncFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Assigns CloudId/UpdatedAt to app-originated writes and converts hard
    /// deletes of synced rows into tombstones (one transaction, so a row can
    /// never vanish locally without its removal being recorded for push).
    /// </summary>
    private void StampSyncFields()
    {
        if (SyncApplyMode)
        {
            return; // remote rows arrive fully stamped — preserve them verbatim
        }

        // Truncate to whole milliseconds: the wire format carries millisecond
        // precision, so sub-ms ticks would come back truncated and every
        // watermark comparison (UpdatedAt > PushedThrough) would re-fire forever.
        var now = ResortSync.Sync.FirestoreValues.TruncateMs(DateTime.UtcNow);
        var entries = ChangeTracker.Entries().ToList(); // materialized: we add below
        var deleted = new List<(string CloudId, string Collection)>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity is ISyncable added:
                    added.CloudId ??= Guid.NewGuid().ToString("N");
                    added.UpdatedAt = now;
                    break;

                case EntityState.Modified when entry.Entity is ISyncable modified:
                    modified.UpdatedAt = now;
                    break;

                case EntityState.Deleted when entry.Entity is ISyncable { CloudId: not null } dying:
                    deleted.Add((dying.CloudId!, SyncCollections.For(entry.Entity.GetType())));
                    break;
            }
        }

        if (deleted.Count == 0)
        {
            return;
        }

        var dyingIds = deleted.Select(d => d.CloudId).Distinct().ToList();
        var alreadyTombstoned = Tombstones
            .Where(t => dyingIds.Contains(t.CloudId))
            .Select(t => t.CloudId)
            .ToList();

        foreach (var (cloudId, collection) in deleted)
        {
            if (alreadyTombstoned.Contains(cloudId))
            {
                continue; // re-delete: keep the original (older) delete time
            }

            Tombstones.Add(new Tombstone
            {
                CloudId = cloudId,
                Collection = collection,
                DeletedAt = now
            });
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── Guest ─────────────────────────────────────────────────────────────
        mb.Entity<Guest>(e =>
        {
            e.HasIndex(g => g.Phone);
            e.Property(g => g.FullName).IsRequired().HasMaxLength(150);
        });

        // ── Booking ───────────────────────────────────────────────────────────
        mb.Entity<Booking>(e =>
        {
            e.HasIndex(b => b.Status);
            e.HasIndex(b => b.CheckInDate);
            e.HasIndex(b => b.DepositDeadline);

            // Store enums as readable strings so the SQLite file is human-auditable.
            e.Property(b => b.OvernightStayType).HasConversion<string>().HasMaxLength(40);
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(40);

            // Money: fixed 2-decimal precision.
            foreach (var prop in new[]
                     {
                         nameof(Booking.BaseAmount), nameof(Booking.AddonsAmount),
                         nameof(Booking.TotalAmount), nameof(Booking.DepositRequired),
                         nameof(Booking.RemainingBalance), nameof(Booking.PricePerExtraGuest)
                     })
            {
                e.Property(prop).HasPrecision(18, 2);
            }

            // Keep the guest row rather than cascading-delete their stay history.
            e.HasOne(b => b.Guest)
             .WithMany(g => g.Bookings)
             .HasForeignKey(b => b.GuestId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── PaymentTransaction ────────────────────────────────────────────────
        mb.Entity<PaymentTransaction>(e =>
        {
            e.HasIndex(p => p.BookingId);
            e.HasIndex(p => p.PaymentDate);

            e.Property(p => p.PaymentType).HasConversion<string>().HasMaxLength(40);
            e.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(40);
            e.Property(p => p.AmountPaid).HasPrecision(18, 2);

            // Deleting a booking removes its payments (cascade).
            e.HasOne(p => p.Booking)
             .WithMany(b => b.Payments)
             .HasForeignKey(p => p.BookingId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Addon ─────────────────────────────────────────────────────────────
        mb.Entity<Addon>(e =>
        {
            e.Property(a => a.Name).IsRequired().HasMaxLength(120);
            e.Property(a => a.UnitPrice).HasPrecision(18, 2);
        });

        // ── BookingAddon ──────────────────────────────────────────────────────
        mb.Entity<BookingAddon>(e =>
        {
            e.HasIndex(x => new { x.BookingId, x.AddonId }).IsUnique();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);

            e.HasOne(x => x.Booking)
             .WithMany(b => b.BookingAddons)
             .HasForeignKey(x => x.BookingId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Addon)
             .WithMany(a => a.BookingAddons)
             .HasForeignKey(x => x.AddonId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Sync columns on every ISyncable entity ───────────────────────────
        foreach (var entityType in new[]
                 {
                     typeof(Guest), typeof(Booking), typeof(PaymentTransaction),
                     typeof(Addon), typeof(BookingAddon)
                 })
        {
            var e = mb.Entity(entityType);
            e.HasIndex(nameof(ISyncable.CloudId)).IsUnique();
            e.HasIndex(nameof(ISyncable.UpdatedAt));
        }

        // ── Tombstones (hard-delete propagation) ─────────────────────────────
        mb.Entity<Tombstone>(e =>
        {
            e.HasIndex(t => t.CloudId);
            e.Property(t => t.CloudId).IsRequired().HasMaxLength(32);
            e.Property(t => t.Collection).IsRequired().HasMaxLength(40);
        });

        // ── Sync watermarks ──────────────────────────────────────────────────
        mb.Entity<SyncState>(e =>
        {
            e.HasKey(s => s.Collection);
            e.Property(s => s.Collection).HasMaxLength(40);
            e.Property(s => s.Cursor).HasMaxLength(40);
            e.Property(s => s.PushedThrough).HasMaxLength(40);
        });
    }
}
