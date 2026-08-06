using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Section Head sees: Workflow history -> Results -> Decision.
// Full decision set per the spec: Approve, Reject, Retest Retained
// Sample, New Sample Request, Investigation, OOS Investigation.
public class ApprovalService
{
    private readonly MicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly IElectronicSignatureService _signatureService;

    public ApprovalService(MicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties, IElectronicSignatureService signatureService)
    {
        _db = db;
        _segregationOfDuties = segregationOfDuties;
        _signatureService = signatureService;
    }

    public async Task<ApprovalDto> DecideAsync(int testOrderId, ApprovalDecision decision, string? comment, int decidedByUserId, string password, string? ipAddress)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        if (order.Status != ApprovalStatus.Reviewed)
            throw new InvalidOperationException("Test order must be reviewed before a decision can be made.");

        if (await _segregationOfDuties.DidUserPerformTestAsync(testOrderId, decidedByUserId))
            throw new InvalidOperationException("You cannot approve a test you performed. Approval must be done by a different person.");

        var reviewerId = await _db.WorkflowHistories
            .Where(w => w.TestOrderId == testOrderId && w.ToStep == WorkflowStep.Reviewed)
            .OrderByDescending(w => w.Timestamp)
            .Select(w => (int?)w.PerformedByUserId)
            .FirstOrDefaultAsync();

        if (reviewerId is not null && reviewerId == decidedByUserId)
            throw new InvalidOperationException("You cannot approve a test you reviewed. Approval must be done by a different person.");

        if (decision == ApprovalDecision.Investigation || decision == ApprovalDecision.OOSInvestigation)
        {
            // OOS/Investigation decisions require a documented reason -
            // GMP requirement, not optional even if the UI allows blank
            // comments elsewhere. Checked before signing so a doomed
            // request never even prompts for password verification.
            if (string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException($"{decision} requires a documented comment/justification.");
        }

        var meaning = decision switch
        {
            ApprovalDecision.Approve => SignatureMeaning.Approved,
            ApprovalDecision.Reject => SignatureMeaning.Rejected,
            ApprovalDecision.RetestRetainedSample => SignatureMeaning.RetestRequested,
            ApprovalDecision.NewSampleRequest => SignatureMeaning.RetestRequested,
            ApprovalDecision.Investigation => SignatureMeaning.InvestigationOrdered,
            ApprovalDecision.OOSInvestigation => SignatureMeaning.InvestigationOrdered,
            _ => throw new InvalidOperationException($"Unknown decision '{decision}'.")
        };

        // Signs first - if password verification fails, nothing below is
        // written (the signature and the state change below commit
        // together in the single SaveChangesAsync at the end).
        await _signatureService.SignAsync(decidedByUserId, password, meaning, "TestOrder", testOrderId, comment, ipAddress);

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
