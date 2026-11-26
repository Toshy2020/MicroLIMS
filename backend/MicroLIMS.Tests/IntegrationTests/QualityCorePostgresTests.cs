using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.Interfaces;
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

    private sealed class Storage : IFileStorageService, IStorageDiagnostics
    {
        private readonly Dictionary<string, byte[]> _files = new();
        public int FileCount => _files.Count;
        public int SavedCount { get; private set; }
        public Task<string> SaveAsync(string key, byte[] content)
        {
            _files.Add(key, content);
            SavedCount++;
            return Task.FromResult(key);
        }
        public byte[]? Replacement { get; set; }
        public bool Missing { get; set; }
        public Task<byte[]> ReadAsync(string key) => Missing
            ? throw new StoredFileNotFoundException(key)
            : Task.FromResult(Replacement ?? _files[key]);
        public StorageDescription Describe() => new("Test", "memory", null, null);
        public Task DeleteAsync(string key)
        {
            _files.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
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

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType type, bool investigate = false, bool canRaise = true, bool oosView = false)
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
        if (oosView) codes.Add(PermissionConstants.OosView);
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
        var signature = await check.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
        Assert.Equal(step.SignatureId, signature.Id);
        Assert.Equal(actor, signature.UserId);
        Assert.Equal(SignatureMeaning.DeviationAssessed, signature.MeaningOfSignature);
        Assert.Equal("QualityRecord", signature.EntityType);
        Assert.Equal(recordId, signature.EntityId);
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
        int actor, reader;
        QualityRecord record;
        await using (var arrange = Context())
        {
            actor = await ActorAsync(arrange, RoleType.Analyst);
            reader = await ActorAsync(arrange, RoleType.Reviewer, canRaise: false);
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
        }
        await using (var act = Context(reader))
        {
            var header = await Core(act).ReadDtoAsync(record.Id, reader);
            Assert.True(Assert.Single(header.Attachments).IsVoided);
            var voided = await Attachments(act, storage).GetContentAsync(record.Id, uploaded.Id, reader);
            Assert.Equal(content, voided.Bytes);
            Assert.True(voided.IsVoided);
            Assert.Equal("Superseded", voided.VoidReason);
            Assert.Equal(actor, voided.VoidedByUserId);
            Assert.Equal(Now, voided.VoidedAtUtc);
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
        var download = await check.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(QualityAttachment) &&
            a.EntityId == uploaded.Id.ToString() && a.Action == "QualityAttachmentDownloaded").OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal(reader, download.UserId);
        Assert.StartsWith("EVT-", download.EventUid);
        var downloadChanges = await check.AuditEventChanges.AsNoTracking().Where(c => c.AuditLogId == download.Id).ToListAsync();
        Assert.Equal(record.Id.ToString(), Assert.Single(downloadChanges, c => c.FieldName == "RecordId").NewValue);
        Assert.Equal(record.Number, Assert.Single(downloadChanges, c => c.FieldName == "RecordNumber").NewValue);
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

    [PostgresFact]
    public async Task Download_distinguishes_record_scope_missing_storage_and_integrity_failure()
    {
        int actor, other;
        QualityRecord record, unrelated;
        await using (var arrange = Context())
        {
            actor = await ActorAsync(arrange, RoleType.Analyst);
            other = await ActorAsync(arrange, RoleType.Analyst);
            record = Record(actor);
            unrelated = Record(actor);
            arrange.AddRange(record, unrelated);
            await arrange.SaveChangesAsync();
        }

        var storage = new Storage();
        var bytes = "%PDF-original"u8.ToArray();
        var corrupt = "%PDF-corrupt"u8.ToArray();
        QualityAttachmentDto attachment;
        await using (var act = Context(actor))
            attachment = await Attachments(act, storage).UploadAsync(record.Id, record.Version, "evidence.pdf", bytes, actor);
        await using (var act = Context(actor))
            await Assert.ThrowsAsync<NotFoundException>(() => Attachments(act, storage).GetContentAsync(unrelated.Id, attachment.Id, actor));
        await using (var act = Context(other))
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Attachments(act, storage).GetContentAsync(record.Id, attachment.Id, other));
            await Assert.ThrowsAsync<NotFoundException>(() => Links(act).ResolveAsync(record.Id, other));
            await Assert.ThrowsAsync<NotFoundException>(() => new QualityCommentService(act, Core(act), Clock)
                .ListAsync(record.Id, other, 0, 25));
        }

        await using (var act = Context(actor))
        {
            var service = Attachments(act, storage);
            storage.Missing = true;
            var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetContentAsync(record.Id, attachment.Id, actor));
            Assert.Equal(MessageCodes.DocumentFileReadFromStorageFailed, missing.Code);
            storage.Missing = false;
            storage.Replacement = corrupt;
            var mismatch = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetContentAsync(record.Id, attachment.Id, actor));
            Assert.Equal(MessageCodes.DocumentFileIntegrityDeliveryBlocked, mismatch.Code);
        }

        await using var check = Context();
        var entityId = attachment.Id.ToString();
        var fault = await check.AuditLogs.AsNoTracking().Include(a => a.Changes).SingleAsync(a =>
            a.EntityName == nameof(QualityAttachment) && a.EntityId == entityId && a.Action == "FileIntegrityVerificationFailed");
        Assert.Equal(actor, fault.UserId);
        Assert.Equal(AuditActionCategory.Security, fault.ActionCategory);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            Assert.Single(fault.Changes, c => c.FieldName == "StoredHash").PreviousValue);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(corrupt)),
            Assert.Single(fault.Changes, c => c.FieldName == "ComputedHash").NewValue);
        Assert.False(await check.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(QualityAttachment) && a.EntityId == entityId && a.Action == "QualityAttachmentDownloaded"));
    }

    [PostgresFact]
    public async Task Resolve_batches_samples_and_oos_while_preserving_target_scope()
    {
        int analyst, outside;
        QualityRecord source;
        Sample[] samples;
        Sample unlinked;
        OosInvestigation owned, unrelated;
        var createTitle = $"refused-{Guid.NewGuid():N}";
        await using (var arrange = Context())
        {
            analyst = await ActorAsync(arrange, RoleType.Analyst, oosView: true);
            outside = await OtherSectionAsync(arrange);
            source = Record(analyst);
            var cause = new CauseOfTesting { Name = $"Quality link scope {Guid.NewGuid():N}" };
            samples = Enumerable.Range(0, 24).Select(i => TestSample(analyst,
                i % 2 == 0 ? fixture.SeededSectionId : outside, cause)).ToArray();
            unlinked = TestSample(analyst, outside, cause);
            arrange.Add(source);
            arrange.AddRange(samples);
            arrange.Add(unlinked);
            await arrange.SaveChangesAsync();

            var order = new TestOrder { SampleId = samples[0].Id, SectionId = fixture.SeededSectionId, AssignedAnalystId = analyst };
            arrange.Add(order);
            await arrange.SaveChangesAsync();

            owned = Investigation(samples[0].Id, fixture.SeededSectionId, analyst);
            owned.Results.Add(new OosInvestigationResult { TestOrderId = order.Id, AddedAtUtc = Now });
            unrelated = Investigation(samples[2].Id, fixture.SeededSectionId, analyst);
            arrange.AddRange(owned, unrelated);
            await arrange.SaveChangesAsync();
            foreach (var sample in samples)
                arrange.Add(NewLink(source.Id, sample.Id, analyst, QualityLinkTargetType.Sample));
            foreach (var investigation in new[] { owned, unrelated })
                arrange.Add(NewLink(source.Id, investigation.Id, analyst, QualityLinkTargetType.OosInvestigation));
            await arrange.SaveChangesAsync();
        }

        await using (var act = Context(analyst))
        {
            var links = Links(act);
            await Assert.ThrowsAsync<NotFoundException>(() =>
                links.AddAsync(source.Id, QualityLinkTargetType.Sample, unlinked.Id, analyst, source.Version));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                links.AddAsync(source.Id, QualityLinkTargetType.OosInvestigation, unrelated.Id, analyst, source.Version));
            var self = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                links.AddAsync(source.Id, QualityLinkTargetType.QualityRecord, source.Id, analyst, source.Version));
            Assert.Equal(MessageCodes.QualitySelfLink, self.Code);
            await Assert.ThrowsAsync<NotFoundException>(() => Core(act).CreateAsync(QualityRecordType.Deviation,
                new(createTitle, "Description", null, [new(QualityLinkTargetType.Sample, unlinked.Id)]), analyst));
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() => Core(act).CreateAsync(QualityRecordType.Deviation,
                new(createTitle, "Description", outside, []), analyst));
        }

        var counter = new QueryCounter();
        await using (var act = fixture.CreateDbContext(counter))
        {
            var resolved = await Links(act).ResolveAsync(source.Id, analyst);
            Assert.Equal(26, resolved.Count);
            foreach (var (sample, index) in samples.Select((sample, index) => (sample, index)))
            {
                var link = Assert.Single(resolved, l => l.Number == sample.ReferenceNumber);
                Assert.Equal(index % 2 != 0, link.IsRestricted);
                Assert.Equal(index % 2 == 0 ? sample.ReferenceNumber : null, link.Label);
                Assert.Equal(index % 2 == 0 ? RecordNavigationTargets.Sample(sample.Id) : null, link.Route);
            }
            var restrictedOos = Assert.Single(resolved, l => l.Number == unrelated.Number);
            Assert.True(restrictedOos.IsRestricted);
            Assert.Null(restrictedOos.Label);
            Assert.Null(restrictedOos.Route);
            var visibleOos = Assert.Single(resolved, l => l.Number == owned.Number);
            Assert.False(visibleOos.IsRestricted);
            Assert.Equal(owned.Number, visibleOos.Label);
            Assert.NotNull(visibleOos.Route);
            Assert.InRange(counter.Count, 1, 14);
        }

        await using var check = Context();
        Assert.False(await check.QualityRecords.AsNoTracking().AnyAsync(r => r.Title == createTitle));
        Assert.False(await check.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.RecordId == source.Id &&
            l.TargetType == QualityLinkTargetType.Sample && l.TargetId == unlinked.Id));
    }

    [PostgresFact]
    public async Task Sign_requires_a_transaction_and_persists_the_failed_password_audit()
    {
        var (actor, recordId, _, version) = await ArrangeLinkAsync();
        await using (var act = Context(actor))
        {
            var core = Core(act);
            var detached = await core.LoadForReadAsync(recordId, actor);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => core.SignStepAsync(detached,
                QualityStepKind.DeviationAssessed, SignatureMeaning.DeviationAssessed, new { }, Password, null, actor, null));
            Assert.Equal(MessageCodes.QualityTransactionRequired, error.Code);

            await Assert.ThrowsAsync<SignatureVerificationException>(() => UnitOfWork.RunAsync(act, () =>
                core.SignStepAsync(detached, QualityStepKind.DeviationAssessed, SignatureMeaning.DeviationAssessed,
                    new { }, "wrong password", null, actor, null)));
        }

        await using (var check = Context())
        {
            Assert.False(await check.QualityRecordSteps.AsNoTracking().AnyAsync(s => s.RecordId == recordId));
            Assert.False(await check.ElectronicSignatures.AsNoTracking()
                .AnyAsync(s => s.EntityType == "QualityRecord" && s.EntityId == recordId));
            var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "ElectronicSignature" &&
                a.EntityId == $"QualityRecord:{recordId}" && a.Action == "SignatureFailed");
            Assert.Equal(actor, audit.UserId);
            Assert.Null(audit.PreviousValue);
            Assert.Contains("DeviationAssessed", audit.NewValue);
        }

        int stepId;
        await using (var act = Context(actor))
        {
            var core = Core(act);
            var detached = await core.LoadForReadAsync(recordId, actor);
            Assert.Equal(version, detached.Version);
            var step = await UnitOfWork.RunAsync(act, () => core.SignStepAsync(detached, QualityStepKind.DeviationAssessed,
                SignatureMeaning.DeviationAssessed, new { }, Password, null, actor, null));
            stepId = step.Id;
        }
        await using (var check = Context())
        {
            var step = await check.QualityRecordSteps.AsNoTracking().SingleAsync(s => s.Id == stepId);
            Assert.Equal(recordId, step.RecordId);
            Assert.Equal(actor, (await AuditAsync<QualityRecordStep>(check, step.Id, "Create")).UserId);
        }
    }

    private static Sample TestSample(int userId, int section, CauseOfTesting cause) => new()
    {
        ReferenceNumber = $"Q-{Guid.NewGuid():N}"[..18], LabSectionId = section,
        ReceivedByUserId = userId, ReceivedAt = Now, CauseOfTesting = cause
    };

    private static OosInvestigation Investigation(int sampleId, int section, int userId) => new()
    {
        Number = $"OOS-{Guid.NewGuid():N}"[..20], SampleId = sampleId, SectionId = section,
        OpenedByUserId = userId, OpenedAtUtc = Now, TemplateSnapshotJson = "{}"
    };

    private QualityAttachmentService Attachments(MicroLimsDbContext db, Storage storage) =>
        new(db, Core(db), storage, new AuditEventService(db, fixture.CreateSequenceHelper(db), Clock), Clock);

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

    private static QualityRecordLink NewLink(int source, int target, int user,
        QualityLinkTargetType type = QualityLinkTargetType.QualityRecord) => new()
    {
        RecordId = source, TargetId = target, TargetType = type,
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

    private sealed class FailQualityAttachmentSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null && eventData.Context.ChangeTracker.Entries<QualityAttachment>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Simulated DB failure after file save.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [PostgresFact]
    public async Task UploadAsync_deletes_stored_file_when_database_step_fails()
    {
        int actor;
        QualityRecord record;
        await using (var arrange = Context())
        {
            actor = await ActorAsync(arrange, RoleType.Analyst);
            record = Record(actor);
            arrange.Add(record);
            await arrange.SaveChangesAsync();
        }

        var storage = new Storage();
        var bytes = "%PDF-1.4\n%%EOF"u8.ToArray();
        await using (var act = fixture.CreateDbContext(new FailQualityAttachmentSaveInterceptor()))
        {
            act.CurrentUserId = actor;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Attachments(act, storage).UploadAsync(record.Id, record.Version, "fail.pdf", bytes, actor));
        }

        Assert.Equal(1, storage.SavedCount);
        Assert.Equal(0, storage.FileCount);
        await using var check = Context();
        Assert.False(await check.QualityAttachments.AsNoTracking().AnyAsync(a => a.RecordId == record.Id));
    }

    [PostgresFact]
    public async Task ReadDtoAsync_computes_IsAddendum_for_attachments_after_terminal_step()
    {
        int actor, qa;
        QualityRecord record;
        await using (var arrange = Context())
        {
            actor = await ActorAsync(arrange, RoleType.Analyst);
            qa = await ActorAsync(arrange, RoleType.Qa, investigate: true);
            record = Record(actor);
            arrange.Add(record);
            await arrange.SaveChangesAsync();
        }

        var storage = new Storage();
        var bytes = "%PDF-1.4\n%%EOF"u8.ToArray();
        await using (var act = Context(actor))
            await Attachments(act, storage).UploadAsync(record.Id, record.Version, "before.pdf", bytes, actor);

        uint currentVersion;
        await using (var check = Context())
            currentVersion = await check.QualityRecords.AsNoTracking().Where(r => r.Id == record.Id).Select(r => r.Version).SingleAsync();

        await using (var act = Context(qa))
        {
            var core = Core(act);
            await UnitOfWork.RunAsync(act, async () =>
            {
                var r = await core.LoadForWriteAsync(record.Id, currentVersion, qa);
                await core.CancelAsync(r, "Cancelled for test", Password, qa, null, CancellationToken.None);
            });
        }

        await using (var check = Context())
            currentVersion = await check.QualityRecords.AsNoTracking().Where(r => r.Id == record.Id).Select(r => r.Version).SingleAsync();

        var laterClock = new MutableClock(Now.AddHours(1));
        await using (var act = fixture.CreateDbContext(laterClock))
        {
            act.CurrentUserId = actor;
            var svc = new QualityAttachmentService(act, new QualityRecordCore(act, new PermissionService(act),
                new UserSectionScopeService(act), new ReferenceNumberGenerator(act, laterClock),
                new ElectronicSignatureService(act, laterClock), laterClock),
                storage, new AuditEventService(act, fixture.CreateSequenceHelper(act), laterClock), laterClock);
            await svc.UploadAsync(record.Id, currentVersion, "after.pdf", bytes, actor);
        }

        await using (var assertDb = Context(actor))
        {
            var dto = await Core(assertDb).ReadDtoAsync(record.Id, actor);
            Assert.Equal(2, dto.Attachments.Count);
            Assert.False(dto.Attachments[0].IsAddendum);
            Assert.True(dto.Attachments[1].IsAddendum);
        }
    }

    private sealed class MutableClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
