using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public sealed class ChangeControlGate(
    IMicroLimsDbContext db,
    IChangeControlContext context,
    TimeProvider clock)
{
    private sealed record LinkCandidate(int Id, QualityLinkTargetType TargetType, int? TargetId, string? NewRecordCode);

    public IChangeControlContext Context => context;

    public Task<ChangeControlGrant?> EnsureAllowedAsync(
        int sectionId,
        QualityLinkTargetType targetType,
        int? targetId,
        string? newRecordCode,
        CancellationToken ct = default) =>
        EnsureAllowedForSectionsAsync([sectionId], targetType, targetId, newRecordCode, ct);

    public Task<ChangeControlGrant?> EnsureAllowedForSectionsAsync(
        IReadOnlyList<int> sectionIds,
        QualityLinkTargetType targetType,
        int? targetId,
        string? matchCode,
        CancellationToken ct = default)
    {
        if (targetId is int id && context.GetScopedGrant(targetType, id) is { } scoped)
            return Task.FromResult<ChangeControlGrant?>(scoped);

        var normalizedCode = string.IsNullOrWhiteSpace(matchCode) ? null : matchCode.Trim();
        return EnsureAllowedCoreAsync(sectionIds, links =>
        {
            if (targetId is int tid)
            {
                var byId = links.FirstOrDefault(l => l.TargetType == targetType && l.TargetId == tid);
                if (byId is not null)
                    return byId;
            }

            if (normalizedCode is not null)
            {
                return links.FirstOrDefault(l =>
                    l.TargetType == targetType &&
                    l.TargetId == null &&
                    l.NewRecordCode != null &&
                    string.Equals(l.NewRecordCode.Trim(), normalizedCode, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }, ct);
    }

    public Task<ChangeControlGrant?> EnsureSpecificationCreateAllowedAsync(
        int sectionId,
        int itemId,
        int testDefinitionId,
        CancellationToken ct = default) =>
        EnsureAllowedCoreAsync([sectionId], links =>
            links.FirstOrDefault(l =>
                (l.TargetType == QualityLinkTargetType.Item && l.TargetId == itemId) ||
                (l.TargetType == QualityLinkTargetType.TestDefinition && l.TargetId == testDefinitionId)),
            ct);

    public async Task RecordAppliedAsync(
        ChangeControlGrant? grant,
        string entityName,
        string entityId,
        CancellationToken ct = default)
    {
        if (grant is null)
            return;

        var auditLogId = db.ChangeTracker.Entries<AuditLog>()
            .Select(e => e.Entity)
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Id)
            .Select(a => (int?)a.Id)
            .FirstOrDefault();

        var link = await db.QualityRecordLinks.SingleOrDefaultAsync(l => l.Id == grant.LinkId, ct);
        if (link is not null)
        {
            link.ChangedAtUtc = clock.GetUtcNow().UtcDateTime;
            link.ChangeAuditLogId = auditLogId;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<ChangeControlGrant?> EnsureAllowedCoreAsync(
        IReadOnlyList<int> sectionIds,
        Func<IReadOnlyList<LinkCandidate>, LinkCandidate?> matchLink,
        CancellationToken ct)
    {
        var distinctSections = sectionIds.Distinct().OrderBy(id => id).ToArray();
        if (distinctSections.Length == 0)
            return null;

        if (!db.Database.IsRelational())
        {
            var inMemoryRequired = await db.QualitySectionSettings.AsNoTracking()
                .Where(s => distinctSections.Contains(s.SectionId) && s.ChangeControlRequired)
                .AnyAsync(ct);
            if (!inMemoryRequired)
                return null;
        }
        else
        {
            if (db.Database.CurrentTransaction is null)
                throw new BusinessRuleException(MessageCodes.QualityTransactionRequired,
                    "A database transaction is required.");

            foreach (var sid in distinctSections)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT "Id" FROM "QualitySectionSettings" WHERE "SectionId" = {sid} FOR SHARE""", ct);
            }

            var gateRequired = await db.QualitySectionSettings.AsNoTracking()
                .Where(s => distinctSections.Contains(s.SectionId) && s.ChangeControlRequired)
                .AnyAsync(ct);
            if (!gateRequired)
                return null;
        }

        var number = context.Number;
        if (string.IsNullOrWhiteSpace(number))
            throw new BusinessRuleException(MessageCodes.ChangeControlGateRequired,
                "This laboratory requires an approved change control for this change.");

        var recordId = await db.Database.SqlQuery<int>(
                $"""SELECT "Id" AS "Value" FROM "QualityRecords" WHERE "Number" = {number} AND "Type" = {(int)QualityRecordType.ChangeControl} FOR SHARE""")
            .FirstOrDefaultAsync(ct);

        if (recordId == 0)
            throw new BusinessRuleException(MessageCodes.ChangeControlGateNotFound,
                $"Change control {number} was not found.",
                new Dictionary<string, string> { ["number"] = number });

        var info = await db.QualityRecords.AsNoTracking()
            .Where(r => r.Id == recordId)
            .Select(r => new
            {
                r.IsCancelled,
                r.SectionId,
                Status = r.ChangeControlDetail != null ? (ChangeControlStatus?)r.ChangeControlDetail.Status : null
            })
            .SingleOrDefaultAsync(ct);

        if (info is null || info.IsCancelled || info.Status != ChangeControlStatus.ApprovedForImplementation)
            throw new BusinessRuleException(MessageCodes.ChangeControlGateNotApproved,
                $"Change control {number} is not approved for implementation.",
                new Dictionary<string, string> { ["number"] = number });

        if (info.SectionId is int ccSectionId && distinctSections.Any(s => s != ccSectionId))
            throw new BusinessRuleException(MessageCodes.ChangeControlGateWrongLaboratory,
                $"Change control {number} belongs to another laboratory.",
                new Dictionary<string, string> { ["number"] = number });

        var links = await db.QualityRecordLinks.AsNoTracking()
            .Where(l => l.RecordId == recordId && l.IsAffectedByChange)
            .OrderBy(l => l.Id)
            .Select(l => new LinkCandidate(l.Id, l.TargetType, l.TargetId, l.NewRecordCode))
            .ToListAsync(ct);

        var matched = matchLink(links)
            ?? throw new BusinessRuleException(MessageCodes.ChangeControlGateRecordNotListed,
                $"Change control {number} does not list this record.",
                new Dictionary<string, string> { ["number"] = number });

        return new ChangeControlGrant(recordId, number, matched.Id);
    }
}
