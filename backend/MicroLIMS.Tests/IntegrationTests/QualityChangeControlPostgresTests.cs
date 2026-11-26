using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
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
public class QualityChangeControlPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Cc-test-password";
    private static readonly DateTime BaseNow = new(2086, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(BaseNow);

    private sealed class MutableClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    private MicroLimsDbContext Context(MutableClock clock, int? actor = null)
    {
        var db = fixture.CreateDbContext(clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static ChangeControlService Service(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        var config = new QualityConfigService(db, core, permissions, scope, clock);
        return new(db, core, permissions, scope, new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(db), config);
    }

    private static QualityLinkService Links(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        return new(db, core, permissions, scope, clock);
    }

    private static QualityConfigService Config(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        return new(db, core, permissions, scope, clock);
    }

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType role, bool member = true)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"CC {role}", Username = key, IsActive = true,
            Role = new Role { Name = key, Type = role, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var codes = new List<string> { PermissionConstants.QualityView, PermissionConstants.QualityRaise };
        if (role == RoleType.Qa) codes.Add(PermissionConstants.QualityApprove);
        if (role == RoleType.SectionHead)
            codes.AddRange([PermissionConstants.QualityInvestigate, PermissionConstants.QualityLabConclude]);
        var ids = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = user.RoleId, PermissionId = id }));
        var membership = await db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && (member ? s.Id == fixture.SeededSectionId : s.Id != fixture.SeededSectionId))
            .OrderBy(s => s.Id).Select(s => new { s.Id, s.DepartmentId }).FirstAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id, DepartmentId = membership.DepartmentId, SectionId = membership.Id
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed record World(int Raiser, int Assessor, int TaskOwner, int Qa, int OtherQa, int OutsideHead,
        int Analyst, int TestDefId, int SpecId, int SampleId);

    private async Task<World> ArrangeAsync(MutableClock clock, bool withChecklist = false)
    {
        await using var db = Context(clock);
        var raiser = await ActorAsync(db, RoleType.QcManager);
        var assessor = await ActorAsync(db, RoleType.SectionHead);
        var taskOwner = await ActorAsync(db, RoleType.Analyst);
        var qa = await ActorAsync(db, RoleType.Qa);
        var otherQa = await ActorAsync(db, RoleType.Qa);
        var outsideHead = await ActorAsync(db, RoleType.SectionHead, member: false);
        var analyst = await ActorAsync(db, RoleType.Analyst);

        var testCode = "TST-" + Guid.NewGuid().ToString("N")[..8];
        var item = new Item
        {
            Code = "ITM-" + Guid.NewGuid().ToString("N")[..8],
            Name = "CC Test Item",
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var testDef = new TestDefinition
        {
            Code = testCode,
            DisplayName = "CC Test Definition",
            SectionId = fixture.SeededSectionId,
            WorkflowType = WorkflowType.Observation,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testCode,
            ParameterName = "Appearance",
            SpecLimit = "Clear"
        };
        db.Specifications.Add(spec);
        var causeId = await db.CausesOfTesting.AsNoTracking().Select(c => c.Id).FirstAsync();
        var sample = new Sample
        {
            ReferenceNumber = "SMP-" + Guid.NewGuid().ToString("N")[..8],
            LabSectionId = fixture.SeededSectionId,
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = causeId,
            ReceivedByUserId = assessor
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var existing = await db.QualityChecklistTemplates
            .Include(t => t.Items)
            .SingleOrDefaultAsync(t => t.Type == QualityRecordType.ChangeControl && t.SectionId == fixture.SeededSectionId);
        if (withChecklist)
        {
            if (existing is null)
            {
                existing = new QualityChecklistTemplate
                {
                    Type = QualityRecordType.ChangeControl,
                    SectionId = fixture.SeededSectionId
                };
                db.QualityChecklistTemplates.Add(existing);
            }
            var itemEntry = existing.Items.SingleOrDefault(i => i.Key == "val-impact");
            if (itemEntry is null)
            {
                existing.Items.Add(new QualityChecklistItem
                {
                    Key = "val-impact",
                    Order = 1,
                    Text = "Is validation impacted?",
                    IsActive = true
                });
            }
            else
            {
                itemEntry.IsActive = true;
            }
            await db.SaveChangesAsync();
        }
        else if (existing is not null)
        {
            foreach (var i in existing.Items) i.IsActive = false;
            await db.SaveChangesAsync();
        }

        return new(raiser, assessor, taskOwner, qa, otherQa, outsideHead, analyst, testDef.Id, spec.Id, sample.Id);
    }

    private async Task<ChangeControlDto> ActAsync(MutableClock clock, int actor, Func<ChangeControlService, Task<ChangeControlDto>> action)
    {
        await using var db = Context(clock, actor);
        return await action(Service(db, clock));
    }

    private Task<ChangeControlDto> ProposeAsync(MutableClock clock, World w, int? actor = null,
        IReadOnlyList<ChangeControlAffectedInput>? affected = null) =>
        ActAsync(clock, actor ?? w.Raiser, s => s.ProposeAsync(
            new("Update test specification", "Revise acceptance criteria", "Method update", fixture.SeededSectionId,
                affected ?? [new(QualityLinkTargetType.TestDefinition, w.TestDefId, null)]),
            actor ?? w.Raiser));

    private Task<ChangeControlDto> SaveAssessmentAsync(MutableClock clock, World w, ChangeControlDto cc,
        IReadOnlyList<ChangeControlChecklistAnswerInput>? answers = null, int? actor = null) =>
        ActAsync(clock, actor ?? w.Assessor, s => s.SaveAssessmentAsync(
            cc.Id,
            new("Moderate impact on test execution",
                [new("Update test method SOP", w.TaskOwner, Today.AddDays(3))],
                answers),
            cc.Version, actor ?? w.Assessor));

    private Task<ChangeControlDto> SignAssessmentAsync(MutableClock clock, World w, ChangeControlDto cc,
        int? actor = null, string password = Password) =>
        ActAsync(clock, actor ?? w.Assessor, s => s.SignAssessmentAsync(cc.Id, password, cc.Version, actor ?? w.Assessor));

    private Task<ChangeControlDto> ApproveAsync(MutableClock clock, World w, ChangeControlDto cc,
        int? actor = null, string password = Password) =>
        ActAsync(clock, actor ?? w.Qa, s => s.ApproveAsync(cc.Id, password, cc.Version, actor ?? w.Qa));

    private Task<ChangeControlDto> CompleteTaskAsync(MutableClock clock, World w, ChangeControlDto cc,
        int? actor = null) =>
        ActAsync(clock, actor ?? w.TaskOwner, s => s.CompleteTaskAsync(
            cc.Tasks[0].Id, new("SOP updated and trained"), cc.Version, actor ?? w.TaskOwner));

    private async Task AssertStepTriadAsync(MutableClock clock, int recordId, QualityStepKind kind,
        SignatureMeaning meaning, int expectedActor, ChangeControlStatus expectedStatus)
    {
        await using var db = Context(clock);
        var record = await db.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == recordId);
        var detail = await db.ChangeControlDetails.AsNoTracking().SingleAsync(d => d.RecordId == recordId);
        Assert.Equal(expectedStatus, detail.Status);

        var step = await db.QualityRecordSteps.AsNoTracking()
            .SingleAsync(s => s.RecordId == recordId && s.Kind == kind);
        Assert.Equal(expectedActor, step.SignedByUserId);
        Assert.Equal(clock.UtcNow, step.SignedAtUtc);

        var signature = await db.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
        Assert.Equal(expectedActor, signature.UserId);
        Assert.Equal(meaning, signature.MeaningOfSignature);
        Assert.Equal(nameof(QualityRecord), signature.EntityType);
        Assert.Equal(recordId, signature.EntityId);

        using var snapshot = JsonDocument.Parse(step.SnapshotJson);
        Assert.Equal(record.Version, snapshot.RootElement.GetProperty("Version").GetUInt32());

        var stepAudit = await db.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(QualityRecordStep) && a.EntityId == step.Id.ToString() && a.Action == "Create");
        Assert.Equal(expectedActor, stepAudit.UserId);

        var detailAudit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(ChangeControlDetail) && a.EntityId == recordId.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstAsync(a => a.NewValue != null && a.NewValue.Contains("Status"));
        Assert.Equal(expectedActor, detailAudit.UserId);
        Assert.Contains($"\"Status\":{(int)expectedStatus}", detailAudit.NewValue);
    }

    private async Task<int> CountAuditLogsAsync(MutableClock clock)
    {
        await using var db = Context(clock);
        return await db.AuditLogs.AsNoTracking().CountAsync(a =>
            a.EntityName == nameof(QualityRecord) || a.EntityName == nameof(ChangeControlDetail) ||
            a.EntityName == nameof(QualityRecordStep) ||
            (a.EntityName == nameof(ElectronicSignature) && a.Action != "SignatureFailed"));
    }

    [PostgresFact]
    public async Task Full_path_succeeds_with_triad_at_each_signed_step()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock, withChecklist: true);

        var proposed = await ProposeAsync(clock, w);
        Assert.StartsWith("CC-", proposed.Record.Number);
        Assert.Equal(ChangeControlStatus.Proposed, proposed.Status);
        Assert.Single(proposed.Affected);
        Assert.Equal(QualityLinkTargetType.TestDefinition, proposed.Affected[0].TargetType);

        clock.UtcNow = BaseNow.AddHours(1);
        var saved = await SaveAssessmentAsync(clock, w, proposed,
            [new("val-impact", QualityChecklistAnswerValue.No, "No validation impact")]);
        Assert.Equal(ChangeControlStatus.Proposed, saved.Status);
        Assert.Single(saved.Tasks);

        clock.UtcNow = BaseNow.AddHours(2);
        var assessed = await SignAssessmentAsync(clock, w, saved);
        await AssertStepTriadAsync(clock, assessed.Id, QualityStepKind.ChangeImpactAssessed,
            SignatureMeaning.ChangeImpactAssessed, w.Assessor, ChangeControlStatus.Assessed);
        await using (var snapDb = Context(clock))
        {
            var step = await snapDb.QualityRecordSteps.AsNoTracking()
                .SingleAsync(s => s.RecordId == assessed.Id && s.Kind == QualityStepKind.ChangeImpactAssessed);
            using var snap = JsonDocument.Parse(step.SnapshotJson);
            var affectedSnap = snap.RootElement.GetProperty("Data").GetProperty("Affected");
            Assert.Equal(1, affectedSnap.GetArrayLength());
            Assert.Equal((int)QualityLinkTargetType.TestDefinition, affectedSnap[0].GetProperty("TargetType").GetInt32());
            Assert.Equal(w.TestDefId, affectedSnap[0].GetProperty("TargetId").GetInt32());
        }

        clock.UtcNow = BaseNow.AddHours(3);
        var approved = await ApproveAsync(clock, w, assessed);
        await AssertStepTriadAsync(clock, approved.Id, QualityStepKind.ChangeApproved,
            SignatureMeaning.ChangeApproved, w.Qa, ChangeControlStatus.ApprovedForImplementation);

        clock.UtcNow = BaseNow.AddHours(4);
        var completed = await CompleteTaskAsync(clock, w, approved);
        Assert.NotNull(completed.Tasks[0].DoneAtUtc);

        await using (var markDb = Context(clock, w.Assessor))
        {
            var link = await markDb.QualityRecordLinks.SingleAsync(l => l.RecordId == completed.Id && l.IsAffectedByChange);
            link.ChangedAtUtc = clock.UtcNow;
            await markDb.SaveChangesAsync();
        }

        clock.UtcNow = BaseNow.AddHours(5);
        var closed = await ActAsync(clock, w.OtherQa, s => s.CloseAsync(
            completed.Id, new("Verified test definition update", null, Password), completed.Version, w.OtherQa));
        await AssertStepTriadAsync(clock, closed.Id, QualityStepKind.ChangeClosed,
            SignatureMeaning.QualityClosed, w.OtherQa, ChangeControlStatus.Closed);
    }

    [PostgresFact]
    public async Task Analyst_cannot_propose()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        var ex = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ProposeAsync(clock, w, actor: w.Analyst));
        Assert.Equal(MessageCodes.QualityAccessDenied, ex.Code);
    }

    [PostgresFact]
    public async Task Unanswered_checklist_refuses_assessment_signature()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock, withChecklist: true);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAssessmentAsync(clock, w, saved));
        Assert.Equal(MessageCodes.ChangeControlChecklistIncomplete, ex.Code);
    }

    [PostgresFact]
    public async Task Reject_signs_and_transitions_to_rejected()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var assessed = await SignAssessmentAsync(clock, w, saved);

        clock.UtcNow = BaseNow.AddHours(1);
        var rejected = await ActAsync(clock, w.Qa, s => s.RejectAsync(
            assessed.Id, "Insufficient justification", Password, assessed.Version, w.Qa));
        await AssertStepTriadAsync(clock, rejected.Id, QualityStepKind.ChangeRejected,
            SignatureMeaning.ChangeRejected, w.Qa, ChangeControlStatus.Rejected);
        Assert.Equal("Insufficient justification", rejected.RejectionReason);
    }

    [PostgresFact]
    public async Task Affected_list_frozen_after_approval()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var assessed = await SignAssessmentAsync(clock, w, saved);
        var approved = await ApproveAsync(clock, w, assessed);

        var exService = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Raiser, s =>
            s.AddAffectedAsync(approved.Id, new(QualityLinkTargetType.Specification, w.SpecId, null),
                approved.Version, w.Raiser)));
        Assert.Equal(MessageCodes.ChangeControlAffectedFrozen, exService.Code);

        await using var linkDb = Context(clock, w.Raiser);
        var exLink = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Links(linkDb, clock).AddAsync(approved.Id, QualityLinkTargetType.Specification, w.SpecId,
                w.Raiser, approved.Version, isAffectedByChange: true));
        Assert.Equal(MessageCodes.ChangeControlAffectedFrozen, exLink.Code);
    }

    [PostgresFact]
    public async Task Close_refused_with_open_task()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var assessed = await SignAssessmentAsync(clock, w, saved);
        var approved = await ApproveAsync(clock, w, assessed);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Qa, s =>
            s.CloseAsync(approved.Id, new("Post review", "Not needed", Password), approved.Version, w.Qa)));
        Assert.Equal(MessageCodes.ChangeControlUnfinishedTasks, ex.Code);
    }

    [PostgresFact]
    public async Task Close_refused_with_unchanged_affected_record_without_justification_and_allowed_with_it()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var assessed = await SignAssessmentAsync(clock, w, saved);
        var approved = await ApproveAsync(clock, w, assessed);
        var completed = await CompleteTaskAsync(clock, w, approved);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Qa, s =>
            s.CloseAsync(completed.Id, new("Post review", null, Password), completed.Version, w.Qa)));
        Assert.Equal(MessageCodes.ChangeControlUnchangedJustificationRequired, ex.Code);

        var closed = await ActAsync(clock, w.Qa, s =>
            s.CloseAsync(completed.Id, new("Post review", "Change superseded before edit", Password), completed.Version, w.Qa));
        Assert.Equal(ChangeControlStatus.Closed, closed.Status);
        Assert.Equal("Change superseded before edit", closed.UnchangedJustification);
    }

    [PostgresFact]
    public async Task Raiser_cannot_approve_or_close()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ActAsync(clock, w.Qa, s => s.ProposeAsync(
            new("Update test specification", "Revise acceptance criteria", "Method update", fixture.SeededSectionId,
                [new(QualityLinkTargetType.TestDefinition, w.TestDefId, null)], w.Assessor),
            w.Qa));
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var assessed = await SignAssessmentAsync(clock, w, saved);

        var exApprove = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsync(clock, w, assessed, actor: w.Qa));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, exApprove.Code);

        var approved = await ApproveAsync(clock, w, assessed, actor: w.OtherQa);
        var completed = await CompleteTaskAsync(clock, w, approved);

        var exClose = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Qa, s =>
            s.CloseAsync(completed.Id, new("Post review", "Justified", Password), completed.Version, w.Qa)));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, exClose.Code);
    }

    [PostgresFact]
    public async Task Wrong_password_changes_nothing()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var auditsBefore = await CountAuditLogsAsync(clock);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            SignAssessmentAsync(clock, w, saved, password: "wrong-password"));

        await using var check = Context(clock);
        var detail = await check.ChangeControlDetails.AsNoTracking().SingleAsync(d => d.RecordId == saved.Id);
        Assert.Equal(ChangeControlStatus.Proposed, detail.Status);
        Assert.Empty(await check.QualityRecordSteps.AsNoTracking().Where(s => s.RecordId == saved.Id).ToListAsync());
        Assert.Equal(auditsBefore, await CountAuditLogsAsync(clock));
    }

    [PostgresFact]
    public async Task Stale_version_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        await SaveAssessmentAsync(clock, w, proposed);

        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            SaveAssessmentAsync(clock, w, proposed));
    }

    [PostgresFact]
    public async Task Other_section_user_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SaveAssessmentAsync(clock, w, proposed, actor: w.OutsideHead));
    }

    [PostgresFact]
    public async Task Target_outside_allowed_types_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ProposeAsync(clock, w,
            affected: [new(QualityLinkTargetType.Sample, w.SampleId, null)]));
        Assert.Equal(MessageCodes.ChangeControlInvalidAffectedType, ex.Code);
    }

    [PostgresFact]
    public async Task Affected_list_frozen_at_assessed()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);
        var assessed = await SignAssessmentAsync(clock, w, saved);

        var exService = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Raiser, s =>
            s.AddAffectedAsync(assessed.Id, new(QualityLinkTargetType.Specification, w.SpecId, null),
                assessed.Version, w.Raiser)));
        Assert.Equal(MessageCodes.ChangeControlAffectedFrozen, exService.Code);

        await using var linkDb = Context(clock, w.Raiser);
        var exLink = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Links(linkDb, clock).AddAsync(assessed.Id, QualityLinkTargetType.Specification, w.SpecId,
                w.Raiser, assessed.Version, isAffectedByChange: true));
        Assert.Equal(MessageCodes.ChangeControlAffectedFrozen, exLink.Code);
    }

    [PostgresFact]
    public async Task AddAffected_by_analyst_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);

        var ex = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ActAsync(clock, w.TaskOwner, s =>
            s.AddAffectedAsync(saved.Id, new(QualityLinkTargetType.Specification, w.SpecId, null),
                saved.Version, w.TaskOwner)));
        Assert.Equal(MessageCodes.QualityAccessDenied, ex.Code);
    }

    [PostgresFact]
    public async Task Owner_validation_enforced_and_defaults_to_proposer()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        var defaulted = await ProposeAsync(clock, w);
        Assert.Equal(w.Raiser, defaulted.Record.OwnerUserId);

        var exAnalyst = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Raiser, s =>
            s.ProposeAsync(new("Title", "Desc", "Reason", fixture.SeededSectionId, null, w.Analyst), w.Raiser)));
        Assert.Equal(MessageCodes.ChangeControlInvalidOwner, exAnalyst.Code);

        var exOutside = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Raiser, s =>
            s.ProposeAsync(new("Title", "Desc", "Reason", fixture.SeededSectionId, null, w.OutsideHead), w.Raiser)));
        Assert.Equal(MessageCodes.ChangeControlInvalidOwner, exOutside.Code);
    }

    [PostgresFact]
    public async Task Checklist_answers_refused_after_assessment_signed()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock, withChecklist: true);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed,
            [new("val-impact", QualityChecklistAnswerValue.No, "Initial answer")]);
        var assessed = await SignAssessmentAsync(clock, w, saved);

        await using var cfgDb = Context(clock, w.Assessor);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Config(cfgDb, clock).AnswerAsync(
            assessed.Id, "val-impact", QualityChecklistAnswerValue.Yes, "Late edit", assessed.Version, w.Assessor));
        Assert.Equal(MessageCodes.ChangeControlAssessmentAlreadySigned, ex.Code);
    }

    [PostgresFact]
    public async Task Task_completion_guards_enforced()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await SaveAssessmentAsync(clock, w, proposed);

        // Before approval
        var exBefore = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteTaskAsync(clock, w, saved));
        Assert.Equal(MessageCodes.ChangeControlWrongState, exBefore.Code);

        var assessed = await SignAssessmentAsync(clock, w, saved);
        var approved = await ApproveAsync(clock, w, assessed);

        // Non-responsible user
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => CompleteTaskAsync(clock, w, approved, actor: w.Assessor));

        // Stale version
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() => ActAsync(clock, w.TaskOwner, s =>
            s.CompleteTaskAsync(approved.Tasks[0].Id, new("Done"), proposed.Version, w.TaskOwner)));

        // Valid completion
        var completed = await CompleteTaskAsync(clock, w, approved);

        // Cannot complete twice
        var exTwice = await Assert.ThrowsAsync<BusinessRuleException>(() => CompleteTaskAsync(clock, w, completed));
        Assert.Equal(MessageCodes.ChangeControlTaskAlreadyDone, exTwice.Code);
    }

    [PostgresFact]
    public async Task Cancel_allowed_while_open_with_triad_and_refused_when_closed_or_rejected()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        // Cancel while open
        var open = await ProposeAsync(clock, w);
        clock.UtcNow = BaseNow.AddHours(1);
        var cancelled = await ActAsync(clock, w.Qa, s => s.CancelAsync(
            open.Id, "No longer needed", Password, open.Version, w.Qa));
        await AssertStepTriadAsync(clock, cancelled.Id, QualityStepKind.QualityCancelled,
            SignatureMeaning.QualityCancelled, w.Qa, ChangeControlStatus.Cancelled);

        // Refused when rejected
        var p2 = await ProposeAsync(clock, w);
        var s2 = await SaveAssessmentAsync(clock, w, p2);
        var a2 = await SignAssessmentAsync(clock, w, s2);
        var rejected = await ActAsync(clock, w.Qa, s => s.RejectAsync(a2.Id, "Not justified", Password, a2.Version, w.Qa));
        var exRejected = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Qa, s =>
            s.CancelAsync(rejected.Id, "Cancel", Password, rejected.Version, w.Qa)));
        Assert.Equal(MessageCodes.ChangeControlFinalCancellation, exRejected.Code);

        // Refused when closed
        var p3 = await ProposeAsync(clock, w);
        var s3 = await SaveAssessmentAsync(clock, w, p3);
        var a3 = await SignAssessmentAsync(clock, w, s3);
        var ap3 = await ApproveAsync(clock, w, a3);
        var c3 = await CompleteTaskAsync(clock, w, ap3);
        var closed = await ActAsync(clock, w.Qa, s => s.CloseAsync(
            c3.Id, new("Post review", "Justified", Password), c3.Version, w.Qa));
        var exClosed = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Qa, s =>
            s.CancelAsync(closed.Id, "Cancel", Password, closed.Version, w.Qa)));
        Assert.Equal(MessageCodes.ChangeControlFinalCancellation, exClosed.Code);
    }

    [PostgresFact]
    public async Task Approved_change_control_task_extension_approved_updates_due_date_and_keeps_planned_fields_locked()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var proposed = await ProposeAsync(clock, w);
        var saved = await ActAsync(clock, w.Assessor, s => s.SaveAssessmentAsync(proposed.Id,
            new("Moderate impact on test execution",
                [
                    new("Update test method SOP", w.TaskOwner, Today.AddDays(3)),
                    new("Train analysts", w.TaskOwner, Today.AddDays(4))
                ], null),
            proposed.Version, w.Assessor));
        var assessed = await SignAssessmentAsync(clock, w, saved);
        var approved = await ApproveAsync(clock, w, assessed);
        var extendedTaskId = approved.Tasks[0].Id;
        var untouchedTaskId = approved.Tasks[1].Id;
        var originalDueDate = approved.Tasks[0].DueDate;
        var requestedDueDate = originalDueDate.AddDays(5);

        QualityExtensionDto pending;
        await using (var act = Context(clock, w.TaskOwner))
            pending = await Config(act, clock).RequestAsync(approved.Id, extendedTaskId, requestedDueDate, "Vendor delay", approved.Version, w.TaskOwner);

        QualityExtensionDto decided;
        await using (var act = Context(clock, w.Qa))
            decided = await Config(act, clock).DecideAsync(pending.Id, true, "Approved by QA", Password, pending.Version, w.Qa);

        Assert.Equal(QualityExtensionDecision.Approved, decided.Decision);
        Assert.NotNull(decided.SignatureId);

        await using (var check = Context(clock))
        {
            var task = await check.QualityActions.AsNoTracking().SingleAsync(a => a.Id == extendedTaskId);
            Assert.Equal(requestedDueDate, task.DueDate);
            var req = await check.QualityExtensionRequests.AsNoTracking().SingleAsync(e => e.Id == pending.Id);
            Assert.Equal(QualityExtensionDecision.Approved, req.Decision);
            Assert.Equal(decided.SignatureId, req.SignatureId);
            var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityAction)
                && a.EntityId == extendedTaskId.ToString() && a.UserId == w.Qa && a.Action == "Update");
            Assert.Contains(originalDueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), audit.PreviousValue);
            Assert.Contains(requestedDueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), audit.NewValue);
        }

        await using (var tamperDb = Context(clock))
        {
            var wrongDateEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"DueDate\" = {requestedDueDate.AddDays(2)} WHERE \"Id\" = {extendedTaskId}"));
            Assert.Equal("P0001", wrongDateEx.SqlState);

            var noRequestEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"DueDate\" = {Today.AddDays(15)} WHERE \"Id\" = {untouchedTaskId}"));
            Assert.Equal("P0001", noRequestEx.SqlState);

            var descEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"Description\" = 'Tampered' WHERE \"Id\" = {extendedTaskId}"));
            Assert.Equal("P0001", descEx.SqlState);
        }
    }
}
