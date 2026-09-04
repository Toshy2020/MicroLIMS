using System.Threading;
using System.Threading.Tasks;
using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface ITrainingMatrixService
{
    /// <summary>
    /// Computes the multi-axis Training Matrix grid across users, documents, and qualification statuses (DC-URS-111, DC-URS-112, DC-URS-113, DC-URS-114).
    /// </summary>
    Task<TrainingMatrixGridDto> GetTrainingMatrixGridAsync(
        TrainingMatrixFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes high-level training compliance KPIs, department rankings, and top overdue documents (DC-URS-118, DC-URS-119, DC-URS-120).
    /// </summary>
    Task<ComplianceKpiSummaryDto> GetComplianceKpisAsync(
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user-level drilldown compliance across all required and assigned documents (DC-URS-111, DC-URS-114).
    /// </summary>
    Task<UserComplianceDetailDto?> GetUserComplianceDetailAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document-level drilldown compliance across all assigned users (DC-URS-111, DC-URS-114).
    /// </summary>
    Task<DocumentComplianceDetailDto?> GetDocumentComplianceDetailAsync(
        int documentMasterId,
        CancellationToken cancellationToken = default);
}
