using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class CalibrationCurveSliceS1Tests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection fpSec, DocumentSection microSec, User fpUser, User microUser, User adminUser) SeedSectionsAndUsers(MicroLimsDbContext db)
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

        var adminRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SystemAdministrator)
            ?? new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        if (adminRole.Id == 0) { db.Roles.Add(adminRole); db.SaveChanges(); }

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

        var microUser = new User
        {
            Username = "micro_analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Micro Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(microUser);

        var adminUser = new User
        {
            Username = "admin_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Admin User",
            RoleId = adminRole.Id,
            Role = adminRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(adminUser);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = microUser.Id, DepartmentId = microSec.DepartmentId, SectionId = microSec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = adminUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.SaveChanges();

        return (fpSec, microSec, fpUser, microUser, adminUser);
    }

    private static (TestDefinition test, Equipment equip, Material standard, TestAnalyte znAnalyte, TestAnalyte caAnalyte) SeedIcpOesTestData(
        MicroLimsDbContext db, int sectionId, int userId)
    {
        var test = new TestDefinition
        {
            Code = "ICP-MIN",
            DisplayName = "ICP-OES Minerals Assay",
            SectionId = sectionId,
            WorkflowType = WorkflowType.ElementalAssay,
            EquationType = EquationType.CalibrationCurve,
            MethodAbbreviation = "ICP-MIN",
            CalibrationEntryMode = CalibrationEntryMode.InstrumentReported,
            CalMinCorrelation = 0.999m,
            CalCorrelationType = CorrelationType.RSquared,
            CalMinStandards = 3,
            CalCheckRecoveryLowPercent = 90.0m,
            CalCheckRecoveryHighPercent = 110.0m,
            CalBlankMax = 0.005m,
            CalIsRecoveryLowPercent = 80.0m,
            CalIsRecoveryHighPercent = 120.0m,
            CalRequireBlank = true,
            CalRequireIcv = true,
            CalRequireCcv = false,
            CalRequireInternalStandard = false,
            ReportedConcentrationBasis = ReportedConcentrationBasis.SamplePpm,
            CalMaxRunAgeHours = 24
        };
        db.TestDefinitions.Add(test);

        db.SaveChanges();

        var znAnalyte = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Zn",
            WavelengthNm = 213.857m,
            View = AnalyteView.Axial,
            LoqMgPerL = 0.005m,
            DisplayOrder = 1,
            IsActive = true
        };
        var caAnalyte = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Ca",
            WavelengthNm = 317.933m,
            View = AnalyteView.Radial,
            LoqMgPerL = 0.010m,
            DisplayOrder = 2,
            IsActive = true
        };
        db.TestAnalytes.AddRange(znAnalyte, caAnalyte);

        var equip = new Equipment
        {
            Name = "PerkinElmer Avio 500 ICP-OES",
            Code = "ICP-01",
            Type = EquipmentType.IcpOes,
            SectionId = sectionId,
            Vendor = "PerkinElmer",
            CdsSoftware = CdsSoftware.PerkinElmerSyngistix
        };
        db.Equipment.Add(equip);

        var standard = new Material
        {
            MaterialName = "Multi-Element Calibration Standard 3",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "PerkinElmer",
            BatchNumber = "LOT-PE-STD-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Milliliter,
            Location = "Standards Cabinet",
            SectionId = sectionId,
            Purity = 99.9m,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.Materials.Add(standard);
        db.SaveChanges();

        return (test, equip, standard, znAnalyte, caAnalyte);
    }

    private static MemoryStream CreateDummyPdfStream(string content = "%PDF-1.4 Dummy ICP Report Content")
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    #region Spec Test Cases: TC2, TC3, TC4, TC5, TC9, TC10

    [Fact]
    public async Task TC2_Recovery92Point4Percent_Passes()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "TC2 test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        // 92.4% recovery: measured 0.924, nominal 1.0 -> 92.4%
                        new(CalibrationCheckType.Icv, 2, 1.0m, 0.924m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Equal(1, run.AnalytesPassed);
        Assert.Equal(1, run.AnalytesTotal);
        var analyte = run.Analytes.Single();
        Assert.True(analyte.Passed);
        Assert.Null(analyte.FailureReasons);
        var icvCheck = analyte.Checks.First(c => c.CheckType == CalibrationCheckType.Icv);
        Assert.True(icvCheck.Passed);
        Assert.Equal(92.4m, icvCheck.RecoveryPercent);
    }

    [Fact]
    public async Task TC3_Recovery89Point0Percent_FailsAnalyteAndRun()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "TC3 test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        // 89.0% recovery: measured 0.89, nominal 1.0 -> below 90.0%
                        new(CalibrationCheckType.Icv, 2, 1.0m, 0.890m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.False(run.Passed);
        Assert.Equal(0, run.AnalytesPassed);
        Assert.Equal(1, run.AnalytesTotal);
        var analyte = run.Analytes.Single();
        Assert.False(analyte.Passed);
        Assert.NotNull(analyte.FailureReasons);
        Assert.Contains("89.00%", analyte.FailureReasons);
        var icvCheck = analyte.Checks.First(c => c.CheckType == CalibrationCheckType.Icv);
        Assert.False(icvCheck.Passed);
    }

    [Fact]
    public async Task TC4_Correlation0Point9989_Below0Point999_Fails()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "TC4 test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    // Criterion is RSquared = 0.999; run gives 0.9989 RSquared
                    CorrelationValue: 0.9989m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.False(run.Passed);
        var analyte = run.Analytes.Single();
        Assert.False(analyte.Passed);
        Assert.Contains("below minimum limit", analyte.FailureReasons);
    }

    [Fact]
    public async Task TC5_Recovery90Point0And110Point0_BothPass()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, caAnalyte) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "TC5 boundary test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        // 90.0% boundary
                        new(CalibrationCheckType.Icv, 2, 1.0m, 0.900m)
                    }
                ),
                new(
                    TestAnalyteId: caAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.002m),
                        // 110.0% boundary
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.100m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Equal(2, run.AnalytesPassed);
        Assert.Equal(2, run.AnalytesTotal);
        Assert.All(run.Analytes, a => Assert.True(a.Passed));
    }

    [Fact]
    public async Task TC9_ExpiredStandard_SavesRunWithFailingAnalyteGate()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        // Expire standard
        standard.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "TC9 expired standard test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        // Run is saved, but gate fails
        Assert.False(run.Passed);
        Assert.Equal(0, run.AnalytesPassed);
        var analyte = run.Analytes.Single();
        Assert.False(analyte.Passed);
        Assert.Contains("standard expired / no expiry date", analyte.FailureReasons);
    }

    [Fact]
    public async Task TC10_NoReport_ControllerRejectsWithBadRequest()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var controller = new CalibrationRunController(service);

        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "TC10 test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Icv, 1, 1.0m, 1.0m)
                    }
                )
            });

        var payload = JsonSerializer.Serialize(request);

        // Missing report file
        var result = await controller.Create(payload, null);
        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(badReq.Value);
        Assert.False(response.Success);
        Assert.Contains("A calibration report file is required.", response.Message);
    }

    #endregion

    #region Spec Test Cases: Conversion, Independence, Code Format, Missing Expiry, IS window, Non-ICP

    [Fact]
    public void RVsRSquared_Conversion_EvaluatesCorrectly()
    {
        // Case 1: Criterion is RSquared = 0.9990, run provides R = 0.9989
        // r² = 0.9989 * 0.9989 = 0.99780121 < 0.9990 -> Fail
        var (p1, r1) = CalibrationRunService.EvaluateCorrelation(
            0.9989m, CorrelationType.R, 0.9990m, CorrelationType.RSquared);
        Assert.False(p1);
        Assert.NotNull(r1);

        // Case 2: Criterion is RSquared = 0.9980, run provides R = 0.9991
        // r² = 0.9991 * 0.9991 = 0.99820081 >= 0.9980 -> Pass
        var (p2, r2) = CalibrationRunService.EvaluateCorrelation(
            0.9991m, CorrelationType.R, 0.9980m, CorrelationType.RSquared);
        Assert.True(p2);
        Assert.Null(r2);

        // Case 3: Criterion is R = 0.9990, run provides RSquared = 0.9985
        // r = sqrt(0.9985) = 0.9992497 >= 0.9990 -> Pass
        var (p3, r3) = CalibrationRunService.EvaluateCorrelation(
            0.9985m, CorrelationType.RSquared, 0.9990m, CorrelationType.R);
        Assert.True(p3);
        Assert.Null(r3);

        // Case 4: Criterion is R = 0.9990, run provides RSquared = 0.9970
        // r = sqrt(0.9970) = 0.998498 < 0.9990 -> Fail
        var (p4, r4) = CalibrationRunService.EvaluateCorrelation(
            0.9970m, CorrelationType.RSquared, 0.9990m, CorrelationType.R);
        Assert.False(p4);
        Assert.NotNull(r4);
    }

    [Fact]
    public async Task PerAnalyteIndependence_ZnFails_CaPasses_RunFails()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, caAnalyte) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "Independence test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                // Zn: bad recovery 85% -> Fail
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 0.850m)
                    }
                ),
                // Ca: good recovery 100% -> Pass
                new(
                    TestAnalyteId: caAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.000m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.False(run.Passed);
        Assert.Equal(1, run.AnalytesPassed);
        Assert.Equal(2, run.AnalytesTotal);

        var zn = run.Analytes.First(a => a.Element == "Zn");
        Assert.False(zn.Passed);

        var ca = run.Analytes.First(a => a.Element == "Ca");
        Assert.True(ca.Passed);
    }

    [Fact]
    public async Task CodeFormatAndSequence_GeneratesCalInfixAndSequentialNumbers()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);

        var createReq = () => new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "Seq test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var s1 = CreateDummyPdfStream();
        var run1 = await service.CreateAsync(createReq(), s1, "report1.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        using var s2 = CreateDummyPdfStream();
        var run2 = await service.CreateAsync(createReq(), s2, "report2.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        var date = DateTime.UtcNow;
        var expectedPrefix1 = $"ICP-MIN CAL 01/{date:MM}{date:yyyy}";
        var expectedPrefix2 = $"ICP-MIN CAL 02/{date:MM}{date:yyyy}";

        Assert.Equal(expectedPrefix1, run1.Code);
        Assert.Equal(expectedPrefix2, run2.Code);
    }

    [Fact]
    public async Task MissingExpiry_StandardHasNoExpiryDate_GateFails()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        standard.ExpiryDate = null;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "Missing expiry test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.False(run.Passed);
        var analyte = run.Analytes.Single();
        Assert.False(analyte.Passed);
        Assert.Contains("standard expired / no expiry date", analyte.FailureReasons);
    }

    [Fact]
    public async Task InternalStandard_WithoutConfiguredWindow_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        // Remove configured IS window
        test.CalIsRecoveryLowPercent = null;
        test.CalIsRecoveryHighPercent = null;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "IS without window test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m),
                        new(CalibrationCheckType.InternalStandard, 3, 1.0m, 0.95m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1"));

        Assert.Contains("no internal standard recovery window is configured", ex.Message);
    }

    [Fact]
    public async Task NonIcpEquipment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, _, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        // Add non-ICP equipment (e.g. HPLC)
        var hplc = new Equipment
        {
            Name = "HPLC-01",
            Code = "HPLC-01",
            Type = EquipmentType.Hplc,
            SectionId = fpSec.Id,
            CdsSoftware = CdsSoftware.ShimadzuLabSolutions
        };
        db.Equipment.Add(hplc);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: hplc.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "Non-ICP equip test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1"));

        Assert.Equal("Selected equipment must be an ICP-OES instrument.", ex.Message);
    }

    #endregion

    #region MasterData Controller & TestAnalyte CRUD Tests

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

    [Fact]
    public async Task MasterData_CreateTestDefinition_CalibrationCurve_Validations()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var controller = CreateMasterDataController(db, fpUser);

        // Missing method abbreviation
        var req1 = new CreateTestDefinitionRequest(
            Code: "TEST-CAL-1",
            DisplayName: "Test Cal 1",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.ElementalAssay,
            EquationType: EquationType.CalibrationCurve,
            MethodAbbreviation: null,
            CalMinCorrelation: 0.999m,
            CalCorrelationType: CorrelationType.RSquared,
            CalMinStandards: 3,
            CalCheckRecoveryLowPercent: 90m,
            CalCheckRecoveryHighPercent: 110m,
            ReportedConcentrationBasis: ReportedConcentrationBasis.SamplePpm);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Method abbreviation is required", ex1.Message);

        // Wrong workflow type (not ElementalAssay)
        var req2 = req1 with { MethodAbbreviation = "CAL-MTH", WorkflowType = WorkflowType.Observation };
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Workflow type must be ElementalAssay", ex2.Message);

        // Low percent > High percent
        var req3 = req1 with { MethodAbbreviation = "CAL-MTH", CalCheckRecoveryLowPercent = 115m, CalCheckRecoveryHighPercent = 90m };
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req3));
        Assert.Contains("Check recovery low percent must be less than or equal to high percent.", ex3.Message);

        // Valid creation succeeds
        var reqValid = req1 with { MethodAbbreviation = "CAL-MTH" };
        var res = await controller.CreateTestDefinition(reqValid);
        var okRes = Assert.IsType<OkObjectResult>(res);
        var apiRes = Assert.IsType<ApiResponse<object>>(okRes.Value);
        Assert.True(apiRes.Success);
    }

    [Fact]
    public async Task MasterData_Equipment_IcpOes_RequiresCdsSoftware()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var controller = CreateMasterDataController(db, fpUser);

        // ICP-OES without CdsSoftware throws
        var reqWithout = new CreateEquipmentRequest(
            Name: "ICP Without CDS",
            Code: "ICP-NO-CDS",
            Type: EquipmentType.IcpOes,
            Location: "Lab 2",
            SetPointTemperature: null,
            CalibrationDueDate: null,
            Vendor: "PerkinElmer",
            CdsSoftware: null,
            ConnectionSettings: null,
            SectionId: fpSec.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateEquipment(reqWithout));
        Assert.Equal("CDS Software is required for ICP-OES equipment.", ex.Message);

        // ICP-OES with CdsSoftware succeeds
        var reqWith = reqWithout with { Code = "ICP-WITH-CDS", CdsSoftware = CdsSoftware.PerkinElmerSyngistix };
        var res = await controller.CreateEquipment(reqWith);
        var ok = Assert.IsType<OkObjectResult>(res);
        var api = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.True(api.Success);
    }

    [Fact]
    public async Task MasterData_TestAnalyte_Crud_And_DeactivationWhenUsed()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);
        var controller = CreateMasterDataController(db, fpUser);

        // Create new analyte (Mg)
        var createReq = new CreateTestAnalyteRequest("Mg", 285.213m, AnalyteView.Radial, 0.002m, 3);
        var createRes = await controller.CreateTestAnalyte(test.Id, createReq);
        var okCreate = Assert.IsType<OkObjectResult>(createRes);
        var dto = Assert.IsType<ApiResponse<object>>(okCreate.Value).Data as TestAnalyteDto;
        Assert.NotNull(dto);
        Assert.Equal("Mg", dto.Element);

        // Update analyte
        var updateReq = new UpdateTestAnalyteRequest(LoqMgPerL: 0.003m);
        var updateRes = await controller.UpdateTestAnalyte(test.Id, dto.Id, updateReq);
        var okUpdate = Assert.IsType<OkObjectResult>(updateRes);
        var updatedDto = Assert.IsType<ApiResponse<object>>(okUpdate.Value).Data as TestAnalyteDto;
        Assert.NotNull(updatedDto);
        Assert.Equal(0.003m, updatedDto.LoqMgPerL);

        // Delete unused analyte (Mg) -> hard deleted
        var deleteRes = await controller.DeleteTestAnalyte(test.Id, dto.Id);
        Assert.IsType<OkObjectResult>(deleteRes);
        Assert.False(await db.TestAnalytes.AnyAsync(a => a.Id == dto.Id));

        // Now create a calibration run using Zn
        var service = TestServiceFactory.CalibrationRun(db);
        var runReq = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "ValidPassword123!",
            Comment: "Used analyte test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        await service.CreateAsync(runReq, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        // Delete used analyte (Zn) -> should deactivate instead of delete!
        var deleteUsedRes = await controller.DeleteTestAnalyte(test.Id, znAnalyte.Id);
        Assert.IsType<OkObjectResult>(deleteUsedRes);
        var storedZn = await db.TestAnalytes.FindAsync(znAnalyte.Id);
        Assert.NotNull(storedZn);
        Assert.False(storedZn.IsActive);
    }

    #endregion

    #region Revision 2 Hardening: TC12-TC19 & SST Boundary

    public class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public async Task TC12_WithdrawRun_WithReasonAndSignature_WithdrawsAndSetsIsUsableFalse_SecondWithdrawFails()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: DateTime.UtcNow,
            Password: "ValidPassword123!",
            Comment: "Initial run before withdrawal",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.Equal(CalibrationRunStatus.Active, run.Status);
        var initialView = CalibrationRunView.From(run);
        Assert.True(initialView.Analytes.First().IsUsable);

        // Reject short withdrawal reason (< 10 chars)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.WithdrawAsync(run.Id, new WithdrawCalibrationRunRequest("Short", "ValidPassword123!"), fpUser.Id, "127.0.0.1"));

        // Valid withdrawal
        var withdrawnRun = await service.WithdrawAsync(
            run.Id,
            new WithdrawCalibrationRunRequest("Instrument baseline drifted significantly during sequence", "ValidPassword123!"),
            fpUser.Id,
            "127.0.0.1");

        Assert.Equal(CalibrationRunStatus.Withdrawn, withdrawnRun.Status);
        Assert.NotNull(withdrawnRun.WithdrawnAt);
        Assert.Equal(fpUser.Id, withdrawnRun.WithdrawnByUserId);
        Assert.Equal("Instrument baseline drifted significantly during sequence", withdrawnRun.WithdrawalReason);
        Assert.NotNull(withdrawnRun.WithdrawalSignature);
        Assert.Equal(SignatureMeaning.CalibrationRunWithdrawn, withdrawnRun.WithdrawalSignature.MeaningOfSignature);

        // Data remains intact
        Assert.Equal(run.Code, withdrawnRun.Code);
        Assert.Single(withdrawnRun.Analytes);
        Assert.NotNull(withdrawnRun.Document);

        // View reflection: IsUsable is false
        var withdrawnView = CalibrationRunView.From(withdrawnRun);
        Assert.False(withdrawnView.Analytes.First().IsUsable);

        // Second withdrawal attempt rejected
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.WithdrawAsync(run.Id, new WithdrawCalibrationRunRequest("Second attempt to withdraw run", "ValidPassword123!"), fpUser.Id, "127.0.0.1"));
        Assert.Contains("already withdrawn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TC13_Nominal5Point0_Measured5Point5_Window90To110_PassesExact110()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: DateTime.UtcNow,
            Password: "ValidPassword123!",
            Comment: "TC13 test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        // Nominal 5.0, measured 5.5 -> (5.5 * 100) / 5.0 = exactly 110.0%
                        new(CalibrationCheckType.Icv, 2, 5.0m, 5.5m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.True(run.Passed);
        var icvCheck = run.Analytes.Single().Checks.First(c => c.CheckType == CalibrationCheckType.Icv);
        Assert.True(icvCheck.Passed);
        Assert.Equal(110.0m, icvCheck.RecoveryPercent);
    }

    [Fact]
    public void TC14_RunR0Point999_CriterionRSquared0Point998001_PassesWithoutSqrt()
    {
        // Run reports r = 0.999, criterion is r^2 >= 0.998001
        // (0.999 * 0.999) = 0.998001m, so exactly equal -> Pass!
        var (passed1, reason1) = CalibrationRunService.EvaluateCorrelation(
            runValue: 0.999m,
            runType: CorrelationType.R,
            minCriterion: 0.998001m,
            criterionType: CorrelationType.RSquared);

        Assert.True(passed1);
        Assert.Null(reason1);

        // Reverse: Run reports r^2 = 0.998001, criterion is r >= 0.999
        // r^2 >= min * min -> 0.998001 >= 0.998001 -> Pass!
        var (passed2, reason2) = CalibrationRunService.EvaluateCorrelation(
            runValue: 0.998001m,
            runType: CorrelationType.RSquared,
            minCriterion: 0.999m,
            criterionType: CorrelationType.R);

        Assert.True(passed2);
        Assert.Null(reason2);

        // Value outside (0, 1] rejected
        var (passedZero, reasonZero) = CalibrationRunService.EvaluateCorrelation(0m, CorrelationType.R, 0.99m, CorrelationType.R);
        Assert.False(passedZero);
        Assert.Contains("(0, 1]", reasonZero);

        var (passedNegative, reasonNegative) = CalibrationRunService.EvaluateCorrelation(-0.5m, CorrelationType.R, 0.99m, CorrelationType.R);
        Assert.False(passedNegative);
        Assert.Contains("(0, 1]", reasonNegative);

        var (passedOver1, reasonOver1) = CalibrationRunService.EvaluateCorrelation(1.05m, CorrelationType.R, 0.99m, CorrelationType.R);
        Assert.False(passedOver1);
        Assert.Contains("(0, 1]", reasonOver1);
    }

    [Fact]
    public async Task TC15_LabLocalBoundaries_RunNearMidnightUtc_GetsLabLocalMonthAndYearInCode()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var timeZone = LabClock.ResolveTimeZone("Africa/Cairo");

        // Case 1: 2026-09-30 22:00:00 UTC
        // In Egypt (UTC+3 summer or UTC+2 winter), verify the local offset puts it into month 10
        var utc1 = new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero);
        var local1 = TimeZoneInfo.ConvertTime(utc1, timeZone);
        Assert.Equal(10, local1.Month); // Verified through TimeZoneInfo, not hardcoded!

        var fakeTime = new FakeTimeProvider(utc1);
        var clock1 = new LabClock(fakeTime, timeZone);
        var service1 = TestServiceFactory.CalibrationRun(db, clock: clock1);

        var request1 = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: utc1.UtcDateTime,
            Password: "ValidPassword123!",
            Comment: "TC15 September/October boundary",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream1 = CreateDummyPdfStream();
        var run1 = await service1.CreateAsync(request1, stream1, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");
        Assert.EndsWith($"10{local1.Year}", run1.Code);
        Assert.Contains("01/", run1.Code);

        // Case 2: 2026-12-31 22:30:00 UTC
        // In Egypt (UTC+2 in winter), 22:30 UTC is 00:30 on 2027-01-01
        var utc2 = new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero);
        var local2 = TimeZoneInfo.ConvertTime(utc2, timeZone);
        Assert.Equal(2027, local2.Year);
        Assert.Equal(1, local2.Month);

        fakeTime.SetUtcNow(utc2);
        var clock2 = new LabClock(fakeTime, timeZone);
        var service2 = TestServiceFactory.CalibrationRun(db, clock: clock2);

        var request2 = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: utc2.UtcDateTime,
            Password: "ValidPassword123!",
            Comment: "TC15 New Year boundary",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream2 = CreateDummyPdfStream();
        var run2 = await service2.CreateAsync(request2, stream2, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        // Code resets to 01 in the new year 2027!
        Assert.Equal($"ICP-MIN CAL 01/01{local2.Year}", run2.Code);
    }

    [Fact]
    public async Task TC16_StandardExpiring20260930_CalibrationAt20261001_FailsAnalyteWithExpiryReason()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        // Standard expires 2026-09-30
        standard.ExpiryDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var timeZone = LabClock.ResolveTimeZone("Africa/Cairo");
        var calAt = new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(calAt, TimeSpan.Zero));
        var clock = new LabClock(fakeTime, timeZone);
        var service = TestServiceFactory.CalibrationRun(db, clock: clock);

        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: calAt,
            Password: "ValidPassword123!",
            Comment: "TC16 test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");

        Assert.False(run.Passed);
        var analyte = run.Analytes.Single();
        Assert.False(analyte.Passed);
        Assert.Contains("standard expired / no expiry date", analyte.FailureReasons);
    }

    [Fact]
    public async Task TC17_RequiredCheckConfiguration_OnlyBlankRequiredPasses_MissingRequiredCcvFails()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        // Configure: Only Blank is required
        test.CalRequireBlank = true;
        test.CalRequireIcv = false;
        test.CalRequireCcv = false;
        test.CalRequireInternalStandard = false;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.CalibrationRun(db);

        // Case A: Analyte with ONLY a Blank check -> passes!
        var requestA = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: DateTime.UtcNow,
            Password: "ValidPassword123!",
            Comment: "Only blank required",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m)
                    }
                )
            });

        using var streamA = CreateDummyPdfStream();
        var runA = await service.CreateAsync(requestA, streamA, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");
        Assert.True(runA.Passed);

        // Case B: Now configure CalRequireCcv = true, submit without CCV -> fails with clear reason
        test.CalRequireCcv = true;
        await db.SaveChangesAsync();

        using var streamB = CreateDummyPdfStream();
        var runB = await service.CreateAsync(requestA, streamB, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1");
        Assert.False(runB.Passed);
        var analyteB = runB.Analytes.Single();
        Assert.False(analyteB.Passed);
        Assert.Contains("Analyte is missing required CCV check.", analyteB.FailureReasons);
    }

    [Fact]
    public async Task TC18_Preview_ProducesSameResultsWithoutSavingOrSequenceConsumption()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var previewReq = new PreviewCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: DateTime.UtcNow,
            Comment: "Preview test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        var preview = await service.PreviewAsync(previewReq, fpUser.Id);

        Assert.True(preview.Passed);
        Assert.Equal(1, preview.AnalytesPassed);
        Assert.Equal(1, preview.AnalytesTotal);
        Assert.Single(preview.Analytes);
        var previewAnalyte = preview.Analytes.Single();
        Assert.True(previewAnalyte.Passed);
        Assert.True(previewAnalyte.IsUsable);
        Assert.Equal(2, previewAnalyte.Checks.Count);

        // Verify that NOTHING was saved to the database!
        Assert.Empty(await db.CalibrationRuns.ToListAsync());
        Assert.Empty(await db.CalibrationRunAnalytes.ToListAsync());
        Assert.Empty(await db.CalibrationRunChecks.ToListAsync());
        Assert.Empty(await db.CalibrationRunDocuments.ToListAsync());
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task TC19_CalibrationAtInFutureBeyondTolerance_IsRejected()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, standard, znAnalyte, _) = SeedIcpOesTestData(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.CalibrationRun(db);
        var futureCalAt = DateTime.UtcNow.AddMinutes(15); // > 5 min tolerance

        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            CalibrationAt: futureCalAt,
            Password: "ValidPassword123!",
            Comment: "Future test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: znAnalyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = CreateDummyPdfStream();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(request, stream, "report.pdf", "application/pdf", fpUser.Id, "127.0.0.1"));

        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SST_LabLocalBoundary_CodeReflectsLabLocalMonthAndYear()
    {
        await using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);

        // Seed an HPLC test definition for SST
        var hplcTest = new TestDefinition
        {
            Code = "VIT-C",
            DisplayName = "Vitamin C HPLC",
            SectionId = fpSec.Id,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "VIT-C",
            SstMaxRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(hplcTest);

        var hplcEquip = new Equipment
        {
            Name = "Agilent 1260 HPLC",
            Code = "HPLC-01",
            Type = EquipmentType.Hplc,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(hplcEquip);

        var column = new ChromatographyColumn
        {
            Code = "COL-01",
            Name = "C18 Column",
            SerialNumber = "SN-COL-01",
            SectionId = fpSec.Id,
            IsActive = true
        };
        db.ChromatographyColumns.Add(column);

        var refStd = new Material
        {
            MaterialName = "Ascorbic Acid Standard",
            MaterialType = MaterialType.ReferenceStandard,
            SectionId = fpSec.Id,
            BatchNumber = "ASC-01",
            Purity = 99.8m,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityRemaining = 1000m,
            QuantityReceived = 1000m
        };
        db.Materials.Add(refStd);
        await db.SaveChangesAsync();

        var timeZone = LabClock.ResolveTimeZone("Africa/Cairo");
        var utc = new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero);
        var local = TimeZoneInfo.ConvertTime(utc, timeZone);
        Assert.Equal(10, local.Month);

        var fakeTime = new FakeTimeProvider(utc);
        var clock = new LabClock(fakeTime, timeZone);
        var sstService = TestServiceFactory.SystemSuitability(db, clock: clock);

        var sstReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: hplcTest.Id,
            EquipmentId: hplcEquip.Id,
            ChromatographyColumnId: column.Id,
            ReferenceStandardMaterialId: refStd.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 100000.0m,
            RsdPercent: 1.2m,
            Resolution: null,
            TailingFactor: null,
            TheoreticalPlates: null,
            Password: "ValidPassword123!",
            Comment: "SST boundary test");

        var sstRun = await sstService.CreateAsync(sstReq, fpUser.Id, "127.0.0.1");

        // Code format must be {Method} S.S {seq:00}/{MM}{yyyy} reflecting lab-local month (10)!
        Assert.Equal($"VIT-C S.S 01/10{local.Year}", sstRun.Code);
    }

    #endregion
}

