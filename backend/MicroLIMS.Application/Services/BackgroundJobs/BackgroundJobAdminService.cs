using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.BackgroundJobs;

// Implemented by a worker whose single pass an administrator may trigger. Only
// Housekeeping workers implement it; the same method is what the schedule calls.
public interface IBackgroundJobWork
{
    string JobName { get; }
    // One pass, without recording or locking: the recorder wraps it. Returns items processed.
    Task<int?> RunOnceAsync(CancellationToken cancellationToken);
}

public sealed record BackgroundJobRunDto(
    long Id,
    string JobName,
    DateTime StartedAt,
    DateTime? FinishedAt,
    string Outcome,
    int? ItemsProcessed,
    string? Error,
    string TriggeredBy,
    int? TriggeredByUserId,
    string? Reason);

public sealed record BackgroundJobStatusDto(
    string Name,
    string Description,
    string Classification,
    bool CanRunManually,
    string ClassificationReason,
    string Schedule,
    BackgroundJobRunDto? LastRun,
    DateTime? LastSuccessAt,
    DateTime? LastFailureAt,
    // Last pass finished in this process, including idle passes that leave no row.
    DateTime? LastPassFinishedAt,
    bool RunningNow);

public sealed record RunBackgroundJobRequest(string? Reason);

public enum ManualJobRunStatus { Completed, NotAllowed, AlreadyRunning }

public sealed record ManualJobRunResult(ManualJobRunStatus Status, string Message, BackgroundJobRunDto? Run);

public interface IBackgroundJobAdminService
{
    Task<List<BackgroundJobStatusDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<BackgroundJobRunDto>> GetRunsAsync(string jobName, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ManualJobRunResult> RunManuallyAsync(string jobName, int userId, string? reason, CancellationToken cancellationToken = default);
}

public sealed class BackgroundJobAdminService : IBackgroundJobAdminService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IBackgroundJobRunRecorder _recorder;
    private readonly IEnumerable<IBackgroundJobWork> _work;
    private readonly ISecurityAuditService _securityAudit;

    public BackgroundJobAdminService(
        IMicroLimsDbContext db,
        IBackgroundJobRunRecorder recorder,
        IEnumerable<IBackgroundJobWork> work,
        ISecurityAuditService securityAudit)
    {
        _db = db;
        _recorder = recorder;
        _work = work;
        _securityAudit = securityAudit;
    }

    public async Task<List<BackgroundJobStatusDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<BackgroundJobStatusDto>();
        foreach (var job in BackgroundJobCatalog.All)
        {
            var runs = _db.BackgroundJobRuns.AsNoTracking().Where(r => r.JobName == job.Name);
            var last = await runs.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var lastSuccess = await runs.Where(r => r.Outcome == BackgroundJobOutcome.Succeeded)
                .OrderByDescending(r => r.StartedAt).Select(r => r.FinishedAt ?? r.StartedAt)
                .Cast<DateTime?>().FirstOrDefaultAsync(cancellationToken);
            var lastFailure = await runs.Where(r => r.Outcome == BackgroundJobOutcome.Failed)
                .OrderByDescending(r => r.StartedAt).Select(r => r.FinishedAt ?? r.StartedAt)
                .Cast<DateTime?>().FirstOrDefaultAsync(cancellationToken);

            result.Add(new BackgroundJobStatusDto(
                job.Name, job.Description, job.Classification.ToString(),
                CanRunManually(job), job.ClassificationReason, job.Schedule,
                last is null ? null : ToDto(last), lastSuccess, lastFailure,
                _recorder.LastPassFinishedAt(job.Name), _recorder.IsRunning(job.Name)));
        }
        return result;
    }

    public async Task<PagedResult<BackgroundJobRunDto>> GetRunsAsync(
        string jobName, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var job = BackgroundJobCatalog.Find(jobName) ?? throw new NotFoundException("Background job not found.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.BackgroundJobRuns.AsNoTracking().Where(r => r.JobName == job.Name);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<BackgroundJobRunDto>
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ManualJobRunResult> RunManuallyAsync(
        string jobName, int userId, string? rawReason, CancellationToken cancellationToken = default)
    {
        var job = BackgroundJobCatalog.Find(jobName) ?? throw new NotFoundException("Background job not found.");
        var reason = AdminActionReason.Require(rawReason, "run a background job manually");

        if (job.Classification != BackgroundJobClassification.Housekeeping)
            return new ManualJobRunResult(ManualJobRunStatus.NotAllowed,
                $"{job.Name} changes GxP state and runs on its schedule only; it cannot be started manually.", null);

        var work = _work.FirstOrDefault(w => string.Equals(w.JobName, job.Name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{job.Name} has no runnable implementation.");

        if (_recorder.IsRunning(job.Name))
            return new ManualJobRunResult(ManualJobRunStatus.AlreadyRunning,
                $"{job.Name} is already running. Try again when it has finished.", null);

        // Audit first and commit, so the request is evidenced even if the job then fails.
        _securityAudit.Record(new SecurityEventRequest(
            EventCode: SecurityEventCodes.BackgroundJobManuallyTriggered,
            Outcome: SecurityEventOutcome.Success,
            ActorUserId: userId,
            Reason: reason,
            Metadata: JsonSerializer.Serialize(new { job = job.Name })));
        await _db.SaveChangesAsync(cancellationToken);

        var run = await _recorder.RunAsync(
            job.Name, BackgroundJobTrigger.Manual, userId, reason, work.RunOnceAsync, cancellationToken);

        if (run.AlreadyRunning)
            return new ManualJobRunResult(ManualJobRunStatus.AlreadyRunning,
                $"{job.Name} is already running. Try again when it has finished.", null);

        BackgroundJobRunDto? dto = null;
        if (run.RunId is long id)
        {
            var row = await _db.BackgroundJobRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (row is not null) dto = ToDto(row);
        }
        return new ManualJobRunResult(ManualJobRunStatus.Completed,
            run.Outcome == BackgroundJobOutcome.Succeeded ? $"{job.Name} finished." : $"{job.Name} failed: {run.Error}", dto);
    }

    private static bool CanRunManually(BackgroundJobDefinition job) =>
        job.Classification == BackgroundJobClassification.Housekeeping;

    private static BackgroundJobRunDto ToDto(BackgroundJobRun r) => new(
        r.Id, r.JobName, r.StartedAt, r.FinishedAt, r.Outcome.ToString(), r.ItemsProcessed, r.Error,
        r.TriggeredBy.ToString(), r.TriggeredByUserId, r.Reason);
}
