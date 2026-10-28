using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SessionTokenCleanupWorkerUnitTests
{
    // Fixed clock so the age of every seeded token is exact.
    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static (MicroLimsDbContext db, SessionTokenCleanupWorker worker, FakeTimeProvider time) CreateWorker()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        var time = new FakeTimeProvider { Now = Now };

        var services = new ServiceCollection();
        services.AddSingleton<IMicroLimsDbContext>(db);
        services.AddScoped<ISecurityAuditService>(_ => new SecurityAuditService(db, new SystemSecurityRequestContext(), time));
        var provider = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder().Build(); // defaults: 30 days

        var worker = new SessionTokenCleanupWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            time,
            NullLogger<SessionTokenCleanupWorker>.Instance);

        return (db, worker, time);
    }

    private static RefreshToken Refresh(string name, DateTime expiresAt, DateTime? revokedAt) => new()
    {
        UserId = 1,
        TokenHash = "refresh-" + name,
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt,
        CreatedAt = expiresAt.AddDays(-30),
    };

    private static PasswordResetToken Reset(string name, DateTime expiresAt, DateTime? usedAt) => new()
    {
        UserId = 1,
        TokenHash = "reset-" + name,
        ExpiresAt = expiresAt,
        UsedAt = usedAt,
        CreatedAt = expiresAt.AddHours(-1),
    };

    [Fact]
    public async Task RunCycle_DeletesOnlyTokensDeadForLongerThanRetention()
    {
        var (db, worker, _) = CreateWorker();
        var future = Now.AddDays(10).UtcDateTime;

        db.RefreshTokens.AddRange(
            Refresh("old-revoked", future, Now.AddDays(-40).UtcDateTime),
            Refresh("old-expired", Now.AddDays(-40).UtcDateTime, null),
            Refresh("recent-revoked", future, Now.AddDays(-5).UtcDateTime),
            Refresh("recent-expired", Now.AddDays(-5).UtcDateTime, null),
            Refresh("active", future, null));

        db.PasswordResetTokens.AddRange(
            Reset("old-used", Now.AddDays(-39).UtcDateTime, Now.AddDays(-40).UtcDateTime),
            Reset("old-expired", Now.AddDays(-40).UtcDateTime, null),
            Reset("recent-used", Now.AddDays(1).UtcDateTime, Now.AddDays(-2).UtcDateTime),
            Reset("active", Now.AddHours(1).UtcDateTime, null));

        db.SaveChanges();

        var result = await worker.RunCycleAsync();

        Assert.Equal(2, result.RefreshTokensDeleted);
        Assert.Equal(2, result.PasswordResetTokensDeleted);

        var refreshHashes = await db.RefreshTokens.Select(r => r.TokenHash).ToListAsync();
        Assert.Equal(
            new[] { "refresh-active", "refresh-recent-expired", "refresh-recent-revoked" }.OrderBy(x => x),
            refreshHashes.OrderBy(x => x));

        var resetHashes = await db.PasswordResetTokens.Select(t => t.TokenHash).ToListAsync();
        Assert.Equal(
            new[] { "reset-active", "reset-recent-used" }.OrderBy(x => x),
            resetHashes.OrderBy(x => x));
    }

    [Fact]
    public async Task RunCycle_RecordsOneSummarySecurityEvent_OnlyWhenRowsWereDeleted()
    {
        var (db, worker, time) = CreateWorker();
        db.RefreshTokens.Add(Refresh("old-expired", Now.AddDays(-40).UtcDateTime, null));
        db.SaveChanges();

        await worker.RunCycleAsync();

        var events = await db.SecurityAuditEvents
            .Where(e => e.EventCode == SecurityEventCodes.SessionTokensPurged)
            .ToListAsync();
        var single = Assert.Single(events);
        Assert.Equal(SecurityEventOutcome.Success, single.Outcome);
        Assert.Contains("\"refreshTokensDeleted\":1", single.Metadata);

        // A second pass with nothing left to delete writes no further event.
        time.Now = Now.AddDays(1);
        await worker.RunCycleAsync();
        Assert.Single(await db.SecurityAuditEvents
            .Where(e => e.EventCode == SecurityEventCodes.SessionTokensPurged)
            .ToListAsync());
    }

    [Fact]
    public async Task RunCycle_LeavesSecurityAndAuditEvidenceUntouched()
    {
        var (db, worker, _) = CreateWorker();
        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            OccurredAtUtc = Now.AddDays(-400).UtcDateTime,
            EventCode = SecurityEventCodes.LoginSuccess,
            Outcome = SecurityEventOutcome.Success,
            Source = "Api",
        });
        db.RefreshTokens.Add(Refresh("old-revoked", Now.AddDays(10).UtcDateTime, Now.AddDays(-40).UtcDateTime));
        db.SaveChanges();

        await worker.RunCycleAsync();

        // The pre-existing evidence row survives; the only new row is the summary event.
        Assert.Equal(1, await db.SecurityAuditEvents.CountAsync(e => e.EventCode == SecurityEventCodes.LoginSuccess));
        Assert.Equal(0, await db.RefreshTokens.CountAsync());
    }
}
