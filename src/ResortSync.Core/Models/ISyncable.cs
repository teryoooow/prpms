namespace ResortSync.Models;

/// <summary>
/// A row that participates in cloud sync. <see cref="CloudId"/> is the globally
/// unique identity used as the Firestore document id (local integer PKs stay
/// device-local and may collide across devices — CloudId never does).
/// </summary>
public interface ISyncable
{
    /// <summary>32-hex GUID assigned at insert (or on first sync backfill). Null only for rows created before sync existed that have not been pushed yet.</summary>
    string? CloudId { get; set; }

    /// <summary>
    /// UTC timestamp of the last write on any device; drives last-write-wins
    /// conflict resolution and the push/pull cursors. Stored as UTC — the
    /// SQLite provider returns <see cref="DateTimeKind.Unspecified"/>, which
    /// must be treated as UTC when compared.
    /// </summary>
    DateTime UpdatedAt { get; set; }
}

/// <summary>Maps syncable entity types to their Firestore collection ids.</summary>
public static class SyncCollections
{
    public const string Guests = "guests";
    public const string Bookings = "bookings";
    public const string Payments = "payments";
    public const string Addons = "addons";
    public const string BookingAddons = "booking_addons";
    public const string Settings = "settings";

    /// <summary>Ordered parents-before-children so remote FKs always resolve on apply.</summary>
    public static readonly string[] PullOrder =
    {
        Guests, Addons, Bookings, BookingAddons, Payments
    };

    public static string For(Type entityType) => entityType.Name switch
    {
        nameof(Guest) => Guests,
        nameof(Booking) => Bookings,
        nameof(PaymentTransaction) => Payments,
        nameof(Addon) => Addons,
        nameof(BookingAddon) => BookingAddons,
        _ => throw new ArgumentException($"No sync collection for {entityType.Name}.")
    };
}
