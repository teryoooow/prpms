namespace ResortSync.Models;

/// <summary>
/// Join table between <see cref="Booking"/> and <see cref="Addon"/> with quantity and a
/// unit-price snapshot, so later catalog price changes never mutate past receipts.
/// </summary>
public class BookingAddon
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int AddonId { get; set; }
    public Addon Addon { get; set; } = null!;

    public int Quantity { get; set; } = 1;

    /// <summary>Price per unit captured at booking time.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
