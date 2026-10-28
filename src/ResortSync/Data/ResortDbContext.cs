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

    // ── DbSets ───────────────────────────────────────────────────────────────
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Addon> Addons => Set<Addon>();
    public DbSet<BookingAddon> BookingAddons => Set<BookingAddon>();

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
    }
}
