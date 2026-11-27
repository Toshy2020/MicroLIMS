using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class RetestActionAvailabilityPostgresTests(PostgresTestFixture fixture)
{
    private readonly ActionGuardScenarios _scenarios = new(fixture);

    private ActionAvailabilityService NewAvailabilityService(MicroLimsDbContext db)
    {
        var scope = new UserSectionScopeService(db);
        var clock = new FixedTimeProvider(new DateTimeOffset(ActionGuardScenarios.Now));
        var documents = new DocumentAuthorizationService(db);
        return new ActionAvailabilityService(db, scope,
            TestServiceFactory.SampleReview(db), TestServiceFactory.SampleApproval(db),
            new SampleAssignmentService(db, TestServiceFactory.TestingWorkspace(db), scope, clock),
            new TestSelfAssignmentService(db, scope, clock),
            TestServiceFactory.MediaRelease(db),
            new DocumentApprovalService(db, new AuditEventService(db, fixture.CreateSequenceHelper(db)), documents, new ElectronicSignatureService(db)),
            documents);
    }

    private static string ActionKey(ApprovalDecision decision) => decision switch
    {
        ApprovalDecision.RetestRetainedSample => "approval.retestRetainedSample",
        ApprovalDecision.NewSampleRequest => "approval.newSampleRequest",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null)
    };

    private static readonly ApprovalDecision[] RetestDecisions =
    [
        ApprovalDecision.RetestRetainedSample,
        ApprovalDecision.NewSampleRequest
    ];

    [PostgresFact]
    public Task Covering_investigation_refuses_availability_and_submit_with_exact_gate_code() =>
        PostgresCases.RunAsync(RetestDecisions, CoveringInvestigationRefusesAsync);

    private async Task CoveringInvestigationRefusesAsync(ApprovalDecision decision)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);

        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.OosInvestigations.Add(new OosInvestigation
            {
                Number = $"OOS-RET-{Guid.NewGuid():N}"[..20],
                Kind = OosInvestigationKind.Oos,
                Status = OosInvestigationStatus.PhaseOne,
                SampleId = seed.Sample.Id,
                SectionId = section.Id,
                OpenedByUserId = reviewer.Id,
                OpenedAtUtc = ActionGuardScenarios.Now,
                TemplateSnapshotJson = "{}"
            });
            await arrange.SaveChangesAsync();
        }

        ActionAvailability entry;
        await using (var act = fixture.CreateDbContext())
        {
            var actions = await NewAvailabilityService(act).ForSampleAsync(seed.Sample.Id, section.Id, head.Id);
            Assert.NotNull(actions);
            entry = Assert.Single(actions, a => a.Action == ActionKey(decision));
        }

        Assert.False(entry.Allowed);
        Assert.Equal(MessageCodes.OosRetestNeedsApprovedPlan, entry.ReasonCode);
        Assert.Equal("A retest needs an approved retest plan.", entry.Reason);

        BusinessRuleException error;
        await using (var submitDb = fixture.CreateDbContext())
        {
            var approval = TestServiceFactory.SampleApproval(submitDb);
            error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                approval.DecideAsync(
                    seed.Sample.Id, head.Id, ActionGuardScenarios.Password, decision,
                    "Attempt retest", null, selectedTestOrderIds: [seed.Orders[0].Id], sectionId: section.Id));
        }

        Assert.Equal(entry.ReasonCode, error.Code);
        Assert.Equal(entry.Reason, error.Message);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking()
            .Where(s => s.Id == seed.Sample.Id).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking()
            .AnyAsync(s => s.EntityType == "Sample" && s.EntityId == seed.Sample.Id));
        Assert.False(await verify.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(Sample) && a.EntityId == seed.Sample.Id.ToString() && a.UserId == head.Id));
    }

    [PostgresFact]
    public Task Failing_result_without_investigation_refuses_availability_and_submit_with_exact_gate_code() =>
        PostgresCases.RunAsync(RetestDecisions, FailingResultWithoutInvestigationRefusesAsync);

    private async Task FailingResultWithoutInvestigationRefusesAsync(ApprovalDecision decision)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);

        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.Results.Add(new Result
            {
                TestOrderId = seed.Orders[0].Id,
                InterpretedValue = nameof(ResultStatus.Detected),
                EnteredByUserId = analyst.Id
            });
            await arrange.SaveChangesAsync();
        }

        ActionAvailability entry;
        await using (var act = fixture.CreateDbContext())
        {
            var actions = await NewAvailabilityService(act).ForSampleAsync(seed.Sample.Id, section.Id, head.Id);
            Assert.NotNull(actions);
            entry = Assert.Single(actions, a => a.Action == ActionKey(decision));
        }

        Assert.False(entry.Allowed);
        Assert.Equal(MessageCodes.OosFailingResultWithoutInvestigation, entry.ReasonCode);
        Assert.Equal("This sample has an out-of-specification result without an investigation.", entry.Reason);

        BusinessRuleException error;
        await using (var submitDb = fixture.CreateDbContext())
        {
            var approval = TestServiceFactory.SampleApproval(submitDb);
            error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                approval.DecideAsync(
                    seed.Sample.Id, head.Id, ActionGuardScenarios.Password, decision,
                    "Attempt retest", null, selectedTestOrderIds: [seed.Orders[0].Id], sectionId: section.Id));
        }

        Assert.Equal(entry.ReasonCode, error.Code);
        Assert.Equal(entry.Reason, error.Message);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(SampleStatus.UnderApproval, await verify.Samples.AsNoTracking()
            .Where(s => s.Id == seed.Sample.Id).Select(s => s.Status).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking()
            .AnyAsync(s => s.EntityType == "Sample" && s.EntityId == seed.Sample.Id));
        Assert.False(await verify.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(Sample) && a.EntityId == seed.Sample.Id.ToString() && a.UserId == head.Id));
    }

    [PostgresFact]
    public Task Clean_section_allows_availability_and_submit_succeeds() =>
        PostgresCases.RunAsync(RetestDecisions, CleanSectionAllowsAndSubmitSucceedsAsync);

    private async Task CleanSectionAllowsAndSubmitSucceedsAsync(ApprovalDecision decision)
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var analystOne = await _scenarios.AnalystAsync(section);
        var analystTwo = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);

        await using (var arrange = fixture.CreateDbContext())
        {
            if (!await arrange.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
                arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
            arrange.Results.Add(new Result
            {
                TestOrderId = seed.Orders[0].Id,
                InterpretedValue = nameof(ResultStatus.WithinLimits),
                EnteredByUserId = analyst.Id
            });
            await arrange.SaveChangesAsync();
        }

        ActionAvailability entry;
        await using (var act = fixture.CreateDbContext())
        {
            var actions = await NewAvailabilityService(act).ForSampleAsync(seed.Sample.Id, section.Id, head.Id);
            Assert.NotNull(actions);
            entry = Assert.Single(actions, a => a.Action == ActionKey(decision));
        }

        Assert.True(entry.Allowed);
        Assert.Null(entry.ReasonCode);
        Assert.Null(entry.Reason);

        await using (var submitDb = fixture.CreateDbContext())
        {
            var approval = TestServiceFactory.SampleApproval(submitDb);
            await approval.DecideAsync(
                seed.Sample.Id, head.Id, ActionGuardScenarios.Password, decision,
                "Retest requested", null,
                selectedTestOrderIds: [seed.Orders[0].Id],
                newSampleAnalystOneId: decision == ApprovalDecision.NewSampleRequest ? analystOne.Id : null,
                newSampleAnalystTwoId: decision == ApprovalDecision.NewSampleRequest ? analystTwo.Id : null,
                sectionId: section.Id);
        }

        var expectedSampleStatus = SampleStatus.RetestRequested;
        var expectedMeaning = decision == ApprovalDecision.RetestRetainedSample
            ? SignatureMeaning.RetestRequested
            : SignatureMeaning.NewSampleRequested;

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
        Assert.Equal(expectedSampleStatus, persisted.Status);
        Assert.Equal(decision, persisted.ApprovalDecision);

        var signature = await verify.ElectronicSignatures.AsNoTracking()
            .SingleAsync(s => s.EntityType == "Sample" && s.EntityId == seed.Sample.Id);
        Assert.Equal(expectedMeaning, signature.MeaningOfSignature);
        Assert.Equal(head.Id, signature.UserId);

        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == seed.Sample.Id.ToString() &&
            a.UserId == head.Id && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)SampleStatus.UnderApproval, before.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
        Assert.Equal((int)expectedSampleStatus, after.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
    }
}
