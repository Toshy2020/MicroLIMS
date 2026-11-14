using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The My Work endpoints against a real database with no sources registered:
// the aggregator must answer 200 with nothing in it, and the controller must
// refuse anonymous callers.
[Collection("PostgresDatabaseCollection")]
public class MyWorkEndpointsPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public MyWorkEndpointsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class NoopErrors : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DefaultSettingsReader : ISystemSettingsReader
    {
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(SystemSettingDefinitions.DefaultInt(key));
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(bool.Parse(SystemSettingDefinitions.Get(key).Default));
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(SystemSettingDefinitions.Get(key).Default);
    }

    private static MyWorkController BuildController(MicroLimsDbContext db, int userId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2031, 3, 4, 5, 6, 7, TimeSpan.Zero)));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ILabClock>(sp => new LabClock(sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IErrorCaptureService, NoopErrors>();
        services.AddSingleton<ISystemSettingsReader, DefaultSettingsReader>();
        services.AddSingleton(db);
        services.AddSingleton<IMicroLimsDbContext>(db);
        services.AddScoped<IUserSectionScopeService, UserSectionScopeService>();
        services.AddScoped<DashboardLabScopeService>();
        services.AddScoped<IMyWorkService, MyWorkService>();
        var provider = services.BuildServiceProvider();

        return new MyWorkController(provider.GetRequiredService<IMyWorkService>(), provider.GetRequiredService<DashboardLabScopeService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"))
                }
            }
        };
    }

    [PostgresFact]
    public async Task NoSources_List_200_EmptyGroups_Summary_ZeroAndComplete()
    {
        await using var actDb = _fixture.CreateDbContext();
        var controller = BuildController(actDb, _fixture.SeededUserId);

        var list = Assert.IsType<OkObjectResult>(await controller.Get(null, default));
        var listBody = Assert.IsType<ApiResponse<MyWorkResponse>>(list.Value);
        Assert.Empty(listBody.Data!.Groups);
        Assert.Empty(listBody.Data.UnavailableSources);
        Assert.Equal(new DateTime(2031, 3, 4, 5, 6, 7, DateTimeKind.Utc), listBody.Data.ServerTimeUtc);

        var summary = Assert.IsType<OkObjectResult>(await controller.GetSummary(default));
        var summaryBody = Assert.IsType<ApiResponse<MyWorkSummary>>(summary.Value);
        Assert.Equal(new MyWorkSummary(0, 0, 0, true), summaryBody.Data);
    }

    [Fact]
    public void Unauthenticated_401()
    {
        // The pipeline answers 401 when a controller carries [Authorize] and no
        // action opts out with [AllowAnonymous]; the authorization matrix pins the roles.
        var controller = typeof(MyWorkController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
