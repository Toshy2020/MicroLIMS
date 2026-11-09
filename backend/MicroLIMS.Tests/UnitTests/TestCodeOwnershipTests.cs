using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TestCodeOwnershipTests
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
        public User FpOnlyUser = null!;
        public User RmOnlyUser = null!;
        public User Admin = null!;
        public TestDefinition Tamc = null!;
        public TestDefinition TamcFrozen = null!;
        public TestDefinition AssayFp = null!;
        public TestDefinition AssayRm = null!;
        public TestDefinition AssayBoth = null!;
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
        var fpOnlyUser = new User { Username = "fpOnly_" + Guid.NewGuid().ToString("N")[..6], FullName = "FP Only", RoleId = headRole.Id, Role = headRole, IsActive = true };
        var rmOnlyUser = new User { Username = "rmOnly_" + Guid.NewGuid().ToString("N")[..6], FullName = "RM Only", RoleId = headRole.Id, Role = headRole, IsActive = true };
        var admin = new User { Username = "admin_" + Guid.NewGuid().ToString("N")[..6], FullName = "Sys Admin", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Users.AddRange(microHead, fpHead, fpOnlyUser, rmOnlyUser, admin);
        db.SaveChanges();

        // microHead belongs to MicroSection
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = microHead.Id, DepartmentId = microSec.DepartmentId, SectionId = microSec.Id });
        // fpHead has no area constraint -> both FP and RM
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpHead.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id, PhyschemArea = null });
        // fpOnlyUser has FinishedProduct area
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpOnlyUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id, PhyschemArea = PhyschemArea.FinishedProduct });
        // rmOnlyUser has RawPackaging area
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = rmOnlyUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id, PhyschemArea = PhyschemArea.RawPackaging });
        db.SaveChanges();

        var tamc = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", SectionId = microSec.Id, WorkflowType = WorkflowType.Observation, IsActive = true };
        var tamcFrozen = new TestDefinition { Code = "TAMC_FROZEN", DisplayName = "TAMC Frozen", SectionId = microSec.Id, WorkflowType = WorkflowType.Observation, IsActive = false };
        var assayFp = new TestDefinition { Code = "ASSAY_FP", DisplayName = "Assay FP", SectionId = fpSec.Id, PhyschemArea = PhyschemArea.FinishedProduct, WorkflowType = WorkflowType.Observation, IsActive = true };
        var assayRm = new TestDefinition { Code = "ASSAY_RM", DisplayName = "Assay RM", SectionId = fpSec.Id, PhyschemArea = PhyschemArea.RawPackaging, WorkflowType = WorkflowType.Observation, IsActive = true };
        var assayBoth = new TestDefinition { Code = "ASSAY_BOTH", DisplayName = "Assay Both", SectionId = fpSec.Id, PhyschemArea = PhyschemArea.Both, WorkflowType = WorkflowType.Observation, IsActive = true };
        db.TestDefinitions.AddRange(tamc, tamcFrozen, assayFp, assayRm, assayBoth);
        db.SaveChanges();

        return new Fixture
        {
            MicroSec = microSec,
            FpSec = fpSec,
            MicroHead = microHead,
            FpHead = fpHead,
            FpOnlyUser = fpOnlyUser,
            RmOnlyUser = rmOnlyUser,
            Admin = admin,
            Tamc = tamc,
            TamcFrozen = tamcFrozen,
            AssayFp = assayFp,
            AssayRm = assayRm,
            AssayBoth = assayBoth
        };
    }

    private static MasterDataControllers BuildController(MicroLimsDbContext db, User actingAs)
    {
        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        var controller = new MasterDataControllers(
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

    // ---- Task 1: TestCodeOwnership helper (direct) ----

    [Fact]
    // Same rule as SpecificationOwnership: master data follows lab membership,
    // so an administrator with no section membership edits nothing.
    public async Task GetEditableCodesAsync_Admin_NoMembership_ReturnsEmpty()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        var result = await TestCodeOwnership.GetEditableCodesAsync(db, scope, f.Admin.Id);
        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task EnsureCanEditAsync_Admin_NoMembership_IsRefused()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestCodeOwnership.EnsureCanEditAsync(db, scope, f.Admin.Id, new[] { "TAMC", "ASSAY_FP" }, "Test"));
    }

    [Fact]
    public async Task MicroHead_OwnSection_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        var editable = await TestCodeOwnership.GetEditableCodesAsync(db, scope, f.MicroHead.Id);
        Assert.NotNull(editable);
        Assert.Contains("TAMC", editable!);
        Assert.Contains("TAMC_FROZEN", editable);
        Assert.DoesNotContain("ASSAY_FP", editable);
        Assert.DoesNotContain("ASSAY_RM", editable);
        Assert.DoesNotContain("ASSAY_BOTH", editable);

        await TestCodeOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "TAMC", "Sampling point");
        // passed
    }

    [Fact]
    public async Task MicroHead_OtherSection_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => TestCodeOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "ASSAY_FP", "Sampling point"));
        Assert.Equal("Sampling point for test ASSAY_FP belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task PhyschemArea_Match_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        // FP user with ASSAY_FP
        var fpCodes = await TestCodeOwnership.GetEditableCodesAsync(db, scope, f.FpOnlyUser.Id);
        Assert.NotNull(fpCodes);
        Assert.Contains("ASSAY_FP", fpCodes!);
        Assert.Contains("ASSAY_BOTH", fpCodes);
        Assert.DoesNotContain("ASSAY_RM", fpCodes);
        await TestCodeOwnership.EnsureCanEditAsync(db, scope, f.FpOnlyUser.Id, "ASSAY_FP", "Point");

        // RM user with ASSAY_RM
        var rmCodes = await TestCodeOwnership.GetEditableCodesAsync(db, scope, f.RmOnlyUser.Id);
        Assert.NotNull(rmCodes);
        Assert.Contains("ASSAY_RM", rmCodes!);
        Assert.Contains("ASSAY_BOTH", rmCodes);
        Assert.DoesNotContain("ASSAY_FP", rmCodes);
        await TestCodeOwnership.EnsureCanEditAsync(db, scope, f.RmOnlyUser.Id, "ASSAY_RM", "Point");
    }

    [Fact]
    public async Task PhyschemArea_Mismatch_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        // FP user trying to edit RM test
        var ex1 = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => TestCodeOwnership.EnsureCanEditAsync(db, scope, f.FpOnlyUser.Id, "ASSAY_RM", "Machine part configuration"));
        Assert.Equal("Machine part configuration for test ASSAY_RM belongs to Finished Product Laboratory - only its Section Head can change it.", ex1.Message);

        // RM user trying to edit FP test
        var ex2 = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => TestCodeOwnership.EnsureCanEditAsync(db, scope, f.RmOnlyUser.Id, "ASSAY_FP", "Water sampling configuration"));
        Assert.Equal("Water sampling configuration for test ASSAY_FP belongs to Finished Product Laboratory - only its Section Head can change it.", ex2.Message);
    }

    [Fact]
    public async Task PhyschemArea_Both_CoversEitherArea()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        // FP area covers Both
        await TestCodeOwnership.EnsureCanEditAsync(db, scope, f.FpOnlyUser.Id, "ASSAY_BOTH", "Point");
        // RM area covers Both
        await TestCodeOwnership.EnsureCanEditAsync(db, scope, f.RmOnlyUser.Id, "ASSAY_BOTH", "Point");
        // User with both areas covers Both
        await TestCodeOwnership.EnsureCanEditAsync(db, scope, f.FpHead.Id, "ASSAY_BOTH", "Point");

        // Micro head cannot edit FP Both test (different section)
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => TestCodeOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "ASSAY_BOTH", "Point"));
        Assert.Equal("Point for test ASSAY_BOTH belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task UnknownTestCode_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var scope = new UserSectionScopeService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => TestCodeOwnership.EnsureCanEditAsync(db, scope, f.MicroHead.Id, "NOPE", "Point"));
        Assert.Equal("Test code 'NOPE' is not in the Test Master.", ex.Message);
    }

    [Fact]
    public async Task UserIdZero_WithEmptySectionList_IsDenied()
    {
        await using var db = NewDb();
        Seed(db);
        var scope = new UserSectionScopeService(db);

        var editable = await TestCodeOwnership.GetEditableCodesAsync(db, scope, 0);
        Assert.NotNull(editable);
        Assert.Empty(editable!);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => TestCodeOwnership.EnsureCanEditAsync(db, scope, 0, "TAMC", "Point"));
    }

    // ---- Task 2: Water Master Data ----

    [Fact]
    public async Task WaterSamplingPoint_Create_OwnTest_Succeeds()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var controller = BuildController(db, f.MicroHead);

        var req = new CreateWaterSamplingPointRequest("SP-1", "Room 101", "Daily", new List<string> { "TAMC" }, null);
        var result = await controller.Water.CreateWaterSamplingPoint(req);
        Assert.IsType<OkObjectResult>(result);

        var point = await db.WaterSamplingPoints.SingleAsync(p => p.Code == "SP-1");
        Assert.Equal(new[] { "TAMC" }, point.AssignedTestCodes);
    }

    [Fact]
    public async Task WaterSamplingPoint_Create_OtherLabTest_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var controller = BuildController(db, f.MicroHead);

        var req = new CreateWaterSamplingPointRequest("SP-1", "Room 101", "Daily", new List<string> { "ASSAY_FP" }, null);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Water.CreateWaterSamplingPoint(req));
        Assert.Equal("Water sampling point for test ASSAY_FP belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task WaterSamplingPoint_Update_UnchangedOtherLabTest_PassesThrough()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var point = new WaterSamplingPoint { Code = "SP-1", Location = "Loc 1", AssignedTestCodes = new() { "ASSAY_FP" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = BuildController(db, f.MicroHead);

        // MicroHead keeps ASSAY_FP untouched, but updates Location and adds TAMC
        var req = new UpdateWaterSamplingPointRequest("SP-1", "Loc 2", "Daily", new List<string> { "ASSAY_FP", "TAMC" }, null, true);
        var result = await controller.Water.UpdateWaterSamplingPoint(point.Id, req);
        Assert.IsType<OkObjectResult>(result);

        var updated = await db.WaterSamplingPoints.FindAsync(point.Id);
        Assert.Equal("Loc 2", updated!.Location);
        Assert.Equal(new[] { "ASSAY_FP", "TAMC" }, updated.AssignedTestCodes);
    }

    [Fact]
    public async Task WaterSamplingPoint_Update_RemovingOtherLabTest_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var point = new WaterSamplingPoint { Code = "SP-1", Location = "Loc 1", AssignedTestCodes = new() { "ASSAY_FP", "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = BuildController(db, f.MicroHead);

        // MicroHead tries to remove ASSAY_FP
        var req = new UpdateWaterSamplingPointRequest("SP-1", "Loc 1", "Daily", new List<string> { "TAMC" }, null, true);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Water.UpdateWaterSamplingPoint(point.Id, req));
        Assert.Equal("Water sampling point for test ASSAY_FP belongs to Finished Product Laboratory - only its Section Head can change it.", ex.Message);
    }

    [Fact]
    public async Task WaterSamplingPoint_Delete_PointWithOtherLabTest_Throws()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var point = new WaterSamplingPoint { Code = "SP-1", Location = "Loc 1", AssignedTestCodes = new() { "ASSAY_FP" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = BuildController(db, f.MicroHead);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Water.DeleteWaterSamplingPoint(point.Id));
        Assert.Equal("Cannot delete 'SP-1' - it still has tests from another laboratory assigned.", ex.Message);
    }

    [Fact]
    public async Task WaterSamplingPoint_Delete_Admin_NoMembership_IsRefused()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var point = new WaterSamplingPoint { Code = "SP-1", Location = "Loc 1", AssignedTestCodes = new() { "ASSAY_FP" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = BuildController(db, f.Admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Water.DeleteWaterSamplingPoint(point.Id));
        Assert.NotNull(await db.WaterSamplingPoints.FindAsync(point.Id));
    }

    [Fact]
    public async Task WaterSamplingConfiguration_OwnershipEnforced()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var point = new WaterSamplingPoint { Code = "SP-1", Location = "Loc 1" };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = BuildController(db, f.MicroHead);

        // Create with other lab test -> throws
        var createFp = new CreateWaterSamplingConfigRequest(point.Id, "ASSAY_FP", "10", "20", "50", "ppm");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Water.CreateWaterSamplingConfiguration(createFp));

        // Create with own test -> succeeds
        var createTamc = new CreateWaterSamplingConfigRequest(point.Id, "TAMC", "10", "20", "50", "cfu/ml");
        var result = await controller.Water.CreateWaterSamplingConfiguration(createTamc);
        Assert.IsType<OkObjectResult>(result);
        var config = await db.SamplingConfigurations.SingleAsync();

        // Update to other lab test -> throws
        var updateToFp = new UpdateWaterSamplingConfigRequest("ASSAY_FP", "10", "20", "50", "ppm");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Water.UpdateWaterSamplingConfiguration(config.Id, updateToFp));

        // Delete own config -> succeeds
        var deleteResult = await controller.Water.DeleteWaterSamplingConfiguration(config.Id);
        Assert.IsType<OkObjectResult>(deleteResult);
        Assert.Empty(db.SamplingConfigurations);
    }

    // ---- Task 3: After Cleaning Master Data ----

    [Fact]
    public async Task MachinePartConfiguration_OwnershipEnforced()
    {
        await using var db = NewDb();
        var f = Seed(db);
        var machine = new Machine { Name = "Mixer 1" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var part = new MachinePart { Name = "Blade", MachineId = machine.Id };
        db.MachineParts.Add(part);
        await db.SaveChangesAsync();

        var fpController = BuildController(db, f.FpOnlyUser);
        var microController = BuildController(db, f.MicroHead);

        // Micro user trying to configure FP test -> throws
        var req = new CreateMachinePartConfigRequest(part.Id, "Swab", "ASSAY_FP", "10", "20", "50", false, "ppm");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => microController.AfterCleaning.CreateMachinePartConfiguration(req));

        // FP user configuring ASSAY_FP -> succeeds
        var createResult = await fpController.AfterCleaning.CreateMachinePartConfiguration(req);
        Assert.IsType<OkObjectResult>(createResult);
        var cfg = await db.MachinePartConfigurations.SingleAsync();

        // FP user trying to update to RM test -> throws
        var updateToRm = new UpdateMachinePartConfigRequest("Swab", "ASSAY_RM", "10", "20", "50", false, "ppm");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fpController.AfterCleaning.UpdateMachinePartConfiguration(cfg.Id, updateToRm));

        // FP user deleting ASSAY_FP -> succeeds
        var deleteResult = await fpController.AfterCleaning.DeleteMachinePartConfiguration(cfg.Id);
        Assert.IsType<OkObjectResult>(deleteResult);
        Assert.Empty(db.MachinePartConfigurations);
    }

    // ---- Task 4: GET test-definitions/editable-codes ----

    [Fact]
    public async Task GetEditableCodes_ReturnsExpectedCodesPerRole()
    {
        await using var db = NewDb();
        var f = Seed(db);

        // Admin with no section membership can edit none
        var adminController = BuildController(db, f.Admin);
        var adminRes = Assert.IsType<OkObjectResult>(await adminController.TestDefinition.GetEditableCodes());
        Assert.Empty(Assert.IsType<string[]>(Assert.IsType<ApiResponse<object>>(adminRes.Value).Data));

        // MicroHead sees only Micro codes
        var microController = BuildController(db, f.MicroHead);
        var microRes = Assert.IsType<OkObjectResult>(await microController.TestDefinition.GetEditableCodes());
        var microCodes = Assert.IsType<string[]>(Assert.IsType<ApiResponse<object>>(microRes.Value).Data);
        Assert.Contains("TAMC", microCodes);
        Assert.DoesNotContain("ASSAY_FP", microCodes);
        Assert.DoesNotContain("ASSAY_RM", microCodes);
        Assert.DoesNotContain("ASSAY_BOTH", microCodes);

        // FpOnlyUser sees FP and Both
        var fpController = BuildController(db, f.FpOnlyUser);
        var fpRes = Assert.IsType<OkObjectResult>(await fpController.TestDefinition.GetEditableCodes());
        var fpCodes = Assert.IsType<string[]>(Assert.IsType<ApiResponse<object>>(fpRes.Value).Data);
        Assert.Contains("ASSAY_FP", fpCodes);
        Assert.Contains("ASSAY_BOTH", fpCodes);
        Assert.DoesNotContain("ASSAY_RM", fpCodes);
        Assert.DoesNotContain("TAMC", fpCodes);
    }
}
