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
public class QualityCapaPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Capa-test-password";
    private static readonly DateTime BaseNow = new(2086, 7, 10, 12, 0, 0, DateTimeKind.Utc);
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

    private static CapaService Service(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        return new(db, core, permissions, scope, new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(db));
    }

    private static DeviationService Deviations(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
        return new(db, core, permissions, scope, new SegregationOfDutiesGuard(db), new LabClock(clock, TimeZoneInfo.Utc),
            new QualityDueDateService(db));
    }

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType role, bool member = true)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "CAPA Test", Username = key, IsActive = true,
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

    private sealed record World(int Raiser, int Owner, int Responsible, int Qa, int OtherQa, int OutsideQa, int OutsideHead,
        int Head, int Classification, int Cause);

    private async Task<World> ArrangeAsync(MutableClock clock)
    {
        await using var db = Context(clock);
        var raiser = await ActorAsync(db, RoleType.SectionHead);
        var owner = await ActorAsync(db, RoleType.SectionHead);
        var responsible = await ActorAsync(db, RoleType.Analyst);
        var qa = await ActorAsync(db, RoleType.Qa);
        var otherQa = await ActorAsync(db, RoleType.Qa);
        var outsideQa = await ActorAsync(db, RoleType.Qa, member: false);
        var outsideHead = await ActorAsync(db, RoleType.SectionHead, member: false);
        var head = await ActorAsync(db, RoleType.SectionHead);
        var classification = new QualityClassification { Name = Guid.NewGuid().ToString("N"), IsActive = true };
        db.QualityClassifications.Add(classification);
        await db.SaveChangesAsync();
        var cause = await db.OosCauseCategories.AsNoTracking().Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        return new(raiser, owner, responsible, qa, otherQa, outsideQa, outsideHead, head, classification.Id, cause);
    }

    private async Task<CapaDto> ActAsync(MutableClock clock, int actor, Func<CapaService, Task<CapaDto>> action)
    {
        await using var db = Context(clock, actor);
        return await action(Service(db, clock));
    }

    private Task<CapaDto> CreateAsync(MutableClock clock, World w, int? actor = null, int? owner = null, int? sourceId = null) =>
        ActAsync(clock, actor ?? w.Raiser, s => s.CreateAsync(
            new("CAPA title", "CAPA description", fixture.SeededSectionId, sourceId, [], owner ?? w.Owner),
            actor ?? w.Raiser));

    private Task<CapaDto> SavePlanAsync(MutableClock clock, World w, CapaDto c, int? actor = null,
        IReadOnlyList<CapaActionInput>? actions = null, DateOnly? plannedDate = null) =>
        ActAsync(clock, actor ?? w.Owner, s => s.SavePlanAsync(c.Id,
            new(actions ?? [new(QualityActionKind.Corrective, "Retrain analysts", w.Responsible, DateOnly.FromDateTime(clock.UtcNow).AddDays(5))],
                "Zero repeat errors", "Review next 10 batches", plannedDate ?? DateOnly.FromDateTime(clock.UtcNow).AddDays(10)),
            c.Version, actor ?? w.Owner));

    private Task<CapaDto> ApprovePlanAsync(MutableClock clock, World w, CapaDto c, int? actor = null, string password = Password) =>
        ActAsync(clock, actor ?? w.Qa, s => s.ApprovePlanAsync(c.Id, password, c.Version, actor ?? w.Qa));

    private Task<CapaDto> CompleteActionAsync(MutableClock clock, World w, CapaDto c, int actionId, int? actor = null) =>
        ActAsync(clock, actor ?? w.Responsible, s => s.CompleteActionAsync(actionId,
            new("Training record TR-101 signed", null), c.Version, actor ?? w.Responsible));

    private Task<CapaDto> VerifyCompletionAsync(MutableClock clock, World w, CapaDto c, int? actor = null, string password = Password) =>
        ActAsync(clock, actor ?? w.Qa, s => s.VerifyCompletionAsync(c.Id, password, c.Version, actor ?? w.Qa));

    private Task<CapaDto> SignEffectivenessAsync(MutableClock clock, World w, CapaDto c, CapaEffectiveness result,
        int? newCapaId = null, int? actor = null, string password = Password) =>
        ActAsync(clock, actor ?? w.Qa, s => s.SignEffectivenessAsync(c.Id,
            new(result, "Effectiveness findings", newCapaId, password), c.Version, actor ?? w.Qa));

    private async Task AssertSignedAsync(MutableClock clock, CapaDto c, CapaStatus before, CapaStatus after,
        QualityStepKind kind, SignatureMeaning meaning, int userId)
    {
        await using var db = Context(clock);
        var persisted = await db.QualityRecords.AsNoTracking().Include(r => r.CapaDetail).SingleAsync(r => r.Id == c.Id);
        Assert.Equal(after, persisted.CapaDetail?.Status);
        Assert.Equal(c.Version, persisted.Version);
        var step = await db.QualityRecordSteps.AsNoTracking().Where(s => s.RecordId == c.Id && s.Kind == kind)
            .OrderByDescending(s => s.Id).FirstAsync();
        Assert.Equal(userId, step.SignedByUserId);
        Assert.Equal(clock.UtcNow, step.SignedAtUtc);
        var signature = await db.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
        Assert.Equal(userId, signature.UserId);
        Assert.Equal(meaning, signature.MeaningOfSignature);
        Assert.Equal(c.Id, signature.EntityId);
        Assert.Equal(nameof(QualityRecord), signature.EntityType);
        using var snapshot = JsonDocument.Parse(step.SnapshotJson);
        Assert.Equal(c.Version, snapshot.RootElement.GetProperty("Version").GetUInt32());
        var detailAudit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(CapaDetail) && a.EntityId == c.Id.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id).FirstAsync(a => a.NewValue != null && a.NewValue.Contains("Status"));
        Assert.Equal(userId, detailAudit.UserId);
        Assert.Contains($"\"Status\":{(int)before}", detailAudit.PreviousValue);
        Assert.Contains($"\"Status\":{(int)after}", detailAudit.NewValue);
        var stepAudit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityRecordStep)
            && a.EntityId == step.Id.ToString() && a.Action == "Create");
        Assert.Equal(userId, stepAudit.UserId);
        Assert.Null(stepAudit.PreviousValue);
        Assert.Contains($"\"SignatureId\":{signature.Id}", stepAudit.NewValue);
    }

    [PostgresFact]
    public async Task Full_path_effective_closes_with_triad_at_each_signed_step()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        Assert.StartsWith("CAPA-2086-", created.Record.Number);
        Assert.Equal(CapaStatus.Plan, created.Status);

        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);

        var approved = await ApprovePlanAsync(clock, w, planned);
        await AssertSignedAsync(clock, approved, CapaStatus.Plan, CapaStatus.PlanApproved,
            QualityStepKind.CapaPlanApproved, SignatureMeaning.CapaPlanApproved, w.Qa);

        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        await using (var check = Context(clock))
        {
            var persistedAction = await check.QualityActions.AsNoTracking().SingleAsync(a => a.Id == action.Id);
            Assert.Equal(w.Responsible, persistedAction.DoneByUserId);
            Assert.Equal(BaseNow, persistedAction.DoneAtUtc);
            Assert.Equal("Training record TR-101 signed", persistedAction.Evidence);
            var actionAudit = await check.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(QualityAction) && a.EntityId == action.Id.ToString() && a.Action == "Update")
                .OrderByDescending(a => a.Id).FirstAsync();
            Assert.Equal(w.Responsible, actionAudit.UserId);
            Assert.Contains($"\"DoneByUserId\":{w.Responsible}", actionAudit.NewValue);
            Assert.Contains("Training record TR-101 signed", actionAudit.NewValue);
        }

        var verified = await VerifyCompletionAsync(clock, w, completed);
        await AssertSignedAsync(clock, verified, CapaStatus.PlanApproved, CapaStatus.CompletionVerified,
            QualityStepKind.CapaCompletionVerified, SignatureMeaning.CapaCompletionVerified, w.Qa);

        clock.UtcNow = BaseNow.AddDays(10);
        var closed = await SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.Effective);
        Assert.Equal(CapaEffectiveness.Effective, closed.EffectivenessResult);
        await AssertSignedAsync(clock, closed, CapaStatus.CompletionVerified, CapaStatus.Closed,
            QualityStepKind.CapaEffectivenessChecked, SignatureMeaning.CapaEffectivenessChecked, w.Qa);

        var finalCancel = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Qa,
            s => s.CancelAsync(closed.Id, "Duplicate", Password, closed.Version, w.Qa)));
        Assert.Equal(MessageCodes.CapaFinalCancellation, finalCancel.Code);
    }

    [PostgresFact]
    public async Task Plan_without_action_or_without_effectiveness_check_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);

        var noActions = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SavePlanAsync(clock, w, created, actions: []));
        Assert.Equal(MessageCodes.CapaPlanEmpty, noActions.Code);

        var noCriteria = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Owner, s =>
            s.SavePlanAsync(created.Id, new(
                [new(QualityActionKind.Corrective, "Do work", w.Responsible, Today.AddDays(3))],
                " ", "Review records", Today.AddDays(10)), created.Version, w.Owner)));
        Assert.Equal(MessageCodes.QualityInvalidText, noCriteria.Code);

        var earlyPlannedDate = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SavePlanAsync(clock, w, created,
                actions: [new(QualityActionKind.Corrective, "Do work", w.Responsible, Today.AddDays(5))],
                plannedDate: Today.AddDays(5)));
        Assert.Equal(MessageCodes.CapaEffectivenessDateOrder, earlyPlannedDate.Code);

        var unapproved = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ApprovePlanAsync(clock, w, created));
        Assert.Equal(MessageCodes.CapaPlanIncomplete, unapproved.Code);
    }

    [PostgresFact]
    public async Task Action_completed_by_someone_else_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var approved = await ApprovePlanAsync(clock, w, planned);
        var actionId = Assert.Single(approved.Actions).Id;

        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            CompleteActionAsync(clock, w, approved, actionId, actor: w.Owner));
    }

    [PostgresFact]
    public async Task Action_edit_after_plan_approval_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var approved = await ApprovePlanAsync(clock, w, planned);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SavePlanAsync(clock, w, approved));
        Assert.Equal(MessageCodes.CapaWrongState, ex.Code);
    }

    [PostgresFact]
    public async Task Verify_with_open_action_refused_naming_it()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created, actions:
        [
            new(QualityActionKind.Corrective, "Calibrate balance B-01", w.Responsible, Today.AddDays(5))
        ]);
        var approved = await ApprovePlanAsync(clock, w, planned);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            VerifyCompletionAsync(clock, w, approved));
        Assert.Equal(MessageCodes.CapaUnfinishedActions, ex.Code);
        Assert.Contains("Calibrate balance B-01", ex.Message);
    }

    [PostgresFact]
    public async Task Effectiveness_refused_before_planned_date()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);
        var approved = await ApprovePlanAsync(clock, w, planned);
        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        var verified = await VerifyCompletionAsync(clock, w, completed);

        clock.UtcNow = BaseNow.AddDays(9);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.Effective));
        Assert.Equal(MessageCodes.CapaEffectivenessEarly, ex.Code);
    }

    [PostgresFact]
    public async Task Not_effective_requires_linked_new_capa()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);
        var approved = await ApprovePlanAsync(clock, w, planned);
        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        var verified = await VerifyCompletionAsync(clock, w, completed);

        clock.UtcNow = BaseNow.AddDays(10);
        var missingNew = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.NotEffective, newCapaId: null));
        Assert.Equal(MessageCodes.CapaNewCapaRequired, missingNew.Code);

        var unrelatedCapa = await CreateAsync(clock, w, sourceId: null);
        var wrongSource = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.NotEffective, newCapaId: unrelatedCapa.Id));
        Assert.Equal(MessageCodes.CapaNewCapaRequired, wrongSource.Code);

        var linkedSuccessor = await CreateAsync(clock, w, sourceId: verified.Id);
        var cancelledSuccessor = await CreateAsync(clock, w, sourceId: verified.Id);
        await ActAsync(clock, w.Qa, s => s.CancelAsync(cancelledSuccessor.Id, "Not needed", Password, cancelledSuccessor.Version, w.Qa));
        var cancelledEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.NotEffective, newCapaId: cancelledSuccessor.Id));
        Assert.Equal(MessageCodes.CapaNewCapaRequired, cancelledEx.Code);

        var closed = await SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.NotEffective, newCapaId: linkedSuccessor.Id);
        Assert.Equal(CapaStatus.Closed, closed.Status);
        Assert.Equal(CapaEffectiveness.NotEffective, closed.EffectivenessResult);
        Assert.Equal(linkedSuccessor.Id, closed.SuccessorCapaId);
        await AssertSignedAsync(clock, closed, CapaStatus.CompletionVerified, CapaStatus.Closed,
            QualityStepKind.CapaEffectivenessChecked, SignatureMeaning.CapaEffectivenessChecked, w.Qa);
    }

    [PostgresFact]
    public async Task Raiser_and_owner_cannot_approve_verify_or_sign_effectiveness()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        await using (var db = Context(clock))
        {
            var qaRaiser = await ActorAsync(db, RoleType.Qa);
            var createdByQa = await CreateAsync(clock, w, actor: qaRaiser);
            var planned = await SavePlanAsync(clock, w, createdByQa);
            var action = Assert.Single(planned.Actions);

            var raiserApprove = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                ApprovePlanAsync(clock, w, planned, actor: qaRaiser));
            Assert.Equal(MessageCodes.QualityRaiserOrOwner, raiserApprove.Code);

            var approved = await ApprovePlanAsync(clock, w, planned, actor: w.Qa);
            var completed = await CompleteActionAsync(clock, w, approved, action.Id);

            var raiserVerify = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                VerifyCompletionAsync(clock, w, completed, actor: qaRaiser));
            Assert.Equal(MessageCodes.QualityRaiserOrOwner, raiserVerify.Code);

            var verified = await VerifyCompletionAsync(clock, w, completed, actor: w.Qa);
            clock.UtcNow = BaseNow.AddDays(10);

            var raiserEff = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.Effective, actor: qaRaiser));
            Assert.Equal(MessageCodes.QualityRaiserOrOwner, raiserEff.Code);
        }

        // Also test owner cannot approve, verify, or sign effectiveness even if given Quality.Approve
        var c2 = await CreateAsync(clock, w);
        var p2 = await SavePlanAsync(clock, w, c2);
        var a2 = Assert.Single(p2.Actions);
        await using (var db = Context(clock))
        {
            var permId = await db.Permissions.AsNoTracking()
                .Where(p => p.Code == PermissionConstants.QualityApprove).Select(p => p.Id).SingleAsync();
            var ownerRoleId = await db.Users.AsNoTracking().Where(u => u.Id == w.Owner).Select(u => u.RoleId).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = ownerRoleId, PermissionId = permId });
            await db.SaveChangesAsync();
        }
        var ownerApprove = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ApprovePlanAsync(clock, w, p2, actor: w.Owner));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, ownerApprove.Code);

        var app2 = await ApprovePlanAsync(clock, w, p2, actor: w.Qa);
        var comp2 = await CompleteActionAsync(clock, w, app2, a2.Id);
        var ownerVerify = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            VerifyCompletionAsync(clock, w, comp2, actor: w.Owner));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, ownerVerify.Code);

        var ver2 = await VerifyCompletionAsync(clock, w, comp2, actor: w.Qa);
        clock.UtcNow = BaseNow.AddDays(10);
        var ownerEff = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignEffectivenessAsync(clock, w, ver2, CapaEffectiveness.Effective, actor: w.Owner));
        Assert.Equal(MessageCodes.QualityRaiserOrOwner, ownerEff.Code);
    }

    [PostgresFact]
    public async Task Deviation_closure_allowed_once_capa_plan_approved()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        async Task<DeviationDto> DevAct(int actor, Func<DeviationService, Task<DeviationDto>> fn)
        {
            await using var db = Context(clock, actor);
            return await fn(Deviations(db, clock));
        }

        var dev = await DevAct(w.Raiser, s => s.RaiseAsync(
            new("Deviation", "What happened", BaseNow, "Lab", "Stopped", fixture.SeededSectionId, []), null, w.Raiser));
        dev = await DevAct(w.Qa, s => s.AssessAsync(dev.Id, new(true, w.Classification, w.Owner, null, Password), dev.Version, w.Qa));
        dev = await DevAct(w.Owner, s => s.SaveInvestigationAsync(dev.Id, new("Impact", w.Cause, "Cause", "Actions"), dev.Version, w.Owner));
        dev = await DevAct(w.Head, s => s.SignLabConclusionAsync(dev.Id, Password, dev.Version, w.Head));
        dev = await DevAct(w.Qa, s => s.ApproveAsync(dev.Id, new(true, null, Password), dev.Version, w.Qa));

        var capa = await CreateAsync(clock, w, sourceId: dev.Id);
        var planned = await SavePlanAsync(clock, w, capa);

        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DevAct(w.Qa, s => s.CloseAsync(dev.Id, Password, dev.Version, w.Qa)));
        Assert.Equal(MessageCodes.DeviationCapaRequired, blocked.Code);

        await ApprovePlanAsync(clock, w, planned);
        var closedDev = await DevAct(w.Qa, s => s.CloseAsync(dev.Id, Password, dev.Version, w.Qa));
        Assert.Equal(QualityDeviationStatus.Closed, closedDev.Status);
    }

    [PostgresFact]
    public async Task Wrong_password_changes_nothing()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);

        var auditCountBeforeApprove = await CountAuditLogsAsync(clock);
        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            ApprovePlanAsync(clock, w, planned, password: "wrong"));
        await AssertNoChangeAsync(clock, planned.Id, CapaStatus.Plan, planned.Version, 0, auditCountBeforeApprove);

        var approved = await ApprovePlanAsync(clock, w, planned);
        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        var auditCountBeforeVerify = await CountAuditLogsAsync(clock);
        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            VerifyCompletionAsync(clock, w, completed, password: "wrong"));
        await AssertNoChangeAsync(clock, completed.Id, CapaStatus.PlanApproved, completed.Version, 1, auditCountBeforeVerify);

        var verified = await VerifyCompletionAsync(clock, w, completed);
        clock.UtcNow = BaseNow.AddDays(10);
        var auditCountBeforeEff = await CountAuditLogsAsync(clock);
        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.Effective, password: "wrong"));
        await AssertNoChangeAsync(clock, verified.Id, CapaStatus.CompletionVerified, verified.Version, 2, auditCountBeforeEff);
    }

    private async Task<int> CountAuditLogsAsync(MutableClock clock)
    {
        await using var db = Context(clock);
        return await db.AuditLogs.AsNoTracking().CountAsync(a =>
            a.EntityName == nameof(QualityRecord) || a.EntityName == nameof(CapaDetail) ||
            a.EntityName == nameof(QualityRecordStep) ||
            (a.EntityName == nameof(ElectronicSignature) && a.Action != "SignatureFailed"));
    }

    private async Task AssertNoChangeAsync(MutableClock clock, int capaId, CapaStatus expectedStatus, uint expectedVersion,
        int expectedStepCount, int expectedAuditCount)
    {
        await using var check = Context(clock);
        var persisted = await check.QualityRecords.AsNoTracking().Include(r => r.CapaDetail)
            .SingleAsync(r => r.Id == capaId);
        Assert.Equal(expectedStatus, persisted.CapaDetail?.Status);
        Assert.Equal(expectedVersion, persisted.Version);
        Assert.Equal(expectedStepCount, await check.QualityRecordSteps.AsNoTracking().CountAsync(s => s.RecordId == capaId));
        Assert.Equal(expectedStepCount, await check.ElectronicSignatures.AsNoTracking()
            .CountAsync(s => s.EntityType == nameof(QualityRecord) && s.EntityId == capaId));
        Assert.Equal(expectedAuditCount, await check.AuditLogs.AsNoTracking().CountAsync(a =>
            a.EntityName == nameof(QualityRecord) || a.EntityName == nameof(CapaDetail) ||
            a.EntityName == nameof(QualityRecordStep) ||
            (a.EntityName == nameof(ElectronicSignature) && a.Action != "SignatureFailed")));
    }

    [PostgresFact]
    public async Task Stale_version_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);

        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            ApprovePlanAsync(clock, w, created));

        var approved = await ApprovePlanAsync(clock, w, planned);
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            CompleteActionAsync(clock, w, planned, action.Id));

        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            VerifyCompletionAsync(clock, w, approved));

        var verified = await VerifyCompletionAsync(clock, w, completed);
        clock.UtcNow = BaseNow.AddDays(10);
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
            SignEffectivenessAsync(clock, w, completed, CapaEffectiveness.Effective));
    }

    [PostgresFact]
    public async Task Other_section_user_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);

        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            CreateAsync(clock, w, actor: w.OutsideHead));

        var created = await CreateAsync(clock, w);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SavePlanAsync(clock, w, created, actor: w.OutsideHead));

        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ApprovePlanAsync(clock, w, planned, actor: w.OutsideQa));

        var approved = await ApprovePlanAsync(clock, w, planned);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CompleteActionAsync(clock, w, approved, action.Id, actor: w.OutsideHead));

        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            VerifyCompletionAsync(clock, w, completed, actor: w.OutsideQa));

        var verified = await VerifyCompletionAsync(clock, w, completed);
        clock.UtcNow = BaseNow.AddDays(10);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SignEffectivenessAsync(clock, w, verified, CapaEffectiveness.Effective, actor: w.OutsideQa));
    }

    [PostgresFact]
    public async Task Undefined_effectiveness_result_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var action = Assert.Single(planned.Actions);
        var approved = await ApprovePlanAsync(clock, w, planned);
        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        var verified = await VerifyCompletionAsync(clock, w, completed);

        clock.UtcNow = BaseNow.AddDays(10);
        var auditCount = await CountAuditLogsAsync(clock);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SignEffectivenessAsync(clock, w, verified, (CapaEffectiveness)99));
        Assert.Equal(MessageCodes.CapaEffectivenessResultInvalid, ex.Code);
        await AssertNoChangeAsync(clock, verified.Id, CapaStatus.CompletionVerified, verified.Version, 2, auditCount);
    }

    [PostgresFact]
    public async Task SavePlan_by_non_owner_and_shrinking_refused()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);

        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            SavePlanAsync(clock, w, created, actor: w.Head));

        var twoActions = await SavePlanAsync(clock, w, created, actions:
        [
            new(QualityActionKind.Corrective, "First action", w.Responsible, Today.AddDays(3)),
            new(QualityActionKind.Preventive, "Second action", w.Responsible, Today.AddDays(5))
        ]);
        Assert.Equal(2, twoActions.Actions.Count);

        var shrink = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SavePlanAsync(clock, w, twoActions, actions:
            [
                new(QualityActionKind.Corrective, "Only one action", w.Responsible, Today.AddDays(3))
            ]));
        Assert.Equal(MessageCodes.CapaActionCannotBeRemoved, shrink.Code);
    }

    [PostgresFact]
    public async Task Return_resave_approve_persists_comment_and_reason()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);

        var returned = await ActAsync(clock, w.Qa, s => s.ReturnPlanAsync(planned.Id, "Add preventive action", planned.Version, w.Qa));
        Assert.Equal(CapaStatus.Plan, returned.Status);
        await using (var check = Context(clock))
        {
            var comment = await check.QualityComments.AsNoTracking().SingleAsync(c => c.RecordId == returned.Id);
            Assert.Equal("Add preventive action", comment.Text);
            Assert.Equal(w.Qa, comment.AuthorUserId);
        }

        var reSaved = await SavePlanAsync(clock, w, returned, actions:
        [
            new(QualityActionKind.Corrective, "Retrain analysts", w.Responsible, Today.AddDays(4)),
            new(QualityActionKind.Preventive, "Update SOP", w.Responsible, Today.AddDays(6))
        ]);
        var approved = await ApprovePlanAsync(clock, w, reSaved);
        await AssertSignedAsync(clock, approved, CapaStatus.Plan, CapaStatus.PlanApproved,
            QualityStepKind.CapaPlanApproved, SignatureMeaning.CapaPlanApproved, w.Qa);
    }

    [PostgresFact]
    public async Task CompleteAction_validates_evidence_link_and_single_completion()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var c1 = await CreateAsync(clock, w);
        var p1 = await SavePlanAsync(clock, w, c1);
        var action = Assert.Single(p1.Actions);
        var approved = await ApprovePlanAsync(clock, w, p1);

        var otherCapa = await CreateAsync(clock, w);
        int foreignLinkId;
        await using (var arrange = Context(clock))
        {
            var link = new QualityRecordLink
            {
                RecordId = otherCapa.Id, TargetType = QualityLinkTargetType.Item, TargetId = 1,
                AddedByUserId = w.Raiser, AddedAtUtc = BaseNow
            };
            arrange.QualityRecordLinks.Add(link);
            await arrange.SaveChangesAsync();
            foreignLinkId = link.Id;
        }

        var emptyEvidence = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Responsible,
            s => s.CompleteActionAsync(action.Id, new("  ", null), approved.Version, w.Responsible)));
        Assert.Equal(MessageCodes.QualityInvalidText, emptyEvidence.Code);

        var wrongLink = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(clock, w.Responsible,
            s => s.CompleteActionAsync(action.Id, new("Done", foreignLinkId), approved.Version, w.Responsible)));
        Assert.Equal(MessageCodes.CapaActionEvidenceLinkInvalid, wrongLink.Code);

        var completed = await CompleteActionAsync(clock, w, approved, action.Id);
        var repeat = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CompleteActionAsync(clock, w, completed, action.Id));
        Assert.Equal(MessageCodes.CapaActionAlreadyDone, repeat.Code);
    }

    [PostgresFact]
    public async Task Cancel_path_records_full_triad()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var approved = await ApprovePlanAsync(clock, w, planned);

        var cancelled = await ActAsync(clock, w.Qa, s => s.CancelAsync(approved.Id, "Superseded by site SOP", Password, approved.Version, w.Qa));
        Assert.Equal(CapaStatus.Cancelled, cancelled.Status);
        Assert.Equal("Superseded by site SOP", cancelled.CancelReason);
        await AssertSignedAsync(clock, cancelled, CapaStatus.PlanApproved, CapaStatus.Cancelled,
            QualityStepKind.QualityCancelled, SignatureMeaning.QualityCancelled, w.Qa);

        await using var check = Context(clock);
        var persisted = await check.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == cancelled.Id);
        Assert.True(persisted.IsCancelled);
        Assert.Equal("Superseded by site SOP", persisted.CancelReason);
    }

    [PostgresFact]
    public async Task Approved_capa_action_database_trigger_enforces_immutability()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeAsync(clock);
        var created = await CreateAsync(clock, w);
        var planned = await SavePlanAsync(clock, w, created);
        var actionId = Assert.Single(planned.Actions).Id;

        // Plan edits before approval are allowed (both via service and raw SQL)
        var rePlanned = await SavePlanAsync(clock, w, planned, actions:
        [
            new(QualityActionKind.Preventive, "Updated before approval", w.Responsible, Today.AddDays(6))
        ]);
        await using (var beforeDb = Context(clock))
        {
            var updated = await beforeDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"Description\" = 'Edited in Plan' WHERE \"Id\" = {actionId}");
            Assert.Equal(1, updated);
        }

        var approved = await ApprovePlanAsync(clock, w, rePlanned);
        var otherCapa = await CreateAsync(clock, w);

        // Planned fields cannot be modified after approval
        await using (var afterDb = Context(clock))
        {
            foreach (var sql in new FormattableString[]
            {
                $"UPDATE \"QualityActions\" SET \"Kind\" = 0 WHERE \"Id\" = {actionId}",
                $"UPDATE \"QualityActions\" SET \"Description\" = 'Tampered' WHERE \"Id\" = {actionId}",
                $"UPDATE \"QualityActions\" SET \"ResponsibleUserId\" = {w.Owner} WHERE \"Id\" = {actionId}",
                $"UPDATE \"QualityActions\" SET \"DueDate\" = {Today.AddDays(30)} WHERE \"Id\" = {actionId}",
                $"UPDATE \"QualityActions\" SET \"RecordId\" = {otherCapa.Id} WHERE \"Id\" = {actionId}"
            })
            {
                var ex = await Assert.ThrowsAsync<PostgresException>(() => afterDb.Database.ExecuteSqlInterpolatedAsync(sql));
                Assert.Equal("P0001", ex.SqlState);
                Assert.Contains("planned fields cannot be modified", ex.Message);
            }
        }

        // First completion is allowed
        var completed = await CompleteActionAsync(clock, w, approved, actionId);
        Assert.NotNull(Assert.Single(completed.Actions).DoneAtUtc);

        // Second completion / clearing is rejected
        await using (var tamperDb = Context(clock))
        {
            var modifyEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"Evidence\" = 'Tampered evidence' WHERE \"Id\" = {actionId}"));
            Assert.Equal("P0001", modifyEx.SqlState);
            Assert.Contains("completion fields cannot be modified or cleared", modifyEx.Message);

            var clearEx = await Assert.ThrowsAsync<PostgresException>(() => tamperDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"QualityActions\" SET \"DoneAtUtc\" = NULL WHERE \"Id\" = {actionId}"));
            Assert.Equal("P0001", clearEx.SqlState);
            Assert.Contains("completion fields cannot be modified or cleared", clearEx.Message);
        }
    }
}
