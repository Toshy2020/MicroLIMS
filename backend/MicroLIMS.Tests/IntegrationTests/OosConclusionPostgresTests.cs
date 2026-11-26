using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
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
    public async Task Failing_retest_moves_awaiting_investigation_to_phase_two()
    {
        var world = await ArrangeAsync(status: OosInvestigationStatus.AwaitingRetest);
        var retest = await AddPlanAsync(world, reviewed: false, failing: true);
        await _seed.CompleteReviewAsync(new(world.Reviewer, retest.SampleId, world.SectionId));
        var moved = await ReadAsync(world.Id);
        Assert.Equal(OosInvestigationStatus.PhaseTwo, moved.Status);
        Assert.NotEqual(world.Version, moved.Version);
        await using (var verify = fixture.CreateDbContext())
        {
            var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r =>
                r.InvestigationId == world.Id && r.TestOrderId == retest.OrderId);
            Assert.False(link.IsInvalidated);
            var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigation) &&
                a.EntityId == world.Id.ToString() && a.UserId == world.Reviewer && a.Action == "Update");
            Assert.Equal(OosInvestigationStatus.AwaitingRetest, ReadStatus(audit.PreviousValue));
            Assert.Equal(OosInvestigationStatus.PhaseTwo, ReadStatus(audit.NewValue));
            Assert.Contains("failing retest", audit.Reason);
            var unrelated = await verify.AuditLogs.AsNoTracking().Where(a => a.UserId == world.Reviewer && a.Action == "Update" &&
                ((a.EntityName == nameof(SampleSectionSignoff) && a.SampleId == retest.SampleId) ||
                 (a.EntityName == nameof(Sample) && a.EntityId == retest.SampleId.ToString()) ||
                 (a.EntityName == nameof(TestOrder) && a.EntityId == retest.OrderId.ToString()))).ToListAsync();
            Assert.Equal(3, unrelated.Count);
            Assert.All(unrelated, row => Assert.DoesNotContain("failing retest", row.Reason ?? ""));

            var creation = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(OosInvestigationResult) &&
                a.EntityId == link.Id.ToString() && a.Action == "Create" && a.UserId == world.Reviewer);
            Assert.Null(creation.PreviousValue);
            Assert.Contains(nameof(link.TestOrderId), creation.NewValue);
        }
        await ConcludeAsync(world with { Version = moved.Version });
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
    }

    [PostgresFact]
    public async Task Confirmed_reverses_invalidation_from_signed_phase_one_after_failing_retest()
    {
        var world = await SignPhaseOneErrorAsync();
        var bytes = await ResultBytesAsync(world.OrderId);
        var awaiting = await ReadAsync(world.Id);
        Assert.Equal(OosInvestigationStatus.AwaitingRetest, awaiting.Status);
        Assert.NotNull(awaiting.PhaseOneSignatureId);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseOne, OosInvestigationStatus.AwaitingRetest);
        int linkId;
        await using (var verify = fixture.CreateDbContext())
        {
            var phaseOneSignature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == awaiting.PhaseOneSignatureId);
            Assert.Equal(SignatureMeaning.OosPhaseOneConcluded, phaseOneSignature.MeaningOfSignature);
            var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
            Assert.True(link.IsInvalidated);
            linkId = link.Id;
        }
        await AssertBooleanAuditAsync(nameof(OosInvestigationResult), linkId, world.Head, nameof(OosInvestigationResult.IsInvalidated));
        await RefuseRawReversalAsync(linkId);
        await _seed.UpdateAsync<TestOrder>(world.OrderId, order => order.IsSuperseded = true);
        var retest = await AddPlanAsync(world, reviewed: false, failing: true);
        await _seed.CompleteReviewAsync(new(world.Reviewer, retest.SampleId, world.SectionId));
        var phaseTwo = await ReadAsync(world.Id);
        Assert.Equal(OosInvestigationStatus.PhaseTwo, phaseTwo.Status);
        await ConcludeAsync(world with { Version = phaseTwo.Version });

        var closed = await ReadAsync(world.Id);
        Assert.Equal(OosConclusion.Confirmed, closed.Conclusion);
        Assert.Equal(world.Head, closed.ConcludedByUserId);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
        await using var assertDb = fixture.CreateDbContext();
        var signature = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == closed.ConclusionSignatureId);
        Assert.Equal(SignatureMeaning.OosInvestigationConcluded, signature.MeaningOfSignature);
        var persistedLink = await assertDb.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.Id == linkId);
        Assert.False(persistedLink.IsInvalidated);
        Assert.Null(persistedLink.InvalidatedAtUtc);
        Assert.False(await assertDb.TestOrders.AsNoTracking().Where(o => o.Id == world.OrderId).Select(o => o.IsSuperseded).SingleAsync());
        await AssertBooleanAuditAsync(nameof(TestOrder), world.OrderId, world.Head, nameof(TestOrder.IsSuperseded), from: true);
        await AssertBooleanAuditAsync(nameof(OosInvestigationResult), linkId, world.Head, nameof(OosInvestigationResult.IsInvalidated), from: true);
        Assert.Equal(bytes, await ResultBytesAsync(world.OrderId));
    }

    [PostgresFact]
    public async Task Awaiting_retest_keeps_the_signed_phase_one_category_after_deactivation()
    {
        var world = await SignPhaseOneErrorAsync();
        var awaiting = await ReadAsync(world.Id);
        await AddPlanAsync(world);
        await _seed.UpdateAsync<OosCauseCategory>(world.Category, category => category.IsActive = false);
        await ConcludeAsync(world with { Version = awaiting.Version }, OosConclusion.Invalidated);
        var closed = await ReadAsync(world.Id);
        Assert.Equal(OosConclusion.Invalidated, closed.Conclusion);
        Assert.Equal(world.Category, closed.RootCauseCategoryId);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.AwaitingRetest, OosInvestigationStatus.Concluded);
    }

    private async Task<World> SignPhaseOneErrorAsync()
    {
        var world = await ArrangeAsync(status: OosInvestigationStatus.PhaseOne,
            snapshot: """{"items":[{"phase":1,"key":"check"}]}""");
        int analyst;
        await using (var verify = fixture.CreateDbContext())
            analyst = await verify.Results.AsNoTracking().Where(r => r.TestOrderId == world.OrderId)
                .Select(r => r.EnteredByUserId).SingleAsync();
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.OosInvestigation(act).AddAnalystStatementAsync(world.Id, Summary, analyst);
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.OosInvestigation(act).AnswerChecklistAsync(world.Id, 1, "check", OosChecklistAnswer.Yes, null, analyst);
        var current = await ReadAsync(world.Id);
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.OosInvestigation(act).SignPhaseOneAsync(world.Id,
                new(OosPhaseOneAssessment.LaboratoryErrorConfirmed, world.Category, Cause, ActionGuardScenarios.Password),
                current.Version, world.Head, null);
        return world;
    }

    private async Task RefuseRawReversalAsync(int linkId)
    {
        await using var act = fixture.CreateDbContext();
        var refused = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => act.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "Id" = {linkId}
            """));
        Assert.Equal(Npgsql.PostgresErrorCodes.RaiseException, refused.SqlState);
    }

    [PostgresFact]
    public async Task Invalidation_leaves_joined_failing_retest_untouched_and_refuses_approval()
    {
        var world = await ArrangeAsync();
        var retest = await AddPlanAsync(world, reviewed: false, failing: true);
        await _seed.CompleteReviewAsync(new(world.Reviewer, retest.SampleId, world.SectionId));
        var before = await ReadAsync(world.Id);
        var bytes = await ResultBytesAsync(retest.OrderId);
        await ConcludeAsync(world with { Version = before.Version }, OosConclusion.Invalidated);

        await using var verify = fixture.CreateDbContext();
        var links = await verify.OosInvestigationResults.AsNoTracking().Where(r => r.InvestigationId == world.Id).ToListAsync();
        var origin = Assert.Single(links, r => r.TestOrderId == world.OrderId);
        var child = Assert.Single(links, r => r.TestOrderId == retest.OrderId);
        Assert.True(origin.IsInvalidated);
        Assert.False(child.IsInvalidated);
        Assert.Null(child.InvalidatedAtUtc);
        Assert.False(await verify.TestOrders.AsNoTracking().Where(o => o.Id == retest.OrderId).Select(o => o.IsSuperseded).SingleAsync());
        Assert.Equal(bytes, await ResultBytesAsync(retest.OrderId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(OosInvestigationResult) &&
            a.EntityId == child.Id.ToString() && a.Action == "Update"));
        await AssertBooleanAuditAsync(nameof(OosInvestigationResult), origin.Id, world.Head, nameof(origin.IsInvalidated));
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, retest.SampleId, ApprovalDecision.Approve));
        Assert.Equal("Oos.FailingRetestCannotBeApproved", refused.Code);
        Assert.Equal(SectionSignoffStatus.UnderApproval, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == retest.SampleId).Select(s => s.Status).SingleAsync());
    }

    [PostgresFact]
    public Task Reopening_reverses_invalidation_and_allows_confirmation() =>
        AssertReopenThenConcludeAsync(OosConclusion.Confirmed);

    [PostgresFact]
    public Task Reopening_reverses_invalidation_and_can_reapply_it() =>
        AssertReopenThenConcludeAsync(OosConclusion.Invalidated);

    private async Task AssertReopenThenConcludeAsync(OosConclusion next)
    {
        var world = await ArrangeAsync();
        await AddPlanAsync(world);
        var bytes = await ResultBytesAsync(world.OrderId);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        var closed = await ReadAsync(world.Id);
        await ReopenAsync(world, closed.Version);
        var reopened = await ReadAsync(world.Id);
        Assert.Null(reopened.Conclusion);
        Assert.Equal(OosInvestigationStatus.PhaseTwo, reopened.Status);
        Assert.NotEqual(closed.Version, reopened.Version);
        await AssertOriginFlagsAsync(world, false);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.Concluded, OosInvestigationStatus.PhaseTwo);
        await using (var verify = fixture.CreateDbContext())
        {
            var history = await verify.OosInvestigationReopenings.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
            Assert.Equal(OosConclusion.Invalidated, history.PreviousConclusion);
            Assert.Equal(closed.ConclusionSignatureId, history.PreviousConclusionSignatureId);
            Assert.Equal(closed.ConclusionSummary, history.PreviousConclusionSummary);
        }
        await ConcludeAsync(world with { Version = reopened.Version }, next);
        Assert.Equal(next, (await ReadAsync(world.Id)).Conclusion);
        await AssertOriginFlagsAsync(world, next == OosConclusion.Invalidated);
        if (next == OosConclusion.Invalidated)
        {
            int linkId;
            await using (var verify = fixture.CreateDbContext())
                linkId = await verify.OosInvestigationResults.AsNoTracking().Where(r => r.InvestigationId == world.Id)
                    .Select(r => r.Id).SingleAsync();
            await AssertBooleanAuditAsync(nameof(OosInvestigationResult), linkId, world.Head, nameof(OosInvestigationResult.IsInvalidated));
            await AssertBooleanAuditAsync(nameof(TestOrder), world.OrderId, world.Head, nameof(TestOrder.IsSuperseded));

            await using var act = fixture.CreateDbContext();
            var refused = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "Id" = {linkId}
                """));
            Assert.Equal(Npgsql.PostgresErrorCodes.RaiseException, refused.SqlState);
            await AssertOriginFlagsAsync(world, true);
        }
        Assert.Equal(bytes, await ResultBytesAsync(world.OrderId));
    }

    [PostgresFact]
    public async Task Reopening_refusals_preserve_conclusion_signature_and_flags()
    {
        var world = await ArrangeAsync();
        var open = await Assert.ThrowsAsync<BusinessRuleException>(() => ReopenAsync(world, world.Version));
        Assert.Equal("Oos.ReopenNotConcluded", open.Code);
        await AssertUnchangedAsync(world);

        await AddPlanAsync(world);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        var closed = await ReadAsync(world.Id);
        var password = await Assert.ThrowsAsync<BusinessRuleException>(() => ReopenAsync(world, closed.Version, "wrong"));
        Assert.Equal("Oos.ReopenPasswordFailed", password.Code);
        var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => ReopenAsync(world, world.Version));
        Assert.Equal("Oos.VersionConflict", stale.Code);
        var other = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage], await _seed.SectionAsync());
        var scope = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => ReopenAsync(world, closed.Version, actor: other.Id));
        Assert.Equal("Oos.SectionAccessDenied", scope.Code);

        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.Results.Add(new Result { TestOrderId = world.OrderId, EnteredByUserId = world.Head,
                InterpretedValue = nameof(ResultStatus.Detected) });
            await arrange.SaveChangesAsync();
        }
        var performer = await Assert.ThrowsAsync<BusinessRuleException>(() => ReopenAsync(world, closed.Version));
        Assert.Equal("Oos.SignerPerformedTest", performer.Code);
        var current = await ReadAsync(world.Id);
        Assert.Equal(closed.Version, current.Version);
        Assert.Equal(closed.ConclusionSignatureId, current.ConclusionSignatureId);
        Assert.Equal(closed.Conclusion, current.Conclusion);
        await AssertOriginFlagsAsync(world, true);
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigationReopenings.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityId == world.Id &&
            s.EntityType == nameof(OosInvestigation) && s.MeaningOfSignature == SignatureMeaning.OosInvestigationReopened));
    }

    [PostgresFact]
    public async Task Entry_refuses_wrong_scope_missing_permission_and_closed_investigation()
    {
        var world = await ArrangeAsync();
        await using var arrange = fixture.CreateDbContext();
        var section = await arrange.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == world.SectionId);
        var unauthorized = await _seed.UserAsync(RoleType.Analyst, [], section);
        var outsider = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage], await _seed.SectionAsync());
        foreach (var (actor, code) in new[] { (unauthorized.Id, "Oos.InvestigationAccessDenied"), (outsider.Id, "Oos.SectionAccessDenied") })
        {
            await using var act = fixture.CreateDbContext();
            var refused = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => TestServiceFactory.OosInvestigation(act)
                .AddEntryAsync(world.Id, OosEntryKind.Note, Summary, null, actor));
            Assert.Equal(code, refused.Code);
        }
        await AssertUnchangedAsync(world);
        await ConcludeAsync(world);
        var closed = await ReadAsync(world.Id);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => EntryAsync(world with { Version = closed.Version }, OosEntryKind.Note));
        Assert.Equal("Oos.EntryClosed", error.Code);
        Assert.Equal(closed.Version, (await ReadAsync(world.Id)).Version);
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigationEntrys.AsNoTracking().AnyAsync(e => e.InvestigationId == world.Id));
    }

    [PostgresFact]
    public async Task Manual_open_refuses_stale_sample_version_and_missing_permission()
    {
        var world = await ArrangeAsync(createInvestigation: false);
        await using var arrange = fixture.CreateDbContext();
        var section = await arrange.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == world.SectionId);
        var unauthorized = await _seed.UserAsync(RoleType.Analyst, [], section);
        var version = await arrange.Samples.AsNoTracking().Where(s => s.Id == world.SampleId).Select(s => s.Version).SingleAsync();
        await using (var act = fixture.CreateDbContext())
        {
            var service = TestServiceFactory.OosInvestigation(act);
            var denied = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => service.OpenManuallyAsync(world.SampleId, world.SectionId, version, unauthorized.Id));
            Assert.Equal("Oos.InvestigationAccessDenied", denied.Code);
        }
        await using (var act = fixture.CreateDbContext())
        {
            var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.OosInvestigation(act)
                .OpenManuallyAsync(world.SampleId, world.SectionId, version - 1, world.Head));
            Assert.Equal("Oos.VersionConflict", stale.Code);
        }
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == world.SampleId));
        Assert.Equal(version, await verify.Samples.AsNoTracking().Where(s => s.Id == world.SampleId).Select(s => s.Version).SingleAsync());
    }

    [PostgresFact]
    public async Task Confirmed_requires_a_described_active_category()
    {
        var world = await ArrangeAsync(noCause: true);
        await using (var act = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.OosInvestigation(act)
                .ConcludeAsync(world.Id, new(OosConclusion.Confirmed, null, null, Summary, ActionGuardScenarios.Password),
                    world.Version, world.Head, null));
            Assert.Equal("Oos.ConclusionCauseDetailsRequired", error.Code);
        }
        await AssertUnchangedAsync(world);
        await ConcludeAsync(world);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
    }

    [PostgresFact]
    public async Task Trigger_refuses_retest_reversal_even_during_origin_reopening()
    {
        var world = await ArrangeAsync();
        var retest = await AddPlanAsync(world, reviewed: false, failing: true);
        await _seed.CompleteReviewAsync(new(world.Reviewer, retest.SampleId, world.SectionId));
        var reviewed = await ReadAsync(world.Id);
        await ConcludeAsync(world with { Version = reviewed.Version }, OosConclusion.Invalidated);
        var closed = await ReadAsync(world.Id);

        int linkId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var link = await arrange.OosInvestigationResults.SingleAsync(r =>
                r.InvestigationId == world.Id && r.TestOrderId == retest.OrderId);
            // Exercise the trigger against a historically invalidated retest link.
            link.IsInvalidated = true;
            link.InvalidatedAtUtc = ActionGuardScenarios.Now;
            await arrange.SaveChangesAsync();
            linkId = link.Id;
        }

        await using (var act = fixture.CreateDbContext())
        {
            await using var transaction = await act.Database.BeginTransactionAsync();
            act.ExpectedVersion = closed.Version;
            await TestServiceFactory.OosInvestigation(act).ReopenAsync(world.Id, "New evidence", ActionGuardScenarios.Password, world.Head, null);
            var refused = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "Id" = {linkId}
                """));
            Assert.Equal(Npgsql.PostgresErrorCodes.RaiseException, refused.SqlState);
            await transaction.RollbackAsync();
        }
        Assert.Equal(closed.Version, (await ReadAsync(world.Id)).Version);
        await using var verify = fixture.CreateDbContext();
        var links = await verify.OosInvestigationResults.AsNoTracking().Where(r => r.InvestigationId == world.Id).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.True(link.IsInvalidated));
        Assert.False(await verify.OosInvestigationReopenings.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id));
    }

    [PostgresFact]
    public async Task Trigger_refuses_reversal_in_transaction_reopening_a_confirmed_conclusion()
    {
        var world = await ArrangeAsync();
        await ConcludeAsync(world);
        var closed = await ReadAsync(world.Id);
        var linkId = await ArrangeInvalidatedLinkAsync(world);
        await using (var act = fixture.CreateDbContext())
        {
            await using var tx = await act.Database.BeginTransactionAsync();
            act.ExpectedVersion = closed.Version;
            await TestServiceFactory.OosInvestigation(act).ReopenAsync(world.Id, "New evidence", ActionGuardScenarios.Password, world.Head, null);
            await AssertTriggerRejectsAsync(act, $"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "Id" = {linkId}
                """);
            await tx.RollbackAsync();
        }
        await AssertInvalidationWasNotReversedAsync(world, linkId, closed.Version);
    }

    [PostgresFact]
    public async Task Trigger_refuses_changes_to_other_columns_even_with_valid_confirmation_evidence()
    {
        var world = await ArrangeAsync();
        var linkId = await ArrangeInvalidatedLinkAsync(world);
        await using (var act = fixture.CreateDbContext())
        {
            await using var tx = await act.Database.BeginTransactionAsync();
            await TestServiceFactory.OosInvestigation(act).ConcludeAsync(world.Id,
                new(OosConclusion.Confirmed, world.Category, Cause, Summary, ActionGuardScenarios.Password),
                world.Version, world.Head, null);
            Assert.Equal(1, await act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = true, "InvalidatedAtUtc" = {ActionGuardScenarios.Now}
                WHERE "Id" = {linkId}
                """));
            await AssertTriggerRejectsAsync(act, $"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL,
                    "ResultSummaryAtOpening" = 'Changed' WHERE "Id" = {linkId}
                """);
            await tx.RollbackAsync();
        }
        await AssertInvalidationWasNotReversedAsync(world, linkId, world.Version);
    }

    [PostgresFact]
    public async Task Trigger_refuses_forged_confirmation_evidence_with_wrong_signature_meaning()
    {
        var world = await ArrangeAsync();
        var linkId = await ArrangeInvalidatedLinkAsync(world);
        await using (var act = fixture.CreateDbContext())
        {
            await using var tx = await act.Database.BeginTransactionAsync();
            act.Database.AutoSavepointsEnabled = false;
            var unrelated = await new ElectronicSignatureService(act).SignAsync(world.Head, ActionGuardScenarios.Password,
                SignatureMeaning.OosPhaseOneConcluded, nameof(OosInvestigation), world.Id, null, null);
            await act.SaveChangesAsync();

            Assert.Equal(1, await act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosInvestigations" SET "Status" = {(int)OosInvestigationStatus.Concluded},
                    "Conclusion" = {(int)OosConclusion.Confirmed}, "ConcludedByUserId" = {world.Head},
                    "ConclusionSignatureId" = {unrelated.Id} WHERE "Id" = {world.Id}
                """));
            await AssertTriggerRejectsAsync(act, $"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "Id" = {linkId}
                """);
            await tx.RollbackAsync();
        }
        await AssertInvalidationWasNotReversedAsync(world, linkId, world.Version);
    }

    [PostgresFact]
    public async Task Trigger_refuses_forged_reopening_whose_previous_signature_is_from_the_same_transaction()
    {
        var world = await ArrangeAsync();
        await AddPlanAsync(world);
        var linkId = await ArrangeInvalidatedLinkAsync(world);
        await using (var act = fixture.CreateDbContext())
        {
            await using var tx = await act.Database.BeginTransactionAsync();
            await TestServiceFactory.OosInvestigation(act).ConcludeAsync(world.Id,
                new(OosConclusion.Invalidated, world.Category, Cause, Summary, ActionGuardScenarios.Password),
                world.Version, world.Head, null);
            var prior = await act.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == world.Id);
            act.Database.AutoSavepointsEnabled = false;
            var signature = await new ElectronicSignatureService(act).SignAsync(world.Head, ActionGuardScenarios.Password,
                SignatureMeaning.OosInvestigationReopened, nameof(OosInvestigation), world.Id, null, null);
            await act.SaveChangesAsync();
            act.OosInvestigationReopenings.Add(new OosInvestigationReopening
            {
                InvestigationId = world.Id, PreviousConclusion = OosConclusion.Invalidated,
                PreviousConclusionSignatureId = prior.ConclusionSignatureId!.Value,
                SignatureId = signature.Id, Reason = "Forged evidence", ReopenedByUserId = world.Head,
                ReopenedAtUtc = ActionGuardScenarios.Now
            });
            await act.SaveChangesAsync();
            await AssertTriggerRejectsAsync(act, $"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "Id" = {linkId}
                """);
            await tx.RollbackAsync();
        }
        await AssertInvalidationWasNotReversedAsync(world, linkId, world.Version);
    }

    private async Task<int> ArrangeInvalidatedLinkAsync(World world)
    {
        await using var arrange = fixture.CreateDbContext();
        var link = await arrange.OosInvestigationResults.SingleAsync(r => r.InvestigationId == world.Id);
        link.IsInvalidated = true;
        link.InvalidatedAtUtc = ActionGuardScenarios.Now;
        await arrange.SaveChangesAsync();
        return link.Id;
    }

    private async Task AssertInvalidationWasNotReversedAsync(World world, int linkId, uint expectedVersion)
    {
        await using var verify = fixture.CreateDbContext();
        var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.Id == linkId);
        Assert.True(link.IsInvalidated);
        Assert.Equal(ActionGuardScenarios.Now, link.InvalidatedAtUtc);
        Assert.Equal("", link.ResultSummaryAtOpening);
        Assert.Equal(expectedVersion, (await ReadAsync(world.Id)).Version);
        Assert.False(await verify.OosInvestigationReopenings.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id));
    }

    private static async Task AssertTriggerRejectsAsync(MicroLimsDbContext act, FormattableString sql)
    {
        var refused = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => act.Database.ExecuteSqlInterpolatedAsync(sql));
        Assert.Equal(Npgsql.PostgresErrorCodes.RaiseException, refused.SqlState);
    }

    [PostgresFact]
    public async Task Inactive_cause_and_missing_description_refuse_conclusion()
    {
        var world = await ArrangeAsync();
        int inactive;
        await using (var arrange = fixture.CreateDbContext())
        {
            var category = new OosCauseCategory { Name = $"Inactive-{Guid.NewGuid():N}", IsActive = false };
            arrange.OosCauseCategories.Add(category);
            await arrange.SaveChangesAsync();
            inactive = category.Id;
        }
        foreach (var (categoryId, description, code) in new[]
        {
            (inactive, (string?)Cause, "Oos.ConclusionCauseInactive"),
            (world.Category, (string?)null, "Oos.ConclusionCauseDetailsRequired")
        })
        {
            await using var act = fixture.CreateDbContext();
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.OosInvestigation(act)
                .ConcludeAsync(world.Id, new(OosConclusion.Confirmed, categoryId, description, Summary, ActionGuardScenarios.Password),
                    world.Version, world.Head, null));
            Assert.Equal(code, error.Code);
        }
        await AssertUnchangedAsync(world);
    }

    private async Task AssertOriginFlagsAsync(World world, bool invalidated)
    {
        await using var verify = fixture.CreateDbContext();
        var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
        Assert.Equal(invalidated, link.IsInvalidated);
        Assert.Equal(invalidated, link.InvalidatedAtUtc.HasValue);
        Assert.Equal(invalidated, await verify.TestOrders.AsNoTracking().Where(o => o.Id == world.OrderId).Select(o => o.IsSuperseded).SingleAsync());
        if (!invalidated)
        {
            await AssertBooleanAuditAsync(nameof(OosInvestigationResult), link.Id, world.Head, nameof(link.IsInvalidated), from: true);
            await AssertBooleanAuditAsync(nameof(TestOrder), world.OrderId, world.Head, nameof(TestOrder.IsSuperseded), from: true);
        }
    }

    private async Task ReopenAsync(World world, uint version, string password = ActionGuardScenarios.Password, int? actor = null)
    {
        await using var act = fixture.CreateDbContext();
        act.ExpectedVersion = version;
        await TestServiceFactory.OosInvestigation(act).ReopenAsync(world.Id, "New evidence", password, actor ?? world.Head, null);
    }

    [PostgresFact]
    public async Task Certificate_refuses_an_invalidated_origin_when_plan_has_multiple_samples_without_reportable_retest()
    {
        var world = await ArrangeAsync();
        await AddTwoSamplePlanAsync(world, reportableNumber: null);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        await using var act = fixture.CreateDbContext();
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new OosGate(act, new OosOpeningRule(act)).EnsureCertificateAllowedAsync(world.SampleId));
        Assert.Equal("Oos.CertificateMultipleRetests", refused.Code);
        Assert.Equal("A certificate cannot be issued because the signed retest plan does not name the reportable retest.", refused.Message);
        await AssertStatusAuditAsync(world, OosInvestigationStatus.PhaseTwo, OosInvestigationStatus.Concluded);
    }

    [PostgresFact]
    public async Task Certificate_allows_invalidated_origin_when_plan_names_reportable_sample()
    {
        var world = await ArrangeAsync();
        var (_, s2) = await AddTwoSamplePlanAsync(world, reportableNumber: 2);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        await using var act = fixture.CreateDbContext();
        await new OosGate(act, new OosOpeningRule(act)).EnsureCertificateAllowedAsync(world.SampleId);
        var resolved = await OosReportableRetest.ResolveAsync(act, world.SampleId);
        Assert.False(resolved.HasUnclear);
        Assert.Equal(s2, resolved.ByInvestigationId[world.Id].ReportableSampleId);
    }

    private async Task<(int Sample1Id, int Sample2Id)> AddTwoSamplePlanAsync(World world, int? reportableNumber)
    {
        await using var arrange = fixture.CreateDbContext();
        var section = await arrange.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == world.SectionId);
        var analyst1 = await _seed.AnalystAsync(section);
        var analyst2 = await _seed.AnalystAsync(section);
        var c1 = await _seed.UnderApprovalAsync(section, world.Reviewer, analystId: analyst1.Id);
        var c2 = await _seed.UnderApprovalAsync(section, world.Reviewer, analystId: analyst2.Id);
        var row = new OosRetestPlan
        {
            InvestigationId = world.Id, Revision = 1, RetestCount = 2, JudgingRule = Summary,
            ReportableRetestNumber = reportableNumber,
            TestOrderIdsJson = $"[{world.OrderId}]", AnalystUserIdsJson = $"[{analyst1.Id},{analyst2.Id}]"
        };
        arrange.OosRetestPlans.Add(row);
        await arrange.SaveChangesAsync();
        var signature = await new ElectronicSignatureService(arrange).SignAsync(world.Head, ActionGuardScenarios.Password,
            SignatureMeaning.OosRetestPlanApproved, nameof(OosRetestPlan), row.Id, null, null);
        await arrange.SaveChangesAsync();
        arrange.OosRetestPlanSamples.AddRange(
            new OosRetestPlanSample { PlanId = row.Id, SampleId = c1.Sample.Id },
            new OosRetestPlanSample { PlanId = row.Id, SampleId = c2.Sample.Id });
        arrange.Results.AddRange(
            new Result { TestOrderId = c1.Orders[0].Id, EnteredByUserId = analyst1.Id, InterpretedValue = nameof(ResultStatus.NotDetected) },
            new Result { TestOrderId = c2.Orders[0].Id, EnteredByUserId = analyst2.Id, InterpretedValue = nameof(ResultStatus.NotDetected) });
        await arrange.SaveChangesAsync();

        row.ReportableSampleId = reportableNumber switch { 1 => c1.Sample.Id, 2 => c2.Sample.Id, _ => null };
        row.SignedAtUtc = ActionGuardScenarios.Now;
        row.SignedByUserId = world.Head;
        row.SignatureId = signature.Id;
        row.SamplesCreatedAtUtc = ActionGuardScenarios.Now;
        await arrange.SaveChangesAsync();
        return (c1.Sample.Id, c2.Sample.Id);
    }

    [PostgresFact]
    public async Task Certificate_uses_highest_signed_revision_when_amendments_exist()
    {
        var world = await ArrangeAsync();
        await AddPlanAsync(world, revision: 1);
        var (rev2Sample, _) = await AddPlanAsync(world, revision: 2);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        await using var act = fixture.CreateDbContext();
        await new OosGate(act, new OosOpeningRule(act)).EnsureCertificateAllowedAsync(world.SampleId);
        var resolved = await OosReportableRetest.ResolveAsync(act, world.SampleId);
        Assert.False(resolved.HasUnclear);
        Assert.Equal(rev2Sample, resolved.ByInvestigationId[world.Id].ReportableSampleId);
        Assert.Equal(2, resolved.ByInvestigationId[world.Id].PlanRevision);
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

    private async Task AssertBooleanAuditAsync(string entity, int id, int actor, string field, bool from = false)
    {
        await using var verify = fixture.CreateDbContext();
        var audit = await verify.AuditLogs.AsNoTracking().Where(a => a.EntityName == entity &&
            a.EntityId == id.ToString() && a.UserId == actor && a.Action == "Update").OrderByDescending(a => a.Id).FirstAsync();
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal(from, before.RootElement.GetProperty(field).GetBoolean());
        Assert.Equal(!from, after.RootElement.GetProperty(field).GetBoolean());
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
        var analyst = await _seed.UserAsync(RoleType.Analyst,
            [PermissionConstants.OosContribute, ..ActionGuardScenarios.AnalystCodes], section);
        var origin = await _seed.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        var order = origin.Orders[0];
        await using var arrange = fixture.CreateDbContext();
        var category = noCause
            ? await arrange.OosCauseCategories.AsNoTracking().SingleAsync(c => c.IsNoCauseFound)
            : new OosCauseCategory { Name = $"Cause-{Guid.NewGuid():N}", IsActive = true };
        if (!noCause)
        {
            arrange.OosCauseCategories.Add(category);
            await arrange.SaveChangesAsync();
        }
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
