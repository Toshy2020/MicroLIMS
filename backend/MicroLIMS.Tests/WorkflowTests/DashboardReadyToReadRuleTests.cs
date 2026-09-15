using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The dashboards count incubations (bench work stays test-level), but decide
// "ready to read" by the same rule as the Workspace tile: the expected reading
// time or the incubation window end has passed, or the minimum duration was
// overridden - and only on active, non-superseded tests.
public class DashboardReadyToReadRuleTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static TestOrder OrderWithOpenIncubation(string testCode, ApprovalStatus status, Action<Incubation> configure, bool superseded = false)
    {
        var order = new TestOrder { TestCode = testCode, Status = status, CurrentStep = WorkflowStep.Incubating, IsSuperseded = superseded };
        var incubation = new Incubation { StepNumber = 1, StepName = "Stage 1", StartedAt = DateTime.UtcNow.AddHours(-30) };
        configure(incubation);
        order.Incubations.Add(incubation);
        return order;
    }

    private static async Task SeedAsync(MicroLimsDbContext db)
    {
        var now = DateTime.UtcNow;
        var cause = new CauseOfTesting { Name = "Routine" };
        var sample = new Sample { ReferenceNumber = "FP-READY", Status = SampleStatus.InTesting, CauseOfTesting = cause };

        // Window ended, no expected reading time: ready (the old dashboard rule said incubating).
        sample.TestOrders.Add(OrderWithOpenIncubation("TAMC", ApprovalStatus.InProgress, i => i.IncubationEndUtc = now.AddHours(-1)));
        // Minimum duration overridden while times are still ahead: ready.
        sample.TestOrders.Add(OrderWithOpenIncubation("TAMC", ApprovalStatus.Pending, i =>
        {
            i.ExpectedReadingAt = now.AddHours(5);
            i.IncubationEndUtc = now.AddHours(5);
            i.MinimumDurationOverriddenByUserId = 7;
        }));
        // Still incubating.
        sample.TestOrders.Add(OrderWithOpenIncubation("TAMC", ApprovalStatus.InProgress, i =>
        {
            i.ExpectedReadingAt = now.AddHours(5);
            i.IncubationEndUtc = now.AddHours(6);
        }));
        // Past reading time, but the tests are no longer active work: not counted at all.
        sample.TestOrders.Add(OrderWithOpenIncubation("TYMC", ApprovalStatus.InProgress, i => i.ExpectedReadingAt = now.AddHours(-2), superseded: true));
        sample.TestOrders.Add(OrderWithOpenIncubation("TYMC", ApprovalStatus.Voided, i => i.ExpectedReadingAt = now.AddHours(-2)));

        db.Samples.Add(sample);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SectionHeadDashboard_CountsReadyAndIncubatingByTheWorkspaceRule_OnActiveTestsOnly()
    {
        await using var db = NewDb();
        await SeedAsync(db);

        var dashboard = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(2, dashboard.ReadyToRead);
        Assert.Equal(1, dashboard.Incubating);
    }

    [Fact]
    public async Task IncubationOverview_SplitsByTheWorkspaceRule_AndSkipsInactiveTests()
    {
        await using var db = NewDb();
        await SeedAsync(db);

        var overview = await TestServiceFactory.Dashboard(db).GetIncubationOverviewAsync();

        var tamc = Assert.Single(overview);
        Assert.Equal("TAMC", tamc.TestCode);
        Assert.Equal(2, tamc.ReadyToRead);
        Assert.Equal(1, tamc.Incubating);
    }

    [Fact]
    public async Task WorkspaceReadyToReadTile_AgreesWithTheDashboardOnTheSameSample()
    {
        await using var db = NewDb();
        await SeedAsync(db);

        var counts = await new TestingWorkspaceService(db).GetWorkloadCountsAsync();
        var page = await new TestingWorkspaceService(db).GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "readyToRead" });

        Assert.Equal(1, counts.ReadyToRead);
        Assert.Single(page.Items);
    }
}
