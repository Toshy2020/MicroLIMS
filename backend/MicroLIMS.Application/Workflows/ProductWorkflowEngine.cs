using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

public record ItemBasedReceiveRequest(
    int ItemId, int CauseOfTestingId, string SampleQuantity, string SampledBy,
    string BatchNumber, string ControlNumber, DateTime? MfgDate, DateTime? ExpDate,
    string? ProductionStage, int ReceivedByUserId);

public interface IProductWorkflowEngine : IStatefulWorkflowEngine
{
    // Shared by Product, Raw Material, and Packaging Material - identical
    // receiving shape (Item.Category on the resolved Item determines which
    // of the three this actually is). ProductionStage is Product-only,
    // descriptive only - it does not change which tests get assigned.
    Task<Sample> ReceiveAsync(ItemBasedReceiveRequest request);
}

// Receive Sample -> Read Item Configuration -> Generate Test Orders ->
// Assign Status -> Return Workspace Cards (the Automatic Test Order
// Generator - this is where it actually lives).
public class ProductWorkflowEngine : IProductWorkflowEngine
{
    private readonly MicroLimsDbContext _db;
    private readonly Application.Services.ReferenceNumberGenerator _refNumbers;

    public ProductWorkflowEngine(MicroLimsDbContext db, Application.Services.ReferenceNumberGenerator refNumbers)
    {
        _db = db;
        _refNumbers = refNumbers;
    }

    public async Task<Sample> ReceiveAsync(ItemBasedReceiveRequest request)
    {
        var item = await _db.Items
            .Include(i => i.AssignedTests)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId)
            ?? throw new InvalidOperationException($"Item {request.ItemId} not found or not configured.");

        if (!item.IsActive)
            throw new InvalidOperationException($"Item '{item.Name}' is frozen and cannot be used to receive new samples.");

        if (item.AssignedTests.Count == 0)
            throw new InvalidOperationException(
                $"Item '{item.Name}' has no assigned tests. Configuration must be completed " +
                "by the Section Head before samples can be received.");

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(item.Category),
            Category = item.Category,
            ItemId = item.Id,
            ProductionStage = item.Category == SampleCategory.FinishedProduct ? request.ProductionStage : null,
            CauseOfTestingId = request.CauseOfTestingId,
            SampleQuantity = request.SampleQuantity,
            SampledBy = request.SampledBy,
            BatchNumber = request.BatchNumber,
            ControlNumber = request.ControlNumber,
            MfgDate = request.MfgDate,
            ExpDate = request.ExpDate,
            ReceivedByUserId = request.ReceivedByUserId,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };

        foreach (var test in item.AssignedTests)
        {
            sample.TestOrders.Add(new TestOrder
            {
                TestCode = test.TestCode,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Waiting
            });
        }

        _db.Samples.Add(sample);
        await _db.SaveChangesAsync();

        return sample;
    }

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);

        if (order.CurrentStep == WorkflowStep.Waiting)
        {
            var sample = await _db.Samples
                .Where(s => s.Id == order.SampleId)
                .Select(s => new { s.Id, s.Category, s.PreparationStatus, s.ItemId, s.ReferenceNumber })
                .FirstAsync();

            var isPrepared = sample.PreparationStatus == SamplePreparationStatus.Ready;
            if (isPrepared && sample.ItemId != null)
            {
                isPrepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id);
            }

            if (!isPrepared)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "TestOrder",
                    EntityId = testOrderId.ToString(),
                    Action = "TestStartRefused",
                    UserId = performedByUserId,
                    SampleId = sample.Id,
                    TestOrderId = testOrderId,
                    SampleReferenceNumber = sample.ReferenceNumber,
                    Reason = $"GMP Refusal: Test preparation stage must be confirmed for sample {sample.ReferenceNumber} before testing can start."
                });

                _db.WorkflowHistories.Add(new WorkflowHistory
                {
                    TestOrderId = testOrderId,
                    FromStep = order.CurrentStep,
                    ToStep = order.CurrentStep,
                    Note = $"Transition refused: Test preparation not confirmed for sample {sample.ReferenceNumber}.",
                    PerformedByUserId = performedByUserId,
                    Timestamp = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                throw new WorkflowStepException(
                    WorkflowErrorCodes.PreparationNotConfirmed,
                    $"Test Preparation must be completed and confirmed for sample {sample.ReferenceNumber} before testing can start.");
            }
        }

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

        if (order.CurrentStep == WorkflowStep.Waiting)
        {
            var sample = await _db.Samples
                .Where(s => s.Id == order.SampleId)
                .Select(s => new { s.Category, s.PreparationStatus, s.ItemId, s.ReferenceNumber })
                .FirstOrDefaultAsync();

            if (sample != null)
            {
                var isPrepared = sample.PreparationStatus == SamplePreparationStatus.Ready;
                if (isPrepared && sample.ItemId != null)
                    isPrepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == order.SampleId);

                if (!isPrepared)
                    errors.Add($"Test preparation must be completed and confirmed for sample {sample.ReferenceNumber} before testing can start.");
            }
        }

        if (order.CurrentStep == WorkflowStep.Running && order.Results.Count == 0)
            errors.Add("No result has been entered for this test yet.");
        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
