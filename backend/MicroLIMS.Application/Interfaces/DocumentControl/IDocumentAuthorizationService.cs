namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentAuthorizationService
{
    Task<bool> CanRegisterDocumentMasterAsync(int userId);
    // Role gates (defence in depth): each Document Control action needs the
    // role to hold the code AND the per-document assignment/ownership rule.
    Task<bool> HoldsPermissionAsync(int userId, string permissionCode);
    Task<bool> CanCreateRevisionAsync(int documentMasterId, int userId);
    Task<bool> CanPerformTechnicalReviewAsync(int userId);
    Task<bool> CanExecuteApprovalAsync(int userId);
    Task<bool> CanPerformPeriodicReviewAsync(int userId);
    Task<bool> CanViewDocumentAsync(int documentMasterId, int userId);
    Task<bool> CanEditDraftMetadataAsync(int documentMasterId, int userId);
    Task<bool> CanUploadOrReplaceDraftFileAsync(int revisionId, int userId);
    Task<bool> CanCancelDraftRevisionAsync(int revisionId, int userId);
    Task<bool> CanVoidDocumentMasterAsync(int documentMasterId, int userId);
    Task<bool> CanAccessSourceFileAsync(int fileId, int userId);
    Task<bool> CanAccessControlledPdfAsync(int fileId, int userId);
    Task<bool> CanManageConfigurationAsync(int userId);
    Task<bool> CanQueryGlobalAuditAsync(int userId);
    Task<bool> CanViewRecordAuditAsync(int documentMasterId, int userId);
}
