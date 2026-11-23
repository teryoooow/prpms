using ResortSync.Sync;

namespace ResortSync.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    /// <summary>
    /// Resuming from background is the cheapest "catch up after downtime"
    /// trigger — mirrors the desktop worker's run-once-immediately tick.
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();

        try
        {
            IPlatformApplication.Current?.Services
                .GetRequiredService<SyncCoordinator>()
                .RequestSync();
        }
        catch
        {
            // Background trigger stays silent; status lands in Settings.
        }
    }
}
