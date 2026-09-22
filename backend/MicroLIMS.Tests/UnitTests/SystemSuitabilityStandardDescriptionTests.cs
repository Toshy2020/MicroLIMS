using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SystemSuitabilityStandardDescriptionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

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
            Username = "analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Test Analyst",
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!"),
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id
        });
        db.SaveChanges();

        return (sec, user);
    }

    private static (TestDefinition test, Equipment equip, ChromatographyColumn col, Material std1, Material std2, TestAnalyte b1, TestAnalyte b2)
        SeedMultiPrerequisites(MicroLimsDbContext db, int sectionId, int userId)
    {
        var test = new TestDefinition
        {
            Code = $"M-VIT_{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DisplayName = "Multivitamin Assay",
            SectionId = sectionId,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "M-VIT"
        };
        db.TestDefinitions.Add(test);

        var equip = new Equipment
        {
            Name = "HPLC Agilent 1260",
            Code = $"HPLC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Hplc,
            SectionId = sectionId
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = "C18 Column",
            Code = $"COL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SerialNumber = "SN-112233",
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.ChromatographyColumns.Add(col);

        var std1 = new Material
        {
            MaterialName = "Vitamin B1 RS",
            MaterialType = MaterialType.ReferenceStandard,
            BatchNumber = "LOT-B1",
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            SectionId = sectionId,
            Purity = 99.5m,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        var std2 = new Material
        {
            MaterialName = "Vitamin B2 RS",
            MaterialType = MaterialType.ReferenceStandard,
            BatchNumber = "LOT-B2",
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
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
            SstMaxRsdPercent = 2.0m
        };
        var b2 = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Vitamin B2",
            WavelengthNm = 267.0m,
            DisplayOrder = 2,
            IsActive = true,
            SstMaxRsdPercent = 2.0m
        };
        db.TestAnalytes.AddRange(b1, b2);
        db.SaveChanges();

        return (test, equip, col, std1, std2, b1, b2);
    }

    private static (TestDefinition test, Equipment equip, ChromatographyColumn col, Material std)
        SeedSinglePrerequisites(MicroLimsDbContext db, int sectionId, int userId)
    {
        var test = new TestDefinition
        {
            Code = $"VIT-C_{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DisplayName = "Vitamin C Assay",
            SectionId = sectionId,
            WorkflowType = WorkflowType.HplcAssay,
            EquationType = EquationType.HplcAssay,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "VIT-C",
            SstMaxRsdPercent = 2.0m
        };
        db.TestDefinitions.Add(test);

        var equip = new Equipment
        {
            Name = "HPLC Agilent 1260",
            Code = $"HPLC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Hplc,
            SectionId = sectionId
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = "C18 Column",
            Code = $"COL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SerialNumber = "SN-445566",
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.ChromatographyColumns.Add(col);

        var std = new Material
        {
            MaterialName = "Ascorbic Acid RS",
            MaterialType = MaterialType.ReferenceStandard,
            BatchNumber = "LOT-C-01",
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            SectionId = sectionId,
            Purity = 99.8m,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.Materials.Add(std);
        db.SaveChanges();

        return (test, equip, col, std);
    }

    #region Weigh-in Deviation & Window Tests

    [Fact]
    public async Task WeighIn_InsideWindow_SetsDeviation_NoOutOfWindowFlag_AllowsPass()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // Target: 50.0 mg, Actual: 51.0 mg -> Deviation = (51 - 50) / 50 * 100 = +2.0% (inside ±5%)
        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 51.0m, StandardDilution: 100m, StandardMeanArea: 2500000m,
                    RsdPercent: 1.0m, TheoreticalWeightMg: 50.0m),
                new(b2.Id, std2.Id, StandardWeightMg: 49.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.1m, TheoreticalWeightMg: 50.0m) // (49 - 50)/50*100 = -2.0%
            });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Null(run.FailureReasons);
        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal(50.0m, r1.TheoreticalWeightMg);
        Assert.Equal(2.0m, r1.StandardWeighInDeviationPercent);
        Assert.False(r1.StandardWeighInOutOfWindow);
        Assert.Null(r1.WeighInJustification);

        var r2 = run.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.Equal(50.0m, r2.TheoreticalWeightMg);
        Assert.Equal(-2.0m, r2.StandardWeighInDeviationPercent);
        Assert.False(r2.StandardWeighInOutOfWindow);
    }

    [Fact]
    public async Task WeighIn_ExactBoundary_IsInsideWindow()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // Exactly +5% (52.5 mg) and -5% (47.5 mg) for 50.0 mg theoretical target
        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 52.5m, StandardDilution: 100m, StandardMeanArea: 2500000m,
                    RsdPercent: 1.0m, TheoreticalWeightMg: 50.0m),
                new(b2.Id, std2.Id, StandardWeightMg: 47.5m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.1m, TheoreticalWeightMg: 50.0m)
            });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal(5.0m, r1.StandardWeighInDeviationPercent);
        Assert.False(r1.StandardWeighInOutOfWindow);

        var r2 = run.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.Equal(-5.0m, r2.StandardWeighInDeviationPercent);
        Assert.False(r2.StandardWeighInOutOfWindow);
    }

    [Fact]
    public async Task WeighIn_OutsideWindow_WithoutJustification_IsRejected()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // Actual: 53.0 mg, Target: 50.0 mg -> Deviation = +6.0% (outside ±5%), no justification
        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 53.0m, StandardDilution: 100m, StandardMeanArea: 2500000m,
                    RsdPercent: 1.0m, TheoreticalWeightMg: 50.0m, WeighInJustification: ""),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.1m, TheoreticalWeightMg: 50.0m)
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));

        Assert.Contains("Weigh-in justification is required", ex.Message);
        Assert.Contains("Vitamin B1", ex.Message);
    }

    [Fact]
    public async Task WeighIn_OutsideWindow_WithJustification_SetsFlagAndAllowsRunToPass()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // Actual: 53.0 mg, Target: 50.0 mg -> +6.0%, with justification
        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 53.0m, StandardDilution: 100m, StandardMeanArea: 2500000m,
                    RsdPercent: 1.0m, TheoreticalWeightMg: 50.0m,
                    WeighInJustification: "Slightly over-weighed standard due to static charge in draft shield."),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.1m, TheoreticalWeightMg: 50.0m)
            });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        // SOP rule: Outside +/-5% is a WARNING, never a hard failure: store deviation and flag,
        // and DO NOT set Passed = false and DO NOT add a failure reason because of it.
        Assert.True(run.Passed);
        Assert.Null(run.FailureReasons);

        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.True(r1.Passed);
        Assert.Null(r1.FailureReasons);
        Assert.Equal(6.0m, r1.StandardWeighInDeviationPercent);
        Assert.True(r1.StandardWeighInOutOfWindow);
        Assert.Equal("Slightly over-weighed standard due to static charge in draft shield.", r1.WeighInJustification);

        // Run level reflects first analyte and out-of-window presence
        Assert.True(run.StandardWeighInOutOfWindow);
        Assert.Equal(6.0m, run.StandardWeighInDeviationPercent);
    }

    [Fact]
    public async Task WeighIn_SingleAnalyte_OutsideWindow_RequiresJustificationAndAllowsPass()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std) = SeedSinglePrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // Missing justification: rejected
        var invalidReq = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 54.0m,
            StandardDilution: 100m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.0m,
            TheoreticalWeightMg: 50.0m,
            WeighInJustification: null,
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(invalidReq, user.Id, "127.0.0.1"));
        Assert.Contains("Weigh-in justification is required", ex.Message);

        // With justification: accepted and passed
        var validReq = invalidReq with { WeighInJustification = "Standard bottle hygroscopic absorption allowed." };
        var run = await service.CreateAsync(validReq, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Null(run.FailureReasons);
        Assert.Equal(50.0m, run.TheoreticalWeightMg);
        Assert.Equal(8.0m, run.StandardWeighInDeviationPercent); // (54 - 50) / 50 * 100 = +8%
        Assert.True(run.StandardWeighInOutOfWindow);
        Assert.Equal("Standard bottle hygroscopic absorption allowed.", run.WeighInJustification);
    }

    [Fact]
    public async Task TheoreticalWeight_NegativeOrZero_IsRejected()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std) = SeedSinglePrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var req = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.0m,
            TheoreticalWeightMg: 0m,
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(req, user.Id, "127.0.0.1"));
        Assert.Contains("Theoretical standard weight must be greater than 0", ex.Message);
    }

    #endregion

    #region Moisture Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(99.9)]
    public async Task Moisture_ValidRange_IsAccepted(double moisture)
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std) = SeedSinglePrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var req = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.0m,
            MoisturePercent: (decimal)moisture,
            Password: "ValidPassword123!");

        var run = await service.CreateAsync(req, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Equal((decimal)moisture, run.MoisturePercent);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-5.0)]
    [InlineData(100.0)]
    [InlineData(105.0)]
    public async Task Moisture_InvalidRange_IsRejected(double moisture)
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std) = SeedSinglePrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var req = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.0m,
            MoisturePercent: (decimal)moisture,
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(req, user.Id, "127.0.0.1"));
        Assert.Contains("Moisture percent", ex.Message);
    }

    [Fact]
    public async Task Moisture_Null_IsAccepted()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std) = SeedSinglePrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var req = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100m,
            StandardMeanArea: 2000000m,
            RsdPercent: 1.0m,
            MoisturePercent: null,
            Password: "ValidPassword123!");

        var run = await service.CreateAsync(req, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Null(run.MoisturePercent);
    }

    #endregion

    #region Standard Replicate Responses & Computed RSD Tests

    [Fact]
    public async Task Responses_MeanWrittenToStandardMeanArea_ComputedRsdMatchesHandCalculatedValue()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        /*
         * HAND-CALCULATED RSD VERIFICATION:
         * -------------------------------------------------------------
         * Replicate responses (5 injections):
         *   x_1 = 2,010,000
         *   x_2 = 1,990,000
         *   x_3 = 2,010,000
         *   x_4 = 1,990,000
         *   x_5 = 2,000,000
         *
         * 1. Sample size: n = 5
         * 2. Sum = 2010000 + 1990000 + 2010000 + 1990000 + 2000000 = 10,000,000
         * 3. Sample mean:
         *      x̄ = 10,000,000 / 5 = 2,000,000.0
         * 4. Deviations from mean (x_i - x̄):
         *      d_1 = 2010000 - 2000000 = +10,000
         *      d_2 = 1990000 - 2000000 = -10,000
         *      d_3 = 2010000 - 2000000 = +10,000
         *      d_4 = 1990000 - 2000000 = -10,000
         *      d_5 = 2000000 - 2000000 =       0
         * 5. Squared deviations (d_i^2):
         *      d_1^2 = 100,000,000
         *      d_2^2 = 100,000,000
         *      d_3^2 = 100,000,000
         *      d_4^2 = 100,000,000
         *      d_5^2 =           0
         * 6. Sum of squared deviations:
         *      Σ(d_i^2) = 400,000,000
         * 7. Sample variance (degrees of freedom n - 1 = 4):
         *      s^2 = 400,000,000 / 4 = 100,000,000
         * 8. Sample standard deviation:
         *      s = sqrt(100,000,000) = 10,000.0 (exact integer)
         * 9. Relative Standard Deviation (RSD%):
         *      RSD% = (s / x̄) * 100
         *           = (10,000 / 2,000,000) * 100
         *           = 0.5% (exactly 0.5m)
         * -------------------------------------------------------------
         */
        var responsesB1 = new List<decimal> { 2010000m, 1990000m, 2010000m, 1990000m, 2000000m };

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 50.0m, StandardDilution: 100m,
                    StandardMeanArea: 0m, // Caller passes 0; service must compute mean from responses
                    RsdPercent: 1.8m,     // Transcribed value recorded alongside
                    Responses: responsesB1),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m,
                    StandardMeanArea: 1800000m, RsdPercent: 1.2m)
            });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);

        // 1. Mean written to StandardMeanArea
        Assert.Equal(2000000m, r1.StandardMeanArea);

        // 2. Computed RSD matches exactly 0.5%
        Assert.Equal(0.5m, r1.ComputedRsdPercent);

        // 3. Transcribed RSD preserved alongside
        Assert.Equal(1.8m, r1.RsdPercent);

        // 4. Standard responses child entity created with 1-based indexes
        Assert.Equal(5, r1.Responses.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, r1.Responses.Select(r => r.Index));
        Assert.Equal(responsesB1, r1.Responses.Select(r => r.Response));
    }

    [Fact]
    public async Task PrecedenceRule_ComputedRsdDrivesPassFail_ConflictingTranscribedValueIgnored()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        // b1 acceptance criteria: max RSD 2.0%
        // Case A: Computed RSD = 0.5% (PASSES), but transcribed RsdPercent = 4.5% (FAILS if used)
        // Expected: Computed RSD drives gate -> run PASSES.
        var passResponses = new List<decimal> { 2010000m, 1990000m, 2010000m, 1990000m, 2000000m }; // RSD = 0.5%

        var reqPass = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 2000000m,
                    RsdPercent: 4.5m, // would fail max 2.0%
                    Responses: passResponses),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.0m)
            });

        var passRun = await service.CreateAsync(reqPass, user.Id, "127.0.0.1");
        Assert.True(passRun.Passed);
        Assert.Null(passRun.FailureReasons);
        var r1 = passRun.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.True(r1.Passed);
        Assert.Equal(0.5m, r1.ComputedRsdPercent);
        Assert.Equal(4.5m, r1.RsdPercent);

        /*
         * Case B:
         * Hand-calculated failing responses:
         *   x_1 = 1,030,000
         *   x_2 =   970,000
         *   x_3 = 1,030,000
         *   x_4 =   970,000
         *   x_5 = 1,000,000
         *
         *   n = 5, Mean = 1,000,000.0
         *   Deviations: [+30000, -30000, +30000, -30000, 0]
         *   Sum of squared diffs = 4 * 900,000,000 = 3,600,000,000
         *   Sample variance = 3,600,000,000 / 4 = 900,000,000
         *   s = sqrt(900,000,000) = 30,000.0
         *   RSD% = (30,000 / 1,000,000) * 100 = 3.0% (EXACTLY 3.0m)
         *
         * Transcribed RsdPercent = 0.8% (PASSES if used), but Computed = 3.0% (FAILS max 2.0%)
         * Expected: Computed RSD drives gate -> run FAILS.
         */
        var failResponses = new List<decimal> { 1030000m, 970000m, 1030000m, 970000m, 1000000m };

        var reqFail = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1000000m,
                    RsdPercent: 0.8m, // would pass if used
                    Responses: failResponses),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.0m)
            });

        var failRun = await service.CreateAsync(reqFail, user.Id, "127.0.0.1");
        Assert.False(failRun.Passed);
        Assert.NotNull(failRun.FailureReasons);
        Assert.Contains("RSD%", failRun.FailureReasons);
        Assert.Contains("exceeds maximum limit", failRun.FailureReasons);
        var rFail = failRun.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.False(rFail.Passed);
        Assert.Equal(3.0m, rFail.ComputedRsdPercent);
        Assert.Equal(0.8m, rFail.RsdPercent);
    }

    [Fact]
    public async Task NoResponses_TranscribedRsdDrivesGate_PreservesTodayBehaviour()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 2500000m,
                    RsdPercent: 1.5m, Responses: null),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.2m, Responses: null)
            });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        var r1 = run.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Null(r1.ComputedRsdPercent);
        Assert.Equal(1.5m, r1.RsdPercent);
        Assert.Empty(r1.Responses);
    }

    [Fact]
    public async Task SingleAnalyte_WithResponses_ComputesMeanAndRsd()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std) = SeedSinglePrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var responses = new List<decimal> { 2010000m, 1990000m, 2010000m, 1990000m, 2000000m }; // RSD = 0.5%

        var req = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100m,
            StandardMeanArea: 0m,
            RsdPercent: 1.9m,
            Responses: responses,
            Password: "ValidPassword123!");

        var run = await service.CreateAsync(req, user.Id, "127.0.0.1");

        Assert.True(run.Passed);
        Assert.Equal(2000000m, run.StandardMeanArea);
        Assert.Equal(0.5m, run.ComputedRsdPercent);
        Assert.Equal(1.9m, run.RsdPercent);
    }

    [Fact]
    public async Task Response_NegativeOrZero_IsRejected()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 2500000m,
                    Responses: new List<decimal> { 2500000m, 0m, 2400000m }),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m)
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, user.Id, "127.0.0.1"));
        Assert.Contains("Standard response for analyte Vitamin B1 must be greater than 0", ex.Message);
    }

    #endregion

    #region Report & View DTOs Tests

    [Fact]
    public async Task GetReportDetails_ExposesNewFieldsAndResponses()
    {
        await using var db = NewDb();
        var (sec, user) = SeedSectionAndUser(db);
        var (test, equip, col, std1, std2, b1, b2) = SeedMultiPrerequisites(db, sec.Id, user.Id);
        var service = TestServiceFactory.SystemSuitability(db);

        var responsesB1 = new List<decimal> { 2010000m, 1990000m, 2010000m, 1990000m, 2000000m };

        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            Password: "ValidPassword123!",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, StandardWeightMg: 53.0m, StandardDilution: 100m, StandardMeanArea: 0m,
                    RsdPercent: 1.0m, TheoreticalWeightMg: 50.0m, MoisturePercent: 1.25m,
                    WeighInJustification: "Slight overfill within warning allowance.",
                    Responses: responsesB1),
                new(b2.Id, std2.Id, StandardWeightMg: 50.0m, StandardDilution: 100m, StandardMeanArea: 1800000m,
                    RsdPercent: 1.1m, TheoreticalWeightMg: 50.0m, MoisturePercent: 0.5m)
            });

        var run = await service.CreateAsync(request, user.Id, "127.0.0.1");

        var report = await service.GetReportDetailsAsync(run.Id, user.Id);

        Assert.NotNull(report.Analytes);
        Assert.Equal(2, report.Analytes.Count);

        var repB1 = report.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal(50.0m, repB1.TheoreticalWeightMg);
        Assert.Equal(1.25m, repB1.MoisturePercent);
        Assert.Equal(6.0m, repB1.StandardWeighInDeviationPercent);
        Assert.True(repB1.StandardWeighInOutOfWindow);
        Assert.Equal("Slight overfill within warning allowance.", repB1.WeighInJustification);
        Assert.Equal(0.5m, repB1.ComputedRsdPercent);
        Assert.Equal(1.0m, repB1.RsdPercent);
        Assert.NotNull(repB1.Responses);
        Assert.Equal(5, repB1.Responses.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, repB1.Responses.Select(r => r.Index));
        Assert.Equal(responsesB1, repB1.Responses.Select(r => r.Response));

        // Controller view test
        var view = SystemSuitabilityRunView.From(run);
        Assert.Equal(50.0m, view.TheoreticalWeightMg);
        Assert.Equal(1.25m, view.MoisturePercent);
        Assert.Equal(6.0m, view.StandardWeighInDeviationPercent);
        Assert.True(view.StandardWeighInOutOfWindow);
        Assert.Equal(0.5m, view.ComputedRsdPercent);
    }

    #endregion
}
