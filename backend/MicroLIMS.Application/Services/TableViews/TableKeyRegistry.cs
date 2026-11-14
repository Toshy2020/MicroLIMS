using System.Reflection;
using System.Text.Json;

namespace MicroLIMS.Application.Services.TableViews;

// The keys of the tables that may carry saved views and audited exports. The list is not kept here:
// it is frontend/src/tables/tableRegistry.json, embedded into this assembly at build, so the frontend
// and the backend read one file. A key is a name for a table, never a permission to read its rows.
public static class TableKeyRegistry
{
    private const string ResourceName = "tableRegistry.json";

    private static readonly Lazy<IReadOnlySet<string>> Loaded = new(Load);

    public static IReadOnlySet<string> Keys => Loaded.Value;

    public static bool IsKnown(string? key) => !string.IsNullOrEmpty(key) && Keys.Contains(key);

    private static IReadOnlySet<string> Load()
    {
        using var stream = typeof(TableKeyRegistry).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException(
                "The table registry (frontend/src/tables/tableRegistry.json) was not embedded in this build. "
                + "Build from a checkout that contains the frontend folder.");
        using var doc = JsonDocument.Parse(stream);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in doc.RootElement.EnumerateArray())
        {
            if (table.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String && key.GetString() is { Length: > 0 } k)
                keys.Add(k);
        }
        if (keys.Count == 0)
            throw new InvalidDataException("The embedded table registry lists no tables.");
        return keys;
    }
}
