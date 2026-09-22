using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SystemSuitabilitySliceA2Tests
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

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpUser.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = microUser.Id,
            DepartmentId = microSec.DepartmentId,
            SectionId = microSec.Id
        });
        db.SaveChanges();

        return (fpSec, microSec, fpUser, microUser, adminUser);
    }

    private static (TestDefinition test, Equipment equip, ChromatographyColumn col, Material standard) SeedSuitabilityPrerequisites(
        MicroLimsDbContext db, int sectionId, int userId, string methodAbbr = "VIT-C")
    {
        var test = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Assay",
            SectionId = sectionId,
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 1.5m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2000m
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
            SerialNumber = "SN-123456",
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.ChromatographyColumns.Add(col);

        var standard = new Material
        {
            MaterialName = "Ascorbic Acid RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ReceivingDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Cabinet A",
            SectionId = sectionId,
            Purity = 99.8m,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.Materials.Add(standard);
        db.SaveChanges();

        return (test, equip, col, standard);
    }

    #region 1. Pass/Fail Rule Tests

    [Fact]
    public void PassRule_AllCriteriaSatisfied_ReturnsPassed()
    {
        var test = new TestDefinition
        {
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 1.5m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2000m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: 1.2m,
            resolution: 2.1m,
            tailingFactor: 1.1m,
            theoreticalPlates: 3500m);

        Assert.True(passed);
        Assert.Null(reasons);
    }

    [Fact]
    public void PassRule_RsdExceeded_ReturnsFailed()
    {
        var test = new TestDefinition
        {
            SstMaxRsdPercent = 2.0m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: 2.5m,
            resolution: null,
            tailingFactor: null,
            theoreticalPlates: null);

        Assert.False(passed);
        Assert.NotNull(reasons);
        Assert.Contains("RSD%", reasons);
        Assert.Contains("exceeds maximum limit", reasons);
    }

    [Fact]
    public void PassRule_ResolutionBelowMin_ReturnsFailed()
    {
        var test = new TestDefinition
        {
            SstMinResolution = 1.5m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: null,
            resolution: 1.2m,
            tailingFactor: null,
            theoreticalPlates: null);

        Assert.False(passed);
        Assert.NotNull(reasons);
        Assert.Contains("Resolution", reasons);
        Assert.Contains("below minimum limit", reasons);
    }

    [Fact]
    public void PassRule_TailingExceeded_ReturnsFailed()
    {
        var test = new TestDefinition
        {
            SstMaxTailingFactor = 2.0m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: null,
            resolution: null,
            tailingFactor: 2.3m,
            theoreticalPlates: null);

        Assert.False(passed);
        Assert.NotNull(reasons);
        Assert.Contains("Tailing factor", reasons);
        Assert.Contains("exceeds maximum limit", reasons);
    }

    [Fact]
    public void PassRule_PlatesBelowMin_ReturnsFailed()
    {
        var test = new TestDefinition
        {
            SstMinTheoreticalPlates = 2000m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: null,
            resolution: null,
            tailingFactor: null,
            theoreticalPlates: 1800m);

        Assert.False(passed);
        Assert.NotNull(reasons);
        Assert.Contains("Theoretical plates", reasons);
        Assert.Contains("below minimum limit", reasons);
    }

    [Fact]
    public void PassRule_UnconfiguredCriteria_AreSkipped()
    {
        // Only RSD is configured
        var test = new TestDefinition
        {
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = null,
            SstMaxTailingFactor = null,
            SstMinTheoreticalPlates = null
        };

        // Resolution, tailing, plates are entered or omitted; only RSD should be evaluated
        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: 1.5m,
            resolution: 0.1m, // would fail if configured
            tailingFactor: 99m, // would fail if configured
            theoreticalPlates: 1m); // would fail if configured

        Assert.True(passed);
        Assert.Null(reasons);
    }

    [Fact]
    public void PassRule_MissingConfiguredCriterion_ReturnsFailed()
    {
        var test = new TestDefinition
        {
            SstMaxRsdPercent = 2.0m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: null,
            resolution: null,
            tailingFactor: null,
            theoreticalPlates: null);

        Assert.False(passed);
        Assert.NotNull(reasons);
        Assert.Contains("RSD% is required", reasons);
    }

    [Fact]
    public void PassRule_MultipleFailures_ReportsAllReasons()
    {
        var test = new TestDefinition
        {
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 2.0m
        };

        var (passed, reasons) = SystemSuitabilityService.EvaluateAcceptanceCriteria(
            test,
            rsdPercent: 3.0m,
            resolution: 1.0m,
            tailingFactor: null,
            theoreticalPlates: null);

        Assert.False(passed);
        Assert.NotNull(reasons);
        Assert.Contains("RSD%", reasons);
        Assert.Contains("Resolution", reasons);
    }

    #endregion

    #region 2. Code Format, Yearly Reset & Continuity

    [Fact]
    public async Task CodeFormat_InitialRun_FollowsFormat()
    {
        var issuedCodes = new List<string>().AsQueryable();
        var date = new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc);

        var code = await SystemSuitabilityRunCode.NextAsync(issuedCodes, "VIT-C", date);

        Assert.Equal("VIT-C S.S 01/112026", code);
    }

    [Fact]
    public async Task CodeFormat_ContinuousAcrossMonths_InSameYear()
    {
        var issued = new List<string>
        {
            "VIT-C S.S 01/012026",
            "VIT-C S.S 02/032026",
            "VIT-C S.S 03/052026"
        }.AsQueryable();

        var novemberDate = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var nextCode = await SystemSuitabilityRunCode.NextAsync(issued, "VIT-C", novemberDate);

        Assert.Equal("VIT-C S.S 04/112026", nextCode);
    }

    [Fact]
    public async Task CodeFormat_ResetsInJanuaryOfNewYear()
    {
        var issued = new List<string>
        {
            "VIT-C S.S 01/012026",
            "VIT-C S.S 02/052026",
            "VIT-C S.S 03/122026"
        }.AsQueryable();

        var januaryDate2027 = new DateTime(2027, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var nextCode = await SystemSuitabilityRunCode.NextAsync(issued, "VIT-C", januaryDate2027);

        Assert.Equal("VIT-C S.S 01/012027", nextCode);
    }

    [Fact]
    public async Task CodeFormat_SequencingIndependentPerMethodAbbreviation()
    {
        var issued = new List<string>
        {
            "VIT-C S.S 01/032026",
            "VIT-C S.S 02/052026"
        }.AsQueryable();

        var date = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc);
        var ibuCode = await SystemSuitabilityRunCode.NextAsync(issued, "IBU", date);

        Assert.Equal("IBU S.S 01/052026", ibuCode);
    }

    #endregion

    #region 3. Create Run Validation & Section 403s

    [Fact]
    public async Task CreateRun_ValidPayload_SucceedsAndComputesPassed()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 2450000m,
            RsdPercent: 1.2m,
            Resolution: 2.5m,
            TailingFactor: 1.2m,
            TheoreticalPlates: 3500m,
            Password: "ValidPassword123!",
            Comment: "Initial run");

        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        Assert.NotNull(run);
        Assert.Equal(test.Id, run.TestDefinitionId);
        Assert.Equal(fpSec.Id, run.SectionId);
        Assert.True(run.Passed);
        Assert.Null(run.FailureReasons);
        Assert.Equal(99.8m, run.StandardPurityPercent); // Snapshot from Material.Purity
        Assert.StartsWith("VIT-C S.S 01/", run.Code);
        Assert.True(run.SignatureId > 0);
    }

    [Fact]
    public async Task CreateRun_FailedRun_StoredAndRetained()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 2450000m,
            RsdPercent: 3.5m, // Exceeds max 2.0%
            Resolution: 2.5m,
            TailingFactor: 1.2m,
            TheoreticalPlates: 3500m,
            Password: "ValidPassword123!",
            Comment: "Failing run test");

        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        Assert.NotNull(run);
        Assert.False(run.Passed);
        Assert.NotNull(run.FailureReasons);
        Assert.Contains("RSD%", run.FailureReasons);

        // Record exists in DB and is listed
        var stored = await db.SystemSuitabilityRuns.FirstOrDefaultAsync(r => r.Id == run.Id);
        Assert.NotNull(stored);
        Assert.False(stored.Passed);
    }

    [Fact]
    public async Task CreateRun_NonHplcEquipment_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, _, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var autoclave = new Equipment
        {
            Name = "Autoclave 1",
            Code = "AUT-001",
            Type = EquipmentType.Autoclave,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(autoclave);
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, autoclave.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Selected equipment must be an HPLC instrument.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_EquipmentDifferentSection_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, microSec, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, _, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var microHplc = new Equipment
        {
            Name = "Micro HPLC",
            Code = "HPLC-MICRO",
            Type = EquipmentType.Hplc,
            SectionId = microSec.Id,
            CdsSoftware = CdsSoftware.WatersEmpower3
        };
        db.Equipment.Add(microHplc);
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, microHplc.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Equipment belongs to a different laboratory section than the test definition.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_InactiveColumn_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        col.IsActive = false;
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Chromatography column is not active.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_ExpiredStandard_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        standard.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Reference standard is expired.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_DepletedStandard_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        standard.QuantityRemaining = 0m;
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Reference standard is depleted.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_StandardNotReferenceStandard_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        standard.MaterialType = MaterialType.Chemical;
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Material must be a reference standard.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_TestDoesNotRequireSuitability_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        test.RequiresSystemSuitability = false;
        db.SaveChanges();

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));
        Assert.Equal("Test definition does not require system suitability.", ex.Message);
    }

    [Fact]
    public async Task CreateRun_WrongPassword_ThrowsSignatureVerificationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "WrongPassword!");

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.CreateAsync(request, fpUser.Id, "127.0.0.1"));

        Assert.Empty(db.SystemSuitabilityRuns);
    }

    [Fact]
    public async Task CreateRun_UserInOtherSection_ThrowsUnauthorizedAccessException()
    {
        using var db = NewDb();
        var (fpSec, _, _, microUser, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, microUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        // microUser tries to create a run for an FP test
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(request, microUser.Id, "127.0.0.1"));
    }

    [Fact]
    public async Task GetById_UserInOtherSection_ThrowsUnauthorizedAccessException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, microUser, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");

        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        // microUser tries to get run
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetByIdAsync(run.Id, microUser.Id));
    }

    #endregion

    #region 4. Linking Samples Rules

    [Fact]
    public async Task LinkSample_ValidPassedRun_Succeeds()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");
        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = test.Code,
            SectionId = fpSec.Id,
            Status = ApprovalStatus.Pending
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        await service.LinkTestOrdersAsync(run.Id, new[] { order.Id }, fpUser.Id);

        var updatedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(run.Id, updatedOrder!.SystemSuitabilityRunId);
    }

    [Fact]
    public async Task LinkSample_FailedRun_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 4m, 2m, 1m, 2500m, "ValidPassword123!"); // Fails RSD
        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");
        Assert.False(run.Passed);

        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        var order = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = fpSec.Id, Status = ApprovalStatus.Pending };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LinkTestOrdersAsync(run.Id, new[] { order.Id }, fpUser.Id));
        Assert.Equal("Cannot link test order to a failed system suitability run.", ex.Message);
    }

    [Fact]
    public async Task LinkSample_DifferentTestCode_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");
        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        var order = new TestOrder { Sample = sample, TestCode = "OTHER_TEST", SectionId = fpSec.Id, Status = ApprovalStatus.Pending };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LinkTestOrdersAsync(run.Id, new[] { order.Id }, fpUser.Id));
        Assert.Contains("does not match run method", ex.Message);
    }

    [Fact]
    public async Task LinkSample_DifferentSection_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, microSec, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");
        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        // Order in micro section
        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        var order = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = microSec.Id, Status = ApprovalStatus.Pending };
        db.TestOrders.Add(order);
        db.SaveChanges();

        // Give user access to micro as well so the section scope check passes and we hit the mismatch rule
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = microSec.DepartmentId, SectionId = microSec.Id });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LinkTestOrdersAsync(run.Id, new[] { order.Id }, fpUser.Id));
        Assert.Contains("belongs to a different laboratory section than the system suitability run", ex.Message);
    }

    [Fact]
    public async Task LinkSample_ClosedOrder_ThrowsInvalidOperationException()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");
        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        var order = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = fpSec.Id, Status = ApprovalStatus.Approved };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LinkTestOrdersAsync(run.Id, new[] { order.Id }, fpUser.Id));
        Assert.Contains("is closed or superseded and cannot be linked", ex.Message);
    }

    [Fact]
    public async Task BulkLink_AllOrNothing_OneInvalidFailsWholeBatch()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id,
            50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!");
        var run = await service.CreateAsync(request, fpUser.Id, "127.0.0.1");

        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);

        var validOrder1 = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = fpSec.Id, Status = ApprovalStatus.Pending };
        var validOrder2 = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = fpSec.Id, Status = ApprovalStatus.Pending };
        var closedOrder = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = fpSec.Id, Status = ApprovalStatus.Rejected };

        db.TestOrders.AddRange(validOrder1, validOrder2, closedOrder);
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LinkTestOrdersAsync(run.Id, new[] { validOrder1.Id, validOrder2.Id, closedOrder.Id }, fpUser.Id));

        Assert.Contains("is closed or superseded", ex.Message);

        // Verify all-or-nothing: NEITHER valid order was linked!
        var o1 = await db.TestOrders.FindAsync(validOrder1.Id);
        var o2 = await db.TestOrders.FindAsync(validOrder2.Id);
        Assert.Null(o1!.SystemSuitabilityRunId);
        Assert.Null(o2!.SystemSuitabilityRunId);
    }

    [Fact]
    public async Task Relink_ToAnotherPassedRun_Succeeds()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var (test, equip, col, standard) = SeedSuitabilityPrerequisites(db, fpSec.Id, fpUser.Id);

        var service = TestServiceFactory.SystemSuitability(db);
        var run1 = await service.CreateAsync(new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id, 50m, 100m, 10000m, 1m, 2m, 1m, 2500m, "ValidPassword123!"), fpUser.Id, "127.0.0.1");
        var run2 = await service.CreateAsync(new CreateSystemSuitabilityRunRequest(
            test.Id, equip.Id, col.Id, standard.Id, 50m, 100m, 10000m, 1.1m, 2.1m, 1.1m, 2600m, "ValidPassword123!"), fpUser.Id, "127.0.0.1");

        var sample = new Sample { ReferenceNumber = "SMP-001", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        var order = new TestOrder { Sample = sample, TestCode = test.Code, SectionId = fpSec.Id, Status = ApprovalStatus.Pending, SystemSuitabilityRunId = run1.Id };
        db.TestOrders.Add(order);
        db.SaveChanges();

        // Relink to run2
        await service.LinkTestOrdersAsync(run2.Id, new[] { order.Id }, fpUser.Id);

        var updated = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(run2.Id, updated!.SystemSuitabilityRunId);
    }

    #endregion

    #region 5. MasterData Test Definition & Equation Types

    [Fact]
    public void MasterData_EquationTypes_ReturnsExpectedList()
    {
        using var db = NewDb();
        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        var controller = new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService);

        var result = controller.GetEquationTypes() as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);
        var envelope = result.Value as ApiResponse<object>;
        Assert.NotNull(envelope);
        var list = envelope.Data as IEnumerable<EquationTypeDto>;
        Assert.NotNull(list);
        var items = list.ToList();
        Assert.True(items.Count >= 3);
        Assert.Contains(items, i => i.Code == "None");
        Assert.Contains(items, i => i.Code == "StandardComparison");
        Assert.DoesNotContain(items, i => i.Code == "HplcAssay" || i.Code == "HplcMultiAnalyte");
        Assert.Contains(items, i => i.Code == "SystemSuitability");
    }

    [Fact]
    public async Task MasterData_CreateTestDefinition_RequiresSst_ValidatesMethodAbbreviationAndCriteria()
    {
        using var db = NewDb();
        var (fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        var controller = new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService);

        // Authenticate controller as fpUser
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, fpUser.Id.ToString()) },
                        "TestAuth"))
            }
        };

        // 1. Missing MethodAbbreviation
        var reqNoAbbr = new CreateTestDefinitionRequest(
            Code: "TEST_NO_ABBR",
            DisplayName: "Test No Abbr",
            SectionId: fpSec.Id,
            RequiresSystemSuitability: true,
            MethodAbbreviation: null,
            SstMaxRsdPercent: 2.0m);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqNoAbbr));
        Assert.Contains("Method abbreviation is required", ex1.Message);

        // 2. Invalid MethodAbbreviation format (contains spaces or special chars)
        var reqBadAbbr = new CreateTestDefinitionRequest(
            Code: "TEST_BAD_ABBR",
            DisplayName: "Test Bad Abbr",
            SectionId: fpSec.Id,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "VIT C ASSAY!",
            SstMaxRsdPercent: 2.0m);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqBadAbbr));
        Assert.Contains("Method abbreviation must be", ex2.Message);

        // 3. Missing all SST criteria
        var reqNoCriteria = new CreateTestDefinitionRequest(
            Code: "TEST_NO_CRITERIA",
            DisplayName: "Test No Criteria",
            SectionId: fpSec.Id,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "VIT-C");

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqNoCriteria));
        Assert.Contains("At least one system suitability criterion is required", ex3.Message);

        // 4. Valid payload succeeds and upper-cases abbreviation
        var reqValid = new CreateTestDefinitionRequest(
            Code: "VITC_ASSAY",
            DisplayName: "Vitamin C Assay",
            SectionId: fpSec.Id,
            RequiresSystemSuitability: true,
            MethodAbbreviation: "vit-c", // lowercase
            SstMaxRsdPercent: 2.0m);

        var result = await controller.CreateTestDefinition(reqValid) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);
        var envelope = result.Value as ApiResponse<object>;
        var created = envelope!.Data as TestDefinition;
        Assert.NotNull(created);
        Assert.Equal("VIT-C", created.MethodAbbreviation);
        Assert.True(created.RequiresSystemSuitability);
        Assert.Equal(2.0m, created.SstMaxRsdPercent);
    }

    #endregion
}
