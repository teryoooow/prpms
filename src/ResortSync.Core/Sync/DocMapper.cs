using ResortSync.Models;
using ResortSync.Services;

namespace ResortSync.Sync;

/// <summary>
/// Maps syncable entities to/from Firestore document fields.
/// Conventions: snake_case keys; <c>updated_at</c> is always UTC;
/// business dates are wall-time instants (converted on both hops);
/// foreign keys travel as <c>*_cloud_id</c> strings and are resolved to local
/// integer ids by <see cref="SyncEngine"/>.
/// </summary>
internal static class DocMapper
{
    // ── Push (entity → fields) ───────────────────────────────────────────────

    public static Dictionary<string, object?> Fields(Guest g) => new()
    {
        ["full_name"] = g.FullName,
        ["phone"] = g.Phone,
        ["email"] = g.Email,
        ["id_type"] = g.IdType,
        ["id_number"] = g.IdNumber,
        ["notes"] = g.Notes,
        ["updated_at"] = FirestoreValues.ToFirestoreUtc(g.UpdatedAt)
    };

    public static Dictionary<string, object?> Fields(Booking b, string guestCloudId) => new()
    {
        ["guest_cloud_id"] = guestCloudId,
        ["check_in"] = b.CheckInDate,
        ["check_out"] = b.CheckOutDate,
        ["stay_type"] = b.OvernightStayType.ToString(),
        ["base_guests"] = b.BaseGuests,
        ["additional_guests"] = b.AdditionalGuests,
        ["price_per_extra_guest"] = b.PricePerExtraGuest,
        ["base_amount"] = b.BaseAmount,
        ["addons_amount"] = b.AddonsAmount,
        ["total_amount"] = b.TotalAmount,
        ["deposit_required"] = b.DepositRequired,
        ["remaining_balance"] = b.RemainingBalance,
        ["status"] = b.Status.ToString(),
        ["created_at"] = b.CreatedAt,
        ["deposit_deadline"] = b.DepositDeadline,
        ["completed_at"] = b.CompletedAt,
        ["notes"] = b.Notes,
        ["updated_at"] = FirestoreValues.ToFirestoreUtc(b.UpdatedAt)
    };

    public static Dictionary<string, object?> Fields(PaymentTransaction p, string bookingCloudId) => new()
    {
        ["booking_cloud_id"] = bookingCloudId,
        ["payment_date"] = p.PaymentDate,
        ["amount_paid"] = p.AmountPaid,
        ["payment_type"] = p.PaymentType.ToString(),
        ["payment_method"] = p.PaymentMethod.ToString(),
        ["reference_number"] = p.ReferenceNumber,
        ["notes"] = p.Notes,
        ["updated_at"] = FirestoreValues.ToFirestoreUtc(p.UpdatedAt)
    };

    public static Dictionary<string, object?> Fields(Addon a) => new()
    {
        ["name"] = a.Name,
        ["unit_price"] = a.UnitPrice,
        ["is_active"] = a.IsActive,
        ["updated_at"] = FirestoreValues.ToFirestoreUtc(a.UpdatedAt)
    };

    public static Dictionary<string, object?> Fields(BookingAddon x, string bookingCloudId, string addonCloudId) => new()
    {
        ["booking_cloud_id"] = bookingCloudId,
        ["addon_cloud_id"] = addonCloudId,
        ["quantity"] = x.Quantity,
        ["unit_price"] = x.UnitPrice,
        ["updated_at"] = FirestoreValues.ToFirestoreUtc(x.UpdatedAt)
    };

    public static Dictionary<string, object?> Fields(SettingsModel s) => new()
    {
        ["resort_name"] = s.ResortName,
        ["downpayment_percent"] = s.DownpaymentPercent,
        ["default_extra_guest_rate"] = s.DefaultExtraGuestRate,
        ["default_base_guests"] = s.DefaultBaseGuests,
        ["updated_at"] = FirestoreValues.ToFirestoreUtc(s.SettingsUpdatedAt)
    };

