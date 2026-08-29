using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record WaterComparisonResult(decimal Average, string Status, string? ExceededLimit);

public record WaterReceiveRequest(
    int WaterDepartmentId, int CauseOfTestingId, string SampleQuantity, string SampledBy,
    string ControlNumber, int ReceivedByUserId);

public interface IWaterWorkflowEngine : IStatefulWorkflowEngine
{
    Task<Sample> ReceiveAsync(WaterReceiveRequest request);

    // The checklist screen: selecting which sampling points are included
    // in this batch generates the TestOrders (one per distinct TestCode
    // across every selected point) and the SampleLocation rows (one per
    // selected point x assigned test code).
    Task<Sample> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId);

    // Calculation engine: averages the entered raw readings and compares
    // against Alert -> Action -> Specification limits, in that order of
    // severity (gap analysis #5). Legacy per-point samples only - see the
    // guard at the top of the method.
    Task<WaterComparisonResult> CalculateAndCompareAsync(int testOrderId, List<decimal> readings);

    Task<List<WaterComparisonResult>> GetDailyAggregateAsync(DateTime date);
}

public class WaterWorkflowEngine : IWaterWorkflowEngine
{
    private readonly MicroLimsDbContext _db;
    private readonly Application.Services.ReferenceNumberGenerator _refNumbers;

    public WaterWorkflowEngine(MicroLimsDbContext db, Application.Services.ReferenceNumberGenerator refNumbers)
    {
        _db = db;
        _refNumbers = refNumbers;
    }

