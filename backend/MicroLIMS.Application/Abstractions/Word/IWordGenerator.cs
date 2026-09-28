namespace MicroLIMS.Application.Abstractions.Word;

public interface IWordGenerator
{
    Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines);
}
