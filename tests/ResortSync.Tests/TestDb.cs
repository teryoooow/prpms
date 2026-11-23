using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResortSync.Data;
using ResortSync.Services;
using ResortSync.Sync;

namespace ResortSync.Tests;

/// <summary>
/// Self-contained SQLite database in %TEMP% with its own service provider.
/// Optionally created with the PRE-SYNC schema (no CloudId/UpdatedAt columns,
/// no Tombstones/SyncStates) to exercise the upgrade path.
/// </summary>
public sealed class TestDb : IDisposable
{
    public string Dir { get; }
    public string DbPath => Path.Combine(Dir, "test.db");
    public ServiceProvider Services { get; }

    public TestDb(bool createPreSyncSchema = false, bool seedData = false)
    {
        Dir = Path.Combine(Path.GetTempPath(), "prpms-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        Settings = new SettingsService(Path.Combine(Dir, "settings.json"));

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ResortDbContext>>(NullLogger<ResortDbContext>.Instance);
        services.AddDbContextFactory<ResortDbContext>(o => o.UseSqlite($"Data Source={DbPath}"));
        Services = services.BuildServiceProvider();

        if (createPreSyncSchema)
        {
            CreatePreSyncSchema(seedData);
        }
    }

    public IDbContextFactory<ResortDbContext> Factory
        => Services.GetRequiredService<IDbContextFactory<ResortDbContext>>();

    /// <summary>Per-device settings file (each TestDb is its own "device").</summary>
    public SettingsService Settings { get; }

    /// <summary>Turns sync on with dummy credentials (FakeFirebaseAuth ignores them).</summary>
    public async Task EnableSyncAsync()
    {
        var s = await Settings.LoadAsync();
        s.SyncEnabled = true;
        s.FirebaseApiKey = "test-api-key";
        s.FirebaseProjectId = "test-project";
        s.SyncEmail = "device@example.com";
        s.SyncPassword = "secret";
        await Settings.SaveAsync(s);
    }

    /// <summary>Builds a SyncEngine bound to this device's settings/db.</summary>
    public SyncEngine CreateEngine(
        IFirestoreTransport transport,
        IFirebaseAuth? auth = null)
        => new(Factory, transport, auth ?? new FakeFirebaseAuth(), Settings);

    /// <summary>Runs one full sync cycle for this device.</summary>
    public Task<SyncResult> SyncAsync(IFirestoreTransport transport, CancellationToken ct = default)
        => CreateEngine(transport).SyncAsync(ct);

    /// <summary>Runs the same startup pipeline the apps use (create/upgrade/seed).</summary>
    public void Initialize() => DbInitializer.Initialize(Services);

    public ResortDbContext CreateContext() => Factory.CreateDbContext();

    public void Dispose()
    {
        Services.Dispose();
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch
        {
            // temp cleanup is best-effort
        }
    }

    /// <summary>
    /// Builds the schema exactly as it shipped before sync existed (business
    /// columns only), with an addon catalog + one guest/booking so backfill has
    /// rows to work on.
    /// </summary>
    private void CreatePreSyncSchema(bool seedData)
    {
        using var db = CreateContext();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "Guests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Guests" PRIMARY KEY AUTOINCREMENT,
                "FullName" TEXT NOT NULL,
                "Phone" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "IdType" TEXT NOT NULL,
                "IdNumber" TEXT NOT NULL,
                "Notes" TEXT NULL);
            CREATE INDEX "IX_Guests_Phone" ON "Guests" ("Phone");

            CREATE TABLE "Bookings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Bookings" PRIMARY KEY AUTOINCREMENT,
                "GuestId" INTEGER NOT NULL,
                "CheckInDate" TEXT NOT NULL,
                "CheckOutDate" TEXT NOT NULL,
                "OvernightStayType" TEXT NOT NULL,
                "BaseGuests" INTEGER NOT NULL,
                "AdditionalGuests" INTEGER NOT NULL,
                "PricePerExtraGuest" TEXT NOT NULL,
                "BaseAmount" TEXT NOT NULL,
                "AddonsAmount" TEXT NOT NULL,
                "TotalAmount" TEXT NOT NULL,
                "DepositRequired" TEXT NOT NULL,
                "RemainingBalance" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "DepositDeadline" TEXT NOT NULL,
                "CompletedAt" TEXT NULL,
                "Notes" TEXT NULL,
                CONSTRAINT "FK_Bookings_Guests_GuestId" FOREIGN KEY ("GuestId") REFERENCES "Guests" ("Id") ON DELETE RESTRICT);
            CREATE INDEX "IX_Bookings_Status" ON "Bookings" ("Status");
            CREATE INDEX "IX_Bookings_CheckInDate" ON "Bookings" ("CheckInDate");
            CREATE INDEX "IX_Bookings_DepositDeadline" ON "Bookings" ("DepositDeadline");
            CREATE INDEX "IX_Bookings_GuestId" ON "Bookings" ("GuestId");

