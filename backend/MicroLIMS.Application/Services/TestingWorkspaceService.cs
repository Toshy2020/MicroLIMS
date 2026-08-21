using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
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
            .Include(s => s.WaterDepartment)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .OrderByDescending(s => s.ReceivedAt)
            .ToListAsync();

        var testOrderIds = samples.SelectMany(s => s.TestOrders.Select(t => t.Id)).ToList();
        var allTestCodes = samples.SelectMany(s => s.TestOrders.Select(t => t.TestCode)).Distinct().ToList();

        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.MediaType)
            .Where(t => allTestCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value))
            .ToListAsync();

        var locationCounts = await GetLocationCountsAsync(testOrderIds);
        var analystNames = await GetAnalystNamesAsync(samples.SelectMany(s => s.TestOrders.Select(t => t.AssignedAnalystId)));

        return samples.Select(s => ToDto(s, testDefs, incubations, locationCounts, analystNames)).ToList();
    }

    public async Task<SampleDto?> GetSampleAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.WaterDepartment)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId);
        if (sample is null) return null;

        var testOrderIds = sample.TestOrders.Select(t => t.Id).ToList();
        var allTestCodes = sample.TestOrders.Select(t => t.TestCode).Distinct().ToList();

        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.MediaType)
            .Where(t => allTestCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value))
            .ToListAsync();

        var locationCounts = await GetLocationCountsAsync(testOrderIds);
        var analystNames = await GetAnalystNamesAsync(sample.TestOrders.Select(t => t.AssignedAnalystId));

        return ToDto(sample, testDefs, incubations, locationCounts, analystNames);
    }

    private async Task<Dictionary<int, int>> GetLocationCountsAsync(List<int> testOrderIds)
    {
        if (testOrderIds == null || testOrderIds.Count == 0)
            return new Dictionary<int, int>();

        return await _db.SampleLocations
            .Where(l => testOrderIds.Contains(l.TestOrderId))
            .GroupBy(l => l.TestOrderId)
            .Select(g => new { TestOrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TestOrderId, x => x.Count);
    }

    private async Task<Dictionary<int, string>> GetAnalystNamesAsync(IEnumerable<int?> assignedAnalystIds)
    {
        var ids = assignedAnalystIds.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        return await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    public static SampleDto ToDto(
        Sample s,
        Dictionary<string, TestDefinition>? testDefs = null,
        List<Incubation>? allIncubations = null,
        Dictionary<int, int>? locationCountsByTestOrderId = null,
        Dictionary<int, string>? analystNamesByUserId = null)
    {
        var locationCounts = locationCountsByTestOrderId
            ?? (s.Locations != null
                ? s.Locations.GroupBy(l => l.TestOrderId).ToDictionary(g => g.Key, g => g.Count())
                : new Dictionary<int, int>());
        var analystNames = analystNamesByUserId ?? new Dictionary<int, string>();
        var toIds = s.TestOrders.Select(t => t.Id).ToHashSet();
        var sampleIncubations = (allIncubations ?? new List<Incubation>())
            .Where(i => i.TestOrderId.HasValue && toIds.Contains(i.TestOrderId.Value))
            .ToList();

        // Check if shared TSB is incubating/complete for this sample
        var sharedTsbInc = TsbDetectionHelper.FindSharedTsbIncubation(sampleIncubations);

        int tsbHoursMin = 24;
        if (testDefs != null)
        {
            foreach (var def in testDefs.Values)
            {
                var tsbStep = def.Steps.FirstOrDefault(step =>
                    step.StepType == StepType.BrothEnrichment ||
                    (!string.IsNullOrEmpty(step.StepName) && step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase)) ||
                    (step.MediaType != null && (step.MediaType.Class == MediaClass.GeneralBroth || step.MediaType.Class == MediaClass.SelectiveBroth)));
                if (tsbStep != null && tsbStep.IncubationMinHours > 0)
                {
                    tsbHoursMin = tsbStep.IncubationMinHours;
                    break;
                }
            }
        }

        bool tsbStarted = sharedTsbInc != null;
        DateTime? tsbStart = sharedTsbInc?.IncubationStartUtc ?? sharedTsbInc?.StartedAt;
        DateTime? tsbMinReadyAt = TsbDetectionHelper.GetTsbMinReadyAt(sharedTsbInc, tsbHoursMin);

        bool tsbIncubating = TsbDetectionHelper.IsTsbIncubating(sharedTsbInc, tsbHoursMin, DateTime.UtcNow);
        bool tsbCompleted = TsbDetectionHelper.IsTsbComplete(sharedTsbInc, tsbHoursMin, DateTime.UtcNow);

        var assignedTests = s.TestOrders.Select(t =>
        {
            TestDefinition? def = null;
            testDefs?.TryGetValue(t.TestCode, out def);

            bool usesTsb = def?.Steps.Any(step =>
                step.StepType == StepType.BrothEnrichment ||
                (!string.IsNullOrEmpty(step.StepName) && step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase)) ||
                (step.MediaType != null && (step.MediaType.Class == MediaClass.GeneralBroth || step.MediaType.Class == MediaClass.SelectiveBroth))) ?? false;

            var testIncubations = sampleIncubations.Where(i => i.TestOrderId == t.Id).ToList();
            var stateResult = WorkflowStateResolver.Resolve(t, usesTsb, sharedTsbInc, testIncubations, null, DateTime.UtcNow);

            return new TestOrderSummaryDto
            {
                TestOrderId = t.Id,
                TestCode = t.TestCode,
                Status = t.Status.ToString(),
                CurrentStep = t.CurrentStep.ToString(),
                WorkflowState = stateResult.WorkflowState,
                WorkflowStateDisplay = stateResult.WorkflowStateDisplay,
                WorkflowStatus = stateResult.WorkflowStatus,
                UsesSharedTsb = usesTsb,
                IsWorkflowLocked = stateResult.IsWorkflowLocked,
                IsResultEntryAllowed = stateResult.IsResultEntryAllowed,
                ResultLockReason = stateResult.LockReason,
                LocationCount = locationCounts.GetValueOrDefault(t.Id),
                AssignedAnalystId = t.AssignedAnalystId,
                AssignedAnalystName = t.AssignedAnalystId is { } id ? analystNames.GetValueOrDefault(id) : null
            };
        }).ToList();

        return new()
        {
            SampleId = s.Id,
            ItemId = s.ItemId,
            ReferenceNumber = s.ReferenceNumber,
            Category = s.Category.ToString(),
            DisplayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.WaterDepartment?.Name ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty,
            DepartmentId = s.DepartmentId,
            MachineId = s.MachineId,
            WaterDepartmentId = s.WaterDepartmentId,
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
            IncubationStarted = sampleIncubations.Count > 0,
            AssignedTests = assignedTests
        };
    }
}
