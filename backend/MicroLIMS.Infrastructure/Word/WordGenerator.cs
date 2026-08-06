namespace MicroLIMS.Infrastructure.Word;

// Mirrors IPdfGenerator/PdfGenerator's role for PDF - kept behind
// IWordGenerator so callers never depend on the concrete .docx approach
// directly, same swap-later reasoning as PdfGenerator's own comment.
public class WordGenerator : IWordGenerator
{
    public Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines) =>
        Task.FromResult(SimpleDocxWriter.WriteTextDocument(title, lines));
}
