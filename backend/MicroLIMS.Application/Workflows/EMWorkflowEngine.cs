using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record EMReceiveRequest(int DepartmentId, int CauseOfTestingId, string SampledBy, string ControlNumber, int ReceivedByUserId);

public interface IEMWorkflowEngine : IStatefulWorkflowEngine
{
    // Receiving only captures the Department - creates a "needs
    // preparation" shell with no TestOrders yet.
    Task<Sample> ReceiveAsync(EMReceiveRequest request);

    // The checklist screen: selecting which rooms are included in this
    // monitoring session is what generates the TestOrders (one per
    // distinct TestCode across the selected rooms' configurations) and
    // the SampleLocation rows (one per selected RoomTestConfiguration).
    Task<Sample> PrepareAsync(int sampleId, List<int> roomTestConfigurationIds, int userId);
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

    public async Task<Sample> PrepareAsync(int sampleId, List<int> roomTestConfigurationIds, int userId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.Locations).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (roomTestConfigurationIds.Count == 0)
            throw new InvalidOperationException("At least one room must be selected.");

        var configs = await _db.RoomTestConfigurations
            .Include(c => c.Room)
            .Where(c => roomTestConfigurationIds.Contains(c.Id))
            .ToListAsync();

        var missing = roomTestConfigurationIds.Except(configs.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Room test configuration(s) not found: {string.Join(", ", missing)}.");

        var wrongDepartment = configs.Where(c => c.Room?.DepartmentId != sample.DepartmentId).ToList();
        if (wrongDepartment.Count > 0)
            throw new InvalidOperationException(
                $"Room(s) {string.Join(", ", wrongDepartment.Select(c => c.Room?.Name))} do not belong to this sample's department.");

        // One TestOrder per distinct TestCode across every selected room -
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
                LocationType = LocationType.Room,
                RoomTestConfigurationId = config.Id
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
