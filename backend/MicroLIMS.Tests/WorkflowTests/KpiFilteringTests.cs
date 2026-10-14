using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// What is left of the pre-remodel KpiService tests: DashboardService still reads
// GetOverdueAnalystStageSamplesAsync for its "Attention Items" list. The filter,
// TAT, queue and return-count behaviours moved to the KPI registry
// (UnitTests/Kpi).
public class KpiFilteringTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task GetOverdueAnalystStageSamplesAsync_RegressionCheck_ReturnsAllOverdueWithoutFilters()
    {
        await using var db = NewDb();

        var now = DateTime.UtcNow;
        var overdueAssigned = now.AddDays(-10); // > 7 days ago

        var s1 = new Sample { Id = 61, Category = SampleCategory.FinishedProduct, ControlNumber = "C61", Status = SampleStatus.InTesting };
        var s2 = new Sample { Id = 62, Category = SampleCategory.RawMaterial, ControlNumber = "C62", Status = SampleStatus.InTesting };
        s1.TestOrders.Add(new TestOrder { Id = 61, AssignedAnalystId = 1, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        s2.TestOrders.Add(new TestOrder { Id = 62, AssignedAnalystId = 2, TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });

        db.Samples.AddRange(s1, s2);
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 61, PreparedAt = overdueAssigned, PreparedByUserId = 1 });
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 62, PreparedAt = overdueAssigned, PreparedByUserId = 2 });
        await db.SaveChangesAsync();

        var service = new KpiService(db);
        var overdue = await service.GetOverdueAnalystStageSamplesAsync(now.AddDays(-30), now);

        // Must return both overdue samples across all categories/analysts
        Assert.Equal(2, overdue.Count);
        Assert.Contains(overdue, o => o.SampleId == 61);
        Assert.Contains(overdue, o => o.SampleId == 62);
    }

    // S2 decision: DashboardService's overdue list keeps the fixed 7-day rule.
    // A 14-day profile target (used by the new KPI tiles) must NOT stretch it.
    [Fact]
    public async Task GetOverdueAnalystStageSamplesAsync_StaysOnSevenDayRule_EvenWithALongerProfileTarget()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        db.Roles.Add(new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst" });
        db.Users.Add(new User { Id = 1, FullName = "A", Username = "a", RoleId = 1, PasswordHash = "x" });
        var sample = new Sample { Id = 1, Category = SampleCategory.FinishedProduct, ControlNumber = "FP-900", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", AssignedAnalystId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        db.Samples.Add(sample);
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 1, PreparedAt = now.AddDays(-8), PreparedByUserId = 1 });
        db.WorkflowProfileVersions.Add(new WorkflowProfileVersion
        {
            WorkflowType = WorkflowType.CountTest, TestCode = null, TargetAnalystDays = 14m, WorkUnits = 1m,
            EffectiveFrom = new DateOnly(2026, 1, 1), Status = WorkflowProfileStatus.Effective, ReasonForChange = "seed", CreatedByUserId = 1
        });
        await db.SaveChangesAsync();

        var overdue = await new KpiService(db).GetOverdueAnalystStageSamplesAsync(now.AddDays(-30), now.AddDays(1));

        Assert.Equal(new[] { 1 }, overdue.Select(o => o.SampleId));
    }
}
