using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed record OosDeviationRequestDto(int Id, uint Version);

public sealed record OosOverdueInvestigation(int Id, string Number, int SampleId, int SectionId,
    OosInvestigationStatus Status, DateOnly? PhaseOneDueDate, DateOnly? DueDate);

public sealed partial class OosInvestigationService
{
    public Task<OosDeviationRequestDto> RequestDeviationAsync(int investigationId, string reason, int userId,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            await EnsureInvestigationPermissionAsync(userId, PermissionConstants.OosRequestDeviation, null, ct);
            var investigation = await LockForDeviationAsync(investigationId, userId, SectionAccess.Read, ct);
            CheckVersion(investigation.Version, db.ExpectedVersion);
            if (!ValidText(reason))
                throw new BusinessRuleException(MessageCodes.OosDeviationReasonInvalid, "Enter a deviation request reason of 1 to 4000 characters.");

            var request = new OosDeviationRequest
            {
                InvestigationId = investigationId,
                Reason = reason.Trim(),
                RequestedByUserId = userId,
                RequestedAtUtc = time.GetUtcNow().UtcDateTime,
                Status = OosDeviationRequestStatus.Open
            };
            db.CurrentUserId = userId;
            db.SetNextAuditReason(request, request.Reason);
            db.OosDeviationRequests.Add(request);
            await db.SaveChangesAsync(ct);
            return new OosDeviationRequestDto(request.Id, request.Version);
        }, ct);

    public Task AddressDeviationRequestAsync(int requestId, string comment, uint expectedVersion, int userId,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            await EnsureInvestigationPermissionAsync(userId, PermissionConstants.OosManage, null, ct);
            var investigationId = await db.OosDeviationRequests.AsNoTracking().Where(r => r.Id == requestId)
                .Select(r => (int?)r.InvestigationId).SingleOrDefaultAsync(ct) ?? throw DeviationRequestNotFound();
            await LockForDeviationAsync(investigationId, userId, SectionAccess.Write, ct);

            var request = await db.OosDeviationRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct)
                ?? throw DeviationRequestNotFound();
            // A caller may already be tracking the request. Refresh it only after acquiring the investigation lock.
            await db.Entry(request).ReloadAsync(ct);
            CheckVersion(request.Version, expectedVersion);
            if (request.Status != OosDeviationRequestStatus.Open)
                throw new BusinessRuleException(MessageCodes.OosDeviationAlreadyAddressed, "This deviation request has already been addressed.");
            if (!ValidText(comment))
                throw new BusinessRuleException(MessageCodes.OosDeviationCommentInvalid, "Enter an addressing comment of 1 to 4000 characters.");

            request.Status = OosDeviationRequestStatus.Addressed;
            request.AddressedComment = comment.Trim();
            request.AddressedByUserId = userId;
            request.AddressedAtUtc = time.GetUtcNow().UtcDateTime;
            db.CurrentUserId = userId;
            db.SetNextAuditReason(request, request.AddressedComment);
            await db.SaveChangesAsync(ct);
        }, ct);

    public async Task<IReadOnlyList<OosOverdueInvestigation>> ListOverdueAsync(DateOnly nowLabDate, int take = 100,
        CancellationToken ct = default) =>
        await db.OosInvestigations.AsNoTracking()
            .Where(i => i.Status != OosInvestigationStatus.Concluded &&
                (i.DueDate < nowLabDate ||
                 (i.Status == OosInvestigationStatus.PhaseOne && i.PhaseOneDueDate < nowLabDate)))
            .OrderBy(i => i.Id)
            .Take(Math.Clamp(take, 1, 100))
            .Select(i => new OosOverdueInvestigation(i.Id, i.Number, i.SampleId, i.SectionId, i.Status, i.PhaseOneDueDate, i.DueDate))
            .ToListAsync(ct);

    private async Task<OosInvestigation> LockForDeviationAsync(int investigationId, int userId, SectionAccess access, CancellationToken ct)
    {
        var target = db.OosInvestigations.Where(i => i.Id == investigationId);
        var sectionId = await target.Select(i => (int?)i.SectionId).SingleOrDefaultAsync(ct) ?? throw InvestigationNotFound();
        await EnsureScopeAsync(userId, sectionId, access, ct);
        return await LockInvestigationAsync(target, closed: null, ct, anyStatus: true) ?? throw InvestigationNotFound();
    }

    private static NotFoundException DeviationRequestNotFound() =>
        new(MessageCodes.OosDeviationRequestNotFound, "The OOS deviation request was not found.");
}
