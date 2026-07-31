using System.Globalization;
using System.Text;

namespace MicroLIMS.Infrastructure.Pdf;

// A dependency-free minimal PDF writer. It produces a genuinely valid
// PDF (opens in any reader) from a list of text lines - not a
// full-fidelity layout engine, but real PDF bytes rather than a plain
// text file with a .pdf extension. Swap for QuestPDF/iText later if
// richer layout (tables, logos) is required; ReportService is already
// isolated behind IPdfGenerator so that swap is a one-file change.
public static class SimplePdfWriter
{
    public static byte[] WriteTextDocument(string title, IEnumerable<string> lines)
    {
        const int pageWidth = 612;   // Letter size in points
        const int pageHeight = 792;
        const int leftMargin = 50;
        const int topMargin = 740;
        const int lineHeight = 16;

        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 14 Tf");
        content.AppendLine($"{leftMargin} {topMargin} Td");
        content.AppendLine($"({Escape(title)}) Tj");
        content.AppendLine("/F1 10 Tf");

        var y = topMargin - lineHeight * 2;
        foreach (var line in lines)
        {
            if (y < 40) break; // single-page writer - truncate gracefully rather than corrupt the PDF
            content.AppendLine($"1 0 0 1 {leftMargin} {y} Tm");
            content.AppendLine($"({Escape(line)}) Tj");
            y -= lineHeight;
        }
        content.AppendLine("ET");

        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {contentBytes.Length} >>"
        };

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\n");
            if (i == 4) // the content stream object
            {
                Write("stream\n");
                ms.Write(contentBytes);
                Write("\nendstream\n");
            }
            Write("endobj\n");
        }

        var xrefStart = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++)
            Write($"{offsets[i]:D10} 00000 n \n");

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");

        return ms.ToArray();
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
