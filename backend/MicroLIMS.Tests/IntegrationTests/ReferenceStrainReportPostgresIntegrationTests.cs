using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose. The rest of the reference-strain report is
// covered by MediaGptAndReferenceStrainReportTests against the in-memory
// provider, which does no SQL translation at all - so a query that EF Core
// cannot translate passes there and throws only once it reaches a real
// database. That is exactly how the filter-options query below reached
// production and failed every call with
// "The LINQ expression ... could not be translated".
[Collection("PostgresDatabaseCollection")]
public class ReferenceStrainReportPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public ReferenceStrainReportPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task GetFilterOptionsAsync_TranslatesToSqlAndDeduplicatesOrganisms()
    {
        await using var db = _fixture.CreateDbContext();

        var salmonella = new Organism { ScientificName = "Salmonella enterica", AtccNumber = "ATCC 14028" };
        var ecoli = new Organism { ScientificName = "Escherichia coli", AtccNumber = "ATCC 8739" };
        db.Organisms.AddRange(salmonella, ecoli);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.LyophilizedMicroorganism,
            MaterialName = "Reference strain stock",
            ManufacturerName = "Test manufacturer",
            BatchNumber = $"RSS-{Guid.NewGuid():n}",
            ReceivingDate = DateTime.UtcNow,
            Location = "Freezer",
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        // Two cryovials of the SAME organism - the Distinct() has to collapse
        // them to one option, and a third of a different organism gives the
        // OrderBy something to sort.
        db.Cryovials.AddRange(
            NewCryovial(material.Id, salmonella, "A"),
            NewCryovial(material.Id, salmonella, "B"),
            NewCryovial(material.Id, ecoli, "C"));
        await db.SaveChangesAsync();

        var service = new ReferenceStrainReportService(db);

        // Before the fix this threw InvalidOperationException at this call.
        var options = await service.GetFilterOptionsAsync();

        var organisms = options.Organisms
            .Where(o => o.Id == salmonella.Id || o.Id == ecoli.Id)
            .ToList();

        Assert.Equal(2, organisms.Count);
        Assert.Single(organisms, o => o.Id == salmonella.Id);
        Assert.Equal(
            new[] { "Escherichia coli", "Salmonella enterica" },
            organisms.Select(o => o.ScientificName).ToArray());
        Assert.Equal("ATCC 14028", organisms.Single(o => o.Id == salmonella.Id).AtccNumber);
    }

    private Cryovial NewCryovial(int materialId, Organism organism, string suffix) => new()
    {
        Code = $"CRY-{Guid.NewGuid():n}-{suffix}",
        MaterialId = materialId,
        OrganismId = organism.Id,
        OrganismNameSnapshot = organism.ScientificName,
        ManufacturerName = "Test manufacturer",
        ExpiryDate = DateTime.UtcNow.AddYears(1),
        NumberOfVialsPrepared = 5,
        VialsRemaining = 5,
        StorageCondition = "-70C",
        PreparedAt = DateTime.UtcNow,
        PreparedByUserId = _fixture.SeededUserId
    };
}
