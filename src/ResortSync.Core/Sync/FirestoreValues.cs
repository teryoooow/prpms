using System.Globalization;
using System.Text.Json.Nodes;

namespace ResortSync.Sync;

/// <summary>
/// Converts CLR values to/from Firestore REST typed values
/// ({ "stringValue": ... }, { "timestampValue": ... }, ...).
/// Money travels as doubleValue (exact for 2-decimal amounts well beyond
/// any resort's totals); DateTime travels as RFC3339 UTC timestamps.
/// </summary>
public static class FirestoreValues
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static JsonNode ToFirestore(object? value) => value switch
    {
        null => new JsonObject { ["nullValue"] = null },
        JsonNode node => node, // pre-formatted (e.g. UTC sync stamps)
        string s => new JsonObject { ["stringValue"] = s },
        bool b => new JsonObject { ["booleanValue"] = b },
        int i => new JsonObject { ["integerValue"] = i.ToString(CultureInfo.InvariantCulture) },
        long l => new JsonObject { ["integerValue"] = l.ToString(CultureInfo.InvariantCulture) },
        decimal m => new JsonObject { ["doubleValue"] = (double)m },
        double d => new JsonObject { ["doubleValue"] = d },
        DateTime dt => ToFirestoreWall(dt),
        _ => throw new NotSupportedException($"Cannot serialize {value.GetType().Name} to Firestore.")
    };

    /// <summary>
    /// Business timestamps (check-in, deadlines, payment dates): the ticks are
    /// LOCAL WALL TIME and must survive as wall time on any device in the same
    /// timezone. Unspecified kind (what SQLite returns) counts as wall.
    /// </summary>
    public static JsonNode ToFirestoreWall(DateTime wall)
    {
        var utc = wall.Kind switch
        {
            DateTimeKind.Utc => wall,
            DateTimeKind.Local => wall.ToUniversalTime(),
            _ => DateTime.SpecifyKind(wall, DateTimeKind.Local).ToUniversalTime()
        };
        return new JsonObject { ["timestampValue"] = FormatUtc(utc) };
    }

    /// <summary>
    /// Sync timestamps (<c>updated_at</c>, tombstone times): always UTC ticks —
    /// SQLite loses the Kind, so NEVER let these pass through local conversion.
    /// </summary>
    public static JsonNode ToFirestoreUtc(DateTime utcTicks)
        => new JsonObject { ["timestampValue"] = FormatUtc(utcTicks) };

    /// <summary>
    /// Drops sub-millisecond ticks. The wire format carries millisecond
    /// precision — stamps truncated after the fact would make every
    /// watermark comparison (UpdatedAt > PushedThrough) re-fire forever.
    /// </summary>
    public static DateTime TruncateMs(DateTime utc)
        => new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, utc.Kind);
    /// <summary>UTC RFC3339 with millisecond precision (Firestore's wire format).</summary>
    public static string FormatUtc(DateTime utcTicks)
        => DateTime.SpecifyKind(utcTicks, DateTimeKind.Utc)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static DateTime ParseTimestamp(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            .ToUniversalTime();

    /// <summary>Wire timestamp → local wall time (business date fields).</summary>
    public static DateTime ParseWallTimestamp(string value)
        => ParseTimestamp(value).ToLocalTime();

    /// <summary>Reads a Firestore typed value back into a CLR value.</summary>
    public static object? FromFirestore(JsonNode node)
    {
        var obj = (JsonObject)node;

        if (obj.TryGetPropertyValue("nullValue", out _)) return null;
        if (obj.TryGetPropertyValue("stringValue", out var s)) return s!.GetValue<string>();
        if (obj.TryGetPropertyValue("booleanValue", out var b)) return b!.GetValue<bool>();
        if (obj.TryGetPropertyValue("integerValue", out var i))
            return long.Parse(i!.GetValue<string>(), CultureInfo.InvariantCulture);
        if (obj.TryGetPropertyValue("doubleValue", out var d)) return d!.GetValue<double>();
        if (obj.TryGetPropertyValue("timestampValue", out var t))
            return ParseTimestamp(t!.GetValue<string>());

        throw new NotSupportedException(
            $"Cannot deserialize Firestore value kind: {string.Join(",", obj.Select(p => p.Key))}");
    }
}

/// <summary>A document returned by a pull query.</summary>
public sealed record RemoteDoc(
    string Id,
    DateTime UpdatedAt,
    bool IsDeleted,
    IReadOnlyDictionary<string, object?> Fields);

/// <summary>An upsert (Firestore "update" write creates the doc when absent).</summary>
public sealed record CloudWrite(
    string Collection,
    string DocId,
    IReadOnlyDictionary<string, object?> Fields);
