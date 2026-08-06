using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// A signed final decision must leave behind an immutable, tamper-evident
// PDF of the record as it stood at that moment.
public class RecordArchiveTests
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
            ApprovalStatus = ApprovalGateStatus.PendingReview, PreparedByUserId = PreparerId
        };
        db.Cryovials.Add(cryovial);
        await db.SaveChangesAsync();
        return cryovial;
    }

    [Fact]
    public async Task Approval_ArchivesAPdfWithAMatchingHash()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);
        var storage = new InMemoryFileStorageService();

        await TestServiceFactory.Cryovial(db, storage)
            .ApproveAsync(cryovial.Id, approved: true, SectionHeadId, Password, null, null);

        var archived = await db.ArchivedRecords
            .SingleAsync(a => a.EntityType == ReviewEntityTypes.Cryovial && a.EntityId == cryovial.Id);

        Assert.Equal("ECO/01/26", archived.DocumentId);
        Assert.Equal("Cryovial batch approved", archived.Reason);
        Assert.Equal(SectionHeadId, archived.GeneratedByUserId);
        Assert.True(archived.SizeBytes > 0);

        // The stored bytes are a real PDF and hash to what was recorded.
        var stored = storage.Files[archived.StoragePath];
        Assert.StartsWith("%PDF-1.4", Encoding.ASCII.GetString(stored, 0, 8));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(stored)), archived.ContentSha256);
    }

    [Fact]
    public async Task ReadAsync_DetectsATamperedArchive()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);
        var storage = new InMemoryFileStorageService();

        await TestServiceFactory.Cryovial(db, storage)
            .ApproveAsync(cryovial.Id, approved: true, SectionHeadId, Password, null, null);

        var archived = await db.ArchivedRecords.FirstAsync();
        var archive = TestServiceFactory.Archive(db, storage);

        var intact = await archive.ReadAsync(archived.Id);
        Assert.True(intact!.Value.IntegrityOk);

        // Someone edits the stored file behind the system's back.
        storage.Files[archived.StoragePath] = Encoding.ASCII.GetBytes("%PDF-1.4 tampered");

        var tampered = await archive.ReadAsync(archived.Id);
        Assert.False(tampered!.Value.IntegrityOk);
    }

    [Fact]
    public async Task Rejection_IsAlsoArchived()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);
        var storage = new InMemoryFileStorageService();

        await TestServiceFactory.Cryovial(db, storage)
            .ApproveAsync(cryovial.Id, approved: false, SectionHeadId, Password, "Identity did not confirm", null);

        var archived = await db.ArchivedRecords.SingleAsync();
        Assert.Equal("Cryovial batch rejected", archived.Reason);
    }

    // Archiving is best-effort: a storage failure must not undo a
    // decision whose signature has already been committed.
    [Fact]
    public async Task StorageFailure_DoesNotRollBackTheDecision()
    {
        await using var db = NewDb();
        await SeedUser(db, SectionHeadId);
        var cryovial = await SeedPendingBatchAsync(db);

        await TestServiceFactory.Cryovial(db, new ThrowingFileStorageService())
            .ApproveAsync(cryovial.Id, approved: true, SectionHeadId, Password, null, null);

        var reloaded = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.Equal(ApprovalGateStatus.Approved, reloaded!.ApprovalStatus);
        Assert.Single(db.ElectronicSignatures);
        Assert.Empty(db.ArchivedRecords);
    }

    private class ThrowingFileStorageService : MicroLIMS.Infrastructure.Storage.IFileStorageService
    {
        public Task<string> SaveAsync(string fileName, byte[] content) => throw new IOException("disk full");
        public Task<byte[]> ReadAsync(string path) => throw new IOException("unavailable");
    }
}
