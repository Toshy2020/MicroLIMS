using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public sealed partial class DeviationService
{
    public Task<DeviationDto> RaiseFromOosRequestAsync(int oosDeviationRequestId, RaiseDeviationFromSourceRequest details,
        uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequirePermissionAsync(userId, PermissionConstants.QualityApprove, ct);
        var source = await (from candidate in db.OosDeviationRequests.AsNoTracking()
                            join investigation in db.OosInvestigations.AsNoTracking() on candidate.InvestigationId equals investigation.Id
                            where candidate.Id == oosDeviationRequestId
                            select new { investigation.Id, investigation.SectionId })
            .SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();
        await EnsureSourceReadableAsync(QualityLinkTargetType.OosInvestigation, source.Id, userId, ct);

        // Match the investigation row lock used by the existing Address command.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "OosInvestigations" WHERE "Id" = {source.Id} FOR UPDATE""", ct);
        var addressed = await db.OosDeviationRequests.AsNoTracking()
            .Where(r => r.Id == oosDeviationRequestId && r.Status == OosDeviationRequestStatus.Addressed)
            .Select(r => new { r.AddressedComment }).SingleOrDefaultAsync(ct);
        if (addressed is not null)
            await EnsureNoSourceDeviationAsync(QualityLinkTargetType.OosInvestigation, source.Id, ct, addressed.AddressedComment);
        var request = await OosInvestigationService.LoadOpenDeviationRequestAsync(db, oosDeviationRequestId, expectedVersion, ct);
        var deviation = await CreateAsync(new(details.Title, request.Reason, details.OccurredAtUtc,
            null, details.ImmediateAction, source.SectionId, [new(QualityLinkTargetType.OosInvestigation, source.Id)]),
            userId, ct, fromSource: true);
        await OosInvestigationService.MarkDeviationRequestAddressedAsync(db, request, deviation.Record.Number, userId,
            clock.UtcNow.UtcDateTime, ct);
        return deviation;
    }, ct);

    public Task<DeviationDto> EscalateMonitoringDeviationAsync(int monitoringDeviationId, RaiseDeviationFromSourceRequest details,
        uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        if (!await HoldsAsync(userId, PermissionConstants.QualityApprove, ct))
            await RequirePermissionAsync(userId, PermissionConstants.QualityInvestigate, ct);
        await EnsureSourceReadableAsync(QualityLinkTargetType.MonitoringDeviation, monitoringDeviationId, userId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "MonitoringDeviations" WHERE "Id" = {monitoringDeviationId} FOR UPDATE""", ct);
        var source = await db.MonitoringDeviations.AsNoTracking().Where(d => d.Id == monitoringDeviationId)
            .Select(d => new { d.Version, d.Description,
                SectionId = d.MonitoringOccurrence == null ? null : d.MonitoringOccurrence.LabSectionId })
            .SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();
        if (source.Version != expectedVersion) throw QualityRecordCore.Stale();
        await EnsureNoSourceDeviationAsync(QualityLinkTargetType.MonitoringDeviation, monitoringDeviationId, ct);
        return await CreateAsync(new(details.Title, source.Description, details.OccurredAtUtc, null,
            details.ImmediateAction, source.SectionId,
            [new(QualityLinkTargetType.MonitoringDeviation, monitoringDeviationId)]), userId, ct, fromSource: true);
    }, ct);

    public Task<DeviationDto> RaiseFromAuditFindingAsync(int findingId, RaiseDeviationFromSourceRequest details,
        uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequirePermissionAsync(userId, PermissionConstants.QualityApprove, ct);
        await EnsureSourceReadableAsync(QualityLinkTargetType.AuditTrailReviewFinding, findingId, userId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "AuditTrailReviewFindings" WHERE "Id" = {findingId} FOR UPDATE""", ct);
        var source = await db.AuditTrailReviewFindings.AsNoTracking().Where(f => f.Id == findingId)
            .Select(f => new { f.Status }).SingleOrDefaultAsync(ct)
            ?? throw QualityRecordCore.Missing();

        // Matches AuditTrailReviewFindingDto.Version. The only permitted edit moves Open to Closed.
        if ((uint)source.Status != expectedVersion) throw QualityRecordCore.Stale();
        await EnsureNoSourceDeviationAsync(QualityLinkTargetType.AuditTrailReviewFinding, findingId, ct);
        return await CreateAsync(new(details.Title, $"Deviation raised from audit finding {findingId}.", details.OccurredAtUtc, null,
            details.ImmediateAction, null, [new(QualityLinkTargetType.AuditTrailReviewFinding, findingId)]),
            userId, ct, fromSource: true);
    }, ct);

    private async Task EnsureNoSourceDeviationAsync(QualityLinkTargetType type, int sourceId, CancellationToken ct,
        string? addressedDeviationNumber = null)
    {
        // All source raises take the source row lock before checking and creating the link.
        var number = await db.QualityRecords.AsNoTracking()
            .Where(r => r.Type == QualityRecordType.Deviation && !r.IsCancelled &&
                r.Links.Any(l => l.TargetType == type && l.TargetId == sourceId))
            .OrderByDescending(r => r.Number == addressedDeviationNumber)
            .ThenBy(r => r.Id).Select(r => r.Number).FirstOrDefaultAsync(ct);
        if (number is not null)
            throw new BusinessRuleException(MessageCodes.DeviationDuplicateSource,
                $"Deviation {number} already links this source.",
                new Dictionary<string, string> { ["number"] = number });
    }

    private async Task EnsureSourceReadableAsync(QualityLinkTargetType type, int id, int userId, CancellationToken ct)
    {
        var source = await new QualityLinkTargets(db, permissions, scope, core).EnsureReadableAsync(type, id, userId, ct);
        if (source.ScopeId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section))
            throw QualityRecordCore.Missing();
    }
}
