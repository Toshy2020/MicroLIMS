using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The cryovial approval gate already existed but recorded no evidence -
// no approver, no timestamp, no signature, and nothing stopping the
// preparer from approving their own batch. These cover the hardened gate.
public class CryovialApprovalTests
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

    private static async Task SeedUser(MicroLimsDbContext db, int id, RoleType roleType = RoleType.SectionHead)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        db.Users.Add(new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) });
        await db.SaveChangesAsync();
    }

    private static CryovialService NewService(MicroLimsDbContext db) => TestServiceFactory.Cryovial(db);

    private static async Task<Cryovial> SeedPendingBatchAsync(MicroLimsDbContext db)
    {
        var organism = new Organism { ScientificName = "E. coli" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = "E. coli disc", ManufacturerName = "Microbiologics",
            BatchNumber = "LOT-9", ReceivingDate = DateTime.UtcNow.AddDays(-3), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "ECO", Location = "Freezer", QuantityReceived = 10, QuantityRemaining = 10, Unit = MaterialUnit.Piece,
            OrganismId = organism.Id
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var cryovial = new Cryovial
        {
            Code = "ECO/01/26", MaterialId = material.Id, OrganismId = organism.Id,
            OrganismNameSnapshot = organism.ScientificName, ExpiryDate = DateTime.UtcNow.AddMonths(6),
            NumberOfVialsPrepared = 10, VialsRemaining = 10,
            ApprovalStatus = ApprovalGateStatus.PendingReview,
            PreparedByUserId = PreparerId
        };
        db.Cryovials.Add(cryovial);
        await db.SaveChangesAsync();
        return cryovial;
    }

    [Fact]
    public async Task ApproveAsync_RecordsApproverTimestampAndSignature()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);

        await NewService(db).ApproveAsync(cryovial.Id, approved: true, SectionHeadId, Password, "Identity confirmed", null);

        var reloaded = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.Equal(ApprovalGateStatus.Approved, reloaded!.ApprovalStatus);
        Assert.Equal(SectionHeadId, reloaded.ApprovedByUserId);
        Assert.NotNull(reloaded.ApprovedAt);
        Assert.False(reloaded.IsDestroyed);

        Assert.Single(db.ElectronicSignatures.Where(s => s.EntityType == "Cryovial" && s.EntityId == cryovial.Id));
        var events = await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Cryovial && e.EntityId == cryovial.Id).ToListAsync();
        Assert.Single(events);
        Assert.Equal(ApprovalDecision.Approve, events[0].Decision);
    }

    [Fact]
    public async Task ApproveAsync_PreparerCannotApproveTheirOwnBatch()
    {
        await using var db = NewDb();
        await SeedUser(db, PreparerId);
        var cryovial = await SeedPendingBatchAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).ApproveAsync(cryovial.Id, approved: true, PreparerId, Password, null, null));
        Assert.Contains("cannot approve a cryovial batch you prepared", ex.Message);

        var reloaded = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloaded!.ApprovalStatus);
        Assert.Empty(db.ElectronicSignatures);
    }

    [Fact]
    public async Task ApproveAsync_WrongPassword_LeavesBatchPending()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).ApproveAsync(cryovial.Id, approved: true, SectionHeadId, "wrong-password", null, null));

        var reloaded = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.Equal(ApprovalGateStatus.PendingReview, reloaded!.ApprovalStatus);
        Assert.Null(reloaded.ApprovedByUserId);
    }

    [Fact]
    public async Task ApproveAsync_Reject_DestroysBatch()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);

        await NewService(db).ApproveAsync(cryovial.Id, approved: false, SectionHeadId, Password, "Identity did not confirm", null);

        var reloaded = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.Equal(ApprovalGateStatus.Rejected, reloaded!.ApprovalStatus);
        Assert.True(reloaded.IsDestroyed);
        Assert.Equal(SectionHeadId, reloaded.ApprovedByUserId);
    }

    [Fact]
    public async Task ApproveAsync_AlreadyDecided_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);
        var service = NewService(db);

        await service.ApproveAsync(cryovial.Id, approved: true, SectionHeadId, Password, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(cryovial.Id, approved: false, SectionHeadId, Password, null, null));
        Assert.Contains("already been decided", ex.Message);
    }
}
