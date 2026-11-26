using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class QualityConfigPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Quality-config-test";
    private static readonly DateOnly Today = new(2088, 6, 7);
    private static readonly TimeProvider Clock = new FixedClock();

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
    }

    private MicroLimsDbContext Context(int? actor = null)
    {
        var db = fixture.CreateDbContext(Clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static QualityConfigService Config(MicroLimsDbContext db)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, Clock),
            new ElectronicSignatureService(db, Clock), Clock);
        return new(db, core, permissions, scope, Clock);
    }

    private async Task<T> ActAsync<T>(int actor, Func<QualityConfigService, Task<T>> command)
    {
        await using var db = Context(actor);
        return await command(Config(db));
    }

    private async Task<int> ActorAsync(MicroLimsDbContext db, bool qa, bool outside = false)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Quality configuration test", Username = key, IsActive = true,
            Role = new Role { Name = key, Type = qa ? RoleType.Qa : RoleType.SectionHead, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var permission = qa ? PermissionConstants.QualityApprove : PermissionConstants.QualityInvestigate;
        var ids = await db.Permissions.AsNoTracking()
            .Where(p => p.Code == permission || p.Code == PermissionConstants.QualityView)
            .Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = user.RoleId, PermissionId = id }));
        var section = await db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && (outside ? s.Id != fixture.SeededSectionId : s.Id == fixture.SeededSectionId))
            .OrderBy(s => s.Id).Select(s => new { s.Id, s.DepartmentId }).FirstAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, SectionId = section.Id, DepartmentId = section.DepartmentId });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(QualityRecord Record, int Owner, int Qa)> WorldAsync(QualityRecordType type, bool qaOwner = false)
    {
        await using var db = Context();
        var owner = await ActorAsync(db, qaOwner);
        var qa = await ActorAsync(db, true);
        var record = new QualityRecord
        {
            Number = Guid.NewGuid().ToString("N")[..20], Title = "Quality configuration test", Description = "Test",
            Type = type, SectionId = fixture.SeededSectionId, OwnerUserId = owner, DueDate = Today.AddDays(-1),
            RaisedAtUtc = Clock.GetUtcNow().UtcDateTime,
            Actions = [new() { ResponsibleUserId = owner, Description = "Action", DueDate = Today.AddDays(-2) }]
        };
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync();
        return (record, owner, qa);
    }

    [PostgresFact]
    public async Task Section_template_wins_and_answers_keep_their_text_after_edit()
    {
        var (record, owner, qa) = await WorldAsync(QualityRecordType.ChangeControl);
        var section = fixture.SeededSectionId;
        var site = new SaveQualityTemplateRequest([new("validation", 1, "Site text", true)]);
        var local = new SaveQualityTemplateRequest([new("validation", 1, "Section text", true)]);
        int outsider;
        await using (var db = Context()) outsider = await ActorAsync(db, false, outside: true);

        await ActAsync(qa, s => s.SaveTemplateAsync(record.Type, null, site, 0, qa));
        Assert.Equal("Site text", Assert.Single((await ActAsync(owner, s => s.GetTemplateAsync(record.Type, section, owner)))!.Items).Text);
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            ActAsync(outsider, s => s.SaveTemplateAsync(record.Type, section, local, 0, outsider)));
        var template = await ActAsync(owner, s => s.SaveTemplateAsync(record.Type, section, local, 0, owner));
        Assert.Equal("Section text", Assert.Single((await ActAsync(owner, s => s.GetTemplateAsync(record.Type, section, owner)))!.Items).Text);

        var first = await ActAsync(owner, s => s.AnswerAsync(record.Id, "validation", QualityChecklistAnswerValue.Yes, "first", record.Version, owner));
        await ActAsync(owner, s => s.SaveTemplateAsync(record.Type, section,
            new([new("validation", 1, "Revised text", true)]), template.Version, owner));
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            ActAsync(owner, s => s.AnswerAsync(record.Id, "validation", QualityChecklistAnswerValue.No, null, record.Version, owner)));
        var second = await ActAsync(owner, s => s.AnswerAsync(record.Id, "validation", QualityChecklistAnswerValue.No, null, first.RecordVersion, owner));
        var latest = await ActAsync(owner, s => s.ListAnswersAsync(record.Id, 1, 100, owner));
        Assert.Equal(second.Id, Assert.Single(latest).Id);

        await using var check = Context();
        var persisted = await check.QualityChecklistAnswers.AsNoTracking().Where(a => a.RecordId == record.Id).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(new[] { "Section text", "Revised text" }, persisted.Select(a => a.ItemText));
        Assert.Equal(owner, persisted[0].AnsweredByUserId);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, persisted[0].AnsweredAtUtc);
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityChecklistAnswer)
            && a.EntityId == first.Id.ToString() && a.Action == "Create");
        Assert.Equal(owner, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains("Section text", audit.NewValue);
    }

    [PostgresFact]
    public async Task Due_rules_skip_lab_holidays_and_null_rules_leave_the_date_empty()
    {
        var (_, owner, qa) = await WorldAsync(QualityRecordType.Complaint);
        var type = QualityRecordType.Complaint;
        var start = new DateOnly(2088, 6, 7);
        await using (var arrange = Context())
        {
            arrange.LabCalendarDates.Add(new() { Date = start.AddDays(1), Description = "Test holiday" });
            await arrange.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ActAsync(owner, s => s.SaveDueRuleAsync(type, null, 1, 0, owner)));
        var rule = await ActAsync(qa, s => s.SaveDueRuleAsync(type, null, 1, 0, qa));
        await using (var act = Context())
            Assert.Equal(start.AddDays(2), await new QualityDueDateService(act).ComputeDueAsync(type, 1, start));
        await ActAsync(qa, s => s.SaveDueRuleAsync(type, 1, null, 0, qa));
        await using var check = Context();
        Assert.Equal(start.AddDays(2), await new QualityDueDateService(check).ComputeDueAsync(type, 1, start));
        Assert.Null(await new QualityDueDateService(check).ComputeDueAsync(QualityRecordType.Capa, null, start));
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityDueRule)
            && a.EntityId == rule.Id.ToString() && a.Action == "Create");
        Assert.Equal(qa, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains("\"WorkingDays\":1", audit.NewValue);
    }

    [PostgresFact]
    public async Task Extension_approval_and_refusal_are_audited_signed_and_immutable()
    {
        var (record, owner, qa) = await WorldAsync(QualityRecordType.Capa);
        var requested = Today.AddDays(10);
        var pending = await ActAsync(owner, s => s.RequestAsync(record.Id, null, requested, "More time", record.Version, owner));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            ActAsync(owner, s => s.DecideAsync(pending.Id, true, null, Password, pending.Version, owner)));
        var badPassword = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ActAsync(qa, s => s.DecideAsync(pending.Id, true, null, "wrong-password", pending.Version, qa)));
        Assert.Equal(MessageCodes.QualityExtensionSignatureFailed, badPassword.Code);
        await using (var check = Context())
        {
            Assert.Equal(record.DueDate, await check.QualityRecords.AsNoTracking().Where(r => r.Id == record.Id).Select(r => r.DueDate).SingleAsync());
            var saved = await check.QualityExtensionRequests.AsNoTracking().SingleAsync(r => r.Id == pending.Id);
            Assert.Null(saved.Decision);
            Assert.Null(saved.SignatureId);
            Assert.Equal(pending.Version, saved.Version);
            Assert.False(await check.QualityRecordSteps.AsNoTracking().AnyAsync(s => s.RecordId == record.Id));
        }
        var approved = await ActAsync(qa, s => s.DecideAsync(pending.Id, true, "Accepted", Password, pending.Version, qa));
        var actionId = Assert.Single(record.Actions).Id;
        var actionRequest = await ActAsync(owner, s => s.RequestAsync(record.Id, actionId, requested, "More action time", approved.RecordVersion, owner));
        var refused = await ActAsync(qa, s => s.DecideAsync(actionRequest.Id, false, "Refused", Password, actionRequest.Version, qa));

        await using (var check = Context())
        {
            Assert.Equal(requested, await check.QualityRecords.AsNoTracking().Where(r => r.Id == record.Id).Select(r => r.DueDate).SingleAsync());
            Assert.Equal(Today.AddDays(-2), await check.QualityActions.AsNoTracking().Where(a => a.Id == actionId).Select(a => a.DueDate).SingleAsync());
            var requestAudit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityExtensionRequest)
                && a.EntityId == pending.Id.ToString() && a.Action == "Create");
            Assert.Equal(owner, requestAudit.UserId);
            Assert.Null(requestAudit.PreviousValue);
            Assert.Contains("More time", requestAudit.NewValue);
            foreach (var decision in new[] { approved, refused })
            {
                var saved = await check.QualityExtensionRequests.AsNoTracking().SingleAsync(r => r.Id == decision.Id);
                Assert.Equal(decision.Id == approved.Id ? QualityExtensionDecision.Approved : QualityExtensionDecision.Refused, saved.Decision);
                Assert.Equal(decision.Version, saved.Version);
                Assert.Equal(qa, saved.DecidedByUserId);
                Assert.Equal(Clock.GetUtcNow().UtcDateTime, saved.DecidedAtUtc);
                var signature = await check.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == saved.SignatureId);
                Assert.Equal(SignatureMeaning.QualityExtensionDecided, signature.MeaningOfSignature);
                Assert.Equal(qa, signature.UserId);
                Assert.Equal(record.Id, signature.EntityId);
                var step = await check.QualityRecordSteps.AsNoTracking().SingleAsync(s => s.SignatureId == signature.Id);
                using var snapshot = JsonDocument.Parse(step.SnapshotJson);
                Assert.Equal(decision.RecordVersion, snapshot.RootElement.GetProperty("Version").GetUInt32());
                Assert.Equal(saved.Id, snapshot.RootElement.GetProperty("Data").GetProperty("RequestId").GetInt32());
                var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityExtensionRequest)
                    && a.EntityId == saved.Id.ToString() && a.Action == "Update");
                Assert.Equal(qa, audit.UserId);
                Assert.Contains("\"Decision\":null", audit.PreviousValue);
                Assert.Contains($"\"SignatureId\":{signature.Id}", audit.NewValue);
            }
        }
        await using (var edit = Context(qa))
        {
            var saved = await edit.QualityExtensionRequests.SingleAsync(r => r.Id == approved.Id);
            saved.DecisionComment = "Tampered";
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => edit.SaveChangesAsync());
            Assert.Contains("immutable", error.InnerException?.Message);
        }
        await using var persisted = Context();
        Assert.Equal("Accepted", await persisted.QualityExtensionRequests.AsNoTracking()
            .Where(r => r.Id == approved.Id).Select(r => r.DecisionComment).SingleAsync());
    }

    [PostgresFact]
    public async Task Responsible_user_can_extend_their_action_without_changing_the_record_due_date()
    {
        var (record, owner, qa) = await WorldAsync(QualityRecordType.Capa);
        var actionId = Assert.Single(record.Actions).Id;
        int responsible;
        await using (var arrange = Context())
        {
            responsible = await ActorAsync(arrange, false);
            var action = await arrange.QualityActions.SingleAsync(a => a.Id == actionId);
            action.ResponsibleUserId = responsible;
            await arrange.SaveChangesAsync();
        }

        var date = Today.AddDays(4);
        var ownerRequest = await ActAsync(owner, s => s.RequestAsync(record.Id, actionId, date, "Owner reason", record.Version, owner));
        Assert.Equal(actionId, ownerRequest.ActionId);
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            ActAsync(responsible, s => s.RequestAsync(record.Id, null, date, "Reason", ownerRequest.RecordVersion, responsible)));
        var pending = await ActAsync(responsible, s => s.RequestAsync(record.Id, actionId, date, "Reason", ownerRequest.RecordVersion, responsible));
        var decided = await ActAsync(qa, s => s.DecideAsync(pending.Id, true, null, Password, pending.Version, qa));
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            ActAsync(qa, s => s.DecideAsync(pending.Id, false, null, Password, pending.Version, qa)));

        await using var check = Context();
        Assert.Equal(date, await check.QualityActions.AsNoTracking().Where(a => a.Id == actionId).Select(a => a.DueDate).SingleAsync());
        Assert.Equal(record.DueDate, await check.QualityRecords.AsNoTracking().Where(r => r.Id == record.Id).Select(r => r.DueDate).SingleAsync());
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityAction)
            && a.EntityId == actionId.ToString() && a.UserId == qa && a.Action == "Update");
        Assert.Contains(Today.AddDays(-2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), audit.PreviousValue);
        Assert.Contains(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), audit.NewValue);
        Assert.NotNull(decided.SignatureId);
    }

    [PostgresFact]
    public async Task Quality_approval_is_required_to_administer_classifications_and_updates_are_audited()
    {
        var (_, owner, qa) = await WorldAsync(QualityRecordType.Complaint);
        var name = Guid.NewGuid().ToString("N");
        var request = new SaveQualityClassificationRequest(name, 4, true);
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            ActAsync(owner, s => s.SaveClassificationAsync(0, request, 0, owner)));
        var added = await ActAsync(qa, s => s.SaveClassificationAsync(0, request, 0, qa));
        var changed = await ActAsync(qa, s => s.SaveClassificationAsync(added.Id, request with { IsActive = false }, added.Version, qa));
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            ActAsync(qa, s => s.SaveClassificationAsync(added.Id, request, added.Version, qa)));
        var listed = await ActAsync(owner, s => s.ListClassificationsAsync(1, 100, owner));
        Assert.Contains(changed, listed);

        await using var check = Context();
        var persisted = await check.QualityClassifications.AsNoTracking().SingleAsync(c => c.Id == added.Id);
        Assert.Equal(name, persisted.Name);
        Assert.False(persisted.IsActive);
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityClassification)
            && a.EntityId == added.Id.ToString() && a.Action == "Update");
        Assert.Equal(qa, audit.UserId);
        Assert.Contains("\"IsActive\":true", audit.PreviousValue);
        Assert.Contains("\"IsActive\":false", audit.NewValue);
    }

    [PostgresFact]
    public async Task Requester_cannot_decide_even_with_quality_approval_permission()
    {
        var (record, owner, qa) = await WorldAsync(QualityRecordType.Deviation, qaOwner: true);
        var pending = await ActAsync(owner, s => s.RequestAsync(record.Id, null, Today.AddDays(2), "Reason", record.Version, owner));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ActAsync(owner, s => s.DecideAsync(pending.Id, true, null, Password, pending.Version, owner)));
        Assert.Equal(MessageCodes.QualityExtensionSelfDecision, error.Code);
        await ActAsync(qa, s => s.DecideAsync(pending.Id, false, null, Password, pending.Version, qa));
    }

    [PostgresFact]
    public async Task Overdue_records_and_actions_use_the_supplied_lab_date()
    {
        var (record, owner, _) = await WorldAsync(QualityRecordType.Deviation);
        var action = Assert.Single(record.Actions);
        var onRecordDueDate = await ActAsync(owner, s => s.ListOverdueAsync(record.DueDate!.Value, owner));
        Assert.DoesNotContain(onRecordDueDate.Records, r => r.RecordId == record.Id);
        Assert.Contains(onRecordDueDate.Actions, r => r.ActionId == action.Id);
        var later = await ActAsync(owner, s => s.ListOverdueAsync(Today, owner));
        Assert.Contains(later.Records, r => r.RecordId == record.Id && r.DueDate == record.DueDate);
    }

    [PostgresFact]
    public async Task Overdue_records_and_actions_respect_section_scope_and_analyst_own_filter()
    {
        var (ownRecord, owner, _) = await WorldAsync(QualityRecordType.Deviation);
        int analyst, otherSectionId, otherRecordId, analystRecordId;
        await using (var arrange = Context())
        {
            var analystKey = Guid.NewGuid().ToString("N");
            var analystUser = new User
            {
                FullName = "Overdue analyst", Username = analystKey, IsActive = true,
                Role = new Role { Name = analystKey, Type = RoleType.Analyst, IsActive = true },
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
            };
            arrange.Users.Add(analystUser);
            await arrange.SaveChangesAsync();
            var viewId = await arrange.Permissions.AsNoTracking()
                .Where(p => p.Code == PermissionConstants.QualityView).Select(p => p.Id).SingleAsync();
            arrange.RolePermissions.Add(new RolePermission { RoleId = analystUser.RoleId, PermissionId = viewId });
            var seededSection = await arrange.DocumentSections.AsNoTracking()
                .Where(s => s.Id == fixture.SeededSectionId).Select(s => new { s.Id, s.DepartmentId }).SingleAsync();
            arrange.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = analystUser.Id, SectionId = seededSection.Id, DepartmentId = seededSection.DepartmentId
            });
            otherSectionId = await arrange.DocumentSections.AsNoTracking()
                .Where(s => s.IsActive && s.Id != fixture.SeededSectionId).OrderBy(s => s.Id)
                .Select(s => s.Id).FirstAsync();

            var otherSectionRecord = new QualityRecord
            {
                Number = Guid.NewGuid().ToString("N")[..20], Title = "Other section overdue", Description = "Test",
                Type = QualityRecordType.Deviation, SectionId = otherSectionId, OwnerUserId = owner,
                DueDate = Today.AddDays(-3), RaisedAtUtc = Clock.GetUtcNow().UtcDateTime
            };
            var analystRecord = new QualityRecord
            {
                Number = Guid.NewGuid().ToString("N")[..20], Title = "Analyst own overdue", Description = "Test",
                Type = QualityRecordType.Deviation, SectionId = fixture.SeededSectionId, OwnerUserId = analystUser.Id,
                DueDate = Today.AddDays(-2), RaisedAtUtc = Clock.GetUtcNow().UtcDateTime,
                Actions = [new() { ResponsibleUserId = analystUser.Id, Description = "Analyst action", DueDate = Today.AddDays(-2) }]
            };
            arrange.QualityRecords.AddRange(otherSectionRecord, analystRecord);
            await arrange.SaveChangesAsync();
            analyst = analystUser.Id;
            otherRecordId = otherSectionRecord.Id;
            analystRecordId = analystRecord.Id;
        }

        var ownerOverdue = await ActAsync(owner, s => s.ListOverdueAsync(Today, owner));
        Assert.Contains(ownerOverdue.Records, r => r.RecordId == ownRecord.Id);
        Assert.DoesNotContain(ownerOverdue.Records, r => r.RecordId == otherRecordId);

        var analystOverdue = await ActAsync(analyst, s => s.ListOverdueAsync(Today, analyst));
        Assert.Contains(analystOverdue.Records, r => r.RecordId == analystRecordId);
        Assert.Contains(analystOverdue.Actions, a => a.RecordId == analystRecordId);
        Assert.DoesNotContain(analystOverdue.Records, r => r.RecordId == ownRecord.Id);
        Assert.DoesNotContain(analystOverdue.Records, r => r.RecordId == otherRecordId);
    }

    [PostgresFact]
    public async Task Answer_is_refused_on_terminal_or_cancelled_record_without_persisting()
    {
        var (record, owner, qa) = await WorldAsync(QualityRecordType.Deviation);
        await ActAsync(qa, s => s.SaveTemplateAsync(record.Type, null, new([new("check", 1, "Check item", true)]), 0, qa));
        await using (var arrange = Context())
        {
            arrange.DeviationDetails.Add(new DeviationDetail
            {
                RecordId = record.Id, Status = QualityDeviationStatus.Closed, OccurredAtUtc = Clock.GetUtcNow().UtcDateTime
            });
            await arrange.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ActAsync(owner, s => s.AnswerAsync(record.Id, "check", QualityChecklistAnswerValue.Yes, null, record.Version, owner)));
        Assert.Equal(MessageCodes.QualityRecordTerminal, error.Code);

        await using var check = Context();
        Assert.False(await check.QualityChecklistAnswers.AsNoTracking().AnyAsync(a => a.RecordId == record.Id));
        Assert.Equal(record.Version, await check.QualityRecords.AsNoTracking()
            .Where(r => r.Id == record.Id).Select(r => r.Version).SingleAsync());
    }

    [PostgresFact]
    public async Task Extension_request_is_refused_on_terminal_or_cancelled_record_without_persisting()
    {
        var (record, owner, _) = await WorldAsync(QualityRecordType.Capa);
        await using (var arrange = Context())
        {
            arrange.CapaDetails.Add(new CapaDetail { RecordId = record.Id, Status = CapaStatus.Closed });
            await arrange.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ActAsync(owner, s => s.RequestAsync(record.Id, null, Today.AddDays(5), "Too late", record.Version, owner)));
        Assert.Equal(MessageCodes.QualityRecordTerminal, error.Code);

        await using var check = Context();
        Assert.False(await check.QualityExtensionRequests.AsNoTracking().AnyAsync(e => e.RecordId == record.Id));
        Assert.Equal(record.Version, await check.QualityRecords.AsNoTracking()
            .Where(r => r.Id == record.Id).Select(r => r.Version).SingleAsync());
    }

    [PostgresFact]
    public async Task Extension_decision_is_refused_on_terminal_or_cancelled_record_without_persisting()
    {
        var (record, owner, qa) = await WorldAsync(QualityRecordType.ChangeControl);
        var pending = await ActAsync(owner, s => s.RequestAsync(record.Id, null, Today.AddDays(5), "Before cancel", record.Version, owner));
        await using (var arrange = Context())
        {
            var saved = await arrange.QualityRecords.SingleAsync(r => r.Id == record.Id);
            saved.IsCancelled = true;
            await arrange.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ActAsync(qa, s => s.DecideAsync(pending.Id, true, "Approve", Password, pending.Version, qa)));
        Assert.Equal(MessageCodes.QualityRecordTerminal, error.Code);

        await using var check = Context();
        var persistedRequest = await check.QualityExtensionRequests.AsNoTracking().SingleAsync(e => e.Id == pending.Id);
        Assert.Null(persistedRequest.Decision);
        Assert.Null(persistedRequest.SignatureId);
        Assert.Equal(record.DueDate, await check.QualityRecords.AsNoTracking()
            .Where(r => r.Id == record.Id).Select(r => r.DueDate).SingleAsync());
        Assert.False(await check.QualityRecordSteps.AsNoTracking().AnyAsync(s => s.RecordId == record.Id));
    }
}
