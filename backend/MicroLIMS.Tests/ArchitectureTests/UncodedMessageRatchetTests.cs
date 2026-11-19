using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// F4 Task 4 ratchet. A refusal the user reads is translated by its message
// code (MessageCodes), so a new `throw new BusinessRuleException(...)` or
// `throw new NotFoundException(...)` in Application or API carries one: its
// first argument is MessageCodes.<Name> (or a parameter named code/Code that
// passes a code through, as ActionCheck.ThrowIfRefused does).
//
// Throw sites written before the codes existed are counted in
// uncoded-messages-baseline.txt. The count may only go down: coding a site
// lowers the number in that file, and a new uncoded site fails this test.
public class UncodedMessageRatchetTests
{
    private const string BaselineFile = "uncoded-messages-baseline.txt";

    private static readonly Regex CodeableThrow = new(
        @"throw\s+new\s+(?:BusinessRule|NotFound)Exception\s*\(\s*(?<first>[^\s,)]*)",
        RegexOptions.Compiled);

    private static readonly Regex CodedFirstArgument = new(
        @"^(?:MessageCodes\.\w+|[cC]ode)$", RegexOptions.Compiled);

    private static string TestsDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(thisFile)!;

    private static string BackendDirectory() => Path.GetFullPath(Path.Combine(TestsDirectory(), "..", ".."));

    [Fact]
    public void Uncoded_throw_sites_do_not_exceed_the_baseline()
    {
        var backend = BackendDirectory();
        var sites = UncodedSites(backend);
        var baseline = int.Parse(File.ReadAllText(Path.Combine(TestsDirectory(), BaselineFile)).Trim());

        Assert.True(sites.Count <= baseline,
            $"{sites.Count} throw sites in Application and API have no message code, the baseline allows {baseline}. " +
            "Give the new refusal a code: add it to MessageCodes (with its parameter names) and to " +
            "frontend/src/locales/{en,ar}/serverMessages.json, then throw new BusinessRuleException(MessageCodes.X, \"English text\").");

        Assert.True(sites.Count >= baseline,
            $"Only {sites.Count} uncoded throw sites remain and the baseline says {baseline}. " +
            $"Lower the number in ArchitectureTests/{BaselineFile} to {sites.Count} so the gain cannot be lost.");
    }

    [Theory]
    [InlineData("throw new BusinessRuleException(\"Not allowed.\");", 1)]
    [InlineData("throw new BusinessRuleException(MessageCodes.SampleCannotReviewTested, \"x\");", 0)]
    [InlineData("throw new BusinessRuleException(code, Reason, Params);", 0)]
    [InlineData("?? throw new NotFoundException($\"Test {id} not found.\");", 1)]
    [InlineData("throw new NotFoundException(\n    $\"Test {id} not found.\");", 1)]
    [InlineData("throw new BusinessRuleException(\n    MessageCodes.TestAlreadyTaken,\n    \"x\");", 0)]
    [InlineData("throw new BusinessRuleException(\n    \"a\" +\n    \"b\");", 1)]
    [InlineData("// throw new BusinessRuleException(\"old\");", 0)]
    [InlineData("/* throw new NotFoundException(\"old\"); */", 0)]
    [InlineData("var url = \"http://x\"; throw new BusinessRuleException(\"y\");", 1)]
    [InlineData("throw new FieldValidationException(\"x\", errors);", 0)]
    public void Scanner_counts_only_uncoded_throws(string source, int expected) =>
        Assert.Equal(expected, CountUncoded(source));

    private static List<string> UncodedSites(string backend)
    {
        var sep = Path.DirectorySeparatorChar;
        var sites = new List<string>();
        foreach (var project in new[] { "MicroLIMS.Application", "MicroLIMS.API" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(backend, project), "*.cs", SearchOption.AllDirectories)
                         .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")))
            {
                var count = CountUncoded(File.ReadAllText(file));
                for (var i = 0; i < count; i++) sites.Add(Path.GetRelativePath(backend, file));
            }
        }
        return sites;
    }

    private static int CountUncoded(string source) =>
        CodeableThrow.Matches(StripComments(source)).Count(m => !CodedFirstArgument.IsMatch(m.Groups["first"].Value));

    // Blanks // and /* */ comments but leaves string and character literals alone, so a "//" inside a string does
    // not hide the rest of the line. Handles "..." with escapes and @"..." with doubled quotes; interpolated
    // strings are read as ordinary strings, which is enough to find the end of the literal.
    private static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                if (i < text.Length) sb.Append('\n');
            }
            else if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) { if (text[i] == '\n') sb.Append('\n'); i++; }
                i++;
            }
            else if (c == '"')
            {
                var verbatim = i > 0 && (text[i - 1] == '@' || (i > 1 && text[i - 1] == '$' && text[i - 2] == '@'));
                sb.Append(c);
                i++;
                while (i < text.Length)
                {
                    sb.Append(text[i]);
                    if (!verbatim && text[i] == '\\' && i + 1 < text.Length) { i++; sb.Append(text[i]); }
                    else if (text[i] == '"')
                    {
                        if (verbatim && i + 1 < text.Length && text[i + 1] == '"') { i++; sb.Append('"'); }
                        else break;
                    }
                    i++;
                }
            }
            else if (c == '\'' && next != '\0')
            {
                sb.Append(c);
                i++;
                while (i < text.Length && text[i] != '\'') { sb.Append(text[i]); if (text[i] == '\\') { i++; sb.Append(text[i]); } i++; }
                if (i < text.Length) sb.Append('\'');
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
