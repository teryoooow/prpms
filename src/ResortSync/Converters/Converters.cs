using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ResortSync.Models;

namespace ResortSync.Converters;

/// <summary>Maps booking status → semantic color used by badges and status text.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush Pending = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00)); // amber
    private static readonly Brush Confirmed = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)); // blue
    private static readonly Brush Paid = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));        // green
    private static readonly Brush Completed = new SolidColorBrush(Color.FromRgb(0x6D, 0x4C, 0x41));   // brown
    private static readonly Brush Cancelled = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));   // grey
    private static readonly Brush Expired = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));     // red

    static StatusToBrushConverter()
    {
        // Freeze so the brushes can be shared safely across threads.
        Pending.Freeze();
        Confirmed.Freeze();
        Paid.Freeze();
        Completed.Freeze();
        Cancelled.Freeze();
        Expired.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            BookingStatus.PendingDeposit => Pending,
            BookingStatus.Confirmed => Confirmed,
            BookingStatus.FullyPaid => Paid,
            BookingStatus.Completed => Completed,
            BookingStatus.Cancelled => Cancelled,
            BookingStatus.Expired => Expired,
            _ => Brushes.Gray
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Returns the opposite bool (for IsEnabled-style targets).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false;
}

/// <summary>Visible when the bound bool is <c>false</c> (Visibility-returning inverse).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

/// <summary>Visible when the bound collection is empty (empty-state placeholder).</summary>
public sealed class IsEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isEmpty = value is System.Collections.IEnumerable enumerable
            && !enumerable.Cast<object>().Any();
        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Currency text without needing StringFormat in every binding: → "₱1,234.56".</summary>
public sealed class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            decimal d => $"₱{d:N2}",
            double d => $"₱{d:N2}",
            int i => $"₱{i:N2}",
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Friendly deadline label for dashboard alerts:
/// "EXPIRED (2h ago)" / "23h 59m left".
/// </summary>
public sealed class DeadlineLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime deadline)
        {
            return string.Empty;
        }

        var delta = deadline - DateTime.Now;
        if (delta.TotalMinutes < 0)
        {
            var ago = delta.Duration();
            return $"EXPIRED ({Format(ago)} ago)";
        }

        return $"{Format(delta)} left";

        static string Format(TimeSpan span)
            => span.TotalDays >= 1
                ? $"{(int)span.TotalDays}d {span.Hours}h"
                : span.TotalHours >= 1
                    ? $"{(int)span.TotalHours}h {span.Minutes}m"
                    : $"{Math.Max(1, span.Minutes)}m";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
