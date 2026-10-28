using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services;

// Everything GetSessionAsync reads from the database besides the Sample
// graph itself, loaded once so the builders that turn it into DTOs can stay
// pure.
public sealed record PathogenSessionData(
    Dictionary<string, TestDefinition> TestDefs,
    Func<int?, string> UserName,
    List<Incubation> Incubations,
    List<WorkflowStepResult> StepResults,
    List<LocationPathogenObservation> PrimaryObservations,
    List<CountTestReading> CountTestReadings)
{
    public static async Task<PathogenSessionData> LoadAsync(IMicroLimsDbContext db, Sample sample)
    {
        var testCodes = sample.TestOrders.Select(t => t.TestCode).Distinct().ToList();

        // Load Test Definitions from Test Master
        var testDefs = await db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.MediaProduct)
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.IncubationCondition)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        // Load Incubations and WorkflowStepResults for all test orders
        var toIds = sample.TestOrders.Select(t => t.Id).ToList();
        var incubations = await db.Incubations
            .Include(i => i.Media)
                .ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .Where(i => i.TestOrderId.HasValue && toIds.Contains(i.TestOrderId.Value))
            .ToListAsync();

        var stepResults = await db.WorkflowStepResults
            .Where(w => toIds.Contains(w.TestOrderId))
            .ToListAsync();

        var primaryObservations = await db.LocationPathogenObservations
            .Include(o => o.ConfirmatoryPlateObservations)
                .ThenInclude(p => p.Material)
            .Where(o => toIds.Contains(o.TestOrderId))
            .ToListAsync();

        var countTestReadings = await db.CountTestReadings
            .Where(c => toIds.Contains(c.TestOrderId))
            .ToListAsync();

        // Load only the users the session actually attributes something to
        // (incubation starter, assigned analyst, confirmatory-plate recorder,
        // result enterer) - not the whole Users table.
        var userIds = incubations.Select(i => i.StartedByUserId)
            .Concat(sample.TestOrders.Select(t => t.AssignedAnalystId))
            .Concat(sample.Locations.Select(l => l.EnteredByUserId))
            .Concat(primaryObservations.SelectMany(o => o.ConfirmatoryPlateObservations).Select(p => (int?)p.RecordedByUserId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);
        string UserName(int? uid) => (uid.HasValue && users.TryGetValue(uid.Value, out var name)) ? name : "Unknown";

        return new PathogenSessionData(testDefs, UserName, incubations, stepResults, primaryObservations, countTestReadings);
    }
}
