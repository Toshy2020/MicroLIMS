using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class StorageLocationChangeTests
{
    private const int UserId = 1;

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<User> SeedUser(MicroLimsDbContext db, int id = UserId)
    {
        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Media> SeedMediaAsync(MicroLimsDbContext db, bool released = true, MediaStatus status = MediaStatus.Active)
    {
        var media = new Media
        {
            LotNumber = "TSA/01/26",
            ManufacturerLot = "LOT-123",
            ManufacturerName = "Himedia",
            PreparedByUserId = UserId,
            IsReleasedForUse = released,
            Status = status,
            ExpiryDate = DateTime.UtcNow.AddMonths(3)
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return media;
    }

    private static async Task<Cryovial> SeedCryovialAsync(MicroLimsDbContext db, ApprovalGateStatus status = ApprovalGateStatus.Approved, bool destroyed = false)
    {
        var organism = new Organism { ScientificName = "Escherichia coli" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.LyophilizedMicroorganism,
            MaterialName = "E. coli disc",
            ManufacturerName = "Microbiologics",
            BatchNumber = "LOT-9",
            ReceivingDate = DateTime.UtcNow.AddDays(-3),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "ECO",
            Location = "Freezer",
            QuantityReceived = 10,
            QuantityRemaining = 10,
            Unit = MaterialUnit.Piece,
            OrganismId = organism.Id
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var cryovial = new Cryovial
        {
            Code = "ECO/01/26",
            MaterialId = material.Id,
            OrganismId = organism.Id,
            OrganismNameSnapshot = organism.ScientificName,
            ManufacturerName = "Microbiologics",
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            NumberOfVialsPrepared = 10,
            VialsRemaining = 10,
            StorageCondition = "Original Freezer - Shelf 1",
            ApprovalStatus = status,
            IsDestroyed = destroyed,
            PreparedByUserId = UserId
        };
        db.Cryovials.Add(cryovial);
        await db.SaveChangesAsync();
        return cryovial;
    }

    private static async Task<EquipmentInventory> SeedStorageEquipmentAsync(
        MicroLimsDbContext db,
        string code = "REF-01",
        string instrumentType = "Refrigerator",
        string manufacturer = "Thermo Fisher",
        EquipmentOperationalStatus status = EquipmentOperationalStatus.InService)
    {
        var eq = new EquipmentInventory
        {
            Code = code,
            InstrumentType = instrumentType,
            ManufacturerName = manufacturer,
            Location = "Microbiology Lab Room 101",
            Status = status
        };
        db.EquipmentInventories.Add(eq);
        await db.SaveChangesAsync();
        return eq;
    }

    [Fact]
    public async Task AddEntry_Rejected_ForUnreleasedLot()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db, released: false);
        var service = TestServiceFactory.MediaStorage(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
                Form: "Plates",
                Quantity: 40,
                LocationText: "Shelf A")));

        Assert.Contains("not currently released for use", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddEntry_Rejected_ForInactiveLot()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db, released: true, status: MediaStatus.OutOfStock);
        var service = TestServiceFactory.MediaStorage(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
                Form: "Plates",
                Quantity: 40,
                LocationText: "Shelf A")));

        Assert.Contains("not currently released for use and active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddEntry_ExactlyOneOf_Validation()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db);
        var eq = await SeedStorageEquipmentAsync(db);
        var service = TestServiceFactory.MediaStorage(db);

        // Both provided
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
                Form: "Plates",
                Quantity: 40,
                EquipmentId: eq.Id,
                LocationText: "Shelf A")));
        Assert.Contains("Exactly one of EquipmentId or LocationText", ex1.Message);

        // Neither provided
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
                Form: "Plates",
                Quantity: 40,
                EquipmentId: null,
                LocationText: null)));
        Assert.Contains("Exactly one of EquipmentId or LocationText", ex2.Message);
    }

    [Fact]
    public async Task AddEntry_EquipmentEligibility_Checked()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db);
        var outOfServiceEq = await SeedStorageEquipmentAsync(db, status: EquipmentOperationalStatus.OutOfService);
        var service = TestServiceFactory.MediaStorage(db);

        // Out of service equipment rejected
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
                Form: "Plates",
                Quantity: 40,
                EquipmentId: outOfServiceEq.Id)));
        Assert.Contains("not currently in service", ex.Message);

        // Non-existent equipment rejected
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
                Form: "Plates",
                Quantity: 40,
                EquipmentId: 99999)));
    }

    [Fact]
    public async Task AddEntry_MergesQuantity_WhenIdenticalTargetExists()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db);
        var eq = await SeedStorageEquipmentAsync(db);
        var service = TestServiceFactory.MediaStorage(db);

        var first = await service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
            Form: "Plates",
            Quantity: 40,
            EquipmentId: eq.Id));
        Assert.Equal(40, first.Quantity);

        var second = await service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
            Form: "Plates",
            Quantity: 20,
            EquipmentId: eq.Id));
        Assert.Equal(60, second.Quantity);
        Assert.Equal(first.Id, second.Id);

        var all = await service.GetStorageEntriesAsync(media.Id);
        Assert.Single(all);
        Assert.Equal(60, all[0].Quantity);
    }

    [Fact]
    public async Task Move_WithSplit_ReducesSourceAndCreatesNewTarget()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db);
        var fridge = await SeedStorageEquipmentAsync(db, code: "REF-01", instrumentType: "Refrigerator", manufacturer: "Thermo");
        var service = TestServiceFactory.MediaStorage(db);

        var initial = await service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
            Form: "Plates",
            Quantity: 40,
            EquipmentId: fridge.Id));

        var moved = await service.MoveStorageEntryAsync(media.Id, initial.Id, UserId, new MoveMediaStorageEntryRequest(
            Quantity: 15,
            LocationText: "Media Storage Area - Shelf B",
            Reason: "Transferred for routine bench testing"));

        Assert.Equal(15, moved.Quantity);
        Assert.Equal("Media Storage Area - Shelf B", moved.LocationText);
        Assert.Null(moved.EquipmentId);

        var all = await service.GetStorageEntriesAsync(media.Id);
        Assert.Equal(2, all.Count);

        var source = Assert.Single(all, e => e.Id == initial.Id);
        Assert.Equal(25, source.Quantity);
        Assert.Equal(fridge.Id, source.EquipmentId);

        var target = Assert.Single(all, e => e.Id != initial.Id);
        Assert.Equal(15, target.Quantity);
        Assert.Equal("Media Storage Area - Shelf B", target.LocationText);

        var auditEvents = await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Media && e.EntityId == media.Id && e.EventType == ReviewWorkflowEventType.StorageLocationChanged)
            .ToListAsync();
        Assert.Equal(2, auditEvents.Count); // 1 add + 1 move
        var moveEvent = auditEvents.Last();
        Assert.Contains("Moved 15 Plates", moveEvent.Comment);
        Assert.Contains("Transferred for routine bench testing", moveEvent.Comment);
    }

    [Fact]
    public async Task Move_WithMerge_RemovesSourceAndIncreasesTarget()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db);
        var fridge1 = await SeedStorageEquipmentAsync(db, code: "REF-01", instrumentType: "Refrigerator");
        var fridge2 = await SeedStorageEquipmentAsync(db, code: "REF-02", instrumentType: "Refrigerator");
        var service = TestServiceFactory.MediaStorage(db);

        var entry1 = await service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
            Form: "AgarBottles",
            Quantity: 10,
            EquipmentId: fridge1.Id));

        var entry2 = await service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
            Form: "AgarBottles",
            Quantity: 25,
            EquipmentId: fridge2.Id));

        // Move all 10 from entry1 to fridge2 (merging into entry2)
        var result = await service.MoveStorageEntryAsync(media.Id, entry1.Id, UserId, new MoveMediaStorageEntryRequest(
            Quantity: 10,
            EquipmentId: fridge2.Id,
            Reason: "Consolidating media bottles"));

        Assert.Equal(entry2.Id, result.Id);
        Assert.Equal(35, result.Quantity);

        var all = await service.GetStorageEntriesAsync(media.Id);
        Assert.Single(all);
        Assert.Equal(35, all[0].Quantity);
        Assert.Equal(fridge2.Id, all[0].EquipmentId);
    }

    [Fact]
    public async Task Move_WithoutReason_Rejected()
    {
        using var db = NewDb();
        await SeedUser(db);
        var media = await SeedMediaAsync(db);
        var fridge = await SeedStorageEquipmentAsync(db);
        var service = TestServiceFactory.MediaStorage(db);

        var entry = await service.AddStorageEntryAsync(media.Id, UserId, new AddMediaStorageEntryRequest(
            Form: "Plates",
            Quantity: 20,
            EquipmentId: fridge.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MoveStorageEntryAsync(media.Id, entry.Id, UserId, new MoveMediaStorageEntryRequest(
                Quantity: 5,
                LocationText: "Shelf C",
                Reason: "   ")));

        Assert.Contains("reason is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cryovial_MoveStorage_UpdatesStorageCondition_AndRequiresReason()
    {
        using var db = NewDb();
        await SeedUser(db);
        var cryovial = await SeedCryovialAsync(db);
        var freezer = await SeedStorageEquipmentAsync(db, code: "DF-01", instrumentType: "Deep Freezer", manufacturer: "Sanyo");
        var service = TestServiceFactory.Cryovial(db);

        // Missing reason rejected
        var exNoReason = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MoveStorageAsync(cryovial.Id, UserId, freezer.Id, null, "   "));
        Assert.Contains("reason is required", exNoReason.Message, StringComparison.OrdinalIgnoreCase);

        // Exactly-one-of rejected
        var exBoth = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MoveStorageAsync(cryovial.Id, UserId, freezer.Id, "Some text", "Reorganizing"));
        Assert.Contains("Exactly one of EquipmentId or LocationText", exBoth.Message);

        // Move to equipment
        var updated = await service.MoveStorageAsync(cryovial.Id, UserId, freezer.Id, null, "Moved to primary storage");
        Assert.Equal(freezer.Id, updated.StorageEquipmentId);
        Assert.Equal("Deep Freezer, Sanyo (DF-01)", updated.StorageCondition);

        var reloaded = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(freezer.Id, reloaded.StorageEquipmentId);
        Assert.Equal("Deep Freezer, Sanyo (DF-01)", reloaded.StorageCondition);

        // Audit event written
        var audit = await db.ReviewWorkflowEvents
            .FirstOrDefaultAsync(e => e.EntityType == ReviewEntityTypes.Cryovial && e.EntityId == cryovial.Id && e.EventType == ReviewWorkflowEventType.StorageLocationChanged);
        Assert.NotNull(audit);
        Assert.Contains("Original Freezer - Shelf 1", audit.Comment);
        Assert.Contains("Deep Freezer, Sanyo (DF-01)", audit.Comment);
        Assert.Contains("Moved to primary storage", audit.Comment);

        // Move to free text location
        var updated2 = await service.MoveStorageAsync(cryovial.Id, UserId, null, "Backup Cryo Box - Bin 4", "Emergency maintenance on freezer");
        Assert.Null(updated2.StorageEquipmentId);
        Assert.Equal("Backup Cryo Box - Bin 4", updated2.StorageCondition);

        var reloaded2 = await db.Cryovials.FindAsync(cryovial.Id);
        Assert.NotNull(reloaded2);
        Assert.Null(reloaded2.StorageEquipmentId);
        Assert.Equal("Backup Cryo Box - Bin 4", reloaded2.StorageCondition);
    }

    [Fact]
    public async Task Cryovial_MoveStorage_Rejected_ForUnapprovedOrDestroyedBatch()
    {
        using var db = NewDb();
        await SeedUser(db);
        var unapproved = await SeedCryovialAsync(db, status: ApprovalGateStatus.PendingReview);
        var destroyed = await SeedCryovialAsync(db, status: ApprovalGateStatus.Approved, destroyed: true);
        var freezer = await SeedStorageEquipmentAsync(db);
        var service = TestServiceFactory.Cryovial(db);

        var exUnapproved = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MoveStorageAsync(unapproved.Id, UserId, freezer.Id, null, "Reason"));
        Assert.Contains("not approved", exUnapproved.Message, StringComparison.OrdinalIgnoreCase);

        var exDestroyed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MoveStorageAsync(destroyed.Id, UserId, freezer.Id, null, "Reason"));
        Assert.Contains("destroyed", exDestroyed.Message, StringComparison.OrdinalIgnoreCase);
    }
}
