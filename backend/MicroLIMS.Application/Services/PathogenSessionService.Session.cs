using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public partial class PathogenSessionService
{
    public async Task<PathogenTestingSessionDto?> GetSessionAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.WaterDepartment)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
                .ThenInclude(l => l.RoomTestConfiguration!)
                .ThenInclude(c => c.Room)
            .Include(s => s.Locations)
                .ThenInclude(l => l.MachinePartConfiguration!)
                .ThenInclude(c => c.MachinePart)
            .Include(s => s.Locations)
                .ThenInclude(l => l.WaterSamplingPoint!)
            .FirstOrDefaultAsync(s => s.Id == sampleId);

        if (sample == null) return null;

        var sessionId = $"SESSION-{sample.ReferenceNumber}";
        var data = await PathogenSessionData.LoadAsync(_db, sample);
        var testDefs = data.TestDefs;
        var UserName = data.UserName;
        var incubations = data.Incubations;
        var stepResults = data.StepResults;
        var primaryObservations = data.PrimaryObservations;
        var countTestReadings = data.CountTestReadings;

        // Group Locations
        var groupedLocations = SessionLocationGrouper.Group(sample);

        var primaryObsMap = primaryObservations
            .ToDictionary(o => (o.SampleLocationId, o.TestOrderId));

        var mediaLookup = incubations
            .Where(i => i.MediaId.HasValue && i.Media != null)
            .GroupBy(i => i.MediaId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (MaterialId: g.First().Media!.MaterialId, MediaProductId: g.First().Media!.Material?.MediaProductId));

        var utcNow = _time.GetUtcNow().UtcDateTime;

        var tsb = SharedTsbStateBuilder.Build(sample, data, mediaLookup, groupedLocations.Count, utcNow);
        var sharedTsbDto = tsb.Dto;
        var tsbApplicableCodes = tsb.ApplicableCodes;
        var ownTsbByOrder = tsb.OwnTsbByOrder;
        var tsbIncubating = tsb.IsIncubating;
        var tsbCompleted = tsb.IsCompleted;

        // Build Assigned Test DTOs and evaluate test-specific workflow states
        var assignedTestDtos = new List<SessionAssignedTestDto>();

        foreach (var to in sample.TestOrders)
        {
            testDefs.TryGetValue(to.TestCode, out var def);
            var steps = def?.Steps.OrderBy(s => s.StepOrder).ToList() ?? new List<TestWorkflowStep>();

            var requiresTsb = tsbApplicableCodes.Contains(to.TestCode);
            var toIncubations = incubations.Where(i => i.TestOrderId == to.Id).ToList();
            var toStepResults = stepResults.Where(w => w.TestOrderId == to.Id).ToList();

            var confirmatoryMediaCount = RequiredConfirmatoryMediaCount(def);

            var ownTsbComplete = ownTsbByOrder.TryGetValue(to.Id, out var ownTsb) && ownTsb.IsComplete;

            var stepDtos = AssignedTestStepBuilder.Build(steps, toIncubations, toStepResults, requiresTsb, ownTsbComplete, mediaLookup);

            // Determine Test Session State & Result Entry Allowance
            var stateResult = WorkflowStateResolver.Resolve(to, requiresTsb, toIncubations, stepDtos, _time.GetUtcNow().UtcDateTime, steps, sample.Status, mediaLookup);
            string testSessionState = stateResult.WorkflowState;
            string testSessionStateDisplay = stateResult.WorkflowStateDisplay;
            bool isResultEntryAllowed = stateResult.IsResultEntryAllowed;
            bool isWorkflowLocked = stateResult.IsWorkflowLocked;
            string? lockReason = stateResult.LockReason;
            string workflowStatus = stateResult.WorkflowStatus;

            var countReading = countTestReadings.FirstOrDefault(c => c.TestOrderId == to.Id);
            var countResultDto = await BuildCountResultAsync(sample, to, countReading);

            assignedTestDtos.Add(new SessionAssignedTestDto(
                to.Id,
                to.TestCode,
                def?.DisplayName ?? to.TestCode,
                def?.WorkflowType.ToString() ?? "Observation",
                to.Status.ToString(),
                to.CurrentStep.ToString(),
                to.AssignedAnalystId.HasValue ? UserName(to.AssignedAnalystId) : null,
                requiresTsb,
                testSessionState,
                testSessionStateDisplay,
                isResultEntryAllowed,
                isWorkflowLocked,
                lockReason,
                stepDtos,
                confirmatoryMediaCount,
                workflowStatus,
                countResultDto));
        }

        var built = ResultMatrixBuilder.Build(groupedLocations, assignedTestDtos, sample, primaryObsMap, UserName);
        var matrix = built.Matrix;
        var missingResults = built.Missing;
        int completedCount = built.Completed;
        int availableCount = built.Available;
        int lockedCount = built.Locked;

        int totalRequired = groupedLocations.Count * assignedTestDtos.Count;
        int pendingCount = availableCount; // Only available and empty cells are truly pending entry

        // Overall Session Status Computation
        var (sessionStatus, sessionStatusDisplay) = SessionStatusResolver.Resolve(
            completedCount, totalRequired, sample.Status, tsbIncubating, tsbCompleted, tsbApplicableCodes.Count);

        var programName = sample.Category switch
        {
            SampleCategory.AfterCleaning => "After Cleaning Monitoring",
            SampleCategory.EnvironmentalMonitoring => "Environmental Monitoring",
            SampleCategory.Water => "Water Monitoring Plan",
            SampleCategory.FinishedProduct => "Finished Product Testing",
            _ => $"{sample.Category} Testing"
        };

        var deptName = sample.Department?.Name
            ?? sample.Machine?.Name
            ?? sample.WaterDepartment?.Name
            ?? sample.WaterSamplingPoint?.Location
            ?? sample.Item?.Name
            ?? "Laboratory Workspace";

        return new PathogenTestingSessionDto(
            sessionId,
            sample.Id,
            sample.ReferenceNumber,
            sample.Category.ToString(),
            programName,
            deptName,
            sample.ControlNumber,
            sample.BatchNumber,
            sample.ReceivedAt,
            sessionStatus,
            sessionStatusDisplay,
            groupedLocations.Count,
            assignedTestDtos.Count,
            totalRequired,
            completedCount,
            availableCount,
            lockedCount,
            pendingCount,
            groupedLocations,
            assignedTestDtos,
            sharedTsbDto,
            matrix,
            missingResults);
    }

    // The count test's recorded reading with its reporting unit: the
    // specification's (or sampling configuration's) unit first, the category
    // default otherwise.
    private async Task<CountResultDto?> BuildCountResultAsync(Sample sample, TestOrder to, CountTestReading? countReading)
    {
        CountResultDto? countResultDto = null;
        if (countReading != null)
        {
            // Same configured-unit-first pattern as TestWorkflowEngine/
            // ResultProjectionService - Specification.Unit is required
            // for Item-based count tests as of the 2026-09 Preparation
            // Configuration simplification, which removed the old
            // SamplePreparation.Unit fallback used here.
            string? configuredUnit = null;
            if (sample.ItemId is not null)
            {
                var spec = await SpecificationLookup.PrimaryAsync(_db, sample.Id, sample.ItemId.Value, to.TestCode);
                configuredUnit = spec?.Unit;
            }
            else if (sample.WaterSamplingPointId is not null)
            {
                var config = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.TestCode == to.TestCode && c.WaterSamplingPointId == sample.WaterSamplingPointId);
                configuredUnit = config?.Unit;
            }

            var unit = !string.IsNullOrWhiteSpace(configuredUnit)
                ? (configuredUnit.StartsWith("CFU/", StringComparison.OrdinalIgnoreCase) ? configuredUnit : $"CFU/{configuredUnit}")
                : TestWorkflowEngine.GetCfuUnit(sample.Category, null);
            countResultDto = new CountResultDto(
                countReading.PlateReadings,
                countReading.DilutionFactor,
                countReading.Average,
                countReading.CalculatedResult,
                countReading.ReportedResult,
                countReading.Status,
                countReading.HasNonNumericReading,
                countReading.NonNumericValue,
                countReading.RequiresReview,
                unit);
        }

        return countResultDto;
    }

}
