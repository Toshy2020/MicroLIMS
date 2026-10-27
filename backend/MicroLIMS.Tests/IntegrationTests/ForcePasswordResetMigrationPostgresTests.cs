using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// DEV-2026-AUD-001: credential hashes were captured in the audit trail, so every user must choose a
// new password and every session must stop renewing. The audit rows themselves are never touched.
public class ForcePasswordResetMigrationPostgresTests
{
    private const string PreviousMigration = "20261026164146_RestrictSystemAdministratorToSystemPermissions";
    private const string ProcessName = "Migration 20261027033154_ForcePasswordResetAfterAuditHashExposure";

    [PostgresFact]
    public async Task Up_ForcesEveryUserToResetAndRevokesEveryRefreshToken_WithOneSystemAuditRow()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            await db.Database.MigrateAsync(PreviousMigration);

            await db.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Roles"" (""Type"", ""Name"") VALUES (1, 'Reset Test Role');");
            var roleId = await db.Roles.Where(r => r.Name == "Reset Test Role").Select(r => r.Id).SingleAsync();
            var active = new User { FullName = "Active", Username = "reset_active", PasswordHash = "x", RoleId = roleId, IsActive = true };
            var inactive = new User { FullName = "Inactive", Username = "reset_inactive", PasswordHash = "x", RoleId = roleId, IsActive = false };
            db.Users.AddRange(active, inactive);
            await db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var alreadyRevokedAt = now.AddDays(-2);
            db.RefreshTokens.AddRange(
                new RefreshToken { UserId = active.Id, TokenHash = "t1", ExpiresAt = now.AddDays(5) },
                new RefreshToken { UserId = active.Id, TokenHash = "t2", ExpiresAt = now.AddDays(5) },
                new RefreshToken { UserId = inactive.Id, TokenHash = "t3", ExpiresAt = now.AddDays(5) },
                new RefreshToken { UserId = inactive.Id, TokenHash = "t4", ExpiresAt = now.AddDays(5), RevokedAt = alreadyRevokedAt });
            await db.SaveChangesAsync();
            Assert.False(await db.Users.AnyAsync(u => u.MustChangePassword));

            var auditBefore = await db.AuditLogs.CountAsync();
            var hashesBefore = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => u.PasswordHash).ToListAsync();

            await db.Database.MigrateAsync(); // applies ForcePasswordResetAfterAuditHashExposure

            Assert.False(await db.Users.AsNoTracking().AnyAsync(u => !u.MustChangePassword));
            Assert.Equal(hashesBefore, await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => u.PasswordHash).ToListAsync());

            var tokens = await db.RefreshTokens.AsNoTracking().ToListAsync();
            Assert.Equal(4, tokens.Count); // revoked, never deleted
            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
            // A token that was already revoked keeps its original revocation time.
            Assert.True(Math.Abs((tokens.Single(t => t.TokenHash == "t4").RevokedAt!.Value - alreadyRevokedAt).TotalSeconds) < 1);

            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.SystemProcessName == ProcessName);
            Assert.Equal(auditBefore + 1, await db.AuditLogs.CountAsync());
            Assert.Equal(ActorType.System, audit.ActorType);
            Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
            Assert.Equal("FORCED_PASSWORD_RESET", audit.Action);
            Assert.Contains("DEV-2026-AUD-001", audit.Reason);
            Assert.Contains("\"UsersAffected\": 2", audit.NewValue);
            Assert.Contains("\"RefreshTokensRevoked\": 3", audit.NewValue);
            Assert.DoesNotContain("PasswordHash", audit.NewValue);
        });
    }

    [Fact]
    public void Down_IsANoOp()
    {
        Assert.Empty(new ForcePasswordResetAfterAuditHashExposure().DownOperations);
    }
}
