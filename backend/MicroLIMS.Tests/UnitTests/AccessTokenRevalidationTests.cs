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
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Persistence.Seed;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// An access token only proves who the user was when it was issued. These
// tests pin that a disabled, locked, re-roled or re-passworded account, or
// one whose role's permissions were edited, loses API access on its next
// request, not when the token expires.
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
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" },
            // Same base type as role 4: the role-type check cannot tell them apart.
            new Role { Id = 5, Type = RoleType.Analyst, Name = "Analyst Lead" });
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

    // What login and refresh issue: the role id and permission version the
    // user holds at that moment. Role 1 is the administrator, 4 the analyst.
    private static string IssueToken(string role = "SystemAdministrator", int roleId = 1, int permissionVersion = 1) =>
        new JwtTokenService(Settings.Key, Settings.Issuer, Settings.Audience, Settings.AccessTokenLifetime)
            .IssueToken(UserId.ToString(), role, roleId: roleId, permissionVersion: permissionVersion);

    private static ClaimsPrincipal Principal(
        string role = "SystemAdministrator", long? issuedAt = null,
        string? roleId = "1", string? permissionVersion = "1")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId.ToString()),
            new(ClaimTypes.Role, role)
        };
        if (roleId is not null) claims.Add(new Claim(JwtTokenService.RoleIdClaim, roleId));
        if (permissionVersion is not null) claims.Add(new Claim(JwtTokenService.PermissionVersionClaim, permissionVersion));
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
        Assert.Null(await RejectionReason(dbName, Principal(role: "Analyst", roleId: "4")));
    }

    // Two roles can share a base type, so the type string alone cannot tell
    // that the user was moved from one to the other.
    [Fact]
    public async Task MovedToAnotherRoleOfTheSameType_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName, RoleType.Analyst);
        Assert.Null(await RejectionReason(dbName, Principal(role: "Analyst", roleId: "4")));

        UpdateUser(dbName, u => u.RoleId = 5);

        Assert.Equal("User has been moved to a different role since the token was issued.",
            await RejectionReason(dbName, Principal(role: "Analyst", roleId: "4")));
        Assert.Null(await RejectionReason(dbName, Principal(role: "Analyst", roleId: "5")));
    }

    [Fact]
    public async Task EditedRolePermissions_RejectTheTokenIssuedBeforeTheEdit()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        Assert.Null(await RejectionReason(dbName, Principal()));

        BumpPermissionVersion(dbName, roleId: 1);

        Assert.Equal("Role permissions have changed since the token was issued.",
            await RejectionReason(dbName, Principal(permissionVersion: "1")));
        Assert.Null(await RejectionReason(dbName, Principal(permissionVersion: "2")));
    }

    [Fact]
    public async Task EditingAnotherRolesPermissions_DoesNotAffectTheToken()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);

        BumpPermissionVersion(dbName, roleId: 4);

        Assert.Null(await RejectionReason(dbName, Principal()));
    }

    [Theory]
    [InlineData(null, "1", "Token carries no role id.")]
    [InlineData("1", null, "Token carries no permission version.")]
    [InlineData("not-a-number", "1", "Token carries no role id.")]
    [InlineData("1", "", "Token carries no permission version.")]
    [InlineData("1", "1.5", "Token carries no permission version.")]
    public async Task TokenWithoutAUsableRoleIdOrPermissionVersion_IsRejected(string? roleId, string? permissionVersion, string expected)
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);

        Assert.Equal(expected, await RejectionReason(dbName, Principal(roleId: roleId, permissionVersion: permissionVersion)));
    }

    private static void BumpPermissionVersion(string dbName, int roleId)
    {
        using var db = NewDb(dbName);
        db.Roles.Single(r => r.Id == roleId).PermissionVersion++;
        db.SaveChanges();
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
        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", IssueToken("Analyst", roleId: 4)));
        Assert.Equal(HttpStatusCode.Forbidden, await Get(client, "/admin-only", IssueToken("Analyst", roleId: 4)));
    }

    [Fact]
    public async Task EndToEnd_PermissionEdit_StopsTheOldToken_AndAFreshTokenWorks()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;
        var oldToken = IssueToken(permissionVersion: 1);

        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", oldToken));

        BumpPermissionVersion(dbName, roleId: 1);

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", oldToken));
        // What /auth/refresh issues now.
        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", IssueToken(permissionVersion: 2)));
    }

    [Fact]
    public async Task EndToEnd_MoveToASameTypeRole_StopsTheOldToken()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName, RoleType.Analyst);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;
        var oldToken = IssueToken("Analyst", roleId: 4);

        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", oldToken));

        UpdateUser(dbName, u => u.RoleId = 5);

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", oldToken));
        Assert.Equal(HttpStatusCode.OK, await Get(client, "/any-user", IssueToken("Analyst", roleId: 5)));
    }

    [Fact]
    public async Task EndToEnd_TokenWithoutRoleIdAndPermissionVersion_IsRefused()
    {
        var dbName = Guid.NewGuid().ToString();
        SeedUser(dbName);
        var (app, client) = await StartApiAsync(dbName);
        await using var _ = app;

        // Signed, current role type, iat present - exactly what login issued
        // before the rid and pv claims existed.
        var legacy = new JwtTokenService(Settings.Key, Settings.Issuer, Settings.Audience, Settings.AccessTokenLifetime)
            .IssueToken(UserId.ToString(), "SystemAdministrator");

        Assert.Equal(HttpStatusCode.Unauthorized, await Get(client, "/any-user", legacy));
    }

    // The real issuing path: AuthenticationService stamps rid and pv from the
    // database, RoleService edits the permissions, and the revalidator holds
    // the first token to what it was issued with.
    [Fact]
    public async Task LoginToken_IsRejectedAfterARolePermissionEdit_AndRefreshIssuesAnAcceptedOne()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDb(dbName);
        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);
        db.Users.AddRange(
            new User { Id = 1, FullName = "Standing Admin", Username = "admin", PasswordHash = "x", RoleId = 1, IsActive = true },
            new User
            {
                Id = UserId, FullName = "Token Holder", Username = "holder", RoleId = 4, IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Real-Password-1!", 4)
            });
        db.SaveChanges();

        var jwt = new JwtTokenService(Settings.Key, Settings.Issuer, Settings.Audience, Settings.AccessTokenLifetime);
        var auth = new AuthenticationService(
            db, subject => jwt.IssueToken(subject.UserId, subject.Role, subject.PermissionCodes, subject.RoleId, subject.PermissionVersion),
            new PermissionService(db), new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
            NullLogger<AuthenticationService>.Instance,
            new SecurityAuditService(db, new SystemSecurityRequestContext()));
        var roles = new RoleService(db, new PermissionService(db), signatureService: new AcceptingSignatureService(db));

        var login = await auth.LoginAsync("holder", "Real-Password-1!");
        Assert.True(login.Success);
        Assert.Equal("Analyst", login.RoleName);
        Assert.Null(await new AccessTokenRevalidator(db).GetRejectionReasonAsync(PrincipalOf(login.Token!)));

        var analystCodes = await new PermissionService(db).GetPermissionCodesForRoleAsync(4);
        await roles.UpdatePermissionsAsync(4, analystCodes.Skip(1).ToList(), actingUserId: 1, "Test reason", "pw");

        Assert.Equal("Role permissions have changed since the token was issued.",
            await new AccessTokenRevalidator(db).GetRejectionReasonAsync(PrincipalOf(login.Token!)));

        var refreshed = await auth.RefreshAsync(login.RefreshToken!);
        Assert.True(refreshed.Success);
        Assert.Null(await new AccessTokenRevalidator(db).GetRejectionReasonAsync(PrincipalOf(refreshed.Token!)));
    }

    private static ClaimsPrincipal PrincipalOf(string token)
    {
        // Claim types as the bearer handler yields them (no inbound mapping:
        // see AddMicroLimsJwtAuthentication), so the revalidator reads them as in production.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "test"));
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
