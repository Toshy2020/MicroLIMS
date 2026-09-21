using System.Globalization;
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
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcMultiAnalyteSliceM2Tests
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

    private static (
        TestDefinition testDef,
        Equipment equip,
        ChromatographyColumn col,
        Material std1,
        Material std2,
        TestAnalyte b1,
        TestAnalyte b2,
        Item item,
        Sample sample,
        TestOrder order,
        Specification specB1,
        Specification specB2,
        SystemSuitabilityRun sstRun)
        SetupHplcMultiScenario(
            MicroLimsDbContext db,
            int preps = 2,
            int injections = 2,
            decimal? maxRsd = 2.0m,
            SampleMatrix matrix = SampleMatrix.Solid)
    {
        var (fpSec, fpUser) = SeedSectionAndUser(db);

        var methodAbbr = "M" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Multivitamin Assay",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcPreparations = preps,
            HplcInjectionsPerPreparation = injections,
            HplcMaxPreparationRsdPercent = maxRsd,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

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
            SerialNumber = "SN-987654",
            SectionId = fpSec.Id,
            IsActive = true,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.ChromatographyColumns.Add(col);

        var std1 = new Material
        {
            MaterialName = "Thiamine Hydrochloride RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-B1",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = fpSec.Id,
            Purity = 99.0m,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        var std2 = new Material
        {
            MaterialName = "Riboflavin RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-B2",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = fpSec.Id,
            Purity = 98.5m,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.Materials.AddRange(std1, std2);
        db.SaveChanges();

        var b1 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Vitamin B1 (thiamine)",
            WavelengthNm = 254.0m,
            DisplayOrder = 1,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2000m
        };
        var b2 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Vitamin B2 (riboflavin)",
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

        var item = new Item
        {
            Code = $"ITEM-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Name = "Multivitamin Tablet 500mg",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var specB1 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Vitamin B1",
            TestAnalyteId = b1.Id,
            ResultBasis = ResultBasis.PercentLabelClaim,
            SampleMatrix = matrix,
            LabelClaim = 5.0m,
            LabelClaimUnit = "mg",
            ConversionFactor = 0.8919m, // thiamine HCl to thiamine base
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 150.0m,
            LowerInclusive = true,
            UpperInclusive = true,
            DisplayOrder = 1
        };
        var specB2 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Vitamin B2",
            TestAnalyteId = b2.Id,
            ResultBasis = ResultBasis.MgPerUnit,
            SampleMatrix = matrix,
            LabelClaim = 2.0m,
            LabelClaimUnit = "mg",
            Unit = "mg",
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 1.8m,
            UpperLimit = 2.4m,
            LowerInclusive = true,
            UpperInclusive = true,
            DisplayOrder = 2
        };
        db.Specifications.AddRange(specB1, specB2);
        db.SaveChanges();

        var cause = new CauseOfTesting { Name = "Routine Release", IsActive = true };
        db.CausesOfTesting.Add(cause);

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            ReceivedAt = DateTime.UtcNow.AddDays(-1),
            CauseOfTesting = cause,
            ReceivedByUserId = fpUser.Id,
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
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = std1.Id,
            StandardWeightMg = 15.0m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.0m,
            StandardMeanArea = 100000m,
            PerformedByUserId = fpUser.Id,
            PerformedAt = DateTime.UtcNow.AddHours(-2),
            Passed = true,
            SignatureId = signature.Id
        };
        db.SystemSuitabilityRuns.Add(sstRun);
        db.SaveChanges();

        signature.EntityId = sstRun.Id;

        var runAnalyteB1 = new SystemSuitabilityRunAnalyte
        {
            SystemSuitabilityRunId = sstRun.Id,
            TestAnalyteId = b1.Id,
            AnalyteName = "Vitamin B1 (thiamine)",
            WavelengthNm = 254.0m,
            ReferenceStandardMaterialId = std1.Id,
            StandardPurityPercent = 99.0m,
            StandardWeightMg = 15.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 100000m,
            RsdPercent = 1.0m,
            TailingFactor = 1.1m,
            TheoreticalPlates = 3000m,
            Passed = true
        };
        var runAnalyteB2 = new SystemSuitabilityRunAnalyte
        {
            SystemSuitabilityRunId = sstRun.Id,
            TestAnalyteId = b2.Id,
            AnalyteName = "Vitamin B2 (riboflavin)",
            WavelengthNm = 267.0m,
            ReferenceStandardMaterialId = std2.Id,
            StandardPurityPercent = 98.5m,
            StandardWeightMg = 20.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 150000m,
            RsdPercent = 0.8m,
            Resolution = 2.5m,
            TailingFactor = 1.2m,
            TheoreticalPlates = 3500m,
            Passed = true
        };
        db.SystemSuitabilityRunAnalytes.AddRange(runAnalyteB1, runAnalyteB2);

        order.SystemSuitabilityRunId = sstRun.Id;
        db.SaveChanges();

        return (testDef, equip, col, std1, std2, b1, b2, item, sample, order, specB1, specB2, sstRun);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_WorkedExample_CalculatesExactNumbersHandChecked()
    {
        /*
         * Worked Example Checked by Hand:
         *
         * Analyte 1: Vitamin B1 (Thiamine)
         * - Standard B1:
         *     Weight W_std = 15.0 mg
         *     Purity P = 99.0 %
         *     Dilution D_std = 100.0 mL
         *     Standard Mean Area A_s = 100,000
         *     C_s = 15.0 * (99.0 / 100) / 100.0 = 0.1485 mg/mL
         *
         * - Sample (Solid):
         *     UnitAmount = 500.0 mg (tablet weight)
         *     Preparation 1: SampleAmount = 250.0 mg, Dilution = 50.0 mL
         *       Inj 1: Area A_u = 42,000
         *         Amount = (42,000 / 100,000) * 0.1485 * 50.0 * 500.0 / 250.0
         *                = 0.42 * 0.1485 * 100 = 6.237 mg/unit
         *         Converted = 6.237 * 0.8919 = 5.5627803 mg/unit
         *       Inj 2: Area A_u = 42,400
         *         Amount = (42,400 / 100,000) * 0.1485 * 50.0 * 500.0 / 250.0
         *                = 0.424 * 0.1485 * 100 = 6.2964 mg/unit
         *         Converted = 6.2964 * 0.8919 = 5.61575916 mg/unit
         *       Prep 1 Mean Amount = (6.237 + 6.2964) / 2 = 6.2667 mg/unit
         *
         *     Preparation 2: SampleAmount = 250.0 mg, Dilution = 50.0 mL
         *       Inj 1: Area A_u = 42,200
         *         Amount = (42,200 / 100,000) * 0.1485 * 50.0 * 500.0 / 250.0
         *                = 0.422 * 0.1485 * 100 = 6.2667 mg/unit
         *         Converted = 6.2667 * 0.8919 = 5.58926973 mg/unit
         *       Inj 2: Area A_u = 42,600
         *         Amount = (42,600 / 100,000) * 0.1485 * 50.0 * 500.0 / 250.0
         *                = 0.426 * 0.1485 * 100 = 6.3261 mg/unit
         *         Converted = 6.3261 * 0.8919 = 5.64224859 mg/unit
         *       Prep 2 Mean Amount = (6.2667 + 6.3261) / 2 = 6.2964 mg/unit
         *
         *     mpu (mean of preps) = (6.2667 + 6.2964) / 2 = 6.28155 mg/unit
         *     result = mpu * ConversionFactor = 6.28155 * 0.8919 = 5.602514445 mg/unit
         *     LabelClaim = 5.0 mg
         *     %LC = (5.602514445 / 5.0) * 100 = 112.0502889 %
         *     ReportedDisplay = "112.1 %"
         *
         *     RSD of Prep Means:
         *       x1 = 6.2667, x2 = 6.2964, mean = 6.28155
         *       diff1 = -0.01485, diff2 = +0.01485
         *       variance = [(-0.01485)^2 + (0.01485)^2] / (2 - 1) = 0.000441045
         *       s = sqrt(0.000441045) = 0.02100107...
         *       RSD % = (s / mean) * 100 = (0.02100107 / 6.28155) * 100 = 0.334330... %
         *       0.33% <= 2.0% -> RSD passes!
         *       ComparisonStatus: 112.05% is within 90-150% -> "WithinLimits"
         *
         * Analyte 2: Vitamin B2 (Riboflavin)
         * - Standard B2:
         *     Weight W_std = 20.0 mg, Purity P = 98.5%, Dilution D_std = 100.0 mL, A_s = 150,000
         *     C_s = 20.0 * 0.985 / 100.0 = 0.197 mg/mL
         * - Sample:
         *     Prep 1 (250 mg, 50 mL):
         *       Inj 1: Area = 38,000 -> Amount = (38000 / 150000) * 0.197 * 50 * 500 / 250 = 4.990666... mg
         *       Inj 2: Area = 38,200 -> Amount = (38200 / 150000) * 0.197 * 50 * 500 / 250 = 5.016933... mg
         *       Prep 1 Mean = 5.0038 mg
         *     Prep 2 (250 mg, 50 mL):
         *       Inj 1: Area = 38,100 -> Amount = (38100 / 150000) * 0.197 * 50 * 500 / 250 = 5.0038 mg
         *       Inj 2: Area = 38,300 -> Amount = (38300 / 150000) * 0.197 * 50 * 500 / 250 = 5.030066... mg
         *       Prep 2 Mean = 5.016933... mg
         *     mpu = (5.0038 + 5.016933...) / 2 = 5.0103666... mg
         *     result = mpu * 1.0 = 5.0103666... mg
         *     ResultBasis = MgPerUnit, Unit = "mg"
         *     ReportedDisplay = "5.0 mg"
         *     Spec range: 1.8 - 2.4 mg -> 5.01 > 2.4 -> ComparisonStatus = "OutOfSpecification"
         */

        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, _, _, _) = SetupHplcMultiScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput>
            {
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m),
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m)
            },
            UnitAmount: 500.0m,
            Areas: new List<HplcAreaInput>
            {
                // B1
                new(b1.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 42000m),
                new(b1.Id, PreparationIndex: 1, InjectionIndex: 2, Area: 42400m),
                new(b1.Id, PreparationIndex: 2, InjectionIndex: 1, Area: 42200m),
                new(b1.Id, PreparationIndex: 2, InjectionIndex: 2, Area: 42600m),
                // B2
                new(b2.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 38000m),
                new(b2.Id, PreparationIndex: 1, InjectionIndex: 2, Area: 38200m),
                new(b2.Id, PreparationIndex: 2, InjectionIndex: 1, Area: 38100m),
                new(b2.Id, PreparationIndex: 2, InjectionIndex: 2, Area: 38300m)
            },
            Password: "ValidPassword123!");

        var analyst = await db.Users.FirstAsync();
        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payload, analyst.Id);

        Assert.NotNull(result);
        Assert.Equal("OutOfSpecification", result.Status); // B2 is OOS

        // Verify database persistence
        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);

        Assert.NotNull(analysis);
        Assert.Equal(WorkflowType.HplcMultiAnalyte, analysis.AnalysisType);
        Assert.Equal(SampleMatrix.Solid, analysis.SampleMatrix);
        Assert.Equal(500.0m, analysis.UnitAmount);
        Assert.Equal(2, analysis.ParameterResults.Count);

        // Verify B1 result
        var prB1 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin B1");
        Assert.Equal("WithinLimits", prB1.ComparisonStatus);
        Assert.Equal("112.1 %", prB1.ReportedDisplay);
        Assert.NotNull(prB1.ReportedValue);
        Assert.True(Math.Abs(prB1.ReportedValue.Value - 112.0502889m) < 0.0001m);
        Assert.Equal(ResultBasis.PercentLabelClaim, prB1.ResultBasis);
        Assert.Equal(4, prB1.Readings.Count);

        // Verify B1 readings (AmountPerUnit * ConversionFactor)
        var r11 = prB1.Readings.First(r => r.Stage == 1 && r.Index == 1);
        Assert.Equal(42000m, r11.Value1);
        Assert.True(Math.Abs(r11.ComputedValue!.Value - 5.5627803m) < 0.0001m);

        // Verify B1 calculation data
        Assert.NotNull(prB1.HplcMultiAnalyteCalculation);
        Assert.Equal("Typed", prB1.HplcMultiAnalyteCalculation.UnitAmountSource);
        Assert.False(prB1.HplcMultiAnalyteCalculation.RsdExceeded);
        Assert.True(Math.Abs(prB1.HplcMultiAnalyteCalculation.PreparationRsdPercent!.Value - 0.334330m) < 0.0001m);

        // Verify B2 result
        var prB2 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin B2");
        Assert.Equal("OutOfSpecification", prB2.ComparisonStatus);
        Assert.Equal("5.0 mg", prB2.ReportedDisplay);
        Assert.True(Math.Abs(prB2.ReportedValue!.Value - 5.0103667m) < 0.0001m);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_1Vs2Preparations()
    {
        // 1 preparation test definition
        using var db1 = NewDb();
        var (testDef1, equip1, _, _, _, b1, b2, _, _, order1, _, _, _) = SetupHplcMultiScenario(db1, preps: 1, injections: 2);
        var engine1 = TestServiceFactory.TestWorkflow(db1);
        var analyst1 = await db1.Users.FirstAsync();

        var payload1Prep = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip1.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput>
            {
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m)
            },
            UnitAmount: 500.0m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 42000m),
                new(b1.Id, PreparationIndex: 1, InjectionIndex: 2, Area: 42400m),
                new(b2.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 38000m),
                new(b2.Id, PreparationIndex: 1, InjectionIndex: 2, Area: 38200m)
            },
            Password: "ValidPassword123!");

        var result1 = await engine1.RecordHplcMultiAnalyteResultAsync(order1.Id, payload1Prep, analyst1.Id);
        Assert.NotNull(result1);

        var analysis1 = await db1.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstAsync(a => a.TestOrderId == order1.Id);

        var prB1 = analysis1.ParameterResults.First(p => p.ParameterName == "Vitamin B1");
        Assert.Null(prB1.HplcMultiAnalyteCalculation!.PreparationRsdPercent);
        Assert.False(prB1.HplcMultiAnalyteCalculation.RsdExceeded);
        Assert.Equal(2, prB1.Readings.Count);

        // 2 preparations test definition
        using var db2 = NewDb();
        var (testDef2, equip2, _, _, _, b1_2, b2_2, _, _, order2, _, _, _) = SetupHplcMultiScenario(db2, preps: 2, injections: 2);
        var engine2 = TestServiceFactory.TestWorkflow(db2);
        var analyst2 = await db2.Users.FirstAsync();

        var payload2Preps = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip2.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput>
            {
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m),
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m)
            },
            UnitAmount: 500.0m,
            Areas: new List<HplcAreaInput>
            {
                new(b1_2.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 42000m),
                new(b1_2.Id, PreparationIndex: 1, InjectionIndex: 2, Area: 42400m),
                new(b1_2.Id, PreparationIndex: 2, InjectionIndex: 1, Area: 42000m),
                new(b1_2.Id, PreparationIndex: 2, InjectionIndex: 2, Area: 42400m),
                new(b2_2.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 38000m),
                new(b2_2.Id, PreparationIndex: 1, InjectionIndex: 2, Area: 38200m),
                new(b2_2.Id, PreparationIndex: 2, InjectionIndex: 1, Area: 38000m),
                new(b2_2.Id, PreparationIndex: 2, InjectionIndex: 2, Area: 38200m)
            },
            Password: "ValidPassword123!");

        var result2 = await engine2.RecordHplcMultiAnalyteResultAsync(order2.Id, payload2Preps, analyst2.Id);
        Assert.NotNull(result2);

        var analysis2 = await db2.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstAsync(a => a.TestOrderId == order2.Id);

        var prB1_2 = analysis2.ParameterResults.First(p => p.ParameterName == "Vitamin B1");
        Assert.NotNull(prB1_2.HplcMultiAnalyteCalculation!.PreparationRsdPercent);
        Assert.Equal(0m, prB1_2.HplcMultiAnalyteCalculation.PreparationRsdPercent.Value);
        Assert.Equal(4, prB1_2.Readings.Count);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_RsdExactlyAtLimitPasses_AndExceededTriggersRequiresReview()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, specB1, specB2, _) = SetupHplcMultiScenario(db, preps: 2, injections: 1, maxRsd: 2.0m);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // Adjust specs so both are WithinLimits
        specB2.LowerLimit = 1.0m;
        specB2.UpperLimit = 5.0m;
        db.SaveChanges();

        /*
         * Let Prep 1 mean = 100, Prep 2 mean = 102.857142857...
         * Let's use HplcMultiAnalyteCalculator.CalculatePreparationRsd to verify exact limit behavior:
         * Mean = (x1 + x2) / 2
         * diff = |x1 - x2| / 2
         * variance = 2 * diff^2 = 2 * (x2 - x1)^2 / 4 = (x2 - x1)^2 / 2
         * s = |x2 - x1| / sqrt(2)
         * RSD = s / mean * 100 = (|x2 - x1| / sqrt(2)) / ((x1 + x2)/2) * 100 = sqrt(2) * |x2 - x1| / (x1 + x2) * 100
         * If RSD = 2.0%:
         * sqrt(2) * (x2 - x1) / (x1 + x2) * 100 = 2.0
         */
        decimal mean1 = 100m;
        // Let x1 = 100, find x2 such that RSD is exactly 2.0%:
        // Let's test CalculatePreparationRsd directly for boundary
        var (rsdPass, exceededPass, _) = HplcMultiAnalyteCalculator.CalculatePreparationRsd(
            new List<decimal> { 100m, 100m }, 2.0m);
        Assert.Equal(0m, rsdPass);
        Assert.False(exceededPass);

        // Means 99, 100, 101: mean 100, s = 1, RSD exactly 1.0 %. At the limit passes, just above it does not.
        var (rsdAtLimit, exceededAtLimit, _) = HplcMultiAnalyteCalculator.CalculatePreparationRsd(
            new List<decimal> { 99m, 100m, 101m }, 1.0m);
        Assert.Equal(1.0m, rsdAtLimit);
        Assert.False(exceededAtLimit);
        var (_, exceededJustAbove, reason) = HplcMultiAnalyteCalculator.CalculatePreparationRsd(
            new List<decimal> { 99m, 100m, 101m }, 0.99m);
        Assert.True(exceededJustAbove);
        Assert.Contains("exceeds maximum allowed 0.99%", reason);

        // High variance that exceeds 2.0%:
        // Prep 1 area = 40000, Prep 2 area = 45000 (~11% difference)
        var payloadHighRsd = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput>
            {
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m),
                new(SampleAmount: 250.0m, SampleDilutionMl: 50.0m)
            },
            UnitAmount: 500.0m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 40000m),
                new(b1.Id, PreparationIndex: 2, InjectionIndex: 1, Area: 45000m),
                new(b2.Id, PreparationIndex: 1, InjectionIndex: 1, Area: 38000m),
                new(b2.Id, PreparationIndex: 2, InjectionIndex: 1, Area: 38000m)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadHighRsd, analyst.Id);
        Assert.Equal("RequiresReview", result.Status);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
            .FirstAsync(a => a.TestOrderId == order.Id);

        var prB1 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin B1");
        Assert.Equal("RequiresReview", prB1.ComparisonStatus);
        Assert.True(prB1.HplcMultiAnalyteCalculation!.RsdExceeded);
        Assert.Contains("exceeds maximum allowed 2.00%", prB1.HplcMultiAnalyteCalculation.ReviewReason);

        var prB2 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin B2");
        Assert.Equal("WithinLimits", prB2.ComparisonStatus);
        Assert.False(prB2.HplcMultiAnalyteCalculation!.RsdExceeded);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_WVUnitWeightUsed_AndTypedRejectedWhenWVExists()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, sample, order, _, _, _) = SetupHplcMultiScenario(db, preps: 1, injections: 1);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // Create another test order on the same sample with a finished WeightVariation result
        var wvOrder = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = "WV_TEST",
            SectionId = testDef.SectionId,
            CurrentStep = WorkflowStep.Ready,
            Status = ApprovalStatus.Approved
        };
        db.TestOrders.Add(wvOrder);
        db.SaveChanges();

        var wvSig = new ElectronicSignature
        {
            UserId = analyst.Id,
            UserFullNameSnapshot = analyst.FullName,
            UsernameSnapshot = analyst.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.ResultRecorded,
            SignedAt = DateTime.UtcNow,
            EntityType = "TestOrder",
            EntityId = wvOrder.Id
        };
        db.ElectronicSignatures.Add(wvSig);
        db.SaveChanges();

        var wvAnalysis = new TestAnalysis
        {
            TestOrderId = wvOrder.Id,
            AnalysisType = WorkflowType.WeightVariation,
            AnalysedAt = DateTime.UtcNow.AddMinutes(-30),
            IsActive = true,
            EnteredByUserId = analyst.Id,
            SignatureId = wvSig.Id,
            ParameterResults = new List<ParameterResult>
            {
                new()
                {
                    TestOrderId = wvOrder.Id,
                    ParameterName = "Weight Variation",
                    ReportedValue = 482.35m, // Mean tablet weight mg
                    ReportedDisplay = "482.4 mg",
                    ComparisonStatus = "WithinLimits",
                    IsActive = true
                }
            }
        };
        db.TestAnalyses.Add(wvAnalysis);
        db.SaveChanges();

        // 1. Supplying a typed UnitAmount when finished WV exists must be rejected
        var payloadWithTyped = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: 500m, // Typed - should be rejected!
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadWithTyped, analyst.Id));
        Assert.Contains("Unit amount cannot be supplied because a finished weight variation result exists", ex.Message);

        // 2. Omitting typed UnitAmount (null) succeeds and uses WV result
        var payloadWithoutTyped = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: null, // Null - engine looks up WV
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadWithoutTyped, analyst.Id);
        Assert.NotNull(result);

        var savedAnalysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
            .FirstAsync(a => a.TestOrderId == order.Id);

        Assert.Equal(482.35m, savedAnalysis.UnitAmount);
        var prB1 = savedAnalysis.ParameterResults.First(p => p.ParameterName == "Vitamin B1");
        Assert.Equal($"WeightVariation #{wvOrder.Id}", prB1.HplcMultiAnalyteCalculation!.UnitAmountSource);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_TypedUnitAmountRequiredWhenNoFinishedWeightVariation()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, _, _, _) = SetupHplcMultiScenario(db, preps: 1, injections: 1);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // 1. Without WV and without UnitAmount -> throws
        var payloadNoUnit = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: null,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadNoUnit, analyst.Id));
        Assert.Contains("Unit amount is required and must be greater than 0", ex1.Message);

        // 2. With typed UnitAmount -> succeeds
        var payloadWithUnit = payloadNoUnit with { UnitAmount = 505.0m };
        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadWithUnit, analyst.Id);
        Assert.NotNull(result);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
            .FirstAsync(a => a.TestOrderId == order.Id);
        Assert.Equal(505.0m, analysis.UnitAmount);
        var prB1 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin B1");
        Assert.Equal("Typed", prB1.HplcMultiAnalyteCalculation!.UnitAmountSource);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_PendingStage2WeightVariation_DoesNotCountAsFinished()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, sample, order, _, _, _) = SetupHplcMultiScenario(db, preps: 1, injections: 1);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // Active WV exists on the same sample, but its comparison status is NextStageRequired
        var wvOrder = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = "WV_TEST",
            SectionId = testDef.SectionId,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(wvOrder);
        db.SaveChanges();

        var wvSig = new ElectronicSignature
        {
            UserId = analyst.Id,
            UserFullNameSnapshot = analyst.FullName,
            UsernameSnapshot = analyst.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.ResultRecorded,
            SignedAt = DateTime.UtcNow,
            EntityType = "TestOrder",
            EntityId = wvOrder.Id
        };
        db.ElectronicSignatures.Add(wvSig);
        db.SaveChanges();

        var wvAnalysis = new TestAnalysis
        {
            TestOrderId = wvOrder.Id,
            AnalysisType = WorkflowType.WeightVariation,
            AnalysedAt = DateTime.UtcNow.AddMinutes(-30),
            IsActive = true,
            EnteredByUserId = analyst.Id,
            SignatureId = wvSig.Id,
            ParameterResults = new List<ParameterResult>
            {
                new()
                {
                    TestOrderId = wvOrder.Id,
                    ParameterName = "Weight Variation",
                    ReportedValue = 480.0m,
                    ReportedDisplay = "Stage 2 required",
                    ComparisonStatus = "NextStageRequired",
                    IsActive = true
                }
            }
        };
        db.TestAnalyses.Add(wvAnalysis);
        db.SaveChanges();

        // Null UnitAmount should throw because NextStageRequired is NOT finished
        var payloadNoUnit = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: null,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadNoUnit, analyst.Id));
        Assert.Contains("Unit amount is required", ex.Message);

        // Typed UnitAmount is accepted and source is "Typed"
        var payloadWithUnit = payloadNoUnit with { UnitAmount = 490.0m };
        var res = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadWithUnit, analyst.Id);
        Assert.NotNull(res);

        var savedAnalysis = await db.TestAnalyses.Include(a => a.ParameterResults).FirstAsync(a => a.TestOrderId == order.Id);
        Assert.Equal(490.0m, savedAnalysis.UnitAmount);
        Assert.Equal("Typed", savedAnalysis.ParameterResults.First().HplcMultiAnalyteCalculation!.UnitAmountSource);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_LiquidPath_AlwaysUsesTypedUnitAmount()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, specB1, specB2, _) = SetupHplcMultiScenario(db, preps: 1, injections: 1, matrix: SampleMatrix.Liquid);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // Liquid with null UnitAmount -> throws
        var payloadNoUnit = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Liquid,
            Preparations: new List<HplcPreparationInput> { new(SampleAmount: 5.0m, SampleDilutionMl: 50m) },
            UnitAmount: null,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadNoUnit, analyst.Id));
        Assert.Contains("Unit amount is required and must be greater than 0 for liquid samples", ex.Message);

        // Liquid with typed dose volume (e.g. 10.0 mL dose) -> succeeds
        var payloadLiquid = payloadNoUnit with { UnitAmount = 10.0m };
        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadLiquid, analyst.Id);
        Assert.NotNull(result);

        var analysis = await db.TestAnalyses.Include(a => a.ParameterResults).FirstAsync(a => a.TestOrderId == order.Id);
        Assert.Equal(SampleMatrix.Liquid, analysis.SampleMatrix);
        Assert.Equal(10.0m, analysis.UnitAmount);
        Assert.Equal("Typed", analysis.ParameterResults.First().HplcMultiAnalyteCalculation!.UnitAmountSource);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_ConversionFactor_SupportsIUAndUg()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, specB1, specB2, _) = SetupHplcMultiScenario(db, preps: 1, injections: 1);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // Change specB2 to Vitamin D3 with ConversionFactor 40000 (1 mg = 40,000 IU) and Unit = "IU"
        specB2.ParameterName = "Vitamin D3";
        specB2.ResultBasis = ResultBasis.MgPerUnit;
        specB2.Unit = "IU";
        specB2.ConversionFactor = 40000m;
        specB2.LowerLimit = 400m;
        specB2.UpperLimit = 1000m;
        db.SaveChanges();

        // Also change specB1 to Vitamin B12 with ConversionFactor 1000 (mg to ug), Unit = "ug"
        specB1.ParameterName = "Vitamin B12";
        specB1.ResultBasis = ResultBasis.MgPerUnit;
        specB1.Unit = "µg";
        specB1.ConversionFactor = 1000m;
        specB1.LowerLimit = 10m;
        specB1.UpperLimit = 50m;
        db.SaveChanges();

        var payload = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 100m),
                new(b2.Id, 1, 1, 150m)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payload, analyst.Id);
        Assert.NotNull(result);

        var analysis = await db.TestAnalyses.Include(a => a.ParameterResults).ThenInclude(pr => pr.Readings).FirstAsync(a => a.TestOrderId == order.Id);

        var prB12 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin B12");
        Assert.EndsWith("µg", prB12.ReportedDisplay);
        // ComputedValue on Reading must also reflect the conversion factor
        var b12Reading = prB12.Readings.First();
        Assert.Equal(prB12.ReportedValue, b12Reading.ComputedValue);

        var prD3 = analysis.ParameterResults.First(p => p.ParameterName == "Vitamin D3");
        Assert.EndsWith("IU", prD3.ReportedDisplay);
        var d3Reading = prD3.Readings.First();
        Assert.Equal(prD3.ReportedValue, d3Reading.ComputedValue);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_MissingOrInvalidArea_IsRejected()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, _, _, _) = SetupHplcMultiScenario(db, preps: 2, injections: 2);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        // 1. Missing area for prep 2 injection 2 of B1
        var payloadMissingArea = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m), new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b1.Id, 1, 2, 42400m),
                new(b1.Id, 2, 1, 42200m),
                // Missing (b1, 2, 2)
                new(b2.Id, 1, 1, 38000m),
                new(b2.Id, 1, 2, 38200m),
                new(b2.Id, 2, 1, 38100m),
                new(b2.Id, 2, 2, 38300m)
            },
            Password: "ValidPassword123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadMissingArea, analyst.Id));
        Assert.Contains("Expected exactly 4 areas for analyte 'Vitamin B1'", ex1.Message);

        // 2. Area <= 0
        var payloadZeroArea = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m), new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 0m), // Zero area
                new(b1.Id, 1, 2, 42400m),
                new(b1.Id, 2, 1, 42200m),
                new(b1.Id, 2, 2, 42600m),
                new(b2.Id, 1, 1, 38000m),
                new(b2.Id, 1, 2, 38200m),
                new(b2.Id, 2, 1, 38100m),
                new(b2.Id, 2, 2, 38300m)
            },
            Password: "ValidPassword123!");

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadZeroArea, analyst.Id));
        Assert.Contains("Area must be greater than zero", ex2.Message);

        // 3. Extra area for unknown analyte
        var payloadExtraAnalyte = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m), new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b1.Id, 1, 2, 42400m),
                new(b1.Id, 2, 1, 42200m),
                new(b1.Id, 2, 2, 42600m),
                new(b2.Id, 1, 1, 38000m),
                new(b2.Id, 1, 2, 38200m),
                new(b2.Id, 2, 1, 38100m),
                new(b2.Id, 2, 2, 38300m),
                new(99999, 1, 1, 1000m) // Unknown analyte
            },
            Password: "ValidPassword123!");

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadExtraAnalyte, analyst.Id));
        Assert.Contains("Areas contain an analyte not configured in specifications", ex3.Message);

        // 4. Incorrect number of preparations
        var payloadWrongPreps = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) }, // Only 1 prep when 2 expected
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, payloadWrongPreps, analyst.Id));
        Assert.Contains("Expected exactly 2 sample preparations", ex4.Message);
    }

    [Fact]
    public async Task RecordHplcMultiAnalyteResult_LinkedRunValidations_ThrowsAppropriateErrors()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, _, _, sstRun) = SetupHplcMultiScenario(db, preps: 1, injections: 1);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        var validPayload = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        // 1. Not linked to a run
        order.SystemSuitabilityRunId = null;
        db.SaveChanges();
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, validPayload, analyst.Id));
        Assert.Contains("must be linked to a system suitability run", ex1.Message);

        // 2. Linked run did not pass
        order.SystemSuitabilityRunId = sstRun.Id;
        sstRun.Passed = false;
        db.SaveChanges();
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, validPayload, analyst.Id));
        Assert.Contains("Linked system suitability run did not pass", ex2.Message);

        // 3. Linked run is for a different test method
        sstRun.Passed = true;
        sstRun.TestDefinitionId = testDef.Id + 100;
        db.SaveChanges();
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, validPayload, analyst.Id));
        Assert.Contains("Linked system suitability run is for a different test method", ex3.Message);

        // 4. Linked run is for a different laboratory section
        sstRun.TestDefinitionId = testDef.Id;
        sstRun.SectionId = testDef.SectionId + 100;
        db.SaveChanges();
        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, validPayload, analyst.Id));
        Assert.Contains("Linked system suitability run is for a different laboratory section", ex4.Message);

        // 5. Linked run missing one of the required analytes
        sstRun.SectionId = testDef.SectionId;
        var runAnalyteB2 = await db.SystemSuitabilityRunAnalytes.FirstAsync(a => a.TestAnalyteId == b2.Id);
        db.SystemSuitabilityRunAnalytes.Remove(runAnalyteB2);
        db.SaveChanges();
        var ex5 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordHplcMultiAnalyteResultAsync(order.Id, validPayload, analyst.Id));
        Assert.Contains("Linked system suitability run does not contain analyte 'Vitamin B2'", ex5.Message);
    }

    [Fact]
    public async Task Relink_AfterHplcMultiAnalyteResult_IsBlocked()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, b1, b2, _, _, order, _, _, sstRun) = SetupHplcMultiScenario(db, preps: 1, injections: 1);
        var engine = TestServiceFactory.TestWorkflow(db);
        var analyst = await db.Users.FirstAsync();

        var payload = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 42000m),
                new(b2.Id, 1, 1, 38000m)
            },
            Password: "ValidPassword123!");

        await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payload, analyst.Id);

        // Attempting to relink test order to another suitability run must fail
        var sstService = TestServiceFactory.SystemSuitability(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sstService.LinkTestOrdersAsync(sstRun.Id, new[] { order.Id }, analyst.Id));

        Assert.Contains("active result already exists", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_HplcMultiAnalyteValidations_EnforcesRules()
    {
        using var db = NewDb();
        var (fpSec, fpUser) = SeedSectionAndUser(db);

        var testDef = new TestDefinition
        {
            Code = "SPEC_TEST",
            DisplayName = "Multi Analyte Spec Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        db.SaveChanges();

        var analyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Vitamin C",
            WavelengthNm = 245m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(analyte);

        var item = new Item { Code = "ITEM-SPEC", Name = "Vitamin C 500mg", IsActive = true };
        db.Items.Add(item);
        db.SaveChanges();
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = testDef.Code });
        db.SaveChanges();

        var specService = new SpecificationService(db);

        // 1. Missing TestAnalyteId
        Specification Spec(int? analyteId, ResultBasis basis = ResultBasis.PercentLabelClaim, decimal? labelClaim = 500m, decimal conversionFactor = 1.0m) => new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Vitamin C",
            TestAnalyteId = analyteId,
            ResultBasis = basis,
            LabelClaim = labelClaim,
            ConversionFactor = conversionFactor,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var specNoAnalyte = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Vitamin C",
            TestAnalyteId = null,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 500m,
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            specService.ValidateAsync(specNoAnalyte));
        Assert.Contains("Test analyte is required for HPLC Multi-Analyte specifications", ex1.Message);

        // 2. TestAnalyteId belonging to another test definition
        var otherAnalyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id + 10,
            Element = "Other Analyte",
            WavelengthNm = 280m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(otherAnalyte);
        db.SaveChanges();

        var specWrongAnalyte = Spec(otherAnalyte.Id);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            specService.ValidateAsync(specWrongAnalyte));
        Assert.Contains("Test analyte does not belong to test", ex2.Message);

        // 3. Invalid ResultBasis (e.g. MgPerKg)
        var specMgPerKg = Spec(analyte.Id, ResultBasis.MgPerKg);
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            specService.ValidateAsync(specMgPerKg));
        Assert.Contains("Result basis must be MgPerUnit or PercentLabelClaim", ex3.Message);

        // 4. PercentLabelClaim with missing or <= 0 LabelClaim
        var specNoClaim = Spec(analyte.Id, labelClaim: 0m);
        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            specService.ValidateAsync(specNoClaim));
        Assert.Contains("Label claim must be greater than zero when result basis is PercentLabelClaim", ex4.Message);

        // 5. ConversionFactor <= 0
        var specBadFactor = Spec(analyte.Id, conversionFactor: 0m);
        var ex5 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            specService.ValidateAsync(specBadFactor));
        Assert.Contains("Conversion factor must be greater than zero", ex5.Message);

        // 6. Valid specification passes validation
        var validSpec = Spec(analyte.Id);
        await specService.ValidateAsync(validSpec);
    }
}
