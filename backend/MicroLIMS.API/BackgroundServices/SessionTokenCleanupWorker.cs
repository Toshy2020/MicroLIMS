using MicroLIMS.Application.Services.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.API.BackgroundServices;

// Deletes session and password-reset tokens that are long dead.
//
// Only the two token tables are touched. RefreshTokens and
// PasswordResetTokens are referenced by nothing else (no foreign key points
// at them), so deleting a row cannot orphan or cascade into other data.
// AuditLogs, SecurityAuditEvents, LoginHistories and ElectronicSignatures
// are never queried or modified here.
//
// A token is eligible once it has been dead for at least RetentionDays:
//   RefreshToken:        revoked more than N days ago, or expired more than N days ago.
//   PasswordResetToken:  used more than N days ago, or expired more than N days ago.
// Keeping the window gives support staff a short period in which a recently
// revoked session can still be explained from its row.
public class SessionTokenCleanupWorker : BackgroundService, IBackgroundJobWork
{
    public const string ProcessName = "SessionTokenCleanupWorker";

    public const int DefaultIntervalHours = 24;
    public const int DefaultRetentionDays = 30;

    // Deleted in batches so a large first run cannot hold one long
    // transaction open against the application database.
    private const int DeleteBatchSize = 500;
    private const int MaxBatchesPerCycle = 40;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly ILogger<SessionTokenCleanupWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLock = new(1, 1);
    private readonly IBackgroundJobRunRecorder? _recorder;

    public SessionTokenCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        TimeProvider time,
        ILogger<SessionTokenCleanupWorker> logger,
        IBackgroundJobRunRecorder? recorder = null)
    {
        _recorder = recorder;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _time = time;
        _logger = logger;
    }

    private bool Enabled =>
        _configuration.GetValue<bool?>("SessionTokenCleanup:Enabled") ?? true;

    private int IntervalHours =>
        Positive(_configuration.GetValue<int?>("SessionTokenCleanup:IntervalHours"), DefaultIntervalHours);

    private int RetentionDays =>
        Positive(_configuration.GetValue<int?>("SessionTokenCleanup:RetentionDays"), DefaultRetentionDays);

    private static int Positive(int? configured, int fallback) =>
        configured is > 0 ? configured.Value : fallback;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            _logger.LogInformation(
                "[{Worker}] Disabled via SessionTokenCleanup:Enabled - no session tokens will be pruned.", ProcessName);
            return;
        }

        _logger.LogInformation(
            "[{Worker}] Starting. Every {Hours}h; dead tokens kept {Days} days.",
            ProcessName, IntervalHours, RetentionDays);

        // Startup cycle, so a host that sleeps between requests still prunes.
        try
        {
            await ScheduledPassAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Worker}] Unhandled error during startup cycle.", ProcessName);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                    await ScheduledPassAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Worker}] Unhandled exception during cleanup cycle.", ProcessName);
            }
        }

        _logger.LogInformation("[{Worker}] Background service has stopped gracefully.", ProcessName);
    }

    public string JobName => ProcessName;

    // The pass an administrator's manual run executes: exactly the scheduled one.
    public async Task<int?> RunOnceAsync(CancellationToken cancellationToken) =>
        (await RunCycleAsync(cancellationToken)).Total;

    private Task ScheduledPassAsync(CancellationToken cancellationToken) =>
        ScheduledJobPass.RunAsync(_recorder, ProcessName, RunOnceAsync, cancellationToken);

    // Runs one cleanup pass and returns the number of token rows deleted.
    public async Task<SessionTokenCleanupResult> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("[{Worker}] Previous cleanup cycle still running. Skipping tick.", ProcessName);
            return new SessionTokenCleanupResult(0, 0);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            db.CurrentSystemProcessName = ProcessName;

            var cutoff = _time.GetUtcNow().UtcDateTime.AddDays(-RetentionDays);

            var refreshTokens = await DeleteInBatchesAsync(
                db,
                () => db.RefreshTokens.Where(r =>
                    (r.RevokedAt != null && r.RevokedAt < cutoff) || r.ExpiresAt < cutoff),
                cancellationToken);

            var resetTokens = await DeleteInBatchesAsync(
                db,
                () => db.PasswordResetTokens.Where(t =>
                    (t.UsedAt != null && t.UsedAt < cutoff) || t.ExpiresAt < cutoff),
                cancellationToken);

            var result = new SessionTokenCleanupResult(refreshTokens, resetTokens);

            if (result.Total > 0)
            {
                // One summary event per pass. It carries counts only - never
                // token values or hashes.
                var securityAudit = scope.ServiceProvider.GetRequiredService<ISecurityAuditService>();
                securityAudit.Record(new SecurityEventRequest(
                    EventCode: SecurityEventCodes.SessionTokensPurged,
                    Outcome: SecurityEventOutcome.Success,
                    ActorIsSystem: true,
                    Reason: $"Purged {refreshTokens} refresh token(s) and {resetTokens} password reset token(s) dead for more than {RetentionDays} day(s).",
                    Metadata: $"{{\"refreshTokensDeleted\":{refreshTokens},\"passwordResetTokensDeleted\":{resetTokens},\"retentionDays\":{RetentionDays}}}"));
                await db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "[{Worker}] Cleanup pass complete. RefreshTokens deleted: {Refresh}, PasswordResetTokens deleted: {Reset}.",
                ProcessName, refreshTokens, resetTokens);

            return result;
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    // Loads and removes matching rows in fixed-size batches. The predicate is
    // re-evaluated for each batch, so the loop terminates once nothing matches.
    private static async Task<int> DeleteInBatchesAsync<TEntity>(
        IMicroLimsDbContext db,
        Func<IQueryable<TEntity>> matching,
        CancellationToken cancellationToken) where TEntity : class
    {
        var deleted = 0;

        for (var batch = 0; batch < MaxBatchesPerCycle; batch++)
        {
            var rows = await matching()
                .Take(DeleteBatchSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0) break;

            db.Set<TEntity>().RemoveRange(rows);
            await db.SaveChangesAsync(cancellationToken);

            deleted += rows.Count;

            if (rows.Count < DeleteBatchSize) break;
        }

        return deleted;
    }
}

public sealed record SessionTokenCleanupResult(int RefreshTokensDeleted, int PasswordResetTokensDeleted)
{
    public int Total => RefreshTokensDeleted + PasswordResetTokensDeleted;
}
