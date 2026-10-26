using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MoistureRoleSpecificationTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // An item assigned the assay titration test, plus any extra tests (code, role).
    private static async Task<(MicroLimsDbContext db, Item item)> SetupAsync(params (string code, WorkflowType wf, MoistureRole role)[] extra)
    {
        var db = NewDb();
        var item = new Item { Code = "RM-1", Name = "Raw material", IsActive = true };
        db.Items.Add(item);
        db.TestDefinitions.Add(new TestDefinition { Code = "ASSAY-TIT", DisplayName = "Assay", WorkflowType = WorkflowType.Titration, IsActive = true });
        foreach (var (code, wf, role) in extra)
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, WorkflowType = wf, MoistureRole = role, IsActive = true });
        await db.SaveChangesAsync();
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "ASSAY-TIT" });
        foreach (var (code, _, _) in extra)
            db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = code });
        await db.SaveChangesAsync();
        return (db, item);
    }

    private static Specification TitrationSpec(Item item, ResultBasis basis) => new()
    {
        ItemId = item.Id,
        TestCode = "ASSAY-TIT",
        ParameterName = "Assay",
        LimitType = LimitType.Range,
        LowerLimit = 98m,
        UpperLimit = 102m,
        Unit = "%",
        ResultBasis = basis
    };

    [Fact]
    public async Task DriedBasisTitrationSpec_NeedsAssignedLodTest()
    {
        var (db, item) = await SetupAsync();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentDriedBasis)));
        Assert.Contains("Assign a loss on drying test to this item before using a dried basis.", ex.Message);
    }

    [Fact]
    public async Task AnhydrousBasisTitrationSpec_NeedsAssignedWaterTest()
    {
        // A LOD test does not satisfy the anhydrous basis.
        var (db, item) = await SetupAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _ = db;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentAnhydrousBasis)));
        Assert.Contains("Assign a water (Karl Fischer) test to this item before using an anhydrous basis.", ex.Message);
    }

    [Fact]
    public async Task DriedBasisTitrationSpec_SavesWhenLodAssigned_WithoutLodSpecRow()
    {
        var (db, item) = await SetupAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentDriedBasis));
    }

    [Fact]
    public async Task AnhydrousBasisTitrationSpec_SavesWhenWaterAssigned()
    {
        var (db, item) = await SetupAsync(("KF", WorkflowType.Titration, MoistureRole.Water));
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentAnhydrousBasis));
    }

    [Fact]
    public async Task AsIsTitrationSpec_NeedsNoMoistureTest()
    {
        var (db, item) = await SetupAsync();
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentAsIs));
    }
}
