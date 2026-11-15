using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Notifications;

public sealed record NotificationPreferenceRowDto(
    string Type,
    string Title,
    bool EmailAllowed,
    EmailMode EmailMode,
    bool Mandatory,
    string? MandatoryReason);

public sealed record UpdateNotificationPreferenceItem(
    string Type,
    EmailMode EmailMode);

public sealed record MandatoryNotificationTypeDto(
    string Type,
    string Title,
    bool EmailAllowed,
    bool Mandatory,
    int? SetByUserId,
    DateTime? SetAt);

/// <summary>
/// Manages per-user email notification preferences and section-level mandatory notification types.
/// </summary>
public sealed class NotificationPreferencesService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly PermissionService _permissions;
    private readonly TimeProvider _time;

    public NotificationPreferencesService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        PermissionService permissions,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _permissions = permissions;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Returns preference rows for every catalogue notification type, locking mandatory types to Immediate.
    /// </summary>
    public async Task<IReadOnlyList<NotificationPreferenceRowDto>> GetPreferencesAsync(
        int userId,
        CancellationToken ct = default)
    {
        var mandatoryByType = await GetMandatorySectionsForUserAsync(userId, ct);

        var savedPrefs = await _db.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Type, p.EmailMode })
            .ToDictionaryAsync(p => p.Type, p => p.EmailMode, StringComparer.Ordinal, ct);

        var rows = new List<NotificationPreferenceRowDto>(NotificationCatalog.Types.Count);
        foreach (var info in NotificationCatalog.Types.Values)
        {
            if (!info.EmailAllowed)
            {
                rows.Add(new NotificationPreferenceRowDto(
                    info.Type,
                    info.TitleTemplate,
                    EmailAllowed: false,
                    EmailMode: EmailMode.Off,
                    Mandatory: false,
                    MandatoryReason: null));
                continue;
            }

            if (mandatoryByType.TryGetValue(info.Type, out var sectionNames) && sectionNames.Count > 0)
            {
                rows.Add(new NotificationPreferenceRowDto(
                    info.Type,
                    info.TitleTemplate,
                    EmailAllowed: true,
                    EmailMode: EmailMode.Immediate,
                    Mandatory: true,
                    MandatoryReason: $"Mandatory for {string.Join(", ", sectionNames)}"));
                continue;
            }

            var mode = savedPrefs.GetValueOrDefault(info.Type, EmailMode.Immediate);
            rows.Add(new NotificationPreferenceRowDto(
                info.Type,
                info.TitleTemplate,
                EmailAllowed: true,
                EmailMode: mode,
                Mandatory: false,
                MandatoryReason: null));
        }

        return rows;
    }

    /// <summary>
    /// Updates the caller's email mode preferences, refusing non-email types and section-mandatory types.
    /// </summary>
    public async Task<IReadOnlyList<NotificationPreferenceRowDto>> UpdatePreferencesAsync(
        int userId,
        IReadOnlyList<UpdateNotificationPreferenceItem> items,
        CancellationToken ct = default)
    {
        if (items is null || items.Count == 0)
            throw new FieldValidationException(
                "Provide at least one notification preference to update.",
                [new FieldError("preferences", "Provide at least one notification preference to update.")]);

        var mandatoryByType = await GetMandatorySectionsForUserAsync(userId, ct);
        var errors = new List<FieldError>();
        var normalized = new Dictionary<string, EmailMode>(StringComparer.Ordinal);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var info = NotificationCatalog.TryGet(item.Type);
            if (info is null)
            {
                errors.Add(new FieldError($"preferences[{i}].type", $"Unknown notification type '{item.Type}'."));
                continue;
            }

            if (!Enum.IsDefined(item.EmailMode))
            {
                errors.Add(new FieldError($"preferences[{i}].emailMode", $"Invalid email mode for '{info.Type}'."));
                continue;
            }

            if (!info.EmailAllowed)
            {
                errors.Add(new FieldError(
                    $"preferences[{i}].emailMode",
                    $"Notification type '{info.Type}' is in-app only and does not support email delivery."));
                continue;
            }

            if (mandatoryByType.TryGetValue(info.Type, out var sections) && sections.Count > 0)
            {
                errors.Add(new FieldError(
                    $"preferences[{i}].emailMode",
                    $"Notification type '{info.Type}' is mandatory for {string.Join(", ", sections)} and cannot be changed."));
                continue;
            }

            normalized[info.Type] = item.EmailMode;
        }

        if (errors.Count > 0)
            throw new FieldValidationException(errors[0].Message, errors);

        var targetTypes = normalized.Keys.ToList();
        var existing = await _db.UserNotificationPreferences
            .Where(p => p.UserId == userId && targetTypes.Contains(p.Type))
            .ToDictionaryAsync(p => p.Type, StringComparer.Ordinal, ct);

        foreach (var (type, emailMode) in normalized)
        {
            if (existing.TryGetValue(type, out var row))
            {
                row.EmailMode = emailMode;
            }
            else
            {
                _db.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    UserId = userId,
                    Type = type,
                    EmailMode = emailMode
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetPreferencesAsync(userId, ct);
    }

    /// <summary>
    /// Lists email-capable notification types and whether each is marked mandatory for the section.
    /// Requires Notifications.Configure and membership in the section.
    /// </summary>
    public async Task<IReadOnlyList<MandatoryNotificationTypeDto>> GetMandatoryAsync(
        int actorUserId,
        int sectionId,
        CancellationToken ct = default)
    {
        await EnsureCanConfigureSectionAsync(actorUserId, sectionId, ct);

        var mandatory = await _db.MandatoryNotificationTypes
            .AsNoTracking()
            .Where(m => m.SectionId == sectionId)
            .Select(m => new { m.Type, m.SetByUserId, m.SetAt })
            .ToDictionaryAsync(m => m.Type, StringComparer.Ordinal, ct);

        return NotificationCatalog.Types.Values
            .Where(info => info.EmailAllowed)
            .Select(info => mandatory.TryGetValue(info.Type, out var row)
                ? new MandatoryNotificationTypeDto(info.Type, info.TitleTemplate, true, true, row.SetByUserId, row.SetAt)
                : new MandatoryNotificationTypeDto(info.Type, info.TitleTemplate, true, false, null, null))
            .ToList();
    }

    /// <summary>
    /// Replaces the set of mandatory notification types for a section.
    /// Requires Notifications.Configure and membership in the section.
    /// </summary>
    public async Task<IReadOnlyList<MandatoryNotificationTypeDto>> SetMandatoryAsync(
        int actorUserId,
        int sectionId,
        IReadOnlyList<string> types,
        CancellationToken ct = default)
    {
        await EnsureCanConfigureSectionAsync(actorUserId, sectionId, ct);

        if (types is null)
            throw new FieldValidationException(
                "Provide the list of mandatory notification types.",
                [new FieldError("types", "Provide the list of mandatory notification types.")]);

        var errors = new List<FieldError>();
        var desired = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < types.Count; i++)
        {
            var raw = types[i];
            var info = NotificationCatalog.TryGet(raw);
            if (info is null)
            {
                errors.Add(new FieldError($"types[{i}]", $"Unknown notification type '{raw}'."));
                continue;
            }

            if (!info.EmailAllowed)
            {
                errors.Add(new FieldError(
                    $"types[{i}]",
                    $"Only email-enabled notification types can be marked mandatory ('{info.Type}' is in-app only)."));
                continue;
            }

            desired.Add(info.Type);
        }

        if (errors.Count > 0)
            throw new FieldValidationException(errors[0].Message, errors);

        var existing = await _db.MandatoryNotificationTypes
            .Where(m => m.SectionId == sectionId)
            .ToListAsync(ct);

        foreach (var row in existing.Where(r => !desired.Contains(r.Type)))
        {
            _db.MandatoryNotificationTypes.Remove(row);
        }

        var existingSet = existing.Select(r => r.Type).ToHashSet(StringComparer.Ordinal);
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var type in desired.Where(t => !existingSet.Contains(t)))
        {
            _db.MandatoryNotificationTypes.Add(new MandatoryNotificationType
            {
                SectionId = sectionId,
                Type = type,
                SetByUserId = actorUserId,
                SetAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetMandatoryAsync(actorUserId, sectionId, ct);
    }

    private async Task EnsureCanConfigureSectionAsync(int actorUserId, int sectionId, CancellationToken ct)
    {
        if (!await _permissions.UserHasPermissionAsync(actorUserId, PermissionConstants.NotificationsConfigure, ct))
            throw new UnauthorizedAccessException("Notifications.Configure permission is required.");

        if (sectionId <= 0)
            throw new FieldValidationException(
                "A valid sectionId is required.",
                [new FieldError("sectionId", "A valid sectionId is required.")]);

        var memberSections = await _scope.GetMemberSectionIdsAsync(actorUserId, ct);
        if (!memberSections.Contains(sectionId))
            throw new UnauthorizedAccessException("You can only configure mandatory notifications for a laboratory section you belong to.");
    }

    private async Task<Dictionary<string, List<string>>> GetMandatorySectionsForUserAsync(int userId, CancellationToken ct)
    {
        var memberSections = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (memberSections.Count == 0)
            return new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var mandatoryRows = await _db.MandatoryNotificationTypes
            .AsNoTracking()
            .Where(m => memberSections.Contains(m.SectionId))
            .OrderBy(m => m.Section!.Name)
            .Select(m => new { m.Type, SectionName = m.Section != null ? m.Section.Name : string.Empty })
            .ToListAsync(ct);

        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in mandatoryRows)
        {
            if (!result.TryGetValue(row.Type, out var list))
            {
                list = new List<string>();
                result[row.Type] = list;
            }

            if (!string.IsNullOrWhiteSpace(row.SectionName) && !list.Contains(row.SectionName, StringComparer.Ordinal))
            {
                list.Add(row.SectionName);
            }
        }

        return result;
    }
}
