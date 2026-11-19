namespace MicroLIMS.Application.DTOs.Responses;

// One item that uses a test. SopStatus is one of Linked | NotFound | NoNumber,
// resolved against Document Control by the rule ItemStmService uses.
public sealed record TestItemRow(
    int ItemId,
    string ItemCode,
    string ItemName,
    string? SopNumber,
    string SopStatus,
    int? DocumentMasterId,
    string? EffectiveRevisionNumber,
    int UploadedDocumentCount,
    bool AutoAssignedOnReceipt,
    bool HasSpecification);

public static class TestItemSopStatus
{
    public const string Linked = "Linked";
    public const string NotFound = "NotFound";
    public const string NoNumber = "NoNumber";
}
