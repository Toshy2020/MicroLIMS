using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// D8: an analyst sees only their own numbers, enforced on the server. These tests go through the controller so the
// claim handling is covered too: "viewTeam" means the JWT carries the Kpi.ViewTeam permission claim.
public class WorkloadOwnRowOnlyTests
{
    private static KpiWorkloadController Controller(MicroLimsDbContext db, int userId, bool viewTeam)
    {
        var time = Clock();
        var controller = new KpiWorkloadController(
            TestServiceFactory.AnalystWorkload(db, time), TestServiceFactory.KpiSettings(db, time), TestServiceFactory.AnalystAvailability(db, time));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (viewTeam) claims.Add(new Claim("permission", PermissionConstants.KpiViewTeam));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private static T Data<T>(IActionResult result) => (T)((ApiResponse<object>)((OkObjectResult)result).Value!).Data!;

    private static async Task<MicroLimsDbContext> SeededAsync()
    {
        var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 2, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 3, "TAMC", Bob, At(8, 12));
        await DeliverAsync(db, 4, "TAMC", Bob, At(8, 12));
        await OpenAsync(db, 500, "PH", Alice);
        await OpenAsync(db, 501, "PH", Bob);
        db.AnalystAvailabilities.AddRange(
            new AnalystAvailability { UserId = Alice, FromDate = new(2026, 10, 14), ToDate = new(2026, 10, 14), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam },
            new AnalystAvailability { UserId = Bob, FromDate = new(2026, 10, 15), ToDate = new(2026, 10, 15), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Workload_WithoutViewTeam_ReturnsOnlyTheCallersRow()
    {
        await using var db = await SeededAsync();

        var dto = Data<AnalystWorkloadDto>(await Controller(db, Alice, false).GetWorkload(WeekFrom, WeekTo, null, null, null, null));

        Assert.True(dto.OnlyOwnRow);
        Assert.Equal(Alice, Assert.Single(dto.Rows).UserId);
    }

    [Fact]
    public async Task Workload_WithoutViewTeam_AskingForAnotherAnalyst_IsForbidden()
    {
        await using var db = await SeededAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Controller(db, Alice, false).GetWorkload(WeekFrom, WeekTo, null, null, null, Bob));
    }

    [Fact]
    public async Task Workload_WithoutViewTeam_AskingForTheirOwnId_IsAllowed()
    {
        await using var db = await SeededAsync();

        var dto = Data<AnalystWorkloadDto>(await Controller(db, Alice, false).GetWorkload(WeekFrom, WeekTo, null, null, null, Alice));

        Assert.Equal(Alice, Assert.Single(dto.Rows).UserId);
    }

    [Fact]
    public async Task WorkloadTests_WithoutViewTeam_ListOnlyTheCallersTests_AndRefuseAnotherAnalyst()
    {
        await using var db = await SeededAsync();
        var controller = Controller(db, Alice, false);

        var page = Data<KpiRecordPageDto>(await controller.GetWorkloadTests(WeekFrom, WeekTo, null, null, null, null, 1, 50));

        Assert.Equal(3, page.Total); // two delivered + one open, all Alice's
        Assert.All(page.Items, i => Assert.Equal("Alice Analyst", i.AnalystName));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.GetWorkloadTests(WeekFrom, WeekTo, null, null, null, Bob, 1, 50));
    }

    [Fact]
    public async Task Availability_WithoutViewTeam_ListsOnlyTheCallersRows_AndRefusesAnotherUser()
    {
        await using var db = await SeededAsync();
        var controller = Controller(db, Alice, false);

        var own = Data<List<AnalystAvailabilityDto>>(await controller.ListAvailability(null, null, null));

        Assert.Equal(Alice, Assert.Single(own).UserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.ListAvailability(Bob, null, null));
    }

    [Fact]
    public async Task WithViewTeam_TheViewerSeesEveryAnalystInTheirSections()
    {
        await using var db = await SeededAsync();
        var controller = Controller(db, Sam, true);

        var workload = Data<AnalystWorkloadDto>(await controller.GetWorkload(WeekFrom, WeekTo, null, null, null, null));
        var leave = Data<List<AnalystAvailabilityDto>>(await controller.ListAvailability(null, null, null));

        Assert.False(workload.OnlyOwnRow);
        Assert.Contains(workload.Rows, r => r.UserId == Alice);
        Assert.Contains(workload.Rows, r => r.UserId == Bob);
        Assert.DoesNotContain(workload.Rows, r => r.UserId == Dana); // section 2 only
        Assert.Equal(2, leave.Count);
    }

    [Fact] // Review Focus 5
    public async Task Workload_ViewerWithoutViewTeamWhoIsNotAnAnalyst_GetsNoRows()
    {
        await using var db = await SeededAsync();

        // Sam is a Section Head holding Kpi.View but not Kpi.ViewTeam, with no assigned tests.
        var dto = Data<AnalystWorkloadDto>(await Controller(db, Sam, false).GetWorkload(WeekFrom, WeekTo, null, null, null, null));

        Assert.True(dto.OnlyOwnRow);
        Assert.Empty(dto.Rows);
    }

    [Fact]
    public void EveryGetAction_IsCoveredByTheTestsAbove()
    {
        // A new read endpoint must be added here AND get an own-row-only test, so D8 cannot silently regress.
        var gets = typeof(KpiWorkloadController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpGetAttribute>() is not null)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "GetSettings", "GetWorkload", "GetWorkloadTests", "ListAvailability" }, gets);
    }
}
