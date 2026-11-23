using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResortSync.Constants;
using ResortSync.Models;

namespace ResortSync.Data;

/// <summary>
/// Creates the SQLite database on first run, upgrades pre-sync databases with
/// the sync schema, and seeds the addon catalog.
///
/// NOTE FOR PRODUCTION SCHEMA EVOLUTION:
/// For a shipped product prefer real EF migrations:
///   dotnet ef migrations add InitialCreate
///   (then call <c>db.Database.Migrate()</c> here instead of EnsureCreated()).
/// EnsureCreated is used so the app runs with zero tooling setup on any machine;
/// new columns added after a release are applied by <see cref="SyncSchema"/>.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// The canonical add-on catalog. Names double as deterministic sync keys:
    /// every device that seeds this list derives the same CloudId from the name,
    /// so independently-seeded catalogs merge instead of duplicating.
    /// </summary>
    private static readonly (string Name, decimal Price)[] SeedCatalog =
    {
        ("Videoke", 500m),
        ("Gas Stove", 150m),
        ("Extra Towels (set of 3)", 100m),
        ("Inflatable Pool", 800m),
        ("Extra Mattress", 350m),
        ("Bluetooth Speaker", 300m),
        ("Grill Set + Charcoal", 250m),
        ("Early Check-in (1 hr)", 400m),
        ("Late Check-out (1 hr)", 400m),
        ("Additional Parking Slot", 150m)
    };

    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ResortDbContext>>();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ResortDbContext>>();

        using var db = factory.CreateDbContext();

        logger.LogInformation("Database path: {Path}", AppConstants.DatabasePath);
        db.Database.EnsureCreated();

        SyncSchema.Apply(db, logger);

        SeedAddons(db, logger);
    }

    /// <summary>
    /// Deterministic CloudId for a catalog add-on seeded by name
    /// (e.g. "Videoke" → "seed-addon-videoke"); null when the name is not part
    /// of the canonical catalog.
    /// </summary>
    public static string? SeedAddonCloudId(string name)
    {
        if (!SeedCatalog.Any(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
        {
            return null;
        }

        var slug = new string(name
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return "seed-addon-" + slug.Trim('-');
    }

    /// <summary>Seeds the rentable-addon catalog (idempotent — skips when already present).</summary>
    private static void SeedAddons(ResortDbContext db, ILogger logger)
    {
        if (db.Addons.Any())
        {
            return; // already seeded
        }

        foreach (var (name, price) in SeedCatalog)
        {
            db.Addons.Add(new Addon
            {
                Name = name,
                UnitPrice = price,
                CloudId = SeedAddonCloudId(name)
            });
        }

        db.SaveChanges();
        logger.LogInformation("Addon catalog seeded ({Count} items).", db.Addons.Count());
    }
}
