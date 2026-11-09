using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Application, Persistence and Domain code reads the time from an injected
// TimeProvider (or ILabClock), never from the static system clock, so every
// time-dependent rule - expiry windows, incubation durations, lockouts, the
// audit timestamp - can be tested at its edges with a fake clock
// (CLAUDE.md Rule 3.3.2 / 5.6).
public class ClockUsageTests
{
    private static readonly Regex SystemClock = new(@"\bDateTime(Offset)?\.(UtcNow|Now|Today)\b");

    // An entity property's default value ("{ get; set; } = DateTime.UtcNow;").
    // Services set these timestamps explicitly; the default only fills a field
    // a caller forgot. Legacy: the count may only go down (ratchet).
    private static readonly Regex PropertyDefault = new(@"\{\s*get;\s*(set|init);\s*\}\s*=\s*DateTime\.UtcNow;");
    private const int MaxDomainPropertyDefaults = 104;

    private static string Backend([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static List<(string File, int Line, string Text)> ClockReads(string project, params string[] excludedFolders)
    {
        var root = Path.Combine(Backend(), project);
        Assert.True(Directory.Exists(root), $"Sources not found at {root}");
        var sep = Path.DirectorySeparatorChar;
        var excluded = new[] { "obj", "bin" }.Concat(excludedFolders).Select(f => $"{sep}{f}{sep}").ToArray();

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !excluded.Any(f.Contains))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetRelativePath(Backend(), f), Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//") && SystemClock.IsMatch(l.Text))
            .ToList();
    }

    private static string List(IEnumerable<(string File, int Line, string Text)> reads) =>
        string.Join("\n", reads.Select(r => $"{r.File}:{r.Line}"));

    [Fact]
    public void ApplicationCode_DoesNotReadTheSystemClockDirectly()
    {
        var offenders = ClockReads("MicroLIMS.Application");
        Assert.True(offenders.Count == 0, "Use the injected TimeProvider/ILabClock instead of the system clock:\n" + List(offenders));
    }

    // Seed data (startup demo rows) and generated migrations are not workflows.
    [Fact]
    public void PersistenceCode_DoesNotReadTheSystemClockDirectly()
    {
        var offenders = ClockReads("MicroLIMS.Persistence", "Seed", "Migrations");
        Assert.True(offenders.Count == 0, "Use the injected TimeProvider (MicroLimsDbContext's clock) instead of the system clock:\n" + List(offenders));
    }

    // Time-dependent decisions on entities take "now" as a parameter
    // (e.g. Incubation.IsIncubationCompleteAt, User.IsLockedAt).
    [Fact]
    public void DomainCode_ReadsTheSystemClockOnlyInLegacyPropertyDefaults()
    {
        var reads = ClockReads("MicroLIMS.Domain");
        var decisions = reads.Where(r => !PropertyDefault.IsMatch(r.Text)).ToList();
        Assert.True(decisions.Count == 0,
            "A domain decision must take the current time as a parameter from the caller's injected clock:\n" + List(decisions));

        var defaults = reads.Count - decisions.Count;
        Assert.True(defaults <= MaxDomainPropertyDefaults,
            $"New DateTime.UtcNow property defaults in Domain ({defaults} > {MaxDomainPropertyDefaults}); set the timestamp from the injected clock in the service instead.");
    }
}
