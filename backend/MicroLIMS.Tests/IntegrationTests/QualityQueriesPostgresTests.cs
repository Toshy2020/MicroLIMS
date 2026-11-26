using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using UglyToad.PdfPig;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class QualityQueriesPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Quality-query-test";

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
        new(db, Core(db, clock), new LabClock(clock, TimeZoneInfo.Utc));

    private static QualityReportBuilder ReportBuilder(MicroLimsDbContext db, TimeProvider clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = Core(db, clock);
        var links = new QualityLinkService(db, core, permissions, scope, clock);
        var query = new QualityQueryService(db, core, new LabClock(clock, TimeZoneInfo.Utc));
        return new QualityReportBuilder(query, links, new PdfGenerator(), new DataExportAuditService(db), db);
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

    [PostgresFact]
    public async Task List_filters_and_paging_clamp_work()
    {
        var clock = new MutableClock(Utc(2089, 3, 10));
        int secA, secB, qaId, headA, headB, ownerA, ownerB, classId;
        int devId, capaId;

        await using (var arrange = Context(clock))
        {
            (secA, secB) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Filter");
            headA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head A");
            headB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Head B");
            ownerA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner A");
            ownerB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Owner B");

            var cls = new QualityClassification { Name = $"Cls-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            arrange.QualityClassifications.Add(cls);
            await arrange.SaveChangesAsync();
            classId = cls.Id;

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var dev = await devSvc.RaiseAsync(new RaiseDeviationRequest("Filter Dev", "Desc", Utc(2089, 3, 9), null, "Immediate", secA, null), null, headA);
            var assessed = await devSvc.AssessAsync(dev.Id, new AssessDeviationRequest(true, classId, ownerA, new DateOnly(2089, 3, 12), Password), dev.Version, qaId);
            devId = assessed.Id;

            clock.CurrentUtc = Utc(2089, 3, 15);
            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var capa = await capaSvc.CreateAsync(new CreateCapaRequest("Filter CAPA", "Desc", secB, null, null, ownerB), headB);
            capaId = capa.Id;
        }

        // Advance clock so devId (due 2089-03-05) is overdue at 2089-03-15
        await using (var act = Context(clock, qaId))
        {
            var svc = QueryService(act, clock);

            var byType = await svc.ListAsync(new QualityRecordFilter(Type: QualityRecordType.Deviation), qaId);
            Assert.Contains(byType.Items, x => x.Id == devId);
            Assert.DoesNotContain(byType.Items, x => x.Id == capaId);

            var byStatus = await svc.ListAsync(new QualityRecordFilter(Status: "Investigation"), qaId);
            Assert.Contains(byStatus.Items, x => x.Id == devId);
            Assert.DoesNotContain(byStatus.Items, x => x.Id == capaId);

            var byClass = await svc.ListAsync(new QualityRecordFilter(ClassificationId: classId), qaId);
            Assert.Contains(byClass.Items, x => x.Id == devId);
            Assert.DoesNotContain(byClass.Items, x => x.Id == capaId);

            var bySec = await svc.ListAsync(new QualityRecordFilter(SectionId: secB), qaId);
            Assert.Contains(bySec.Items, x => x.Id == capaId);
            Assert.DoesNotContain(bySec.Items, x => x.Id == devId);

            var byOwner = await svc.ListAsync(new QualityRecordFilter(OwnerUserId: ownerA), qaId);
            Assert.Contains(byOwner.Items, x => x.Id == devId);

            var byOverdue = await svc.ListAsync(new QualityRecordFilter(Overdue: true), qaId);
            Assert.Contains(byOverdue.Items, x => x.Id == devId);
            Assert.DoesNotContain(byOverdue.Items, x => x.Id == capaId);

            var byDate = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 14), To: Utc(2089, 3, 16)), qaId);
            Assert.Contains(byDate.Items, x => x.Id == capaId);
            Assert.DoesNotContain(byDate.Items, x => x.Id == devId);

            var clampedHigh = await svc.ListAsync(new QualityRecordFilter(Page: 0, PageSize: 999), qaId);
            Assert.Equal(1, clampedHigh.Page);
            Assert.Equal(100, clampedHigh.PageSize);

            var clampedLow = await svc.ListAsync(new QualityRecordFilter(Page: -2, PageSize: 0), qaId);
            Assert.Equal(1, clampedLow.Page);
            Assert.Equal(25, clampedLow.PageSize);
        }
    }

    [PostgresFact]
    public async Task Read_scope_and_analyst_visibility_enforced_across_list_detail_and_pdf()
    {
        var clock = new MutableClock(Utc(2089, 4, 1));
        int secA, secB, headA, headB, analystA1, analystA2, qaId;
        int devA1Id, devA2Id, sampleBId;

        await using (var arrange = Context(clock))
        {
            (secA, secB) = await TwoSectionsAsync(arrange);
            headA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head A");
            headB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Head B");
            analystA1 = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst One");
            analystA2 = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst Two");
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Scope");

            var cause = await arrange.CausesOfTesting.AsNoTracking().FirstAsync();
            var sampleB = new Sample
            {
                ReferenceNumber = $"SMP-B-{Guid.NewGuid():N}"[..16],
                ControlNumber = $"CN-B-{Guid.NewGuid():N}"[..16],
                SampledBy = "qc",
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = headB,
                ReceivedAt = Utc(2089, 4, 1),
                Status = SampleStatus.Received
            };
            arrange.Samples.Add(sampleB);
            await arrange.SaveChangesAsync();
            arrange.TestOrders.Add(new TestOrder { SampleId = sampleB.Id, SectionId = secB, TestCode = "TAMC", Status = ApprovalStatus.Pending });
            await arrange.SaveChangesAsync();
            sampleBId = sampleB.Id;

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            // Raised by analystA1 in secA, plus a link to sampleB (which headA cannot read) added by qaId
            var d1 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Analyst1 Dev", "Desc", Utc(2089, 4, 1), null, "Imm", secA, null), null, analystA1);
            devA1Id = d1.Id;
            var linkSvc = new QualityLinkService(arrange, Core(arrange, clock), new PermissionService(arrange), new UserSectionScopeService(arrange), clock);
            await linkSvc.AddAsync(devA1Id, QualityLinkTargetType.Sample, sampleBId, qaId, d1.Version);

            // Raised by headA in secA (analystA1 is neither raiser, owner, nor action assignee)
            var d2 = await devSvc.RaiseAsync(new RaiseDeviationRequest("HeadA Dev", "Desc", Utc(2089, 4, 1), null, "Imm", secA, null), null, headA);
            devA2Id = d2.Id;
        }

        // Other-section user (headB) sees nothing from secA and is refused on detail and PDF
        await using (var actB = Context(clock, headB))
        {
            var svc = QueryService(actB, clock);
            var list = await svc.ListAsync(new QualityRecordFilter(), headB);
            Assert.DoesNotContain(list.Items, x => x.Id == devA1Id || x.Id == devA2Id);

            await Assert.ThrowsAsync<NotFoundException>(() => svc.GetDetailAsync(devA1Id, headB));
            await Assert.ThrowsAsync<NotFoundException>(() => ReportBuilder(actB, clock).GeneratePdfAsync(devA1Id, headB));
        }

        // Analyst A1 sees devA1Id (raised by them) but NOT devA2Id
        await using (var actAnalyst1 = Context(clock, analystA1))
        {
            var svc = QueryService(actAnalyst1, clock);
            var list = await svc.ListAsync(new QualityRecordFilter(), analystA1);
            Assert.Contains(list.Items, x => x.Id == devA1Id);
            Assert.DoesNotContain(list.Items, x => x.Id == devA2Id);

            await Assert.ThrowsAsync<NotFoundException>(() => svc.GetDetailAsync(devA2Id, analystA1));
        }

        // Analyst A2 sees neither
        await using (var actAnalyst2 = Context(clock, analystA2))
        {
            var svc = QueryService(actAnalyst2, clock);
            var list = await svc.ListAsync(new QualityRecordFilter(), analystA2);
            Assert.DoesNotContain(list.Items, x => x.Id == devA1Id || x.Id == devA2Id);
        }

        // Head A can read devA1Id, and the restricted link to sampleB shows its number only (no label/route)
        await using (var actHeadA = Context(clock, headA))
        {
            var detail = await QueryService(actHeadA, clock).GetDetailAsync(devA1Id, headA);
            Assert.Equal(1, detail.LinksCount);

            var (_, pdfBytes) = await ReportBuilder(actHeadA, clock).GeneratePdfAsync(devA1Id, headA);
            using var pdf = PdfDocument.Open(pdfBytes);
            var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            Assert.Contains("SMP-B-", text);
        }
    }

    [PostgresFact]
    public async Task Detail_and_pdf_include_path_steps_signed_timeline_and_export_audit()
    {
        var clock = new MutableClock(Utc(2089, 5, 10, 14));
        int secA, qaId, headId, ownerId, devId, classId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "Dr. Nadia QA");
            headId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Tariq Head");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Salma Owner");

            var cls = new QualityClassification { Name = $"Major-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            arrange.QualityClassifications.Add(cls);
            await arrange.SaveChangesAsync();
            classId = cls.Id;

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            var raised = await devSvc.RaiseAsync(new RaiseDeviationRequest("PDF Dev", "Desc", Utc(2089, 5, 10, 9), "Lab 1", "Quarantined", secA, null), null, headId);
            var assessed = await devSvc.AssessAsync(raised.Id, new AssessDeviationRequest(true, classId, ownerId, new DateOnly(2089, 5, 20), Password), raised.Version, qaId);
            devId = assessed.Id;
        }

        await using (var act = Context(clock, qaId))
        {
            var detail = await QueryService(act, clock).GetDetailAsync(devId, qaId);
            Assert.NotNull(detail.Deviation);
            Assert.NotEmpty(detail.PathSteps);
            var sig = Assert.Single(detail.Timeline);
            Assert.Equal("Dr. Nadia QA", sig.SignerFullName);
            Assert.Contains("UTC", sig.SignedAtUtcText);
            Assert.Equal(ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.DeviationAssessed)), sig.MeaningText);

            var (fileName, bytes) = await ReportBuilder(act, clock).GeneratePdfAsync(devId, qaId);
            Assert.EndsWith(".pdf", fileName);
            using var pdf = PdfDocument.Open(bytes);
            var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            Assert.Contains("Dr. Nadia QA", text);
            Assert.Contains("2089-05-10 14:00:00 UTC", text);
            Assert.Contains(ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.DeviationAssessed)), text);
        }

        await using (var assertDb = Context(clock))
        {
            var exportLog = await assertDb.DataExportLogs.AsNoTracking()
                .Where(e => e.UserId == qaId && e.ExportType == "QualityRecordPdf")
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            Assert.NotNull(exportLog);
        }
    }

    [PostgresFact]
    public async Task Trends_group_by_each_dimension_and_detect_repeat_causes()
    {
        var clock = new MutableClock(Utc(2089, 6, 1));
        int secA, qaId, headId, ownerId, classId, causeId, itemId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Trend");
            headId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head Trend");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner Trend");

            var cls = new QualityClassification { Name = $"Crit-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            var cause = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}"[..16], IsActive = true };
            var item = new Item { Name = $"Prod-{Guid.NewGuid():N}"[..16], Code = $"P-{Guid.NewGuid():N}"[..10], Category = SampleCategory.FinishedProduct, IsActive = true };
            arrange.AddRange(cls, cause, item);
            await arrange.SaveChangesAsync();
            classId = cls.Id;
            causeId = cause.Id;
            itemId = item.Id;

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            // Two deviations with the same cause + item within 10 days (repeat cause!)
            for (var i = 0; i < 2; i++)
            {
                clock.CurrentUtc = Utc(2089, 6, 1 + i * 5);
                var d = await devSvc.RaiseAsync(new RaiseDeviationRequest($"Trend Dev {i}", "Desc", clock.CurrentUtc, null, "Imm",
                    secA, [new QualityLinkRequest(QualityLinkTargetType.Item, itemId)]), null, headId);
                d = await devSvc.AssessAsync(d.Id, new AssessDeviationRequest(true, classId, ownerId, new DateOnly(2089, 6, 20), Password), d.Version, qaId);
                d = await devSvc.SaveInvestigationAsync(d.Id, new SaveDeviationInvestigationRequest("Impact", causeId, "Root", "Prop"), d.Version, ownerId);
                d = await devSvc.SignLabConclusionAsync(d.Id, Password, d.Version, headId);
                d = await devSvc.ApproveAsync(d.Id, new ApproveDeviationRequest(false, null, Password), d.Version, qaId);
                if (i == 0)
                {
                    clock.CurrentUtc = Utc(2089, 6, 4);
                    await devSvc.CloseAsync(d.Id, Password, d.Version, qaId);
                }
            }
        }

        await using (var act = Context(clock, qaId))
        {
            var svc = QueryService(act, clock);
            foreach (var groupBy in new[] { "type", "classification", "cause", "section", "item", "source" })
            {
                var report = await svc.GetTrendsAsync(groupBy, Utc(2089, 5, 30), Utc(2089, 6, 30), 30, qaId);
                Assert.Equal(groupBy, report.GroupBy);
                Assert.NotEmpty(report.Groups);
            }

            var causeReport = await svc.GetTrendsAsync("cause", Utc(2089, 5, 30), Utc(2089, 6, 30), 30, qaId);
            var repeat = Assert.Single(causeReport.RepeatCauses, r => r.CauseCategoryId == causeId && r.TargetType == QualityLinkTargetType.Item && r.TargetId == itemId);
            Assert.Equal(2, repeat.OccurrenceCount);

            var tightWindow = await svc.GetTrendsAsync("cause", Utc(2089, 5, 30), Utc(2089, 6, 30), 2, qaId);
            Assert.DoesNotContain(tightWindow.RepeatCauses, r => r.CauseCategoryId == causeId && r.TargetId == itemId);

            await Assert.ThrowsAsync<BusinessRuleException>(() => svc.GetTrendsAsync("invalid", null, null, 30, qaId));
        }
    }

    [PostgresFact]
    public async Task Dashboard_returns_role_specific_items()
    {
        var clock = new MutableClock(Utc(2089, 7, 10));
        int secA, qaId, headId, ownerId, analystId, devId, capaId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Dash");
            headId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head Dash");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner Dash");
            analystId = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst Dash");

            var cls = new QualityClassification { Name = $"Min-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            arrange.QualityClassifications.Add(cls);
            await arrange.SaveChangesAsync();

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var dev = await devSvc.RaiseAsync(new RaiseDeviationRequest("Dash Dev", "Desc", Utc(2089, 7, 1), null, "Imm", secA, null), null, analystId);
            dev = await devSvc.AssessAsync(dev.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2089, 7, 11), Password), dev.Version, qaId);
            devId = dev.Id;

            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var capa = await capaSvc.CreateAsync(new CreateCapaRequest("Dash CAPA", "Desc", secA, null, null, ownerId), headId);
            capa = await capaSvc.SavePlanAsync(capa.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Do thing", analystId, new DateOnly(2089, 7, 12))],
                "Crit", "Meth", new DateOnly(2089, 7, 15)), capa.Version, ownerId);
            capa = await capaSvc.ApprovePlanAsync(capa.Id, Password, capa.Version, qaId);
            var actionId = capa.Actions[0].Id;
            capa = await capaSvc.CompleteActionAsync(actionId, new CompleteCapaActionRequest("Done", null), capa.Version, analystId);
            await capaSvc.VerifyCompletionAsync(capa.Id, Password, capa.Version, qaId);
            capaId = capa.Id;
        }

        // Advance clock to 2089-07-16 so devId is overdue (due 07-05) and capaId effectiveness check is due (planned 07-15)
        clock.CurrentUtc = Utc(2089, 7, 16);

        await using (var actQa = Context(clock, qaId))
        {
            var dash = await QueryService(actQa, clock).GetDashboardAsync(qaId);
            Assert.Contains(dash.EffectivenessChecksDue, x => x.Id == capaId);
            Assert.Contains(dash.WaitingForMyDecision, x => x.Id == capaId);
            Assert.Contains(dash.OverdueRecords, x => x.Id == devId);
        }

        await using (var actOwner = Context(clock, ownerId))
        {
            var dash = await QueryService(actOwner, clock).GetDashboardAsync(ownerId);
            Assert.Contains(dash.MyOpenRecords, x => x.Id == devId);
            Assert.Contains(dash.OverdueRecords, x => x.Id == devId);
        }
    }

    [PostgresFact]
    public async Task Overdue_worker_creates_one_notification_per_day_per_item_and_none_when_not_overdue()
    {
        var clock = new MutableClock(Utc(2089, 8, 10));
        int secA, qaId, ownerId, analystId, overdueDevId, notOverdueDevId, overdueActionId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Overdue");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner Overdue");
            analystId = await CreateUserAsync(arrange, RoleType.Analyst, secA, "Analyst Overdue");

            var cls = new QualityClassification { Name = $"Ov-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            arrange.QualityClassifications.Add(cls);
            await arrange.SaveChangesAsync();

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            var d1 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Overdue Dev", "Desc", Utc(2089, 8, 1), null, "Imm", secA, null), null, ownerId);
            d1 = await devSvc.AssessAsync(d1.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2089, 8, 11), Password), d1.Version, qaId);
            overdueDevId = d1.Id;

            var d2 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Future Dev", "Desc", Utc(2089, 8, 1), null, "Imm", secA, null), null, ownerId);
            d2 = await devSvc.AssessAsync(d2.Id, new AssessDeviationRequest(true, cls.Id, ownerId, new DateOnly(2089, 8, 25), Password), d2.Version, qaId);
            notOverdueDevId = d2.Id;

            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var capa = await capaSvc.CreateAsync(new CreateCapaRequest("Overdue Action CAPA", "Desc", secA, null, null, ownerId), ownerId);
            capa = await capaSvc.SavePlanAsync(capa.Id, new SaveCapaPlanRequest(
                [new CapaActionInput(QualityActionKind.Corrective, "Overdue Task", analystId, new DateOnly(2089, 8, 11))],
                "Crit", "Meth", new DateOnly(2089, 8, 20)), capa.Version, ownerId);
            capa = await capaSvc.ApprovePlanAsync(capa.Id, Password, capa.Version, qaId);
            overdueActionId = capa.Actions[0].Id;
        }

        // Advance to 2089-08-12: overdueDevId (due 08-05) and overdueActionId (due 08-11) are overdue; notOverdueDevId (due 08-25) is not.
        clock.CurrentUtc = Utc(2089, 8, 12);
        var labClock = new LabClock(clock, TimeZoneInfo.Utc);

        await using var sp = new ServiceCollection()
            .AddScoped<IMicroLimsDbContext>(_ => fixture.CreateDbContext(clock))
            .AddSingleton<TimeProvider>(clock)
            .AddSingleton<ILabClock>(labClock)
            .AddScoped<OosRecipientResolver>()
            .AddScoped<IPermissionRecipientResolver>(sp2 => sp2.GetRequiredService<OosRecipientResolver>())
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .AddScoped<QualityNotifier>()
            .AddSingleton(NullLogger<QualityNotifier>.Instance)
            .BuildServiceProvider();

        var worker = new QualityOverdueWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<QualityOverdueWorker>.Instance);

        await worker.ScanOnceAsync();
        await worker.ScanOnceAsync(); // Second run same day must dedupe!

        await using (var assertDb = Context(clock))
        {
            var recNotifs = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.Type == "QualityRecordOverdue" && n.TargetId == overdueDevId && n.UserId == ownerId)
                .ToListAsync();
            Assert.Single(recNotifs);

            var actNotifs = await assertDb.NotificationLogs.AsNoTracking()
                .Where(n => n.Type == "QualityActionOverdue" && n.UserId == analystId && n.DedupeKey == $"quality:overdue:action:{overdueActionId}:2089-08-12")
                .ToListAsync();
            Assert.Single(actNotifs);

            Assert.False(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.Type == "QualityRecordOverdue" && n.TargetId == notOverdueDevId));
        }

        // Advance to next lab day (2089-08-13): creates 1 new notification for that day
        clock.CurrentUtc = Utc(2089, 8, 13);
        await worker.ScanOnceAsync();

        await using (var assertDb2 = Context(clock))
        {
            var count = await assertDb2.NotificationLogs.AsNoTracking()
                .CountAsync(n => n.Type == "QualityRecordOverdue" && n.TargetId == overdueDevId && n.UserId == ownerId);
            Assert.Equal(2, count);
        }
    }

    private sealed class ThrowingQualityNotifier : IQualityNotifier
    {
        public Task RaisedAsync(int recordId, CancellationToken ct = default) => throw new InvalidOperationException("Boom");
        public Task AssignedAsync(int recordId, CancellationToken ct = default) => throw new InvalidOperationException("Boom");
        public Task ReturnedAsync(int recordId, CancellationToken ct = default) => throw new InvalidOperationException("Boom");
        public Task AwaitingQaAsync(int recordId, CancellationToken ct = default) => throw new InvalidOperationException("Boom");
        public Task ActionAssignedAsync(int recordId, IReadOnlyList<int> actionIds, CancellationToken ct = default) => throw new InvalidOperationException("Boom");
        public Task ClosedAsync(int recordId, CancellationToken ct = default) => throw new InvalidOperationException("Boom");
    }

    [PostgresFact]
    public async Task Notifier_failure_does_not_undo_committed_step_and_events_notify_after_commit()
    {
        var clock = new MutableClock(Utc(2089, 9, 1));
        int secA, qaId, headId, ownerId, devId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Notif");
            headId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head Notif");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Owner Notif");
        }

        // 1. Throwing notifier must NOT fail or roll back RaiseAsync
        await using (var actThrow = Context(clock, headId))
        {
            var svc = new DeviationService(actThrow, Core(actThrow, clock), new PermissionService(actThrow),
                new UserSectionScopeService(actThrow), new SegregationOfDutiesGuard(actThrow),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actThrow),
                new ThrowingQualityNotifier(), NullLogger<DeviationService>.Instance);
            var created = await svc.RaiseAsync(new RaiseDeviationRequest("Survives Notifier Boom", "Desc", Utc(2089, 9, 1), null, "Imm", secA, null), null, headId);
            devId = created.Id;
        }

        await using (var assertDb = Context(clock))
        {
            Assert.True(await assertDb.QualityRecords.AsNoTracking().AnyAsync(r => r.Id == devId));
        }

        // 2. Real QualityNotifier sends Raised notification
        await using var sp = new ServiceCollection()
            .AddScoped<IMicroLimsDbContext>(_ => fixture.CreateDbContext(clock))
            .AddSingleton<TimeProvider>(clock)
            .AddScoped<OosRecipientResolver>()
            .AddScoped<IPermissionRecipientResolver>(sp2 => sp2.GetRequiredService<OosRecipientResolver>())
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .BuildServiceProvider();

        var notifier = new QualityNotifier(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<QualityNotifier>.Instance);
        await using (var actReal = Context(clock, headId))
        {
            var svc = new DeviationService(actReal, Core(actReal, clock), new PermissionService(actReal),
                new UserSectionScopeService(actReal), new SegregationOfDutiesGuard(actReal),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(actReal),
                notifier, NullLogger<DeviationService>.Instance);
            var created = await svc.RaiseAsync(new RaiseDeviationRequest("Real Notif", "Desc", Utc(2089, 9, 1), null, "Imm", secA, null), null, headId);
            devId = created.Id;
        }

        await using (var assertDb = Context(clock))
        {
            Assert.True(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.Type == "QualityRecordRaised" && n.TargetId == devId && n.UserId == qaId));
        }
    }

    [PostgresFact]
    public async Task Oos_notifications_unchanged_after_resolver_generalisation()
    {
        var clock = new MutableClock(Utc(2089, 10, 1));
        int secA, secB, qaId, headA, headB;

        await using (var arrange = Context(clock))
        {
            (secA, secB) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "QA Resolver");
            headA = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Head A Resolver", PermissionConstants.OosManage);
            headB = await CreateUserAsync(arrange, RoleType.SectionHead, secB, "Head B Resolver", PermissionConstants.OosManage);
        }

        await using (var act = Context(clock))
        {
            var oosResolver = new OosRecipientResolver(act, clock);
            IPermissionRecipientResolver genericResolver = oosResolver;

            var oosIds = await oosResolver.ResolveAsync(secA, PermissionConstants.OosManage);
            var genericIds = await genericResolver.ResolveAsync(secA, PermissionConstants.OosManage);

            Assert.Equal(oosIds.OrderBy(x => x), genericIds.OrderBy(x => x));
            Assert.Contains(headA, genericIds);
            Assert.DoesNotContain(headB, genericIds);

            var siteWideQa = await genericResolver.ResolveAsync((int?)null, PermissionConstants.QualityApprove);
            Assert.Contains(qaId, siteWideQa);
        }
    }
}
