using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Sample approval, per laboratory section: a Section Head decides once for
// their section's tests on the sample (SampleSectionSignoff), and
// Sample.Status is rolled up from the sections (SampleSectionRollup) - a
// single-section sample behaves exactly as the old whole-sample approval
// did. Rejecting rejects the whole sample; a retest carries only the
// deciding section's tests. Only 4 of ApprovalDecision's 6 values are
// reachable here - Investigation/OOSInvestigation stay TestOrder-level-only
// (ApprovalService).
public class SampleApprovalService
{
    private readonly MicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;
    private readonly SampleSummaryService _summary;
    private readonly RecordArchiveService _archive;
    private readonly ResultProjectionService _resultProjection;
    private readonly ReferenceNumberGenerator _refNumbers;
    private readonly IUserSectionScopeService _scope;

    public SampleApprovalService(MicroLimsDbContext db, ReviewGateService reviewGate,
        SampleSummaryService summary, RecordArchiveService archive, ResultProjectionService resultProjection,
        ReferenceNumberGenerator refNumbers, IUserSectionScopeService scope)
    {
        _scope = scope;
        _db = db;
        _reviewGate = reviewGate;
        _summary = summary;
        _archive = archive;
        _resultProjection = resultProjection;
        _refNumbers = refNumbers;
    }

    // Copies the physical/identity attributes of a sample onto a fresh OOS
    // retest spin-off - deliberately does NOT copy SamplePreparation (the
    // new sample "starts fresh": NeedsPreparation, no prep row) or
    // TestOrders (the caller adds only the tests actually selected for
    // retest). OriginSampleId is the only link back to the sample whose
    // approval decision created it.
    private static Sample BuildRetestSample(Sample original, string referenceNumber, int causeOfTestingId, int receivedByUserId, string oosGroupCode)
    {
        return new Sample
        {
            ReferenceNumber = referenceNumber,
            Category = original.Category,
            ItemId = original.ItemId,
            WaterSamplingPointId = original.WaterSamplingPointId,
            DepartmentId = original.DepartmentId,
            MachineId = original.MachineId,
            PreviousProductName = original.PreviousProductName,
            PreviousProductBatchNumber = original.PreviousProductBatchNumber,
            WaterDepartmentId = original.WaterDepartmentId,
            ProductionStage = original.ProductionStage,
            CauseOfTestingId = causeOfTestingId,
            SampleQuantity = original.SampleQuantity,
            SampledBy = original.SampledBy,
            BatchNumber = original.BatchNumber,
            ControlNumber = original.ControlNumber,
            MfgDate = original.MfgDate,
            ExpDate = original.ExpDate,
            ReceivedByUserId = receivedByUserId,
            ReceivedAt = DateTime.UtcNow,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation,
            OriginSampleId = original.Id,
            OosGroupCode = oosGroupCode
        };
    }

    // Water/EM/After Cleaning TestOrders are batches of per-location
    // results (SampleLocation rows), not one result per order. A retest
    // must only ever re-run the locations that actually failed on the
    // original order - carrying over every assigned location (or worse,
    // letting the analyst re-pick from the whole department/machine/room
    // catalog in the Preparation screen) would silently retest points
    // that already conformed. Returns true if any location was cloned
    // (signals the caller to skip the Preparation screen for this order's
    // sample, since it now already has everything it needs).
    private async Task<bool> CloneFailedLocationsAsync(SampleCategory category, TestOrder originalOrder, Sample newSample, TestOrder newOrder)
    {
        if (category is not (SampleCategory.Water or SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning))
            return false;

        var originalLocations = await _db.SampleLocations.AsNoTracking()
            .Where(l => l.TestOrderId == originalOrder.Id)
            .ToListAsync();

        if (originalLocations.Count == 0)
            return false;

        // Conforming statuses mirror DetermineOwnResultConformanceAsync
        // below. If none of the original locations actually failed (e.g.
        // the order was sent to retest for a procedural/OOS reason rather
        // than a location result), fall back to carrying every original
        // location rather than creating a TestOrder with no locations at
        // all - an empty batch order can't be tested.
        var failedLocations = originalLocations.Where(l => l.Status is not ("WithinLimits" or "Absent")).ToList();
        var locationsToClone = failedLocations.Count > 0 ? failedLocations : originalLocations;

        foreach (var loc in locationsToClone)
        {
            newSample.Locations.Add(new SampleLocation
            {
                TestOrder = newOrder,
                LocationType = loc.LocationType,
                RoomTestConfigurationId = loc.RoomTestConfigurationId,
                MachinePartConfigurationId = loc.MachinePartConfigurationId,
                WaterSamplingPointId = loc.WaterSamplingPointId,
                SamplingConfigurationId = loc.SamplingConfigurationId
                // Deliberately not copying DilutionFactor/CFUResult/
                // CalculatedResult/ReportedResult/AlertLimit/ActionLimit/
                // SpecLimit/Status/Unit/RawReadings/EnteredAt/
                // EnteredByUserId - this is a fresh, unread location on the
                // retest order; limits get freshly snapshotted when the
                // analyst records the result, same as any normal batch order.
            });
        }

        return true;
    }