    // ── Pull (fields → entity) ───────────────────────────────────────────────

    public static Guest ToGuest(RemoteDoc doc) => new()
    {
        CloudId = doc.Id,
        FullName = Str(doc, "full_name"),
        Phone = Str(doc, "phone"),
        Email = Str(doc, "email"),
        IdType = Str(doc, "id_type"),
        IdNumber = Str(doc, "id_number"),
        Notes = OptStr(doc, "notes"),
        UpdatedAt = doc.UpdatedAt
    };

    public static Booking ToBooking(RemoteDoc doc) => new()
    {
        CloudId = doc.Id,
        CheckInDate = Wall(doc, "check_in"),
        CheckOutDate = Wall(doc, "check_out"),
        OvernightStayType = Enum.Parse<OvernightStayType>(Str(doc, "stay_type")),
        BaseGuests = Int(doc, "base_guests"),
        AdditionalGuests = Int(doc, "additional_guests"),
        PricePerExtraGuest = Dec(doc, "price_per_extra_guest"),
        BaseAmount = Dec(doc, "base_amount"),
        AddonsAmount = Dec(doc, "addons_amount"),
        TotalAmount = Dec(doc, "total_amount"),
        DepositRequired = Dec(doc, "deposit_required"),
        RemainingBalance = Dec(doc, "remaining_balance"),
        Status = Enum.Parse<BookingStatus>(Str(doc, "status")),
        CreatedAt = Wall(doc, "created_at"),
        DepositDeadline = Wall(doc, "deposit_deadline"),
        CompletedAt = doc.Fields.TryGetValue("completed_at", out var ca) && ca is DateTime cdt
            ? cdt.ToLocalTime()
            : null,
        Notes = OptStr(doc, "notes"),
        UpdatedAt = doc.UpdatedAt
    };

    public static PaymentTransaction ToPayment(RemoteDoc doc) => new()
    {
        CloudId = doc.Id,
        PaymentDate = Wall(doc, "payment_date"),
        AmountPaid = Dec(doc, "amount_paid"),
        PaymentType = Enum.Parse<PaymentType>(Str(doc, "payment_type")),
        PaymentMethod = Enum.Parse<PaymentMethod>(Str(doc, "payment_method")),
        ReferenceNumber = OptStr(doc, "reference_number"),
        Notes = OptStr(doc, "notes"),
        UpdatedAt = doc.UpdatedAt
    };

    public static Addon ToAddon(RemoteDoc doc) => new()
    {
        CloudId = doc.Id,
        Name = Str(doc, "name"),
        UnitPrice = Dec(doc, "unit_price"),
        IsActive = !doc.Fields.TryGetValue("is_active", out var act) || act is true,
        UpdatedAt = doc.UpdatedAt
    };

    public static BookingAddon ToBookingAddon(RemoteDoc doc) => new()
    {
        CloudId = doc.Id,
        Quantity = Int(doc, "quantity"),
        UnitPrice = Dec(doc, "unit_price"),
        UpdatedAt = doc.UpdatedAt
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Str(RemoteDoc doc, string key)
        => doc.Fields.TryGetValue(key, out var v) && v is string s ? s : string.Empty;

    private static string? OptStr(RemoteDoc doc, string key)
        => doc.Fields.TryGetValue(key, out var v) ? v as string : null;

    private static int Int(RemoteDoc doc, string key)
        => doc.Fields.TryGetValue(key, out var v)
            ? v switch { long l => (int)l, int i => i, double d => (int)d, _ => 0 }
            : 0;

    private static decimal Dec(RemoteDoc doc, string key)
        => doc.Fields.TryGetValue(key, out var v)
            ? v switch
            {
                double d => (decimal)d,
                decimal m => m,
                long l => l,
                _ => 0m
            }
            : 0m;

    private static DateTime Wall(RemoteDoc doc, string key)
        => doc.Fields.TryGetValue(key, out var v) && v is DateTime utc
            ? utc.ToLocalTime()
            : default;
}
