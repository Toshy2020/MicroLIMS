using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentApprovalService
{
    Task<DocumentApprovalTaskDto> CreateApprovalTaskAsync(int revisionId, CreateApprovalTaskRequest request, int userId);
    Task<DocumentApprovalTaskDto> GetApprovalTaskByIdAsync(int approvalTaskId, int userId);
    Task<DocumentApprovalTaskDto?> GetActiveApprovalTaskForRevisionAsync(int revisionId, int userId);
    Task<IReadOnlyList<DocumentApprovalTaskDto>> GetApprovalTasksAsync(int? revisionId, int? approverUserId, DocumentApprovalTaskStatus? status, int userId);
    Task<ApprovalDossierDto> GetApprovalDossierAsync(int approvalTaskId, int userId);
    Task<ApprovalReadinessDto> ValidateApprovalReadinessAsync(int approvalTaskId, int userId);
    Task<DocumentApprovalTaskDto> ExecuteApprovalDecisionAsync(int approvalTaskId, ExecuteApprovalDecisionRequest request, int userId, string? ipAddress = null);
    Task<ElectronicSignatureDto?> GetApprovalSignatureAsync(int approvalTaskId, int userId);
}