            CREATE TABLE "PaymentTransactions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PaymentTransactions" PRIMARY KEY AUTOINCREMENT,
                "BookingId" INTEGER NOT NULL,
                "PaymentDate" TEXT NOT NULL,
                "AmountPaid" TEXT NOT NULL,
                "PaymentType" TEXT NOT NULL,
                "PaymentMethod" TEXT NOT NULL,
                "ReferenceNumber" TEXT NULL,
                "Notes" TEXT NULL,
                CONSTRAINT "FK_PaymentTransactions_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE);
            CREATE INDEX "IX_PaymentTransactions_BookingId" ON "PaymentTransactions" ("BookingId");
            CREATE INDEX "IX_PaymentTransactions_PaymentDate" ON "PaymentTransactions" ("PaymentDate");

            CREATE TABLE "Addons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Addons" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "UnitPrice" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL);

            CREATE TABLE "BookingAddons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BookingAddons" PRIMARY KEY AUTOINCREMENT,
                "BookingId" INTEGER NOT NULL,
                "AddonId" INTEGER NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "UnitPrice" TEXT NOT NULL,
                CONSTRAINT "FK_BookingAddons_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_BookingAddons_Addons_AddonId" FOREIGN KEY ("AddonId") REFERENCES "Addons" ("Id") ON DELETE RESTRICT);
            CREATE UNIQUE INDEX "IX_BookingAddons_BookingId_AddonId" ON "BookingAddons" ("BookingId", "AddonId");
            """);

        if (seedData)
        {
            db.Database.ExecuteSqlRaw("""
                INSERT INTO "Addons" ("Name", "UnitPrice", "IsActive") VALUES ('Videoke', '500.0', 1);
                INSERT INTO "Addons" ("Name", "UnitPrice", "IsActive") VALUES ('Gas Stove', '150.0', 1);
                INSERT INTO "Guests" ("FullName", "Phone", "Email", "IdType", "IdNumber")
                    VALUES ('Juan Dela Cruz', '09171234567', '', '', '');
                INSERT INTO "Bookings" ("GuestId", "CheckInDate", "CheckOutDate", "OvernightStayType",
                    "BaseGuests", "AdditionalGuests", "PricePerExtraGuest", "BaseAmount", "AddonsAmount",
                    "TotalAmount", "DepositRequired", "RemainingBalance", "Status", "CreatedAt",
                    "DepositDeadline", "CompletedAt", "Notes")
                    VALUES (1, '2026-11-20 14:00:00', '2026-11-21 12:00:00', 'Overnight',
                    2, 1, '300.0', '5000.0', '0.0', '5300.0', '2650.0', '5300.0', 'PendingDeposit',
                    '2026-11-01 08:00:00', '2026-11-02 08:00:00', NULL, NULL);
                """);
        }
    }
}
