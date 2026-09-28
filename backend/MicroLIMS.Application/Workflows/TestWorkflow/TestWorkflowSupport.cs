using MicroLIMS.Shared.Exceptions;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Notifications;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

// What every part of the test workflow shares: the dependencies, loading
// a test order with its template, step facts and navigation, incubation
// windows, and finalising a result. The step-specific parts derive from
// it; TestWorkflowEngine is the facade that routes to them.
public class TestWorkflowSupport
{
    protected readonly IMicroLimsDbContext _db;
    protected readonly SampleReviewService _sampleReviewService;
    protected readonly ResultProjectionService _resultProjection;
    protected readonly IncubatorEligibilityService _incubatorEligibility;
    protected readonly MediaAppearanceSnapshotService _appearanceSnapshot;
    protected readonly SegregationOfDutiesGuard _sodGuard;
    protected readonly ReviewGateService _reviewGate;
    protected readonly INotificationService _notifications;
    protected readonly IElectronicSignatureService _signatureService;
    protected readonly IUserSectionScopeService _sectionScope;
    protected readonly ILabClock _clock;

    public TestWorkflowSupport(TestWorkflowDependencies deps)
    {
        _db = deps.Db;
        _sampleReviewService = deps.SampleReviewService;
        _resultProjection = deps.ResultProjection;
        _incubatorEligibility = deps.IncubatorEligibility;
        _appearanceSnapshot = deps.AppearanceSnapshot;
        _sodGuard = deps.SodGuard;
        _reviewGate = deps.ReviewGate;
        _notifications = deps.Notifications;
        _signatureService = deps.SignatureService;
        _sectionScope = deps.SectionScope;
        _clock = deps.Clock;
    }

