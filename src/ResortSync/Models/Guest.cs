using System.ComponentModel.DataAnnotations;

namespace ResortSync.Models;

/// <summary>
/// A resort guest. Guests are reused across bookings via phone / ID number lookup.
/// </summary>
public class Guest
{
    public int Id { get; set; }

    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Kind of government ID presented (e.g. National ID, Driver's License, Passport).</summary>
    [MaxLength(60)]
    public string IdType { get; set; } = string.Empty;

    [MaxLength(80)]
    public string IdNumber { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Bookings belonging to this guest.</summary>
    public List<Booking> Bookings { get; set; } = new();

    /// <summary>Display name used in autocomplete lists.</summary>
    public string DisplayName => $"{FullName} · {Phone}";
}
