using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentMasterService
{
    Task<DocumentMasterDto> RegisterDocumentMasterAsync(RegisterDocumentMasterRequest request, int userId);
    Task<DocumentMasterDto> GetByIdAsync(int id, int userId);
    Task<DocumentLibraryResponse> GetLibraryAsync(DocumentLibraryFilterRequest filter, int userId);
    Task<DocumentMasterDto> UpdateDraftMetadataAsync(int id, UpdateDocumentMasterDraftRequest request, int userId);
    Task<DocumentMasterDto> VoidMasterAsync(int id, VoidDocumentMasterRequest request, int userId);
    Task<DocumentRevisionDto> CancelDraftRevisionAsync(int revisionId, CancelDraftRevisionRequest request, int userId);
    Task<DocumentMasterAssignmentDto> AddOrUpdateAssignmentAsync(int documentMasterId, CreateAssignmentRequest request, int userId);
    Task RemoveAssignmentAsync(int documentMasterId, int assignmentId, int userId);
}
