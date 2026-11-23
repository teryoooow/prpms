using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResortSync.Data;
using ResortSync.Models;
using Xunit;

namespace ResortSync.Tests;

public class MigrationTests
{
    // ── Fresh database ────────────────────────────────────────────────────────

    [Fact]
    public void FreshDatabase_CreatesSyncSchema_AndSeedCloudIds()
    {
        using var test = new TestDb();
        test.Initialize();

        using var db = test.CreateContext();

        var addon = db.Addons.Single(a => a.Name == "Videoke");
        Assert.Equal("seed-addon-videoke", addon.CloudId);
        Assert.NotEqual(default, addon.UpdatedAt);

        // Tombstones + SyncStates exist on a brand-new install too.
        Assert.Empty(db.Tombstones);
        Assert.Empty(db.SyncStates);
    }

    [Fact]
    public void FreshDatabase_AllowSeededCatalogToConvergeAcrossDevices()
    {
        // Two devices seeding independently must derive identical CloudIds.
        using var a = new TestDb();
        a.Initialize();
        using var b = new TestDb();
        b.Initialize();

        using var dbA = a.CreateContext();
        using var dbB = b.CreateContext();

        var idsA = dbA.Addons.AsNoTracking().ToDictionary(x => x.Name, x => x.CloudId);
        var idsB = dbB.Addons.AsNoTracking().ToDictionary(x => x.Name, x => x.CloudId);

        Assert.Equal(idsA.Count, idsB.Count);
        foreach (var (name, id) in idsA)
        {
            Assert.Equal(id, idsB[name]);
        }
    }

    // ── Pre-sync (existing production) database ───────────────────────────────

    [Fact]
    public void PreSyncDatabase_GainsColumns_Indexes_AndBackfills()
    {
        using var test = new TestDb(createPreSyncSchema: true, seedData: true);
        test.Initialize();

        using var db = test.CreateContext();

        // Columns added.
        var guest = db.Guests.Single(g => g.FullName == "Juan Dela Cruz");
        Assert.False(string.IsNullOrEmpty(guest.CloudId));
        Assert.NotEqual(default, guest.UpdatedAt);

        var booking = db.Bookings.Single();
        Assert.False(string.IsNullOrEmpty(booking.CloudId));

        // Canonical catalog names converge on deterministic seed slugs.
        Assert.Equal("seed-addon-videoke", db.Addons.Single(x => x.Name == "Videoke").CloudId);
        Assert.Equal("seed-addon-gas-stove", db.Addons.Single(x => x.Name == "Gas Stove").CloudId);

        // Sync tables created.
        Assert.Empty(db.Tombstones);
        Assert.Empty(db.SyncStates);

        // Unique CloudId index enforces identity.
        Assert.ThrowsAny<DbUpdateException>(() =>
        {
            db.Guests.Add(new Guest
            {
                FullName = "Dup",
                CloudId = guest.CloudId, // collision on purpose
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });
    }

    [Fact]
    public void PreSyncDatabase_InitializeIsIdempotent()
    {
        using var test = new TestDb(createPreSyncSchema: true, seedData: true);
        test.Initialize();
        test.Initialize(); // second launch must be a no-op
        test.Initialize();

        using var db = test.CreateContext();
        Assert.Single(db.Bookings);
        Assert.Equal(2, db.Addons.Count());
    }

    [Fact]
    public void PreSyncDatabase_SeededAddonsKeepStableIds_AfterRepeatedInit()
    {
        using var test = new TestDb(createPreSyncSchema: true, seedData: true);
        test.Initialize();
        var first = test.CreateContext().Addons.Single(x => x.Name == "Videoke").CloudId;

        test.Initialize();

        using var db = test.CreateContext();
        Assert.Equal(first, db.Addons.Single(x => x.Name == "Videoke").CloudId);
    }

    // ── Real production database (skipped when absent) ────────────────────────

    [Fact]
    public void RealDesktopDatabase_UpgradesCleanly()
    {
        var real = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ResortSync", "resortsync.db");

        if (!File.Exists(real))
        {
            return; // not on this machine — covered by the synthetic tests above
        }

        var copyDir = Path.Combine(Path.GetTempPath(), "prpms-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(copyDir);
        var copy = Path.Combine(copyDir, "resortsync.db");
        File.Copy(real, copy);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILogger<ResortDbContext>>(
                NullLogger<ResortDbContext>.Instance);
            services.AddDbContextFactory<ResortDbContext>(
                o => o.UseSqlite($"Data Source={copy}"));
            using var provider = services.BuildServiceProvider();

            DbInitializer.Initialize(provider);

            using var db = provider
                .GetRequiredService<IDbContextFactory<ResortDbContext>>()
                .CreateDbContext();

            Assert.All(db.Guests.ToList(), g => Assert.False(string.IsNullOrEmpty(g.CloudId)));
            Assert.All(db.Bookings.ToList(), b => Assert.False(string.IsNullOrEmpty(b.CloudId)));
            Assert.All(db.Addons.ToList(), a => Assert.False(string.IsNullOrEmpty(a.CloudId)));
        }
        finally
        {
            try { Directory.Delete(copyDir, true); } catch { /* best effort */ }
        }
    }
}
