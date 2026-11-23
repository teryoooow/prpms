using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>
/// Firestore REST transport: <c>:runQuery</c> for pulls, <c>:commit</c> for
/// upsert pushes. Auth uses the bearer token from <see cref="IFirebaseAuth"/>;
/// a 401 invalidates the token and retries once.
/// </summary>
public sealed class FirestoreRestClient : IFirestoreTransport
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly ISettingsService _settings;
    private readonly IFirebaseAuth _auth;

    public FirestoreRestClient(ISettingsService settings, IFirebaseAuth auth)
    {
        _settings = settings;
        _auth = auth;
    }

    public async Task<IReadOnlyList<RemoteDoc>> QueryAsync(
        string collection, DateTime? minUpdatedAt, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            // REST StructuredQuery.from is a CollectionSelector[] — a bare
            // string collection id is rejected with INVALID_ARGUMENT.
            ["structuredQuery"] = new JsonObject
            {
                ["from"] = new JsonArray(new JsonObject { ["collectionId"] = collection }),
                ["orderBy"] = new JsonArray(new JsonObject
                {
                    ["field"] = new JsonObject { ["fieldPath"] = "updated_at" },
                    ["direction"] = "ASCENDING"
                })
            }
        };

        if (minUpdatedAt is DateTime since)
        {
            body["structuredQuery"]!["where"] = new JsonObject
            {
                ["fieldFilter"] = new JsonObject
                {
                    ["field"] = new JsonObject { ["fieldPath"] = "updated_at" },
                    ["op"] = "GREATER_THAN_OR_EQUAL",
                    ["value"] = FirestoreValues.ToFirestoreUtc(since)
                }
            };
        }

        // BaseAsync() already ends in /documents — the verb attaches to that
        // path segment (…/documents:runQuery), not a nested documents path.
        var url = $"{await BaseAsync()}:runQuery";
        var text = await SendAsync(url, body, ct);
        return ParseQueryResponse(text);
    }

    public async Task CommitAsync(IReadOnlyList<CloudWrite> writes, CancellationToken ct = default)
    {
        // Firestore allows at most 500 writes per commit.
        var baseUri = await BaseAsync();
        foreach (var chunk in writes.Chunk(500))
        {
            var arr = new JsonArray();
            foreach (var write in chunk)
            {
                arr.Add(new JsonObject
                {
                    ["update"] = new JsonObject
                    {
                        ["name"] = await DocNameAsync(write.Collection, write.DocId),
                        ["fields"] = ToFieldObject(write.Fields)
                    }
                });
            }

            var body = new JsonObject { ["writes"] = arr };
            await SendAsync($"{baseUri}:commit", body, ct);
        }
    }

    /// <summary>
    /// Document resource name for write payloads: <c>projects/…/documents/…</c>
    /// (the wire format rejects absolute https URLs in <c>update.name</c>).
    /// </summary>
    private async Task<string> DocNameAsync(string collection, string docId)
    {
        var settings = await _settings.LoadAsync();
        return $"projects/{settings.FirebaseProjectId}/databases/(default)/documents/{collection}/{docId}";
    }

    private async Task<string> BaseAsync()
    {
        var settings = await _settings.LoadAsync();
        if (string.IsNullOrWhiteSpace(settings.FirebaseProjectId))
        {
            throw new InvalidOperationException("Firebase project id is not configured.");
        }

        return $"https://firestore.googleapis.com/v1/projects/{settings.FirebaseProjectId}"
             + "/databases/(default)/documents";
    }

    private async Task<string> SendAsync(string url, JsonObject body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await _auth.GetIdTokenAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new("Bearer", token);
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                _auth.Invalidate(); // stale token — refresh and retry once
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Firestore request failed ({(int)response.StatusCode} {response.StatusCode}): "
                    + (text.Length <= 400 ? text : text[..400]));
            }

            return text;
        }
    }

    private static JsonObject ToFieldObject(IReadOnlyDictionary<string, object?> fields)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in fields)
        {
            obj[key] = FirestoreValues.ToFirestore(value);
        }

        return obj;
    }

    /// <summary>
    /// runQuery streams RunQueryResponse objects. Depending on Accept
    /// negotiation the body arrives as one JSON array, newline-delimited
    /// objects, or concatenated objects — all three are handled.
    /// </summary>
    internal static IReadOnlyList<RemoteDoc> ParseQueryResponse(string text)
    {
        var results = new List<RemoteDoc>();
        foreach (var node in EnumerateNodes(text))
        {
            if (node is not JsonObject obj
                || !obj.TryGetPropertyValue("document", out var docNode)
                || docNode is not JsonObject doc)
            {
                continue; // readTime-only end marker
            }

            var name = doc["name"]?.GetValue<string>() ?? "";
            var id = Uri.UnescapeDataString(name[(name.LastIndexOf('/') + 1)..]);

            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (doc["fields"] is JsonObject fieldObj)
            {
                foreach (var (key, value) in fieldObj)
                {
                    fields[key] = value is null ? null : FirestoreValues.FromFirestore(value);
                }
            }

            var updatedAt = fields.TryGetValue("updated_at", out var ua) && ua is DateTime dt
                ? dt
                : FirestoreValues.ParseTimestamp(
                    doc["updateTime"]?.GetValue<string>()
                    ?? doc["createTime"]?.GetValue<string>()
                    ?? throw new InvalidOperationException($"Doc {id} has no timestamp."));

            var isDeleted = fields.TryGetValue("is_deleted", out var del) && del is true;

            results.Add(new RemoteDoc(id, updatedAt, isDeleted, fields));
        }

        return results;
    }

    private static IEnumerable<JsonNode> EnumerateNodes(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        // 1. A single JSON array (most common for application/json).
        List<JsonNode>? arrayItems = null;
        try
        {
            if (JsonNode.Parse(text) is JsonArray arr)
            {
                arrayItems = arr.Where(n => n is not null).ToList()!;
            }
        }
        catch (JsonException)
        {
            // fall through to streaming forms
        }

        if (arrayItems is not null)
        {
            foreach (var item in arrayItems)
            {
                yield return item;
            }

            yield break;
        }

        // 2. Newline-delimited JSON objects.
        using (var reader = new StringReader(text))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var trimmed = line.Trim().TrimEnd(',');
                if (trimmed is "{" or "[" or "]" or "")
                {
                    continue;
                }

                JsonNode? node = null;
                try
                {
                    node = JsonNode.Parse(trimmed);
                }
                catch (JsonException)
                {
                    // partial line — keep reading
                }

                if (node is JsonObject obj)
                {
                    yield return obj;
                }
            }
        }
    }
}
