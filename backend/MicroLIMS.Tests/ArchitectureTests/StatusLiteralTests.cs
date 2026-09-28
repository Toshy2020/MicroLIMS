using System.Reflection;
using System.Text.RegularExpressions;
using MicroLIMS.Domain.Constants;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Result statuses are stored as text and read by the frontend, so their
// values must never change, and code must name them through ResultStatus
// rather than retyping them - a typo in a literal silently becomes a new
// status that nothing recognises.
public class StatusLiteralTests
{
    private static string BackendDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void EveryResultStatus_IsStoredAsItsOwnName()
    {
        var constants = typeof(ResultStatus).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .ToList();

        Assert.All(constants, f => Assert.Equal(f.Name, (string)f.GetRawConstantValue()!));
        Assert.Equal(constants.Select(f => (string)f.GetRawConstantValue()!).ToHashSet(), ResultStatus.All.ToHashSet());
    }

    [Fact]
    public void ProductionCode_NamesResultStatusesThroughTheConstants()
    {
        var sep = Path.DirectorySeparatorChar;
        var literal = new Regex("\"(" + string.Join("|", ResultStatus.All) + ")\"");
        var offenders = new[] { "MicroLIMS.API", "MicroLIMS.Application", "MicroLIMS.Infrastructure" }
            .Select(p => Path.Combine(BackendDirectory(), p))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Code: StripComment(line))))
            // A specification's expected state ("Present"/"Absent") is a
            // display label, not a result status.
            .Where(l => literal.IsMatch(l.Code) && !l.Code.Contains("ExpectedPresence"))
            .Select(l => $"{Path.GetRelativePath(BackendDirectory(), l.File)}:{l.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "Use ResultStatus.* instead of the literal:\n" + string.Join("\n", offenders));
    }

    private static string StripComment(string line)
    {
        var i = line.IndexOf("//", StringComparison.Ordinal);
        return i < 0 ? line : line[..i];
    }
}
