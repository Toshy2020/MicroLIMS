using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.IntegrationTests;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

[Collection("PostgresDatabaseCollection")]
public class RetestBatchLocationTests(PostgresTestFixture fixture)
{
    [PostgresFact]
    public async Task RetestRetainedSample_EmBatchOrder_CarriesOnlyFailedLocationsAndSkipsPreparation()
    {
        var scenarios = new ActionGuardScenarios(fixture);
        var section = await scenarios.SectionAsync();
        var analyst = await scenarios.AnalystAsync(section);
        var reviewer = await scenarios.ReviewerAsync(section);
        var head = await scenarios.HeadAsync(section);
        var seed = await scenarios.UnderApprovalAsync(section, reviewer.Id, analystId: analyst.Id);
        var sampleId = seed.Sample.Id;
        var orderId = seed.Orders[0].Id;
        int[] configurationIds;

        await using (var arrange = fixture.CreateDbContext())
        {
            if (!await arrange.CausesOfTesting.AnyAsync(c => c.Name == "Retest"))
                arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });

            var tag = Guid.NewGuid().ToString("N")[..8];
            var department = new Department { Name = $"Filling Suite {tag}", Class = "B", TestingFrequency = "Monthly" };
            var sample = await arrange.Samples.SingleAsync(s => s.Id == sampleId);
            sample.Category = SampleCategory.EnvironmentalMonitoring;
            sample.Department = department;
            sample.PreparationStatus = SamplePreparationStatus.Ready;
            var statuses = new[] { ResultStatus.ActionLimitExceeded, ResultStatus.AlertLimitExceeded, ResultStatus.WithinLimits };
            var configurations = statuses.Select((_, index) => new RoomTestConfiguration
            {
                Room = new Room { Name = $"Room {index + 1} {tag}", Department = department, GradeClassification = "B" },
                TestType = "PassiveAirSample", TestCode = seed.Orders[0].TestCode,
                AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Unit = "CFU/plate/4 hours"
            }).ToArray();
            arrange.SampleLocations.AddRange(configurations.Zip(statuses, (configuration, status) => new SampleLocation
            {
                SampleId = sampleId, TestOrderId = orderId, LocationType = LocationType.Room,
                RoomTestConfiguration = configuration, Status = status
            }));
            await arrange.SaveChangesAsync();
            configurationIds = configurations.Select(c => c.Id).ToArray();
        }

        await using (var refusal = fixture.CreateDbContext())
        {
            refusal.CurrentUserId = head.Id;
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.SampleApproval(refusal).DecideAsync(
                sampleId, head.Id, ActionGuardScenarios.Password, ApprovalDecision.RetestRetainedSample,
                null, null, selectedTestOrderIds: [int.MaxValue]));
            Assert.Equal("One or more selected tests do not belong to this sample's current test orders.", error.Message);
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = head.Id;
            await TestServiceFactory.SampleApproval(act).DecideAsync(
                sampleId, head.Id, ActionGuardScenarios.Password, ApprovalDecision.RetestRetainedSample,
                null, null, selectedTestOrderIds: [orderId]);
        }

        await using var verify = fixture.CreateDbContext();
        var origin = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
        var retest = await verify.Samples.AsNoTracking().SingleAsync(s => s.OriginSampleId == sampleId);
        Assert.Equal(SampleStatus.RetestRequested, origin.Status);
        Assert.NotNull(origin.OosGroupCode);
        Assert.Equal(origin.OosGroupCode, retest.OosGroupCode);
        Assert.Equal(SamplePreparationStatus.Ready, retest.PreparationStatus);
        var retestOrder = await verify.TestOrders.AsNoTracking().SingleAsync(o => o.SampleId == retest.Id);
        Assert.Equal(seed.Orders[0].TestCode, retestOrder.TestCode);
        var retestLocations = await verify.SampleLocations.AsNoTracking().Where(l => l.TestOrderId == retestOrder.Id).ToListAsync();
        Assert.Equal(configurationIds.Take(2).Order(), retestLocations.Select(l => l.RoomTestConfigurationId.GetValueOrDefault()).Order());
        Assert.All(retestLocations, l =>
        {
            Assert.Null(l.Status);
            Assert.Null(l.CFUResult);
            Assert.Null(l.EnteredAt);
            Assert.Null(l.EnteredByUserId);
        });
        Assert.Equal(3, await verify.SampleLocations.AsNoTracking().CountAsync(l => l.TestOrderId == orderId));
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.EntityId == sampleId && s.EntityType == "Sample");
        Assert.Equal(SignatureMeaning.RetestRequested, signature.MeaningOfSignature);
        Assert.Equal(head.Id, signature.UserId);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Sample) && a.EntityId == sampleId.ToString() && a.UserId == head.Id && a.Action == "Update");
        using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)SampleStatus.UnderApproval, before.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
        Assert.Equal((int)SampleStatus.RetestRequested, after.RootElement.GetProperty(nameof(Sample.Status)).GetInt32());
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == sampleId));
    }
}
