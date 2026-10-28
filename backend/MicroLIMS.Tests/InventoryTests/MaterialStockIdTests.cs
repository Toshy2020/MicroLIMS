using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Search;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Master Testing Materials phase 5: the system-generated stock ID (STK-{lab-local year}-{seq:D5}) on every received lot,
// its search by scan, and the strain fields on the cryovial summary.
public class MaterialStockIdTests
{
    private static readonly DateTimeOffset NewYearsEveUtc = new(2026, 12, 31, 22, 30, 0, TimeSpan.Zero);

    private class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };

    private static async Task<(MicroLimsDbContext db, int userId)> SeedAsync()
    {
        var db = NewDb();
        TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { Username = "analyst", FullName = "Analyst", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (db, user.Id);
    }

    private static SaveMaterialRequest Receive(string batch = "LOT-1", string name = "Supplement X") =>
        new(MaterialType.DehydratedMedia, name, "Merck", batch, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, "Shelf 1", 10m,
            MaterialUnit.Gram, null, null, null, MediaProductId: null);

    private static async Task<SaveMaterialRequest> ReceiveMediaAsync(MicroLimsDbContext db, string batch)
    {
        var product = await db.MediaProducts.FirstOrDefaultAsync();
        if (product is null)
        {
            product = new MediaProduct { Name = "TSA", Code = "TSA", IsActive = true };
            db.MediaProducts.Add(product);
            await db.SaveChangesAsync();
        }
        return Receive(batch) with { MediaProductId = product.Id };
    }

    [Fact]
    public async Task Receipt_GeneratesFormattedUniqueIds_ForEveryNewLot()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = new MaterialService(db, new UserSectionScopeService(db), new DatabaseSequenceHelper(db));

        var a = await service.CreateAsync(await ReceiveMediaAsync(db, "A"), userId);
        var b = await service.CreateAsync(await ReceiveMediaAsync(db, "B"), userId);

        Assert.Matches(@"^STK-\d{4}-\d{5}$", a.StockId);
        Assert.Matches(@"^STK-\d{4}-\d{5}$", b.StockId);
        Assert.NotEqual(a.StockId, b.StockId);
        Assert.Equal(a.StockId, (await db.Materials.SingleAsync(m => m.Id == a.Id)).StockId);
        // One running number: the second lot's number is the first's plus one.
        Assert.Equal(long.Parse(a.StockId![^5..]) + 1, long.Parse(b.StockId![^5..]));
    }

    [Fact]
    public async Task Receipt_PutsTheLabLocalYearOfReceiptInTheId()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        // 22:30 UTC on 31 Dec is already 1 Jan in the Cairo lab.
        var service = new MaterialService(db, new UserSectionScopeService(db), new DatabaseSequenceHelper(db),
            new FixedTime(NewYearsEveUtc), new LabClock(new FixedTime(NewYearsEveUtc), LabClock.ResolveTimeZone("Africa/Cairo")));

        var lot = await service.CreateAsync(await ReceiveMediaAsync(db, "A"), userId);

        Assert.StartsWith("STK-2027-", lot.StockId);
    }

    [Fact]
    public void Format_PadsTheNumber_AndKeepsLongerOnes()
    {
        Assert.Equal("STK-2026-00123", StockIdGenerator.Format(2026, 123));
        Assert.Equal("STK-2026-100000", StockIdGenerator.Format(2026, 100000));
        Assert.True(StockIdGenerator.Format(2026, 99999999).Length <= 20);
    }

    [Fact]
    public async Task Update_NeverChangesTheStockId()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = new MaterialService(db, new UserSectionScopeService(db), new DatabaseSequenceHelper(db));
        var request = await ReceiveMediaAsync(db, "A");
        var lot = await service.CreateAsync(request, userId);
        var before = lot.StockId;

        await service.UpdateAsync(lot.Id, request with { Location = "Shelf 9", BatchNumber = "A2" }, userId);

        var reloaded = await db.Materials.AsNoTracking().SingleAsync(m => m.Id == lot.Id);
        Assert.Equal("Shelf 9", reloaded.Location);
        Assert.Equal(before, reloaded.StockId);
        // No request field carries it.
        Assert.DoesNotContain(typeof(SaveMaterialRequest).GetProperties(), p => p.Name == "StockId");
    }

    [Fact]
    public async Task GlobalSearch_FindsTheLotByExactOrPartialStockId()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = new MaterialService(db, new UserSectionScopeService(db), new DatabaseSequenceHelper(db));
        var target = await service.CreateAsync(await ReceiveMediaAsync(db, "ZZ-777"), userId);
        await service.CreateAsync(await ReceiveMediaAsync(db, "ZZ-778"), userId);
        var search = new GlobalSearchService(db, new UserSectionScopeService(db));

        var exact = await search.SearchAsync(new GlobalSearchRequest(target.StockId!.ToLowerInvariant()), userId);
        var partial = await search.SearchAsync(new GlobalSearchRequest(target.StockId![^7..]), userId);

        var hit = Assert.Single(Assert.Single(exact.Categories, c => c.CategoryName == "Materials").Items);
        Assert.Equal(target.Id, hit.Id);
        Assert.Equal("StockId", hit.MatchedField);
        Assert.Contains(partial.Categories.Single(c => c.CategoryName == "Materials").Items, i => i.Id == target.Id);
    }

    [Fact]
    public async Task CryovialSummary_CarriesTheReferenceStrainFields()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var micro = await db.DocumentSections.FirstAsync();
        var organism = new Organism { ScientificName = "Escherichia coli", AtccNumber = "8739" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();
        var strain = new ReferenceOrganism
        {
            Code = "ORG-001", OrganismId = organism.Id, CultureCollection = CultureCollection.ATCC, StrainNumber = "8739",
            OwnerSectionId = micro.Id, IsActive = true, CreatedByUserId = userId, LastModifiedByUserId = userId,
        };
        db.ReferenceOrganisms.Add(strain);
        await db.SaveChangesAsync();
        var lot = new Material
        {
            SectionId = micro.Id, MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = "E. coli", BatchNumber = "L1",
            OrganismId = organism.Id, ReferenceOrganismId = strain.Id, QuantityReceived = 1, QuantityRemaining = 1, CreatedByUserId = userId, LastModifiedByUserId = userId,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        var withStrain = new Cryovial
        {
            Code = "C1", MaterialId = lot.Id, OrganismId = organism.Id, OrganismNameSnapshot = "Escherichia coli", ReferenceOrganismId = strain.Id,
            ExpiryDate = DateTime.UtcNow.AddYears(1), NumberOfVialsPrepared = 2, VialsRemaining = 2, PreparedByUserId = userId,
        };
        var legacy = new Cryovial
        {
            Code = "C2", MaterialId = lot.Id, OrganismId = organism.Id, OrganismNameSnapshot = "Escherichia coli",
            ExpiryDate = DateTime.UtcNow.AddYears(1), NumberOfVialsPrepared = 2, VialsRemaining = 2, PreparedByUserId = userId,
        };
        db.Cryovials.AddRange(withStrain, legacy);
        await db.SaveChangesAsync();
        var summaries = TestServiceFactory.CryovialSummary(db);

        var summary = (await summaries.GetSummaryAsync(withStrain.Id))!;
        var old = (await summaries.GetSummaryAsync(legacy.Id))!;

        Assert.Equal("ORG-001", summary.ReferenceOrganismCode);
        Assert.Equal(CultureCollection.ATCC, summary.CultureCollection);
        Assert.Equal("8739", summary.StrainNumber);
        Assert.Equal("ORG-001 (ATCC 8739)", CryovialSummaryService.StrainText(summary));
        Assert.Null(old.ReferenceOrganismCode);
        Assert.Null(old.CultureCollection);
        Assert.Equal("-", CryovialSummaryService.StrainText(old));
        var report = await summaries.BuildReportDocumentAsync(withStrain.Id);
        var identity = report!.Blocks.OfType<MicroLIMS.Application.Abstractions.Pdf.TwoColumnBlock>().First().Left;
        Assert.Contains((("Reference strain", "ORG-001 (ATCC 8739)")), identity);
    }
}
