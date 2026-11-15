using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

public sealed record SectionSelfAssignmentDto(
    int SectionId,
    string SectionName,
    bool Enabled,
    DateTime? ChangedAt,
    string? ChangedByName,
    uint Version);

// Per-section switch controlling whether analysts holding Tests.SelfAssign may take unassigned tests
// in that section from My Work. Owned by the Section Head per section; every change requires a reason
// and is automatically audited with OldValues/NewValues and Reason by MicroLimsDbContext.
public sealed class SectionSelfAssignmentService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public SectionSelfAssignmentService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        TimeProvider? time = null)
    {
        _db = db;
        _scope = scope;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<SectionSelfAssignmentDto>> GetForUserAsync(int userId, CancellationToken ct = default)
    {
        var memberIds = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (memberIds.Count == 0)
            return Array.Empty<SectionSelfAssignmentDto>();

        var ids = memberIds.ToArray();
        var sections = await _db.DocumentSections
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);

        var settings = await _db.SectionSelfAssignmentSettings
            .AsNoTracking()
            .Where(s => ids.Contains(s.SectionId))
            .Select(s => new
            {
                s.SectionId,
                s.Enabled,
                s.ChangedAt,
                ChangedByName = s.ChangedByUser != null ? s.ChangedByUser.FullName : null,
                s.Version
            })
            .ToDictionaryAsync(s => s.SectionId, ct);

        return sections
            .Select(s => settings.TryGetValue(s.Id, out var cfg)
                ? new SectionSelfAssignmentDto(s.Id, s.Name, cfg.Enabled, cfg.ChangedAt, cfg.ChangedByName, cfg.Version)
                : new SectionSelfAssignmentDto(s.Id, s.Name, true, null, null, 0u))
            .ToList();
    }

    public async Task<SectionSelfAssignmentDto> SetAsync(
        int userId,
        int sectionId,
        bool enabled,
        string? reason,
        uint? version = null,
        CancellationToken ct = default)
    {
        var memberIds = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (!memberIds.Contains(sectionId))
            throw new UnauthorizedAccessException("You are not a member of this laboratory section.");

        var trimmed = ValidateReason(reason);

        var sectionName = await _db.DocumentSections
            .AsNoTracking()
            .Where(s => s.Id == sectionId)
            .Select(s => s.Name)
            .SingleAsync(ct);

        if (version is uint expectedVersion)
            _db.ExpectedVersion = expectedVersion;

        var row = await _db.SectionSelfAssignmentSettings
            .FirstOrDefaultAsync(s => s.SectionId == sectionId, ct);

        if (row is null)
        {
            if (_db.ExpectedVersion is uint expected && expected != 0)
            {
                throw new DbUpdateConcurrencyException(
                    $"SectionSelfAssignmentSetting changed after the form was loaded (version {expected}, now 0).");
            }

            if (enabled)
                return new SectionSelfAssignmentDto(sectionId, sectionName, true, null, null, 0u);

            row = new SectionSelfAssignmentSetting
            {
                SectionId = sectionId,
                Enabled = false,
                ChangedAt = _time.GetUtcNow().UtcDateTime,
                ChangedByUserId = userId
            };
            _db.SectionSelfAssignmentSettings.Add(row);
        }
        else
        {
            RecordVersion.EnsureCurrent(_db, row);

            if (row.Enabled == enabled)
            {
                var currentChanger = row.ChangedByUserId is int uid
                    ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(ct)
                    : null;
                return new SectionSelfAssignmentDto(sectionId, sectionName, row.Enabled, row.ChangedAt, currentChanger, row.Version);
            }

            row.Enabled = enabled;
            row.ChangedAt = _time.GetUtcNow().UtcDateTime;
            row.ChangedByUserId = userId;
        }

        _db.CurrentUserId = userId;
        _db.CurrentReason = trimmed;
        await _db.SaveChangesAsync(ct);

        var changerName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .SingleOrDefaultAsync(ct);

        return new SectionSelfAssignmentDto(sectionId, sectionName, row.Enabled, row.ChangedAt, changerName, row.Version);
    }

    public Task<bool> IsEnabledAsync(int sectionId, CancellationToken ct = default) =>
        IsEnabledAsync(_db, sectionId, ct);

    public static async Task<bool> IsEnabledAsync(IMicroLimsDbContext db, int sectionId, CancellationToken ct = default)
    {
        var enabled = await db.SectionSelfAssignmentSettings
            .AsNoTracking()
            .Where(s => s.SectionId == sectionId)
            .Select(s => (bool?)s.Enabled)
            .SingleOrDefaultAsync(ct);
        return enabled ?? true;
    }

    public IQueryable<int> DisabledSectionIds() => DisabledSectionIds(_db);

    public static IQueryable<int> DisabledSectionIds(IMicroLimsDbContext db) =>
        db.SectionSelfAssignmentSettings
            .AsNoTracking()
            .Where(s => !s.Enabled)
            .Select(s => s.SectionId);

    private static string ValidateReason(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            var msg = $"A reason of at least {ChangeReasonGuard.MinLength} characters is required for this change.";
            throw new FieldValidationException(msg, new[] { new FieldError(ChangeReasonGuard.FieldName, msg) });
        }

        if (trimmed.Length < ChangeReasonGuard.MinLength)
        {
            var msg = $"Reason must be at least {ChangeReasonGuard.MinLength} characters (entered {trimmed.Length}).";
            throw new FieldValidationException(msg, new[] { new FieldError(ChangeReasonGuard.FieldName, msg) });
        }

        if (trimmed.Length > ChangeReasonGuard.MaxLength)
        {
            var msg = $"Reason must be at most {ChangeReasonGuard.MaxLength} characters.";
            throw new FieldValidationException(msg, new[] { new FieldError(ChangeReasonGuard.FieldName, msg) });
        }

        return trimmed;
    }
}
