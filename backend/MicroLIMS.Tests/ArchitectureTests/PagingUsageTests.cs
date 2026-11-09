using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// CLAUDE.md Rule 5.4: list endpoints normalise paging only through
// Paging.Normalize, so the 100-row cap cannot drift per service again (it had
// grown to 100 in some services and 200 in others).
public class PagingUsageTests
{
    // A hand-rolled cap or clamp on a page size, e.g. Math.Min(filter.PageSize, 200).
    private static readonly Regex HandRolledCap = new(@"Math\.(Min|Max|Clamp)\([^;]*\bpageSize\b", RegexOptions.IgnoreCase);
    // A page actually being taken.
    private static readonly Regex TakesAPage = new(@"\.Take\(\s*pageSize\s*\)", RegexOptions.IgnoreCase);

    private static string ApplicationRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "MicroLIMS.Application"));

    private static IEnumerable<string> SourceFiles()
    {
        var sep = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(ApplicationRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")
                        && !f.EndsWith($"{sep}Helpers{sep}Paging.cs"));
    }

    [Fact]
    public void NoService_CapsAPageSizeByHand()
    {
        var offenders = SourceFiles()
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//") && HandRolledCap.IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(ApplicationRoot(), l.File)}:{l.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "Normalise paging with Paging.Normalize instead:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryServiceThatTakesAPage_NormalisesItThroughPaging()
    {
        var offenders = SourceFiles()
            .Where(f => { var text = File.ReadAllText(f); return TakesAPage.IsMatch(text) && !text.Contains("Paging.Normalize("); })
            .Select(f => Path.GetRelativePath(ApplicationRoot(), f))
            .ToList();

        Assert.True(offenders.Count == 0, "These take a page without Paging.Normalize:\n" + string.Join("\n", offenders));
    }
}
