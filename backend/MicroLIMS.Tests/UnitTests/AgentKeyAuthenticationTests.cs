using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Test-host only: the real ingest endpoint arrives in a later task. Public and
// top level so MVC discovers it from the test assembly.
[ApiController]
[Route("api/test-agent-probe")]
[Authorize(AuthenticationSchemes = AgentKeyDefaults.Scheme, Policy = AgentKeyDefaults.Policy)]
public class AgentProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        agentId = User.FindFirst(AgentKeyDefaults.AgentIdClaim)?.Value,
        agentCode = User.FindFirst(AgentKeyDefaults.AgentCodeClaim)?.Value,
        equipmentId = User.FindFirst(AgentKeyDefaults.EquipmentIdClaim)?.Value,
        sectionId = User.FindFirst(AgentKeyDefaults.SectionIdClaim)?.Value,
        hasNameIdentifier = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) is not null
    });

    [HttpGet("limited")]
    [EnableRateLimiting(RateLimitingPolicies.Agent)]
    public IActionResult Limited() => Ok();
}

// The edge agent authenticates with a per-agent key, never a user login. These
// pin the scheme, its policy, and that the key opens nothing else.
public class AgentKeyAuthenticationTests
{
    private const string KeyA = "lims_agent_AGT-HPLC-01_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string KeyB = "lims_agent_AGT-HPLC-02_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static void SeedAgents(MicroLimsDbContext db, bool withEquipment = true)
    {
        if (withEquipment)
            db.Equipment.AddRange(
                new Equipment { Id = 11, Name = "HPLC 1", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = 3 },
                new Equipment { Id = 12, Name = "HPLC 2", Code = "HPLC-02", Type = EquipmentType.Hplc, SectionId = 3 });
        db.InstrumentAgents.AddRange(
            new InstrumentAgent { Id = 1, Code = "AGT-HPLC-01", Name = "PC 1", EquipmentId = 11, SectionId = 3, TimeZoneId = "UTC", KeyHash = InstrumentAgentService.HashKey(KeyA), KeyPrefix = "aaaaaaaa", IsActive = true },
            new InstrumentAgent { Id = 2, Code = "AGT-HPLC-02", Name = "PC 2", EquipmentId = 12, SectionId = 3, TimeZoneId = "UTC", KeyHash = InstrumentAgentService.HashKey(KeyB), KeyPrefix = "bbbbbbbb", IsActive = true });
        db.SaveChanges();
    }

