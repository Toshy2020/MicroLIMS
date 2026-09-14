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
        new(db, new EquipmentConfigurationService(db), TestServiceFactory.MediaProduct(db), TestServiceFactory.MediaIncubationCondition(db));

    private static async Task<MediaConfiguration> AddConfigurationAsync(
        MicroLimsDbContext db, MediaProduct product, MediaIncubationCondition condition,
        EvaluationType evaluationType = EvaluationType.GrowthPromotion,
        List<MediaConfigurationChallenge>? challenges = null)
    {
        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = evaluationType,
            MediaIncubationConditionId = condition.Id,
            Challenges = challenges ?? new List<MediaConfigurationChallenge>()
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    [Fact]
    public async Task GetMediaConfigurations_ReturnsConfigurationsWithChallengesAndOrganisms()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30.0m, 35.0m);
        var org = new Organism { ScientificName = "Staphylococcus aureus", AtccNumber = "6538" };
        db.Organisms.Add(org);
        await db.SaveChangesAsync();

        var config = await AddConfigurationAsync(db, product, condition,
            challenges: new List<MediaConfigurationChallenge> { new() { OrganismId = org.Id } });
        config.RecoveryPercentMin = 70.0m;
        config.RecoveryPercentMax = 200.0m;
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
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35.0m, 37.0m);
        var org = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        db.Organisms.Add(org);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.IndicationInhibition,
            condition.Id,
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
        Assert.Equal(condition.Id, saved.MediaIncubationConditionId);
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
        var req = new CreateMediaConfigurationRequest(9999, EvaluationType.GrowthPromotion, 1, null, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
        Assert.Contains("Media product with ID 9999 not found", ex.Message);
    }

    [Fact]
    public async Task CreateMediaConfiguration_MissingCondition_ThrowsNotFound()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(product.Id, EvaluationType.GrowthPromotion, 9999, null, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
        Assert.Equal("Incubation condition 9999 not found.", ex.Message);
        Assert.False(await db.MediaConfigurations.AnyAsync());
    }

    [Fact]
    public async Task CreateMediaConfiguration_ConditionOfAnotherProduct_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var otherProduct = await MediaProductTestData.CreateOrGetAsync(db, "R2A Agar", "R2A");
        var otherCondition = await MediaProductTestData.AddConditionAsync(db, otherProduct);
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(product.Id, EvaluationType.GrowthPromotion, otherCondition.Id, null, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
        Assert.Contains("belongs to a different media product", ex.Message);
        Assert.False(await db.MediaConfigurations.AnyAsync());
    }

    [Fact]
    public async Task CreateMediaConfiguration_SecondConfigurationForSameProduct_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Sabouraud Dextrose Agar", "SDA");
        var existingCondition = await MediaProductTestData.AddConditionAsync(db, product, 48, 120, 20.0m, 25.0m);
        var otherCondition = await MediaProductTestData.AddConditionAsync(db, product, 72, 120, 30.0m, 35.0m);
        await AddConfigurationAsync(db, product, existingCondition);

        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(product.Id, EvaluationType.GrowthPromotion, otherCondition.Id, null, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateMediaConfiguration(req));
        Assert.Contains("already has an evaluation configuration", ex.Message);
        Assert.Single(await db.MediaConfigurations.ToListAsync());
    }

    [Fact]
    public async Task CreateMediaConfiguration_NonExistentOrganism_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Blood Agar", "BA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 35.0m, 37.0m);
        var controller = CreateController(db);
        var req = new CreateMediaConfigurationRequest(
            product.Id,
            EvaluationType.GrowthPromotion,
            condition.Id,
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
    public async Task UpdateMediaConfiguration_ValidPayload_UpdatesConditionAndChildChallenges()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "XLD Agar", "XLD");
        var conditionA = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35.0m, 37.0m);
        var conditionB = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 35.0m, 37.0m);
        var org1 = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        var org2 = new Organism { ScientificName = "Salmonella enterica", AtccNumber = "14028" };
        db.Organisms.AddRange(org1, org2);
        await db.SaveChangesAsync();

        var config = await AddConfigurationAsync(db, product, conditionA, EvaluationType.IndicationInhibition,
            new List<MediaConfigurationChallenge>
            {
                new() { OrganismId = org1.Id, ChallengeRole = ChallengeRole.Inhibition }
            });

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(
            product.Id,
            EvaluationType.IndicationInhibition,
            conditionB.Id,
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

        Assert.Equal(product.Id, updated.MediaProductId);
        Assert.Equal(product.Name, updated.Name);
        Assert.Equal(conditionB.Id, updated.MediaIncubationConditionId);
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
    public async Task UpdateMediaConfiguration_ChangedProduct_Throws()
    {
        await using var db = NewDb();
        var product1 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 1", "TSA1");
        var product2 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 2", "TSA2");
        var condition1 = await MediaProductTestData.AddConditionAsync(db, product1);
        var condition2 = await MediaProductTestData.AddConditionAsync(db, product2);
        var config = await AddConfigurationAsync(db, product1, condition1);

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(product2.Id, EvaluationType.GrowthPromotion, condition2.Id, null, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(config.Id, req));
        Assert.Contains("can't be moved to another media product", ex.Message);

        var reloaded = await db.MediaConfigurations.AsNoTracking().SingleAsync(m => m.Id == config.Id);
        Assert.Equal(product1.Id, reloaded.MediaProductId);
        Assert.Equal(condition1.Id, reloaded.MediaIncubationConditionId);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_MissingCondition_ThrowsNotFound()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Nutrient Agar", "NA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product);
        var config = await AddConfigurationAsync(db, product, condition);

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(product.Id, EvaluationType.GrowthPromotion, 9999, 80.0m, 150.0m, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(config.Id, req));
        Assert.Equal("Incubation condition 9999 not found.", ex.Message);

        var reloaded = await db.MediaConfigurations.AsNoTracking().SingleAsync(m => m.Id == config.Id);
        Assert.Equal(condition.Id, reloaded.MediaIncubationConditionId);
        Assert.Null(reloaded.RecoveryPercentMin);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_ConditionOfAnotherProduct_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Cetrimide Agar", "CA");
        var otherProduct = await MediaProductTestData.CreateOrGetAsync(db, "R2A Agar", "R2A");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35.0m, 37.0m);
        var otherCondition = await MediaProductTestData.AddConditionAsync(db, otherProduct, 18, 24, 35.0m, 37.0m);
        var config = await AddConfigurationAsync(db, product, condition);

        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(product.Id, EvaluationType.GrowthPromotion, otherCondition.Id, null, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(config.Id, req));
        Assert.Contains("belongs to a different media product", ex.Message);

        var reloaded = await db.MediaConfigurations.AsNoTracking().SingleAsync(m => m.Id == config.Id);
        Assert.Equal(condition.Id, reloaded.MediaIncubationConditionId);
    }

    [Fact]
    public async Task UpdateMediaConfiguration_NonExistentId_Throws()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product);
        var controller = CreateController(db);
        var req = new UpdateMediaConfigurationRequest(product.Id, EvaluationType.GrowthPromotion, condition.Id, null, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateMediaConfiguration(9999, req));
    }

    [Fact]
    public async Task DeleteMediaConfiguration_NotReferenced_SucceedsAndKeepsCondition()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Unused Agar", "UA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product);
        var config = await AddConfigurationAsync(db, product, condition);

        var controller = CreateController(db);
        var result = await controller.DeleteMediaConfiguration(config.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.False(await db.MediaConfigurations.AnyAsync(m => m.Id == config.Id));
        Assert.True(await db.MediaIncubationConditions.AnyAsync(c => c.Id == condition.Id));
    }
}
