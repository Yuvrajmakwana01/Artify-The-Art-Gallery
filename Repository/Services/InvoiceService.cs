using Repository.Models;
using System.Globalization;
using System.Text;

namespace API.Services;

public class InvoiceService
{
    public byte[] GenerateInvoice(t_OrderDetail order)
    {
        var lines = BuildInvoiceLines(order);
        return SimplePdfBuilder.CreateTextPdf(lines);
    }

    private static List<string> BuildInvoiceLines(t_OrderDetail order)
    {
        var lines = new List<string>
        {
            "ARTIFY - THE ART GALLERY",
            "INVOICE",
            string.Empty,
            $"Invoice Number: ART-{order.OrderId:D6}",
            $"Order ID: #{order.OrderId}",
            $"Purchase Date: {order.OrderDate:dd MMM yyyy, hh:mm tt}",
            $"Payment Method: {order.PaymentMethod}",
            $"Payment Status: {order.PaymentStatus}",
            string.Empty,
            "BILLED TO",
            order.BuyerName,
            order.BuyerEmail
        };

        if (!string.IsNullOrWhiteSpace(order.BuyerPhone))
        {
            lines.Add(order.BuyerPhone);
        }

        lines.Add(string.Empty);
        lines.Add("PURCHASED ARTWORKS");

        foreach (var item in order.Items)
        {
            lines.Add($"- {item.Title} | Artist: {item.ArtistName} | Price: {FormatCurrency(item.Price)}");
        }

        lines.Add(string.Empty);
        lines.Add($"Subtotal: {FormatCurrency(order.TotalAmount)}");
        lines.Add($"Platform Commission (20%): {FormatCurrency(order.CommissionDeducted)}");
        lines.Add($"Artist Payout: {FormatCurrency(order.ArtistPayout)}");
        lines.Add("Tax: Included");
        lines.Add($"TOTAL CHARGED: {FormatCurrency(order.TotalAmount)}");
        lines.Add(string.Empty);
        lines.Add("support@artify.in | www.artify.in");
        lines.Add("This is a computer-generated invoice.");

        return lines;
    }

    private static string FormatCurrency(decimal value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"INR {value:N2}");
    }

    private static class SimplePdfBuilder
    {
        public static byte[] CreateTextPdf(IReadOnlyList<string> lines)
        {
            var content = BuildContentStream(lines);

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            var offsets = new List<long>();

            writer.WriteLine("%PDF-1.4");
            WriteObject(writer, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
            WriteObject(writer, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            WriteObject(writer, offsets, 3,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 5 0 R /Resources << /Font << /F1 4 0 R >> >> >>");
            WriteObject(writer, offsets, 4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            WriteObject(writer, offsets, 5,
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");

            writer.Flush();
            var xrefStart = stream.Position;
            writer.WriteLine("xref");
            writer.WriteLine($"0 {offsets.Count + 1}");
            writer.WriteLine("0000000000 65535 f ");

            foreach (var offset in offsets)
            {
                writer.WriteLine($"{offset:D10} 00000 n ");
            }

            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(xrefStart);
            writer.Write("%%EOF");
            writer.Flush();

            return stream.ToArray();
        }

        private static void WriteObject(StreamWriter writer, List<long> offsets, int objectNumber, string body)
        {
            writer.Flush();
            offsets.Add(writer.BaseStream.Position);
            writer.WriteLine($"{objectNumber} 0 obj");
            writer.WriteLine(body);
            writer.WriteLine("endobj");
        }

        private static string BuildContentStream(IReadOnlyList<string> lines)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BT");
            sb.AppendLine("/F1 12 Tf");

            var y = 800;
            foreach (var rawLine in lines)
            {
                sb.AppendLine($"1 0 0 1 50 {y} Tm ({EscapePdfText(rawLine)}) Tj");
                y -= 18;
                if (y < 50)
                {
                    break;
                }
            }

            sb.AppendLine("ET");
            return sb.ToString();
        }

        private static string EscapePdfText(string input)
        {
            return input
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }
}
