namespace MicroLIMS.Application.Abstractions.Pdf;

public interface IPdfGenerator
{
    // The laid-out path: cards, stat boxes, signature blocks and the
    // controlled-document footer. Used for anything that gets archived.
    Task<byte[]> GenerateReportAsync(ReportDocument document);
}
