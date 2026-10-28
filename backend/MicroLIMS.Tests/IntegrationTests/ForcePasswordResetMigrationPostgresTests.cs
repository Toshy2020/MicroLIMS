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
            // Raw SQL: the model has columns (Users.DeactivatedAt) this older schema does not.
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO ""Users"" (""FullName"", ""Username"", ""PasswordHash"", ""RoleId"", ""IsActive"", ""CreatedAt"", ""FailedLoginAttempts"", ""MustChangePassword"") VALUES
                ('Active', 'reset_active', 'x', {roleId}, TRUE, {DateTime.UtcNow}, 0, FALSE),
                ('Inactive', 'reset_inactive', 'x', {roleId}, FALSE, {DateTime.UtcNow}, 0, FALSE)");
            var activeId = await db.Database.SqlQuery<int>($@"SELECT ""Id"" AS ""Value"" FROM ""Users"" WHERE ""Username"" = 'reset_active'").SingleAsync();
            var inactiveId = await db.Database.SqlQuery<int>($@"SELECT ""Id"" AS ""Value"" FROM ""Users"" WHERE ""Username"" = 'reset_inactive'").SingleAsync();

            var now = DateTime.UtcNow;
            var alreadyRevokedAt = now.AddDays(-2);
            // Raw SQL: the model now has columns this older schema does not.
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO ""RefreshTokens"" (""UserId"", ""TokenHash"", ""ExpiresAt"", ""RevokedAt"", ""CreatedAt"") VALUES
                ({activeId}, 't1', {now.AddDays(5)}, NULL, {now}),
                ({activeId}, 't2', {now.AddDays(5)}, NULL, {now}),
                ({inactiveId}, 't3', {now.AddDays(5)}, NULL, {now}),
                ({inactiveId}, 't4', {now.AddDays(5)}, {alreadyRevokedAt}, {now})");
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
