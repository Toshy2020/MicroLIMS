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
    public async Task Grant_racing_review_loses_and_rolls_back_its_signature()
    {
        var (sample, head, analyst) = await ArrangeAsync();
        var order = sample.Orders[0];
        var (_, reviewer, _) = await ReadyForReviewAsync(sample);
        await using var grantDb = fixture.CreateDbContext();
        var pause = new PausedSignature(new ElectronicSignatureService(grantDb));
        var grant = GrantService(grantDb, pause).GrantAsync(order.Id, analyst, Reason, ActionGuardScenarios.Password, head);
        try
        {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var reviewDb = fixture.CreateDbContext();
            await ReviewAsync(reviewDb, sample.Sample.Id, reviewer, order.SectionId);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var conflict = await Assert.ThrowsAsync<BusinessRuleException>(() => grant);
        Assert.Equal("Competency.OverrideConcurrencyConflict", conflict.Code);
        await AssertNoGrantAsync(order.Id, head);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(ApprovalStatus.Reviewed,
            await verify.TestOrders.AsNoTracking().Where(t => t.Id == order.Id).Select(t => t.Status).SingleAsync());
        Assert.True(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.UserId == reviewer
            && s.EntityType == "Sample" && s.EntityId == sample.Sample.Id && s.MeaningOfSignature == SignatureMeaning.Reviewed));
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
