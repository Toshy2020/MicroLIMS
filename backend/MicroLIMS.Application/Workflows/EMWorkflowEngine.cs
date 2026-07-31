using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record EMReceiveRequest(int DepartmentId, int CauseOfTestingId, string SampledBy, string ControlNumber, int ReceivedByUserId);
public record EMPreparationSelection(int RoomId, List<string> TestTypes); // "PassiveAirSample" / "SurfaceAirSample"

public interface IEMWorkflowEngine : IStatefulWorkflowEngine
{
    // Receiving only captures the Department - creates a "needs
    // preparation" shell with no TestOrders yet.
    Task<Sample> ReceiveAsync(EMReceiveRequest request);

    // The checkbox screen: selecting (Room x TestType) pairs is what
    // actually generates the TestOrders, one per checked combination.
    Task<Sample> PrepareAsync(int sampleId, List<EMPreparationSelection> selections, int userId);

    // Single-stage-looking API, but internally Step 1 and Step 2 are
    // just two sequential incubation time windows - only ONE final
    // colony count is entered, after both windows have elapsed.
    Task StartStep1Async(int testOrderId, int userId);
    Task StartStep2Async(int testOrderId, int userId);
    Task<RoomMonitoring> CompleteAsync(int testOrderId, int finalCount, int userId, int actionLimit);
}

public class EMWorkflowEngine : IEMWorkflowEngine
{
    private readonly MicroLimsDbContext _db;
    private readonly Application.Services.ReferenceNumberGenerator _refNumbers;

    public EMWorkflowEngine(MicroLimsDbContext db, Application.Services.ReferenceNumberGenerator refNumbers)
    {
        _db = db;
        _refNumbers = refNumbers;
    }

    public async Task<Sample> ReceiveAsync(EMReceiveRequest request)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId)
            ?? throw new InvalidOperationException($"Department {request.DepartmentId} not found.");

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.EnvironmentalMonitoring),
            Category = SampleCategory.EnvironmentalMonitoring,
            DepartmentId = department.Id,
            CauseOfTestingId = request.CauseOfTestingId,
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

    public async Task<Sample> PrepareAsync(int sampleId, List<EMPreparationSelection> selections, int userId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (selections.Count == 0 || selections.All(s => s.TestTypes.Count == 0))
            throw new InvalidOperationException("At least one Room/test-type combination must be selected.");

        foreach (var selection in selections)
        {
            var configs = await _db.RoomTestConfigurations
                .Where(c => c.RoomId == selection.RoomId && selection.TestTypes.Contains(c.TestType))
                .ToListAsync();

            foreach (var config in configs)
            {
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = config.TestCode,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Waiting
                });
            }
        }

        sample.PreparationStatus = SamplePreparationStatus.Ready;
        await _db.SaveChangesAsync();
        return sample;
    }

    public async Task StartStep1Async(int testOrderId, int userId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        if (order.CurrentStep != WorkflowStep.Waiting)
            throw new InvalidOperationException("Step 1 can only start from the Waiting state.");

        order.Incubations.Add(new Incubation { StepNumber = 1, StepName = "Step 1 window", StartedAt = DateTime.UtcNow });
        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Incubating, userId, "Started Step 1 incubation window");
    }

    // Step 1 and Step 2 are sequential incubation TIME WINDOWS, not two
    // separate counts - starting Step 2 just closes the Step 1 window
    // and opens the Step 2 window. No count is entered until CompleteAsync.
    public async Task StartStep2Async(int testOrderId, int userId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var step1 = order.Incubations.Where(i => i.StepNumber == 1).OrderByDescending(i => i.StartedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Step 1 was never started.");

        if (step1.CompletedAt is null)
            step1.CompletedAt = DateTime.UtcNow;

        order.Incubations.Add(new Incubation { StepNumber = 2, StepName = "Step 2 window", StartedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task<RoomMonitoring> CompleteAsync(int testOrderId, int finalCount, int userId, int actionLimit)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var step2 = order.Incubations.Where(i => i.StepNumber == 2).OrderByDescending(i => i.StartedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Step 2 window was never started.");

        if (step2.CompletedAt is not null)
            throw new InvalidOperationException("This test order has already been completed - workflow order violation.");

        step2.CompletedAt = DateTime.UtcNow;
        step2.Outcome = finalCount.ToString();

        var isOutOfTrend = finalCount > actionLimit;

        var monitoring = new RoomMonitoring
        {
            TestOrderId = testOrderId,
            Step1Count = 0, // Step 1/2 are time windows only - no intermediate count is recorded
            Step2Count = finalCount,
            IsOutOfTrend = isOutOfTrend,
            SampledAt = DateTime.UtcNow
        };
        _db.RoomMonitorings.Add(monitoring);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId,
            isOutOfTrend ? $"Final count {finalCount} - OUT OF TREND" : $"Final count {finalCount} - within trend");

        return monitoring;
    }

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = await ValidateAsync(testOrderId);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var next = order.CurrentStep switch
        {
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
        if (order.CurrentStep is WorkflowStep.Waiting or WorkflowStep.Running or WorkflowStep.Incubating)
            errors.Add("Both incubation windows must complete and a final count be entered before this test order can proceed.");
        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
