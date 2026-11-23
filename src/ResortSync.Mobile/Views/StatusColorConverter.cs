using ResortSync.Models;

namespace ResortSync.Mobile.Views;

/// <summary>Maps a BookingStatus to its badge/background color (mirrors the desktop StatusToBrush).</summary>
public class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value switch
        {
            BookingStatus.PendingDeposit => Colors.Orange,
            BookingStatus.Confirmed => Color.FromArgb("#1976D2"),
            BookingStatus.FullyPaid => Color.FromArgb("#2E7D32"),
            BookingStatus.Completed => Color.FromArgb("#546E7A"),
            BookingStatus.Cancelled => Color.FromArgb("#C62828"),
            BookingStatus.Expired => Color.FromArgb("#757575"),
            _ => Colors.Black
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
