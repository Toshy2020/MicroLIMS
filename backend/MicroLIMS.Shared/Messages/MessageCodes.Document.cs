namespace MicroLIMS.Shared.Messages;

// Document.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string DocumentAlreadyVoided = "Document.AlreadyVoided";
    public const string DocumentApprovePermissionRequired = "Document.ApprovePermissionRequired";
    public const string DocumentExternalReportHasRecordedResult = "Document.ExternalReportHasRecordedResult";
    public const string DocumentExternalReportMustUploadFromTest = "Document.ExternalReportMustUploadFromTest";
    public const string DocumentFileReadFailed = "Document.FileReadFailed";
    public const string DocumentIntegrityCheckFailed = "Document.IntegrityCheckFailed";
    public const string DocumentNotFound = "Document.NotFound";
    public const string DocumentOnlyAssignedApprover = "Document.OnlyAssignedApprover";
    public const string DocumentVoidReasonRequired = "Document.VoidReasonRequired";

    private static Dictionary<string, string[]> DocumentParameters() => new()
    {
        [DocumentAlreadyVoided] = [],
        [DocumentApprovePermissionRequired] = [],
        [DocumentExternalReportHasRecordedResult] = [],
        [DocumentExternalReportMustUploadFromTest] = [],
        [DocumentFileReadFailed] = [],
        [DocumentIntegrityCheckFailed] = [],
        [DocumentNotFound] = ["documentId"],
        [DocumentOnlyAssignedApprover] = [],
        [DocumentVoidReasonRequired] = [],
    };
}
