using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The recently-opened list against a real database. RecentRecord is excluded from automatic audit capture
// (convenience list, not an access log; approved 2026-11-11), so every write also proves no AuditLogs row is made.
// Arrange, act and assert each use their own DbContext.
[Collection("PostgresDatabaseCollection")]
public class RecentRecordPostgresTests
{
    private static readonly DateTimeOffset T0 = new(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);
    private readonly PostgresTestFixture _fixture;

    public RecentRecordPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }

    private static RecentRecordService NewService(MicroLimsDbContext db, TimeProvider? clock = null) =>
        new(db, new RecordLabelResolver(db, new UserSectionScopeService(db), new DocumentAuthorizationService(db)),
            clock ?? new FixedTimeProvider(T0));

    private static RecentRecordsController NewController(RecentRecordService service, int userId) =>
        new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };

    // ---- arrange helpers ----

    private sealed record Records(int Sample, int TestOrder, int Item, int Document, int Equipment, int Material, int Media, int Solution)
    {
        public IEnumerable<(string Type, int Id)> All => new[]
        {
            (RecentRecordTypes.Sample, Sample), (RecentRecordTypes.TestOrder, TestOrder), (RecentRecordTypes.Item, Item),
            (RecentRecordTypes.Document, Document), (RecentRecordTypes.Equipment, Equipment), (RecentRecordTypes.Material, Material),
            (RecentRecordTypes.Media, Media), (RecentRecordTypes.Solution, Solution)
        };
    }

    private async Task<DocumentSection> NewSectionAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var template = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
        var uid = Guid.NewGuid().ToString("N")[..8];
        var section = new DocumentSection { DepartmentId = template.DepartmentId, Name = $"Recent {uid}", Code = $"R{uid}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    // An Analyst-type role with no permissions: no Sections.ViewAll, so its read scope is its memberships.
    private async Task<User> NewUserAsync(params DocumentSection[] memberOf)
    {
        await using var db = _fixture.CreateDbContext();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var role = new Role { Type = RoleType.Analyst, Name = $"Recent role {uid}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { Username = $"recent_{uid}", FullName = $"Recent {uid}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Item> NewItemAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var item = new Item { Code = $"RI-{Guid.NewGuid():N}"[..14], Name = "Recent item " + Guid.NewGuid().ToString("N")[..6] };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private async Task<Records> SeedRecordsAsync(DocumentSection section, int creatorUserId, string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        var item = new Item { Code = $"RI-{tag}-{Guid.NewGuid():N}"[..14], Name = $"Recent item {tag}" };
        db.Items.Add(item);
        var sample = new Sample
        {
            ReferenceNumber = $"RS-{tag}-{Guid.NewGuid():N}"[..14], Category = SampleCategory.FinishedProduct, CauseOfTestingId = cause.Id,
            ReceivedAt = T0.UtcDateTime, ReceivedByUserId = creatorUserId, Status = SampleStatus.UnderReview
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = new TestOrder { SampleId = sample.Id, TestCode = $"T{Guid.NewGuid():N}"[..12], SectionId = section.Id };
        db.TestOrders.Add(order);

        var docType = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Code == "SOP");
        if (docType is null)
        {
            docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
            db.DocumentTypes.Add(docType);
            await db.SaveChangesAsync();
        }
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-RC-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-RC-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            Title = "Recent record SOP " + tag, DocumentTypeId = docType.Id, DepartmentId = section.DepartmentId, SectionId = section.Id,
            DocumentOwnerUserId = creatorUserId, CreatedByUserId = creatorUserId, RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);

        var equipment = new Equipment
        {
            Name = "Recent incubator " + tag, Code = $"INC-{Guid.NewGuid():N}"[..14], Type = EquipmentType.Incubator, SetPointTemperature = 32, SectionId = section.Id
        };
        db.Equipment.Add(equipment);

        Material NewMaterial() => new()
        {
            SectionId = section.Id, MaterialType = MaterialType.DehydratedMedia, MaterialName = $"TSA {Guid.NewGuid():N}"[..12],
            ManufacturerName = "Acme", BatchNumber = $"B-{Guid.NewGuid():N}"[..10], ReceivingDate = T0.UtcDateTime.AddDays(-30),
            ExpiryDate = T0.UtcDateTime.AddDays(300), Location = "Shelf", QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Gram
        };
        var material = NewMaterial();
        var mediaMaterial = NewMaterial();
        db.Materials.AddRange(material, mediaMaterial);
        var solutionMaster = new SolutionMaster
        {
            SectionId = section.Id, Name = $"Diluent {tag}", Type = SolutionType.Diluent, ShelfLifeValue = 7, ShelfLifeUnit = ShelfLifeUnit.Days,
            StorageCondition = "Room temp", FinalVolumeMl = 1000m, Instructions = "Mix", SterilizationRequired = false, IsActive = true,
            CreatedByUserId = creatorUserId, CreatedAt = T0.UtcDateTime, LastModifiedByUserId = creatorUserId, LastModifiedAt = T0.UtcDateTime
        };
        db.SolutionMasters.Add(solutionMaster);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MaterialId = mediaMaterial.Id, LotNumber = $"LOT-{Guid.NewGuid():N}"[..14], ManufacturerLot = mediaMaterial.BatchNumber, ManufacturerName = "Acme",
            TotalWeight = 10m, TotalVolume = "500 ml", AutoclaveProgram = "P1", LoadType = "agar (500 ml)", Temperature = 121m, CycleTime = 15,
            CycleNumber = 1, Ph = 7.0m, ExpiryDate = T0.UtcDateTime.AddDays(60), PreparedAt = T0.UtcDateTime.AddDays(-5), PreparedByUserId = creatorUserId,
            Status = MediaStatus.Prepared
        };
        db.Media.Add(media);
        var prep = new SolutionPreparation
        {
            SectionId = section.Id, SolutionMasterId = solutionMaster.Id, Type = SolutionType.Diluent, Status = SolutionPreparationStatus.InProgress,
            StartedByUserId = creatorUserId, StartedAt = T0.UtcDateTime
        };
        db.SolutionPreparations.Add(prep);
        await db.SaveChangesAsync();

        return new Records(sample.Id, order.Id, item.Id, master.Id, equipment.Id, material.Id, media.Id, prep.Id);
    }

    private async Task TouchAsync(int userId, string type, int id, DateTimeOffset at)
    {
        await using var actDb = _fixture.CreateDbContext();
        await NewService(actDb, new FixedTimeProvider(at)).TouchAsync(userId, type, id);
    }

    private async Task<IReadOnlyList<RecentRecordDto>> ListAsync(int userId, CommandCounter? counter = null)
    {
        await using var actDb = counter is null ? _fixture.CreateDbContext() : _fixture.CreateDbContext(counter);
        return await NewService(actDb).ListAsync(userId);
    }

    // ---- tests ----

    [PostgresFact]
    public async Task Touch_AddsOnce_UpdatesTimeOnRepeat()
    {
        var section = await NewSectionAsync();
        var user = await NewUserAsync(section);
        var records = await SeedRecordsAsync(section, user.Id);

        await TouchAsync(user.Id, RecentRecordTypes.Sample, records.Sample, T0);
        await TouchAsync(user.Id, RecentRecordTypes.Sample, records.Sample, T0.AddMinutes(10));

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var row = await assertDb.RecentRecords.AsNoTracking().SingleAsync(r => r.UserId == user.Id);
            Assert.Equal(RecentRecordTypes.Sample, row.RecordType);
            Assert.Equal(records.Sample, row.RecordId);
            Assert.Equal(T0.AddMinutes(10).UtcDateTime, row.OpenedAt);
            Assert.Equal(DateTimeKind.Utc, row.OpenedAt.Kind);
            // A convenience list, not an access log: no audit row for the writes.
            Assert.Empty(await assertDb.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(RecentRecord)).ToListAsync());
        }

        // The label is written by the server and carries the record's own identifier; it is not stored.
        var list = await ListAsync(user.Id);
        var dto = Assert.Single(list);
        Assert.Equal(RecentRecordTypes.Sample, dto.RecordType);
        Assert.Equal(records.Sample, dto.RecordId);
        Assert.Equal(T0.AddMinutes(10).UtcDateTime, dto.OpenedAtUtc);
        await using (var assertDb = _fixture.CreateDbContext())
        {
            var reference = await assertDb.Samples.AsNoTracking().Where(s => s.Id == records.Sample).Select(s => s.ReferenceNumber).SingleAsync();
            Assert.Equal(reference, dto.Identifier);
        }
    }

    [PostgresFact]
    public async Task TwentyFirst_DropsOldest()
    {
        var user = await NewUserAsync();
        var items = new List<Item>();
        for (var i = 0; i < 21; i++) items.Add(await NewItemAsync());

        for (var i = 0; i < 20; i++) await TouchAsync(user.Id, RecentRecordTypes.Item, items[i].Id, T0.AddMinutes(i));

        await using (var assertDb = _fixture.CreateDbContext())
            Assert.Equal(20, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == user.Id));

        // Re-opening a record already in the list does not push another one out.
        await TouchAsync(user.Id, RecentRecordTypes.Item, items[0].Id, T0.AddMinutes(30));
        await using (var assertDb = _fixture.CreateDbContext())
            Assert.Equal(20, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == user.Id));

        // The 21st distinct record evicts the oldest (items[1]; items[0] was just re-opened).
        await TouchAsync(user.Id, RecentRecordTypes.Item, items[20].Id, T0.AddMinutes(31));

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var ids = await assertDb.RecentRecords.AsNoTracking().Where(r => r.UserId == user.Id).Select(r => r.RecordId).ToListAsync();
            Assert.Equal(20, ids.Count);
            Assert.DoesNotContain(items[1].Id, ids);
            Assert.Contains(items[0].Id, ids);
            Assert.Contains(items[20].Id, ids);
            Assert.Equal(items.Where((_, i) => i != 1).Select(i => i.Id).OrderBy(x => x), ids.OrderBy(x => x));
        }

        var list = await ListAsync(user.Id);
        Assert.Equal(20, list.Count);
        Assert.Equal(items[20].Id, list[0].RecordId);
        Assert.Equal(items[0].Id, list[1].RecordId);
    }

    [PostgresFact]
    public async Task List_OnlyMine()
    {
        var section = await NewSectionAsync();
        var mine = await NewUserAsync(section);
        var other = await NewUserAsync(section);
        var records = await SeedRecordsAsync(section, mine.Id);

        await TouchAsync(mine.Id, RecentRecordTypes.Sample, records.Sample, T0);
        await TouchAsync(other.Id, RecentRecordTypes.Equipment, records.Equipment, T0.AddMinutes(1));

        var mineList = await ListAsync(mine.Id);
        var otherList = await ListAsync(other.Id);

        Assert.Equal(new[] { (RecentRecordTypes.Sample, records.Sample) }, mineList.Select(r => (r.RecordType, r.RecordId)));
        Assert.Equal(new[] { (RecentRecordTypes.Equipment, records.Equipment) }, otherList.Select(r => (r.RecordType, r.RecordId)));

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == mine.Id));
        Assert.Equal(1, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == other.Id));
    }

    [PostgresFact]
    public async Task Touch_UnreadableRecord_204_StoresNothing()
    {
        var section = await NewSectionAsync();
        var author = await NewUserAsync(section);
        var outsider = await NewUserAsync(); // member of no section
        var records = await SeedRecordsAsync(section, author.Id);

        // Every section-scoped type, plus a record that does not exist at all, answers 204 and stores nothing.
        var attempts = records.All.Where(r => r.Type != RecentRecordTypes.Item && r.Type != RecentRecordTypes.Document)
            .Append((RecentRecordTypes.Sample, 2_000_000_000)).Append((RecentRecordTypes.Item, 2_000_000_000)).ToList();
        foreach (var (type, id) in attempts)
        {
            await using var actDb = _fixture.CreateDbContext();
            var result = await NewController(NewService(actDb), outsider.Id).Touch(new TouchRecentRecordRequest(type, id), CancellationToken.None);
            Assert.IsType<NoContentResult>(result);
        }

        await using (var assertDb = _fixture.CreateDbContext())
            Assert.Empty(await assertDb.RecentRecords.AsNoTracking().Where(r => r.UserId == outsider.Id).ToListAsync());

        // A member of the section opening the same record is stored (the check is not just "always refuse").
        await using (var actDb = _fixture.CreateDbContext())
        {
            var result = await NewController(NewService(actDb), author.Id).Touch(new TouchRecentRecordRequest(RecentRecordTypes.Sample, records.Sample), CancellationToken.None);
            Assert.IsType<NoContentResult>(result);
        }
        await using (var assertDb = _fixture.CreateDbContext())
            Assert.Equal(1, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == author.Id));
    }

    [PostgresFact]
    public async Task List_CarriesTheServerRouteThatOpensEachRecord()
    {
        var section = await NewSectionAsync();
        var user = await NewUserAsync(section);
        var records = await SeedRecordsAsync(section, user.Id);

        var at = T0;
        foreach (var (type, id) in records.All) await TouchAsync(user.Id, type, id, at = at.AddMinutes(1));

        var routes = (await ListAsync(user.Id)).ToDictionary(r => r.RecordType, r => r.Route);

        // A test order opens through its sample, like a search result; the list pages take the record id.
        Assert.Equal($"/receiving-testing?sampleId={records.Sample}", routes[RecentRecordTypes.Sample]);
        Assert.Equal($"/receiving-testing?sampleId={records.Sample}&testOrderId={records.TestOrder}", routes[RecentRecordTypes.TestOrder]);
        Assert.Equal($"/laboratory-configuration/items?item={records.Item}", routes[RecentRecordTypes.Item]);
        Assert.Equal($"/document-control/documents/{records.Document}", routes[RecentRecordTypes.Document]);
        Assert.Equal($"/inventory/equipment?equipmentId={records.Equipment}", routes[RecentRecordTypes.Equipment]);
        Assert.Equal($"/inventory/materials?materialId={records.Material}", routes[RecentRecordTypes.Material]);
        Assert.Equal($"/laboratory-configuration/media?mediaId={records.Media}", routes[RecentRecordTypes.Media]);
        Assert.Equal($"/preparation/{records.Solution}", routes[RecentRecordTypes.Solution]);
    }

    [PostgresFact]
    public async Task List_DropsRecordINoLongerMayRead_AndReturnsNoLabelForIt()
    {
        var section = await NewSectionAsync();
        var user = await NewUserAsync(section);
        var records = await SeedRecordsAsync(section, user.Id);

        var at = T0;
        foreach (var (type, id) in records.All) await TouchAsync(user.Id, type, id, at = at.AddMinutes(1));

        var before = await ListAsync(user.Id);
        Assert.Equal(8, before.Count);
        Assert.All(before, r => Assert.False(string.IsNullOrWhiteSpace(r.Title)));

        // The user leaves the laboratory section, and the document is voided (only Section Head / audit oversight may open it).
        await using (var changeDb = _fixture.CreateDbContext())
        {
            await changeDb.UserOrgMemberships.Where(m => m.UserId == user.Id).ExecuteDeleteAsync();
            await changeDb.DocumentMasters.Where(d => d.Id == records.Document).ExecuteUpdateAsync(s => s.SetProperty(d => d.RecordStatus, DocumentRecordStatus.Void));
        }

        var after = await ListAsync(user.Id);

        // Only the item (no read-scope rule: every signed-in user may read items) is left.
        var remaining = Assert.Single(after);
        Assert.Equal((RecentRecordTypes.Item, records.Item), (remaining.RecordType, remaining.RecordId));
        var droppedLabels = before.Where(b => b.RecordType != RecentRecordTypes.Item).SelectMany(b => new[] { b.Identifier, b.Title }).ToList();
        Assert.DoesNotContain(after, r => droppedLabels.Contains(r.Identifier) || droppedLabels.Contains(r.Title));

        // The rows are not deleted by reading, so access that returns is honoured.
        await using (var assertDb = _fixture.CreateDbContext())
            Assert.Equal(8, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == user.Id));

        // The resolver itself returns nothing for what the user may not read.
        await using (var actDb = _fixture.CreateDbContext())
        {
            var resolver = new RecordLabelResolver(actDb, new UserSectionScopeService(actDb), new DocumentAuthorizationService(actDb));
            var resolved = await resolver.ResolveAsync(user.Id, records.All.ToList());
            Assert.Equal(new[] { (RecentRecordTypes.Item, records.Item) }, resolved.Keys);
        }
    }

    [PostgresFact]
    public async Task List_AtMostEightLabelQueries()
    {
        var section = await NewSectionAsync();
        var user = await NewUserAsync(section);
        var one = await SeedRecordsAsync(section, user.Id);

        var at = T0;
        foreach (var (type, id) in one.All) await TouchAsync(user.Id, type, id, at = at.AddMinutes(1));

        var counterOne = new CommandCounter();
        var listOne = await ListAsync(user.Id, counterOne);
        Assert.Equal(8, listOne.Count);

        // One more record of every type (16 of the 20 the list can hold): the number of queries must not grow with the number of rows.
        for (var i = 0; i < 1; i++)
        {
            var more = await SeedRecordsAsync(section, user.Id);
            foreach (var (type, id) in more.All) await TouchAsync(user.Id, type, id, at = at.AddMinutes(1));
        }

        var counterMany = new CommandCounter();
        var listMany = await ListAsync(user.Id, counterMany);
        Assert.Equal(16, listMany.Count);

        Assert.Equal(counterOne.Count, counterMany.Count);
        // The recents read (1), the section scope (at most 3), the document permission lookups (at most 3) and one
        // query per record type (8).
        Assert.InRange(counterMany.Count, 9, 15);
    }

    [PostgresFact]
    public async Task Clear_RemovesAll()
    {
        var section = await NewSectionAsync();
        var me = await NewUserAsync(section);
        var other = await NewUserAsync(section);
        var records = await SeedRecordsAsync(section, me.Id);

        await TouchAsync(me.Id, RecentRecordTypes.Sample, records.Sample, T0);
        await TouchAsync(me.Id, RecentRecordTypes.Item, records.Item, T0.AddMinutes(1));
        await TouchAsync(other.Id, RecentRecordTypes.Sample, records.Sample, T0.AddMinutes(2));

        await using (var actDb = _fixture.CreateDbContext())
        {
            var result = await NewController(NewService(actDb), me.Id).Clear(CancellationToken.None);
            Assert.IsType<NoContentResult>(result);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Empty(await assertDb.RecentRecords.AsNoTracking().Where(r => r.UserId == me.Id).ToListAsync());
        Assert.Equal(1, await assertDb.RecentRecords.AsNoTracking().CountAsync(r => r.UserId == other.Id));
        Assert.Empty(await assertDb.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(RecentRecord)).ToListAsync());
    }

    [PostgresFact]
    public async Task UnknownType_400()
    {
        var user = await NewUserAsync();
        var item = await NewItemAsync();

        // A BusinessRuleException is what ExceptionMiddleware answers with 400.
        foreach (var type in new[] { "bogus", "", "Sample", "SAMPLE" })
        {
            await using var actDb = _fixture.CreateDbContext();
            var controller = NewController(NewService(actDb), user.Id);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                controller.Touch(new TouchRecentRecordRequest(type, item.Id), CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Empty(await assertDb.RecentRecords.AsNoTracking().Where(r => r.UserId == user.Id).ToListAsync());
    }
}
