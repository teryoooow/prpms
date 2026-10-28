using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResortSync.Constants;
using ResortSync.Models;

namespace ResortSync.Data;

/// <summary>
/// Creates the SQLite database on first run and seeds the addon catalog.
///
/// NOTE FOR PRODUCTION SCHEMA EVOLUTION:
/// For a shipped product prefer real EF migrations:
///   dotnet ef migrations add InitialCreate
///   (then call <c>db.Database.Migrate()</c> here instead of EnsureCreated()).
/// EnsureCreated is used so the app runs with zero tooling setup on any machine.
/// </summary>
public static class DbInitializer
{
    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ResortDbContext>>();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ResortDbContext>>();

        using var db = factory.CreateDbContext();

        logger.LogInformation("Database path: {Path}", AppConstants.DatabasePath);
        db.Database.EnsureCreated();

        SeedAddons(db, logger);
    }

    /// <summary>Seeds the rentable-addon catalog (idempotent — skips when already present).</summary>
    private static void SeedAddons(ResortDbContext db, ILogger logger)
    {
        if (db.Addons.Any())
        {
            return; // already seeded
        }

        db.AddRange(
            new Addon { Name = "Videoke", UnitPrice = 500m },
            new Addon { Name = "Gas Stove", UnitPrice = 150m },
            new Addon { Name = "Extra Towels (set of 3)", UnitPrice = 100m },
            new Addon { Name = "Inflatable Pool", UnitPrice = 800m },
            new Addon { Name = "Extra Mattress", UnitPrice = 350m },
            new Addon { Name = "Bluetooth Speaker", UnitPrice = 300m },
            new Addon { Name = "Grill Set + Charcoal", UnitPrice = 250m },
            new Addon { Name = "Early Check-in (1 hr)", UnitPrice = 400m },
            new Addon { Name = "Late Check-out (1 hr)", UnitPrice = 400m },
            new Addon { Name = "Additional Parking Slot", UnitPrice = 150m });

        db.SaveChanges();
        logger.LogInformation("Addon catalog seeded ({Count} items).", db.Addons.Count());
    }
}
