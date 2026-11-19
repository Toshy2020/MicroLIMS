using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SsoSchemaPostgresTests
{
    private const string AdminPassword = "SsoSchemaAdminPass1!";
    private readonly PostgresTestFixture _fixture;

    public SsoSchemaPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static User NewUser(
        int roleId,
        string prefix,
        SignInMethod signInMethod = SignInMethod.Local,
        string? directoryUsername = null,
        string? directorySubjectId = null,
        bool isEmergencyAccount = false)
    {
        var tag = Guid.NewGuid().ToString("N");
        return new User
        {
            FullName = $"SSO User {tag[..8]}",
            Username = $"{prefix}_{tag}",
            Email = $"{prefix}_{tag}@example.test",
            PasswordHash = TestPasswords.Hash("TempPass1!"),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            SignInMethod = signInMethod,
            DirectoryUsername = directoryUsername,
            DirectorySubjectId = directorySubjectId,
            IsEmergencyAccount = isEmergencyAccount
        };
    }

    private async Task<int> GetRoleIdAsync(RoleType roleType = RoleType.Analyst)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();
    }

    [PostgresFact]
    public async Task TwoUsersWithSameDirectoryUsername_DifferingOnlyInCase_AreRejected()
    {
        var roleId = await GetRoleIdAsync();
        var tag = Guid.NewGuid().ToString("N");
        var firstNormalized = User.NormalizeDirectoryUsername($"  Alice.{tag}@Corp.Test  ");
        var secondNormalized = User.NormalizeDirectoryUsername($"alice.{tag}@corp.test");
        Assert.Equal($"alice.{tag}@corp.test", firstNormalized);
        Assert.Equal(firstNormalized, secondNormalized);

        await using (var db1 = _fixture.CreateDbContext())
        {
            db1.Users.Add(NewUser(roleId, "sso_du1", SignInMethod.Directory, directoryUsername: firstNormalized));
            await db1.SaveChangesAsync();
        }

        await using var db2 = _fixture.CreateDbContext();
        db2.Users.Add(NewUser(roleId, "sso_du2", SignInMethod.Directory, directoryUsername: secondNormalized));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task TwoUsersWithSameSubjectId_AreRejected()
    {
        var roleId = await GetRoleIdAsync();
        var subjectId = $"https://idp.example.test|{Guid.NewGuid():N}";

        await using (var db1 = _fixture.CreateDbContext())
        {
            db1.Users.Add(NewUser(roleId, "sso_sub1", SignInMethod.Directory, directorySubjectId: subjectId));
            await db1.SaveChangesAsync();
        }

        await using var db2 = _fixture.CreateDbContext();
        db2.Users.Add(NewUser(roleId, "sso_sub2", SignInMethod.Directory, directorySubjectId: subjectId));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task ManyUsersWithNullDirectoryUsername_AreAllowed()
    {
        var roleId = await GetRoleIdAsync();
        var ids = new List<int>();

        await using (var db = _fixture.CreateDbContext())
        {
            var u1 = NewUser(roleId, "sso_null1", SignInMethod.Local, directoryUsername: null, directorySubjectId: null);
            var u2 = NewUser(roleId, "sso_null2", SignInMethod.Local, directoryUsername: null, directorySubjectId: null);
            var u3 = NewUser(roleId, "sso_null3", SignInMethod.Local, directoryUsername: null, directorySubjectId: null);
            db.Users.AddRange(u1, u2, u3);
            await db.SaveChangesAsync();
            ids.AddRange(new[] { u1.Id, u2.Id, u3.Id });
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync();
        Assert.Equal(3, persisted.Count);
        Assert.All(persisted, u =>
        {
            Assert.Null(u.DirectoryUsername);
            Assert.Null(u.DirectorySubjectId);
        });
    }

    [PostgresFact]
    public async Task EmergencyAccountWithDirectorySignIn_IsRejectedByCheckConstraint()
    {
        var roleId = await GetRoleIdAsync(RoleType.SystemAdministrator);

        await using var db = _fixture.CreateDbContext();
        db.Users.Add(NewUser(
            roleId,
            "sso_emg_bad",
            SignInMethod.Directory,
            directoryUsername: User.NormalizeDirectoryUsername($"emg_{Guid.NewGuid():N}@corp.test"),
            isEmergencyAccount: true));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task ExistingUsers_DefaultToLocal_NotEmergency()
    {
        var roleId = await GetRoleIdAsync();
        var username = $"sso_legacy_{Guid.NewGuid():N}";

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""Users"" (""FullName"", ""Username"", ""PasswordHash"", ""RoleId"", ""IsActive"", ""CreatedAt"", ""FailedLoginAttempts"", ""MustChangePassword"")
                VALUES ({username}, {username}, {TestPasswords.Hash("LegacyPass1!")}, {roleId}, {true}, {DateTime.UtcNow}, {0}, {false})");
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Username == username);
        Assert.Equal(SignInMethod.Local, persisted.SignInMethod);
        Assert.False(persisted.IsEmergencyAccount);
        Assert.Null(persisted.DirectoryUsername);
        Assert.Null(persisted.DirectorySubjectId);
        Assert.Null(persisted.SigningPasswordSetAt);
        Assert.Null(persisted.DirectorySignInRefusedAt);
    }

    [PostgresFact]
    public async Task SsoPendingRequestAndExchangeCode_AreNotAutoAudited_AndSecretsNeverReachAuditLogs()
    {
        var roleId = await GetRoleIdAsync();
        var now = DateTime.UtcNow;
        var stateHashSecret = $"STATE_HASH_SECRET_{Guid.NewGuid():N}";
        var verifierSecret = $"CODE_VERIFIER_SECRET_{Guid.NewGuid():N}";
        var codeHashSecret = $"CODE_HASH_SECRET_{Guid.NewGuid():N}";

        int pendingId;
        int exchangeId;

        await using (var db = _fixture.CreateDbContext())
        {
            var user = NewUser(roleId, "sso_audit", SignInMethod.Directory);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var pending = new SsoPendingRequest
            {
                StateHash = stateHashSecret,
                Nonce = "nonce-value-123",
                CodeVerifier = verifierSecret,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(10)
            };
            var exchange = new SsoExchangeCode
            {
                UserId = user.Id,
                CodeHash = codeHashSecret,
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(60)
            };
            db.SsoPendingRequests.Add(pending);
            db.SsoExchangeCodes.Add(exchange);
            await db.SaveChangesAsync();

            pendingId = pending.Id;
            exchangeId = exchange.Id;
        }

        // CONF-193: both types are excluded from automatic audit capture; sign-in outcomes are
        // recorded as explicit security events. The secrets must still never reach AuditLogs.
        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.SsoPendingRequests.AsNoTracking().AnyAsync(p => p.Id == pendingId));
        Assert.True(await assertDb.SsoExchangeCodes.AsNoTracking().AnyAsync(c => c.Id == exchangeId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(SsoPendingRequest) || a.EntityName == nameof(SsoExchangeCode)));
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => (a.NewValue != null && (a.NewValue.Contains(stateHashSecret) || a.NewValue.Contains(verifierSecret) || a.NewValue.Contains(codeHashSecret)))
                           || (a.PreviousValue != null && (a.PreviousValue.Contains(stateHashSecret) || a.PreviousValue.Contains(verifierSecret) || a.PreviousValue.Contains(codeHashSecret)))));
    }

    [PostgresFact]
    public async Task SsoSettings_HaveDefaults_AndSessionMaxHoursRejectsOutOfRange()
    {
        var modeDef = SystemSettingDefinitions.Get(SystemSettingKeys.SsoMode);
        var maxHoursDef = SystemSettingDefinitions.Get(SystemSettingKeys.SsoSessionMaxHours);
        Assert.Equal("Off", modeDef.Default);
        Assert.True(modeDef.IsGxpRelevant);
        Assert.Equal("12", maxHoursDef.Default);
        Assert.Equal(1, maxHoursDef.Min);
        Assert.Equal(24, maxHoursDef.Max);
        Assert.True(maxHoursDef.IsGxpRelevant);

        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(cs);
            await db.Database.MigrateAsync();

            var adminRole = new Role
            {
                Name = "System Administrator",
                Type = RoleType.SystemAdministrator,
                IsSystemRole = true,
                IsActive = true
            };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();

            var admin = new User
            {
                FullName = "SSO Admin",
                Username = "sso_settings_admin",
                PasswordHash = TestPasswords.Hash(AdminPassword),
                RoleId = adminRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            var clock = TimeProvider.System;
            var cache = new SystemSettingsCache();
            var reader = new SystemSettingsReader(db, cache, new ConfigurationBuilder().Build(), clock);
            var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
            var signatures = new ElectronicSignatureService(db, clock, reader);
            var service = new SystemSettingsService(db, cache, signatures, null, clock, null, audit);

            Assert.Equal("Off", await reader.GetStringAsync(SystemSettingKeys.SsoMode));
            Assert.Equal(12, await reader.GetIntAsync(SystemSettingKeys.SsoSessionMaxHours));

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdateAsync(SystemSettingKeys.SsoSessionMaxHours, "0", "Too low", admin.Id, AdminPassword, null));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdateAsync(SystemSettingKeys.SsoSessionMaxHours, "25", "Too high", admin.Id, AdminPassword, null));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdateAsync(SystemSettingKeys.SsoMode, "Saml", "Unsupported", admin.Id, AdminPassword, null));

            await service.UpdateAsync(SystemSettingKeys.SsoSessionMaxHours, "8", "Valid cap", admin.Id, AdminPassword, null);
            Assert.Equal(8, await reader.GetIntAsync(SystemSettingKeys.SsoSessionMaxHours));
        });
    }
}
