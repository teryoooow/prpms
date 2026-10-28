using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ResortSync.Models;

namespace ResortSync.Services;

/// <summary>Builds printable FlowDocument receipts and drives the print dialog.</summary>
public interface IReceiptService
{
    /// <summary>Creates the receipt document for a booking (must include Guest + Payments).</summary>
    FlowDocument BuildReceipt(Booking booking, string resortName);

    /// <summary>Opens the OS print dialog; returns false when cancelled.</summary>
    bool Print(FlowDocument document, string jobName = "PRPMS Receipt");

    /// <summary>Simple CSV export of the payment ledger for accounting hand-off.</summary>
    void ExportPaymentsCsv(IEnumerable<PaymentRow> rows, string filePath);
}

public sealed class ReceiptService : IReceiptService
{
    private static readonly Brush Ink = Brushes.Black;

    public FlowDocument BuildReceipt(Booking booking, string resortName)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            PagePadding = new Thickness(36),
            ColumnWidth = double.PositiveInfinity // single column print layout
        };

        // ── Header ────────────────────────────────────────────────────────────
        doc.Blocks.Add(Heading(resortName, 18, FontWeights.Bold));
        doc.Blocks.Add(Paragraph("Official Payment Receipt", 11, FontWeights.Medium));
        doc.Blocks.Add(Paragraph($"Generated: {DateTime.Now:MMM dd, yyyy hh:mm tt}", 9));
        doc.Blocks.Add(Divider());

        // ── Booking + guest details ───────────────────────────────────────────
        doc.Blocks.Add(Paragraph($"Booking #{booking.Id}   •   Status: {booking.Status}", 12, FontWeights.Bold));
        doc.Blocks.Add(Paragraph($"Guest: {booking.Guest?.FullName}"));
        doc.Blocks.Add(Paragraph($"Phone: {booking.Guest?.Phone}    Email: {booking.Guest?.Email}"));
        doc.Blocks.Add(Paragraph($"Stay: {booking.StayLabel}"));
        doc.Blocks.Add(Paragraph(
            $"Check-in: {booking.CheckInDate:MMM dd, yyyy}    " +
            $"Check-out: {booking.CheckOutDate:MMM dd, yyyy}"));
        doc.Blocks.Add(Paragraph($"Occupancy: {booking.BaseGuests} base + {booking.AdditionalGuests} extra"));
        doc.Blocks.Add(Divider());

        // ── Charge breakdown ──────────────────────────────────────────────────
        doc.Blocks.Add(Paragraph("CHARGES", 11, FontWeights.Bold));
        doc.Blocks.Add(KeyValue("Base amount", booking.BaseAmount));
        if (booking.AdditionalGuests > 0)
        {
            doc.Blocks.Add(KeyValue(
                $"Extra guests ({booking.AdditionalGuests} x {booking.PricePerExtraGuest:N2})",
                booking.AdditionalGuests * booking.PricePerExtraGuest));
        }

        foreach (var line in booking.BookingAddons)
        {
            doc.Blocks.Add(KeyValue(
                $"{line.Addon?.Name ?? "Addon"} x {line.Quantity}", line.LineTotal));
        }

        doc.Blocks.Add(KeyValue("TOTAL", booking.TotalAmount, bold: true));
        doc.Blocks.Add(KeyValue("Required downpayment", booking.DepositRequired));
        doc.Blocks.Add(Divider());

        // ── Payment ledger ────────────────────────────────────────────────────
        doc.Blocks.Add(Paragraph("PAYMENTS RECEIVED", 11, FontWeights.Bold));

        if (booking.Payments.Count == 0)
        {
            doc.Blocks.Add(Paragraph("No payments recorded yet.", 10));
        }
        else
        {
            foreach (var p in booking.Payments.OrderByDescending(p => p.PaymentDate))
            {
                doc.Blocks.Add(Paragraph(
                    $"{p.PaymentDate:MMM dd, yyyy hh:mm tt}  |  {p.PaymentType}  |  " +
                    $"{p.PaymentMethod}  |  {p.AmountPaid:N2}" +
                    (string.IsNullOrWhiteSpace(p.ReferenceNumber) ? "" : $"  |  Ref: {p.ReferenceNumber}"),
                    10));
            }

            doc.Blocks.Add(KeyValue("Total paid", booking.Payments
                .Where(p => p.PaymentType is PaymentType.Downpayment or PaymentType.FinalBalance)
                .Sum(p => p.AmountPaid), bold: true));
            doc.Blocks.Add(KeyValue("Remaining balance", booking.RemainingBalance, bold: true));
        }

        if (booking.Payments.Any(p => p.PaymentType == PaymentType.SecurityDeposit))
        {
            var held = booking.Payments
                .Where(p => p.PaymentType == PaymentType.SecurityDeposit)
                .Sum(p => p.AmountPaid);
            doc.Blocks.Add(KeyValue("Security deposit held (refundable)", held));
        }

        doc.Blocks.Add(Divider());
        doc.Blocks.Add(Paragraph(
            "This is a system-generated receipt. Downpayment window: 24 hours from booking.",
            9));
        doc.Blocks.Add(Paragraph("Thank you for staying with us!", 10, FontWeights.Medium));

        return doc;
    }

    public bool Print(FlowDocument document, string jobName = "PRPMS Receipt")
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        // Fit the document to the selected paper size.
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PagePadding = new Thickness(36);

        if (document is IDocumentPaginatorSource source)
        {
            dialog.PrintDocument(source.DocumentPaginator, jobName);
            return true;
        }

        return false;
    }

    public void ExportPaymentsCsv(IEnumerable<PaymentRow> rows, string filePath)
    {
        using var writer = new StreamWriter(filePath, append: false, System.Text.Encoding.UTF8);
        writer.WriteLine("PaymentId,BookingId,Guest,Date,Amount,Type,Method,Reference,Notes");

        foreach (var r in rows)
        {
            writer.WriteLine(string.Join(',',
                r.PaymentId,
                r.BookingId,
                Csv(r.GuestName),
                r.PaymentDate.ToString("yyyy-MM-dd HH:mm"),
                r.AmountPaid.ToString("0.00"),
                r.PaymentType,
                r.PaymentMethod,
                Csv(r.ReferenceNumber ?? ""),
                Csv(r.Notes ?? "")));
        }
    }

    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    // ── FlowDocument helpers ────────────────────────────────────────────────

    /// <summary>
    /// Horizontal rule. WPF has no Block-level Separator, so emulate one with a
    /// bottom-bordered paragraph.
    /// </summary>
    private static Paragraph Divider()
        => new()
        {
            Margin = new Thickness(0, 6, 0, 6),
            Padding = new Thickness(0, 0, 0, 4),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

    private static Paragraph Heading(string text, double size, FontWeight weight)
        => new(new Run(text))
        {
            FontSize = size,
            FontWeight = weight,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

    private static Paragraph Paragraph(string text, double size = 11, FontWeight? weight = null)
        => new(new Run(text))
        {
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 1, 0, 1)
        };

    private static Paragraph KeyValue(string label, decimal value, bool bold = false)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        var style = bold ? FontWeights.Bold : FontWeights.Normal;
        paragraph.Inlines.Add(new Run(label) { FontWeight = style });
        paragraph.Inlines.Add(new Run("\t" + $"₱{value:N2}") { FontWeight = style });
        return paragraph;
    }
}
