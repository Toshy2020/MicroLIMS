namespace MicroLIMS.Application.Interfaces;

// Route is the page that opens the record, built by the server (RecordNavigationTargets) as for search results.
public sealed record RecentRecordDto(string RecordType, int RecordId, string Identifier, string Title, string Route, DateTime OpenedAtUtc);

// The caller's own recently opened records. Always scoped to the user passed in; the controller takes that id
// from the caller's claims. The server writes the label itself, after checking the user may read the record.
public interface IRecentRecordService
{
    // Upserts and keeps the newest 20. Stores nothing, and says nothing, when the record is unknown or unreadable.
    // An unknown record type is a BusinessRuleException.
    Task TouchAsync(int userId, string recordType, int recordId, CancellationToken ct = default);
    // Newest first; a record the user may no longer read is left out.
    Task<IReadOnlyList<RecentRecordDto>> ListAsync(int userId, CancellationToken ct = default);
    Task ClearAsync(int userId, CancellationToken ct = default);
}

// Resolves the label of records, returning only the ones the user may read right now.
public interface IRecordLabelResolver
{
    Task<IReadOnlyDictionary<(string Type, int Id), (string Identifier, string Title, string Route)>> ResolveAsync(
        int userId, IReadOnlyCollection<(string Type, int Id)> records, CancellationToken ct = default);
}

public static class RecentRecordTypes
{
    public const string Sample = "sample";
    public const string TestOrder = "testOrder";
    public const string Item = "item";
    public const string Document = "document";
    public const string Equipment = "equipment";
    public const string Material = "material";
    public const string Media = "media";
    public const string Solution = "solution";

    public static readonly IReadOnlyList<string> All =
        new[] { Sample, TestOrder, Item, Document, Equipment, Material, Media, Solution };

    public static bool IsKnown(string? type) => type is not null && All.Contains(type);
}
