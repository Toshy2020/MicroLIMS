using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The incubation lock applies to BrothEnrichment, SelectiveBroth and
// ConfirmatoryPlating steps only (spec 3.5).
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

    [Fact]
    public async Task SubmitBrothAsync_RecordsTheIncubationWindowAndDoesNotSetAFinalResult()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, "Slight turbidity.", userId: 4);

        var incubation = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id && i.StepName == "Broth Enrichment");
        Assert.Equal(start, incubation.IncubationStartUtc);
        Assert.Equal(end, incubation.IncubationEndUtc);
        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task SubmitBrothAsync_BeforeTheWindowEnds_ThrowsIncubationNotComplete()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23), null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
        Assert.NotNull(ex.RemainingSeconds);
        Assert.True(ex.RemainingSeconds > 0);
    }

    [Fact]
    public async Task SubmitBrothAsync_WithAnOutOfRangeIncubator_ThrowsTempOutOfRange()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, _) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var wrongIncubator = new Equipment { Name = "INC-99", Code = "INC-99", Type = EquipmentType.Incubator, SetPointTemperature = 55 };
        db.Equipment.Add(wrongIncubator);
        await db.SaveChangesAsync();
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, wrongIncubator.Id,
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubatorTempOutOfRange, ex.ErrorCode);
    }
}
