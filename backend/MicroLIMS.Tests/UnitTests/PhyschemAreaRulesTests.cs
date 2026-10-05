using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PhyschemAreaRulesTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    // FP and MICRO sections plus a user who belongs to both.
    private static async Task<(DocumentSection fp, DocumentSection micro, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = micro.DepartmentId, SectionId = fp.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = micro.DepartmentId, SectionId = micro.Id });
        await db.SaveChangesAsync();
        return (fp, micro, user.Id);
    }

    private static TestDefinitionMasterDataService TestSvc(MicroLimsDbContext db) => TestServiceFactory.TestDefinitionMaster(db);

    private static CreateTestDefinitionRequest Create(string code, int sectionId, PhyschemArea? area) =>
        new(Code: code, DisplayName: code, SectionId: sectionId, WorkflowType: WorkflowType.Observation, PhyschemArea: area);

    private static async Task<Item> AddItemAsync(MicroLimsDbContext db, string code, SampleCategory category, string testCode)
    {
        var item = new Item
        {
            Code = code, Name = code, Category = category,
            AssignedTests = { new SampleTest { TestCode = testCode, DisplayName = testCode } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static Specification Spec(Item item, string testCode) => new()
    {
        ItemId = item.Id, TestCode = testCode, ParameterName = "Appearance",
        LimitType = LimitType.Qualitative, ExpectedResultText = "White powder"
    };

    [Fact]
    public async Task Create_InPhyschemSection_WithoutArea_Throws()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestSvc(db).CreateTestDefinitionAsync(userId, Create("T1", fp.Id, null)));
        Assert.Equal("Choose the area of this test (FP, RM & PM or Both).", ex.Message);
    }

    [Fact]
    public async Task Create_InPhyschemSection_StoresArea_AndReturnsIt()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        var r = await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T1", fp.Id, PhyschemArea.RawPackaging));
        Assert.Equal(PhyschemArea.RawPackaging, r.PhyschemArea);
        Assert.Equal(PhyschemArea.RawPackaging, (await db.TestDefinitions.SingleAsync()).PhyschemArea);
    }

    [Fact]
    public async Task Create_InMicroSection_ClearsArea()
    {
        using var db = NewDb();
        var (_, micro, userId) = await SeedAsync(db);
        var r = await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T1", micro.Id, PhyschemArea.Both));
        Assert.Null(r.PhyschemArea);
        Assert.Null((await db.TestDefinitions.SingleAsync()).PhyschemArea);
    }

    [Fact]
    public async Task Spec_ForFinishedProductItem_OnRawPackagingTest_Throws()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T-RM", fp.Id, PhyschemArea.RawPackaging));
        var item = await AddItemAsync(db, "FP-1", SampleCategory.FinishedProduct, "T-RM");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.Specification(db).ValidateAsync(Spec(item, "T-RM")));
        Assert.Equal("Test T-RM is not used for finished products - it belongs to RM & PM.", ex.Message);
    }

    [Fact]
    public async Task Spec_ForRawMaterialItem_OnFinishedProductTest_Throws()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T-FP", fp.Id, PhyschemArea.FinishedProduct));
        var item = await AddItemAsync(db, "RM-1", SampleCategory.RawMaterial, "T-FP");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.Specification(db).ValidateAsync(Spec(item, "T-FP")));
        Assert.Equal("Test T-FP is not used for raw and packaging materials - it belongs to FP.", ex.Message);
    }

    [Fact]
    public async Task Spec_OnBothTest_AcceptsEitherKindOfItem()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T-BOTH", fp.Id, PhyschemArea.Both));
        var fpItem = await AddItemAsync(db, "FP-1", SampleCategory.FinishedProduct, "T-BOTH");
        var rmItem = await AddItemAsync(db, "RM-1", SampleCategory.RawMaterial, "T-BOTH");
        var svc = TestServiceFactory.Specification(db);
        await svc.ValidateAsync(Spec(fpItem, "T-BOTH"));
        await svc.ValidateAsync(Spec(rmItem, "T-BOTH"));
    }

    [Fact]
    public async Task Update_BothToFinishedProduct_WhileRawMaterialItemHasSpec_ListsItemCodes()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        var test = await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T-BOTH", fp.Id, PhyschemArea.Both));
        var rm = await AddItemAsync(db, "RM-CITRIC", SampleCategory.RawMaterial, "T-BOTH");
        var fpItem = await AddItemAsync(db, "FP-1", SampleCategory.FinishedProduct, "T-BOTH");
        db.Specifications.Add(Spec(rm, "T-BOTH"));
        db.Specifications.Add(Spec(fpItem, "T-BOTH"));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestSvc(db).UpdateTestDefinitionAsync(
            userId, test.Id, new UpdateTestDefinitionRequest(Code: "T-BOTH", DisplayName: "T-BOTH", PhyschemArea: PhyschemArea.FinishedProduct)));
        Assert.Equal("Items RM-CITRIC still have specifications on this test for RM & PM - remove them first.", ex.Message);

        // The mirror: FP items still have specs, so narrowing to RM & PM is refused too.
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestSvc(db).UpdateTestDefinitionAsync(
            userId, test.Id, new UpdateTestDefinitionRequest(Code: "T-BOTH", DisplayName: "T-BOTH", PhyschemArea: PhyschemArea.RawPackaging)));
        Assert.Equal("Items FP-1 still have specifications on this test for FP - remove them first.", ex.Message);
    }

    [Fact]
    public async Task Update_WithoutArea_KeepsExistingArea()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        var test = await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T1", fp.Id, PhyschemArea.RawPackaging));
        var r = await TestSvc(db).UpdateTestDefinitionAsync(userId, test.Id, new UpdateTestDefinitionRequest(Code: "T1", DisplayName: "Renamed"));
        Assert.Equal(PhyschemArea.RawPackaging, r.PhyschemArea);
    }

    [Fact]
    public async Task Update_NarrowingWithNoConflictingSpecs_Succeeds()
    {
        using var db = NewDb();
        var (fp, _, userId) = await SeedAsync(db);
        var test = await TestSvc(db).CreateTestDefinitionAsync(userId, Create("T1", fp.Id, PhyschemArea.Both));
        var r = await TestSvc(db).UpdateTestDefinitionAsync(userId, test.Id,
            new UpdateTestDefinitionRequest(Code: "T1", DisplayName: "T1", PhyschemArea: PhyschemArea.FinishedProduct));
        Assert.Equal(PhyschemArea.FinishedProduct, r.PhyschemArea);
    }
}
