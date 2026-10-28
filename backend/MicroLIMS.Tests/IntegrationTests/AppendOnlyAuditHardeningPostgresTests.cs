using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Database-level guarantees added by HardenAppendOnlyAuditTables: TRUNCATE is
// blocked on every append-only table, LoginHistories and SecurityAuditEvents
// are append-only, and a user referenced by security events cannot be deleted
// (FK RESTRICT - SecurityAuditEvents is an immutable regulated record).
[Collection("PostgresDatabaseCollection")]
public class AppendOnlyAuditHardeningPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public AppendOnlyAuditHardeningPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static readonly string[] AppendOnlyTables =
    {
        "AuditLogs",
        "AuditEventChanges",
        "ElectronicSignatures",
        "DocumentAcknowledgementRecords",
        "WorkflowProfileVersions",
        "InstrumentIngestPayloads",
        "InstrumentIngestVersions",
        "InstrumentIngests",
        "LoginHistories",
        "SecurityAuditEvents",
        "SystemSettingVersions"
    };

    [PostgresFact]
    public Task Truncate_IsBlocked_OnEveryAppendOnlyTable() =>
        PostgresCases.RunAsync(AppendOnlyTables, async table =>
        {
            await using var db = _fixture.CreateDbContext();

            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{table}\" CASCADE"));

            Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TRUNCATE", ex.Message);
        });

    [PostgresFact]
    public async Task LoginHistories_UpdateAndDelete_AreBlocked()
    {
        await using var db = _fixture.CreateDbContext();
        var row = new LoginHistory { Username = "append_only_login", Success = false, FailureReason = "test" };
        db.LoginHistories.Add(row);
        await db.SaveChangesAsync();

        await using var sqlDb = _fixture.CreateDbContext();
        var updateEx = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("UPDATE \"LoginHistories\" SET \"Success\" = TRUE WHERE \"Id\" = {0}", row.Id));
        Assert.Contains("append-only", updateEx.Message, StringComparison.OrdinalIgnoreCase);

        var deleteEx = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("DELETE FROM \"LoginHistories\" WHERE \"Id\" = {0}", row.Id));
        Assert.Contains("append-only", deleteEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task SecurityAuditEvents_UpdateAndDelete_AreBlocked()
    {
        await using var db = _fixture.CreateDbContext();
        var evt = NewEvent(actorUserId: null);
        db.SecurityAuditEvents.Add(evt);
        await db.SaveChangesAsync();

        await using var sqlDb = _fixture.CreateDbContext();
        var updateEx = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("UPDATE \"SecurityAuditEvents\" SET \"Reason\" = 'TAMPERED' WHERE \"Id\" = {0}", evt.Id));
        Assert.Contains("append-only", updateEx.Message, StringComparison.OrdinalIgnoreCase);

        var deleteEx = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("DELETE FROM \"SecurityAuditEvents\" WHERE \"Id\" = {0}", evt.Id));
        Assert.Contains("append-only", deleteEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task DeletingUser_ReferencedBySecurityEvents_IsRestricted_AndEventSurvivesUntouched()
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = (await db.Users.AsNoTracking().FirstAsync(u => u.Id == _fixture.SeededUserId)).RoleId;
        var user = new User
        {
            FullName = "Restrict Subject",
            Username = $"restrict_{Guid.NewGuid():N}".Substring(0, 24),
            PasswordHash = "hashed_dummy",
            RoleId = roleId,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var evt = NewEvent(actorUserId: user.Id);
        evt.TargetUserId = user.Id;
        db.SecurityAuditEvents.Add(evt);
        await db.SaveChangesAsync();

        await using var sqlDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = {0}", user.Id));
        Assert.Contains(ex.SqlState, new[] { "23001", "23503" });

        await using var verify = _fixture.CreateDbContext();
        var stored = await verify.SecurityAuditEvents.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal(user.Id, stored.ActorUserId);
        Assert.Equal(user.Id, stored.TargetUserId);
    }

    private static SecurityAuditEvent NewEvent(int? actorUserId) => new()
    {
        EventCode = "AUTH_LOGIN_FAILED",
        Source = "Api",
        ActorUserId = actorUserId,
        TargetUsername = "append_only_subject",
        Reason = "append-only hardening test"
    };
}
