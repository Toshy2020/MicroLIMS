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
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public SampleReviewService(IMicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate,
        IUserSectionScopeService scope, IAuditEventService audit,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
        _scope = scope;
        _audit = audit;
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

    public Task CompleteReviewAsync(int sampleId, int reviewerUserId, string password, string? comment, string? ipAddress, int? sectionId = null, decimal? retentionQuantity = null, string? retentionUnit = null) =>
        UnitOfWork.RunAsync(_db, () => CompleteReviewCoreAsync(sampleId, reviewerUserId, password, comment, ipAddress, sectionId, retentionQuantity, retentionUnit));

    private async Task CompleteReviewCoreAsync(int sampleId, int reviewerUserId, string password, string? comment, string? ipAddress, int? sectionId = null, decimal? retentionQuantity = null, string? retentionUnit = null)
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
                throw new BusinessRuleException("You cannot review a sample you tested.");
        }

        if (currentOrders.Any(t => t.CurrentStep != WorkflowStep.Ready))
            throw new BusinessRuleException("Cannot complete review: one or more tests are not in Ready step.");

        // Checked before anything is signed or written.
        var retentionUnitCanonical = await ValidateRetentionAsync(sample, section, retentionQuantity, retentionUnit);

        _db.CurrentUserId = reviewerUserId;

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

        if (retentionUnitCanonical is not null)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var unchanged = sample.RetentionQuantity == retentionQuantity && sample.RetentionQuantityUnit == retentionUnitCanonical;
            var previous = sample.RetentionQuantity is null ? null : string.Create(inv, $"{sample.RetentionQuantity:0.######} {sample.RetentionQuantityUnit}");
            sample.RetentionQuantity = retentionQuantity;
            sample.RetentionQuantityUnit = retentionUnitCanonical;
            if (!unchanged)
                await _audit.RecordUserEventAsync("RetentionQuantityRecorded", AuditActionCategory.Review, ReviewEntityTypes.Sample,
                    reason: "Retention quantity recorded at physicochemical review",
                    changes: new[] { new AuditFieldChange("RetentionQuantity", previous, string.Create(inv, $"{retentionQuantity:0.######} {retentionUnitCanonical}")) },
                    entityId: sampleId.ToString(), sampleId: sampleId);
        }

        SampleSectionRollup.Apply(sample);
        await _db.SaveChangesAsync();
    }

    private static readonly string[] RetentionUnits = { "g", "mg", "kg", "mL", "L" };

    // Retention is kept and recorded only by the physicochemical (FP) reviewer of a
    // raw material. Returns the unit in canonical spelling, or null when none applies.
    private async Task<string?> ValidateRetentionAsync(Sample sample, int sectionId, decimal? quantity, string? unit)
    {
        var sectionCode = await _db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.Code).FirstOrDefaultAsync();
        var required = sample.Category == SampleCategory.RawMaterial && string.Equals(sectionCode, PhyschemAreas.SectionCode, StringComparison.OrdinalIgnoreCase);
        if (!required)
        {
            if (quantity is not null || !string.IsNullOrWhiteSpace(unit))
                throw new BusinessRuleException("Retention quantity is recorded only at the physicochemical review of a raw material.");
            return null;
        }

        if (quantity is null) throw new BusinessRuleException("Enter the retention quantity kept for this raw material.");
        if (quantity <= 0) throw new BusinessRuleException("Retention quantity must be greater than zero.");
        if (quantity >= 1_000_000m) throw new BusinessRuleException("Retention quantity must be less than 1,000,000.");
        return RetentionUnits.FirstOrDefault(u => string.Equals(u, unit?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Retention unit must be g, mg, kg, mL or L.");
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
            throw new BusinessRuleException("Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead.");
        }

        if (order.Status != ApprovalStatus.ResultEntered)
            throw new BusinessRuleException($"Cannot return a test order in {order.Status} status. Only test orders in ResultEntered status can be returned to the analyst.");

        var definition = await _db.TestDefinitions.FirstOrDefaultAsync(d => d.Code == order.TestCode)
            ?? throw new BusinessRuleException($"Test definition \"{order.TestCode}\" not found.");

        if (definition.WorkflowType != WorkflowType.CountTest && !AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
            throw new BusinessRuleException($"Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{order.TestCode}\" is a {definition.WorkflowType} workflow.");

        // A moisture (LOD / water) result is read by the dried / anhydrous assays of the same sample:
        // returning it returns every assay that used it, in the same unit of work. Found first - the
        // state transition below saves, and the moisture results are inactive from then on.
        // The moisture order's row is locked first, so an assay being recorded against this result either
        // commits before the scan below (and is returned) or waits and then finds the result returned.
        var dependents = new List<TestOrder>();
        if (definition.MoistureRole != MoistureRole.None)
        {
            await SampleMoistureResolver.LockMoistureOrderAsync(_db, order.Id);
            dependents = await FindDependentAssaysAsync(order);
            foreach (var dependent in dependents)
            {
                var dependentSection = sample is null ? SectionSignoffStatus.InTesting : SampleSectionRollup.StatusOf(sample, dependent.SectionId);
                if (dependent.Status != ApprovalStatus.ResultEntered
                    || dependentSection is not (SectionSignoffStatus.InTesting or SectionSignoffStatus.UnderReview))
                    throw new BusinessRuleException($"Cannot return {order.TestCode}: assay {dependent.TestCode} has already been reviewed.");
            }
        }

        var returnEvent = await ReturnOrderAsync(order, definition, sample, sectionStatus, reviewerId, reason);

        var phrase = definition.MoistureRole == MoistureRole.LossOnDrying ? "loss on drying" : "water content";
        foreach (var dependent in dependents)
        {
            var dependentDefinition = await _db.TestDefinitions.FirstAsync(d => d.Code == dependent.TestCode);
            await ReturnOrderAsync(dependent, dependentDefinition, sample, null, reviewerId, $"Recalculate: the {phrase} result was returned.");
        }

        await _db.SaveChangesAsync();

        return returnEvent;
    }

    // Live (not cancelled, voided or superseded) titration, HPLC and UV orders of the same sample, in any status, whose
    // active analysis took its moisture value from one of this order's active results. The caller refuses
    // the return when one is already past ResultEntered. Must run before those results are superseded.
    private async Task<List<TestOrder>> FindDependentAssaysAsync(TestOrder moistureOrder)
    {
        var resultIds = await _db.ParameterResults.Where(r => r.TestOrderId == moistureOrder.Id && r.IsActive).Select(r => r.Id).ToListAsync();
        var candidates = await _db.TestAnalyses
            .Where(a => a.IsActive && (a.AnalysisType == WorkflowType.Titration || a.AnalysisType == WorkflowType.HplcMethodAssay
                || a.AnalysisType == WorkflowType.UvSpectrophotometry) && a.TestOrderId != moistureOrder.Id
                && a.TestOrder!.SampleId == moistureOrder.SampleId && !a.TestOrder.IsSuperseded
                && a.TestOrder.CancelledAtStep == null && a.TestOrder.Status != ApprovalStatus.Cancelled && a.TestOrder.Status != ApprovalStatus.Voided)
            .Select(a => new { a.TestOrder, a.ConditionsJson })
            .ToListAsync();
        return candidates
            .Where(c => UsesMoistureResult(c.ConditionsJson, resultIds))
            .Select(c => c.TestOrder!)
            .Distinct()
            .ToList();
    }

    private static bool UsesMoistureResult(string? conditionsJson, List<int> resultIds)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson)) return false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(conditionsJson);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("moisture", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Object
                && m.TryGetProperty("parameterResultId", out var id) && id.TryGetInt32(out var value) && resultIds.Contains(value);
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    // Steps 1-5 of a return for one order (queued on the context; the caller saves).
    private async Task<TestReturnEvent> ReturnOrderAsync(
        TestOrder order, TestDefinition definition, Sample? sample, SectionSignoffStatus? knownSectionStatus, int reviewerId, string? reason)
    {
        var testOrderId = order.Id;
        var sectionStatus = knownSectionStatus ?? (sample is null ? SectionSignoffStatus.InTesting : SampleSectionRollup.StatusOf(sample, order.SectionId));
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

        return returnEvent;
    }
}
