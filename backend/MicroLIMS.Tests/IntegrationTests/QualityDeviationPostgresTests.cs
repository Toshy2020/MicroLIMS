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
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public partial class QualityDeviationPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Deviation-test-password";
    private static readonly DateTime Now = new(2086, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider Clock = new FixedClock();
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private MicroLimsDbContext Context(int? actor = null)
    {
        var db = fixture.CreateDbContext(Clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static DeviationService Service(MicroLimsDbContext db)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, Clock),
            new ElectronicSignatureService(db, Clock), Clock);
        return new(db, core, permissions, scope, new SegregationOfDutiesGuard(db), new LabClock(Clock, TimeZoneInfo.Utc));
    }

    private async Task<int> ActorAsync(MicroLimsDbContext db, RoleType role, bool member = true)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Deviation Test", Username = key, IsActive = true,
            Role = new Role { Name = key, Type = role, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var codes = new List<string> { PermissionConstants.QualityView, PermissionConstants.QualityRaise };
        if (role == RoleType.Qa) codes.AddRange([PermissionConstants.QualityApprove, PermissionConstants.OosView, PermissionConstants.MonitoringPlanView, PermissionConstants.AuditView]);
        if (!member && role == RoleType.Qa) codes.Add(PermissionConstants.SectionsViewAll);
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

    private sealed record World(int Raiser, int Owner, int Head, int Qa, int Classification, int Cause, int Outside);

    private async Task<World> ArrangeAsync()
    {
        await using var db = Context();
        var raiser = await ActorAsync(db, RoleType.Analyst);
        var owner = await ActorAsync(db, RoleType.SectionHead);
        var head = await ActorAsync(db, RoleType.SectionHead);
        var qa = await ActorAsync(db, RoleType.Qa);
        var outside = await ActorAsync(db, RoleType.Analyst, member: false);
        var classification = new QualityClassification { Name = Guid.NewGuid().ToString("N"), IsActive = true };
        db.QualityClassifications.Add(classification);
        await db.SaveChangesAsync();
        var cause = await db.OosCauseCategories.AsNoTracking().Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        return new(raiser, owner, head, qa, classification.Id, cause, outside);
    }

    private async Task<DeviationDto> ActAsync(int actor, Func<DeviationService, Task<DeviationDto>> action)
    {
        await using var db = Context(actor);
        return await action(Service(db));
    }

    private Task<DeviationDto> RaiseAsync(World world, int? actor = null) =>
        ActAsync(actor ?? world.Raiser, service => service.RaiseAsync(
            new("Deviation", "What happened", Now, "Laboratory", "Stopped work", fixture.SeededSectionId, []),
            null, actor ?? world.Raiser));

    private Task<DeviationDto> AssessAsync(World w, DeviationDto d, int? owner = null, string password = Password) =>
        ActAsync(w.Qa, s => s.AssessAsync(d.Id, new(true, w.Classification, owner ?? w.Owner, null, password), d.Version, w.Qa));

    private Task<DeviationDto> InvestigateAsync(World w, DeviationDto d) =>
        ActAsync(w.Owner, s => s.SaveInvestigationAsync(d.Id, new("Impact", w.Cause, "Cause", "Actions"), d.Version, w.Owner));

    private Task<DeviationDto> ConcludeAsync(World w, DeviationDto d, int? actor = null) =>
        ActAsync(actor ?? w.Head, s => s.SignLabConclusionAsync(d.Id, Password, d.Version, actor ?? w.Head));

    private Task<DeviationDto> ApproveAsync(World w, DeviationDto d, bool capa = false) =>
        ActAsync(w.Qa, s => s.ApproveAsync(d.Id, new(capa, null, Password), d.Version, w.Qa));

    private Task<DeviationDto> CloseAsync(World w, DeviationDto d) =>
        ActAsync(w.Qa, s => s.CloseAsync(d.Id, Password, d.Version, w.Qa));

    private async Task AssertSignedAsync(DeviationDto d, QualityDeviationStatus before, QualityDeviationStatus after,
        QualityStepKind kind, SignatureMeaning meaning, int userId)
    {
        await using var db = Context();
        var persisted = await db.QualityRecords.AsNoTracking().Include(r => r.DeviationDetail).SingleAsync(r => r.Id == d.Id);
        Assert.Equal(after, persisted.DeviationDetail?.Status);
        Assert.Equal(d.Version, persisted.Version);
        var step = await db.QualityRecordSteps.AsNoTracking().Where(s => s.RecordId == d.Id && s.Kind == kind)
            .OrderByDescending(s => s.Id).FirstAsync();
        Assert.Equal(userId, step.SignedByUserId);
        Assert.Equal(Now, step.SignedAtUtc);
        var signature = await db.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == step.SignatureId);
        Assert.Equal(userId, signature.UserId);
        Assert.Equal(meaning, signature.MeaningOfSignature);
        Assert.Equal(d.Id, signature.EntityId);
        Assert.Equal(nameof(QualityRecord), signature.EntityType);
        using var snapshot = JsonDocument.Parse(step.SnapshotJson);
        Assert.Equal(d.Version, snapshot.RootElement.GetProperty("Version").GetUInt32());
        var detailAudit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(DeviationDetail) && a.EntityId == d.Id.ToString() && a.Action == "Update")
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
    public async Task Full_path_closes_without_CAPA_when_not_required()
    {
        var w = await ArrangeAsync();
        var raised = await RaiseAsync(w);
        var assessed = await AssessAsync(w, raised);
        await AssertSignedAsync(assessed, QualityDeviationStatus.Raised, QualityDeviationStatus.Investigation,
            QualityStepKind.DeviationAssessed, SignatureMeaning.DeviationAssessed, w.Qa);
        await using (var check = Context())
        {
            var record = await check.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == assessed.Id);
            Assert.Equal(w.Owner, record.OwnerUserId);
            Assert.Equal(w.Classification, record.ClassificationId);
            Assert.NotEqual(raised.Version, record.Version);
        }
        var investigated = await InvestigateAsync(w, assessed);
        var concluded = await ConcludeAsync(w, investigated);
        await AssertSignedAsync(concluded, QualityDeviationStatus.Investigation, QualityDeviationStatus.LabConcluded,
            QualityStepKind.DeviationLabConclusion, SignatureMeaning.QualityLabConclusion, w.Head);
        var approved = await ApproveAsync(w, concluded);
        await AssertSignedAsync(approved, QualityDeviationStatus.LabConcluded, QualityDeviationStatus.Approved,
            QualityStepKind.DeviationApproved, SignatureMeaning.QualityApproved, w.Qa);
        var closed = await CloseAsync(w, approved);
        await AssertSignedAsync(closed, QualityDeviationStatus.Approved, QualityDeviationStatus.Closed,
            QualityStepKind.DeviationClosed, SignatureMeaning.QualityClosed, w.Qa);
    }

    [PostgresFact]
    public async Task Signed_step_fields_cannot_be_edited()
    {
        var w = await ArrangeAsync();
        var concluded = await ConcludeAsync(w, await InvestigateAsync(w, await AssessAsync(w, await RaiseAsync(w))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => InvestigateAsync(w, concluded));
        await using var db = Context();
        Assert.Equal("Impact", await db.DeviationDetails.AsNoTracking().Where(x => x.RecordId == concluded.Id)
            .Select(x => x.ImpactAssessment).SingleAsync());
    }

    [PostgresFact]
    public async Task Owner_cannot_sign_lab_conclusion()
    {
        var w = await ArrangeAsync();
        var d = await InvestigateAsync(w, await AssessAsync(w, await RaiseAsync(w)));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ConcludeAsync(w, d, w.Owner));
        Assert.Equal("The owner cannot sign the laboratory conclusion.", error.Message);
        await using var db = Context();
        Assert.False(await db.QualityRecordSteps.AsNoTracking().AnyAsync(s => s.RecordId == d.Id &&
            s.Kind == QualityStepKind.DeviationLabConclusion));
    }

    [PostgresFact]
    public async Task Raiser_cannot_approve_or_close()
    {
        var w = await ArrangeAsync();
        int otherQa;
        await using (var db = Context()) otherQa = await ActorAsync(db, RoleType.Qa);
        var d = await RaiseAsync(w, w.Qa);
        d = await ConcludeAsync(w, await InvestigateAsync(w, await AssessAsync(w with { Qa = otherQa }, d)));
        const string message = "You raised or own this record, so you cannot approve or close it.";
        Assert.Equal(message, (await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsync(w, d))).Message);
        d = await ApproveAsync(w with { Qa = otherQa }, d);
        Assert.Equal(message, (await Assert.ThrowsAsync<BusinessRuleException>(() => CloseAsync(w, d))).Message);
        await using var check = Context();
        Assert.Equal(QualityDeviationStatus.Approved, await check.DeviationDetails.AsNoTracking()
            .Where(x => x.RecordId == d.Id).Select(x => x.Status).SingleAsync());
    }

    [PostgresFact]
    public async Task Return_preserves_the_signed_conclusion_and_requires_a_new_one()
    {
        var w = await ArrangeAsync();
        var d = await ConcludeAsync(w, await InvestigateAsync(w, await AssessAsync(w, await RaiseAsync(w))));
        d = await ActAsync(w.Qa, s => s.ReturnAsync(d.Id, "Recheck scope", d.Version, w.Qa));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsync(w, d));
        d = await ConcludeAsync(w, await InvestigateAsync(w, d));
        await using var db = Context();
        Assert.Equal(2, await db.QualityRecordSteps.AsNoTracking().CountAsync(s => s.RecordId == d.Id &&
            s.Kind == QualityStepKind.DeviationLabConclusion));
        var comment = await db.QualityComments.AsNoTracking().SingleAsync(c => c.RecordId == d.Id);
        Assert.Equal("Recheck scope", comment.Text);
        Assert.Equal(w.Qa, comment.AuthorUserId);
        Assert.Equal(Now, comment.CreatedAtUtc);
        Assert.Equal(QualityDeviationStatus.Approved, (await ApproveAsync(w, d)).Status);
    }

    [PostgresFact]
    public async Task Closure_requires_an_active_approved_CAPA_and_refuses_cancelled_after_approval()
    {
        var w = await ArrangeAsync();
        var d = await ApproveAsync(w,
            await ConcludeAsync(w, await InvestigateAsync(w, await AssessAsync(w, await RaiseAsync(w)))), capa: true);
        const string message = "A CAPA with an approved plan is required before closure.";
        Assert.Equal(message, (await Assert.ThrowsAsync<BusinessRuleException>(() => CloseAsync(w, d))).Message);
        await SeedCapaAsync(d.Id, w.Qa, cancelled: true);
        Assert.Equal(message, (await Assert.ThrowsAsync<BusinessRuleException>(() => CloseAsync(w, d))).Message);
        await SeedCapaAsync(d.Id, w.Qa, cancelled: false);
        var closed = await CloseAsync(w, d);
        await AssertSignedAsync(closed, QualityDeviationStatus.Approved, QualityDeviationStatus.Closed,
            QualityStepKind.DeviationClosed, SignatureMeaning.QualityClosed, w.Qa);
    }

    private async Task SeedCapaAsync(int deviationId, int qa, bool cancelled)
    {
        await using var db = Context(qa);
        var capa = new QualityRecord
        {
            Type = QualityRecordType.Capa, Number = $"CAPA-{Guid.NewGuid():N}"[..20],
            Title = "CAPA", Description = "Test CAPA", RaisedAtUtc = Now, RaisedByUserId = qa,
            CapaDetail = new CapaDetail { SourceRecordId = deviationId, Status = CapaStatus.PlanApproved }
        };
        db.QualityRecords.Add(capa);
        await db.SaveChangesAsync();
        await MicroLIMS.Application.Helpers.UnitOfWork.RunAsync(db, () =>
            new QualityRecordCore(db, new PermissionService(db), new UserSectionScopeService(db),
                new ReferenceNumberGenerator(db, Clock), new ElectronicSignatureService(db, Clock), Clock)
            .SignStepAsync(capa, QualityStepKind.CapaPlanApproved, SignatureMeaning.CapaPlanApproved, new { capa.Id },
                Password, null, qa, null));
        if (cancelled)
        {
            Assert.NotNull(capa.CapaDetail);
            capa.CapaDetail.Status = CapaStatus.Cancelled;
            capa.IsCancelled = true;
            capa.CancelReason = "CAPA superseded";
            await db.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task Reject_and_cancel_are_signed()
    {
        var w = await ArrangeAsync();
        var raised = await RaiseAsync(w);
        var rejected = await ActAsync(w.Qa, s => s.RejectAsync(raised.Id, "Outside scope", Password, raised.Version, w.Qa));
        await AssertSignedAsync(rejected, QualityDeviationStatus.Raised, QualityDeviationStatus.Rejected,
            QualityStepKind.DeviationRejected, SignatureMeaning.DeviationRejected, w.Qa);
        raised = await RaiseAsync(w);
        var cancelled = await ActAsync(w.Qa, s => s.CancelAsync(raised.Id, "Duplicate", Password, raised.Version, w.Qa));
        await AssertSignedAsync(cancelled, QualityDeviationStatus.Raised, QualityDeviationStatus.Cancelled,
            QualityStepKind.QualityCancelled, SignatureMeaning.QualityCancelled, w.Qa);
    }

    [PostgresFact]
    public async Task Assessment_defaults_to_the_lab_working_calendar()
    {
        var w = await ArrangeAsync();
        var today = DateOnly.FromDateTime(Now);
        var holiday = today.AddDays(1);
        int weekdays;
        await using (var arrange = Context())
        {
            weekdays = await arrange.MonitoringSettings.AsNoTracking().Select(s => (int?)s.WorkingWeekdays)
                .FirstOrDefaultAsync() ?? new MonitoringSettings().WorkingWeekdays;
            arrange.QualityDueRules.Add(new QualityDueRule
            {
                Type = QualityRecordType.Deviation, ClassificationId = w.Classification, WorkingDays = 1
            });
            arrange.LabCalendarDates.Add(new LabCalendarDate { Date = holiday, Description = "Test holiday" });
            await arrange.SaveChangesAsync();
        }

        var expected = Enumerable.Range(2, 7).Select(today.AddDays)
            .First(date => (weekdays & (1 << (((int)date.DayOfWeek + 6) % 7))) != 0);
        var d = await AssessAsync(w, await RaiseAsync(w));
        await using var check = Context();
        var persisted = await check.QualityRecords.AsNoTracking().SingleAsync(r => r.Id == d.Id);
        Assert.Equal(expected, persisted.DueDate);
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(QualityRecord)
            && a.EntityId == d.Id.ToString() && a.Action == "Update" && a.NewValue != null && a.NewValue.Contains("DueDate"));
        Assert.Equal(w.Qa, audit.UserId);
        Assert.Contains("\"DueDate\":null", audit.PreviousValue);
        Assert.Contains(expected.ToString("yyyy-MM-dd"), audit.NewValue);
    }

    [PostgresFact]
    public async Task Invalid_owner_wrong_password_non_QA_stale_and_other_section_are_refused()
    {
        var w = await ArrangeAsync();
        var raised = await RaiseAsync(w);
        await Assert.ThrowsAsync<BusinessRuleException>(() => AssessAsync(w, raised, w.Outside));
        await Assert.ThrowsAsync<MicroLIMS.Application.Interfaces.SignatureVerificationException>(() => AssessAsync(w, raised, password: "incorrect"));
        var withoutQa = w with { Qa = w.Head };
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => AssessAsync(withoutQa, raised));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ApproveAsync(withoutQa, raised));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => CloseAsync(withoutQa, raised));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ActAsync(w.Head, s =>
            s.RejectAsync(raised.Id, "Refusal", Password, raised.Version, w.Head)));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ActAsync(w.Head, s =>
            s.ReturnAsync(raised.Id, "Refusal", raised.Version, w.Head)));
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ActAsync(w.Head, s =>
            s.CancelAsync(raised.Id, "Refusal", Password, raised.Version, w.Head)));
        await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() => AssessAsync(w, raised with { Record = raised.Record with { Version = raised.Version + 1 } }));
        int outsideQa;
        await using (var db = Context()) outsideQa = await ActorAsync(db, RoleType.Qa, member: false);
        await Assert.ThrowsAsync<NotFoundException>(() => AssessAsync(w with { Qa = outsideQa }, raised));
        await using var check = Context();
        var record = await check.QualityRecords.AsNoTracking().Include(r => r.DeviationDetail).SingleAsync(r => r.Id == raised.Id);
        Assert.Null(record.OwnerUserId);
        Assert.Null(record.ClassificationId);
        Assert.Equal(raised.Version, record.Version);
        Assert.Equal(QualityDeviationStatus.Raised, record.DeviationDetail?.Status);
        Assert.False(await check.QualityRecordSteps.AsNoTracking().AnyAsync(s => s.RecordId == raised.Id));
        Assert.False(await check.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityId == raised.Id &&
            s.EntityType == nameof(QualityRecord)));
    }
}
