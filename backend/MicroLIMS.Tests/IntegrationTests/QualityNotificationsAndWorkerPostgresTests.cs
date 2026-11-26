using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class QualityNotificationsAndWorkerPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Quality-notif-test";

    private sealed class MutableClock(DateTime initialUtc) : TimeProvider
    {
        public DateTime CurrentUtc { get; set; } = initialUtc;
        public override DateTimeOffset GetUtcNow() => new(CurrentUtc, TimeSpan.Zero);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 10) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private MicroLimsDbContext Context(TimeProvider clock, int? actor = null)
    {
        var db = fixture.CreateDbContext(clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static QualityRecordCore Core(MicroLimsDbContext db, TimeProvider clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        return new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock),
            new ElectronicSignatureService(db, clock), clock);
    }

    private async Task<int> CreateUserAsync(MicroLimsDbContext db, RoleType role, int sectionId,
        string fullName, params string[] extraPermissions)
    {
        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = fullName,
            Username = key,
            IsActive = true,
            Role = new Role { Name = key, Type = role, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var codes = new HashSet<string>(StringComparer.Ordinal) { PermissionConstants.QualityView };
        if (role != RoleType.Qa)
            codes.Add(PermissionConstants.QualityRaise);
        if (role == RoleType.Qa)
            codes.UnionWith([PermissionConstants.QualityRaise, PermissionConstants.QualityApprove, PermissionConstants.SectionsViewAll]);
        if (role == RoleType.SectionHead)
            codes.UnionWith([PermissionConstants.QualityInvestigate, PermissionConstants.QualityLabConclude]);
        foreach (var extra in extraPermissions)
            codes.Add(extra);

        var permIds = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(permIds.Select(pid => new RolePermission { RoleId = user.RoleId, PermissionId = pid }));

        var deptId = await db.DocumentSections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => s.DepartmentId).SingleAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, SectionId = sectionId, DepartmentId = deptId });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(int SectionA, int SectionB)> TwoSectionsAsync(MicroLimsDbContext db)
    {
        var sections = await db.DocumentSections.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id).Select(s => s.Id).Take(2).ToListAsync();
        if (sections.Count >= 2) return (sections[0], sections[1]);

        var dept = await db.Departments.AsNoTracking().OrderBy(d => d.Id).FirstAsync();
        var s2 = new DocumentSection { Name = $"Sec-{Guid.NewGuid():N}"[..16], Code = $"S-{Guid.NewGuid():N}"[..10], DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(s2);
        await db.SaveChangesAsync();
        return (sections[0], s2.Id);
    }

    private ServiceProvider BuildNotifierProvider(MutableClock clock)
    {
        var labClock = new LabClock(clock, TimeZoneInfo.Utc);
        return new ServiceCollection()
            .AddScoped<IMicroLimsDbContext>(_ => fixture.CreateDbContext(clock))
            .AddSingleton<TimeProvider>(clock)
            .AddSingleton<ILabClock>(labClock)
            .AddScoped<OosRecipientResolver>()
            .AddScoped<IPermissionRecipientResolver>(sp => sp.GetRequiredService<OosRecipientResolver>())
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .AddScoped<QualityNotifier>()
            .AddSingleton(NullLogger<QualityNotifier>.Instance)
            .BuildServiceProvider();
    }

    [PostgresFact]
    public async Task Overdue_worker_respects_open_state_o2_enabled_flag_and_dedupe()
    {
        var clock = new MutableClock(Utc(2090, 8, 10));
        int secA, qaId, headId, ownerId, analystId, overdueDevId, closedDevId, cancelledDevId, notOverdueDevId, overdueActionId, planActionId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Overdue");
            headId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head Overdue");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner Overdue");
            analystId = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst Overdue");

            var cls = new QualityClassification { Name = $"Ov-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            var cause = new OosCauseCategory { Name = $"OvC-{Guid.NewGuid():N}"[..16], IsActive = true };
            arrange.AddRange(cls, cause);
            await arrange.SaveChangesAsync();

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            var d1 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Overdue Dev", "Desc", Utc(2090, 8, 1), null, "Imm", secA, null), null, ownerId);
            d1 = await devSvc.AssessAsync(d1.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2090, 8, 11), Password), d1.Version, qaId);
            overdueDevId = d1.Id;

            var d2 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Future Dev", "Desc", Utc(2090, 8, 1), null, "Imm", secA, null), null, ownerId);
            d2 = await devSvc.AssessAsync(d2.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2090, 8, 25), Password), d2.Version, qaId);
            notOverdueDevId = d2.Id;

            // Closed deviation with past due date -> must NOT notify
            var dClosed = await devSvc.RaiseAsync(new RaiseDeviationRequest("Closed Dev", "Desc", Utc(2090, 8, 1), null, "Imm", secA, null), null, headId);
            dClosed = await devSvc.AssessAsync(dClosed.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2090, 8, 11), Password), dClosed.Version, qaId);
            dClosed = await devSvc.SaveInvestigationAsync(dClosed.Id, new SaveDeviationInvestigationRequest("Impact", cause.Id, "Root", "Prop"), dClosed.Version, ownerId);
            dClosed = await devSvc.SignLabConclusionAsync(dClosed.Id, Password, dClosed.Version, headId);
            dClosed = await devSvc.ApproveAsync(dClosed.Id, new ApproveDeviationRequest(false, null, Password), dClosed.Version, qaId);
            await devSvc.CloseAsync(dClosed.Id, Password, dClosed.Version, qaId);
            closedDevId = dClosed.Id;

            // Cancelled deviation with past due date -> must NOT notify
            var dCancel = await devSvc.RaiseAsync(new RaiseDeviationRequest("Cancelled Dev", "Desc", Utc(2090, 8, 1), null, "Imm", secA, null), null, headId);
            dCancel = await devSvc.AssessAsync(dCancel.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2090, 8, 11), Password), dCancel.Version, qaId);
            await devSvc.CancelAsync(dCancel.Id, "No longer needed", Password, dCancel.Version, qaId);
            cancelledDevId = dCancel.Id;

            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var capa = await capaSvc.CreateAsync(new CreateCapaRequest("Approved Action CAPA", "Desc", secA, null, null, ownerId), ownerId);
            capa = await capaSvc.SavePlanAsync(capa.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Overdue Task", analystId, new DateOnly(2090, 8, 11))],
                "Crit", "Meth", new DateOnly(2090, 8, 20)), capa.Version, ownerId);
            capa = await capaSvc.ApprovePlanAsync(capa.Id, Password, capa.Version, qaId);
            overdueActionId = capa.Actions[0].Id;

            // O2: Unapproved-plan CAPA action with past due date -> must NOT notify
            var capaPlan = await capaSvc.CreateAsync(new CreateCapaRequest("Unapproved Plan CAPA", "Desc", secA, null, null, ownerId), headId);
            capaPlan = await capaSvc.SavePlanAsync(capaPlan.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Plan Task", analystId, new DateOnly(2090, 8, 11))],
                "Crit", "Meth", new DateOnly(2090, 8, 20)), capaPlan.Version, ownerId);
            planActionId = capaPlan.Actions[0].Id;
        }

        clock.CurrentUtc = Utc(2090, 8, 12);
        await using var sp = BuildNotifierProvider(clock);

        // QualityOverdue:Enabled = false disables execution
        var disabledConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["QualityOverdue:Enabled"] = "false" })
            .Build();
        var disabledWorker = new QualityOverdueWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            disabledConfig,
            NullLogger<QualityOverdueWorker>.Instance);
        await disabledWorker.StartAsync(CancellationToken.None);
        await disabledWorker.StopAsync(CancellationToken.None);

        await using (var assertDisabled = Context(clock))
        {
            Assert.False(await assertDisabled.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.Type == "QualityRecordOverdue" && n.TargetId == overdueDevId));
        }

        var worker = new QualityOverdueWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<QualityOverdueWorker>.Instance);

        await worker.ScanOnceAsync();
        await worker.ScanOnceAsync(); // Second run same day must dedupe!

        await using (var assertDb = Context(clock))
        {
            var recNotifs = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.Type == "QualityRecordOverdue" && n.TargetId == overdueDevId)
                .ToListAsync();
            Assert.Contains(recNotifs, n => n.UserId == ownerId);
            Assert.Contains(recNotifs, n => n.UserId == qaId);
            Assert.Single(recNotifs, n => n.UserId == ownerId);

            Assert.Single(await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.Type == "QualityActionOverdue" && n.UserId == analystId && n.DedupeKey == $"quality:overdue:action:{overdueActionId}:2090-08-12")
                .ToListAsync());

            Assert.False(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.Type == "QualityRecordOverdue" && (n.TargetId == notOverdueDevId || n.TargetId == closedDevId || n.TargetId == cancelledDevId)));
            Assert.False(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.Type == "QualityActionOverdue" && n.DedupeKey == $"quality:overdue:action:{planActionId}:2090-08-12"));
        }

        // Advance to next lab day (2090-08-13): creates 1 new notification for that day
        clock.CurrentUtc = Utc(2090, 8, 13);
        await worker.ScanOnceAsync();

        await using (var assertDb2 = Context(clock))
        {
            var count = await assertDb2.NotificationLogs.AsNoTracking()
                .CountAsync(n => n.Type == "QualityRecordOverdue" && n.TargetId == overdueDevId && n.UserId == ownerId);
            Assert.Equal(2, count);
        }
    }

    private sealed class CancellingQualityNotifier : IQualityNotifier
    {
        public Task RaisedAsync(int recordId, CancellationToken ct = default) => throw new OperationCanceledException("Cancelled");
        public Task AssignedAsync(int recordId, CancellationToken ct = default) => throw new OperationCanceledException("Cancelled");
        public Task ReturnedAsync(int recordId, CancellationToken ct = default) => throw new OperationCanceledException("Cancelled");
        public Task AwaitingQaAsync(int recordId, CancellationToken ct = default) => throw new OperationCanceledException("Cancelled");
        public Task ActionAssignedAsync(int recordId, IReadOnlyList<int> actionIds, CancellationToken ct = default) => throw new OperationCanceledException("Cancelled");
        public Task ClosedAsync(int recordId, CancellationToken ct = default) => throw new OperationCanceledException("Cancelled");
    }

    [PostgresFact]
    public async Task QualityNotifier_events_recipients_scope_and_failure_safety()
    {
        var clock = new MutableClock(Utc(2090, 9, 1));
        int secA, secB, qaId, headA, headB, ownerA, analystA;

        await using (var arrange = Context(clock))
        {
            (secA, secB) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Notif");
            headA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head A Notif");
            headB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Head B Notif");
            ownerA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner A Notif");
            analystA = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst A Notif");
        }

        // 1. Failure-safety (including OperationCanceledException) across Deviation, CAPA, ChangeControl, Complaint
        var cancelling = new CancellingQualityNotifier();
        await using (var actCancel = Context(clock, headA))
        {
            var devSvc = new DeviationService(actCancel, Core(actCancel, clock), new PermissionService(actCancel),
                new UserSectionScopeService(actCancel), new SegregationOfDutiesGuard(actCancel),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actCancel),
                cancelling, NullLogger<DeviationService>.Instance);
            var dev = await devSvc.RaiseAsync(new RaiseDeviationRequest("Survives Cancel", "Desc", Utc(2090, 9, 1), null, "Imm", secA, null), null, headA);
            Assert.True(dev.Id > 0);

            var capaSvc = new CapaService(actCancel, Core(actCancel, clock), new PermissionService(actCancel),
                new UserSectionScopeService(actCancel), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actCancel),
                cancelling, NullLogger<CapaService>.Instance);
            var capa = await capaSvc.CreateAsync(new CreateCapaRequest("CAPA Survives", "Desc", secA, null, null, ownerA), headA);
            Assert.True(capa.Id > 0);

            var ccSvc = new ChangeControlService(actCancel, Core(actCancel, clock), new PermissionService(actCancel),
                new UserSectionScopeService(actCancel), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actCancel),
                new QualityConfigService(actCancel, Core(actCancel, clock), new PermissionService(actCancel), new UserSectionScopeService(actCancel), clock),
                cancelling, NullLogger<ChangeControlService>.Instance);
            var cc = await ccSvc.ProposeAsync(new ProposeChangeControlRequest("CC Survives", "Desc", "Reason", secA, null, ownerA), headA);
            Assert.True(cc.Id > 0);

            var cmpSvc = new ComplaintService(actCancel, Core(actCancel, clock), new PermissionService(actCancel),
                new UserSectionScopeService(actCancel), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actCancel),
                cancelling, NullLogger<ComplaintService>.Instance);
            var cmp = await cmpSvc.ReceiveAsync(new ReceiveComplaintRequest("CMP Survives", "Src", new DateOnly(2090, 9, 1), null, null, null, "Text", secA), qaId);
            Assert.True(cmp.Id > 0);
        }

        // 2. Real QualityNotifier: Assigned, Returned, AwaitingQa (deduped + action/task completion), ActionAssigned, Closed, Rejected, Cancelled
        await using var sp = BuildNotifierProvider(clock);
        var notifier = sp.GetRequiredService<QualityNotifier>();

        int devRejId, devCancelId, capaId, capaSelfId, ccId;
        await using (var actReal = Context(clock, headA))
        {
            var cls = new QualityClassification { Name = $"NCls-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            var cause = new OosCauseCategory { Name = $"NCause-{Guid.NewGuid():N}"[..16], IsActive = true };
            actReal.AddRange(cls, cause);
            await actReal.SaveChangesAsync();

            var devSvc = new DeviationService(actReal, Core(actReal, clock), new PermissionService(actReal),
                new UserSectionScopeService(actReal), new SegregationOfDutiesGuard(actReal),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actReal),
                notifier, NullLogger<DeviationService>.Instance);

            var dRej = await devSvc.RaiseAsync(new RaiseDeviationRequest("Rej Dev", "Desc", Utc(2090, 9, 1), null, "Imm", secA, null), null, analystA);
            devRejId = dRej.Id;
            await devSvc.RejectAsync(dRej.Id, "Not valid", Password, dRej.Version, qaId);

            var dCan = await devSvc.RaiseAsync(new RaiseDeviationRequest("Can Dev", "Desc", Utc(2090, 9, 1), null, "Imm", secA, null), null, analystA);
            devCancelId = dCan.Id;
            await devSvc.CancelAsync(dCan.Id, "Duplicate", Password, dCan.Version, qaId);

            var capaSvc = new CapaService(actReal, Core(actReal, clock), new PermissionService(actReal),
                new UserSectionScopeService(actReal), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actReal),
                notifier, NullLogger<CapaService>.Instance);

            // Owner == creator -> NO Assigned notification
            var cSelf = await capaSvc.CreateAsync(new CreateCapaRequest("Self CAPA", "Desc", secA, null, null, headA), headA);
            capaSelfId = cSelf.Id;

            var c = await capaSvc.CreateAsync(new CreateCapaRequest("Flow CAPA", "Desc", secA, null, null, ownerA), headA);
            capaId = c.Id;
            // Save plan twice -> AwaitingQa deduped (only 1 per QA user)
            c = await capaSvc.SavePlanAsync(c.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Act", analystA, new DateOnly(2090, 9, 5))],
                "Crit", "Meth", new DateOnly(2090, 9, 10)), c.Version, ownerA);
            c = await capaSvc.SavePlanAsync(c.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Act Updated", analystA, new DateOnly(2090, 9, 5))],
                "Crit", "Meth", new DateOnly(2090, 9, 10)), c.Version, ownerA);
            c = await capaSvc.ReturnPlanAsync(c.Id, "Clarify", c.Version, qaId);
            c = await capaSvc.ApprovePlanAsync(c.Id, Password, c.Version, qaId);
            c = await capaSvc.CompleteActionAsync(c.Actions[0].Id, new CompleteCapaActionRequest("Done", null), c.Version, analystA);
            c = await capaSvc.VerifyCompletionAsync(c.Id, Password, c.Version, qaId);
            clock.CurrentUtc = Utc(2090, 9, 10);
            var succ = await capaSvc.CreateAsync(new CreateCapaRequest("Succ", "Desc", secA, null, null, ownerA), headA);
            await capaSvc.SignEffectivenessAsync(c.Id, new SignCapaEffectivenessRequest(CapaEffectiveness.Effective, "Effective", null, Password), c.Version, qaId);

            var ccSvc = new ChangeControlService(actReal, Core(actReal, clock), new PermissionService(actReal),
                new UserSectionScopeService(actReal), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actReal),
                new QualityConfigService(actReal, Core(actReal, clock), new PermissionService(actReal), new UserSectionScopeService(actReal), clock),
                notifier, NullLogger<ChangeControlService>.Instance);
            var cc = await ccSvc.ProposeAsync(new ProposeChangeControlRequest("Flow CC", "Desc", "Reason", secA, null, ownerA), headA);
            ccId = cc.Id;
            cc = await ccSvc.SaveAssessmentAsync(cc.Id, new SaveChangeControlAssessmentRequest("Impact",
                [new ChangeControlTaskInput("Task", analystA, new DateOnly(2090, 9, 15))], null), cc.Version, headA);
            cc = await ccSvc.SignAssessmentAsync(cc.Id, Password, cc.Version, headA);
            cc = await ccSvc.ApproveAsync(cc.Id, Password, cc.Version, qaId);
            await ccSvc.CompleteTaskAsync(cc.Tasks[0].Id, new CompleteChangeControlTaskRequest("Ev", null), cc.Version, analystA);
        }

        await using (var assertDb = Context(clock))
        {
            // Raised excludes raiser (analystA) and other-section user (headB)
            var rejLogs = await assertDb.NotificationLogs.AsNoTracking().Where(n => n.TargetId == devRejId).ToListAsync();
            Assert.Contains(rejLogs, n => n.Type == "QualityRecordRaised" && n.UserId == qaId);
            Assert.DoesNotContain(rejLogs, n => n.Type == "QualityRecordRaised" && (n.UserId == analystA || n.UserId == headB));
            Assert.Contains(rejLogs, n => n.Type == "QualityRecordRejected" && n.UserId == analystA);

            // Cancelled notifies raiser (analystA)
            Assert.True(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.TargetId == devCancelId && n.Type == "QualityRecordCancelled" && n.UserId == analystA));

            // Self-owned CAPA has NO Assigned notification
            Assert.False(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.TargetId == capaSelfId && n.Type == "QualityRecordAssigned"));

            // CAPA events: Assigned, Returned, AwaitingQa (deduped for plan + verify), ActionAssigned, Closed
            var capaLogs = await assertDb.NotificationLogs.AsNoTracking().Where(n => n.TargetId == capaId).ToListAsync();
            Assert.Contains(capaLogs, n => n.Type == "QualityRecordAssigned" && n.UserId == ownerA);
            Assert.Contains(capaLogs, n => n.Type == "QualityRecordReturned" && n.UserId == ownerA);
            Assert.Single(capaLogs, n => n.Type == "QualityAwaitingQa" && n.UserId == qaId && n.DedupeKey == $"quality:{capaId}:awaiting-qa:plan");
            Assert.Single(capaLogs, n => n.Type == "QualityAwaitingQa" && n.UserId == qaId && n.DedupeKey == $"quality:{capaId}:awaiting-qa:verify");
            Assert.Contains(capaLogs, n => n.Type == "QualityActionAssigned" && n.UserId == analystA);
            Assert.Contains(capaLogs, n => n.Type == "QualityRecordClosed" && n.UserId == headA);
            Assert.DoesNotContain(capaLogs, n => n.UserId == headB);

            // CC last task completed -> AwaitingQa
            Assert.True(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.TargetId == ccId && n.Type == "QualityAwaitingQa" && n.UserId == qaId && n.DedupeKey == $"quality:{ccId}:awaiting-qa:close"));
        }
    }
}
