using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

internal sealed class QualityLinkTargets(IMicroLimsDbContext db, PermissionService permissions,
    IUserSectionScopeService scope, QualityRecordCore core)
{
    internal sealed record Target(int Id, string Number, string Label, string Route, int? ScopeId,
        DocumentRecordStatus? DocumentStatus = null, List<int>? SampleOrderSections = null,
        bool AwaitingTestSelection = false, string? PendingTargetSectionIds = null);

    internal async Task<Target> EnsureReadableAsync(QualityLinkTargetType type, int id, int userId, CancellationToken ct)
    {
        var target = await FindAsync(type, id, ct) ?? throw QualityRecordCore.Missing();
        try
        {
            await EnsureAccessAsync(type, id, target, userId, ct);
        }
        catch (UnauthorizedAccessException)
        {
            throw QualityRecordCore.Missing();
        }
        return target;
    }

    private async Task EnsureAccessAsync(QualityLinkTargetType type, int id, Target target, int userId, CancellationToken ct)
    {
        switch (type)
        {
            case QualityLinkTargetType.Sample:
                await scope.EnsureSampleAccessAsync(userId, id, ct, SectionAccess.Read);
                break;
            case QualityLinkTargetType.TestOrder:
                await scope.EnsureTestOrderAccessAsync(userId, id, ct, SectionAccess.Read);
                break;
            case QualityLinkTargetType.Equipment:
                await scope.EnsureEquipmentAccessAsync(userId, id, ct, SectionAccess.Read);
                break;
            case QualityLinkTargetType.MediaLot:
                await scope.EnsureMediaAccessAsync(userId, id, ct, SectionAccess.Read);
                break;
            case QualityLinkTargetType.StockLot:
                await scope.EnsureMaterialAccessAsync(userId, id, ct, SectionAccess.Read);
                break;
            case QualityLinkTargetType.QualityRecord:
                if (!await (await core.ReadableRecordsAsync(userId, ct)).AnyAsync(r => r.Id == id, ct))
                    throw QualityRecordCore.Missing();
                break;
            case QualityLinkTargetType.DocumentMaster:
            case QualityLinkTargetType.DocumentRevision:
                var documentId = target.ScopeId ?? id;
                var visible = await new DocumentAuthorizationService(db).FilterViewableDocumentIdsAsync([documentId], userId, ct);
                if (!visible.Contains(documentId)) throw QualityRecordCore.Missing();
                break;
            case QualityLinkTargetType.AuditTrailReviewFinding:
                await RequirePermissionAsync(userId, PermissionConstants.AuditView, ct);
                break;
            case QualityLinkTargetType.OosInvestigation:
                await RequirePermissionAsync(userId, PermissionConstants.OosView, ct);
                await EnsureSectionAsync(userId, target.ScopeId, ct);
                if (await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role != null &&
                        u.Role.Type == RoleType.Analyst, ct) &&
                    !await permissions.UserHasPermissionAsync(userId, PermissionConstants.OosManage, ct) &&
                    !await Oos.OosInvestigationService.FilterAnalystOwned(db, db.OosInvestigations.AsNoTracking(), userId)
                        .AnyAsync(i => i.Id == id, ct))
                    throw QualityRecordCore.Missing();
                break;
            case QualityLinkTargetType.MonitoringDeviation:
                await RequirePermissionAsync(userId, PermissionConstants.MonitoringPlanView, ct);
                await EnsureSectionAsync(userId, target.ScopeId, ct);
                break;
            case QualityLinkTargetType.TestDefinition:
                await EnsureSectionAsync(userId, target.ScopeId, ct);
                break;
            case QualityLinkTargetType.Specification:
                var sections = await scope.GetReadableSectionIdsAsync(userId, ct);
                if (sections is not null && !await db.TestDefinitions.AsNoTracking()
                    .AnyAsync(t => t.Code == target.Number && sections.Contains(t.SectionId), ct))
                    throw QualityRecordCore.Missing();
                break;
        }
    }

    private async Task EnsureSectionAsync(int userId, int? section, CancellationToken ct)
    {
        if (section is int sectionId)
            await scope.EnsureSectionsAccessAsync(userId, [sectionId], ct, SectionAccess.Read);
    }

    private async Task RequirePermissionAsync(int userId, string permission, CancellationToken ct)
    {
        if (!await permissions.UserHasPermissionAsync(userId, permission, ct))
            throw QualityRecordCore.Missing();
    }

    internal async Task<Dictionary<int, (Target Target, bool IsReadable)>> ResolveBatchAsync(
        QualityLinkTargetType type, int[] ids, int userId, QualityRecordCore.ReadAccess access, CancellationToken ct)
    {
        var targets = await FindAllAsync(type, ids, ct);
        switch (type)
        {
            case QualityLinkTargetType.QualityRecord:
                var qualityIds = await core.ReadableRecords(userId, access).Where(r => ids.Contains(r.Id))
                    .Select(r => r.Id).ToListAsync(ct);
                return Map(targets, qualityIds.ToHashSet().Contains);
            case QualityLinkTargetType.OosInvestigation:
                if (!access.Holds(PermissionConstants.OosView)) return Map(targets, _ => false);
                if (access.Role == RoleType.Analyst && !access.Holds(PermissionConstants.OosManage))
                {
                    var ownedIds = await Oos.OosInvestigationService.FilterAnalystOwned(db,
                            db.OosInvestigations.AsNoTracking().Where(i => ids.Contains(i.Id)), userId)
                        .Select(i => i.Id).ToListAsync(ct);
                    var owned = ownedIds.ToHashSet();
                    return targets.ToDictionary(t => t.Id, t => (t, access.ReadsSection(t.ScopeId) && owned.Contains(t.Id)));
                }
                break;
            case QualityLinkTargetType.Specification:
                if (access.Sections is { } sections)
                {
                    var codes = targets.Select(t => t.Number).Distinct().ToArray();
                    var readable = (await db.TestDefinitions.AsNoTracking()
                        .Where(t => codes.Contains(t.Code) && sections.Contains(t.SectionId))
                        .Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
                    return targets.ToDictionary(t => t.Id, t => (t, readable.Contains(t.Number)));
                }
                break;
        }

        return targets.ToDictionary(t => t.Id, t => (t, type switch
        {
            QualityLinkTargetType.Sample => ReadsSample(t, access),
            QualityLinkTargetType.DocumentMaster or QualityLinkTargetType.DocumentRevision =>
                t.DocumentStatus != DocumentRecordStatus.Void || access.Role == RoleType.SectionHead ||
                    access.Holds(PermissionConstants.AuditView),
            QualityLinkTargetType.AuditTrailReviewFinding => access.Holds(PermissionConstants.AuditView),
            QualityLinkTargetType.MonitoringDeviation =>
                access.Holds(PermissionConstants.MonitoringPlanView) && access.ReadsSection(t.ScopeId),
            _ => access.ReadsSection(t.ScopeId)
        }));
    }

    private static Dictionary<int, (Target Target, bool IsReadable)> Map(
        IEnumerable<Target> targets, Func<int, bool> isReadable) =>
        targets.ToDictionary(t => t.Id, t => (t, isReadable(t.Id)));

    private static bool ReadsSample(Target target, QualityRecordCore.ReadAccess access) =>
        access.Sections is not { } sections ||
        (target.ScopeId is int sectionId && sections.Contains(sectionId)) ||
        (target.SampleOrderSections ?? []).Concat(
            SampleSectionRollup.PendingSectionIds(target.AwaitingTestSelection, target.PendingTargetSectionIds))
            .Any(sections.Contains);

    private static string FormatId(int id) => id.ToString(CultureInfo.InvariantCulture);

    private async Task<Target?> FindAsync(QualityLinkTargetType type, int id, CancellationToken ct) =>
        (await FindAllAsync(type, [id], ct)).SingleOrDefault();

    private Task<List<Target>> FindAllAsync(QualityLinkTargetType type, int[] ids, CancellationToken ct)
    {
        var query = type switch
        {
            QualityLinkTargetType.Sample => db.Samples.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.ReferenceNumber, x.ReferenceNumber, RecordNavigationTargets.Sample(x.Id), x.LabSectionId, null,
                    x.TestOrders.Where(o => !o.IsSuperseded).Select(o => o.SectionId).ToList(),
                    x.AwaitingTestSelection, x.PendingTargetSectionIds)),
            QualityLinkTargetType.TestOrder => db.TestOrders.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, FormatId(x.Id), x.TestCode, RecordNavigationTargets.TestOrder(x.SampleId, x.Id), x.SectionId)),
            QualityLinkTargetType.OosInvestigation => db.OosInvestigations.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.Number, x.Number, "/oos-tracking?investigationId=" + FormatId(x.Id), x.SectionId)),
            QualityLinkTargetType.MonitoringDeviation => db.MonitoringDeviations.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.Number, x.Number, "/monitoring-plan?deviationId=" + FormatId(x.Id),
                    x.MonitoringOccurrence == null ? null : x.MonitoringOccurrence.LabSectionId)),
            QualityLinkTargetType.Equipment => db.Equipment.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.Code, x.Name, "/laboratory-configuration/equipment?id=" + FormatId(x.Id), x.SectionId)),
            QualityLinkTargetType.MediaLot => db.Media.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.LotNumber, x.LotNumber, RecordNavigationTargets.MediaLot(), x.Material == null ? null : x.Material.SectionId)),
            QualityLinkTargetType.StockLot => db.Materials.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.StockId ?? FormatId(x.Id), x.MaterialName, RecordNavigationTargets.Material(), x.SectionId)),
            QualityLinkTargetType.Item => db.Items.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.Code, x.Name, RecordNavigationTargets.Item(), null)),
            QualityLinkTargetType.Specification => db.Specifications.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.TestCode, x.ParameterName, RecordNavigationTargets.Specification(x.Id), null)),
            QualityLinkTargetType.TestDefinition => db.TestDefinitions.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.Code, x.DisplayName, RecordNavigationTargets.TestDefinition(x.PhyschemArea, x.Id), x.SectionId)),
            QualityLinkTargetType.DocumentMaster => db.DocumentMasters.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.MicroLimsDocumentId, x.Title, "/document-control/documents/" + FormatId(x.Id), null, DocumentStatus: x.RecordStatus)),
            QualityLinkTargetType.DocumentRevision => db.DocumentRevisions.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.DocumentMaster.MicroLimsDocumentId + "/" + x.RevisionNumber, x.DocumentMaster.Title,
                    "/document-control/documents/" + x.DocumentMasterId + "?revisionId=" + FormatId(x.Id), x.DocumentMasterId, DocumentStatus: x.DocumentMaster.RecordStatus)),
            QualityLinkTargetType.AuditTrailReviewFinding => db.AuditTrailReviewFindings.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, FormatId(x.Id), FormatId(x.Id), "/audit-trail-reviews/" + x.ReviewId, null)),
            QualityLinkTargetType.QualityRecord => db.QualityRecords.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new Target(x.Id, x.Number, x.Title, "/quality/" + FormatId(x.Id), null)),
            _ => throw new BusinessRuleException(MessageCodes.QualityInvalidTarget, "Unknown quality link target type.")
        };
        return query.ToListAsync(ct);
    }
}
