using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public sealed class OosRetestPlanPostgresTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private readonly ActionGuardScenarios _seed = new(fixture);

    [PostgresFact]
    public async Task Sign_creates_exactly_planned_samples()
    {
        var world = await ArrangeAsync(otherSectionInvestigation: true);
        var plan = await DraftAsync(world);
        var before = await VersionAsync(world);
        await SignAsync(world, plan.Id);

        await using var verify = fixture.CreateDbContext();
        var row = await verify.OosRetestPlans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == row.SignatureId);
        Assert.Equal(nameof(OosRetestPlan), signature.EntityType);
        Assert.Equal(plan.Id, signature.EntityId);
        Assert.Equal(SignatureMeaning.OosRetestPlanApproved, signature.MeaningOfSignature);
        Assert.Equal(world.Head, signature.UserId);
        Assert.Equal(world.Head, row.SignedByUserId);
        Assert.Equal(signature.SignedAt, row.SignedAtUtc);
        Assert.Equal(row.SignedAtUtc, row.SamplesCreatedAtUtc);
        var investigation = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == world.Id);
        Assert.NotEqual(before, investigation.Version);
        Assert.Equal(OosInvestigationStatus.AwaitingRetest, investigation.Status);
        var origin = await verify.Samples.AsNoTracking().Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .SingleAsync(s => s.Id == world.Origin);
        Assert.Equal(SampleStatus.UnderApproval, origin.Status);
        Assert.Equal(SectionSignoffStatus.UnderApproval, Assert.Single(origin.SectionSignoffs).Status);
        Assert.Null(Assert.Single(origin.SectionSignoffs).ApprovalSignatureId);
        Assert.All(origin.TestOrders, o => Assert.False(o.IsSuperseded));
        var links = await verify.OosRetestPlanSamples.AsNoTracking().Where(l => l.PlanId == plan.Id).OrderBy(l => l.SampleId).ToListAsync();
        var samples = await verify.Samples.AsNoTracking().Include(s => s.TestOrders)
            .Where(s => s.OriginSampleId == origin.Id).OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(2, samples.Count);
        Assert.Equal(samples.Select(s => s.Id), links.Select(l => l.SampleId));
        Assert.Equal(2, samples.Select(s => s.ReferenceNumber).Distinct().Count());
        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            Assert.Equal(investigation.Number, sample.OosGroupCode);
            Assert.NotEqual(origin.OosGroupCode, sample.OosGroupCode);
            Assert.Equal(world.Head, sample.ReceivedByUserId);
            var order = Assert.Single(sample.TestOrders);
            Assert.Equal(world.Analysts[i], order.AssignedAnalystId);
            Assert.Equal(origin.TestOrders.Single(o => o.Id == world.FailingOrder).TestCode, order.TestCode);
            Assert.Equal(world.Section.Id, order.SectionId);
            var audit = await AuditAsync(verify, nameof(Sample), sample.Id, world.Head, "Create");
            Assert.Null(audit.PreviousValue);
            using var payload = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
            Assert.Equal(investigation.Number, payload.RootElement.GetProperty(nameof(Sample.OosGroupCode)).GetString());
        }
        var update = await AuditAsync(verify, nameof(OosRetestPlan), plan.Id, world.Head, "Update");
        using var oldPlan = JsonDocument.Parse(Assert.IsType<string>(update.PreviousValue));
        using var newPlan = JsonDocument.Parse(Assert.IsType<string>(update.NewValue));
        Assert.Equal(JsonValueKind.Null, oldPlan.RootElement.GetProperty(nameof(OosRetestPlan.SignedAtUtc)).ValueKind);
        var auditedAt = newPlan.RootElement.GetProperty(nameof(OosRetestPlan.SignedAtUtc)).GetDateTime();
        Assert.InRange((auditedAt - row.SignedAtUtc!.Value).Ticks, 0, 9);
    }

    [PostgresFact]
    public async Task Plan_refused_before_phase_one_signed()
    {
        var world = await ArrangeAsync(phaseOneSigned: false);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world));
        Assert.Equal("Phase I must be concluded before a retest is planned.", error.Message);
        await AssertNoPlanAsync(world, world.Head);
    }

    [PostgresFact]
    public async Task Unsigned_plan_creates_nothing()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var plan = await DraftAsync(world);
        Assert.NotEqual(version, await VersionAsync(world));
        await AssertUnsignedAsync(world, plan.Id);
        await using var verify = fixture.CreateDbContext();
        var audit = await AuditAsync(verify, nameof(OosRetestPlan), plan.Id, world.Head, "Create");
        Assert.Null(audit.PreviousValue);
        using var payload = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal(1, payload.RootElement.GetProperty(nameof(OosRetestPlan.Revision)).GetInt32());
    }

    [PostgresFact]
    public async Task Wrong_password_creates_nothing()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world, plan.Id, password: "wrong"));
        Assert.Equal("Oos.RetestPlanPasswordFailed", check.Code);
        await AssertUnsignedAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Sign_revalidates_orders_after_drafting()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        await _seed.UpdateAsync<TestOrder>(world.FailingOrder, o => o.IsSuperseded = true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world, plan.Id));
        Assert.Equal("Oos.RetestPlanOrdersInvalid", error.Code);
        await AssertUnsignedAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Signer_who_performed_test_refused()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.Results.Add(new Result { TestOrderId = world.FailingOrder, EnteredByUserId = world.Head, InterpretedValue = nameof(ResultStatus.Detected) });
            await arrange.SaveChangesAsync();
        }

        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world, plan.Id));
        Assert.Equal("Oos.SignerPerformedTest", check.Code);
        await AssertUnsignedAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Signer_listed_as_analyst_refused()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world, Request(world) with { RetestCount = 1, AnalystUserIds = [world.Head] });
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world, plan.Id));
        Assert.Equal("Oos.RetestPlanSignerIsAnalyst", check.Code);
        await AssertUnsignedAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Other_section_head_refused()
    {
        var world = await ArrangeAsync();
        var other = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage], await _seed.SectionAsync());
        var check = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => DraftAsync(world, actor: other.Id));
        Assert.Equal("Oos.SectionAccessDenied", check.Code);
        await AssertNoPlanAsync(world, other.Id);
        var plan = await DraftAsync(world);
        await using var act = fixture.CreateDbContext();
        var current = await VersionAsync(world);
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            Service(act).SignAndCreateAsync(plan.Id, ActionGuardScenarios.Password, current, other.Id, null));
        await AssertUnsignedAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Plan_with_non_investigated_order_refused()
    {
        var world = await ArrangeAsync();
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DraftAsync(world, Request(world) with { TestOrderIds = [world.OtherOrder] }));
        Assert.Equal("Oos.RetestPlanOrdersInvalid", check.Code);
        await AssertNoPlanAsync(world, world.Head);
    }

    [PostgresFact]
    public async Task Original_analyst_allowed_on_retained_retest_but_refused_on_new_sample()
    {
        var world = await ArrangeAsync();
        var request = Request(world) with { RetestCount = 1, AnalystUserIds = [world.OriginalAnalyst] };
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DraftAsync(world, request with { Source = OosRetestSource.NewSample }));
        Assert.Equal("Oos.RetestPlanOriginalAnalyst", check.Code);
        await AssertNoPlanAsync(world, world.Head);
        var retained = await DraftAsync(world, request);
        await SignAsync(world, retained.Id);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(world.OriginalAnalyst, await verify.TestOrders.AsNoTracking()
            .Where(o => o.Sample != null && o.Sample.OriginSampleId == world.Origin).Select(o => o.AssignedAnalystId).SingleAsync());
    }

    [PostgresFact]
    public async Task Amendment_needs_reason_and_new_signature()
    {
        var world = await ArrangeAsync();
        var first = await DraftAsync(world, Request(world) with { RetestCount = 10, AnalystUserIds = Enumerable.Repeat(world.Analysts[0], 10).ToArray() });
        await SignAsync(world, first.Id);
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world));
        Assert.Equal("Oos.RetestPlanAmendmentReasonRequired", check.Code);
        var second = await DraftAsync(world, Request(world) with { AmendmentReason = "Repeat after documented review." });
        await AssertUnsignedAsync(world, second.Id, existingSampleCount: 10);
        await SignAsync(world, second.Id);
        await using var verify = fixture.CreateDbContext();
        var signatures = await verify.ElectronicSignatures.AsNoTracking().Where(s => s.EntityType == nameof(OosRetestPlan) &&
            (s.EntityId == first.Id || s.EntityId == second.Id)).OrderBy(s => s.EntityId).ToListAsync();
        Assert.Equal(2, signatures.Count);
        Assert.Equal("Repeat after documented review.", signatures[1].Comment);
        Assert.Equal(12, await verify.Samples.AsNoTracking().CountAsync(s => s.OriginSampleId == world.Origin));
    }

    [PostgresFact]
    public async Task Draft_after_sign_is_new_revision_signed_row_unchanged()
    {
        var world = await ArrangeAsync();
        var first = await DraftAsync(world);
        await SignAsync(world, first.Id);
        var second = await DraftAsync(world, Request(world) with { AmendmentReason = "Additional retest.", JudgingRule = "Revised instruction." });
        var stale = await VersionAsync(world);
        var edited = await DraftAsync(world, Request(world) with { AmendmentReason = "Additional retest.", JudgingRule = "Updated draft." });
        Assert.Equal(second.Id, edited.Id);
        Assert.Equal(2, edited.Revision);
        await using (var act = fixture.CreateDbContext())
        {
            var check = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Service(act).SignAndCreateAsync(second.Id, ActionGuardScenarios.Password, stale, world.Head, null));
            Assert.Equal("Oos.VersionConflict", check.Code);
        }
        await AssertUnsignedAsync(world, second.Id, existingSampleCount: 2);
        await using var verify = fixture.CreateDbContext();
        var signed = await verify.OosRetestPlans.AsNoTracking().SingleAsync(x => x.Id == first.Id);
        Assert.Equal(1, signed.Revision);
        Assert.Equal(Request(world).JudgingRule, signed.JudgingRule);
        Assert.NotNull(signed.SignatureId);
    }

    [PostgresFact]
    public async Task Second_sign_creates_nothing()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        await SignAsync(world, plan.Id);
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world, plan.Id));
        Assert.Equal("Oos.RetestPlanAlreadySigned", check.Code);
        await AssertExactlyOneSignAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Concurrent_signs_create_samples_once()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        var version = await VersionAsync(world);

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        await using var observer = fixture.CreateDbContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        await second.Database.OpenConnectionAsync(ct);
        var pid = ((Npgsql.NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;
        await using var transaction = await first.Database.BeginTransactionAsync(ct);
        await Service(first).SignAndCreateAsync(plan.Id, ActionGuardScenarios.Password, version, world.Head, null, ct);
        var waiting = Service(second).SignAndCreateAsync(plan.Id, ActionGuardScenarios.Password, version, world.Head, null, ct);
        var blockedOnInvestigation = false;
        try
        {
            while (!waiting.IsCompleted)
            {
                var blocked = await observer.Database.SqlQuery<bool>($"""
                    SELECT EXISTS (
                        SELECT 1 FROM pg_stat_activity
                        WHERE pid = {pid} AND cardinality(pg_blocking_pids(pid)) > 0
                        AND query LIKE '%FOR UPDATE%' AND query LIKE '%OosInvestigations%'
                    ) AS "Value"
                    """).SingleAsync(ct);
                if (blocked)
                {
                    blockedOnInvestigation = true;
                    break;
                }
                if (await observer.Database.SqlQuery<bool>(
                    $"SELECT cardinality(pg_blocking_pids({pid})) > 0 AS \"Value\"").SingleAsync(ct))
                {
                    // Recheck the statement in case the second signer just reached the lock.
                    blockedOnInvestigation = await observer.Database.SqlQuery<bool>($"""
                        SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {pid}
                            AND query LIKE '%FOR UPDATE%' AND query LIKE '%OosInvestigations%') AS "Value"
                        """).SingleAsync(ct);
                    break;
                }
                await Task.Delay(10, ct);
            }
        }
        finally
        {
            await transaction.CommitAsync();
        }
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => waiting);
        Assert.Equal("Oos.RetestPlanAlreadySigned", check.Code);
        Assert.True(blockedOnInvestigation);
        await AssertExactlyOneSignAsync(world, plan.Id);
    }

    [PostgresFact]
    public async Task Passing_plan_retest_cannot_be_approved_while_open()
    {
        var world = await ArrangeAsync();
        var child = await CreatePassingRetestAsync(world);
        var check = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, child, ApprovalDecision.Approve));
        Assert.Equal("Oos.InvestigationOpen", check.Code);
        await AssertNoDecisionAsync(world, child);
    }

    [PostgresFact]
    public async Task Retest_sample_is_gated_until_conclusion()
    {
        var world = await ArrangeAsync();
        var child = await CreatePassingRetestAsync(world);
        var reject = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, child, ApprovalDecision.Reject));
        Assert.Equal("Oos.InvestigationOpen", reject.Code);
        foreach (var decision in new[] { ApprovalDecision.RetestRetainedSample, ApprovalDecision.NewSampleRequest })
        {
            var check = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, child, decision));
            Assert.Equal("Oos.RetestNeedsApprovedPlan", check.Code);
        }
        await using (var act = fixture.CreateDbContext())
        {
            var certificate = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                new OosGate(act, new OosOpeningRule(act)).EnsureCertificateAllowedAsync(child));
            Assert.Equal("Oos.InvestigationOpen", certificate.Code);
        }
        await AssertNoDecisionAsync(world, child);

        await ConcludeAsync(world, OosConclusion.Confirmed);
        var approval = await Assert.ThrowsAsync<BusinessRuleException>(() => DecideAsync(world, child, ApprovalDecision.Approve));
        Assert.Equal("Oos.ConfirmedCannotBeApproved", approval.Code);
        await AssertNoDecisionAsync(world, child);
        await DecideAsync(world, child, ApprovalDecision.Reject);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.Rejected, await verify.Samples.AsNoTracking().Where(s => s.Id == child).Select(s => s.Status).SingleAsync());
        Assert.Equal("Sample Reject", (await verify.ArchivedRecords.AsNoTracking().SingleAsync(r =>
            r.EntityType == "Sample" && r.EntityId == child)).Reason);
        _ = await AuditAsync(verify, nameof(Sample), child, world.Head, "Update");
    }

    [PostgresFact]
    public async Task Origin_is_not_finalised_automatically_when_retests_close()
    {
        var world = await ArrangeAsync();
        var child = await CreatePassingRetestAsync(world);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        await DecideAsync(world, child, ApprovalDecision.Approve);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.Approved, await verify.Samples.AsNoTracking().Where(s => s.Id == child).Select(s => s.Status).SingleAsync());
        Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking().Where(s => s.Id == world.Origin).Select(s => s.Status).SingleAsync());
        Assert.Equal(SectionSignoffStatus.UnderApproval, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == world.Origin).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == world.Origin));
        _ = await AuditAsync(verify, nameof(Sample), child, world.Head, "Update");
    }

    [PostgresFact]
    public async Task Existing_investigation_prevents_legacy_propagation()
    {
        var world = await ArrangeAsync();
        var child = await CreatePassingRetestAsync(world);
        await ConcludeAsync(world, OosConclusion.Invalidated);
        await using (var arrange = fixture.CreateDbContext())
        {
            var origin = await arrange.Samples.Include(s => s.SectionSignoffs).SingleAsync(s => s.Id == world.Origin);
            origin.Status = SampleStatus.RetestRequested;
            var signoff = Assert.Single(origin.SectionSignoffs);
            signoff.Status = SectionSignoffStatus.RetestRequested;
            signoff.ApprovalDecision = ApprovalDecision.RetestRetainedSample;
            await arrange.SaveChangesAsync();
        }
        await DecideAsync(world, child, ApprovalDecision.Approve);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SectionSignoffStatus.RetestRequested, await verify.SampleSectionSignoffs.AsNoTracking()
            .Where(s => s.SampleId == world.Origin).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == world.Origin));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(Sample) &&
            a.EntityId == world.Origin.ToString() && a.UserId == world.Head));
        _ = await AuditAsync(verify, nameof(Sample), child, world.Head, "Update");
    }

    [PostgresFact]
    public async Task Retest_plan_sample_links_reject_update_delete_and_truncate()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        await SignAsync(world, plan.Id);
        await using var act = fixture.CreateDbContext();
        foreach (var sql in new[]
        {
            $"""UPDATE "OosRetestPlanSamples" SET "SampleId" = "SampleId" WHERE "PlanId" = {plan.Id}""",
            $"""DELETE FROM "OosRetestPlanSamples" WHERE "PlanId" = {plan.Id}""",
            """TRUNCATE "OosRetestPlanSamples" """
        })
        {
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => act.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(Npgsql.PostgresErrorCodes.RaiseException, error.SqlState);
            Assert.Contains("OosRetestPlanSamples", error.MessageText);
        }
        await AssertExactlyOneSignAsync(world, plan.Id);
    }

    private async Task<int> CreatePassingRetestAsync(World world)
    {
        var plan = await DraftAsync(world, Request(world) with { RetestCount = 1, AnalystUserIds = [world.Analysts[0]] });
        await SignAsync(world, plan.Id);
        await using var arrange = fixture.CreateDbContext();
        var sample = await arrange.Samples.Include(s => s.TestOrders).SingleAsync(s => s.OriginSampleId == world.Origin);
        sample.Status = SampleStatus.UnderApproval;
        var order = Assert.Single(sample.TestOrders);
        order.Status = ApprovalStatus.Reviewed;
        order.CurrentStep = WorkflowStep.Reviewed;
        arrange.Results.Add(new Result
        {
            TestOrderId = order.Id, EnteredByUserId = world.Analysts[0], InterpretedValue = nameof(ResultStatus.Absent)
        });
        arrange.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id, SectionId = world.Section.Id, Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = world.Reviewer, ReviewedAt = ActionGuardScenarios.Now
        });
        await arrange.SaveChangesAsync();
        return sample.Id;
    }

    private Task ConcludeAsync(World world, OosConclusion conclusion) =>
        _seed.UpdateAsync<OosInvestigation>(world.Id, i =>
        {
            i.Status = OosInvestigationStatus.Concluded;
            i.Conclusion = conclusion;
        });

    private async Task DecideAsync(World world, int sampleId, ApprovalDecision decision)
    {
        await using var act = fixture.CreateDbContext();
        act.CurrentUserId = world.Head;
        await TestServiceFactory.SampleApproval(act).DecideAsync(sampleId, world.Head, ActionGuardScenarios.Password, decision, null, null);
    }

    private async Task AssertNoDecisionAsync(World world, int sampleId)
    {
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.UnderApproval,
            await verify.Samples.AsNoTracking().Where(s => s.Id == sampleId).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == "Sample" && s.EntityId == sampleId));
        Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == sampleId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(Sample) &&
            a.EntityId == sampleId.ToString() && a.UserId == world.Head && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task Failing_to_persist_links_rolls_back_signature_samples_and_audit()
    {
        var world = await ArrangeAsync();
        var plan = await DraftAsync(world);
        var version = await VersionAsync(world);
        await using (var act = fixture.CreateDbContext(new RefusePlanLinkInsert()))
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                Service(act).SignAndCreateAsync(plan.Id, ActionGuardScenarios.Password, version, world.Head, null));
            Assert.Equal("Test failure writing the retest link.", Assert.IsType<InvalidOperationException>(error.InnerException).Message);
        }
        await AssertUnsignedAsync(world, plan.Id);
        Assert.Equal(version, await VersionAsync(world));
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.Head &&
            a.EntityName == nameof(Sample) && a.Action == "Create"));
    }

    private sealed class RefusePlanLinkInsert : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"OosRetestPlanSamples\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Test failure writing the retest link.");
            return ValueTask.FromResult(result);
        }
    }

    [PostgresFact]
    public async Task Signer_who_performed_previously_planned_retest_refused()
    {
        var world = await ArrangeAsync();
        var first = await DraftAsync(world);
        await SignAsync(world, first.Id);
        var next = await DraftAsync(world, Request(world) with { AmendmentReason = "Additional observations required." });
        await using (var arrange = fixture.CreateDbContext())
        {
            var orderId = await arrange.TestOrders.AsNoTracking()
                .Where(o => o.Sample != null && o.Sample.OriginSampleId == world.Origin)
                .OrderBy(o => o.Id).Select(o => o.Id).FirstAsync();
            arrange.Results.Add(new Result { TestOrderId = orderId, EnteredByUserId = world.Head, InterpretedValue = nameof(ResultStatus.Absent) });
            await arrange.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world, next.Id));
        Assert.Equal("Oos.SignerPerformedTest", error.Code);
        await AssertUnsignedAsync(world, next.Id, existingSampleCount: 2);
    }

    private async Task AssertExactlyOneSignAsync(World world, int planId)
    {
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(2, await verify.Samples.AsNoTracking().CountAsync(s => s.OriginSampleId == world.Origin));
        Assert.Equal(2, await verify.OosRetestPlanSamples.AsNoTracking().CountAsync(l => l.PlanId == planId));
        Assert.Equal(1, await verify.ElectronicSignatures.AsNoTracking().CountAsync(s =>
            s.EntityType == nameof(OosRetestPlan) && s.EntityId == planId));
        _ = await AuditAsync(verify, nameof(OosRetestPlan), planId, world.Head, "Update");
    }

    private async Task AssertUnsignedAsync(World world, int planId, int existingSampleCount = 0)
    {
        await using var verify = fixture.CreateDbContext();
        var row = await verify.OosRetestPlans.AsNoTracking().SingleAsync(p => p.Id == planId);
        Assert.Null(row.SignedAtUtc);
        Assert.Null(row.SignedByUserId);
        Assert.Null(row.SignatureId);
        Assert.Null(row.SamplesCreatedAtUtc);
        var updates = await verify.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(OosRetestPlan) &&
            a.EntityId == planId.ToString() && a.Action == "Update").Select(a => a.NewValue).ToListAsync();
        foreach (var update in updates)
        {
            using var fields = JsonDocument.Parse(Assert.IsType<string>(update));
            Assert.Equal(JsonValueKind.Null, fields.RootElement.GetProperty(nameof(OosRetestPlan.SignedAtUtc)).ValueKind);
        }
        Assert.Equal(existingSampleCount, await verify.Samples.AsNoTracking().CountAsync(s => s.OriginSampleId == world.Origin));
        Assert.False(await verify.OosRetestPlanSamples.AsNoTracking().AnyAsync(l => l.PlanId == planId));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == nameof(OosRetestPlan) && s.EntityId == planId));
    }

    private async Task AssertNoPlanAsync(World world, int actor)
    {
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosRetestPlans.AsNoTracking().AnyAsync(p => p.InvestigationId == world.Id));
        Assert.False(await verify.Samples.AsNoTracking().AnyAsync(s => s.OriginSampleId == world.Origin));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == actor && a.EntityName == nameof(OosRetestPlan)));
    }

    private sealed record World(int Id, int Origin, DocumentSection Section, int Head, int Reviewer, int OriginalAnalyst, int FailingOrder, int OtherOrder, int[] Analysts);

    private async Task<World> ArrangeAsync(bool phaseOneSigned = true, bool otherSectionInvestigation = false)
    {
        var section = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage, ..ActionGuardScenarios.HeadCodes], section);
        var reviewer = await _seed.ReviewerAsync(section);
        var originalAnalyst = await _seed.AnalystAsync(section);
        var analysts = new[] { (await _seed.AnalystAsync(section)).Id, (await _seed.AnalystAsync(section)).Id };
        var origin = await _seed.UnderApprovalAsync(section, reviewer.Id, orders: 2, analystId: originalAnalyst.Id);
        var otherSection = otherSectionInvestigation ? await _seed.SectionAsync() : null;
        await using var arrange = fixture.CreateDbContext();
        if (otherSection is not null)
        {
            var sample = await arrange.Samples.SingleAsync(s => s.Id == origin.Sample.Id);
            sample.OosGroupCode = $"OTHER-{Guid.NewGuid():N}"[..20];
            arrange.OosInvestigations.Add(new OosInvestigation
            {
                Number = sample.OosGroupCode, SampleId = sample.Id, SectionId = otherSection.Id,
                Status = OosInvestigationStatus.PhaseOne, Kind = OosInvestigationKind.Oos,
                OpenedAtUtc = ActionGuardScenarios.Now, OpenedByUserId = reviewer.Id, TemplateSnapshotJson = "{}"
            });
        }
        if (!await arrange.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
            arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
        var investigation = new OosInvestigation
        {
            Number = $"PLAN-{Guid.NewGuid():N}"[..20], SampleId = origin.Sample.Id, SectionId = section.Id,
            Status = OosInvestigationStatus.PhaseOne, Kind = OosInvestigationKind.Oos,
            OpenedAtUtc = ActionGuardScenarios.Now, OpenedByUserId = reviewer.Id, TemplateSnapshotJson = "{}"
        };
        arrange.OosInvestigations.Add(investigation);
        await arrange.SaveChangesAsync();
        arrange.OosInvestigationResults.Add(new OosInvestigationResult
        {
            InvestigationId = investigation.Id, TestOrderId = origin.Orders[0].Id,
            OutcomeAtOpening = OosOutcomeClass.Fails, AddedAtUtc = ActionGuardScenarios.Now
        });
        if (phaseOneSigned)
        {
            var signature = await new ElectronicSignatureService(arrange).SignAsync(head.Id, ActionGuardScenarios.Password,
                SignatureMeaning.OosPhaseOneConcluded, nameof(OosInvestigation), investigation.Id, null, null);
            await arrange.SaveChangesAsync();
            investigation.PhaseOneSignatureId = signature.Id;
            investigation.PhaseOneSignedAtUtc = signature.SignedAt;
            investigation.PhaseOneSignedByUserId = head.Id;
            investigation.Status = OosInvestigationStatus.AwaitingRetest;
        }
        await arrange.SaveChangesAsync();
        return new(investigation.Id, origin.Sample.Id, section, head.Id, reviewer.Id, originalAnalyst.Id,
            origin.Orders[0].Id, origin.Orders[1].Id, analysts);
    }

    private static SaveRetestPlanRequest Request(World world) =>
        new(OosRetestSource.RetainedSample, 2, [world.FailingOrder], world.Analysts, "Compare results to the recorded limits.", null);

    private async Task<OosRetestPlanDto> DraftAsync(World world, SaveRetestPlanRequest? request = null, int? actor = null)
    {
        await using var act = fixture.CreateDbContext();
        return await Service(act).DraftAsync(world.Id, request ?? Request(world), await VersionAsync(world), actor ?? world.Head);
    }

    private async Task SignAsync(World world, int planId, string password = ActionGuardScenarios.Password)
    {
        await using var act = fixture.CreateDbContext();
        await Service(act).SignAndCreateAsync(planId, password, await VersionAsync(world), world.Head, null);
    }

    private async Task<uint> VersionAsync(World world)
    {
        await using var read = fixture.CreateDbContext();
        return await read.OosInvestigations.AsNoTracking().Where(i => i.Id == world.Id).Select(i => i.Version).SingleAsync();
    }

    internal static OosRetestPlanService Service(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.OosInvestigation(db), new UserSectionScopeService(db), new SegregationOfDutiesGuard(db),
            new ElectronicSignatureService(db), new RetestSampleFactory(db, new ReferenceNumberGenerator(db), TimeProvider.System), TimeProvider.System);

    private static Task<AuditLog> AuditAsync(MicroLimsDbContext db, string entity, int id, int actor, string action) =>
        db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == entity && a.EntityId == id.ToString() && a.UserId == actor && a.Action == action);
}
