using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// TestWorkflowEngine reached 5,000+ lines and MasterDataController 3,300
// before they were split. This keeps any hand-written source file in the
// production projects from growing that large again. Migrations are
// generated and exempt.
public class SourceFileSizeTests
{
    private const int MaxLines = 2000;

    private static string BackendDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void NoSourceFile_ExceedsTheLimit()
    {
        var sep = Path.DirectorySeparatorChar;
        var projects = new[] { "MicroLIMS.API", "MicroLIMS.Application", "MicroLIMS.Domain", "MicroLIMS.Infrastructure", "MicroLIMS.Persistence", "MicroLIMS.Shared" };

        var oversized = projects
            .Select(p => Path.Combine(BackendDirectory(), p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}Migrations{sep}"))
            .Select(f => (File: Path.GetRelativePath(BackendDirectory(), f), Lines: File.ReadLines(f).Count()))
            .Where(f => f.Lines > MaxLines)
            .Select(f => $"{f.File}: {f.Lines} lines")
            .ToList();

        Assert.True(oversized.Count == 0, $"Split these by responsibility (limit {MaxLines} lines):\n" + string.Join("\n", oversized));
    }
}
