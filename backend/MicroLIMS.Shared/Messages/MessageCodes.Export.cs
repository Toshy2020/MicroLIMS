namespace MicroLIMS.Shared.Messages;

// Export.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ExportColumnsAndRecordIdsRequired = "Export.ColumnsAndRecordIdsRequired";
    public const string ExportFilterJsonInvalid = "Export.FilterJsonInvalid";
    public const string ExportMetadataInvalidJson = "Export.MetadataInvalidJson";
    public const string ExportMetadataMustBeObject = "Export.MetadataMustBeObject";
    public const string ExportMetadataRecordIdsRequired = "Export.MetadataRecordIdsRequired";
    public const string ExportMetadataRequired = "Export.MetadataRequired";
    public const string ExportRowCountMismatch = "Export.RowCountMismatch";
    public const string ExportRowLimitExceeded = "Export.RowLimitExceeded";
    public const string ExportUnknownTableKey = "Export.UnknownTableKey";
    public const string ExportUserNotFound = "Export.UserNotFound";

    private static Dictionary<string, string[]> ExportParameters() => new()
    {
        [ExportColumnsAndRecordIdsRequired] = [],
        [ExportFilterJsonInvalid] = [],
        [ExportMetadataInvalidJson] = [],
        [ExportMetadataMustBeObject] = [],
        [ExportMetadataRecordIdsRequired] = [],
        [ExportMetadataRequired] = [],
        [ExportRowCountMismatch] = ["recordIdCount", "rowCount"],
        [ExportRowLimitExceeded] = ["maxRows"],
        [ExportUnknownTableKey] = ["tableKey"],
        [ExportUserNotFound] = [],
    };
}
