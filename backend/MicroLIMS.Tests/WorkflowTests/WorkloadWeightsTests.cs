using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WorkloadWeightsTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task UpdateWorkloadWeightAsync_PersistsNewWeightAndCreatesAuditHistory()
    {
        await using var db = NewDb();
        var weight = new WorkloadWeight
        {
            TestCode = "TAMC",
            TestName = "Total Aerobic Microbial Count",
            Category = SampleCategory.FinishedProduct,
            Weight = 1.0m,
            EffectiveDate = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            ReasonForChange = "Initial baseline",
            ChangedByUserId = 1,
            ChangedByName = "System Administrator",
            ChangedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.WorkloadWeights.Add(weight);
        await db.SaveChangesAsync();

        var kpiService = new KpiService(db);
        var result = await kpiService.UpdateWorkloadWeightAsync(
            "TAMC",
            1.5m,
            "Updated procedure complexity",
            userId: 2,
            userName: "Jane SectionHead"
        );

        Assert.NotNull(result);
        Assert.Equal(1.5m, result.WorkloadWeight);

        var updated = await db.WorkloadWeights.SingleAsync(w => w.TestCode == "TAMC");
        Assert.Equal(1.5m, updated.Weight);
        Assert.Equal("Updated procedure complexity", updated.ReasonForChange);
        Assert.Equal(2, updated.ChangedByUserId);
        Assert.Equal("Jane SectionHead", updated.ChangedByName);

        var history = await db.WorkloadWeightHistories.SingleAsync(h => h.WorkloadWeightId == updated.Id);
        Assert.Equal(1.0m, history.PreviousWeight);
        Assert.Equal(1.5m, history.NewWeight);
        Assert.Equal("Updated procedure complexity", history.ReasonForChange);
        Assert.Equal(2, history.ChangedByUserId);
        Assert.Equal("Jane SectionHead", history.ChangedByName);
    }

    [Fact]
    public async Task GetAnalystKpisAsync_CalculatesWorkloadUnitsFromConfiguredWeights()
    {
        await using var db = NewDb();
        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var analyst = new User { Id = 10, FullName = "Amal Hamdy", Username = "ahamdy", RoleId = role.Id, PasswordHash = "x" };
        db.Users.Add(analyst);

        var weight = new WorkloadWeight
        {
            TestCode = "STERILITY",
            TestName = "Sterility Test",
            Category = SampleCategory.FinishedProduct,
            Weight = 3.0m,
            EffectiveDate = DateTime.UtcNow,
            IsActive = true,
            ReasonForChange = "Baseline",
            ChangedByUserId = 1,
            ChangedByName = "Admin",
            ChangedAt = DateTime.UtcNow
        };
        db.WorkloadWeights.Add(weight);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-10", Status = SampleStatus.Received };
        var order1 = new TestOrder { TestCode = "STERILITY", AssignedAnalystId = 10, Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved };
        var order2 = new TestOrder { TestCode = "STERILITY", AssignedAnalystId = 10, Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved };
        sample.TestOrders.Add(order1);
        sample.TestOrders.Add(order2);
        db.Samples.Add(sample);

        await db.SaveChangesAsync();

        var kpiService = new KpiService(db);
        var kpis = await kpiService.GetAnalystKpisAsync();

        var analystKpi = Assert.Single(kpis, k => k.UserId == 10);
        Assert.Equal(2, analystKpi.CompletedTests);
        // 2 completed STERILITY tests * 3.0 weight = 6.0 workload units
        Assert.Equal(6.0, analystKpi.WorkloadUnits);
    }
}
