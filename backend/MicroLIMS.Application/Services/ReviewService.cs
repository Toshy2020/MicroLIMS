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

        if (order.Status != ApprovalStatus.ResultEntered)
            throw new InvalidOperationException($"Cannot return a test order in {order.Status} status. Only test orders in ResultEntered status can be returned to the analyst.");

        var definition = await _db.TestDefinitions.FirstOrDefaultAsync(d => d.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");

        if (definition.WorkflowType != WorkflowType.CountTest)
            throw new InvalidOperationException($"Return to Analyst is only supported for Count Test workflows. \"{order.TestCode}\" is a {definition.WorkflowType} workflow.");

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

        // 3. If parent sample was auto-submitted for review, revert it to InTesting
        var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == order.SampleId);
        if (sample != null && sample.Status == SampleStatus.UnderReview)
        {
            sample.Status = SampleStatus.InTesting;
        }

        // 4. Revert TestOrder state back to Incubating (keeps AssignedAnalystId unchanged)
        var transitionNote = string.IsNullOrWhiteSpace(reason)
            ? "Returned to analyst by reviewer"
            : $"Returned to analyst: {reason.Trim()}";

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Incubating, reviewerId, transitionNote);

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
