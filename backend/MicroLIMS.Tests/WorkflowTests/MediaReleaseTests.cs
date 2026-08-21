using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The human gate on media release: a Conform evaluation only qualifies a
// lot: a Section Head must sign for it before it can be used in routine
// testing, and cannot be the person who prepared or evaluated it.
public class MediaReleaseTests
{
    private const string Password = "Correct-Horse-1!";
    private const int PreparerId = 1;
    private const int SectionHeadId = 2;

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<User> SeedUser(MicroLimsDbContext db, int id, RoleType roleType = RoleType.SectionHead)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static MediaReleaseService NewReleaseService(MicroLimsDbContext db) => TestServiceFactory.MediaRelease(db);

    // Prepares a lot (by PreparerId) and drives its single GrowthPromotion
    // challenge to the given outcome, leaving the lot qualified-but-unreleased.
    private static async Task<Media> PrepareAndEvaluateAsync(MicroLimsDbContext db, bool conform)
    {
        var organism = new Organism { ScientificName = "E. coli" };
        db.Organisms.Add(organism);
        var mediaType = new MediaType
        {
            Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48,
            RequiredTemperatureMin = 30, RequiredTemperatureMax = 35,
            RecoveryPercentMin = 50, RecoveryPercentMax = 200
        };
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA", ManufacturerName = "Himedia",
            BatchNumber = "LOT-1", ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "MAT", Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        db.MediaTypes.Add(mediaType);
        db.Materials.Add(material);
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();

        db.MediaChallengeSpecs.Add(new MediaChallengeSpec
        {
            MaterialName = "TSA", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = organism.Id
        });
        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id,
            DocumentType = MaterialDocumentType.COA,
            OriginalFileName = "COA.pdf",
            StorageKey = "test/coa.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "HASH",
            UploadedByUserId = PreparerId,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();

        var media = await TestServiceFactory.MediaPreparation(db).PrepareAsync(new PrepareMediaRequest(
            mediaType.Id, material.Id, TotalWeight: 100m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id,
            AutoclaveProgram: "A", LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
            Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(6), UserId: PreparerId));

        var evaluation = await db.MediaEvaluations.Include(e => e.Challenges).FirstAsync(e => e.MediaId == media.Id);
        var challenge = evaluation.Challenges[0];

        var cryovial = new Cryovial
        {
            Code = "CRY/01/26", MaterialId = material.Id, OrganismId = organism.Id,
            OrganismNameSnapshot = "E. coli", ExpiryDate = DateTime.UtcNow.AddMonths(6),
            NumberOfVialsPrepared = 10, VialsRemaining = 10, ApprovalStatus = ApprovalGateStatus.Approved
        };
        db.Cryovials.Add(cryovial);
        await db.SaveChangesAsync();

        var engine = new MediaEvaluationEngine(db);
        await engine.SelectCryovialAsync(challenge.Id, cryovial.Id, PreparerId);
        var incubation = await engine.RecordIncubationAsync(challenge.Id, incubatorEquipmentId: autoclave.Id, PreparerId);
        incubation.ExpectedReadingAt = DateTime.UtcNow.AddMinutes(-1); // simulate the incubation period elapsing
        await db.SaveChangesAsync();

        await engine.RecordResultAsync(new RecordResultRequest(
            challenge.Id, PreparerId, OldMediaCount: 100, NewMediaCount: conform ? 95 : 10,
            GrowthObserved: null, ObservedDescription: null, ManualConform: null, IsTurbid: null));

        return media;
    }

    [Fact]
    public async Task ConformEvaluation_DoesNotReleaseOnItsOwn_AndAppearsInApprovalQueue()
    {
        await using var db = NewDb();
        var media = await PrepareAndEvaluateAsync(db, conform: true);

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.False(reloaded!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloaded.ApprovalStatus);

        var queue = await NewReleaseService(db).GetAwaitingApprovalAsync();
        Assert.Single(queue);
        Assert.Equal(media.Id, queue[0].Id);
    }

