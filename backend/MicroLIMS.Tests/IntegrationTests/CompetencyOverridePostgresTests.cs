using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Npgsql;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class CompetencyOverridePostgresTests(PostgresTestFixture fixture)
{
    private const string Reason = "Supervised method execution";
    private readonly ActionGuardScenarios _seed = new(fixture);

    [PostgresFact]
    public async Task Grant_persists_signed_override_and_sample_audit()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        uint version;
        await using (var before = fixture.CreateDbContext())
            version = await before.TestOrders.AsNoTracking().Where(t => t.Id == order.Id)
                .Select(t => EF.Property<uint>(t, "xmin")).SingleAsync();
        await GrantAsync(order.Id, analyst, head);
        await using var verify = fixture.CreateDbContext();
        var row = await verify.CompetencyOverrides.AsNoTracking().Include(o => o.Signature).SingleAsync(o => o.TestOrderId == order.Id);
        Assert.Equal(sample.Sample.Id, row.SampleId);
        Assert.Equal(head, row.GrantedByUserId);
        Assert.Equal(analyst, row.AnalystUserId);
        Assert.Equal(Reason, row.Reason);
        Assert.Null(row.EndedAtUtc);
        Assert.Null(row.EndReason);
        Assert.Equal(47, (int)row.Signature.MeaningOfSignature);
        Assert.Equal(head, row.Signature.UserId);
        Assert.Equal(row.Id, row.Signature.EntityId);
        Assert.Equal("CompetencyOverride", row.Signature.EntityType);
        Assert.Equal(Reason, row.Signature.Comment);
        Assert.Equal(row.GrantedAtUtc, row.Signature.SignedAt);
        Assert.NotEqual(version, await verify.TestOrders.AsNoTracking().Where(t => t.Id == order.Id).Select(t => EF.Property<uint>(t, "xmin")).SingleAsync());
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "CompetencyOverride" && a.EntityId == row.Id.ToString());
        Assert.Equal("Create", audit.Action);
        Assert.Equal(head, audit.UserId);
        Assert.Equal(sample.Sample.Id, audit.SampleId);
        Assert.Equal(order.Id, audit.TestOrderId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains(Reason, audit.NewValue);
        Assert.Equal(Reason, audit.Reason);
    }

    [PostgresFact]
    public async Task Override_covers_one_analyst_one_test_order_only()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        await GrantAsync(sample.Orders[0].Id, analyst, head);
        await using var verify = fixture.CreateDbContext();
        var guard = new CompetencyGuard(verify, Clock(), new ItemSopResolver(verify));
        var decisions = await guard.EvaluateManyAsync(analyst, sample.Orders.Select(t => t.Id).ToArray());
        Assert.True(decisions[sample.Orders[0].Id].Allowed);
        Assert.NotNull(decisions[sample.Orders[0].Id].ViaOverrideId);
        Assert.False(decisions[sample.Orders[1].Id].Allowed);
        Assert.False((await guard.EvaluateAsync(head, sample.Orders[0].Id)).Allowed);
        var settings = new CompetencySettingsService(verify, new PermissionService(verify), new UserSectionScopeService(verify),
            Clock(), new ElectronicSignatureService(verify), guard);
        var impact = await settings.GetImpactAsync(sample.Orders[0].SectionId, head);
        Assert.Equal(1, impact.TotalRefusedOrders);
        Assert.Equal(sample.Orders[1].Id, Assert.Single(Assert.Single(impact.Analysts).Orders).TestOrderId);
    }

    [PostgresFact]
    public async Task Self_override_is_refused()
    {
        var (sample, head, _) = await ArrangeAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(sample.Orders[0].Id, head, head));
        Assert.Equal("Competency.OverrideSelf", error.Code);
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.CompetencyOverrides.AsNoTracking().AnyAsync(o => o.SampleId == sample.Sample.Id));
    }

    [PostgresFact]
    public async Task Grant_requires_permission_and_write_scope()
    {
        var (sample, _, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var manager = await _seed.UserAsync(RoleType.QcManager, [PermissionConstants.CompetencyManage]);
        var outsider = await _seed.OutsiderAsync(RoleType.SectionHead, [PermissionConstants.CompetencyOverride]);
        var denied = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => GrantAsync(order.Id, analyst, manager.Id));
        Assert.Equal("Competency.OverridePermissionRequired", denied.Code);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => GrantAsync(order.Id, analyst, outsider.Id));
        await AssertNoGrantAsync(order.Id, manager.Id, outsider.Id);
    }

    [PostgresFact]
    public async Task Closed_order_and_ineligible_analyst_are_refused()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var outsider = await _seed.AnalystAsync();
        var ineligible = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(order.Id, outsider.Id, head));
        Assert.Equal("Competency.OverrideAnalystIneligible", ineligible.Code);

        await using (var arrange = fixture.CreateDbContext())
        {
            var tracked = await arrange.TestOrders.SingleAsync(t => t.Id == order.Id);
            tracked.Status = ApprovalStatus.Reviewed;
            await arrange.SaveChangesAsync();
        }
        var closed = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(order.Id, analyst, head));
        Assert.Equal("Competency.OverrideOrderClosed", closed.Code);
        await AssertNoGrantAsync(order.Id, head);
    }

    [PostgresFact]
    public async Task Bad_password_and_blank_reason_do_not_commit_grant()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var blank = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(order.Id, analyst, head, reason: " \t "));
        Assert.Equal("Competency.OverrideReasonRequired", blank.Code);
        var invalid = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(order.Id, analyst, head, password: "wrong"));
        Assert.Equal("Competency.OverridePasswordFailed", invalid.Code);
        await AssertNoGrantAsync(order.Id, head);
    }

    [PostgresFact]
    public async Task Active_override_is_refused()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var id = await GrantAsync(order.Id, analyst, head);
        var denied = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(order.Id, analyst, head));
        Assert.Equal("Competency.OverrideAlreadyActive", denied.Code);
        await using var verify = fixture.CreateDbContext();
        var saved = Assert.Single(await verify.CompetencyOverrides.AsNoTracking().Where(o => o.TestOrderId == order.Id).ToListAsync());
        Assert.Equal(id, saved.Id);
        Assert.Null(saved.EndedAtUtc);
        Assert.Equal(1, await verify.ElectronicSignatures.AsNoTracking().CountAsync(s => s.UserId == head && s.EntityType == "CompetencyOverride"));
    }

    [PostgresFact]
    public async Task Override_is_unneeded_with_gate_disabled()
    {
        var (sample, head, analyst) = await ArrangeAsync(gate: false);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(sample.Orders[0].Id, analyst, head));
        Assert.Equal("Competency.OverrideNotNeeded", error.Code);
        await AssertNoGrantAsync(sample.Orders[0].Id, head);
    }

    [PostgresFact]
    public async Task Override_is_unneeded_for_qualified_analyst()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        await SeedQualificationAsync(order, analyst, head);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(order.Id, analyst, head));
        Assert.Equal("Competency.OverrideNotNeeded", error.Code);
        await AssertNoGrantAsync(order.Id, head);
    }

    [PostgresFact]
    public async Task Review_ends_override_and_separation_applies_to_the_whole_sample()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var id = await GrantAsync(order.Id, analyst, head);
        var (otherSection, reviewer, otherHead) = await ReadyForReviewAsync(sample);
        await using (var arrange = fixture.CreateDbContext())
        {
            var grantor = await arrange.Users.SingleAsync(u => u.Id == head);
            grantor.FullName = "Changed after signature";
            arrange.UserOrgMemberships.Add(new() { UserId = head, DepartmentId = otherSection.DepartmentId, SectionId = otherSection.Id });
            await arrange.SaveChangesAsync();
        }

        await using (var act = fixture.CreateDbContext())
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => ReviewAsync(act, sample.Sample.Id, head, otherSection.Id));
            Assert.Equal("Sample.CannotReviewOverrode", refused.Code);
        }
        await using (var act = fixture.CreateDbContext())
            await ReviewAsync(act, sample.Sample.Id, reviewer, order.SectionId);

        await using (var act = fixture.CreateDbContext())
        {
            var approval = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.SampleApproval(act).DecideAsync(
                sample.Sample.Id, head, ActionGuardScenarios.Password, ApprovalDecision.Approve, null, null, sectionId: order.SectionId));
            Assert.Equal("Sample.CannotApproveOverrode", approval.Code);
        }
        await using (var act = fixture.CreateDbContext())
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => ReviewAsync(act, sample.Sample.Id, head, otherSection.Id));
            Assert.Equal("Sample.CannotReviewOverrode", refused.Code);
        }
        await using var verify = fixture.CreateDbContext();
        var row = await verify.CompetencyOverrides.AsNoTracking().Include(o => o.Signature).Include(o => o.AnalystUser).SingleAsync(o => o.Id == id);
        var persisted = await verify.TestOrders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(ApprovalStatus.Reviewed, persisted.Status);
        Assert.Equal(CompetencyOverrideEndReason.Reviewed, row.EndReason);
        Assert.NotNull(row.EndedAtUtc);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "CompetencyOverride" && a.EntityId == id.ToString() && a.Action == "Update");
        Assert.Equal(reviewer, audit.UserId);
        Assert.Equal(sample.Sample.Id, audit.SampleId);
        Assert.Equal(order.Id, audit.TestOrderId);
        Assert.NotEqual(audit.PreviousValue, audit.NewValue);

        var dto = await TestServiceFactory.SampleSummary(verify).GetSummaryAsync(sample.Sample.Id);
        var manifest = Assert.Single(dto!.TestOrders.Single(t => t.TestOrderId == order.Id).CompetencyOverrides);
        Assert.Equal(row.AnalystUser.FullName, manifest.AnalystName);
        Assert.Equal(row.Signature.UserFullNameSnapshot, manifest.GrantedByFullName);
        Assert.NotEqual("Changed after signature", manifest.GrantedByFullName);
        Assert.Equal(row.GrantedAtUtc, manifest.GrantedAtUtc);
        Assert.Equal("Competency override", manifest.Meaning);
        Assert.Equal(Reason, manifest.Reason);
        Assert.Equal(row.EndedAtUtc, manifest.EndedAtUtc);

        var recipients = new NotificationRecipients(verify, new SegregationOfDutiesGuard(verify));
        var reviewRecipients = await recipients.ReviewersAsync(otherSection.Id, [sample.Orders[^1].Id]);
        Assert.DoesNotContain(head, reviewRecipients);
        var approvers = await recipients.ApproversAsync(order.SectionId, [order.Id]);
        Assert.DoesNotContain(head, approvers);
        Assert.Contains(otherHead, approvers);
        var scope = new UserSectionScopeService(verify);
        var clock = new FixedTimeProvider(new DateTimeOffset(ActionGuardScenarios.Now));
        var documents = new DocumentAuthorizationService(verify);
        var availability = new ActionAvailabilityService(verify, scope,
            TestServiceFactory.SampleReview(verify), TestServiceFactory.SampleApproval(verify),
            new SampleAssignmentService(verify, TestServiceFactory.TestingWorkspace(verify), scope, clock),
            new TestSelfAssignmentService(verify, scope, clock), TestServiceFactory.MediaRelease(verify),
            new DocumentApprovalService(verify, new AuditEventService(verify, fixture.CreateSequenceHelper(verify)), documents, new ElectronicSignatureService(verify)),
            documents);
        var screen = await availability.ForSampleAsync(sample.Sample.Id, order.SectionId, head);
        var headApprove = Assert.Single(screen!, x => x.Action == GatedActions.ApprovalApprove);
        Assert.False(headApprove.Allowed);
        Assert.Equal("Sample.CannotApproveOverrode", headApprove.ReasonCode);
        var otherScreen = await availability.ForSampleAsync(sample.Sample.Id, order.SectionId, otherHead);
        var otherApprove = Assert.Single(otherScreen!, x => x.Action == GatedActions.ApprovalApprove);
        Assert.True(otherApprove.Allowed, otherApprove.Reason);
    }

    [PostgresFact]
    public async Task Reassigning_to_another_analyst_ends_override()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var replacement = await _seed.AnalystAsync(await ReadSectionAsync(order.SectionId));
        await SeedQualificationAsync(order, replacement.Id, head);
        var id = await GrantAsync(order.Id, analyst, head);
        await using (var act = fixture.CreateDbContext())
        {
            var service = new SampleAssignmentService(act, new TestingWorkspaceService(act, new UserSectionScopeService(act)), new UserSectionScopeService(act), competency: TestServiceFactory.Competency(act));
            await service.AssignTestsAsync(sample.Sample.Id, [new(order.Id, replacement.Id)], head);
        }

        await using var verify = fixture.CreateDbContext();
        var saved = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.NotNull(saved.EndedAtUtc);
        Assert.Equal(CompetencyOverrideEndReason.Reassigned, saved.EndReason);
        Assert.Equal(replacement.Id, await verify.TestOrders.AsNoTracking().Where(t => t.Id == order.Id).Select(t => t.AssignedAnalystId).SingleAsync());
        Assert.False((await TestServiceFactory.Competency(verify).EvaluateAsync(analyst, order.Id)).Allowed);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "CompetencyOverride" && a.EntityId == id.ToString() && a.Action == "Update");
        Assert.Equal(head, audit.UserId);
        Assert.Equal(order.Id, audit.TestOrderId);
        Assert.NotEqual(audit.PreviousValue, audit.NewValue);
        var secondReason = "Second supervised grant";
        var secondId = await GrantAsync(order.Id, analyst, head, secondReason);
        await using var historyDb = fixture.CreateDbContext();
        var dto = await TestServiceFactory.SampleSummary(historyDb).GetSummaryAsync(sample.Sample.Id);
        Assert.NotNull(dto);
        var history = dto.TestOrders.Single(t => t.TestOrderId == order.Id).CompetencyOverrides;
        Assert.Collection(history,
            first =>
            {
                Assert.Equal(Reason, first.Reason);
                Assert.Equal(saved.EndedAtUtc, first.EndedAtUtc);
            },
            second =>
            {
                Assert.Equal(secondReason, second.Reason);
                Assert.Null(second.EndedAtUtc);
            });
        Assert.True(secondId > id);
    }

    [PostgresFact]
    public async Task Trigger_rejects_tampering_and_a_second_end()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var id = await GrantAsync(order.Id, analyst, head);
        await using (var act = fixture.CreateDbContext())
        {
            await RejectAsync(act, """UPDATE "CompetencyOverrides" SET "Reason" = 'changed' WHERE "Id" = {0}""", id);
            await RejectAsync(act, """DELETE FROM "CompetencyOverrides" WHERE "Id" = {0}""", id);
            await RejectAsync(act, """TRUNCATE TABLE "CompetencyOverrides" """);
            await RejectAsync(act, """
                INSERT INTO "CompetencyOverrides" ("TestOrderId", "SampleId", "AnalystUserId", "Reason",
                    "GrantedByUserId", "GrantedAtUtc", "SignatureId")
                VALUES ({0}, {1}, {2}, 'Unsigned', {3}, now(), 0)
                """, sample.Orders[1].Id, sample.Sample.Id, analyst, head);
        }

        var (_, reviewer, _) = await ReadyForReviewAsync(sample);
        await using (var act = fixture.CreateDbContext())
            await ReviewAsync(act, sample.Sample.Id, reviewer, order.SectionId);
        await using (var act = fixture.CreateDbContext())
            await RejectAsync(act, """UPDATE "CompetencyOverrides" SET "EndReason" = 1 WHERE "Id" = {0}""", id);

        await using var verify = fixture.CreateDbContext();
        var row = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.SampleId == sample.Sample.Id);
        Assert.Equal(id, row.Id);
        Assert.Equal(Reason, row.Reason);
        Assert.Equal(CompetencyOverrideEndReason.Reviewed, row.EndReason);
    }

    [PostgresFact]
    public async Task Grant_racing_reassignment_loses_and_rolls_back_its_signature()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var section = await ReadSectionAsync(order.SectionId);
        var replacement = await _seed.AnalystAsync(section);
        await SeedQualificationAsync(order, replacement.Id, head);
        await using var grantDb = fixture.CreateDbContext();
        var pause = new PausedSignature(new ElectronicSignatureService(grantDb));
        var grant = GrantService(grantDb, pause).GrantAsync(order.Id, analyst, Reason, ActionGuardScenarios.Password, head);
        try
        {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var assignDb = fixture.CreateDbContext();
            var assignSvc = new SampleAssignmentService(assignDb, TestServiceFactory.TestingWorkspace(assignDb),
                new UserSectionScopeService(assignDb), competency: TestServiceFactory.Competency(assignDb));
            await assignSvc.AssignTestsAsync(sample.Sample.Id, [new(order.Id, replacement.Id)], head);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var conflict = await Assert.ThrowsAsync<BusinessRuleException>(() => grant);
        Assert.Equal("Competency.OverrideConcurrencyConflict", conflict.Code);
        await AssertNoGrantAsync(order.Id, head);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(replacement.Id,
            await verify.TestOrders.AsNoTracking().Where(t => t.Id == order.Id).Select(t => t.AssignedAnalystId).SingleAsync());
    }

    [PostgresFact]
    public async Task Recording_through_override_succeeds_and_audits_override_id()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        int specId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var def = new TestDefinition
            {
                Code = order.TestCode,
                DisplayName = order.TestCode,
                SectionId = order.SectionId,
                WorkflowType = WorkflowType.Measurement,
                ReplicateCount = 1,
                EvaluationBasis = MeasurementEvaluationBasis.Mean
            };
            arrange.TestDefinitions.Add(def);
            var trackedSample = await arrange.Samples.SingleAsync(s => s.Id == sample.Sample.Id);
            var item = new Item { Code = $"ITEM-{sample.Sample.Id}", Name = "Override Item", Category = SampleCategory.FinishedProduct };
            arrange.Items.Add(item);
            await arrange.SaveChangesAsync();
            trackedSample.ItemId = item.Id;
            var trackedOrder = await arrange.TestOrders.SingleAsync(t => t.Id == order.Id);
            trackedOrder.CurrentStep = WorkflowStep.Running;
            var spec = new Specification
            {
                ItemId = item.Id,
                TestCode = order.TestCode,
                ParameterName = "pH",
                Unit = "pH",
                LowerLimit = 1m,
                UpperLimit = 14m,
                LimitType = LimitType.Range
            };
            arrange.Specifications.Add(spec);
            await arrange.SaveChangesAsync();
            specId = spec.Id;
        }

        var overrideId = await GrantAsync(order.Id, analyst, head);
        await using (var act = fixture.CreateDbContext())
        {
            await TestServiceFactory.TestWorkflow(act).RecordMeasurementResultAsync(
                order.Id,
                new(ActionGuardScenarios.Now, null, [new(specId, [7.0m])], ActionGuardScenarios.Password),
                analyst);
        }

        await using var verify = fixture.CreateDbContext();
        var analysis = await verify.TestAnalyses.AsNoTracking().SingleAsync(a => a.TestOrderId == order.Id);
        Assert.Equal(analyst, analysis.EnteredByUserId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(TestAnalysis) && a.EntityId == analysis.Id.ToString() && a.Action == "Create");
        Assert.Contains($"Competency override #{overrideId}", audit.Reason);
    }

    [PostgresFact]
    public async Task SampleAssignment_allows_overridden_analyst_on_overridden_order_only()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        await using (var arrange = fixture.CreateDbContext())
        {
            foreach (var o in await arrange.TestOrders.Where(t => t.SampleId == sample.Sample.Id).ToListAsync())
                o.AssignedAnalystId = null;
            await arrange.SaveChangesAsync();
        }
        await GrantAsync(sample.Orders[0].Id, analyst, head);

        await using (var act = fixture.CreateDbContext())
        {
            var svc = new SampleAssignmentService(act, TestServiceFactory.TestingWorkspace(act), new UserSectionScopeService(act), competency: TestServiceFactory.Competency(act));
            await svc.AssignTestsAsync(sample.Sample.Id, [new(sample.Orders[0].Id, analyst)], head);
        }

        await using (var act = fixture.CreateDbContext())
        {
            var svc = new SampleAssignmentService(act, TestServiceFactory.TestingWorkspace(act), new UserSectionScopeService(act), competency: TestServiceFactory.Competency(act));
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.AssignAnalystAsync(sample.Sample.Id, analyst, head));
            Assert.Equal(MessageCodes.CompetencyAssignmentRefused, refused.Code);
        }

        // Single-order sample with an active override allows sample-level assignment too.
        var section = await ReadSectionAsync(sample.Orders[0].SectionId);
        var single = await _seed.SampleAsync(section, SampleStatus.InTesting, ApprovalStatus.InProgress,
            WorkflowStep.Waiting, orders: 1, analystId: null, testCode: sample.Orders[0].TestCode);
        await GrantAsync(single.Orders[0].Id, analyst, head);
        await using (var act = fixture.CreateDbContext())
        {
            var svc = new SampleAssignmentService(act, TestServiceFactory.TestingWorkspace(act), new UserSectionScopeService(act), competency: TestServiceFactory.Competency(act));
            await svc.AssignAnalystAsync(single.Sample.Id, analyst, head);
        }
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(analyst, await verify.TestOrders.AsNoTracking().Where(t => t.Id == single.Orders[0].Id).Select(t => t.AssignedAnalystId).SingleAsync());
    }

    [PostgresFact]
    public async Task Override_does_not_carry_over_to_retest()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        await GrantAsync(order.Id, analyst, head);

        await using var verify = fixture.CreateDbContext();
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AnalystAssignmentRules.EnsureAssignableAsync(verify, new UserSectionScopeService(verify),
                analyst, head, [order.SectionId], Clock().UtcNow.UtcDateTime,
                testOrderIds: [order.Id], competency: TestServiceFactory.Competency(verify), allowOrderOverride: false));
        Assert.Equal(MessageCodes.CompetencyAssignmentRefused, refused.Code);
    }

    [PostgresFact]
    public async Task Grant_refused_for_sample_in_oos_retest_plan()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        await using (var arrange = fixture.CreateDbContext())
        {
            var inv = new OosInvestigation
            {
                Number = $"OOS-{sample.Sample.Id}",
                SampleId = sample.Sample.Id,
                SectionId = sample.Orders[0].SectionId,
                Status = OosInvestigationStatus.AwaitingRetest,
                Kind = OosInvestigationKind.Oos,
                OpenedByUserId = head,
                OpenedAtUtc = ActionGuardScenarios.Now,
                TemplateSnapshotJson = "{}"
            };
            arrange.OosInvestigations.Add(inv);
            await arrange.SaveChangesAsync();
            var plan = new OosRetestPlan
            {
                InvestigationId = inv.Id,
                Revision = 1,
                RetestCount = 1,
                TestOrderIdsJson = "[]",
                AnalystUserIdsJson = "[]",
                JudgingRule = "AllMustPass"
            };
            arrange.OosRetestPlans.Add(plan);
            await arrange.SaveChangesAsync();
            arrange.OosRetestPlanSamples.Add(new OosRetestPlanSample { PlanId = plan.Id, SampleId = sample.Sample.Id });
            await arrange.SaveChangesAsync();
        }

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(sample.Orders[0].Id, analyst, head));
        Assert.Equal(MessageCodes.CompetencyOverrideOrderClosed, refused.Code);
        await AssertNoGrantAsync(sample.Orders[0].Id, head);
    }

    [PostgresFact]
    public async Task Override_ends_on_void_section_close_supersede_self_take_and_preparation()
    {
        // 1. Void
        var (voidSample, voidHead, voidAnalyst) = await ArrangeAsync();
        var voidOverrideId = await GrantAsync(voidSample.Orders[0].Id, voidAnalyst, voidHead);
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.SampleCorrection(act).VoidAsync(voidSample.Sample.Id, "Voided for test", ActionGuardScenarios.Password, voidHead, null);
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.Id == voidOverrideId);
            Assert.NotNull(row.EndedAtUtc);
            Assert.Equal(CompetencyOverrideEndReason.Cancelled, row.EndReason);
        }

        // 2. Section close
        var (closeSample, closeHead, closeAnalyst) = await ArrangeAsync();
        var closeOverrideId = await GrantAsync(closeSample.Orders[0].Id, closeAnalyst, closeHead);
        var otherSection = await _seed.SectionAsync();
        await _seed.AddSectionAsync(closeSample, otherSection, ApprovalStatus.Rejected, WorkflowStep.Ready, SectionSignoffStatus.Rejected);
        await using (var act = fixture.CreateDbContext())
        {
            var scope = new UserSectionScopeService(act);
            var closure = new SectionClosureService(act, TestServiceFactory.ReviewGate(act), scope, TestServiceFactory.SampleApproval(act));
            await closure.CloseTestingAsync(closeSample.Sample.Id, closeSample.Orders[0].SectionId, closeHead, ActionGuardScenarios.Password, "Other lab rejected", null);
        }
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.Id == closeOverrideId);
            Assert.NotNull(row.EndedAtUtc);
            Assert.Equal(CompetencyOverrideEndReason.Cancelled, row.EndReason);
        }

        // 3. Supersede (RetestRetainedSample)
        var (retSample, retHead, retAnalyst) = await ArrangeAsync();
        var retOrder = retSample.Orders[0];
        var retOverrideId = await GrantAsync(retOrder.Id, retAnalyst, retHead);
        var retSection = await ReadSectionAsync(retOrder.SectionId);
        var retOtherHead = await _seed.HeadAsync(retSection);
        var retReviewer = await _seed.HeadAsync(retSection);
        await using (var arrange = fixture.CreateDbContext())
        {
            if (!await arrange.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
                arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
            var s = await arrange.Samples.Include(x => x.TestOrders).SingleAsync(x => x.Id == retSample.Sample.Id);
            s.Status = SampleStatus.UnderApproval;
            foreach (var o in s.TestOrders)
            {
                o.Status = ApprovalStatus.Reviewed;
                o.CurrentStep = WorkflowStep.Reviewed;
            }
            arrange.SampleSectionSignoffs.Add(new() { SampleId = s.Id, SectionId = retOrder.SectionId, Status = SectionSignoffStatus.UnderApproval, ReviewedByUserId = retReviewer.Id });
            await arrange.SaveChangesAsync();
        }
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.SampleApproval(act).DecideAsync(retSample.Sample.Id, retOtherHead.Id, ActionGuardScenarios.Password,
                ApprovalDecision.RetestRetainedSample, "Retest needed", null, selectedTestOrderIds: [retOrder.Id], sectionId: retOrder.SectionId);
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.Id == retOverrideId);
            Assert.NotNull(row.EndedAtUtc);
            Assert.Equal(CompetencyOverrideEndReason.Superseded, row.EndReason);
        }

        // 4. Self-take
        var (takeSample, takeHead, takeAnalyst) = await ArrangeAsync();
        var takeOrder = takeSample.Orders[0];
        await using (var arrange = fixture.CreateDbContext())
        {
            var o = await arrange.TestOrders.SingleAsync(t => t.Id == takeOrder.Id);
            o.AssignedAnalystId = null;
            await arrange.SaveChangesAsync();
        }
        var takeOverrideId = await GrantAsync(takeOrder.Id, takeAnalyst, takeHead);
        var takeSection = await ReadSectionAsync(takeOrder.SectionId);
        var otherAnalyst = await _seed.UserAsync(RoleType.Analyst, [PermissionConstants.TestsSelfAssign], takeSection);
        await using (var act = fixture.CreateDbContext())
            await new TestSelfAssignmentService(act, new UserSectionScopeService(act)).TakeAsync(takeOrder.Id, otherAnalyst.Id);
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.Id == takeOverrideId);
            Assert.NotNull(row.EndedAtUtc);
            Assert.Equal(CompetencyOverrideEndReason.Reassigned, row.EndReason);
        }

        // 5. Take-at-preparation
        var (prepSample, prepHead, prepAnalyst) = await ArrangeAsync();
        var prepOrder = prepSample.Orders[0];
        await using (var arrange = fixture.CreateDbContext())
        {
            var item = new Item { Code = $"PREP-{prepSample.Sample.Id}", Name = "Prep Item", Category = SampleCategory.FinishedProduct };
            arrange.Items.Add(item);
            await arrange.SaveChangesAsync();
            var s = await arrange.Samples.Include(x => x.TestOrders).SingleAsync(x => x.Id == prepSample.Sample.Id);
            s.Category = SampleCategory.FinishedProduct;
            s.ItemId = item.Id;
            s.PreparationStatus = SamplePreparationStatus.NeedsPreparation;
            foreach (var o in s.TestOrders)
                o.AssignedAnalystId = null;
            await arrange.SaveChangesAsync();
        }
        var prepOverrideId = await GrantAsync(prepOrder.Id, prepAnalyst, prepHead);
        var prepSection = await ReadSectionAsync(prepOrder.SectionId);
        var preparer = await _seed.UserAsync(RoleType.Analyst, [PermissionConstants.SamplesPrepare], prepSection);
        await SeedQualificationAsync(prepOrder, preparer.Id, prepHead);
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.SamplePreparation(act).PrepareAsync(
                await MicroLIMS.Tests.WorkflowTests.PreparationTestData.ManualRequestAsync(
                    act, prepSample.Sample.Id, 10m, preparer.Id, ActionGuardScenarios.Password));
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.CompetencyOverrides.AsNoTracking().SingleAsync(o => o.Id == prepOverrideId);
            Assert.NotNull(row.EndedAtUtc);
            Assert.Equal(CompetencyOverrideEndReason.Reassigned, row.EndReason);
        }
    }

    [PostgresFact]
    public async Task Grant_refused_when_granter_already_reviewed_or_approved_any_section()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var (otherSection, _, _) = await ReadyForReviewAsync(sample);
        var sectionA = await ReadSectionAsync(sample.Orders[0].SectionId);
        var approver = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.CompetencyOverride, ..ActionGuardScenarios.HeadCodes], sectionA);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.UserOrgMemberships.Add(new() { UserId = head, DepartmentId = otherSection.DepartmentId, SectionId = otherSection.Id });
            arrange.UserOrgMemberships.Add(new() { UserId = approver.Id, DepartmentId = otherSection.DepartmentId, SectionId = otherSection.Id });
            foreach (var o in await arrange.TestOrders.Where(t => t.SampleId == sample.Sample.Id && t.SectionId == sample.Orders[0].SectionId).ToListAsync())
            {
                o.Status = ApprovalStatus.InProgress;
                o.CurrentStep = WorkflowStep.Waiting;
            }
            await arrange.SaveChangesAsync();
        }

        // Head reviews section B, then tries to grant on section A.
        await using (var act = fixture.CreateDbContext())
            await ReviewAsync(act, sample.Sample.Id, head, otherSection.Id);
        var afterReview = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(sample.Orders[0].Id, analyst, head));
        Assert.Equal(MessageCodes.CompetencyOverrideAlreadySignedOff, afterReview.Code);
        await AssertNoGrantAsync(sample.Orders[0].Id, head);

        // Approver approves section B, then tries to grant on section A.
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.SampleApproval(act).DecideAsync(sample.Sample.Id, approver.Id, ActionGuardScenarios.Password,
                ApprovalDecision.Approve, "Approved", null, sectionId: otherSection.Id);
        var afterApproval = await Assert.ThrowsAsync<BusinessRuleException>(() => GrantAsync(sample.Orders[0].Id, analyst, approver.Id));
        Assert.Equal(MessageCodes.CompetencyOverrideAlreadySignedOff, afterApproval.Code);
        await AssertNoGrantAsync(sample.Orders[0].Id, approver.Id);
    }

    [PostgresFact]
    public async Task Grant_racing_approval_by_same_user_on_different_sections_allows_exactly_one()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var (otherSection, reviewer, _) = await ReadyForReviewAsync(sample);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.UserOrgMemberships.Add(new() { UserId = head, DepartmentId = otherSection.DepartmentId, SectionId = otherSection.Id });
            foreach (var o in await arrange.TestOrders.Where(t => t.SampleId == sample.Sample.Id && t.SectionId == sample.Orders[0].SectionId).ToListAsync())
            {
                o.Status = ApprovalStatus.InProgress;
                o.CurrentStep = WorkflowStep.Waiting;
            }
            var signoff = await arrange.SampleSectionSignoffs.SingleAsync(s => s.SampleId == sample.Sample.Id && s.SectionId == otherSection.Id);
            signoff.Status = SectionSignoffStatus.UnderApproval;
            signoff.ReviewedByUserId = reviewer;
            await arrange.SaveChangesAsync();
        }

        await using var grantDb = fixture.CreateDbContext();
        var pause = new PausedSignature(new ElectronicSignatureService(grantDb));
        var grant = GrantService(grantDb, pause).GrantAsync(sample.Orders[0].Id, analyst, Reason, ActionGuardScenarios.Password, head);
        Task? approve = null;
        try
        {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            approve = Task.Run(async () =>
            {
                await using var approveDb = fixture.CreateDbContext();
                await TestServiceFactory.SampleApproval(approveDb).DecideAsync(
                    sample.Sample.Id, head, ActionGuardScenarios.Password, ApprovalDecision.Approve, "Approved", null, sectionId: otherSection.Id);
            });
            await Task.Delay(150);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var overrideId = await grant;
        Assert.True(overrideId > 0);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => approve!);
        Assert.Equal(MessageCodes.SampleCannotApproveOverrode, refused.Code);

        await using var verify = fixture.CreateDbContext();
        Assert.True(await verify.CompetencyOverrides.AsNoTracking().AnyAsync(o => o.Id == overrideId));
        var signoffAfter = await verify.SampleSectionSignoffs.AsNoTracking()
            .SingleAsync(s => s.SampleId == sample.Sample.Id && s.SectionId == otherSection.Id);
        Assert.Equal(SectionSignoffStatus.UnderApproval, signoffAfter.Status);
        Assert.Null(signoffAfter.ApprovedByUserId);
    }

    [PostgresFact]
    public async Task Grant_racing_review_by_same_user_on_different_sections_allows_exactly_one()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var (otherSection, _, _) = await ReadyForReviewAsync(sample);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.UserOrgMemberships.Add(new() { UserId = head, DepartmentId = otherSection.DepartmentId, SectionId = otherSection.Id });
            foreach (var o in await arrange.TestOrders.Where(t => t.SampleId == sample.Sample.Id && t.SectionId == sample.Orders[0].SectionId).ToListAsync())
            {
                o.Status = ApprovalStatus.InProgress;
                o.CurrentStep = WorkflowStep.Waiting;
            }
            await arrange.SaveChangesAsync();
        }

        await using var grantDb = fixture.CreateDbContext();
        var pause = new PausedSignature(new ElectronicSignatureService(grantDb));
        var grant = GrantService(grantDb, pause).GrantAsync(sample.Orders[0].Id, analyst, Reason, ActionGuardScenarios.Password, head);
        Task? review = null;
        try
        {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            review = Task.Run(async () =>
            {
                await using var reviewDb = fixture.CreateDbContext();
                await ReviewAsync(reviewDb, sample.Sample.Id, head, otherSection.Id);
            });
            await Task.Delay(150);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var overrideId = await grant;
        Assert.True(overrideId > 0);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => review!);

        await using var verify = fixture.CreateDbContext();
        Assert.True(await verify.CompetencyOverrides.AsNoTracking().AnyAsync(o => o.Id == overrideId));
        Assert.True(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == nameof(CompetencyOverride) && s.EntityId == overrideId && s.UserId == head));
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(CompetencyOverride) && a.EntityId == overrideId.ToString()));

        var signoffAfter = await verify.SampleSectionSignoffs.AsNoTracking()
            .SingleAsync(s => s.SampleId == sample.Sample.Id && s.SectionId == otherSection.Id);
        Assert.Equal(SectionSignoffStatus.UnderReview, signoffAfter.Status);
        Assert.Null(signoffAfter.ReviewedByUserId);
        Assert.Null(signoffAfter.ReviewSignatureId);
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == ReviewEntityTypes.Sample && s.EntityId == sample.Sample.Id && s.UserId == head));
        Assert.False(await verify.ReviewWorkflowEvents.AsNoTracking().AnyAsync(e => e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sample.Sample.Id && e.PerformedByUserId == head));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(SampleSectionSignoff) && a.UserId == head));
    }

    private async Task SeedQualificationAsync(TestOrder order, int analyst, int head)
    {
        await using var arrange = fixture.CreateDbContext();
        var definition = new TestDefinition { Code = order.TestCode, DisplayName = order.TestCode, SectionId = order.SectionId };
        arrange.TestDefinitions.Add(definition);
        await arrange.SaveChangesAsync();
        var id = await fixture.CreateSequenceHelper(arrange).ReserveIdAsync<AnalystQualification>();
        arrange.AnalystQualifications.Add(new()
        {
            Id = id, UserId = analyst, TestDefinitionId = definition.Id, Basis = QualificationBasis.ObservedPractice,
            Evidence = "Observed testing", QualifiedOn = Clock().LabToday, SignedByUserId = head,
            Signature = new()
            {
                EntityId = id, EntityType = nameof(AnalystQualification), UserId = head,
                MeaningOfSignature = SignatureMeaning.AnalystQualified, SignedAt = Clock().UtcNow.UtcDateTime
            }
        });
        await arrange.SaveChangesAsync();
    }

    private async Task AssertNoGrantAsync(int order, params int[] actors)
    {
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.CompetencyOverrides.AsNoTracking().AnyAsync(o => o.TestOrderId == order));
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => actors.Contains(s.UserId) && s.EntityType == "CompetencyOverride"));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.TestOrderId == order && a.EntityName == "CompetencyOverride"));
    }

    private async Task<(DocumentSection OtherSection, int Reviewer, int OtherHead)> ReadyForReviewAsync(ActionGuardScenarios.SampleSeed sample)
    {
        var section = await ReadSectionAsync(sample.Orders[0].SectionId);
        var reviewer = await _seed.HeadAsync(section);
        var otherHead = await _seed.HeadAsync(section);
        var otherSection = await _seed.SectionAsync();
        await _seed.AddSectionAsync(sample, otherSection, ApprovalStatus.ResultEntered, WorkflowStep.Ready, SectionSignoffStatus.UnderReview);
        await using var arrange = fixture.CreateDbContext();
        var tracked = await arrange.Samples.Include(s => s.TestOrders).SingleAsync(s => s.Id == sample.Sample.Id);
        tracked.Status = SampleStatus.UnderReview;
        foreach (var order in tracked.TestOrders)
            order.CurrentStep = WorkflowStep.Ready;
        arrange.SampleSectionSignoffs.Add(new() { SampleId = tracked.Id, SectionId = section.Id, Status = SectionSignoffStatus.UnderReview });
        await arrange.SaveChangesAsync();
        return (otherSection, reviewer.Id, otherHead.Id);
    }

    private async Task<DocumentSection> ReadSectionAsync(int section)
    {
        await using var db = fixture.CreateDbContext();
        return await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == section);
    }

    private static Task ReviewAsync(MicroLimsDbContext db, int sample, int reviewer, int section) =>
        TestServiceFactory.SampleReview(db).CompleteReviewAsync(sample, reviewer, ActionGuardScenarios.Password, null, null, section);

    private static async Task RejectAsync(MicroLimsDbContext db, string sql, params object[] args)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, args));
        Assert.Contains("append-only", error.Message);
    }

    private sealed class PausedSignature(IElectronicSignatureService inner) : IElectronicSignatureService
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ElectronicSignature> SignAsync(int userId, string password, SignatureMeaning meaning, string entityType,
            int entityId, string? comment, string? ipAddress, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(ct);
            return await inner.SignAsync(userId, password, meaning, entityType, entityId, comment, ipAddress, ct);
        }
    }

    private async Task<int> GrantAsync(int order, int analyst, int head, string reason = Reason, string password = ActionGuardScenarios.Password)
    {
        await using var db = fixture.CreateDbContext();
        return await GrantService(db).GrantAsync(order, analyst, reason, password, head);
    }

    private CompetencyOverrideService GrantService(MicroLimsDbContext db, IElectronicSignatureService? signatures = null)
    {
        var clock = Clock();
        return new CompetencyOverrideService(db, new PermissionService(db), new UserSectionScopeService(db),
            clock, fixture.CreateSequenceHelper(db), signatures ?? new ElectronicSignatureService(db, TimeProvider.System),
            new CompetencyGuard(db, clock, new ItemSopResolver(db)));
    }
    private static LabClock Clock() => new(TimeProvider.System, LabClock.ResolveTimeZone("Africa/Cairo"));

    private async Task<(ActionGuardScenarios.SampleSeed Sample, int Head, int Analyst)> ArrangeAsync(bool gate = true)
    {
        var section = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.CompetencyOverride, PermissionConstants.CompetencyManage, .. ActionGuardScenarios.HeadCodes], section);
        var analyst = await _seed.AnalystAsync(section);
        var sample = await _seed.SampleAsync(section, SampleStatus.InTesting, ApprovalStatus.InProgress,
            WorkflowStep.Waiting, orders: 2, analystId: analyst.Id, testCode: $"OV-{section.Code}");
        await using var db = fixture.CreateDbContext();
        db.CompetencySectionSettings.Add(new() { SectionId = section.Id, GateEnabled = gate });
        await db.SaveChangesAsync();
        return (sample, head.Id, analyst.Id);
    }
}
