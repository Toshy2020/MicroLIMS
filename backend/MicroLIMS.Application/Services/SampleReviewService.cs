using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Sample-level review, replacing the per-TestOrder ReviewService for the
// main review flow: the whole Sample moves through UnderReview as one
// unit once every test on it is done, instead of each TestOrder being
// reviewed independently.
public class SampleReviewService
{
    private readonly MicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;

    public SampleReviewService(MicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate)
    {
        _db = db;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
    }

    public async Task<bool> CanSubmitForReviewAsync(int sampleId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.Status != SampleStatus.InTesting) return false;

        var currentOrders = sample.TestOrders.Where(t => !t.IsSuperseded).ToList();
        return currentOrders.Count > 0 && currentOrders.All(t => t.CurrentStep == WorkflowStep.Ready);
    }

    // Called at the end of TestWorkflowEngine.RecordResultAsync whenever a
    // result completes a TestOrder's workflow - flips the Sample to
    // UnderReview the moment every test on it is Ready. Queues its
    // changes on the shared DbContext without saving so the caller can
    // commit them in the same transaction as the result that triggered it.
    public async Task AutoSubmitForReviewIfReadyAsync(int sampleId, int triggeredByUserId)
    {
        if (!await CanSubmitForReviewAsync(sampleId)) return;

        var sample = await _db.Samples.FirstAsync(s => s.Id == sampleId);
        sample.Status = SampleStatus.UnderReview;

        await _reviewGate.LogEventAsync(
            ReviewEntityTypes.Sample, sampleId, triggeredByUserId,
            ReviewWorkflowEventType.SubmittedForReview,
            "All tests completed - automatically submitted for review");
    }

    public async Task CompleteReviewAsync(int sampleId, int reviewerUserId, string password, string? comment, string? ipAddress)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.Status != SampleStatus.UnderReview)
            throw new InvalidOperationException("Sample must be under review before it can be reviewed.");

        foreach (var order in sample.TestOrders.Where(t => !t.IsSuperseded))
        {
            if (await _segregationOfDuties.DidUserPerformTestAsync(order.Id, reviewerUserId))
                throw new InvalidOperationException("You cannot review a sample you tested.");
        }

        // Signs first - if password verification fails, nothing below is
        // written (the signature, the event, and the state change below
        // commit together in the single SaveChangesAsync at the end).
        await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Sample, sampleId, reviewerUserId, password,
            SignatureMeaning.Reviewed, ReviewWorkflowEventType.ReviewCompleted, comment, ipAddress);

        sample.Status = SampleStatus.UnderApproval;
        sample.ReviewedByUserId = reviewerUserId;
        sample.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
