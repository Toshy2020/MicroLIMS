using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Pathogen-shaped tests (WorkflowType.Observation - a generic TSB ->
// Detection chain; WorkflowType.DualPlate - Salmonella's TSB -> RVS ->
// XLD_TSI chain) run through the generic TestWorkflowEngine, driven
// entirely by seeded TestWorkflowStep templates - mirrors the seed
// shapes in DbSeeder.SeedWorkflowTemplates. No test code or step name
// is special-cased in the engine itself; these are just two different
// template shapes.
public class PathogenWorkflowTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<MediaType> AddMediaTypeAsync(MicroLimsDbContext db, MediaClass mediaClass, decimal tempMin, decimal tempMax)
    {
        var mediaType = new MediaType { Class = mediaClass, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = tempMin, RequiredTemperatureMax = tempMax };
        db.MediaTypes.Add(mediaType);
        await db.SaveChangesAsync();
        return mediaType;
    }

    private static async Task<Media> AddReleasedMediaAsync(MicroLimsDbContext db, MediaType mediaType, string lotNumber)
    {
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = $"{lotNumber} Powder", ManufacturerName = "Himedia",
            BatchNumber = $"LOT-{lotNumber}", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = lotNumber,
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media { MediaTypeId = mediaType.Id, MaterialId = material.Id, LotNumber = lotNumber, IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return media;
    }

    // ---- Observation (generic pathogen, e.g. PATHOGEN_ECOLI): TSB -> Detection ----

    private static async Task<(TestOrder order, Media tsbMedia, Media detectionMedia, Media wrongClassMedia)> SeedObservationOrderAsync(MicroLimsDbContext db)
    {
        var generalBroth = await AddMediaTypeAsync(db, MediaClass.GeneralBroth, 35, 37);
        var selectiveAgar = await AddMediaTypeAsync(db, MediaClass.SelectiveAgar, 35, 37);
        var selectiveBroth = await AddMediaTypeAsync(db, MediaClass.SelectiveBroth, 41, 43); // wrong class for either step

        var testDefinition = new TestDefinition { Code = "PATHOGEN_ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.AddRange(
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "TSB", MediaTypeId = generalBroth.Id, IncubationMinHours = 24, IncubationMaxHours = 72, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = false, IsDualPlate = false },
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 2, StepName = "Detection", MediaTypeId = selectiveAgar.Id, IncubationMinHours = 24, IncubationMaxHours = 72, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = true, IsDualPlate = false });
        await db.SaveChangesAsync();

        var tsbMedia = await AddReleasedMediaAsync(db, generalBroth, "TSB/1/26");
        var detectionMedia = await AddReleasedMediaAsync(db, selectiveAgar, "DET/1/26");
        var wrongClassMedia = await AddReleasedMediaAsync(db, selectiveBroth, "RVS/1/26");

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "PATHOGEN_ECOLI", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, tsbMedia, detectionMedia, wrongClassMedia);
    }

    [Fact]
    public async Task Observation_SelectMediaAsync_WrongMediaClass_Throws()
    {
        await using var db = NewDb();
        var (order, _, _, wrongClassMedia) = await SeedObservationOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SelectMediaAsync(order.Id, "TSB", wrongClassMedia.Id, incubatorEquipmentId: 1, userId: 1));
        Assert.Contains("requires", ex.Message);
    }

    [Fact]
    public async Task Observation_RecordResultAsync_BeforeSelectMediaAsync_Throws()
    {
        await using var db = NewDb();
        var (order, _, _, _) = await SeedObservationOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordResultAsync(order.Id, "TSB", new ObservationPayload(true), userId: 1));
    }

    [Fact]
    public async Task Observation_SelectMediaAsync_OutOfOrder_ThrowsWorkflowOrderViolation()
    {
        await using var db = NewDb();
        var (order, _, detectionMedia, _) = await SeedObservationOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SelectMediaAsync(order.Id, "Detection", detectionMedia.Id, incubatorEquipmentId: 1, userId: 1));
        Assert.Contains("Workflow order violation", ex.Message);
    }

    [Fact]
    public async Task Observation_TsbNoGrowth_DoesNotShortCircuit_DetectionStepStillRequired()
    {
        // Confirmed design decision: every step runs to completion, no
        // early exit on "no growth at TSB" - a deliberate behavior
        // change from the old PathogenWorkflowEngine.
        await using var db = NewDb();
        var (order, tsbMedia, _, _) = await SeedObservationOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "TSB", tsbMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "TSB", new ObservationPayload(false), userId: 1);

        Assert.False(result.AllStepsComplete);
        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Incubating, reloaded.CurrentStep);

        var current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Detection", current.Step!.StepName);
    }

    [Fact]
    public async Task Observation_FullChainGrowthThenGrowth_InterpretsAsDetectedAndTransitionsToReady()
    {
        await using var db = NewDb();
        var (order, tsbMedia, detectionMedia, _) = await SeedObservationOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.SelectMediaAsync(order.Id, "TSB", tsbMedia.Id, incubatorEquipmentId: 1, userId: 1);
        await engine.RecordResultAsync(order.Id, "TSB", new ObservationPayload(true), userId: 1);

        await engine.SelectMediaAsync(order.Id, "Detection", detectionMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var result = await engine.RecordResultAsync(order.Id, "Detection", new ObservationPayload(true), userId: 1);

        Assert.Equal("Detected", result.FinalResult);
        Assert.True(result.AllStepsComplete);

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        var savedResult = await db.Results.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.Equal("Detected", savedResult.InterpretedValue);
    }

    // ---- DualPlate (Salmonella): TSB -> RVS -> XLD_TSI (dual plate) ----

    private static async Task<(TestOrder order, Media tsbMedia, Media rvsMedia, Media xldTsiMedia)> SeedDualPlateOrderAsync(MicroLimsDbContext db)
    {
        var generalBroth = await AddMediaTypeAsync(db, MediaClass.GeneralBroth, 35, 37);
        var selectiveBroth = await AddMediaTypeAsync(db, MediaClass.SelectiveBroth, 42, 43);
        var selectiveAgar = await AddMediaTypeAsync(db, MediaClass.SelectiveAgar, 35, 37);

        var testDefinition = new TestDefinition { Code = "PATHOGEN_SALMONELLA", DisplayName = "Pathogen - Salmonella", WorkflowType = WorkflowType.DualPlate };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.AddRange(
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "TSB", MediaTypeId = generalBroth.Id, IncubationMinHours = 24, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = false, IsDualPlate = false },
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 2, StepName = "RVS", MediaTypeId = selectiveBroth.Id, IncubationMinHours = 24, IncubationMaxHours = 24, TemperatureMin = 42, TemperatureMax = 43, IsFinalStep = false, IsDualPlate = false },
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 3, StepName = "XLD_TSI", MediaTypeId = selectiveAgar.Id, IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = true, IsDualPlate = true });
        await db.SaveChangesAsync();

        var tsbMedia = await AddReleasedMediaAsync(db, generalBroth, "TSB/2/26");
        var rvsMedia = await AddReleasedMediaAsync(db, selectiveBroth, "RVS/2/26");
        var xldTsiMedia = await AddReleasedMediaAsync(db, selectiveAgar, "XLD/2/26");

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-2", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "PATHOGEN_SALMONELLA", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, tsbMedia, rvsMedia, xldTsiMedia);
    }

    private static async Task RunTsbAndRvsAsync(TestWorkflowEngine engine, TestOrder order, Media tsbMedia, Media rvsMedia)
    {
        await engine.SelectMediaAsync(order.Id, "TSB", tsbMedia.Id, incubatorEquipmentId: 1, userId: 1);
        await engine.RecordResultAsync(order.Id, "TSB", new ObservationPayload(true), userId: 1);
        await engine.SelectMediaAsync(order.Id, "RVS", rvsMedia.Id, incubatorEquipmentId: 1, userId: 1);
        await engine.RecordResultAsync(order.Id, "RVS", new ObservationPayload(true), userId: 1);
    }

    [Fact]
    public async Task DualPlate_BothPlatesGrowth_InterpretsAsDetectedAndTransitionsToReady()
    {
        await using var db = NewDb();
        var (order, tsbMedia, rvsMedia, xldTsiMedia) = await SeedDualPlateOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await RunTsbAndRvsAsync(engine, order, tsbMedia, rvsMedia);

        await engine.SelectMediaAsync(order.Id, "XLD_TSI", xldTsiMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var result = await engine.RecordResultAsync(order.Id, "XLD_TSI", new DualPlatePayload(true, true), userId: 1);

        Assert.Equal("Detected", result.FinalResult);
        Assert.True(result.AllStepsComplete);

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        Assert.Equal(2, await db.PathogenObservations.CountAsync(o => o.TestOrderId == order.Id && o.StepName == "XLD_TSI"));
    }

    [Fact]
    public async Task DualPlate_BothPlatesNoGrowth_InterpretsAsAbsent()
    {
        await using var db = NewDb();
        var (order, tsbMedia, rvsMedia, xldTsiMedia) = await SeedDualPlateOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await RunTsbAndRvsAsync(engine, order, tsbMedia, rvsMedia);

        await engine.SelectMediaAsync(order.Id, "XLD_TSI", xldTsiMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var result = await engine.RecordResultAsync(order.Id, "XLD_TSI", new DualPlatePayload(false, false), userId: 1);

        Assert.Equal("Absent", result.FinalResult);
    }

    [Fact]
    public async Task DualPlate_DisagreeingPlates_LeavesIncubatingWithInconclusiveHistoryNote()
    {
        await using var db = NewDb();
        var (order, tsbMedia, rvsMedia, xldTsiMedia) = await SeedDualPlateOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await RunTsbAndRvsAsync(engine, order, tsbMedia, rvsMedia);

        await engine.SelectMediaAsync(order.Id, "XLD_TSI", xldTsiMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var result = await engine.RecordResultAsync(order.Id, "XLD_TSI", new DualPlatePayload(true, false), userId: 1);

        Assert.False(result.IsDefinitive);
        Assert.False(result.AllStepsComplete);
        Assert.Equal("Inconclusive - Retest Required", result.OutcomeSummary);

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Incubating, reloadedOrder.CurrentStep);

        var history = await db.WorkflowHistories.Where(h => h.TestOrderId == order.Id).OrderByDescending(h => h.Id).FirstAsync();
        Assert.Contains("Inconclusive", history.Note);

        // Still the current step - the analyst can retry with a fresh media lot.
        var current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("XLD_TSI", current.Step!.StepName);
        Assert.Null(current.OpenIncubation); // the inconclusive attempt's incubation was closed
    }

    [Fact]
    public async Task DualPlate_OutOfOrderStep_ThrowsWorkflowOrderViolation()
    {
        await using var db = NewDb();
        var (order, _, _, xldTsiMedia) = await SeedDualPlateOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SelectMediaAsync(order.Id, "XLD_TSI", xldTsiMedia.Id, incubatorEquipmentId: 1, userId: 1));
        Assert.Contains("Workflow order violation", ex.Message);
    }
}
