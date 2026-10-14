using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiDashboardControllerTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);

    private sealed class CapturingKpi : IKpi
    {
        public KpiQuery? Seen { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }
        public string Key => "cap";
        public string Name => "cap";
        public string Unit => "%";
        public KpiDirection Direction => KpiDirection.Neutral;
        public KpiGroup Group => KpiGroup.Headline;
        public int Order => 1;
        public string Definition => "d";
        public string ClockEvents => "c";
        public string Population => "p";
        public string Source => "s";
        public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct)
        {
            Seen = q;
            return Task.FromResult(KpiScope.ToResult(KpiCore.Nothing(), null, Unit));
        }
        public Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct)
        {
            Seen = q;
            Page = page;
            PageSize = pageSize;
            return Task.FromResult(new KpiRecordPageDto(Key, page, pageSize, 0, Array.Empty<KpiRecordDto>()));
        }
    }

    private static (KpiDashboardController Controller, CapturingKpi Kpi) Build(params string[] permissions)
    {
        var kpi = new CapturingKpi();
        var service = new KpiDashboardService(KpiTestData.NewDb(), new KpiRegistry(new IKpi[] { kpi }),
            new StubSectionScope(_ => null), new FixedTimeProvider(new DateTimeOffset(KpiTestData.Now)));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "5") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var controller = new KpiDashboardController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
        return (controller, kpi);
    }

    [Fact]
    public async Task ViewTeamClaim_SetsCanViewTeam_AndUserIdComesFromTheToken()
    {
        var (controller, kpi) = Build(PermissionConstants.KpiView, PermissionConstants.KpiViewTeam);
        var result = await controller.GetDashboard(From, To, null, null, null, 9, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(kpi.Seen!.CanViewTeam);
        Assert.Equal(5, kpi.Seen.ViewerUserId);
        Assert.Equal(9, kpi.Seen.AnalystId);
    }

    [Fact]
    public async Task WithoutViewTeamClaim_AnotherAnalyst_IsForbidden_OnBothEndpoints()
    {
        var (controller, _) = Build(PermissionConstants.KpiView);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetDashboard(From, To, null, null, null, 9, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetRecords("cap", From, To, null, null, null, 9, 1, 50, default));
    }

    [Fact]
    public async Task WithoutViewTeamClaim_NoAnalystId_StillGetsLabLevelFigures()
    {
        var (controller, kpi) = Build(PermissionConstants.KpiView);
        await controller.GetDashboard(From, To, null, null, null, null, default);

        Assert.False(kpi.Seen!.CanViewTeam);
        Assert.Null(kpi.Seen.AnalystId);
    }

    [Fact]
    public async Task Records_PassTheKeyAndPaging()
    {
        var (controller, kpi) = Build(PermissionConstants.KpiView);
        var result = Assert.IsType<OkObjectResult>(await controller.GetRecords("cap", From, To, null, null, null, null, 2, 5000, default));
        Assert.NotNull(result.Value);
        Assert.Equal(2, kpi.Page);
        Assert.Equal(200, kpi.PageSize);                 // capped by the service
        Assert.Equal(5, kpi.Seen!.ViewerUserId);
    }
}
