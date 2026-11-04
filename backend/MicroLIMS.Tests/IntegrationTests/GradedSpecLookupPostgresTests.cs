using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed: the (Item, Test, Parameter, Stage, Grade) unique index and
// the Grade column only exist in the real schema.
[Collection("PostgresDatabaseCollection")]
public class GradedSpecLookupPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public GradedSpecLookupPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static Specification Spec(int itemId, string testCode, string? grade, decimal upper) => new()
    {
        ItemId = itemId, TestCode = testCode, ParameterName = "Result", Grade = grade,
        LimitType = LimitType.NotMoreThan, UpperLimit = upper, Unit = "%", SpecLimit = $"NMT {upper}",
    };

    [PostgresFact]
    public async Task PrimaryAsync_ChoosesTheSamplesGradeThenFallsBackToUngraded()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string assay = $"AS{uid}", tamc = $"TC{uid}", chloride = $"CL{uid}";

        await using var db = _fixture.CreateDbContext();
        foreach (var code in new[] { assay, tamc, chloride })
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = _fixture.SeededSectionId, IsActive = true });
        var item = new Item { Code = $"RM-{uid}", Name = $"Graded {uid}", Category = SampleCategory.RawMaterial, IsActive = true };
        foreach (var code in new[] { assay, tamc, chloride })
            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = code });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.Specifications.AddRange(
            Spec(item.Id, assay, "BP", 1m), Spec(item.Id, assay, "USP", 2m),
            Spec(item.Id, tamc, null, 3m), Spec(item.Id, chloride, "USP", 4m));
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;
        var sample = new Sample
        {
            ReferenceNumber = $"RM-G-{uid}", Category = SampleCategory.RawMaterial, ItemId = item.Id, Grade = "BP",
            ReceivedAt = DateTime.UtcNow, CauseOfTesting = cause, ReceivedByUserId = _fixture.SeededUserId,
            Status = SampleStatus.Received,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        await using var read = _fixture.CreateDbContext();
        Assert.Equal("BP", (await SpecificationLookup.PrimaryAsync(read, sample.Id, item.Id, assay))!.Grade);
        Assert.Equal(1m, (await SpecificationLookup.PrimaryAsync(read, sample.Id, item.Id, assay))!.UpperLimit);
        Assert.Null((await SpecificationLookup.PrimaryAsync(read, sample.Id, item.Id, tamc))!.Grade);
        // No BP row for CHLORIDE and no ungraded one either: nothing applies.
        Assert.Null(await SpecificationLookup.PrimaryAsync(read, sample.Id, item.Id, chloride));
        Assert.Equal("BP", await SpecificationLookup.SampleGradeAsync(read, sample.Id));
        Assert.Single(await SpecificationLookup.ForSampleAsync(read, sample.Id, item.Id, assay));
    }

    [PostgresFact]
    public async Task InHouseGrade_SavedWithDifferentCase_IsOneGradeForEveryTest()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string assay = $"AS{uid}", lod = $"LD{uid}";

        await using var db = _fixture.CreateDbContext();
        foreach (var code in new[] { assay, lod })
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = _fixture.SeededSectionId, IsActive = true });
        var item = new Item { Code = $"RM-{uid}", Name = $"Case {uid}", Category = SampleCategory.RawMaterial, IsActive = true };
        foreach (var code in new[] { assay, lod })
            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = code });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var service = new SpecificationService(db);
        var first = Spec(item.Id, assay, "In-house", 1m);
        await service.ValidateAsync(first);
        db.Specifications.Add(first);
        await db.SaveChangesAsync();
        var second = Spec(item.Id, lod, "in-house", 2m);
        await service.ValidateAsync(second);
        db.Specifications.Add(second);
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;
        var sample = new Sample
        {
            ReferenceNumber = $"RM-C-{uid}", Category = SampleCategory.RawMaterial, ItemId = item.Id, Grade = "In-house",
            ReceivedAt = DateTime.UtcNow, CauseOfTesting = cause, ReceivedByUserId = _fixture.SeededUserId,
            Status = SampleStatus.Received,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        Assert.Equal("In-house", second.Grade);
        await using var read = _fixture.CreateDbContext();
        Assert.Equal(1m, (await SpecificationLookup.PrimaryAsync(read, sample.Id, item.Id, assay))!.UpperLimit);
        Assert.Equal(2m, (await SpecificationLookup.PrimaryAsync(read, sample.Id, item.Id, lod))!.UpperLimit);
        Assert.Equal(new[] { "In-house" }, await new ItemService(read).GetGradesAsync(item.Id));
    }
}
