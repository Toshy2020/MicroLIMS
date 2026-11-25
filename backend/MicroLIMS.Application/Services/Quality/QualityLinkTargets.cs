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
    internal sealed record Target(string Number, string Label, string Route, int? ScopeId = null);

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

    internal async Task EnsureAccessAsync(QualityLinkTargetType type, int id, Target target, int userId, CancellationToken ct)
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

    internal Task<Target?> FindAsync(QualityLinkTargetType type, int id, CancellationToken ct)
    {
        var number = id.ToString(CultureInfo.InvariantCulture);
        var query = type switch
        {
            QualityLinkTargetType.Sample => db.Samples.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.ReferenceNumber, x.ReferenceNumber, RecordNavigationTargets.Sample(id), null)),
            QualityLinkTargetType.TestOrder => db.TestOrders.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(number, x.TestCode, RecordNavigationTargets.TestOrder(x.SampleId, id), null)),
            QualityLinkTargetType.OosInvestigation => db.OosInvestigations.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.Number, x.Number, "/oos-tracking?investigationId=" + number, x.SectionId)),
            QualityLinkTargetType.MonitoringDeviation => db.MonitoringDeviations.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.Number, x.Number, "/monitoring-plan?deviationId=" + number,
                    x.MonitoringOccurrence == null ? null : x.MonitoringOccurrence.LabSectionId)),
            QualityLinkTargetType.Equipment => db.Equipment.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.Code, x.Name, "/laboratory-configuration/equipment?id=" + number, null)),
            QualityLinkTargetType.MediaLot => db.Media.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.LotNumber, x.LotNumber, RecordNavigationTargets.MediaLot(), null)),
            QualityLinkTargetType.StockLot => db.Materials.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.StockId ?? number, x.MaterialName, RecordNavigationTargets.Material(), null)),
            QualityLinkTargetType.Item => db.Items.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.Code, x.Name, RecordNavigationTargets.Item(), null)),
            QualityLinkTargetType.Specification => db.Specifications.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.TestCode, x.ParameterName, "/laboratory-configuration/specifications?id=" + number, null)),
            QualityLinkTargetType.TestDefinition => db.TestDefinitions.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.Code, x.DisplayName, TestDefinitionRoute(x.PhyschemArea, id), x.SectionId)),
            QualityLinkTargetType.DocumentMaster => db.DocumentMasters.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.MicroLimsDocumentId, x.Title, "/document-control/documents/" + number, null)),
            QualityLinkTargetType.DocumentRevision => db.DocumentRevisions.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.DocumentMaster.MicroLimsDocumentId + "/" + x.RevisionNumber, x.DocumentMaster.Title,
                    "/document-control/documents/" + x.DocumentMasterId + "?revisionId=" + number, x.DocumentMasterId)),
            QualityLinkTargetType.AuditTrailReviewFinding => db.AuditTrailReviewFindings.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(number, number, "/audit-trail-reviews/" + x.ReviewId, null)),
            QualityLinkTargetType.QualityRecord => db.QualityRecords.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new Target(x.Number, x.Title, "/quality/" + number, null)),
            _ => throw new BusinessRuleException(MessageCodes.QualityInvalidTarget, "Unknown quality link target type.")
        };
        return query.SingleOrDefaultAsync(ct);
    }

    private static string TestDefinitionRoute(PhyschemArea? area, int id)
    {
        var path = area switch
        {
            PhyschemArea.RawPackaging => "rm-pm-test-master",
            PhyschemArea.FinishedProduct or PhyschemArea.Both => "fp-test-master",
            _ => "test-master"
        };
        return $"/laboratory-configuration/{path}?id={id}";
    }
}
