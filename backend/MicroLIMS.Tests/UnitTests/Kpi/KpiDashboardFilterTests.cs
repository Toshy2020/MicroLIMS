using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// The category / location / test-code filters that the retired
// KpiService.GetCompletionStatsAsync test pinned, asserted now on the
// registry's wip-backlog tile (open = Pending or InProgress, not superseded).
public class KpiDashboardFilterTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Sample A (finished product, "Product A"): one Approved and one Rejected TAMC test - nothing open.
    // Sample B (raw material, "Product B"): one Approved and one Pending TYMC test - one open.
    private static async Task<MicroLimsDbContext> SeedAsync()
    {
        var db = NewDb();

        // An administrator is unrestricted, so section scope does not narrow the figures under test.
        db.Roles.Add(new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" });
        db.Users.Add(new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, PasswordHash = "x" });

        var itemA = new Item { Id = 101, Name = "Product A", Code = "PA-01" };
        var itemB = new Item { Id = 102, Name = "Product B", Code = "PB-01" };
        db.Items.AddRange(itemA, itemB);

        var sampleA = new Sample
        {
            Id = 1, Category = SampleCategory.FinishedProduct, Item = itemA, ItemId = itemA.Id,
            ControlNumber = "FP-001", Status = SampleStatus.Approved
        };
        sampleA.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        sampleA.TestOrders.Add(new TestOrder { Id = 2, TestCode = "TAMC", Status = ApprovalStatus.Rejected, CurrentStep = WorkflowStep.Approved });

        var sampleB = new Sample
        {
            Id = 2, Category = SampleCategory.RawMaterial, Item = itemB, ItemId = itemB.Id,
            ControlNumber = "RM-001", Status = SampleStatus.InTesting
        };
        sampleB.TestOrders.Add(new TestOrder { Id = 3, TestCode = "TYMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        sampleB.TestOrders.Add(new TestOrder { Id = 4, TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        db.Samples.AddRange(sampleA, sampleB);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<decimal> OpenTestsAsync(
        MicroLimsDbContext db, string? category = null, string? location = null, string? testCode = null)
    {
        var service = TestServiceFactory.KpiDashboard(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dashboard = await service.GetDashboardAsync(1, true, today.AddDays(-30), today, category, location, testCode, null);
        var entry = dashboard.Kpis.Single(k => k.Key == "wip-backlog");
        Assert.True(entry.Error is null, entry.Error);
        return entry.Result!.Value ?? 0m;
    }

    [Fact]
    public async Task WipBacklog_Unfiltered_CountsEveryOpenTest()
    {
        await using var db = await SeedAsync();
        Assert.Equal(1m, await OpenTestsAsync(db));
    }

    [Fact]
    public async Task WipBacklog_FiltersByCategory()
    {
        await using var db = await SeedAsync();
        Assert.Equal(0m, await OpenTestsAsync(db, category: "FinishedProduct"));
        Assert.Equal(1m, await OpenTestsAsync(db, category: "RawMaterial"));
    }

    [Fact]
    public async Task WipBacklog_FiltersByLocation()
    {
        await using var db = await SeedAsync();
        Assert.Equal(1m, await OpenTestsAsync(db, location: "Product B"));
        Assert.Equal(0m, await OpenTestsAsync(db, location: "Product A"));
    }

    [Fact]
    public async Task WipBacklog_FiltersByTestCode()
    {
        await using var db = await SeedAsync();
        Assert.Equal(1m, await OpenTestsAsync(db, testCode: "TYMC"));
        Assert.Equal(0m, await OpenTestsAsync(db, testCode: "TAMC"));
    }
}
