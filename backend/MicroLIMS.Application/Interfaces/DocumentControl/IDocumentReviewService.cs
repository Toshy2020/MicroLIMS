using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentReviewService
{
    Task<DocumentReviewTaskDto> SubmitForReviewAsync(int revisionId, SubmitForReviewRequest request, int userId);
    Task<DocumentReviewTaskDto> GetReviewTaskByIdAsync(int reviewTaskId, int userId);
    Task<List<DocumentReviewTaskDto>> GetReviewTasksByRevisionIdAsync(int revisionId, int userId);
    Task<List<DocumentReviewTaskDto>> GetMyAssignedReviewTasksAsync(int userId);
    Task<DocumentReviewFindingDto> AddReviewFindingAsync(int reviewTaskId, AddReviewFindingRequest request, int userId);
    Task<DocumentReviewFindingDto> RespondToFindingAsync(int findingId, RespondToFindingRequest request, int userId);
    Task<DocumentReviewFindingDto> VerifyFindingAsync(int findingId, VerifyFindingRequest request, int userId);
    Task<DocumentReviewFindingDto> ResolveFindingAsync(int findingId, int userId);
    Task<DocumentReviewTaskDto> DecideReviewAsync(int reviewTaskId, ReviewDecisionRequest request, int userId);
}
