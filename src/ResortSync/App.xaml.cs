using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ResortSync.Constants;
using ResortSync.Data;
using ResortSync.Services;
using ResortSync.ViewModels;
using ResortSync.Views;

namespace ResortSync;

/// <summary>
/// Application root: builds the Generic Host (DI + background services), seeds the
/// database, applies the persisted theme, and shows the shell window.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>Access point for services that cannot use constructor injection.</summary>
    public static IServiceProvider Services => ((App)Current)._host!.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global safety net — never let a dispatcher exception vanish silently.
        DispatcherUnhandledException += HandleDispatcherException;

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            // Create the SQLite file (if needed) and seed the addon catalog
            // BEFORE the hosted worker starts querying the database.
            DbInitializer.Initialize(_host.Services);

            // Apply the persisted light/dark preference before the window paints.
            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.Apply((await settingsService.LoadAsync()).Theme);

            await _host.StartAsync();

            // Shell window + landing page.
            var window = _host.Services.GetRequiredService<Views.MainWindow>();
            MainWindow = window;
            window.Show();

            await _host.Services.GetRequiredService<INavigationService>()
                .NavigateAsync<DashboardViewModel>();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"PRPMS failed to start:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // ── Data ─────────────────────────────────────────────────────────────
        // IDbContextFactory is the recommended EF pattern for WPF: short-lived
        // contexts are created per operation instead of one long-lived context.
        services.AddDbContextFactory<ResortDbContext>(options =>
            options.UseSqlite(AppConstants.ConnectionString));

        // ── Preferences & presentation services ──────────────────────────────
        services.AddSingleton<ISettingsService>(_ => new SettingsService());
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IReceiptService, ReceiptService>();

        // ── Domain services ──────────────────────────────────────────────────
        services.AddSingleton<IBookingService, BookingService>();
        services.AddSingleton<IPaymentService, PaymentService>();
        services.AddSingleton<IDashboardService, DashboardService>();

        // ── Navigation & background → UI bridge ──────────────────────────────
        services.AddSingleton<DashboardInvalidator>();
        services.AddSingleton<IDashboardInvalidator>(sp => sp.GetRequiredService<DashboardInvalidator>());
        services.AddSingleton<INavigationService, NavigationService>();

        // ── Shell ────────────────────────────────────────────────────────────
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        // ── Background workers ───────────────────────────────────────────────
        services.AddHostedService<BookingExpiryService>();
    }

    /// <summary>
    /// Re-entrancy latch: exceptions raised while this dialog's message pump is
    /// running (e.g. a layout failure re-raised on the next arrange pass) must not
    /// open another nested MessageBox — nested pumps recurse until stack overflow.
    /// </summary>
    private static int _errorDialogOpen;

    private static void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // keep the app alive for monitoring kiosk usage

        if (Interlocked.CompareExchange(ref _errorDialogOpen, 1, 0) != 0)
        {
            return; // a dialog is already showing — swallow the re-raise silently
        }

        try
        {
            MessageBox.Show(
                $"Unexpected error:\n\n{e.Exception.Message}",
                "PRPMS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Volatile.Write(ref _errorDialogOpen, 0);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // Gracefully cancel the expiry worker (it stops on the first tick cancellation).
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch
        {
            // Never block process exit on a misbehaving worker.
        }

        _host?.Dispose();
        base.OnExit(e);
    }
}
