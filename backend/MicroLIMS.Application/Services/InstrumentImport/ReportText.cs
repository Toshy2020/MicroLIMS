namespace MicroLIMS.Application.Services.InstrumentImport;

// Where a report's text came from. OCR text is read off a picture of the
// page, so every value taken from it must be checked by the analyst.
public enum ReportTextSource
{
    TextLayer,
    Ocr
}

// One line of report text. Confidence is the OCR engine's mean word
// confidence (0-100); null for a PDF text layer.
public sealed record ReportLine(string Text, double? Confidence);

// Lines: the text the parser reads. CrossCheckLines: a second, independent
// OCR pass over the same page; fields read differently by the two passes
// are flagged. Empty for a text layer. ReaderProblems: what the reader
// itself found wrong with the file (e.g. a photo too small to read).
public sealed record ReportText(
    ReportTextSource Source,
    IReadOnlyList<ReportLine> Lines,
    IReadOnlyList<ReportLine> CrossCheckLines,
    IReadOnlyList<string>? ReaderProblems = null);

// Turns an instrument report file (text PDF, scanned PDF, or photo) into
// lines of text. One page per report: only the first page is read.
public interface IReportTextReader
{
    Task<ReportText> ReadAsync(byte[] content, string fileName, CancellationToken ct = default);
}
