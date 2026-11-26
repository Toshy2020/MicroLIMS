using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public sealed class OosRetestPlanValidationPostgresTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private readonly ActionGuardScenarios _seed = new(fixture);

    [PostgresFact]
    public async Task Draft_refuses_retest_count_zero()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var request = Request(world) with { RetestCount = 0, AnalystUserIds = [] };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, request));

        Assert.Equal(MessageCodes.OosRetestPlanAssignmentInvalid, error.Code);
        Assert.Equal("Choose a retest source and a count from 1 to 10, with one analyst per sample.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_retest_count_eleven()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var request = Request(world) with { RetestCount = 11, AnalystUserIds = Enumerable.Repeat(world.Analysts[0], 11).ToArray() };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, request));

        Assert.Equal(MessageCodes.OosRetestPlanAssignmentInvalid, error.Code);
        Assert.Equal("Choose a retest source and a count from 1 to 10, with one analyst per sample.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_analyst_count_mismatch()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var request = Request(world) with { AnalystUserIds = [world.Analysts[0]] };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, request));

        Assert.Equal(MessageCodes.OosRetestPlanAssignmentInvalid, error.Code);
        Assert.Equal("Choose a retest source and a count from 1 to 10, with one analyst per sample.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_empty_judging_rule()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, Request(world) with { JudgingRule = "" }));

        Assert.Equal(MessageCodes.OosRetestPlanJudgingRuleInvalid, error.Code);
        Assert.Equal("Enter a judging rule of 1 to 4000 characters.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_judging_rule_over_4000_chars()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DraftAsync(world, Request(world) with { JudgingRule = new string('x', 4001) }));

        Assert.Equal(MessageCodes.OosRetestPlanJudgingRuleInvalid, error.Code);
        Assert.Equal("Enter a judging rule of 1 to 4000 characters.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_amendment_reason_over_4000_chars()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DraftAsync(world, Request(world) with { AmendmentReason = new string('a', 4001) }));

        Assert.Equal(MessageCodes.OosRetestPlanAmendmentReasonRequired, error.Code);
        Assert.Equal("Enter an amendment reason of 1 to 4000 characters.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_duplicate_test_order_ids()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DraftAsync(world, Request(world) with { TestOrderIds = [world.FailingOrder, world.FailingOrder] }));

        Assert.Equal(MessageCodes.OosRetestPlanOrdersInvalid, error.Code);
        Assert.Equal("Select distinct current test orders linked to this investigation.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_analyst_outside_investigation_section()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var outsider = await _seed.AnalystAsync(await _seed.SectionAsync());
        var request = Request(world) with { AnalystUserIds = [world.Analysts[0], outsider.Id] };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, request));

        // Assignment refusals carry the sentence only, no message code.
        Assert.Null(error.Code);
        Assert.Equal($"'{outsider.FullName}' is not a member of the {world.Section.Name} laboratory section.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_concluded_investigation()
    {
        var world = await ArrangeAsync(concluded: true);
        var version = await VersionAsync(world);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world));

        Assert.Equal(MessageCodes.OosRetestPlanInvestigationConcluded, error.Code);
        Assert.Equal("A retest plan cannot be changed after the investigation is concluded.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_stale_if_match_version()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, version: version + 1));

        Assert.Equal(MessageCodes.OosVersionConflict, error.Code);
        Assert.Equal("This record changed. Reload it and try again.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_user_in_section_without_oos_manage()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var withoutManage = await _seed.UserAsync(RoleType.SectionHead, [], world.Section);

        var error = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => DraftAsync(world, actor: withoutManage.Id));

        Assert.Equal(MessageCodes.OosInvestigationAccessDenied, error.Code);
        Assert.Equal("You do not have permission for this investigation action.", error.Message);
        await AssertNothingPersistedAsync(world, withoutManage.Id, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_missing_reportable_retest_when_multiple()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var request = Request(world) with { ReportableRetestNumber = null };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, request));

        Assert.Equal(MessageCodes.OosRetestPlanReportableRetestRequired, error.Code);
        Assert.Equal("Choose which retest gives the reportable result.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_refuses_reportable_retest_out_of_range()
    {
        var world = await ArrangeAsync();
        var version = await VersionAsync(world);
        var request = Request(world) with { ReportableRetestNumber = 3 };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DraftAsync(world, request));

        Assert.Equal(MessageCodes.OosRetestPlanReportableRetestInvalid, error.Code);
        Assert.Equal("The reportable retest must be one of the planned retests.", error.Message);
        await AssertNothingPersistedAsync(world, world.Head, version);
    }

    [PostgresFact]
    public async Task Draft_stores_reportable_retest_one_when_single_retest_and_null()
    {
        var world = await ArrangeAsync();
        var request = Request(world) with
        {
            RetestCount = 1,
            AnalystUserIds = [world.Analysts[0]],
            ReportableRetestNumber = null
        };

        var dto = await DraftAsync(world, request);

        Assert.Equal(1, dto.ReportableRetestNumber);
        await using var verify = fixture.CreateDbContext();
        var row = await verify.OosRetestPlans.AsNoTracking().SingleAsync(p => p.Id == dto.Id);
        Assert.Equal(1, row.ReportableRetestNumber);
    }

    private async Task AssertNothingPersistedAsync(World world, int actor, uint version)
    {
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosRetestPlans.AsNoTracking().AnyAsync(p => p.InvestigationId == world.Id));
        Assert.Equal(version, await verify.OosInvestigations.AsNoTracking().Where(i => i.Id == world.Id)
            .Select(i => i.Version).SingleAsync());
        Assert.False(await verify.Samples.AsNoTracking().AnyAsync(s => s.OriginSampleId == world.Origin));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == actor &&
            a.EntityName == nameof(OosRetestPlan)));
    }

    private sealed record World(int Id, int Origin, DocumentSection Section, int Head, int FailingOrder, int[] Analysts);

    private async Task<World> ArrangeAsync(bool concluded = false)
    {
        var section = await _seed.SectionAsync();
        var head = await _seed.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage, ..ActionGuardScenarios.HeadCodes], section);
        var reviewer = await _seed.ReviewerAsync(section);
        var originalAnalyst = await _seed.AnalystAsync(section);
        var analysts = new[] { (await _seed.AnalystAsync(section)).Id, (await _seed.AnalystAsync(section)).Id };
        var origin = await _seed.UnderApprovalAsync(section, reviewer.Id, orders: 2, analystId: originalAnalyst.Id);
        await using var arrange = fixture.CreateDbContext();
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
        var signature = await new ElectronicSignatureService(arrange).SignAsync(head.Id, ActionGuardScenarios.Password,
            SignatureMeaning.OosPhaseOneConcluded, nameof(OosInvestigation), investigation.Id, null, null);
        await arrange.SaveChangesAsync();
        investigation.PhaseOneSignatureId = signature.Id;
        investigation.PhaseOneSignedAtUtc = signature.SignedAt;
        investigation.PhaseOneSignedByUserId = head.Id;
        investigation.Status = concluded ? OosInvestigationStatus.Concluded : OosInvestigationStatus.AwaitingRetest;
        if (concluded)
            investigation.Conclusion = OosConclusion.Confirmed;
        await arrange.SaveChangesAsync();
        return new(investigation.Id, origin.Sample.Id, section, head.Id, origin.Orders[0].Id, analysts);
    }

    private static SaveRetestPlanRequest Request(World world) =>
        new(OosRetestSource.RetainedSample, 2, [world.FailingOrder], world.Analysts, "Compare results to the recorded limits.", null);

    private async Task<OosRetestPlanDto> DraftAsync(World world, SaveRetestPlanRequest? request = null,
        int? actor = null, uint? version = null)
    {
        await using var act = fixture.CreateDbContext();
        return await Service(act).DraftAsync(world.Id, request ?? Request(world), version ?? await VersionAsync(world),
            actor ?? world.Head);
    }

    private async Task<uint> VersionAsync(World world)
    {
        await using var read = fixture.CreateDbContext();
        return await read.OosInvestigations.AsNoTracking().Where(i => i.Id == world.Id).Select(i => i.Version).SingleAsync();
    }

    internal static OosRetestPlanService Service(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.OosInvestigation(db), new UserSectionScopeService(db), new SegregationOfDutiesGuard(db),
            new ElectronicSignatureService(db), new RetestSampleFactory(db, new ReferenceNumberGenerator(db), TimeProvider.System), TimeProvider.System);
}