    private static MicroLimsDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(name).Options);

    // Hosts the extensions Program.cs calls, with MVC controllers from the API
    // assembly (so the real HplcWorkspaceController is routable) and the probe.
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(
        Action<IServiceCollection> database, TimeProvider? time = null, params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        database(builder.Services);
        if (time is not null) builder.Services.AddSingleton(time);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ISecurityRequestContext, HttpSecurityRequestContext>();
        builder.Services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        builder.Services.AddMicroLimsJwtAuthentication(new JwtSettings(
            JwtTokenServiceTests.TestSigningKey, "MicroLIMS", "MicroLIMS.Client", TimeSpan.FromMinutes(15)));
        builder.Services.AddMicroLimsAgentAuthentication();
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AgentProbeController).Assembly)
            .AddApplicationPart(typeof(HplcWorkspaceController).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static Action<IServiceCollection> InMemory(string dbName) =>
        s => s.AddMicroLimsDbContext(o => o.UseInMemoryDatabase(dbName));

    private static async Task<HttpResponseMessage> Get(HttpClient client, string path, string? agentKey = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (agentKey is not null) request.Headers.TryAddWithoutValidation(AgentKeyDefaults.HeaderName, agentKey);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await client.SendAsync(request);
    }

    private static List<SecurityAuditEvent> Failures(string dbName)
    {
        using var db = NewDb(dbName);
        return db.SecurityAuditEvents.Where(e => e.EventCode == SecurityEventCodes.AgentAuthFailed).ToList();
    }

    [Fact]
    public async Task ValidKey_Returns200_WithAgentClaims_AndNoNameIdentifier()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        var response = await Get(client, "/api/test-agent-probe", KeyA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"agentId\":\"1\"", body);
        Assert.Contains("\"agentCode\":\"AGT-HPLC-01\"", body);
        Assert.Contains("\"equipmentId\":\"11\"", body);
        Assert.Contains("\"sectionId\":\"3\"", body);
        // AuditMiddleware reads NameIdentifier as a user id.
        Assert.Contains("\"hasNameIdentifier\":false", body);
        Assert.Empty(Failures(dbName));
    }

    [Fact]
    public async Task MissingOrEmptyKey_Is401_AndNotAuditedAsAFailedKey()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/api/test-agent-probe")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/api/test-agent-probe", "")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/api/test-agent-probe", "   ")).StatusCode);
        Assert.Empty(Failures(dbName));
    }

    [Theory]
    [InlineData("lims_agent_AGT-HPLC-01_notTheRealKeyXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX", "Unknown")]
    [InlineData("not-an-agent-key", "Malformed")]
    public async Task UnknownOrMalformedKey_Is401_AndAudited_WithoutKeyMaterial(string presented, string reasonWord)
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/api/test-agent-probe", presented)).StatusCode);

        var evt = Assert.Single(Failures(dbName));
        Assert.Equal(SecurityEventOutcome.Failure, evt.Outcome);
        Assert.Contains(reasonWord, evt.Reason);
        var stored = string.Join('|', evt.Reason, evt.Metadata, evt.TargetUsername, evt.RequestPath, evt.UserAgent);
        Assert.DoesNotContain(presented, stored);
        Assert.DoesNotContain(InstrumentAgentService.HashKey(presented), stored);
    }

    [Fact]
    public async Task RevokedAgent_Is401_AndAuditedWithItsCode_EvenThoughItKeepsItsHash()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName))
        {
            SeedAgents(db);
            var agent = db.InstrumentAgents.Single(a => a.Id == 1);
            agent.IsActive = false;
            agent.RevokedAt = T0.UtcDateTime;
            db.SaveChanges();
        }
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);
        // The other agent is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe", KeyB)).StatusCode);

        var evt = Assert.Single(Failures(dbName));
        Assert.Contains("AGT-HPLC-01", evt.Metadata);
        Assert.Contains("revoked", evt.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(KeyA, string.Join('|', evt.Reason, evt.Metadata));
    }

    [Fact]
    public async Task AgentWhoseEquipmentIsGone_Is401()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db, withEquipment: false);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);

        var evt = Assert.Single(Failures(dbName));
        Assert.Contains("equipment", evt.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HumanJwt_OnTheAgentProbe_IsRefused()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;
        var token = new JwtTokenService(JwtTokenServiceTests.TestSigningKey, "MicroLIMS", "MicroLIMS.Client", TimeSpan.FromMinutes(15))
            .IssueToken("7", "SystemAdministrator");

        var status = (await Get(client, "/api/test-agent-probe", bearer: token)).StatusCode;

        Assert.True(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"was {status}");
    }

    [Fact]
    public async Task AgentKey_OnAHumanEndpoint_IsRefused()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        // HplcWorkspaceController is [Authorize] on the default (JWT) scheme.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Get(client, "/api/hplc-workspace/instruments", KeyA)).StatusCode);
    }

    // The in-memory provider cannot run the set-based LastSeenAt update, so
    // this proves a failed update is swallowed rather than failing the request.
    [Fact]
    public async Task ValidKey_StillAuthenticates_WhenTheLastSeenUpdateFails()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName));
        await using var _ = app;

        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);
    }

    [PostgresFact]
    public async Task LastSeenAt_IsUpdatedAtMostOncePerMinute_AndWritesNoGxpAuditRow()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            int sectionId;
            await using (var db = ScratchPostgresDatabase.NewContext(cs))
            {
                await db.Database.MigrateAsync();
                sectionId = await db.DocumentSections.Select(s => s.Id).FirstAsync();
                var eq = new Equipment { Name = "HPLC 1", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = sectionId };
                db.Equipment.Add(eq);
                await db.SaveChangesAsync();
                db.InstrumentAgents.Add(new InstrumentAgent
                {
                    Code = "AGT-HPLC-01", Name = "PC 1", EquipmentId = eq.Id, SectionId = sectionId, TimeZoneId = "UTC",
                    KeyHash = InstrumentAgentService.HashKey(KeyA), KeyPrefix = "aaaaaaaa", IsActive = true
                });
                await db.SaveChangesAsync();
            }

            var time = new HplcRunServiceTests.FakeTimeProvider(T0);
            var (app, client) = await StartAsync(s => s.AddMicroLimsDbContext(o => o.UseNpgsql(cs)), time);
            await using var _ = app;

            async Task<DateTime?> LastSeen()
            {
                await using var db = ScratchPostgresDatabase.NewContext(cs);
                return await db.InstrumentAgents.Select(a => a.LastSeenAt).SingleAsync();
            }
            async Task<int> AuditRows()
            {
                await using var db = ScratchPostgresDatabase.NewContext(cs);
                return await db.AuditLogs.CountAsync(a => a.EntityName == nameof(InstrumentAgent));
            }
            var auditBefore = await AuditRows();

            Assert.Null(await LastSeen());

            Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);
            Assert.Equal(T0.UtcDateTime, await LastSeen());

            time.SetUtcNow(T0.AddSeconds(59));
            Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);
            Assert.Equal(T0.UtcDateTime, await LastSeen());

            time.SetUtcNow(T0.AddSeconds(61));
            Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe", KeyA)).StatusCode);
            Assert.Equal(T0.AddSeconds(61).UtcDateTime, await LastSeen());

            // Heartbeats are operational, not GxP records.
            Assert.Equal(auditBefore, await AuditRows());
        });
    }

    [Fact]
    public async Task RateLimit_IsPerAgent_NotPerAddress()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = NewDb(dbName)) SeedAgents(db);
        var (app, client) = await StartAsync(InMemory(dbName), null, ("RateLimiting:Agent:PermitLimit", "2"));
        await using var _ = app;

        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe/limited", KeyA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe/limited", KeyA)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Get(client, "/api/test-agent-probe/limited", KeyA)).StatusCode);

        // Same address, different agent: its own allowance.
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/test-agent-probe/limited", KeyB)).StatusCode);
    }
}
