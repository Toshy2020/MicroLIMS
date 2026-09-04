using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentRevisionService
{
    Task<ProposeNextRevisionResponse> ProposeNextRevisionAsync(int documentMasterId, RevisionType revisionType, int userId);
    Task<DocumentRevisionDto> CreateRevisionFromEffectiveAsync(int documentMasterId, CreateRevisionRequest request, int userId);
    Task<DocumentRevisionDto> GetRevisionDetailsAsync(int revisionId, int userId);
    Task<RevisionChangeItemDto> AddChangeItemAsync(int revisionId, AddChangeItemRequest request, int userId);
    Task<RevisionChangeItemDto> UpdateChangeItemAsync(int changeItemId, UpdateChangeItemRequest request, int userId);
    Task DeleteChangeItemAsync(int changeItemId, int userId);
    Task<IReadOnlyList<RevisionChangeItemDto>> GetChangeItemsAsync(int revisionId, int userId);
    Task<RevisionChangeItemDto> ConvertFindingToChangeItemAsync(int revisionId, int findingId, ConvertFindingRequest request, int userId);
    Task<RevisionImpactAssessmentDto> SaveImpactAssessmentAsync(int revisionId, SaveImpactAssessmentRequest request, int userId);
    Task<RevisionImpactAssessmentDto?> GetImpactAssessmentAsync(int revisionId, int userId);
}
