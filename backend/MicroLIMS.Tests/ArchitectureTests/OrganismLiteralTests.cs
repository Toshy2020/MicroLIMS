using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// CLAUDE.md Principle 1 / Rule 3.2: every pathogen runs through the same
// configuration-driven workflow. Production logic must never branch on an
// organism name or a test code naming one (e.g. TestCode.Contains("SALMONELLA")).
// Comments are ignored; seed data (MicroLIMS.Persistence/Seed) is configuration
// and is not scanned.
public class OrganismLiteralTests
{
    private static readonly Regex OrganismInStringLiteral = new(
        "\"[^\"]*(salmonella|e\\.? ?coli|aeruginosa|aureus|albicans|clostridi|brasiliensis)[^\"]*\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Files allowed to name an organism in a string literal. Empty: keep it
    // that way, and never add an entry without the owner's approval.
    private static readonly HashSet<string> KnownOffenders = new(StringComparer.Ordinal);

    private static string BackendDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void ProductionCode_DoesNotBranchOnOrganismNames()
    {
        var backend = BackendDirectory();
        var sep = Path.DirectorySeparatorChar;

        var offenders = new[] { "MicroLIMS.Application", "MicroLIMS.API", "MicroLIMS.Domain" }
            .Select(p => Path.Combine(backend, p))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Where(f => !KnownOffenders.Contains(Path.GetRelativePath(backend, f).Replace(sep, '/')))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => OrganismInStringLiteral.IsMatch(StripComment(l.Text)))
            .Select(l => $"{Path.GetRelativePath(backend, l.File)}:{l.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Organism names must come from configuration, not code (CLAUDE.md Rule 3.2):\n" + string.Join("\n", offenders));
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
