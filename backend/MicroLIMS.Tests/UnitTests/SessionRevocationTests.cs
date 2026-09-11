using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The security invariant: a session must stop working once the account
// behind it has had its access withdrawn. Before this, refresh only
// checked the token row, so disabling or locking a user blocked the login
// form while their refresh token went on minting new access tokens.
//
// In-memory persistence throughout - no PostgreSQL fixture, nothing
// destructive.
public class SessionRevocationTests
{
    private const string CurrentPassword = "CurrentPass1!";
    private const string ReplacementPassword = "Replacement2@";

    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" }
        );
        db.SaveChanges();
        return db;
    }

    private static (UserService UserService, AuthenticationService AuthService) CreateServices(MicroLimsDbContext db)
    {
        var emailSender = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        Func<string, string, IEnumerable<string>, string> tokenIssuer = (id, role, codes) => $"jwt-for-{id}";
        var securityAudit = new SecurityAuditService(db, new SystemSecurityRequestContext());
        var authService = new AuthenticationService(
            db, tokenIssuer, new PermissionService(db), emailSender, NullLogger<AuthenticationService>.Instance, securityAudit);
        return (new UserService(db, authService, securityAudit), authService);
    }

    private static User SeedUser(MicroLimsDbContext db, int id = 10, string username = "analyst1")
    {
        var user = new User
        {
            Id = id,
            FullName = "Test Analyst",
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(CurrentPassword),
            RoleId = 4,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    // Establishes a real session the way login does, returning the raw
    // refresh token the client would hold.
    private static async Task<string> LoginAndGetRefreshTokenAsync(AuthenticationService auth, string username = "analyst1")
    {
        var outcome = await auth.LoginAsync(username, CurrentPassword);
        Assert.True(outcome.Success);
        Assert.NotNull(outcome.RefreshToken);
        return outcome.RefreshToken!;
    }

    // ---------- Refresh-token / session behaviour ----------

    // 1. Active user + valid refresh token -> refresh succeeds.
    [Fact]
    public async Task Refresh_ActiveUserWithValidToken_Succeeds()
    {
        using var db = CreateDbContext();
        SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Token);
        Assert.NotNull(outcome.RefreshToken);
    }

    // 2. Revoked refresh token -> refresh fails.
    [Fact]
    public async Task Refresh_RevokedToken_Fails()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        await auth.RevokeAllRefreshTokensAsync(user.Id);
        await db.SaveChangesAsync();

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Null(outcome.RefreshToken);
    }

    // 3. Logged-out session -> refresh fails.
    [Fact]
    public async Task Refresh_AfterLogout_Fails()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        await auth.LogoutAsync(user.Id, refreshToken);

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
    }

    // 4. Disabled user + otherwise valid refresh token -> refresh fails.
    [Fact]
    public async Task Refresh_DisabledUser_Fails()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (users, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        await users.SetStatusAsync(user.Id, isActive: false, reason: "Left the laboratory", actingUserId: 1);

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Null(outcome.RefreshToken);
    }

    // Disabling must revoke the stored rows, not merely fail the check -
    // so the session is dead even if the user is re-enabled later.
    [Fact]
    public async Task Disable_RevokesStoredRefreshTokens_AndReEnablingDoesNotResurrectThem()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (users, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        await users.SetStatusAsync(user.Id, isActive: false, reason: "Suspended", actingUserId: 1);
        Assert.All(await db.RefreshTokens.Where(r => r.UserId == user.Id).ToListAsync(),
            r => Assert.NotNull(r.RevokedAt));

        await users.SetStatusAsync(user.Id, isActive: true, reason: null, actingUserId: 1);

        var outcome = await auth.RefreshAsync(refreshToken);
        Assert.False(outcome.Success);
    }

    // 5. Locked user + otherwise valid refresh token -> refresh fails.
    [Fact]
    public async Task Refresh_LockedUser_Fails()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        var stored = await db.Users.FirstAsync(u => u.Id == user.Id);
        stored.LockedUntil = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
    }

    // An expired lock is not a lock - access comes back on its own.
    [Fact]
    public async Task Refresh_AfterLockExpires_Succeeds()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        var stored = await db.Users.FirstAsync(u => u.Id == user.Id);
        stored.LockedUntil = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.True(outcome.Success);
    }

    // Automatic lockout after repeated failures must also end live sessions.
    [Fact]
    public async Task Refresh_AfterAutomaticLockoutFromFailedLogins_Fails()
    {
        using var db = CreateDbContext();
        SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        for (var attempt = 0; attempt < 5; attempt++)
            await auth.LoginAsync("analyst1", "WrongPassword9!");

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
    }

    // 6. Password-changed user + old refresh token -> refresh fails.
    [Fact]
    public async Task Refresh_AfterPasswordChange_Fails()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        Assert.True(await auth.ChangePasswordAsync(user.Id, CurrentPassword, ReplacementPassword));

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
    }

    // 7. Password-reset user + old refresh token -> refresh fails.
    [Fact]
    public async Task Refresh_AfterPasswordReset_Fails()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        // Mint a reset token directly - RequestPasswordResetAsync only
        // delivers the raw value by email, which is not exercised here.
        var rawResetToken = Guid.NewGuid().ToString("N");
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashLikeService(rawResetToken),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        Assert.True(await auth.ConfirmPasswordResetAsync(rawResetToken, ReplacementPassword));

        var outcome = await auth.RefreshAsync(refreshToken);

        Assert.False(outcome.Success);
    }

    // 8. Rotation still works normally for an active user, and a rotated
    // token cannot be replayed.
    [Fact]
    public async Task Refresh_RotationStillWorks_AndUsedTokenCannotBeReplayed()
    {
        using var db = CreateDbContext();
        SeedUser(db);
        var (_, auth) = CreateServices(db);
        var first = await LoginAndGetRefreshTokenAsync(auth);

        var rotated = await auth.RefreshAsync(first);
        Assert.True(rotated.Success);
        Assert.NotEqual(first, rotated.RefreshToken);

        // The replaced token is now revoked.
        Assert.False((await auth.RefreshAsync(first)).Success);
        // The replacement still works.
        Assert.True((await auth.RefreshAsync(rotated.RefreshToken!)).Success);
    }

    // ---------- Logout ----------

    // 9. Logout revokes the intended session, and 10. reuse fails.
    [Fact]
    public async Task Logout_RevokesOnlyTheNamedSession()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);

        var sessionA = await LoginAndGetRefreshTokenAsync(auth);
        var sessionB = await LoginAndGetRefreshTokenAsync(auth);

        await auth.LogoutAsync(user.Id, sessionA);

        Assert.False((await auth.RefreshAsync(sessionA)).Success);
        Assert.True((await auth.RefreshAsync(sessionB)).Success);
    }

    // Without a session identifier logout fails safe: every session ends.
    [Fact]
    public async Task Logout_WithoutRefreshToken_RevokesEverySession()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);

        var sessionA = await LoginAndGetRefreshTokenAsync(auth);
        var sessionB = await LoginAndGetRefreshTokenAsync(auth);

        await auth.LogoutAsync(user.Id, null);

        Assert.False((await auth.RefreshAsync(sessionA)).Success);
        Assert.False((await auth.RefreshAsync(sessionB)).Success);
    }

    // A token belonging to somebody else must never revoke their session.
    [Fact]
    public async Task Logout_WithAnotherUsersToken_DoesNotRevokeThatSession()
    {
        using var db = CreateDbContext();
        SeedUser(db, id: 10, username: "analyst1");
        SeedUser(db, id: 11, username: "analyst2");
        var (_, auth) = CreateServices(db);

        var victimSession = await LoginAndGetRefreshTokenAsync(auth, "analyst2");
        await LoginAndGetRefreshTokenAsync(auth, "analyst1");

        // User 10 tries to log out user 11's session.
        await auth.LogoutAsync(10, victimSession);

        Assert.True((await auth.RefreshAsync(victimSession)).Success);
    }

    // 11. Logout is idempotent and safe on an already-revoked token.
    [Fact]
    public async Task Logout_IsIdempotent_AndPreservesTheOriginalRevocationTime()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        await auth.LogoutAsync(user.Id, refreshToken);
        var firstRevokedAt = (await db.RefreshTokens.SingleAsync(r => r.UserId == user.Id)).RevokedAt;

        await auth.LogoutAsync(user.Id, refreshToken);
        var secondRevokedAt = (await db.RefreshTokens.SingleAsync(r => r.UserId == user.Id)).RevokedAt;

        Assert.Equal(firstRevokedAt, secondRevokedAt);
        Assert.False((await auth.RefreshAsync(refreshToken)).Success);
    }

    // ---------- Regression ----------

    // 12. Login still works for an active user.
    [Fact]
    public async Task Login_StillSucceedsForActiveUser()
    {
        using var db = CreateDbContext();
        SeedUser(db);
        var (_, auth) = CreateServices(db);

        var outcome = await auth.LoginAsync("analyst1", CurrentPassword);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Token);
        Assert.NotNull(outcome.RefreshToken);
    }

    // 13. A fresh session after a password change works as expected.
    [Fact]
    public async Task Login_AfterPasswordChange_IssuesAWorkingSession()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        await LoginAndGetRefreshTokenAsync(auth);

        Assert.True(await auth.ChangePasswordAsync(user.Id, CurrentPassword, ReplacementPassword));

        var relogin = await auth.LoginAsync("analyst1", ReplacementPassword);
        Assert.True(relogin.Success);
        Assert.True((await auth.RefreshAsync(relogin.RefreshToken!)).Success);
    }

    // Disabling one account must not touch anybody else's sessions.
    [Fact]
    public async Task Disable_DoesNotAffectOtherUsersSessions()
    {
        using var db = CreateDbContext();
        SeedUser(db, id: 10, username: "analyst1");
        SeedUser(db, id: 11, username: "analyst2");
        var (users, auth) = CreateServices(db);

        var bystander = await LoginAndGetRefreshTokenAsync(auth, "analyst2");
        await LoginAndGetRefreshTokenAsync(auth, "analyst1");

        await users.SetStatusAsync(10, isActive: false, reason: "Suspended", actingUserId: 1);

        Assert.True((await auth.RefreshAsync(bystander)).Success);
    }

    // Revocation is technical authentication state: it must not invent a
    // GxP audit record of its own. The explicit USER_DISABLED entry the
    // existing service already wrote stays the audit evidence, exactly
    // once, and revocation adds no bespoke entry alongside it.
    //
    // (The generic Create/Update rows around it come from the DbContext's
    // entity-level CaptureAuditEntries, which predates this change -
    // RefreshToken is not on its exclusion list, so refresh-token writes
    // have always produced them. Left as-is deliberately.)
    [Fact]
    public async Task Disable_WritesTheExistingAuditEntryAndNoBespokeRevocationEntry()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (users, auth) = CreateServices(db);
        await LoginAndGetRefreshTokenAsync(auth);

        await users.SetStatusAsync(user.Id, isActive: false, reason: "Suspended", actingUserId: 1);

        var explicitEntries = await db.AuditLogs
            .Where(a => a.EntityName == nameof(User) && a.Action == "USER_DISABLED")
            .ToListAsync();
        Assert.Single(explicitEntries);

        var invented = await db.AuditLogs
            .Where(a => a.Action.Contains("REVOK") || a.Action.Contains("SESSION") || a.Action.Contains("LOGOUT"))
            .ToListAsync();
        Assert.Empty(invented);
    }

    // Logging out likewise writes no GxP audit action of its own.
    [Fact]
    public async Task Logout_WritesNoBespokeAuditEntry()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var (_, auth) = CreateServices(db);
        var refreshToken = await LoginAndGetRefreshTokenAsync(auth);

        await auth.LogoutAsync(user.Id, refreshToken);

        var invented = await db.AuditLogs
            .Where(a => a.Action.Contains("REVOK") || a.Action.Contains("SESSION") || a.Action.Contains("LOGOUT"))
            .ToListAsync();
        Assert.Empty(invented);
    }

    // Mirrors AuthenticationService.Hash so a reset token can be seeded.
    private static string HashLikeService(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
