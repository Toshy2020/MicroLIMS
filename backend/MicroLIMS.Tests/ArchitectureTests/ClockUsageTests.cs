using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Application code reads the time from an injected TimeProvider (or
// ILabClock), never from the static system clock, so every time-dependent
// rule - expiry windows, incubation durations, lockouts - can be tested
// at its edges with a fake clock.
public class ClockUsageTests
{
    private static readonly Regex SystemClock = new(@"\bDateTime(Offset)?\.(UtcNow|Now|Today)\b");

    private static string ApplicationSourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "MicroLIMS.Application"));

    [Fact]
    public void ApplicationCode_DoesNotReadTheSystemClockDirectly()
    {
        var root = ApplicationSourceDirectory();
        Assert.True(Directory.Exists(root), $"Application sources not found at {root}");

        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//") && SystemClock.IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "Use the injected TimeProvider/ILabClock instead of the system clock:\n" + string.Join("\n", offenders));
    }
}
