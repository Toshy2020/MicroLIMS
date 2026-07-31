using Microsoft.EntityFrameworkCore;
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

    public ReviewService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task MarkReviewedAsync(int testOrderId, int reviewerId, string? comment, ReviewMode mode = ReviewMode.Detailed)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        if (order.Status != ApprovalStatus.ResultEntered)
            throw new InvalidOperationException("Cannot review a test order before results are entered.");

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
    public async Task<List<int>> QuickReviewBatchAsync(List<int> testOrderIds, int reviewerId)
    {
        var reviewed = new List<int>();
        foreach (var id in testOrderIds)
        {
            try
            {
                await MarkReviewedAsync(id, reviewerId, "Quick table review", ReviewMode.QuickTable);
                reviewed.Add(id);
            }
            catch (InvalidOperationException)
            {
                // Skip test orders that aren't eligible (e.g. still Waiting) -
                // caller can inspect the difference between requested and returned IDs.
            }
        }
        return reviewed;
    }
}
