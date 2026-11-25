using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class QualityCorePostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "QualityCore-test-password";
    private static readonly DateTime Now = new(2085, 5, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider Clock = new FixedClock();
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Storage : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new();
        public Task<string> SaveAsync(string key, byte[] content)
        {
            _files.Add(key, content);
            return Task.FromResult(key);
        }
        public Task<byte[]> ReadAsync(string key) => Task.FromResult(_files[key]);
    }

    private MicroLimsDbContext Context(int? actor = null)
    {
        var db = fixture.CreateDbContext(Clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static QualityRecordCore Core(MicroLimsDbContext db) => new(db, new PermissionService(db),
        new UserSectionScopeService(db), new ReferenceNumberGenerator(db, Clock), new ElectronicSignatureService(db, Clock), Clock);
    private static QualityLinkService Links(MicroLimsDbContext db) => new(db, Core(db), new PermissionService(db),
        new UserSectionScopeService(db), Clock);

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType type, bool investigate = false, bool canRaise = true)
    {
        var key = Guid.NewGuid().ToString("N");
        var role = new Role { Name = key, Type = type, IsActive = true };
        db.Roles.Add(role);
        var user = new User
        {
            FullName = "Quality Core Test", Username = key, Role = role, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var codes = new List<string> { PermissionConstants.QualityView };
        if (canRaise) codes.Add(PermissionConstants.QualityRaise);
        if (investigate) codes.Add(PermissionConstants.QualityInvestigate);
        var permissions = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        Assert.Equal(codes.Count, permissions.Count);
        db.RolePermissions.AddRange(permissions.Select(id => new RolePermission { RoleId = role.Id, PermissionId = id }));
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id, DepartmentId = fixture.SeededDepartmentId, SectionId = fixture.SeededSectionId
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static QualityRecord Record(int? raiser = null, int? section = null, int? owner = null) => new()
    {
        Number = $"DEV-{Guid.NewGuid():N}"[..20], Title = "Scope test", Description = "Quality Core record",
        RaisedAtUtc = Now, RaisedByUserId = raiser, SectionId = section, OwnerUserId = owner
    };

    private async Task<int> OtherSectionAsync(MicroLimsDbContext db) =>
        await db.DocumentSections.AsNoTracking().Where(s => s.Id != fixture.SeededSectionId)
            .OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

    [PostgresFact]
    public async Task Create_persists_the_number_actor_and_audit_and_rejects_a_stale_version()
    {
        int actor;
        await using (var arrange = Context())
            actor = await ActorAsync(arrange, RoleType.Analyst);
        QualityRecord created;
        await using (var act = Context(actor))
        {
            var core = Core(act);
            created = await core.CreateAsync(QualityRecordType.Deviation,
                new("  Raised  ", "Evidence", fixture.SeededSectionId, []), actor);
            Assert.Equal(MessageCodes.QualityStale, ((ICodedMessage)await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
                core.LoadForWriteAsync(created.Id, created.Version + 1, actor))).Code);
            core.EnsureNotRaiserOrOwner(new(), actor);
            Assert.Throws<BusinessRuleException>(() => core.EnsureNotRaiserOrOwner(new() { OwnerUserId = actor }, actor));
        }
        await using var check = Context();
        var persisted = await check.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        Assert.Matches("^DEV-[0-9]{4}-[0-9]{4,}$", persisted.Number);
        Assert.Equal("Raised", persisted.Title);
        Assert.Equal(actor, persisted.RaisedByUserId);
        Assert.Equal(Now, persisted.RaisedAtUtc);
        Assert.Equal(created.Version, persisted.Version);
        var audit = await AuditAsync<QualityRecord>(check, created.Id, "Create");
        Assert.Equal(actor, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains("Evidence", audit.NewValue);
    }

    [PostgresFact]
    public async Task Scope_requires_the_section_and_analyst_participation_including_site_wide_records()
    {
        int analyst, reviewer;
        QualityRecord raised, owned, assigned, unrelated, outside;
        await using (var arrange = Context())
        {
            analyst = await ActorAsync(arrange, RoleType.Analyst);
            reviewer = await ActorAsync(arrange, RoleType.Reviewer);
            raised = Record(analyst, fixture.SeededSectionId);
            owned = Record(owner: analyst);
            assigned = Record(section: fixture.SeededSectionId);
            assigned.Actions.Add(new QualityAction { ResponsibleUserId = analyst, Description = "Follow up", DueDate = DateOnly.FromDateTime(Now) });
            unrelated = Record();
            outside = Record(analyst, await OtherSectionAsync(arrange));
            arrange.AddRange(raised, owned, assigned, unrelated, outside);
            await arrange.SaveChangesAsync();
        }
        await using var act = Context();
        var core = Core(act);
        foreach (var record in new[] { raised, owned, assigned })
            Assert.Equal(record.Id, (await core.LoadForReadAsync(record.Id, analyst)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => core.LoadForReadAsync(unrelated.Id, analyst));
        await Assert.ThrowsAsync<NotFoundException>(() => core.LoadForReadAsync(outside.Id, analyst));
        Assert.Equal(unrelated.Id, (await core.LoadForReadAsync(unrelated.Id, reviewer)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => core.LoadForReadAsync(unrelated.Id, fixture.SeededUserId));
    }

    [PostgresFact]
    public async Task Link_checks_target_access_and_reverse_lookup_filters_its_sources()
    {
        int analyst, investigator;
        QualityRecord source, target, unreadable, unlinkedUnreadable, scopedOut;
        await using (var arrange = Context())
        {
            analyst = await ActorAsync(arrange, RoleType.Analyst);
            investigator = await ActorAsync(arrange, RoleType.SectionHead, true);
            source = Record(analyst);
            target = Record(analyst);
            unreadable = Record();
            unlinkedUnreadable = Record();
            scopedOut = Record(owner: analyst, section: await OtherSectionAsync(arrange));
            arrange.AddRange(source, target, unreadable, unlinkedUnreadable, scopedOut);
            await arrange.SaveChangesAsync();
            arrange.QualityRecordLinks.AddRange(NewLink(source.Id, unreadable.Id, investigator),
                NewLink(scopedOut.Id, target.Id, investigator));
            await arrange.SaveChangesAsync();
        }
        QualityLinkDto added;
        await using (var act = Context(analyst))
        {
            var links = Links(act);
            await Assert.ThrowsAsync<NotFoundException>(() => links.AddAsync(source.Id, QualityLinkTargetType.QualityRecord,
                unlinkedUnreadable.Id, analyst, source.Version));
            await Assert.ThrowsAsync<NotFoundException>(() => links.AddAsync(source.Id, QualityLinkTargetType.QualityRecord,
                int.MaxValue, analyst, source.Version));
            Assert.Equal(MessageCodes.QualityInvalidTarget, (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                links.AddAsync(source.Id, (QualityLinkTargetType)999, target.Id, analyst, source.Version))).Code);
            added = await links.AddAsync(source.Id, QualityLinkTargetType.QualityRecord, target.Id, analyst, source.Version);
        }
        await using (var act = Context(analyst))
        {
            var links = Links(act);
            var restricted = Assert.Single(await links.ResolveAsync(source.Id, analyst), l => l.IsRestricted);
            Assert.Equal(unreadable.Number, restricted.Number);
            Assert.Null(restricted.Label);
            Assert.Null(restricted.Route);
            Assert.Equal(source.Id, Assert.Single(await links.ForTargetAsync(QualityLinkTargetType.QualityRecord, target.Id, analyst)).Id);
            await Assert.ThrowsAsync<NotFoundException>(() => links.ForTargetAsync(QualityLinkTargetType.QualityRecord, unreadable.Id, analyst));
        }
        await using var check = Context();
        Assert.Equal(target.Id, (await check.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == added.Id)).TargetId);
        var audit = await AuditAsync<QualityRecordLink>(check, added.Id, "Create");
        Assert.Equal(analyst, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains($"\"TargetId\":{target.Id}", audit.NewValue);
    }

    [PostgresFact]
    public async Task Signed_step_blocks_link_removal_in_service_and_database()
    {
        var (actor, recordId, linkId, version) = await ArrangeLinkAsync();
        await using (var act = Context(actor))
            await SignAsync(act, recordId, version, actor);
        await using (var act = Context(actor))
        {
            var current = await Core(act).LoadForReadAsync(recordId, actor);
            Assert.True(Core(act).IsStepSigned(current, QualityStepKind.DeviationAssessed));
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Links(act).RemoveAsync(recordId, linkId, actor, current.Version));
            Assert.Equal(MessageCodes.QualitySignedLink, error.Code);
        }
        await using (var act = Context(actor))
            await Assert.ThrowsAsync<PostgresException>(() =>
                act.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "QualityRecordLinks" WHERE "Id" = {linkId}"""));
        await using var check = Context();
        Assert.True(await check.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.Id == linkId));
        var step = await check.QualityRecordSteps.AsNoTracking().SingleAsync(s => s.RecordId == recordId);
        Assert.Equal(actor, step.SignedByUserId);
        Assert.Equal(Now, step.SignedAtUtc);
        using var snapshot = JsonDocument.Parse(step.SnapshotJson);
        var signedVersion = await check.QualityRecords.AsNoTracking().Where(r => r.Id == recordId)
            .Select(r => r.Version).SingleAsync();
        Assert.Equal(signedVersion, snapshot.RootElement.GetProperty("Version").GetUInt32());
        Assert.Equal("signed state", snapshot.RootElement.GetProperty("Data").GetProperty("Note").GetString());
        var audit = await AuditAsync<QualityRecordStep>(check, step.Id, "Create");
        Assert.Equal(actor, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains("signed state", audit.NewValue);
    }

    [PostgresFact]
    public async Task First_signature_serializes_with_concurrent_removal()
    {
        var (actor, recordId, linkId, version) = await ArrangeLinkAsync();
        await using var signer = Context(actor);
        await using var transaction = await signer.Database.BeginTransactionAsync();
        await SignAsync(signer, recordId, version, actor);

        await using var remover = Context(actor);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var removal = Links(remover).RemoveAsync(recordId, linkId, actor, version, timeout.Token);
        try
        {
            Assert.NotSame(removal, await Task.WhenAny(removal, Task.Delay(250, timeout.Token)));
        }
        finally
        {
            await transaction.CommitAsync(timeout.Token);
        }
        Assert.Equal(MessageCodes.QualityStale, ((ICodedMessage)await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() => removal)).Code);
        await using var check = Context();
        Assert.True(await check.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.Id == linkId));
        var step = await check.QualityRecordSteps.AsNoTracking().SingleAsync(s => s.RecordId == recordId);
        Assert.Equal(actor, (await AuditAsync<QualityRecordStep>(check, step.Id, "Create")).UserId);
    }

    [PostgresFact]
    public async Task Removal_that_wins_the_row_lock_invalidates_a_pending_signature()
    {
        var (actor, recordId, linkId, version) = await ArrangeLinkAsync();
        await using var remover = Context(actor);
        await using var transaction = await remover.Database.BeginTransactionAsync();
        await Links(remover).RemoveAsync(recordId, linkId, actor, version);
        await using var signer = Context(actor);
        var signing = SignAsync(signer, recordId, version, actor);
        try
        {
            Assert.NotSame(signing, await Task.WhenAny(signing, Task.Delay(250)));
        }
        finally
        {
            await transaction.CommitAsync();
        }

        var error = await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() => signing);
        Assert.Equal(MessageCodes.QualityStale, ((ICodedMessage)error).Code);
        await using var check = Context();
        Assert.False(await check.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.Id == linkId));
        Assert.False(await check.QualityRecordSteps.AsNoTracking().AnyAsync(s => s.RecordId == recordId));
        var audit = await AuditAsync<QualityRecordLink>(check, linkId, "Delete");
        Assert.Equal(actor, audit.UserId);
        Assert.NotNull(audit.PreviousValue);
        Assert.Null(audit.NewValue);
    }

    [PostgresFact]
    public async Task Attachments_reject_invalid_evidence_and_void_retains_the_row_and_audit()
    {
        int actor;
        QualityRecord record;
        await using (var arrange = Context())
        {
            actor = await ActorAsync(arrange, RoleType.Analyst);
            record = Record(actor, owner: actor);
            arrange.Add(record);
            await arrange.SaveChangesAsync();
        }
        var storage = new Storage();
        var content = "%PDF-test"u8.ToArray();
        QualityAttachmentDto uploaded;
        await using (var act = Context(actor))
        {
            var service = new QualityAttachmentService(act, Core(act), storage,
                new AuditEventService(act, fixture.CreateSequenceHelper(act), Clock), Clock);
            foreach (var (name, bytes) in new[] { ("unsafe.exe", content), ("wrong.pdf", "not a PDF"u8.ToArray()),
                ("large.pdf", new byte[DiscussionAttachmentPolicy.MaxFileBytes + 1]) })
                Assert.Equal(MessageCodes.QualityInvalidAttachment, (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.UploadAsync(record.Id, record.Version, name, bytes, actor))).Code);
            uploaded = await service.UploadAsync(record.Id, record.Version, "evidence.pdf", content, actor);
        }
        await using (var act = Context(actor))
        {
            var service = new QualityAttachmentService(act, Core(act), storage,
                new AuditEventService(act, fixture.CreateSequenceHelper(act), Clock), Clock);
            Assert.Equal(content, (await service.GetContentAsync(record.Id, uploaded.Id, actor)).Bytes);
            var header = await Core(act).ReadDtoAsync(record.Id, actor);
            Assert.Equal(uploaded, Assert.Single(header.Attachments));
            var version = header.Version;
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.VoidAsync(record.Id, uploaded.Id, version, " ", actor));
            await service.VoidAsync(record.Id, uploaded.Id, version, "Superseded", actor);
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetContentAsync(record.Id, uploaded.Id, actor));
        }
        await using var check = Context();
        var persisted = await check.QualityAttachments.AsNoTracking().SingleAsync(a => a.Id == uploaded.Id);
        Assert.Equal(actor, persisted.UploadedByUserId);
        Assert.Equal(Now, persisted.VoidedAtUtc);
        Assert.Equal(actor, persisted.VoidedByUserId);
        Assert.Equal("Superseded", persisted.VoidReason);
        Assert.Equal(content, await storage.ReadAsync(persisted.StorageKey));
        var create = await AuditAsync<QualityAttachment>(check, uploaded.Id, "Create");
        Assert.Equal(actor, create.UserId);
        Assert.Null(create.PreviousValue);
        Assert.Contains("evidence.pdf", create.NewValue);
        var audit = await AuditAsync<QualityAttachment>(check, uploaded.Id, "Update");
        Assert.Equal(actor, audit.UserId);
        Assert.Contains("\"VoidReason\":null", audit.PreviousValue);
        Assert.Contains("Superseded", audit.NewValue);
        var download = await AuditAsync<QualityAttachment>(check, uploaded.Id, "QualityAttachmentDownloaded");
        Assert.Equal(actor, download.UserId);
        Assert.StartsWith("EVT-", download.EventUid);
    }

    [PostgresFact]
    public async Task Comment_is_append_only_through_the_service_and_uses_record_scope()
    {
        int actor, other;
        QualityRecord record;
        await using (var arrange = Context())
        {
            actor = await ActorAsync(arrange, RoleType.Analyst);
            other = await ActorAsync(arrange, RoleType.Analyst);
            record = Record(actor);
            arrange.Add(record);
            await arrange.SaveChangesAsync();
        }
        QualityCommentDto comment;
        await using (var act = Context(actor))
        {
            var service = new QualityCommentService(act, Core(act), Clock);
            await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(record.Id, record.Version, "Outside scope", other));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddAsync(record.Id, record.Version, new string('a', 2001), actor));
            comment = await service.AddAsync(record.Id, record.Version, " Investigation note ", actor);
            Assert.Equal(comment, Assert.Single(await service.ListAsync(record.Id, actor, 0, 25)));
        }
        await using var check = Context();
        var saved = await check.QualityComments.AsNoTracking().SingleAsync(c => c.Id == comment.Id);
        Assert.Equal("Investigation note", saved.Text);
        Assert.Equal(actor, saved.AuthorUserId);
        Assert.Equal(Now, saved.CreatedAtUtc);
        var audit = await AuditAsync<QualityComment>(check, saved.Id, "Create");
        Assert.Equal(actor, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains(saved.Text, audit.NewValue);
    }

    [PostgresFact]
    public async Task Evidence_writes_require_raise_and_destruction_requires_owner_or_investigator()
    {
        int analyst, reader;
        QualityRecord source, target;
        await using (var arrange = Context())
        {
            analyst = await ActorAsync(arrange, RoleType.Analyst);
            reader = await ActorAsync(arrange, RoleType.Reviewer, canRaise: false);
            source = Record();
            source.Actions.Add(new QualityAction
            {
                ResponsibleUserId = analyst, Description = "Provide evidence", DueDate = DateOnly.FromDateTime(Now)
            });
            target = Record(analyst);
            arrange.AddRange(source, target);
            await arrange.SaveChangesAsync();
        }
        QualityLinkDto link;
        await using (var act = Context(analyst))
        {
            var links = Links(act);
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                links.AddAsync(source.Id, QualityLinkTargetType.QualityRecord, target.Id, reader, source.Version));
            link = await links.AddAsync(source.Id, QualityLinkTargetType.QualityRecord, target.Id, analyst, source.Version);
            var current = await Core(act).LoadForReadAsync(source.Id, analyst);
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                links.RemoveAsync(source.Id, link.Id, analyst, current.Version));
        }
        await using var check = Context();
        var persisted = await check.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == link.Id);
        Assert.Equal(analyst, persisted.AddedByUserId);
        Assert.Equal(Now, persisted.AddedAtUtc);
        var audit = await AuditAsync<QualityRecordLink>(check, link.Id, "Create");
        Assert.Equal(analyst, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.NotNull(audit.NewValue);
    }

    private async Task<(int Actor, int RecordId, int LinkId, uint Version)> ArrangeLinkAsync()
    {
        await using var arrange = Context();
        var actor = await ActorAsync(arrange, RoleType.SectionHead, true);
        var record = Record(actor);
        var target = Record(actor);
        arrange.AddRange(record, target);
        await arrange.SaveChangesAsync();
        var link = NewLink(record.Id, target.Id, actor);
        arrange.Add(link);
        await arrange.SaveChangesAsync();
        return (actor, record.Id, link.Id, record.Version);
    }

    private static QualityRecordLink NewLink(int source, int target, int user) => new()
    {
        RecordId = source, TargetId = target, TargetType = QualityLinkTargetType.QualityRecord,
        AddedByUserId = user, AddedAtUtc = Now
    };

    private static Task SignAsync(MicroLimsDbContext db, int id, uint version, int actor) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var core = Core(db);
            var record = await core.LoadForWriteAsync(id, version, actor);
            await core.SignStepAsync(record, QualityStepKind.DeviationAssessed, SignatureMeaning.DeviationAssessed,
                new { Note = "signed state" }, Password, null, actor, null);
        });

    private static Task<AuditLog> AuditAsync<T>(MicroLimsDbContext db, int id, string action) =>
        db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == typeof(T).Name &&
            a.EntityId == id.ToString() && a.Action == action);
}
