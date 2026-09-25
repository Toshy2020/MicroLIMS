using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: the unique index on Cryovial.Code only
// exists in a real database. The interceptor plays the other analyst - it
// commits a batch under the code this preparation just picked, after the
// code was chosen and before the insert runs.
[Collection("PostgresDatabaseCollection")]
public class CryovialCodePostgresIntegrationTests
{
    private static readonly string Yy = DateTime.UtcNow.ToString("yy", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public CryovialCodePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task UniqueIndex_RejectsASecondBatchWithTheSameCode()
    {
        var code = NewCode();
        var seed = await SeedAsync(code);

        await using (var db = _fixture.CreateDbContext())
        {
            db.Cryovials.Add(OtherAnalystsBatch(seed, $"{code}/01/{Yy}"));
            await db.SaveChangesAsync();
        }

        await using var duplicate = _fixture.CreateDbContext();
        duplicate.Cryovials.Add(OtherAnalystsBatch(seed, $"{code}/01/{Yy}"));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task PrepareCryovials_CodeTakenAtTheSameMoment_TakesTheNextCodeAndAuditsOnce()
    {
        var code = NewCode();
        var seed = await SeedAsync(code);
        await using var db = _fixture.CreateDbContext(new TakeCodeBeforeSave(this, seed, times: 1));

        var cryovial = await TestServiceFactory.Cryovial(db).PrepareCryovialsAsync(Request(seed));

        Assert.Equal($"{code}/02/{Yy}", cryovial.Code);

        await using var check = _fixture.CreateDbContext();
        Assert.Equal(new[] { $"{code}/01/{Yy}", $"{code}/02/{Yy}" }, await CodesAsync(check, seed.MaterialId));
        Assert.Equal(9m, (await check.Materials.SingleAsync(m => m.Id == seed.MaterialId)).QuantityRemaining);
        var saved = await check.Cryovials.Include(c => c.IdentityConfirmations).SingleAsync(c => c.Code == cryovial.Code);
        Assert.Single(saved.IdentityConfirmations);

        // One Cryovial audit row per batch actually saved - none left behind
        // by the rejected attempt carrying the code it never got.
        var auditCodes = await check.AuditLogs
            .Where(a => a.EntityName == nameof(Cryovial) && a.CryovialCode!.StartsWith(code + "/"))
            .Select(a => a.CryovialCode!)
            .OrderBy(c => c)
            .ToListAsync();
        Assert.Equal(new[] { $"{code}/01/{Yy}", $"{code}/02/{Yy}" }, auditCodes);
    }

    [PostgresFact]
    public async Task PrepareCryovials_CodeTakenTwiceInARow_ReportsTheClashAndSavesNothing()
    {
        var code = NewCode();
        var seed = await SeedAsync(code);
        await using var db = _fixture.CreateDbContext(new TakeCodeBeforeSave(this, seed, times: 2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.Cryovial(db).PrepareCryovialsAsync(Request(seed)));

        Assert.Contains($"{code}/02/{Yy}", ex.Message);

        // Only the other analyst's two batches exist; this preparation left
        // no batch, no identity confirmations, and no disc deduction.
        await using var check = _fixture.CreateDbContext();
        Assert.Equal(new[] { $"{code}/01/{Yy}", $"{code}/02/{Yy}" }, await CodesAsync(check, seed.MaterialId));
        Assert.All(
            await check.Cryovials.Include(c => c.IdentityConfirmations).Where(c => c.MaterialId == seed.MaterialId).ToListAsync(),
            c => Assert.Empty(c.IdentityConfirmations));
        Assert.Equal(10m, (await check.Materials.SingleAsync(m => m.Id == seed.MaterialId)).QuantityRemaining);
    }

    private sealed record Seed(int MaterialId, int OrganismId, string OrganismName, int PanelMediaId, int IncubatorId);

    // Codes (and every other identifying value) are unique per test so
    // tests sharing the collection database never see each other's rows.
    private static string NewCode() => "C" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();

    private async Task<Seed> SeedAsync(string code)
    {
        await using var db = _fixture.CreateDbContext();

        var organism = new Organism { ScientificName = $"Pseudomonas aeruginosa {code}", AtccNumber = $"ATCC {code}" };
        var microSectionId = (await db.DocumentSections.FirstAsync(s => s.Code == "MICRO")).Id;
        var incubator = new Equipment { SectionId = microSectionId, Name = $"Incubator {code}", Code = $"INC-{code}", Type = EquipmentType.Incubator };
        var panelMaterial = new Material
        {
            SectionId = microSectionId,
            MaterialType = MaterialType.DehydratedMedia, MaterialName = $"Panel media {code}", ManufacturerName = "Himedia",
            BatchNumber = $"PANEL-{code}", ReceivingDate = DateTime.UtcNow.AddDays(-30), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = $"P{code}", Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram,
            CreatedByUserId = _fixture.SeededUserId, LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Organisms.Add(organism);
        db.Equipment.Add(incubator);
        db.Materials.Add(panelMaterial);
        await db.SaveChangesAsync();

        var material = new Material
        {
            SectionId = microSectionId,
            MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = organism.ScientificName, ManufacturerName = "Tody laboratories",
            BatchNumber = $"BATCH-{code}", ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = code, AtccNumber = organism.AtccNumber, OrganismId = organism.Id,
            Location = "Micro refrigerator", QuantityReceived = 10, QuantityRemaining = 10, Unit = MaterialUnit.Disc,
            CreatedByUserId = _fixture.SeededUserId, LastModifiedByUserId = _fixture.SeededUserId
        };
        var panelMedia = new Media
        {
            MaterialId = panelMaterial.Id, LotNumber = $"P{code}/01/{Yy}", IsReleasedForUse = true, Status = MediaStatus.Active,
            ExpiryDate = DateTime.UtcNow.AddYears(1), PreparedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(material);
        db.Media.Add(panelMedia);
        await db.SaveChangesAsync();

        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id, DocumentType = MaterialDocumentType.COA, OriginalFileName = "COA.pdf",
            StorageKey = $"material-documents/{material.Id}/test.pdf", FileExtension = ".pdf", ContentType = "application/pdf",
            FileSizeBytes = 1024, ContentSha256 = "AABBCC", UploadedByUserId = _fixture.SeededUserId, UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();

        return new Seed(material.Id, organism.Id, organism.ScientificName, panelMedia.Id, incubator.Id);
    }

    private PrepareCryovialsRequest Request(Seed seed) => new(
        seed.MaterialId, NumberOfVialsPrepared: 5, ExpiryDate: DateTime.UtcNow.AddMonths(6),
        StorageCondition: "Freezer -15 to -25", PhysicalCheckConfirmed: true, PhysicalCheckText: "Conforms to reference description",
        Panel: new List<IdentityConfirmationRow>
        {
            new(seed.PanelMediaId, seed.IncubatorId, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), "Typical colonies")
        },
        DiscsUsed: 1, UserId: _fixture.SeededUserId);

    private Cryovial OtherAnalystsBatch(Seed seed, string code) => new()
    {
        Code = code, MaterialId = seed.MaterialId, OrganismId = seed.OrganismId, OrganismNameSnapshot = seed.OrganismName,
        ManufacturerName = "Tody laboratories", ExpiryDate = DateTime.UtcNow.AddMonths(6), NumberOfVialsPrepared = 5,
        VialsRemaining = 5, StorageCondition = "Freezer -15 to -25", PreparedAt = DateTime.UtcNow, PreparedByUserId = _fixture.SeededUserId
    };

    private static Task<List<string>> CodesAsync(MicroLimsDbContext db, int materialId) =>
        db.Cryovials.Where(c => c.MaterialId == materialId).Select(c => c.Code).OrderBy(c => c).ToListAsync();

    // Stands in for a second analyst whose preparation commits first: on
    // each of the next `times` saves that insert a batch, it commits a
    // batch under the code that save is about to use.
    private sealed class TakeCodeBeforeSave : SaveChangesInterceptor
    {
        private readonly CryovialCodePostgresIntegrationTests _test;
        private readonly Seed _seed;
        private int _remaining;

        public TakeCodeBeforeSave(CryovialCodePostgresIntegrationTests test, Seed seed, int times)
        {
            _test = test;
            _seed = seed;
            _remaining = times;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<Cryovial>().FirstOrDefault(e => e.State == EntityState.Added);
            if (pending is not null && _remaining > 0)
            {
                _remaining--;
                await using var otherAnalyst = _test._fixture.CreateDbContext();
                otherAnalyst.Cryovials.Add(_test.OtherAnalystsBatch(_seed, pending.Entity.Code));
                await otherAnalyst.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
