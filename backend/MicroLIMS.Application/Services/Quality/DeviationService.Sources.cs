using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Quality;

public sealed partial class DeviationService
{
    public Task<DeviationDto> RaiseFromOosRequestAsync(int oosDeviationRequestId, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequirePermissionAsync(userId, PermissionConstants.QualityApprove, ct);
        var source = await (from candidate in db.OosDeviationRequests.AsNoTracking()
                            join investigation in db.OosInvestigations.AsNoTracking() on candidate.InvestigationId equals investigation.Id
                            where candidate.Id == oosDeviationRequestId
                            select new { investigation.Id, investigation.Number, investigation.SectionId, investigation.OpenedAtUtc })
            .SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();
        await EnsureSourceReadableAsync(QualityLinkTargetType.OosInvestigation, source.Id, userId, ct);

        // Match the investigation row lock used by the existing Address command.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "OosInvestigations" WHERE "Id" = {source.Id} FOR UPDATE""", ct);
        var request = await OosInvestigationService.LoadOpenDeviationRequestAsync(db, oosDeviationRequestId, expectedVersion, ct);
        var deviation = await CreateAsync(new($"Deviation from {source.Number}", request.Reason, source.OpenedAtUtc,
            null, "Raised from an OOS deviation request.", source.SectionId, [new(QualityLinkTargetType.OosInvestigation, source.Id)]),
            userId, ct, fromSource: true);
        await OosInvestigationService.MarkDeviationRequestAddressedAsync(db, request, deviation.Record.Number, userId,
            clock.UtcNow.UtcDateTime, ct);
        return deviation;
    }, ct);

    public Task<DeviationDto> EscalateMonitoringDeviationAsync(int monitoringDeviationId, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        if (!await HoldsAsync(userId, PermissionConstants.QualityApprove, ct))
            await RequirePermissionAsync(userId, PermissionConstants.QualityInvestigate, ct);
        await EnsureSourceReadableAsync(QualityLinkTargetType.MonitoringDeviation, monitoringDeviationId, userId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "MonitoringDeviations" WHERE "Id" = {monitoringDeviationId} FOR UPDATE""", ct);
        var source = await db.MonitoringDeviations.AsNoTracking().Where(d => d.Id == monitoringDeviationId)
            .Select(d => new { d.Version, d.Number, d.Description, d.OpenedAt,
                SectionId = d.MonitoringOccurrence == null ? null : d.MonitoringOccurrence.LabSectionId })
            .SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();
        if (source.Version != expectedVersion) throw QualityRecordCore.Stale();
        return await CreateAsync(new($"Deviation from {source.Number}", source.Description, source.OpenedAt, null,
            "Escalated from a monitoring deviation.", source.SectionId,
            [new(QualityLinkTargetType.MonitoringDeviation, monitoringDeviationId)]), userId, ct, fromSource: true);
    }, ct);

    public Task<DeviationDto> RaiseFromAuditFindingAsync(int findingId, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequirePermissionAsync(userId, PermissionConstants.QualityApprove, ct);
        await EnsureSourceReadableAsync(QualityLinkTargetType.AuditTrailReviewFinding, findingId, userId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "AuditTrailReviewFindings" WHERE "Id" = {findingId} FOR UPDATE""", ct);
        var source = await db.AuditTrailReviewFindings.AsNoTracking().Where(f => f.Id == findingId)
            .Select(f => new { f.Status, f.Note, f.RecordedAtUtc }).SingleOrDefaultAsync(ct)
            ?? throw QualityRecordCore.Missing();

        // A finding can only be edited once, when it moves monotonically from Open to Closed.
        if ((uint)source.Status != expectedVersion) throw QualityRecordCore.Stale();
        return await CreateAsync(new($"Deviation from audit finding {findingId}", source.Note, source.RecordedAtUtc, null,
            "Raised from an audit trail review finding.", null, [new(QualityLinkTargetType.AuditTrailReviewFinding, findingId)]),
            userId, ct, fromSource: true);
    }, ct);

    private async Task EnsureSourceReadableAsync(QualityLinkTargetType type, int id, int userId, CancellationToken ct)
    {
        var source = await new QualityLinkTargets(db, permissions, scope, core).EnsureReadableAsync(type, id, userId, ct);
        if (source.ScopeId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section))
            throw QualityRecordCore.Missing();
    }
}
