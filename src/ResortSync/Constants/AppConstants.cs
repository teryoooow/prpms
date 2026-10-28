using System.IO;

namespace ResortSync.Constants;

/// <summary>
/// Central application configuration: file-system locations and business-rule defaults.
/// </summary>
public static class AppConstants
{
    /// <summary>Default share of the total charged as the required downpayment (50%).</summary>
    public const decimal DefaultDownpaymentPercent = 0.50m;

    /// <summary>Hours allowed to pay the downpayment before the booking auto-expires.</summary>
    public const int DepositDeadlineHours = 24;

    /// <summary>Dashboard "about to expire" alert threshold.</summary>
    public const int ExpiringSoonAlertHours = 4;

    /// <summary>How often the auto-expiry background worker wakes up.</summary>
    public static readonly TimeSpan ExpiryCheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Per-user data directory: %LocalAppData%\ResortSync.
    /// Created on first access.
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ResortSync");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Full path of the SQLite database file.</summary>
    public static string DatabasePath => Path.Combine(DataDirectory, "resortsync.db");

    /// <summary>SQLite connection string used by EF Core.</summary>
    public static string ConnectionString => $"Data Source={DatabasePath}";

    /// <summary>Path of the persisted UI settings JSON file.</summary>
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
}
