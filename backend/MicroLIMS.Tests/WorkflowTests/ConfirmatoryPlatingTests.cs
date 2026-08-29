using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class ConfirmatoryPlatingTests
{
    private static async Task<(int orderId, SeededMedia media, int incubatorId, ITestWorkflowEngine engine, MicroLimsDbContext db)> ReadyForConfirmatoryAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, 4);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, 4);
        return (order.Id, media, incubator.Id, engine, db);
    }

    private static ConfirmatorySelectionInput[] BothMedia(SeededMedia media, int incubatorId) => new[]
    {
        new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubatorId),
        new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubatorId)
    };

    [Fact]
    public async Task Setup_RecordsOneSelectionPerChosenMedium()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var result = await db.WorkflowStepResults.Include(r => r.Selections).SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.Equal(2, result.Selections.Count);
        Assert.All(result.Selections, s => Assert.False(s.WasAnalystAdded));
    }

    // The real-world case that motivated the Media Configuration
    // Migration's Test Master reversal: a confirmatory panel can mix media
    // with genuinely different windows on the same step (XLD vs TSI). One
    // shared Incubation row can't hold two windows, so the panel's window
    // is the widest safe bound - never shorter than the strictest medium's
    // minimum, never shorter than the longest medium's maximum - and the
    // display reflects both ranges rather than a single misleading one.
    [Fact]
    public async Task Setup_MediaWithDifferentWindows_UsesWidestSafeBoundAndShowsBothRanges()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        var xld = await db.TestWorkflowStepMedias.FirstAsync(m => m.Id == media.XldStepMediaId);
        xld.TempMin = 35; xld.TempMax = 37; xld.IncubationMinHours = 18; xld.IncubationMaxHours = 24;
        var tsi = await db.TestWorkflowStepMedias.FirstAsync(m => m.Id == media.TsiStepMediaId);
        tsi.TempMin = 40; tsi.TempMax = 45; tsi.IncubationMinHours = 24; tsi.IncubationMaxHours = 30;
        // TSI's 40-45C range is disjoint from the fixture's shared
        // incubator (36C, sized for XLD's 35-37C) - a real incubator can't
        // serve both, same reasoning as every other two-window fixture.
        var tsiIncubator = new Equipment { Name = "INC-TSI", Code = "INC-TSI", Type = EquipmentType.Incubator, SetPointTemperature = 42 };
        db.Equipment.Add(tsiIncubator);
        await db.SaveChangesAsync();

        var selections = new[]
        {
            new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubatorId),
            new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, tsiIncubator.Id)
        };

        var start = DateTime.UtcNow.AddHours(-30);
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", selections,
            start, start.AddHours(1), 4); // requested end is ignored/recalculated server-side

        var incubation = await db.Incubations.SingleAsync(i => i.TestOrderId == orderId && i.StepName == "Confirmatory Plating");

        // Widest maximum (TSI's 30h, not XLD's 24h or either step-level value).
        Assert.Equal(start.AddHours(30), incubation.IncubationEndUtc);
        // Both temperature ranges are visible, not a single step-level fiction.
        Assert.Contains("35-37", incubation.Temperature);
        Assert.Contains("40-45", incubation.Temperature);
        Assert.Contains("18-24h", incubation.Duration);
        Assert.Contains("24-30h", incubation.Duration);
    }

    [Fact]
    public async Task Setup_WithNoMedia_ThrowsNoMediaSelected()
    {
        var (orderId, _, _, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _db = db;

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            orderId, "Confirmatory Plating", Array.Empty<ConfirmatorySelectionInput>(),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4));

        Assert.Equal(WorkflowErrorCodes.NoMediaSelected, ex.ErrorCode);
    }

    [Fact]
    public async Task Setup_WithAMediumFromAnotherStep_ThrowsMediaNotInPermittedList()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        // The selective plating step's own medium is not on the
        // confirmatory step's permitted list.
        var foreign = await db.TestWorkflowStepMedias
            .Include(m => m.TestWorkflowStep)
            .FirstAsync(m => m.TestWorkflowStep!.StepType == StepType.SelectivePlating);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatorySelectionInput(foreign.Id, media.SelectivePlatingLotId, incubatorId) },
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4));

        Assert.Equal(WorkflowErrorCodes.MediaNotInPermittedList, ex.ErrorCode);
    }

    [Fact]
    public async Task Observations_AllConforming_RequiresAnAnalystDecision()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        Assert.Equal("AllConforming", outcome.ConfirmatoryResult);
        Assert.True(outcome.AnalystDecisionRequired);
        Assert.Empty(outcome.Flags);
    }

    [Theory]
    [InlineData(GrowthObservation.NoGrowth)]
    [InlineData(GrowthObservation.GrowthNonConforming)]
    public async Task Observations_MixedResults_AreInconclusiveAndFlagged(GrowthObservation second)
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, second)
        }, 4);

        Assert.Equal("Inconclusive", outcome.ConfirmatoryResult);
        Assert.False(outcome.AnalystDecisionRequired);
        Assert.Contains("InconclusiveResult", outcome.Flags);
    }

    // An Inconclusive confirmatory result used to be a scientific dead end -
    // SubmitBiochemicalAsync refused any preceding result that wasn't
    // AllConforming, and the analyst had no way forward. Inconclusive
    // doesn't rule the organism out, though - it just means morphology
    // alone couldn't call it, which is exactly what biochemical ID is for.
    [Fact]
    public async Task SubmitBiochemicalAsync_AfterInconclusiveConfirmatory_Succeeds()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthNonConforming)
        }, 4);
        Assert.Equal("Inconclusive", outcome.ConfirmatoryResult);

        var result = await engine.SubmitBiochemicalAsync(orderId, "Biochemical Test", "IMViC: +,-,-,+ (E. coli pattern)", null, true, 4);

        Assert.Equal("Complete", result.Status);
        Assert.Equal("Detected", result.WorkflowFinalResult);
        // Driven by the explicit organismDetected argument, not a hardcode -
        // proven by the companion test below with the same setup but
        // organismDetected: false, which must NOT also report "Detected".
        var stored = await db.WorkflowStepResults.SingleAsync(r => r.TestOrderId == orderId && r.StepType == StepType.BiochemicalTest);
        Assert.True(stored.BiochemicalOrganismDetected);
    }

    // The exact incident this field exists to prevent: an Inconclusive (or
    // AllConforming) confirmatory result followed by a biochemical result
    // the analyst interprets as negative must finalize as NotDetected, not
    // silently as Detected because SubmitBiochemicalAsync used to hardcode it.
    [Fact]
    public async Task SubmitBiochemicalAsync_OrganismNotDetected_FinalizesAsNotDetected()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthNonConforming)
        }, 4);
        Assert.Equal("Inconclusive", outcome.ConfirmatoryResult);

        var result = await engine.SubmitBiochemicalAsync(orderId, "Biochemical Test", "IMViC: -,-,-,- (absence of E. coli pattern)", null, false, 4);

        Assert.Equal("Complete", result.Status);
        Assert.Equal("NotDetected", result.WorkflowFinalResult);

        var order = await db.TestOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
        var stored = await db.WorkflowStepResults.SingleAsync(r => r.TestOrderId == orderId && r.StepType == StepType.BiochemicalTest);
        Assert.False(stored.BiochemicalOrganismDetected);
    }

    // A Biochemical Test step can now bundle several phenotypic tests (e.g.
    // Gram Stain + Oxidase + Identification Kit) instead of chaining a
    // separate step per type - confirms SubmitBiochemicalAsync still
    // records one combined result and one Detected/Not-Detected decision
    // for the whole step, unchanged, when the step has several configured
    // phenotypic tests instead of one.
    [Fact]
    public async Task SubmitBiochemicalAsync_StepWithBundledPhenotypicTests_Succeeds()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);
        await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        var biochemicalStep = await db.TestWorkflowSteps.FirstAsync(s => s.StepName == "Biochemical Test");
        db.TestWorkflowStepPhenotypicTests.AddRange(
            new TestWorkflowStepPhenotypicTest { TestWorkflowStepId = biochemicalStep.Id, PhenotypicTestType = PhenotypicTestType.Gram, DisplayOrder = 0 },
            new TestWorkflowStepPhenotypicTest { TestWorkflowStepId = biochemicalStep.Id, PhenotypicTestType = PhenotypicTestType.Oxidase, DisplayOrder = 1 },
            new TestWorkflowStepPhenotypicTest { TestWorkflowStepId = biochemicalStep.Id, PhenotypicTestType = PhenotypicTestType.IdentificationKit, DisplayOrder = 2 });
        await db.SaveChangesAsync();

        var result = await engine.SubmitBiochemicalAsync(orderId, "Biochemical Test",
            "Gram: negative rods. Oxidase: negative. IMViC: +,-,-,+ (E. coli pattern).", null, true, 4);

        Assert.Equal("Complete", result.Status);
        Assert.Equal("Detected", result.WorkflowFinalResult);
        var stored = await db.WorkflowStepResults.SingleAsync(r => r.TestOrderId == orderId && r.StepType == StepType.BiochemicalTest);
        Assert.True(stored.BiochemicalOrganismDetected);
    }

    [Fact]
    public async Task Observations_SnapshotExpectedAppearancePerMedium()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        var observations = await db.ConfirmatoryPlateObservations.ToListAsync();
        Assert.Equal(2, observations.Count);
        Assert.Contains(observations, o => o.ExpectedAppearanceSnapshot == "Red colonies with black centres");
        Assert.Contains(observations, o => o.ExpectedAppearanceSnapshot == "Alkaline slant, acid butt, H2S positive");
    }

    [Fact]
    public async Task Observations_BeforeTheWindowEnds_ThrowIncubationNotComplete()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23), 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming) }, 4));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task Observations_MissingAMediumThatWasSetUp_ThrowsIncompleteSetup()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming) }, 4));

        Assert.Equal(WorkflowErrorCodes.IncompleteConfirmatorySetup, ex.ErrorCode);
    }
}
