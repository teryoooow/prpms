using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ResortSync.Data;

/// <summary>
/// Non-destructive upgrade path for databases created before sync existed.
/// (The project uses <c>EnsureCreated</c> instead of EF migrations, so new
/// columns are added by hand with guarded ALTERs — see DbInitializer docs.)
/// </summary>
internal static class SyncSchema
{
    /// <summary>
    /// Adds CloudId/UpdatedAt columns, sync indexes and the Tombstones/SyncStates
    /// tables when missing, then backfills CloudIds for pre-sync rows.
    /// Idempotent — no-ops on databases that already have the schema.
    /// </summary>
    public static void Apply(ResortDbContext db, ILogger logger)
    {
        var tables = QueryStrings(db,
            "SELECT name FROM sqlite_master WHERE type = 'table'");

        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff",
            CultureInfo.InvariantCulture);

        var syncTables = new Dictionary<string, (string Table, string Entity)>
        {
            ["Guests"] = ("Guests", "guests"),
            ["Bookings"] = ("Bookings", "bookings"),
            ["PaymentTransactions"] = ("PaymentTransactions", "payments"),
            ["Addons"] = ("Addons", "addons"),
            ["BookingAddons"] = ("BookingAddons", "booking_addons")
        };

        var changed = false;

        foreach (var (table, _) in syncTables.Values)
        {
            if (!tables.Contains(table, StringComparer.OrdinalIgnoreCase))
            {
                continue; // fresh DB — EnsureCreated already built the full schema
            }

            var columns = QueryStrings(db, $"PRAGMA table_info(\"{table}\")", columnNameIndex: 1)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (columns.Contains("CloudId"))
            {
                continue;
            }

            Execute(db, $"ALTER TABLE \"{table}\" ADD COLUMN \"CloudId\" TEXT");
            Execute(db,
                $"ALTER TABLE \"{table}\" ADD COLUMN \"UpdatedAt\" TEXT NOT NULL DEFAULT '{stamp}'");
            changed = true;
        }

        if (changed)
        {
            logger.LogInformation("Sync columns added to pre-existing database tables.");
        }

        // Indexes (names match EF conventions; IF NOT EXISTS covers both paths).
        foreach (var table in syncTables.Keys)
        {
            Execute(db, $"CREATE UNIQUE INDEX IF NOT EXISTS \"IX_{table}_CloudId\" ON \"{table}\" (\"CloudId\")");
            Execute(db, $"CREATE INDEX IF NOT EXISTS \"IX_{table}_UpdatedAt\" ON \"{table}\" (\"UpdatedAt\")");
        }

        Execute(db, """
            CREATE TABLE IF NOT EXISTS "Tombstones" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Tombstones" PRIMARY KEY AUTOINCREMENT,
                "CloudId" TEXT NOT NULL,
                "Collection" TEXT NOT NULL,
                "DeletedAt" TEXT NOT NULL
            );
            """);
        Execute(db, """CREATE INDEX IF NOT EXISTS "IX_Tombstones_CloudId" ON "Tombstones" ("CloudId");""");
        Execute(db, """
            CREATE TABLE IF NOT EXISTS "SyncStates" (
                "Collection" TEXT NOT NULL CONSTRAINT "PK_SyncStates" PRIMARY KEY,
                "Cursor" TEXT NOT NULL,
                "PushedThrough" TEXT NOT NULL,
                "LastSyncAt" TEXT NULL
            );
            """);

        BackfillCloudIds(db, logger);
    }

    /// <summary>
    /// Gives pre-sync rows their permanent CloudIds. Addons are keyed by a
    /// deterministic slug derived from their catalog name so that two devices
    /// seeding the same catalog independently converge on the same CloudId
    /// instead of duplicating every add-on after the first sync.
    /// </summary>
    private static void BackfillCloudIds(ResortDbContext db, ILogger logger)
    {
        // Bulk random GUIDs for rows that predate sync (SQL = one pass per table).
        foreach (var table in new[] { "Guests", "Bookings", "PaymentTransactions", "BookingAddons" })
        {
            Execute(db,
                $"UPDATE \"{table}\" SET \"CloudId\" = lower(hex(randomblob(16))) WHERE \"CloudId\" IS NULL");
        }

        // Addons: canonical catalog names get deterministic seed slugs first.
        var addons = db.Addons.Where(a => a.CloudId == null).ToList();
        if (addons.Count == 0)
        {
            return;
        }

        var used = db.Addons
            .Where(a => a.CloudId != null)
            .Select(a => a.CloudId)
            .ToList()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var addon in addons)
        {
            var candidate = DbInitializer.SeedAddonCloudId(addon.Name);
            addon.CloudId = candidate is not null && used.Add(candidate)
                ? candidate
                : Guid.NewGuid().ToString("N");
        }

        db.SaveChanges();
        logger.LogInformation("Backfilled CloudId for {Count} pre-sync addon row(s).", addons.Count);
    }

    private static void Execute(ResortDbContext db, string sql)
        => db.Database.ExecuteSqlRaw(sql);

    private static HashSet<string> QueryStrings(ResortDbContext db, string sql, int columnNameIndex = 0)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(columnNameIndex));
            }
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }

        return result;
    }
}
