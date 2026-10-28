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
            .Include(s => s.StepMedia).ThenInclude(m => m.MediaProduct)
            .Include(s => s.TargetOrganism)
            .Include(s => s.TestDefinition)
            .FirstOrDefaultAsync(s => s.TestDefinition!.Code == order.TestCode && s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");

        // The run still awaiting its plate readings - the same row
        // SubmitConfirmatoryObservationsAsync reads; null before plating starts.
        var awaitingReadout = await _db.WorkflowStepResults
            .AsNoTracking()
            .Include(r => r.Selections).ThenInclude(x => x.Media).ThenInclude(m => m!.Material)
            .Where(r => r.TestOrderId == testOrderId && r.StepName == stepName && r.ConfirmatoryResult == null)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        var now = _time.GetUtcNow().UtcDateTime;
        var permitted = new List<object>();
        foreach (var medium in step.StepMedia.OrderBy(m => m.DisplayOrder))
        {
            var expected = step.TargetOrganismId is int organismId
                ? await _appearanceSnapshot.GetExpectedAppearanceSnapshotForProductAsync(medium.MediaProductId, organismId)
                : null;

            var lots = await _db.Media
                .Where(m => m.Material!.MediaProductId == medium.MediaProductId && m.IsReleasedForUse && m.Status == MediaStatus.Active && m.ExpiryDate > now)
                .OrderBy(m => m.ExpiryDate)
                .Select(m => new { m.Id, lotNumber = m.LotNumber, expiryDate = m.ExpiryDate, materialId = m.MaterialId })
                .ToListAsync();

            // What the analyst actually plated for this medium: the observation is
            // recorded against this lot's material, not the product.
            var selected = awaitingReadout?.Selections
                .FirstOrDefault(x => x.Media?.Material?.MediaProductId == medium.MediaProductId);

            permitted.Add(new
            {
                stepMediaId = medium.Id,
                mediaProductId = medium.MediaProductId,
                mediaName = medium.MediaProduct!.Name,
                expectedAppearance = expected,
                tempMin = medium.TempMin,
                tempMax = medium.TempMax,
                availableLots = lots,
                selection = selected is null ? null : new { materialId = selected.MaterialId, mediaId = selected.MediaId, lotNumber = selected.Media!.LotNumber, equipmentId = selected.EquipmentId }
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
