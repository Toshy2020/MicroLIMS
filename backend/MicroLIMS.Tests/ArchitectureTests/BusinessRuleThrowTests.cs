using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Application code reports a broken business or workflow rule with
// BusinessRuleException, never a plain InvalidOperationException. Otherwise
// ExceptionMiddleware cannot tell a rule the user broke from a framework
// InvalidOperationException that signals a bug (EF Core's "Sequence contains
// no elements", a tracking conflict).
//
// The one allowed form wraps an infrastructure failure with its inner
// exception - new InvalidOperationException("Document storage failed...", ex) -
// which is not a rule violation. Comments are ignored.
public class BusinessRuleThrowTests
{
    private static readonly Regex PlainThrow = new(@"new\s+InvalidOperationException\s*\(", RegexOptions.Compiled);
    private static readonly Regex WrapsInnerException = new(
        @"new\s+InvalidOperationException\s*\(.*,\s*[A-Za-z_][A-Za-z0-9_]*\s*\)\s*;", RegexOptions.Compiled);

    private static string BackendDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void ApplicationCode_ThrowsBusinessRuleException_ForRuleViolations()
    {
        var backend = BackendDirectory();
        var sep = Path.DirectorySeparatorChar;
        var root = Path.Combine(backend, "MicroLIMS.Application");

        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: StripComment(line))))
            .Where(l => PlainThrow.IsMatch(l.Text) && !WrapsInnerException.IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(backend, l.File)}:{l.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Throw BusinessRuleException for a broken rule, not InvalidOperationException:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData("throw new InvalidOperationException(\"Not allowed.\");", true)]
    [InlineData("?? throw new InvalidOperationException($\"Item {id} is inactive.\");", true)]
    [InlineData("throw new InvalidOperationException(\"Document storage failed.\", ex);", false)]
    [InlineData("throw new BusinessRuleException(\"Not allowed.\");", false)]
    [InlineData("// throw new InvalidOperationException(\"old\");", false)]
    public void Scanner_FlagsOnlyPlainRuleThrows(string line, bool flagged)
    {
        var text = StripComment(line);
        Assert.Equal(flagged, PlainThrow.IsMatch(text) && !WrapsInnerException.IsMatch(text));
    }

    // Drops a trailing // comment that is not inside a string literal.
    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
            if (!inString && line[i] == '/' && line[i + 1] == '/') return line[..i];
        }
        return line;
    }
}
