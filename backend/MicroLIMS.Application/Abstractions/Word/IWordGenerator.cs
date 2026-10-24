using MicroLIMS.Application.Abstractions.Pdf;

namespace MicroLIMS.Application.Abstractions.Word;

public interface IWordGenerator
{
    Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines);

    // The laid-out path, IPdfGenerator.GenerateReportAsync's editable
    // counterpart: the same ReportDocument, so a report's PDF and Word
    // copies always carry the same content.
    Task<byte[]> GenerateReportAsync(ReportDocument document);
}
