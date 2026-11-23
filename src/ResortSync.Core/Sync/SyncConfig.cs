using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>Firebase backend configuration (device-local, entered in Settings).</summary>
public sealed record SyncConfig(
    string ApiKey,
    string ProjectId,
    string Email,
    string Password)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password);

    public static SyncConfig Empty { get; } = new("", "", "", "");
}

public static class SyncConfigExtensions
{
    /// <summary>Projects device-local settings into a backend configuration.</summary>
    public static SyncConfig ToSyncConfig(this SettingsModel settings)
        => new(settings.FirebaseApiKey, settings.FirebaseProjectId,
               settings.SyncEmail, settings.SyncPassword);

    /// <summary>The business fields that travel through the cloud (theme and credentials stay local).</summary>
    public static bool BusinessSettingsEqual(this SettingsModel a, SettingsModel b)
        => a.ResortName == b.ResortName
        && a.DownpaymentPercent == b.DownpaymentPercent
        && a.DefaultExtraGuestRate == b.DefaultExtraGuestRate
        && a.DefaultBaseGuests == b.DefaultBaseGuests;
}

/// <summary>Outcome of one sync cycle (drives the Settings status line).</summary>
public sealed record SyncResult(
    SyncStatus State,
    int Pushed = 0,
    int Pulled = 0,
    int Deleted = 0,
    string? Error = null)
{
    public static SyncResult Disabled { get; } = new(SyncStatus.Disabled);
    public static SyncResult NotConfigured { get; } = new(SyncStatus.SignedOut);
}

public enum SyncStatus
{
    /// <summary>Cloud sync switched off in Settings.</summary>
    Disabled,

    /// <summary>Sync on but Firebase credentials are incomplete.</summary>
    SignedOut,

    /// <summary>Last cycle completed cleanly (Pushed/Pulled/Deleted > 0 when work happened).</summary>
    Idle,

    Syncing,

    Error
}
