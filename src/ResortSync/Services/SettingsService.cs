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
