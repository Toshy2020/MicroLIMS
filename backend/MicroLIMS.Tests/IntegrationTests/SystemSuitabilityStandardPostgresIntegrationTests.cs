using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SystemSuitabilityStandardPostgresIntegrationTests
{
    private static readonly string YearStr = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
    private static readonly string Mm = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public SystemSuitabilityStandardPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task MultiAnalyteRun_WithStandardResponsesAndWeighInWindow_RoundTripsToPostgres()
    {
        var methodAbbr = "S" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Standard Responses Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcMaxPreparationRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var equip = new Equipment
        {
            Name = $"HPLC {methodAbbr}",
            Code = $"EQ-{methodAbbr}",
            Type = EquipmentType.Hplc,
            SectionId = section.Id,
            CdsSoftware = CdsSoftware.ShimadzuLabSolutions
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = $"Column {methodAbbr}",
            Code = $"COL-{methodAbbr}",
            SerialNumber = $"SN-{methodAbbr}",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.ChromatographyColumns.Add(col);

        var std1 = new Material
        {
            MaterialName = $"Std1 {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-1-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 99.5m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        var std2 = new Material
        {
            MaterialName = $"Std2 {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-2-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 98.8m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.AddRange(std1, std2);
        await db.SaveChangesAsync();

        var b1 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Active A",
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
            Element = "Active B",
            WavelengthNm = 280.0m,
            DisplayOrder = 2,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2500m
        };
        db.TestAnalytes.AddRange(b1, b2);
        await db.SaveChangesAsync();

        // Analyte 1: Theoretical 50mg, actual 53mg (+6% deviation, > 5%, out of window, justified)
        // Responses: [2010000, 1990000, 2010000, 1990000, 2000000] -> mean = 2000000, RSD = 0.5%
        var responsesA1 = new List<decimal> { 2010000m, 1990000m, 2010000m, 1990000m, 2000000m };

        // Analyte 2: Theoretical 40mg, actual 40.5mg (+1.25% deviation, <= 5%, inside window)
        // Responses: [1500000, 1500000, 1500000] -> mean = 1500000, RSD = 0.0%
        var responsesA2 = new List<decimal> { 1500000m, 1500000m, 1500000m };

        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "IntegrationPassword123!",
            Comment: "Postgres SC-1 multi-analyte standard description run",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: b1.Id,
                    ReferenceStandardMaterialId: std1.Id,
                    StandardWeightMg: 53.0m,
                    StandardDilution: 100.0m,
                    StandardMeanArea: 0m,
                    RsdPercent: 1.0m,
                    Resolution: null,
                    TailingFactor: 1.1m,
                    TheoreticalPlates: 3000m,
                    TheoreticalWeightMg: 50.0m,
                    MoisturePercent: 0.45m,
                    WeighInJustification: "Target weight increased slightly due to sample balance linearity limit.",
                    Responses: responsesA1),
                new(
                    TestAnalyteId: b2.Id,
                    ReferenceStandardMaterialId: std2.Id,
                    StandardWeightMg: 40.5m,
                    StandardDilution: 100.0m,
                    StandardMeanArea: 0m,
                    RsdPercent: 1.2m,
                    Resolution: 2.5m,
                    TailingFactor: 1.2m,
                    TheoreticalPlates: 3200m,
                    TheoreticalWeightMg: 40.0m,
                    MoisturePercent: 1.20m,
                    WeighInJustification: null,
                    Responses: responsesA2)
            });

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");

        Assert.NotNull(run);
        Assert.True(run.Passed);
        Assert.Equal(2, run.Analytes.Count);

        // Verify persistence from a fresh DbContext instance directly against PostgreSQL
        await using var verifyDb = _fixture.CreateDbContext();
        var storedRun = await verifyDb.SystemSuitabilityRuns
            .Include(r => r.Analytes)
                .ThenInclude(a => a.Responses)
            .FirstOrDefaultAsync(r => r.Id == run.Id);

        Assert.NotNull(storedRun);
        Assert.True(storedRun.Passed);
        Assert.Null(storedRun.FailureReasons);

        // Run-level columns
        Assert.Equal(50.0m, storedRun.TheoreticalWeightMg);
        Assert.Equal(0.45m, storedRun.MoisturePercent);
        Assert.Equal(6.0m, storedRun.StandardWeighInDeviationPercent);
        Assert.True(storedRun.StandardWeighInOutOfWindow);
        Assert.Equal("Target weight increased slightly due to sample balance linearity limit.", storedRun.WeighInJustification);
        Assert.Equal(0.5m, storedRun.ComputedRsdPercent);

        // Analyte 1
        var storedA1 = storedRun.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal(50.0m, storedA1.TheoreticalWeightMg);
        Assert.Equal(0.45m, storedA1.MoisturePercent);
        Assert.Equal(6.0m, storedA1.StandardWeighInDeviationPercent);
        Assert.True(storedA1.StandardWeighInOutOfWindow);
        Assert.Equal("Target weight increased slightly due to sample balance linearity limit.", storedA1.WeighInJustification);
        Assert.Equal(2000000m, storedA1.StandardMeanArea);
        Assert.Equal(0.5m, storedA1.ComputedRsdPercent);
        Assert.Equal(1.0m, storedA1.RsdPercent); // Transcribed RSD preserved
        Assert.True(storedA1.Passed);

        // Verify child responses collection
        Assert.Equal(5, storedA1.Responses.Count);
        var orderedA1Responses = storedA1.Responses.OrderBy(r => r.Index).ToList();
        for (int i = 0; i < responsesA1.Count; i++)
        {
            Assert.Equal(i + 1, orderedA1Responses[i].Index);
            Assert.Equal(responsesA1[i], orderedA1Responses[i].Response);
            Assert.Equal(storedA1.Id, orderedA1Responses[i].SystemSuitabilityRunAnalyteId);
            Assert.True(orderedA1Responses[i].Id > 0);
        }

        // Analyte 2
        var storedA2 = storedRun.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.Equal(40.0m, storedA2.TheoreticalWeightMg);
        Assert.Equal(1.20m, storedA2.MoisturePercent);
        Assert.Equal(1.25m, storedA2.StandardWeighInDeviationPercent);
        Assert.False(storedA2.StandardWeighInOutOfWindow);
        Assert.Null(storedA2.WeighInJustification);
        Assert.Equal(1500000m, storedA2.StandardMeanArea);
        Assert.Equal(0.0m, storedA2.ComputedRsdPercent);
        Assert.Equal(1.2m, storedA2.RsdPercent);
        Assert.True(storedA2.Passed);

        Assert.Equal(3, storedA2.Responses.Count);
        var orderedA2Responses = storedA2.Responses.OrderBy(r => r.Index).ToList();
        for (int i = 0; i < responsesA2.Count; i++)
        {
            Assert.Equal(i + 1, orderedA2Responses[i].Index);
            Assert.Equal(responsesA2[i], orderedA2Responses[i].Response);
        }

        // Direct query on SystemSuitabilityStandardResponses table
        var dbResponsesCount = await verifyDb.SystemSuitabilityStandardResponses
            .CountAsync(r => r.SystemSuitabilityRunAnalyteId == storedA1.Id || r.SystemSuitabilityRunAnalyteId == storedA2.Id);
        Assert.Equal(8, dbResponsesCount);
    }

    [PostgresFact]
    public async Task SingleAnalyteRun_WithWeighInAndMoisture_RoundTripsToPostgres()
    {
        var methodAbbr = "T" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Single Analyte Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2500m,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var equip = new Equipment
        {
            Name = $"HPLC {methodAbbr}",
            Code = $"EQ-{methodAbbr}",
            Type = EquipmentType.Hplc,
            SectionId = section.Id,
            CdsSoftware = CdsSoftware.ShimadzuLabSolutions
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = $"Column {methodAbbr}",
            Code = $"COL-{methodAbbr}",
            SerialNumber = $"SN-{methodAbbr}",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.ChromatographyColumns.Add(col);

        var standard = new Material
        {
            MaterialName = $"Standard {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 99.5m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(standard);
        await db.SaveChangesAsync();

        var responses = new List<decimal> { 2010000m, 1990000m, 2010000m, 1990000m, 2000000m };

        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.5m,
            StandardDilution: 100.0m,
            StandardMeanArea: 0m,
            RsdPercent: 1.0m,
            Resolution: 2.5m,
            TailingFactor: 1.1m,
            TheoreticalPlates: 3000m,
            Password: "IntegrationPassword123!",
            Comment: "Postgres SC-1 single-analyte run",
            TheoreticalWeightMg: 50.0m,
            MoisturePercent: 0.35m,
            WeighInJustification: null,
            Responses: responses);

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");

        Assert.NotNull(run);
        Assert.True(run.Passed);
        Assert.Empty(run.Analytes);

        // Verify from fresh DbContext against PostgreSQL
        await using var verifyDb = _fixture.CreateDbContext();
        var storedRun = await verifyDb.SystemSuitabilityRuns.FirstOrDefaultAsync(r => r.Id == run.Id);

        Assert.NotNull(storedRun);
        Assert.True(storedRun.Passed);
        Assert.Equal(50.0m, storedRun.TheoreticalWeightMg);
        Assert.Equal(50.5m, storedRun.StandardWeightMg);
        Assert.Equal(1.0m, storedRun.StandardWeighInDeviationPercent);
        Assert.False(storedRun.StandardWeighInOutOfWindow);
        Assert.Null(storedRun.WeighInJustification);
        Assert.Equal(0.35m, storedRun.MoisturePercent);
        Assert.Equal(2000000m, storedRun.StandardMeanArea);
        Assert.Equal(0.5m, storedRun.ComputedRsdPercent);
        Assert.Equal(1.0m, storedRun.RsdPercent);
    }

    [PostgresFact]
    public async Task StandardResponse_DuplicateIndexOnSameAnalyte_EnforcedByPostgresUniqueConstraint()
    {
        var methodAbbr = "U" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Unique Constraint Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var equip = new Equipment
        {
            Name = $"HPLC {methodAbbr}",
            Code = $"EQ-{methodAbbr}",
            Type = EquipmentType.Hplc,
            SectionId = section.Id,
            CdsSoftware = CdsSoftware.ShimadzuLabSolutions
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = $"Column {methodAbbr}",
            Code = $"COL-{methodAbbr}",
            SerialNumber = $"SN-{methodAbbr}",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.ChromatographyColumns.Add(col);

        var std = new Material
        {
            MaterialName = $"Std {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 99.5m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(std);
        await db.SaveChangesAsync();

        var analyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Active X",
            WavelengthNm = 254.0m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(analyte);
        await db.SaveChangesAsync();

        var signature = new ElectronicSignature
        {
            UserId = _fixture.SeededUserId,
            UserFullNameSnapshot = user.FullName,
            UsernameSnapshot = user.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.SuitabilityRunPerformed,
            EntityType = "SystemSuitabilityRun",
            EntityId = 0
        };
        db.ElectronicSignatures.Add(signature);

        var run = new SystemSuitabilityRun
        {
            Code = $"{methodAbbr} S.S 01/{Mm}{YearStr}",
            TestDefinitionId = testDef.Id,
            EquipmentId = equip.Id,
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = std.Id,
            StandardPurityPercent = std.Purity!.Value,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 2000000m,
            Passed = true,
            SectionId = section.Id,
            PerformedByUserId = _fixture.SeededUserId,
            PerformedAt = DateTime.UtcNow,
            Signature = signature
        };
        db.SystemSuitabilityRuns.Add(run);
        await db.SaveChangesAsync();

        var runAnalyte = new SystemSuitabilityRunAnalyte
        {
            SystemSuitabilityRunId = run.Id,
            TestAnalyteId = analyte.Id,
            AnalyteName = analyte.Element,
            WavelengthNm = analyte.WavelengthNm,
            ReferenceStandardMaterialId = std.Id,
            StandardPurityPercent = std.Purity.Value,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 2000000m,
            Passed = true
        };
        db.SystemSuitabilityRunAnalytes.Add(runAnalyte);
        await db.SaveChangesAsync();

        // Add two responses with the same Index (1) to the same analyte row
        db.SystemSuitabilityStandardResponses.Add(new SystemSuitabilityStandardResponse
        {
            SystemSuitabilityRunAnalyteId = runAnalyte.Id,
            Index = 1,
            Response = 2000000m
        });
        db.SystemSuitabilityStandardResponses.Add(new SystemSuitabilityStandardResponse
        {
            SystemSuitabilityRunAnalyteId = runAnalyte.Id,
            Index = 1, // Duplicate Index
            Response = 2005000m
        });

        // The PostgreSQL unique index IX_SystemSuitabilityStandardResponses_SystemSuitabilityRunAnal~ must reject this
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
