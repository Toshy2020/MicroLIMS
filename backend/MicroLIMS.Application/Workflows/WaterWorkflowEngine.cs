using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record WaterComparisonResult(decimal Average, string Status, string? ExceededLimit);

public record WaterReceiveRequest(
    int WaterSamplingPointId, int CauseOfTestingId, string SampleQuantity, string SampledBy,
    string ControlNumber, int ReceivedByUserId);

public interface IWaterWorkflowEngine : IStatefulWorkflowEngine
{
    Task<Sample> ReceiveAsync(WaterReceiveRequest request);

    // Calculation engine: averages the entered raw readings and compares
    // against Alert -> Action -> Specification limits, in that order of
    // severity (gap analysis #5).
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
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == request.WaterSamplingPointId)
            ?? throw new InvalidOperationException($"Sampling point {request.WaterSamplingPointId} not found.");

        if (point.AssignedTestCodes.Count == 0)
            throw new InvalidOperationException($"Sampling point {point.Code} has no assigned tests configured.");

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.Water),
            Category = SampleCategory.Water,
            WaterSamplingPointId = point.Id,
            CauseOfTestingId = request.CauseOfTestingId,
            SampleQuantity = request.SampleQuantity,
            SampledBy = request.SampledBy,
            ControlNumber = request.ControlNumber,
            ReceivedByUserId = request.ReceivedByUserId,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };

        foreach (var testCode in point.AssignedTestCodes)
            sample.TestOrders.Add(new TestOrder { TestCode = testCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        _db.Samples.Add(sample);
        await _db.SaveChangesAsync();
        return sample;
    }

    public async Task<WaterComparisonResult> CalculateAndCompareAsync(int testOrderId, List<decimal> readings)
    {
        if (readings.Count == 0)
            throw new InvalidOperationException("At least one reading is required to calculate an average.");

        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
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
        if (decimal.TryParse(spec, out var specLimit) && average > specLimit)
            return ("OutOfSpecification", "Specification");
        if (decimal.TryParse(action, out var actionLimit) && average > actionLimit)
            return ("ActionLimitExceeded", "Action");
        if (decimal.TryParse(alert, out var alertLimit) && average > alertLimit)
            return ("AlertLimitExceeded", "Alert");
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
