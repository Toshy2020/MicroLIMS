using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// GET api/client-settings: the few server-held values the browser needs before
// it can behave correctly (how long a bench tablet keeps unsaved drafts, which
// optional features are on). Runs through the real authentication pipeline.
[Collection("PostgresDatabaseCollection")]
public class ClientSettingsPostgresTests
{
    private const string Key = "bench.draftRetentionHours";
    private const string Password = "ClientSettings1!";
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";

    private readonly PostgresTestFixture _fixture;

    public ClientSettingsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

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
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
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
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private async Task<string> SeedAnalystAsync()
    {
        var username = "client_settings_" + Guid.NewGuid().ToString("N");
        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == RoleType.Analyst).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        arrangeDb.Users.Add(new User
        {
            FullName = "Client Settings " + username[^8..],
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

    private async Task SetStoredRetentionAsync(string? value)
    {
        await using var db = _fixture.CreateDbContext();
        var existing = await db.SystemSettings.Where(s => s.Key == Key).ToListAsync();
        db.SystemSettings.RemoveRange(existing);
        if (value is not null)
            db.SystemSettings.Add(new SystemSetting
            {
                Key = Key, Value = value, ValueType = SystemSettingValueType.Integer,
                Category = "Bench", Description = "test", CurrentVersion = 1
            });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> GetAsAsync(HttpClient client, string username)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("data").GetProperty("token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/client-settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    [PostgresFact]
    public async Task ClientSettings_ReturnsTheDefault_WhenNothingIsStored()
    {
        await SetStoredRetentionAsync(null);
        var username = await SeedAnalystAsync();

        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var data = await GetAsAsync(client, username);
            Assert.Equal(24, data.GetProperty("benchDraftRetentionHours").GetInt32());
            Assert.Equal(JsonValueKind.Array, data.GetProperty("features").ValueKind);
            // Label printing is built, so the workspace's Print labels action is offered.
            Assert.Contains("labels", data.GetProperty("features").EnumerateArray().Select(f => f.GetString()));
        }
    }

    [PostgresFact]
    public async Task ClientSettings_ReturnsTheStoredValue_WhenChanged()
    {
        var username = await SeedAnalystAsync();
        await SetStoredRetentionAsync("48");
        try
        {
            var (app, client) = await StartHostAsync();
            await using (app)
            {
                var data = await GetAsAsync(client, username);
                Assert.Equal(48, data.GetProperty("benchDraftRetentionHours").GetInt32());
            }
        }
        finally
        {
            await SetStoredRetentionAsync(null);
        }
    }

    [PostgresFact]
    public async Task ClientSettings_FallsBackToTheDefault_WhenTheStoredValueIsOutOfRange()
    {
        var username = await SeedAnalystAsync();
        await SetStoredRetentionAsync("500");
        try
        {
            var (app, client) = await StartHostAsync();
            await using (app)
            {
                var data = await GetAsAsync(client, username);
                Assert.Equal(24, data.GetProperty("benchDraftRetentionHours").GetInt32());
            }
        }
        finally
        {
            await SetStoredRetentionAsync(null);
        }
    }

    [PostgresFact]
    public async Task ClientSettings_WithoutToken_Returns401()
    {
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var response = await client.GetAsync("/api/client-settings");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
