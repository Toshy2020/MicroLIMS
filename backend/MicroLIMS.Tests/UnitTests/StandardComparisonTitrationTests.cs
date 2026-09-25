using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// SC-4 "titration mode" of the Standard-Comparison assay (ResponseMode.TitrationVolume).
// Production code under test: StandardComparisonCalculator, SystemSuitabilityService.CreateAsync,
// TestWorkflowEngine.RecordStandardComparisonResultAsync, MasterDataController test-definition
// create/update - see E:\MicroLIMS\MicroLIMS\CLAUDE.md task brief for the exact behaviours.
public class StandardComparisonTitrationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    #region 1. Calculator (StandardComparisonCalculator)

    [Fact]
    public void CalculatePreparationAssay_WithNonZeroBlank_MatchesHandCalculation()
    {
        /*
         HAND CALCULATION (SOP STM-PC-013 6.9.2.5 titration formula):
           blank (EP_blank)      = 0.10 mL
           EP_test (sample titre) = 9.90 mL  -> EP_test - blank = 9.90 - 0.10 = 9.80
           EP_std (standard titre) = 10.10 mL -> EP_std - blank = 10.10 - 0.10 = 10.00
           Response ratio = 9.80 / 10.00 = 0.98
           ActWt_std / ThWt_std   = 50.0 / 50.0 = 1.0
           ThWt_test / ActWt_test = 100.0 / 100.0 = 1.0
           Moisture correction    = (100 - 0) / 100 = 1.0
           Purity                 = 100
           %Assay = 0.98 x 1.0 x 1.0 x 1.0 x 100 = 98.0 %
        */
        decimal assay = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: 9.90m,
            responseStd: 10.10m,
            actWtStd: 50.0m,
            thWtStd: 50.0m,
            thWtTest: 100.0m,
            actWtTest: 100.0m,
            moisturePercent: 0m,
            purityPercent: 100m,
            blankTitre: 0.10m);

        Assert.Equal(98.0m, assay);
    }

    [Fact]
    public void CalculatePreparationAssay_BlankZero_EqualsPlainRatio_TC24()
    {
        // TC24: blank = 0 must behave identically to omitting the blank entirely
        // (plain Response_test / Response_std ratio, no titration adjustment).
        // Hand calc: ratio = 9.8 / 10.0 = 0.98; weights/moisture/purity all neutral -> 98.0%
        decimal withZeroBlank = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: 9.8m, responseStd: 10.0m,
            actWtStd: 50.0m, thWtStd: 50.0m,
            thWtTest: 100.0m, actWtTest: 100.0m,
            moisturePercent: 0m, purityPercent: 100m,
            blankTitre: 0m);

        decimal withoutBlank = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: 9.8m, responseStd: 10.0m,
            actWtStd: 50.0m, thWtStd: 50.0m,
            thWtTest: 100.0m, actWtTest: 100.0m,
            moisturePercent: 0m, purityPercent: 100m,
            blankTitre: null);

        Assert.Equal(98.0m, withZeroBlank);
        Assert.Equal(98.0m, withoutBlank);
        Assert.Equal(withoutBlank, withZeroBlank);
    }

    [Fact]
    public void CalculatePreparationAssay_TestTitreAtOrBelowBlank_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.CalculatePreparationAssay(
                responseTest: 5.0m, responseStd: 10.0m, // EP_test == blank
                actWtStd: 50.0m, thWtStd: 50.0m,
                thWtTest: 100.0m, actWtTest: 100.0m,
                moisturePercent: 0m, purityPercent: 100m,
                blankTitre: 5.0m));

        Assert.Contains("Sample titre must be greater than the blank titre.", ex.Message);
    }

    [Fact]
    public void CalculatePreparationAssay_StdTitreAtOrBelowBlank_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.CalculatePreparationAssay(
                responseTest: 6.0m, responseStd: 5.0m, // EP_std == blank
                actWtStd: 50.0m, thWtStd: 50.0m,
                thWtTest: 100.0m, actWtTest: 100.0m,
                moisturePercent: 0m, purityPercent: 100m,
                blankTitre: 5.0m));

        Assert.Contains("Standard titre must be greater than the blank titre.", ex.Message);
    }

    [Fact]
    public void CalculatePreparationAssay_NegativeBlank_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.CalculatePreparationAssay(
                responseTest: 9.0m, responseStd: 10.0m,
                actWtStd: 50.0m, thWtStd: 50.0m,
                thWtTest: 100.0m, actWtTest: 100.0m,
                moisturePercent: 0m, purityPercent: 100m,
                blankTitre: -0.1m));

        Assert.Contains("Blank titre must not be negative.", ex.Message);
    }

    [Fact]
    public void Calculate_TitrationVolume_NoBlank_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.Calculate(
                analyteName: "X", testAnalyteId: 1, systemSuitabilityRunAnalyteId: 1,
                standardTheoreticalWeightMg: 50m, standardActualWeightMg: 50m,
                standardPurityPercent: 100m, moisturePercent: 0m, standardMeanArea: 10m,
                spec: null!, preparations: null!, responses: null!,
                maxPreparationRsdPercent: null,
                responseMode: ResponseMode.TitrationVolume,
                blankTitreMl: null));

        Assert.Contains("Blank titre is required for titration.", ex.Message);
    }

    [Fact]
    public void Calculate_PeakArea_WithBlank_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.Calculate(
                analyteName: "X", testAnalyteId: 1, systemSuitabilityRunAnalyteId: 1,
                standardTheoreticalWeightMg: 50m, standardActualWeightMg: 50m,
                standardPurityPercent: 100m, moisturePercent: 0m, standardMeanArea: 1000000m,
                spec: null!, preparations: null!, responses: null!,
                maxPreparationRsdPercent: null,
                responseMode: ResponseMode.PeakArea,
                blankTitreMl: 0.5m));

        Assert.Contains("Blank titre applies only to titration.", ex.Message);
    }

    [Fact]
    public void Calculate_TitrationVolume_FullRun_MatchesHandCalculation_CalculationDataCarriesModeAndBlank()
    {
        var spec = new Specification
        {
            ParameterName = "Ascorbic Acid",
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 105.0m,
            LowerInclusive = true,
            UpperInclusive = true
        };

        var preps = new List<StandardComparisonPreparationInput>
        {
            new(100.0m, 100.0m, null),
            new(100.0m, 100.0m, null)
        };

        var responses = new List<StandardComparisonResponseInput>
        {
            new(TestAnalyteId: 1, PreparationIndex: 1, Response: 9.90m),  // EP_test 1
            new(TestAnalyteId: 1, PreparationIndex: 2, Response: 10.10m)  // EP_test 2
        };

        /*
         HAND CALCULATION (blank = 0.10 mL, EP_std mean = 10.10 mL, weights/moisture/purity all neutral):
           EP_std - blank = 10.10 - 0.10 = 10.00
           Prep 1: EP_test - blank = 9.90 - 0.10 = 9.80  -> ratio = 9.80 / 10.00 = 0.98
                   %Assay = 0.98 x (50.0/50.0) x (100.0/100.0) x ((100-0)/100) x 100 = 98.0 %
           Prep 2: EP_test - blank = 10.10 - 0.10 = 10.00 -> ratio = 10.00 / 10.00 = 1.00
                   %Assay = 1.00 x 1 x 1 x 1 x 100 = 100.0 %
           Mean = (98.0 + 100.0) / 2 = 99.0 %
        */
        var result = StandardComparisonCalculator.Calculate(
            analyteName: "Ascorbic Acid",
            testAnalyteId: 1,
            systemSuitabilityRunAnalyteId: 10,
            standardTheoreticalWeightMg: 50.0m,
            standardActualWeightMg: 50.0m,
            standardPurityPercent: 100m,
            moisturePercent: 0m,
            standardMeanArea: 10.10m,
            spec: spec,
            preparations: preps,
            responses: responses,
            maxPreparationRsdPercent: null,
            responseMode: ResponseMode.TitrationVolume,
            blankTitreMl: 0.10m);

        Assert.Equal(98.0m, result.Preparations[0].PercentAssay);
        Assert.Equal(100.0m, result.Preparations[1].PercentAssay);
        Assert.Equal(99.0m, result.ReportedValue);
        Assert.Equal("99.0 %", result.ReportedDisplay);
        Assert.Equal("WithinLimits", result.ComparisonStatus);

        Assert.Equal("TitrationVolume", result.CalculationData.ResponseMode);
        Assert.Equal(0.10m, result.CalculationData.BlankTitreMl);
        Assert.Equal(10.10m, result.CalculationData.StandardMeanArea);
    }

    #endregion

    #region 2. Run creation (SystemSuitabilityService.CreateAsync)

    private static (DocumentSection sec, User user) SeedSectionAndUser(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var sec = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(sec);

        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();

        var user = new User
        {
            Username = "titr_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Titration Analyst",
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!"),
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = sec.Id });
        db.SaveChanges();

        return (sec, user);
    }

    private static (
        DocumentSection sec, User user,
        TestDefinition titrationTest, TestDefinition peakAreaTest,
        Equipment titrator, Equipment hplc, ChromatographyColumn col, Material std,
        TestAnalyte titrationAnalyte, TestAnalyte peakAreaAnalyte)
        SeedRunCreationPrerequisites(MicroLimsDbContext db)
    {
        var (sec, user) = SeedSectionAndUser(db);

        var titrationTest = new TestDefinition
        {
            Code = $"TITR_{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DisplayName = "Titration Assay",
            SectionId = sec.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "TITR",
            ResponseMode = ResponseMode.TitrationVolume
        };
        var peakAreaTest = new TestDefinition
        {
            Code = $"PKAR_{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DisplayName = "Peak Area Assay",
            SectionId = sec.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "PKAR"
            // ResponseMode defaults to PeakArea
        };
        db.TestDefinitions.AddRange(titrationTest, peakAreaTest);

        var titrator = new Equipment
        {
            Name = "Titrator 1",
            Code = $"TTR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Titrator,
            SectionId = sec.Id
        };
        var hplc = new Equipment
        {
            Name = "HPLC 1",
            Code = $"HPLC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Hplc,
            SectionId = sec.Id
        };
        db.Equipment.AddRange(titrator, hplc);

        var col = new ChromatographyColumn
        {
            Name = "C18 Column",
            Code = $"COL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SerialNumber = "SN-TITR-01",
            SectionId = sec.Id,
            IsActive = true,
            CreatedByUserId = user.Id,
            LastModifiedByUserId = user.Id
        };
        db.ChromatographyColumns.Add(col);

        var std = new Material
        {
            MaterialName = "Ascorbic Acid RS",
            MaterialType = MaterialType.ReferenceStandard,
            BatchNumber = "LOT-TITR-01",
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            SectionId = sec.Id,
            Purity = 99.5m,
            CreatedByUserId = user.Id,
            LastModifiedByUserId = user.Id
        };
        db.Materials.Add(std);
        db.SaveChanges();

        var titrationAnalyte = new TestAnalyte
        {
            TestDefinitionId = titrationTest.Id,
            Element = "Ascorbic Acid",
            WavelengthNm = 0m,
            DisplayOrder = 1,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 1.5m // configured, but must be ignored for a titration run
        };
        var peakAreaAnalyte = new TestAnalyte
        {
            TestDefinitionId = peakAreaTest.Id,
            Element = "Ascorbic Acid",
            WavelengthNm = 254.0m,
            DisplayOrder = 1,
            IsActive = true,
            SstMaxRsdPercent = 2.0m
        };
        db.TestAnalytes.AddRange(titrationAnalyte, peakAreaAnalyte);
        db.SaveChanges();

        return (sec, user, titrationTest, peakAreaTest, titrator, hplc, col, std, titrationAnalyte, peakAreaAnalyte);
    }

    [Fact]
    public async Task CreateAsync_TitrationTest_HplcEquipment_Rejected()
    {
        await using var db = NewDb();
        var (_, user, titrationTest, _, _, hplc, _, _, _, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: titrationTest.Id,
            EquipmentId: hplc.Id,
            ChromatographyColumnId: null,
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Selected equipment must be a titrator.", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_TitrationTest_ColumnSupplied_Rejected()
    {
        await using var db = NewDb();
        var (_, user, titrationTest, _, titrator, _, col, _, _, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: titrationTest.Id,
            EquipmentId: titrator.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Titration runs do not use a chromatography column.", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_TitrationTest_MissingBlank_Rejected()
    {
        await using var db = NewDb();
        var (_, user, titrationTest, _, titrator, _, _, std, titrationAnalyte, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var row = new CreateSystemSuitabilityRunAnalyteRequest(
            TestAnalyteId: titrationAnalyte.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 0m,
            StandardMeanArea: 0m,
            Responses: new List<decimal> { 10.10m, 10.10m, 10.10m });
            // BlankTitreMl omitted -> null

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: titrationTest.Id,
            EquipmentId: titrator.Id,
            ChromatographyColumnId: null,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest> { row });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Blank titre is required for analyte", ex.Message);
        Assert.Contains("Ascorbic Acid", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_TitrationTest_TitreAtOrBelowBlank_Rejected()
    {
        await using var db = NewDb();
        var (_, user, titrationTest, _, titrator, _, _, std, titrationAnalyte, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var row = new CreateSystemSuitabilityRunAnalyteRequest(
            TestAnalyteId: titrationAnalyte.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 0m,
            StandardMeanArea: 0m,
            Responses: new List<decimal> { 10.10m }, // equal to the blank below
            BlankTitreMl: 10.10m);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: titrationTest.Id,
            EquipmentId: titrator.Id,
            ChromatographyColumnId: null,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest> { row });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("must be greater than the blank titre", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_TitrationTest_ResolutionSupplied_Rejected()
    {
        await using var db = NewDb();
        var (_, user, titrationTest, _, titrator, _, _, std, titrationAnalyte, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var row = new CreateSystemSuitabilityRunAnalyteRequest(
            TestAnalyteId: titrationAnalyte.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 0m,
            StandardMeanArea: 0m,
            Resolution: 1.0m,
            Responses: new List<decimal> { 10.10m, 10.10m },
            BlankTitreMl: 0.10m);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: titrationTest.Id,
            EquipmentId: titrator.Id,
            ChromatographyColumnId: null,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest> { row });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Resolution, tailing and plates do not apply to a titration run", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_TitrationTest_Titrator_PassesOnRsdAlone_PersistsBlankAndNullColumn()
    {
        await using var db = NewDb();
        var (_, user, titrationTest, _, titrator, _, _, std, titrationAnalyte, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        // Identical titres -> computed RSD = 0%, well within the analyte's max 2.0%.
        // titrationAnalyte.SstMinResolution = 1.5 is configured but must be ignored for titration.
        var row = new CreateSystemSuitabilityRunAnalyteRequest(
            TestAnalyteId: titrationAnalyte.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 0m,
            StandardMeanArea: 0m,
            Responses: new List<decimal> { 10.10m, 10.10m, 10.10m },
            BlankTitreMl: 0.10m);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: titrationTest.Id,
            EquipmentId: titrator.Id,
            ChromatographyColumnId: null,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest> { row });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Null(run.FailureReasons);
        Assert.Null(run.ChromatographyColumnId);

        var runAnalyte = run.Analytes.Single();
        Assert.True(runAnalyte.Passed);
        Assert.Null(runAnalyte.FailureReasons);
        Assert.Equal(0.10m, runAnalyte.BlankTitreMl);
        Assert.Equal(10.10m, runAnalyte.StandardMeanArea); // mean of identical titres
        Assert.Equal(0m, runAnalyte.ComputedRsdPercent);
        Assert.Null(runAnalyte.Resolution);

        // Verify persisted values from a fresh read
        var reloaded = await db.SystemSuitabilityRuns.Include(r => r.Analytes).FirstAsync(r => r.Id == run.Id);
        Assert.Null(reloaded.ChromatographyColumnId);
        Assert.Equal(0.10m, reloaded.Analytes.Single().BlankTitreMl);
    }

    [Fact]
    public async Task CreateAsync_PeakAreaTest_BlankSupplied_Rejected()
    {
        await using var db = NewDb();
        var (_, user, _, peakAreaTest, _, hplc, col, std, _, peakAreaAnalyte) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var row = new CreateSystemSuitabilityRunAnalyteRequest(
            TestAnalyteId: peakAreaAnalyte.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.0m,
            BlankTitreMl: 0.10m);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: peakAreaTest.Id,
            EquipmentId: hplc.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest> { row });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Blank titre applies only to titration runs", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_PeakAreaTest_MissingColumn_Rejected()
    {
        await using var db = NewDb();
        var (_, user, _, peakAreaTest, _, hplc, _, _, _, _) = SeedRunCreationPrerequisites(db);
        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: peakAreaTest.Id,
            EquipmentId: hplc.Id,
            ChromatographyColumnId: null,
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Chromatography column is required.", ex.Message);
    }

    #endregion

    #region 3. Entry (TestWorkflowEngine.RecordStandardComparisonResultAsync)

    private static (
        DocumentSection fpSec, User fpUser, TestDefinition testDef, TestAnalyte analyte,
        Sample sample, TestOrder order, Specification spec, SystemSuitabilityRun sstRun)
        SetupEntryScenario(MicroLimsDbContext db, ResponseMode responseMode, decimal? blankTitreMl)
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

        var fpUser = new User
        {
            Username = "titr_entry_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Titration Entry Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!"),
            IsActive = true
        };
        db.Users.Add(fpUser);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.SaveChanges();

        var methodAbbr = "TT" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Assay",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            ResponseMode = responseMode,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        db.SaveChanges();

        var stage = new ProductionStage { Name = "Finished Product Stage", Role = ProductionStageRole.Finished, IsActive = true };
        db.ProductionStages.Add(stage);
        db.SaveChanges();

        var stageReplicate = new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Finished,
            StandardReplicates = 3,
            SampleReplicates = 1
        };
        db.TestDefinitionStageReplicates.Add(stageReplicate);
        db.SaveChanges();

        var isTitration = responseMode == ResponseMode.TitrationVolume;
        var equip = new Equipment
        {
            Name = isTitration ? "Titrator 1" : "HPLC 1",
            Code = $"EQ-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = isTitration ? EquipmentType.Titrator : EquipmentType.Hplc,
            SectionId = fpSec.Id,
            CdsSoftware = isTitration ? null : CdsSoftware.WatersEmpower3
        };
        db.Equipment.Add(equip);
        db.SaveChanges();

        var stdMat = new Material
        {
            MaterialName = "Ascorbic Acid RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-TT",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = fpSec.Id,
            Purity = 100m,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.Materials.Add(stdMat);
        db.SaveChanges();

        var analyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Ascorbic Acid",
            WavelengthNm = 0m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(analyte);
        db.SaveChanges();

        var item = new Item
        {
            Code = $"ITEM-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Name = "Vitamin C Tablets",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Ascorbic Acid Assay",
            TestAnalyteId = analyte.Id,
            LimitType = LimitType.Range,
            LowerLimit = 95.0m,
            UpperLimit = 105.0m,
            LowerInclusive = true,
            UpperInclusive = true,
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);
        db.SaveChanges();

        var cause = new CauseOfTesting { Name = "Commercial Release", IsActive = true };
        db.CausesOfTesting.Add(cause);
        db.SaveChanges();

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            ReceivedAt = DateTime.UtcNow.AddDays(-1),
            CauseOfTesting = cause,
            ReceivedByUserId = fpUser.Id,
            ProductionStageId = stage.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = fpSec.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var signature = new ElectronicSignature
        {
            UserId = fpUser.Id,
            UserFullNameSnapshot = fpUser.FullName,
            UsernameSnapshot = fpUser.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.SuitabilityRunPerformed,
            SignedAt = DateTime.UtcNow,
            EntityType = "SystemSuitabilityRun",
            EntityId = 0
        };
        db.ElectronicSignatures.Add(signature);
        db.SaveChanges();

        var sstRun = new SystemSuitabilityRun
        {
            Code = $"{methodAbbr} S.S 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            EquipmentId = equip.Id,
            ChromatographyColumnId = null,
            ReferenceStandardMaterialId = stdMat.Id,
            StandardWeightMg = 50.0m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 100m,
            StandardMeanArea = 10.10m,
            Passed = true,
            PerformedByUserId = fpUser.Id,
            PerformedAt = DateTime.UtcNow,
            SignatureId = signature.Id
        };
        db.SystemSuitabilityRuns.Add(sstRun);
        db.SaveChanges();

        var runAnalyte = new SystemSuitabilityRunAnalyte
        {
            SystemSuitabilityRunId = sstRun.Id,
            TestAnalyteId = analyte.Id,
            AnalyteName = analyte.Element,
            WavelengthNm = analyte.WavelengthNm,
            ReferenceStandardMaterialId = stdMat.Id,
            StandardWeightMg = 50.0m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 100m,
            StandardMeanArea = 10.10m,
            Passed = true,
            BlankTitreMl = blankTitreMl
        };
        db.SystemSuitabilityRunAnalytes.Add(runAnalyte);
        db.SaveChanges();

        order.SystemSuitabilityRunId = sstRun.Id;
        db.SaveChanges();

        return (fpSec, fpUser, testDef, analyte, sample, order, spec, sstRun);
    }

    [Fact]
    public async Task RecordResult_Titration_EndToEnd_ReadingKindAndCalculationJsonAndReportedValue()
    {
        await using var db = NewDb();
        var (_, fpUser, _, analyte, _, order, _, sstRun) =
            SetupEntryScenario(db, ResponseMode.TitrationVolume, blankTitreMl: 0.10m);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100.0m, 100.0m, null) },
            new List<StandardComparisonResponseInput> { new(analyte.Id, 1, 9.90m) }, // EP_test
            "ValidPassword123!");

        var result = await engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id);
        Assert.NotNull(result);

        var savedAnalysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(p => p.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);
        Assert.NotNull(savedAnalysis);

        var param = savedAnalysis!.ParameterResults.Single();
        Assert.Single(param.Readings);
        var reading = param.Readings[0];
        Assert.Equal(ReadingKind.Titration, reading.Kind);
        Assert.Equal(9.90m, reading.Value1); // raw sample titre, not net of blank

        /*
         HAND CALCULATION:
           blank = 0.10, EP_std (mean) = 10.10 -> EP_std - blank = 10.00
           EP_test = 9.90 -> EP_test - blank = 9.80
           Response ratio = 9.80 / 10.00 = 0.98
           Std weight ratio = ActWtStd/ThWtStd = 50.0/50.0 = 1.0
           Sample weight ratio = ThWtTest/ActWtTest = 100.0/100.0 = 1.0
           Moisture correction = (100-0)/100 = 1.0
           Purity = 100
           %Assay = 0.98 x 1.0 x 1.0 x 1.0 x 100 = 98.0
        */
        Assert.Equal(98.0m, param.ReportedValue);
        Assert.Equal("98.0 %", param.ReportedDisplay);
        Assert.Equal("WithinLimits", param.ComparisonStatus);
        Assert.Equal(98.0m, reading.ComputedValue);

        var calc = JsonSerializer.Deserialize<StandardComparisonCalculationData>(
            param.CalculationJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(calc);
        Assert.Equal("TitrationVolume", calc!.ResponseMode);
        Assert.Equal(0.10m, calc.BlankTitreMl);
    }

    [Fact]
    public async Task RecordResult_TitrationTest_LinkedToPeakAreaRunAnalyte_Rejected()
    {
        await using var db = NewDb();
        // Titration test, but the linked run analyte has no blank -> not actually a titration run.
        var (_, fpUser, _, analyte, _, order, _, sstRun) =
            SetupEntryScenario(db, ResponseMode.TitrationVolume, blankTitreMl: null);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100.0m, 100.0m, null) },
            new List<StandardComparisonResponseInput> { new(analyte.Id, 1, 9.90m) },
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));
        Assert.Contains("has no blank titre - it is not a titration run.", ex.Message);
    }

    [Fact]
    public async Task RecordResult_PeakAreaTest_LinkedToTitrationRunAnalyte_Rejected()
    {
        await using var db = NewDb();
        // Peak-area test, but the linked run analyte carries a blank -> actually a titration run.
        var (_, fpUser, _, analyte, _, order, _, sstRun) =
            SetupEntryScenario(db, ResponseMode.PeakArea, blankTitreMl: 0.10m);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100.0m, 100.0m, null) },
            new List<StandardComparisonResponseInput> { new(analyte.Id, 1, 2000000m) },
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));
        Assert.Contains("is a titration run, but this test measures peak area.", ex.Message);
    }

    #endregion

    #region 4. Test Master (MasterDataController)

    private static MasterDataController BuildController(MicroLimsDbContext db)
    {
        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        return new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService);
    }

    private static void Authenticate(MasterDataController controller, int userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"))
            }
        };
    }

    [Fact]
    public async Task CreateTestDefinition_ResponseModeTitration_OnNonStandardComparison_Rejected()
    {
        using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var controller = BuildController(db);
        Authenticate(controller, user.Id);

        var req = new CreateTestDefinitionRequest(
            Code: "OBS_TITR_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            DisplayName: "Observation Titration",
            SectionId: sec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.None,
            RequiresSystemSuitability: false,
            ResponseMode: ResponseMode.TitrationVolume);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("Response mode applies only to standard-comparison tests.", ex.Message);
    }

    [Fact]
    public async Task UpdateTestDefinition_ResponseModeChange_AllowedWithoutRuns_RejectedOnceRunExists()
    {
        using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var controller = BuildController(db);
        Authenticate(controller, user.Id);

        var createReq = new CreateTestDefinitionRequest(
            Code: "SC_TITR_UPD_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            DisplayName: "SC Titration Update Test",
            SectionId: sec.Id,
            WorkflowType: WorkflowType.StandardComparison,
            EquationType: EquationType.StandardComparison,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "SCTU",
            ResponseMode: ResponseMode.PeakArea);

        var createResult = await controller.CreateTestDefinition(createReq) as OkObjectResult;
        Assert.NotNull(createResult);
        var created = ((ApiResponse<object>)createResult!.Value!).Data as TestDefinition;
        Assert.NotNull(created);
        Assert.Equal(ResponseMode.PeakArea, created!.ResponseMode);

        // No suitability runs exist yet -> ResponseMode change is allowed.
        var updateReq1 = new UpdateTestDefinitionRequest(
            Code: created.Code,
            DisplayName: created.DisplayName,
            ResponseMode: ResponseMode.TitrationVolume);

        var updateResult1 = await controller.UpdateTestDefinition(created.Id, updateReq1) as OkObjectResult;
        Assert.NotNull(updateResult1);

        var afterFirstUpdate = await db.TestDefinitions.FindAsync(created.Id);
        Assert.Equal(ResponseMode.TitrationVolume, afterFirstUpdate!.ResponseMode);

        // Now create a suitability run for this test definition.
        var titrator = new Equipment
        {
            Name = "Titrator 1",
            Code = $"TTR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Titrator,
            SectionId = sec.Id
        };
        db.Equipment.Add(titrator);

        var std = new Material
        {
            MaterialName = "Std RS",
            MaterialType = MaterialType.ReferenceStandard,
            BatchNumber = "LOT-UPD-01",
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            SectionId = sec.Id,
            Purity = 99m,
            CreatedByUserId = user.Id,
            LastModifiedByUserId = user.Id
        };
        db.Materials.Add(std);
        db.SaveChanges();

        var signature = new ElectronicSignature
        {
            UserId = user.Id,
            UserFullNameSnapshot = user.FullName,
            UsernameSnapshot = user.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.SuitabilityRunPerformed,
            SignedAt = DateTime.UtcNow,
            EntityType = "TestDefinition",
            EntityId = created.Id
        };
        db.ElectronicSignatures.Add(signature);
        db.SaveChanges();

        var run = new SystemSuitabilityRun
        {
            Code = "SCTU S.S 01/092026",
            TestDefinitionId = created.Id,
            SectionId = sec.Id,
            EquipmentId = titrator.Id,
            ChromatographyColumnId = null,
            ReferenceStandardMaterialId = std.Id,
            StandardPurityPercent = 99m,
            StandardWeightMg = 50m,
            StandardDilution = 100m,
            StandardMeanArea = 10m,
            Passed = true,
            PerformedByUserId = user.Id,
            PerformedAt = DateTime.UtcNow,
            SignatureId = signature.Id
        };
        db.SystemSuitabilityRuns.Add(run);
        db.SaveChanges();

        // ResponseMode change is now rejected.
        var updateReq2 = new UpdateTestDefinitionRequest(
            Code: created.Code,
            DisplayName: created.DisplayName,
            ResponseMode: ResponseMode.PeakArea);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.UpdateTestDefinition(created.Id, updateReq2));
        Assert.Contains("Response mode cannot be changed once suitability runs exist for this test.", ex.Message);

        // Updating other fields (ResponseMode omitted -> unchanged) still succeeds.
        var updateReq3 = new UpdateTestDefinitionRequest(
            Code: created.Code,
            DisplayName: "SC Titration Update Test (renamed)");
        var updateResult3 = await controller.UpdateTestDefinition(created.Id, updateReq3) as OkObjectResult;
        Assert.NotNull(updateResult3);
    }

    #endregion
}
