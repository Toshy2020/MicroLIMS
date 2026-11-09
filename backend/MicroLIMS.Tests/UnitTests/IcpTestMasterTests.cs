using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// ICP tests define their own elements, target ppm and sample basis in the Test Master; item specifications
// are keyed by those elements.
public class IcpTestMasterTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<(DocumentSection Section, int UserId)> SeedAsync(MicroLimsDbContext db)
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

    private static CreateTestDefinitionRequest Create(int sectionId, IcpTestMode? mode, IcpSampleForm? form = null,
        decimal? amount = null, decimal? gravity = null, List<IcpTestElementInput>? elements = null, bool sst = false) =>
        new(Code: "ICP-T1", DisplayName: "ICP Test", SectionId: sectionId,
            WorkflowType: WorkflowType.IcpMethodAssay, EquationType: EquationType.IcpMethodAssay,
            RequiresSystemSuitability: sst, MethodAbbreviation: "ICP-T1")
        {
            IcpMode = mode, IcpSampleForm = form, IcpTheoreticalAmount = amount, IcpSpecificGravity = gravity, IcpElements = elements,
        };

    private static List<IcpTestElementInput> Assay(params (string Symbol, decimal? Target)[] e) =>
        e.Select(x => new IcpTestElementInput(null, x.Symbol, x.Target)).ToList();

    private static async Task AssertThrowsAsync(string message, Func<Task> act) =>
        Assert.Equal(message, (await Assert.ThrowsAsync<InvalidOperationException>(act)).Message);

    [Fact]
    public async Task Create_Assay_StoresElementsTargetsAndBasis()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.TestDefinitionMaster(db);

        var created = await service.CreateTestDefinitionAsync(userId,
            Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("zn", 10m), ("CA", 20m))));

        Assert.Equal(IcpTestMode.Assay, created.IcpMode);
        Assert.Equal(IcpSampleForm.Solid, created.IcpSampleForm);
        Assert.Equal(0.5m, created.IcpTheoreticalAmount);
        Assert.Null(created.IcpSpecificGravity);
        Assert.Equal(new[] { "Zn", "Ca" }, created.IcpElements.Select(e => e.Symbol).ToArray());
        Assert.Equal(new decimal?[] { 10m, 20m }, created.IcpElements.Select(e => e.TargetPpm).ToArray());
        Assert.Equal(2, await db.TestDefinitionIcpElements.CountAsync());
    }

    [Fact]
    public async Task Create_AssayLiquid_NeedsSpecificGravity()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.TestDefinitionMaster(db);

        await AssertThrowsAsync("The specific gravity must be greater than zero for a liquid sample.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Liquid, 10m, null, Assay(("Zn", 10m)))));

        var ok = await service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Liquid, 10m, 1.2m, Assay(("Zn", 10m))));
        Assert.Equal(1.2m, ok.IcpSpecificGravity);
    }

    [Fact]
    public async Task Create_SolidAssay_DropsSpecificGravity()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var created = await TestServiceFactory.TestDefinitionMaster(db).CreateTestDefinitionAsync(userId,
            Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, 1.2m, Assay(("Zn", 10m))));
        Assert.Null(created.IcpSpecificGravity);
    }

    [Fact]
    public async Task Create_Assay_Validation()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.TestDefinitionMaster(db);

        await AssertThrowsAsync("The ICP test type (assay or impurity) is required.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, null, elements: Assay(("Zn", 10m)))));
        await AssertThrowsAsync("Choose whether the sample is a solid or a liquid.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, null, 0.5m, null, Assay(("Zn", 10m)))));
        await AssertThrowsAsync("The theoretical weight (g) must be greater than zero.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, null, null, Assay(("Zn", 10m)))));
        await AssertThrowsAsync("At least one element is required.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay())));
        await AssertThrowsAsync("Zn: the target concentration (ppm) must be greater than zero.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn", null)))));
        await AssertThrowsAsync("Element \"Zn\" is listed more than once.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn", 10m), ("zn", 5m)))));
        await AssertThrowsAsync("Element symbol must be 1-3 letters.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn2", 10m)))));
        await AssertThrowsAsync("ICP method assay tests use the run calibration, not system suitability.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn", 10m)), sst: true)));
    }

    [Fact]
    public async Task Create_Impurity_HasNoTargetOrSampleBasis()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.TestDefinitionMaster(db);

        await AssertThrowsAsync("Pb: impurity tests are judged against the item specification; no target is used.",
            () => service.CreateTestDefinitionAsync(userId, Create(section.Id, IcpTestMode.Impurity, elements: Assay(("Pb", 5m)))));

        var created = await service.CreateTestDefinitionAsync(userId,
            Create(section.Id, IcpTestMode.Impurity, IcpSampleForm.Solid, 0.5m, 1m, Assay(("Pb", null), ("As", null))));

        Assert.Equal(IcpTestMode.Impurity, created.IcpMode);
        Assert.Null(created.IcpSampleForm);
        Assert.Null(created.IcpTheoreticalAmount);
        Assert.Null(created.IcpSpecificGravity);
        Assert.Equal(2, created.IcpElements.Count);
    }

    [Fact]
    public async Task NonIcpTest_RejectsIcpSettings_AndClearsStaleOnes()
    {
        await using var db = NewDb();
        var entity = new TestDefinition { Code = "UV-1", DisplayName = "UV", SectionId = 1, IcpMode = IcpTestMode.Assay, IcpTheoreticalAmount = 1m };

        await AssertThrowsAsync("ICP settings are only allowed for ICP method assay tests.",
            () => IcpTestDefinitionRules.ApplyAsync(db, entity, false, IcpTestMode.Assay, null, null, null, null));

        await IcpTestDefinitionRules.ApplyAsync(db, entity, false, null, null, null, null, null);
        Assert.Null(entity.IcpMode);
        Assert.Null(entity.IcpTheoreticalAmount);
    }

    [Fact]
    public async Task Update_KeepsElementIds_AddsAndRemoves()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.TestDefinitionMaster(db);
        var created = await service.CreateTestDefinitionAsync(userId,
            Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn", 10m), ("Ca", 20m))));
        var zn = created.IcpElements.Single(e => e.Symbol == "Zn");

        var updated = await service.UpdateTestDefinitionAsync(userId, created.Id, new UpdateTestDefinitionRequest(created.Code, created.DisplayName)
        {
            IcpElements = new() { new IcpTestElementInput(zn.Id, "Zn", 12.5m), new IcpTestElementInput(null, "Mg", 8m) },
        });

        Assert.Equal(new[] { "Zn", "Mg" }, updated.IcpElements.Select(e => e.Symbol).ToArray());
        Assert.Equal(zn.Id, updated.IcpElements.First().Id);
        Assert.Equal(12.5m, updated.IcpElements.First().TargetPpm);
        Assert.Equal(2, await db.TestDefinitionIcpElements.CountAsync());
    }

    [Fact]
    public async Task Update_WithoutIcpFields_LeavesTheConfigurationAlone()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.TestDefinitionMaster(db);
        var created = await service.CreateTestDefinitionAsync(userId,
            Create(section.Id, IcpTestMode.Assay, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn", 10m))));

        var updated = await service.UpdateTestDefinitionAsync(userId, created.Id, new UpdateTestDefinitionRequest(created.Code, "Renamed"));

        Assert.Equal("Renamed", updated.DisplayName);
        Assert.Equal(IcpTestMode.Assay, updated.IcpMode);
        Assert.Single(updated.IcpElements);
        Assert.Equal(10m, updated.IcpElements.Single().TargetPpm);
    }

    // ---- item specifications ----

    private record Arr(SpecificationMasterDataService Svc, Item Item, string Code, int ZnId, int UserId);

    private static async Task<Arr> ArrangeSpecAsync(MicroLimsDbContext db, IcpTestMode mode)
    {
        var (section, userId) = await SeedAsync(db);
        var def = await TestServiceFactory.TestDefinitionMaster(db).CreateTestDefinitionAsync(userId, mode == IcpTestMode.Assay
            ? Create(section.Id, mode, IcpSampleForm.Solid, 0.5m, null, Assay(("Zn", 10m)))
            : Create(section.Id, mode, elements: Assay(("Zn", null))));
        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = def.Code, DisplayName = def.Code } },
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return new Arr(new SpecificationMasterDataService(db, new UserSectionScopeService(db)), item, def.Code, def.IcpElements.Single().Id, userId);
    }

    private static CreateSpecificationRequest Spec(Arr a, LimitType limit = LimitType.Range, int? elementId = -1,
        ResultBasis? basis = null, decimal? claim = null, string name = "Zn") =>
        new(ItemId: a.Item.Id, TestCode: a.Code, ParameterName: name, LimitType: limit,
            LowerLimit: limit == LimitType.Range ? 90m : null, UpperLimit: limit == LimitType.Range ? 110m : 5m,
            ResultBasis: basis, LabelClaim: claim, LabelClaimUnit: claim.HasValue ? "mg" : null,
            IcpTestElementId: elementId == -1 ? a.ZnId : elementId);

    [Fact]
    public async Task Spec_Assay_RangeOnTheTestsElement_Accepted()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Assay);
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a));
        Assert.Equal(1, await db.Specifications.CountAsync(s => s.IcpTestElementId == a.ZnId));
    }

    [Fact]
    public async Task Spec_Impurity_NotMoreThan_AcceptsZeroAsNotDetected()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Impurity);
        var req = Spec(a, LimitType.NotMoreThan) with { UpperLimit = 0m };
        await a.Svc.CreateSpecificationAsync(a.UserId, req);
        Assert.Equal(0m, (await db.Specifications.SingleAsync()).UpperLimit);
    }

    [Fact]
    public async Task Spec_Impurity_RejectsRange()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Impurity);
        await AssertThrowsAsync("Elemental impurity specifications use a not-more-than limit.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a)));
    }

    [Fact]
    public async Task Spec_RequiresAnElement_OfThisTest()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Assay);
        await AssertThrowsAsync("Element is required for ICP method assay specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, elementId: null)));

        var other = new TestDefinitionIcpElement { TestDefinition = new TestDefinition { Code = "OTHER", DisplayName = "x", SectionId = 1 }, Symbol = "Fe", TargetPpm = 1m };
        db.TestDefinitionIcpElements.Add(other);
        await db.SaveChangesAsync();
        await AssertThrowsAsync($"That element does not belong to test '{a.Code}'.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, elementId: other.Id)));
    }

    [Fact]
    public async Task Spec_RejectsResultBasisAndLabelClaim()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Assay);
        await AssertThrowsAsync("Result basis is not used for ICP specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, basis: ResultBasis.PercentLabelClaim)));
        await AssertThrowsAsync("Label claim is not used for ICP specifications.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, claim: 10m)));
    }

    [Fact]
    public async Task Spec_DuplicateForTheSameElement_Rejected()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Assay);
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a));
        await Assert.ThrowsAsync<InvalidOperationException>(() => a.Svc.CreateSpecificationAsync(a.UserId, Spec(a, name: "Zn again")));
    }

    [Fact]
    public async Task Update_RemovingAnElementUsedBySpecs_Throws_AndSoDoesChangingTheMode()
    {
        await using var db = NewDb();
        var a = await ArrangeSpecAsync(db, IcpTestMode.Assay);
        await a.Svc.CreateSpecificationAsync(a.UserId, Spec(a));
        var service = TestServiceFactory.TestDefinitionMaster(db);
        var def = await db.TestDefinitions.AsNoTracking().SingleAsync();

        await AssertThrowsAsync("Element \"Zn\" is used by specifications; it can't be removed.",
            () => service.UpdateTestDefinitionAsync(a.UserId, def.Id, new UpdateTestDefinitionRequest(def.Code, def.DisplayName)
                { IcpElements = new() { new IcpTestElementInput(null, "Ca", 5m) } }));
        await AssertThrowsAsync("This test has specifications; remove them before changing between assay and impurity.",
            () => service.UpdateTestDefinitionAsync(a.UserId, def.Id, new UpdateTestDefinitionRequest(def.Code, def.DisplayName)
                { IcpMode = IcpTestMode.Impurity, IcpElements = new() { new IcpTestElementInput(a.ZnId, "Zn", null) } }));
    }
}
