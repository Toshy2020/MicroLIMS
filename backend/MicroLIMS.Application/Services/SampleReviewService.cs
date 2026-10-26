using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Services;

// Sample review, per laboratory section: each section's tests move through
// UnderReview together once every one of them is done, and are reviewed by
// someone assigned to that section. Sample.Status is rolled up from the
// sections (SampleSectionRollup); a single-section sample behaves exactly as
// the old whole-sample review did.
public class SampleReviewService
{
    private readonly IMicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public SampleReviewService(IMicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate,
        IUserSectionScopeService scope,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
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
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

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
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

        if (!IsBeingTested(sample)) return;

        var now = _time.GetUtcNow().UtcDateTime;
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

    public Task CompleteReviewAsync(int sampleId, int reviewerUserId, string password, string? comment, string? ipAddress, int? sectionId = null) =>
        UnitOfWork.RunAsync(_db, () => CompleteReviewCoreAsync(sampleId, reviewerUserId, password, comment, ipAddress, sectionId));

    private async Task CompleteReviewCoreAsync(int sampleId, int reviewerUserId, string password, string? comment, string? ipAddress, int? sectionId = null)
    {
        var sample = await LoadAsync(sampleId)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

        var userScope = await _scope.GetMemberSectionIdsAsync(reviewerUserId);
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

        var now = _time.GetUtcNow().UtcDateTime;
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

    public Task<TestReturnEventResponse> ReturnToAnalystAsync(int testOrderId, int reviewerId, string? reason) =>
        UnitOfWork.RunAsync(_db, async () => TestReturnEventResponse.From(await ReturnToAnalystCoreAsync(testOrderId, reviewerId, reason)));

    private async Task<TestReturnEvent> ReturnToAnalystCoreAsync(int testOrderId, int reviewerId, string? reason)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");

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

        if (definition.WorkflowType != WorkflowType.CountTest && !AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
            throw new InvalidOperationException($"Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{order.TestCode}\" is a {definition.WorkflowType} workflow.");

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
        else if (AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
        {
            // 1. Soft-supersede all active TestAnalysis rows for this test order
            var activeAnalyses = await _db.TestAnalyses
                .Where(e => e.TestOrderId == testOrderId && e.IsActive)
                .ToListAsync();
            foreach (var e in activeAnalyses)
            {
                e.IsActive = false;
            }

            // 2. Soft-supersede all active ParameterResult rows for this test order
            var activeResults = await _db.ParameterResults
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

        var targetStep = AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType)
            ? WorkflowStep.Running
            : WorkflowStep.Incubating;

        await WorkflowStateMachine.TransitionAsync(_db, order, targetStep, reviewerId, transitionNote);

        // 5. Create distinct queryable audit record for Return to Analyst event
        var returnEvent = new TestReturnEvent
        {
            TestOrderId = testOrderId,
            ReviewerUserId = reviewerId,
            AssignedAnalystId = order.AssignedAnalystId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReturnedAt = _time.GetUtcNow().UtcDateTime
        };

        _db.TestReturnEvents.Add(returnEvent);
        await _db.SaveChangesAsync();

        return returnEvent;
    }
}
