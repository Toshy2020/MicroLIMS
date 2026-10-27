using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.BackgroundJobs;

public sealed record BackgroundJobRunResult(
    long? RunId,
    BackgroundJobOutcome Outcome,
    int? ItemsProcessed,
    string? Error,
    // True when another pass of the same job was already running, so this one did not execute.
    bool AlreadyRunning = false);

// Wraps one pass of a background worker: records start, finish, outcome, item
// count and error, and refuses to run the same job twice at once.
public interface IBackgroundJobRunRecorder
{
    // Runs body and records the pass. A failing body is recorded as Failed and
    // NOT rethrown (the worker loop must survive). Cancellation is recorded and rethrown.
    Task<BackgroundJobRunResult> RunAsync(
        string jobName,
        BackgroundJobTrigger trigger,
        int? triggeredByUserId,
        string? reason,
        Func<CancellationToken, Task<int?>> body,
        CancellationToken cancellationToken);

    bool IsRunning(string jobName);

    // When a pass of the job last finished in this process, recorded or not.
    DateTime? LastPassFinishedAt(string jobName);
}

public sealed class BackgroundJobRunRecorder : IBackgroundJobRunRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<BackgroundJobRunRecorder> _logger;
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastPass = new(StringComparer.OrdinalIgnoreCase);

    public BackgroundJobRunRecorder(
        IServiceScopeFactory scopeFactory, TimeProvider time, ILogger<BackgroundJobRunRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _time = time;
        _logger = logger;
    }

    public bool IsRunning(string jobName) => _running.ContainsKey(jobName);

    public DateTime? LastPassFinishedAt(string jobName) =>
        _lastPass.TryGetValue(jobName, out var at) ? at : null;

    public async Task<BackgroundJobRunResult> RunAsync(
        string jobName,
        BackgroundJobTrigger trigger,
        int? triggeredByUserId,
        string? reason,
        Func<CancellationToken, Task<int?>> body,
        CancellationToken cancellationToken)
    {
        var startedAt = _time.GetUtcNow().UtcDateTime;

        if (!_running.TryAdd(jobName, 0))
        {
            const string message = "A previous pass of this job is still running.";
            _logger.LogWarning("[{Job}] {Message} Skipping.", jobName, message);
            await TryInsertFinishedAsync(
                jobName, trigger, triggeredByUserId, reason, startedAt, BackgroundJobOutcome.Skipped, null, message);
            return new BackgroundJobRunResult(null, BackgroundJobOutcome.Skipped, null, message, AlreadyRunning: true);
        }

        try
        {
            // Idle high-frequency scheduled passes are not recorded unless they fail or do something.
            var recordStart = trigger == BackgroundJobTrigger.Manual
                || (BackgroundJobCatalog.Find(jobName)?.RecordIdleScheduledPasses ?? true);

            long? runId = null;
            if (recordStart)
                runId = await TryStartAsync(jobName, trigger, triggeredByUserId, reason, startedAt);

            int? items = null;
            string? error = null;
            var outcome = BackgroundJobOutcome.Succeeded;
            OperationCanceledException? cancelled = null;
            try
            {
                items = await body(cancellationToken);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = ex;
                outcome = BackgroundJobOutcome.Failed;
                error = "Cancelled before the pass finished (host shutting down or request aborted).";
            }
            catch (Exception ex)
            {
                outcome = BackgroundJobOutcome.Failed;
                error = Truncate($"{ex.GetType().Name}: {ex.Message}");
                _logger.LogError(ex, "[{Job}] Pass failed; it will run again on the next tick.", jobName);
            }

            var finishedAt = _time.GetUtcNow().UtcDateTime;
            _lastPass[jobName] = finishedAt;

            if (runId is not null)
                await TryFinishAsync(runId.Value, finishedAt, outcome, items, error);
            else if (outcome == BackgroundJobOutcome.Failed || items is > 0)
                runId = await TryInsertFinishedAsync(
                    jobName, trigger, triggeredByUserId, reason, startedAt, outcome, items, error, finishedAt);

            if (cancelled is not null) throw cancelled;
            return new BackgroundJobRunResult(runId, outcome, items, error);
        }
        finally
        {
            _running.TryRemove(jobName, out _);
        }
    }

    internal static string Truncate(string text)
    {
        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= BackgroundJobRun.MaxErrorLength
            ? flat
            : flat[..(BackgroundJobRun.MaxErrorLength - 1)] + "…";
    }

    private static string? TrimReason(string? reason) =>
        reason is null ? null : reason.Length <= BackgroundJobRun.MaxReasonLength ? reason : reason[..BackgroundJobRun.MaxReasonLength];

    // The recorder must never stop a job from running: its own storage failures are logged and swallowed.
    private async Task<long?> TryStartAsync(
        string jobName, BackgroundJobTrigger trigger, int? userId, string? reason, DateTime startedAt)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();

            // The in-process gate is held, so a Running row for this job can only be left over from a
            // host that stopped mid-pass. Close it once (its single finishing update).
            var orphans = await db.BackgroundJobRuns
                .Where(r => r.JobName == jobName && r.Outcome == BackgroundJobOutcome.Running)
                .ToListAsync(CancellationToken.None);
            foreach (var orphan in orphans)
            {
                orphan.Outcome = BackgroundJobOutcome.Failed;
                orphan.FinishedAt = startedAt;
                orphan.Error = "Interrupted: the host stopped before this pass finished.";
            }

            var run = new BackgroundJobRun
            {
                JobName = jobName,
                StartedAt = startedAt,
                Outcome = BackgroundJobOutcome.Running,
                TriggeredBy = trigger,
                TriggeredByUserId = userId,
                Reason = TrimReason(reason)
            };
            db.BackgroundJobRuns.Add(run);
            await db.SaveChangesAsync(CancellationToken.None);
            return run.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Job}] Could not record the start of the pass.", jobName);
            return null;
        }
    }

    private async Task TryFinishAsync(
        long runId, DateTime finishedAt, BackgroundJobOutcome outcome, int? items, string? error)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            var run = await db.BackgroundJobRuns.FirstAsync(r => r.Id == runId, CancellationToken.None);
            run.FinishedAt = finishedAt;
            run.Outcome = outcome;
            run.ItemsProcessed = items;
            run.Error = error;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record the end of background job run {RunId}.", runId);
        }
    }

    private async Task<long?> TryInsertFinishedAsync(
        string jobName, BackgroundJobTrigger trigger, int? userId, string? reason, DateTime startedAt,
        BackgroundJobOutcome outcome, int? items, string? error, DateTime? finishedAt = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            var run = new BackgroundJobRun
            {
                JobName = jobName,
                StartedAt = startedAt,
                FinishedAt = finishedAt ?? startedAt,
                Outcome = outcome,
                ItemsProcessed = items,
                Error = error,
                TriggeredBy = trigger,
                TriggeredByUserId = userId,
                Reason = TrimReason(reason)
            };
            db.BackgroundJobRuns.Add(run);
            await db.SaveChangesAsync(CancellationToken.None);
            return run.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Job}] Could not record the pass.", jobName);
            return null;
        }
    }
}
