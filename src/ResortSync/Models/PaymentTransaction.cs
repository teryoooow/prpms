using System.ComponentModel.DataAnnotations;

namespace ResortSync.Models;

/// <summary>
/// A single money movement against a booking. Multiple partial payments are allowed;
/// each row is an immutable receipt-grade record (method + reference number).
/// </summary>
public class PaymentTransaction
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public DateTime PaymentDate { get; set; }
    public decimal AmountPaid { get; set; }

    public PaymentType PaymentType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>GCash / Maya / bank trace number (optional for cash).</summary>
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
