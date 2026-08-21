using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Validation;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class AdminPasswordRecoveryTests
{
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

    [Fact]
    public async Task Scenario01_AdminCanGenerateRecoveryCodeForAnotherUser()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        db.Users.Add(new User { Id = 2, FullName = "Target User", Username = "target1", RoleId = 4, IsActive = true });
        await db.SaveChangesAsync();

        var result = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "User forgot password", actingUserId: 1);

        Assert.NotNull(result.RecoveryCode);
        Assert.Equal(14, result.RecoveryCode.Length); // XXXX-XXXX-XXXX
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void Scenario02_NonAdminBlockedOnControllerEndpoint()
    {
        var controllerType = typeof(MicroLIMS.API.Controllers.UserController);
        var authorizeAttrs = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(authorizeAttrs);
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorizeAttrs[0];
        Assert.Equal(MicroLIMS.Shared.Constants.RoleConstants.SystemAdministrator, attr.Roles);
    }

    [Fact]
    public async Task Scenario03_AdminCannotGenerateRecoveryCodeForThemselves()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        db.Users.Add(new User { Id = 1, FullName = "Admin User", Username = "admin", RoleId = 1, IsActive = true });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRecoveryRequestAsync(targetUserId: 1, reason: "Self recovery attempt", actingUserId: 1));

        Assert.Contains("cannot initiate admin-assisted password recovery for their own account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario04_MandatoryReasonEnforced()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        db.Users.Add(new User { Id = 2, FullName = "Target User", Username = "target2", RoleId = 4, IsActive = true });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "   ", actingUserId: 1));

        Assert.Contains("reason is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scenario05_RecoveryCodeIsCryptographicallyRandom()
    {
        var code1 = AdminPasswordRecoveryService.GenerateRecoveryCode();
        var code2 = AdminPasswordRecoveryService.GenerateRecoveryCode();

        Assert.NotEqual(code1, code2);
        Assert.Matches(@"^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$", code1);
    }

    [Fact]
    public async Task Scenario06_PlaintextCodeNotStoredInDatabase()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        db.Users.Add(new User { Id = 2, FullName = "Target User", Username = "target6", RoleId = 4, IsActive = true });
        await db.SaveChangesAsync();

        var result = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Testing DB storage", actingUserId: 1);

        var dbRecord = await db.AdminPasswordRecoveries.FirstOrDefaultAsync(r => r.UserId == 2);
        Assert.NotNull(dbRecord);
        Assert.NotEqual(result.RecoveryCode, dbRecord.CodeHash);
        Assert.DoesNotContain(result.RecoveryCode, dbRecord.CodeHash);
    }

    [Fact]
    public async Task Scenario07_RecoveryCodeExpires()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target7", RoleId = 4, IsActive = true };
        db.Users.Add(user);

        var code = "ABCD-EFGH-JKMN";
        var hash = AdminPasswordRecoveryService.HashRecoveryCode(code);
        db.AdminPasswordRecoveries.Add(new AdminPasswordRecovery
        {
            UserId = 2,
            CreatedByUserId = 1,
            CodeHash = hash,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-15),
            Status = AdminPasswordRecoveryStatus.Pending,
            Reason = "Expired request"
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmRecoveryAsync("target7", code, "NewP@ss1234!"));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario08_RecoveryCodeWorksBeforeExpiry()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target8", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Valid recovery", actingUserId: 1);

        await service.ConfirmRecoveryAsync("target8", request.RecoveryCode, "ValidP@ss123!");

        var updatedUser = await db.Users.FirstAsync(u => u.Id == 2);
        Assert.False(updatedUser.MustChangePassword);
    }

    [Fact]
    public async Task Scenario09_10_RecoveryCodeCannotBeUsedTwice()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target9", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "One time use test", actingUserId: 1);

        await service.ConfirmRecoveryAsync("target9", request.RecoveryCode, "ValidP@ss123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmRecoveryAsync("target9", request.RecoveryCode, "AnotherP@ss123!"));

        Assert.Contains("invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario11_FifthFailedAttemptInvalidatesCode()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target11", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Failed attempts test", actingUserId: 1);

        for (int i = 1; i <= 4; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmRecoveryAsync("target11", "WRON-GCOD-E123", "ValidP@ss123!"));
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmRecoveryAsync("target11", "WRON-GCOD-E123", "ValidP@ss123!"));

        Assert.Contains("exceeded", ex.Message, StringComparison.OrdinalIgnoreCase);

        var recoveryRecord = await db.AdminPasswordRecoveries.FirstAsync(r => r.UserId == 2);
        Assert.Equal(AdminPasswordRecoveryStatus.FailedLimitExceeded, recoveryRecord.Status);
    }

    [Fact]
    public async Task Scenario12_PasswordPolicyEnforcedOnRecovery()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target12", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Policy test", actingUserId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmRecoveryAsync("target12", request.RecoveryCode, "weak"));

        Assert.Contains("at least 8 characters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario13_PasswordHistoryEnforcedOnRecovery()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target13", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var oldHash = BCrypt.Net.BCrypt.HashPassword("PreviousP@ss123!");
        db.PasswordHistories.Add(new PasswordHistory { UserId = 2, PasswordHash = oldHash });
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "History test", actingUserId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmRecoveryAsync("target13", request.RecoveryCode, "PreviousP@ss123!"));

        Assert.Contains("must not match any of your last 5 passwords", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario14_15_SuccessfulRecoveryUpdatesTimestampsAndFlags()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target14", RoleId = 4, IsActive = true, MustChangePassword = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Timestamp test", actingUserId: 1);

        await service.ConfirmRecoveryAsync("target14", request.RecoveryCode, "NewValidP@ss123!");

        var updatedUser = await db.Users.FirstAsync(u => u.Id == 2);
        Assert.False(updatedUser.MustChangePassword);
        Assert.NotNull(updatedUser.PasswordChangedAt);
    }

    [Fact]
    public async Task Scenario16_DisabledUserNotReactivatedByRecovery()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Disabled User", Username = "disabled2", RoleId = 4, IsActive = false };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Attempt on disabled", actingUserId: 1));

        Assert.Contains("disabled user account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario17_LockedUserLockoutClearedOnSuccessfulRecovery()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Locked User", Username = "locked2", RoleId = 4, IsActive = true, FailedLoginAttempts = 5, LockedUntil = DateTime.UtcNow.AddHours(1) };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Lockout clear test", actingUserId: 1);

        await service.ConfirmRecoveryAsync("locked2", request.RecoveryCode, "UnlockedP@ss123!");

        var updatedUser = await db.Users.FirstAsync(u => u.Id == 2);
        Assert.False(updatedUser.IsLocked);
        Assert.Equal(0, updatedUser.FailedLoginAttempts);
        Assert.Null(updatedUser.LockedUntil);
    }

    [Fact]
    public async Task Scenario18_19_20_AuditEventsGeneratedForRecoveryLifecycle()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Audit User", Username = "auditrec", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Audit lifecycle test", actingUserId: 1);
        await service.ConfirmRecoveryAsync("auditrec", request.RecoveryCode, "AuditP@ss123!");

        var logs = await db.AuditLogs.Where(a => a.EntityName == nameof(AdminPasswordRecovery)).ToListAsync();
        Assert.Contains(logs, l => l.Action == "ADMIN_PASSWORD_RECOVERY_REQUESTED");
        Assert.Contains(logs, l => l.Action == "ADMIN_PASSWORD_RECOVERY_USED");
    }

    [Fact]
    public async Task Scenario21_22_SecretsNeverWrittenToAuditLogs()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Secret Target", Username = "secretrec", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Secret log test", actingUserId: 1);
        await service.ConfirmRecoveryAsync("secretrec", request.RecoveryCode, "SecretP@ss123!");

        var logs = await db.AuditLogs.ToListAsync();
        foreach (var log in logs)
        {
            var text = (log.PreviousValue ?? "") + (log.NewValue ?? "");
            Assert.DoesNotContain("SecretP@ss123!", text);
            Assert.DoesNotContain(request.RecoveryCode, text);
        }
    }

    [Fact]
    public async Task Scenario25_ActiveRefreshTokensRevokedOnRecovery()
    {
        var db = CreateDbContext();
        var service = new AdminPasswordRecoveryService(db);
        var user = new User { Id = 2, FullName = "Token User", Username = "tokenrec", RoleId = 4, IsActive = true };
        db.Users.Add(user);

        db.RefreshTokens.Add(new RefreshToken { UserId = 2, TokenHash = "old-refresh-token-hash", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = null });
        await db.SaveChangesAsync();

        var request = await service.CreateRecoveryRequestAsync(targetUserId: 2, reason: "Revocation test", actingUserId: 1);
        await service.ConfirmRecoveryAsync("tokenrec", request.RecoveryCode, "RevokedP@ss123!");

        var tokenRecord = await db.RefreshTokens.FirstAsync(r => r.UserId == 2);
        Assert.NotNull(tokenRecord.RevokedAt);
    }
}
