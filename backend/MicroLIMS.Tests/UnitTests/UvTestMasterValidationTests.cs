using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UvTestMasterValidationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection fpSec, User fpHead, MasterDataControllers controller) SetupController(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var fpSec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (fpSec == null)
        {
            fpSec = new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(fpSec);
            db.SaveChanges();
        }

        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead);
        if (headRole == null)
        {
            headRole = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
            db.Roles.Add(headRole);
            db.SaveChanges();
        }

        var fpHead = new User
        {
            Username = "fpHead_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Section Head",
            RoleId = headRole.Id,
            Role = headRole,
            IsActive = true
        };
        db.Users.Add(fpHead);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpHead.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.SaveChanges();

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
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, fpHead.Id.ToString()) },
                    "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (fpSec, fpHead, controller);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_EachMode_SavesWithConstants()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // 1. SpecificAbsorbanceAssay
        var reqAssay = new CreateTestDefinitionRequest(
            Code: "UV-RIBO",
            DisplayName: "Riboflavin 5-Phosphate Assay",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.SpecificAbsorbanceAssay,
            UvSpecificAbsorbance: 328m,
            UvTotalVolumeMl: 5000m,
            PhyschemArea: PhyschemArea.Both);

        var resAssay = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.CreateTestDefinition(reqAssay)).Value).Data);

        Assert.Equal(UvCalculationMode.SpecificAbsorbanceAssay, resAssay.UvCalculationMode);
        Assert.Equal(328m, resAssay.UvSpecificAbsorbance);
        Assert.Equal(5000m, resAssay.UvTotalVolumeMl);
        Assert.Null(resAssay.UvStandardVolumeMl);

        // 2. SpecificAbsorbanceValue
        var reqVal = new CreateTestDefinitionRequest(
            Code: "UV-PYR",
            DisplayName: "Pyridoxine Identification",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.SpecificAbsorbanceValue,
            UvTotalVolumeMl: 250000m,
            PhyschemArea: PhyschemArea.Both);

        var resVal = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.CreateTestDefinition(reqVal)).Value).Data);

        Assert.Equal(UvCalculationMode.SpecificAbsorbanceValue, resVal.UvCalculationMode);
        Assert.Equal(250000m, resVal.UvTotalVolumeMl);
        Assert.Null(resVal.UvSpecificAbsorbance);
        Assert.Null(resVal.UvStandardVolumeMl);

        // 3. StandardComparison
        var reqStd = new CreateTestDefinitionRequest(
            Code: "UV-STD",
            DisplayName: "UV Standard Comparison",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 2,
            UvCalculationMode: UvCalculationMode.StandardComparison,
            UvTotalVolumeMl: 100m,
            UvStandardVolumeMl: 100m,
            PhyschemArea: PhyschemArea.Both);

        var resStd = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.CreateTestDefinition(reqStd)).Value).Data);

        Assert.Equal(UvCalculationMode.StandardComparison, resStd.UvCalculationMode);
        Assert.Equal(100m, resStd.UvTotalVolumeMl);
        Assert.Equal(100m, resStd.UvStandardVolumeMl);

        // 4. AbsorbanceLimit
        var reqLimit = new CreateTestDefinitionRequest(
            Code: "UV-PO4",
            DisplayName: "Inorganic Phosphate Limit",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.AbsorbanceLimit,
            PhyschemArea: PhyschemArea.Both);

        var resLimit = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.CreateTestDefinition(reqLimit)).Value).Data);

        Assert.Equal(UvCalculationMode.AbsorbanceLimit, resLimit.UvCalculationMode);
        Assert.Null(resLimit.UvSpecificAbsorbance);
        Assert.Null(resLimit.UvTotalVolumeMl);
        Assert.Null(resLimit.UvStandardVolumeMl);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_NeedsCalculationMode()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "UV-NOMODE",
            DisplayName: "UV without Mode",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: null,
            PhyschemArea: PhyschemArea.Both);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(req));
        Assert.Equal("A UV test needs a calculation mode.", ex.Message);
    }

    [Fact]
    public async Task Uv_UndefinedCalculationMode_RefusedOnCreateAndUpdate()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "UV-BADMODE",
            DisplayName: "UV with bad mode",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: (UvCalculationMode)9,
            PhyschemArea: PhyschemArea.Both);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(req));
        Assert.Equal("Unknown UV calculation mode.", ex.Message);

        var created = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.CreateTestDefinition(
                    req with { Code = "UV-GOODMODE", UvCalculationMode = UvCalculationMode.AbsorbanceLimit })).Value).Data);

        var exUpd = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.UpdateTestDefinition(
            created.Id, new UpdateTestDefinitionRequest(Code: "UV-GOODMODE", DisplayName: "UV with bad mode", UvCalculationMode: (UvCalculationMode)9)));
        Assert.Equal("Unknown UV calculation mode.", exUpd.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_SpecificAbsorbanceAssay_RequiresConstants()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Missing specific absorbance
        var reqNoAbs = new CreateTestDefinitionRequest(
            Code: "UV-ASSAY-1",
            DisplayName: "UV Assay",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.SpecificAbsorbanceAssay,
            UvSpecificAbsorbance: null,
            UvTotalVolumeMl: 5000m,
            PhyschemArea: PhyschemArea.Both);

        var exNoAbs = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqNoAbs));
        Assert.Equal("This UV mode needs a specific absorbance above 0.", exNoAbs.Message);

        // Specific absorbance <= 0
        var reqZeroAbs = reqNoAbs with { UvSpecificAbsorbance = 0m };
        var exZeroAbs = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqZeroAbs));
        Assert.Equal("This UV mode needs a specific absorbance above 0.", exZeroAbs.Message);

        // Missing total volume
        var reqNoVol = reqNoAbs with { UvSpecificAbsorbance = 328m, UvTotalVolumeMl = null };
        var exNoVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqNoVol));
        Assert.Equal("This UV mode needs a total volume above 0.", exNoVol.Message);

        // Total volume <= 0
        var reqZeroVol = reqNoAbs with { UvSpecificAbsorbance = 328m, UvTotalVolumeMl = 0m };
        var exZeroVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqZeroVol));
        Assert.Equal("This UV mode needs a total volume above 0.", exZeroVol.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_SpecificAbsorbanceValue_RequiresTotalVolume()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Missing total volume
        var reqNoVol = new CreateTestDefinitionRequest(
            Code: "UV-VAL-1",
            DisplayName: "UV Value",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.SpecificAbsorbanceValue,
            UvTotalVolumeMl: null,
            PhyschemArea: PhyschemArea.Both);

        var exNoVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqNoVol));
        Assert.Equal("This UV mode needs a total volume above 0.", exNoVol.Message);

        // Total volume <= 0
        var reqZeroVol = reqNoVol with { UvTotalVolumeMl = 0m };
        var exZeroVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqZeroVol));
        Assert.Equal("This UV mode needs a total volume above 0.", exZeroVol.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_StandardComparison_RequiresVolumes()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Missing total volume
        var reqNoTotalVol = new CreateTestDefinitionRequest(
            Code: "UV-STD-1",
            DisplayName: "UV Standard",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.StandardComparison,
            UvTotalVolumeMl: null,
            UvStandardVolumeMl: 100m,
            PhyschemArea: PhyschemArea.Both);

        var exNoTotalVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqNoTotalVol));
        Assert.Equal("This UV mode needs a total volume above 0.", exNoTotalVol.Message);

        // Missing standard volume
        var reqNoStdVol = reqNoTotalVol with { UvTotalVolumeMl = 100m, UvStandardVolumeMl = null };
        var exNoStdVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqNoStdVol));
        Assert.Equal("A UV test against a standard needs a standard volume above 0.", exNoStdVol.Message);

        // Standard volume <= 0
        var reqZeroStdVol = reqNoTotalVol with { UvTotalVolumeMl = 100m, UvStandardVolumeMl = 0m };
        var exZeroStdVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqZeroStdVol));
        Assert.Equal("A UV test against a standard needs a standard volume above 0.", exZeroStdVol.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_NonUvTestRefusesUvFields()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Gravimetric test with UV calculation mode
        var reqGravMode = new CreateTestDefinitionRequest(
            Code: "GRAV-WITH-UV",
            DisplayName: "Gravimetric with UV mode",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.GravimetricLoss,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.AbsorbanceLimit,
            PhyschemArea: PhyschemArea.Both);

        var exMode = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqGravMode));
        Assert.Equal("UV fields are only allowed for UV spectrophotometry tests.", exMode.Message);

        // Gravimetric test with UV total volume
        var reqGravVol = new CreateTestDefinitionRequest(
            Code: "GRAV-WITH-VOL",
            DisplayName: "Gravimetric with UV volume",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.GravimetricLoss,
            ReplicateCount: 1,
            UvTotalVolumeMl: 100m,
            PhyschemArea: PhyschemArea.Both);

        var exVol = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqGravVol));
        Assert.Equal("UV fields are only allowed for UV spectrophotometry tests.", exVol.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_RequiresReplicateCount1To30()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var reqBase = new CreateTestDefinitionRequest(
            Code: "UV-REP",
            DisplayName: "UV Replicate Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: null,
            UvCalculationMode: UvCalculationMode.AbsorbanceLimit,
            PhyschemArea: PhyschemArea.Both);

        // null ReplicateCount
        var exNull = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqBase));
        Assert.Contains("Replicate count must be between 1 and 30", exNull.Message);

        // 0 ReplicateCount
        var exZero = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqBase with { ReplicateCount = 0 }));
        Assert.Contains("Replicate count must be between 1 and 30", exZero.Message);

        // 31 ReplicateCount
        var ex31 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqBase with { ReplicateCount = 31 }));
        Assert.Contains("Replicate count must be between 1 and 30", ex31.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Uv_PairsOnlyWithUvSpectrophotometry()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // WorkflowType UV with EquationType Qualitative
        var reqUvWithQual = new CreateTestDefinitionRequest(
            Code: "UV-PAIR-1",
            DisplayName: "UV with Qualitative",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.Qualitative,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.AbsorbanceLimit,
            PhyschemArea: PhyschemArea.Both);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqUvWithQual));
        Assert.Equal("Equation type must be UvSpectrophotometry when workflow type is UvSpectrophotometry.", ex1.Message);

        // WorkflowType Qualitative with EquationType UV
        var reqQualWithUv = new CreateTestDefinitionRequest(
            Code: "UV-PAIR-2",
            DisplayName: "Qualitative with UV",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Qualitative,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            PhyschemArea: PhyschemArea.Both);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.CreateTestDefinition(reqQualWithUv));
        Assert.Equal("Workflow type must be UvSpectrophotometry when equation type is UvSpectrophotometry.", ex2.Message);
    }

    [Fact]
    public async Task UpdateTestDefinition_Uv_ValidatesConstantsAndUpdatesFields()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var createReq = new CreateTestDefinitionRequest(
            Code: "UV-UPD",
            DisplayName: "UV Update Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.UvSpectrophotometry,
            EquationType: EquationType.UvSpectrophotometry,
            ReplicateCount: 1,
            UvCalculationMode: UvCalculationMode.AbsorbanceLimit,
            PhyschemArea: PhyschemArea.Both);

        var created = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.CreateTestDefinition(createReq)).Value).Data);

        // Update to SpecificAbsorbanceAssay without constants should fail
        var updateInvalid = new UpdateTestDefinitionRequest(
            Code: "UV-UPD",
            DisplayName: "UV Update Test",
            UvCalculationMode: UvCalculationMode.SpecificAbsorbanceAssay);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.TestDefinition.UpdateTestDefinition(created.Id, updateInvalid));
        Assert.Equal("This UV mode needs a specific absorbance above 0.", ex.Message);

        // Update with valid constants succeeds
        var updateValid = new UpdateTestDefinitionRequest(
            Code: "UV-UPD",
            DisplayName: "UV Update Test",
            UvCalculationMode: UvCalculationMode.SpecificAbsorbanceAssay,
            UvSpecificAbsorbance: 328m,
            UvTotalVolumeMl: 5000m);

        var updated = Assert.IsType<TestDefinitionResponse>(
            Assert.IsType<ApiResponse<object>>(
                Assert.IsType<OkObjectResult>(await controller.TestDefinition.UpdateTestDefinition(created.Id, updateValid)).Value).Data);

        Assert.Equal(UvCalculationMode.SpecificAbsorbanceAssay, updated.UvCalculationMode);
        Assert.Equal(328m, updated.UvSpecificAbsorbance);
        Assert.Equal(5000m, updated.UvTotalVolumeMl);
    }
}
