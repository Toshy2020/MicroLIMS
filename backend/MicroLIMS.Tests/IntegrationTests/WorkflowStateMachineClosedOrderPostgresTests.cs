using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// WorkflowStateMachine is the one place every bench, review and navigation
// step goes through. A closed test order (Approved, Rejected, Voided,
// Cancelled, or superseded by a retest) must never move again: before this
// guard, a late "Ready" transition silently set an Approved test back to
// ResultEntered.
//
// Postgres-backed: the guard protects a regulated state change, so the
// persisted row, its WorkflowHistory and its audit trail are all checked
// from a fresh context (CLAUDE.md Rule 4.4).
[Collection("PostgresDatabaseCollection")]
public class WorkflowStateMachineClosedOrderPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public WorkflowStateMachineClosedOrderPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresTheory]
    [InlineData(ApprovalStatus.Approved, WorkflowStep.Approved)]
    [InlineData(ApprovalStatus.Rejected, WorkflowStep.Reviewed)]
    [InlineData(ApprovalStatus.Voided, WorkflowStep.Ready)]
    [InlineData(ApprovalStatus.Cancelled, WorkflowStep.Incubating)]
    public async Task ClosedOrder_RefusesEveryTransition_AndNothingIsRecorded(ApprovalStatus status, WorkflowStep step)
    {
        var orderId = await SeedOrderAsync(status, step, superseded: false);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var order = await WorkflowStateMachine.LoadOrThrowAsync(actDb, orderId);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                WorkflowStateMachine.TransitionAsync(actDb, order, WorkflowStep.Ready, _fixture.SeededUserId, "late entry"));
            Assert.Contains(status.ToString(), ex.Message);
        }

        await AssertUnchangedAsync(orderId, status, step);
    }

    [PostgresFact]
    public async Task SupersededOrder_RefusesTransition_AndNothingIsRecorded()
    {
        var orderId = await SeedOrderAsync(ApprovalStatus.InProgress, WorkflowStep.Incubating, superseded: true);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var order = await WorkflowStateMachine.LoadOrThrowAsync(actDb, orderId);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                WorkflowStateMachine.TransitionAsync(actDb, order, WorkflowStep.Ready, _fixture.SeededUserId));
        }

        await AssertUnchangedAsync(orderId, ApprovalStatus.InProgress, WorkflowStep.Incubating);
    }

    [PostgresFact]
    public async Task UndefinedStep_IsRefused_AndNothingIsRecorded()
    {
        var orderId = await SeedOrderAsync(ApprovalStatus.InProgress, WorkflowStep.Incubating, superseded: false);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var order = await WorkflowStateMachine.LoadOrThrowAsync(actDb, orderId);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                WorkflowStateMachine.TransitionAsync(actDb, order, (WorkflowStep)99, _fixture.SeededUserId));
        }

        await AssertUnchangedAsync(orderId, ApprovalStatus.InProgress, WorkflowStep.Incubating);
    }

    [PostgresFact]
    public async Task OpenOrder_Transitions_WithHistoryAndAudit()
    {
        var orderId = await SeedOrderAsync(ApprovalStatus.InProgress, WorkflowStep.Incubating, superseded: false);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var order = await WorkflowStateMachine.LoadOrThrowAsync(actDb, orderId);
            await WorkflowStateMachine.TransitionAsync(actDb, order, WorkflowStep.Ready, _fixture.SeededUserId, "plates read");
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
        Assert.Equal(WorkflowStep.Ready, persisted.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, persisted.Status);

        var history = await assertDb.WorkflowHistories.AsNoTracking().SingleAsync(h => h.TestOrderId == orderId);
        Assert.Equal(WorkflowStep.Incubating, history.FromStep);
        Assert.Equal(WorkflowStep.Ready, history.ToStep);
        Assert.Equal(_fixture.SeededUserId, history.PerformedByUserId);
        Assert.Equal("plates read", history.Note);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == "TestOrder" && a.EntityId == orderId.ToString() && a.Action == "Update");
        Assert.Equal(_fixture.SeededUserId, audit.UserId);
        Assert.Contains("CurrentStep", audit.PreviousValue);
        Assert.Contains("CurrentStep", audit.NewValue);
    }

    private async Task AssertUnchangedAsync(int orderId, ApprovalStatus status, WorkflowStep step)
    {
        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
        Assert.Equal(status, persisted.Status);
        Assert.Equal(step, persisted.CurrentStep);
        Assert.False(await assertDb.WorkflowHistories.AsNoTracking().AnyAsync(h => h.TestOrderId == orderId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == "TestOrder" && a.EntityId == orderId.ToString() && a.Action == "Update"));
    }

    private async Task<int> SeedOrderAsync(ApprovalStatus status, WorkflowStep step, bool superseded)
    {
        await using var arrangeDb = _fixture.CreateDbContext();

        var cause = await arrangeDb.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Release" };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var sample = new Sample
        {
            ReferenceNumber = $"WSM-{tag}",
            ControlNumber = $"WSM-CTRL-{tag}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = _fixture.SeededUserId,
            SampledBy = "Workflow state machine test"
        };
        var microSectionId = (await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO")).Id;
        var order = new TestOrder
        {
            Sample = sample, TestCode = "TAMC", Status = status, CurrentStep = step,
            SectionId = microSectionId, IsSuperseded = superseded
        };
        sample.TestOrders.Add(order);
        arrangeDb.Samples.Add(sample);
        await arrangeDb.SaveChangesAsync();
        return order.Id;
    }
}
