using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP4.4: maintenance mode (write-blocking, job gate, signed toggle with a security
// event) and the public status endpoint.
public class MaintenanceModeTests
{
    private const string AdminPassword = "AdminP@ssw0rd!";
    private const string Reason = "Database upgrade window";

    private sealed class Rig
    {
        public required MicroLimsDbContext Db { get; init; }
        public required SystemSettingsCache Cache { get; init; }
        public required SystemSettingsReader Reader { get; init; }
        public required SystemSettingsService Service { get; init; }
    }

    private static Rig CreateRig()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true });
        db.Users.Add(new User { Id = 1, FullName = "Acting Admin", Username = "actor", RoleId = 1, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, 4) });
        db.SaveChanges();
        var cache = new SystemSettingsCache();
        var reader = new SystemSettingsReader(db, cache);
        var signatures = new ElectronicSignatureService(db, TimeProvider.System, reader);
        var service = new SystemSettingsService(db, cache, signatures, null, null, null,
            new SecurityAuditService(db, new SystemSecurityRequestContext()));
        return new Rig { Db = db, Cache = cache, Reader = reader, Service = service };
    }

    private static async Task SetOn(Rig rig, bool on = true) =>
        await rig.Service.UpdateAsync(SystemSettingKeys.MaintenanceMode, on ? "true" : "false", Reason, 1, AdminPassword, null);

    private static ClaimsPrincipal User(params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    // Returns the status code, or null when the request reached the next delegate.
    private static async Task<(int? Status, string Body, HttpContext Context)> Send(
        Rig rig, string method, string path, ClaimsPrincipal? user)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (user is not null) context.User = user;
        var reached = false;
        var middleware = new MaintenanceModeMiddleware(_ => { reached = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, rig.Reader);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (reached ? null : context.Response.StatusCode, body, context);
    }

    [Fact]
    public async Task WhenOff_EverythingPasses()
    {
        var rig = CreateRig();
        Assert.Null((await Send(rig, "POST", "/api/samples", User())).Status);
    }

    [Theory]
    [InlineData("POST", "/api/samples")]
    [InlineData("PUT", "/api/equipment/4")]
    [InlineData("DELETE", "/api/documents/9")]
    [InlineData("PATCH", "/api/users/2")]
    [InlineData("POST", "/api/admin/jobs/SessionTokenCleanupWorker/run")]
    public async Task WhenOn_WritesAreRefusedWith503AndTheMessage(string method, string path)
    {
        var rig = CreateRig();
        await SetOn(rig);
        var (status, body, context) = await Send(rig, method, path, User(PermissionConstants.SamplesReview));

        Assert.Equal(503, status);
        Assert.Equal("on", context.Response.Headers[MaintenanceModeMiddleware.HeaderName]);
        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(SystemSettingDefinitions.DefaultMaintenanceMessage, json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task WhenOn_TheConfiguredMessageIsUsed()
    {
        var rig = CreateRig();
        await rig.Service.UpdateAsync(SystemSettingKeys.MaintenanceMessage, "Back at 18:00 UTC", Reason, 1, null, null);
        await SetOn(rig);
        var (_, body, _) = await Send(rig, "POST", "/api/samples", User());
        Assert.Contains("Back at 18:00 UTC", body);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task WhenOn_ReadsStillPass(string method)
    {
        var rig = CreateRig();
        await SetOn(rig);
        Assert.Null((await Send(rig, method, "/api/samples", User())).Status);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/logout")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/auth/change-password")]
    public async Task WhenOn_AuthenticationEndpointsPass(string path)
    {
        var rig = CreateRig();
        await SetOn(rig);
        Assert.Null((await Send(rig, "POST", path, User())).Status);
        Assert.Null((await Send(rig, "POST", path, null)).Status);
    }

    [Fact]
    public async Task WhenOn_TheSettingsEndpointStaysOpenSoMaintenanceCanBeEnded()
    {
        var rig = CreateRig();
        await SetOn(rig);
        Assert.Null((await Send(rig, "PUT", "/api/admin/settings/System.MaintenanceMode", User())).Status);
    }

    [Fact]
    public async Task WhenOn_AdminAreaIsOpenOnlyToConfigureSettingsHolders()
    {
        var rig = CreateRig();
        await SetOn(rig);
        var path = "/api/admin/jobs/SessionTokenCleanupWorker/run";
        Assert.Null((await Send(rig, "POST", path, User(PermissionConstants.SystemConfigureSettings))).Status);
        Assert.Equal(503, (await Send(rig, "POST", path, User(PermissionConstants.SystemViewErrorLog))).Status);
        // Holding the permission does not open the rest of the API.
        Assert.Equal(503, (await Send(rig, "POST", "/api/samples", User(PermissionConstants.SystemConfigureSettings))).Status);
    }

    [Fact]
    public async Task WhenOn_AnonymousRequestsAreNotTheMiddlewaresBusiness()
    {
        var rig = CreateRig();
        await SetOn(rig);
        Assert.Null((await Send(rig, "POST", "/api/auth/password-reset/request", null)).Status);
    }

    [Fact]
    public async Task WhenOn_InstrumentAgentWritesAreRefusedToo()
    {
        var rig = CreateRig();
        await SetOn(rig);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/instrument-ingest/results";
        context.Request.Headers[MicroLIMS.API.Authorization.AgentKeyDefaults.HeaderName] = "key";
        context.Response.Body = new MemoryStream();
        var reached = false;
        await new MaintenanceModeMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(context, rig.Reader);
        Assert.False(reached);
        Assert.Equal(503, context.Response.StatusCode);
    }

    [Fact]
    public async Task TurningItOffReopensWritesImmediately_WithoutWaitingForTheCacheToExpire()
    {
        var rig = CreateRig();
        await SetOn(rig);
        Assert.Equal(503, (await Send(rig, "POST", "/api/samples", User())).Status);   // caches "on"
        await SetOn(rig, false);
        Assert.Null((await Send(rig, "POST", "/api/samples", User())).Status);
    }

    // ---- toggling -----------------------------------------------------------

    [Fact]
    public async Task Toggling_RequiresTheElectronicSignature()
    {
        var rig = CreateRig();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            rig.Service.UpdateAsync(SystemSettingKeys.MaintenanceMode, "true", Reason, 1, null, null));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            rig.Service.UpdateAsync(SystemSettingKeys.MaintenanceMode, "true", Reason, 1, "wrong-password", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.UpdateAsync(SystemSettingKeys.MaintenanceMode, "true", "  ", 1, AdminPassword, null));
        Assert.False(await rig.Reader.GetBoolAsync(SystemSettingKeys.MaintenanceMode));
        Assert.Empty(rig.Db.SecurityAuditEvents);

        await SetOn(rig);
        Assert.True(await rig.Reader.GetBoolAsync(SystemSettingKeys.MaintenanceMode));
        var version = await rig.Db.SystemSettingVersions.SingleAsync();
        Assert.NotNull(version.ElectronicSignatureId);
    }

    [Fact]
    public async Task Toggling_RecordsASecurityEventWithActorReasonAndNewState()
    {
        var rig = CreateRig();
        await SetOn(rig);
        await SetOn(rig, false);

        var events = await rig.Db.SecurityAuditEvents.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(SecurityEventCodes.MaintenanceModeChanged, e.EventCode);
            Assert.Equal(1, e.ActorUserId);
            Assert.Equal(Reason, e.Reason);
        });
        Assert.Contains("\"MaintenanceMode\":true", events[0].Metadata);
        Assert.Contains("\"MaintenanceMode\":false", events[1].Metadata);
    }

    [Fact]
    public async Task ChangingTheMessageOrBanner_NeedsReasonButNoSignature_AndRecordsNoMaintenanceEvent()
    {
        var rig = CreateRig();
        await rig.Service.UpdateAsync(SystemSettingKeys.AnnouncementBanner, "Planned downtime Friday", Reason, 1, null, null);
        await rig.Service.UpdateAsync(SystemSettingKeys.AnnouncementSeverity, "warning", Reason, 1, null, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.UpdateAsync(SystemSettingKeys.AnnouncementSeverity, "critical", Reason, 1, null, null));
        Assert.Empty(rig.Db.SecurityAuditEvents);

        // An empty banner means none.
        await rig.Service.UpdateAsync(SystemSettingKeys.AnnouncementBanner, "", Reason, 1, null, null);
        Assert.Equal("", await rig.Reader.GetStringAsync(SystemSettingKeys.AnnouncementBanner));
    }

    // ---- background jobs ----------------------------------------------------

    private static (BackgroundJobRunRecorder Recorder, MicroLimsDbContext Db, Rig Rig) CreateRecorder()
    {
        var rig = CreateRig();
        var services = new ServiceCollection();
        services.AddSingleton<IMicroLimsDbContext>(rig.Db);
        services.AddSingleton<ISystemSettingsReader>(rig.Reader);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var recorder = new BackgroundJobRunRecorder(scopes, TimeProvider.System, NullLogger<BackgroundJobRunRecorder>.Instance);
        return (recorder, rig.Db, rig);
    }

    [Fact]
    public async Task WhenOn_GxpWorkersSkipTheirPass_AndTheSkipIsRecorded()
    {
        var (recorder, db, rig) = CreateRecorder();
        await SetOn(rig);

        foreach (var job in BackgroundJobCatalog.All.Where(j => j.Classification == BackgroundJobClassification.Gxp))
        {
            var ran = false;
            var result = await recorder.RunAsync(job.Name, BackgroundJobTrigger.Schedule, null, null,
                _ => { ran = true; return Task.FromResult<int?>(1); }, default);
            Assert.False(ran, job.Name);
            Assert.Equal(BackgroundJobOutcome.Skipped, result.Outcome);
        }

        var rows = await db.BackgroundJobRuns.ToListAsync();
        Assert.NotEmpty(rows);
        Assert.All(rows, r =>
        {
            Assert.Equal(BackgroundJobOutcome.Skipped, r.Outcome);
            Assert.Contains("maintenance", r.Error, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task WhenOn_HousekeepingJobsContinue()
    {
        var (recorder, db, rig) = CreateRecorder();
        await SetOn(rig);

        foreach (var job in BackgroundJobCatalog.All.Where(j => j.Classification == BackgroundJobClassification.Housekeeping))
        {
            var ran = false;
            var result = await recorder.RunAsync(job.Name, BackgroundJobTrigger.Schedule, null, null,
                _ => { ran = true; return Task.FromResult<int?>(2); }, default);
            Assert.True(ran, job.Name);
            Assert.Equal(BackgroundJobOutcome.Succeeded, result.Outcome);
        }
        Assert.DoesNotContain(await db.BackgroundJobRuns.ToListAsync(), r => r.Outcome == BackgroundJobOutcome.Skipped);
    }

    [Fact]
    public async Task WhenOff_GxpWorkersRunNormally()
    {
        var (recorder, _, _) = CreateRecorder();
        var ran = false;
        var result = await recorder.RunAsync("EquipmentOverdueWorker", BackgroundJobTrigger.Schedule, null, null,
            _ => { ran = true; return Task.FromResult<int?>(1); }, default);
        Assert.True(ran);
        Assert.Equal(BackgroundJobOutcome.Succeeded, result.Outcome);
    }

    // ---- status endpoint ----------------------------------------------------

    [Fact]
    public async Task StatusEndpoint_IsAnonymous_AndReturnsOnlyTheFourFields()
    {
        var method = typeof(SystemStatusController).GetMethod(nameof(SystemStatusController.Get))!;
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(typeof(SystemStatusController).GetCustomAttribute<AuthorizeAttribute>());

        var rig = CreateRig();
        await rig.Service.UpdateAsync(SystemSettingKeys.AnnouncementBanner, "Hello", Reason, 1, null, null);
        await SetOn(rig);
        var controller = new SystemStatusController(rig.Reader) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var ok = Assert.IsType<OkObjectResult>(await controller.Get(default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(new[] { "banner", "maintenance", "message", "severity" },
            data.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.True(data.GetProperty("maintenance").GetBoolean());
        Assert.Equal("Hello", data.GetProperty("banner").GetString());
        Assert.Equal("info", data.GetProperty("severity").GetString());
    }
}
