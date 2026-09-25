using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed: the in-memory provider does not enforce unique indexes,
// so a suitability run code clash between two runs saved at the same moment
// can only be verified against a real database.
// The interceptor simulates a race condition: it inserts a run under the
// exact code this run just picked, between NextAsync and SaveChangesAsync.
[Collection("PostgresDatabaseCollection")]
public class SystemSuitabilityRunCodePostgresIntegrationTests
{
    private static readonly string YearStr = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
    private static readonly string Mm = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public SystemSuitabilityRunCodePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task CreateRun_CodeTakenAtTheSameMoment_TakesTheNextNumberAndSucceeds()
    {
        var methodAbbr = NewAbbr();
        var (test, equip, col, standard) = await SeedPrerequisitesAsync(methodAbbr);

        await using var db = _fixture.CreateDbContext(new TakeRunCodeBeforeSave(_fixture, test.Id, test.SectionId, equip.Id, col.Id, standard.Id, methodAbbr, times: 1));

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 2500000m,
            RsdPercent: 1.0m,
            Resolution: 2.0m,
            TailingFactor: 1.0m,
            TheoreticalPlates: 3000m,
            Password: "IntegrationPassword123!",
            Comment: "Retry test");

        var run = await service.CreateAsync(request, _fixture.SeededUserId, "127.0.0.1");

        // The first code (01) was taken by the concurrent insert; this run retried and took 02.
        Assert.Equal($"{methodAbbr} S.S 02/{Mm}{YearStr}", run.Code);

        await using var check = _fixture.CreateDbContext();
        var codes = await check.SystemSuitabilityRuns
            .Where(r => r.TestDefinitionId == test.Id)
            .OrderBy(r => r.Code)
            .Select(r => r.Code)
            .ToListAsync();

        Assert.Equal(new[] { $"{methodAbbr} S.S 01/{Mm}{YearStr}", $"{methodAbbr} S.S 02/{Mm}{YearStr}" }, codes);
    }

    [PostgresFact]
    public async Task CreateRun_CodeTakenTwiceInARow_ReportsTheClashAndSavesNothing()
    {
        var methodAbbr = NewAbbr();
        var (test, equip, col, standard) = await SeedPrerequisitesAsync(methodAbbr);

        await using var db = _fixture.CreateDbContext(new TakeRunCodeBeforeSave(_fixture, test.Id, test.SectionId, equip.Id, col.Id, standard.Id, methodAbbr, times: 2));

        var service = TestServiceFactory.SystemSuitability(db);
        var request = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 2500000m,
            RsdPercent: 1.0m,
            Resolution: 2.0m,
            TailingFactor: 1.0m,
            TheoreticalPlates: 3000m,
            Password: "IntegrationPassword123!",
            Comment: "Double retry clash test");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, _fixture.SeededUserId, "127.0.0.1"));

        Assert.Contains("was taken by another run at the same moment", ex.Message);

        // Only the 2 concurrent runs exist; this run was not saved.
        await using var check = _fixture.CreateDbContext();
        var codes = await check.SystemSuitabilityRuns
            .Where(r => r.TestDefinitionId == test.Id)
            .OrderBy(r => r.Code)
            .Select(r => r.Code)
            .ToListAsync();

        Assert.Equal(new[] { $"{methodAbbr} S.S 01/{Mm}{YearStr}", $"{methodAbbr} S.S 02/{Mm}{YearStr}" }, codes);
    }

    private static string NewAbbr() => "A" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<(TestDefinition test, Equipment equip, ChromatographyColumn col, Material standard)> SeedPrerequisitesAsync(string methodAbbr)
    {
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        // Ensure user has valid password hash
        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var test = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Integration Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            SstMaxRsdPercent = 2.0m
        };
        db.TestDefinitions.Add(test);

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
            SerialNumber = "SN-INT-1",
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
        return (test, equip, col, standard);
    }

    private sealed class TakeRunCodeBeforeSave : SaveChangesInterceptor
    {
        private readonly PostgresTestFixture _fixture;
        private readonly int _testId;
        private readonly int _sectionId;
        private readonly int _equipId;
        private readonly int _colId;
        private readonly int _matId;
        private readonly string _abbr;
        private int _remaining;

        public TakeRunCodeBeforeSave(
            PostgresTestFixture fixture,
            int testId, int sectionId, int equipId, int colId, int matId, string abbr,
            int times)
        {
            _fixture = fixture;
            _testId = testId;
            _sectionId = sectionId;
            _equipId = equipId;
            _colId = colId;
            _matId = matId;
            _abbr = abbr;
            _remaining = times;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<SystemSuitabilityRun>()
                .FirstOrDefault(e => e.State == EntityState.Added);

            if (pending is not null && _remaining > 0)
            {
                _remaining--;
                await using var concurrentDb = _fixture.CreateDbContext();

                var sig = new ElectronicSignature
                {
                    UserId = _fixture.SeededUserId,
                    UserFullNameSnapshot = "Concurrent User",
                    UsernameSnapshot = "concurrent",
                    RoleSnapshot = "Analyst",
                    MeaningOfSignature = SignatureMeaning.SuitabilityRunPerformed,
                    EntityType = "SystemSuitabilityRun",
                    EntityId = 0
                };
                concurrentDb.ElectronicSignatures.Add(sig);

                var concurrentRun = new SystemSuitabilityRun
                {
                    Code = pending.Entity.Code,
                    TestDefinitionId = _testId,
                    SectionId = _sectionId,
                    EquipmentId = _equipId,
                    ChromatographyColumnId = _colId,
                    ReferenceStandardMaterialId = _matId,
                    StandardPurityPercent = 99.5m,
                    StandardWeightMg = 50m,
                    StandardDilution = 100m,
                    StandardMeanArea = 2000000m,
                    RsdPercent = 1.0m,
                    Passed = true,
                    PerformedByUserId = _fixture.SeededUserId,
                    PerformedAt = DateTime.UtcNow,
                    Signature = sig
                };
                concurrentDb.SystemSuitabilityRuns.Add(concurrentRun);
                await concurrentDb.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
