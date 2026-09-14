using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MediaConfigurationMasterDataTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static MasterDataController CreateController(MicroLimsDbContext db) =>
        new(db, new EquipmentConfigurationService(db), TestServiceFactory.MediaProduct(db));

    [Fact]
    public async Task GetMediaConfigurations_ReturnsConfigurationsWithChallengesAndOrganisms()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var org = new Organism { ScientificName = "Staphylococcus aureus", AtccNumber = "6538" };
        db.Organisms.Add(org);
        await db.SaveChangesAsync();

        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m,
            RecoveryPercentMin = 70.0m,
            RecoveryPercentMax = 200.0m,
            Challenges = new List<MediaConfigurationChallenge>
            {
                new() { OrganismId = org.Id }
            }
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetMediaConfigurations();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task CreateMediaConfiguration_ValidPayload_CreatesEntityAndAuditLog()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "MacConkey Agar", "MCA");
        var org = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        db.Organisms.Add(org);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.IndicationInhibition,
            18,
            24,
            35.0m,
            37.0m,
            null,
            null,
            new List<CreateMediaConfigurationChallengeRequest>
            {
                new(org.Id, ChallengeRole.Indication, "Pink-red colonies", "10^2")
            }
        );

        var result = await controller.CreateMediaConfiguration(req);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);

        var saved = await db.MediaConfigurations
            .Include(m => m.Challenges)
            .SingleAsync(m => m.Name == "MacConkey Agar");

        Assert.Equal(product.Id, saved.MediaProductId);
        Assert.Equal(product.Name, saved.Name);
        Assert.Equal(EvaluationType.IndicationInhibition, saved.EvaluationType);
        Assert.Equal(18, saved.IncubationMinHours);
        Assert.Equal(24, saved.IncubationMaxHours);
        Assert.Equal(35.0m, saved.TemperatureMin);
        Assert.Equal(37.0m, saved.TemperatureMax);
        Assert.Single(saved.Challenges);
        Assert.Equal(org.Id, saved.Challenges[0].OrganismId);
        Assert.Equal(ChallengeRole.Indication, saved.Challenges[0].ChallengeRole);
        Assert.Equal("Pink-red colonies", saved.Challenges[0].ExpectedDescription);
        Assert.Equal("10^2", saved.Challenges[0].InitialInoculum);

        // Verify audit log
        var audits = await db.AuditLogs.Where(a => a.EntityName == nameof(MediaConfiguration)).ToListAsync();
        Assert.NotEmpty(audits);
        Assert.Contains(audits, a => a.Action == "Create");
    }

    [Fact]
    public async Task CreateMediaConfiguration_NonExistentProduct_Throws()
    {
        await using var db = NewDb();
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            9999,
            EvaluationType.GrowthPromotion,
            24,
            48,
            30.0m,
            35.0m,
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
    }

    [Fact]
    public async Task CreateMediaConfiguration_InvalidIncubationRange_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            48,
            24, // Min > Max
            30.0m,
            35.0m,
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
    }

    [Fact]
    public async Task CreateMediaConfiguration_InvalidTemperatureRange_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            24,
            48,
            40.0m,
            35.0m, // Min > Max
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
    }

    [Fact]
    public async Task CreateMediaConfiguration_DuplicateProfile_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Sabouraud Dextrose Agar", "SDA");
        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 48,
            IncubationMaxHours = 120,
            TemperatureMin = 20.0m,
            TemperatureMax = 25.0m
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            48,
            120,
            20.0m,
            25.0m,
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
    }

    [Fact]
    public async Task CreateMediaConfiguration_NonExistentOrganism_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Blood Agar", "BA");
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            24,
            48,
            35.0m,
            37.0m,
            null,
            null,
            new List<CreateMediaConfigurationChallengeRequest>
            {
                new(9999, null, null, null)
            }
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
    }

    [Fact]
    public async Task UpdateMediaConfiguration_ValidPayload_UpdatesEntityAndChildChallenges()
    {
        await using var db = NewDb();
        var product1 = await MediaProductTestData.CreateOrGetAsync(db, "XLD Agar", "XLD");
        var product2 = await MediaProductTestData.CreateOrGetAsync(db, "Xylose Lysine Deoxycholate Agar", "XLDA");
        var org1 = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        var org2 = new Organism { ScientificName = "Salmonella enterica", AtccNumber = "14028" };
        db.Organisms.AddRange(org1, org2);

        var config = new MediaConfiguration
        {
            MediaProductId = product1.Id,
            Name = product1.Name,
            EvaluationType = EvaluationType.IndicationInhibition,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TemperatureMin = 35.0m,
            TemperatureMax = 37.0m,
            Challenges = new List<MediaConfigurationChallenge>
            {
                new() { OrganismId = org1.Id, ChallengeRole = ChallengeRole.Inhibition }
            }
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(
            product2.Id,
            EvaluationType.IndicationInhibition,
            24,
            48,
            35.0m,
            37.0m,
            null,
            null,
            new List<CreateMediaConfigurationChallengeRequest>
            {
                new(org2.Id, ChallengeRole.Indication, "Red colonies with black centers", "10^2")
            }
        );

        var result = await controller.UpdateMediaConfiguration(config.Id, req);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);

        var updated = await db.MediaConfigurations
            .Include(m => m.Challenges)
            .SingleAsync(m => m.Id == config.Id);

        Assert.Equal(product2.Id, updated.MediaProductId);
        Assert.Equal("Xylose Lysine Deoxycholate Agar", updated.Name);
        Assert.Equal(24, updated.IncubationMinHours);
        Assert.Equal(48, updated.IncubationMaxHours);
        Assert.Single(updated.Challenges);
        Assert.Equal(org2.Id, updated.Challenges[0].OrganismId);
        Assert.Equal(ChallengeRole.Indication, updated.Challenges[0].ChallengeRole);
        Assert.Equal("Red colonies with black centers", updated.Challenges[0].ExpectedDescription);
        Assert.Equal("10^2", updated.Challenges[0].InitialInoculum);

        // Audit verification
        var audits = await db.AuditLogs.Where(a => a.EntityName == nameof(MediaConfiguration) && a.Action == "Update").ToListAsync();
        Assert.NotEmpty(audits);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_SameProfileSelf_SucceedsWithoutDuplicateError()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Nutrient Agar", "NA");
        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            24,
            48,
            30.0m,
            35.0m,
            80.0m,
            150.0m,
            null
        );

        var result = await controller.UpdateMediaConfiguration(config.Id, req);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);

        var updated = await db.MediaConfigurations.SingleAsync(m => m.Id == config.Id);
        Assert.Equal(product.Id, updated.MediaProductId);
        Assert.Equal(product.Name, updated.Name);
        Assert.Equal(80.0m, updated.RecoveryPercentMin);
        Assert.Equal(150.0m, updated.RecoveryPercentMax);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_DuplicateProfileOtherRow_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Cetrimide Agar", "CA");
        var configA = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TemperatureMin = 35.0m,
            TemperatureMax = 37.0m
        };
        var configB = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 35.0m,
            TemperatureMax = 37.0m
        };
        db.MediaConfigurations.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        // Attempt to update B to have configA's profile (18-24h @ 35-37C)
        var req = new UpdateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            18,
            24,
            35.0m,
            37.0m,
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(configB.Id, req));
    }

    [Fact]
    public async Task UpdateMediaConfiguration_NonExistentId_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            24,
            48,
            30.0m,
            35.0m,
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(9999, req));
    }

    [Fact]
    public async Task DeleteMediaConfiguration_NotReferenced_Succeeds()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Unused Agar", "UA");
        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.DeleteMediaConfiguration(config.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.False(await db.MediaConfigurations.AnyAsync(m => m.Id == config.Id));
    }

    [Fact]
    public async Task DeleteMediaConfiguration_ReferencedByStepMedia_ThrowsNamingTheStep()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 1,
            IncubationMaxHours = 2,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m
        };
        db.MediaConfigurations.Add(config);
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = product.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "LOT-1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Micro Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        MediaProductTestData.Link(material, product);

        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id,
            StepOrder = 1,
            StepName = "CountIncubation",
            IncubationMinHours = 1,
            IncubationMaxHours = 2,
            TemperatureMin = 30,
            TemperatureMax = 35,
            IsFinalStep = true,
            StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id,
            MaterialId = material.Id,
            MediaConfigurationId = config.Id,
            TempMin = 30,
            TempMax = 35
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.DeleteMediaConfiguration(config.Id));
        Assert.Contains("CountIncubation", ex.Message);

        Assert.True(await db.MediaConfigurations.AnyAsync(m => m.Id == config.Id));
    }

    [Fact]
    public async Task UpdateMediaConfiguration_ReferencedByStepMedia_RefusesMovingToDifferentProduct()
    {
        await using var db = NewDb();
        var product1 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 1", "TSA1");
        var product2 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 2", "TSA2");

        var config = new MediaConfiguration
        {
            MediaProductId = product1.Id,
            Name = product1.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30,
            TemperatureMax = 35
        };
        db.MediaConfigurations.Add(config);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = product1.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "LOT-1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        MediaProductTestData.Link(material, product1);

        var testDef = new TestDefinition { Code = "TEST1", DisplayName = "Test 1", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDef.Id,
            StepOrder = 1,
            StepName = "Step 1",
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30,
            TemperatureMax = 35,
            StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id,
            MaterialId = material.Id,
            MediaConfigurationId = config.Id,
            TempMin = 30,
            TempMax = 35
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(
            product2.Id,
            EvaluationType.GrowthPromotion,
            24,
            48,
            30,
            35,
            null,
            null,
            null
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(config.Id, req));
        Assert.Contains("This media configuration is used by workflow steps in Test Master, so it can't be moved to a different media product.", ex.Message);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_Unreferenced_AllowsMovingToDifferentProduct()
    {
        await using var db = NewDb();
        var product1 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 1", "TSA1");
        var product2 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 2", "TSA2");

        var config = new MediaConfiguration
        {
            MediaProductId = product1.Id,
            Name = product1.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30,
            TemperatureMax = 35
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(
            product2.Id,
            EvaluationType.GrowthPromotion,
            24,
            48,
            30,
            35,
            null,
            null,
            null
        );

        var result = await controller.UpdateMediaConfiguration(config.Id, req);
        Assert.IsType<OkObjectResult>(result);

        var updated = await db.MediaConfigurations.SingleAsync(m => m.Id == config.Id);
        Assert.Equal(product2.Id, updated.MediaProductId);
        Assert.Equal(product2.Name, updated.Name);
    }
}
