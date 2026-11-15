using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.Notifications;

/// <summary>
/// Claims due rows from <see cref="EmailOutbox"/> using Postgres row-level locking
/// (<c>FOR UPDATE SKIP LOCKED</c>) and a short-lived lease on <c>NextAttemptAt</c>,
/// delivers them via <see cref="IEmailSender"/> outside any database transaction (Rule 5.4),
/// and records delivery outcomes in separate saves with bounded exponential backoff.
/// </summary>
public sealed class EmailOutboxSender
{
    public const int MaxAttempts = 5;
    public const int MaxErrorLength = 500;
    public static readonly TimeSpan ClaimLeaseDuration = TimeSpan.FromMinutes(5);
    public static readonly int[] RetryDelaysMinutes = [1, 5, 15, 60, 240];

    private readonly IMicroLimsDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _time;
    private readonly ILogger<EmailOutboxSender>? _logger;

    public EmailOutboxSender(
        IMicroLimsDbContext db,
        IEmailSender emailSender,
        TimeProvider? timeProvider = null,
        ILogger<EmailOutboxSender>? logger = null)
    {
        _db = db;
        _emailSender = emailSender;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public async Task<int> SendDueAsync(int max = 50, CancellationToken ct = default)
    {
        if (max <= 0)
        {
            return 0;
        }

        var claimNow = _time.GetUtcNow().UtcDateTime;
        var claimed = await ClaimBatchAsync(claimNow, max, ct);
        if (claimed.Count == 0)
        {
            return 0;
        }

        var sentCount = 0;

        foreach (var row in claimed)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _emailSender.SendAsync(row.ToAddress, row.Subject, row.Body);
                await MarkSentAsync(row.Id, ct);
                sentCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Failed to deliver EmailOutbox row {OutboxId} (attempt {Attempt}).", row.Id, row.Attempts + 1);
                await MarkFailedAttemptAsync(row.Id, ex, ct);
            }
        }

        return sentCount;
    }

    private async Task<List<EmailOutbox>> ClaimBatchAsync(DateTime now, int max, CancellationToken ct)
    {
        var leaseUntil = now.Add(ClaimLeaseDuration);
        var efDb = _db as DbContext;

        if (efDb is not null && efDb.Database.IsRelational())
        {
            await using var tx = await efDb.Database.BeginTransactionAsync(ct);

            // Claim approach: SELECT ... FOR UPDATE SKIP LOCKED prevents concurrent workers
            // from locking the same pending rows while Tx is open, and extending NextAttemptAt
            // by ClaimLeaseDuration (5 min) inside the same transaction ensures that once Tx
            // commits (releasing row locks before any SMTP call), another sender starting
            // immediately afterward still sees NextAttemptAt > now and will not re-claim them.
            List<int> claimedIds;
            if (efDb.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                claimedIds = await efDb.Database
                    .SqlQuery<int>($@"
                        SELECT ""Id"" AS ""Value""
                        FROM ""EmailOutbox""
                        WHERE ""SentAt"" IS NULL
                          AND ""FailedAt"" IS NULL
                          AND ""NextAttemptAt"" <= {now}
                        ORDER BY ""NextAttemptAt"", ""Id""
                        LIMIT {max}
                        FOR UPDATE SKIP LOCKED")
                    .ToListAsync(ct);
            }
            else
            {
                claimedIds = await efDb.Set<EmailOutbox>()
                    .AsNoTracking()
                    .Where(e => e.SentAt == null && e.FailedAt == null && e.NextAttemptAt <= now)
                    .OrderBy(e => e.NextAttemptAt)
                    .ThenBy(e => e.Id)
                    .Select(e => e.Id)
                    .Take(max)
                    .ToListAsync(ct);
            }

            List<EmailOutbox> rows = new();
            if (claimedIds.Count > 0)
            {
                await efDb.Set<EmailOutbox>()
                    .Where(e => claimedIds.Contains(e.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAt, leaseUntil), ct);

                rows = await efDb.Set<EmailOutbox>()
                    .AsNoTracking()
                    .Where(e => claimedIds.Contains(e.Id))
                    .OrderBy(e => e.NextAttemptAt)
                    .ThenBy(e => e.Id)
                    .ToListAsync(ct);
            }

            await tx.CommitAsync(ct);
            efDb.ChangeTracker.Clear();
            return rows;
        }

        var inMemoryRows = await _db.EmailOutbox
            .Where(e => e.SentAt == null && e.FailedAt == null && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .ThenBy(e => e.Id)
            .Take(max)
            .ToListAsync(ct);

        if (inMemoryRows.Count > 0)
        {
            foreach (var row in inMemoryRows)
            {
                row.NextAttemptAt = leaseUntil;
            }

            await _db.SaveChangesAsync(ct);
        }

        return inMemoryRows;
    }

    private async Task MarkSentAsync(int outboxId, CancellationToken ct)
    {
        var sentAt = _time.GetUtcNow().UtcDateTime;
        var entity = await _db.EmailOutbox.FirstOrDefaultAsync(e => e.Id == outboxId, ct);
        if (entity is null)
        {
            return;
        }

        entity.SentAt = sentAt;
        entity.LastError = null;

        if (entity.NotificationLogId is { } logId)
        {
            var log = await _db.NotificationLogs.FirstOrDefaultAsync(n => n.Id == logId, ct);
            if (log is not null)
            {
                log.EmailSent = true;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAttemptAsync(int outboxId, Exception ex, CancellationToken ct)
    {
        var failedAt = _time.GetUtcNow().UtcDateTime;
        var entity = await _db.EmailOutbox.FirstOrDefaultAsync(e => e.Id == outboxId, ct);
        if (entity is null)
        {
            return;
        }

        var attempts = entity.Attempts + 1;
        entity.Attempts = attempts;

        var rawMessage = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
        entity.LastError = rawMessage.Length > MaxErrorLength ? rawMessage[..MaxErrorLength] : rawMessage;

        var delayIndex = Math.Clamp(attempts - 1, 0, RetryDelaysMinutes.Length - 1);
        entity.NextAttemptAt = failedAt.AddMinutes(RetryDelaysMinutes[delayIndex]);

        if (attempts >= MaxAttempts)
        {
            entity.FailedAt = failedAt;
        }

        await _db.SaveChangesAsync(ct);
    }
}
