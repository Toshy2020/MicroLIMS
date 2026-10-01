using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// A product's Appearance test can have one specification at Bulk and
// another at Finished. Specification.ProductionStageRole carries the
// stage; a sample whose stage has rows of its own uses them, every other
// sample uses the every-stage (null) rows, and the two are never mixed.
public class SpecificationProductionStageTests
{
    private const string BulkText = "White to off-white granular powder";
    private const string FinishedText = "White, round biconvex tablets";
    private const string AllStagesText = "Conforms to description";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record Seed(Item Item, TestDefinition Test, User User, DocumentSection Section, CauseOfTesting Cause,
        Dictionary<ProductionStageRole, ProductionStage> Stages);

    private static Seed SeedProduct(MicroLimsDbContext db, SampleCategory category = SampleCategory.FinishedProduct)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();

        var user = new User { Username = "analyst", FullName = "Test Analyst", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        db.SaveChanges();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = fp.DepartmentId, SectionId = fp.Id });

        var test = new TestDefinition { Code = "APPEARANCE", DisplayName = "Appearance", SectionId = fp.Id, WorkflowType = WorkflowType.Qualitative, EquationType = EquationType.Qualitative, IsActive = true };
        var item = new Item { Code = "TAB-1", Name = "Paracetamol Tablets", Category = category, IsActive = true };
        var cause = new CauseOfTesting { Name = "Release", IsActive = true };
        db.TestDefinitions.Add(test);
        db.Items.Add(item);
        db.CausesOfTesting.Add(cause);
        var stages = new[] { ProductionStageRole.Bulk, ProductionStageRole.Finished, ProductionStageRole.Stability }
            .ToDictionary(r => r, r => new ProductionStage { Name = r.ToString(), Role = r });
        db.ProductionStages.AddRange(stages.Values);
        db.SaveChanges();

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = test.Code, DisplayName = test.DisplayName });
        db.SaveChanges();
        return new Seed(item, test, user, fp, cause, stages);
    }

    private static Specification AddSpec(MicroLimsDbContext db, Seed seed, ProductionStageRole? stage, string text)
    {
        var spec = new Specification
        {
            ItemId = seed.Item.Id, TestCode = seed.Test.Code, ParameterName = "Appearance",
            LimitType = LimitType.Qualitative, ExpectedResultText = text, SpecLimit = text, ProductionStageRole = stage
        };
        db.Specifications.Add(spec);
        db.SaveChanges();
        return spec;
    }

    private static TestOrder AddSampleOrder(MicroLimsDbContext db, Seed seed, ProductionStageRole? stage)
    {
        var sample = new Sample
        {
            ReferenceNumber = "SMP-" + Guid.NewGuid().ToString("N")[..6], Category = SampleCategory.FinishedProduct, ItemId = seed.Item.Id,
            ReceivedAt = DateTime.UtcNow, CauseOfTestingId = seed.Cause.Id, ReceivedByUserId = seed.User.Id, Status = SampleStatus.InTesting,
            ProductionStageId = stage is { } r ? seed.Stages[r].Id : null
        };
        db.Samples.Add(sample);
        db.SaveChanges();
        var order = new TestOrder { SampleId = sample.Id, TestCode = seed.Test.Code, SectionId = seed.Section.Id, CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress };
        db.TestOrders.Add(order);
        db.SaveChanges();
        return order;
    }

    private static async Task<string?> SpecTextFor(MicroLimsDbContext db, TestOrder order) =>
        (await SpecificationLookup.ForSampleAsync(db, order.SampleId, order.Sample?.ItemId ?? db.Samples.Single(s => s.Id == order.SampleId).ItemId!.Value, order.TestCode))
            .SingleOrDefault()?.ExpectedResultText;

    [Fact]
    public async Task BulkAndFinishedSamples_EachGetTheirOwnStageSpecification()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        AddSpec(db, seed, ProductionStageRole.Bulk, BulkText);
        AddSpec(db, seed, ProductionStageRole.Finished, FinishedText);

        Assert.Equal(BulkText, await SpecTextFor(db, AddSampleOrder(db, seed, ProductionStageRole.Bulk)));
        Assert.Equal(FinishedText, await SpecTextFor(db, AddSampleOrder(db, seed, ProductionStageRole.Finished)));
    }

    [Fact]
    public async Task StageWithoutItsOwnRows_FallsBackToAllStagesRow()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        AddSpec(db, seed, null, AllStagesText);
        AddSpec(db, seed, ProductionStageRole.Bulk, BulkText);

        Assert.Equal(BulkText, await SpecTextFor(db, AddSampleOrder(db, seed, ProductionStageRole.Bulk)));
        Assert.Equal(AllStagesText, await SpecTextFor(db, AddSampleOrder(db, seed, ProductionStageRole.Stability)));
        Assert.Equal(AllStagesText, await SpecTextFor(db, AddSampleOrder(db, seed, null)));
    }

    [Fact]
    public async Task StageWithNoRowsAndNoAllStagesRow_HasNoSpecification()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        AddSpec(db, seed, ProductionStageRole.Finished, FinishedText);

        Assert.Null(await SpecTextFor(db, AddSampleOrder(db, seed, ProductionStageRole.Bulk)));
    }

    [Fact]
    public async Task RecordQualitativeResult_BulkSample_JudgedAgainstBulkSpecification()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        var bulk = AddSpec(db, seed, ProductionStageRole.Bulk, BulkText);
        AddSpec(db, seed, ProductionStageRole.Finished, FinishedText);
        var order = AddSampleOrder(db, seed, ProductionStageRole.Bulk);

        var result = await TestServiceFactory.TestWorkflow(db).RecordQualitativeResultAsync(order.Id, new QualitativePayload(
            AnalysedAt: DateTime.UtcNow, EquipmentId: null,
            Parameters: new List<QualitativeParameterInput> { new(bulk.Id, Conforms: true, Observation: "White powder") },
            Password: "Password123!", Comment: null), seed.User.Id);

        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        var pr = await db.ParameterResults.SingleAsync();
        Assert.Equal(bulk.Id, pr.SpecificationId);
        Assert.Equal(BulkText, pr.SpecLimit);
    }

    [Fact]
    public async Task RecordQualitativeResult_FinishedSpecificationOnBulkSample_IsRejected()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        AddSpec(db, seed, ProductionStageRole.Bulk, BulkText);
        var finished = AddSpec(db, seed, ProductionStageRole.Finished, FinishedText);
        var order = AddSampleOrder(db, seed, ProductionStageRole.Bulk);

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.TestWorkflow(db).RecordQualitativeResultAsync(order.Id, new QualitativePayload(
            AnalysedAt: DateTime.UtcNow, EquipmentId: null,
            Parameters: new List<QualitativeParameterInput> { new(finished.Id, Conforms: true, Observation: "White tablets") },
            Password: "Password123!", Comment: null), seed.User.Id));
    }

    [Fact]
    public async Task SampleSummary_ShowsTheSampleStagesSpecificationText()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        AddSpec(db, seed, ProductionStageRole.Bulk, BulkText);
        AddSpec(db, seed, ProductionStageRole.Finished, FinishedText);
        var order = AddSampleOrder(db, seed, ProductionStageRole.Finished);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(order.SampleId);

        Assert.Equal(FinishedText, summary!.TestOrders.Single().SpecificationText);
    }

    [Fact]
    public async Task Validate_SameParameterAtDifferentStages_IsAllowed_SameStageTwice_IsRejected()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db);
        var service = TestServiceFactory.Specification(db);
        Specification New(ProductionStageRole? stage) => new()
        {
            ItemId = seed.Item.Id, TestCode = seed.Test.Code, ParameterName = "Appearance",
            LimitType = LimitType.Qualitative, ExpectedResultText = "x", ProductionStageRole = stage
        };

        await service.CreateAsync(New(ProductionStageRole.Bulk));
        await service.CreateAsync(New(ProductionStageRole.Finished));
        await service.CreateAsync(New(null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(New(ProductionStageRole.Bulk)));
        Assert.Contains("Bulk", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(New(null)));
    }

    [Fact]
    public async Task Validate_StageSpecificationOnNonFinishedProductItem_IsRejected()
    {
        await using var db = NewDb();
        var seed = SeedProduct(db, SampleCategory.RawMaterial);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.Specification(db).CreateAsync(new Specification
        {
            ItemId = seed.Item.Id, TestCode = seed.Test.Code, ParameterName = "Appearance",
            LimitType = LimitType.Qualitative, ExpectedResultText = "x", ProductionStageRole = ProductionStageRole.Bulk
        }));
        Assert.Contains("Finished Product", ex.Message);
    }

    [Fact]
    public void SelectForStage_NeverMixesStageRowsWithAllStagesRows()
    {
        var all = new Specification { Id = 1, ProductionStageRole = null };
        var bulk = new Specification { Id = 2, ProductionStageRole = ProductionStageRole.Bulk };

        Assert.Equal(new[] { 2 }, SpecificationLookup.SelectForStage(new[] { all, bulk }, ProductionStageRole.Bulk).Select(s => s.Id));
        Assert.Equal(new[] { 1 }, SpecificationLookup.SelectForStage(new[] { all, bulk }, ProductionStageRole.Finished).Select(s => s.Id));
        Assert.Equal(new[] { 1 }, SpecificationLookup.SelectForStage(new[] { all, bulk }, null).Select(s => s.Id));
    }
}
