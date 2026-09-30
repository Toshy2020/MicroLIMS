using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroLIMS.Application.Helpers;

// JSON options for the snapshots and audit payloads the HPLC chain stores
// (recipe, method and standardization-settings snapshots, method history).
// Enums are written by name, as the API writes them, so the stored JSON reads
// the same as the API and the frontend can use it as is. Reading still
// accepts the numeric form written before this existed.
public static class SnapshotJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
