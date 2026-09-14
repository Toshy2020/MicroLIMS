using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: the in-memory provider enforces no unique
// index, so a lot-number clash between two preparations saved at the same
// moment can only happen against a real database. The interceptor plays
// the other analyst - it commits a lot under the number this preparation
// just picked, after the number was chosen and before the insert runs.
[Collection("PostgresDatabaseCollection")]
public class MediaLotNumberPostgresIntegrationTests
{
    private static readonly string Yy = DateTime.UtcNow.ToString("yy", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public MediaLotNumberPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task PrepareMedia_LotNumberTakenAtTheSameMoment_TakesTheNextNumberAndAuditsOnce()
    {
        var code = NewCode();
        var (material, autoclaveId) = await SeedAsync(code);
        await using var db = _fixture.CreateDbContext(new TakeLotNumberBeforeSave(_fixture, material.Id, times: 1));

        var media = await TestServiceFactory.MediaPreparation(db).PrepareAsync(Request(material.Id, autoclaveId));

        Assert.Equal($"{code}/02/{Yy}", media.LotNumber);

        await using var check = _fixture.CreateDbContext();
        Assert.Equal(new[] { $"{code}/01/{Yy}", $"{code}/02/{Yy}" }, await LotNumbersAsync(check, material.Id));
        Assert.Equal(490m, (await check.Materials.SingleAsync(m => m.Id == material.Id)).QuantityRemaining);

        // One Media audit row per lot actually saved - none left behind by
        // the rejected attempt carrying the number it never got.
        var mediaAuditLots = await check.AuditLogs
            .Where(a => a.EntityName == nameof(Media) && a.MediaLotNumber!.StartsWith(code + "/"))
            .Select(a => a.MediaLotNumber!)
            .OrderBy(n => n)
            .ToListAsync();
        Assert.Equal(new[] { $"{code}/01/{Yy}", $"{code}/02/{Yy}" }, mediaAuditLots);
        Assert.Single(await check.AuditLogs
            .Where(a => a.EntityName == nameof(Material) && a.Action == "Update" && a.BatchNumber == material.BatchNumber)
            .ToListAsync());
    }

    [PostgresFact]
    public async Task PrepareMedia_LotNumberTakenTwiceInARow_ReportsTheClashAndSavesNothing()
    {
        var code = NewCode();
        var (material, autoclaveId) = await SeedAsync(code);
        await using var db = _fixture.CreateDbContext(new TakeLotNumberBeforeSave(_fixture, material.Id, times: 2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.MediaPreparation(db).PrepareAsync(Request(material.Id, autoclaveId)));

        Assert.Contains($"{code}/02/{Yy}", ex.Message);

        // Only the other analyst's two lots exist; this preparation left no
        // lot, no evaluation, and no stock deduction.
        await using var check = _fixture.CreateDbContext();
        Assert.Equal(new[] { $"{code}/01/{Yy}", $"{code}/02/{Yy}" }, await LotNumbersAsync(check, material.Id));
        var mediaIds = await check.Media.Where(m => m.MaterialId == material.Id).Select(m => m.Id).ToListAsync();
        Assert.Equal(0, await check.MediaEvaluations.CountAsync(e => mediaIds.Contains(e.MediaId)));
        Assert.Equal(500m, (await check.Materials.SingleAsync(m => m.Id == material.Id)).QuantityRemaining);
    }

    // Codes are unique per test so tests sharing the collection database
    // never see each other's lots.
    private static string NewCode() => "L" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();

    private async Task<(Material material, int autoclaveId)> SeedAsync(string code)
    {
        await using var db = _fixture.CreateDbContext();

        var product = new MediaProduct
        {
            Name = $"Lot number test media {code}",
            Code = code
        };
        db.MediaProducts.Add(product);
        await db.SaveChangesAsync();

        var autoclave = new Equipment { Name = $"Autoclave {code}", Code = $"AUT-{code}", Type = EquipmentType.Autoclave };
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = product.Name, ManufacturerName = "Himedia",
            BatchNumber = $"BATCH-{code}", ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = code, Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram,
            CreatedByUserId = _fixture.SeededUserId, LastModifiedByUserId = _fixture.SeededUserId,
            MediaProductId = product.Id
        };
        db.Equipment.Add(autoclave);
        db.Materials.Add(material);
        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = material.MaterialName, EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product),
            RecoveryPercentMin = 50, RecoveryPercentMax = 200
        });
        await db.SaveChangesAsync();

        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id, DocumentType = MaterialDocumentType.COA, OriginalFileName = "COA.pdf",
            StorageKey = $"material-documents/{material.Id}/test.pdf", FileExtension = ".pdf", ContentType = "application/pdf",
            FileSizeBytes = 1024, ContentSha256 = "AABBCC", UploadedByUserId = _fixture.SeededUserId, UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();

        return (material, autoclave.Id);
    }

    private PrepareMediaRequest Request(int materialId, int autoclaveId) => new(
        MaterialId: materialId,
        TotalWeight: 10m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclaveId, AutoclaveProgram: "Program A",
        LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
        Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(1), UserId: _fixture.SeededUserId);

    private static Task<List<string>> LotNumbersAsync(MicroLIMS.Persistence.DbContext.MicroLimsDbContext db, int materialId) =>
        db.Media.Where(m => m.MaterialId == materialId).Select(m => m.LotNumber).OrderBy(n => n).ToListAsync();

    // Stands in for a second analyst whose preparation commits first: on
    // each of the next `times` saves that insert a lot, it commits a lot
    // under the number that save is about to use.
    private sealed class TakeLotNumberBeforeSave : SaveChangesInterceptor
    {
        private readonly PostgresTestFixture _fixture;
        private readonly int _materialId;
        private int _remaining;

        public TakeLotNumberBeforeSave(PostgresTestFixture fixture, int materialId, int times)
        {
            _fixture = fixture;
            _materialId = materialId;
            _remaining = times;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<Media>().FirstOrDefault(e => e.State == EntityState.Added);
            if (pending is not null && _remaining > 0)
            {
                _remaining--;
                await using var otherAnalyst = _fixture.CreateDbContext();
                otherAnalyst.Media.Add(new Media
                {
                    MaterialId = _materialId, LotNumber = pending.Entity.LotNumber,
                    ExpiryDate = DateTime.UtcNow.AddMonths(1), PreparedByUserId = _fixture.SeededUserId
                });
                await otherAnalyst.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
