using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record AfterCleaningReceiveRequest(
    int MachineId,
    int CauseOfTestingId,
    string SampledBy,
    string ControlNumber,
    int ReceivedByUserId,
    string PreviousProductName,
    string PreviousProductBatchNumber
);

public interface IAfterCleaningWorkflowEngine : IStatefulWorkflowEngine
{
    Task<Sample> ReceiveAsync(AfterCleaningReceiveRequest request);

    // The checklist screen: selecting which machine parts are included
    // in this batch is what generates the TestOrders (one per distinct
    // TestCode across the selected parts' configurations) and the
    // SampleLocation rows (one per selected MachinePartConfiguration).
    Task<Sample> PrepareAsync(int sampleId, List<int> machinePartConfigurationIds, int userId);
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

        if (string.IsNullOrWhiteSpace(request.PreviousProductName))
        {
            throw new InvalidOperationException("Previous Product is required for After Cleaning receiving.");
        }

        if (string.IsNullOrWhiteSpace(request.PreviousProductBatchNumber))
        {
            throw new InvalidOperationException("Previous Product Batch Number is required for After Cleaning receiving.");
        }

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.AfterCleaning),
            Category = SampleCategory.AfterCleaning,
            MachineId = machine.Id,
            ItemId = null,
            PreviousProductName = request.PreviousProductName.Trim(),
            PreviousProductBatchNumber = request.PreviousProductBatchNumber.Trim(),
            BatchNumber = request.PreviousProductBatchNumber.Trim(),
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

    public async Task<Sample> PrepareAsync(int sampleId, List<int> machinePartConfigurationIds, int userId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.Locations).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (machinePartConfigurationIds.Count == 0)
            throw new InvalidOperationException("At least one machine part must be selected.");

        var configs = await _db.MachinePartConfigurations
            .Include(c => c.MachinePart)
            .Where(c => machinePartConfigurationIds.Contains(c.Id))
            .ToListAsync();

        var missing = machinePartConfigurationIds.Except(configs.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Machine part configuration(s) not found: {string.Join(", ", missing)}.");

        var wrongMachine = configs.Where(c => c.MachinePart?.MachineId != sample.MachineId).ToList();
        if (wrongMachine.Count > 0)
            throw new InvalidOperationException(
                $"Part(s) {string.Join(", ", wrongMachine.Select(c => c.MachinePart?.Name))} do not belong to this sample's machine.");

        // One TestOrder per distinct TestCode across every selected part -
        // the whole batch shares a single incubation setup per test type.
        var testOrdersByCode = new Dictionary<string, TestOrder>();
        foreach (var config in configs)
        {
            if (!testOrdersByCode.TryGetValue(config.TestCode, out var order))
            {
                order = new TestOrder
                {
                    TestCode = config.TestCode,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Waiting,
                    AssignedAnalystId = userId
                };
                sample.TestOrders.Add(order);
                testOrdersByCode[config.TestCode] = order;
            }

            sample.Locations.Add(new SampleLocation
            {
                TestOrder = order,
                LocationType = LocationType.MachinePart,
                MachinePartConfigurationId = config.Id
            });
        }

        sample.PreparationStatus = SamplePreparationStatus.Ready;
        await _db.SaveChangesAsync();
        return sample;
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
            errors.Add("Batch results must be entered for every location before this test order can proceed.");
        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
