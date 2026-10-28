using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResortSync.Models;

/// <summary>
/// A stay / tour reservation. Money amounts are snapshot-computed at save time so that
/// later catalog price changes never rewrite historical bookings.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    // ── Guest ────────────────────────────────────────────────────────────────
    public int GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    // ── Stay window ──────────────────────────────────────────────────────────
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public OvernightStayType OvernightStayType { get; set; }

    // ── Occupancy & pricing inputs ───────────────────────────────────────────
    /// <summary>Included guests covered by <see cref="BaseAmount"/>.</summary>
    public int BaseGuests { get; set; } = 2;

    /// <summary>Guests beyond <see cref="BaseGuests"/>; charged at <see cref="PricePerExtraGuest"/>.</summary>
    public int AdditionalGuests { get; set; }

    public decimal PricePerExtraGuest { get; set; }

    // ── Money ────────────────────────────────────────────────────────────────
    public decimal BaseAmount { get; set; }
    public decimal AddonsAmount { get; set; }

    /// <summary>BaseAmount + (AdditionalGuests × PricePerExtraGuest) + AddonsAmount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>50% of <see cref="TotalAmount"/> by default (configurable in Settings).</summary>
    public decimal DepositRequired { get; set; }

    /// <summary>TotalAmount minus Downpayment/FinalBalance payments (security deposits excluded).</summary>
    public decimal RemainingBalance { get; set; }

    // ── Status & deadlines ───────────────────────────────────────────────────
    public BookingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Auto-set to 24 hours after creation; drives the auto-expiry worker.</summary>
    public DateTime DepositDeadline { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public List<PaymentTransaction> Payments { get; set; } = new();
    public List<BookingAddon> BookingAddons { get; set; } = new();

    // ── Computed helpers (not persisted) ─────────────────────────────────────

    /// <summary>Sum of Downpayment + FinalBalance payments already recorded.</summary>
    [NotMapped]
    public decimal PaidAmount => Payments
        .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
        .Sum(p => p.AmountPaid);

    /// <summary>True when a PendingDeposit booking has passed its deadline.</summary>
    [NotMapped]
    public bool IsDepositOverdue => Status == BookingStatus.PendingDeposit
                                    && DepositDeadline < DateTime.Now;

    /// <summary>True when the deadline is within the next few hours (dashboard alert).</summary>
    [NotMapped]
    public bool IsDepositExpiringSoon => Status == BookingStatus.PendingDeposit
                                         && DepositDeadline >= DateTime.Now
                                         && DepositDeadline <= DateTime.Now.AddHours(4);

    /// <summary>Friendly multi-day label for list views.</summary>
    [NotMapped]
    public string StayLabel => OvernightStayType switch
    {
        OvernightStayType.DayTour => "Day Tour",
        OvernightStayType.NightTour => "Night Tour",
        OvernightStayType.TwentyTwoHourExclusive => "22-Hour Exclusive",
        _ => $"{CheckInDate:MMM dd} – {CheckOutDate:MMM dd}"
    };
}
