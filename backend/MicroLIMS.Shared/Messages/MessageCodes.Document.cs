namespace MicroLIMS.Shared.Messages;

// Document.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string DocumentAlreadyVoided = "Document.AlreadyVoided";
    public const string DocumentApprovePermissionRequired = "Document.ApprovePermissionRequired";
    public const string DocumentAssigneeInactive = "Document.AssigneeInactive";
    public const string DocumentAssigneeMissingPermission = "Document.AssigneeMissingPermission";
    public const string DocumentCompanyCodeAlreadyExists = "Document.CompanyCodeAlreadyExists";
    public const string DocumentCompanyCodeInUse = "Document.CompanyCodeInUse";
    public const string DocumentDepartmentDeactivated = "Document.DepartmentDeactivated";
    public const string DocumentDepartmentNotFound = "Document.DepartmentNotFound";
    public const string DocumentDraftRevisionOnlyCanCancel = "Document.DraftRevisionOnlyCanCancel";
    public const string DocumentEffectiveMetadataCannotEditDirectly = "Document.EffectiveMetadataCannotEditDirectly";
    public const string DocumentExternalReportHasRecordedResult = "Document.ExternalReportHasRecordedResult";
    public const string DocumentExternalReportMustUploadFromTest = "Document.ExternalReportMustUploadFromTest";
    public const string DocumentFileReadFailed = "Document.FileReadFailed";
    public const string DocumentIntegrityCheckFailed = "Document.IntegrityCheckFailed";
    public const string DocumentMasterAlreadyVoided = "Document.MasterAlreadyVoided";
    public const string DocumentMasterEffectiveCannotVoid = "Document.MasterEffectiveCannotVoid";
    public const string DocumentNotFound = "Document.NotFound";
    public const string DocumentOnlyAssignedApprover = "Document.OnlyAssignedApprover";
    public const string DocumentOwnerUserInactive = "Document.OwnerUserInactive";
    public const string DocumentOwnerUserNotFound = "Document.OwnerUserNotFound";
    public const string DocumentSectionDeactivated = "Document.SectionDeactivated";
    public const string DocumentSectionNotFound = "Document.SectionNotFound";
    public const string DocumentSectionWrongDepartment = "Document.SectionWrongDepartment";
    public const string DocumentTypeDeactivated = "Document.TypeDeactivated";
    public const string DocumentTypeNotFound = "Document.TypeNotFound";
    public const string DocumentUnsupportedSortColumn = "Document.UnsupportedSortColumn";
    public const string DocumentVoidReasonRequired = "Document.VoidReasonRequired";
    public const string DocumentVoidedCannotAddAssignments = "Document.VoidedCannotAddAssignments";
    public const string DocumentVoidedCannotEdit = "Document.VoidedCannotEdit";

    private static Dictionary<string, string[]> DocumentParameters() => new()
    {
        [DocumentAlreadyVoided] = [],
        [DocumentApprovePermissionRequired] = [],
        [DocumentAssigneeInactive] = ["fullName"],
        [DocumentAssigneeMissingPermission] = ["fullName", "assignmentRole", "permissionCode"],
        [DocumentCompanyCodeAlreadyExists] = ["companyDocumentCode"],
        [DocumentCompanyCodeInUse] = ["companyDocumentCode"],
        [DocumentDepartmentDeactivated] = ["name"],
        [DocumentDepartmentNotFound] = ["departmentId"],
        [DocumentDraftRevisionOnlyCanCancel] = ["status"],
        [DocumentEffectiveMetadataCannotEditDirectly] = [],
        [DocumentExternalReportHasRecordedResult] = [],
        [DocumentExternalReportMustUploadFromTest] = [],
        [DocumentFileReadFailed] = [],
        [DocumentIntegrityCheckFailed] = [],
        [DocumentMasterAlreadyVoided] = [],
        [DocumentMasterEffectiveCannotVoid] = [],
        [DocumentNotFound] = ["documentId"],
        [DocumentOnlyAssignedApprover] = [],
        [DocumentOwnerUserInactive] = ["fullName"],
        [DocumentOwnerUserNotFound] = ["userId"],
        [DocumentSectionDeactivated] = ["name"],
        [DocumentSectionNotFound] = ["sectionId"],
        [DocumentSectionWrongDepartment] = ["sectionName", "departmentName"],
        [DocumentTypeDeactivated] = ["name"],
        [DocumentTypeNotFound] = ["documentTypeId"],
        [DocumentUnsupportedSortColumn] = ["column"],
        [DocumentVoidReasonRequired] = [],
        [DocumentVoidedCannotAddAssignments] = [],
        [DocumentVoidedCannotEdit] = [],
    };
}
