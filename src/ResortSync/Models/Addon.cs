using System.ComponentModel.DataAnnotations;

namespace ResortSync.Models;

/// <summary>Rentable extra the guest can add to a booking (Videoke, Gas Stove, Towels, ...).</summary>
public class Addon
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Price per unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Soft-delete flag so historical bookings keep their catalog reference.</summary>
    public bool IsActive { get; set; } = true;

    public List<BookingAddon> BookingAddons { get; set; } = new();
}
