namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentAuthorizationService
{
    Task<bool> CanRegisterDocumentMasterAsync(int userId);
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
