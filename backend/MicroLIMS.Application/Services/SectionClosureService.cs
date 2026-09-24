using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// After one laboratory rejects a sample, another laboratory may stop its
// own work instead of finishing it. Its open tests are cancelled - not
// rejected, nothing was judged - each keeping the step it had reached.
public class SectionClosureService
{
    private readonly MicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;
    private readonly IUserSectionScopeService _scope;
    private readonly SampleApprovalService _approval;

    public SectionClosureService(MicroLimsDbContext db, ReviewGateService reviewGate, IUserSectionScopeService scope, SampleApprovalService approval)
    {
        _db = db;
        _reviewGate = reviewGate;
        _scope = scope;
        _approval = approval;
    }

    public async Task CloseTestingAsync(int sampleId, int sectionId, int sectionHeadUserId, string password, string reason, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to close testing.");

        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        var userScope = await _scope.GetAccessibleSectionIdsAsync(sectionHeadUserId);
        if (userScope is not null && !userScope.Contains(sectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        if (!SampleSectionRollup.SectionIds(sample).Contains(sectionId))
            throw new InvalidOperationException("This sample has no tests in the selected laboratory section.");

        var otherRejected = SampleSectionRollup.SectionIds(sample)
            .Any(id => id != sectionId && SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.Rejected);
        if (!otherRejected)
            throw new InvalidOperationException("Testing can be closed only after another laboratory rejected the sample.");

        var status = SampleSectionRollup.StatusOf(sample, sectionId);
        if (status == SectionSignoffStatus.Cancelled)
            throw new InvalidOperationException("This laboratory's testing is already closed.");
        if (!SampleSectionRollup.IsOpen(status))
            throw new InvalidOperationException("This laboratory has no open tests on the sample.");

        SampleSectionRollup.FreezeOpenSections(sample);

        var signature = await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Sample, sampleId, sectionHeadUserId, password,
            SignatureMeaning.TestingClosed, ReviewWorkflowEventType.SectionTestingClosed,
            reason.Trim(), ipAddress, null, sectionId);

        var orderIds = SampleSectionRollup.CurrentOrders(sample, sectionId).Select(o => o.Id).ToList();
        var stageByOrder = await _db.Incubations
            .Where(i => i.TestOrderId != null && orderIds.Contains(i.TestOrderId.Value))
            .GroupBy(i => i.TestOrderId!.Value)
            .Select(g => new { g.Key, Stage = g.Max(i => i.StageNumber) })
            .ToDictionaryAsync(x => x.Key, x => x.Stage);

        foreach (var order in SampleSectionRollup.CurrentOrders(sample, sectionId))
        {
            if (order.Status is ApprovalStatus.Approved or ApprovalStatus.Voided or ApprovalStatus.Cancelled) continue;
            order.CancelledAtStep = order.CurrentStep;
            order.CancelledAtStage = stageByOrder.TryGetValue(order.Id, out var stage) ? stage : null;
            order.Status = ApprovalStatus.Cancelled;
            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = order.Id,
                FromStep = order.CurrentStep,
                ToStep = order.CurrentStep,
                Note = $"Testing closed by {signature.UserFullNameSnapshot}: {reason.Trim()}",
                PerformedByUserId = sectionHeadUserId
            });
        }

        var signoff = SampleSectionRollup.GetOrAdd(sample, sectionId);
        signoff.Status = SectionSignoffStatus.Cancelled;
        signoff.ClosedByUserId = sectionHeadUserId;
        signoff.ClosedAt = DateTime.UtcNow;
        signoff.CloseReason = reason.Trim();
        signoff.CloseSignature = signature;
        SampleSectionRollup.Apply(sample);

        await _db.SaveChangesAsync();
        await _approval.FinalizeIfClosedAsync(sample, "Laboratory testing closed", sectionHeadUserId);
    }
}
