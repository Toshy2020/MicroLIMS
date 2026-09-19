using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public enum ReviewMode { Detailed, QuickTable }

// Reviewer chooses: Detailed workflow (opens full workflow history +
// individual observations/incubations) OR Quick table review (approves
// a batch of straightforward results from a single grid).
public class ReviewService
{
    private readonly MicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly IElectronicSignatureService _signatureService;

    public ReviewService(MicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties, IElectronicSignatureService signatureService)
    {
        _db = db;
        _segregationOfDuties = segregationOfDuties;
        _signatureService = signatureService;
    }

    public async Task MarkReviewedAsync(int testOrderId, int reviewerId, string? comment, string password, string? ipAddress, ReviewMode mode = ReviewMode.Detailed)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        if (order.Status != ApprovalStatus.ResultEntered)
            throw new InvalidOperationException("Cannot review a test order before results are entered.");

        if (await _segregationOfDuties.DidUserPerformTestAsync(testOrderId, reviewerId))
            throw new InvalidOperationException("You cannot review a test you performed. Review must be done by a different person.");

        // Signs first - if password verification fails, nothing below is
        // written (the signature and the state change below commit
        // together in the single SaveChangesAsync at the end).
        await _signatureService.SignAsync(reviewerId, password, SignatureMeaning.Reviewed, "TestOrder", testOrderId, comment, ipAddress);

        order.Status = ApprovalStatus.Reviewed;
        order.CurrentStep = WorkflowStep.Reviewed;

        _db.WorkflowHistories.Add(new Domain.Entities.WorkflowHistory
        {
            TestOrderId = testOrderId,
            FromStep = WorkflowStep.Ready,
            ToStep = WorkflowStep.Reviewed,
            Note = $"[{mode}] {comment}",
            PerformedByUserId = reviewerId
        });

