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
using MicroLIMS.Application.Abstractions.Pdf;
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
        new(db, Core(db, clock), Links(db, clock), new LabClock(clock, TimeZoneInfo.Utc));

    private static QualityLinkService Links(MicroLimsDbContext db, TimeProvider clock) =>
        new(db, Core(db, clock), new PermissionService(db), new UserSectionScopeService(db), clock);

    private static QualityReportBuilder ReportBuilder(MicroLimsDbContext db, TimeProvider clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var core = Core(db, clock);
        var links = new QualityLinkService(db, core, permissions, scope, clock);
        var query = new QualityQueryService(db, core, links, new LabClock(clock, TimeZoneInfo.Utc));
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

            clock.CurrentUtc = Utc(2089, 3, 15, 18);
            var capaSvc = new CapaService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));
            var capa = await capaSvc.CreateAsync(new CreateCapaRequest("Filter CAPA", "Desc", secB, null, null, ownerB), headB);
            capaId = capa.Id;

            var cancelledDev = await devSvc.RaiseAsync(new RaiseDeviationRequest("Cancelled Dev", "Desc", Utc(2089, 3, 15, 10), null, "Imm", secA, null), null, headA);
            await devSvc.CancelAsync(cancelledDev.Id, "Duplicate", Password, cancelledDev.Version, qaId);
        }

        // Advance clock so devId (due 2089-03-12) is overdue at 2089-03-15
        await using (var act = Context(clock, qaId))
        {
            var svc = QueryService(act, clock);

            var byType = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), Type: QualityRecordType.Deviation), qaId);
            Assert.Contains(byType.Items, x => x.Id == devId);
            Assert.DoesNotContain(byType.Items, x => x.Id == capaId);

            var byStatus = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), Status: "Investigation"), qaId);
            Assert.Contains(byStatus.Items, x => x.Id == devId);
            Assert.DoesNotContain(byStatus.Items, x => x.Id == capaId);

            var byCancelled = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), Status: "Cancelled"), qaId);
            Assert.Contains(byCancelled.Items, x => x.IsCancelled);
            Assert.DoesNotContain(byCancelled.Items, x => x.Id == devId);

            var byClass = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), ClassificationId: classId), qaId);
            Assert.Contains(byClass.Items, x => x.Id == devId);
            Assert.DoesNotContain(byClass.Items, x => x.Id == capaId);

            var bySec = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), SectionId: secB), qaId);
            Assert.Contains(bySec.Items, x => x.Id == capaId);
            Assert.DoesNotContain(bySec.Items, x => x.Id == devId);

            var byOwner = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), OwnerUserId: ownerA), qaId);
            Assert.Contains(byOwner.Items, x => x.Id == devId);

            var byOverdue = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), Overdue: true), qaId);
            Assert.Contains(byOverdue.Items, x => x.Id == devId);
            Assert.DoesNotContain(byOverdue.Items, x => x.Id == capaId);

            var byNotOverdue = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 1, 0), To: Utc(2089, 3, 31, 0), Overdue: false), qaId);
            Assert.Contains(byNotOverdue.Items, x => x.Id == capaId);
            Assert.DoesNotContain(byNotOverdue.Items, x => x.Id == devId);

            // Date-only `To` includes the entire day (capaId raised at 18:00Z on 2089-03-15)
            var byDate = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 14, 0), To: Utc(2089, 3, 15, 0)), qaId);
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

            var equipB = new Equipment
            {
                Code = $"EQ-B-{Guid.NewGuid():N}"[..12],
                Name = "Secret Section-B Centrifuge",
                SectionId = secB
            };
            arrange.Equipment.Add(equipB);
            await arrange.SaveChangesAsync();

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            // Raised by analystA1 in secA, plus a link to equipB (which headA cannot read; label != code) added by qaId
            var d1 = await devSvc.RaiseAsync(new RaiseDeviationRequest("Analyst1 Dev", "Desc", Utc(2089, 4, 1), null, "Imm", secA, null), null, analystA1);
            devA1Id = d1.Id;
            var linkSvc = new QualityLinkService(arrange, Core(arrange, clock), new PermissionService(arrange), new UserSectionScopeService(arrange), clock);
            await linkSvc.AddAsync(devA1Id, QualityLinkTargetType.Equipment, equipB.Id, qaId, d1.Version);

            // Raised by headA in secA (analystA1 is neither raiser, owner, nor action assignee)
            var d2 = await devSvc.RaiseAsync(new RaiseDeviationRequest("HeadA Dev", "Desc", Utc(2089, 4, 1), null, "Imm", secA, null), null, headA);
            devA2Id = d2.Id;
        }

        // Other-section user (headB) sees nothing from secA and is refused on detail and PDF
        await using (var actB = Context(clock, headB))
        {
            var svc = QueryService(actB, clock);
            var list = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 31, 0), To: Utc(2089, 4, 2, 0)), headB);
            Assert.DoesNotContain(list.Items, x => x.Id == devA1Id || x.Id == devA2Id);

            await Assert.ThrowsAsync<NotFoundException>(() => svc.GetDetailAsync(devA1Id, headB));
            await Assert.ThrowsAsync<NotFoundException>(() => ReportBuilder(actB, clock).GeneratePdfAsync(devA1Id, headB));
        }

        // Analyst A1 sees devA1Id (raised by them) but NOT devA2Id
        await using (var actAnalyst1 = Context(clock, analystA1))
        {
            var svc = QueryService(actAnalyst1, clock);
            var list = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 31, 0), To: Utc(2089, 4, 2, 0)), analystA1);
            Assert.Contains(list.Items, x => x.Id == devA1Id);
            Assert.DoesNotContain(list.Items, x => x.Id == devA2Id);

            await Assert.ThrowsAsync<NotFoundException>(() => svc.GetDetailAsync(devA2Id, analystA1));
        }

        // Analyst A2 sees neither
        await using (var actAnalyst2 = Context(clock, analystA2))
        {
            var svc = QueryService(actAnalyst2, clock);
            var list = await svc.ListAsync(new QualityRecordFilter(From: Utc(2089, 3, 31, 0), To: Utc(2089, 4, 2, 0)), analystA2);
            Assert.DoesNotContain(list.Items, x => x.Id == devA1Id || x.Id == devA2Id);
        }

        // Head A can read devA1Id, and the restricted link to equipB shows its code + "Restricted" (no label/route)
        await using (var actHeadA = Context(clock, headA))
        {
            var detail = await QueryService(actHeadA, clock).GetDetailAsync(devA1Id, headA);
            Assert.Equal(1, detail.LinksCount);

            var (_, pdfBytes) = await ReportBuilder(actHeadA, clock).GeneratePdfAsync(devA1Id, headA);
            using var pdf = PdfDocument.Open(pdfBytes);
            var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            Assert.Contains("EQ-B-", text);
            Assert.Contains("Restricted", text);
            Assert.DoesNotContain("Secret Section-B Centrifuge", text);
            Assert.DoesNotContain("/laboratory-configuration/equipment", text);
        }
    }

    [PostgresFact]
    public async Task Detail_and_pdf_include_path_steps_multiple_signers_truncated_links_note_and_export_audit()
    {
        var clock = new MutableClock(Utc(2089, 5, 10, 14));
        int secA, qaId, headId, ownerId, devId, classId, causeId;

        await using (var arrange = Context(clock))
        {
            (secA, _) = await TwoSectionsAsync(arrange);
            qaId = await CreateUserAsync(arrange, RoleType.Qa, secA, "Dr. Nadia QA");
            headId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Tariq Head");
            ownerId = await CreateUserAsync(arrange, RoleType.SectionHead, secA, "Salma Owner");

            var cls = new QualityClassification { Name = $"Major-{Guid.NewGuid():N}"[..16], Order = 1, IsActive = true };
            var cause = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}"[..16], IsActive = true };
            arrange.AddRange(cls, cause);
            await arrange.SaveChangesAsync();
            classId = cls.Id;
            causeId = cause.Id;

            var devSvc = new DeviationService(arrange, Core(arrange, clock), new PermissionService(arrange),
                new UserSectionScopeService(arrange), new SegregationOfDutiesGuard(arrange),
                new LabClock(clock, TimeZoneInfo.Utc), new QualityDueDateService(arrange));

            var raised = await devSvc.RaiseAsync(new RaiseDeviationRequest("PDF Dev", "Desc", Utc(2089, 5, 10, 9), "Lab 1", "Quarantined", secA, null), null, headId);
            var assessed = await devSvc.AssessAsync(raised.Id, new AssessDeviationRequest(true, classId, ownerId, new DateOnly(2089, 5, 20), Password), raised.Version, qaId);
            var investigated = await devSvc.SaveInvestigationAsync(assessed.Id, new SaveDeviationInvestigationRequest("Impact", causeId, "Root", "Prop"), assessed.Version, ownerId);

            clock.CurrentUtc = Utc(2089, 5, 11, 15);
            var labConcluded = await devSvc.SignLabConclusionAsync(investigated.Id, Password, investigated.Version, headId);

            clock.CurrentUtc = Utc(2089, 5, 12, 16);
            var approved = await devSvc.ApproveAsync(labConcluded.Id, new ApproveDeviationRequest(false, null, Password), labConcluded.Version, qaId);

            clock.CurrentUtc = Utc(2089, 5, 13, 17);
            var closed = await devSvc.CloseAsync(approved.Id, Password, approved.Version, qaId);
            devId = closed.Id;
        }

        await using (var act = Context(clock, qaId))
        {
            var detail = await QueryService(act, clock).GetDetailAsync(devId, qaId);
            Assert.NotNull(detail.Deviation);
            Assert.NotEmpty(detail.PathSteps);
            Assert.Equal(4, detail.Timeline.Count);

            var (fileName, bytes) = await ReportBuilder(act, clock).GeneratePdfAsync(devId, qaId);
            Assert.EndsWith(".pdf", fileName);
            using var pdf = PdfDocument.Open(bytes);
            var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            Assert.Contains("Dr. Nadia QA", text);
            Assert.Contains("Tariq Head", text);
            Assert.Contains("2089-05-10 14:00:00 UTC", text);
            Assert.Contains("2089-05-11 15:00:00 UTC", text);
            Assert.Contains("2089-05-12 16:00:00 UTC", text);
            Assert.Contains("2089-05-13 17:00:00 UTC", text);
            Assert.Contains(ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.DeviationAssessed)), text);
            Assert.Contains(ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.QualityLabConclusion)), text);
            Assert.Contains(ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.QualityApproved)), text);
            Assert.Contains(ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.QualityClosed)), text);

            // Truncated links note when LinksCount > resolvedLinks.Count
            var fakeTruncatedDetail = detail with { LinksCount = 5 };
            var fakeLinks = new[] { new QualityLinkDto(1, "SMP-001", "SMP-001", "/samples/1", false) };
            var truncatedDoc = QualityReportBuilder.Build(fakeTruncatedDetail, fakeLinks);
            var listBlocks = truncatedDoc.Blocks.OfType<ListBlock>().ToList();
            Assert.Contains(listBlocks, b => b.Rows.Any(r => r.Right.Contains("4 more linked records not shown")));
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
    public async Task Qa_availability_is_true_when_active_qa_user_exists_and_false_when_none()
    {
        var clock = new MutableClock(Utc(2090, 1, 1));
        int qaId;
        await using (var arrange = Context(clock))
        {
            qaId = await CreateUserAsync(arrange, RoleType.Qa, fixture.SeededSectionId, "Active QA User",
                PermissionConstants.QualityView, PermissionConstants.QualityApprove);
        }

        await using (var act = Context(clock, qaId))
        {
            var available = await QueryService(act, clock).GetQaAvailabilityAsync();
            Assert.True(available.HasActiveQa);
        }

        // Deactivate all active QA users holding Quality.Approve (including any seeded QA users) in a fresh context,
        // then verify HasActiveQa becomes false and restore them afterwards.
        List<int> deactivatedIds;
        await using (var arrange = Context(clock))
        {
            var qaUsers = await arrange.Users
                .Where(u => u.IsActive && u.Role != null && u.Role.IsActive && u.Role.Type == RoleType.Qa &&
                    arrange.RolePermissions.Any(rp => rp.RoleId == u.RoleId && rp.Permission!.Code == PermissionConstants.QualityApprove))
                .ToListAsync();
            deactivatedIds = qaUsers.Select(u => u.Id).ToList();
            foreach (var u in qaUsers)
                u.IsActive = false;
            await arrange.SaveChangesAsync();
        }

        try
        {
            await using var act = Context(clock, qaId);
            var unavailable = await QueryService(act, clock).GetQaAvailabilityAsync();
            Assert.False(unavailable.HasActiveQa);
        }
        finally
        {
            await using var cleanup = Context(clock);
            var toRestore = await cleanup.Users.Where(u => deactivatedIds.Contains(u.Id)).ToListAsync();
            foreach (var u in toRestore)
                u.IsActive = true;
            await cleanup.SaveChangesAsync();
        }
    }
}
