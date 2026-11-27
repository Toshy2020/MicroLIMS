using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
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
public sealed class QualityTrendsAndDashboardPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Quality-trend-test";

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

    private static QualityQueryService QueryService(MicroLimsDbContext db, TimeProvider clock) =>
        new(db, Core(db, clock), new QualityLinkService(db, Core(db, clock), new PermissionService(db), new UserSectionScopeService(db), clock), new LabClock(clock, TimeZoneInfo.Utc));

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

    [PostgresFact]
    public async Task Trends_group_by_each_dimension_o1_o3_o4_scope_and_repeat_causes()
    {
        var clock = new MutableClock(Utc(2095, 6, 1));
        int secA, secB, qaId, headA, headB, ownerA, analystA, classId, causeId, itemId, equipBId;
        string equipBCode = $"EQ-B-{Guid.NewGuid():N}"[..10];
        string itemName = $"Prod-{Guid.NewGuid():N}"[..16];

        await using (var arrange = Context(clock))
        {
            (secA, secB) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Trend");
            headA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head A Trend");
            headB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Head B Trend");
            ownerA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner A Trend");
            analystA = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst A Trend");

            var cls = new QualityClassification { Name = $"Crit-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            var cause = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}"[..16], IsActive = true };
            var item = new Item { Name = itemName, Code = $"P-{Guid.NewGuid():N}"[..10], Category = SampleCategory.FinishedProduct, IsActive = true };
            var equipB = new Equipment { Code = equipBCode, Name = "Secret B Centrifuge", SectionId = secB };
            arrange.AddRange(cls, cause, item, equipB);
            await arrange.SaveChangesAsync();
            classId = cls.Id;
            causeId = cause.Id;
            itemId = item.Id;
            equipBId = equipB.Id;

            var causeOfTesting = await arrange.CausesOfTesting.AsNoTracking().FirstAsync();
            var sampleWithItem = new Sample
            {
                ReferenceNumber = $"SMP-I-{Guid.NewGuid():N}"[..16],
                ControlNumber = $"CN-I-{Guid.NewGuid():N}"[..16],
                SampledBy = "qc",
                LabSectionId = secA,
                Category = SampleCategory.FinishedProduct,
                ItemId = itemId,
                CauseOfTestingId = causeOfTesting.Id,
                ReceivedByUserId = headA,
                ReceivedAt = Utc(2095, 6, 1),
                Status = SampleStatus.Received
            };
            arrange.Samples.Add(sampleWithItem);
            await arrange.SaveChangesAsync();

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            // 1. Deviation linked ONLY to a Sample (O1) - closed in exactly 3.0 days, on-time
            clock.CurrentUtc = Utc(2095, 6, 1, 10);
            var d1 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Sample-Linked Dev", "Desc", clock.CurrentUtc, null, "Imm",
                secA, [new QualityLinkRequest(QualityLinkTargetType.Sample, sampleWithItem.Id)]), null, headA);
            d1 = await devSvc.AssessAsync(d1.Id, new AssessDeviationRequest(true, classId, ownerA, new DateOnly(2095, 6, 10), Password), d1.Version, qaId);
            d1 = await devSvc.SaveInvestigationAsync(d1.Id, new SaveDeviationInvestigationRequest("Impact", causeId, "Root", "Prop"), d1.Version, ownerA);
            d1 = await devSvc.SignLabConclusionAsync(d1.Id, Password, d1.Version, headA);
            d1 = await devSvc.ApproveAsync(d1.Id, new ApproveDeviationRequest(false, null, Password), d1.Version, qaId);
            clock.CurrentUtc = Utc(2095, 6, 4, 10);
            await devSvc.CloseAsync(d1.Id, Password, d1.Version, qaId);

            // 2. Cancelled deviation (O3: excluded from repeat-cause/time-to-close/on-time; counted in TotalCount + CancelledCount)
            clock.CurrentUtc = Utc(2095, 6, 2, 10);
            var dCancel = await devSvc.RaiseAsync(new RaiseDeviationRequest("Cancelled Dev", "Desc", clock.CurrentUtc, null, "Imm",
                secA, [new QualityLinkRequest(QualityLinkTargetType.Item, itemId)]), null, headA);
            dCancel = await devSvc.AssessAsync(dCancel.Id, new AssessDeviationRequest(true, classId, ownerA, new DateOnly(2095, 6, 10), Password), dCancel.Version, qaId);
            dCancel = await devSvc.SaveInvestigationAsync(dCancel.Id, new SaveDeviationInvestigationRequest("Impact", causeId, "Root", "Prop"), dCancel.Version, ownerA);
            await devSvc.CancelAsync(dCancel.Id, "Duplicate", Password, dCancel.Version, qaId);

            // 3. Rejected deviation (O3: excluded from repeat-cause/time-to-close/on-time; counted in TotalCount + RejectedCount)
            clock.CurrentUtc = Utc(2095, 6, 3, 10);
            var dReject = await devSvc.RaiseAsync(new RaiseDeviationRequest("Rejected Dev", "Desc", clock.CurrentUtc, null, "Imm", secA, null), null, headA);
            await devSvc.RejectAsync(dReject.Id, "Not a deviation", Password, dReject.Version, qaId);

            // 4. Site-wide deviations linked to Section B equipment with same cause:
            //    2 inside 10-day window (June 1 & June 5) + 1 outside (Oct 1) -> cluster count must be 2, span <= 10d
            foreach (var day in new[] { Utc(2095, 6, 1, 12), Utc(2095, 6, 5, 12), Utc(2095, 10, 1, 12) })
            {
                clock.CurrentUtc = day;
                var dw = await devSvc.RaiseAsync(new RaiseDeviationRequest("Site Equip Dev", "Desc", clock.CurrentUtc, null, "Imm",
                    null, [new QualityLinkRequest(QualityLinkTargetType.Equipment, equipBId)]), null, qaId);
                dw = await devSvc.AssessAsync(dw.Id, new AssessDeviationRequest(true, classId, ownerA, new DateOnly(2095, 10, 20), Password), dw.Version, qaId);
                await devSvc.SaveInvestigationAsync(dw.Id, new SaveDeviationInvestigationRequest("Impact", causeId, "Root", "Prop"), dw.Version, ownerA);
            }

            // 5. Section B deviation (hidden from headA)
            clock.CurrentUtc = Utc(2095, 6, 6, 10);
            await devSvc.RaiseAsync(new RaiseDeviationRequest("SecB Dev", "Desc", clock.CurrentUtc, null, "Imm", secB, null), null, headB);

            // 6. Old deviation (> 365 days before 2095-10-15) to test O4 default 365-day window
            clock.CurrentUtc = Utc(2093, 1, 1, 10);
            await devSvc.RaiseAsync(new RaiseDeviationRequest("Ancient Dev", "Desc", clock.CurrentUtc, null, "Imm", secA, null), null, headA);

            // 7. Two closed CAPAs: one Effective, one NotEffective (CapaEffectivenessRate == 0.5)
            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            clock.CurrentUtc = Utc(2095, 6, 1, 10);
            var c1 = await capaSvc.CreateAsync(new CreateCapaRequest("Eff CAPA", "Desc", secA, null, null, ownerA), headA);
            c1 = await capaSvc.SavePlanAsync(c1.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Act1", analystA, new DateOnly(2095, 6, 2))],
                "Crit", "Meth", new DateOnly(2095, 6, 3)), c1.Version, ownerA);
            c1 = await capaSvc.ApprovePlanAsync(c1.Id, Password, c1.Version, qaId);
            c1 = await capaSvc.CompleteActionAsync(c1.Actions[0].Id, new CompleteCapaActionRequest("Ev", null), c1.Version, analystA);
            c1 = await capaSvc.VerifyCompletionAsync(c1.Id, Password, c1.Version, qaId);
            clock.CurrentUtc = Utc(2095, 6, 3, 10);
            await capaSvc.SignEffectivenessAsync(c1.Id, new SignCapaEffectivenessRequest(CapaEffectiveness.Effective, "Good", null, Password), c1.Version, qaId);

            clock.CurrentUtc = Utc(2095, 6, 1, 10);
            var c2 = await capaSvc.CreateAsync(new CreateCapaRequest("NotEff CAPA", "Desc", secA, null, null, ownerA), headA);
            c2 = await capaSvc.SavePlanAsync(c2.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Act2", analystA, new DateOnly(2095, 6, 2))],
                "Crit", "Meth", new DateOnly(2095, 6, 3)), c2.Version, ownerA);
            c2 = await capaSvc.ApprovePlanAsync(c2.Id, Password, c2.Version, qaId);
            c2 = await capaSvc.CompleteActionAsync(c2.Actions[0].Id, new CompleteCapaActionRequest("Ev", null), c2.Version, analystA);
            c2 = await capaSvc.VerifyCompletionAsync(c2.Id, Password, c2.Version, qaId);
            var successor = await capaSvc.CreateAsync(new CreateCapaRequest("Successor CAPA", "Desc", secA, c2.Id, null, ownerA), headA);
            clock.CurrentUtc = Utc(2095, 6, 3, 10);
            await capaSvc.SignEffectivenessAsync(c2.Id, new SignCapaEffectivenessRequest(CapaEffectiveness.NotEffective, "Failed", successor.Id, Password), c2.Version, qaId);
        }

        clock.CurrentUtc = Utc(2095, 10, 15, 10);

        await using (var actQa = Context(clock, qaId))
        {
            var svc = QueryService(actQa, clock);

            // O4: date-only `to` (2095-10-01 00:00Z) includes the Oct 1 12:00Z record
            var typeReport = await svc.GetTrendsAsync("type", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, qaId);
            var devGroup = Assert.Single(typeReport.Groups, g => g.Key == nameof(QualityRecordType.Deviation));
            Assert.Equal(7, devGroup.TotalCount); // d1 + dCancel + dReject + 3 equipB devs + secB dev
            Assert.Equal(1, devGroup.ClosedCount);
            Assert.Equal(1, devGroup.CancelledCount);
            Assert.Equal(1, devGroup.RejectedCount);
            Assert.Equal(4, devGroup.OpenCount);
            Assert.Equal(3.0, devGroup.AverageTimeToCloseDays);
            Assert.Equal(1.0, devGroup.OnTimeRate);

            var capaGroup = Assert.Single(typeReport.Groups, g => g.Key == nameof(QualityRecordType.Capa));
            Assert.Equal(3, capaGroup.TotalCount);
            Assert.Equal(2, capaGroup.ClosedCount);
            Assert.Equal(1, capaGroup.OpenCount);
            Assert.Equal(0.5, capaGroup.CapaEffectivenessRate);

            // O1: sample-linked deviation appears under itemId
            var itemReport = await svc.GetTrendsAsync("item", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, qaId);
            var itemGroup = Assert.Single(itemReport.Groups, g => g.Key == itemId.ToString());
            Assert.Equal(itemName, itemGroup.Label);
            Assert.Equal(2, itemGroup.TotalCount); // d1 (via sample) + dCancel (direct item link)
            Assert.Equal(1, itemGroup.CancelledCount);
            Assert.Equal(1, itemGroup.ClosedCount);

            // Other groupBy dimensions return exact keys
            var clsReport = await svc.GetTrendsAsync("classification", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, qaId);
            Assert.Contains(clsReport.Groups, g => g.Key == classId.ToString());

            var causeReport = await svc.GetTrendsAsync("cause", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, qaId);
            Assert.Contains(causeReport.Groups, g => g.Key == causeId.ToString());

            var secReport = await svc.GetTrendsAsync("section", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, qaId);
            Assert.Contains(secReport.Groups, g => g.Key == secA.ToString());
            Assert.Contains(secReport.Groups, g => g.Key == secB.ToString());
            Assert.Contains(secReport.Groups, g => g.Key == "site-wide");

            var srcReport = await svc.GetTrendsAsync("source", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, qaId);
            Assert.Contains(srcReport.Groups, g => g.Key == "other");

            // Repeat cause cluster windowing: June 1 & June 5 are within 10d, Oct 1 is outside -> OccurrenceCount == 2
            var equipRepeat = Assert.Single(typeReport.RepeatCauses,
                r => r.CauseCategoryId == causeId && r.TargetType == QualityLinkTargetType.Equipment && r.TargetId == equipBId);
            Assert.Equal(2, equipRepeat.OccurrenceCount);
            Assert.True(equipRepeat.LastRaisedAtUtc - equipRepeat.FirstRaisedAtUtc <= TimeSpan.FromDays(10));

            // O4: default 365 lab days excludes Ancient Dev (2093-01-01)
            var defaultReport = await svc.GetTrendsAsync("type", null, null, 30, qaId);
            Assert.Equal(7, defaultReport.Groups.Single(g => g.Key == nameof(QualityRecordType.Deviation)).TotalCount);

            // O4: > 5 years refused with coded message
            var err = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.GetTrendsAsync("type", Utc(2080, 1, 1), Utc(2095, 1, 1), 30, qaId));
            Assert.Equal(MessageCodes.QualityTrendWindowTooLarge, err.Code);
        }

        // Scope for trends: headA sees NO secB records, and secB equipment repeat cause hides equipment name ("Restricted" + code only)
        await using (var actHeadA = Context(clock, headA))
        {
            var svc = QueryService(actHeadA, clock);
            var report = await svc.GetTrendsAsync("section", Utc(2095, 6, 1, 0), Utc(2095, 10, 1, 0), 10, headA);
            Assert.DoesNotContain(report.Groups, g => g.Key == secB.ToString());

            var equipRepeat = Assert.Single(report.RepeatCauses,
                r => r.TargetType == QualityLinkTargetType.Equipment && r.TargetId == equipBId);
            Assert.Contains(equipBCode, equipRepeat.TargetLabel);
            Assert.Contains("Restricted", equipRepeat.TargetLabel);
            Assert.DoesNotContain("Secret B Centrifuge", equipRepeat.TargetLabel);
        }
    }

    [PostgresFact]
    public async Task Dashboard_enforces_scope_sod_o2_o5_and_no_false_positives()
    {
        var clock = new MutableClock(Utc(2090, 7, 10));
        int secA, secB, qaId, headA, headB, ownerA, analystA;
        int devReadyToCloseId, devNeedsCapaId, devWaitingLabId, capaReturnedId, capaIncompleteId, capaPlanActionId, secBDevId;

        await using (var arrange = Context(clock))
        {
            (secA, secB) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Dash");
            headA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head A Dash");
            headB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Head B Dash");
            ownerA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner A Dash");
            analystA = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst A Dash");

            var cls = new QualityClassification { Name = $"Min-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            var cause = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}"[..16], IsActive = true };
            arrange.AddRange(cls, cause);
            await arrange.SaveChangesAsync();

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            // 1. Deviation waiting for Lab Conclusion (headA)
            var dLab = await devSvc.RaiseAsync(new RaiseDeviationRequest("Lab Conclusion Waiting", "Desc", Utc(2090, 7, 1), null, "Imm", secA, null), null, analystA);
            dLab = await devSvc.AssessAsync(dLab.Id, new AssessDeviationRequest(true, cls.Id, ownerA, new DateOnly(2090, 7, 20), Password), dLab.Version, qaId);
            dLab = await devSvc.SaveInvestigationAsync(dLab.Id, new SaveDeviationInvestigationRequest("Impact", cause.Id, "Root", "Prop"), dLab.Version, ownerA);
            devWaitingLabId = dLab.Id;

            // 2. Deviation in Approved with CapaRequired = false (ready to close, due 2090-07-11 -> overdue at 07-12)
            var dNoCapa = await devSvc.RaiseAsync(new RaiseDeviationRequest("Ready No CAPA", "Desc", Utc(2090, 7, 1), null, "Imm", secA, null), null, analystA);
            dNoCapa = await devSvc.AssessAsync(dNoCapa.Id, new AssessDeviationRequest(true, cls.Id, ownerA, new DateOnly(2090, 7, 11), Password), dNoCapa.Version, qaId);
            dNoCapa = await devSvc.SaveInvestigationAsync(dNoCapa.Id, new SaveDeviationInvestigationRequest("Impact", cause.Id, "Root", "Prop"), dNoCapa.Version, ownerA);
            dNoCapa = await devSvc.SignLabConclusionAsync(dNoCapa.Id, Password, dNoCapa.Version, headA);
            dNoCapa = await devSvc.ApproveAsync(dNoCapa.Id, new ApproveDeviationRequest(false, null, Password), dNoCapa.Version, qaId);
            devReadyToCloseId = dNoCapa.Id;

            // 3. Deviation in Approved with CapaRequired = true and NO approved CAPA (must NOT be in WaitingForMyDecision)
            var dNeedsCapa = await devSvc.RaiseAsync(new RaiseDeviationRequest("Needs CAPA", "Desc", Utc(2090, 7, 1), null, "Imm", secA, null), null, analystA);
            dNeedsCapa = await devSvc.AssessAsync(dNeedsCapa.Id, new AssessDeviationRequest(true, cls.Id, ownerA, new DateOnly(2090, 7, 20), Password), dNeedsCapa.Version, qaId);
            dNeedsCapa = await devSvc.SaveInvestigationAsync(dNeedsCapa.Id, new SaveDeviationInvestigationRequest("Impact", cause.Id, "Root", "Prop"), dNeedsCapa.Version, ownerA);
            dNeedsCapa = await devSvc.SignLabConclusionAsync(dNeedsCapa.Id, Password, dNeedsCapa.Version, headA);
            dNeedsCapa = await devSvc.ApproveAsync(dNeedsCapa.Id, new ApproveDeviationRequest(true, null, Password), dNeedsCapa.Version, qaId);
            devNeedsCapaId = dNeedsCapa.Id;

            // 4. Section B deviation (hidden from headA)
            var dSecB = await devSvc.RaiseAsync(new RaiseDeviationRequest("Sec B Dev", "Desc", Utc(2090, 7, 1), null, "Imm", secB, null), null, headB);
            secBDevId = dSecB.Id;

            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            // 5. CAPA with NO plan saved yet (must NOT be in QA's WaitingForMyDecision)
            var cInc = await capaSvc.CreateAsync(new CreateCapaRequest("Incomplete CAPA", "Desc", secA, null, null, ownerA), headA);
            capaIncompleteId = cInc.Id;

            // 6. CAPA with complete plan, then RETURNED by QA (must NOT be in QA's WaitingForMyDecision; O2 action due 2090-07-11 not overdue because Plan)
            var cRet = await capaSvc.CreateAsync(new CreateCapaRequest("Returned CAPA", "Desc", secA, null, null, ownerA), headA);
            cRet = await capaSvc.SavePlanAsync(cRet.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Plan Act", analystA, new DateOnly(2090, 7, 11))],
                "Crit", "Meth", new DateOnly(2090, 7, 15)), cRet.Version, ownerA);
            capaPlanActionId = cRet.Actions[0].Id;
            await capaSvc.ReturnPlanAsync(cRet.Id, "Fix criteria", cRet.Version, qaId);
            capaReturnedId = cRet.Id;

            // 7. CAPA raised by QA (complete plan) -> SoD: QA must NOT see it in WaitingForMyDecision!
            var cQa = await capaSvc.CreateAsync(new CreateCapaRequest("QA Raised CAPA", "Desc", secA, null, null, ownerA), qaId);
            await capaSvc.SavePlanAsync(cQa.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "QA Act", analystA, new DateOnly(2090, 7, 12))],
                "Crit", "Meth", new DateOnly(2090, 7, 15)), cQa.Version, ownerA);

            // 8. 52 open CAPAs (incomplete plan, so they don't fill QA's WaitingForMyDecision) raised by headA for O5 50-item cap + TotalCount
            for (var i = 0; i < 52; i++)
                await capaSvc.CreateAsync(new CreateCapaRequest($"Bulk CAPA {i:D2}", "Desc", secA, null, null, ownerA), headA);
        }

        // Advance clock to 2090-07-12 so capaPlanActionId (due 07-11) is past due, but still in Plan status (O2!)
        clock.CurrentUtc = Utc(2090, 7, 12);

        await using (var actQa = Context(clock, qaId))
        {
            var dash = await QueryService(actQa, clock).GetDashboardAsync(qaId);
            Assert.Contains(dash.WaitingForMyDecision, x => x.Id == devReadyToCloseId);
            Assert.DoesNotContain(dash.WaitingForMyDecision, x => x.Id == devNeedsCapaId);
            Assert.DoesNotContain(dash.WaitingForMyDecision, x => x.Id == capaIncompleteId);
            Assert.DoesNotContain(dash.WaitingForMyDecision, x => x.Id == capaReturnedId);
            // SoD: QA raised QA Raised CAPA so QA cannot see it in WaitingForMyDecision
            Assert.DoesNotContain(dash.WaitingForMyDecision, x => x.Title == "QA Raised CAPA");
            // O2: Plan-status CAPA action is NOT in OverdueActions
            Assert.DoesNotContain(dash.OverdueActions, x => x.ActionId == capaPlanActionId);
            // QA has no LaboratoryOpenRecords
            Assert.True(dash.LaboratoryOpenRecords is null or { Count: 0 });
        }

        await using (var actHeadA = Context(clock, headA))
        {
            var dash = await QueryService(actHeadA, clock).GetDashboardAsync(headA);
            Assert.Contains(dash.WaitingForMyDecision, x => x.Id == devWaitingLabId);
            Assert.DoesNotContain(dash.WaitingForMyDecision, x => x.Id == secBDevId);
            // O5: 50-item cap + TotalCount >= 52
            Assert.Equal(50, dash.MyOpenRecords.Count);
            Assert.True(dash.MyOpenRecordsTotalCount >= 52);
            // O5: Section Head gets LaboratoryOpenRecords & LaboratoryOpenActions for secA (capped at 50), no secB
            Assert.NotNull(dash.LaboratoryOpenRecords);
            Assert.Equal(50, dash.LaboratoryOpenRecords!.Count);
            Assert.True(dash.LaboratoryOpenRecordsTotalCount >= 52);
            Assert.DoesNotContain(dash.LaboratoryOpenRecords, x => x.Id == secBDevId);
        }

        await using (var actHeadB = Context(clock, headB))
        {
            var dash = await QueryService(actHeadB, clock).GetDashboardAsync(headB);
            Assert.DoesNotContain(dash.MyOpenRecords, x => x.Id == devWaitingLabId);
            Assert.DoesNotContain(dash.OverdueRecords, x => x.Id == devReadyToCloseId);
            Assert.Contains(dash.MyOpenRecords, x => x.Id == secBDevId);
            Assert.DoesNotContain(dash.LaboratoryOpenRecords ?? [], x => x.Id == devWaitingLabId);
        }

        await using (var actAnalyst = Context(clock, analystA))
        {
            var dash = await QueryService(actAnalyst, clock).GetDashboardAsync(analystA);
            Assert.Empty(dash.WaitingForMyDecision);
            Assert.Contains(dash.MyOpenRecords, x => x.Id == devWaitingLabId);
            Assert.DoesNotContain(dash.MyOpenRecords, x => x.Id == secBDevId);
            Assert.True(dash.LaboratoryOpenRecords is null or { Count: 0 });
        }
    }
}
