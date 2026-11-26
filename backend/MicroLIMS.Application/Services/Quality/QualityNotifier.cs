using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Quality;

public interface IQualityNotifier
{
    Task RaisedAsync(int recordId, CancellationToken ct = default);
    Task AssignedAsync(int recordId, CancellationToken ct = default);
    Task ReturnedAsync(int recordId, CancellationToken ct = default);
    Task AwaitingQaAsync(int recordId, CancellationToken ct = default);
    Task ActionAssignedAsync(int recordId, IReadOnlyList<int> actionIds, CancellationToken ct = default);
    Task ClosedAsync(int recordId, CancellationToken ct = default);
}

public sealed class QualityNotifier(IServiceScopeFactory scopes, ILogger<QualityNotifier>? logger = null) : IQualityNotifier
{
    public Task RaisedAsync(int recordId, CancellationToken ct = default) =>
        SafeSendAsync("Raised", recordId, async (db, recipients, publisher, token) =>
        {
            var record = await LoadRecordAsync(db, recordId, token);
            if (record is null) return;
            var userIds = (await recipients.ResolveAsync(record.SectionId, PermissionConstants.QualityApprove, token)).ToHashSet();
            userIds.UnionWith(await recipients.ResolveAsync(record.SectionId, PermissionConstants.QualityInvestigate, token));
            if (record.OwnerUserId is int ownerId) userIds.Add(ownerId);
            if (record.RaisedByUserId is int raiserId) userIds.Remove(raiserId);

            await publisher.PublishAsync(new NotificationRequest(
                "QualityRecordRaised",
                userIds.ToList(),
                $"{Label(record.Type)} {record.Number} ({record.Title}) was raised.",
                "quality-record",
                record.Id,
                record.SectionId,
                $"quality:{record.Id}:raised"), token);
            await db.SaveChangesAsync(token);
        }, ct);

    public Task AssignedAsync(int recordId, CancellationToken ct = default) =>
        SafeSendAsync("Assigned", recordId, async (db, _, publisher, token) =>
        {
            var record = await LoadRecordAsync(db, recordId, token);
            if (record?.OwnerUserId is not int ownerId) return;

            await publisher.PublishAsync(new NotificationRequest(
                "QualityRecordAssigned",
                [ownerId],
                $"{Label(record.Type)} {record.Number} ({record.Title}) was assigned to you.",
                "quality-record",
                record.Id,
                record.SectionId,
                $"quality:{record.Id}:assigned:{ownerId}"), token);
            await db.SaveChangesAsync(token);
        }, ct);

    public Task ReturnedAsync(int recordId, CancellationToken ct = default) =>
        SafeSendAsync("Returned", recordId, async (db, _, publisher, token) =>
        {
            var record = await LoadRecordAsync(db, recordId, token);
            if (record?.OwnerUserId is not int ownerId) return;

            await publisher.PublishAsync(new NotificationRequest(
                "QualityRecordReturned",
                [ownerId],
                $"{Label(record.Type)} {record.Number} ({record.Title}) was returned for revision.",
                "quality-record",
                record.Id,
                record.SectionId,
                null), token);
            await db.SaveChangesAsync(token);
        }, ct);

    public Task AwaitingQaAsync(int recordId, CancellationToken ct = default) =>
        SafeSendAsync("AwaitingQa", recordId, async (db, recipients, publisher, token) =>
        {
            var record = await LoadRecordAsync(db, recordId, token);
            if (record is null) return;
            var userIds = (await recipients.ResolveAsync(record.SectionId, PermissionConstants.QualityApprove, token)).ToHashSet();
            if (record.RaisedByUserId is int raiserId) userIds.Remove(raiserId);
            if (record.OwnerUserId is int ownerId) userIds.Remove(ownerId);

            await publisher.PublishAsync(new NotificationRequest(
                "QualityAwaitingQa",
                userIds.ToList(),
                $"{Label(record.Type)} {record.Number} ({record.Title}) is awaiting QA decision.",
                "quality-record",
                record.Id,
                record.SectionId,
                null), token);
            await db.SaveChangesAsync(token);
        }, ct);

    public Task ActionAssignedAsync(int recordId, IReadOnlyList<int> actionIds, CancellationToken ct = default) =>
        SafeSendAsync("ActionAssigned", recordId, async (db, _, publisher, token) =>
        {
            if (actionIds.Count == 0) return;
            var record = await LoadRecordAsync(db, recordId, token);
            if (record is null) return;

            var actions = await db.QualityActions.AsNoTracking()
                .Where(a => a.RecordId == recordId && actionIds.Contains(a.Id))
                .Select(a => new { a.Id, a.ResponsibleUserId, a.Description, a.DueDate })
                .ToListAsync(token);

            foreach (var action in actions)
            {
                await publisher.PublishAsync(new NotificationRequest(
                    "QualityActionAssigned",
                    [action.ResponsibleUserId],
                    $"Action assigned on {Label(record.Type)} {record.Number}: {action.Description} (due {action.DueDate:yyyy-MM-dd}).",
                    "quality-record",
                    record.Id,
                    record.SectionId,
                    $"quality:{record.Id}:action:{action.Id}:assigned:{action.ResponsibleUserId}"), token);
            }
            await db.SaveChangesAsync(token);
        }, ct);

