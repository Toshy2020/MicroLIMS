using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class MediaAppearanceSnapshotTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static MediaAppearanceSnapshotService Service(MicroLimsDbContext db) =>
        new(db, NullLogger<MediaAppearanceSnapshotService>.Instance);

    private static async Task<(int materialId, int organismId)> SeedAsync(MicroLimsDbContext db, string? expectedDescription)
    {
        var organism = new Organism { ScientificName = "Escherichia coli" };
        db.Organisms.Add(organism);
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "EMB Agar", ManufacturerName = "Himedia",
            BatchNumber = "B-1", ReceivingDate = DateTime.UtcNow, Location = "Micro Lab",
            QuantityReceived = 100, QuantityRemaining = 100, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        if (expectedDescription is not null)
        {
            db.MediaConfigurations.Add(new MediaConfiguration
            {
                Name = "EMB Agar", EvaluationType = EvaluationType.IndicationInhibition,
                IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35,
                Challenges = new List<MediaConfigurationChallenge>
                {
                    new() { OrganismId = organism.Id, ExpectedDescription = expectedDescription }
                }
            });
            await db.SaveChangesAsync();
        }

        return (material.Id, organism.Id);
    }

    [Fact]
    public async Task ReturnsExpectedDescription_WhenSpecExists()
    {
        await using var db = NewDb();
        var (materialId, organismId) = await SeedAsync(db, "Metallic green sheen colonies, 1-2 mm");

        var snapshot = await Service(db).GetExpectedAppearanceSnapshotAsync(materialId, organismId);

        Assert.Equal("Metallic green sheen colonies, 1-2 mm", snapshot);
    }

    [Fact]
    public async Task ReturnsNull_WhenNoSpecExists()
    {
        await using var db = NewDb();
        var (materialId, organismId) = await SeedAsync(db, null);

        Assert.Null(await Service(db).GetExpectedAppearanceSnapshotAsync(materialId, organismId));
    }

    [Fact]
    public async Task ReturnsNull_WhenSpecExistsForADifferentOrganism()
    {
        await using var db = NewDb();
        var (materialId, _) = await SeedAsync(db, "Metallic green sheen colonies");
        var other = new Organism { ScientificName = "Salmonella enterica" };
        db.Organisms.Add(other);
        await db.SaveChangesAsync();

        Assert.Null(await Service(db).GetExpectedAppearanceSnapshotAsync(materialId, other.Id));
    }

    [Fact]
    public async Task ReturnsNull_WhenMaterialIsUnknown()
    {
        await using var db = NewDb();
        var (_, organismId) = await SeedAsync(db, "Metallic green sheen colonies");

        Assert.Null(await Service(db).GetExpectedAppearanceSnapshotAsync(999, organismId));
    }
}
