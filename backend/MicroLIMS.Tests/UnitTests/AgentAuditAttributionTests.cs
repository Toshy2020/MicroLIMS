using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Test-host only: an agent-authenticated write, so the audit row is produced by
// the real pipeline (AgentKey scheme -> AuditMiddleware -> SaveChanges).
[ApiController]
[Route("api/test-agent-audit-probe")]
[Authorize(AuthenticationSchemes = AgentKeyDefaults.Scheme, Policy = AgentKeyDefaults.Policy)]
public class AgentAuditProbeController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromServices] MicroLimsDbContext db)
    {
        db.Equipment.Add(new Equipment { Name = "Written by agent", Code = "AGENT-WROTE", Type = EquipmentType.Hplc, SectionId = 3 });
        await db.SaveChangesAsync();
        return Ok();
    }
}

// An instrument agent has no user id. Its audit rows must say so honestly:
// no user, a System actor, and the agent's code - never "user 0".
public class AgentAuditAttributionTests
{
    private const string Key = "lims_agent_AGT-HPLC-01_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static MicroLimsDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(name).Options);

    // One create, one update and one delete, so every generic capture path is covered.
    private static void CreateUpdateDelete(MicroLimsDbContext db)
    {
        var created = new Equipment { Id = 1, Name = "A", Code = "A", Type = EquipmentType.Hplc, SectionId = 3 };
        var doomed = new Equipment { Id = 2, Name = "B", Code = "B", Type = EquipmentType.Hplc, SectionId = 3 };
        db.Equipment.AddRange(created, doomed);
        db.SaveChanges();
        created.Name = "A2";
        db.Equipment.Remove(doomed);
        db.SaveChanges();
    }

    [Fact]
    public void AgentCode_WithoutUser_AttributesEveryGenericRowToTheAgent()
    {
        using var db = NewDb(Guid.NewGuid().ToString());
        db.CurrentAgentCode = "HPLC1";

        CreateUpdateDelete(db);

        var logs = db.AuditLogs.ToList();
        Assert.Equal(4, logs.Count); // 2 creates, 1 update, 1 delete
        Assert.All(logs, l =>
        {
            Assert.Null(l.UserId);
            Assert.Equal(ActorType.System, l.ActorType);
            Assert.Equal("InstrumentAgent:HPLC1", l.SystemProcessName);
        });
        Assert.DoesNotContain(logs, l => l.UserId == 0);
    }

    [Fact]
    public void NeitherSet_BehaviourIsUnchanged()
    {
        using var db = NewDb(Guid.NewGuid().ToString());

        CreateUpdateDelete(db);

        Assert.All(db.AuditLogs.ToList(), l =>
        {
            Assert.Equal(0, l.UserId);
            Assert.Null(l.ActorType);
            Assert.Null(l.SystemProcessName);
        });
    }

    [Fact]
    public void UserAndAgentCode_BothSet_UserWins_AndAgentCodeIsIgnored()
    {
        using var db = NewDb(Guid.NewGuid().ToString());
        db.CurrentUserId = 7;
        db.CurrentAgentCode = "HPLC1";

        CreateUpdateDelete(db);

        Assert.All(db.AuditLogs.ToList(), l =>
        {
            Assert.Equal(7, l.UserId);
            Assert.Null(l.ActorType);
            Assert.Null(l.SystemProcessName);
        });
    }

    // The agent's identity reaches the DbContext through the real middleware,
    // not just through a test setting the property.
    [Fact]
    public async Task AgentAuthenticatedRequest_WritesAuditRow_AttributedToTheAgent()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = NewDb(dbName))
        {
            seed.Equipment.Add(new Equipment { Id = 11, Name = "HPLC 1", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = 3 });
            seed.InstrumentAgents.Add(new InstrumentAgent
            {
                Id = 1, Code = "AGT-HPLC-01", Name = "PC 1", EquipmentId = 11, SectionId = 3, TimeZoneId = "UTC",
                KeyHash = InstrumentAgentService.HashKey(Key), KeyPrefix = "aaaaaaaa", IsActive = true
            });
            seed.SaveChanges();
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(Array.Empty<KeyValuePair<string, string?>>());
        builder.Services.AddMicroLimsDbContext(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ISecurityRequestContext, HttpSecurityRequestContext>();
        builder.Services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        builder.Services.AddMicroLimsJwtAuthentication(new JwtSettings(
            JwtTokenServiceTests.TestSigningKey, "MicroLIMS", "MicroLIMS.Client", TimeSpan.FromMinutes(15)));
        builder.Services.AddMicroLimsAgentAuthentication();
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AgentAuditProbeController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMicroLimsAuditPipeline(); // same order as Program.cs
        app.MapControllers();
        await app.StartAsync();
        var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-agent-audit-probe");
        request.Headers.TryAddWithoutValidation(AgentKeyDefaults.HeaderName, Key);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var db = NewDb(dbName);
        var log = db.AuditLogs.Single(l => l.EntityName == nameof(Equipment) && l.NewValue!.Contains("AGENT-WROTE"));
        Assert.Null(log.UserId);
        Assert.Equal(ActorType.System, log.ActorType);
        Assert.Equal("InstrumentAgent:AGT-HPLC-01", log.SystemProcessName);
    }
}
