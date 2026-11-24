using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public sealed class OosConclusionPostgresTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private readonly ActionGuardScenarios _seed = new(fixture);
    private const string Summary = "Investigation findings recorded.";
    private const string Cause = "Documented assignable cause.";

    [PostgresFact]
    public async Task Conclusion_and_reopening_preserve_signed_history()
    {
        var world = await ArrangeAsync();
        await ConcludeAsync(world);
        var closed = await ReadAsync(world.Id);
        Assert.Equal(OosInvestigationStatus.Concluded, closed.Status);
        Assert.Equal(OosConclusion.Confirmed, closed.Conclusion);
        Assert.Equal(world.Category, closed.RootCauseCategoryId);
        Assert.Equal(Cause, closed.RootCauseDescription);
        Assert.Equal(Summary, closed.ConclusionSummary);
        Assert.Equal(world.Head, closed.ConcludedByUserId);
        Assert.NotEqual(world.Version, closed.Version);
        await using (var verify = fixture.CreateDbContext())
        {
            var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == closed.ConclusionSignatureId);
            Assert.Equal(SignatureMeaning.OosInvestigationConcluded, signature.MeaningOfSignature);
            Assert.Equal(world.Id, signature.EntityId);
            Assert.Equal(world.Head, signature.UserId);
            Assert.Equal(closed.ConcludedAtUtc, signature.SignedAt);
            await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.ExpectedVersion = closed.Version;
            await TestServiceFactory.OosInvestigation(act).ReopenAsync(world.Id, "New evidence", ActionGuardScenarios.Password, world.Head, null);
        }

        var reopened = await ReadAsync(world.Id);
        Assert.Equal(OosInvestigationStatus.PhaseTwo, reopened.Status);
        Assert.NotEqual(closed.Version, reopened.Version);
        Assert.Null(reopened.Conclusion);
        Assert.Null(reopened.RootCauseCategoryId);
        Assert.Null(reopened.RootCauseDescription);
        Assert.Null(reopened.ConclusionSummary);
        Assert.Null(reopened.ConcludedAtUtc);
        Assert.Null(reopened.ConcludedByUserId);
        Assert.Null(reopened.ConclusionSignatureId);
        await using var assertDb = fixture.CreateDbContext();
        var history = await assertDb.OosInvestigationReopenings.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
        Assert.Equal(closed.Conclusion, history.PreviousConclusion);
        Assert.Equal(closed.ConclusionSignatureId, history.PreviousConclusionSignatureId);
        Assert.Equal(closed.RootCauseCategoryId, history.PreviousRootCauseCategoryId);
        Assert.Equal(closed.RootCauseDescription, history.PreviousRootCauseDescription);
        Assert.Equal(closed.ConclusionSummary, history.PreviousConclusionSummary);
        Assert.Equal(closed.ConcludedByUserId, history.PreviousConcludedByUserId);
        Assert.Equal(closed.ConcludedAtUtc, history.PreviousConcludedAtUtc);
        Assert.Equal(world.Head, history.ReopenedByUserId);
        var reopeningSignature = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == history.SignatureId);
        Assert.Equal(SignatureMeaning.OosInvestigationReopened, reopeningSignature.MeaningOfSignature);
        Assert.Equal(history.ReopenedAtUtc, reopeningSignature.SignedAt);
        var audit = await assertDb.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigationReopening) &&
            a.EntityId == history.Id.ToString() && a.Action == "Create" && a.UserId == world.Head);
        Assert.Null(audit.PreviousValue);
        Assert.Contains(Summary, audit.NewValue);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.Concluded, OosInvestigationStatus.PhaseTwo);
    }

    [PostgresFact]
    public async Task Conclusion_refuses_unanswered_items()
    {
        var world = await ArrangeAsync(snapshot: """{"items":[{"phase":2,"key":"check"}]}""");
        await RefusedAsync(world, "Oos.PhaseTwoChecklistIncomplete");
    }

    [PostgresFact]
    public async Task Invalidated_without_signed_plan_refused()
    {
        var world = await ArrangeAsync();
        await RefusedAsync(world, "Oos.ConclusionPlanRequired", OosConclusion.Invalidated);
    }

    [PostgresFact]
    public async Task No_cause_found_cannot_invalidate()
    {
        var world = await ArrangeAsync(noCause: true);
        await RefusedAsync(world, "Oos.ConclusionCauseRequired", OosConclusion.Invalidated);
    }

    [PostgresFact]
    public async Task Awaiting_retest_cannot_be_confirmed()
    {
        var world = await ArrangeAsync(status: OosInvestigationStatus.AwaitingRetest);
        await RefusedAsync(world, "Oos.ConclusionPhaseInvalid");
    }

    [PostgresFact]
    public async Task Stale_version_refused()
    {
        var world = await ArrangeAsync();
        await RefusedAsync(world, "Oos.VersionConflict", version: world.Version - 1);
    }

    [PostgresFact]
    public async Task Wrong_password_leaves_conclusion_unchanged()
    {
        var world = await ArrangeAsync();
        await RefusedAsync(world, "Oos.ConclusionPasswordFailed", password: "wrong");
    }

    [PostgresFact]
    public async Task Signer_who_performed_an_investigated_test_refused()
    {
        var world = await ArrangeAsync(performerIsHead: true);
        await RefusedAsync(world, "Oos.SignerPerformedTest");
    }

    [PostgresFact]
    public async Task Other_section_head_refused()
    {
        var world = await ArrangeAsync();
        var other = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage], await _seed.SectionAsync());
        var error = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ConcludeAsync(world, actor: other.Id));
        Assert.Equal("Oos.SectionAccessDenied", error.Code);
        await AssertUnchangedAsync(world);
    }

    [PostgresFact]
    public async Task Conclusion_refused_until_every_planned_retest_sample_is_reviewed()
    {
        var world = await ArrangeAsync();
        await AddPlanAsync(world, reviewed: false);
        await RefusedAsync(world, "Oos.ConclusionRetestsUnreviewed");
    }

    [PostgresFact]
    public async Task Signer_who_performed_a_retest_refused()
    {
        var world = await ArrangeAsync();
        await AddPlanAsync(world, performerIsHead: true);
        await RefusedAsync(world, "Oos.SignerPerformedTest");
    }

    [PostgresFact]
    public async Task Awaiting_retest_uses_phase_one_cause_and_preserves_result_bytes()
    {
        var world = await ArrangeAsync(status: OosInvestigationStatus.AwaitingRetest);
        var child = await AddPlanAsync(world);
        var bytes = await ResultBytesAsync(world.OrderId);
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.OosInvestigation(act).ConcludeAsync(world.Id,
                new(OosConclusion.Invalidated, null, "Ignored input", Summary, ActionGuardScenarios.Password),
                world.Version, world.Head, null);

        var closed = await ReadAsync(world.Id);
        Assert.Equal(OosConclusion.Invalidated, closed.Conclusion);
        Assert.Equal(world.Category, closed.RootCauseCategoryId);
        Assert.Equal(Cause, closed.RootCauseDescription);
        Assert.NotEmpty(bytes);
        Assert.Equal(bytes, await ResultBytesAsync(world.OrderId));
        await AssertStatusAuditAsync(world, OosInvestigationStatus.AwaitingRetest, OosInvestigationStatus.Concluded);
        await using (var verify = fixture.CreateDbContext())
        {
            var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
            Assert.True(link.IsInvalidated);
            Assert.Equal(closed.ConcludedAtUtc, link.InvalidatedAtUtc);
            Assert.True(await verify.TestOrders.AsNoTracking().Where(o => o.Id == world.OrderId).Select(o => o.IsSuperseded).SingleAsync());
            await AssertBooleanAuditAsync(nameof(OosInvestigationResult), link.Id, world.Head, nameof(link.IsInvalidated));
            await AssertBooleanAuditAsync(nameof(TestOrder), world.OrderId, world.Head, nameof(TestOrder.IsSuperseded));
        }

        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, world.SampleId, ApprovalDecision.Approve));
        Assert.Equal("Oos.RetestSamplesNotApproved", blocked.Code);
        await DecideAsync(world, child.SampleId, ApprovalDecision.Approve);
        await DecideAsync(world, world.SampleId, ApprovalDecision.Approve);
        await AssertDecisionAsync(world, world.SampleId, SectionSignoffStatus.Approved);
        await AssertDecisionAsync(world, child.SampleId, SectionSignoffStatus.Approved);
        await AssertReopeningRefusedAsync(world, closed.Version);
    }

    [PostgresFact]
    public async Task Confirmed_then_approve_refused_reject_allowed()
    {
        var world = await ArrangeAsync();
        await ConcludeAsync(world);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, world.SampleId, ApprovalDecision.Approve));
        Assert.Equal("Oos.ConfirmedCannotBeApproved", error.Code);
        await DecideAsync(world, world.SampleId, ApprovalDecision.Reject);
        await AssertDecisionAsync(world, world.SampleId, SectionSignoffStatus.Rejected);
        var closed = await ReadAsync(world.Id);
        await AssertReopeningRefusedAsync(world, closed.Version);
    }

    [PostgresFact]
    public async Task Phase_two_entries_require_phase_and_an_investigation_local_hypothesis()
    {
        var early = await ArrangeAsync(status: OosInvestigationStatus.PhaseOne);
        Assert.Equal("Oos.EntryInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            EntryAsync(early, OosEntryKind.Hypothesis))).Code);
        await AssertUnchangedAsync(early);
        var world = await ArrangeAsync();
        await EntryAsync(world, OosEntryKind.Hypothesis);
        int parent;
        await using (var verify = fixture.CreateDbContext())
            parent = await verify.OosInvestigationEntrys.AsNoTracking().Where(e => e.InvestigationId == world.Id).Select(e => e.Id).SingleAsync();
        var other = await ArrangeAsync();
        Assert.Equal("Oos.EntryParentInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            EntryAsync(other, OosEntryKind.HypothesisOutcome, parent))).Code);
        await AssertUnchangedAsync(other);
        var current = await ReadAsync(world.Id);
        await EntryAsync(world with { Version = current.Version }, OosEntryKind.HypothesisOutcome, parent);

        await using var assertDb = fixture.CreateDbContext();
        var entries = await assertDb.OosInvestigationEntrys.AsNoTracking().Where(e => e.InvestigationId == world.Id)
            .OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(OosEntryKind.Hypothesis, entries[0].Kind);
        Assert.Null(entries[0].ParentEntryId);
        Assert.Equal(OosEntryKind.HypothesisOutcome, entries[1].Kind);
        Assert.Equal(parent, entries[1].ParentEntryId);
        Assert.NotEqual(world.Version, current.Version);
        Assert.NotEqual(current.Version, (await ReadAsync(world.Id)).Version);
        foreach (var entry in entries)
        {
            Assert.Equal(world.Head, entry.RecordedByUserId);
            Assert.Equal(2, entry.Phase);
            Assert.Equal(Summary, entry.Text);
            var audit = await assertDb.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigationEntry) &&
                a.EntityId == entry.Id.ToString() && a.UserId == world.Head && a.Action == "Create");
            Assert.Null(audit.PreviousValue);
            Assert.Contains(Summary, audit.NewValue);
        }
    }

    [PostgresFact]
    public async Task Manual_open_requires_a_reviewed_failing_section_and_is_audited()
    {
        var world = await ArrangeAsync(createInvestigation: false);
        var passing = await ArrangeAsync(createInvestigation: false, failing: false);
        await using (var act = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<NotFoundException>(() => TestServiceFactory.OosInvestigation(act)
                .OpenManuallyAsync(world.SampleId, passing.SectionId, 0u, passing.Head));
            Assert.Equal("Sample.NotFound", error.Code);
        }
        Assert.Equal("Oos.ManualOpenUnavailable", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            OpenAsync(passing))).Code);
        var openedId = await OpenAsync(world);
        Assert.Equal("Oos.ManualOpenUnavailable", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            OpenAsync(world))).Code);

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == passing.SampleId));
        var opened = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.Id == openedId);
        Assert.Equal(OosInvestigationStatus.PhaseOne, opened.Status);
        Assert.Equal(world.Head, opened.OpenedByUserId);
        Assert.Equal(world.OrderId, Assert.Single(opened.Results).TestOrderId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == openedId.ToString() && a.UserId == world.Head && a.Action == "Create");
        Assert.Null(audit.PreviousValue);
        Assert.Contains(opened.Number, audit.NewValue);
    }

    [PostgresFact]
    public async Task Conclusion_waits_for_retest_join_and_rejects_the_stale_version()
    {
        var world = await ArrangeAsync();
        var retest = await AddPlanAsync(world, failing: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        await using var join = fixture.CreateDbContext();
        await using var sign = fixture.CreateDbContext();
        await using var observer = fixture.CreateDbContext();
        await sign.Database.OpenConnectionAsync(ct);
        var pid = ((Npgsql.NpgsqlConnection)sign.Database.GetDbConnection()).ProcessID;
        await using var transaction = await join.Database.BeginTransactionAsync(ct);
        var sample = await join.Samples.Include(s => s.TestOrders).SingleAsync(s => s.Id == retest.SampleId, ct);
        join.CurrentUserId = world.Reviewer;
        var joined = await TestServiceFactory.OosInvestigation(join)
            .OpenForReviewedSectionAsync(sample, world.SectionId, sample.TestOrders, world.Reviewer, ct);
        Assert.Equal(world.Id, joined?.Id);

        var waiting = TestServiceFactory.OosInvestigation(sign).ConcludeAsync(world.Id,
            new(OosConclusion.Confirmed, world.Category, Cause, Summary, ActionGuardScenarios.Password),
            world.Version, world.Head, null, ct);
        var locked = false;
        try
        {
            while (!waiting.IsCompleted)
            {
                var blocked = await observer.Database.SqlQuery<string>($"""
                    SELECT query AS "Value" FROM pg_stat_activity
                    WHERE pid = {pid} AND cardinality(pg_blocking_pids(pid)) > 0
                    """).SingleOrDefaultAsync(ct);
                if (blocked != null)
                {
                    locked = blocked.Contains("FOR UPDATE", StringComparison.Ordinal) &&
                        blocked.Contains("OosInvestigations", StringComparison.Ordinal);
                    break;
                }
                await Task.Delay(10, ct);
            }
            await join.SaveChangesAsync(ct);
        }
        finally
        {
            await transaction.CommitAsync(CancellationToken.None);
        }
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => waiting);
        Assert.True(locked);
        Assert.Equal("Oos.VersionConflict", error.Code);
        await using var verify = fixture.CreateDbContext();
        var investigation = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.Id == world.Id);
        Assert.Null(investigation.ConclusionSignatureId);
        Assert.NotEqual(world.Version, investigation.Version);
        var added = Assert.Single(investigation.Results, r => r.TestOrderId == retest.OrderId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigationResult) &&
            a.EntityId == added.Id.ToString() && a.UserId == world.Reviewer && a.Action == "Create");
        Assert.Null(audit.PreviousValue);
        Assert.Contains(nameof(OosInvestigationResult.OutcomeAtOpening), audit.NewValue);
    }

    [PostgresFact]
    public async Task Retest_cannot_join_when_conclusion_wins_the_row_lock()
    {
        var world = await ArrangeAsync();
        var retest = await AddPlanAsync(world, failing: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = timeout.Token;
        await using var sign = fixture.CreateDbContext();
        await using var join = fixture.CreateDbContext();
        await using var observer = fixture.CreateDbContext();
        await join.Database.OpenConnectionAsync(ct);
        var pid = ((Npgsql.NpgsqlConnection)join.Database.GetDbConnection()).ProcessID;
        var child = await join.Samples.Include(s => s.TestOrders).SingleAsync(s => s.Id == retest.SampleId, ct);
        join.CurrentUserId = world.Reviewer;
        await using var joinTransaction = await join.Database.BeginTransactionAsync(ct);
        await using var signTransaction = await sign.Database.BeginTransactionAsync(ct);
        await TestServiceFactory.OosInvestigation(sign).ConcludeAsync(world.Id,
            new(OosConclusion.Confirmed, world.Category, Cause, Summary, ActionGuardScenarios.Password),
            world.Version, world.Head, null, ct);

        var opening = TestServiceFactory.OosInvestigation(join)
            .OpenForReviewedSectionAsync(child, world.SectionId, child.TestOrders, world.Reviewer, ct);
        var blocked = false;
        try
        {
            while (!opening.IsCompleted)
            {
                blocked = await observer.Database.SqlQuery<bool>($"""
                    SELECT cardinality(pg_blocking_pids({pid})) > 0 AS "Value"
                    """).SingleAsync(ct);
                if (blocked) break;
                await Task.Delay(10, ct);
            }
        }
        finally
        {
            await signTransaction.CommitAsync(CancellationToken.None);
        }
        var opened = await opening;
        Assert.True(blocked);
        Assert.NotNull(opened);
        Assert.Equal(retest.SampleId, opened.SampleId);
        await join.SaveChangesAsync(ct);
        await joinTransaction.CommitAsync(ct);

        await using var verify = fixture.CreateDbContext();
        var origin = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.Id == world.Id);
        Assert.Equal(OosInvestigationStatus.Concluded, origin.Status);
        Assert.NotNull(origin.ConclusionSignatureId);
        Assert.Equal(world.OrderId, Assert.Single(origin.Results).TestOrderId);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
        var separate = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.Id == opened.Id);
        Assert.Equal(OosInvestigationStatus.PhaseOne, separate.Status);
        Assert.Equal(retest.OrderId, Assert.Single(separate.Results).TestOrderId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == separate.Id.ToString() && a.UserId == world.Reviewer && a.Action == "Create");
        Assert.Null(audit.PreviousValue);
        Assert.Contains(separate.Number, audit.NewValue);
    }

    [PostgresFact]
    public async Task Failing_retest_while_awaiting_retest_blocks_conclusion()
    {
        var world = await ArrangeAsync(status: OosInvestigationStatus.AwaitingRetest);
        var retest = await AddPlanAsync(world, failing: true);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.OosInvestigationResults.Add(new()
            {
                InvestigationId = world.Id, TestOrderId = retest.OrderId,
                OutcomeAtOpening = OosOutcomeClass.Fails, AddedAtUtc = ActionGuardScenarios.Now
            });
            await arrange.SaveChangesAsync();
        }
        await RefusedAsync(world, "Oos.ConclusionFailingRetest", OosConclusion.Invalidated);
    }

    [PostgresFact]
    public async Task Certificate_refuses_an_invalidated_origin_with_multiple_retests()
    {
        var world = await ArrangeAsync();
        await AddPlanAsync(world);
        await AddPlanAsync(world, revision: 2);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        await using var act = fixture.CreateDbContext();
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new OosGate(act, new OosOpeningRule(act)).EnsureCertificateAllowedAsync(world.SampleId));
        Assert.Equal("Oos.CertificateMultipleRetests", refused.Code);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
    }

    private async Task DecideAsync(World world, int sampleId, ApprovalDecision decision)
    {
        await using var act = fixture.CreateDbContext();
        act.CurrentUserId = world.Head;
        await TestServiceFactory.SampleApproval(act).DecideAsync(sampleId, world.Head, ActionGuardScenarios.Password,
            decision, "Decided", null, sectionId: world.SectionId);
    }

    private async Task AssertDecisionAsync(World world, int sampleId, SectionSignoffStatus status)
    {
        await using var verify = fixture.CreateDbContext();
        var row = await verify.SampleSectionSignoffs.AsNoTracking().SingleAsync(s => s.SampleId == sampleId && s.SectionId == world.SectionId);
        Assert.Equal(status, row.Status);
        Assert.Equal(world.Head, row.ApprovedByUserId);
        Assert.NotNull(row.ApprovedAt);
        Assert.NotNull(row.ApprovalSignatureId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(SampleSectionSignoff) &&
            a.EntityId == row.Id.ToString() && a.UserId == world.Head && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)SectionSignoffStatus.UnderApproval, before.RootElement.GetProperty(nameof(row.Status)).GetInt32());
        Assert.Equal((int)status, after.RootElement.GetProperty(nameof(row.Status)).GetInt32());
    }

    private async Task AssertReopeningRefusedAsync(World world, uint version)
    {
        await using (var act = fixture.CreateDbContext())
        {
            act.ExpectedVersion = version;
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.OosInvestigation(act)
                .ReopenAsync(world.Id, "New evidence", ActionGuardScenarios.Password, world.Head, null));
            Assert.Equal("Oos.ReopenSectionDecided", error.Code);
        }
        Assert.Equal(version, (await ReadAsync(world.Id)).Version);
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigationReopenings.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == nameof(OosInvestigation) &&
            s.EntityId == world.Id && s.MeaningOfSignature == SignatureMeaning.OosInvestigationReopened));
    }

    private async Task<string[]> ResultBytesAsync(int order)
    {
        await using var verify = fixture.CreateDbContext();
        return (await verify.Database.SqlQuery<string>($"""
            SELECT r.xmin::text || row_to_json(r)::text AS "Value" FROM "Results" r WHERE "TestOrderId" = {order} ORDER BY "Id"
            """).ToListAsync()).ToArray();
    }

    private async Task AssertBooleanAuditAsync(string entity, int id, int actor, string field)
    {
        await using var verify = fixture.CreateDbContext();
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == entity &&
            a.EntityId == id.ToString() && a.UserId == actor && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.False(before.RootElement.GetProperty(field).GetBoolean());
        Assert.True(after.RootElement.GetProperty(field).GetBoolean());
    }

    private async Task<int> OpenAsync(World world)
    {
        await using var act = fixture.CreateDbContext();
        return (await TestServiceFactory.OosInvestigation(act).OpenManuallyAsync(world.SampleId, world.SectionId, world.Head)).Id;
    }

    private async Task EntryAsync(World world, OosEntryKind kind, int? parent = null)
    {
        await using var act = fixture.CreateDbContext();
        act.ExpectedVersion = world.Version;
        await TestServiceFactory.OosInvestigation(act).AddEntryAsync(world.Id, kind, Summary, parent, world.Head);
    }

    private async Task<(int SampleId, int OrderId)> AddPlanAsync(World world, bool reviewed = true, bool performerIsHead = false,
        bool failing = false, int revision = 1)
    {
        await using var arrange = fixture.CreateDbContext();
        var section = await arrange.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == world.SectionId);
        var analyst = await _seed.AnalystAsync(section);
        var child = reviewed
            ? await _seed.UnderApprovalAsync(section, world.Reviewer, analystId: analyst.Id)
            : await _seed.UnderReviewAsync(section, analystId: analyst.Id);
        var row = new OosRetestPlan
        {
            InvestigationId = world.Id, Revision = revision, RetestCount = 1, JudgingRule = Summary,
            AmendmentReason = revision > 1 ? "Revised plan" : null,
            TestOrderIdsJson = $"[{world.OrderId}]", AnalystUserIdsJson = $"[{analyst.Id}]"
        };
        arrange.OosRetestPlans.Add(row);
        await arrange.SaveChangesAsync();
        var signature = await new ElectronicSignatureService(arrange).SignAsync(world.Head, ActionGuardScenarios.Password,
            SignatureMeaning.OosRetestPlanApproved, nameof(OosRetestPlan), row.Id, null, null);
        await arrange.SaveChangesAsync();
        row.SignedAtUtc = ActionGuardScenarios.Now;
        row.SignedByUserId = world.Head;
        row.SignatureId = signature.Id;
        row.SamplesCreatedAtUtc = ActionGuardScenarios.Now;
        arrange.OosRetestPlanSamples.Add(new() { PlanId = row.Id, SampleId = child.Sample.Id });
        arrange.Results.Add(new Result
        {
            TestOrderId = child.Orders[0].Id, EnteredByUserId = performerIsHead ? world.Head : analyst.Id,
            InterpretedValue = failing ? nameof(ResultStatus.Detected) : nameof(ResultStatus.NotDetected)
        });
        await arrange.SaveChangesAsync();
        return (child.Sample.Id, child.Orders[0].Id);
    }

    private async Task RefusedAsync(World world, string code, OosConclusion conclusion = OosConclusion.Confirmed,
        uint? version = null, string password = ActionGuardScenarios.Password)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ConcludeAsync(world, conclusion, version, password));
        Assert.Equal(code, error.Code);
        await AssertUnchangedAsync(world);
    }

    private async Task AssertUnchangedAsync(World world)
    {
        var after = await ReadAsync(world.Id);
        Assert.Equal(world.Version, after.Version);
        Assert.Null(after.ConclusionSignatureId);
        Assert.Null(after.Conclusion);
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == nameof(OosInvestigation) &&
            s.EntityId == world.Id && s.MeaningOfSignature == SignatureMeaning.OosInvestigationConcluded));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == world.Id.ToString() && a.Action == "Update"));
    }

    private async Task ConcludeAsync(World world, OosConclusion conclusion = OosConclusion.Confirmed,
        uint? version = null, string password = ActionGuardScenarios.Password, int? actor = null)
    {
        await using var act = fixture.CreateDbContext();
        await TestServiceFactory.OosInvestigation(act).ConcludeAsync(world.Id,
            new ConcludeRequest(conclusion, world.Category, Cause, Summary, password), version ?? world.Version, actor ?? world.Head, null);
    }

    private async Task AssertStatusAuditAsync(World world, OosInvestigationStatus before, OosInvestigationStatus after)
    {
        await using var verify = fixture.CreateDbContext();
        var rows = await verify.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(OosInvestigation) &&
            a.EntityId == world.Id.ToString() && a.UserId == world.Head && a.Action == "Update").ToListAsync();
        Assert.Contains(rows, row => ReadStatus(row.PreviousValue) == before && ReadStatus(row.NewValue) == after);
    }

    private static OosInvestigationStatus ReadStatus(string? value)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(value));
        var status = document.RootElement.GetProperty(nameof(OosInvestigation.Status));
        return status.ValueKind == JsonValueKind.String
            ? Enum.Parse<OosInvestigationStatus>(status.GetString()!)
            : (OosInvestigationStatus)status.GetInt32();
    }

    private async Task<OosInvestigation> ReadAsync(int id)
    {
        await using var verify = fixture.CreateDbContext();
        return await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == id);
    }

    private sealed record World(int Id, int Head, int Category, uint Version, int SampleId, int SectionId, int OrderId, int Reviewer);

    private async Task<World> ArrangeAsync(string snapshot = "{}", bool noCause = false, bool performerIsHead = false,
        OosInvestigationStatus status = OosInvestigationStatus.PhaseTwo, bool createInvestigation = true, bool failing = true)
    {
        var section = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage, ..ActionGuardScenarios.HeadCodes], section);
        var reviewer = await _seed.ReviewerAsync(section);
        var analyst = await _seed.AnalystAsync(section);
        var origin = await _seed.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        var order = origin.Orders[0];
        await using var arrange = fixture.CreateDbContext();
        var category = await arrange.OosCauseCategories.AsNoTracking().Where(c => c.IsNoCauseFound == noCause).OrderBy(c => c.Id).FirstAsync();
        var investigation = new OosInvestigation
        {
            Number = $"TEST-{Guid.NewGuid():N}"[..20], SampleId = origin.Sample.Id, SectionId = section.Id,
            Kind = OosInvestigationKind.Oos, Status = status, TemplateSnapshotJson = snapshot,
            OpenedByUserId = reviewer.Id, OpenedAtUtc = ActionGuardScenarios.Now,
            PhaseOneCauseCategoryId = status == OosInvestigationStatus.AwaitingRetest ? category.Id : null,
            PhaseOneCauseDescription = status == OosInvestigationStatus.AwaitingRetest ? Cause : null,
            Results = [new() { TestOrderId = order.Id, OutcomeAtOpening = OosOutcomeClass.Fails, AddedAtUtc = ActionGuardScenarios.Now }]
        };
        if (createInvestigation)
            arrange.OosInvestigations.Add(investigation);
        arrange.Results.Add(new Result
        {
            TestOrderId = order.Id, EnteredByUserId = performerIsHead ? head.Id : analyst.Id,
            InterpretedValue = failing ? nameof(ResultStatus.Detected) : nameof(ResultStatus.NotDetected)
        });
        await arrange.SaveChangesAsync();
        return new(investigation.Id, head.Id, category.Id, investigation.Version, origin.Sample.Id, section.Id, order.Id, reviewer.Id);
    }
}
