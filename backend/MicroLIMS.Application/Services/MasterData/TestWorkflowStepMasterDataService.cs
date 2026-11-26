using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: test workflow step. Behind TestWorkflowStepMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class TestWorkflowStepMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly ChangeControlGate _gate;

    public TestWorkflowStepMasterDataService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        ChangeControlGate? changeControlGate = null)
    {
        _db = db;
        _scope = scope;
        _gate = changeControlGate ?? new ChangeControlGate(db, NullChangeControlContext.Instance, TimeProvider.System);
    }

    public Task<TestDefinitionResponse> UpdateWorkflowTypeAsync(int currentUserId, int id, UpdateWorkflowTypeRequest request, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync(_db, async () =>
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.WorkflowStepTestNotFound,
                $"Test {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        await TestConfigurationScope.EnsureCanConfigureAsync(_scope, currentUserId, entity.SectionId, cancellationToken);
        RecordVersion.EnsureCurrent(_db, entity);
        var grant = await _gate.EnsureAllowedAsync(entity.SectionId, QualityLinkTargetType.TestDefinition, entity.Id, entity.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");
        // Only the step-driven workflows (CountTest <-> Observation) can be
        // switched here; every other workflow is set by the test type and
        // paired with its equation in TestDefinitionMasterDataService.
        static bool StepDriven(WorkflowType w) => w is WorkflowType.CountTest or WorkflowType.Observation;
        var dedicatedEquation = entity.EquationType is EquationType.HplcMethodAssay or EquationType.IcpMethodAssay
            or EquationType.Dissolution or EquationType.Disintegration or EquationType.WeightVariation
            or EquationType.Titration or EquationType.Measurement or EquationType.Qualitative
            or EquationType.GravimetricLoss or EquationType.GravimetricResidue
            or EquationType.UvSpectrophotometry;
        if (!StepDriven(entity.WorkflowType) || !StepDriven(request.WorkflowType) || dedicatedEquation)
            throw new BusinessRuleException(
                MessageCodes.WorkflowStepTestWorkflowTypeLocked,
                "The workflow type of this test is set by its test type; change it in the test settings.");
        entity.WorkflowType = request.WorkflowType;
        await _db.SaveChangesAsync(cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestDefinition), entity.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return TestDefinitionResponse.From(entity);
    }, cancellationToken);

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
                    stepMediaId = m.Id, m.MediaProductId, mediaProductName = m.MediaProduct!.Name,
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

    public Task<object> CreateTestWorkflowStepAsync(int currentUserId, int id, CreateTestWorkflowStepRequest request, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync<object>(_db, async () =>
    {
        var test = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.WorkflowStepTestNotFound,
                $"Test {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        await TestConfigurationScope.EnsureCanConfigureAsync(_scope, currentUserId, test.SectionId, cancellationToken);
        if (AnalysisWorkflows.UsesTestAnalysis(test.WorkflowType))
            throw new BusinessRuleException(
                MessageCodes.WorkflowStepWorkflowTypeHasNoSteps,
                $"{test.WorkflowType} tests have no workflow steps.",
                new Dictionary<string, string> { ["workflowType"] = test.WorkflowType.ToString() });
        var grant = await _gate.EnsureAllowedAsync(test.SectionId, QualityLinkTargetType.TestDefinition, test.Id, test.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        var nextOrder = 1 + await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == id)
            .Select(s => (int?)s.StepOrder).MaxAsync(cancellationToken) ?? 1;

        var stepMedias = await BuildStepMediaAsync(request.StepMedia, cancellationToken: cancellationToken);
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

        await ValidateStepRulesAsync(id, excludeStepId: null, entity, cancellationToken);

        _db.TestWorkflowSteps.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await ValidateContiguousStepOrderAsync(id, cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestWorkflowStep), entity.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return new { entity.Id, entity.StepOrder, entity.StepName };
    }, cancellationToken);

    // Editing is always allowed, even for a step already used by a real
    // TestOrder - unlike delete, it can't corrupt anything, since a
    // completed Incubation row already has its own copied-at-the-time
    // Temperature/Duration and isn't affected retroactively.
    public Task<object> UpdateTestWorkflowStepAsync(int currentUserId, int stepId, UpdateTestWorkflowStepRequest request, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync<object>(_db, async () =>
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.TestDefinition).Include(s => s.StepMedia).Include(s => s.IncubationStages).Include(s => s.PhenotypicTests)
            .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.WorkflowStepStepNotFound,
                $"Workflow step {stepId} not found.",
                new Dictionary<string, string> { ["stepId"] = stepId.ToString(CultureInfo.InvariantCulture) });
        await EnsureCanConfigureStepAsync(currentUserId, step, cancellationToken);
        RecordVersion.EnsureCurrent(_db, step);
        var grant = await _gate.EnsureAllowedAsync(step.TestDefinition!.SectionId, QualityLinkTargetType.TestDefinition, step.TestDefinitionId, step.TestDefinition.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        var rebuiltStepMedia = await BuildStepMediaAsync(
            request.StepMedia, step.StepMedia.Where(m => m.MediaProductId != null).Select(m => m.MediaProductId!.Value).ToHashSet(), cancellationToken);
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

        await ValidateStepRulesAsync(step.TestDefinitionId, excludeStepId: stepId, step, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestWorkflowStep), step.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);

        return new { step.Id, step.StepOrder, step.StepName };
    }, cancellationToken);

    // Swaps StepOrder with the adjacent step - simpler and safer than
    // accepting an arbitrary new position from the client (no risk of
    // gaps or duplicate StepOrder values from a bad request). Staged
    // through a temporary out-of-range value across two SaveChanges
    // calls - a single-batch swap of two rows sharing the unique
    // (TestDefinitionId, StepOrder) index makes EF's change tracker
    // throw "circular dependency detected" since it can't find a safe
    // statement order for a direct swap against that index.
    public Task<object> MoveTestWorkflowStepAsync(int currentUserId, int stepId, MoveTestWorkflowStepRequest request, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync(_db, () => MoveTestWorkflowStepCoreAsync(currentUserId, stepId, request, cancellationToken));

    private async Task<object> MoveTestWorkflowStepCoreAsync(int currentUserId, int stepId, MoveTestWorkflowStepRequest request, CancellationToken cancellationToken)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.TestDefinition).FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.WorkflowStepStepNotFound,
                $"Workflow step {stepId} not found.",
                new Dictionary<string, string> { ["stepId"] = stepId.ToString(CultureInfo.InvariantCulture) });
        // Scope and the moved step's version are checked before the neighbour is even loaded,
        // so nothing tracked has been changed when either refusal is thrown.
        await EnsureCanConfigureStepAsync(currentUserId, step, cancellationToken);
        RecordVersion.EnsureCurrent(_db, step);
        var grant = await _gate.EnsureAllowedAsync(step.TestDefinition!.SectionId, QualityLinkTargetType.TestDefinition, step.TestDefinitionId, step.TestDefinition.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        var neighborOrder = request.Direction == "up" ? step.StepOrder - 1 : step.StepOrder + 1;
        var neighbor = await _db.TestWorkflowSteps.FirstOrDefaultAsync(s => s.TestDefinitionId == step.TestDefinitionId && s.StepOrder == neighborOrder, cancellationToken);
        if (neighbor is null)
            throw new BusinessRuleException(
                MessageCodes.WorkflowStepStepAlreadyAtEnd,
                "This step is already at that end of the sequence.");

        var stepOrder = step.StepOrder;
        var neighborStepOrder = neighbor.StepOrder;

        step.StepOrder = -1; // StepOrder is always >= 1, so this never collides
        await _db.SaveChangesAsync(cancellationToken);

        neighbor.StepOrder = stepOrder;
        step.StepOrder = neighborStepOrder;
        await _db.SaveChangesAsync(cancellationToken);
        await ValidateContiguousStepOrderAsync(step.TestDefinitionId, cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestWorkflowStep), step.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);

        return new { };
    }

    // Blocked if any real TestOrder for this test code has already used
    // this step (an Incubation row referencing it exists) - same
    // "guard with a clear message" pattern as ItemService.DeleteAsync.
    public Task<object> DeleteTestWorkflowStepAsync(int currentUserId, int stepId, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync<object>(_db, async () =>
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.TestDefinition).FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.WorkflowStepStepNotFound,
                $"Workflow step {stepId} not found.",
                new Dictionary<string, string> { ["stepId"] = stepId.ToString(CultureInfo.InvariantCulture) });
        await EnsureCanConfigureStepAsync(currentUserId, step, cancellationToken);
        RecordVersion.EnsureCurrent(_db, step);
        var grant = await _gate.EnsureAllowedAsync(step.TestDefinition!.SectionId, QualityLinkTargetType.TestDefinition, step.TestDefinitionId, step.TestDefinition.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        var inUse = await _db.Incubations.Include(i => i.TestOrder)
            .AnyAsync(i => i.StepName == step.StepName && i.TestOrder!.TestCode == step.TestDefinition!.Code, cancellationToken);
        if (inUse)
            throw new BusinessRuleException(
                MessageCodes.WorkflowStepStepCannotDeleteUsed,
                $"Cannot delete step \"{step.StepName}\" - it has already been used by a test order.",
                new Dictionary<string, string> { ["stepName"] = step.StepName });

        _db.TestWorkflowSteps.Remove(step);

        // Close the gap left behind so "contiguous from 1" (Gap 4) stays
        // true afterward instead of only being checked at create/move time.
        var laterSteps = await _db.TestWorkflowSteps
            .Where(s => s.TestDefinitionId == step.TestDefinitionId && s.StepOrder > step.StepOrder)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);
        foreach (var laterStep in laterSteps)
            laterStep.StepOrder -= 1;

        await _db.SaveChangesAsync(cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestWorkflowStep), step.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return new { };
    }, cancellationToken);

    // A step belongs to the laboratory of its test.
    private Task EnsureCanConfigureStepAsync(int currentUserId, TestWorkflowStep step, CancellationToken cancellationToken) =>
        step.TestDefinition is { } test
            ? TestConfigurationScope.EnsureCanConfigureAsync(_scope, currentUserId, test.SectionId, cancellationToken)
            : throw new NotFoundException(
                MessageCodes.WorkflowStepTestNotFound,
                $"Test {step.TestDefinitionId} not found.",
                new Dictionary<string, string> { ["id"] = step.TestDefinitionId.ToString(CultureInfo.InvariantCulture) });

    // TempMin/Max and IncubationMinHours/MaxHours are copied from the chosen
    // MediaIncubationCondition at save time - single source of truth,
    // conditions are locked once used.
    // Each medium is a Culture Media product (never a stock lot). An inactive
    // product can't be newly chosen; one already on the step being edited
    // (alreadyOnStep) keeps working so an untouched row can be saved again.
    private async Task<List<TestWorkflowStepMedia>> BuildStepMediaAsync(
        List<StepMediaRequest> requests, IReadOnlySet<int>? alreadyOnStep = null, CancellationToken cancellationToken = default)
    {
        var productIds = requests.Select(m => m.MediaProductId).Distinct().ToList();
        var products = await _db.MediaProducts.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var conditionIds = requests.Where(m => m.MediaIncubationConditionId.HasValue)
            .Select(m => m.MediaIncubationConditionId!.Value)
            .Distinct()
            .ToList();
        var conditions = await _db.MediaIncubationConditions.Where(c => conditionIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var result = new List<TestWorkflowStepMedia>();
        foreach (var m in requests)
        {
            if (!products.TryGetValue(m.MediaProductId, out var product))
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepMediaProductNotFound,
                $"Media product {m.MediaProductId} not found.",
                new Dictionary<string, string> { ["mediaProductId"] = m.MediaProductId.ToString(CultureInfo.InvariantCulture) });

            if (!product.IsActive && alreadyOnStep?.Contains(product.Id) != true)
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepMediaProductInactive,
                $"Media product '{product.Name}' is inactive and can't be added to a test step.",
                new Dictionary<string, string> { ["name"] = product.Name });

            if (!m.MediaIncubationConditionId.HasValue)
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepIncubationConditionMissingForMedium,
                $"Choose an incubation condition for medium '{product.Name}'.",
                new Dictionary<string, string> { ["name"] = product.Name });

            if (!conditions.TryGetValue(m.MediaIncubationConditionId.Value, out var condition))
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepIncubationConditionNotFound,
                $"Incubation condition {m.MediaIncubationConditionId.Value} not found.",
                new Dictionary<string, string> { ["id"] = m.MediaIncubationConditionId.Value.ToString(CultureInfo.InvariantCulture) });

            if (condition.MediaProductId != product.Id)
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepIncubationConditionChosenWrongProduct,
                $"The incubation condition chosen for '{product.Name}' belongs to a different media product.",
                new Dictionary<string, string> { ["name"] = product.Name });

            var entity = new TestWorkflowStepMedia
            {
                MediaProductId = product.Id,
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
    private async Task ValidateStepRulesAsync(int testDefinitionId, int? excludeStepId, TestWorkflowStep candidate, CancellationToken cancellationToken)
    {
        var errors = WorkflowTemplateValidator.Validate(candidate);
        if (errors.Count > 0)
            throw new BusinessRuleException(
                MessageCodes.WorkflowStepTemplateValidationFailed,
                string.Join(" ",
                    errors.Select(e => $"Rule {e.RuleNumber} ({e.StepName}): {e.Message}")),
                new Dictionary<string, string>
                {
                    ["message"] = string.Join(" ",
                        errors.Select(e => $"Rule {e.RuleNumber} ({e.StepName}): {e.Message}"))
                });

        if (candidate.IsFinalStep)
        {
            var otherFinalExists = await _db.TestWorkflowSteps
                .AnyAsync(s => s.TestDefinitionId == testDefinitionId && s.IsFinalStep && s.Id != (excludeStepId ?? -1), cancellationToken);
            if (otherFinalExists)
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepFinalStepAlreadyExists,
                "Only one step per test can be marked as the final step.");
        }
    }

    // Post-condition guard for the "no gaps, no duplicates" invariant -
    // Create always appends and Move always swaps two adjacent orders,
    // both of which preserve contiguity by construction, but this makes
    // that invariant an enforced, checked fact rather than an assumption.
    private async Task ValidateContiguousStepOrderAsync(int testDefinitionId, CancellationToken cancellationToken)
    {
        var orders = await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == testDefinitionId)
            .OrderBy(s => s.StepOrder).Select(s => s.StepOrder).ToListAsync(cancellationToken);
        for (var i = 0; i < orders.Count; i++)
        {
            if (orders[i] != i + 1)
                throw new BusinessRuleException(
                MessageCodes.WorkflowStepContiguousStepNumbers,
                "Workflow steps must have contiguous step numbers starting from 1.");
        }
    }
}
