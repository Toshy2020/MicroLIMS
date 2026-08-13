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

    // The lock only ever asked "has the declared end passed?", which a
    // one-second window satisfies as readily as a real 18-24h one. The
    // seeded Broth Enrichment step mandates a minimum of 18 hours.
    [Fact]
    public async Task SubmitBrothAsync_WithAOneSecondWindow_ThrowsWindowTooShort()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var end = DateTime.UtcNow.AddHours(-6);
        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            end.AddSeconds(-1), end, null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubationWindowTooShort, ex.ErrorCode);
        Assert.Empty(await db.Incubations.Where(i => i.TestOrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task SubmitBrothAsync_WithAWindowThatEndsBeforeItStarts_ThrowsWindowInvalid()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            DateTime.UtcNow.AddHours(-6), DateTime.UtcNow.AddHours(-10), null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubationWindowInvalid, ex.ErrorCode);
        Assert.Empty(await db.Incubations.Where(i => i.TestOrderId == order.Id).ToListAsync());
    }

    // Over-incubation is legitimate and explained, never blocked.
    [Fact]
    public async Task SubmitBrothAsync_WithAWindowLongerThanTheTemplateMaximum_IsAccepted()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 42h against an 18-24h template.
        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            DateTime.UtcNow.AddHours(-48), DateTime.UtcNow.AddHours(-6), "Held over the weekend.", userId: 4);

        Assert.True(result.NextStepUnlocked);
    }

    // The declared window is a claim; the received-at stamp is the one
    // timestamp on the row the analyst cannot influence.
    [Fact]
    public async Task SubmitBrothAsync_StampsAServerGeneratedReceivedAt_AlongsideTheDeclaredWindow()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var before = DateTime.UtcNow;
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, userId: 4);
        var after = DateTime.UtcNow;

        var incubation = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id);
        Assert.NotNull(incubation.WindowReceivedAtUtc);
        Assert.InRange(incubation.WindowReceivedAtUtc!.Value, before, after);
        // The claim itself is still recorded verbatim next to it.
        Assert.Equal(start, incubation.IncubationStartUtc);
        Assert.Equal(end, incubation.IncubationEndUtc);
    }

    [Fact]
    public async Task SubmitConfirmatorySetupAsync_StampsAServerGeneratedReceivedAt()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, userId: 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, userId: 4);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, userId: 4);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating",
            new[] { new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id) }, start, end, userId: 4);

        var incubation = await db.Incubations.SingleAsync(i => i.StepName == "Confirmatory Plating");
        Assert.NotNull(incubation.WindowReceivedAtUtc);
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
