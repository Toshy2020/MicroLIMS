using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
using MicroLIMS.Application.Security;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class WorkingHoursPostgresTests
{
    private const string Password = "WorkingHours-123!";
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private static readonly DateTimeOffset FixedUtc = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public WorkingHoursPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static ILabClock CreateClock() =>
        new LabClock(new FixedTimeProvider(FixedUtc), LabClock.ResolveTimeZone("Africa/Cairo"));

    private static WorkingHoursController CreateController(MicroLimsDbContext db, int userId)
    {
        db.CurrentUserId = userId;
        var service = new WorkingHoursService(db, CreateClock(), new PermissionService(db));
        return new WorkingHoursController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "Test"))
                }
            }
        };
    }

    private async Task<(int UserId, string Username)> SeedUserAsync(RoleType roleType)
    {
        var tag = Guid.NewGuid().ToString("N");
        var username = $"wh_{roleType}_{tag[..10]}".ToLowerInvariant();

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"WH {roleType} {tag[..6]}",
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = FixedUtc.UtcDateTime
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username);
    }

    private async Task<(WebApplication App, HttpClient Client)> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtSigningKey,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["Mfa:ActiveKeyId"] = "test-k1",
            ["Mfa:EncryptionKeys:test-k1"] = FixedKeyBase64,
            ["Lab:TimeZoneId"] = "Africa/Cairo",
            ["ConnectionStrings:DefaultConnection"] = _fixture.TestConnectionString
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(_fixture.TestConnectionString));
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(WorkingHoursController).Assembly)
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

    private static UpdateWorkingHoursRequest StandardCairoWeek() => new(
    [
        new(DayOfWeek.Sunday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Monday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Tuesday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Wednesday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Thursday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Friday, false, 0, 0),
        new(DayOfWeek.Saturday, false, 0, 0),
    ]);

    [PostgresFact]
    public async Task Put_ReplacesAllSeven_Audited()
    {
        var (headId, _) = await SeedUserAsync(RoleType.SectionHead);

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await arrangeDb.Database.ExecuteSqlRawAsync(@"DELETE FROM ""LabWorkingHours"";");
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ctrl = CreateController(actDb, headId);
            var ok = Assert.IsType<OkObjectResult>(await ctrl.Update(StandardCairoWeek()));
            var env = Assert.IsType<ApiResponse<WorkingHoursDto>>(ok.Value);
            Assert.True(env.Data!.IsConfigured);
            Assert.Equal(7, env.Data.Days.Count);
        }

        var replacement = new UpdateWorkingHoursRequest(
        [
            new(DayOfWeek.Sunday, true, 9 * 60, 17 * 60),
            new(DayOfWeek.Monday, true, 9 * 60, 17 * 60),
            new(DayOfWeek.Tuesday, true, 9 * 60, 17 * 60),
            new(DayOfWeek.Wednesday, false, 0, 0),
            new(DayOfWeek.Thursday, false, 0, 0),
            new(DayOfWeek.Friday, false, 0, 0),
            new(DayOfWeek.Saturday, true, 10 * 60, 14 * 60),
        ]);

        await using (var actDb2 = _fixture.CreateDbContext())
        {
            var ctrl = CreateController(actDb2, headId);
            await ctrl.Update(replacement);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var rows = await assertDb.LabWorkingHours.AsNoTracking()
                .OrderBy(w => w.DayOfWeek)
                .ToListAsync();
            Assert.Equal(7, rows.Count);
            Assert.Equal((true, 9 * 60, 17 * 60), (rows[0].IsWorkingDay, rows[0].StartMinute, rows[0].EndMinute));
            Assert.Equal((false, 0, 0), (rows[3].IsWorkingDay, rows[3].StartMinute, rows[3].EndMinute));
            Assert.Equal((true, 10 * 60, 14 * 60), (rows[6].IsWorkingDay, rows[6].StartMinute, rows[6].EndMinute));

            var audits = await assertDb.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(LabWorkingHours) && a.UserId == headId)
                .ToListAsync();
            Assert.Equal(7, audits.Count(a => a.Action == "Create"));
            Assert.True(audits.Count(a => a.Action == "Update") >= 5);
            Assert.All(audits.Where(a => a.Action == "Update"), a =>
            {
                Assert.False(string.IsNullOrWhiteSpace(a.PreviousValue));
                Assert.False(string.IsNullOrWhiteSpace(a.NewValue));
            });
        }

        await using (var readDb = _fixture.CreateDbContext())
        {
            var ctrl = CreateController(readDb, headId);
            var ok = Assert.IsType<OkObjectResult>(await ctrl.Get());
            var env = Assert.IsType<ApiResponse<WorkingHoursDto>>(ok.Value);
            Assert.Equal(CreateClock().LabTimeZone.Id, env.Data!.TimeZoneId);
            Assert.True(env.Data.IsConfigured);
            Assert.Equal(7, env.Data.Days.Count);
        }
    }

    [PostgresFact]
    public async Task Put_StartAfterEnd_FieldError()
    {
        var (headId, _) = await SeedUserAsync(RoleType.SectionHead);

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await arrangeDb.Database.ExecuteSqlRawAsync(@"DELETE FROM ""LabWorkingHours"";");
        }

        var invalid = new UpdateWorkingHoursRequest(
        [
            new(DayOfWeek.Sunday, true, 8 * 60, 16 * 60),
            new(DayOfWeek.Monday, true, 8 * 60, 16 * 60),
            new(DayOfWeek.Tuesday, true, 16 * 60, 8 * 60),   // row 2: start > end
            new(DayOfWeek.Wednesday, true, 9 * 60, 9 * 60),  // row 3: start == end
            new(DayOfWeek.Thursday, true, 8 * 60, 16 * 60),
            new(DayOfWeek.Friday, false, 16 * 60, 8 * 60),   // non-working: allowed
            new(DayOfWeek.Saturday, false, 0, 0),
        ]);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ctrl = CreateController(actDb, headId);
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => ctrl.Update(invalid));

            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "endMinute");
            Assert.Contains(ex.FieldErrors, e => e.Row == 3 && e.Field == "endMinute");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 5);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(0, await assertDb.LabWorkingHours.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task Put_WithoutPermission_403()
    {
        var (_, analystUsername) = await SeedUserAsync(RoleType.Analyst);
        var (_, headUsername) = await SeedUserAsync(RoleType.SectionHead);

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await arrangeDb.Database.ExecuteSqlRawAsync(@"DELETE FROM ""LabWorkingHours"";");
        }

        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var analystLogin = await client.PostAsJsonAsync("/api/auth/login", new { username = analystUsername, password = Password });
        Assert.Equal(HttpStatusCode.OK, analystLogin.StatusCode);
        using var analystJson = JsonDocument.Parse(await analystLogin.Content.ReadAsStringAsync());
        var analystToken = analystJson.RootElement.GetProperty("data").GetProperty("token").GetString();

        // Signed-in user can GET working hours
        using (var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/working-hours"))
        {
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", analystToken);
            var getResp = await client.SendAsync(getReq);
            Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        }

        // Analyst without Notifications.Configure receives 403 on PUT
        using (var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/working-hours"))
        {
            putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", analystToken);
            putReq.Content = JsonContent.Create(StandardCairoWeek());
            var putResp = await client.SendAsync(putReq);
            Assert.Equal(HttpStatusCode.Forbidden, putResp.StatusCode);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(0, await assertDb.LabWorkingHours.AsNoTracking().CountAsync());
        }

        // SectionHead with Notifications.Configure can PUT
        var headLogin = await client.PostAsJsonAsync("/api/auth/login", new { username = headUsername, password = Password });
        Assert.Equal(HttpStatusCode.OK, headLogin.StatusCode);
        using var headJson = JsonDocument.Parse(await headLogin.Content.ReadAsStringAsync());
        var headToken = headJson.RootElement.GetProperty("data").GetProperty("token").GetString();

        using (var headPut = new HttpRequestMessage(HttpMethod.Put, "/api/working-hours"))
        {
            headPut.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headToken);
            headPut.Content = JsonContent.Create(StandardCairoWeek());
            var headResp = await client.SendAsync(headPut);
            Assert.Equal(HttpStatusCode.OK, headResp.StatusCode);
        }

        await using var finalDb = _fixture.CreateDbContext();
        Assert.Equal(7, await finalDb.LabWorkingHours.AsNoTracking().CountAsync());
    }
}
