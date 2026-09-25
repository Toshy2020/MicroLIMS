using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Prepared media lots and cryovials belong to the laboratory section of the
// material they were made from: a test or a GPT challenge may only use its
// own section's, and lists/actions follow the user's sections.
public class MediaCryovialSectionScopeTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record World(
        int Micro, int Fp, int MicroUser, int Admin,
        Media MicroLot, Media FpLot, Cryovial MicroVial, Cryovial FpVial,
        Material MicroMaterial, Material FpMaterial, MediaEvaluationChallenge MicroChallenge, MediaEvaluationChallenge FpChallenge);

    private static async Task<World> SeedAsync(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);

        var analyst = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin" };
        db.Roles.AddRange(analyst, adminRole);
        await db.SaveChangesAsync();
        db.Users.AddRange(
            new User { Id = 1, FullName = "Micro Analyst", Username = "micro", RoleId = analyst.Id },
            new User { Id = 2, FullName = "Admin", Username = "admin", RoleId = adminRole.Id });
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, 1);

        var organism = new Organism { ScientificName = "E. coli" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        Material NewMaterial(int sectionId, string name) => new()
        {
            SectionId = sectionId, MaterialName = name, MaterialType = MaterialType.LyophilizedMicroorganism, OrganismId = organism.Id,
            BatchNumber = $"B-{name}", ExpiryDate = DateTime.UtcNow.AddYears(1), QuantityReceived = 10, QuantityRemaining = 10
        };
        var microMaterial = NewMaterial(micro.Id, "Micro TSA");
        var fpMaterial = NewMaterial(fp.Id, "FP TSA");
        db.Materials.AddRange(microMaterial, fpMaterial);
        await db.SaveChangesAsync();

        Media NewLot(Material m, string lot) => new()
        {
            MaterialId = m.Id, LotNumber = lot, IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var microLot = NewLot(microMaterial, "TSA/1/26");
        var fpLot = NewLot(fpMaterial, "TSA/2/26");
        db.Media.AddRange(microLot, fpLot);

        Cryovial NewVial(Material m, string code) => new()
        {
            MaterialId = m.Id, Code = code, OrganismId = organism.Id, ApprovalStatus = ApprovalGateStatus.Approved,
            ExpiryDate = DateTime.UtcNow.AddDays(30), NumberOfVialsPrepared = 5, VialsRemaining = 5
        };
        var microVial = NewVial(microMaterial, "EC/01/26");
        var fpVial = NewVial(fpMaterial, "EC/02/26");
        db.Cryovials.AddRange(microVial, fpVial);
        await db.SaveChangesAsync();

        MediaEvaluationChallenge NewChallenge(Media lot)
        {
            var evaluation = new MediaEvaluation { MediaId = lot.Id, EvaluationType = EvaluationType.GrowthPromotion };
            var challenge = new MediaEvaluationChallenge { OrganismId = organism.Id };
            evaluation.Challenges.Add(challenge);
            db.MediaEvaluations.Add(evaluation);
            return challenge;
        }
        var microChallenge = NewChallenge(microLot);
        var fpChallenge = NewChallenge(fpLot);
        await db.SaveChangesAsync();

        return new World(micro.Id, fp.Id, 1, 2, microLot, fpLot, microVial, fpVial, microMaterial, fpMaterial, microChallenge, fpChallenge);
    }

    [Fact]
    public async Task Rule_ALotOrMaterialFromAnotherSection_CannotBeUsedForATest()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var microLot = await db.Media.Include(m => m.Material).FirstAsync(m => m.Id == w.MicroLot.Id);

        SectionMediaRule.EnsureLot(microLot, w.Micro);
        var ex = Assert.Throws<InvalidOperationException>(() => SectionMediaRule.EnsureLot(microLot, w.Fp));
        Assert.Contains("TSA/1/26", ex.Message);

        await SectionMediaRule.EnsureLotsAsync(db, new[] { w.MicroLot.Id }, w.Micro);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SectionMediaRule.EnsureLotsAsync(db, new[] { w.MicroLot.Id, w.FpLot.Id }, w.Micro));

        await SectionMediaRule.EnsureMaterialsAsync(db, new[] { w.MicroMaterial.Id }, w.Micro);
        var materialEx = await Assert.ThrowsAsync<InvalidOperationException>(() => SectionMediaRule.EnsureMaterialsAsync(db, new[] { w.FpMaterial.Id }, w.Micro));
        Assert.Contains("FP TSA", materialEx.Message);
    }

    [Fact]
    public async Task Guards_FollowTheSourceMaterialsSection()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var scope = new UserSectionScopeService(db);

        await scope.EnsureMediaAccessAsync(w.MicroUser, w.MicroLot.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.EnsureMediaAccessAsync(w.MicroUser, w.FpLot.Id));
        await scope.EnsureMediaAccessAsync(w.Admin, w.FpLot.Id);
        await scope.EnsureMediaAccessAsync(w.MicroUser, 999); // unknown id: left to not-found handling

        await scope.EnsureCryovialAccessAsync(w.MicroUser, w.MicroVial.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.EnsureCryovialAccessAsync(w.MicroUser, w.FpVial.Id));

        await scope.EnsureMediaEvaluationChallengeAccessAsync(w.MicroUser, w.MicroChallenge.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.EnsureMediaEvaluationChallengeAccessAsync(w.MicroUser, w.FpChallenge.Id));
    }

    [Fact]
    public async Task Lists_ShowOnlyTheUsersSections()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var microOnly = new[] { w.Micro };

        var released = await TestServiceFactory.MediaPreparation(db).GetReleasedAsync(sectionIds: microOnly);
        Assert.Equal(new[] { w.MicroLot.Id }, released.Select(m => m.Id).ToArray());
        Assert.Equal(2, (await TestServiceFactory.MediaPreparation(db).GetReleasedAsync()).Count); // unrestricted

        var vials = await TestServiceFactory.Cryovial(db).GetAllAsync(microOnly);
        Assert.Equal(new[] { w.MicroVial.Id }, vials.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task Gpt_ACryovialOrDiskFromAnotherSection_CannotChallengeTheLot()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var engine = new MediaEvaluationEngine(db, new MaterialService(db, new UserSectionScopeService(db)), new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SelectCryovialAsync(w.MicroChallenge.Id, w.FpVial.Id, w.Admin));
        Assert.Contains("another laboratory section", ex.Message);

        var diskEx = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SelectLyophilizedDiskAsync(w.MicroChallenge.Id, w.FpMaterial.Id, w.Admin));
        Assert.Contains("another laboratory section", diskEx.Message);
        Assert.Equal(10, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == w.FpMaterial.Id)).QuantityRemaining);
    }
}
