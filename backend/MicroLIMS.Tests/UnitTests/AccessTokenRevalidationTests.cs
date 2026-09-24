using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Extensions;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// An access token only proves who the user was when it was issued. These
// tests pin that a disabled, locked, re-roled or re-passworded account
// loses API access on its next request, not when the token expires.
public class AccessTokenRevalidationTests
{
    private const int UserId = 7;

    private static readonly JwtSettings Settings = new(
        JwtTokenServiceTests.TestSigningKey, "MicroLIMS", "MicroLIMS.Client", TimeSpan.FromMinutes(15));

    private static MicroLimsDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(name).Options);

    private static void SeedUser(string dbName, RoleType roleType = RoleType.SystemAdministrator)
    {
        using var db = NewDb(dbName);
        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" });
        db.Users.Add(new User
        {
            Id = UserId,
            FullName = "Token Holder",
            Username = "holder",
            PasswordHash = "not-used",
            RoleId = roleType == RoleType.SystemAdministrator ? 1 : 4,
            IsActive = true
        });
        db.SaveChanges();
    }

    private static void UpdateUser(string dbName, Action<User> change)
    {
        using var db = NewDb(dbName);
        change(db.Users.Single(u => u.Id == UserId));
        db.SaveChanges();
    }

    private static string IssueToken(string role = "SystemAdministrator") =>
        new JwtTokenService(Settings.Key, Settings.Issuer, Settings.Audience, Settings.AccessTokenLifetime)
            .IssueToken(UserId.ToString(), role);

    private static ClaimsPrincipal Principal(string role = "SystemAdministrator", long? issuedAt = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId.ToString()),
            new(ClaimTypes.Role, role)
        };
        claims.Add(new Claim(JwtRegisteredClaimNames.Iat,
            (issuedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static async Task<string?> RejectionReason(string dbName, ClaimsPrincipal principal)
    {
        await using var db = NewDb(dbName);
        return await new AccessTokenRevalidator(db).GetRejectionReasonAsync(principal);
    }

    // ---- AccessTokenRevalidator rules ----

    [Fact]
    public async Task ActiveUser_WithCurrentRole_IsAccepted()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);

        Assert.Null(await RejectionReason(dbName, Principal()));
    }

    [Fact]
    public async Task DisabledUser_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        UpdateUser(dbName, u => u.IsActive = false);

        Assert.Equal("Account is disabled.", await RejectionReason(dbName, Principal()));
    }

    [Fact]
    public async Task LockedUser_IsRejected_UntilTheLockExpires()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);

        UpdateUser(dbName, u => u.LockedUntil = DateTime.UtcNow.AddMinutes(15));
        Assert.Equal("Account is locked.", await RejectionReason(dbName, Principal()));

        UpdateUser(dbName, u => u.LockedUntil = DateTime.UtcNow.AddMinutes(-1));
        Assert.Null(await RejectionReason(dbName, Principal()));
    }

    [Fact]
    public async Task DeletedUser_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        using (var db = NewDb(dbName))
        {
            db.Users.Remove(db.Users.Single(u => u.Id == UserId));
            db.SaveChanges();
        }

        Assert.Equal("Account no longer exists.", await RejectionReason(dbName, Principal()));
    }

    [Fact]
    public async Task TokenWithAnOldRole_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        UpdateUser(dbName, u => u.RoleId = 4); // demoted to Analyst

        Assert.Equal("Role has changed since the token was issued.",
            await RejectionReason(dbName, Principal(role: "SystemAdministrator")));
        Assert.Null(await RejectionReason(dbName, Principal(role: "Analyst")));
    }

    [Fact]
    public async Task TokenIssuedBeforeThePasswordChange_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        UpdateUser(dbName, u => u.PasswordChangedAt = DateTime.UtcNow.AddMinutes(-1));

        Assert.Equal("Password was changed after the token was issued.",
            await RejectionReason(dbName, Principal(issuedAt: issuedAt)));
    }

    [Fact]
    public async Task TokenIssuedAfterThePasswordChange_IsAccepted_EvenInTheSameSecond()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var changedAt = DateTime.UtcNow.AddMinutes(-1);
        UpdateUser(dbName, u => u.PasswordChangedAt = changedAt);

        var sameSecond = new DateTimeOffset(changedAt).ToUnixTimeSeconds();
        Assert.Null(await RejectionReason(dbName, Principal(issuedAt: sameSecond)));
        Assert.Null(await RejectionReason(dbName, Principal(issuedAt: sameSecond + 30)));
    }

    [Fact]
    public async Task TokenWithoutAnIssueTime_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Role, "SystemAdministrator")
        }, "test"));

        Assert.Equal("Token carries no issue time.", await RejectionReason(dbName, principal));
    }

    // ---- End to end through the real JWT bearer setup ----

    // Hosts AddMicroLimsJwtAuthentication - the method Program.cs calls -
    // in a test server, with an in-memory database, and real signed tokens
    // going through real [Authorize] role checks.
    private static async Task<(WebApplication App, HttpClient Client)> StartApiAsync(string dbName)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<MicroLimsDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddMicroLimsJwtAuthentication(Settings);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/any-user", () => "ok").RequireAuthorization();
        app.MapGet("/admin-only", () => "ok")
            .RequireAuthorization(p => p.RequireRole("SystemAdministrator"));

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<HttpStatusCode> Get(HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task EndToEnd_ValidTokenStopsWorking_AsSoonAsTheAccountIsDisabled()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;
        var token = IssueToken();

        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", token));

        UpdateUser(dbName, u => u.IsActive = false);

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", token));
    }

    [Fact]
    public async Task EndToEnd_ValidTokenStopsWorking_AsSoonAsTheAccountIsLocked()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;
        var token = IssueToken();

        UpdateUser(dbName, u => u.LockedUntil = DateTime.UtcNow.AddMinutes(15));

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", token));
    }

    [Fact]
    public async Task EndToEnd_DemotedAdministrator_LosesAdminAccessImmediately()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;
        var adminToken = IssueToken("SystemAdministrator");

        Assert.Equal(HttpStatusCode.OK, await Get(client, "/admin-only", adminToken));

        UpdateUser(dbName, u => u.RoleId = 4); // now an Analyst

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/admin-only", adminToken));
        // A token for the current role (what /auth/refresh issues) works.
        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", IssueToken("Analyst")));
        Assert.Equal(HttpStatusCode.Forbidden, await Get(client, "/admin-only", IssueToken("Analyst")));
    }

    [Fact]
    public async Task EndToEnd_TokenIssuedBeforeAPasswordChange_StopsWorking()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;
        var token = IssueToken();

        UpdateUser(dbName, u => u.PasswordChangedAt = DateTime.UtcNow.AddSeconds(2));

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", token));
    }

    [Fact]
    public async Task EndToEnd_TokenWithoutAnIssueTime_IsRefused()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;

        // Correctly signed, but shaped like a token issued before this check
        // existed: no iat claim.
        var legacy = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Settings.Issuer, Settings.Audience,
            new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()), new Claim(ClaimTypes.Role, "SystemAdministrator") },
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Key)), SecurityAlgorithms.HmacSha256)));

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", legacy));
    }
}