    public async Task DecideAsync(
        int sampleId, int sectionHeadUserId, string password, ApprovalDecision decision,
        string? comment, string? ipAddress, string? certificateRemarks = null,
        List<int>? selectedTestOrderIds = null, int? newSampleAnalystOneId = null, int? newSampleAnalystTwoId = null,
        int? sectionId = null)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        var userScope = await _scope.GetAccessibleSectionIdsAsync(sectionHeadUserId);
        var section = SampleSectionRollup.ResolveSection(
            sample, sectionId, SectionSignoffStatus.UnderApproval, userScope,
            "Sample must be under approval before a decision can be made.");
        var signoff = SampleSectionRollup.GetOrAdd(sample, section);
        // Every section without its own sign-off row shares the sample's
        // state; freeze them now so a lab still in testing/review doesn't
        // later read as sharing this decision's own outcome.
        SampleSectionRollup.FreezeOpenSections(sample);

        if (sectionHeadUserId == signoff.ReviewedByUserId)
            throw new InvalidOperationException("You cannot approve a sample you reviewed.");

        // Only this section's tests are decided here.
        var currentOrders = SampleSectionRollup.CurrentOrders(sample, section);
        foreach (var order in currentOrders)
        {
            if (order.AssignedAnalystId == sectionHeadUserId)
                throw new InvalidOperationException("You cannot approve a sample you tested.");

            var enteredResult = await _db.Results.AnyAsync(r => r.TestOrderId == order.Id && r.EnteredByUserId == sectionHeadUserId)
                || await _db.CountTestReadings.AnyAsync(r => r.TestOrderId == order.Id && r.EnteredByUserId == sectionHeadUserId)
                || await _db.PathogenObservations.AnyAsync(p => p.TestOrderId == order.Id && p.ObservedByUserId == sectionHeadUserId)
                || await _db.TestAnalyses.AnyAsync(e => e.TestOrderId == order.Id && e.EnteredByUserId == sectionHeadUserId);
            if (enteredResult)
                throw new InvalidOperationException("You cannot approve a sample you tested.");
        }

