using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public class QualityLinkService(IMicroLimsDbContext db, QualityRecordCore core, PermissionService permissions,
    IUserSectionScopeService scope, TimeProvider clock)
{
    private readonly QualityLinkTargets _targets = new(db, permissions, scope, core);

    public Task<QualityLinkDto> AddAsync(int recordId, QualityLinkTargetType targetType, int targetId, int userId,
        CancellationToken ct = default) => AddAsync(recordId, targetType, targetId, userId, ExpectedVersion(), ct);

    public Task<QualityLinkDto> AddAsync(int recordId, QualityLinkTargetType targetType, int targetId, int userId,
        uint version, CancellationToken ct = default) => core.MutateAsync(recordId, version, userId, false, async _ =>
    {
        var target = await _targets.EnsureReadableAsync(targetType, targetId, userId, ct);
        if (await db.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.RecordId == recordId &&
            l.TargetType == targetType && l.TargetId == targetId, ct))
            throw new BusinessRuleException(MessageCodes.QualityDuplicateLink, "This target is already linked.");

        var link = new QualityRecordLink
        {
            RecordId = recordId, TargetType = targetType, TargetId = targetId, AddedByUserId = userId,
            AddedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.QualityRecordLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return new QualityLinkDto(link.Id, target.Number, target.Label, target.Route, false);
    }, ct);

    public Task RemoveAsync(int recordId, int linkId, int userId, CancellationToken ct = default) =>
        RemoveAsync(recordId, linkId, userId, ExpectedVersion(), ct);

    public Task RemoveAsync(int recordId, int linkId, int userId, uint version, CancellationToken ct = default) =>
        core.MutateAsync(recordId, version, userId, true, async record =>
        {
            if (record.Steps.Count != 0)
                throw new BusinessRuleException(MessageCodes.QualitySignedLink, "Links cannot be removed after the first signed step.");
            var link = await db.QualityRecordLinks.SingleOrDefaultAsync(l => l.RecordId == recordId && l.Id == linkId, ct)
                ?? throw QualityRecordCore.Missing();
            db.QualityRecordLinks.Remove(link);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public Task<IReadOnlyList<QualityLinkDto>> ResolveAsync(int recordId, int userId, CancellationToken ct = default) =>
        ResolveAsync(recordId, userId, 0, 100, ct);

    public async Task<IReadOnlyList<QualityLinkDto>> ResolveAsync(int recordId, int userId, int offset, int pageSize,
        CancellationToken ct = default)
    {
        var readable = await core.ReadableRecordsAsync(userId, ct);
        if (!await readable.AnyAsync(r => r.Id == recordId, ct)) throw QualityRecordCore.Missing();
        var links = await db.QualityRecordLinks.AsNoTracking().Where(l => l.RecordId == recordId)
            .OrderBy(l => l.Id).Skip(Math.Max(0, offset))
            .Take(Paging.Normalize(1, pageSize, 1).PageSize)
            .Select(l => new { l.Id, l.TargetType, l.TargetId, l.NewRecordCode }).ToListAsync(ct);

        var resolved = new List<QualityLinkDto>(links.Count);
        foreach (var link in links)
        {
            var target = link.TargetId is int id ? await _targets.FindAsync(link.TargetType, id, ct) : null;
            if (target is not null && link.TargetId is int targetId)
            {
                try
                {
                    await _targets.EnsureAccessAsync(link.TargetType, targetId, target, userId, ct);
                    resolved.Add(new(link.Id, target.Number, target.Label, target.Route, false));
                    continue;
                }
                catch (Exception e) when (e is UnauthorizedAccessException or NotFoundException)
                {
                    // The target identifier is visible; its metadata and route are restricted.
                }
            }
            resolved.Add(new(link.Id, target?.Number ?? link.NewRecordCode, null, null, true));
        }
        return resolved;
    }

    public Task<IReadOnlyList<QualityRecordRefDto>> ForTargetAsync(QualityLinkTargetType targetType, int targetId,
        int userId, CancellationToken ct = default) => ForTargetAsync(targetType, targetId, userId, 0, 100, ct);

    public async Task<IReadOnlyList<QualityRecordRefDto>> ForTargetAsync(QualityLinkTargetType targetType, int targetId,
        int userId, int offset, int pageSize, CancellationToken ct = default)
    {
        await core.EnsureViewAsync(userId, ct);
        await _targets.EnsureReadableAsync(targetType, targetId, userId, ct);
        var records = await core.ReadableRecordsAsync(userId, ct);
        return await records.Where(r => r.Links.Any(l => l.TargetType == targetType && l.TargetId == targetId))
            .OrderByDescending(r => r.Id).Skip(Math.Max(0, offset))
            .Take(Paging.Normalize(1, pageSize, 1).PageSize)
            .Select(r => new QualityRecordRefDto(r.Id, r.Number, r.Type, r.Title)).ToListAsync(ct);
    }

    private uint ExpectedVersion() => db.ExpectedVersion ?? throw new BusinessRuleException(
        MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
