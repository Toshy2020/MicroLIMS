using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Application code reports a broken business or workflow rule with
// BusinessRuleException, never a plain InvalidOperationException. Otherwise
// ExceptionMiddleware cannot tell a rule the user broke from a framework
// InvalidOperationException that signals a bug (EF Core's "Sequence contains
// no elements", a tracking conflict).
//
// Infrastructure failures may wrap their inner exception. Documented
// caller preconditions also signal programming errors. Comments are ignored.
public class BusinessRuleThrowTests
{
    private static readonly Regex PlainThrow = new(@"new\s+InvalidOperationException\s*\(", RegexOptions.Compiled);
    private static readonly Regex WrapsInnerException = new(
        @"new\s+InvalidOperationException\s*\(.*,\s*[A-Za-z_][A-Za-z0-9_]*\s*\)\s*;", RegexOptions.Compiled);

    // Programming-error guards, not rules a user can break: a missing
    // transaction is a bug in the calling code and must surface as a 500, not
    // as a 4xx business message. Each entry needs this justification.
    private static readonly string[] ProgrammingErrorGuards =
    [
        Path.Combine("MicroLIMS.Application", "Services", "ReferenceNumberGenerator.cs"),
    ];

    // This internal finalisation entry point requires an already closed sample.
    // An open sample here means a caller skipped the close-out guard.
    private static readonly string SampleApprovalPath =
        Path.Combine("MicroLIMS.Application", "Services", "SampleApprovalService.cs");
    private const string SampleClosingPrecondition =
        "throw new InvalidOperationException(\"Only a closed sample can be finalised.\");";

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
            .Where(f => !ProgrammingErrorGuards.Any(g => f.EndsWith(g, StringComparison.Ordinal)))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: StripComment(line))))
            .Where(l => PlainThrow.IsMatch(l.Text) && !WrapsInnerException.IsMatch(l.Text))
            .Where(l => !l.File.EndsWith(SampleApprovalPath, StringComparison.Ordinal) ||
                l.Text.Trim() != SampleClosingPrecondition)
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
