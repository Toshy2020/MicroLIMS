using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: test workflow step. Behind TestWorkflowStepMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class TestWorkflowStepMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public TestWorkflowStepMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<TestDefinitionResponse> UpdateWorkflowTypeAsync(int id, UpdateWorkflowTypeRequest request)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Test {id} not found.");
        // Only the step-driven workflows (CountTest <-> Observation) can be
        // switched here; every other workflow is set by the test type and
        // paired with its equation in TestDefinitionMasterDataService.
        static bool StepDriven(WorkflowType w) => w is WorkflowType.CountTest or WorkflowType.Observation;
        var dedicatedEquation = entity.EquationType is EquationType.HplcMethodAssay or EquationType.IcpMethodAssay
            or EquationType.Dissolution or EquationType.Disintegration or EquationType.WeightVariation
            or EquationType.Titration or EquationType.Measurement or EquationType.Qualitative
            or EquationType.GravimetricLoss or EquationType.GravimetricResidue;
        if (!StepDriven(entity.WorkflowType) || !StepDriven(request.WorkflowType) || dedicatedEquation)
            throw new InvalidOperationException("The workflow type of this test is set by its test type; change it in the test settings.");
        entity.WorkflowType = request.WorkflowType;
        await _db.SaveChangesAsync();
        return TestDefinitionResponse.From(entity);
    }

    public async Task<object> GetTestWorkflowStepsAsync(int id) =>
        await _db.TestWorkflowSteps
            .Where(s => s.TestDefinitionId == id)
            .OrderBy(s => s.StepOrder)
            .Select(s => new
            {
                s.Id, s.Version, s.StepOrder, s.StepName, s.PhenotypicTestType,
                phenotypicTestTypes = s.PhenotypicTests.OrderBy(t => t.DisplayOrder).Select(t => t.PhenotypicTestType),
                s.IncubationMinHours, s.IncubationMaxHours, s.TemperatureMin, s.TemperatureMax,
                s.IsFinalStep,
                stepType = s.StepType.ToString(),
                s.TargetOrganismId,
                targetOrganism = s.TargetOrganism == null ? null : new { s.TargetOrganism.Id, name = s.TargetOrganism.ScientificName },
                s.ConfirmatoryMediaCount,
                stepMedia = s.StepMedia.OrderBy(m => m.DisplayOrder).Select(m => new
                {
                    stepMediaId = m.Id, m.MaterialId, materialName = m.Material!.MaterialName,
                    // The operative incubation window/temperature for this
                    // medium at execution time (see TestWorkflowEngine.cs) -
                    // not the step's own IncubationMinHours/MaxHours/
                    // TemperatureMin/Max below, which are no longer read.
                    m.TempMin, m.TempMax, m.IncubationMinHours, m.IncubationMaxHours,
                    m.IsRequired, m.DisplayOrder,
                    m.MediaIncubationConditionId
                }),
                s.RequiresIncubationTransfer,
                incubationStages = s.IncubationStages.OrderBy(x => x.StageNumber).Select(x => new
                {
                    x.StageNumber, x.TempMin, x.TempMax, x.IncubationMinHours, x.IncubationMaxHours
                })
            })
            .ToListAsync();

    public async Task<object> CreateTestWorkflowStepAsync(int id, CreateTestWorkflowStepRequest request)
    {
        var test = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Test {id} not found.");
        if (AnalysisWorkflows.UsesTestAnalysis(test.WorkflowType))
            throw new InvalidOperationException($"{test.WorkflowType} tests have no workflow steps.");

        var nextOrder = 1 + await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == id)
            .Select(s => (int?)s.StepOrder).MaxAsync() ?? 1;

        var stepMedias = await BuildStepMediaAsync(request.StepMedia);
        var firstMedia = stepMedias.OrderBy(m => m.DisplayOrder).FirstOrDefault();
        var tempMin = request.TemperatureMin > 0 ? request.TemperatureMin : (firstMedia?.TempMin ?? 0);
        var tempMax = request.TemperatureMax > 0 ? request.TemperatureMax : (firstMedia?.TempMax ?? 0);
        var incMin = request.IncubationMinHours > 0 ? request.IncubationMinHours : (firstMedia?.IncubationMinHours ?? 0);
        var incMax = request.IncubationMaxHours > 0 ? request.IncubationMaxHours : (firstMedia?.IncubationMaxHours ?? 0);

        var entity = new TestWorkflowStep
        {
            TestDefinitionId = id, StepOrder = nextOrder, StepName = request.StepName,
            IncubationMinHours = incMin, IncubationMaxHours = incMax,
            TemperatureMin = tempMin, TemperatureMax = tempMax,
            IsFinalStep = request.IsFinalStep, StepType = request.StepType, TargetOrganismId = request.TargetOrganismId,
            RequiresIncubationTransfer = request.RequiresIncubationTransfer,
            ConfirmatoryMediaCount = request.ConfirmatoryMediaCount ?? 1,
            PhenotypicTestType = request.PhenotypicTestType
        };
        entity.StepMedia.AddRange(stepMedias);
        entity.IncubationStages.AddRange((request.IncubationStages ?? new()).Select(s => new TestWorkflowStepIncubationStage
        {
            StageNumber = s.StageNumber, TempMin = s.TempMin, TempMax = s.TempMax,
            IncubationMinHours = s.IncubationMinHours, IncubationMaxHours = s.IncubationMaxHours
        }));
        entity.PhenotypicTests.AddRange((request.PhenotypicTestTypes ?? new()).Select((t, i) => new TestWorkflowStepPhenotypicTest
        {
            PhenotypicTestType = t, DisplayOrder = i
        }));

        await ValidateStepRulesAsync(id, excludeStepId: null, entity);

        _db.TestWorkflowSteps.Add(entity);
        await _db.SaveChangesAsync();
        await ValidateContiguousStepOrderAsync(id);
        return new { entity.Id, entity.StepOrder, entity.StepName };
    }

    // Editing is always allowed, even for a step already used by a real
    // TestOrder - unlike delete, it can't corrupt anything, since a
    // completed Incubation row already has its own copied-at-the-time
    // Temperature/Duration and isn't affected retroactively.
    public async Task<object> UpdateTestWorkflowStepAsync(int stepId, UpdateTestWorkflowStepRequest request)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.StepMedia).Include(s => s.IncubationStages).Include(s => s.PhenotypicTests)
            .FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new NotFoundException($"Workflow step {stepId} not found.");
        RecordVersion.EnsureCurrent(_db, step);

        var rebuiltStepMedia = await BuildStepMediaAsync(request.StepMedia);
        var firstMedia = rebuiltStepMedia.OrderBy(m => m.DisplayOrder).FirstOrDefault();
        var tempMin = request.TemperatureMin > 0 ? request.TemperatureMin : (firstMedia?.TempMin ?? 0);
        var tempMax = request.TemperatureMax > 0 ? request.TemperatureMax : (firstMedia?.TempMax ?? 0);
        var incMin = request.IncubationMinHours > 0 ? request.IncubationMinHours : (firstMedia?.IncubationMinHours ?? 0);
        var incMax = request.IncubationMaxHours > 0 ? request.IncubationMaxHours : (firstMedia?.IncubationMaxHours ?? 0);

        step.StepName = request.StepName;
        step.IncubationMinHours = incMin;
        step.IncubationMaxHours = incMax;
        step.TemperatureMin = tempMin;
        step.TemperatureMax = tempMax;
        step.IsFinalStep = request.IsFinalStep;
        step.StepType = request.StepType;
        step.TargetOrganismId = request.TargetOrganismId;
        step.RequiresIncubationTransfer = request.RequiresIncubationTransfer;
        step.ConfirmatoryMediaCount = request.ConfirmatoryMediaCount ?? 1;
        step.PhenotypicTestType = request.PhenotypicTestType;

        // StepMedia is replaced wholesale on update - the analyst edits the
        // panel as a set, and the unique index makes incremental merging
        // error-prone for no benefit.
        _db.TestWorkflowStepMedias.RemoveRange(step.StepMedia);
        step.StepMedia.Clear();
        foreach (var m in rebuiltStepMedia) m.TestWorkflowStepId = step.Id;
        step.StepMedia.AddRange(rebuiltStepMedia);

        _db.TestWorkflowStepIncubationStages.RemoveRange(step.IncubationStages);
        step.IncubationStages.Clear();
        step.IncubationStages.AddRange((request.IncubationStages ?? new()).Select(s => new TestWorkflowStepIncubationStage
        {
            TestWorkflowStepId = step.Id, StageNumber = s.StageNumber, TempMin = s.TempMin, TempMax = s.TempMax,
            IncubationMinHours = s.IncubationMinHours, IncubationMaxHours = s.IncubationMaxHours
        }));

        // Replaced wholesale, same reasoning as StepMedia above - only when
        // the client actually sends the new list field. A null
        // PhenotypicTestTypes (an older client still using only the single
        // field) leaves the existing bundled list untouched rather than
        // wiping it on every edit.
        if (request.PhenotypicTestTypes is not null)
        {
            _db.TestWorkflowStepPhenotypicTests.RemoveRange(step.PhenotypicTests);
            step.PhenotypicTests.Clear();
            step.PhenotypicTests.AddRange(request.PhenotypicTestTypes.Select((t, i) => new TestWorkflowStepPhenotypicTest
            {
                TestWorkflowStepId = step.Id, PhenotypicTestType = t, DisplayOrder = i
            }));
        }

        await ValidateStepRulesAsync(step.TestDefinitionId, excludeStepId: stepId, step);
        await _db.SaveChangesAsync();

        return new { step.Id, step.StepOrder, step.StepName };
    }

    // Swaps StepOrder with the adjacent step - simpler and safer than
    // accepting an arbitrary new position from the client (no risk of
    // gaps or duplicate StepOrder values from a bad request). Staged
    // through a temporary out-of-range value across two SaveChanges
    // calls - a single-batch swap of two rows sharing the unique
    // (TestDefinitionId, StepOrder) index makes EF's change tracker
    // throw "circular dependency detected" since it can't find a safe
    // statement order for a direct swap against that index.
    public Task<object> MoveTestWorkflowStepAsync(int stepId, MoveTestWorkflowStepRequest request) =>
        UnitOfWork.RunAsync(_db, () => MoveTestWorkflowStepCoreAsync(stepId, request));

    private async Task<object> MoveTestWorkflowStepCoreAsync(int stepId, MoveTestWorkflowStepRequest request)
    {
        var step = await _db.TestWorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new NotFoundException($"Workflow step {stepId} not found.");

        var neighborOrder = request.Direction == "up" ? step.StepOrder - 1 : step.StepOrder + 1;
        var neighbor = await _db.TestWorkflowSteps.FirstOrDefaultAsync(s => s.TestDefinitionId == step.TestDefinitionId && s.StepOrder == neighborOrder);
        if (neighbor is null)
            throw new InvalidOperationException("This step is already at that end of the sequence.");

        var stepOrder = step.StepOrder;
        var neighborStepOrder = neighbor.StepOrder;

        step.StepOrder = -1; // StepOrder is always >= 1, so this never collides
        await _db.SaveChangesAsync();

        neighbor.StepOrder = stepOrder;
        step.StepOrder = neighborStepOrder;
        await _db.SaveChangesAsync();
        await ValidateContiguousStepOrderAsync(step.TestDefinitionId);

        return new { };
    }

    // Blocked if any real TestOrder for this test code has already used
    // this step (an Incubation row referencing it exists) - same
    // "guard with a clear message" pattern as ItemService.DeleteAsync.
    public async Task<object> DeleteTestWorkflowStepAsync(int stepId)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.TestDefinition).FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new NotFoundException($"Workflow step {stepId} not found.");

        var inUse = await _db.Incubations.Include(i => i.TestOrder)
            .AnyAsync(i => i.StepName == step.StepName && i.TestOrder!.TestCode == step.TestDefinition!.Code);
        if (inUse)
            throw new InvalidOperationException($"Cannot delete step \"{step.StepName}\" - it has already been used by a test order.");

        _db.TestWorkflowSteps.Remove(step);

        // Close the gap left behind so "contiguous from 1" (Gap 4) stays
        // true afterward instead of only being checked at create/move time.
        var laterSteps = await _db.TestWorkflowSteps
            .Where(s => s.TestDefinitionId == step.TestDefinitionId && s.StepOrder > step.StepOrder)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
        foreach (var laterStep in laterSteps)
            laterStep.StepOrder -= 1;

        await _db.SaveChangesAsync();
        return new { };
    }

    // TempMin/Max and IncubationMinHours/MaxHours are copied from the chosen
    // MediaIncubationCondition at save time - single source of truth,
    // conditions are locked once used.
    private async Task<List<TestWorkflowStepMedia>> BuildStepMediaAsync(List<StepMediaRequest> requests)
    {
        var materialIds = requests.Select(m => m.MaterialId).Distinct().ToList();
        var materials = await _db.Materials.Where(m => materialIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        var conditionIds = requests.Where(m => m.MediaIncubationConditionId.HasValue)
            .Select(m => m.MediaIncubationConditionId!.Value)
            .Distinct()
            .ToList();
        var conditions = await _db.MediaIncubationConditions.Where(c => conditionIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var result = new List<TestWorkflowStepMedia>();
        foreach (var m in requests)
        {
            if (!materials.TryGetValue(m.MaterialId, out var material))
                throw new InvalidOperationException($"Material {m.MaterialId} not found.");

            if (!m.MediaIncubationConditionId.HasValue)
                throw new InvalidOperationException($"Choose an incubation condition for medium '{material.MaterialName}'.");

            if (material.MediaProductId is null)
                throw new InvalidOperationException($"'{material.MaterialName}' isn't linked to a media product. Link it in Inventory > Materials Stock first.");

            if (!conditions.TryGetValue(m.MediaIncubationConditionId.Value, out var condition))
                throw new InvalidOperationException($"Incubation condition {m.MediaIncubationConditionId.Value} not found.");

            if (condition.MediaProductId != material.MediaProductId.Value)
                throw new InvalidOperationException($"The incubation condition chosen for '{material.MaterialName}' belongs to a different media product.");

            var entity = new TestWorkflowStepMedia
            {
                MaterialId = m.MaterialId,
                IsRequired = m.IsRequired,
                DisplayOrder = m.DisplayOrder,
                MediaIncubationConditionId = condition.Id,
                TempMin = condition.TemperatureMin,
                TempMax = condition.TemperatureMax,
                IncubationMinHours = condition.IncubationMinHours,
                IncubationMaxHours = condition.IncubationMaxHours
            };
            result.Add(entity);
        }
        return result;
    }

    // Structural rules come from WorkflowTemplateValidator; this adds the
    // one rule that spans the whole template rather than a single step.
    private async Task ValidateStepRulesAsync(int testDefinitionId, int? excludeStepId, TestWorkflowStep candidate)
    {
        var errors = WorkflowTemplateValidator.Validate(candidate);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ",
                errors.Select(e => $"Rule {e.RuleNumber} ({e.StepName}): {e.Message}")));

        if (candidate.IsFinalStep)
        {
            var otherFinalExists = await _db.TestWorkflowSteps
                .AnyAsync(s => s.TestDefinitionId == testDefinitionId && s.IsFinalStep && s.Id != (excludeStepId ?? -1));
            if (otherFinalExists)
                throw new InvalidOperationException("Only one step per test can be marked as the final step.");
        }
    }

    // Post-condition guard for the "no gaps, no duplicates" invariant -
    // Create always appends and Move always swaps two adjacent orders,
    // both of which preserve contiguity by construction, but this makes
    // that invariant an enforced, checked fact rather than an assumption.
    private async Task ValidateContiguousStepOrderAsync(int testDefinitionId)
    {
        var orders = await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == testDefinitionId)
            .OrderBy(s => s.StepOrder).Select(s => s.StepOrder).ToListAsync();
        for (var i = 0; i < orders.Count; i++)
        {
            if (orders[i] != i + 1)
                throw new InvalidOperationException("Workflow steps must have contiguous step numbers starting from 1.");
        }
    }
}
