using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.WorkflowTests;

public record SeededMedia(int BrothLotId, int SelectiveBrothLotId, int SelectivePlatingLotId, int XldLotId, int TsiLotId,
    int BrothMaterialId, int SelectiveBrothMaterialId, int SelectivePlatingMaterialId, int XldMaterialId, int TsiMaterialId,
    int XldStepMediaId, int TsiStepMediaId, int SelectiveBrothIncubatorId);

// Builds the canonical five-stage pathogen template plus every master
// row it depends on. Mirrors DbSeeder.SeedPathogenTemplate.
public static class PathogenTestData
{
    public static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    public static async Task<(TestOrder order, SeededMedia media, Equipment incubator)> SeedFiveStageOrderAsync(MicroLimsDbContext db)
    {
        var organism = new Organism { ScientificName = "Salmonella enterica" };
        db.Organisms.Add(organism);

        var incubator = new Equipment { Name = "INC-03", Code = "INC-03", Type = EquipmentType.Incubator, SetPointTemperature = 36 };
        // Selective Broth's 41-43C range is disjoint from every other
        // step's 35-37C - a real incubator can't serve both, so this
        // fixture needs a second one rather than one shared set point.
        var selectiveBrothIncubator = new Equipment { Name = "INC-07", Code = "INC-07", Type = EquipmentType.Incubator, SetPointTemperature = 42 };
        db.Equipment.AddRange(incubator, selectiveBrothIncubator);

        var test = new TestDefinition { Code = "PATHOGEN_SALMONELLA", DisplayName = "Salmonella", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();

        var steps = new[]
        {
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 1, StepName = "Broth Enrichment", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 2, StepName = "Selective Broth", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 41, TemperatureMax = 43, StepType = StepType.SelectiveBroth },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 3, StepName = "Selective Plating", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.SelectivePlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 4, StepName = "Confirmatory Plating", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.ConfirmatoryPlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = true, StepType = StepType.BiochemicalTest }
        };
        db.TestWorkflowSteps.AddRange(steps);
        await db.SaveChangesAsync();

        var (brothMaterial, brothLot) = await AddMediumAsync(db, "Tryptone Soya Broth", "TSB/1/26");
        var (selBrothMaterial, selBrothLot) = await AddMediumAsync(db, "Rappaport Vassiliadis Broth", "RVS/1/26");
        var (platingMaterial, platingLot) = await AddMediumAsync(db, "XLD Agar", "XLD/1/26");
        var (tsiMaterial, tsiLot) = await AddMediumAsync(db, "TSI Agar", "TSI/1/26");

        // IncubationMinHours/MaxHours mirror each parent step's own 18-24h
        // window - these are now the operative source at execution time
        // (see TestWorkflowEngine.cs), not the step-level fields above.
        db.TestWorkflowStepMedias.AddRange(
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[0].Id, MaterialId = brothMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[1].Id, MaterialId = selBrothMaterial.Id, TempMin = 41, TempMax = 43, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[2].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 });
        var xldStepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = steps[3].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = false, DisplayOrder = 1 };
        var tsiStepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = steps[3].Id, MaterialId = tsiMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = false, DisplayOrder = 2 };
        db.TestWorkflowStepMedias.AddRange(xldStepMedia, tsiStepMedia);

        db.MediaConfigurations.AddRange(
            new MediaConfiguration
            {
                Name = "XLD Agar", EvaluationType = EvaluationType.IndicationInhibition,
                IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
                Challenges = new List<MediaConfigurationChallenge> { new() { OrganismId = organism.Id, ExpectedDescription = "Red colonies with black centres" } }
            },
            new MediaConfiguration
            {
                Name = "TSI Agar", EvaluationType = EvaluationType.IndicationInhibition,
                IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
                Challenges = new List<MediaConfigurationChallenge> { new() { OrganismId = organism.Id, ExpectedDescription = "Alkaline slant, acid butt, H2S positive" } }
            });
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        // Set so the reviewer-send-back notification path (which notifies
        // AssignedAnalystId) is actually reachable from this fixture -
        // without it, RecordBiochemicalReviewDecisionAsync's notify call is
        // silently skipped and a test named for it exercises nothing.
        var order = new TestOrder { TestCode = "PATHOGEN_SALMONELLA", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 4 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var media = new SeededMedia(brothLot.Id, selBrothLot.Id, platingLot.Id, platingLot.Id, tsiLot.Id,
            brothMaterial.Id, selBrothMaterial.Id, platingMaterial.Id, platingMaterial.Id, tsiMaterial.Id,
            xldStepMedia.Id, tsiStepMedia.Id, selectiveBrothIncubator.Id);
        return (order, media, incubator);
    }

    private static async Task<(Material material, Media lot)> AddMediumAsync(MicroLimsDbContext db, string materialName, string lotNumber)
    {
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = materialName, ManufacturerName = "Himedia",
            BatchNumber = $"LOT-{lotNumber}", ReceivingDate = DateTime.UtcNow.AddDays(-10), Location = "Micro Lab",
            QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var lot = new Media { MaterialId = material.Id, LotNumber = lotNumber, IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(lot);
        await db.SaveChangesAsync();
        return (material, lot);
    }

    public static async Task<MicroLIMS.Application.Workflows.StepResultDto> SubmitBrothAsync(this MicroLIMS.Application.Workflows.ITestWorkflowEngine engine,
        int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId,
        DateTime startUtc, DateTime endUtc, string? observation, int userId)
    {
        var incubation = await engine.SelectMediaAsync(testOrderId, stepName, mediaLotId, incubatorEquipmentId, userId);
        incubation.StartedAt = startUtc;
        incubation.IncubationStartUtc = startUtc;
        incubation.IncubationEndUtc = endUtc;
        incubation.ExpectedReadingAt = endUtc;
        return await engine.SubmitBrothAsync(testOrderId, stepName, observation, userId);
    }
}
