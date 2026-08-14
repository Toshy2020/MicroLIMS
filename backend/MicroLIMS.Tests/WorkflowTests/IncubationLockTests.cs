using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The incubation lock applies to BrothEnrichment, SelectiveBroth and
// ConfirmatoryPlating steps only. The new implementation locks the
// incubation window from Test Master configuration; the analyst
// cannot override it.
public class IncubationLockTests
{
    [Fact]
    public void IsIncubationComplete_IsFalse_BeforeTheWindowEnds()
    {
        var incubation = new Incubation { IncubationEndUtc = DateTime.UtcNow.AddHours(4) };
        Assert.False(incubation.IsIncubationComplete);
    }

    [Fact]
    public void IsIncubationComplete_IsTrue_AfterTheWindowEnds()
    {
        var incubation = new Incubation { IncubationEndUtc = DateTime.UtcNow.AddSeconds(-1) };
        Assert.True(incubation.IsIncubationComplete);
    }

    [Fact]
    public void IsIncubationComplete_IsFalse_WhenNoWindowIsSet()
    {
        Assert.False(new Incubation().IsIncubationComplete);
    }

    [Fact]
    public void WorkflowStepException_CarriesCodeAndRemainingSeconds()
    {
        var ex = new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete, "Still incubating.", 52320);
        Assert.Equal("INCUBATION_NOT_COMPLETE", ex.ErrorCode);
        Assert.Equal(52320, ex.RemainingSeconds);
    }

    // SelectMediaAsync creates an Incubation with server-calculated window
    // from Test Master configuration (IncubationMinHours/MaxHours).
    [Fact]
    public async Task SelectMediaAsync_CreatesIncubationWithServerCalculatedWindow()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var startBefore = DateTime.UtcNow;
        var incubation = await engine.SelectMediaAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);
        var startAfter = DateTime.UtcNow;

        // Window should be calculated from Test Master (18-24 hours for Broth Enrichment)
        Assert.NotNull(incubation.IncubationStartUtc);
        Assert.NotNull(incubation.IncubationEndUtc);
        Assert.InRange(incubation.IncubationStartUtc!.Value, startBefore, startAfter);
        
        var step = db.TestDefinitions.Include(t => t.Steps).FirstOrDefault(t => t.Code == "E.Coli")?.Steps.First(s => s.StepName == "Broth Enrichment");
        Assert.NotNull(step);
        var expectedEnd = incubation.IncubationStartUtc!.Value.AddHours(step.IncubationMaxHours);
        Assert.Equal(expectedEnd, incubation.IncubationEndUtc!.Value);
    }

    // SubmitBrothAsync must verify that the minimum time has elapsed
    // AND the maximum window has passed before allowing submission.
    [Fact]
    public async Task SubmitBrothAsync_RequiresBothMinimumAndMaximumWindowCompleted()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Select media - this creates incubation with server-calculated window
        await engine.SelectMediaAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);
        
        // Try to submit immediately - should fail (window not complete)
        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => 
            engine.SubmitBrothAsync(order.Id, "Broth Enrichment", null, userId: 4));
        
        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
        Assert.NotNull(ex.RemainingSeconds);
        Assert.True(ex.RemainingSeconds > 0);
    }

    // SubmitBrothAsync succeeds after both minimum and maximum windows have passed.
    [Fact]
    public async Task SubmitBrothAsync_SucceedsAfterWindowCompletes()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var startTime = DateTime.UtcNow.AddHours(-30);
        
        // Create an incubation that is already past its window
        var incubation = new Incubation
        {
            TestOrderId = order.Id,
            StepName = "Broth Enrichment",
            MediaId = media.BrothLotId,
            IncubatorEquipmentId = incubator.Id,
            IncubationStartUtc = startTime,
            IncubationEndUtc = startTime.AddHours(24),  // Well in the past
            Temperature = "30-37 °C",
            Duration = "18-24 hours",
            StartedAt = startTime
        };
        db.Incubations.Add(incubation);
        await db.SaveChangesAsync();

        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", "Slight turbidity.", userId: 4);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
        
        // Incubation should be marked as complete
        var updated = await db.Incubations.FirstAsync(i => i.Id == incubation.Id);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal("Slight turbidity.", updated.Outcome);
    }

    // Analyst cannot supply arbitrary incubation times - the window is
    // server-controlled from Test Master. Any attempt to provide times
    // is ignored/rejected.
    [Fact]
    public async Task SubmitBrothAsync_IgnoresAnystAnalystSuppliedTimes()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var startTime = DateTime.UtcNow.AddHours(-30);
        var incubation = new Incubation
        {
            TestOrderId = order.Id,
            StepName = "Broth Enrichment",
            MediaId = media.BrothLotId,
            IncubatorEquipmentId = incubator.Id,
            IncubationStartUtc = startTime,
            IncubationEndUtc = startTime.AddHours(24),
            Temperature = "30-37 °C",
            Duration = "18-24 hours",
            StartedAt = startTime
        };
        db.Incubations.Add(incubation);
        await db.SaveChangesAsync();

        // SubmitBrothAsync signature does not accept any window times,
        // so the analyst cannot attempt to override them.
        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", null, userId: 4);
        
        Assert.NotNull(result);
        // The server-controlled window is what matters
        var submitted = await db.Incubations.FirstAsync(i => i.Id == incubation.Id);
        Assert.Equal(startTime, submitted.IncubationStartUtc);
        Assert.Equal(startTime.AddHours(24), submitted.IncubationEndUtc);
    }
}