    public Task ClosedAsync(int recordId, CancellationToken ct = default) =>
        SafeSendAsync("Closed", recordId, async (db, recipients, publisher, token) =>
        {
            var record = await LoadRecordAsync(db, recordId, token);
            if (record is null) return;
            var userIds = (await recipients.ResolveAsync(record.SectionId, PermissionConstants.QualityApprove, token)).ToHashSet();
            userIds.UnionWith(await recipients.ResolveAsync(record.SectionId, PermissionConstants.QualityInvestigate, token));
            if (record.RaisedByUserId is int raiserId) userIds.Add(raiserId);
            if (record.OwnerUserId is int ownerId) userIds.Add(ownerId);

            await publisher.PublishAsync(new NotificationRequest(
                "QualityRecordClosed",
                userIds.ToList(),
                $"{Label(record.Type)} {record.Number} ({record.Title}) was closed.",
                "quality-record",
                record.Id,
                record.SectionId,
                $"quality:{record.Id}:closed"), token);
            await db.SaveChangesAsync(token);
        }, ct);

    public async Task<int> NotifyOverdueAsync(DateOnly labDate, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
        var recipients = scope.ServiceProvider.GetRequiredService<IPermissionRecipientResolver>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        var open = db.QualityRecords.AsNoTracking().Where(QualityConfigService.IsOpenExpression);

        var overdueRecords = await open
            .Where(r => r.DueDate != null && r.DueDate < labDate)
            .Select(r => new { r.Id, r.Number, r.Type, r.Title, r.SectionId, r.OwnerUserId, DueDate = r.DueDate!.Value })
            .ToListAsync(ct);

        var overdueActions = await open
            .SelectMany(r => r.Actions.Where(a => a.DoneAtUtc == null && a.DueDate < labDate),
                (r, a) => new
                {
                    RecordId = r.Id, r.Number, r.Type, r.SectionId, r.OwnerUserId,
                    ActionId = a.Id, a.Description, a.ResponsibleUserId, a.DueDate
                })
            .ToListAsync(ct);

        var count = 0;
        var qaCache = new Dictionary<int, IReadOnlyList<int>>();

        async Task<IReadOnlyList<int>> GetQaAsync(int? sectionId)
        {
            var key = sectionId ?? -1;
            if (!qaCache.TryGetValue(key, out var list))
            {
                list = await recipients.ResolveAsync(sectionId, PermissionConstants.QualityApprove, ct);
                qaCache[key] = list;
            }
            return list;
        }

        foreach (var rec in overdueRecords)
        {
            var userIds = (await GetQaAsync(rec.SectionId)).ToHashSet();
            if (rec.OwnerUserId is int ownerId) userIds.Add(ownerId);
            if (userIds.Count == 0) continue;

            await publisher.PublishAsync(new NotificationRequest(
                "QualityRecordOverdue",
                userIds.ToList(),
                $"{Label(rec.Type)} {rec.Number} ({rec.Title}) is overdue (due {rec.DueDate:yyyy-MM-dd}).",
                "quality-record",
                rec.Id,
                rec.SectionId,
                $"quality:overdue:record:{rec.Id}:{labDate:yyyy-MM-dd}"), ct);
            count++;
        }

        foreach (var act in overdueActions)
        {
            var userIds = (await GetQaAsync(act.SectionId)).ToHashSet();
            userIds.Add(act.ResponsibleUserId);
            if (act.OwnerUserId is int ownerId) userIds.Add(ownerId);

            await publisher.PublishAsync(new NotificationRequest(
                "QualityActionOverdue",
                userIds.ToList(),
                $"Action on {Label(act.Type)} {act.Number} is overdue (due {act.DueDate:yyyy-MM-dd}): {act.Description}",
                "quality-record",
                act.RecordId,
                act.SectionId,
                $"quality:overdue:action:{act.ActionId}:{labDate:yyyy-MM-dd}"), ct);
            count++;
        }

        if (count > 0)
            await db.SaveChangesAsync(ct);

        return count;
    }

    private async Task SafeSendAsync(string eventName, int recordId,
        Func<IMicroLimsDbContext, IPermissionRecipientResolver, INotificationPublisher, CancellationToken, Task> send,
        CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            var recipients = scope.ServiceProvider.GetRequiredService<IPermissionRecipientResolver>();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
            await send(db, recipients, publisher, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to publish quality notification {EventName} for record {RecordId}.", eventName, recordId);
        }
    }

    private static Task<RecordHeader?> LoadRecordAsync(IMicroLimsDbContext db, int recordId, CancellationToken ct) =>
        db.QualityRecords.AsNoTracking()
            .Where(r => r.Id == recordId)
            .Select(r => new RecordHeader(r.Id, r.Number, r.Type, r.Title, r.SectionId, r.RaisedByUserId, r.OwnerUserId))
            .SingleOrDefaultAsync(ct);

    private static string Label(QualityRecordType type) => ReportVocabulary.Humanize(type.ToString());

    internal static async Task SendPostCommitAsync(IQualityNotifier? notifier, Func<IQualityNotifier, Task> send,
        ILogger? logger, int recordId)
    {
        if (notifier is null) return;
        try
        {
            await send(notifier);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to send post-commit quality notification for record {RecordId}.", recordId);
        }
    }

    private sealed record RecordHeader(int Id, string Number, QualityRecordType Type, string Title,
        int? SectionId, int? RaisedByUserId, int? OwnerUserId);
}