        // REQ-FP-033: server-side reject approval of a section if any test in it
        // that requires system suitability lacks a linked PASSED run.
        if (decision == ApprovalDecision.Approve)
        {
            var testCodes = currentOrders.Select(o => o.TestCode).Distinct().ToList();
            var sstCodes = await _db.TestDefinitions
                .Where(t => testCodes.Contains(t.Code) && t.RequiresSystemSuitability)
                .Select(t => t.Code)
                .ToListAsync();

            foreach (var sstOrder in currentOrders.Where(o => sstCodes.Contains(o.TestCode)))
            {
                if (sstOrder.SystemSuitabilityRunId is null)
                {
                    throw new InvalidOperationException(
                        $"Cannot approve section: test order {sstOrder.Id} (\"{sstOrder.TestCode}\") lacks a linked system suitability run.");
                }

                var run = await _db.SystemSuitabilityRuns
                    .FirstOrDefaultAsync(r => r.Id == sstOrder.SystemSuitabilityRunId.Value);

                if (run is null || !run.Passed)
                {
                    throw new InvalidOperationException(
                        $"Cannot approve section: test order {sstOrder.Id} (\"{sstOrder.TestCode}\") lacks a linked passed system suitability run.");
                }
            }

            var analysisDefinitions = await _db.TestDefinitions
                .Where(t => testCodes.Contains(t.Code))
                .Select(t => new { t.Code, t.WorkflowType })
                .ToListAsync();

            var testAnalysisCodes = analysisDefinitions
                .Where(t => AnalysisWorkflows.UsesTestAnalysis(t.WorkflowType))
                .ToDictionary(t => t.Code, t => t.WorkflowType);

            if (testAnalysisCodes.Count > 0)
            {
                var analysisOrders = currentOrders.Where(o => testAnalysisCodes.ContainsKey(o.TestCode)).ToList();
                foreach (var analysisOrder in analysisOrders)
                {
                    var activeAnalyses = await _db.TestAnalyses
                        .Include(e => e.ParameterResults)
                        .Where(e => e.TestOrderId == analysisOrder.Id && e.IsActive)
                        .ToListAsync();

                    if (activeAnalyses.Count == 0)
                    {
                        var wfType = testAnalysisCodes[analysisOrder.TestCode];
                        var displayName = AnalysisWorkflows.GetDisplayName(wfType);
                        throw new InvalidOperationException(
                            $"Cannot approve section: test order {analysisOrder.Id} (\"{analysisOrder.TestCode}\") lacks an active {displayName} entry.");
                    }

                    if (activeAnalyses.Any(e => e.ParameterResults.Any(pr => pr.ComparisonStatus == "NextStageRequired")))
                    {
                        throw new InvalidOperationException(
                            $"Cannot approve section: test order {analysisOrder.Id} (\"{analysisOrder.TestCode}\") has a pending stage.");
                    }
                }
            }
        }

        // Retest paths need the Section Head's chosen subset of tests -
        // validated against this sample's own current TestOrders so a
        // stale/foreign TestOrderId can never be smuggled in.
        var selectedOrders = new List<TestOrder>();
        if (decision is ApprovalDecision.RetestRetainedSample or ApprovalDecision.NewSampleRequest)
        {
            if (selectedTestOrderIds is null || selectedTestOrderIds.Count == 0)
                throw new InvalidOperationException("At least one test must be selected for retest.");

            var distinctIds = selectedTestOrderIds.Distinct().ToList();
            selectedOrders = currentOrders.Where(o => distinctIds.Contains(o.Id)).ToList();
            if (selectedOrders.Count != distinctIds.Count)
                throw new InvalidOperationException("One or more selected tests do not belong to this sample's current test orders.");
        }

        // NewSampleRequest spins up two independent samples that must go
        // to two different analysts, and neither may be whoever already
        // tested the original sample - the same segregation-of-duties
        // reasoning as the approver/reviewer/tester checks above.
        List<User> newAnalysts = new();
        if (decision == ApprovalDecision.NewSampleRequest)
        {
            if (newSampleAnalystOneId is null || newSampleAnalystTwoId is null)
                throw new InvalidOperationException("Two analysts must be assigned - one per new sample - when requesting a new sample.");
            if (newSampleAnalystOneId == newSampleAnalystTwoId)
                throw new InvalidOperationException("The two new samples must be assigned to two different analysts.");

            var now = DateTime.UtcNow;
            newAnalysts = await _db.Users.Include(u => u.Role)
                .Where(u => (u.Id == newSampleAnalystOneId || u.Id == newSampleAnalystTwoId)
                    && u.IsActive && (u.LockedUntil == null || u.LockedUntil <= now) && u.Role != null && u.Role.Type == RoleType.Analyst)
                .ToListAsync();
            if (newAnalysts.Count != 2)
                throw new InvalidOperationException("Both retest analysts must be active, eligible Analyst-role users.");

            foreach (var order in selectedOrders)
            {
                if (order.AssignedAnalystId == newSampleAnalystOneId || order.AssignedAnalystId == newSampleAnalystTwoId)
                    throw new InvalidOperationException("Neither new sample's analyst may be the analyst who tested the original sample.");
            }
        }

