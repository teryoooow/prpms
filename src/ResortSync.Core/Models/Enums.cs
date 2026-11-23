namespace ResortSync.Models;

/// <summary>How the resort property is booked.</summary>
public enum OvernightStayType
{
    /// <summary>Whole-day tour (day use only).</summary>
    DayTour,

    /// <summary>Night-only tour.</summary>
    NightTour,

    /// <summary>Standard overnight stay (check-in to check-out).</summary>
    Overnight,

    /// <summary>Exclusive 22-hour block (full property buy-out).</summary>
    TwentyTwoHourExclusive
}

/// <summary>Lifecycle status of a booking.</summary>
public enum BookingStatus
{
    /// <summary>Created; awaiting the 24-hour downpayment window.</summary>
    PendingDeposit,

    /// <summary>Downpayment received; stay is reserved.</summary>
    Confirmed,

    /// <summary>Total amount fully settled.</summary>
    FullyPaid,

    /// <summary>Guest has checked out; booking closed.</summary>
    Completed,

    /// <summary>Cancelled by guest or staff.</summary>
    Cancelled,

    /// <summary>Downpayment window elapsed without payment (set by background worker).</summary>
    Expired
}

/// <summary>Purpose of a payment transaction.</summary>
public enum PaymentType
{
    /// <summary>Initial reservation fee (typically 50% of total).</summary>
    Downpayment,

    /// <summary>Payment(s) toward the outstanding balance.</summary>
    FinalBalance,

    /// <summary>Refundable hold; does not count toward the booking total.</summary>
    SecurityDeposit,

    /// <summary>Money returned to the guest.</summary>
    Refund
}

/// <summary>Supported payment channels.</summary>
public enum PaymentMethod
{
    GCash,
    Maya,
    BankTransfer,
    Cash,
    CreditCard
}
