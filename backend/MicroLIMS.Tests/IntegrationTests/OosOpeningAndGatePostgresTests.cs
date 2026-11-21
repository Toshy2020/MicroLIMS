using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class OosOpeningAndGatePostgresTests(PostgresTestFixture fixture)
{
    private readonly ActionGuardScenarios _scenarios = new(fixture);

    [PostgresFact]
    public async Task Review_of_failing_result_opens_investigation_in_same_transaction()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, orders: 3, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.Results.AddRange(seed.Orders.Select((order, index) => new Result
            {
                TestOrderId = order.Id,
                InterpretedValue = index == 2 ? nameof(ResultStatus.WithinLimits) : nameof(ResultStatus.OutOfSpecification),
                EnteredByUserId = analyst.Id
            }));
            await arrange.SaveChangesAsync();
        }

        var clock = new FixedTimeProvider(new DateTimeOffset(2041, 9, 2, 10, 0, 0, TimeSpan.Zero));
        await using (var act = fixture.CreateDbContext(clock))
            await TestServiceFactory.SampleReview(act, clock)
                .CompleteReviewAsync(seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null, section.Id);

        await using var verify = fixture.CreateDbContext();
        var investigation = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == seed.Sample.Id);
        Assert.StartsWith("OOS0941", investigation.Number);
        Assert.Equal(OosInvestigationKind.Oos, investigation.Kind);
        Assert.Equal(OosInvestigationStatus.PhaseOne, investigation.Status);
        Assert.Equal(reviewer.Id, investigation.OpenedByUserId);
        Assert.Equal(section.Id, investigation.SectionId);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, investigation.OpenedAtUtc);
        Assert.Null(investigation.PhaseOneDueDate);
        Assert.Null(investigation.DueDate);
        using var snapshot = JsonDocument.Parse(investigation.TemplateSnapshotJson);
        Assert.Equal(JsonValueKind.Null, snapshot.RootElement.GetProperty("templateId").ValueKind);
        Assert.Equal(0, snapshot.RootElement.GetProperty("items").GetArrayLength());
        var results = await verify.OosInvestigationResults.AsNoTracking()
            .Where(r => r.InvestigationId == investigation.Id).OrderBy(r => r.TestOrderId).ToListAsync();
        Assert.Equal(seed.Orders.Take(2).Select(o => o.Id).Order(), results.Select(r => r.TestOrderId));
        Assert.All(results, r => Assert.Equal(OosOutcomeClass.Fails, r.OutcomeAtOpening));
        var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
        Assert.Equal(investigation.Number, sample.OosGroupCode);
        Assert.Equal(SampleStatus.UnderApproval, sample.Status);
        var signature = await verify.ElectronicSignatures.AsNoTracking()
            .SingleAsync(s => s.EntityType == "Sample" && s.EntityId == sample.Id);
        Assert.Equal(SignatureMeaning.Reviewed, signature.MeaningOfSignature);
        Assert.Equal(reviewer.Id, signature.UserId);

        var sampleAudit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == sample.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(sampleAudit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(sampleAudit.NewValue));
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty(nameof(Sample.OosGroupCode)).ValueKind);
        Assert.Equal(investigation.Number, after.RootElement.GetProperty(nameof(Sample.OosGroupCode)).GetString());
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(OosInvestigation) && a.EntityId == investigation.Id.ToString() &&
            a.UserId == reviewer.Id && a.Action == "Create"));
        foreach (var result in results)
            Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(OosInvestigationResult) && a.EntityId == result.Id.ToString() &&
                a.UserId == reviewer.Id && a.Action == "Create"));
    }

    [PostgresFact]
    public async Task Pending_stage_preserves_existing_approval_refusal()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var code = await _scenarios.TestDefinitionAsync(section, WorkflowType.Titration);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id, testCode: code);
        await _scenarios.AnalysisAsync(seed.Orders[0].Id, analyst, comparison: ResultStatus.NextStageRequired);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(new(head.Id, seed.Sample.Id)));
        Assert.Equal($"Cannot approve section: test order {seed.Orders[0].Id} (\"{code}\") has a pending stage.", error.Message);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SectionSignoffStatus.UnderApproval, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == seed.Sample.Id).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == seed.Sample.Id));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == seed.Sample.Id.ToString() && a.UserId == head.Id));
    }
    [PostgresFact]
    public async Task Retest_sample_in_plan_can_be_approved_after_conclusion_but_not_while_open()
    {
        var world = await SeedInvalidatedPlanAsync(concluded: false);
        var target = new ActionGuardScenarios.Target(world.HeadId, world.RetestId);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(target));
        Assert.Equal("Oos.InvestigationOpen", error.Code);
        Assert.Equal($"OOS investigation {world.Number} is open.", error.Message);
        await using (var verify = fixture.CreateDbContext())
        {
            Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityId == world.RetestId && s.EntityType == "Sample"));
            Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking().Where(s => s.Id == world.RetestId).Select(s => s.Status).SingleAsync());
        }

        await _scenarios.UpdateAsync<OosInvestigation>(world.InvestigationId, i =>
        {
            i.Status = OosInvestigationStatus.Concluded;
            i.Conclusion = OosConclusion.Invalidated;
        });
        await ApproveAsAsync(target);
        await AssertApprovedAsync(world.RetestId, world.HeadId);
    }

    [PostgresFact]
    public async Task Origin_approval_after_invalidated_requires_plan_samples_approved()
    {
        var world = await SeedInvalidatedPlanAsync(concluded: true);
        var target = new ActionGuardScenarios.Target(world.HeadId, world.OriginId);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(target));
        Assert.Equal("Oos.RetestSamplesNotApproved", error.Code);
        Assert.Equal($"Retest samples of {world.Number} are not all approved.", error.Message);
        await using (var verify = fixture.CreateDbContext())
        {
            Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityId == world.OriginId && s.EntityType == "Sample"));
            Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking().Where(s => s.Id == world.OriginId).Select(s => s.Status).SingleAsync());
        }

        await ApproveAsAsync(new(world.HeadId, world.RetestId));
        await ApproveAsAsync(target);
        await AssertApprovedAsync(world.OriginId, world.HeadId);
    }

    private async Task AssertApprovedAsync(int sampleId, int headId)
    {
        await using var verify = fixture.CreateDbContext();
        var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
        Assert.Equal(SampleStatus.Approved, sample.Status);
        Assert.Equal(headId, sample.ApprovedByUserId);
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s =>
            s.EntityId == sampleId && s.EntityType == "Sample" && s.MeaningOfSignature == SignatureMeaning.Approved);
        Assert.Equal(SignatureMeaning.Approved, signature.MeaningOfSignature);
        Assert.Equal(headId, signature.UserId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == sampleId.ToString() && a.Action == "Update" && a.UserId == headId);
        AssertAuditedStatus(audit, SampleStatus.UnderApproval, SampleStatus.Approved);
    }

    private sealed record PlanWorld(int OriginId, int RetestId, int HeadId, int InvestigationId, string Number);

    private async Task<PlanWorld> SeedInvalidatedPlanAsync(bool concluded)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var origin = await _scenarios.UnderApprovalAsync(section, reviewer.Id);
        var retest = await _scenarios.UnderApprovalAsync(section, reviewer.Id);
        await using var arrange = fixture.CreateDbContext();
        var investigation = new OosInvestigation
        {
            SampleId = origin.Sample.Id,
            SectionId = section.Id,
            Number = $"T3-{Guid.NewGuid():N}"[..20],
            Status = concluded ? OosInvestigationStatus.Concluded : OosInvestigationStatus.AwaitingRetest,
            Conclusion = concluded ? OosConclusion.Invalidated : null,
            OpenedAtUtc = ActionGuardScenarios.Now,
            OpenedByUserId = reviewer.Id,
            TemplateSnapshotJson = "{}"
        };
        var linked = await arrange.Samples.SingleAsync(s => s.Id == retest.Sample.Id);
        linked.OriginSampleId = origin.Sample.Id;
        linked.OosGroupCode = investigation.Number;
        arrange.Results.AddRange(origin.Orders.Concat(retest.Orders).Select(o => new Result
        {
            TestOrderId = o.Id, EnteredByUserId = analyst.Id, InterpretedValue = nameof(ResultStatus.Detected)
        }));
        arrange.OosInvestigations.Add(investigation);
        await arrange.SaveChangesAsync();
        arrange.OosInvestigationResults.AddRange(origin.Orders.Concat(retest.Orders).Select(o => new OosInvestigationResult
        {
            InvestigationId = investigation.Id,
            TestOrderId = o.Id,
            OutcomeAtOpening = OosOutcomeClass.Fails,
            AddedAtUtc = ActionGuardScenarios.Now
        }));
        var plan = new OosRetestPlan
        {
            InvestigationId = investigation.Id,
            Revision = 1,
            RetestCount = 1,
            AnalystUserIdsJson = "[]",
            TestOrderIdsJson = "[]",
            JudgingRule = "Compare recorded results to specifications.",
            SignedAtUtc = ActionGuardScenarios.Now,
            SignedByUserId = head.Id
        };
        arrange.OosRetestPlans.Add(plan);
        await arrange.SaveChangesAsync();
        arrange.OosRetestPlanSamples.Add(new OosRetestPlanSample { PlanId = plan.Id, SampleId = retest.Sample.Id });
        await arrange.SaveChangesAsync();
        return new(origin.Sample.Id, retest.Sample.Id, head.Id, investigation.Id, investigation.Number);
    }

    [PostgresFact]
    public Task Review_and_approval_follow_the_same_status_allow_list()
    {
        (ResultStatus? Status, bool Opens)[] cases =
        [
            (null, false),
            ((ResultStatus)0, false),
            (ResultStatus.WithinLimits, false),
            (ResultStatus.AlertLimitExceeded, false),
            (ResultStatus.ActionLimitExceeded, false),
            (ResultStatus.OutOfSpecification, true),
            (ResultStatus.LimitsNotConfigured, false),
            (ResultStatus.RequiresReview, false),
            (ResultStatus.Absent, false),
            (ResultStatus.Detected, true),
            (ResultStatus.PendingConfirmation, false),
            (ResultStatus.NotDetected, false),
            (ResultStatus.NextStageRequired, false),
            (ResultStatus.Inconclusive, false)
        ];
        return PostgresCases.RunAsync(cases, c => CheckStatusAsync(c.Status, c.Opens));
    }

    private async Task CheckStatusAsync(ResultStatus? status, bool opens)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = seed.Sample.Id, TestOrderId = seed.Orders[0].Id, Status = status
            });
            await arrange.SaveChangesAsync();
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await using (var verify = fixture.CreateDbContext())
        {
            var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
            Assert.Equal(SampleStatus.UnderApproval, sample.Status);
            Assert.Equal(opens, sample.OosGroupCode is not null);
            Assert.Equal(opens, await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == sample.Id));
            Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(Sample) && a.EntityId == sample.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Update"));
        }

        if (opens)
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(new(head.Id, seed.Sample.Id)));
            Assert.Equal("Oos.InvestigationOpen", error.Code);
        }
        else
        {
            await ApproveAsAsync(new(head.Id, seed.Sample.Id));
            await AssertApprovedAsync(seed.Sample.Id, head.Id);
        }
    }

    [PostgresFact]
    public Task Review_uses_the_recorded_qualitative_call()
    {
        (string Value, bool Opens)[] cases =
        [
            ("NotDetected", false),
            ("Inconclusive", false),
            ("PendingConfirmation", false),
            ("Absent", false),
            ("Detected", true),
            ("detected", true),
            ("Not Detected", false)
        ];
        return PostgresCases.RunAsync(cases, c => CheckQualitativeCallAsync(c.Value, c.Opens));
    }

    private async Task CheckQualitativeCallAsync(string value, bool opens)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, value);

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(opens, await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
        Assert.True(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s =>
            s.EntityType == "Sample" && s.EntityId == seed.Sample.Id && s.UserId == reviewer.Id &&
            s.MeaningOfSignature == SignatureMeaning.Reviewed));
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == seed.Sample.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task Dissolution_value_OutOfSpecification_opens()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var code = await _scenarios.TestDefinitionAsync(section, WorkflowType.Dissolution);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id, testCode: code);
        var interpretation = "Stage 3: Does not comply " + new string('x', 520) + " (OutOfSpecification)";
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, interpretation);

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await AssertOpenedForOrderAsync(seed.Sample.Id, seed.Orders[0].Id, reviewer.Id);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(interpretation[..500], await verify.OosInvestigationResults.AsNoTracking()
            .Where(r => r.TestOrderId == seed.Orders[0].Id).Select(r => r.ResultSummaryAtOpening).SingleAsync());
    }

    [PostgresFact]
    public Task Review_uses_the_CoA_detection_call()
    {
        (GrowthObservation Growth, bool? Biochemical, bool Opens)[] cases =
        [
            (GrowthObservation.GrowthConforming, null, true),
            (GrowthObservation.GrowthNonConforming, null, false),
            (GrowthObservation.NoGrowth, null, false),
            (GrowthObservation.GrowthConforming, false, false),
            (GrowthObservation.NoGrowth, true, true)
        ];
        return PostgresCases.RunAsync(cases, c => CheckDetectionCallAsync(c.Growth, c.Biochemical, c.Opens));
    }

    private async Task CheckDetectionCallAsync(GrowthObservation growth, bool? biochemical, bool opens)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        var orderId = seed.Orders[0].Id;
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.PathogenObservations.Add(new PathogenObservation
            {
                TestOrderId = orderId, Observation = growth, StepName = "Observation", ObservedByUserId = analyst.Id
            });
            if (biochemical.HasValue)
            {
                var incubation = new Incubation { TestOrderId = orderId };
                arrange.Incubations.Add(incubation);
                await arrange.SaveChangesAsync();
                arrange.WorkflowStepResults.Add(new WorkflowStepResult
                {
                    IncubationId = incubation.Id, TestOrderId = orderId, StepName = "Confirmation",
                    BiochemicalResultText = "Recorded interpretation",
                    BiochemicalOrganismDetected = biochemical, SubmittedByUserId = analyst.Id
                });
            }
            await arrange.SaveChangesAsync();
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        if (opens)
            await AssertOpenedForOrderAsync(seed.Sample.Id, orderId, reviewer.Id);
        else
        {
            await using var verify = fixture.CreateDbContext();
            Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
            var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
                a.EntityName == nameof(Sample) && a.EntityId == seed.Sample.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Update");
            AssertAuditedStatus(audit, SampleStatus.UnderReview, SampleStatus.UnderApproval);
        }
    }

    [PostgresFact]
    public Task Section_with_failing_order_and_no_investigation_cannot_be_decided() =>
        PostgresCases.RunAsync(
            new[] { ApprovalDecision.Approve, ApprovalDecision.Reject, ApprovalDecision.RetestRetainedSample, ApprovalDecision.NewSampleRequest },
            CheckDecisionWithoutInvestigationAsync);

    private async Task CheckDecisionWithoutInvestigationAsync(ApprovalDecision decision)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsAsync(new(head.Id, seed.Sample.Id), decision));
        Assert.Equal("Oos.FailingResultWithoutInvestigation", error.Code);
        Assert.Equal("This sample has an out-of-specification result without an investigation.", error.Message);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SectionSignoffStatus.UnderApproval, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == seed.Sample.Id).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == seed.Sample.Id));
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == head.Id && a.EntityId == seed.Sample.Id.ToString() && a.EntityName == nameof(Sample)));
    }

    [PostgresFact]
    public async Task Every_certificate_path_is_blocked_while_open()
    {
        var world = await SeedInvalidatedPlanAsync(concluded: false);
        await using var act = fixture.CreateDbContext();
        var summary = TestServiceFactory.SampleSummary(act);
        foreach (var sampleId in new[] { world.OriginId, world.RetestId })
        {
            Func<Task>[] exports =
            [
                () => summary.GetCertificateSummaryAsync(sampleId, null),
                () => summary.GenerateSummaryPdfAsync(sampleId),
                () => summary.GenerateSummaryWordAsync(sampleId),
                () => summary.BuildReportDocumentAsync(sampleId)
            ];
            foreach (var export in exports)
            {
                var error = await Assert.ThrowsAsync<BusinessRuleException>(export);
                Assert.Equal("Oos.InvestigationOpen", error.Code);
                Assert.Equal($"OOS investigation {world.Number} is open.", error.Message);
            }
            Assert.NotNull(await summary.GetSummaryAsync(sampleId));
        }

        var approval = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(new(world.HeadId, world.RetestId)));
        Assert.Equal("Oos.InvestigationOpen", approval.Code);
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == world.RetestId));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == world.RetestId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.HeadId && a.EntityId == world.RetestId.ToString() && a.EntityName == nameof(Sample)));
    }

    private async Task RecordResultAsync(int orderId, int analystId, string value)
    {
        await using var arrange = fixture.CreateDbContext();
        arrange.Results.Add(new Result { TestOrderId = orderId, EnteredByUserId = analystId, InterpretedValue = value });
        await arrange.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Second_failing_section_does_not_join_first_section_investigation_nor_overwrite_group_code()
    {
        var first = await _scenarios.SectionAsync();
        var second = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(first, second);
        var analyst = await _scenarios.AnalystAsync(first, second);
        var sample = await _scenarios.UnderReviewAsync(first, analystId: analyst.Id);
        await _scenarios.AddSectionAsync(sample, second, ApprovalStatus.ResultEntered, WorkflowStep.Ready,
            SectionSignoffStatus.UnderReview, analystId: analyst.Id);
        foreach (var order in sample.Orders)
            await RecordResultAsync(order.Id, analyst.Id, nameof(ResultStatus.Detected));

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, sample.Sample.Id, first.Id));
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, sample.Sample.Id, second.Id));
        await using var verify = fixture.CreateDbContext();
        var investigations = await verify.OosInvestigations.AsNoTracking().Where(i => i.SampleId == sample.Sample.Id)
            .Include(i => i.Results).OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(new[] { first.Id, second.Id }, investigations.Select(i => i.SectionId));
        Assert.NotEqual(investigations[0].Number, investigations[1].Number);
        Assert.Equal(sample.Orders.Select(o => o.Id), investigations.Select(i => Assert.Single(i.Results).TestOrderId));
        Assert.Equal(investigations[0].Number, await verify.Samples.AsNoTracking()
            .Where(s => s.Id == sample.Sample.Id).Select(s => s.OosGroupCode).SingleAsync());
        foreach (var investigation in investigations)
            Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(OosInvestigation) && a.EntityId == investigation.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Create"));
    }

    [PostgresFact]
    public async Task Two_openings_in_same_month_after_a_gap_get_distinct_numbers()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        var second = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        const string legacyCode = "OOS0642002";
        await _scenarios.UpdateAsync<Sample>(seed.Sample.Id, s => s.OosGroupCode = legacyCode);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        await RecordResultAsync(second.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        var clock = new FixedTimeProvider(new DateTimeOffset(2042, 6, 5, 12, 0, 0, TimeSpan.Zero));

        await using (var act = fixture.CreateDbContext(clock))
            await TestServiceFactory.SampleReview(act, clock)
                .CompleteReviewAsync(seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null);
        await using (var act = fixture.CreateDbContext(clock))
            await TestServiceFactory.SampleReview(act, clock)
                .CompleteReviewAsync(second.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null);

        await using var verify = fixture.CreateDbContext();
        var investigation = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == seed.Sample.Id);
        Assert.Equal("OOS0642003", investigation.Number);
        var next = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == second.Sample.Id);
        Assert.Equal("OOS0642004", next.Number);
        Assert.Equal(reviewer.Id, next.OpenedByUserId);
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(OosInvestigation) && a.EntityId == next.Id.ToString() && a.Action == "Create" && a.UserId == reviewer.Id));
        Assert.Equal(reviewer.Id, investigation.OpenedByUserId);
        var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
        Assert.Equal(legacyCode, sample.OosGroupCode);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == sample.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal(legacyCode, before.RootElement.GetProperty(nameof(Sample.OosGroupCode)).GetString());
        Assert.Equal(legacyCode, after.RootElement.GetProperty(nameof(Sample.OosGroupCode)).GetString());
    }

    [PostgresFact]
    public async Task Failing_retest_result_joins_the_origin_investigation()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var origin = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        var head = await _scenarios.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage], section);
        await RecordResultAsync(origin.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, origin.Sample.Id));
        int investigationId;
        uint version;
        await using (var setup = fixture.CreateDbContext())
        {
            var investigation = await setup.OosInvestigations.SingleAsync(i => i.SampleId == origin.Sample.Id);
            var signature = await new ElectronicSignatureService(setup).SignAsync(head.Id, ActionGuardScenarios.Password,
                SignatureMeaning.OosPhaseOneConcluded, nameof(OosInvestigation), investigation.Id, null, null);
            if (!await setup.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
                setup.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
            await setup.SaveChangesAsync();
            investigation.PhaseOneSignatureId = signature.Id;
            investigation.PhaseOneSignedAtUtc = signature.SignedAt;
            investigation.PhaseOneSignedByUserId = head.Id;
            investigation.Status = OosInvestigationStatus.AwaitingRetest;
            await setup.SaveChangesAsync();
            investigationId = investigation.Id;
            version = investigation.Version;
        }

        OosRetestPlanDto plan;
        await using (var draft = fixture.CreateDbContext())
            plan = await OosRetestPlanPostgresTests.Service(draft).DraftAsync(investigationId,
                new(OosRetestSource.RetainedSample, 1, [origin.Orders[0].Id], [analyst.Id], "Compare the result to its specification.", null),
                version, head.Id);
        await using (var sign = fixture.CreateDbContext())
            await OosRetestPlanPostgresTests.Service(sign).SignAndCreateAsync(plan.Id, ActionGuardScenarios.Password,
                plan.InvestigationVersion, head.Id, null);

        int retestId;
        int retestOrderId;
        await using (var setup = fixture.CreateDbContext())
        {
            var link = await setup.OosRetestPlanSamples.AsNoTracking().SingleAsync(l => l.PlanId == plan.Id);
            var child = await setup.Samples.Include(x => x.TestOrders).SingleAsync(x => x.Id == link.SampleId);
            child.OosGroupCode = "UNRELATED-GROUP";
            child.Status = SampleStatus.UnderReview;
            var order = Assert.Single(child.TestOrders);
            order.Status = ApprovalStatus.ResultEntered;
            order.CurrentStep = WorkflowStep.Ready;
            setup.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = child.Id, SectionId = section.Id, Status = SectionSignoffStatus.UnderReview
            });
            await setup.SaveChangesAsync();
            retestId = child.Id;
            retestOrderId = order.Id;
        }
        await RecordResultAsync(retestOrderId, analyst.Id, nameof(ResultStatus.Detected));
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, retestId));
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == retestId));
        var persisted = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .SingleAsync(i => i.SampleId == origin.Sample.Id);
        Assert.Equal(new[] { origin.Orders[0].Id, retestOrderId }, persisted.Results.Select(r => r.TestOrderId).Order());
        var added = persisted.Results.Single(r => r.TestOrderId == retestOrderId);
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(OosInvestigationResult) && a.EntityId == added.Id.ToString() && a.UserId == reviewer.Id && a.Action == "Create"));
        Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking()
            .Where(s => s.Id == retestId).Select(s => s.Status).SingleAsync());
    }

    [PostgresFact]
    public async Task Review_waits_for_conclusion_and_does_not_join_the_concluded_investigation()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        int originalId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var original = new OosInvestigation
            {
                SampleId = seed.Sample.Id, SectionId = section.Id, OpenedByUserId = reviewer.Id,
                Number = $"RACE-{Guid.NewGuid():N}"[..20], TemplateSnapshotJson = "{}"
            };
            arrange.OosInvestigations.Add(original);
            await arrange.SaveChangesAsync();
            originalId = original.Id;
        }

        await using var closer = fixture.CreateDbContext();
        await using var review = fixture.CreateDbContext();
        await using var observer = fixture.CreateDbContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = timeout.Token;
        await review.Database.OpenConnectionAsync(ct);
        var reviewPid = ((Npgsql.NpgsqlConnection)review.Database.GetDbConnection()).ProcessID;
        await using var closingTransaction = await closer.Database.BeginTransactionAsync(ct);
        closer.CurrentUserId = head.Id;
        var closing = await closer.OosInvestigations.SingleAsync(i => i.Id == originalId, ct);
        closing.Status = OosInvestigationStatus.Concluded;
        closing.Conclusion = OosConclusion.Confirmed;
        await closer.SaveChangesAsync(ct);
        var completion = TestServiceFactory.SampleReview(review).CompleteReviewAsync(
            seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null, ct: ct);
        bool blocked;
        try
        {
            while (!completion.IsCompleted && !await observer.Database.SqlQuery<bool>(
                $"SELECT cardinality(pg_blocking_pids({reviewPid})) > 0 AS \"Value\"").SingleAsync(ct))
                await Task.Delay(10, ct);
            blocked = !completion.IsCompleted;
        }
        finally
        {
            await closingTransaction.CommitAsync();
        }
        await completion;
        Assert.True(blocked);

        await using var verify = fixture.CreateDbContext();
        var originalAfter = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.Id == originalId);
        Assert.Equal(OosInvestigationStatus.Concluded, originalAfter.Status);
        Assert.Empty(originalAfter.Results);
        var opened = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .SingleAsync(i => i.SampleId == seed.Sample.Id && i.Status == OosInvestigationStatus.PhaseOne);
        Assert.Equal(seed.Orders[0].Id, Assert.Single(opened.Results).TestOrderId);
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == opened.Id.ToString() && a.Action == "Create" && a.UserId == reviewer.Id));
    }

    [PostgresFact]
    public async Task Earlier_batch_location_failure_opens()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section);
        await using (var arrange = fixture.CreateDbContext())
        {
            foreach (var status in new[] { ResultStatus.OutOfSpecification, ResultStatus.WithinLimits })
            {
                arrange.SampleLocations.Add(new SampleLocation
                {
                    SampleId = seed.Sample.Id, TestOrderId = seed.Orders[0].Id, Status = status
                });
                await arrange.SaveChangesAsync();
            }
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await AssertOpenedForOrderAsync(seed.Sample.Id, seed.Orders[0].Id, reviewer.Id);
    }

    [PostgresFact]
    public async Task Physicochemical_parameter_result_out_of_spec_opens()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var code = await _scenarios.TestDefinitionAsync(section, WorkflowType.HplcMethodAssay);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id, testCode: code);
        await _scenarios.AnalysisAsync(seed.Orders[0].Id, analyst, comparison: ResultStatus.OutOfSpecification);

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await AssertOpenedForOrderAsync(seed.Sample.Id, seed.Orders[0].Id, reviewer.Id);
    }

    [PostgresFact]
    public Task Analysis_with_one_location_drives_the_gate_and_two_locations_keep_location_precedence() =>
        PostgresCases.RunAsync(new[] { 1, 2 }, CheckAnalysisLocationsAsync);

    private async Task CheckAnalysisLocationsAsync(int locations)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var code = await _scenarios.TestDefinitionAsync(section, WorkflowType.HplcMethodAssay);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id, testCode: code);
        var orderId = seed.Orders[0].Id;
        var parameterId = await _scenarios.AnalysisAsync(orderId, analyst, comparison: ResultStatus.OutOfSpecification);
        await _scenarios.UpdateAsync<ParameterResult>(parameterId, p => p.Unit = "mg");
        await using (var arrange = fixture.CreateDbContext())
        {
            var analysis = await arrange.TestAnalyses.SingleAsync(a => a.TestOrderId == orderId);
            analysis.AnalysisType = WorkflowType.HplcMethodAssay;
            for (var i = 0; i < locations; i++)
                arrange.SampleLocations.Add(new SampleLocation { SampleId = seed.Sample.Id, TestOrderId = orderId });
            await arrange.SaveChangesAsync();
        }
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        if (locations == 1)
        {
            await AssertOpenedForOrderAsync(seed.Sample.Id, orderId, reviewer.Id);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(new(head.Id, seed.Sample.Id)));
            Assert.Equal("Oos.InvestigationOpen", error.Code);
            await using var verify = fixture.CreateDbContext();
            Assert.Equal("2.5 mg", await verify.OosInvestigationResults.AsNoTracking()
                .Where(r => r.TestOrderId == orderId).Select(r => r.ResultSummaryAtOpening).SingleAsync());
        }
        else
        {
            await ApproveAsAsync(new(head.Id, seed.Sample.Id));
            await AssertApprovedAsync(seed.Sample.Id, head.Id);
            await using var verify = fixture.CreateDbContext();
            Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
        }
    }

    [PostgresFact]
    public async Task Review_snapshots_active_checklist_and_skips_holiday_in_due_dates()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        int templateId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var template = new OosInvestigationTemplate { SectionId = section.Id, PhaseOneDueWorkingDays = 1, TotalDueWorkingDays = 2 };
            arrange.OosInvestigationTemplates.Add(template);
            arrange.LabCalendarDates.Add(new LabCalendarDate { Date = new(2043, 1, 6), Description = "Holiday" });
            await arrange.SaveChangesAsync();
            templateId = template.Id;
            arrange.OosChecklistItems.AddRange(
                new OosChecklistItem { TemplateId = templateId, Phase = 1, Key = "equipment", Order = 1, Text = "Check equipment", IsActive = true },
                new OosChecklistItem { TemplateId = templateId, Phase = 2, Key = "records", Order = 1, Text = "Check records", IsActive = true },
                new OosChecklistItem { TemplateId = templateId, Phase = 1, Key = "inactive", Order = 2, Text = "Old item" });
            await arrange.SaveChangesAsync();
        }

        var clock = new FixedTimeProvider(new DateTimeOffset(2043, 1, 5, 12, 0, 0, TimeSpan.Zero));
        await using (var act = fixture.CreateDbContext(clock))
            await TestServiceFactory.SampleReview(act, clock)
                .CompleteReviewAsync(seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null);

        await using var verify = fixture.CreateDbContext();
        var investigation = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == seed.Sample.Id);
        Assert.Equal(new DateOnly(2043, 1, 7), investigation.PhaseOneDueDate);
        Assert.Equal(new DateOnly(2043, 1, 8), investigation.DueDate);
        using var snapshot = JsonDocument.Parse(investigation.TemplateSnapshotJson);
        Assert.Equal(templateId, snapshot.RootElement.GetProperty("templateId").GetInt32());
        Assert.Equal(new (int, string?, int, string?)[] { (1, "equipment", 1, "Check equipment"), (2, "records", 1, "Check records") },
            snapshot.RootElement.GetProperty("items").EnumerateArray().Select(i =>
                (i.GetProperty("phase").GetInt32(), i.GetProperty("key").GetString(), i.GetProperty("order").GetInt32(), i.GetProperty("text").GetString())));
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == investigation.Id.ToString() && a.Action == "Create");
        Assert.Equal(reviewer.Id, audit.UserId);
        Assert.Contains("2043-01-07", audit.NewValue);
    }

    [PostgresFact]
    public async Task Excursion_neither_blocks_gate_nor_is_joined()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var passing = await _scenarios.UnderApprovalAsync(section, reviewer.Id);
        var failing = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(failing.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.OosInvestigations.AddRange(new[] { passing.Sample.Id, failing.Sample.Id }.Select(id => new OosInvestigation
            {
                SampleId = id, SectionId = section.Id, OpenedByUserId = reviewer.Id, Kind = OosInvestigationKind.Excursion,
                Number = $"EX-{Guid.NewGuid():N}"[..20], TemplateSnapshotJson = "{}"
            }));
            await arrange.SaveChangesAsync();
        }
        await ApproveAsAsync(new(head.Id, passing.Sample.Id));
        await AssertApprovedAsync(passing.Sample.Id, head.Id);
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, failing.Sample.Id));
        await using var verify = fixture.CreateDbContext();
        var investigations = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .Where(i => i.SampleId == failing.Sample.Id).ToListAsync();
        Assert.Empty(Assert.Single(investigations, i => i.Kind == OosInvestigationKind.Excursion).Results);
        var opened = Assert.Single(investigations, i => i.Kind == OosInvestigationKind.Oos);
        Assert.Equal(failing.Orders[0].Id, Assert.Single(opened.Results).TestOrderId);
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == opened.Id.ToString() && a.Action == "Create" && a.UserId == reviewer.Id));
    }

    private async Task AssertOpenedForOrderAsync(int sampleId, int orderId, int reviewerId)
    {
        await using var verify = fixture.CreateDbContext();
        var investigation = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.SampleId == sampleId);
        Assert.Equal(OosInvestigationStatus.PhaseOne, investigation.Status);
        Assert.Equal(reviewerId, investigation.OpenedByUserId);
        var result = Assert.Single(investigation.Results);
        Assert.Equal(orderId, result.TestOrderId);
        Assert.Equal(OosOutcomeClass.Fails, result.OutcomeAtOpening);
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(OosInvestigationResult) && a.EntityId == result.Id.ToString() && a.UserId == reviewerId && a.Action == "Create"));
    }

    [PostgresFact]
    public async Task Other_section_of_same_sample_can_still_be_approved_but_certificate_is_blocked()
    {
        var section = await _scenarios.SectionAsync();
        var other = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section, other);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section, other);
        var sample = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await _scenarios.AddSectionAsync(sample, other, ApprovalStatus.Reviewed, WorkflowStep.Reviewed,
            SectionSignoffStatus.UnderApproval, reviewer.Id);
        await RecordResultAsync(sample.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, sample.Sample.Id, section.Id));

        await ApproveAsAsync(new(head.Id, sample.Sample.Id, other.Id));
        await using (var act = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.SampleSummary(act).GetCertificateSummaryAsync(sample.Sample.Id, null));
            Assert.Equal("Oos.InvestigationOpen", error.Code);
        }

        await using var verify = fixture.CreateDbContext();
        var signoffs = await verify.SampleSectionSignoffs.AsNoTracking().Where(s => s.SampleId == sample.Sample.Id)
            .ToDictionaryAsync(s => s.SectionId);
        Assert.Equal(SectionSignoffStatus.UnderApproval, signoffs[section.Id].Status);
        Assert.Equal(SectionSignoffStatus.Approved, signoffs[other.Id].Status);
        Assert.Equal(head.Id, signoffs[other.Id].ApprovedByUserId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(SampleSectionSignoff) && a.UserId == head.Id && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)SectionSignoffStatus.UnderApproval, before.RootElement.GetProperty(nameof(SampleSectionSignoff.Status)).GetInt32());
        Assert.Equal((int)SectionSignoffStatus.Approved, after.RootElement.GetProperty(nameof(SampleSectionSignoff.Status)).GetInt32());
    }

    [PostgresFact]
    public Task Approve_refused_while_open() =>
        CheckDecisionWithOpenInvestigationAsync(ApprovalDecision.Approve, "Oos.InvestigationOpen");

    [PostgresFact]
    public Task Reject_refused_while_open() =>
        CheckDecisionWithOpenInvestigationAsync(ApprovalDecision.Reject, "Oos.InvestigationOpen");

    [PostgresFact]
    public Task Direct_retest_decision_refused_for_section_with_investigation() =>
        PostgresCases.RunAsync(new[] { ApprovalDecision.RetestRetainedSample, ApprovalDecision.NewSampleRequest },
            decision => CheckDecisionWithOpenInvestigationAsync(decision, "Oos.RetestNeedsApprovedPlan"));

    private async Task CheckDecisionWithOpenInvestigationAsync(ApprovalDecision decision, string expectedCode)
    {
        var world = await SeedInvalidatedPlanAsync(concluded: false);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DecideAsAsync(new(world.HeadId, world.OriginId), decision));
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedCode == "Oos.InvestigationOpen" ? $"OOS investigation {world.Number} is open."
            : "A retest needs an approved retest plan.", error.Message);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SectionSignoffStatus.UnderApproval, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == world.OriginId).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == world.OriginId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.HeadId &&
            a.EntityId == world.OriginId.ToString() && a.EntityName == nameof(Sample)));
    }

    [PostgresFact]
    public Task Reviewer_who_tested_is_refused_and_opens_nothing() => CheckRefusedReviewAsync(performedTest: true);

    [PostgresFact]
    public Task Wrong_review_password_opens_nothing() => CheckRefusedReviewAsync(performedTest: false);

    private async Task CheckRefusedReviewAsync(bool performedTest)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = performedTest ? reviewer : await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));
        await using (var act = fixture.CreateDbContext())
        {
            var review = TestServiceFactory.SampleReview(act);
            if (performedTest)
            {
                var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    review.CompleteReviewAsync(seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null));
                Assert.Equal("You cannot review a sample you tested.", error.Message);
            }
            else
            {
                await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                    review.CompleteReviewAsync(seed.Sample.Id, reviewer.Id, "wrong password", null, null));
            }
        }

        await AssertReviewUnchangedAsync(seed.Sample.Id, reviewer.Id);
    }

    private async Task AssertReviewUnchangedAsync(int sampleId, int reviewerId)
    {
        await using var verify = fixture.CreateDbContext();
        var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
        Assert.Equal(SampleStatus.UnderReview, sample.Status);
        Assert.Equal(SectionSignoffStatus.UnderReview, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == sampleId).Select(s => s.Status).SingleAsync());
        Assert.Null(sample.OosGroupCode);
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == sampleId));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == sampleId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == sampleId.ToString() && a.UserId == reviewerId && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task Review_and_opening_roll_back_together()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));

        var fault = new FailAuditInsert();
        await using (var act = fixture.CreateDbContext(fault))
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => TestServiceFactory.SampleReview(act)
                .CompleteReviewAsync(seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null));
            Assert.Same(fault.Error, error.InnerException);
            Assert.True(fault.InvestigationInserted);
        }
        await AssertReviewUnchangedAsync(seed.Sample.Id, reviewer.Id);
    }

    private sealed class FailAuditInsert : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public InvalidOperationException Error { get; } = new("Injected audit insert failure.");
        public bool InvestigationInserted { get; private set; }

        public override ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
            System.Data.Common.DbDataReader result, CancellationToken cancellationToken = default)
        {
            InvestigationInserted |= command.CommandText.Contains("INSERT INTO \"OosInvestigations\"", StringComparison.Ordinal);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"AuditLogs\"", StringComparison.Ordinal))
                throw Error;
            return ValueTask.FromResult(result);
        }
    }

    [PostgresFact]
    public async Task Concurrent_reviews_of_same_section_open_one_investigation()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var secondReviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await RecordResultAsync(seed.Orders[0].Id, analyst.Id, nameof(ResultStatus.Detected));

        var numberReads = new NumberReadCounter();
        await using var first = fixture.CreateDbContext(numberReads);
        await using var second = fixture.CreateDbContext(numberReads);
        await using var observer = fixture.CreateDbContext();
        await using var blocker = fixture.CreateDbContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = timeout.Token;
        await first.Database.OpenConnectionAsync(ct);
        await second.Database.OpenConnectionAsync(ct);
        var firstPid = ((Npgsql.NpgsqlConnection)first.Database.GetDbConnection()).ProcessID;
        var secondPid = ((Npgsql.NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;
        var key = MicroLIMS.Application.Helpers.AdvisoryLockKeys.OosNumber;
        await using var lockTransaction = await blocker.Database.BeginTransactionAsync(ct);
        await blocker.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);

        var a = Record.ExceptionAsync(() => TestServiceFactory.SampleReview(first).CompleteReviewAsync(
            seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null, ct: ct));
        var b = Record.ExceptionAsync(() => TestServiceFactory.SampleReview(second).CompleteReviewAsync(
            seed.Sample.Id, secondReviewer.Id, ActionGuardScenarios.Password, null, null, ct: ct));
        try
        {
            while (!a.IsCompleted && !b.IsCompleted && await observer.Database.SqlQuery<int>(
                $"""SELECT count(*)::int AS "Value" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND pid IN ({firstPid}, {secondPid})""")
                .SingleAsync(ct) != 2)
                await Task.Delay(10, ct);
        }
        finally
        {
            await lockTransaction.CommitAsync();
        }

        var outcomes = await Task.WhenAll(a, b);
        Assert.Single(outcomes, e => e is null);
        Assert.IsType<DbUpdateConcurrencyException>(Assert.Single(outcomes, e => e is not null));
        Assert.Equal(1, numberReads.Count);
        await using var verify = fixture.CreateDbContext();
        var row = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .SingleAsync(i => i.SampleId == seed.Sample.Id);
        Assert.Equal(seed.Orders[0].Id, Assert.Single(row.Results).TestOrderId);
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.EntityId == seed.Sample.Id && s.EntityType == "Sample");
        Assert.Equal(SignatureMeaning.Reviewed, signature.MeaningOfSignature);
        Assert.Equal(row.OpenedByUserId, signature.UserId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(OosInvestigation) && a.EntityId == row.Id.ToString() && a.Action == "Create");
        Assert.Equal(signature.UserId, audit.UserId);
    }

    private sealed class NumberReadCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private int _count;
        public int Count => _count;

        public override ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
            System.Data.Common.DbDataReader result, CancellationToken cancellationToken = default)
        {
            // Allocation reads the union of historical sample group codes and investigation numbers.
            if (command.CommandText.Contains("UNION", StringComparison.Ordinal) &&
                command.CommandText.Contains("OosGroupCode", StringComparison.Ordinal) &&
                command.CommandText.Contains("OosInvestigations", StringComparison.Ordinal))
                Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }

    private Task ApproveAsAsync(ActionGuardScenarios.Target target) => DecideAsAsync(target, ApprovalDecision.Approve);

    private static void AssertAuditedStatus(AuditLog audit, SampleStatus previous, SampleStatus current)
    {
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)previous, before.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
        Assert.Equal((int)current, after.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
    }

    private async Task DecideAsAsync(ActionGuardScenarios.Target target, ApprovalDecision decision)
    {
        await using var act = fixture.CreateDbContext();
        act.CurrentUserId = target.UserId;
        await TestServiceFactory.SampleApproval(act).DecideAsync(target.RecordId, target.UserId,
            ActionGuardScenarios.Password, decision, null, null, sectionId: target.SectionId);
    }

    [PostgresFact]
    public async Task Confirmed_allows_reject_refuses_approve_and_certificate_gate_lifts()
    {
        var world = await SeedInvalidatedPlanAsync(concluded: true);
        await _scenarios.UpdateAsync<OosInvestigation>(world.InvestigationId, i => i.Conclusion = OosConclusion.Confirmed);
        var target = new ActionGuardScenarios.Target(world.HeadId, world.OriginId);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsAsync(target));
        Assert.Equal("Oos.ConfirmedCannotBeApproved", error.Code);
        Assert.Equal("A confirmed out-of-specification result cannot be approved.", error.Message);

        await DecideAsAsync(target, ApprovalDecision.Reject);
        await using (var act = fixture.CreateDbContext())
            Assert.NotNull(await TestServiceFactory.SampleSummary(act).GetCertificateSummaryAsync(world.OriginId, null));

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.Rejected, await verify.Samples.AsNoTracking().Where(s => s.Id == world.OriginId)
            .Select(s => s.Status).SingleAsync());
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.EntityType == "Sample" && s.EntityId == world.OriginId);
        Assert.Equal(SignatureMeaning.Rejected, signature.MeaningOfSignature);
        Assert.Equal(world.HeadId, signature.UserId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == world.OriginId.ToString() && a.UserId == world.HeadId && a.Action == "Update");
        AssertAuditedStatus(audit, SampleStatus.UnderApproval, SampleStatus.Rejected);
    }

    [PostgresFact]
    public async Task Approval_availability_reports_open_investigation()
    {
        var world = await SeedInvalidatedPlanAsync(concluded: false);
        await using (var act = fixture.CreateDbContext())
        {
            var scope = new UserSectionScopeService(act);
            var clock = new FixedTimeProvider(new DateTimeOffset(ActionGuardScenarios.Now));
            var documents = new DocumentAuthorizationService(act);
            var service = new ActionAvailabilityService(act, scope,
                TestServiceFactory.SampleReview(act), TestServiceFactory.SampleApproval(act),
                new SampleAssignmentService(act, TestServiceFactory.TestingWorkspace(act), scope, clock),
                new TestSelfAssignmentService(act, scope, clock),
                TestServiceFactory.MediaRelease(act),
                new DocumentApprovalService(act,
                    new AuditEventService(act, fixture.CreateSequenceHelper(act)),
                    documents, new ElectronicSignatureService(act)),
                documents);
            var actions = await service.ForSampleAsync(world.OriginId, null, world.HeadId);
            Assert.NotNull(actions);
            foreach (var key in new[] { "approval.approve", "approval.reject" })
            {
                var action = Assert.Single(actions, a => a.Action == key);
                Assert.False(action.Allowed);
                Assert.Equal("Oos.InvestigationOpen", action.ReasonCode);
                Assert.Equal($"OOS investigation {world.Number} is open.", action.Reason);
                Assert.Equal(world.Number, action.ReasonParams?["number"]);
            }
        }

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking().Where(s => s.Id == world.OriginId)
            .Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == world.OriginId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == world.OriginId.ToString() && a.UserId == world.HeadId));
    }
}
