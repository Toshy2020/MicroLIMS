using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentEscalationService
{
    /// <summary>
    /// Evaluates active training assignments across configured escalation windows (T-X, Due, T+Y)
    /// and generates attributable, immutable escalation records idempotently.
    /// Recovers missed events across downtime windows.
    /// </summary>
    Task<EscalationProcessingResultDto> ProcessDueEscalationsAsync(
        DateTime? utcNowOverride = null,
        string processName = "DocumentEffectiveDateWorker",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical escalation records for a specific training assignment.
    /// </summary>
    Task<IReadOnlyList<DocumentEscalationSummaryDto>> GetAssignmentEscalationHistoryAsync(
        int assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active overdue assignments and their highest escalation level.
    /// </summary>
    Task<OverdueTrainingSummaryDto> GetOverdueTrainingSummaryAsync(
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an open escalation record with attributable reason and user context.
    /// </summary>
    Task<DocumentEscalationSummaryDto> ResolveEscalationAsync(
        ResolveEscalationRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);
}
