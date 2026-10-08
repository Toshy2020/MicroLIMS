using MicroLIMS.Application.Services.InstrumentImport;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MicroLIMS.Infrastructure.InstrumentImport;

// PDF with a text layer -> its words, grouped into lines (exact, no OCR).
// Scanned PDF (no text, an embedded page image) or a photo -> OCR.
public sealed class ReportTextReader(TesseractOcr ocr) : IReportTextReader
{
    public async Task<ReportText> ReadAsync(byte[] content, string fileName, CancellationToken ct = default)
    {
        if (!IsPdf(content))
            return await ocr.ReadPageAsync(content, ct);

        using var pdf = PdfDocument.Open(content);
        var page = pdf.GetPage(1);
        var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
        if (words.Count > 0)
            return new ReportText(ReportTextSource.TextLayer, GroupIntoLines(words), []);

        var image = page.GetImages().OrderByDescending(i => i.Bounds.Width * i.Bounds.Height).FirstOrDefault()
            ?? throw new InvalidOperationException($"'{fileName}' has neither text nor a page image to read.");
        var bytes = image.TryGetPng(out var png) ? png : image.RawBytes.ToArray();
        return await ocr.ReadPageAsync(bytes, ct);
    }

    private static bool IsPdf(byte[] b) => b.Length > 4 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F';

    // Words whose vertical centres are within half a word height share a line.
    private static List<ReportLine> GroupIntoLines(List<UglyToad.PdfPig.Content.Word> words)
    {
        var lines = new List<List<UglyToad.PdfPig.Content.Word>>();
        foreach (var w in words.OrderByDescending(w => w.BoundingBox.Centroid.Y))
        {
            var line = lines.LastOrDefault();
            if (line is not null && Math.Abs(line[0].BoundingBox.Centroid.Y - w.BoundingBox.Centroid.Y)
                < Math.Max(line[0].BoundingBox.Height, w.BoundingBox.Height) / 2)
                line.Add(w);
            else
                lines.Add([w]);
        }
        return lines
            .Select(l => new ReportLine(string.Join(' ', l.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)), null))
            .ToList();
    }
}
