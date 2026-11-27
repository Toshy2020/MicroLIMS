using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class SampleClosingCharacterisationPostgresTests(PostgresTestFixture fixture)
{
    private readonly ActionGuardScenarios _scenarios = new(fixture);

    [PostgresFact]
    public Task Approval_close_archives_with_decision_label_and_projects_approval_fields() => AssertApprovalClosureAsync();

    private async Task AssertApprovalClosureAsync()
    {
        var (section, reviewer, head, analyst) = await ActorsAsync();
        var origin = await _scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        await AddLocationAndProjectionAsync(origin, analyst, ResultStatus.WithinLimits);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => DecideAsync(origin.Sample.Id, head.Id, ApprovalDecision.Approve, password: "wrong"));
        await using (var verify = fixture.CreateDbContext())
        {
            Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == origin.Sample.Id));
            Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(Sample) && a.EntityId == origin.Sample.Id.ToString() && a.UserId == head.Id));
        }

        await DecideAsync(origin.Sample.Id, head.Id, ApprovalDecision.Approve);
        await AssertClosedAsync(origin.Sample.Id, head, "Sample Approve", SampleStatus.UnderApproval);
    }

    [PostgresFact]
    public Task Legacy_retest_close_archives_origin_with_oos_resolved_label() => AssertLegacyPropagationAsync();

    private async Task AssertLegacyPropagationAsync()
    {
        var (section, reviewer, head, analyst) = await ActorsAsync();
        var origin = await _scenarios.UnderApprovalAsync(section, reviewer.Id, orders: 2, analystId: analyst.Id);
        await AddLocationAndProjectionAsync(origin, analyst, ResultStatus.AlertLimitExceeded);
        await DecideAsync(origin.Sample.Id, head.Id, ApprovalDecision.RetestRetainedSample, orderIds: [origin.Orders[0].Id]);
        var retestId = Assert.Single(await RetestIdsAsync(origin.Sample.Id));
        await ReadyForApprovalAsync(retestId, section.Id, reviewer.Id);

        await DecideAsync(retestId, head.Id, ApprovalDecision.Approve);

        await AssertClosedAsync(origin.Sample.Id, head, "Sample Approve (OOS resolved)", SampleStatus.RetestRequested);
        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.TestOrders.AsNoTracking().Where(o => o.SampleId == origin.Sample.Id).OrderBy(o => o.Id).ToListAsync();
        var outcomes = await new TestOrderOutcomeService(verify)
            .GetOutcomesAsync(persisted.Select(o => o.Id).ToArray());
        Assert.True(outcomes[persisted[0].Id].Fails);
        Assert.False(outcomes[persisted[1].Id].Fails);
        Assert.Equal(new[] { ApprovalStatus.Rejected, ApprovalStatus.Approved }, persisted.Select(o => o.Status));
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == origin.Sample.Id));
    }

    [PostgresFact]
    public async Task Legacy_two_section_origin_not_archived_until_last_section()
    {
        var (section, reviewer, head, analyst) = await ActorsAsync();
        var second = await _scenarios.SectionAsync();
        var otherHead = await _scenarios.HeadAsync(second);
        var origin = await _scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        await _scenarios.AddSectionAsync(origin, second, ApprovalStatus.Reviewed, WorkflowStep.Reviewed,
            SectionSignoffStatus.UnderApproval, reviewer.Id);
        await AddLocationAndProjectionAsync(origin, analyst, ResultStatus.AlertLimitExceeded);
        await DecideAsync(origin.Sample.Id, head.Id, ApprovalDecision.RetestRetainedSample, section.Id, [origin.Orders[0].Id]);
        var retestId = Assert.Single(await RetestIdsAsync(origin.Sample.Id));
        await ReadyForApprovalAsync(retestId, section.Id, reviewer.Id);
        await DecideAsync(retestId, head.Id, ApprovalDecision.Approve);

        await using (var verify = fixture.CreateDbContext())
        {
            Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == origin.Sample.Id));
            Assert.Equal(SectionSignoffStatus.Approved, await verify.SampleSectionSignoffs.AsNoTracking()
                .Where(s => s.SampleId == origin.Sample.Id && s.SectionId == section.Id).Select(s => s.Status).SingleAsync());
            Assert.Null(await verify.ResultRecords.AsNoTracking().Where(r => r.SampleId == origin.Sample.Id).Select(r => r.ApprovedAt).SingleAsync());
        }

        await DecideAsync(origin.Sample.Id, otherHead.Id, ApprovalDecision.Approve, second.Id);
        await AssertClosedAsync(origin.Sample.Id, otherHead, "Sample Approve", SampleStatus.UnderApproval);
    }

    [PostgresFact]
    public Task Retained_retest_creates_same_sample_orders_locations_analysts() => AssertCreationAsync(ApprovalDecision.RetestRetainedSample);

    [PostgresFact]
    public Task New_sample_request_creates_two_samples_same_as_before() => AssertCreationAsync(ApprovalDecision.NewSampleRequest);

    [PostgresFact]
    public Task Legacy_group_without_investigation_still_propagates_as_before() => AssertLegacyPropagationAsync();

    [PostgresFact]
    public async Task Origin_closed_by_retest_runs_the_same_finalisation()
    {
        await AssertApprovalClosureAsync();
        await AssertLegacyPropagationAsync();

        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(SourceDirectory(), "..", "..",
            "MicroLIMS.Application", "Services", "SampleApprovalService.cs")));
        const string archiveCall = "_archive.ArchiveAsync(ReviewEntityTypes.Sample";
        Assert.Equal(2, source.Split(archiveCall, StringSplitOptions.None).Length);
    }

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Cannot resolve the test source directory.");

    [PostgresFact]
    public async Task New_sample_decision_on_non_oos_sample_signs_new_meaning()
    {
        var (section, reviewer, head, analyst) = await ActorsAsync();
        var origin = await _scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        await AddLocationAndProjectionAsync(origin, analyst, ResultStatus.WithinLimits);
        var first = await _scenarios.AnalystAsync(section);
        var second = await _scenarios.AnalystAsync(section);

        await DecideAsync(origin.Sample.Id, head.Id, ApprovalDecision.NewSampleRequest, orderIds: [origin.Orders[0].Id],
            analystOne: first.Id, analystTwo: second.Id);
        await using var verify = fixture.CreateDbContext();
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s =>
            s.EntityType == "Sample" && s.EntityId == origin.Sample.Id);
        Assert.Equal(SignatureMeaning.NewSampleRequested, signature.MeaningOfSignature);
        Assert.Equal(head.Id, signature.UserId);
        var signoff = await verify.SampleSectionSignoffs.AsNoTracking().SingleAsync(s => s.SampleId == origin.Sample.Id);
        Assert.Equal(signature.Id, signoff.ApprovalSignatureId);
        Assert.Equal(SectionSignoffStatus.RetestRequested, signoff.Status);
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == origin.Sample.Id));
        Assert.False(await verify.ArchivedRecords.AsNoTracking().AnyAsync(r => r.EntityType == "Sample" && r.EntityId == origin.Sample.Id));
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(ElectronicSignature) &&
            a.EntityId == signature.Id.ToString() && a.Action == "Create" && a.UserId == head.Id);
        Assert.Null(audit.PreviousValue);
        using var payload = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)SignatureMeaning.NewSampleRequested, payload.RootElement.GetProperty(nameof(ElectronicSignature.MeaningOfSignature)).GetInt32());
    }

    private async Task AssertCreationAsync(ApprovalDecision decision)
    {
        var (section, reviewer, head, originalAnalyst) = await ActorsAsync();
        var origin = await _scenarios.UnderApprovalAsync(section, reviewer.Id, orders: 2, analystId: originalAnalyst.Id);
        await _scenarios.UpdateAsync<Sample>(origin.Sample.Id, s => s.Category = SampleCategory.Water);
        await AddLocationAndProjectionAsync(origin, originalAnalyst, ResultStatus.AlertLimitExceeded);
        await using (var arrange = fixture.CreateDbContext())
        {
            var order = await arrange.TestOrders.SingleAsync(o => o.Id == origin.Orders[0].Id);
            order.RoomId = await arrange.Rooms.Select(r => (int?)r.Id).FirstOrDefaultAsync();
            origin.Orders[0].RoomId = order.RoomId;
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = origin.Sample.Id, TestOrderId = order.Id, LocationType = LocationType.WaterSamplingPoint,
                Status = ResultStatus.WithinLimits
            });
            await arrange.SaveChangesAsync();
        }

        var analysts = decision == ApprovalDecision.NewSampleRequest
            ? new[] { (await _scenarios.AnalystAsync(section)).Id, (await _scenarios.AnalystAsync(section)).Id }
            : new[] { originalAnalyst.Id };
        await DecideAsync(origin.Sample.Id, head.Id, decision, orderIds: [origin.Orders[0].Id],
            analystOne: analysts[0], analystTwo: analysts.Length == 2 ? analysts[1] : null);

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Samples.AsNoTracking().Include(s => s.TestOrders).SingleAsync(s => s.Id == origin.Sample.Id);
        Assert.Equal(SampleStatus.RetestRequested, persisted.Status);
        Assert.False(string.IsNullOrEmpty(persisted.OosGroupCode));
        Assert.True(persisted.TestOrders.Single(o => o.Id == origin.Orders[0].Id).IsSuperseded);
        Assert.False(persisted.TestOrders.Single(o => o.Id == origin.Orders[1].Id).IsSuperseded);
        var children = await verify.Samples.AsNoTracking().Include(s => s.TestOrders)
            .Where(s => s.OriginSampleId == origin.Sample.Id).OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(analysts.Length, children.Count);
        Assert.Equal(children.Count, children.Select(s => s.ReferenceNumber).Distinct().Count());
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            Assert.Matches("^WT[0-9]{7,}$", child.ReferenceNumber);
            Assert.Equal(persisted.OosGroupCode, child.OosGroupCode);
            Assert.Equal(SampleStatus.Received, child.Status);
            Assert.Equal(SamplePreparationStatus.Ready, child.PreparationStatus);
            var order = Assert.Single(child.TestOrders);
            Assert.Equal(origin.Orders[0].TestCode, order.TestCode);
            Assert.Equal(section.Id, order.SectionId);
            Assert.Equal(analysts[i], order.AssignedAnalystId);
            Assert.Equal(origin.Orders[0].RoomId, order.RoomId);
            Assert.Equal(ApprovalStatus.Pending, order.Status);
            Assert.Equal(WorkflowStep.Waiting, order.CurrentStep);
            var location = await verify.SampleLocations.AsNoTracking().SingleAsync(l => l.SampleId == child.Id);
            Assert.Equal(order.Id, location.TestOrderId);
            Assert.Equal(LocationType.WaterSamplingPoint, location.LocationType);
            Assert.Null(location.Status);
            Assert.Null(location.ReportedResult);
            var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(Sample) &&
                a.EntityId == child.Id.ToString() && a.Action == "Create" && a.UserId == head.Id);
            using var payload = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
            Assert.Equal(origin.Sample.Id, payload.RootElement.GetProperty(nameof(Sample.OriginSampleId)).GetInt32());
        }

        var history = await verify.WorkflowHistories.AsNoTracking().SingleAsync(h => h.TestOrderId == origin.Orders[0].Id);
        Assert.Equal(head.Id, history.PerformedByUserId);
        Assert.Equal(decision == ApprovalDecision.NewSampleRequest
            ? $"New sample requested by {head.FullName}"
            : $"Retest ordered by {head.FullName} - new sample pending reference number", history.Note);
    }

    private async Task<(DocumentSection Section, User Reviewer, User Head, User Analyst)> ActorsAsync()
    {
        var section = await _scenarios.SectionAsync();
        return (section, await _scenarios.ReviewerAsync(section), await _scenarios.HeadAsync(section), await _scenarios.AnalystAsync(section));
    }

    private async Task AddLocationAndProjectionAsync(ActionGuardScenarios.SampleSeed seed, User analyst, ResultStatus status)
    {
        await using var arrange = fixture.CreateDbContext();
        if (!await arrange.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
        {
            arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
        }

        var order = seed.Orders[0];
        var location = new SampleLocation
        {
            SampleId = seed.Sample.Id, TestOrderId = order.Id, LocationType = LocationType.WaterSamplingPoint,
            Status = status, ReportedResult = "12", EnteredByUserId = analyst.Id, EnteredAt = ActionGuardScenarios.Now
        };
        arrange.SampleLocations.Add(location);
        await arrange.SaveChangesAsync();
        arrange.ResultRecords.Add(new ResultRecord
        {
            SampleId = seed.Sample.Id, TestOrderId = order.Id, TestCode = order.TestCode, SourceTable = nameof(SampleLocation),
            SourceId = location.Id, SampleStatus = SampleStatus.UnderApproval, ResultEnteredByUserId = analyst.Id,
            ResultEnteredAt = ActionGuardScenarios.Now, UpdatedAt = ActionGuardScenarios.Now
        });
        await arrange.SaveChangesAsync();
    }

    private async Task<int[]> RetestIdsAsync(int originId)
    {
        await using var verify = fixture.CreateDbContext();
        return await verify.Samples.AsNoTracking().Where(s => s.OriginSampleId == originId).Select(s => s.Id).ToArrayAsync();
    }

    private async Task ReadyForApprovalAsync(int sampleId, int sectionId, int reviewerId)
    {
        await using var arrange = fixture.CreateDbContext();
        var sample = await arrange.Samples.Include(s => s.TestOrders).SingleAsync(s => s.Id == sampleId);
        sample.Status = SampleStatus.UnderApproval;
        foreach (var order in sample.TestOrders)
        {
            order.Status = ApprovalStatus.Reviewed;
            order.CurrentStep = WorkflowStep.Reviewed;
        }
        arrange.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sampleId, SectionId = sectionId, Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = reviewerId, ReviewedAt = ActionGuardScenarios.Now
        });
        await arrange.SaveChangesAsync();
    }

    private async Task DecideAsync(int sampleId, int actorId, ApprovalDecision decision, int? sectionId = null,
        IReadOnlyList<int>? orderIds = null, int? analystOne = null, int? analystTwo = null, string password = ActionGuardScenarios.Password)
    {
        await using var act = fixture.CreateDbContext();
        act.CurrentUserId = actorId;
        await TestServiceFactory.SampleApproval(act).DecideAsync(sampleId, actorId, password, decision, null, null,
            selectedTestOrderIds: orderIds?.ToList(), newSampleAnalystOneId: analystOne, newSampleAnalystTwoId: analystTwo, sectionId: sectionId);
    }

    private async Task AssertClosedAsync(int sampleId, User head, string label, SampleStatus previous)
    {
        await using var verify = fixture.CreateDbContext();
        var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
        Assert.Equal(SampleStatus.Approved, sample.Status);
        Assert.Equal(head.Id, sample.ApprovedByUserId);
        Assert.NotNull(sample.ApprovedAt);
        var archive = await verify.ArchivedRecords.AsNoTracking().SingleAsync(r => r.EntityType == "Sample" && r.EntityId == sampleId);
        Assert.Equal(label, archive.Reason);
        Assert.Equal(head.Id, archive.GeneratedByUserId);
        var projection = await verify.ResultRecords.AsNoTracking().SingleAsync(r => r.SampleId == sampleId);
        Assert.Equal(sample.Status, projection.SampleStatus);
        Assert.Equal(sample.ApprovedByUserId, projection.ApprovedByUserId);
        Assert.Equal(head.FullName, projection.ApprovedByName);
        Assert.Equal(sample.ApprovedAt, projection.ApprovedAt);

        var audit = await verify.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(Sample) &&
            a.EntityId == sampleId.ToString() && a.Action == "Update" && a.UserId == head.Id).OrderByDescending(a => a.Id).FirstAsync();
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)previous, before.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
        Assert.Equal((int)SampleStatus.Approved, after.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
    }
}
