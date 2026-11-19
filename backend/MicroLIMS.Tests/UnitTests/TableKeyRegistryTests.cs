using System.Text.Json;
using MicroLIMS.Application.Services.TableViews;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The canonical list is frontend/src/tables/tableRegistry.json. The backend embeds a copy of it (the Docker build
// context is backend/ only); these tests read the frontend file from disk, so a stale or edited copy fails here.
public class TableKeyRegistryTests
{
    private static string RegistryPath([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "frontend", "src", "tables", "tableRegistry.json"));

    private static string BackendCopyPath([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "MicroLIMS.Application", "Services", "TableViews", "tableRegistry.json"));

    private static List<string> FrontendKeys()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RegistryPath()));
        return doc.RootElement.EnumerateArray().Select(t => t.GetProperty("key").GetString()!).ToList();
    }

    [Fact]
    public void BackendRegistryKeys_EqualFrontendRegistryKeys()
    {
        var frontend = FrontendKeys();

        Assert.NotEmpty(frontend);
        Assert.Equal(frontend.OrderBy(k => k, StringComparer.Ordinal), TableKeyRegistry.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void BackendCopy_IsIdenticalToFrontendRegistry()
    {
        // Compared after normalising line endings only: any other difference means the copy was not refreshed.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.True(Read(BackendCopyPath()) == Read(RegistryPath()),
            "backend/MicroLIMS.Application/Services/TableViews/tableRegistry.json differs from frontend/src/tables/tableRegistry.json; copy the frontend file over it.");
    }

    [Fact]
    public void FrontendRegistry_HasNoDuplicateKeys()
    {
        var frontend = FrontendKeys();
        Assert.Equal(frontend.Count, frontend.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void IsKnown_AcceptsEveryRegisteredKey_AndNothingElse()
    {
        foreach (var key in FrontendKeys())
            Assert.True(TableKeyRegistry.IsKnown(key), key);

        Assert.False(TableKeyRegistry.IsKnown(null));
        Assert.False(TableKeyRegistry.IsKnown(""));
        Assert.False(TableKeyRegistry.IsKnown("no.such.table"));
        // Keys are exact: no trimming, no case folding.
        var first = FrontendKeys()[0];
        Assert.False(TableKeyRegistry.IsKnown(first.ToUpperInvariant() == first ? first.ToLowerInvariant() : first.ToUpperInvariant()));
        Assert.False(TableKeyRegistry.IsKnown(" " + first));
    }
}