        CauseOfTesting? retestCause = null;
        if (decision is ApprovalDecision.RetestRetainedSample or ApprovalDecision.NewSampleRequest)
        {
            retestCause = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Name == "Retest")
                ?? throw new InvalidOperationException("The 'Retest' cause of testing is not configured.");
        }

        var meaning = decision switch
        {
            ApprovalDecision.Approve => SignatureMeaning.Approved,
            ApprovalDecision.Reject => SignatureMeaning.Rejected,
            ApprovalDecision.NewSampleRequest => SignatureMeaning.Rejected,
            ApprovalDecision.RetestRetainedSample => SignatureMeaning.RetestRequested,
            _ => throw new InvalidOperationException($"'{decision}' is not a valid sample-level decision.")
        };

        // Signs first - if password verification fails, nothing below is
        // written (the signature, the event, and the state change below
        // commit together in the single SaveChangesAsync at the end).
        var signature = await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Sample, sampleId, sectionHeadUserId, password,
            meaning, ReviewWorkflowEventType.ApprovalDecisionMade, comment, ipAddress, decision, section);

        signoff.ApprovalSignature = signature;
        signoff.ApprovalDecision = decision;

        switch (decision)
        {
            case ApprovalDecision.Approve:
            {
                var now = DateTime.UtcNow;
                signoff.Status = SectionSignoffStatus.Approved;
                signoff.ApprovedByUserId = sectionHeadUserId;
                signoff.ApprovedAt = now;
                // Never auto-derived from the internal review/approval
                // Comment above - the Approver must explicitly type this,
                // or leave it null, at the moment of approval only.
                signoff.CertificateRemarks = string.IsNullOrWhiteSpace(certificateRemarks) ? null : certificateRemarks.Trim();

                // The sample is approved once its last open section is.
                SampleSectionRollup.Apply(sample);
                if (sample.Status == SampleStatus.Approved)
                {
                    sample.ApprovedByUserId = sectionHeadUserId;
                    sample.ApprovedAt = now;
                    sample.ApprovalDecision = ApprovalDecision.Approve;
                    // Remarks are per section; the sample-level copy is only
                    // unambiguous when one section tested the sample.
                    sample.CertificateRemarks = SampleSectionRollup.SectionIds(sample).Count == 1 ? signoff.CertificateRemarks : null;
                }
                foreach (var order in currentOrders)
                {
                    var previousStep = order.CurrentStep;
                    order.Status = ApprovalStatus.Approved;
                    order.CurrentStep = WorkflowStep.Approved;
                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = order.Id,
                        FromStep = previousStep,
                        ToStep = WorkflowStep.Approved,
                        Note = $"Sample approved by {signature.UserFullNameSnapshot}",
                        PerformedByUserId = sectionHeadUserId
                    });
                }
                break;
            }

            case ApprovalDecision.Reject:
                signoff.Status = SectionSignoffStatus.Rejected;
                // Who decided and when - the combined certificate names the
                // Section Head behind each lab's conclusion, rejections too.
                signoff.ApprovedByUserId = sectionHeadUserId;
                signoff.ApprovedAt = DateTime.UtcNow;
                sample.ApprovalDecision = ApprovalDecision.Reject;
                // A rejection is this section's own judgement only - it no
                // longer closes any other lab still open on the sample.
                SampleSectionRollup.Apply(sample);
                foreach (var order in currentOrders)
                {
                    order.Status = ApprovalStatus.Rejected;
                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = order.Id,
                        FromStep = order.CurrentStep,
                        ToStep = order.CurrentStep,
                        Note = $"Sample rejected by {signature.UserFullNameSnapshot}",
                        PerformedByUserId = sectionHeadUserId
                    });
                }
                break;

            case ApprovalDecision.RetestRetainedSample:
            {
                // The original sample is held/reopened, not closed - it
                // carries no replacement TestOrder of its own any more
                // (that used to live here and caused every test, passing
                // or not, to be silently re-run). Only the tests the
                // Section Head actually selected move to a brand-new
                // sample; every other TestOrder on the original is left
                // completely untouched. Other sections carry on with their
                // own review/approval.
                signoff.Status = SectionSignoffStatus.RetestRequested;
                SampleSectionRollup.Apply(sample);
                if (sample.Status == SampleStatus.RetestRequested)
                    sample.ApprovalDecision = ApprovalDecision.RetestRetainedSample;

                if (sample.OosGroupCode is null)
                    sample.OosGroupCode = await _refNumbers.GenerateOosCodeAsync();

                var newSample = BuildRetestSample(sample, await _refNumbers.GenerateAsync(sample.Category), retestCause!.Id, sectionHeadUserId, sample.OosGroupCode);
                var carriedAnyLocations = false;
                foreach (var order in selectedOrders)
                {
                    var newOrder = new TestOrder
                    {
                        TestCode = order.TestCode,
                        SectionId = order.SectionId,
                        Status = ApprovalStatus.Pending,
                        CurrentStep = WorkflowStep.Waiting,
                        AssignedAnalystId = order.AssignedAnalystId,
                        RoomId = order.RoomId,
                        IsSuperseded = false
                    };
                    newSample.TestOrders.Add(newOrder);

                    if (await CloneFailedLocationsAsync(sample.Category, order, newSample, newOrder))
                        carriedAnyLocations = true;

                    order.IsSuperseded = true;
                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = order.Id,
                        FromStep = order.CurrentStep,
                        ToStep = order.CurrentStep,
                        Note = $"Retest ordered by {signature.UserFullNameSnapshot} - new sample pending reference number",
                        PerformedByUserId = sectionHeadUserId
                    });
                }

                // Water/EM/After Cleaning: the failed locations are already
                // carried onto the new TestOrders above, so this sample
                // skips the checkbox Preparation screen entirely - it would
                // otherwise let the analyst freely add back every other
                // point/room/part (and every test assigned to it), exactly
                // the "retest everything" bug this exists to prevent.
                if (carriedAnyLocations)
                    newSample.PreparationStatus = SamplePreparationStatus.Ready;
                else
                    newSample.PreparationStatus = await PreparationRules.InitialStatusAsync(_db, newSample.TestOrders.Select(o => o.SectionId));

                _db.Samples.Add(newSample);
                break;
            }

            case ApprovalDecision.NewSampleRequest:
            {
                signoff.Status = SectionSignoffStatus.RetestRequested;
                SampleSectionRollup.Apply(sample);
                if (sample.Status == SampleStatus.RetestRequested)
                    sample.ApprovalDecision = ApprovalDecision.NewSampleRequest;

                if (sample.OosGroupCode is null)
                    sample.OosGroupCode = await _refNumbers.GenerateOosCodeAsync();

                // ReferenceNumberGenerator counts existing rows straight from
                // the database - it can't see an Add()ed-but-unsaved Sample
                // from earlier in this same loop, so generating both
                // reference numbers before either is persisted would hand
                // out the same number twice. Saving between the two calls
                // is what keeps them distinct.
                var analystIds = new[] { newSampleAnalystOneId!.Value, newSampleAnalystTwoId!.Value };
                foreach (var analystId in analystIds)
                {
                    var spinoff = BuildRetestSample(sample, await _refNumbers.GenerateAsync(sample.Category), retestCause!.Id, sectionHeadUserId, sample.OosGroupCode);
                    var carriedAnyLocations = false;
                    foreach (var order in selectedOrders)
                    {
                        var newOrder = new TestOrder
                        {
                            TestCode = order.TestCode,
                            SectionId = order.SectionId,
                            Status = ApprovalStatus.Pending,
                            CurrentStep = WorkflowStep.Waiting,
                            AssignedAnalystId = analystId,
                            RoomId = order.RoomId,
                            IsSuperseded = false
                        };
                        spinoff.TestOrders.Add(newOrder);

                        if (await CloneFailedLocationsAsync(sample.Category, order, spinoff, newOrder))
                            carriedAnyLocations = true;
                    }

                    // See the matching comment in the RetestRetainedSample
                    // branch above - same reasoning, applied per spinoff.
                    if (carriedAnyLocations)
                        spinoff.PreparationStatus = SamplePreparationStatus.Ready;
                    else
                        spinoff.PreparationStatus = await PreparationRules.InitialStatusAsync(_db, spinoff.TestOrders.Select(o => o.SectionId));

                    _db.Samples.Add(spinoff);
                    await _db.SaveChangesAsync();
                }

                foreach (var order in selectedOrders)
                {
                    order.IsSuperseded = true;
                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = order.Id,
                        FromStep = order.CurrentStep,
                        ToStep = order.CurrentStep,
                        Note = $"New sample requested by {signature.UserFullNameSnapshot}",
                        PerformedByUserId = sectionHeadUserId
                    });
                }
                break;
            }
        }

        // A sample left waiting on a retest carries the retest's decision,
        // whichever section's decision closed the rest of it.
        if (sample.Status == SampleStatus.RetestRequested)
            sample.ApprovalDecision = sample.SectionSignoffs
                .First(s => s.Status == SectionSignoffStatus.RetestRequested).ApprovalDecision;

        await _db.SaveChangesAsync();

        // Approval happens after every result is already projected, so the
        // ApprovedBy/ApprovedAt/SampleStatus fields on this Sample's
        // ResultRecord rows are only ever filled in on this second pass.
        // A retest (either flavor) sends the selected tests back into
        // testing on a different Sample entirely - there is no final
        // version of THIS sample's record to project or archive yet. With
        // several sections, only the decision that closes the sample (its
        // last approval, or any rejection) finalizes it.
        await FinalizeIfClosedAsync(sample, $"Sample {decision}", sectionHeadUserId);
    }

    // A sample is final once no lab is still open. The decision that closes it
    // may be another lab's approval after an earlier rejection, so the OOS
    // outcome follows the sample's own final state, not this one decision.
    internal async Task FinalizeIfClosedAsync(Sample sample, string archiveLabel, int userId)
    {
        if (sample.Status is not (SampleStatus.Approved or SampleStatus.Rejected)) return;

        await _resultProjection.RefreshApprovalFieldsAsync(sample.Id);
        var document = await _summary.BuildReportDocumentAsync(sample.Id);
        if (document is not null)
            await _archive.ArchiveAsync(ReviewEntityTypes.Sample, sample.Id, document, archiveLabel, userId);

        var outcome = sample.Status == SampleStatus.Rejected ? ApprovalDecision.Reject : ApprovalDecision.Approve;
        await PropagateOosOutcomeAsync(sample, outcome, userId);
    }

    private async Task PropagateOosOutcomeAsync(Sample sample, ApprovalDecision decision, int sectionHeadUserId)
    {
        if (sample.OriginSampleId is null)
            return;

        var origin = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == sample.OriginSampleId.Value);
        if (origin is null)
            return;

        // A retest carries only the deciding section's tests, so it resolves
        // only that section of the origin.
        var sectionIds = SampleSectionRollup.SectionIds(sample);
        if (sectionIds.Count != 1)
            return;
        var section = sectionIds[0];
        var originSignoff = origin.SectionSignoffs.FirstOrDefault(s => s.SectionId == section);
        if (originSignoff is null || originSignoff.Status != SectionSignoffStatus.RetestRequested)
            return;

        ApprovalDecision outcome;
        if (originSignoff.ApprovalDecision == ApprovalDecision.RetestRetainedSample)
        {
            outcome = decision;
        }
        else if (originSignoff.ApprovalDecision == ApprovalDecision.NewSampleRequest)
        {
            var sibling = await _db.Samples.FirstOrDefaultAsync(x => x.OriginSampleId == origin.Id && x.Id != sample.Id
                && x.TestOrders.Any(t => t.SectionId == section));
            if (sibling is null)
                return;

            if (sibling.Status is not (SampleStatus.Approved or SampleStatus.Rejected))
                return;

            outcome = (decision == ApprovalDecision.Approve && sibling.Status == SampleStatus.Approved)
                ? ApprovalDecision.Approve
                : ApprovalDecision.Reject;
        }
        else
        {
            return;
        }


        // Unlike a directly-Rejected sample (which has its own dedicated
        // Rejected-meaning signature to point to), the origin never gets a
        // signature of its own for this mirrored outcome - it was never
        // itself put in front of a Section Head again. ApprovedByUserId/At
        // is therefore the only place that records who/when the chain
        // actually resolved, so it's populated for both outcomes here
        // (unlike the direct-Reject branch above, which leaves them null).
        var now = DateTime.UtcNow;
        // Every section of the origin without its own sign-off row shares
        // the origin's state; freeze them now so a lab still open on the
        // origin doesn't read as sharing this retest's own outcome.
        SampleSectionRollup.FreezeOpenSections(origin);
        originSignoff.Status = outcome == ApprovalDecision.Approve ? SectionSignoffStatus.Approved : SectionSignoffStatus.Rejected;
        originSignoff.ApprovedByUserId = sectionHeadUserId;
        originSignoff.ApprovedAt = now;
        SampleSectionRollup.Apply(origin);
        var originClosed = origin.Status is SampleStatus.Approved or SampleStatus.Rejected;
        if (originClosed)
        {
            origin.ApprovedByUserId = sectionHeadUserId;
            origin.ApprovedAt = now;
        }

        // Unlike a plain direct Approve/Reject (which blanket-sets every
        // current TestOrder to match the one decision a Section Head just
        // made for the whole sample), an OOS-resolved origin can carry a
        // genuine mix: tests that never had a problem and were left alone
        // (still ResultEntered/Reviewed - stuck at "Pending Review"
        // forever otherwise) alongside the superseded one(s) that actually
        // triggered the retest. Each of the origin's own TestOrders is
        // finalized here from its own recorded result, independent of the
        // sample-level outcome and of whether it was ever superseded.
        var originOrders = origin.TestOrders.Where(o => o.SectionId == section).ToList();
        foreach (var order in originOrders)
        {
            var conforms = await DetermineOwnResultConformanceAsync(order.Id);
            order.Status = conforms ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            if (conforms)
            {
                order.CurrentStep = WorkflowStep.Approved;
            }
        }

        await _db.SaveChangesAsync();

        // Another section of the origin is still open - it finalizes the
        // origin when it closes.
        if (!originClosed)
            return;

        await _resultProjection.RefreshApprovalFieldsAsync(origin.Id);
        var document = await _summary.BuildReportDocumentAsync(origin.Id);
        if (document is not null)
            await _archive.ArchiveAsync(ReviewEntityTypes.Sample, origin.Id, document, $"Sample {outcome} (OOS resolved)", sectionHeadUserId);

        await PropagateOosOutcomeAsync(origin, outcome, sectionHeadUserId);
    }

    // Same conform/non-conform precedence the COA's client-side
    // aggregation uses (coaAggregation.ts's buildCoaMatrix/buildCoaSimpleRows),
    // just server-side and against this TestOrder's own recorded result
    // directly - deliberately ignores IsSuperseded, since a superseded
    // order's own result is exactly what proved it needed a retest.
    private async Task<bool> DetermineOwnResultConformanceAsync(int testOrderId)
    {
        var lastReading = await _db.CountTestReadings
            .Where(r => r.TestOrderId == testOrderId && r.IsActive).OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        if (lastReading is not null)
            return lastReading.Status == "WithinLimits";

        var lastLocation = await _db.SampleLocations
            .Where(l => l.TestOrderId == testOrderId).OrderByDescending(l => l.Id).FirstOrDefaultAsync();
        if (lastLocation is not null)
            return lastLocation.Status == "WithinLimits" || lastLocation.Status == "Absent";

        var lastBiochemical = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId && r.BiochemicalOrganismDetected != null)
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        if (lastBiochemical is not null)
            return lastBiochemical.BiochemicalOrganismDetected == false;

        var hasPathogenChain = await _db.PathogenObservations.AnyAsync(p => p.TestOrderId == testOrderId);
        if (hasPathogenChain)
        {
            var detected = await _db.PathogenObservations
                .AnyAsync(p => p.TestOrderId == testOrderId && p.Observation == GrowthObservation.GrowthConforming);
            return !detected;
        }

        var lastResult = await _db.Results
            .Where(r => r.TestOrderId == testOrderId).OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        if (lastResult is not null)
        {
            var value = lastResult.InterpretedValue ?? lastResult.RawValue;
            return !string.Equals(value, "Detected", StringComparison.OrdinalIgnoreCase);
        }

        // No result recorded anywhere for this TestOrder - nothing to
        // contradict, leave it conforming by default.
        return true;
    }
}
