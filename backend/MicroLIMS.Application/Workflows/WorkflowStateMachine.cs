using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

// Every workflow engine calls this to make a step transition, instead
// of updating TestOrder.CurrentStep directly - guarantees every
// transition is captured in WorkflowHistory (gap analysis #3, #9 review).
public static class WorkflowStateMachine
{
    public static async Task<WorkflowStep> TransitionAsync(
        MicroLimsDbContext db, TestOrder order, WorkflowStep toStep, int performedByUserId, string? note = null)
    {
        var fromStep = order.CurrentStep;

        db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = order.Id,
            FromStep = fromStep,
            ToStep = toStep,
            Note = note,
            PerformedByUserId = performedByUserId
        });

        order.CurrentStep = toStep;

        // Keep the coarser ApprovalStatus roughly aligned with the
        // fine-grained WorkflowStep so existing Review/Approval logic
        // (which reads ApprovalStatus) keeps working.
        order.Status = toStep switch
        {
            WorkflowStep.Waiting => ApprovalStatus.Pending,
            WorkflowStep.Running => ApprovalStatus.InProgress,
            WorkflowStep.Incubating => ApprovalStatus.InProgress,
            WorkflowStep.Ready => ApprovalStatus.ResultEntered,
            WorkflowStep.Reviewed => ApprovalStatus.Reviewed,
            WorkflowStep.Approved => ApprovalStatus.Approved,
            _ => order.Status
        };

        await db.SaveChangesAsync();
        return toStep;
    }

    public static async Task<TestOrder> LoadOrThrowAsync(MicroLimsDbContext db, int testOrderId)
    {
        return await db.TestOrders
            .Include(t => t.Incubations)
            .Include(t => t.Results)
            .FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
    }
}
