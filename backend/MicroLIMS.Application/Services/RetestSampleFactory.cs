using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services;

public sealed class RetestSampleFactory(IMicroLimsDbContext db, ReferenceNumberGenerator numbers, TimeProvider time)
{
    internal async Task<int> GetRetestCauseIdAsync(CancellationToken ct = default) =>
        await db.CausesOfTesting.Where(c => c.Name == "Retest").Select(c => (int?)c.Id).FirstOrDefaultAsync(ct)
        ?? throw new BusinessRuleException(MessageCodes.SampleRetestCauseNotConfigured,
            "The 'Retest' cause of testing is not configured.");

    public async Task<Sample> AddRetestSampleAsync(Sample origin, IReadOnlyList<TestOrder> orders,
        Func<TestOrder, int?> analystFor, string oosGroupCode, int causeId, int receivedBy, CancellationToken ct = default)
    {
        var sample = BuildRetestSample(origin, await numbers.GenerateAsync(origin.Category), causeId, receivedBy, oosGroupCode);
        var carriedAnyLocations = false;
        foreach (var order in orders)
        {
            var newOrder = new TestOrder
            {
                TestCode = order.TestCode,
                SectionId = order.SectionId,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Waiting,
                AssignedAnalystId = analystFor(order),
                RoomId = order.RoomId,
                IsSuperseded = false
            };
            sample.TestOrders.Add(newOrder);
            if (await CloneFailedLocationsAsync(origin.Category, order, sample, newOrder, ct))
                carriedAnyLocations = true;
        }

        // Batch retests with carried locations already have everything needed for testing.
        sample.PreparationStatus = carriedAnyLocations
            ? SamplePreparationStatus.Ready
            : await PreparationRules.InitialStatusAsync(db, sample.TestOrders.Select(o => o.SectionId));
        db.Samples.Add(sample);
        return sample;
    }

    // Copies the physical/identity attributes of a sample onto a fresh OOS
    // retest spin-off - deliberately does NOT copy SamplePreparation (the
    // new sample "starts fresh": NeedsPreparation, no prep row) or
    // TestOrders (the caller adds only the tests actually selected for
    // retest). OriginSampleId is the only link back to the sample whose
    // approval decision created it.
    private Sample BuildRetestSample(Sample original, string referenceNumber, int causeOfTestingId, int receivedByUserId, string oosGroupCode)
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
            PreviousProductItemId = original.PreviousProductItemId,
            CleaningTechniques = original.CleaningTechniques,
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
            ReceivedAt = time.GetUtcNow().UtcDateTime,
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
    private async Task<bool> CloneFailedLocationsAsync(SampleCategory category, TestOrder originalOrder, Sample newSample, TestOrder newOrder, CancellationToken ct)
    {
        if (category is not (SampleCategory.Water or SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning))
            return false;

        var originalLocations = await db.SampleLocations.AsNoTracking()
            .Where(l => l.TestOrderId == originalOrder.Id)
            .ToListAsync(ct);

        if (originalLocations.Count == 0)
            return false;

        // Conforming statuses mirror the test order outcome service. If none of the original locations actually failed (e.g.
        // the order was sent to retest for a procedural/OOS reason rather
        // than a location result), fall back to carrying every original
        // location rather than creating a TestOrder with no locations at
        // all - an empty batch order can't be tested.
        var failedLocations = originalLocations.Where(l => l.Status is not (ResultStatus.WithinLimits or ResultStatus.Absent)).ToList();
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
}
