using MicroLIMS.Domain.Constants;
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

// Which step a test order is on and what it shows next: current-step
// details for one or many orders, sibling pathogen orders, and the generic
// advance/validate/complete contract.
public sealed class TestStepNavigator : TestWorkflowSupport
{
    public TestStepNavigator(TestWorkflowDependencies deps) : base(deps) { }

    // A step is "done" once a definitive result has been recorded for
    // it: a CountTestReading for CountTest workflows (always one step),
    // a WorkflowStepResult for a pathogen step, or any observation for a
    // plain Observation step.
    public async Task<bool> IsStepDoneAsync(int testOrderId, WorkflowType workflowType, TestWorkflowStep step) =>
        IsStepDone(await LoadStepFactsAsync(testOrderId), workflowType, step);

    public async Task<CurrentStepResult> GetCurrentStepAsync(int testOrderId) =>
        (await GetCurrentStepDetailsAsync(testOrderId)).Result;

    public async Task<CurrentStepDetails> GetCurrentStepDetailsAsync(int testOrderId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        // LoadWithTemplateAsync already loaded the order's incubations.
        var facts = await LoadStepFactsAsync(testOrderId, order.Incubations);
        return BuildCurrentStepDetails(order, definition, facts);
    }

    // For callers walking many orders (grouped actions): per order, the same
    // result GetCurrentStepDetailsAsync gives, from one load of all orders,
    // templates and facts. An order whose step can't be computed carries the
    // message GetCurrentStepDetailsAsync would have thrown instead.
    public async Task<IReadOnlyDictionary<int, CurrentStepLookup>> GetCurrentStepDetailsForOrdersAsync(IReadOnlyCollection<int> testOrderIds)
    {
        var ids = testOrderIds.Distinct().ToList();
        var lookups = new Dictionary<int, CurrentStepLookup>();
        if (ids.Count == 0) return lookups;

        var orders = await _db.TestOrders
            .Include(t => t.Incubations)
            .Include(t => t.Results)
            .Where(t => ids.Contains(t.Id))
            .ToListAsync();
        var codes = orders.Select(o => o.TestCode).Distinct().ToList();
        var definitionsByCode = (await TemplatesWithSteps().Where(t => codes.Contains(t.Code)).ToListAsync())
            .GroupBy(t => t.Code)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Id).First());
        var facts = await LoadStepFactsForOrdersAsync(orders.ToDictionary(o => o.Id, o => o.Incubations.ToList()));

        foreach (var id in ids)
        {
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order is null)
            {
                lookups[id] = new CurrentStepLookup(null, $"Test order {id} not found.");
                continue;
            }

            try
            {
                var definition = RequireTemplate(order, definitionsByCode.GetValueOrDefault(order.TestCode));
                lookups[id] = new CurrentStepLookup(BuildCurrentStepDetails(order, definition, facts[id]), null);
            }
            catch (Exception ex)
            {
                lookups[id] = new CurrentStepLookup(null, ex.Message);
            }
        }

        return lookups;
    }

    private static CurrentStepDetails BuildCurrentStepDetails(TestOrder order, TestDefinition definition, TestStepFacts facts)
    {
        var totalSteps = definition.Steps.Count;
        var allSteps = definition.Steps.OrderBy(s => s.StepOrder).Select(s => new StepOutline(s.StepOrder, s.StepName)).ToList();

        // Once a TestOrder is at Ready or beyond, its workflow is done -
        // short-circuit rather than walking the step template. Needed for
        // EM/After Cleaning batch orders, whose RecordBatchResultsAsync
        // transitions straight to Ready without ever writing a
        // CountTestReading, so IsStepDoneAsync's CountTestReadings check
        // would otherwise report the order as perpetually incomplete.
        if (order.CurrentStep is WorkflowStep.Ready or WorkflowStep.Reviewed or WorkflowStep.Approved)
        {
            var doneResult = order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.InterpretedValue
                ?? order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.RawValue;
            var completed = BuildCompletedSteps(facts, definition, currentStep: null);
            return new CurrentStepDetails(
                new CurrentStepResult(null, definition.WorkflowType, null, true, doneResult, completed, totalSteps, allSteps),
                order, definition, facts);
        }

        if (AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
        {
            var isDone = facts.HasActiveAnalysis;
            var analysisFinalResult = isDone
                ? (order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.InterpretedValue
                   ?? order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.RawValue)
                : null;
            return new CurrentStepDetails(
                new CurrentStepResult(null, definition.WorkflowType, null, isDone, analysisFinalResult, new List<CompletedStepSummary>(), totalSteps, allSteps),
                order, definition, facts);
        }

        var step = FindFirstIncompleteStep(facts, definition);
        if (step is not null)
        {
            var openIncubation = facts.Incubations
                .Where(i => i.CompletedAt == null)
                .OrderByDescending(i => i.StartedAt)
                .FirstOrDefault(i => IsIncubationForStep(i, step));

            var completed = BuildCompletedSteps(facts, definition, currentStep: step);
            return new CurrentStepDetails(
                new CurrentStepResult(step, definition.WorkflowType, openIncubation, false, null, completed, totalSteps, allSteps),
                order, definition, facts);
        }

        var finalResult = order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.InterpretedValue
            ?? order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.RawValue;
        var allCompleted = BuildCompletedSteps(facts, definition, currentStep: null);
        return new CurrentStepDetails(
            new CurrentStepResult(null, definition.WorkflowType, null, true, finalResult, allCompleted, totalSteps, allSteps),
            order, definition, facts);
    }

    // Every step ordered before currentStep (or every step, once the
    // whole template is done) that IsStepDoneAsync confirms is actually
    // complete - drives the frontend's step-chain strip. Outcome is
    // always read back from that step's own closed Incubation.Outcome
    // (the summary RecordResultAsync/RecordBatchResultsAsync already
    // computed), never recomputed here.
    private static List<CompletedStepSummary> BuildCompletedSteps(TestStepFacts facts, TestDefinition definition, TestWorkflowStep? currentStep)
    {
        var summaries = new List<CompletedStepSummary>();
        var completedIncubations = facts.Incubations
            .Where(i => i.CompletedAt != null)
            .OrderByDescending(i => i.CompletedAt)
            .ToList();

        foreach (var step in definition.Steps.OrderBy(s => s.StepOrder))
        {
            if (currentStep is not null && step.StepOrder >= currentStep.StepOrder) break;
            if (!IsStepDone(facts, definition.WorkflowType, step)) continue;

            var latestIncubation = completedIncubations.FirstOrDefault(i => IsIncubationForStep(i, step));
            var outcome = latestIncubation?.Outcome ?? string.Empty;
            var observedAt = latestIncubation?.CompletedAt;

            string? reportedResult = null, status = null;
            decimal? calculatedResult = null;

            if (step.StepType == StepType.PlateCount)
            {
                var reading = facts.ActiveCountReadings
                    .Where(r => r.StepName == step.StepName)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();
                if (reading is not null)
                {
                    reportedResult = reading.ReportedResult;
                    calculatedResult = reading.CalculatedResult;
                    status = reading.Status;
                    observedAt = reading.EnteredAt;
                }
            }
            else if (step.StepType == StepType.BiochemicalTest)
            {
                // Previously left entirely blank here - a submitted
                // biochemical result and its Detected/NotDetected
                // interpretation were never visible past the analyst's own
                // dialog. ReportedResult carries the free-text result,
                // Status the explicit decision, so a reviewer/summary view
                // sees both without a new DTO field.
                var bioResult = facts.StepResults
                    .Where(r => r.StepName == step.StepName)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();
                if (bioResult is not null)
                {
                    reportedResult = bioResult.BiochemicalResultText;
                    status = bioResult.BiochemicalOrganismDetected is true ? ResultStatus.Detected
                        : bioResult.BiochemicalOrganismDetected is false ? ResultStatus.NotDetected
                        : null;
                    observedAt = bioResult.SubmittedAtUtc;
                }
            }

            summaries.Add(new CompletedStepSummary(
                step.StepOrder, step.StepName, step.StepType, step.IsFinalStep,
                outcome, observedAt,
                reportedResult, calculatedResult, status));
        }
        return summaries;
    }

    public async Task<List<SiblingPathogenOrderDto>> GetSiblingPathogenOrdersAsync(int testOrderId, CancellationToken ct = default)
    {
        var testOrder = await _db.TestOrders
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test order #{testOrderId} not found.");

        var siblings = await _db.TestOrders
            .Include(t => t.Sample)
            .Where(t => t.SampleId == testOrder.SampleId && t.Id != testOrderId && t.Status != ApprovalStatus.Approved && !t.IsSuperseded)
            .ToListAsync(ct);

        var testCodes = siblings.Select(t => t.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, ct);

        var result = new List<SiblingPathogenOrderDto>();
        foreach (var sib in siblings)
        {
            if (testDefs.TryGetValue(sib.TestCode, out var def))
            {
                var requiresBroth = def.Steps.Any(s =>
                    s.StepType == StepType.BrothEnrichment ||
                    s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));

                if (requiresBroth)
                {
                    result.Add(new SiblingPathogenOrderDto(
                        sib.Id,
                        def.DisplayName ?? sib.TestCode,
                        sib.TestCode));
                }
            }
        }

        return result;
    }

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        if (order.CurrentStep == WorkflowStep.Waiting)
        {
            await RequireSamplePreparedAsync(testOrderId, order.SampleId, "Advance", performedByUserId, order.CurrentStep);
        }

        var errors = await ValidateAsync(testOrderId);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var next = order.CurrentStep switch
        {
            WorkflowStep.Waiting => WorkflowStep.Incubating,
            WorkflowStep.Incubating => WorkflowStep.Ready,
            WorkflowStep.Ready => WorkflowStep.Reviewed,
            WorkflowStep.Reviewed => WorkflowStep.Approved,
            _ => order.CurrentStep
        };
        return await WorkflowStateMachine.TransitionAsync(_db, order, next, performedByUserId, note);
    }

    public async Task<List<string>> ValidateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = new List<string>();

        if (order.CurrentStep == WorkflowStep.Waiting)
        {
            var sample = await _db.Samples
                .Where(s => s.Id == order.SampleId)
                .Select(s => new { s.Category, s.PreparationStatus, s.ItemId, s.ReferenceNumber })
                .FirstOrDefaultAsync();

            if (sample != null && !await PreparationRules.TestOrderSkipsPreparationAsync(_db, testOrderId))
            {
                var isPrepared = sample.PreparationStatus == SamplePreparationStatus.Ready;
                if (isPrepared && sample.ItemId != null)
                    isPrepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == order.SampleId);

                if (!isPrepared)
                    errors.Add($"Test preparation must be completed and confirmed for sample {sample.ReferenceNumber} before the test can be started.");
                else if (sample.Category is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning)
                {
                    var hasLocations = await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId);
                    if (!hasLocations)
                        errors.Add("Preparation not complete - no locations assigned to this test.");
                }
            }
        }

        if (order.CurrentStep is WorkflowStep.Waiting or WorkflowStep.Incubating)
        {
            var current = await GetCurrentStepAsync(testOrderId);
            if (!current.AllStepsComplete)
                errors.Add("Not all workflow steps are complete for this test yet.");
        }

        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
