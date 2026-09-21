using System.Globalization;
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
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class HplcMultiAnalytePostgresIntegrationTests
{
    private static readonly string YearStr = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
    private static readonly string Mm = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public HplcMultiAnalytePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task CreateHplcMultiAnalyteSuitabilityRun_PostgresRoundTrip_SavesAnalytesAndPreventsDelete()
    {
        var methodAbbr = "M" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Multi-Analyte Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcPreparations = 2,
            HplcInjectionsPerPreparation = 2,
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
            TestDefinitionId = testDef.Id,
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
        await db.SaveChangesAsync();

        // Create suitability run through service
        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "IntegrationPassword123!",
            Comment: "Postgres multi-analyte run",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.2m, 2.5m, 1.2m, 3200m)
            });

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");

        Assert.NotNull(run);
        Assert.True(run.Passed);
        Assert.Equal($"{methodAbbr} S.S 01/{Mm}{YearStr}", run.Code);
        Assert.Equal(2, run.Analytes.Count);

        // Verify round trip from fresh DbContext
        await using var verifyDb = _fixture.CreateDbContext();
        var storedRun = await verifyDb.SystemSuitabilityRuns
            .Include(r => r.Analytes)
            .FirstOrDefaultAsync(r => r.Id == run.Id);

        Assert.NotNull(storedRun);
        Assert.True(storedRun.Passed);
        Assert.Equal(2, storedRun.Analytes.Count);

        var storedB1 = storedRun.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal("Vitamin B1", storedB1.AnalyteName);
        Assert.Equal(254.0m, storedB1.WavelengthNm);
        Assert.Equal(99.5m, storedB1.StandardPurityPercent);
        Assert.Equal(50.0m, storedB1.StandardWeightMg);
        Assert.Equal(100.0m, storedB1.StandardDilution);
        Assert.Equal(2500000m, storedB1.StandardMeanArea);
        Assert.True(storedB1.Passed);

        var storedB2 = storedRun.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.Equal("Vitamin B2", storedB2.AnalyteName);
        Assert.Equal(267.0m, storedB2.WavelengthNm);
        Assert.Equal(98.8m, storedB2.StandardPurityPercent);
        Assert.Equal(45.0m, storedB2.StandardWeightMg);
        Assert.Equal(100.0m, storedB2.StandardDilution);
        Assert.Equal(1800000m, storedB2.StandardMeanArea);
        Assert.True(storedB2.Passed);

        // Verify that deleting an analyte referenced by the suitability run deactivates it in Postgres
        var scope = new UserSectionScopeService(verifyDb);
        var colService = new ChromatographyColumnService(verifyDb, scope);
        var masterDataController = new MasterDataController(
            verifyDb,
            new EquipmentConfigurationService(verifyDb),
            TestServiceFactory.MediaProduct(verifyDb),
            TestServiceFactory.MediaIncubationCondition(verifyDb),
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

        var deleteRes = await masterDataController.DeleteTestAnalyte(testDef.Id, b1.Id);
        Assert.IsType<OkObjectResult>(deleteRes);

        await using var checkDb = _fixture.CreateDbContext();
        var deactivatedB1 = await checkDb.TestAnalytes.FindAsync(b1.Id);
        Assert.NotNull(deactivatedB1);
        Assert.False(deactivatedB1.IsActive);
    }
}
