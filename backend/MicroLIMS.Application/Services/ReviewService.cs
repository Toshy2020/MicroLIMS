using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
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
}
