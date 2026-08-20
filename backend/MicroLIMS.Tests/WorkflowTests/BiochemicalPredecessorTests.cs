using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// SubmitBiochemicalAsync used to hardcode ConfirmatoryPlating as the
// biochemical step's predecessor. Some organisms (e.g. Burkholderia
// cepacia complex) go straight from SelectivePlating to phenotypic
// BiochemicalTest steps with no ConfirmatoryPlating step at all - these
// tests cover that shape, plus the "only the last biochemical step
// finalizes the order" behaviour a multi-step chain like Oxidase ->
// Identification Kit depends on.
public class BiochemicalPredecessorTests
{
    private const int AnalystId = 4;

    // BrothEnrichment -> SelectivePlating -> BiochemicalTest (Oxidase) ->
    // BiochemicalTest (Identification Kit, final). No ConfirmatoryPlating
    // step - mirrors the reconfigured Burkholderia cepacia complex template.
    private static async Task<(TestOrder order, int brothLotId, int platingLotId, Equipment incubator)> SeedNoConfirmatoryOrderAsync(MicroLimsDbContext db)
    {
        var generalBroth = new MediaType { Class = MediaClass.GeneralBroth, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = 35, RequiredTemperatureMax = 37 };
        var selectiveAgar = new MediaType { Class = MediaClass.SelectiveAgar, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = 35, RequiredTemperatureMax = 37 };
        db.MediaTypes.AddRange(generalBroth, selectiveAgar);

        var organism = new Organism { ScientificName = "Burkholderia cepacia complex" };
        db.Organisms.Add(organism);

        var incubator = new Equipment { Name = "INC-03", Code = "INC-03", Type = EquipmentType.Incubator, SetPointTemperature = 36 };
        db.Equipment.Add(incubator);

        var test = new TestDefinition { Code = "PATHOGEN_BCC", DisplayName = "Burkholderia cepacia complex", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();

        var steps = new[]
        {
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 1, StepName = "Broth Enrichment", MediaTypeId = generalBroth.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 2, StepName = "BUR", MediaTypeId = selectiveAgar.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.SelectivePlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 3, StepName = "Oxidase", IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 0, TemperatureMax = 0, StepType = StepType.BiochemicalTest, PhenotypicTestType = PhenotypicTestType.Oxidase },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 4, StepName = "Identification Kit", IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 0, TemperatureMax = 0, IsFinalStep = true, StepType = StepType.BiochemicalTest, PhenotypicTestType = PhenotypicTestType.IdentificationKit }
        };
        db.TestWorkflowSteps.AddRange(steps);
        await db.SaveChangesAsync();

        var brothMaterial = new Material { MaterialType = MaterialType.DehydratedMedia, MaterialName = "Tryptone Soya Broth", ManufacturerName = "Himedia", BatchNumber = "LOT-TSB", ReceivingDate = DateTime.UtcNow.AddDays(-10), Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram };
        var platingMaterial = new Material { MaterialType = MaterialType.DehydratedMedia, MaterialName = "Burkholderia Cepacia Medium", ManufacturerName = "Himedia", BatchNumber = "LOT-BUR", ReceivingDate = DateTime.UtcNow.AddDays(-10), Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram };
        db.Materials.AddRange(brothMaterial, platingMaterial);
        await db.SaveChangesAsync();

        var brothLot = new Media { MediaTypeId = generalBroth.Id, MaterialId = brothMaterial.Id, LotNumber = "TSB/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        var platingLot = new Media { MediaTypeId = selectiveAgar.Id, MaterialId = platingMaterial.Id, LotNumber = "BUR/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.AddRange(brothLot, platingLot);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.AddRange(
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[0].Id, MaterialId = brothMaterial.Id, TempMin = 35, TempMax = 37, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[1].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IsRequired = true, DisplayOrder = 1 });
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-BCC-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "PATHOGEN_BCC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = AnalystId };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, brothLot.Id, platingLot.Id, incubator);
    }

    [Fact]
    public async Task SubmitBiochemical_WithSelectivePlatingPredecessor_Succeeds()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, brothLotId, platingLotId, incubator) = await SeedNoConfirmatoryOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", brothLotId, incubator.Id, start, end, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "BUR", platingLotId, incubator.Id, start, end, GrowthObservation.GrowthConforming, AnalystId);

        var result = await engine.SubmitBiochemicalAsync(order.Id, "Oxidase", "Oxidase positive.", null, AnalystId);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Oxidase");
        Assert.Equal("Oxidase positive.", stored.BiochemicalResultText);

        var reloaded = await db.TestOrders.SingleAsync(t => t.Id == order.Id);
        Assert.NotEqual(WorkflowStep.Ready, reloaded.CurrentStep);
    }

    [Fact]
    public async Task SubmitBiochemical_OnlyTheLastStepInTheChain_FinalizesTheOrder()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, brothLotId, platingLotId, incubator) = await SeedNoConfirmatoryOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", brothLotId, incubator.Id, start, end, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "BUR", platingLotId, incubator.Id, start, end, GrowthObservation.GrowthConforming, AnalystId);
        await engine.SubmitBiochemicalAsync(order.Id, "Oxidase", "Oxidase positive.", null, AnalystId);

        var result = await engine.SubmitBiochemicalAsync(order.Id, "Identification Kit", "Confirmed B. cepacia complex.", null, AnalystId);

        Assert.Equal("Detected", result.WorkflowFinalResult);
        Assert.False(result.NextStepUnlocked);

        var reloaded = await db.TestOrders.SingleAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloaded.CurrentStep);
    }

    [Fact]
    public async Task SubmitBiochemical_WithoutAConformingSelectivePlatingResult_IsRejected()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, brothLotId, platingLotId, incubator) = await SeedNoConfirmatoryOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", brothLotId, incubator.Id, start, end, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "BUR", platingLotId, incubator.Id, start, end, GrowthObservation.GrowthNonConforming, AnalystId);

        // A non-conforming SelectivePlating result already finalizes the
        // order as NotDetected (SubmitSelectivePlatingObservationAsync),
        // so this also exercises RequireOrderNotFinalized's rejection -
        // there is no scenario where a non-conforming plate leaves the
        // biochemical step reachable.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SubmitBiochemicalAsync(order.Id, "Oxidase", "Oxidase positive.", null, AnalystId));
    }

    [Fact]
    public async Task SubmitBiochemical_WithNoPrecedingPlateStepConfigured_IsRejected()
    {
        await using var db = PathogenTestData.NewDb();
        var test = new TestDefinition { Code = "PATHOGEN_MISCONFIGURED", DisplayName = "Misconfigured", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 1, StepName = "Gram Stain", IncubationMinHours = 0, IncubationMaxHours = 0,
            TemperatureMin = 0, TemperatureMax = 0, IsFinalStep = true, StepType = StepType.BiochemicalTest, PhenotypicTestType = PhenotypicTestType.Gram
        });
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MISC-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "PATHOGEN_MISCONFIGURED", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = AnalystId };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SubmitBiochemicalAsync(order.Id, "Gram Stain", "Gram negative.", null, AnalystId));
        Assert.Contains("no preceding selective or confirmatory plating step", ex.Message);
    }
}
