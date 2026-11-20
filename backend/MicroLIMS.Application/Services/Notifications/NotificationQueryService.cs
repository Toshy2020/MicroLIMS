using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Live;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Notifications;

public sealed record NotificationTargetDto(
    string? Type,
    int? Id,
    int? SampleId,
    int? TestOrderId);

public sealed record NotificationDto(
    int Id,
    string Type,
    string Title,
    string Message,
    string Severity,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? ResolvedAt,
    NotificationTargetDto Target,
    string? SectionName);

public sealed record NotificationSummaryDto(int Unread);

public sealed record UserWithoutEmailDto(
    int Id,
    string Username,
    string FullName,
    string RoleName);

public sealed record EmailOutboxSummaryDto(
    int Waiting,
    int Failed,
    DateTime? LastSentAt,
    IReadOnlyList<UserWithoutEmailDto> UsersWithoutEmail);

/// <summary>
/// Read/mark queries for the notification centre and admin email-outbox summary.
/// Every notification query is scoped strictly to the authenticated caller's UserId.
/// </summary>
public sealed class NotificationQueryService
{
    public const int DefaultPageSize = 25;

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;
    private readonly PermissionService _permissions;

    public NotificationQueryService(
        IMicroLimsDbContext db,
        TimeProvider? timeProvider = null,
        PermissionService? permissions = null)
    {
        _db = db;
        _time = timeProvider ?? TimeProvider.System;
        _permissions = permissions ?? new PermissionService(db);
    }

    /// <summary>
    /// Lists the authenticated user's notifications with optional state, type, section, and text filtering.
    /// </summary>
    public async Task<PagedResult<NotificationDto>> ListAsync(
        int userId,
        string? state = null,
        string? type = null,
        int? sectionId = null,
        string? q = null,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize, DefaultPageSize);

        var query = _db.NotificationLogs
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var normalizedState = string.IsNullOrWhiteSpace(state) ? "all" : state.Trim().ToLowerInvariant();
        query = normalizedState switch
        {
            "all" => query,
            "unread" => query.Where(n => !n.IsRead && n.ResolvedAt == null),
            "read" => query.Where(n => n.IsRead && n.ResolvedAt == null),
            "done" => query.Where(n => n.ResolvedAt != null),
            _ => throw new BusinessRuleException(
                MessageCodes.NotificationInvalidStateFilter,
                "State must be one of: unread, read, done, all.")
        };

        if (!string.IsNullOrWhiteSpace(type))
        {
            var trimmedType = type.Trim();
            query = query.Where(n => n.Type == trimmedType);
        }

        if (sectionId.HasValue)
        {
            query = query.Where(n => n.SectionId == sectionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(n =>
                n.Message.ToLower().Contains(term)
                || (n.Title != null && n.Title.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var rows = await (
            from n in query
            join s in _db.DocumentSections.AsNoTracking() on n.SectionId equals s.Id into sec
            from s in sec.DefaultIfEmpty()
            orderby n.CreatedAt descending, n.Id descending
            select new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.Severity,
                n.CreatedAt,
                n.ReadAt,
                n.ResolvedAt,
                n.TargetType,
                n.TargetId,
                n.SampleId,
                n.TestOrderId,
                SectionName = s != null ? s.Name : null
            })
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        var items = rows.Select(r => new NotificationDto(
            r.Id,
            r.Type,
            string.IsNullOrWhiteSpace(r.Title)
                ? NotificationCatalog.TryGet(r.Type)?.TitleTemplate ?? r.Type
                : r.Title,
            r.Message,
            r.Severity,
            r.CreatedAt,
            r.ReadAt,
            r.ResolvedAt,
            new NotificationTargetDto(r.TargetType, r.TargetId, r.SampleId, r.TestOrderId),
            r.SectionName)).ToList();

        return new PagedResult<NotificationDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Counts notifications for the caller that are both unread and unresolved.
    /// </summary>
    public async Task<NotificationSummaryDto> GetSummaryAsync(int userId, CancellationToken ct = default)
    {
        var unread = await _db.NotificationLogs
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead && n.ResolvedAt == null, ct);

        return new NotificationSummaryDto(unread);
    }

    /// <summary>
    /// Marks a single notification owned by the caller as read using the injected clock.
    /// Throws NotFoundException when the notification does not exist or belongs to another user.
    /// </summary>
    public async Task MarkReadAsync(int userId, int notificationId, CancellationToken ct = default)
    {
        var log = await _db.NotificationLogs
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (log is null)
            throw new NotFoundException(
                MessageCodes.NotificationNotFound,
                $"Notification {notificationId} was not found.",
                new Dictionary<string, string> { ["notificationId"] = notificationId.ToString(CultureInfo.InvariantCulture) });

        if (!log.IsRead || !log.ReadAt.HasValue)
        {
            log.IsRead = true;
            log.ReadAt ??= _time.GetUtcNow().UtcDateTime;
            _db.QueueSignal(LiveSignalKinds.Notifications, $"user:{userId}");
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Marks all unread notifications owned by the caller as read using the injected clock.
    /// </summary>
    public async Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        if (_db is DbContext efDb && efDb.Database.IsRelational())
        {
            var updated = await _db.NotificationLogs
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, n => n.ReadAt ?? now), ct);
            if (updated > 0)
            {
                _db.QueueSignal(LiveSignalKinds.Notifications, $"user:{userId}");
                if (_db.Database.CurrentTransaction is null)
                    await _db.FlushSignalsAsync(ct);
            }
            return;
        }

        var unread = await _db.NotificationLogs
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return;

        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAt ??= now;
        }

        _db.QueueSignal(LiveSignalKinds.Notifications, $"user:{userId}");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Returns email outbox queue health counts and active users missing an email address.
    /// Requires the Users.Manage permission.
    /// </summary>
    public async Task<EmailOutboxSummaryDto> GetOutboxSummaryAsync(int actorUserId, CancellationToken ct = default)
    {
        if (!await _permissions.UserHasPermissionAsync(actorUserId, PermissionConstants.UsersManage, ct))
            throw new UnauthorizedAccessException("Users.Manage permission is required to view email outbox status.");

        var waiting = await _db.EmailOutbox
            .AsNoTracking()
            .CountAsync(e => e.SentAt == null && e.FailedAt == null, ct);

        var failed = await _db.EmailOutbox
            .AsNoTracking()
            .CountAsync(e => e.FailedAt != null, ct);

        var lastSentAt = await _db.EmailOutbox
            .AsNoTracking()
            .Where(e => e.SentAt != null)
            .MaxAsync(e => (DateTime?)e.SentAt, ct);

        var usersWithoutEmail = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && (u.Email == null || u.Email.Trim() == ""))
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .Select(u => new UserWithoutEmailDto(
                u.Id,
                u.Username,
                u.FullName,
                u.Role != null ? u.Role.Name : string.Empty))
            .ToListAsync(ct);

        return new EmailOutboxSummaryDto(waiting, failed, lastSentAt, usersWithoutEmail);
    }
}
