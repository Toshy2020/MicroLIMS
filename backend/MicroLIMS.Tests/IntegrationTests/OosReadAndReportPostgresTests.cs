using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Validators;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using UglyToad.PdfPig;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public sealed class OosReadAndReportPostgresTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private readonly ActionGuardScenarios _seed = new(fixture);

    private static readonly string[] ViewCodes = [PermissionConstants.OosView];
    private static readonly string[] AnalystOosCodes = [PermissionConstants.TestsSelfAssign, PermissionConstants.OosView, PermissionConstants.OosContribute];
    private static readonly string[] HeadOosCodes =
    [
        .. ActionGuardScenarios.HeadCodes,
        PermissionConstants.OosView,
        PermissionConstants.OosContribute,
        PermissionConstants.OosManage
    ];

    private static DateTime Utc(int month, int day, int hour = 9) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task Kpi_oos_rate_and_retest_rate_unchanged_for_fixed_dataset()
    {
        var lab = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, HeadOosCodes, lab);
        var t0 = Utc(10, 1, 8);

        await using (var arrange = fixture.CreateDbContext())
        {
            var cause = await arrange.CausesOfTesting.FirstAsync();
            var s1 = new Sample
            {
                ReferenceNumber = $"KPI-C1-{Guid.NewGuid():N}"[..16],
                ControlNumber = $"CN-C1-{Guid.NewGuid():N}"[..16],
                SampledBy = "qc",
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = head.Id,
                ReceivedAt = t0,
                ApprovedAt = t0.AddDays(2),
                ApprovalDecision = ApprovalDecision.Approve,
                Status = SampleStatus.Approved
            };
            var s2 = new Sample
            {
                ReferenceNumber = $"KPI-C2-{Guid.NewGuid():N}"[..16],
                ControlNumber = $"CN-C2-{Guid.NewGuid():N}"[..16],
                SampledBy = "qc",
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = head.Id,
                ReceivedAt = t0.AddDays(1),
                ApprovalDecision = ApprovalDecision.RetestRetainedSample,
                Status = SampleStatus.RetestRequested,
                OosGroupCode = $"OOS-KPI-{Guid.NewGuid():N}"[..16]
            };
            arrange.Samples.AddRange(s1, s2);
            await arrange.SaveChangesAsync();

            arrange.TestOrders.AddRange(
                new TestOrder { SampleId = s1.Id, SectionId = lab.Id, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved },
                new TestOrder { SampleId = s2.Id, SectionId = lab.Id, TestCode = "TAMC", Status = ApprovalStatus.RetestRequested, CurrentStep = WorkflowStep.Approved });
            arrange.SampleSectionSignoffs.AddRange(
                new SampleSectionSignoff
                {
                    SampleId = s1.Id,
                    SectionId = lab.Id,
                    Status = SectionSignoffStatus.Approved,
                    ApprovedByUserId = head.Id,
                    ApprovedAt = t0.AddDays(2),
                    ApprovalDecision = ApprovalDecision.Approve
                },
                new SampleSectionSignoff
                {
                    SampleId = s2.Id,
                    SectionId = lab.Id,
                    Status = SectionSignoffStatus.Approved,
                    ApprovedByUserId = head.Id,
                    ApprovedAt = t0.AddDays(3),
                    ApprovalDecision = ApprovalDecision.RetestRetainedSample
                });
            await arrange.SaveChangesAsync();
        }

        await using var assertDb = fixture.CreateDbContext();
        var query = new KpiQuery(t0.AddDays(-1), t0.AddDays(10), [lab.Id], null, null, null, null, t0.AddDays(10), head.Id, true);
        var oosRate = await new OosRateKpi(assertDb).ComputeAsync(query, CancellationToken.None);
        var retestRate = await new RetestRateKpi(assertDb).ComputeAsync(query, CancellationToken.None);

        Assert.Equal(0.5m, oosRate.Value);
        Assert.Equal(0.5m, retestRate.Value);
    }

    [PostgresFact]
    public async Task List_filters_and_paging_clamp_work()
    {
        var labA = await _seed.SectionAsync();
        var labB = await _seed.SectionAsync();
        var viewer = await _seed.UserAsync(RoleType.QcManager, ViewCodes, labA, labB);
        var now = Utc(11, 10, 10);

        int openOosId, concludedOosId, overdueExcursionId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var s1 = await _seed.SampleAsync(labA, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed);
            var s2 = await _seed.SampleAsync(labA, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed);
            var s3 = await _seed.SampleAsync(labB, SampleStatus.InTesting, ApprovalStatus.Pending, WorkflowStep.Running);

            var inv1 = new OosInvestigation
            {
                Number = $"OOS-F1-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = s1.Sample.Id,
                SectionId = labA.Id,
                OpenedAtUtc = now.AddDays(-3),
                OpenedByUserId = viewer.Id,
                PhaseOneDueDate = DateOnly.FromDateTime(now.AddDays(2)),
                DueDate = DateOnly.FromDateTime(now.AddDays(5)),
                TemplateSnapshotJson = """{"items":[]}"""
            };
            var inv2 = new OosInvestigation
            {
                Number = $"OOS-F2-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.Concluded,
                Conclusion = OosConclusion.Confirmed,
                SampleId = s2.Sample.Id,
                SectionId = labA.Id,
                OpenedAtUtc = now.AddDays(-1),
                OpenedByUserId = viewer.Id,
                ConcludedAtUtc = now,
                ConcludedByUserId = viewer.Id,
                TemplateSnapshotJson = """{"items":[]}"""
            };
            var inv3 = new OosInvestigation
            {
                Number = $"OOS-F3-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Excursion,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = s3.Sample.Id,
                SectionId = labB.Id,
                OpenedAtUtc = now.AddDays(-5),
                OpenedByUserId = viewer.Id,
                PhaseOneDueDate = DateOnly.FromDateTime(now.AddDays(-1)),
                DueDate = DateOnly.FromDateTime(now.AddDays(2)),
                TemplateSnapshotJson = """{"items":[]}"""
            };
            arrange.OosInvestigations.AddRange(inv1, inv2, inv3);
            await arrange.SaveChangesAsync();
            openOosId = inv1.Id;
            concludedOosId = inv2.Id;
            overdueExcursionId = inv3.Id;
        }

        await using var act = fixture.CreateDbContext();
        var clock = new FixedTimeProvider(now);
        var svc = TestServiceFactory.OosInvestigation(act, clock, viewer.Id);

        var clampedHigh = await svc.ListAsync(new OosInvestigationFilter(Page: 1, PageSize: 500));
        Assert.Equal(100, clampedHigh.PageSize);
        Assert.Equal(3, clampedHigh.TotalCount);

        var clampedLow = await svc.ListAsync(new OosInvestigationFilter(Page: 0, PageSize: 0));
        Assert.Equal(1, clampedLow.Page);
        Assert.Equal(25, clampedLow.PageSize);
        Assert.Equal(3, clampedLow.Items.Count);

        var onlyConcluded = await svc.ListAsync(new OosInvestigationFilter(Status: OosInvestigationStatus.Concluded));
        Assert.Equal(concludedOosId, Assert.Single(onlyConcluded.Items).Id);

        var onlyExcursion = await svc.ListAsync(new OosInvestigationFilter(Kind: OosInvestigationKind.Excursion));
        Assert.Equal(overdueExcursionId, Assert.Single(onlyExcursion.Items).Id);

        var onlyLabA = await svc.ListAsync(new OosInvestigationFilter(SectionId: labA.Id));
        Assert.Equal(2, onlyLabA.TotalCount);
        Assert.DoesNotContain(onlyLabA.Items, x => x.Id == overdueExcursionId);

        var onlyOverdue = await svc.ListAsync(new OosInvestigationFilter(Overdue: true));
        Assert.Equal(overdueExcursionId, Assert.Single(onlyOverdue.Items).Id);

        var dateWindow = await svc.ListAsync(new OosInvestigationFilter(
            From: DateTime.SpecifyKind(now.AddDays(-4), DateTimeKind.Unspecified),
            To: DateTime.SpecifyKind(now.AddDays(-2), DateTimeKind.Unspecified)));
        Assert.Equal(openOosId, Assert.Single(dateWindow.Items).Id);
    }

    [PostgresFact]
    public async Task Section_scope_other_section_returns_not_found()
    {
        var labA = await _seed.SectionAsync();
        var labB = await _seed.SectionAsync();
        var viewerA = await _seed.UserAsync(RoleType.QcManager, ViewCodes, labA);

        int invBId;
        int sampleBId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var sB = await _seed.SampleAsync(labB, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed);
            sampleBId = sB.Sample.Id;
            var invB = new OosInvestigation
            {
                Number = $"OOS-SB-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = sB.Sample.Id,
                SectionId = labB.Id,
                OpenedAtUtc = Utc(11, 10),
                OpenedByUserId = viewerA.Id,
                TemplateSnapshotJson = """{"items":[]}"""
            };
            arrange.OosInvestigations.Add(invB);
            await arrange.SaveChangesAsync();
            invBId = invB.Id;
        }

        await using var act = fixture.CreateDbContext();
        var svc = TestServiceFactory.OosInvestigation(act, actor: viewerA.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetByIdAsync(invBId));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetFactsAsync(invBId));
        Assert.Empty((await svc.ListAsync(new OosInvestigationFilter())).Items);
        Assert.Empty(await svc.GetForSampleAsync(sampleBId));
        await Assert.ThrowsAsync<NotFoundException>(() => ReportBuilder(act, svc).GeneratePdfAsync(invBId));
        Assert.Empty((await Trends(act).GetTrendsAsync("conclusion", sectionId: labB.Id)).Groups);
    }

    [PostgresFact]
    public async Task Analyst_without_manage_sees_only_investigations_with_test_they_performed()
    {
        var lab = await _seed.SectionAsync();
        var analyst1 = await _seed.UserAsync(RoleType.Analyst, AnalystOosCodes, lab);
        var analyst2 = await _seed.UserAsync(RoleType.Analyst, AnalystOosCodes, lab);

        int inv1Id, inv2Id, sample1Id, sample2Id;
        await using (var arrange = fixture.CreateDbContext())
        {
            var s1 = await _seed.SampleAsync(lab, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed, analystId: analyst1.Id);
            var s2 = await _seed.SampleAsync(lab, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed, analystId: analyst2.Id);
            sample1Id = s1.Sample.Id;
            sample2Id = s2.Sample.Id;

            var inv1 = new OosInvestigation
            {
                Number = $"OOS-A1-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = s1.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = Utc(11, 10),
                OpenedByUserId = analyst1.Id,
                TemplateSnapshotJson = """{"items":[]}""",
                Results = { new OosInvestigationResult { TestOrderId = s1.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.Fails, ResultSummaryAtOpening = "OOS", AddedAtUtc = Utc(11, 10) } }
            };
            var inv2 = new OosInvestigation
            {
                Number = $"OOS-A2-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = s2.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = Utc(11, 10),
                OpenedByUserId = analyst2.Id,
                TemplateSnapshotJson = """{"items":[]}""",
                Results = { new OosInvestigationResult { TestOrderId = s2.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.Fails, ResultSummaryAtOpening = "OOS", AddedAtUtc = Utc(11, 10) } }
            };
            arrange.OosInvestigations.AddRange(inv1, inv2);
            await arrange.SaveChangesAsync();
            inv1Id = inv1.Id;
            inv2Id = inv2.Id;
        }

        await using var act = fixture.CreateDbContext();
        var svc = TestServiceFactory.OosInvestigation(act, actor: analyst1.Id);

        var list = await svc.ListAsync(new OosInvestigationFilter());
        Assert.Equal(inv1Id, Assert.Single(list.Items).Id);
        Assert.Equal(inv1Id, (await svc.GetByIdAsync(inv1Id)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetByIdAsync(inv2Id));
        Assert.Equal(inv1Id, Assert.Single(await svc.GetForSampleAsync(sample1Id)).Id);
        Assert.Empty(await svc.GetForSampleAsync(sample2Id));
        await Assert.ThrowsAsync<NotFoundException>(() => ReportBuilder(act, svc).GeneratePdfAsync(inv2Id));
        var trend = Assert.Single((await Trends(act).GetTrendsAsync("conclusion", sectionId: lab.Id)).Groups);
        Assert.Equal(1, trend.TotalCount);
    }

    [PostgresFact]
    public async Task Detail_timeline_and_pdf_include_signer_name_utc_time_with_zone_meaning_and_export_audit()
    {
        var lab = await _seed.SectionAsync();
        var analyst = await _seed.UserAsync(RoleType.Analyst, AnalystOosCodes, lab);
        var head = await _seed.UserAsync(RoleType.SectionHead, HeadOosCodes, lab);
        var t0 = Utc(11, 1, 9);

        int oosId, excId, categoryId;
        await using (var arrange = fixture.CreateDbContext())
        {
            if (!await arrange.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
                arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
            var cat = new OosCauseCategory { Name = $"Cat-{Guid.NewGuid():N}"[..16], IsActive = true };
            arrange.OosCauseCategories.Add(cat);
            await arrange.SaveChangesAsync();
            categoryId = cat.Id;

            var sOos = await _seed.SampleAsync(lab, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed, analystId: analyst.Id);
            var sExc = await _seed.SampleAsync(lab, SampleStatus.InTesting, ApprovalStatus.Pending, WorkflowStep.Running, analystId: analyst.Id);

            var inv = new OosInvestigation
            {
                Number = $"OOS-TL-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = sOos.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = t0,
                OpenedByUserId = head.Id,
                TemplateSnapshotJson = """{"items":[{"phase":1,"key":"c1","order":1,"text":"Check 1"}]}""",
                Results = { new OosInvestigationResult { TestOrderId = sOos.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.Fails, ResultSummaryAtOpening = "OOS", AddedAtUtc = t0 } }
            };
            var exc = new OosInvestigation
            {
                Number = $"EXC-TL-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Excursion,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = sExc.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = t0,
                OpenedByUserId = head.Id,
                TemplateSnapshotJson = """{"items":[{"phase":1,"key":"e1","order":1,"text":"Excursion check"}]}""",
                Results = { new OosInvestigationResult { TestOrderId = sExc.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.ActionLimitExceeded, ResultSummaryAtOpening = "Action", AddedAtUtc = t0 } }
            };
            arrange.OosInvestigations.AddRange(inv, exc);
            await arrange.SaveChangesAsync();
            oosId = inv.Id;
            excId = exc.Id;
        }

        // Phase I -> Retest plan -> Conclude -> Reopen
        await using (var act = fixture.CreateDbContext())
        {
            var svc = TestServiceFactory.OosInvestigation(act, new FixedTimeProvider(t0.AddHours(1)), analyst.Id);
            await svc.AddAnalystStatementAsync(oosId, "Statement", analyst.Id);
            await svc.AnswerChecklistAsync(oosId, 1, "c1", OosChecklistAnswer.Yes, "OK", analyst.Id);
        }

        uint v;
        int orderId;
        await using (var read = fixture.CreateDbContext())
        {
            v = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == oosId)).Version;
            orderId = (await read.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.InvestigationId == oosId)).TestOrderId;
        }

        await using (var act = fixture.CreateDbContext())
        {
            await TestServiceFactory.OosInvestigation(act, new FixedTimeProvider(t0.AddHours(2)), head.Id)
                .SignPhaseOneAsync(oosId, new SignPhaseOneRequest(OosPhaseOneAssessment.NoLaboratoryErrorFound, null, null, ActionGuardScenarios.Password), v, head.Id, null);
        }

        await using (var read = fixture.CreateDbContext())
            v = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == oosId)).Version;

        int planId;
        await using (var act = fixture.CreateDbContext())
        {
            var tPlan = new FixedTimeProvider(t0.AddHours(3));
            var planSvc = new OosRetestPlanService(
                act,
                TestServiceFactory.OosInvestigation(act, tPlan, head.Id),
                new UserSectionScopeService(act),
                new SegregationOfDutiesGuard(act),
                new ElectronicSignatureService(act, tPlan),
                new RetestSampleFactory(act, new ReferenceNumberGenerator(act, tPlan), tPlan),
                tPlan);
            var planDto = await planSvc.DraftAsync(oosId, new SaveRetestPlanRequest(OosRetestSource.RetainedSample, 1, [orderId], [analyst.Id], "Retest once", null), v, head.Id);
            planId = planDto.Id;
        }

        await using (var read = fixture.CreateDbContext())
            v = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == oosId)).Version;

        await using (var act = fixture.CreateDbContext())
        {
            var tPlan = new FixedTimeProvider(t0.AddHours(3));
            var planSvc = new OosRetestPlanService(
                act,
                TestServiceFactory.OosInvestigation(act, tPlan, head.Id),
                new UserSectionScopeService(act),
                new SegregationOfDutiesGuard(act),
                new ElectronicSignatureService(act, tPlan),
                new RetestSampleFactory(act, new ReferenceNumberGenerator(act, tPlan), tPlan),
                tPlan);
            await planSvc.SignAndCreateAsync(planId, ActionGuardScenarios.Password, v, head.Id, null);
        }

        // Review the retest sample so conclusion is allowed
        await using (var act = fixture.CreateDbContext())
        {
            var retestSampleId = await act.OosRetestPlanSamples.AsNoTracking().Where(l => l.PlanId == planId).Select(l => l.SampleId).SingleAsync();
            var retestSample = await act.Samples.SingleAsync(s => s.Id == retestSampleId);
            retestSample.Status = SampleStatus.UnderApproval;
            act.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = retestSampleId,
                SectionId = lab.Id,
                Status = SectionSignoffStatus.UnderApproval
            });
            await act.SaveChangesAsync();
        }

        await using (var read = fixture.CreateDbContext())
            v = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == oosId)).Version;

        await using (var act = fixture.CreateDbContext())
        {
            await TestServiceFactory.OosInvestigation(act, new FixedTimeProvider(t0.AddHours(4)), head.Id)
                .ConcludeAsync(oosId, new ConcludeRequest(OosConclusion.Confirmed, categoryId, "Cause", "Summary", ActionGuardScenarios.Password), v, head.Id, null);
        }

        await using (var read = fixture.CreateDbContext())
            v = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == oosId)).Version;

        await using (var act = fixture.CreateDbContext())
        {
            act.ExpectedVersion = v;
            await TestServiceFactory.OosInvestigation(act, new FixedTimeProvider(t0.AddHours(5)), head.Id)
                .ReopenAsync(oosId, "Reopen reason", ActionGuardScenarios.Password, head.Id, null);
        }

        // Excursion close
        await using (var act = fixture.CreateDbContext())
        {
            await TestServiceFactory.OosInvestigation(act, new FixedTimeProvider(t0.AddHours(6)), analyst.Id)
                .AnswerChecklistAsync(excId, 1, "e1", OosChecklistAnswer.Yes, "OK", analyst.Id);
        }

        uint excV;
        await using (var read = fixture.CreateDbContext())
            excV = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == excId)).Version;

        await using (var act = fixture.CreateDbContext())
        {
            await TestServiceFactory.OosInvestigation(act, new FixedTimeProvider(t0.AddHours(7)), head.Id)
                .CloseExcursionAsync(excId, new CloseExcursionRequest(categoryId, "Cause", "Action", ActionGuardScenarios.Password), excV, head.Id, null);
        }

        // Assert DTO timelines and PDF + DataExportLog
        await using var assertAct = fixture.CreateDbContext();
        var readSvc = TestServiceFactory.OosInvestigation(assertAct, actor: head.Id);
        var oosDetail = await readSvc.GetByIdAsync(oosId);
        Assert.Equal(4, oosDetail.Timeline.Count);
        Assert.Contains(oosDetail.Timeline, e => e.MeaningText == "Phase I concluded" && e.SignerFullName == head.FullName && e.SignedAtUtcText.Contains("UTC"));
        Assert.Contains(oosDetail.Timeline, e => e.MeaningText == "Retest plan approved" && e.SignerFullName == head.FullName && e.SignedAtUtcText.Contains("UTC"));
        Assert.Contains(oosDetail.Timeline, e => e.MeaningText == "Investigation concluded" && e.SignerFullName == head.FullName && e.SignedAtUtcText.Contains("UTC"));
        Assert.Contains(oosDetail.Timeline, e => e.MeaningText == "Investigation reopened" && e.SignerFullName == head.FullName && e.SignedAtUtcText.Contains("UTC"));

        var excDetail = await readSvc.GetByIdAsync(excId);
        var excSig = Assert.Single(excDetail.Timeline);
        Assert.Equal("Excursion review closed", excSig.MeaningText);
        Assert.Equal(head.FullName, excSig.SignerFullName);
        Assert.Contains("UTC", excSig.SignedAtUtcText);

        var reportBuilder = new OosInvestigationReportBuilder(readSvc, new PdfGenerator(), new DataExportAuditService(assertAct), assertAct);
        var (_, pdfBytes) = await reportBuilder.GeneratePdfAsync(oosId);
        using var pdf = PdfDocument.Open(pdfBytes);
        var pdfText = string.Join("\n", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
        Assert.Contains(head.FullName, pdfText);
        Assert.Contains("UTC", pdfText);
        Assert.Contains("Phase I concluded", pdfText);
        Assert.Contains("Retest plan approved", pdfText);
        Assert.Contains("Investigation concluded", pdfText);
        Assert.Contains("Investigation reopened", pdfText);

        await using var assertDb = fixture.CreateDbContext();
        var exportLog = await assertDb.DataExportLogs.AsNoTracking()
            .Where(l => l.UserId == head.Id && l.ExportType == "OosInvestigationPdf")
            .OrderByDescending(l => l.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(exportLog);
        Assert.Contains(oosDetail.Number, exportLog.FilterJson);
    }

    [PostgresFact]
    public async Task Certificate_row_shows_investigation_number_and_never_invalidated_value_and_ignores_excursions()
    {
        var lab = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, HeadOosCodes, lab);
        var t0 = Utc(11, 2);

        int originId;
        string oosNumber;
        await using (var arrange = fixture.CreateDbContext())
        {
            var sOrigin = await _seed.SampleAsync(lab, SampleStatus.Approved, ApprovalStatus.Approved, WorkflowStep.Approved, testCode: "TAMC");
            var sRetest = await _seed.SampleAsync(lab, SampleStatus.Approved, ApprovalStatus.Approved, WorkflowStep.Approved, testCode: "TAMC");
            originId = sOrigin.Sample.Id;

            var originOrder = await arrange.TestOrders.SingleAsync(o => o.Id == sOrigin.Orders[0].Id);
            var originSample = await arrange.Samples.SingleAsync(s => s.Id == originId);
            var retestSample = await arrange.Samples.SingleAsync(s => s.Id == sRetest.Sample.Id);
            originOrder.IsSuperseded = true;
            retestSample.OriginSampleId = originId;
            originSample.OosGroupCode = $"OOS-CERT-{Guid.NewGuid():N}"[..16];
            retestSample.OosGroupCode = originSample.OosGroupCode;
            oosNumber = originSample.OosGroupCode;

            arrange.CountTestReadings.AddRange(
                new CountTestReading
                {
                    TestOrderId = originOrder.Id,
                    PlateReadings = "999",
                    DilutionFactor = 1,
                    ReportedResult = "999 CFU/g",
                    Status = ResultStatus.OutOfSpecification,
                    EnteredByUserId = head.Id,
                    EnteredAt = t0
                },
                new CountTestReading
                {
                    TestOrderId = sRetest.Orders[0].Id,
                    PlateReadings = "10",
                    DilutionFactor = 1,
                    ReportedResult = "10 CFU/g",
                    Status = ResultStatus.WithinLimits,
                    EnteredByUserId = head.Id,
                    EnteredAt = t0.AddHours(2)
                });

            var inv = new OosInvestigation
            {
                Number = oosNumber,
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.Concluded,
                Conclusion = OosConclusion.Invalidated,
                SampleId = originId,
                SectionId = lab.Id,
                OpenedAtUtc = t0,
                OpenedByUserId = head.Id,
                ConcludedAtUtc = t0.AddHours(3),
                ConcludedByUserId = head.Id,
                TemplateSnapshotJson = """{"items":[]}""",
                Results = { new OosInvestigationResult { TestOrderId = originOrder.Id, OutcomeAtOpening = OosOutcomeClass.Fails, ResultSummaryAtOpening = "999 CFU/g", AddedAtUtc = t0, IsInvalidated = true, InvalidatedAtUtc = t0.AddHours(3) } }
            };
            var exc = new OosInvestigation
            {
                Number = $"EXC-C-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Excursion,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = originId,
                SectionId = lab.Id,
                OpenedAtUtc = t0,
                OpenedByUserId = head.Id,
                TemplateSnapshotJson = """{"items":[]}""",
                Results = { new OosInvestigationResult { TestOrderId = sRetest.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.ActionLimitExceeded, ResultSummaryAtOpening = "10 CFU/g", AddedAtUtc = t0 } }
            };
            arrange.OosInvestigations.AddRange(inv, exc);
            await arrange.SaveChangesAsync();
        }

        await using var act = fixture.CreateDbContext();
        var summary = await TestServiceFactory.SampleSummary(act).GetCertificateSummaryAsync(originId, [lab.Id]);
        Assert.NotNull(summary);

        var rows = summary.Certificate.Sample.Simple!.Rows;
        var row = Assert.Single(rows);
        Assert.Equal("10 CFU/g", row.Result);
        Assert.NotEqual("999 CFU/g", row.Result);
        Assert.Equal(oosNumber, row.OosInvestigationNumber);

        var doc = ReportDocumentMapper.ForSample(summary);
        var cards = doc.Blocks.OfType<CardBlock>().ToList();
        Assert.Contains(cards, c => c.Subtitle.Contains(oosNumber));
        Assert.DoesNotContain(cards, c => c.Subtitle.Contains("EXC-C-"));
    }

    [PostgresFact]
    public async Task Legacy_groups_are_listed_separately_from_investigations()
    {
        var lab = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, HeadOosCodes, lab);
        var legacyCode = $"OOS-LEG-{Guid.NewGuid():N}"[..16];
        var newCode = $"OOS-NEW-{Guid.NewGuid():N}"[..16];

        await using (var arrange = fixture.CreateDbContext())
        {
            var legOrigin = await _seed.SampleAsync(lab, SampleStatus.RetestRequested, ApprovalStatus.Approved, WorkflowStep.Approved);
            var legRetest = await _seed.SampleAsync(lab, SampleStatus.InTesting, ApprovalStatus.Pending, WorkflowStep.Running);
            var legOriginRow = await arrange.Samples.SingleAsync(s => s.Id == legOrigin.Sample.Id);
            var legRetestRow = await arrange.Samples.SingleAsync(s => s.Id == legRetest.Sample.Id);
            legOriginRow.OosGroupCode = legacyCode;
            legRetestRow.OriginSampleId = legOriginRow.Id;
            legRetestRow.OosGroupCode = legacyCode;

            var newOrigin = await _seed.SampleAsync(lab, SampleStatus.UnderApproval, ApprovalStatus.Reviewed, WorkflowStep.Reviewed);
            var newOriginRow = await arrange.Samples.SingleAsync(s => s.Id == newOrigin.Sample.Id);
            newOriginRow.OosGroupCode = newCode;
            var newRetest = await _seed.SampleAsync(lab, SampleStatus.InTesting, ApprovalStatus.Pending, WorkflowStep.Running);
            var newRetestRow = await arrange.Samples.SingleAsync(s => s.Id == newRetest.Sample.Id);
            newRetestRow.OriginSampleId = newOriginRow.Id;
            newRetestRow.OosGroupCode = newCode;
            arrange.OosInvestigations.Add(new OosInvestigation
            {
                Number = newCode,
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = newOrigin.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = Utc(11, 3),
                OpenedByUserId = head.Id,
                TemplateSnapshotJson = """{"items":[]}"""
            });
            await arrange.SaveChangesAsync();
        }

        await using var act = fixture.CreateDbContext();
        var legacyGroups = await new OosTrackingService(act).GetOosGroupsAsync([lab.Id]);
        Assert.Contains(legacyGroups, g => g.OosGroupCode == legacyCode);
        Assert.DoesNotContain(legacyGroups, g => g.OosGroupCode == newCode);
    }

    [PostgresFact]
    public async Task Trends_work_for_each_group_by_and_compute_median_working_days()
    {
        var lab = await _seed.SectionAsync();
        var analyst = await _seed.UserAsync(RoleType.Analyst, AnalystOosCodes, lab);
        var head = await _seed.UserAsync(RoleType.SectionHead, HeadOosCodes, lab);
        var mon = Utc(11, 2, 9); // Monday

        await using (var arrange = fixture.CreateDbContext())
        {
            var item = new Item { Name = $"Item-{Guid.NewGuid():N}"[..16], Code = $"IT-{Guid.NewGuid():N}"[..12], IsActive = true };
            var equip = new Equipment { Name = $"Eq-{Guid.NewGuid():N}"[..14], Code = $"EQ-{Guid.NewGuid():N}"[..12], Type = EquipmentType.Hplc, SectionId = lab.Id };
            var cat = new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}"[..16], IsActive = true };
            arrange.Items.Add(item);
            arrange.Equipment.Add(equip);
            arrange.OosCauseCategories.Add(cat);
            await arrange.SaveChangesAsync();

            var s1 = await _seed.SampleAsync(lab, SampleStatus.Rejected, ApprovalStatus.Approved, WorkflowStep.Approved, orders: 2, analystId: analyst.Id, testCode: "TAMC");
            var s2 = await _seed.SampleAsync(lab, SampleStatus.Approved, ApprovalStatus.Approved, WorkflowStep.Approved, analystId: analyst.Id, testCode: "TAMC");
            var s1Row = await arrange.Samples.SingleAsync(s => s.Id == s1.Sample.Id);
            var s2Row = await arrange.Samples.SingleAsync(s => s.Id == s2.Sample.Id);
            s1Row.ItemId = item.Id;
            s2Row.ItemId = item.Id;
            var sig = new ElectronicSignature
            {
                UserId = analyst.Id,
                UserFullNameSnapshot = analyst.FullName,
                UsernameSnapshot = analyst.Username,
                RoleSnapshot = "Analyst",
                EntityType = nameof(TestAnalysis),
                EntityId = s1.Orders[0].Id,
                MeaningOfSignature = SignatureMeaning.Reviewed,
                SignedAt = mon
            };
            arrange.ElectronicSignatures.Add(sig);
            await arrange.SaveChangesAsync();
            arrange.TestAnalyses.Add(new TestAnalysis
            {
                TestOrderId = s1.Orders[0].Id,
                AnalysisType = WorkflowType.Titration,
                EquipmentId = equip.Id,
                AnalysedAt = mon,
                EnteredByUserId = analyst.Id,
                EnteredAt = mon,
                SignatureId = sig.Id
            });

            arrange.OosInvestigations.AddRange(
                new OosInvestigation
                {
                    Number = $"OOS-T1-{Guid.NewGuid():N}"[..16],
                    Kind = OosInvestigationKind.Oos,
                    Status = OosInvestigationStatus.Concluded,
                    Conclusion = OosConclusion.Confirmed,
                    RootCauseCategoryId = cat.Id,
                    SampleId = s1.Sample.Id,
                    SectionId = lab.Id,
                    OpenedAtUtc = mon,
                    OpenedByUserId = head.Id,
                    ConcludedAtUtc = mon.AddDays(2), // +2 working days
                    ConcludedByUserId = head.Id,
                    TemplateSnapshotJson = """{"items":[]}""",
                    Results = s1.Orders.Select(order => new OosInvestigationResult
                    {
                        TestOrderId = order.Id, OutcomeAtOpening = OosOutcomeClass.Fails,
                        ResultSummaryAtOpening = "OOS", AddedAtUtc = mon
                    }).ToList()
                },
                new OosInvestigation
                {
                    Number = $"OOS-T2-{Guid.NewGuid():N}"[..16],
                    Kind = OosInvestigationKind.Oos,
                    Status = OosInvestigationStatus.Concluded,
                    Conclusion = OosConclusion.Invalidated,
                    RootCauseCategoryId = cat.Id,
                    SampleId = s2.Sample.Id,
                    SectionId = lab.Id,
                    OpenedAtUtc = mon,
                    OpenedByUserId = head.Id,
                    ConcludedAtUtc = mon.AddDays(4), // Friday is non-working, so +3 working days
                    ConcludedByUserId = head.Id,
                    TemplateSnapshotJson = """{"items":[]}""",
                    Results = { new OosInvestigationResult { TestOrderId = s2.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.Fails, ResultSummaryAtOpening = "OOS", AddedAtUtc = mon } }
                });
            await arrange.SaveChangesAsync();
        }

        await using var act = fixture.CreateDbContext();
        act.CurrentUserId = head.Id;
        var trends = Trends(act);
        var from = DateTime.SpecifyKind(mon.AddDays(-1), DateTimeKind.Unspecified);
        var to = DateTime.SpecifyKind(mon.AddDays(10), DateTimeKind.Unspecified);

        foreach (var groupBy in new[] { "product", "test", "analyst", "instrument", "cause", "conclusion" })
        {
            var result = await trends.GetTrendsAsync(groupBy, from, to);
            Assert.Equal(DateTimeKind.Utc, result.FromUtc?.Kind);
            Assert.Equal(DateTimeKind.Utc, result.ToUtc?.Kind);
            if (groupBy == "conclusion")
            {
                Assert.Collection(result.Groups,
                    g => { Assert.Equal(1, g.ConfirmedCount); Assert.Equal(2.0, g.MedianWorkingDaysToConclude); },
                    g => { Assert.Equal(1, g.InvalidatedCount); Assert.Equal(3.0, g.MedianWorkingDaysToConclude); });
            }
            else
            {
                var group = Assert.Single(result.Groups);
                Assert.Equal(groupBy == "instrument" ? 1 : 2, group.TotalCount);
                Assert.Equal(groupBy == "instrument" ? 2.0 : 2.5, group.MedianWorkingDaysToConclude);
            }
        }
    }

    [PostgresFact]
    public async Task Document_reads_apply_investigation_scope_and_legacy_group_sections()
    {
        var lab = await _seed.SectionAsync();
        var otherLab = await _seed.SectionAsync();
        var viewer = await _seed.UserAsync(RoleType.QcManager, ViewCodes, otherLab);
        var inScope = await _seed.UserAsync(RoleType.QcManager, ViewCodes, lab);
        var analyst = await _seed.UserAsync(RoleType.Analyst, AnalystOosCodes, lab);
        var sample = await _seed.SampleAsync(lab, SampleStatus.InTesting, ApprovalStatus.Pending, WorkflowStep.Running);
        var legacy = await _seed.SampleAsync(lab, SampleStatus.InTesting, ApprovalStatus.Pending, WorkflowStep.Running);
        var code = $"OOS-N-{Guid.NewGuid():N}"[..16];
        var legacyCode = $"OOS-L-{Guid.NewGuid():N}"[..16];
        var bytes = "%PDF-1.4 test document"u8.ToArray();
        var storage = new InMemoryFileStorageService();
        var documentIds = new Dictionary<string, int>();
        await using (var arrange = fixture.CreateDbContext())
        {
            (await arrange.Samples.SingleAsync(s => s.Id == sample.Sample.Id)).OosGroupCode = code;
            (await arrange.Samples.SingleAsync(s => s.Id == legacy.Sample.Id)).OosGroupCode = legacyCode;
            var opened = Utc(11, 6);
            arrange.OosInvestigations.Add(new OosInvestigation
            {
                Number = code,
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = sample.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = opened,
                OpenedByUserId = inScope.Id,
                TemplateSnapshotJson = """{"items":[]}""",
                Results = { new OosInvestigationResult { TestOrderId = sample.Orders[0].Id, OutcomeAtOpening = OosOutcomeClass.Fails, ResultSummaryAtOpening = "OOS", AddedAtUtc = opened } }
            });
            foreach (var group in new[] { code, legacyCode })
            {
                var document = new OosInvestigationDocument
                {
                    OosGroupCode = group,
                    OriginalFileName = "Investigation.pdf",
                    StorageKey = await storage.SaveAsync(group, bytes),
                    FileExtension = ".pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = bytes.Length,
                    ContentSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                    UploadedByUserId = inScope.Id,
                    UploadedAt = opened
                };
                arrange.OosInvestigationDocuments.Add(document);
                await arrange.SaveChangesAsync();
                documentIds[group] = document.Id;
            }
        }

        foreach (var (actor, group) in new[] { (viewer, code), (analyst, code), (viewer, legacyCode) })
        {
            await using var act = fixture.CreateDbContext();
            var controller = Documents(act, storage, actor.Id);
            await Assert.ThrowsAsync<NotFoundException>(() => controller.GetDocuments(group));
            await Assert.ThrowsAsync<NotFoundException>(() => controller.GetContent(documentIds[group], group));
        }

        await using var allowed = fixture.CreateDbContext();
        var allowedController = Documents(allowed, storage, inScope.Id);
        foreach (var group in documentIds.Keys)
        {
            var response = Assert.IsType<OkObjectResult>(await allowedController.GetDocuments(group));
            var data = Assert.IsType<MicroLIMS.Shared.Responses.ApiResponse<object>>(response.Value);
            var docs = Assert.IsType<List<OosInvestigationDocumentDto>>(data.Data);
            Assert.Equal(documentIds[group], Assert.Single(docs).Id);
            var file = Assert.IsType<FileContentResult>(await allowedController.GetContent(documentIds[group], group));
            Assert.Equal(bytes, file.FileContents);
        }
        await Assert.ThrowsAsync<NotFoundException>(() => allowedController.GetDocuments("missing-group"));
        storage.Files[code] = [0];
        await Assert.ThrowsAsync<BusinessRuleException>(() => allowedController.GetContent(documentIds[code], code));
    }

    [PostgresFact]
    public Task Certificate_omits_a_non_superseded_invalidated_simple_result_without_a_replacement() =>
        AssertCertificateHasNoReportableResultsAsync(locationBased: false);

    [PostgresFact]
    public Task Certificate_omits_a_non_superseded_invalidated_location_result_without_a_replacement() =>
        AssertCertificateHasNoReportableResultsAsync(locationBased: true);

    private async Task AssertCertificateHasNoReportableResultsAsync(bool locationBased)
    {
        var lab = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, HeadOosCodes, lab);
        var sample = await _seed.SampleAsync(lab, SampleStatus.Approved, ApprovalStatus.Approved, WorkflowStep.Approved,
            signoff: SectionSignoffStatus.Approved, testCode: "TAMC");
        var orderId = sample.Orders[0].Id;
        var measuredAt = Utc(11, 6);
        const string invalidatedValue = "999 CFU/g";
        await using (var arrange = fixture.CreateDbContext())
        {
            if (locationBased)
            {
                arrange.SampleLocations.Add(new SampleLocation
                {
                    SampleId = sample.Sample.Id, TestOrderId = orderId, LocationType = LocationType.Room,
                    ReportedResult = invalidatedValue, Status = ResultStatus.OutOfSpecification,
                    EnteredByUserId = head.Id, EnteredAt = measuredAt
                });
            }
            else
            {
                arrange.CountTestReadings.Add(new CountTestReading
                {
                    TestOrderId = orderId, PlateReadings = "999", DilutionFactor = 1,
                    ReportedResult = invalidatedValue, Status = ResultStatus.OutOfSpecification,
                    EnteredByUserId = head.Id, EnteredAt = measuredAt
                });
            }

            arrange.OosInvestigations.Add(new OosInvestigation
            {
                Number = $"OOS-NR-{Guid.NewGuid():N}"[..16],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.Concluded,
                Conclusion = OosConclusion.Invalidated,
                SampleId = sample.Sample.Id,
                SectionId = lab.Id,
                OpenedAtUtc = measuredAt,
                OpenedByUserId = head.Id,
                ConcludedAtUtc = measuredAt,
                ConcludedByUserId = head.Id,
                TemplateSnapshotJson = """{"items":[]}""",
                Results = { new OosInvestigationResult
                {
                    TestOrderId = orderId, OutcomeAtOpening = OosOutcomeClass.Fails,
                    ResultSummaryAtOpening = invalidatedValue, AddedAtUtc = measuredAt,
                    IsInvalidated = true, InvalidatedAtUtc = measuredAt
                } }
            });
            await arrange.SaveChangesAsync();
        }

        await using var act = fixture.CreateDbContext();
        var summary = await TestServiceFactory.SampleSummary(act).GetCertificateSummaryAsync(sample.Sample.Id, [lab.Id]);
        Assert.NotNull(summary);
        var order = Assert.Single(summary.TestOrders);
        Assert.False(order.IsSuperseded);
        Assert.True(order.IsOosInvalidated);
        Assert.True(summary.CombinedCoaAvailable);
        var certificate = summary.Certificate.Sample;
        Assert.Null(certificate.Matrix);
        Assert.Null(certificate.Simple);
        Assert.False(certificate.Complies);
        Assert.Equal("Cannot certify because no results are recorded.", certificate.ConclusionText);
    }

    private static OosTrendService Trends(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.OosInvestigation(db), LabClock.Default);

    private static OosInvestigationReportBuilder ReportBuilder(MicroLimsDbContext db, OosInvestigationService service) =>
        new(service, new PdfGenerator(), new DataExportAuditService(db), db);

    private static OosInvestigationDocumentController Documents(MicroLimsDbContext db, InMemoryFileStorageService storage, int actor)
    {
        var service = new OosInvestigationDocumentService(db, storage, new MaterialDocumentFileValidator(1024),
            NullLogger<OosInvestigationDocumentService>.Instance, TestServiceFactory.OosInvestigation(db));
        return new(service, new UserSectionScopeService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "Test"))
                }
            }
        };
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }
}