    public async Task<Sample> ReceiveAsync(WaterReceiveRequest request)
    {
        var department = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == request.WaterDepartmentId)
            ?? throw new InvalidOperationException($"Water department {request.WaterDepartmentId} not found.");

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.Water),
            Category = SampleCategory.Water,
            WaterDepartmentId = department.Id,
            CauseOfTestingId = request.CauseOfTestingId,
            SampleQuantity = request.SampleQuantity,
            SampledBy = request.SampledBy,
            ControlNumber = request.ControlNumber,
            ReceivedByUserId = request.ReceivedByUserId,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };

        _db.Samples.Add(sample);
        await _db.SaveChangesAsync();
        return sample;
    }

    public async Task<Sample> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.Locations).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (waterSamplingPointIds.Count == 0)
            throw new InvalidOperationException("At least one sampling point must be selected.");

        var points = await _db.WaterSamplingPoints
            .Where(p => waterSamplingPointIds.Contains(p.Id))
            .ToListAsync();

        var missing = waterSamplingPointIds.Except(points.Select(p => p.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Sampling point(s) not found: {string.Join(", ", missing)}.");

        var wrongDepartment = points.Where(p => p.WaterDepartmentId != sample.WaterDepartmentId).ToList();
        if (wrongDepartment.Count > 0)
            throw new InvalidOperationException(
                $"Sampling point(s) {string.Join(", ", wrongDepartment.Select(p => p.Code))} do not belong to this sample's department.");

        var allCodes = points.SelectMany(p => p.AssignedTestCodes).Distinct().ToList();
        var countTestCodeSet = (await _db.TestDefinitions
            .Where(t => allCodes.Contains(t.Code) && t.WorkflowType == WorkflowType.CountTest)
            .Select(t => t.Code)
            .ToListAsync())
            .ToHashSet();

        var configs = await _db.SamplingConfigurations
            .Where(c => waterSamplingPointIds.Contains(c.WaterSamplingPointId))
            .ToListAsync();

        // One TestOrder per distinct TestCode across every selected point -
        // the whole batch shares a single workflow per test type, same as
        // EMWorkflowEngine.PrepareAsync.
        var testOrdersByCode = new Dictionary<string, TestOrder>();
        foreach (var point in points)
        {
            foreach (var testCode in point.AssignedTestCodes)
            {
                if (!testOrdersByCode.TryGetValue(testCode, out var order))
                {
                    order = new TestOrder
                    {
                        TestCode = testCode,
                        Status = ApprovalStatus.Pending,
                        CurrentStep = WorkflowStep.Waiting,
                        AssignedAnalystId = userId
                    };
                    sample.TestOrders.Add(order);
                    testOrdersByCode[testCode] = order;
                }

                var location = new SampleLocation
                {
                    TestOrder = order,
                    LocationType = LocationType.WaterSamplingPoint,
                    WaterSamplingPointId = point.Id
                };

                if (countTestCodeSet.Contains(testCode))
                {
                    var config = configs.FirstOrDefault(c => c.WaterSamplingPointId == point.Id && c.TestCode == testCode);
                    if (config != null)
                        location.SamplingConfigurationId = config.Id;
                }

                sample.Locations.Add(location);
            }
        }

        sample.PreparationStatus = SamplePreparationStatus.Ready;
        await _db.SaveChangesAsync();
        return sample;
    }

    public async Task<WaterComparisonResult> CalculateAndCompareAsync(int testOrderId, List<decimal> readings)
    {
        if (readings.Count == 0)
            throw new InvalidOperationException("At least one reading is required to calculate an average.");

        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);

        // A TestOrder that went through the batch PrepareAsync always has
        // SampleLocation rows (one per selected sampling point). Per-
        // location result entry for those ships separately - this legacy
        // single-average path must never silently misattribute a batch
        // order's result to "the" sampling point.
        var isBatchPrepared = await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId);
        if (isBatchPrepared)
            throw new InvalidOperationException(
                "This water test was prepared across multiple sampling points; per-location result entry is not available yet.");

        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == order.SampleId)
            ?? throw new InvalidOperationException("Sample not found for this test order.");

        var config = sample.WaterSamplingPointId is null
            ? null
            : await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.TestCode == order.TestCode && c.WaterSamplingPointId == sample.WaterSamplingPointId);

        var average = readings.Average();

        var (status, exceeded) = Compare(average, config?.AlertLimit, config?.ActionLimit, config?.SpecLimit);

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = string.Join(",", readings),
            InterpretedValue = $"{average:0.##} ({status})",
            Type = ResultType.Numeric
        });

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, order.AssignedAnalystId ?? 0,
            $"Average {average:0.##}, {status}");

        return new WaterComparisonResult(average, status, exceeded);
    }

    // Alert -> Action -> Specification, in ascending order of severity -
    // the first limit exceeded (starting from Spec, the most severe) wins.
    private static (string status, string? exceeded) Compare(decimal average, string? alert, string? action, string? spec)
    {
        var hasSpec = decimal.TryParse(spec, out var specLimit);
        if (hasSpec && average > specLimit)
            return ("OutOfSpecification", "Specification");
        var hasAction = decimal.TryParse(action, out var actionLimit);
        if (hasAction && average > actionLimit)
            return ("ActionLimitExceeded", "Action");
        var hasAlert = decimal.TryParse(alert, out var alertLimit);
        if (hasAlert && average > alertLimit)
            return ("AlertLimitExceeded", "Alert");
        if (!hasSpec && !hasAction && !hasAlert)
            return ("LimitsNotConfigured", null);
        return ("WithinLimits", null);
    }

    public async Task<List<WaterComparisonResult>> GetDailyAggregateAsync(DateTime date)
    {
        var results = await _db.Results
            .Where(r => r.EnteredAt.Date == date.Date)
            .Where(r => _db.TestOrders.Any(t => t.Id == r.TestOrderId &&
                        _db.Samples.Any(s => s.Id == t.SampleId && s.Category == SampleCategory.Water)))
            .ToListAsync();

        return results.Select(ParseAggregateResult).ToList();
    }

    private static WaterComparisonResult ParseAggregateResult(Result r)
    {
        var parts = r.InterpretedValue?.Split('(', ')') ?? Array.Empty<string>();
        var avg = parts.Length > 0 && decimal.TryParse(parts[0].Trim(), out var a) ? a : 0m;
        var status = parts.Length > 1 ? parts[1].Trim() : "Unknown";
        return new WaterComparisonResult(avg, status, null);
    }

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = await ValidateAsync(testOrderId);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var next = order.CurrentStep switch
        {
            WorkflowStep.Waiting => WorkflowStep.Running,
            WorkflowStep.Running => WorkflowStep.Ready,
            WorkflowStep.Ready => WorkflowStep.Reviewed,
            WorkflowStep.Reviewed => WorkflowStep.Approved,
            _ => order.CurrentStep
        };
        return await WorkflowStateMachine.TransitionAsync(_db, order, next, performedByUserId, note);
    }

    public async Task<List<string>> ValidateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = new List<string>();
        if (order.CurrentStep == WorkflowStep.Running && order.Results.Count == 0)
            errors.Add("No readings have been entered for this test yet.");
        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
