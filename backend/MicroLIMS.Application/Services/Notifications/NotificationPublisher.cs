using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Abstractions.Live;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Notifications;

/// <summary>
/// Stages per-user notification rows and immediate email outbox rows onto the caller's
/// DbContext without saving, so notifications commit or roll back with the triggering change.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;
    private readonly string _appBaseUrl;
    private readonly string _timeZoneId;

    public NotificationPublisher(
        IMicroLimsDbContext db,
        TimeProvider? timeProvider = null,
        IConfiguration? config = null,
        ILabClock? clock = null)
    {
        _db = db;
        _time = timeProvider ?? TimeProvider.System;
        _appBaseUrl = NotificationEmailComposer.ResolveAppBaseUrl(config);
        _timeZoneId = clock?.LabTimeZone.Id ?? config?["Lab:TimeZoneId"] ?? "Africa/Cairo";
    }

    public async Task PublishAsync(NotificationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var info = NotificationCatalog.Get(request.Type);

        var userIds = request.UserIds.Where(id => id > 0).Distinct().ToList();
        if (userIds.Count == 0)
            return;

        var dedupeKey = string.IsNullOrWhiteSpace(request.DedupeKey) ? null : request.DedupeKey;
        if (dedupeKey is not null)
        {
            var openInDb = await _db.NotificationLogs.AsNoTracking()
                .Where(n => n.DedupeKey == dedupeKey && n.ResolvedAt == null && userIds.Contains(n.UserId))
                .Select(n => n.UserId)
                .ToListAsync(ct);

            var skipSet = new HashSet<int>(openInDb);
            foreach (var local in _db.NotificationLogs.Local
                .Where(n => n.DedupeKey == dedupeKey && n.ResolvedAt == null && userIds.Contains(n.UserId)))
            {
                skipSet.Add(local.UserId);
            }

            userIds = userIds.Where(id => !skipSet.Contains(id)).ToList();
            if (userIds.Count == 0)
                return;
        }

        Dictionary<int, string?> emailsByUser = new();
        if (info.EmailAllowed)
        {
            var isMandatory = request.SectionId is int sectionId && await _db.MandatoryNotificationTypes
                .AsNoTracking()
                .AnyAsync(m => m.SectionId == sectionId && m.Type == info.Type, ct);

            var prefs = isMandatory
                ? new Dictionary<int, EmailMode>()
                : await _db.UserNotificationPreferences.AsNoTracking()
                    .Where(p => p.Type == info.Type && userIds.Contains(p.UserId))
                    .ToDictionaryAsync(p => p.UserId, p => p.EmailMode, ct);

            var immediateUserIds = userIds
                .Where(id => isMandatory || prefs.GetValueOrDefault(id, EmailMode.Immediate) == EmailMode.Immediate)
                .ToList();

            if (immediateUserIds.Count > 0)
            {
                emailsByUser = await _db.Users.AsNoTracking()
                    .Where(u => immediateUserIds.Contains(u.Id) && u.Email != null && u.Email != "")
                    .ToDictionaryAsync(u => u.Id, u => u.Email, ct);
            }
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var severity = string.IsNullOrWhiteSpace(request.Severity) ? info.DefaultSeverity : request.Severity;
        var title = info.TitleTemplate;

        foreach (var userId in userIds)
        {
            var log = new NotificationLog
            {
                UserId = userId,
                Type = info.Type,
                Title = title,
                Message = request.Message,
                Severity = severity,
                TargetType = request.TargetType,
                TargetId = request.TargetId,
                SectionId = request.SectionId,
                DedupeKey = dedupeKey,
                SampleId = request.SampleId,
                TestOrderId = request.TestOrderId,
                IsRead = false,
                EmailSent = false,
                CreatedAt = now
            };
            _db.NotificationLogs.Add(log);
            _db.QueueSignal(LiveSignalKinds.Notifications, $"user:{userId}");

            if (info.EmailAllowed && emailsByUser.TryGetValue(userId, out var email) && !string.IsNullOrWhiteSpace(email))
            {
                var (subject, body) = NotificationEmailComposer.Compose(log, _appBaseUrl, _timeZoneId, request.DueAtUtc);
                _db.EmailOutbox.Add(new EmailOutbox
                {
                    NotificationLog = log,
                    UserId = userId,
                    ToAddress = email.Trim(),
                    Subject = subject,
                    Body = body,
                    Kind = EmailOutboxKind.Immediate,
                    CreatedAt = now,
                    NextAttemptAt = now,
                    Attempts = 0
                });
            }
        }
    }

    public async Task ResolveAsync(string dedupeKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dedupeKey))
            return;

        var now = _time.GetUtcNow().UtcDateTime;

        var affectedUserIds = new HashSet<int>();

        foreach (var local in _db.NotificationLogs.Local
            .Where(n => n.DedupeKey == dedupeKey && n.ResolvedAt == null))
        {
            local.ResolvedAt = now;
            affectedUserIds.Add(local.UserId);
        }

        var openRows = await _db.NotificationLogs
            .Where(n => n.DedupeKey == dedupeKey && n.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var row in openRows)
        {
            row.ResolvedAt = now;
            affectedUserIds.Add(row.UserId);
        }

        foreach (var userId in affectedUserIds)
        {
            _db.QueueSignal(LiveSignalKinds.Notifications, $"user:{userId}");
        }
    }
}