    [Fact]
    public async Task DecideAsync_Approve_ReleasesLotAndRecordsApprover()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);

        await NewReleaseService(db).DecideAsync(media.Id, SectionHeadId, Password, approved: true, comment: "Looks good", ipAddress: null);

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.True(reloaded!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.Approved, reloaded.ApprovalStatus);
        Assert.Equal(MediaStatus.Active, reloaded.Status);
        Assert.Equal(SectionHeadId, reloaded.ApprovedByUserId);
        Assert.NotNull(reloaded.ApprovedAt);

        // The decision is signed and logged, not just flipped.
        Assert.Single(db.ElectronicSignatures.Where(s => s.EntityType == "Media" && s.EntityId == media.Id));
        var events = await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Media && e.EntityId == media.Id).ToListAsync();
        Assert.Single(events);
        Assert.Equal(ApprovalDecision.Approve, events[0].Decision);
    }

    [Fact]
    public async Task DecideAsync_PreparerCannotApproveTheirOwnLot()
    {
        await using var db = NewDb();
        await SeedUser(db, PreparerId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewReleaseService(db).DecideAsync(media.Id, PreparerId, Password, approved: true, comment: null, ipAddress: null));
        Assert.Contains("cannot approve a media lot you prepared", ex.Message);

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.False(reloaded!.IsReleasedForUse);
        Assert.Empty(db.ElectronicSignatures);
    }

    [Fact]
    public async Task DecideAsync_NonConformEvaluation_CannotBeReleased()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewReleaseService(db).DecideAsync(media.Id, SectionHeadId, Password, approved: true, comment: null, ipAddress: null));
        Assert.Contains("did not conform", ex.Message);

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.False(reloaded!.IsReleasedForUse);
    }

    [Fact]
    public async Task DecideAsync_Reject_QuarantinesLotAndLeavesItUnreleased()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);

        await NewReleaseService(db).DecideAsync(media.Id, SectionHeadId, Password, approved: false, comment: "pH drifted", ipAddress: null);

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.False(reloaded!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.Rejected, reloaded.ApprovalStatus);
        Assert.Equal(MediaStatus.QuarantineFailed, reloaded.Status);
    }

    [Fact]
    public async Task DecideAsync_AlreadyDecided_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);
        var service = NewReleaseService(db);

        await service.DecideAsync(media.Id, SectionHeadId, Password, approved: true, comment: null, ipAddress: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideAsync(media.Id, SectionHeadId, Password, approved: true, comment: null, ipAddress: null));
        Assert.Contains("already been decided", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_WrongPassword_LeavesLotUnreleased()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewReleaseService(db).DecideAsync(media.Id, SectionHeadId, "wrong-password", approved: true, comment: null, ipAddress: null));

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.False(reloaded!.IsReleasedForUse);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloaded.ApprovalStatus);
    }

    [Fact]
    public async Task MarkOutOfStockAsync_ReleasedLot_SetsStatusOutOfStockAndExcludesFromGetReleased()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);
        var releaseService = NewReleaseService(db);
        await releaseService.DecideAsync(media.Id, SectionHeadId, Password, approved: true, comment: null, ipAddress: null);

        var prepService = TestServiceFactory.MediaPreparation(db);
        var releasedBefore = await prepService.GetReleasedAsync();
        Assert.Contains(releasedBefore, m => m.Id == media.Id);

        await prepService.MarkOutOfStockAsync(media.Id, SectionHeadId, "Lot consumed");

        var reloaded = await db.Media.FindAsync(media.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(MediaStatus.OutOfStock, reloaded.Status);

        var releasedAfter = await prepService.GetReleasedAsync();
        Assert.DoesNotContain(releasedAfter, m => m.Id == media.Id);
    }

    [Fact]
    public async Task MarkOutOfStockAsync_UnreleasedLot_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var media = await PrepareAndEvaluateAsync(db, conform: true);

        var prepService = TestServiceFactory.MediaPreparation(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            prepService.MarkOutOfStockAsync(media.Id, SectionHeadId, null));
        Assert.Contains("not currently released for use", ex.Message);
    }
}
