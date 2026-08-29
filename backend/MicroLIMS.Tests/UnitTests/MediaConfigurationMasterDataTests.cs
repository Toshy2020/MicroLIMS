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

    [Fact]
    public async Task GetMediaConfigurations_ReturnsConfigurationsWithChallengesAndOrganisms()
    {
        await using var db = NewDb();
        var org = new Organism { ScientificName = "Staphylococcus aureus", AtccNumber = "6538" };
        db.Organisms.Add(org);
        await db.SaveChangesAsync();

        var config = new MediaConfiguration
        {
            Name = "Tryptic Soy Agar",
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

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
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
        var org = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        db.Organisms.Add(org);
        await db.SaveChangesAsync();

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new CreateMediaConfigurationRequest(
            "MacConkey Agar",
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
    public async Task CreateMediaConfiguration_EmptyName_Throws()
    {
        await using var db = NewDb();
        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new CreateMediaConfigurationRequest(
            "",
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
        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new CreateMediaConfigurationRequest(
            "TSA",
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
        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new CreateMediaConfigurationRequest(
            "TSA",
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
        db.MediaConfigurations.Add(new MediaConfiguration
        {
            Name = "Sabouraud Dextrose Agar",
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 48,
            IncubationMaxHours = 120,
            TemperatureMin = 20.0m,
            TemperatureMax = 25.0m
        });
        await db.SaveChangesAsync();

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new CreateMediaConfigurationRequest(
            "Sabouraud Dextrose Agar",
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
        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new CreateMediaConfigurationRequest(
            "Blood Agar",
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
        var org1 = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        var org2 = new Organism { ScientificName = "Salmonella enterica", AtccNumber = "14028" };
        db.Organisms.AddRange(org1, org2);

        var config = new MediaConfiguration
        {
            Name = "XLD Agar",
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

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new UpdateMediaConfigurationRequest(
            "Xylose Lysine Deoxycholate Agar",
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
        var config = new MediaConfiguration
        {
            Name = "Nutrient Agar",
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new UpdateMediaConfigurationRequest(
            "Nutrient Agar",
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
        Assert.Equal(80.0m, updated.RecoveryPercentMin);
        Assert.Equal(150.0m, updated.RecoveryPercentMax);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_DuplicateProfileOtherRow_Throws()
    {
        await using var db = NewDb();
        var configA = new MediaConfiguration
        {
            Name = "Cetrimide Agar",
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TemperatureMin = 35.0m,
            TemperatureMax = 37.0m
        };
        var configB = new MediaConfiguration
        {
            Name = "Cetrimide Agar",
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TemperatureMin = 35.0m,
            TemperatureMax = 37.0m
        };
        db.MediaConfigurations.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        // Attempt to update B to have configA's profile (18-24h @ 35-37C)
        var req = new UpdateMediaConfigurationRequest(
            "Cetrimide Agar",
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
        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var req = new UpdateMediaConfigurationRequest(
            "TSA",
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
        var config = new MediaConfiguration
        {
            Name = "Unused Agar", EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 30.0m, TemperatureMax = 35.0m
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var result = await controller.DeleteMediaConfiguration(config.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.False(await db.MediaConfigurations.AnyAsync(m => m.Id == config.Id));
    }

    [Fact]
    public async Task DeleteMediaConfiguration_ReferencedByStepMedia_ThrowsNamingTheStep()
    {
        await using var db = NewDb();
        var config = new MediaConfiguration
        {
            Name = "Tryptic Soy Agar", EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 1, IncubationMaxHours = 2, TemperatureMin = 30.0m, TemperatureMax = 35.0m
        };
        db.MediaConfigurations.Add(config);
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "Tryptic Soy Agar", ManufacturerName = "Himedia",
            BatchNumber = "LOT-1", ReceivingDate = DateTime.UtcNow, Location = "Micro Lab",
            QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation",
            IncubationMinHours = 1, IncubationMaxHours = 2, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id, MaterialId = material.Id, MediaConfigurationId = config.Id,
            TempMin = 30, TempMax = 35
        });
        await db.SaveChangesAsync();

        var controller = new MasterDataController(db, new EquipmentConfigurationService(db));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.DeleteMediaConfiguration(config.Id));
        Assert.Contains("CountIncubation", ex.Message);

        Assert.True(await db.MediaConfigurations.AnyAsync(m => m.Id == config.Id));
    }
}
