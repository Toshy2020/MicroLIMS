namespace MicroLIMS.Infrastructure.Pdf;

public interface IPdfGenerator
{
    Task<byte[]> GenerateAsync(string templateName, Dictionary<string, object> data);
    Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines);
}
