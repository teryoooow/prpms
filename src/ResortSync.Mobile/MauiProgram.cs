using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResortSync.Data;
using ResortSync.Services;
using ResortSync.Sync;

namespace ResortSync.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── Shared data + domain layer (same code as the WPF desktop app) ────
        builder.Services.AddDbContextFactory<ResortDbContext>(options =>
            options.UseSqlite(Constants.AppConstants.ConnectionString));
        builder.Services.AddSingleton<ISettingsService>(_ => new SettingsService());
        builder.Services.AddSingleton<IBookingService, BookingService>();
        builder.Services.AddSingleton<IPaymentService, PaymentService>();

        // ── Cloud sync (same Core classes as desktop) ───────────────────────
        builder.Services.AddSingleton<IDashboardInvalidator, DashboardInvalidator>();
        builder.Services.AddSingleton<IFirebaseAuth, FirebaseAuthClient>();
        builder.Services.AddSingleton<IFirestoreTransport, FirestoreRestClient>();
        builder.Services.AddSingleton<SyncEngine>();
        builder.Services.AddSingleton<SyncCoordinator>();

        // Background workers: MAUI has no Generic Host, so they are registered
        // as plain singletons and StartAsync'd after build (see below).
        builder.Services.AddSingleton<BookingExpiryService>();
        builder.Services.AddSingleton<SyncWorker>();

        var app = builder.Build();

        // Create/upgrade the SQLite file and seed the addon catalog before
        // any page starts querying (mirrors the desktop startup order).
        DbInitializer.Initialize(app.Services);

        // Kick off the background loops (fire-and-forget; they self-cancel on
        // process exit). BookingExpiryService flips overdue PendingDeposit
        // rows; SyncWorker runs a cloud cycle every AppConstants.SyncInterval.
        _ = app.Services.GetRequiredService<BookingExpiryService>()
            .StartAsync(CancellationToken.None);
        _ = app.Services.GetRequiredService<SyncWorker>()
            .StartAsync(CancellationToken.None);

        return app;
    }
}
