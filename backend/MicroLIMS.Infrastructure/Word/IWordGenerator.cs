namespace MicroLIMS.Infrastructure.Word;

public interface IWordGenerator
{
    Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines);
}
