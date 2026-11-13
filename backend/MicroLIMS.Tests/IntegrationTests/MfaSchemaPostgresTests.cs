using MicroLIMS.Domain.Attributes;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MfaSchemaPostgresTests
{
    private const string UniqueViolation = "23505";
    private static readonly DateTime FixedUtc = new(2026, 11, 13, 3, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public MfaSchemaPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<int> SeedUserAsync()
    {
        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var tag = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"MFA Test {tag[..8]}",
            Username = $"mfa_{tag}",
            PasswordHash = TestPasswords.Hash("MfaTestPass1!"),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = FixedUtc
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();
        return user.Id;
    }

    private static UserMfaCredential NewCredential(int userId, MfaCredentialStatus status, string secret = "k1:nonce:cipher:tag") => new()
    {
        UserId = userId,
        Type = MfaCredentialType.AuthenticatorApp,
        SecretProtected = secret,
        StepSeconds = 30,
        Status = status,
        CreatedAt = FixedUtc,
        ConfirmedAt = status == MfaCredentialStatus.Active ? FixedUtc : null
    };

    [PostgresFact]
    public async Task SecondActiveCredentialForSameUser_IsRejectedByUniqueIndex()
    {
        var userId = await SeedUserAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.UserMfaCredentials.Add(NewCredential(userId, MfaCredentialStatus.Active, "k1:n1:c1:t1"));
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.UserMfaCredentials.Add(NewCredential(userId, MfaCredentialStatus.Active, "k1:n2:c2:t2"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task SecondPendingCredentialForSameUser_IsRejectedByUniqueIndex()
    {
        var userId = await SeedUserAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.UserMfaCredentials.Add(NewCredential(userId, MfaCredentialStatus.Pending, "k1:n1:c1:t1"));
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.UserMfaCredentials.Add(NewCredential(userId, MfaCredentialStatus.Pending, "k1:n2:c2:t2"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task PendingAndActiveCredential_CanCoexist()
    {
        var userId = await SeedUserAsync();
        int activeId;
        int pendingId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var active = NewCredential(userId, MfaCredentialStatus.Active, "k1:n1:c1:t1");
            arrangeDb.UserMfaCredentials.Add(active);
            await arrangeDb.SaveChangesAsync();
            activeId = active.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var pending = NewCredential(userId, MfaCredentialStatus.Pending, "k1:n2:c2:t2");
            actDb.UserMfaCredentials.Add(pending);
            await actDb.SaveChangesAsync();
            pendingId = pending.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var rows = await assertDb.UserMfaCredentials.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Id)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Id == activeId && r.Status == MfaCredentialStatus.Active);
        Assert.Contains(rows, r => r.Id == pendingId && r.Status == MfaCredentialStatus.Pending);
    }

    [PostgresFact]
    public async Task RevokedCredentials_AreNotLimited()
    {
        var userId = await SeedUserAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var first = NewCredential(userId, MfaCredentialStatus.Revoked, "k1:n1:c1:t1");
            first.RevokedAt = FixedUtc;
            first.RevokedByUserId = userId;
            first.RevokeReason = "Lost device";
            arrangeDb.UserMfaCredentials.Add(first);
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var second = NewCredential(userId, MfaCredentialStatus.Revoked, "k1:n2:c2:t2");
            second.RevokedAt = FixedUtc.AddMinutes(5);
            second.RevokedByUserId = userId;
            second.RevokeReason = "Second replacement";
            actDb.UserMfaCredentials.Add(second);
            await actDb.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var count = await assertDb.UserMfaCredentials.AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Revoked);
        Assert.Equal(2, count);
    }

    [PostgresFact]
    public async Task DuplicateTrustedDeviceTokenHash_IsRejected()
    {
        var userA = await SeedUserAsync();
        var userB = await SeedUserAsync();
        var tokenHash = $"device-{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.TrustedDevices.Add(new TrustedDevice
            {
                UserId = userA,
                TokenHash = tokenHash,
                CreatedAt = FixedUtc,
                ExpiresAt = FixedUtc.AddHours(12),
                CreatedIp = "127.0.0.1",
                UserAgent = "TestBrowser/1.0"
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.TrustedDevices.Add(new TrustedDevice
        {
            UserId = userB,
            TokenHash = tokenHash,
            CreatedAt = FixedUtc,
            ExpiresAt = FixedUtc.AddHours(12)
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task DuplicateChallengeTokenHash_IsRejected()
    {
        var userA = await SeedUserAsync();
        var userB = await SeedUserAsync();
        var tokenHash = $"challenge-{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.MfaChallenges.Add(new MfaChallenge
            {
                UserId = userA,
                TokenHash = tokenHash,
                Purpose = MfaChallengePurpose.Verify,
                ExpiresAt = FixedUtc.AddMinutes(5),
                FailedAttempts = 0
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.MfaChallenges.Add(new MfaChallenge
        {
            UserId = userB,
            TokenHash = tokenHash,
            Purpose = MfaChallengePurpose.Enrol,
            ExpiresAt = FixedUtc.AddMinutes(5),
            FailedAttempts = 0
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task DuplicateRecoveryCodeForSameUser_IsRejected()
    {
        var userId = await SeedUserAsync();
        var codeHash = $"code-{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.MfaRecoveryCodes.Add(new MfaRecoveryCode
            {
                UserId = userId,
                CodeHash = codeHash,
                CreatedAt = FixedUtc
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.MfaRecoveryCodes.Add(new MfaRecoveryCode
        {
            UserId = userId,
            CodeHash = codeHash,
            CreatedAt = FixedUtc
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task ExistingUsers_HaveNullMfaRequiredSince_AndRefreshTokensDefaultMfaVerifiedFalse()
    {
        var userId = await SeedUserAsync();
        int refreshTokenId;

        await using (var actDb = _fixture.CreateDbContext())
        {
            var token = new RefreshToken
            {
                UserId = userId,
                TokenHash = $"rt-{Guid.NewGuid():N}",
                CreatedAt = FixedUtc,
                ExpiresAt = FixedUtc.AddDays(7)
            };
            actDb.RefreshTokens.Add(token);
            await actDb.SaveChangesAsync();
            refreshTokenId = token.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var seededUser = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == _fixture.SeededUserId);
        Assert.Null(seededUser.MfaRequiredSince);

        var createdUser = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Null(createdUser.MfaRequiredSince);

        var persistedToken = await assertDb.RefreshTokens.AsNoTracking().SingleAsync(r => r.Id == refreshTokenId);
        Assert.False(persistedToken.MfaVerified);
    }

    [PostgresFact]
    public async Task Credential_insert_is_audited_with_the_secret_redacted()
    {
        var userId = await SeedUserAsync();
        var protectedSecret = $"k1:nonce-{Guid.NewGuid():N}:ciphertext:tag";
        int credentialId;

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var credential = NewCredential(userId, MfaCredentialStatus.Active, protectedSecret);
            credential.SerialNumber = "HW-1001";
            actDb.UserMfaCredentials.Add(credential);
            await actDb.SaveChangesAsync();
            credentialId = credential.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var audit = await assertDb.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(UserMfaCredential)
            && a.EntityId == credentialId.ToString()
            && a.Action == "Create");

        Assert.Equal(userId, audit.UserId);
        Assert.NotNull(audit.NewValue);
        Assert.DoesNotContain(protectedSecret, audit.NewValue);
        Assert.Contains($"\"SecretProtected\":\"{AuditRedactedAttribute.RedactedValue}\"", audit.NewValue);
        Assert.Contains("\"SerialNumber\":\"HW-1001\"", audit.NewValue);
    }

    [PostgresFact]
    public async Task Challenge_insert_writes_no_audit_row()
    {
        var userId = await SeedUserAsync();
        int challengeId;

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var challenge = new MfaChallenge
            {
                UserId = userId,
                TokenHash = $"challenge-{Guid.NewGuid():N}",
                Purpose = MfaChallengePurpose.Verify,
                ExpiresAt = FixedUtc.AddMinutes(5),
                FailedAttempts = 0
            };
            actDb.MfaChallenges.Add(challenge);
            await actDb.SaveChangesAsync();
            challengeId = challenge.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var hasAudit = await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(MfaChallenge) && a.EntityId == challengeId.ToString());
        Assert.False(hasAudit);
    }
}
