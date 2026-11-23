using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>
/// Firebase Authentication via the identitytoolkit REST API: password sign-in
/// returns a short-lived ID token plus a long-lived refresh token (persisted
/// through <see cref="ISettingsService"/> so devices stay signed in).
/// </summary>
public sealed class FirebaseAuthClient : IFirebaseAuth, IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ISettingsService _settings;
    private readonly object _gate = new();

    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

    public FirebaseAuthClient(ISettingsService settings) => _settings = settings;

    public async Task<string> GetIdTokenAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_accessToken is not null
                && DateTime.UtcNow < _accessTokenExpiresAtUtc.AddSeconds(-60))
            {
                return _accessToken;
            }
        }

        var config = (await _settings.LoadAsync()).ToSyncConfig();

        // 1. Try the persisted refresh token first (no credentials round-trip).
        var cachedRefresh = (await _settings.LoadAsync()).SyncRefreshToken;
        if (!string.IsNullOrEmpty(cachedRefresh))
        {
            try
            {
                var refreshed = await PostAsync(
                    $"https://identitytoolkit.googleapis.com/v1/accounts:token?key={config.ApiKey}",
                    new JsonObject
                    {
                        ["grantType"] = "refresh_token",
                        ["refreshToken"] = cachedRefresh
                    },
                    ct);

                return AcceptTokens(refreshed, keepRefreshToken: true);
            }
            catch (HttpRequestException)
            {
                // Refresh token revoked/expired — fall through to full sign-in.
            }
        }

        if (!config.IsConfigured)
        {
            throw new InvalidOperationException(
                "Cloud sync is not configured (Firebase settings missing).");
        }

        // 2. Full password sign-in.
        var signedIn = await PostAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={config.ApiKey}",
            new JsonObject
            {
                ["email"] = config.Email,
                ["password"] = config.Password,
                ["returnSecureToken"] = true
            },
            ct);

        return AcceptTokens(signedIn, keepRefreshToken: true);
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _accessToken = null;
            _accessTokenExpiresAtUtc = DateTime.MinValue;
        }
    }

    private string AcceptTokens(JsonObject response, bool keepRefreshToken)
    {
        var idToken = response["idToken"]?.GetValue<string>()
                      ?? throw new InvalidOperationException("Auth response missing idToken.");
        var expiresIn = long.Parse(response["expiresIn"]?.GetValue<string>() ?? "3600");
        var refreshToken = response["refreshToken"]?.GetValue<string>();

        lock (_gate)
        {
            _accessToken = idToken;
            _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);
        }

        if (keepRefreshToken && refreshToken is not null)
        {
            _ = PersistRefreshTokenAsync(refreshToken);
        }

        return idToken;
    }

    private async Task PersistRefreshTokenAsync(string refreshToken)
    {
        var settings = await _settings.LoadAsync();
        if (settings.SyncRefreshToken == refreshToken)
        {
            return;
        }

        settings.SyncRefreshToken = refreshToken;
        await _settings.SaveAsync(settings);
    }

    private static async Task<JsonObject> PostAsync(
        string url, JsonObject body, CancellationToken ct)
    {
        using var content = new StringContent(
            body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(url, content, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Firebase auth failed ({(int)response.StatusCode}): {Truncate(text)}");
        }

        return JsonNode.Parse(text)?.AsObject()
               ?? throw new InvalidOperationException("Empty auth response.");
    }

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];

    public void Dispose() => Invalidate();
}
