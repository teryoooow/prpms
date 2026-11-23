namespace ResortSync.Sync;

/// <summary>
/// Backend-facing Firestore operations. The real implementation speaks the
/// Firestore REST API; tests substitute an in-memory fake so the full sync
/// engine runs against two local databases without any network.
/// </summary>
public interface IFirestoreTransport
{
    /// <summary>
    /// All documents in <paramref name="collection"/> whose <c>updated_at</c> is
    /// &gt;= <paramref name="minUpdatedAt"/> (inclusive), ordered by it.
    /// <paramref name="minUpdatedAt"/> null = everything.
    /// </summary>
    Task<IReadOnlyList<RemoteDoc>> QueryAsync(
        string collection, DateTime? minUpdatedAt, CancellationToken ct = default);

    /// <summary>Upserts the given documents (Firestore commits are atomic per call).</summary>
    Task CommitAsync(IReadOnlyList<CloudWrite> writes, CancellationToken ct = default);
}

/// <summary>
/// Supplies Firebase ID tokens (sign-in + silent refresh). The engine calls
/// <see cref="GetIdTokenAsync"/> before every network cycle.
/// </summary>
public interface IFirebaseAuth
{
    /// <summary>A valid ID token, signing in or refreshing as needed.</summary>
    Task<string> GetIdTokenAsync(CancellationToken ct = default);

    /// <summary>Forgets cached tokens (forces full sign-in next call).</summary>
    void Invalidate();
}
