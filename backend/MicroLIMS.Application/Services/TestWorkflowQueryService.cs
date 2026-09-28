using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Read-side lookups behind TestWorkflowController that are not engine
// commands: what the entry screens need to show before the analyst acts.
public class TestWorkflowQueryService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IncubatorEligibilityService _incubatorEligibility;
    private readonly MediaAppearanceSnapshotService _appearanceSnapshot;
    private readonly TimeProvider _time;

    public TestWorkflowQueryService(
        IMicroLimsDbContext db,
        IncubatorEligibilityService incubatorEligibility,
        MediaAppearanceSnapshotService appearanceSnapshot,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _incubatorEligibility = incubatorEligibility;
        _appearanceSnapshot = appearanceSnapshot;
        _time = timeProvider ?? TimeProvider.System;
    }

    // The response mode and how many sample preparations the sample's stage
    // requires. Message is set when the stage can't be resolved; entry is
    // still validated server-side on submit.
    public async Task<StandardComparisonContextDto> GetStandardComparisonContextAsync(int testOrderId, CancellationToken ct = default)
    {
        var testCode = await _db.TestOrders.Where(o => o.Id == testOrderId).Select(o => o.TestCode).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var definition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode, ct)
            ?? throw new InvalidOperationException($"Test code '{testCode}' is not in the Test Master.");
        if (definition.WorkflowType != WorkflowType.StandardComparison)
            throw new InvalidOperationException($"Test \"{testCode}\" is not a standard-comparison test.");

        var resolution = await StageReplicateResolver.ResolveForTestOrderAsync(_db, testOrderId, ct);
        return new StandardComparisonContextDto(
            definition.ResponseMode.ToString(),
            resolution.StageRole?.ToString(),
            resolution.IsConfigured ? resolution.SampleReplicates : null,
            resolution.IsConfigured ? resolution.StandardReplicates : null,
            StandardComparisonCalculator.SampleWeighInTolerancePercent,
            definition.HplcMaxPreparationRsdPercent,
            resolution.IsConfigured ? null : resolution.Message);
    }

    public async Task<object> GetEligibleIncubatorsAsync(int stepMediaId)
    {
        var incubators = await _incubatorEligibility.GetEligibleIncubatorsAsync(stepMediaId);
        var stepMedia = await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.Id == stepMediaId)
            ?? throw new NotFoundException($"Step media {stepMediaId} not found.");

        return new
        {
            stepMediaId,
            tempMin = stepMedia.TempMin,
            tempMax = stepMedia.TempMax,
            eligibleIncubators = incubators.Select(i => new
            {
                i.Id, name = i.Name, code = i.Code, setTemperature = i.SetTemperature, calibrationStatus = i.CalibrationStatus
            })
        };
    }

    public async Task<object> GetPermittedConfirmatoryMediaAsync(int testOrderId, string stepName)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var step = await _db.TestWorkflowSteps
            .Include(s => s.StepMedia).ThenInclude(m => m.Material)
            .Include(s => s.TargetOrganism)
            .Include(s => s.TestDefinition)
            .FirstOrDefaultAsync(s => s.TestDefinition!.Code == order.TestCode && s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");

        var now = _time.GetUtcNow().UtcDateTime;
        var permitted = new List<object>();
        foreach (var medium in step.StepMedia.OrderBy(m => m.DisplayOrder))
        {
            var expected = step.TargetOrganismId is int organismId
                ? await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(medium.MaterialId, organismId)
                : null;

            var lots = await _db.Media
                .Where(m => m.MaterialId == medium.MaterialId && m.IsReleasedForUse && m.Status == MediaStatus.Active && m.ExpiryDate > now)
                .OrderBy(m => m.ExpiryDate)
                .Select(m => new { m.Id, lotNumber = m.LotNumber, expiryDate = m.ExpiryDate })
                .ToListAsync();

            permitted.Add(new
            {
                stepMediaId = medium.Id,
                materialId = medium.MaterialId,
                mediaName = medium.Material!.MaterialName,
                expectedAppearance = expected,
                tempMin = medium.TempMin,
                tempMax = medium.TempMax,
                availableLots = lots
            });
        }

        return new
        {
            testOrderId,
            stepName,
            organism = step.TargetOrganism is null ? null : new { step.TargetOrganism.Id, name = step.TargetOrganism.ScientificName },
            permittedMedia = permitted
        };
    }

    // Null when the result does not exist; the engine then reports it.
    public Task<int?> FindTestOrderIdForStepResultAsync(int workflowStepResultId) =>
        _db.WorkflowStepResults
            .AsNoTracking()
            .Where(w => w.Id == workflowStepResultId)
            .Select(w => (int?)w.TestOrderId)
            .FirstOrDefaultAsync();
}
