using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MediaProductServiceTests
{
    private const string Password = "Correct-Password-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<User> SeedUserAsync(MicroLimsDbContext db)
    {
        var role = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User
        {
            FullName = "Test SectionHead",
            Username = "sectionhead",
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Theory]
    [InlineData("A")]
    [InlineData("TOOLONGCODE1")]
    [InlineData("TS A")]
    [InlineData("TS/A")]
    public async Task CreateAsync_InvalidCodeFormat_Throws(string invalidCode)
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync("Test Product", invalidCode));
        Assert.Contains("code must be 2-10 characters", ex.Message);
    }

    [Theory]
    [InlineData("TSA")]
    [InlineData("Ps.")]
    [InlineData("R2A-2")]
    public async Task CreateAsync_ValidCodeFormat_Succeeds(string validCode)
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);

        var product = await service.CreateAsync($"Product {validCode}", validCode);

        Assert.NotNull(product);
        Assert.Equal(validCode, product.Code);
        Assert.True(product.Id > 0);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_RejectedCaseInsensitively()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        await service.CreateAsync("Tryptic Soy Agar", "TSA");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync("tryptic soy agar", "TSA2"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_RejectedCaseInsensitively()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        await service.CreateAsync("Tryptic Soy Agar", "TSA");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync("Another TSA", "tsa"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task RenameAsync_UpdatesNameOnChildConfigurations_LeavesBatchSnapshotsIntact()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("Original Name", "ORIG");

        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product)
        };
        db.MediaConfigurations.Add(config);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = product.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-1",
            ReceivingDate = DateTime.UtcNow,
            Code = product.Code,
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var updated = await service.RenameAsync(product.Id, "Renamed Product");

        Assert.Equal("Renamed Product", updated.Name);

        var reloadedConfig = await db.MediaConfigurations.SingleAsync(c => c.Id == config.Id);
        Assert.Equal("Renamed Product", reloadedConfig.Name);

        var reloadedMaterial = await db.Materials.SingleAsync(m => m.Id == material.Id);
        Assert.Equal("Original Name", reloadedMaterial.MaterialName);
    }

    [Fact]
    public async Task DeleteAsync_BlockedWhenReferencedByConfiguration()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product)
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(product.Id));
        Assert.Contains("referenced by 1 media configuration(s)", ex.Message);
        Assert.True(await db.MediaProducts.AnyAsync(p => p.Id == product.Id));
    }

    [Fact]
    public async Task DeleteAsync_BlockedWhenReferencedByBatch()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        db.Materials.Add(new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = product.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(product.Id));
        Assert.Contains("referenced by 0 media configuration(s) and 1 material batch(es)", ex.Message);
        Assert.True(await db.MediaProducts.AnyAsync(p => p.Id == product.Id));
    }

    [Fact]
    public async Task DeleteAsync_Unreferenced_Succeeds()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        await service.DeleteAsync(product.Id);

        Assert.False(await db.MediaProducts.AnyAsync(p => p.Id == product.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesUnusedIncubationConditions()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");
        await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30, 35);
        await MediaProductTestData.AddConditionAsync(db, product, 48, 72, 20, 25);

        await service.DeleteAsync(product.Id);

        Assert.False(await db.MediaProducts.AnyAsync(p => p.Id == product.Id));
        Assert.False(await db.MediaIncubationConditions.AnyAsync(c => c.MediaProductId == product.Id));
    }

    [Fact]
    public async Task DeleteAsync_BlockedWhenConditionUsedByStepMedia()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product);

        // The step medium's batch isn't linked to the product, so only the
        // condition ties the step medium to it.
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = "Legacy TSA",
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id,
            StepOrder = 1,
            StepName = "CountIncubation",
            IsFinalStep = true,
            StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id,
            MaterialId = material.Id,
            MediaIncubationConditionId = condition.Id,
            TempMin = 30,
            TempMax = 35,
            IncubationMinHours = 24,
            IncubationMaxHours = 48
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(product.Id));
        Assert.Contains("used by 1 Test Master step medium/media", ex.Message);
        Assert.True(await db.MediaProducts.AnyAsync(p => p.Id == product.Id));
        Assert.True(await db.MediaIncubationConditions.AnyAsync(c => c.Id == condition.Id));
    }

    [Fact]
    public async Task ChangeCodeAsync_MissingReason_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeCodeAsync(product.Id, "TSAX", "   ", Password, user.Id, "127.0.0.1"));
        Assert.Contains("reason for the code change is required", ex.Message);
    }

    [Fact]
    public async Task ChangeCodeAsync_SameCode_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeCodeAsync(product.Id, "TSA", "Reason for change", Password, user.Id, "127.0.0.1"));
        Assert.Contains("new code must be different", ex.Message);
    }

    [Fact]
    public async Task ChangeCodeAsync_ClashingCode_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = TestServiceFactory.MediaProduct(db);
        var productA = await service.CreateAsync("TSA", "TSA");
        await service.CreateAsync("R2A", "R2A");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeCodeAsync(productA.Id, "r2a", "Reason for change", Password, user.Id, "127.0.0.1"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task ChangeCodeAsync_WrongPassword_ThrowsAndLeavesCodeUnchanged()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.ChangeCodeAsync(product.Id, "TSAX", "Updating abbreviation", "WrongPassword!", user.Id, "127.0.0.1"));

        var reloaded = await db.MediaProducts.SingleAsync(p => p.Id == product.Id);
        Assert.Equal("TSA", reloaded.Code);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
        Assert.Empty(await db.AuditLogs.Where(a => a.ActionCode == MediaProductService.CodeChangedActionCode).ToListAsync());
    }

    [Fact]
    public async Task ChangeCodeAsync_CorrectPassword_UpdatesCodeWritesSignatureAndAuditEvent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = TestServiceFactory.MediaProduct(db);
        var product = await service.CreateAsync("TSA", "TSA");

        var updated = await service.ChangeCodeAsync(
            product.Id, "TSAX", "Harmonising with pharmacopoeia prefix", Password, user.Id, "10.0.0.1");

        Assert.Equal("TSAX", updated.Code);

        var reloaded = await db.MediaProducts.SingleAsync(p => p.Id == product.Id);
        Assert.Equal("TSAX", reloaded.Code);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.MediaProduct, signature.EntityType);
        Assert.Equal(product.Id, signature.EntityId);
        Assert.Equal(user.Id, signature.UserId);
        Assert.Equal("10.0.0.1", signature.IpAddress);
        Assert.Contains("TSA -> TSAX", signature.Comment);
        Assert.Contains("Harmonising with pharmacopoeia prefix", signature.Comment);

        var audit = Assert.Single(await db.AuditLogs.Include(a => a.Changes)
            .Where(a => a.ActionCode == MediaProductService.CodeChangedActionCode && a.EntityId == product.Id.ToString())
            .ToListAsync());

        Assert.Equal(ReviewEntityTypes.MediaProduct, audit.EntityName);
        Assert.Equal(AuditActionCategory.Configuration, audit.ActionCategory);
        Assert.Equal("Harmonising with pharmacopoeia prefix", audit.Reason);
        var change = Assert.Single(audit.Changes);
        Assert.Equal("Code", change.FieldName);
        Assert.Equal("TSA", change.PreviousValue);
        Assert.Equal("TSAX", change.NewValue);
    }
}
