using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public sealed record TestOrderOutcome(bool Fails, ResultStatus? Status);

public interface ITestOrderOutcomeService
{
    Task<TestOrderOutcome> GetOutcomeAsync(int testOrderId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, TestOrderOutcome>> GetOutcomesAsync(
        IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default);
}

public sealed class TestOrderOutcomeService(IMicroLimsDbContext db) : ITestOrderOutcomeService
{
    public async Task<TestOrderOutcome> GetOutcomeAsync(int testOrderId, CancellationToken ct = default)
    {
        var outcomes = await GetOutcomesAsync([testOrderId], ct);
        return outcomes[testOrderId];
    }

    public async Task<IReadOnlyDictionary<int, TestOrderOutcome>> GetOutcomesAsync(
        IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default)
    {
        var outcomes = testOrderIds.Distinct().ToDictionary(id => id, _ => new TestOrderOutcome(false, null));
        if (outcomes.Count == 0)
            return outcomes;

        var ids = outcomes.Keys.ToArray();
        // Superseded orders still carry the original result used by retest propagation.
        var readings = await db.TestOrders.AsNoTracking()
            .Where(order => ids.Contains(order.Id))
            .Select(order => new
            {
                order.Id,
                CountStatus = db.CountTestReadings.Where(r => r.TestOrderId == order.Id && r.IsActive)
                    .OrderByDescending(r => r.Id).Select(r => (ResultStatus?)r.Status).FirstOrDefault(),
                // A location with a null status still takes precedence over the other result sources.
                Location = db.SampleLocations.Where(l => l.TestOrderId == order.Id)
                    .OrderByDescending(l => l.Id).Select(l => new { l.Id, l.Status }).FirstOrDefault(),
                BiochemicalDetection = db.WorkflowStepResults
                    .Where(r => r.TestOrderId == order.Id && r.BiochemicalOrganismDetected != null)
                    .OrderByDescending(r => r.Id).Select(r => r.BiochemicalOrganismDetected).FirstOrDefault(),
                HasPathogenObservations = db.PathogenObservations.Any(p => p.TestOrderId == order.Id),
                HasConformingGrowth = db.PathogenObservations.Any(p =>
                    p.TestOrderId == order.Id && p.Observation == GrowthObservation.GrowthConforming),
                ResultValue = db.Results.Where(r => r.TestOrderId == order.Id)
                    .OrderByDescending(r => r.Id).Select(r => r.InterpretedValue ?? r.RawValue).FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var reading in readings)
        {
            outcomes[reading.Id] = reading switch
            {
                { CountStatus: { } status } => FromStatus(status),
                { Location: { } location } => FromStatus(location.Status),
                { BiochemicalDetection: bool detected } => FromDetection(detected),
                { HasPathogenObservations: true } => FromDetection(reading.HasConformingGrowth),
                _ => new TestOrderOutcome(
                    ResultConformanceRules.FromResultValue(reading.ResultValue) == ResultConformance.DoesNotConform,
                    null)
            };
        }

        return outcomes;
    }

    private static TestOrderOutcome FromStatus(ResultStatus? status) =>
        new(ResultConformanceRules.FromStatus(status) != ResultConformance.Conforms, status);

    private static TestOrderOutcome FromDetection(bool detected) =>
        new(ResultConformanceRules.FromDetection(detected) != ResultConformance.Conforms, null);
}
