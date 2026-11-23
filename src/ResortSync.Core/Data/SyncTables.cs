namespace ResortSync.Data;

/// <summary>
/// Record of a locally hard-deleted syncable row (voided payment, rebuilt
/// booking-addon line). Pushed to the cloud as a document delete so the removal
/// propagates to other devices; dropped after the cloud acknowledges it.
/// </summary>
public class Tombstone
{
    public int Id { get; set; }

    /// <summary>CloudId of the deleted row.</summary>
    public string CloudId { get; set; } = string.Empty;

    /// <summary>Firestore collection the row belonged to.</summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>UTC time of the local delete; wins/loses against remote edits via last-write-wins.</summary>
    public DateTime DeletedAt { get; set; }
}

/// <summary>
/// Per-collection sync watermarks: <see cref="Cursor"/> is the highest
/// <c>updated_at</c> already pulled; <see cref="PushedThrough"/> is the highest
/// <c>updated_at</c> already pushed. Both use inclusive <c>&gt;=</c> re-reads,
/// so boundary rows are re-fetched/re-sent harmlessly (apply and upsert are
/// idempotent).
/// </summary>
public class SyncState
{
    /// <summary>Firestore collection id (see <see cref="Models.SyncCollections"/>).</summary>
    public string Collection { get; set; } = string.Empty;

    public string Cursor { get; set; } = string.Empty;

    public string PushedThrough { get; set; } = string.Empty;

    public DateTime? LastSyncAt { get; set; }
}
