using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class TestingWorkspaceService : ITestWorkspaceService
{
    private readonly MicroLimsDbContext _db;

    public TestingWorkspaceService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<SampleDto>> GetActiveSamplesAsync()
    {
        var samples = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .OrderByDescending(s => s.ReceivedAt)
            .ToListAsync();

        var incubatedTestOrderIds = (await _db.Incubations.Where(i => i.TestOrderId != null).Select(i => i.TestOrderId!.Value).Distinct().ToListAsync()).ToHashSet();
        var locationCounts = await GetLocationCountsAsync(samples.SelectMany(s => s.TestOrders.Select(t => t.Id)).ToList());
        var analystNames = await GetAnalystNamesAsync(samples.SelectMany(s => s.TestOrders.Select(t => t.AssignedAnalystId)));
        return samples.Select(s => ToDto(s, incubatedTestOrderIds, locationCounts, analystNames)).ToList();
    }

    public async Task<SampleDto?> GetSampleAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId);
        if (sample is null) return null;

        var testOrderIds = sample.TestOrders.Select(t => t.Id).ToList();
        var incubationStarted = await _db.Incubations.AnyAsync(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value));
        var locationCounts = await GetLocationCountsAsync(testOrderIds);
        var analystNames = await GetAnalystNamesAsync(sample.TestOrders.Select(t => t.AssignedAnalystId));
        return ToDto(sample, incubationStarted ? testOrderIds.ToHashSet() : new HashSet<int>(), locationCounts, analystNames);
    }

    private async Task<Dictionary<int, int>> GetLocationCountsAsync(List<int> testOrderIds) =>
        await _db.SampleLocations
            .Where(l => testOrderIds.Contains(l.TestOrderId))
            .GroupBy(l => l.TestOrderId)
            .Select(g => new { TestOrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TestOrderId, x => x.Count);

    // Resolves AssignedAnalystId -> FullName server-side so the frontend
    // never needs its own (currently SystemAdministrator-only) user-list
    // call just to show a name in the Assigned To column.
    private async Task<Dictionary<int, string>> GetAnalystNamesAsync(IEnumerable<int?> assignedAnalystIds)
    {
        var ids = assignedAnalystIds.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        return await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    // incubatedTestOrderIds/locationCountsByTestOrderId default to empty
    // for call sites that map a just-received sample (WaterService/
    // ReceivingService) - correct, since a brand-new sample's TestOrders
    // can't have any Incubation or SampleLocation rows yet. EMService/
    // AfterCleaningService's PrepareAsync call sites pass the sample with
    // s.Locations already loaded instead - falls back to that when no
    // dictionary is supplied. analystNamesByUserId defaults to empty for
    // those same freshly-received call sites, since a brand-new sample's
    // TestOrders never have an AssignedAnalystId yet either.
    public static SampleDto ToDto(Sample s, HashSet<int>? incubatedTestOrderIds = null, Dictionary<int, int>? locationCountsByTestOrderId = null, Dictionary<int, string>? analystNamesByUserId = null)
    {
        var locationCounts = locationCountsByTestOrderId
            ?? s.Locations.GroupBy(l => l.TestOrderId).ToDictionary(g => g.Key, g => g.Count());
        var analystNames = analystNamesByUserId ?? new Dictionary<int, string>();

        return new()
        {
            SampleId = s.Id,
            ReferenceNumber = s.ReferenceNumber,
            Category = s.Category.ToString(),
            DisplayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty,
            DepartmentId = s.DepartmentId,
            MachineId = s.MachineId,
            ProductionStage = s.ProductionStage,
            CauseOfTesting = s.CauseOfTesting?.Name ?? string.Empty,
            BatchNumber = s.BatchNumber,
            ControlNumber = s.ControlNumber,
            Status = s.Status.ToString(),
            PreparationStatus = s.PreparationStatus.ToString(),
            ReceivedAt = s.ReceivedAt,
            SampleQuantity = s.SampleQuantity,
            SampledBy = s.SampledBy,
            MfgDate = s.MfgDate,
            ExpDate = s.ExpDate,
            WaterSamplingPointCode = s.WaterSamplingPoint?.Code,
            WaterSamplingPointLocation = s.WaterSamplingPoint?.Location,
            StorageCondition = s.StorageCondition,
            StorageTimeHours = s.StorageTimeHours,
            IncubationStarted = s.TestOrders.Any(t => incubatedTestOrderIds?.Contains(t.Id) ?? false),
            AssignedTests = s.TestOrders.Select(t => new TestOrderSummaryDto
            {
                TestOrderId = t.Id,
                TestCode = t.TestCode,
                Status = t.Status.ToString(),
                LocationCount = locationCounts.GetValueOrDefault(t.Id),
                AssignedAnalystId = t.AssignedAnalystId,
                AssignedAnalystName = t.AssignedAnalystId is { } id ? analystNames.GetValueOrDefault(id) : null
            }).ToList()
        };
    }
}
