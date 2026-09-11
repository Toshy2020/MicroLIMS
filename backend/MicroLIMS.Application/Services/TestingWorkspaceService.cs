using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

public class TestingWorkspaceService : ITestWorkspaceService
{
    private readonly MicroLimsDbContext _db;

    // A sample past one of these is finished: it is not outstanding work no
    // matter how long ago it arrived. Without excluding them an "Overdue" count
    // grows forever, because every closed sample eventually passes 24 hours.
    //
    // Held as an array rather than repeated inline so the tiles and the register
    // cannot disagree about what "closed" means - EF translates Contains to a
    // NOT IN (...), so it composes inside any of the predicates below.
    private static readonly SampleStatus[] ClosedSampleStatuses =
    {
        SampleStatus.Approved,
        SampleStatus.Rejected,
        SampleStatus.RetestRequested,
        SampleStatus.Cancelled,
        SampleStatus.Voided
    };

    public TestingWorkspaceService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<SampleDto>> GetActiveSamplesAsync()
    {
        var samples = await _db.Samples
            .AsNoTracking()
            .Include(s => s.OriginSample)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.WaterDepartment)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .OrderByDescending(s => s.ReceivedAt)
            .ToListAsync();

        return await MapSamplesToDtosAsync(samples);
    }

    public async Task<PagedResult<SampleDto>> GetActiveSamplesAsync(TestingWorkspaceFilterDto filter, int? currentUserId = null)
    {
        int page = filter.Page <= 0 ? 1 : filter.Page;
        // Default 50, hard server-side max 200: caller cannot request unbounded data
        int pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 200);

        var query = _db.Samples.AsNoTracking();
        var now = DateTime.UtcNow;

        // 1. Workload Tile Filter (when clicking or deep-linking to a specific tile)
        if (!string.IsNullOrWhiteSpace(filter.WorkloadFilter))
        {
            var wf = filter.WorkloadFilter.Trim();
            if (string.Equals(wf, "needsPreparation", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.PreparationStatus == SamplePreparationStatus.NeedsPreparation
                                      && !ClosedSampleStatuses.Contains(s.Status));
            }
            else if (string.Equals(wf, "readyToRead", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded
                    && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                    && t.Incubations.Any(i => i.CompletedAt == null
                        && ((i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now)
                            || (i.IncubationEndUtc != null && i.IncubationEndUtc <= now)
                            || i.MinimumDurationOverriddenByUserId != null))));
            }
            else if (string.Equals(wf, "awaitingReview", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.Status == SampleStatus.UnderReview
                    || s.TestOrders.Any(t => !t.IsSuperseded
                        && (t.Status == ApprovalStatus.ResultEntered || t.CurrentStep == WorkflowStep.Ready)));
            }
            else if (string.Equals(wf, "overdue", StringComparison.OrdinalIgnoreCase))
            {
                var overdueCutoff = now.AddHours(-24);
                query = query.Where(s => !ClosedSampleStatuses.Contains(s.Status)
                                      && s.ReceivedAt < overdueCutoff);
            }
            else if (string.Equals(wf, "mine", StringComparison.OrdinalIgnoreCase))
            {
                if (currentUserId.HasValue)
                {
                    query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId == currentUserId.Value));
                }
            }
            else if (string.Equals(wf, "unassigned", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => !ClosedSampleStatuses.Contains(s.Status)
                                      && !s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId != null));
            }
        }

        // 2. Free Text Search
        // Matches sampleId, displayName (machine name, water point/department name, EM department name, or item name),
        // referenceNumber, batchNumber, controlNumber, causeOfTesting, sampledBy
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Id.ToString().Contains(q) ||
                (s.ReferenceNumber != null && s.ReferenceNumber.ToLower().Contains(q)) ||
                (s.BatchNumber != null && s.BatchNumber.ToLower().Contains(q)) ||
                (s.ControlNumber != null && s.ControlNumber.ToLower().Contains(q)) ||
                (s.SampledBy != null && s.SampledBy.ToLower().Contains(q)) ||
                (s.CauseOfTesting != null && s.CauseOfTesting.Name.ToLower().Contains(q)) ||
                (s.Category == SampleCategory.AfterCleaning && s.Machine != null && s.Machine.Name.ToLower().Contains(q)) ||
                (s.Category == SampleCategory.Water && (
                    (s.WaterSamplingPoint != null && s.WaterSamplingPoint.Code.ToLower().Contains(q)) ||
                    (s.WaterDepartment != null && s.WaterDepartment.Name.ToLower().Contains(q)))) ||
                (s.Category == SampleCategory.EnvironmentalMonitoring && s.Department != null && s.Department.Name.ToLower().Contains(q)) ||
                (s.Category != SampleCategory.AfterCleaning && s.Category != SampleCategory.Water && s.Category != SampleCategory.EnvironmentalMonitoring && s.Item != null && s.Item.Name.ToLower().Contains(q))
            );
        }

        // 3. Category Filter
        if (!string.IsNullOrWhiteSpace(filter.Category) && !filter.Category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<SampleCategory>(filter.Category, true, out var cat))
            {
                query = query.Where(s => s.Category == cat);
            }
        }

        // 4. Sample Status Filter
        // Note: The UI options define PendingReview (which expands to UnderReview and UnderApproval)
        // and RetestRequested (which represents Cancelled / Voided / RetestRequested in GMP domain).
        var sampleStatus = filter.ResolvedSampleStatus;
        if (!string.IsNullOrWhiteSpace(sampleStatus) && !sampleStatus.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(sampleStatus, "PendingReview", StringComparison.OrdinalIgnoreCase))
            {
                // Ambiguity Note: Client TSX matches "UnderReview", "UnderApproval", or "PendingReview".
                // In Domain.Enums.SampleStatus, samples awaiting review/approval are UnderReview or UnderApproval.
                query = query.Where(s => s.Status == SampleStatus.UnderReview || s.Status == SampleStatus.UnderApproval);
            }
            else if (string.Equals(sampleStatus, "RetestRequested", StringComparison.OrdinalIgnoreCase))
            {
                // The client this replaced matched RetestRequested, Cancelled and
                // Voided together. When that was written the latter two were not
                // members of SampleStatus, so those two comparisons were dead and
                // this filter was narrowed to RetestRequested alone. They exist
                // now, so the original intent is restored: this option means
                // "closed without an approval", whichever of the three it was.
                query = query.Where(s => s.Status == SampleStatus.RetestRequested
                                      || s.Status == SampleStatus.Cancelled
                                      || s.Status == SampleStatus.Voided);
            }
            else if (Enum.TryParse<SampleStatus>(sampleStatus, true, out var parsedSampleStatus))
            {
                query = query.Where(s => s.Status == parsedSampleStatus);
            }
        }

        // 5. Test Status Filter
        if (!string.IsNullOrWhiteSpace(filter.TestStatus) && !filter.TestStatus.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var ts = filter.TestStatus.Trim();
            // Ambiguity Note: Client checks t.status against "Waiting", "Pending", "InProgress", "Running", "Incubating",
            // "ResultEntered", "UnderReview", "Reviewed", "ReadyToRead". In Domain, TestOrder.Status is ApprovalStatus
            // and TestOrder.CurrentStep is WorkflowStep. We map each predicate to its underlying domain representations.
            if (string.Equals(ts, "Waiting", StringComparison.OrdinalIgnoreCase) || string.Equals(ts, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.CurrentStep == WorkflowStep.Waiting)));
            }
            else if (string.Equals(ts, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && (t.Status == ApprovalStatus.InProgress || t.CurrentStep == WorkflowStep.Running || t.CurrentStep == WorkflowStep.Incubating)));
            }
            else if (string.Equals(ts, "ReadyToRead", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded
                    && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                    && t.Incubations.Any(i => i.CompletedAt == null
                        && ((i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now)
                            || (i.IncubationEndUtc != null && i.IncubationEndUtc <= now)
                            || i.MinimumDurationOverriddenByUserId != null))));
            }
            else if (string.Equals(ts, "ResultEntered", StringComparison.OrdinalIgnoreCase) || string.Equals(ts, "UnderReview", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && (t.Status == ApprovalStatus.ResultEntered || t.Status == ApprovalStatus.Reviewed)));
            }
            else if (string.Equals(ts, "Reviewed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.Status == ApprovalStatus.Reviewed));
            }
            else if (string.Equals(ts, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.Status == ApprovalStatus.Approved));
            }
            else if (string.Equals(ts, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.Status == ApprovalStatus.Rejected));
            }
            else if (Enum.TryParse<ApprovalStatus>(ts, true, out var parsedApprovalStatus))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.Status == parsedApprovalStatus));
            }
            else if (Enum.TryParse<WorkflowStep>(ts, true, out var parsedWorkflowStep))
            {
                query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.CurrentStep == parsedWorkflowStep));
            }
        }

        // 6. Analyst ID Filter
        if (filter.AnalystId.HasValue)
        {
            query = query.Where(s => s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId == filter.AnalystId.Value));
        }

        // 7. Urgency Filter
        // Ambiguity Note: Client TSX specifically checks urgencyFilter === "overdue" using WORKLOAD_PREDICATES.overdue.
        // A sample is overdue if open (not closed: Approved, Rejected, RetestRequested) and received > 24 hours ago.
        if (string.Equals(filter.Urgency, "overdue", StringComparison.OrdinalIgnoreCase))
        {
            var overdueCutoff = now.AddHours(-24);
            query = query.Where(s => !ClosedSampleStatuses.Contains(s.Status)
                                  && s.ReceivedAt < overdueCutoff);
        }

        // 8. Date Range Filters
        // Client compares slice(0,10): fromDate <= dateStr <= toDate
        if (!string.IsNullOrWhiteSpace(filter.FromDate) && DateTime.TryParse(filter.FromDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var fromDt))
        {
            var fromUtc = DateTime.SpecifyKind(fromDt.Date, DateTimeKind.Utc);
            query = query.Where(s => s.ReceivedAt >= fromUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.ToDate) && DateTime.TryParse(filter.ToDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var toDt))
        {
            var toUtcInclusiveEnd = DateTime.SpecifyKind(toDt.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(s => s.ReceivedAt < toUtcInclusiveEnd);
        }

        var totalCount = await query.CountAsync();

        var pagedSamples = await query
            .OrderByDescending(s => s.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(s => s.OriginSample)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.WaterDepartment)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .ToListAsync();

        var dtos = await MapSamplesToDtosAsync(pagedSamples);

        return new PagedResult<SampleDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WorkspaceTileCountsDto> GetWorkloadCountsAsync(int? currentUserId = null)
    {
        var now = DateTime.UtcNow;
        var overdueCutoff = now.AddHours(-24);

        var needsPrep = await _db.Samples
            .AsNoTracking()
            .CountAsync(s => s.PreparationStatus == SamplePreparationStatus.NeedsPreparation
                          && !ClosedSampleStatuses.Contains(s.Status));

        var readyToRead = await _db.Samples
            .AsNoTracking()
            .CountAsync(s => s.TestOrders.Any(t => !t.IsSuperseded
                && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                && t.Incubations.Any(i => i.CompletedAt == null
                    && ((i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now)
                        || (i.IncubationEndUtc != null && i.IncubationEndUtc <= now)
                        || i.MinimumDurationOverriddenByUserId != null))));

        var awaitingReview = await _db.Samples
            .AsNoTracking()
            .CountAsync(s => s.Status == SampleStatus.UnderReview
                          || s.TestOrders.Any(t => !t.IsSuperseded
                              && (t.Status == ApprovalStatus.ResultEntered || t.CurrentStep == WorkflowStep.Ready)));

        var overdue = await _db.Samples
            .AsNoTracking()
            .CountAsync(s => !ClosedSampleStatuses.Contains(s.Status)
                          && s.ReceivedAt < overdueCutoff);

        var mine = currentUserId.HasValue
            ? await _db.Samples
                .AsNoTracking()
                .CountAsync(s => s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId == currentUserId.Value))
            : 0;

        var unassigned = await _db.Samples
            .AsNoTracking()
            .CountAsync(s => !ClosedSampleStatuses.Contains(s.Status)
                          && !s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId != null));

        return new WorkspaceTileCountsDto
        {
            NeedsPreparation = needsPrep,
            ReadyToRead = readyToRead,
            AwaitingReview = awaitingReview,
            Overdue = overdue,
            Mine = mine,
            Unassigned = unassigned
        };
    }

    private async Task<List<SampleDto>> MapSamplesToDtosAsync(List<Sample> samples)
    {
        var testOrderIds = samples.SelectMany(s => s.TestOrders.Select(t => t.Id)).ToList();
        var allTestCodes = samples.SelectMany(s => s.TestOrders.Select(t => t.TestCode)).Distinct().ToList();

        var testDefs = await _db.TestDefinitions
            .AsNoTracking()
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
            .Include(t => t.Steps)
                .ThenInclude(s => s.IncubationStages)
            .Where(t => allTestCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var incubations = await _db.Incubations
            .AsNoTracking()
            .Where(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value))
            .ToListAsync();

        var locationCounts = await GetLocationCountsAsync(testOrderIds);
        var analystNames = await GetAnalystNamesAsync(samples.SelectMany(s => s.TestOrders.Select(t => t.AssignedAnalystId)));

        // ToDto used to filter the whole incubation list for every sample it
        // mapped, which is quadratic: at 3,200 samples over 29,056 incubations
        // that was ~93M comparisons and 87% of this request. Bucket them once
        // instead, in a single ordered pass.
        //
        // Grouping is by sample rather than by test order on purpose. The list
        // ToDto receives has to keep the order it had when it was one filtered
        // pass over `incubations`, because TsbDetectionHelper picks the shared
        // TSB incubation with FirstOrDefault - a different order there would
        // resolve a different incubation and change reported workflow state.
        var sampleIdByTestOrderId = new Dictionary<int, int>();
        foreach (var s in samples)
            foreach (var t in s.TestOrders)
                sampleIdByTestOrderId[t.Id] = s.Id;

        var incubationsBySampleId = new Dictionary<int, List<Incubation>>();
        foreach (var i in incubations)
        {
            if (!i.TestOrderId.HasValue) continue;
            if (!sampleIdByTestOrderId.TryGetValue(i.TestOrderId.Value, out var sampleId)) continue;
            if (!incubationsBySampleId.TryGetValue(sampleId, out var bucket))
                incubationsBySampleId[sampleId] = bucket = new List<Incubation>();
            bucket.Add(i);
        }

        return samples
            .Select(s => ToDto(s, testDefs, incubations, locationCounts, analystNames, incubationsBySampleId))
            .ToList();
    }

    public async Task<SampleDto?> GetSampleAsync(int sampleId)
    {
        var sample = await _db.Samples
            .AsNoTracking()
            .Include(s => s.OriginSample)
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
            .AsNoTracking()
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
            .Include(t => t.Steps)
                .ThenInclude(s => s.IncubationStages)
            .Where(t => allTestCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var incubations = await _db.Incubations
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(l => testOrderIds.Contains(l.TestOrderId))
            .GroupBy(l => l.TestOrderId)
            .Select(g => new { TestOrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TestOrderId, x => x.Count);
    }

    private async Task<Dictionary<int, string>> GetAnalystNamesAsync(IEnumerable<int?> assignedAnalystIds)
    {
        var ids = assignedAnalystIds.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        return await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    public static SampleDto ToDto(
        Sample s,
        Dictionary<string, TestDefinition>? testDefs = null,
        List<Incubation>? allIncubations = null,
        Dictionary<int, int>? locationCountsByTestOrderId = null,
        Dictionary<int, string>? analystNamesByUserId = null,
        // Callers mapping more than one sample pass this so the incubation list
        // is not re-scanned per sample. Buckets must be in the same order a
        // filtered pass over allIncubations would have produced - see the call
        // site in GetActiveSamplesAsync.
        Dictionary<int, List<Incubation>>? incubationsBySampleId = null)
    {
        var locationCounts = locationCountsByTestOrderId
            ?? (s.Locations != null
                ? s.Locations.GroupBy(l => l.TestOrderId).ToDictionary(g => g.Key, g => g.Count())
                : new Dictionary<int, int>());
        var analystNames = analystNamesByUserId ?? new Dictionary<int, string>();
        var toIds = s.TestOrders.Select(t => t.Id).ToHashSet();
        var sampleIncubations =
            incubationsBySampleId != null
                ? (incubationsBySampleId.TryGetValue(s.Id, out var bucket) ? bucket : new List<Incubation>())
                : (allIncubations ?? new List<Incubation>())
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
                    step.StepType is StepType.BrothEnrichment ||
                    (!string.IsNullOrEmpty(step.StepName) && step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase)));
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
                step.StepType is StepType.BrothEnrichment or StepType.SelectiveBroth ||
                (!string.IsNullOrEmpty(step.StepName) && step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase))) ?? false;

            var testIncubations = sampleIncubations.Where(i => i.TestOrderId == t.Id).ToList();
            var stateResult = WorkflowStateResolver.Resolve(t, usesTsb, sharedTsbInc, testIncubations, null, DateTime.UtcNow, 24, def?.Steps);

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

        string displayName = s.Category switch
        {
            SampleCategory.AfterCleaning => s.Machine?.Name ?? string.Empty,
            SampleCategory.Water => s.WaterSamplingPoint?.Code ?? s.WaterDepartment?.Name ?? string.Empty,
            SampleCategory.EnvironmentalMonitoring => s.Department?.Name ?? string.Empty,
            _ => s.Item?.Name ?? string.Empty
        };

        return new()
        {
            SampleId = s.Id,
            ItemId = s.ItemId,
            ReferenceNumber = s.ReferenceNumber,
            Category = s.Category.ToString(),
            DisplayName = displayName,
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
            AssignedAnalystId = assignedTests.FirstOrDefault(t => t.AssignedAnalystId != null)?.AssignedAnalystId,
            AssignedAnalystName = assignedTests.FirstOrDefault(t => !string.IsNullOrEmpty(t.AssignedAnalystName))?.AssignedAnalystName,
            AssignedTests = assignedTests,
            PreviousProductName = s.Category == SampleCategory.AfterCleaning ? s.PreviousProductName : null,
            PreviousProductBatchNumber = s.Category == SampleCategory.AfterCleaning ? (s.PreviousProductBatchNumber ?? s.BatchNumber) : null,
            OriginSampleId = s.OriginSampleId,
            OriginReferenceNumber = s.OriginSample?.ReferenceNumber,
            OosGroupCode = s.OosGroupCode
        };
    }
}
