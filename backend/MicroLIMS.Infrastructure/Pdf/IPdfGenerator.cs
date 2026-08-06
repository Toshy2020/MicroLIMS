namespace MicroLIMS.Infrastructure.Pdf;

public interface IPdfGenerator
{
    Task<byte[]> GenerateAsync(string templateName, Dictionary<string, object> data);
    Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines);

    // The laid-out path: cards, stat boxes, signature blocks and the
    // controlled-document footer. Used for anything that gets archived.
    Task<byte[]> GenerateReportAsync(ReportDocument document);
}
