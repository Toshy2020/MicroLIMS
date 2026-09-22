using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcMultiAnalyteSliceM1Tests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection fpSec, User fpUser) SeedSectionAndUser(MicroLimsDbContext db)
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

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
            ?? new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        if (analystRole.Id == 0) { db.Roles.Add(analystRole); db.SaveChanges(); }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!");

        var fpUser = new User
        {
            Username = "fp_analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpUser);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpUser.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.SaveChanges();

        return (fpSec, fpUser);
    }

    private static MasterDataController CreateMasterDataController(MicroLimsDbContext db, User user)
    {
        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        return new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()) },
                            "TestAuth"))
                }
            }
        };
    }

    private static (TestDefinition test, Equipment equip, ChromatographyColumn col, Material std1, Material std2, TestAnalyte b1, TestAnalyte b2)
        SeedHplcMultiPrerequisites(MicroLimsDbContext db, int sectionId, int userId, string methodAbbr = "M-VIT")
    {
        var test = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Assay",
            SectionId = sectionId,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcPreparations = 2,
            HplcInjectionsPerPreparation = 2,
            HplcMaxPreparationRsdPercent = 2.0m
        };
        db.TestDefinitions.Add(test);

        var equip = new Equipment
        {
            Name = "HPLC Agilent 1260",
            Code = $"HPLC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Hplc,
            SectionId = sectionId,
            CdsSoftware = CdsSoftware.AgilentOpenLab
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = "C18 150x4.6mm",
            Code = $"COL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SerialNumber = "SN-987654",
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.ChromatographyColumns.Add(col);

        var std1 = new Material
        {
            MaterialName = "Thiamine HCl RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-B1-001",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 1000m,
            QuantityRemaining = 1000m,
            Unit = MaterialUnit.Gram,
            Location = "Standards Refrigerator",
            SectionId = sectionId,
            Purity = 99.5m,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        var std2 = new Material
        {
            MaterialName = "Riboflavin RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-B2-001",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 1000m,
            QuantityRemaining = 1000m,
            Unit = MaterialUnit.Gram,
            Location = "Standards Refrigerator",
            SectionId = sectionId,
            Purity = 98.8m,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.Materials.AddRange(std1, std2);
        db.SaveChanges();

        var b1 = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Vitamin B1",
            WavelengthNm = 254.0m,
            DisplayOrder = 1,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2000m
        };
        var b2 = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Vitamin B2",
            WavelengthNm = 267.0m,
            DisplayOrder = 2,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2500m
        };
        db.TestAnalytes.AddRange(b1, b2);
        db.SaveChanges();

        return (test, equip, col, std1, std2, b1, b2);
    }

    #region Master Data Tests

    [Fact]
    public async Task MasterData_CreateTestDefinition_HplcMultiAnalyte_PairRuleEnforced()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var controller = CreateMasterDataController(db, fpUser);

        // EquationType=HplcMultiAnalyte with WorkflowType=Observation -> throws
        var req1 = new CreateTestDefinitionRequest(
            Code: "T_PAIR_1",
            DisplayName: "Pair test 1",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "HPLC-M");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Workflow type must be HplcMultiAnalyte when equation type is HplcMultiAnalyte", ex1.Message);

        // WorkflowType=HplcMultiAnalyte with EquationType=None -> throws
        var req2 = new CreateTestDefinitionRequest(
            Code: "T_PAIR_2",
            DisplayName: "Pair test 2",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.HplcMultiAnalyte,
            EquationType: EquationType.None,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "HPLC-M");

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Equation type must be HplcMultiAnalyte when workflow type is HplcMultiAnalyte", ex2.Message);
    }

    [Fact]
    public async Task MasterData_CreateTestDefinition_HplcMultiAnalyte_RequiresSystemSuitabilityMustBeTrue()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var controller = CreateMasterDataController(db, fpUser);

        var req = new CreateTestDefinitionRequest(
            Code: "T_SST_REQ",
            DisplayName: "SST required test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.HplcMultiAnalyte,
            EquationType: EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability: false,
            MethodAbbreviation: "HPLC-M");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("HPLC multi-analyte tests must require system suitability", ex.Message);
    }

    [Fact]
    public async Task MasterData_CreateTestDefinition_HplcMultiAnalyte_ValidatesPreparationsAndInjections()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var controller = CreateMasterDataController(db, fpUser);

        // Preps out of range (< 1)
        var reqBadPreps = new CreateTestDefinitionRequest(
            Code: "T_BAD_PREPS",
            DisplayName: "Bad Preps",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.HplcMultiAnalyte,
            EquationType: EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "HPLC-M",
            HplcPreparations: 0);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqBadPreps));
        Assert.Contains("HPLC preparations must be between 1 and 10", ex1.Message);

        // Injections out of range (> 10)
        var reqBadInj = new CreateTestDefinitionRequest(
            Code: "T_BAD_INJ",
            DisplayName: "Bad Inj",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.HplcMultiAnalyte,
            EquationType: EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "HPLC-M",
            HplcInjectionsPerPreparation: 11);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqBadInj));
        Assert.Contains("HPLC injections per preparation must be between 1 and 10", ex2.Message);

        // Max preparation RSD <= 0
        var reqBadRsd = new CreateTestDefinitionRequest(
            Code: "T_BAD_RSD",
            DisplayName: "Bad RSD",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.HplcMultiAnalyte,
            EquationType: EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "HPLC-M",
            HplcMaxPreparationRsdPercent: 0m);

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqBadRsd));
        Assert.Contains("HPLC maximum preparation RSD percent must be greater than zero", ex3.Message);

        // Valid with defaults (null preps and injections -> defaults to 2 and 2)
        var reqValid = new CreateTestDefinitionRequest(
            Code: "T_VALID_DEF",
            DisplayName: "Valid Defaults",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.HplcMultiAnalyte,
            EquationType: EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "HPLC-M");

        var res = await controller.CreateTestDefinition(reqValid);
        var ok = Assert.IsType<OkObjectResult>(res);
        var created = Assert.IsType<ApiResponse<object>>(ok.Value).Data as TestDefinition;
        Assert.NotNull(created);
        Assert.Equal(2, created.HplcPreparations);
        Assert.Equal(2, created.HplcInjectionsPerPreparation);
        Assert.Null(created.HplcMaxPreparationRsdPercent);
        // Note: run-level SST criteria are not set and not required on TestDefinition for HplcMultiAnalyte
        Assert.Null(created.SstMaxRsdPercent);
    }

    [Fact]
    public async Task MasterData_CreateTestWorkflowStep_HplcMultiAnalyte_Rejected()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, _, _, _, _, _, _) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var controller = CreateMasterDataController(db, fpUser);

        var stepReq = new CreateTestWorkflowStepRequest(
            StepName: "Step 1",
            IncubationMinHours: 0,
            IncubationMaxHours: 0,
            TemperatureMin: 0,
            TemperatureMax: 0,
            IsFinalStep: true,
            StepType: StepType.PlateCount,
            TargetOrganismId: null,
            StepMedia: new List<StepMediaRequest>(),
            RequiresIncubationTransfer: false,
            IncubationStages: null,
            ConfirmatoryMediaCount: null,
            PhenotypicTestType: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestWorkflowStep(test.Id, stepReq));
        Assert.Contains("tests have no workflow steps", ex.Message);
    }

    #endregion

    #region Test Analyte CRUD Tests

    [Fact]
    public async Task MasterData_TestAnalyte_HplcMultiAnalyte_RejectsView()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, _, _, _, _, _, _) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var controller = CreateMasterDataController(db, fpUser);

        var createReq = new CreateTestAnalyteRequest(
            Element: "Vitamin C",
            WavelengthNm: 245.0m,
            View: AnalyteView.Axial);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestAnalyte(test.Id, createReq));
        Assert.Equal("View is not allowed for analyte-based tests.", ex.Message);
    }

    [Fact]
    public async Task MasterData_TestAnalyte_HplcMultiAnalyte_OptionalLoq_And_SstCriteria()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, _, _, _, _, _, _) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var controller = CreateMasterDataController(db, fpUser);

        // Optional LOQ and criteria can be saved
        var createReq = new CreateTestAnalyteRequest(
            Element: "Vitamin B6",
            WavelengthNm: 290.0m,
            View: null,
            LoqMgPerL: 0.05m,
            DisplayOrder: 3,
            SstMaxRsdPercent: 1.5m,
            SstMinResolution: 2.5m,
            SstMaxTailingFactor: 1.8m,
            SstMinTheoreticalPlates: 3000m);

        var res = await controller.CreateTestAnalyte(test.Id, createReq);
        var ok = Assert.IsType<OkObjectResult>(res);
        var dto = Assert.IsType<ApiResponse<object>>(ok.Value).Data as TestAnalyteDto;
        Assert.NotNull(dto);
        Assert.Equal("Vitamin B6", dto.Element);
        Assert.Null(dto.View);
        Assert.Equal(0.05m, dto.LoqMgPerL);
        Assert.Equal(1.5m, dto.SstMaxRsdPercent);
        Assert.Equal(2.5m, dto.SstMinResolution);
        Assert.Equal(1.8m, dto.SstMaxTailingFactor);
        Assert.Equal(3000m, dto.SstMinTheoreticalPlates);

        // Update SST criteria
        var updateReq = new UpdateTestAnalyteRequest(
            SstMaxRsdPercent: 1.2m,
            SstMinTheoreticalPlates: 3500m);

        var updateRes = await controller.UpdateTestAnalyte(test.Id, dto.Id, updateReq);
        var okUpdate = Assert.IsType<OkObjectResult>(updateRes);
        var updatedDto = Assert.IsType<ApiResponse<object>>(okUpdate.Value).Data as TestAnalyteDto;
        Assert.NotNull(updatedDto);
        Assert.Equal(1.2m, updatedDto.SstMaxRsdPercent);
        Assert.Equal(3500m, updatedDto.SstMinTheoreticalPlates);
        Assert.Equal(2.5m, updatedDto.SstMinResolution); // unchanged
    }

    [Fact]
    public async Task MasterData_TestAnalyte_CalibrationCurve_RequiresView_And_RejectsSstCriteria()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var controller = CreateMasterDataController(db, fpUser);

        var calTest = new TestDefinition
        {
            Code = "ICP_CAL_TEST",
            DisplayName = "ICP Calibration Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.ElementalAssay,
            EquationType = EquationType.CalibrationCurve,
            MethodAbbreviation = "ICP-CAL",
            CalMinCorrelation = 0.999m,
            CalCorrelationType = CorrelationType.RSquared,
            CalMinStandards = 5,
            CalCheckRecoveryLowPercent = 90m,
            CalCheckRecoveryHighPercent = 110m,
            ReportedConcentrationBasis = ReportedConcentrationBasis.SamplePpm
        };
        db.TestDefinitions.Add(calTest);
        db.SaveChanges();

        // CalibrationCurve missing View -> throws
        var reqNoView = new CreateTestAnalyteRequest(
            Element: "Zinc",
            WavelengthNm: 213.8m,
            View: null,
            LoqMgPerL: 0.01m);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestAnalyte(calTest.Id, reqNoView));
        Assert.Equal("View is required for calibration curve tests.", ex1.Message);

        // CalibrationCurve with SST criteria -> throws
        var reqWithSst = new CreateTestAnalyteRequest(
            Element: "Zinc",
            WavelengthNm: 213.8m,
            View: AnalyteView.Axial,
            LoqMgPerL: 0.01m,
            SstMaxRsdPercent: 2.0m);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestAnalyte(calTest.Id, reqWithSst));
        Assert.Equal("SST criteria are not allowed for calibration curve tests.", ex2.Message);
    }

    [Fact]
    public async Task MasterData_TestAnalyte_Delete_DeactivatesWhenReferencedBySuitabilityRun()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var controller = CreateMasterDataController(db, fpUser);

        // Create suitability run referencing b1 and b2
        var sstService = TestServiceFactory.SystemSuitability(db);
        var runReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Comment: "Reference test",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.2m, 2.5m, 1.2m, 3200m)
            });

        await sstService.CreateAsync(runReq, fpUser.Id, "127.0.0.1");

        // Attempting to delete b1 should deactivate instead of hard delete
        var deleteRes = await controller.DeleteTestAnalyte(test.Id, b1.Id);
        Assert.IsType<OkObjectResult>(deleteRes);

        var storedB1 = await db.TestAnalytes.FindAsync(b1.Id);
        Assert.NotNull(storedB1);
        Assert.False(storedB1.IsActive);

        // Delete an unreferenced analyte -> hard deleted
        var unreferenced = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Vitamin B12",
            WavelengthNm = 361.0m,
            IsActive = true
        };
        db.TestAnalytes.Add(unreferenced);
        await db.SaveChangesAsync();

        var deleteUnrefRes = await controller.DeleteTestAnalyte(test.Id, unreferenced.Id);
        Assert.IsType<OkObjectResult>(deleteUnrefRes);
        Assert.False(await db.TestAnalytes.AnyAsync(a => a.Id == unreferenced.Id));
    }

    #endregion

    #region System Suitability Service Tests

    [Fact]
    public async Task Suitability_HplcMultiAnalyte_AllAnalytesPass_RunPassed()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var runReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Comment: "Passing run",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.2m, 2.5m, 1.2m, 3200m)
            });

        var run = await service.CreateAsync(runReq, fpUser.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Null(run.FailureReasons);
        Assert.Equal(2, run.Analytes.Count);

        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal("Vitamin B1", r1.AnalyteName);
        Assert.Equal(254.0m, r1.WavelengthNm);
        Assert.Equal(std1.Purity, r1.StandardPurityPercent);
        Assert.Equal(50.0m, r1.StandardWeightMg);
        Assert.Equal(100.0m, r1.StandardDilution);
        Assert.Equal(2500000m, r1.StandardMeanArea);
        Assert.True(r1.Passed);
        Assert.Null(r1.FailureReasons);

        var r2 = run.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.Equal("Vitamin B2", r2.AnalyteName);
        Assert.Equal(267.0m, r2.WavelengthNm);
        Assert.Equal(std2.Purity, r2.StandardPurityPercent);
        Assert.True(r2.Passed);
        Assert.Null(r2.FailureReasons);

        // First analyte values mapped to run-level standard columns
        Assert.Equal(std1.Id, run.ReferenceStandardMaterialId);
        Assert.Equal(std1.Purity, run.StandardPurityPercent);
        Assert.Equal(50.0m, run.StandardWeightMg);
        Assert.Equal(100.0m, run.StandardDilution);
        Assert.Equal(2500000m, run.StandardMeanArea);
    }

    [Fact]
    public async Task Suitability_HplcMultiAnalyte_OneAnalyteFails_RunFailed_PrefixedFailureReasons()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // b1 passes, b2 fails resolution (1.5 < min 2.0) and plates (2000 < min 2500)
        var runReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Comment: "Partial failure test",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.2m, 1.5m, 1.2m, 2000m)
            });

        var run = await service.CreateAsync(runReq, fpUser.Id, "127.0.0.1");

        Assert.False(run.Passed);
        Assert.NotNull(run.FailureReasons);
        Assert.Contains("Vitamin B2:", run.FailureReasons);

        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.True(r1.Passed);

        var r2 = run.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.False(r2.Passed);
        Assert.NotNull(r2.FailureReasons);
        Assert.Contains("Resolution", r2.FailureReasons);
        Assert.Contains("Theoretical plates", r2.FailureReasons);
    }

    [Fact]
    public async Task Suitability_HplcMultiAnalyte_NullCriteria_NotChecked()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // Clear all SST criteria on b1
        b1.SstMaxRsdPercent = null;
        b1.SstMaxTailingFactor = null;
        b1.SstMinTheoreticalPlates = null;
        b1.SstMinResolution = null;
        await db.SaveChangesAsync();

        // Even with high RSD and high tailing factor, b1 passes because criteria are null
        var runReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Comment: "Null criteria test",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 15.0m, null, 5.0m, 500m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.0m, 2.5m, 1.2m, 3200m)
            });

        var run = await service.CreateAsync(runReq, fpUser.Id, "127.0.0.1");

        Assert.True(run.Passed);
        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.True(r1.Passed);
    }

    [Fact]
    public async Task Suitability_HplcMultiAnalyte_ActiveAnalytesValidation()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // 1. Missing active analyte (only b1 provided, b2 omitted)
        var reqMissing = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m)
            });

        var exMissing = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(reqMissing, fpUser.Id, "127.0.0.1"));
        Assert.Contains("Missing analyte row for: Vitamin B2", exMissing.Message);

        // 2. Duplicate analyte (b1 twice)
        var reqDup = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m)
            });

        var exDup = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(reqDup, fpUser.Id, "127.0.0.1"));
        Assert.Contains("Duplicate analyte entry", exDup.Message);

        // 3. Unknown analyte from another test
        var otherTest = new TestDefinition
        {
            Code = "OTHER_TEST",
            DisplayName = "Other Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "OTHER"
        };
        db.TestDefinitions.Add(otherTest);
        var otherAnalyte = new TestAnalyte
        {
            TestDefinitionId = otherTest.Id,
            Element = "Other Analyte",
            WavelengthNm = 280.0m,
            IsActive = true
        };
        db.TestAnalytes.Add(otherAnalyte);
        await db.SaveChangesAsync();

        var reqUnknown = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(otherAnalyte.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.0m, 2.5m, 1.2m, 3200m)
            });

        var exUnknown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(reqUnknown, fpUser.Id, "127.0.0.1"));
        Assert.Contains("is not configured for test", exUnknown.Message);

        // 4. Inactive analyte included
        b2.IsActive = false;
        await db.SaveChangesAsync();

        var reqInactive = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.0m, 2.5m, 1.2m, 3200m)
            });

        var exInactive = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(reqInactive, fpUser.Id, "127.0.0.1"));
        Assert.Contains("is inactive", exInactive.Message);
    }

    [Fact]
    public async Task Suitability_SingleAnalyte_HplcAssay_Unchanged()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var test = new TestDefinition
        {
            Code = "VITC_SINGLE",
            DisplayName = "Single Vitamin C",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.HplcAssay,
            EquationType = EquationType.HplcAssay,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "VIT-C",
            SstMaxRsdPercent = 2.0m,
            SstMinTheoreticalPlates = 2000m
        };
        db.TestDefinitions.Add(test);

        var equip = new Equipment
        {
            Name = "HPLC Agilent 1260",
            Code = $"HPLC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Hplc,
            SectionId = fpSec.Id,
            CdsSoftware = CdsSoftware.AgilentOpenLab
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = "C18 150x4.6mm",
            Code = $"COL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SerialNumber = "SN-SINGLE",
            SectionId = fpSec.Id,
            IsActive = true,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.ChromatographyColumns.Add(col);

        var std = new Material
        {
            MaterialName = "Ascorbic Acid RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-AA-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 500m,
            QuantityRemaining = 500m,
            Unit = MaterialUnit.Gram,
            Location = "Standards Refrigerator",
            SectionId = fpSec.Id,
            Purity = 99.8m,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.Materials.Add(std);
        await db.SaveChangesAsync();

        var runReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.2m,
            TheoreticalPlates: 2500m,
            Password: "ValidPassword123!",
            Comment: "Single assay run");

        var run = await service.CreateAsync(runReq, fpUser.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Empty(run.Analytes);
        Assert.Equal(1.2m, run.RsdPercent);
        Assert.Equal(2500m, run.TheoreticalPlates);
    }

    [Fact]
    public async Task Suitability_HplcMultiAnalyte_GetReportDetails_IncludesAnalyteDetails()
    {
        await using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedHplcMultiPrerequisites(db, fpSec.Id, fpUser.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var runReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "ValidPassword123!",
            Comment: "Report test",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.2m, 2.5m, 1.2m, 3200m)
            });

        var run = await service.CreateAsync(runReq, fpUser.Id, "127.0.0.1");

        var report = await service.GetReportDetailsAsync(run.Id, fpUser.Id);
        Assert.NotNull(report);
        Assert.NotNull(report.Analytes);
        Assert.Equal(2, report.Analytes.Count);

        var repB1 = report.Analytes.First(a => a.AnalyteName == "Vitamin B1");
        Assert.Equal("Thiamine HCl RS", repB1.ReferenceStandardName);
        Assert.Equal(50.0m, repB1.StandardWeightMg);
        Assert.Equal(100.0m, repB1.StandardDilution);
        Assert.Equal(2500000m, repB1.StandardMeanArea);
        Assert.Equal(1.0m, repB1.RsdPercent);
        Assert.True(repB1.Passed);

        var repB2 = report.Analytes.First(a => a.AnalyteName == "Vitamin B2");
        Assert.Equal("Riboflavin RS", repB2.ReferenceStandardName);
        Assert.Equal(45.0m, repB2.StandardWeightMg);
        Assert.Equal(100.0m, repB2.StandardDilution);
        Assert.Equal(1800000m, repB2.StandardMeanArea);
        Assert.Equal(2.5m, repB2.Resolution);
        Assert.True(repB2.Passed);
    }

    #endregion
}
