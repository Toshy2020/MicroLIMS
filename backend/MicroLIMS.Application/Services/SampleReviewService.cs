using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Sample review, per laboratory section: each section's tests move through
// UnderReview together once every one of them is done, and are reviewed by
// someone assigned to that section. Sample.Status is rolled up from the
// sections (SampleSectionRollup); a single-section sample behaves exactly as
// the old whole-sample review did.
public class SampleReviewService
{
    private readonly MicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;
    private readonly IUserSectionScopeService _scope;

    public SampleReviewService(MicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate,
        IUserSectionScopeService scope)
    {
        _db = db;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
        _scope = scope;
    }

    private Task<Sample?> LoadAsync(int sampleId) =>
        _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs).FirstOrDefaultAsync(s => s.Id == sampleId);

    // Only starting an incubation moves a sample off Received, so a sample
    // whose finished tests never incubate (Finished Product) is still
    // Received when its results complete - it is being tested all the same.
    private static bool IsBeingTested(Sample sample) =>
        sample.Status is SampleStatus.Received or SampleStatus.InTesting;

    // Sections still in testing whose current tests are all Ready.
    private static List<int> SectionsReadyForReview(Sample sample) =>
        SampleSectionRollup.SectionIds(sample)
            .Where(id => SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.InTesting)
            .Where(id =>
            {
                var orders = SampleSectionRollup.CurrentOrders(sample, id);
                return orders.Count > 0 && orders.All(t => t.CurrentStep == WorkflowStep.Ready);
            })
            .ToList();

    public async Task<bool> CanSubmitForReviewAsync(int sampleId)
    {
        var sample = await LoadAsync(sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (!IsBeingTested(sample)) return false;

        return SectionsReadyForReview(sample).Count > 0;
    }

    // Called at the end of TestWorkflowEngine.RecordResultAsync whenever a
    // result completes a TestOrder's workflow - moves each section to
    // UnderReview the moment every one of its tests is Ready. Queues its
    // changes on the shared DbContext without saving so the caller can
    // commit them in the same transaction as the result that triggered it.
    public async Task AutoSubmitForReviewIfReadyAsync(int sampleId, int triggeredByUserId)
    {
        var sample = await LoadAsync(sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (!IsBeingTested(sample)) return;

        var now = DateTime.UtcNow;
        foreach (var sectionId in SectionsReadyForReview(sample))
        {
            var signoff = SampleSectionRollup.GetOrAdd(sample, sectionId);
            signoff.Status = SectionSignoffStatus.UnderReview;
            signoff.SubmittedForReviewAt = now;

            await _reviewGate.LogEventAsync(
                ReviewEntityTypes.Sample, sampleId, triggeredByUserId,
                ReviewWorkflowEventType.SubmittedForReview,
                "All tests completed - automatically submitted for review", sectionId: sectionId);
        }

        SampleSectionRollup.Apply(sample);
    }

    public async Task CompleteReviewAsync(int sampleId, int reviewerUserId, string password, string? comment, string? ipAddress, int? sectionId = null)
    {
        var sample = await LoadAsync(sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        var userScope = await _scope.GetAccessibleSectionIdsAsync(reviewerUserId);
        var section = SampleSectionRollup.ResolveSection(
            sample, sectionId, SectionSignoffStatus.UnderReview, userScope,
            "Sample must be under review before it can be reviewed.");

        var currentOrders = SampleSectionRollup.CurrentOrders(sample, section);
        foreach (var order in currentOrders)
        {
            if (await _segregationOfDuties.DidUserPerformTestAsync(order.Id, reviewerUserId))
                throw new InvalidOperationException("You cannot review a sample you tested.");
        }

        if (currentOrders.Any(t => t.CurrentStep != WorkflowStep.Ready))
            throw new InvalidOperationException("Cannot complete review: one or more tests are not in Ready step.");

        // Signs first - if password verification fails, nothing below is
        // written (the signature, the event, and the state change below
        // commit together in the single SaveChangesAsync at the end).
        var signature = await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Sample, sampleId, reviewerUserId, password,
            SignatureMeaning.Reviewed, ReviewWorkflowEventType.ReviewCompleted, comment, ipAddress, sectionId: section);

        var now = DateTime.UtcNow;
        var signoff = SampleSectionRollup.GetOrAdd(sample, section);
        signoff.Status = SectionSignoffStatus.UnderApproval;
        signoff.ReviewedByUserId = reviewerUserId;
        signoff.ReviewedAt = now;
        signoff.ReviewSignature = signature;

        // Kept for everything that still reads the sample-level fields: the
        // most recent review of any section.
        sample.ReviewedByUserId = reviewerUserId;
        sample.ReviewedAt = now;

        foreach (var order in currentOrders)
        {
            var fromStep = order.CurrentStep;
            order.Status = ApprovalStatus.Reviewed;
            order.CurrentStep = WorkflowStep.Reviewed;

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = order.Id,
                FromStep = fromStep,
                ToStep = WorkflowStep.Reviewed,
                Note = $"Sample review completed by {signature.UserFullNameSnapshot}",
                PerformedByUserId = reviewerUserId
            });
        }

        SampleSectionRollup.Apply(sample);
        await _db.SaveChangesAsync();
    }
}
