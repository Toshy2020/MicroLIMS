using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// CI runs the suite twice and verify_test_results.py requires both runs to discover the same number
// of tests. Without a database xUnit reports a skipped [PostgresTheory] as ONE test, but with a
// database it reports one test per data row, so a multi-row database theory fails CI even when every
// test passes. Write such a test as a [PostgresFact] that loops its cases through PostgresCases.RunAsync.
// This test catches the mistake locally, where the suite usually runs only with the database.
public class PostgresTheoryRowTests
{
    private static readonly Regex DataRow = new(@"^\s*\[(InlineData|MemberData|ClassData)\b", RegexOptions.Compiled);
    private static readonly Regex OtherAttribute = new(@"^\s*\[", RegexOptions.Compiled);
    private static readonly Regex PostgresTheory = new(@"^\s*\[PostgresTheory\b", RegexOptions.Compiled);

    private static string TestsDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    // Counts the data-row attributes directly under each [PostgresTheory]. MemberData and ClassData
    // supply an unknown number of rows, so any of them counts as more than one.
    public static IReadOnlyList<string> MultiRowTheories(string source, string fileLabel)
    {
        var lines = source.Split('\n');
        var offenders = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!PostgresTheory.IsMatch(lines[i])) continue;
            var rows = 0;
            for (var j = i + 1; j < lines.Length && OtherAttribute.IsMatch(lines[j]); j++)
            {
                var m = DataRow.Match(lines[j]);
                if (m.Success) rows += m.Groups[1].Value == "InlineData" ? 1 : 2;
            }
            if (rows > 1) offenders.Add($"{fileLabel}:{i + 1}");
        }
        return offenders;
    }

    [Fact]
    public void NoPostgresTheoryHasMoreThanOneDataRow()
    {
        var root = TestsDirectory();
        var sep = Path.DirectorySeparatorChar;
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .SelectMany(f => MultiRowTheories(File.ReadAllText(f), Path.GetRelativePath(root, f)))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Multi-row [PostgresTheory] found; use a [PostgresFact] with PostgresCases.RunAsync instead:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Detector_FlagsMultiRowTheories_AndAllowsOthers()
    {
        const string multi = "    [PostgresTheory]\n    [InlineData(1)]\n    [InlineData(2)]\n    public async Task A(int x) { }\n";
        const string member = "    [PostgresTheory]\n    [MemberData(nameof(Cases))]\n    public async Task B(int x) { }\n";
        const string single = "    [PostgresTheory]\n    [InlineData(1)]\n    public async Task C(int x) { }\n";
        const string plainTheory = "    [Theory]\n    [InlineData(1)]\n    [InlineData(2)]\n    public void D(int x) { }\n";

        Assert.Equal(new[] { "f:1" }, MultiRowTheories(multi, "f"));
        Assert.Equal(new[] { "f:1" }, MultiRowTheories(member, "f"));
        Assert.Empty(MultiRowTheories(single, "f"));
        Assert.Empty(MultiRowTheories(plainTheory, "f"));
    }

    private static readonly Regex ClassStart = new(
        @"^\s*(public\s+|internal\s+|private\s+|protected\s+)?((sealed|static|abstract|partial)\s+)*class\s", RegexOptions.Compiled);
    private static readonly Regex PlainTestAttribute = new(@"^\s*\[(Fact|Theory)\s*(\(|\]|,)", RegexOptions.Compiled);
    private static readonly Regex UsesDatabaseFixture = new(
        @"^(?!\s*(using\b|//|\+|""|@))[^""]*(PostgresTestFixture|PostgresDatabaseCollection)", RegexOptions.Compiled);

    // Without a database a plain [Fact]/[Theory] in a class that uses the PostgreSQL fixture runs and
    // fails (the fixture cannot connect) instead of being reported as Skipped. Every test in such a
    // class must be a [PostgresFact] or a single-row [PostgresTheory].
    public static IReadOnlyList<string> PlainTestsInDatabaseClasses(string source, string fileLabel)
    {
        var lines = source.Split('\n');
        var offenders = new List<string>();
        var starts = new List<int>();
        for (var i = 0; i < lines.Length; i++)
            if (ClassStart.IsMatch(lines[i])) starts.Add(i);
        // Attributes such as [Collection("PostgresDatabaseCollection")] sit directly above a class line
        // and belong to that class, not to the one before it.
        int WithAttributes(int classLine)
        {
            var from = classLine;
            while (from > 0 && OtherAttribute.IsMatch(lines[from - 1])) from--;
            return from;
        }
        for (var k = 0; k < starts.Count; k++)
        {
            var end = k + 1 < starts.Count ? WithAttributes(starts[k + 1]) : lines.Length;
            var start = starts[k];
            var from = WithAttributes(start);
            var uses = false;
            for (var i = from; i < end && !uses; i++) uses = UsesDatabaseFixture.IsMatch(lines[i]);
            if (!uses) continue;
            for (var i = start; i < end; i++)
                if (PlainTestAttribute.IsMatch(lines[i])) offenders.Add($"{fileLabel}:{i + 1}");
        }
        return offenders;
    }

    [Fact]
    public void NoPlainFactOrTheoryInAClassThatUsesThePostgresFixture()
    {
        var root = TestsDirectory();
        var sep = Path.DirectorySeparatorChar;
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .SelectMany(f => PlainTestsInDatabaseClasses(File.ReadAllText(f), Path.GetRelativePath(root, f)))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Plain [Fact]/[Theory] in a class that uses PostgresTestFixture fails without a database; "
            + "use [PostgresFact] (or a single-row [PostgresTheory]):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void PlainTestDetector_FlagsTheOldSopRevisionCaptureForm_AndAllowsOthers()
    {
        var oldForm = Lines(
            "[Collection(\"PostgresDatabaseCollection\")]",
            "public class SopX(PostgresTestFixture fixture)",
            "{",
            "    [Theory]",
            "    [InlineData(\"a\")]",
            "    public async Task A(string s) { }",
            "",
            "    [PostgresFact]",
            "    public async Task B() { }",
            "}");
        var plainFact = Lines(
            "public class Y : IClassFixture<PostgresTestFixture>",
            "{",
            "    [Fact]",
            "    public void C() { }",
            "}");
        var good = Lines(
            "[Collection(\"PostgresDatabaseCollection\")]",
            "public class Z(PostgresTestFixture fixture)",
            "{",
            "    [PostgresFact]",
            "    public async Task D() { }",
            "    [PostgresTheory]",
            "    [InlineData(1)]",
            "    public async Task E(int x) { }",
            "}");
        var unrelated = Lines(
            "public class W",
            "{",
            "    [Fact]",
            "    public void F() { }",
            "}");
        var mixedFile = Lines(
            "public class W",
            "{",
            "    [Fact]",
            "    public void F() { }",
            "}",
            "public class V(PostgresTestFixture fixture)",
            "{",
            "    [PostgresFact]",
            "    public async Task G() { }",
            "}");

        Assert.Equal(new[] { "f:4" }, PlainTestsInDatabaseClasses(oldForm, "f"));
        Assert.Equal(new[] { "f:3" }, PlainTestsInDatabaseClasses(plainFact, "f"));
        Assert.Empty(PlainTestsInDatabaseClasses(good, "f"));
        Assert.Empty(PlainTestsInDatabaseClasses(unrelated, "f"));
        Assert.Empty(PlainTestsInDatabaseClasses(mixedFile, "f"));
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";
}
