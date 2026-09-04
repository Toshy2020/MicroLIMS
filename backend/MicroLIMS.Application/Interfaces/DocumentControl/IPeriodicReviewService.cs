using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IPeriodicReviewService
{
    Task<PeriodicReviewGenerationResultDto> GenerateDueReviewTasksAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default);
    Task<List<PeriodicReviewTaskDto>> GetReviewTasksAsync(int? masterId = null, int? revisionId = null, PeriodicReviewTaskStatus? status = null, int? reviewerUserId = null);
    Task<PeriodicReviewTaskDto> GetReviewTaskByIdAsync(int taskId, int userId);
    Task<PeriodicReviewWorkspaceDto> GetReviewWorkspaceAsync(int taskId, int userId);
    Task<PeriodicReviewFindingDto> AddFindingAsync(int taskId, CreatePeriodicReviewFindingRequest request, int userId);
    Task<PeriodicReviewFindingDto> ResolveFindingAsync(int taskId, int findingId, int userId);
    Task<PeriodicReviewTaskDto> AssignReviewerAsync(int taskId, AssignPeriodicReviewerRequest request, int userId);
    Task<PeriodicReviewTaskDto> CompleteReviewAsync(int taskId, CompletePeriodicReviewRequest request, int userId);
    Task<List<PeriodicReviewTaskDto>> GetMasterReviewHistoryAsync(int masterId, int userId);
}
