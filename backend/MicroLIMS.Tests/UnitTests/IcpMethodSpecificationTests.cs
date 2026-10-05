using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class IcpMethodSpecificationTests
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

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<IcpMethod> AddMethodAsync(MicroLimsDbContext db, int userId, IcpMethodMode mode, string abbr)
    {
        var std = new MaterialMasterEntry
        {
            SectionId = TestServiceFactory.EnsureMicroSection(db).Id, Code = "RS-" + Guid.NewGuid().ToString("N")[..5], Name = "ICP standard",
            Category = MaterialMasterCategory.ReferenceStandard, BaseUnit = MaterialUnit.Gram, IsActive = true,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(std);
        await db.SaveChangesAsync();
        var m = await TestServiceFactory.IcpMethod(db).CreateAsync(new SaveIcpMethodRequest(
            Name: "ICP " + abbr, Abbreviation: abbr, EffectiveDate: new DateTime(2026, 10, 1), Mode: mode,
            StandardLevelsMgPerL: "0.1, 0.5, 1", CalibrationStandardEntryId: std.Id, MinCorrelation: 0.999m, SampleVolumeMl: 50m,
            Elements: new() { new IcpElementInput(null, "zn", 213.857m, AnalyteView.Axial) }), userId);
        return await db.IcpMethods.Include(x => x.Elements).FirstAsync(x => x.Id == m.Id);
    }

    private record Arr(SpecificationMasterDataService Svc, Item Item, string Code, int ElementId, int UserId);

    private static async Task<Arr> ArrangeAsync(MicroLimsDbContext db, IcpMethodMode mode)
    {
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, userId, mode, "icp-a");
        var def = await new TestDefinitionMasterDataService(db, new UserSectionScopeService(db))
            .CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(
                Code: "ICP-T1", DisplayName: "ICP-T1", SectionId: section.Id,
                WorkflowType: WorkflowType.IcpMethodAssay, EquationType: EquationType.IcpMethodAssay,
                RequiresSystemSuitability: false, MethodAbbreviation: "ICP-T1", IcpMethodId: method.Id));
        return new Arr(new SpecificationMasterDataService(db, new UserSectionScopeService(db)), await AddItemAsync(db, def.Code),
            def.Code, method.Elements.Single().Id, userId);
    }

    private static async Task<Item> AddItemAsync(MicroLimsDbContext db, string code)
    {
        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = code, DisplayName = code } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static CreateSpecificationRequest Spec(Arr a, ResultBasis basis, string name = "Zn", decimal? claim = 10m, string? unit = "mg",
        int? elementId = -1, decimal? cf = null, LimitType limit = LimitType.Range) =>
        new(ItemId: a.Item.Id, TestCode: a.Code, ParameterName: name, LimitType: limit,
            LowerLimit: limit == LimitType.Range ? 90m : null, UpperLimit: 110m, ResultBasis: basis,
            LabelClaim: claim, LabelClaimUnit: unit, ConversionFactor: cf,
            IcpMethodElementId: elementId == -1 ? a.ElementId : elementId);

    private static async Task AssertThrowsAsync(string message, Func<Task> act) =>
        Assert.Equal(message, (await Assert.ThrowsAsync<InvalidOperationException>(act)).Message);

    [Fact]
    public async Task Spec_MineralAssay_MgPerUnitAndPercent_Accepted()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, "Zn mg"));
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.PercentLabelClaim, "Zn %"));
        Assert.Equal(2, await db.Specifications.CountAsync(s => s.IcpMethodElementId == a.ElementId));
    }

    [Fact]
    public async Task Spec_MineralAssay_RejectsMgPerKg()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        await AssertThrowsAsync("Result basis must be mg per unit or % of label claim for mineral assay.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerKg)));
    }

    [Fact]
    public async Task Spec_MineralAssay_RequiresLabelClaim()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        await AssertThrowsAsync("Mineral assay specifications need a label claim and its unit.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.PercentLabelClaim, claim: null, unit: null)));
    }

    [Fact]
    public async Task Spec_Impurities_AcceptsMgPerKg()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.ElementalImpurities);
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerKg, claim: null, unit: null, limit: LimitType.NotMoreThan));
        Assert.Equal(1, await db.Specifications.CountAsync(s => s.IcpMethodElementId == a.ElementId));
    }

    [Fact]
    public async Task Spec_Impurities_RejectsMgPerUnit()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.ElementalImpurities);
        await AssertThrowsAsync("Result basis must be µg/g (MgPerKg) for elemental impurities.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, claim: null, unit: null)));
    }

    [Fact]
    public async Task Spec_Impurities_RejectsLabelClaim()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.ElementalImpurities);
        await AssertThrowsAsync("Label claim is not used for elemental impurities.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerKg)));
    }

    [Fact]
    public async Task Spec_Impurities_RejectsRange()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.ElementalImpurities);
        await AssertThrowsAsync("Elemental impurity specifications use a not-more-than limit.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerKg, claim: null, unit: null)));
    }

    [Fact]
    public async Task Spec_ElementFromOtherMethod_Throws()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        var other = await AddMethodAsync(db, a.UserId, IcpMethodMode.MineralAssay, "icp-b");
        await AssertThrowsAsync("That element does not belong to the method of test 'ICP-T1'.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, elementId: other.Elements.Single().Id)));
    }

    [Fact]
    public async Task Spec_MissingElement_Throws()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        await AssertThrowsAsync("Method element is required for ICP method assay specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, elementId: null)));
    }

    [Fact]
    public async Task Spec_DuplicateElementBasis_Throws()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, "Zn a"));
        await AssertThrowsAsync("A specification for this element and basis already exists.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, "Zn b")));
    }

    [Fact]
    public async Task Spec_ConversionFactorNotOne_Throws()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        await AssertThrowsAsync("Sample matrix and conversion factor are not used for ICP method assay specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, ResultBasis.MgPerUnit, cf: 2m)));
    }

    [Fact]
    public async Task Spec_UnsupportedLimitType_Throws()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        var req = new CreateSpecificationRequest(ItemId: a.Item.Id, TestCode: a.Code, ParameterName: "Zn", LimitType: LimitType.Qualitative,
            ExpectedResultText: "x", ResultBasis: ResultBasis.MgPerUnit, LabelClaim: 10m, LabelClaimUnit: "mg", IcpMethodElementId: a.ElementId);
        await AssertThrowsAsync("Limit type 'Qualitative' is not supported for ICP method assay specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, req));
    }

    [Fact]
    public async Task Spec_NonIcpTest_WithIcpElement_Throws()
    {
        await using var db = NewDb();
        var a = await ArrangeAsync(db, IcpMethodMode.MineralAssay);
        var section = TestServiceFactory.EnsureMicroSection(db);
        var plain = new TestDefinition { Code = "PLAIN-T1", DisplayName = "Plain", SectionId = section.Id, WorkflowType = WorkflowType.CountTest, IsActive = true };
        db.TestDefinitions.Add(plain);
        await db.SaveChangesAsync();
        var item = await AddItemAsync(db, plain.Code);
        var req = new CreateSpecificationRequest(ItemId: item.Id, TestCode: plain.Code, ParameterName: "X", LimitType: LimitType.Range,
            LowerLimit: 1m, UpperLimit: 2m, IcpMethodElementId: a.ElementId);
        await AssertThrowsAsync("Method element is only allowed for ICP method assay specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, req));
    }
}
