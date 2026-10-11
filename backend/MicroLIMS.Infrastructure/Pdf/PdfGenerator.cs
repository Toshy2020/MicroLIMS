using MicroLIMS.Application.Abstractions.Pdf;
namespace MicroLIMS.Infrastructure.Pdf;

// Kept behind IPdfGenerator so callers never depend on the concrete PDF
// approach directly - swap in QuestPDF/iText here later without touching
// any calling code if richer layout is needed.
public class PdfGenerator : IPdfGenerator
{
    public Task<byte[]> GenerateReportAsync(ReportDocument document)
    {
        return Task.FromResult(new ReportPdfRenderer().Render(document));
    }
}