    protected async Task<(TestOrder order, TestDefinition definition)> LoadWithTemplateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var definition = await TemplatesWithSteps().FirstOrDefaultAsync(t => t.Code == order.TestCode);
        return (order, RequireTemplate(order, definition));
    }

    protected IQueryable<TestDefinition> TemplatesWithSteps() => _db.TestDefinitions
        .Include(t => t.Steps).ThenInclude(s => s.IncubationStages)
        .Include(t => t.Steps).ThenInclude(s => s.StepMedia).ThenInclude(m => m.Material)
        .Include(t => t.Steps).ThenInclude(s => s.StepMedia).ThenInclude(m => m.IncubationCondition)
        .Include(t => t.Steps).ThenInclude(s => s.PhenotypicTests);

    protected static TestDefinition RequireTemplate(TestOrder order, TestDefinition? definition)
    {
        if (definition is null)
            throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow template configured in Test Master.");

        if (definition.Steps.Count == 0 && !AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
            throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow steps configured yet - add them in Test Master.");

        return definition;
    }

    // The five pathogen step types record their completion as a
    // WorkflowStepResult row, not a PathogenObservation - only
    // SubmitSelectivePlatingAsync writes an observation, so a
    // PathogenObservations-only "done" test can never see a broth,
    // confirmatory or biochemical step finish. Legacy Observation-type
    // steps (StepType.PlateCount aside, anything driven through
    // RecordResultAsync) still go through PathogenObservations.
    protected static bool IsPathogenStepType(StepType stepType) =>
        stepType is StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.SelectivePlating
            or StepType.ConfirmatoryPlating or StepType.BiochemicalTest;

    protected static bool IsIncubationForStep(Incubation inc, TestWorkflowStep step)
    {
        if (inc.StepNumber > 0 && inc.StepNumber == step.StepOrder) return true;
        if (string.Equals(inc.StepName, step.StepName, StringComparison.OrdinalIgnoreCase)) return true;
        if (step.StepType == StepType.BrothEnrichment)
        {
            return (inc.StepNumber == 1 ||
                    inc.StepName.Equals("Broth Enrichment", StringComparison.OrdinalIgnoreCase) ||
                    inc.StepName.Equals("TSB", StringComparison.OrdinalIgnoreCase) ||
                    inc.StepName.Equals("TSB Enrichment", StringComparison.OrdinalIgnoreCase))
                   && !inc.StepName.Contains("Selective", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    // Loads every row the completion rules below read for one test order, so
    // walking a whole template costs these few queries rather than two or
    // three per step. Callers that already hold the order's incubations pass
    // them in. Incubations stay tracked entities, as the per-step queries
    // returned them; the other rows are read-only projections.
    protected async Task<TestStepFacts> LoadStepFactsAsync(int testOrderId, IEnumerable<Incubation>? loadedIncubations = null)
    {
        var incubations = loadedIncubations?.ToList()
            ?? await _db.Incubations.Where(i => i.TestOrderId == testOrderId).ToListAsync();
        var facts = await LoadStepFactsForOrdersAsync(new Dictionary<int, List<Incubation>> { [testOrderId] = incubations });
        return facts[testOrderId];
    }

    // The same facts for any number of orders, in the same fixed number of
    // queries. Keyed by order id, with each order's incubations supplied.
    protected async Task<Dictionary<int, TestStepFacts>> LoadStepFactsForOrdersAsync(IReadOnlyDictionary<int, List<Incubation>> incubationsByOrderId)
    {
        var ids = incubationsByOrderId.Keys.ToList();

        var ordersWithLocations = (await _db.SampleLocations
                .Where(l => ids.Contains(l.TestOrderId))
                .Select(l => l.TestOrderId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();
        var stepResults = (await _db.WorkflowStepResults
                .AsNoTracking()
                .Where(r => ids.Contains(r.TestOrderId))
                .Select(r => new
                {
                    r.TestOrderId, r.Id, r.IncubationId, r.StepName, r.IsSharedSessionStep,
                    HasConfirmatoryResult = r.ConfirmatoryResult != null,
                    r.BiochemicalResultText, r.BiochemicalOrganismDetected, r.SubmittedAtUtc
                })
                .ToListAsync())
            .ToLookup(r => r.TestOrderId, r => new StepResultFact(r.Id, r.IncubationId, r.StepName, r.IsSharedSessionStep,
                r.HasConfirmatoryResult, r.BiochemicalResultText, r.BiochemicalOrganismDetected, r.SubmittedAtUtc));
        var activeCountReadings = (await _db.CountTestReadings
                .AsNoTracking()
                .Where(r => ids.Contains(r.TestOrderId) && r.IsActive)
                .Select(r => new { r.TestOrderId, r.Id, r.StepName, r.ReportedResult, r.CalculatedResult, r.Status, r.EnteredAt })
                .ToListAsync())
            .ToLookup(r => r.TestOrderId, r => new CountReadingFact(r.Id, r.StepName, r.ReportedResult, r.CalculatedResult, r.Status, r.EnteredAt));
        var observationStepNames = (await _db.PathogenObservations
                .Where(o => ids.Contains(o.TestOrderId))
                .Select(o => new { o.TestOrderId, o.StepName })
                .Distinct()
                .ToListAsync())
            .ToLookup(o => o.TestOrderId, o => o.StepName);
        var activeAnalysisOrderIds = (await _db.TestAnalyses
                .AsNoTracking()
                .Where(e => ids.Contains(e.TestOrderId) && e.IsActive
                    && !e.ParameterResults.Any(pr => pr.ComparisonStatus == "NextStageRequired"))
                .Select(e => e.TestOrderId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        return ids.ToDictionary(id => id, id => new TestStepFacts(
            ordersWithLocations.Contains(id),
            incubationsByOrderId[id],
            stepResults[id].ToList(),
            activeCountReadings[id].ToList(),
            observationStepNames[id].ToHashSet(StringComparer.Ordinal),
            activeAnalysisOrderIds.Contains(id)));
    }

    // The completion rules, evaluated against loaded facts. String
    // comparisons are ordinal, matching PostgreSQL's = and LIKE on these
    // columns, which the rules were previously evaluated with.
    public static bool IsStepDone(TestStepFacts facts, WorkflowType workflowType, TestWorkflowStep step)
    {
        // EM/After Cleaning batch orders never write a CountTestReading or
        // PathogenObservation for any step - they carry per-location
        // results on SampleLocation instead, only for the final step
        // (see RecordBatchResultsAsync). A non-final step is "done" once
        // its incubation window has been explicitly closed via
        // CloseCurrentIncubationWindowAsync; the final step's "done"-ness
        // is irrelevant here since a completed batch order's TestOrder
        // moves straight to Ready and GetCurrentStepAsync short-circuits
        // before ever reaching this check again.
        if (facts.HasLocations)
        {
            if (step.RequiresIncubationTransfer)
            {
                return facts.Incubations.Any(i =>
                    i.StepName == step.StepName && i.StageNumber == 2 && i.CompletedAt != null);
            }

            return facts.Incubations.Any(i => i.StepName == step.StepName && i.CompletedAt != null);
        }

        if (workflowType == WorkflowType.CountTest)
            return facts.ActiveCountReadings.Any(r => r.StepName == step.StepName);

        if (AnalysisWorkflows.UsesTestAnalysis(workflowType))
            return facts.HasActiveAnalysis;

        if (IsPathogenStepType(step.StepType))
        {
            // Confirmatory plating is the one pathogen step whose result
            // row is written in two passes - setup first, plate readings
            // afterwards. It is only done once the readings have produced
            // a ConfirmatoryResult; treating the setup row as "done"
            // would push the chain past the step the analyst still has to
            // read out.
            if (step.StepType == StepType.ConfirmatoryPlating)
                return facts.StepResults.Any(r => r.StepName == step.StepName && r.HasConfirmatoryResult);

            var relevantIncubations = facts.Incubations.Where(i => IsIncubationForStep(i, step)).ToList();

            // Any pathogen step with an active, uncompleted incubation is NOT done.
            if (relevantIncubations.Any(i => i.CompletedAt == null))
                return false;

            // For broth enrichment where an incubation exists, it must be completed.
            if (step.StepType == StepType.BrothEnrichment && relevantIncubations.Count > 0)
            {
                if (!relevantIncubations.Any(i => i.CompletedAt != null))
                    return false;
            }

            return facts.StepResults.Any(r =>
                r.StepName == step.StepName || (step.StepType == StepType.BrothEnrichment && (r.StepName == "Broth Enrichment" || r.StepName == "TSB" || r.StepName == "TSB Enrichment")));
        }

        return facts.ObservationStepNames.Contains(step.StepName);
    }

    // Finds the lowest-StepOrder step that isn't done yet, or null if the
    // whole template is complete. Shared by GetCurrentStepAsync (to know
    // what to show) and SelectMediaAsync (to reject an out-of-order
    // start attempt - the equivalent of the old PathogenWorkflowEngine's
    // "chain violation" guard, generalized to any template).
    protected async Task<TestWorkflowStep?> FindFirstIncompleteStepAsync(int testOrderId, TestDefinition definition) =>
        FindFirstIncompleteStep(await LoadStepFactsAsync(testOrderId), definition);

    protected static TestWorkflowStep? FindFirstIncompleteStep(TestStepFacts facts, TestDefinition definition) =>
        definition.Steps.OrderBy(s => s.StepOrder).FirstOrDefault(step => !IsStepDone(facts, definition.WorkflowType, step));

    protected async Task CheckPredecessorStepsReadyAsync(TestOrder order, TestDefinition definition, TestWorkflowStep targetStep)
    {
        var predecessors = definition.Steps
            .Where(s => s.StepOrder < targetStep.StepOrder)
            .OrderBy(s => s.StepOrder)
            .ToList();

        if (predecessors.Count == 0) return;

        var facts = await LoadStepFactsAsync(order.Id);
        var allIncubations = facts.Incubations.OrderByDescending(i => i.StartedAt).ToList();

        foreach (var pred in predecessors)
        {
            var predIncubations = allIncubations.Where(i => IsIncubationForStep(i, pred)).ToList();

            var activeInc = predIncubations.FirstOrDefault(i => i.CompletedAt == null);
            if (activeInc != null)
            {
                long remainingSeconds = 0;
                if (!activeInc.MinimumDurationOverriddenByUserId.HasValue && activeInc.IncubationEndUtc.HasValue)
                {
                    remainingSeconds = Math.Max(0, (long)Math.Ceiling((activeInc.IncubationEndUtc.Value - _clock.UtcNow.UtcDateTime).TotalSeconds));
                }

                throw new PredecessorStepIncubationActiveException(pred.StepName, remainingSeconds);
            }

            var isDone = IsStepDone(facts, definition.WorkflowType, pred);
            if (!isDone)
            {
                throw new InvalidOperationException($"Workflow order violation: step \"{pred.StepName}\" must be completed before \"{targetStep.StepName}\".");
            }
        }
    }

    // Test Preparation gate: no incubation or test transition may start on a sample whose
    // preparation step is still outstanding. Product/RM/PM must have their preparation stage
    // confirmed (both Sample.PreparationStatus == Ready AND a confirmed SamplePreparation record
    // for received items). EM/After Cleaning must have locations assigned.
    // Contemporaneously logs any refusal to AuditLog ("TestStartRefused") and WorkflowHistory
    // in accordance with GMP data integrity requirements.
    protected async Task RequireSamplePreparedAsync(int testOrderId, int sampleId, string stepName, int userId, WorkflowStep currentStep)
    {
        if (await PreparationRules.TestOrderSkipsPreparationAsync(_db, testOrderId))
            return;

        var sample = await _db.Samples
            .Where(s => s.Id == sampleId)
            .Select(s => new { s.Id, s.Category, s.PreparationStatus, s.ItemId, s.ReferenceNumber })
            .FirstAsync();

        var isPrepared = sample.PreparationStatus == SamplePreparationStatus.Ready;
        if (isPrepared && sample.ItemId != null)
        {
            isPrepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id);
        }

        if (!isPrepared)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = "TestOrder",
                EntityId = testOrderId.ToString(),
                Action = "TestStartRefused",
                UserId = userId,
                SampleId = sample.Id,
                TestOrderId = testOrderId,
                SampleReferenceNumber = sample.ReferenceNumber,
                Reason = $"GMP Refusal: Test preparation stage must be confirmed for sample {sample.ReferenceNumber} before testing can start."
            });

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = testOrderId,
                FromStep = currentStep,
                ToStep = currentStep,
                Note = $"Transition refused: Test preparation not confirmed for sample {sample.ReferenceNumber} (step \"{stepName}\").",
                PerformedByUserId = userId,
                Timestamp = _clock.UtcNow.UtcDateTime
            });

            await _db.SaveChangesAsync();

            throw new WorkflowStepException(
                WorkflowErrorCodes.PreparationNotConfirmed,
                $"Test Preparation must be completed and confirmed for sample {sample.ReferenceNumber} before incubation can be started for step \"{stepName}\".");
        }

        // Belt and braces for the location-based categories: PreparationStatus
        // going Ready and the locations landing are two separate writes, and
        // only the second is what an incubation actually needs.
        if (sample.Category is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning)
        {
            var hasLocations = await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId);
            if (!hasLocations)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "TestOrder",
                    EntityId = testOrderId.ToString(),
                    Action = "TestStartRefused",
                    UserId = userId,
                    SampleId = sample.Id,
                    TestOrderId = testOrderId,
                    SampleReferenceNumber = sample.ReferenceNumber,
                    Reason = $"GMP Refusal: No locations assigned to test {testOrderId} for sample {sample.ReferenceNumber}."
                });

                _db.WorkflowHistories.Add(new WorkflowHistory
                {
                    TestOrderId = testOrderId,
                    FromStep = currentStep,
                    ToStep = currentStep,
                    Note = $"Transition refused: Preparation not complete - no locations assigned to this test (step \"{stepName}\").",
                    PerformedByUserId = userId,
                    Timestamp = _clock.UtcNow.UtcDateTime
                });

                await _db.SaveChangesAsync();

                throw new WorkflowStepException(
                    WorkflowErrorCodes.PreparationNotConfirmed,
                    "Preparation not complete - no locations assigned to this test.");
            }
        }
    }

    public Task PropagateSharedTsbToSiblingOrdersAsync(
        int testOrderId,
        int incubationId,
        int userId,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => PropagateSharedTsbToSiblingOrdersCoreAsync(testOrderId, incubationId, userId, ct));

    protected async Task PropagateSharedTsbToSiblingOrdersCoreAsync(
        int testOrderId,
        int incubationId,
        int userId,
        CancellationToken ct = default)
    {
        var sourceOrder = await _db.TestOrders
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test order #{testOrderId} not found.");

        var incubation = await _db.Incubations
            .Include(i => i.Media)
                .ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .FirstOrDefaultAsync(i => i.Id == incubationId, ct)
            ?? throw new NotFoundException($"Incubation #{incubationId} not found.");

        // Without a lot there is nothing a sibling's own medium could accept.
        if (incubation.Media is null) return;

        var siblings = await FindSharedTsbSiblingsAsync(sourceOrder, incubation.Media, ct);
        if (siblings.Count == 0) return;

        var brothStepName = "Broth Enrichment";
        var mediaLotNumber = incubation.Media.LotNumber;
        var incubatorCode = incubation.IncubatorEquipment?.Code ?? "INC";
        var sourceStartUtc = incubation.IncubationStartUtc ?? incubation.StartedAt;

        foreach (var sibling in siblings)
        {
            var targetStepName = sibling.Step.StepName;
            // Each test keeps its own window from its own TSB medium - the
            // shared TSB only lends the lot, incubator and start.
            var window = IncubationWindowResolver.TryGet(sibling.Medium);

            int siblingIncId;
            if (sibling.ExistingIncubation is null)
            {
                // SelectMediaAsync refuses a start that a joining test can't
                // take; a TSB started before that check still leaves such a
                // test to start on its own rather than guess its window.
                if (window is null || incubation.IncubatorEquipmentId is not int incubatorId
                    || !await _incubatorEligibility.IsWithinRangeAsync(sibling.Medium.Id, incubatorId, ct))
                    continue;

                var newInc = new Incubation
                {
                    TestOrderId = sibling.Order.Id,
                    StepNumber = sibling.Step.StepOrder,
                    StepName = targetStepName,
                    StageNumber = 1,
                    MediaId = incubation.MediaId,
                    IncubatorEquipmentId = incubation.IncubatorEquipmentId,
                    Temperature = window.TemperatureText,
                    Duration = window.DurationText,
                    StartedAt = incubation.StartedAt,
                    IncubationStartUtc = incubation.IncubationStartUtc,
                    IncubationEndUtc = window.EndAt(sourceStartUtc),
                    ExpectedReadingAt = window.EndAt(sourceStartUtc),
                    WindowReceivedAtUtc = incubation.WindowReceivedAtUtc,
                    StartedByUserId = userId
                };
                CopyCompletionOnceOwnMinimumElapsed(incubation, newInc, window);
                _db.Incubations.Add(newInc);
                await _db.SaveChangesAsync(ct);
                siblingIncId = newInc.Id;
            }
            else
            {
                // A test that started its own TSB on another lot is not part of this one.
                if (sibling.ExistingIncubation.MediaId != incubation.MediaId) continue;
                if (window is not null)
                    CopyCompletionOnceOwnMinimumElapsed(incubation, sibling.ExistingIncubation, window);
                siblingIncId = sibling.ExistingIncubation.Id;
            }

            // Ensure WorkflowStepResult exists for sibling
            var existsWsr = await _db.WorkflowStepResults
                .AnyAsync(r => r.TestOrderId == sibling.Order.Id && (r.StepName == targetStepName || r.StepName == brothStepName), ct);

            if (!existsWsr)
            {
                _db.WorkflowStepResults.Add(new WorkflowStepResult
                {
                    TestOrderId = sibling.Order.Id,
                    StepName = targetStepName,
                    StepType = StepType.BrothEnrichment,
                    IncubationId = siblingIncId,
                    IsSharedSessionStep = true,
                    SubmittedByUserId = userId,
                    SubmittedAtUtc = _clock.UtcNow.UtcDateTime
                });

                if (sibling.Order.CurrentStep == WorkflowStep.Waiting)
                {
                    await WorkflowStateMachine.TransitionAsync(_db, sibling.Order, WorkflowStep.Incubating, userId,
                        $"Broth enrichment linked to shared TSB (propagated from Test Order #{testOrderId}). Lot: {mediaLotNumber}, Incubator: {incubatorCode}.");
                }
                else
                {
                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = sibling.Order.Id,
                        FromStep = sibling.Order.CurrentStep,
                        ToStep = sibling.Order.CurrentStep,
                        Note = $"Broth enrichment linked to shared TSB (propagated from Test Order #{testOrderId}). Lot: {mediaLotNumber}, Incubator: {incubatorCode}.",
                        PerformedByUserId = userId,
                        Timestamp = _clock.UtcNow.UtcDateTime
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    protected sealed record SharedTsbSibling(TestOrder Order, TestWorkflowStep Step, TestWorkflowStepMedia Medium, Incubation? ExistingIncubation);

    // The sample's other open tests that join a shared TSB on this lot. A
    // test joins only when its own TSB step medium accepts the lot; one
    // configured with a different medium is left to start on its own.
    protected async Task<List<SharedTsbSibling>> FindSharedTsbSiblingsAsync(TestOrder sourceOrder, Media lot, CancellationToken ct = default)
    {
        var siblings = await _db.TestOrders
            .Where(t => t.SampleId == sourceOrder.SampleId && t.Id != sourceOrder.Id && t.Status != ApprovalStatus.Approved && !t.IsSuperseded)
            .ToListAsync(ct);
        if (siblings.Count == 0) return new List<SharedTsbSibling>();

        var testCodes = siblings.Select(t => t.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.Material)
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.IncubationCondition)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, ct);

        var siblingIds = siblings.Select(t => t.Id).ToList();
        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId.HasValue && siblingIds.Contains(i.TestOrderId.Value))
            .ToListAsync(ct);

        var lotProductId = lot.Material?.MediaProductId;
        var result = new List<SharedTsbSibling>();
        foreach (var sibling in siblings)
        {
            if (!testDefs.TryGetValue(sibling.TestCode, out var def)) continue;

            var tsbStep = def.Steps.OrderBy(s => s.StepOrder).FirstOrDefault(TsbDetectionHelper.IsSharedTsbStep);
            if (tsbStep is null) continue;

            var medium = IncubationWindowResolver.MatchStepMedium(tsbStep.StepMedia, lot.MaterialId, lotProductId);
            if (medium is null) continue;

            var existing = incubations
                .Where(i => i.TestOrderId == sibling.Id && (i.StepName == tsbStep.StepName || i.StepName == "Broth Enrichment"))
                .OrderByDescending(i => i.StartedAt)
                .FirstOrDefault();
            result.Add(new SharedTsbSibling(sibling, tsbStep, medium, existing));
        }
        return result;
    }

    // A sibling's completion is copied from the shared TSB only when its own
    // minimum had elapsed by the time the source was completed; otherwise it
    // stays open and is completed once its own window allows.
    protected static void CopyCompletionOnceOwnMinimumElapsed(Incubation source, Incubation target, IncubationWindow window)
    {
        if (source.CompletedAt is not DateTime completedAt || target.CompletedAt.HasValue) return;
        if (completedAt < window.MinReadyAt(target.IncubationStartUtc ?? target.StartedAt)) return;

        target.CompletedAt = completedAt;
        target.CompletedByUserId = source.CompletedByUserId;
        target.Outcome = source.Outcome;
    }

    protected async Task<(TestOrder Order, TestDefinition Definition, Dictionary<int, Specification> SpecById, DateTime AnalysedAtUtc)> ValidateTestAnalysisOrderAsync(
        int testOrderId,
        DateTime analysedAt,
        int? equipmentId,
        IReadOnlyCollection<int>? suppliedSpecIds,
        string password,
        WorkflowType expectedWorkflowType,
        int userId)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Password is required to sign the result.");
        if (expectedWorkflowType != WorkflowType.Dissolution && expectedWorkflowType != WorkflowType.Disintegration && expectedWorkflowType != WorkflowType.WeightVariation && expectedWorkflowType != WorkflowType.StandardComparison && (suppliedSpecIds == null || suppliedSpecIds.Count == 0))
            throw new InvalidOperationException("At least one parameter result is required.");

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var analysedAtUtc = analysedAt.Kind switch
        {
            DateTimeKind.Utc => analysedAt,
            DateTimeKind.Local => analysedAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(analysedAt, DateTimeKind.Utc)
        };

        if (analysedAtUtc > nowUtc.AddMinutes(5))
            throw new InvalidOperationException("Analysis time cannot be in the future.");

        var order = await _db.TestOrders
            .Include(t => t.Results)
            .Include(t => t.Sample)
            .FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");

        RequireOrderNotFinalized(order);

        if (order.IsSuperseded)
            throw new InvalidOperationException("Cannot record result for a superseded test order.");

        var definition = await _db.TestDefinitions
            .FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");

        if (definition.WorkflowType != expectedWorkflowType)
            throw new InvalidOperationException($"Test order {testOrderId} is not a {expectedWorkflowType} workflow.");

        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        if (await _db.TestAnalyses.AnyAsync(e => e.TestOrderId == testOrderId && e.IsActive))
            throw new InvalidOperationException($"An active {AnalysisWorkflows.GetDisplayName(expectedWorkflowType)} entry already exists for this test order.");

        if (equipmentId.HasValue)
        {
            var equip = await _db.Equipment
                .Include(e => e.Section)
                .FirstOrDefaultAsync(e => e.Id == equipmentId.Value)
                ?? throw new NotFoundException($"Equipment {equipmentId.Value} not found.");

            var inventoryEquip = await _db.EquipmentInventories
                .FirstOrDefaultAsync(i => i.Code == equip.Code);
            if (inventoryEquip != null && inventoryEquip.Status != EquipmentOperationalStatus.InService)
                throw new InvalidOperationException($"Equipment \"{equip.Name}\" is not active.");

            var equipSectionCode = equip.Section?.Code
                ?? (await _db.DocumentSections.Where(s => s.Id == equip.SectionId).Select(s => s.Code).FirstOrDefaultAsync());

            if (!string.Equals(equipSectionCode, "FP", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Equipment \"{equip.Name}\" is not in the Finished Product (FP) section.");
        }

        if (order.Sample?.ItemId is null)
            throw new InvalidOperationException("Test order sample must be linked to an item with specifications.");

        var itemId = order.Sample.ItemId.Value;

        var specs = await _db.Specifications
            .Include(s => s.TestAnalyte)
            .Where(s => s.ItemId == itemId && s.TestCode == order.TestCode)
            .ToListAsync();

        if (specs.Count == 0)
            throw new InvalidOperationException("No specifications configured for this test and item.");

        if (expectedWorkflowType == WorkflowType.Dissolution)
        {
            if (specs.Count != 1 || specs[0].LimitType != LimitType.DissolutionQ)
                throw new InvalidOperationException("Dissolution tests require exactly one DissolutionQ specification.");
        }
        else if (expectedWorkflowType == WorkflowType.Disintegration)
        {
            if (specs.Count != 1 || specs[0].LimitType != LimitType.DisintegrationTime)
                throw new InvalidOperationException("Disintegration tests require exactly one DisintegrationTime specification.");
        }
        else if (expectedWorkflowType == WorkflowType.WeightVariation)
        {
            if (specs.Count != 1 || specs[0].LimitType != LimitType.WeightVariation)
                throw new InvalidOperationException("Weight variation tests require exactly one WeightVariation specification.");
        }
        else if (expectedWorkflowType == WorkflowType.StandardComparison)
        {
            if (specs.Any(s => !s.TestAnalyteId.HasValue))
                throw new InvalidOperationException("Every specification for Standard-Comparison must be linked to a test analyte.");
        }
        else
        {
            if (suppliedSpecIds == null || suppliedSpecIds.Count != specs.Count)
                throw new InvalidOperationException($"Expected {specs.Count} parameter results matching specifications, but received {suppliedSpecIds?.Count ?? 0}.");

            var specIds = specs.Select(s => s.Id).ToHashSet();
            if (suppliedSpecIds.Distinct().Count() != suppliedSpecIds.Count || !specIds.SetEquals(suppliedSpecIds))
                throw new InvalidOperationException("Every specification for this test must be supplied exactly once.");
        }

        return (order, definition, specs.ToDictionary(s => s.Id), analysedAtUtc);
    }

    protected async Task<TestWorkflowResult> PersistTestAnalysisAndFinalizeAsync(
        TestOrder order,
        WorkflowType workflowType,
        int? equipmentId,
        DateTime analysedAtUtc,
        string? conditionsJson,
        List<ParameterResult> parameterResults,
        ResultType resultType,
        string password,
        string? comment,
        string workflowDisplayName,
        int userId,
        string? ipAddress,
        decimal? unitAmount = null,
        SampleMatrix? sampleMatrix = null,
        string? validityRecordType = null,
        int? validityRecordId = null)
    {
        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            password,
            SignatureMeaning.ResultRecorded,
            "TestOrder",
            order.Id,
            comment,
            ipAddress);

        var entry = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = workflowType,
            EquipmentId = equipmentId,
            AnalysedAt = analysedAtUtc,
            UnitAmount = unitAmount,
            SampleMatrix = sampleMatrix,
            ConditionsJson = conditionsJson,
            ValidityRecordType = validityRecordType,
            ValidityRecordId = validityRecordId,
            IsActive = true,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime,
            Signature = signature,
            Comment = comment,
            ParameterResults = parameterResults
        };

        _db.TestAnalyses.Add(entry);

        string overallStatus;
        if (parameterResults.Any(r => r.ComparisonStatus == "OutOfSpecification"))
            overallStatus = "OutOfSpecification";
        else if (parameterResults.Any(r => r.ComparisonStatus == "RequiresReview"))
            overallStatus = "RequiresReview";
        else if (parameterResults.Any(r => r.ComparisonStatus == "WithinLimits"))
            overallStatus = "WithinLimits";
        else
            overallStatus = parameterResults.First().ComparisonStatus;

        var outcomeSummary = string.Join(", ", parameterResults.Select(r => $"{r.ParameterName}: {r.ReportedDisplay}"));

        _db.Results.Add(new Result
        {
            TestOrderId = order.Id,
            RawValue = string.Join(",", parameterResults.Select(r => $"{r.ParameterName}={(r.ReportedValue.HasValue ? r.ReportedValue.Value.ToString(CultureInfo.InvariantCulture) : r.ReportedDisplay)}")),
            InterpretedValue = $"{outcomeSummary} ({overallStatus})",
            Type = resultType,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime
        });

        await _db.SaveChangesAsync();

        foreach (var pr in parameterResults)
        {
            await _resultProjection.UpsertFromParameterResultAsync(pr.Id);
        }
        await _db.SaveChangesAsync();

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"{workflowDisplayName} complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }

    // A TestOrder at Ready or beyond has had its result reported and (for
    // Reviewed/Approved) signed. Re-submitting a step against it would
    // silently append a second, contradictory result behind the reported
    // one - the same class of falsification B2 blocks for confirmatory
    // re-runs, at order level.
    protected static void RequireOrderNotFinalized(TestOrder order)
    {
        if (order.CurrentStep is WorkflowStep.Ready or WorkflowStep.Reviewed or WorkflowStep.Approved)
            throw new InvalidOperationException(
                $"Test order {order.Id} is already at {order.CurrentStep} - its workflow can no longer be submitted against.");
    }

    protected async Task<TestWorkflowStepMedia> LoadStepMediumAsync(int stepId, int materialId)
    {
        return await _db.TestWorkflowStepMedias
            .FirstOrDefaultAsync(m => m.TestWorkflowStepId == stepId && m.MaterialId == materialId)
            ?? throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                "That medium is not on this step's permitted list.");
    }

    // The step's own IncubationMinHours/MaxHours/TemperatureMin/Max are no
    // longer read at execution time - a step's permitted medium can carry
    // its own window (e.g. Confirmatory Plating's XLD vs TSI), which the
    // step alone can't represent. Given an already-open Incubation's
    // MediaId (the lot picked at selection time), resolves the specific
    // TestWorkflowStepMedia row that was actually chosen, so callers can
    // read its window instead of the step's.
    protected async Task<TestWorkflowStepMedia> ResolveSelectedStepMediumAsync(int stepId, int mediaLotId)
    {
        var mediaRow = await _db.Media.Where(m => m.Id == mediaLotId)
            .Select(m => new { m.MaterialId, m.Material!.MediaProductId })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Media lot {mediaLotId} not found.");

        var stepMedia = await _db.TestWorkflowStepMedias
            .Include(m => m.IncubationCondition)
            .Include(m => m.Material)
            .Where(m => m.TestWorkflowStepId == stepId)
            .ToListAsync();

        return IncubationWindowResolver.MatchStepMedium(stepMedia, mediaRow.MaterialId, mediaRow.MediaProductId)
            ?? throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                "That medium is not on this step's permitted list.");
    }

    protected async Task RequireEligibleIncubatorAsync(int stepMediaId, int equipmentId)
    {
        if (!await _incubatorEligibility.IsWithinRangeAsync(stepMediaId, equipmentId))
            throw new WorkflowStepException(WorkflowErrorCodes.IncubatorTempOutOfRange,
                "The selected incubator's set point is outside this medium's temperature range.");
    }

    protected void RequireIncubationComplete(Incubation incubation)
    {
        if (incubation.MinimumDurationOverriddenByUserId.HasValue) return;
        var remaining = (long)Math.Ceiling((incubation.IncubationEndUtc!.Value - _clock.UtcNow.UtcDateTime).TotalSeconds);
        if (remaining > 0)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                "This step's incubation period has not finished yet.", remaining);
    }

    // RequireIncubationComplete only asks "has the declared end passed?",
    // which a one-second window satisfies as readily as a real 18-24h
    // one - and a window that ends before it starts satisfies it too.
    // The window is analyst-supplied, so it has to be checked against a
    // minimum the same way RequireMinimumDurationElapsed checks a
    // server-recorded window against it.
    //
    // Deliberately no upper bound: over-incubation happens in real labs
    // and is handled by explanation/deviation, not by refusing the
    // record. Under-incubation is the falsification risk.
    protected static void RequireValidIncubationWindow(string stepName, int minHours, DateTime incubationStartUtc, DateTime incubationEndUtc)
    {
        if (incubationEndUtc < incubationStartUtc)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowInvalid,
                $"The incubation window ends before it starts ({incubationStartUtc:yyyy-MM-dd HH:mm} to {incubationEndUtc:yyyy-MM-dd HH:mm} UTC).");

        var declaredHours = (incubationEndUtc - incubationStartUtc).TotalHours;
        if (declaredHours < minHours)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowTooShort,
                $"Step \"{stepName}\" requires at least {minHours} hours of incubation - " +
                $"the declared window is {declaredHours:0.##} hours.");
    }
}

// The engine's collaborators, resolved once and handed to each part.
public sealed record TestWorkflowDependencies(
    IMicroLimsDbContext Db,
    SampleReviewService SampleReviewService,
    ResultProjectionService ResultProjection,
    IncubatorEligibilityService IncubatorEligibility,
    MediaAppearanceSnapshotService AppearanceSnapshot,
    SegregationOfDutiesGuard SodGuard,
    ReviewGateService ReviewGate,
    INotificationService Notifications,
    IElectronicSignatureService SignatureService,
    IUserSectionScopeService SectionScope,
    ILabClock Clock);
