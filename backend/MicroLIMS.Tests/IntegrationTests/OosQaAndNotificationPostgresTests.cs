using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public sealed class OosQaAndNotificationPostgresTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IDisposable
{
    private readonly ActionGuardScenarios _seed = new(fixture);
    private static readonly TimeProvider Clock = new FixedClock();
    private readonly ServiceProvider _notificationServices = new ServiceCollection()
        .AddScoped<IMicroLimsDbContext>(_ => fixture.CreateDbContext())
        .AddSingleton(Clock)
        .AddScoped<SegregationOfDutiesGuard>()
        .AddScoped<OosRecipientResolver>()
        .AddScoped<INotificationPublisher, NotificationPublisher>()
        .BuildServiceProvider();

    public void Dispose() => _notificationServices.Dispose();
    private static readonly DateOnly Today = DateOnly.FromDateTime(ActionGuardScenarios.Now);
    private const string Reason = "  Review the supporting records.  ";
    private const string Comment = "The request was reviewed.";
    private const string Summary = "The original result was confirmed.";
    private static readonly string[] Manage = [PermissionConstants.OosManage];
    private static readonly string[] Request = [PermissionConstants.OosRequestDeviation];

    [PostgresFact]
    public async Task Resolver_uses_memberships_and_view_all_and_excludes_inactive_and_locked_users()
    {
        var section = await _seed.SectionAsync();
        var explicitMember = await _seed.UserAsync(RoleType.SectionHead, Manage, section);
        var departmentMember = await _seed.UserAsync(RoleType.SectionHead, Manage);
        var global = await _seed.OutsiderAsync(RoleType.SectionHead, Manage);
        var outsider = await _seed.UserAsync(RoleType.SectionHead, Manage, await _seed.SectionAsync());
        var wrongPermission = await _seed.UserAsync(RoleType.Qa, Request, section);
        var inactive = await _seed.UserAsync(RoleType.SectionHead, Manage, section);
        var locked = await _seed.UserAsync(RoleType.SectionHead, Manage, section);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.UserOrgMemberships.Add(new() { UserId = departmentMember.Id, DepartmentId = section.DepartmentId });
            (await arrange.Users.SingleAsync(u => u.Id == inactive.Id)).IsActive = false;
            (await arrange.Users.SingleAsync(u => u.Id == locked.Id)).LockedUntil = ActionGuardScenarios.Now.AddMinutes(1);
            await arrange.SaveChangesAsync();
        }

        IReadOnlyList<int> ids;
        await using (var act = fixture.CreateDbContext())
            ids = await new OosRecipientResolver(act, Clock).ResolveAsync(section.Id, PermissionConstants.OosManage);

        Assert.All(new[] { explicitMember.Id, departmentMember.Id, global.Id }, id => Assert.Contains(id, ids));
        Assert.All(new[] { outsider.Id, wrongPermission.Id, inactive.Id, locked.Id }, id => Assert.DoesNotContain(id, ids));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [PostgresFact]
    public async Task Review_phase_two_and_conclusion_notify_performers_management_and_qa()
    {
        var section = await _seed.SectionAsync();
        var reviewer = await _seed.ReviewerAsync(section);
        var analyst = await _seed.AnalystAsync(section);
        var unrelatedAnalyst = await _seed.AnalystAsync(section);
        var head = await _seed.UserAsync(RoleType.SectionHead, Manage, section);
        var qa = await _seed.OutsiderAsync(RoleType.Qa, Request);
        var origin = await _seed.UnderReviewAsync(section);
        var category = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}", IsActive = true };
        await using (var arrange = fixture.CreateDbContext())
        {
            var template = new OosInvestigationTemplate { SectionId = section.Id, Kind = OosInvestigationKind.Oos };
            arrange.AddRange(category, template);
            arrange.Results.Add(new()
            {
                EnteredByUserId = analyst.Id, TestOrderId = origin.Orders[0].Id,
                InterpretedValue = nameof(ResultStatus.OutOfSpecification)
            });
            await arrange.SaveChangesAsync();
            arrange.OosChecklistItems.Add(new()
            {
                TemplateId = template.Id, Phase = 1, Key = "records", Text = "Were records available?", IsActive = true
            });
            await arrange.SaveChangesAsync();
        }

        await using (var review = fixture.CreateDbContext())
        {
            var controller = new SampleReviewController(TestServiceFactory.SampleReview(review, Clock),
                new UserSectionScopeService(review), Notifier(), NullLogger<SampleReviewController>.Instance)
            {
                ControllerContext = Context(reviewer.Id)
            };
            Assert.IsType<OkObjectResult>(await controller.Complete(origin.Sample.Id,
                new(ActionGuardScenarios.Password, null, section.Id)));
        }

        OosInvestigation investigation;
        await using (var verify = fixture.CreateDbContext())
        {
            investigation = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == origin.Sample.Id);
            Assert.Equal(OosInvestigationStatus.PhaseOne, investigation.Status);
            Assert.Equal(reviewer.Id, investigation.OpenedByUserId);
            Assert.Equal(ActionGuardScenarios.Now, investigation.OpenedAtUtc);
            Assert.NotEqual(0u, investigation.Version);
            var audit = await AuditAsync(verify, nameof(OosInvestigation), investigation.Id, reviewer.Id, "Create");
            Assert.Null(audit.PreviousValue);
            AssertStatus(audit.NewValue, (int)OosInvestigationStatus.PhaseOne);
            var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.InvestigationId == investigation.Id);
            Assert.Equal(origin.Orders[0].Id, link.TestOrderId);
            Assert.Equal(OosOutcomeClass.Fails, link.OutcomeAtOpening);
            var linkAudit = await AuditAsync(verify, nameof(OosInvestigationResult), link.Id, reviewer.Id, "Create");
            Assert.Null(linkAudit.PreviousValue);
            Assert.Contains(nameof(link.TestOrderId), linkAudit.NewValue);
        }

        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.OosInvestigationEntrys.AddRange(
                new OosInvestigationEntry { InvestigationId = investigation.Id, Phase = 1, Kind = OosEntryKind.AnalystStatement,
                    Text = "Records reviewed.", RecordedByUserId = analyst.Id, RecordedAtUtc = ActionGuardScenarios.Now },
                new OosInvestigationEntry { InvestigationId = investigation.Id, Phase = 1, Kind = OosEntryKind.ChecklistAnswer,
                    ChecklistItemKey = "records", Answer = OosChecklistAnswer.Yes, RecordedByUserId = analyst.Id,
                    RecordedAtUtc = ActionGuardScenarios.Now });
            await arrange.SaveChangesAsync();
        }

        await using (var sign = fixture.CreateDbContext())
            Assert.IsType<NoContentResult>(await Controller(sign, head.Id, Notifier(), investigation.Version)
                .SignPhaseOne(investigation.Id, new(OosPhaseOneAssessment.NoLaboratoryErrorFound, null, null, ActionGuardScenarios.Password)));

        uint phaseTwoVersion;
        await using (var verify = fixture.CreateDbContext())
        {
            var signed = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == investigation.Id);
            phaseTwoVersion = signed.Version;
            Assert.NotEqual(investigation.Version, signed.Version);
            Assert.Equal(OosInvestigationStatus.PhaseTwo, signed.Status);
            Assert.Equal(OosPhaseOneAssessment.NoLaboratoryErrorFound, signed.PhaseOneAssessment);
            Assert.Equal(ActionGuardScenarios.Now, signed.PhaseOneSignedAtUtc);
            Assert.Equal(head.Id, signed.PhaseOneSignedByUserId);
            Assert.NotNull(signed.PhaseOneSignatureId);
            var audit = await AuditAsync(verify, nameof(OosInvestigation), investigation.Id, head.Id, "Update");
            AssertStatus(audit.PreviousValue, (int)OosInvestigationStatus.PhaseOne);
            AssertStatus(audit.NewValue, (int)OosInvestigationStatus.PhaseTwo);
        }

        await using (var conclude = fixture.CreateDbContext())
            Assert.IsType<NoContentResult>(await Controller(conclude, head.Id, Notifier(), phaseTwoVersion)
                .Conclude(investigation.Id, new(OosConclusion.Confirmed, category.Id, Comment, Summary, ActionGuardScenarios.Password)));

        await using var final = fixture.CreateDbContext();
        var closed = await final.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == investigation.Id);
        Assert.Equal(OosInvestigationStatus.Concluded, closed.Status);
        Assert.Equal(OosConclusion.Confirmed, closed.Conclusion);
        Assert.Equal(head.Id, closed.ConcludedByUserId);
        Assert.Equal(ActionGuardScenarios.Now, closed.ConcludedAtUtc);
        Assert.NotEqual(phaseTwoVersion, closed.Version);
        var conclusionAudit = await final.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == investigation.Id.ToString() && a.UserId == head.Id && a.Action == "Update").OrderByDescending(a => a.Id).FirstAsync();
        AssertStatus(conclusionAudit.PreviousValue, (int)OosInvestigationStatus.PhaseTwo);
        AssertStatus(conclusionAudit.NewValue, (int)OosInvestigationStatus.Concluded);

        foreach (var name in new[] { "opened", "phase-two-started", "concluded" })
        {
            var users = await final.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == $"oos:{investigation.Id}:{name}" && n.SectionId == section.Id)
                .Select(n => n.UserId).ToListAsync();
            Assert.All(new[] { analyst.Id, head.Id, qa.Id }, id => Assert.Contains(id, users));
            Assert.DoesNotContain(unrelatedAnalyst.Id, users);
            Assert.DoesNotContain(reviewer.Id, users);
        }
    }

    [PostgresFact]
    public async Task Request_refuses_non_qa_and_out_of_scope_qa()
    {
        var world = await ArrangeAsync();
        var outsider = await _seed.UserAsync(RoleType.Qa, Request, await _seed.SectionAsync());
        foreach (var (actor, code) in new[] { (world.Head, "Oos.InvestigationAccessDenied"), (outsider.Id, "Oos.SectionAccessDenied") })
        {
            await using var act = fixture.CreateDbContext();
            var error = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Service(act).RequestDeviationAsync(world.Id, Reason, actor));
            Assert.Equal(code, error.Code);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosDeviationRequests.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id));
    }

    [PostgresFact]
    public async Task Request_refuses_a_stale_investigation_version_and_a_blank_reason()
    {
        var world = await ArrangeAsync();
        await using (var act = fixture.CreateDbContext())
        {
            var controller = Controller(act, world.Qa, Notifier(), world.Version - 1);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.RequestDeviation(world.Id, new(Reason)));
            Assert.Equal("Oos.VersionConflict", error.Code);
        }
        await using (var act = fixture.CreateDbContext())
        {
            var controller = Controller(act, world.Qa, Notifier(), world.Version);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.RequestDeviation(world.Id, new(" ")));
            Assert.Equal("Oos.DeviationReasonInvalid", error.Code);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosDeviationRequests.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id));
        Assert.False(await verify.NotificationLogs.AsNoTracking().AnyAsync(n => n.DedupeKey == $"oos:{world.Id}:deviation-requested"));
    }

    [PostgresFact]
    public async Task Request_persists_and_notifies_section_managers_once()
    {
        var world = await ArrangeAsync();
        var second = await _seed.UserAsync(RoleType.SectionHead, Manage, world.Section);
        var outsider = await _seed.UserAsync(RoleType.SectionHead, Manage, await _seed.SectionAsync());
        await using (var act = fixture.CreateDbContext())
        {
            var notifier = Notifier();
            Assert.IsType<OkObjectResult>(await Controller(act, world.Qa, notifier, world.Version)
                .RequestDeviation(world.Id, new(Reason)));
            await notifier.DeviationRequestedAsync(world.Id);
        }

        await AssertOpenRequestAsync(world);
        await using var verify = fixture.CreateDbContext();
        var users = await verify.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == $"oos:{world.Id}:deviation-requested" && n.SectionId == world.Section.Id && n.TargetId == world.Id)
            .OrderBy(n => n.UserId).Select(n => n.UserId).ToListAsync();
        Assert.Equal(new[] { world.Head, second.Id }.Order(), users);
        Assert.DoesNotContain(outsider.Id, users);
    }

    [PostgresFact]
    public async Task Notification_failure_returns_success_and_preserves_committed_request()
    {
        var world = await ArrangeAsync();
        var failing = new ThrowingNotifier(fixture);
        await using (var act = fixture.CreateDbContext())
            Assert.IsType<OkObjectResult>(await Controller(act, world.Qa, failing, world.Version).RequestDeviation(world.Id, new(Reason)));
        Assert.Equal(world.Id, failing.InvestigationId);
        Assert.True(failing.SawCommittedRequest);
        await AssertOpenRequestAsync(world);
    }

    [PostgresFact]
    public async Task Address_refuses_stale_version_and_other_section_then_persists_and_audits()
    {
        var world = await ArrangeAsync();
        await using (var act = fixture.CreateDbContext())
            await Service(act).RequestDeviationAsync(world.Id, Reason, world.Qa);
        var before = await AssertOpenRequestAsync(world);
        var outsider = await _seed.OutsiderAsync(RoleType.SectionHead, Manage);
        await using (var act = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Service(act).AddressDeviationRequestAsync(before.Id, Comment, before.Version, outsider.Id));
            Assert.Equal("Oos.SectionAccessDenied", error.Code);
        }
        await using (var act = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Service(act).AddressDeviationRequestAsync(before.Id, Comment, before.Version - 1, world.Head));
            Assert.Equal("Oos.VersionConflict", error.Code);
        }
        Assert.Equal(before.Version, (await AssertOpenRequestAsync(world)).Version);
        await using (var act = fixture.CreateDbContext())
            await Service(act).AddressDeviationRequestAsync(before.Id, Comment, before.Version, world.Head);

        await using var verify = fixture.CreateDbContext();
        var after = await verify.OosDeviationRequests.AsNoTracking().SingleAsync(r => r.Id == before.Id);
        Assert.Equal(OosDeviationRequestStatus.Addressed, after.Status);
        Assert.Equal(world.Head, after.AddressedByUserId);
        Assert.Equal(Comment, after.AddressedComment);
        Assert.Equal(ActionGuardScenarios.Now, after.AddressedAtUtc);
        Assert.NotEqual(before.Version, after.Version);
        var audit = await AuditAsync(verify, nameof(OosDeviationRequest), after.Id, world.Head, "Update");
        AssertStatus(audit.PreviousValue, (int)OosDeviationRequestStatus.Open);
        AssertStatus(audit.NewValue, (int)OosDeviationRequestStatus.Addressed);
        Assert.Equal(Comment, audit.Reason);
        Assert.Contains(Comment, audit.NewValue);
        await using var retry = fixture.CreateDbContext();
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(retry).AddressDeviationRequestAsync(after.Id, "Replace recorded comment", after.Version, world.Head));
        Assert.Equal("Oos.DeviationAlreadyAddressed", refused.Code);
    }

    [PostgresFact]
    public async Task Open_request_allows_conclusion_and_qa_can_request_on_concluded_investigation()
    {
        var world = await ArrangeAsync();
        await using (var act = fixture.CreateDbContext())
            await Service(act).RequestDeviationAsync(world.Id, Reason, world.Qa);
        var request = await AssertOpenRequestAsync(world);
        await using (var act = fixture.CreateDbContext())
            await Service(act).ConcludeAsync(world.Id,
                new(OosConclusion.Confirmed, world.Category, Comment, Summary, ActionGuardScenarios.Password), world.Version, world.Head, null);

        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == world.Id);
            Assert.Equal(OosInvestigationStatus.Concluded, row.Status);
            Assert.Equal(OosConclusion.Confirmed, row.Conclusion);
            Assert.Equal(world.Head, row.ConcludedByUserId);
            Assert.Equal(ActionGuardScenarios.Now, row.ConcludedAtUtc);
            Assert.Equal(Summary, row.ConclusionSummary);
            Assert.Equal(Comment, row.RootCauseDescription);
            Assert.Equal(world.Category, row.RootCauseCategoryId);
            Assert.NotNull(row.ConclusionSignatureId);
            Assert.NotEqual(world.Version, row.Version);
            var audit = await AuditAsync(verify, nameof(OosInvestigation), row.Id, world.Head, "Update");
            AssertStatus(audit.PreviousValue, (int)OosInvestigationStatus.PhaseTwo);
            AssertStatus(audit.NewValue, (int)OosInvestigationStatus.Concluded);
        }
        Assert.Equal(request.Version, (await AssertOpenRequestAsync(world)).Version);
        await using (var act = fixture.CreateDbContext())
            await Service(act).RequestDeviationAsync(world.Id, "Additional evidence", world.Qa);

        await using var final = fixture.CreateDbContext();
        var added = await final.OosDeviationRequests.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id && r.Id != request.Id);
        Assert.Equal("Additional evidence", added.Reason);
        Assert.Equal(world.Qa, added.RequestedByUserId);
        Assert.Equal(ActionGuardScenarios.Now, added.RequestedAtUtc);
        Assert.Equal(OosDeviationRequestStatus.Open, added.Status);
        Assert.NotEqual(0u, added.Version);
        var created = await AuditAsync(final, nameof(OosDeviationRequest), added.Id, world.Qa, "Create");
        Assert.Null(created.PreviousValue);
        Assert.Contains(added.Reason, created.NewValue);
    }

    [PostgresFact]
    public async Task Overdue_uses_lab_date_and_only_applicable_deadlines()
    {
        var phaseOne = await ArrangeAsync(OosInvestigationStatus.PhaseOne, phaseOneDue: Today.AddDays(-1));
        var whole = await ArrangeAsync(due: Today.AddDays(-1));
        var dueToday = await ArrangeAsync(due: Today);
        var completedPhase = await ArrangeAsync(phaseOneDue: Today.AddDays(-1));
        var concluded = await ArrangeAsync(OosInvestigationStatus.Concluded, due: Today.AddDays(-1));
        var undated = await ArrangeAsync();
        await using var act = fixture.CreateDbContext();
        var overdue = (await Service(act).ListOverdueAsync(Today)).Select(i => i.Id).ToArray();
        Assert.Contains(phaseOne.Id, overdue);
        Assert.Contains(whole.Id, overdue);
        Assert.All(new[] { dueToday.Id, completedPhase.Id, concluded.Id, undated.Id }, id => Assert.DoesNotContain(id, overdue));
    }

    private async Task<World> ArrangeAsync(OosInvestigationStatus status = OosInvestigationStatus.PhaseTwo,
        DateOnly? phaseOneDue = null, DateOnly? due = null)
    {
        var section = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, Manage, section);
        var qa = await _seed.OutsiderAsync(RoleType.Qa, Request);
        var reviewer = await _seed.ReviewerAsync(section);
        var origin = await _seed.UnderApprovalAsync(section, reviewer.Id);
        await using var arrange = fixture.CreateDbContext();
        var category = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}", IsActive = true };
        var investigation = new OosInvestigation
        {
            Number = $"TEST-{Guid.NewGuid():N}"[..20], SampleId = origin.Sample.Id, SectionId = section.Id,
            Kind = OosInvestigationKind.Oos, Status = status, TemplateSnapshotJson = "{}",
            OpenedByUserId = reviewer.Id, OpenedAtUtc = ActionGuardScenarios.Now,
            PhaseOneDueDate = phaseOneDue, DueDate = due
        };
        arrange.AddRange(category, investigation);
        await arrange.SaveChangesAsync();
        return new(investigation.Id, investigation.Version, section, head.Id, qa.Id, category.Id);
    }

    private async Task<OosDeviationRequest> AssertOpenRequestAsync(World world)
    {
        await using var verify = fixture.CreateDbContext();
        var row = await verify.OosDeviationRequests.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
        Assert.Equal(Reason.Trim(), row.Reason);
        Assert.Equal(world.Qa, row.RequestedByUserId);
        Assert.Equal(ActionGuardScenarios.Now, row.RequestedAtUtc);
        Assert.Equal(OosDeviationRequestStatus.Open, row.Status);
        Assert.Null(row.AddressedComment);
        Assert.Null(row.AddressedByUserId);
        Assert.Null(row.AddressedAtUtc);
        Assert.NotEqual(0u, row.Version);
        var audit = await AuditAsync(verify, nameof(OosDeviationRequest), row.Id, world.Qa, "Create");
        Assert.Null(audit.PreviousValue);
        Assert.Equal(row.Reason, audit.Reason);
        Assert.Contains(row.Reason, audit.NewValue);
        AssertStatus(audit.NewValue, (int)OosDeviationRequestStatus.Open);
        return row;
    }

    private static Task<AuditLog> AuditAsync(MicroLimsDbContext db, string entity, int id, int user, string action) =>
        db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == entity && a.EntityId == id.ToString() &&
            a.UserId == user && a.Action == action);

    private static void AssertStatus(string? values, int status)
    {
        using var json = JsonDocument.Parse(Assert.IsType<string>(values));
        Assert.Equal(status, json.RootElement.GetProperty("Status").GetInt32());
    }

    private static OosInvestigationService Service(MicroLimsDbContext db) => TestServiceFactory.OosInvestigation(db, Clock);
    private OosNotifier Notifier() => new(_notificationServices.GetRequiredService<IServiceScopeFactory>());

    private static OosInvestigationsController Controller(MicroLimsDbContext db, int actor, IOosNotifier notifier, uint version)
    {
        db.ExpectedVersion = version;
        var service = Service(db);
        return new(service, OosRetestPlanPostgresTests.Service(db), notifier,
            NullLogger<OosInvestigationsController>.Instance,
            new OosTrendService(db, service, LabClock.Default),
            new OosInvestigationReportBuilder(service, new PdfGenerator(), new DataExportAuditService(db), db))
        {
            ControllerContext = Context(actor, version)
        };
    }

    private static ControllerContext Context(int actor, uint? version = null)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "Test")) };
        if (version is uint expected)
            context.Request.Headers.IfMatch = expected.ToString();
        return new ControllerContext { HttpContext = context };
    }

    private sealed record World(int Id, uint Version, DocumentSection Section, int Head, int Qa, int Category);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(ActionGuardScenarios.Now);
    }

    private sealed class ThrowingNotifier(PostgresTestFixture fixture) : IOosNotifier
    {
        public int InvestigationId { get; private set; }
        public bool SawCommittedRequest { get; private set; }

        public Task OpenedAsync(int investigationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PhaseTwoStartedAsync(int investigationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ConcludedAsync(int investigationId, CancellationToken ct = default) => throw new NotSupportedException();

        public async Task DeviationRequestedAsync(int investigationId, CancellationToken ct = default)
        {
            InvestigationId = investigationId;
            await using var observer = fixture.CreateDbContext();
            SawCommittedRequest = await observer.OosDeviationRequests.AsNoTracking()
                .AnyAsync(r => r.InvestigationId == investigationId, ct);
            throw new InvalidOperationException("Simulated notification failure.");
        }
    }
}
