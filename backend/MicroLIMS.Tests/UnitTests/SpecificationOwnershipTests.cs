using Microsoft.AspNetCore.Http;
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

// Task 9 - specification rows are owned by their test's lab. An item has one
// shared specification list; each lab's Section Head may add/edit/delete
// only the rows whose TestCode belongs to their own lab's Test Master entry.
public class SpecificationOwnershipTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private class Fixture
    {
        public DocumentSection MicroSec = null!;
        public DocumentSection FpSec = null!;
        public User MicroHead = null!;
        public User FpHead = null!;
        public User Admin = null!;
        public Item Item = null!;
        public TestDefinition Tamc = null!; // micro-owned
        public TestDefinition Assay = null!; // FP-owned
    }

    private static Fixture Seed(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var fpSec = new DocumentSection
        {
            Name = "Finished Product Laboratory",
            Code = "FP",
            DepartmentId = microSec.DepartmentId,
            IsActive = true
        };
        db.DocumentSections.Add(fpSec);
        db.SaveChanges();

        var headRole = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.AddRange(headRole, adminRole);
        db.SaveChanges();

        var microHead = new User { Username = "microHead_" + Guid.NewGuid().ToString("N")[..6], FullName = "Micro Head", RoleId = headRole.Id, Role = headRole, IsActive = true };
        var fpHead = new User { Username = "fpHead_" + Guid.NewGuid().ToString("N")[..6], FullName = "FP Head", RoleId = headRole.Id, Role = headRole, IsActive = true };
        var admin = new User { Username = "admin_" + Guid.NewGuid().ToString("N")[..6], FullName = "Sys Admin", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Users.AddRange(microHead, fpHead, admin);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = microHead.Id, DepartmentId = microSec.DepartmentId, SectionId = microSec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpHead.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.SaveChanges();

        var item = new Item { Name = "Test Product", Code = "TP-1", Category = SampleCategory.FinishedProduct, SopNumber = "SOP-1", IsActive = true };
        db.Items.Add(item);
        db.SaveChanges();

        var tamc = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", SectionId = microSec.Id, WorkflowType = WorkflowType.Observation, IsActive = true };
        var assay = new TestDefinition { Code = "ASSAY", DisplayName = "Assay", SectionId = fpSec.Id, WorkflowType = WorkflowType.Observation, IsActive = true };
        db.TestDefinitions.AddRange(tamc, assay);
        db.SaveChanges();

        db.SampleTests.AddRange(
            new SampleTest { ItemId = item.Id, TestCode = "TAMC", DisplayName = "TAMC" },
            new SampleTest { ItemId = item.Id, TestCode = "ASSAY", DisplayName = "Assay" });
        db.SaveChanges();

        return new Fixture
        {
            MicroSec = microSec,
            FpSec = fpSec,
            MicroHead = microHead,
            FpHead = fpHead,
            Admin = admin,
            Item = item,
            Tamc = tamc,
            Assay = assay
        };
    }

    private static MasterDataController BuildController(MicroLimsDbContext db, User actingAs)
    {
        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        var controller = new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService);

        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, actingAs.Id.ToString()) },
                    "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static Specification SeedSpec(MicroLimsDbContext db, int itemId, string testCode, string parameterName)
    {
        var spec = new Specification
        {
            ItemId = itemId,
            TestCode = testCode,
            ParameterName = parameterName,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100,
            SpecLimit = "NMT 100",
            Unit = "cfu/g"
        };
        db.Specifications.Add(spec);
        db.SaveChanges();
        return spec;
    }

    // ---- SpecificationOwnership helper (direct) ----

    [Fact]
    public async Task EnsureCanEditAsync_MicroHead_OwnTamcRow_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        await SpecificationOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "TAMC");
        // no exception - passed
    }

    [Fact]
    public async Task EnsureCanEditAsync_MicroHead_AssayRow_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => SpecificationOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "ASSAY"));
        Assert.Equal("This specification belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task EnsureCanEditAsync_Admin_AnyRow_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        await SpecificationOwnership.EnsureCanEditAsync(db, scope, f.Admin.Id, "TAMC");
        await SpecificationOwnership.EnsureCanEditAsync(db, scope, f.Admin.Id, "ASSAY");
        // no exception either way - passed
    }

    [Fact]
    public async Task EnsureCanEditAsync_UnknownTestCode_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SpecificationOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "NOPE"));
        Assert.Equal("Test code 'NOPE' is not in the Test Master.", ex.Message);
    }

    // ---- Controller actions ----

    [Fact]
    public async Task Update_MicroHead_TamcRow_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var spec = SeedSpec(db, f.Item.Id, "TAMC", "TAMC limit");
        var controller = BuildController(db, f.MicroHead);

        var request = new UpdateSpecificationRequest("TAMC", ParameterName: "TAMC limit", LimitType: LimitType.NotMoreThan, UpperLimit: 200, SpecLimit: "NMT 200", Unit: "cfu/g");
        var result = await controller.UpdateSpecification(spec.Id, request);

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.Specifications.FindAsync(spec.Id);
        Assert.Equal(200, updated!.UpperLimit);
    }

    [Fact]
    public async Task Update_MicroHead_AssayRow_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var spec = SeedSpec(db, f.Item.Id, "ASSAY", "Assay limit");
        var controller = BuildController(db, f.MicroHead);

        var request = new UpdateSpecificationRequest("ASSAY", ParameterName: "Assay limit", LimitType: LimitType.NotMoreThan, UpperLimit: 200, SpecLimit: "NMT 200", Unit: "%");
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.UpdateSpecification(spec.Id, request));
        Assert.Equal("This specification belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task Update_MicroHead_ChangesTestCodeFromTamcToAssay_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var spec = SeedSpec(db, f.Item.Id, "TAMC", "TAMC limit");
        var controller = BuildController(db, f.MicroHead);

        // Reassigning a micro-owned row to an FP test code is forbidden even
        // though the row currently belongs to micro's own lab.
        var request = new UpdateSpecificationRequest("ASSAY", ParameterName: "TAMC limit", LimitType: LimitType.NotMoreThan, UpperLimit: 200, SpecLimit: "NMT 200", Unit: "cfu/g");
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.UpdateSpecification(spec.Id, request));
        Assert.Equal("This specification belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);

        // Row must be unchanged.
        var unchanged = await db.Specifications.FindAsync(spec.Id);
        Assert.Equal("TAMC", unchanged!.TestCode);
    }

    [Fact]
    public async Task Delete_MicroHead_AssayRow_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var spec = SeedSpec(db, f.Item.Id, "ASSAY", "Assay limit");
        var controller = BuildController(db, f.MicroHead);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.DeleteSpecification(spec.Id));
        Assert.Equal("This specification belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);

        Assert.NotNull(await db.Specifications.FindAsync(spec.Id));
    }

    [Fact]
    public async Task Delete_Admin_AssayRow_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var spec = SeedSpec(db, f.Item.Id, "ASSAY", "Assay limit");
        var controller = BuildController(db, f.Admin);

        var result = await controller.DeleteSpecification(spec.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(await db.Specifications.FindAsync(spec.Id));
    }

    [Fact]
    public async Task Create_FpHead_TamcRow_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var controller = BuildController(db, f.FpHead);

        var request = new CreateSpecificationRequest(f.Item.Id, "TAMC", ParameterName: "TAMC limit", LimitType: LimitType.NotMoreThan, UpperLimit: 100, SpecLimit: "NMT 100", Unit: "cfu/g");
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.CreateSpecification(request));
        Assert.Equal("This specification belongs to Microbiology Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task Create_Admin_AnyRow_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var controller = BuildController(db, f.Admin);

        var request = new CreateSpecificationRequest(f.Item.Id, "ASSAY", ParameterName: "Assay limit", LimitType: LimitType.NotMoreThan, UpperLimit: 100, SpecLimit: "NMT 100", Unit: "%");
        var result = await controller.CreateSpecification(request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.Specifications.Where(s => s.TestCode == "ASSAY"));
    }

    [Fact]
    public async Task Get_MicroHead_SeesBothRows_ButCanEditOnlyOwn()
    {
        await using var db = NewDb();
        var f = Seed(db);
        SeedSpec(db, f.Item.Id, "TAMC", "TAMC limit");
        SeedSpec(db, f.Item.Id, "ASSAY", "Assay limit");
        var controller = BuildController(db, f.MicroHead);

        var result = await controller.GetSpecifications(f.Item.Id);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        var rows = Assert.IsType<List<SpecificationRowDto>>(response.Data);

        Assert.Equal(2, rows.Count);
        var tamcRow = rows.Single(r => r.TestCode == "TAMC");
        var assayRow = rows.Single(r => r.TestCode == "ASSAY");
        Assert.True(tamcRow.CanEdit);
        Assert.Equal("Microbiology Laboratory", tamcRow.SectionName);
        Assert.False(assayRow.CanEdit);
        Assert.Equal("Finished Product Laboratory", assayRow.SectionName);
    }

    [Fact]
    public async Task Get_Admin_SeesBothRows_CanEditBoth()
    {
        await using var db = NewDb();
        var f = Seed(db);
        SeedSpec(db, f.Item.Id, "TAMC", "TAMC limit");
        SeedSpec(db, f.Item.Id, "ASSAY", "Assay limit");
        var controller = BuildController(db, f.Admin);

        var result = await controller.GetSpecifications(f.Item.Id);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        var rows = Assert.IsType<List<SpecificationRowDto>>(response.Data);

        Assert.All(rows, r => Assert.True(r.CanEdit));
    }
}
