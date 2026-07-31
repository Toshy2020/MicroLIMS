using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record AfterCleaningReceiveRequest(int MachineId, int CauseOfTestingId, string SampledBy, string ControlNumber, int ReceivedByUserId);
public record AfterCleaningPreparationSelection(int MachinePartId, List<string> TestTypes); // "Swab" / "Rinse" / a pathogen TestCode

public interface IAfterCleaningWorkflowEngine : IStatefulWorkflowEngine
{
    Task<Sample> ReceiveAsync(AfterCleaningReceiveRequest request);

    // Checking (Part x TestType) is what generates the TestOrders - one
    // per checked combination, no "collective sample" grouping at all.
    Task<Sample> PrepareAsync(int sampleId, List<AfterCleaningPreparationSelection> selections, int userId);

    // Swab and Rinse TAMC both use the same two-window incubation as EM.
    Task StartStep1Async(int testOrderId, int userId);
    Task StartStep2Async(int testOrderId, int userId);
    Task<Result> CompleteAsync(int testOrderId, int finalCount, int userId);
}

public class AfterCleaningWorkflowEngine : IAfterCleaningWorkflowEngine
{
    private readonly MicroLimsDbContext _db;
    private readonly Application.Services.ReferenceNumberGenerator _refNumbers;

    public AfterCleaningWorkflowEngine(MicroLimsDbContext db, Application.Services.ReferenceNumberGenerator refNumbers)
    {
        _db = db;
        _refNumbers = refNumbers;
    }

    public async Task<Sample> ReceiveAsync(AfterCleaningReceiveRequest request)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId)
            ?? throw new InvalidOperationException($"Machine {request.MachineId} not found.");

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.AfterCleaning),
            Category = SampleCategory.AfterCleaning,
            MachineId = machine.Id,
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

    public async Task<Sample> PrepareAsync(int sampleId, List<AfterCleaningPreparationSelection> selections, int userId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (selections.Count == 0 || selections.All(s => s.TestTypes.Count == 0))
            throw new InvalidOperationException("At least one Part/test-type combination must be selected.");

        foreach (var selection in selections)
        {
            var configs = await _db.MachinePartConfigurations
                .Where(c => c.MachinePartId == selection.MachinePartId && selection.TestTypes.Contains(c.TestType))
                .ToListAsync();

            // Checking a part's Swab/Rinse also pulls in any pathogen
            // tests configured for that same part - TAMC + whatever
            // pathogens are configured, per part.
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

    public async Task<Result> CompleteAsync(int testOrderId, int finalCount, int userId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var step2 = order.Incubations.Where(i => i.StepNumber == 2).OrderByDescending(i => i.StartedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Step 2 window was never started.");

        if (step2.CompletedAt is not null)
            throw new InvalidOperationException("This test order has already been completed - workflow order violation.");

        step2.CompletedAt = DateTime.UtcNow;
        step2.Outcome = finalCount.ToString();

        var result = new Result { TestOrderId = testOrderId, RawValue = finalCount.ToString(), EnteredByUserId = userId, Type = ResultType.Numeric };
        _db.Results.Add(result);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Final count {finalCount}");
        return result;
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
            errors.Add("Incubation must complete and a final count be entered before this test order can proceed.");
        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
