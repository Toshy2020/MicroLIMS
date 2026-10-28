using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP2.3: administrators list and revoke a user's sessions. In-memory throughout.
public class UserSessionsAdminTests
{
    private static MicroLimsDbContext CreateDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);
        db.Users.AddRange(
            new User { Id = 900, FullName = "Admin", Username = "admin", RoleId = 1, IsActive = true },
            new User { Id = 10, FullName = "Analyst", Username = "analyst1", RoleId = 4, IsActive = true, PasswordHash = TestPasswords.Hash("CurrentPass1!") });
        db.SaveChanges();
        return db;
    }

    private static (UserService Users, AuthenticationService Auth) Services(MicroLimsDbContext db)
    {
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext());
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}";
        var auth = new AuthenticationService(db, issuer, new PermissionService(db),
            new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
            NullLogger<AuthenticationService>.Instance, audit);
        return (new UserService(db, auth, audit, signatureService: new AcceptingSignatureService(db)), auth);
    }

    [Fact]
    public async Task Login_CapturesIpAndTruncatedUserAgent_AndRefreshRecordsLastUse()
    {
        using var db = CreateDb();
        var (_, auth) = Services(db);
        var login = await auth.LoginAsync("analyst1", "CurrentPass1!", "10.1.2.3", new string('x', 1000));
        var first = await db.RefreshTokens.SingleAsync();
        Assert.Equal("10.1.2.3", first.CreatedIp);
        Assert.Equal(RefreshToken.MaxUserAgentLength, first.UserAgent!.Length);
        Assert.Null(first.LastUsedAt);

        await auth.RefreshAsync(login.RefreshToken!, "10.9.9.9", "Other/2.0");
        var tokens = await db.RefreshTokens.OrderBy(t => t.Id).ToListAsync();
        Assert.NotNull(tokens[0].LastUsedAt);
        Assert.Equal("10.9.9.9", tokens[1].CreatedIp);
        Assert.Equal("Other/2.0", tokens[1].UserAgent);
    }

    [Fact]
    public async Task List_ExcludesRevokedAndExpired_AndExposesNoHash()
    {
        using var db = CreateDb();
        var (users, _) = Services(db);
        var now = DateTime.UtcNow;
        db.RefreshTokens.AddRange(
            new RefreshToken { UserId = 10, TokenHash = "SECRETHASH-active", ExpiresAt = now.AddDays(1), CreatedIp = "1.1.1.1", UserAgent = "UA" },
            new RefreshToken { UserId = 10, TokenHash = "SECRETHASH-revoked", ExpiresAt = now.AddDays(1), RevokedAt = now },
            new RefreshToken { UserId = 10, TokenHash = "SECRETHASH-expired", ExpiresAt = now.AddDays(-1) },
            new RefreshToken { UserId = 900, TokenHash = "SECRETHASH-other", ExpiresAt = now.AddDays(1) });
        db.SaveChanges();

        var list = await users.GetActiveSessionsAsync(10);

        var session = Assert.Single(list);
        Assert.Equal("1.1.1.1", session.CreatedIp);
        Assert.DoesNotContain("SECRETHASH", JsonSerializer.Serialize(list));
        Assert.DoesNotContain(typeof(UserSessionDto).GetProperties(), p => p.Name.Contains("Hash"));
    }

    [Fact]
    public async Task Revoke_RequiresReason_AndChangesNothingWithout()
    {
        using var db = CreateDb();
        var (users, _) = Services(db);
        db.RefreshTokens.Add(new RefreshToken { UserId = 10, TokenHash = "h", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.SaveChanges();

        await Assert.ThrowsAsync<InvalidOperationException>(() => users.RevokeSessionsAsync(10, "  ", 900));
        await Assert.ThrowsAsync<InvalidOperationException>(() => users.RevokeSessionsAsync(10, null, 900));

        Assert.Null((await db.RefreshTokens.SingleAsync()).RevokedAt);
        Assert.Empty(await db.SecurityAuditEvents.Where(e => e.EventCode == SecurityEventCodes.SessionRevoked).ToListAsync());
    }

    [Fact]
    public async Task Revoke_RevokesAllAndWritesSecurityEventAndAuditReason()
    {
        using var db = CreateDb();
        var (users, auth) = Services(db);
        var login = await auth.LoginAsync("analyst1", "CurrentPass1!", "10.0.0.1", "UA");
        await auth.LoginAsync("analyst1", "CurrentPass1!", "10.0.0.2", "UA2");
        db.RefreshTokens.Add(new RefreshToken { UserId = 900, TokenHash = "admins", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.SaveChanges();

        var count = await users.RevokeSessionsAsync(10, "  Lost laptop ", 900);

        Assert.Equal(2, count);
        Assert.Empty(await users.GetActiveSessionsAsync(10));
        Assert.Null((await db.RefreshTokens.SingleAsync(t => t.UserId == 900)).RevokedAt);
        Assert.False((await auth.RefreshAsync(login.RefreshToken!)).Success);

        var evt = await db.SecurityAuditEvents.SingleAsync(e => e.EventCode == SecurityEventCodes.SessionRevoked);
        Assert.Equal(900, evt.ActorUserId);
        Assert.Equal(10, evt.TargetUserId);
        Assert.Equal("Lost laptop", evt.Reason);
        Assert.Contains("\"revokedSessionCount\":2", evt.Metadata);

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "USER_SESSIONS_REVOKED");
        Assert.Equal("Lost laptop", audit.Reason);
        Assert.Equal(900, audit.UserId);
    }

    [Fact]
    public async Task UnknownUser_IsNotFound()
    {
        using var db = CreateDb();
        var (users, _) = Services(db);
        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(() => users.GetActiveSessionsAsync(404));
        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(() => users.RevokeSessionsAsync(404, "r", 900));
    }

    [Fact]
    public void Controller_RequiresUsersManage()
    {
        var attr = typeof(AdminUserSessionsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(PermissionConstants.UsersManage, attr?.Policy);
        Assert.All(typeof(AdminUserSessionsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
    }
}
