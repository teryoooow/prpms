using System.Text.Json.Nodes;

namespace ResortSync.Sync;

/// <summary>
/// In-memory Firestore stand-in shared by tests: one dictionary per
/// collection, guarded like a real backend (documents are plain upserts and
/// queries honor the <c>updated_at &gt;=</c> contract).
/// </summary>
public sealed class FakeFirestoreTransport : IFirestoreTransport
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, object?>>>
        _collections = new(StringComparer.Ordinal);

    public int Commits { get; private set; }

    public Task<IReadOnlyList<RemoteDoc>> QueryAsync(
        string collection, DateTime? minUpdatedAt, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var docs = _collections.TryGetValue(collection, out var rows)
                ? rows
                    .Select(ToRemote)
                    .Where(d => minUpdatedAt is null || d.UpdatedAt >= minUpdatedAt.Value)
                    .OrderBy(d => d.UpdatedAt)
                    .ToList()
                : new List<RemoteDoc>();

            return Task.FromResult<IReadOnlyList<RemoteDoc>>(docs);
        }
    }

    public Task CommitAsync(IReadOnlyList<CloudWrite> writes, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var write in writes)
            {
                if (!_collections.TryGetValue(write.Collection, out var rows))
                {
                    rows = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
                    _collections[write.Collection] = rows;
                }

                rows[write.DocId] = new Dictionary<string, object?>(write.Fields, StringComparer.Ordinal);
            }

            Commits++;
        }

        return Task.CompletedTask;
    }

    /// <summary>Test helper: seed a document directly (bypasses the engine).</summary>
    public void Seed(string collection, string docId, IReadOnlyDictionary<string, object?> fields)
    {
        lock (_gate)
        {
            if (!_collections.TryGetValue(collection, out var rows))
            {
                rows = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
                _collections[collection] = rows;
            }

            rows[docId] = new Dictionary<string, object?>(fields, StringComparer.Ordinal);
        }
    }

    /// <summary>Test helper: does a doc exist (after is_deleted markers included)?</summary>
    public bool Contains(string collection, string docId)
    {
        lock (_gate)
        {
            return _collections.TryGetValue(collection, out var rows) && rows.ContainsKey(docId);
        }
    }

    /// <summary>Test helper: raw stored fields (typed like the REST parser), or null.</summary>
    public IReadOnlyDictionary<string, object?>? Get(string collection, string docId)
    {
        lock (_gate)
        {
            if (!_collections.TryGetValue(collection, out var rows)
                || !rows.TryGetValue(docId, out var raw))
            {
                return null;
            }

            var fields = new Dictionary<string, object?>(raw.Count, StringComparer.Ordinal);
            foreach (var (key, value) in raw)
            {
                fields[key] = value is JsonObject node
                    ? FirestoreValues.FromFirestore(node)
                    : value;
            }

            return fields;
        }
    }

    private static RemoteDoc ToRemote(KeyValuePair<string, Dictionary<string, object?>> pair)
    {
        // Mirror FirestoreRestClient: DocMapper.Fields ships updated_at (and
        // only that) as a typed JsonNode — unwrap the same way the REST parser does.
        var raw = pair.Value;
        var fields = new Dictionary<string, object?>(raw.Count, StringComparer.Ordinal);
        foreach (var (key, value) in raw)
        {
            fields[key] = value is JsonObject node
                ? FirestoreValues.FromFirestore(node)
                : value;
        }

        var updatedAt = fields.TryGetValue("updated_at", out var ua) && ua is DateTime dt
            ? dt
            : default;
        var isDeleted = fields.TryGetValue("is_deleted", out var del) && del is true;
        return new RemoteDoc(pair.Key, updatedAt, isDeleted, fields);
    }
}

/// <summary>Test auth: hands out a fixed token, counts sign-ins.</summary>
public sealed class FakeFirebaseAuth : IFirebaseAuth
{
    public int SignInCount { get; private set; }

    public Task<string> GetIdTokenAsync(CancellationToken ct = default)
    {
        SignInCount++;
        return Task.FromResult("fake-token");
    }

    public void Invalidate() { }
}
