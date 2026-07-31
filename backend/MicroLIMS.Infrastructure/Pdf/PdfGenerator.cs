namespace MicroLIMS.Infrastructure.Pdf;

// Produces a real, valid PDF via SimplePdfWriter. Kept behind
// IPdfGenerator so ReportService never depends on the concrete PDF
// approach directly - swap in QuestPDF/iText here later without
// touching any calling code if richer layout is needed.
public class PdfGenerator : IPdfGenerator
{
    public Task<byte[]> GenerateAsync(string templateName, Dictionary<string, object> data)
    {
        var lines = data.Select(kv => $"{kv.Key}: {kv.Value}");
        var bytes = SimplePdfWriter.WriteTextDocument(templateName, lines);
        return Task.FromResult(bytes);
    }

    // Preferred entry point for report generation - takes pre-formatted
    // lines rather than a flat dictionary, so ReportService controls layout.
    public Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines)
    {
        return Task.FromResult(SimplePdfWriter.WriteTextDocument(title, lines));
    }
}
