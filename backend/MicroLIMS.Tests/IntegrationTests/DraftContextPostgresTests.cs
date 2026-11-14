using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Bench tablets send X-Draft-Started-At when a saved entry began life as a
// local draft. The audit rows of that request then say so in SourceContext.
// The audit Timestamp stays the server clock: the device time is a statement,
// not a measurement, and is stored as stated.
[Collection("PostgresDatabaseCollection")]
public class DraftContextPostgresTests
{
    private const string Password = "DraftContext1!";
    private const string Header = "X-Draft-Started-At";
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";

    // Deliberately unlike every header value used below.
    private static readonly DateTimeOffset ServerNow = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public DraftContextPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<(WebApplication App, HttpClient Client)> StartHostAsync()
    {
        var cs = _fixture.TestConnectionString;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtSigningKey,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["ConnectionStrings:DefaultConnection"] = cs
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(cs));
        builder.Services.AddApplicationServices(builder.Configuration);
        // After AddApplicationServices, which registers the system clock; the last registration wins.
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(ServerNow));
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddMicroLimsCors(new[] { "http://localhost:5173" });
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(AuthenticationController).Assembly)
            .AddJsonOptions(o => ApiJsonOptions.Configure(o.JsonSerializerOptions));

        var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseRouting();
        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMicroLimsAuditPipeline();
        app.UseRateLimiter();
        app.MapControllers();

        // A regulated write through the real pipeline: creates an audited row.
        app.MapPost("/probe/organism", [Authorize] async (string name, MicroLimsDbContext db) =>
        {
            var organism = new Organism { ScientificName = name };
            db.Organisms.Add(organism);
            await db.SaveChangesAsync();
            return Results.Ok(organism.Id);
        });

        // An audit event written explicitly with its own SourceContext.
        app.MapPost("/probe/explicit", [Authorize] async (string marker, IAuditEventService audit) =>
        {
            var log = await audit.RecordUserEventAsync("DRAFT_PROBE", AuditActionCategory.Configuration,
                "DraftProbe", sourceContext: marker, entityId: marker);
            return Results.Ok(log.Id);
        });

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private async Task<string> SeedAnalystAsync()
    {
        var username = "draft_ctx_" + Guid.NewGuid().ToString("N");
        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == RoleType.Analyst).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        arrangeDb.Users.Add(new User
        {
            FullName = "Draft Context " + username[^8..],
            Username = username,
            Email = username + "@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await arrangeDb.SaveChangesAsync();
        return username;
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string url, string? header)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (header is not null)
            request.Headers.TryAddWithoutValidation(Header, header);
        return await client.SendAsync(request);
    }

    private async Task<AuditLog> PostOrganismAndGetAuditAsync(string? header)
    {
        var username = await SeedAnalystAsync();
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var token = await LoginAsync(client, username);
            var name = "Draft organism " + Guid.NewGuid().ToString("N")[..10];
            var response = await PostAsync(client, token, "/probe/organism?name=" + name, header);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var id = await response.Content.ReadFromJsonAsync<int>();

            await using var assertDb = _fixture.CreateDbContext();
            return await assertDb.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.EntityName == nameof(Organism) && a.EntityId == id.ToString() && a.Action == "Create");
        }
    }

    [PostgresFact]
    public async Task HeaderPresent_AuditRowsOfThatRequest_CarrySourceContext_TimestampIsServerClock()
    {
        // 09:15 at +02:00 is 07:15 UTC; the server clock is 2031-03-04 05:06:07.
        var audit = await PostOrganismAndGetAuditAsync("2026-11-13T09:15:42+02:00");

        Assert.Equal("Submitted from a device draft started at 2026-11-13 07:15 UTC (device clock, not verified)",
            audit.SourceContext);
        Assert.Equal(ServerNow.UtcDateTime, audit.Timestamp);
        Assert.NotEqual(0, audit.UserId);
    }

    [PostgresFact]
    public async Task HeaderInFuture_StoredVerbatim_TimestampUnaffected()
    {
        // Ten years after the server clock: stored as stated, not corrected, not rejected.
        var audit = await PostOrganismAndGetAuditAsync("2041-03-04T05:06:00Z");

        Assert.Equal("Submitted from a device draft started at 2041-03-04 05:06 UTC (device clock, not verified)",
            audit.SourceContext);
        Assert.Equal(ServerNow.UtcDateTime, audit.Timestamp);
    }

    [PostgresFact]
    public async Task HeaderVeryOld_StoredVerbatim()
    {
        var audit = await PostOrganismAndGetAuditAsync("1999-12-31T23:59:59-05:00");

        Assert.Equal("Submitted from a device draft started at 2000-01-01 04:59 UTC (device clock, not verified)",
            audit.SourceContext);
    }

    [PostgresTheory]
    [InlineData("not-a-date")]
    [InlineData("2026-11-13")]
    [InlineData("2026-11-13T09:15:42")]            // no offset: the device did not say which clock
    [InlineData("   ")]
    public async Task HeaderMalformed_Ignored_RequestSucceeds(string header)
    {
        var audit = await PostOrganismAndGetAuditAsync(header);

        Assert.Null(audit.SourceContext);
    }

    [PostgresFact]
    public async Task HeaderLongerThan64Chars_Ignored()
    {
        // A valid instant padded with fractional-second zeros to 65 characters.
        var prefix = "2026-11-13T09:15:42.";
        var padded = prefix + new string('0', 65 - prefix.Length - 1) + "Z";
        Assert.Equal(65, padded.Length);
        Assert.True(DateTimeOffset.TryParse(padded, out _));

        var audit = await PostOrganismAndGetAuditAsync(padded);

        Assert.Null(audit.SourceContext);
    }

    [PostgresFact]
    public async Task Header64Chars_IsAccepted()
    {
        var prefix = "2026-11-13T09:15:42.";
        var padded = prefix + new string('0', 64 - prefix.Length - 1) + "Z";
        Assert.Equal(64, padded.Length);

        var audit = await PostOrganismAndGetAuditAsync(padded);

        Assert.Equal("Submitted from a device draft started at 2026-11-13 09:15 UTC (device clock, not verified)",
            audit.SourceContext);
    }

    [PostgresFact]
    public async Task NoHeader_SourceContextNull()
    {
        var audit = await PostOrganismAndGetAuditAsync(null);

        Assert.Null(audit.SourceContext);
        Assert.Equal(ServerNow.UtcDateTime, audit.Timestamp);
    }

    [PostgresFact]
    public async Task ExplicitSourceContextFromAuditEventService_IsNotOverwritten()
    {
        var username = await SeedAnalystAsync();
        var marker = "explicit-" + Guid.NewGuid().ToString("N")[..10];
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var token = await LoginAsync(client, username);
            var response = await PostAsync(client, token, "/probe/explicit?marker=" + marker, "2026-11-13T09:15:42Z");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var row = await assertDb.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityId == marker);
        Assert.Equal(marker, row.SourceContext);
    }

    [PostgresFact]
    public async Task PreflightAllowsTheHeader()
    {
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/client-settings");
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5173");
            request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
            request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,x-draft-started-at");

            var response = await client.SendAsync(request);

            Assert.True(response.IsSuccessStatusCode, response.StatusCode.ToString());
            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowed));
            Assert.Contains("x-draft-started-at", string.Join(",", allowed!), StringComparison.OrdinalIgnoreCase);
        }
    }
}
