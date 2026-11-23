using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResortSync.Services;

/// <summary>Persisted user preferences (JSON in %LocalAppData%\ResortSync).</summary>
public sealed class SettingsModel
{
    /// <summary>"Dark", "Light", or "System".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>0.50 = 50% required downpayment.</summary>
    public decimal DownpaymentPercent { get; set; } = 0.50m;

    /// <summary>Pre-filled extra-guest rate on the booking form.</summary>
    public decimal DefaultExtraGuestRate { get; set; } = 300m;

    /// <summary>Pre-filled included-guest count on the booking form.</summary>
    public int DefaultBaseGuests { get; set; } = 2;

    /// <summary>Resort name printed on receipts.</summary>
    public string ResortName { get; set; } = "My Private Resort";

    // ── Cloud sync (device-local — never uploaded) ───────────────────────────
    public bool SyncEnabled { get; set; }

    public string FirebaseApiKey { get; set; } = string.Empty;

    public string FirebaseProjectId { get; set; } = string.Empty;

    public string SyncEmail { get; set; } = string.Empty;

    public string SyncPassword { get; set; } = string.Empty;

    /// <summary>Persisted Firebase refresh token (keeps the device signed in).</summary>
    public string SyncRefreshToken { get; set; } = string.Empty;

    /// <summary>Stable per-install id (diagnostics / future device labels).</summary>
    public string DeviceId { get; set; } = string.Empty;

    // ── Sync (business fields travel as one Firestore doc) ───────────────────

    /// <summary>
    /// UTC stamp of the last change to the SYNCED business fields
    /// (ResortName, DownpaymentPercent, DefaultExtraGuestRate, DefaultBaseGuests).
    /// </summary>
    public DateTime SettingsUpdatedAt { get; set; } = default;
}

/// <summary>Loads/saves <see cref="SettingsModel"/> as JSON, tolerating corruption gracefully.</summary>
public interface ISettingsService
{
    Task<SettingsModel> LoadAsync();
    Task SaveAsync(SettingsModel settings);
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private SettingsModel? _cache;

    public SettingsService(string? path = null)
    {
        _path = path ?? Constants.AppConstants.SettingsPath;
    }

    public async Task<SettingsModel> LoadAsync()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        try
        {
            if (File.Exists(_path))
            {
                var json = await File.ReadAllTextAsync(_path);
                _cache = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions)
                         ?? new SettingsModel();
                return _cache;
            }
        }
        catch
        {
            // Corrupt settings file — fall through to defaults rather than crash the app.
        }

        _cache = new SettingsModel();
        return _cache;
    }

    public async Task SaveAsync(SettingsModel settings)
    {
        _cache = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_path, json);
    }
}
