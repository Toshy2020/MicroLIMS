using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// HPLC Workspace Part B, Task B3: Send for Review (HplcMethodAssayRecorder),
// the approval gate for HplcMethodAssay tests, run completion, return to
// analyst and re-submission.
public partial class HplcRunServiceTests
{
    // A passed run with one assigned, fully entered sample (report uploaded).
    private sealed class Submittable
    {
        public required HplcRunService Service;
        public required HplcRunDto Run;
        public required int RunSampleId;
        public required TestOrder Order;
        public required AssayFixture Fixture;
        public required Scenario Scenario;
        public required ILabClock Clock;
    }

    private static async Task<Submittable> ArrangeSubmittableAsync(
        MicroLimsDbContext db, ProductionStageRole role = ProductionStageRole.Finished,
        bool amountSpec = false, bool assaySpec = true, int replicates = 2, bool enter = true, bool report = true,
        decimal[]? responses = null)
    {
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s, amountSpec, assaySpec);
        var order = await AddAssayOrderAsync(db, s, f, role, replicates);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        var runSampleId = assigned.Samples[0].Id;
        if (enter)
        {
            var values = responses ?? new[] { 1000m, 1000m };
            await service.SaveReplicatesAsync(runSampleId,
                Replicates(f.Analyte.Id, values.Select(v => (50m, v)).ToArray()), s.UserId);
        }
        if (report)
            await service.UploadEvidenceAsync(run.Id, runSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport,
                "sample.pdf", "application/pdf", PdfBytes(), s.UserId);
        return new Submittable { Service = service, Run = run, RunSampleId = runSampleId, Order = order, Fixture = f, Scenario = s, Clock = clock };
    }

    private static Task<MicroLIMS.Application.Workflows.TestWorkflowResult> SubmitAsync(
        MicroLimsDbContext db, Submittable t, string password = Password) =>
        TestServiceFactory.TestWorkflow(db, clock: t.Clock).SubmitHplcMethodAssayAsync(t.RunSampleId, password, "ok", t.Scenario.UserId, "127.0.0.1");

    [Fact]
    public async Task Submit_MeanBasis_WritesOneAssayResultWithReplicateReadings()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);

        var result = await SubmitAsync(db, t);

        Assert.True(result.AllStepsComplete);
        var analysis = await db.TestAnalyses.Include(a => a.ParameterResults).ThenInclude(p => p.Readings).SingleAsync(a => a.TestOrderId == t.Order.Id);
        Assert.True(analysis.IsActive);
        Assert.Equal(WorkflowType.HplcMethodAssay, analysis.AnalysisType);
        Assert.Equal(t.Scenario.Equipment.Id, analysis.EquipmentId);
        Assert.Equal("HplcSstRecord", analysis.ValidityRecordType);
        var pr = Assert.Single(analysis.ParameterResults);
        Assert.Equal("Vitamin C", pr.ParameterName);
        Assert.Equal(99.0025m, Math.Round(pr.ReportedValue!.Value, 4));
        Assert.Equal("99.0 %", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
        Assert.Equal(t.Fixture.AssaySpec.Id, pr.SpecificationId);
        Assert.Equal(2, pr.Readings.Count);
        Assert.All(pr.Readings, r => Assert.Equal(ReadingKind.Replicate, r.Kind));
        Assert.Equal(new[] { 1, 2 }, pr.Readings.OrderBy(r => r.Stage).Select(r => r.Stage!.Value).ToArray());
        Assert.Contains("\"sstCode\"", pr.CalculationJson);
        var order = await db.TestOrders.SingleAsync(o => o.Id == t.Order.Id);
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
        Assert.NotEmpty(db.ElectronicSignatures.Where(e => e.MeaningOfSignature == SignatureMeaning.ResultRecorded));
        Assert.Single(db.ResultRecords.Where(r => r.TestOrderId == t.Order.Id));
    }

    [Fact]
    public async Task Submit_BulkStage_WritesOneResultPerReplicate()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, ProductionStageRole.Bulk, responses: new[] { 1000m, 1100m });

        await SubmitAsync(db, t);

        var results = await db.ParameterResults.Where(p => p.TestOrderId == t.Order.Id).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, results.Count);
        Assert.Equal("Vitamin C – replicate 1", results[0].ParameterName);
        Assert.Equal("Vitamin C – replicate 2", results[1].ParameterName);
        Assert.Equal(ResultStatus.WithinLimits, results[0].ComparisonStatus);
        Assert.Equal(ResultStatus.OutOfSpecification, results[1].ComparisonStatus);
        Assert.Equal(1, await db.ResultReadings.CountAsync(r => r.ParameterResultId == results[0].Id));
    }

    // Finished + an MgPerUnit spec row: a second result, judged on its own row.
    [Fact]
    public async Task Submit_FinishedWithAmountSpec_WritesAssayAndAmountWithTwoStatuses()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, ProductionStageRole.Finished, amountSpec: true);
        t.Fixture.AmountSpec!.LowerLimit = 480m;
        t.Fixture.AmountSpec.UpperLimit = 490m; // amount 495.0125 is outside, assay 99.0 is inside
        await db.SaveChangesAsync();

        await SubmitAsync(db, t);

        var results = await db.ParameterResults.Where(p => p.TestOrderId == t.Order.Id).ToListAsync();
        Assert.Equal(2, results.Count);
        var assay = results.Single(p => p.SpecificationId == t.Fixture.AssaySpec.Id);
        var amount = results.Single(p => p.SpecificationId == t.Fixture.AmountSpec.Id);
        Assert.Equal(ResultStatus.WithinLimits, assay.ComparisonStatus);
        Assert.Equal(ResultStatus.OutOfSpecification, amount.ComparisonStatus);
        Assert.Equal(495.0125m, Math.Round(amount.ReportedValue!.Value, 4));
        Assert.Equal("mg", amount.Unit);
    }

    [Fact]
    public async Task Submit_StabilityStage_WritesNoAmountRow()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, ProductionStageRole.Stability, amountSpec: true);

        await SubmitAsync(db, t);

        var pr = Assert.Single(await db.ParameterResults.Where(p => p.TestOrderId == t.Order.Id).ToListAsync());
        Assert.Equal(t.Fixture.AssaySpec.Id, pr.SpecificationId);
    }

    [Fact]
    public async Task Submit_WithoutSampleReport_Throws()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, report: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(db, t));

        Assert.Contains("sample report", ex.Message);
        Assert.Empty(db.TestAnalyses);
    }

    // Review Focus: replicate count per stage not met -> refused, naming the count.
    [Fact]
    public async Task Submit_WrongReplicateCount_ThrowsNamingTheCount()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, replicates: 3, responses: new[] { 1000m, 1000m });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(db, t));

        Assert.Contains("Expected exactly 3 replicates", ex.Message);
        Assert.Contains("2 entered", ex.Message);
        Assert.Empty(db.TestAnalyses);
    }

    [Fact]
    public async Task Submit_NoAssaySpecification_Throws()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, assaySpec: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(db, t));

        Assert.Equal("No assay specification for Vitamin C on this item.", ex.Message);
    }

    [Fact]
    public async Task Submit_WrongPassword_LeavesNothingPersisted()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);

        await Assert.ThrowsAnyAsync<Exception>(() => SubmitAsync(db, t, "WrongPassword!"));

        Assert.Empty(db.TestAnalyses);
        Assert.Empty(db.ParameterResults);
        Assert.Equal(WorkflowStep.Running, (await db.TestOrders.SingleAsync(o => o.Id == t.Order.Id)).CurrentStep);
    }

    [Fact]
    public async Task Submit_Twice_SecondIsRefused()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(db, t));
    }

    [Fact]
    public async Task Entry_AfterSubmit_IsNotEditable()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);

        var entry = await t.Service.GetSampleEntryAsync(t.RunSampleId, t.Scenario.UserId);

        Assert.False(entry.Editable);
        Assert.True(entry.Submitted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Service.SaveReplicatesAsync(
            t.RunSampleId, Replicates(t.Fixture.Analyte.Id, (50m, 1000m), (50m, 1000m)), t.Scenario.UserId));
    }

    // ---- Return to analyst, then re-submit ----

    private static async Task<User> AddSectionHeadAsync(MicroLimsDbContext db, Scenario s)
    {
        var role = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var head = new User
        {
            Username = "head_" + Guid.NewGuid().ToString("N")[..6], FullName = "Head", RoleId = role.Id, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(head);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, head.Id);
        return head;
    }

    // Review Focus: after a return the entry is editable again and
    // re-submission supersedes the old result (existing return flow).
    [Fact]
    public async Task Return_ThenResubmit_SupersedesTheOldResult()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);
        var head = await AddSectionHeadAsync(db, t.Scenario);

        await TestServiceFactory.Review(db).ReturnToAnalystAsync(t.Order.Id, head.Id, "Recheck weights");

        var entry = await t.Service.GetSampleEntryAsync(t.RunSampleId, t.Scenario.UserId);
        Assert.True(entry.Editable);
        Assert.False(entry.Submitted);
        await t.Service.SaveReplicatesAsync(t.RunSampleId, Replicates(t.Fixture.Analyte.Id, (50m, 990m), (50m, 990m)), t.Scenario.UserId);

        await SubmitAsync(db, t);

        var analyses = await db.TestAnalyses.Include(a => a.ParameterResults).Where(a => a.TestOrderId == t.Order.Id).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, analyses.Count);
        Assert.False(analyses[0].IsActive);
        Assert.True(analyses[1].IsActive);
        Assert.False(Assert.Single(analyses[0].ParameterResults).IsActive);
        Assert.Equal(98.0125m, Math.Round(Assert.Single(analyses[1].ParameterResults).ReportedValue!.Value, 4));
    }

    // ---- CompleteRunAsync ----

    [Fact]
    public async Task Complete_WithUnsubmittedSample_Throws()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => t.Service.CompleteRunAsync(t.Run.Id, t.Scenario.UserId));

        Assert.Contains("have not been sent for review", ex.Message);
    }

    [Fact]
    public async Task Complete_AllSubmitted_ClosesTheRun()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);

        var run = await t.Service.CompleteRunAsync(t.Run.Id, t.Scenario.UserId);

        Assert.Equal(HplcRunStatus.Completed, run.Status);
        Assert.NotNull(run.ClosedAt);
        Assert.True(Assert.Single(run.Samples).Submitted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Service.CompleteRunAsync(t.Run.Id, t.Scenario.UserId));
    }

    [Fact]
    public async Task Complete_IgnoresRemovedSamples()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await t.Service.RemoveSampleAsync(t.Run.Id, t.RunSampleId, "not run", t.Scenario.UserId);

        var run = await t.Service.CompleteRunAsync(t.Run.Id, t.Scenario.UserId);

        Assert.Equal(HplcRunStatus.Completed, run.Status);
    }

    // ---- Approval gate ----

    private static async Task<(User Head, Sample Sample, DocumentSection Section)> PrepareForApprovalAsync(MicroLimsDbContext db, Submittable t)
    {
        var head = await AddSectionHeadAsync(db, t.Scenario);
        var order = await db.TestOrders.Include(o => o.Sample).SingleAsync(o => o.Id == t.Order.Id);
        order.CurrentStep = WorkflowStep.Reviewed;
        order.Status = ApprovalStatus.Approved;
        var signoff = await db.SampleSectionSignoffs.FirstOrDefaultAsync(x => x.SampleId == order.SampleId && x.SectionId == t.Scenario.Section.Id);
        if (signoff == null)
        {
            signoff = new SampleSectionSignoff { SampleId = order.SampleId, SectionId = t.Scenario.Section.Id };
            db.SampleSectionSignoffs.Add(signoff);
        }
        signoff.Status = SectionSignoffStatus.UnderApproval;
        signoff.ReviewedByUserId = t.Scenario.UserId;
        order.Sample!.Status = SampleStatus.UnderReview;
        await db.SaveChangesAsync();
        return (head, order.Sample, t.Scenario.Section);
    }

    [Fact]
    public async Task Approval_HplcAssay_RequiresAnAssignedRunWithPassedSst()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);
        var (head, sample, section) = await PrepareForApprovalAsync(db, t);
        var approval = TestServiceFactory.SampleApproval(db);
        Task Approve() => approval.DecideAsync(sample.Id, head.Id, Password, ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: section.Id);

        var row = await db.HplcRunSamples.SingleAsync();
        row.Status = HplcRunSampleStatus.Removed;
        await db.SaveChangesAsync();
        var noRun = await Assert.ThrowsAsync<InvalidOperationException>(Approve);
        Assert.Contains("lacks an HPLC run with a passed system suitability", noRun.Message);

        row.Status = HplcRunSampleStatus.Assigned;
        (await db.HplcSstRecords.SingleAsync()).Status = HplcSstStatus.Failed;
        await db.SaveChangesAsync();
        var failed = await Assert.ThrowsAsync<InvalidOperationException>(Approve);
        Assert.Contains("lacks an HPLC run with a passed system suitability", failed.Message);

        (await db.HplcSstRecords.SingleAsync()).Status = HplcSstStatus.Passed;
        await db.SaveChangesAsync();
        await Approve();

        Assert.Equal(SampleStatus.Approved, (await db.Samples.SingleAsync(x => x.Id == sample.Id)).Status);
    }

    [Fact]
    public async Task Approval_OtherWorkflow_StillNeedsItsOwnSystemSuitabilityRunId()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);
        // Same order, but the test is not an HPLC-workspace test any more: the
        // legacy SystemSuitabilityRunId check applies (and fails - none linked).
        t.Fixture.Definition.WorkflowType = WorkflowType.StandardComparison;
        await db.SaveChangesAsync();
        var (head, sample, section) = await PrepareForApprovalAsync(db, t);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SampleApproval(db).DecideAsync(sample.Id, head.Id, Password, ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: section.Id));

        Assert.Contains("lacks a linked system suitability run", ex.Message);
    }
}
