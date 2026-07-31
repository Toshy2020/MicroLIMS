using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Section Head sees: Workflow history -> Results -> Decision.
// Full decision set per the spec: Approve, Reject, Retest Retained
// Sample, New Sample Request, Investigation, OOS Investigation.
public class ApprovalService
{
    private readonly MicroLimsDbContext _db;

    public ApprovalService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<ApprovalDto> DecideAsync(int testOrderId, ApprovalDecision decision, string? comment, int decidedByUserId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        if (order.Status != ApprovalStatus.Reviewed)
            throw new InvalidOperationException("Test order must be reviewed before a decision can be made.");

        order.Status = decision switch
        {
            ApprovalDecision.Approve => ApprovalStatus.Approved,
            ApprovalDecision.Reject => ApprovalStatus.Rejected,
            ApprovalDecision.RetestRetainedSample => ApprovalStatus.RetestRequested,
            ApprovalDecision.NewSampleRequest => ApprovalStatus.RetestRequested,
            ApprovalDecision.Investigation => ApprovalStatus.RetestRequested,
            ApprovalDecision.OOSInvestigation => ApprovalStatus.RetestRequested,
            _ => throw new InvalidOperationException($"Unknown decision '{decision}'.")
        };

        if (decision == ApprovalDecision.Investigation || decision == ApprovalDecision.OOSInvestigation)
        {
            // OOS/Investigation decisions require a documented reason -
            // GMP requirement, not optional even if the UI allows blank comments elsewhere.
            if (string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException($"{decision} requires a documented comment/justification.");
        }

        RecordDecisionHistory(order.Id, decision, comment, decidedByUserId);

        await _db.SaveChangesAsync();

        return new ApprovalDto
        {
            TestOrderId = testOrderId,
            Decision = decision.ToString(),
            Comment = comment,
            DecidedByUserId = decidedByUserId
        };
    }

    // WorkflowHistory doubles as the decision trail for approvals too -
    // keeps one single source of truth for "what happened to this test order and when".
    private void RecordDecisionHistory(int testOrderId, ApprovalDecision decision, string? comment, int userId)
    {
        _db.WorkflowHistories.Add(new Domain.Entities.WorkflowHistory
        {
            TestOrderId = testOrderId,
            FromStep = Domain.Enums.WorkflowStep.Reviewed,
            ToStep = decision == ApprovalDecision.Approve ? Domain.Enums.WorkflowStep.Approved : Domain.Enums.WorkflowStep.Reviewed,
            Note = $"{decision}: {comment}",
            PerformedByUserId = userId
        });
    }
}