        await _db.SaveChangesAsync();
    }

    // Quick table review: review many test orders in one action, as long
    // as none of them require the detailed workflow (e.g. no OOT/OOS flags).
    // Ineligible orders (already reviewed, results not entered, a
    // segregation-of-duties violation, or a failed signature) are
    // skipped rather than aborting the whole batch - but the caller must
    // be told exactly which ones were skipped and why, so a reviewer
    // can't be silently short of their own test without knowing it.
    public async Task<QuickReviewBatchResult> QuickReviewBatchAsync(List<int> testOrderIds, int reviewerId, string password, string? ipAddress)
    {
        var reviewed = new List<int>();
        var skipped = new List<SkippedReview>();
        foreach (var id in testOrderIds)
        {
            try
            {
                await MarkReviewedAsync(id, reviewerId, "Quick table review", password, ipAddress, ReviewMode.QuickTable);
                reviewed.Add(id);
            }
            catch (InvalidOperationException ex)
            {
                skipped.Add(new SkippedReview(id, ex.Message));
            }
        }
        return new QuickReviewBatchResult(reviewed, skipped);
    }

    public async Task<TestReturnEvent> ReturnToAnalystAsync(int testOrderId, int reviewerId, string? reason)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == order.SampleId);
        // Review is per section: this test's own section must not have been
        // reviewed yet. A sample that is past review as a whole (every
        // section is, then) or closed is refused outright.
        var sectionStatus = sample is null ? SectionSignoffStatus.InTesting : SampleSectionRollup.StatusOf(sample, order.SectionId);
        if (sample != null && (sample.Status is SampleStatus.UnderApproval
            or SampleStatus.Approved
            or SampleStatus.Rejected
            or SampleStatus.RetestRequested
            or SampleStatus.Cancelled
            or SampleStatus.Voided
            || sectionStatus is not (SectionSignoffStatus.InTesting or SectionSignoffStatus.UnderReview)))
        {
            throw new InvalidOperationException("Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead.");
        }

        if (order.Status != ApprovalStatus.ResultEntered)
            throw new InvalidOperationException($"Cannot return a test order in {order.Status} status. Only test orders in ResultEntered status can be returned to the analyst.");

        var definition = await _db.TestDefinitions.FirstOrDefaultAsync(d => d.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");

        if (definition.WorkflowType != WorkflowType.CountTest && definition.WorkflowType != WorkflowType.HplcAssay && definition.WorkflowType != WorkflowType.ElementalAssay)
            throw new InvalidOperationException($"Return to Analyst is only supported for Count Test, HPLC Assay, and Elemental Assay workflows. \"{order.TestCode}\" is a {definition.WorkflowType} workflow.");

        if (definition.WorkflowType == WorkflowType.CountTest)
        {
            // 1. Soft-supersede all active CountTestReading rows for this test order
            var activeReadings = await _db.CountTestReadings
                .Where(r => r.TestOrderId == testOrderId && r.IsActive)
                .ToListAsync();
            foreach (var r in activeReadings)
            {
                r.IsActive = false;
            }

            // 2. Reopen the closed Incubation row for this count test step
            var latestIncubation = await _db.Incubations
                .Where(i => i.TestOrderId == testOrderId)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();

            if (latestIncubation != null)
            {
                latestIncubation.CompletedAt = null;
                latestIncubation.CompletedByUserId = null;
                latestIncubation.Outcome = null;
            }
        }
        else if (definition.WorkflowType == WorkflowType.HplcAssay)
        {
            // 1. Soft-supersede all active HplcAssayResult rows for this test order
            var activeHplcResults = await _db.HplcAssayResults
                .Where(r => r.TestOrderId == testOrderId && r.IsActive)
                .ToListAsync();
            foreach (var r in activeHplcResults)
            {
                r.IsActive = false;
            }
        }
        else if (definition.WorkflowType == WorkflowType.ElementalAssay)
        {
            // 1. Soft-supersede all active ElementalAssayEntry rows for this test order
            var activeEntries = await _db.ElementalAssayEntries
                .Where(e => e.TestOrderId == testOrderId && e.IsActive)
                .ToListAsync();
            foreach (var e in activeEntries)
            {
                e.IsActive = false;
            }

            // 2. Soft-supersede all active ElementalAssayResult rows for this test order
            var activeResults = await _db.ElementalAssayResults
                .Where(r => r.TestOrderId == testOrderId && r.IsActive)
                .ToListAsync();
            foreach (var r in activeResults)
            {
                r.IsActive = false;
            }
        }

        // 3. If this test's section was auto-submitted for review, send the
        // section back to testing and roll the sample status up again
        if (sample != null && sectionStatus == SectionSignoffStatus.UnderReview)
        {
            SampleSectionRollup.GetOrAdd(sample, order.SectionId).Status = SectionSignoffStatus.InTesting;
            SampleSectionRollup.Apply(sample);
        }

        // 4. Revert TestOrder state back to Incubating/Running (keeps AssignedAnalystId unchanged)
        var transitionNote = string.IsNullOrWhiteSpace(reason)
            ? "Returned to analyst by reviewer"
            : $"Returned to analyst: {reason.Trim()}";

        var targetStep = (definition.WorkflowType == WorkflowType.HplcAssay || definition.WorkflowType == WorkflowType.ElementalAssay)
            ? WorkflowStep.Running
            : WorkflowStep.Incubating;

        await WorkflowStateMachine.TransitionAsync(_db, order, targetStep, reviewerId, transitionNote);

        // 5. Create distinct queryable audit record for Return to Analyst event
        var returnEvent = new Domain.Entities.TestReturnEvent
        {
            TestOrderId = testOrderId,
            ReviewerUserId = reviewerId,
            AssignedAnalystId = order.AssignedAnalystId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReturnedAt = DateTime.UtcNow
        };

        _db.TestReturnEvents.Add(returnEvent);
        await _db.SaveChangesAsync();

        return returnEvent;
    }
}
