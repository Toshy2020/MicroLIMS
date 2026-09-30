using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: the unique index on SolutionPreparation.Code
// only exists in a real database. The interceptor plays the other analyst -
// it completes a second, independently-saved preparation of the same
// series the instant this preparation's own completion save is about to
// write its code, so both picked the same next number ("01") from what
// each saw as an empty series.
[Collection("PostgresDatabaseCollection")]
public class SolutionPreparationPostgresIntegrationTests
{
    private const string Password = "IntegrationPassword123!";
    private static readonly string YearStr = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
    private static readonly string Mm = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);

    private readonly PostgresTestFixture _fixture;

    public SolutionPreparationPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Complete_SameSeriesAtSameMoment_OneWins_OtherThrowsClash_StockDeductedOnce()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var seed = await SeedAsync(suffix);

        var interceptor = new CompleteOtherFirst(_fixture, seed.UserId);
        await using var dbA = _fixture.CreateDbContext(interceptor);
        var serviceA = TestServiceFactory.SolutionPreparation(dbA);
        var prepA = await StartAndSaveAsync(serviceA, seed, seed.LotAId);

        // A separate lot of the same reagent, so the two completions race
        // only over the Code series - not over a shared Material row, which
        // would (correctly) also raise its own xmin concurrency conflict.
        await using var dbB = _fixture.CreateDbContext();
        var serviceB = TestServiceFactory.SolutionPreparation(dbB);
        var prepB = await StartAndSaveAsync(serviceB, seed, seed.LotBId);

        interceptor.OtherPrepId = prepB.Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => serviceA.CompleteAsync(prepA.Id, new CompletePreparationRequest(Password, null), seed.UserId, null));

        Assert.Contains("Another preparation took that code", ex.Message);

        await using var check = _fixture.CreateDbContext();
        var codes = await check.SolutionPreparations
            .Where(p => p.SolutionMasterId == seed.SolutionId && p.Code != null)
            .Select(p => p.Code!)
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Single(codes);
        Assert.Equal($"MP-{seed.MethodAbbreviation} 01/{Mm}/{YearStr}", codes[0]);

        var lotA = await check.Materials.AsNoTracking().SingleAsync(m => m.Id == seed.LotAId);
        var lotB = await check.Materials.AsNoTracking().SingleAsync(m => m.Id == seed.LotBId);
        // Exactly one of the two lots was ever consumed - the losing
        // preparation's ConsumeAsync call never ran because signing (and
        // therefore everything after it) only happens once completion's
        // earlier checks pass, and the clash is detected in the same save
        // that would have persisted its own consumption.
        var consumedCount = (seed.LotQuantity - lotA.QuantityRemaining == 5m ? 1 : 0) + (seed.LotQuantity - lotB.QuantityRemaining == 5m ? 1 : 0);
        Assert.Equal(1, consumedCount);
    }

    private sealed record Seed(int SectionId, int UserId, int SolutionId, int MethodId, string MethodAbbreviation, int ComponentEntryId, int LotAId, int LotBId, decimal LotQuantity);

    private async Task<Seed> SeedAsync(string suffix)
    {
        await using var db = _fixture.CreateDbContext();

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
        user.IsActive = true;
        await db.SaveChangesAsync();

        var entry = new MaterialMasterEntry
        {
            SectionId = _fixture.SeededSectionId,
            Code = $"BUF-{suffix}",
            Name = $"Buffer {suffix}",
            Category = MaterialMasterCategory.Reagent,
            BaseUnit = MaterialUnit.Milliliter,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId,
        };
        var diluentEntry = new MaterialMasterEntry
        {
            SectionId = _fixture.SeededSectionId,
            Code = $"DIL-{suffix}",
            Name = $"Diluent component {suffix}",
            Category = MaterialMasterCategory.Reagent,
            BaseUnit = MaterialUnit.Milliliter,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId,
        };
        var standardEntry = new MaterialMasterEntry
        {
            SectionId = _fixture.SeededSectionId,
            Code = $"STD-{suffix}",
            Name = $"Standard {suffix}",
            Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Gram,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId,
        };
        db.MaterialMasterEntries.AddRange(entry, diluentEntry, standardEntry);
        await db.SaveChangesAsync();

        var solutionService = TestServiceFactory.SolutionMaster(db);
        var diluent = await solutionService.CreateAsync(new SaveSolutionMasterRequest(
            $"Diluent {suffix}", SolutionType.Diluent, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Mix.",
            new List<SolutionComponentInput> { new(diluentEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: _fixture.SeededSectionId), _fixture.SeededUserId);

        var mobilePhase = await solutionService.CreateAsync(new SaveSolutionMasterRequest(
            $"Mobile Phase {suffix}", SolutionType.MobilePhase, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Mix and filter.",
            new List<SolutionComponentInput> { new(entry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: _fixture.SeededSectionId), _fixture.SeededUserId);

        var methodAbbreviation = ("M" + suffix).ToUpperInvariant();
        var methodService = TestServiceFactory.HplcMethod(db);
        var method = await methodService.CreateAsync(new SaveHplcMethodRequest(
            $"Method {suffix}", methodAbbreviation, DateTime.UtcNow,
            "L1", 150m, 4.6m, 5m, 30m, ElutionMode.Isocratic, 1m,
            HplcDetectorType.UV, 20m, 15m, diluent.Id,
            new List<HplcMobilePhaseInput> { new("A", mobilePhase.Id, null) },
            new List<HplcGradientStepInput>(),
            new List<HplcAnalyteInput> { new(null, "Analyte", 254m, standardEntry.Id, 50m, 50m, 5) },
            SectionId: _fixture.SeededSectionId), _fixture.SeededUserId);

        var lotQuantity = 100m;
        Material NewLot(string batchSuffix) => new()
        {
            SectionId = _fixture.SeededSectionId,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Acme",
            BatchNumber = $"LOT-{suffix}-{batchSuffix}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Shelf 1",
            QuantityReceived = lotQuantity,
            QuantityRemaining = lotQuantity,
            Unit = MaterialUnit.Milliliter,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId,
        };

        var lotA = NewLot("A");
        var lotB = NewLot("B");
        db.Materials.AddRange(lotA, lotB);
        await db.SaveChangesAsync();

        return new Seed(_fixture.SeededSectionId, _fixture.SeededUserId, mobilePhase.Id, method.Id, methodAbbreviation, entry.Id, lotA.Id, lotB.Id, lotQuantity);
    }

    private static async Task<SolutionPreparationResponse> StartAndSaveAsync(SolutionPreparationService service, Seed seed, int lotId)
    {
        var prep = await service.StartAsync(new StartPreparationRequest(seed.SolutionId, seed.MethodId), seed.UserId);
        return await service.SaveAsync(
            prep.Id,
            new SavePreparationRequest(
                new List<PreparationComponentInput> { new(prep.Components[0].Id, lotId, 5m) },
                1000m, null),
            seed.UserId);
    }

    // Stands in for a second analyst whose completion commits first: the
    // instant this preparation's own completion save is about to write a
    // non-null Code, it completes the other preparation (already Saved,
    // never touched otherwise) on a brand-new DbContext.
    private sealed class CompleteOtherFirst : SaveChangesInterceptor
    {
        private readonly PostgresTestFixture _fixture;
        private readonly int _userId;
        private bool _fired;

        public int OtherPrepId { get; set; }

        public CompleteOtherFirst(PostgresTestFixture fixture, int userId)
        {
            _fixture = fixture;
            _userId = userId;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<SolutionPreparation>()
                .FirstOrDefault(e => e.State == EntityState.Modified && e.Entity.Code != null);

            if (pending is not null && !_fired && OtherPrepId != 0)
            {
                _fired = true;
                await using var otherDb = _fixture.CreateDbContext();
                var otherService = TestServiceFactory.SolutionPreparation(otherDb);
                await otherService.CompleteAsync(
                    OtherPrepId, new CompletePreparationRequest(Password, null), _userId, null, cancellationToken);
            }

            return result;
        }
    }
}
