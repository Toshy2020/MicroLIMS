using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public sealed class CapaService(
    IMicroLimsDbContext db,
    QualityRecordCore core,
    PermissionService permissions,
    IUserSectionScopeService scope,
    ILabClock clock,
    QualityDueDateService dueDates)
{
    public Task<CapaDto> CreateAsync(CreateCapaRequest request, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            db.CurrentUserId = userId;
            var record = await core.CreateAsync(QualityRecordType.Capa,
                new(request.Title, request.Description, request.SectionId, request.Links), userId, ct);

            await EnsureLaboratoryMemberAsync(request.OwnerUserId, record.SectionId,
                MessageCodes.CapaInvalidOwner, "Select an active laboratory user in the CAPA's section as the owner.", ct);

            if (request.SourceRecordId is int sourceId)
            {
                var readable = await core.ReadableRecordsAsync(userId, ct);
                if (!await readable.AnyAsync(r => r.Id == sourceId, ct))
                    throw QualityRecordCore.Missing();
            }

            record.OwnerUserId = request.OwnerUserId;
            record.DueDate = await dueDates.ComputeDueAsync(QualityRecordType.Capa, null, clock.LabToday, ct);

            var detail = new CapaDetail
            {
                RecordId = record.Id,
                Status = CapaStatus.Plan,
                SourceRecordId = request.SourceRecordId
            };
            db.CapaDetails.Add(detail);
            await db.SaveChangesAsync(ct);
            return CapaDto.From(record, detail, []);
        }, ct);

    public Task<CapaDto> SavePlanAsync(int id, SaveCapaPlanRequest request, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, null, async (record, detail) =>
        {
            if (record.OwnerUserId != userId || await HoldsAsync(userId, PermissionConstants.QualityApprove, ct))
                throw QualityRecordCore.Denied();
            RequireStatus(detail, CapaStatus.Plan);

            if (request.Actions is not { Count: > 0 })
                throw new BusinessRuleException(MessageCodes.CapaPlanEmpty, "Add at least one action to the CAPA plan.");

            var criteria = QualityRecordCore.RequiredText(request.EffectivenessCriteria, 4000);
            var method = QualityRecordCore.RequiredText(request.EffectivenessMethod, 4000);
            var today = clock.LabToday;

            var normalized = new List<(QualityActionKind Kind, string Description, int ResponsibleUserId, DateOnly DueDate)>(request.Actions.Count);
            foreach (var action in request.Actions)
            {
                if (action.Kind is not (QualityActionKind.Corrective or QualityActionKind.Preventive))
                    throw new BusinessRuleException(MessageCodes.CapaActionKindInvalid, "Select a corrective or preventive action kind.");
                var description = QualityRecordCore.RequiredText(action.Description, 4000);
                await EnsureLaboratoryMemberAsync(action.ResponsibleUserId, record.SectionId,
                    MessageCodes.CapaActionResponsibleInvalid,
                    "Select an active laboratory user in the CAPA's section as the responsible person.", ct);
                if (action.DueDate < today)
                    throw new BusinessRuleException(MessageCodes.CapaActionPastDueDate,
                        "An action due date cannot be before the current laboratory date.");
                normalized.Add((action.Kind, description, action.ResponsibleUserId, action.DueDate));
            }

            var lastActionDue = normalized.Max(a => a.DueDate);
            if (request.EffectivenessPlannedDate <= lastActionDue)
                throw new BusinessRuleException(MessageCodes.CapaEffectivenessDateOrder,
                    "The effectiveness check planned date must be after the last action due date.");

            var existing = await db.QualityActions.Where(a => a.RecordId == id).OrderBy(a => a.Id).ToListAsync(ct);
            if (normalized.Count < existing.Count)
                throw new BusinessRuleException(MessageCodes.CapaActionCannotBeRemoved, "Existing CAPA actions cannot be removed.");

            for (var i = 0; i < existing.Count; i++)
            {
                existing[i].Kind = normalized[i].Kind;
                existing[i].Description = normalized[i].Description;
                existing[i].ResponsibleUserId = normalized[i].ResponsibleUserId;
                existing[i].DueDate = normalized[i].DueDate;
            }
            for (var i = existing.Count; i < normalized.Count; i++)
            {
                db.QualityActions.Add(new QualityAction
                {
                    RecordId = id,
                    Kind = normalized[i].Kind,
                    Description = normalized[i].Description,
                    ResponsibleUserId = normalized[i].ResponsibleUserId,
                    DueDate = normalized[i].DueDate
                });
            }

            detail.EffectivenessCriteria = criteria;
            detail.EffectivenessMethod = method;
            detail.EffectivenessPlannedDate = request.EffectivenessPlannedDate;
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<CapaDto> ApprovePlanAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, CapaStatus.Plan);
            core.EnsureNotRaiserOrOwner(record, userId);

            var actions = await db.QualityActions.AsNoTracking().Where(a => a.RecordId == id).OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.Kind, a.Description, a.ResponsibleUserId, a.DueDate }).ToListAsync(ct);
            if (actions.Count == 0 || string.IsNullOrWhiteSpace(detail.EffectivenessCriteria) ||
                string.IsNullOrWhiteSpace(detail.EffectivenessMethod) || detail.EffectivenessPlannedDate is null)
                throw new BusinessRuleException(MessageCodes.CapaPlanIncomplete,
                    "Save the CAPA plan with actions and an effectiveness check before approval.");

            await core.SignStepAsync(record, QualityStepKind.CapaPlanApproved, SignatureMeaning.CapaPlanApproved,
                new { detail.EffectivenessCriteria, detail.EffectivenessMethod, detail.EffectivenessPlannedDate, Actions = actions },
                password, null, userId, ip, ct);

            detail.Status = CapaStatus.PlanApproved;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<CapaDto> ReturnPlanAsync(int id, string comment, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, CapaStatus.Plan);
            core.EnsureNotRaiserOrOwner(record, userId);
            var reason = QualityRecordCore.RequiredText(comment, 2000);
            db.SetNextAuditReason(detail, reason);
            db.QualityComments.Add(new QualityComment
            {
                RecordId = id, AuthorUserId = userId, Text = reason, CreatedAtUtc = clock.UtcNow.UtcDateTime
            });
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<CapaDto> CompleteActionAsync(int actionId, CompleteCapaActionRequest request, uint expectedVersion,
        int userId, string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        var recordId = await db.QualityActions.AsNoTracking().Where(a => a.Id == actionId)
            .Select(a => (int?)a.RecordId).SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();

        var record = await core.LockForWriteAsync(recordId, expectedVersion, userId, ct);
        if (record.Type != QualityRecordType.Capa ||
            (record.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section)))
            throw QualityRecordCore.Missing();
        var detail = await db.CapaDetails.SingleOrDefaultAsync(d => d.RecordId == recordId, ct) ?? throw QualityRecordCore.Missing();
        if (record.IsCancelled) throw WrongState();
        RequireStatus(detail, CapaStatus.PlanApproved);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "QualityActions" WHERE "Id" = {actionId} FOR UPDATE""", ct);
        var action = await db.QualityActions.SingleAsync(a => a.Id == actionId && a.RecordId == recordId, ct);
        if (action.ResponsibleUserId != userId)
            throw QualityRecordCore.Denied();
        if (action.DoneAtUtc is not null)
            throw new BusinessRuleException(MessageCodes.CapaActionAlreadyDone, "This CAPA action is already completed.");

        var evidence = QualityRecordCore.RequiredText(request.Evidence, 4000);
        if (request.EvidenceLinkId is int linkId &&
            !await db.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.Id == linkId && l.RecordId == recordId, ct))
            throw new BusinessRuleException(MessageCodes.CapaActionEvidenceLinkInvalid,
                "The selected evidence link does not belong to this CAPA.");

        db.CurrentUserId = userId;
        action.DoneAtUtc = clock.UtcNow.UtcDateTime;
        action.DoneByUserId = userId;
        action.Evidence = evidence;
        action.EvidenceLinkId = request.EvidenceLinkId;
        core.AdvanceVersion(record);
        await db.SaveChangesAsync(ct);

        var actions = await db.QualityActions.AsNoTracking().Where(a => a.RecordId == recordId).OrderBy(a => a.Id).ToListAsync(ct);
        return CapaDto.From(record, detail, actions);
    }, ct);

    public Task<CapaDto> VerifyCompletionAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, CapaStatus.PlanApproved);
            core.EnsureNotRaiserOrOwner(record, userId);

            var unfinishedAction = await db.QualityActions.AsNoTracking().Where(a => a.RecordId == id && a.DoneAtUtc == null)
                .OrderBy(a => a.Id).Select(a => a.Description).FirstOrDefaultAsync(ct);
            if (unfinishedAction is not null)
                throw new BusinessRuleException(MessageCodes.CapaUnfinishedActions,
                    $"Complete the unfinished action before verification: {unfinishedAction}",
                    new Dictionary<string, string> { ["action"] = unfinishedAction });

            var actions = await db.QualityActions.AsNoTracking().Where(a => a.RecordId == id).OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.DoneAtUtc, a.DoneByUserId, a.Evidence, a.EvidenceLinkId }).ToListAsync(ct);

            await core.SignStepAsync(record, QualityStepKind.CapaCompletionVerified, SignatureMeaning.CapaCompletionVerified,
                new { Actions = actions }, password, null, userId, ip, ct);

            detail.Status = CapaStatus.CompletionVerified;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<CapaDto> SignEffectivenessAsync(int id, SignCapaEffectivenessRequest request, uint expectedVersion,
        int userId, string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, CapaStatus.CompletionVerified);
            core.EnsureNotRaiserOrOwner(record, userId);

            if (detail.EffectivenessPlannedDate is null || clock.LabToday < detail.EffectivenessPlannedDate.Value)
                throw new BusinessRuleException(MessageCodes.CapaEffectivenessEarly,
                    "The effectiveness check cannot be signed before its planned date.");

            var findings = QualityRecordCore.RequiredText(request.Findings, 4000);
            int? successorId = null;
            if (request.Result == CapaEffectiveness.NotEffective)
            {
                if (request.NewCapaId is not int newCapaId || newCapaId == id ||
                    !await db.QualityRecords.AsNoTracking().AnyAsync(r => r.Id == newCapaId && r.Type == QualityRecordType.Capa
                        && !r.IsCancelled && r.CapaDetail != null && r.CapaDetail.SourceRecordId == id, ct))
                    throw new BusinessRuleException(MessageCodes.CapaNewCapaRequired,
                        "A not-effective CAPA requires a linked new CAPA created from this record.");
                successorId = newCapaId;
            }

            detail.EffectivenessResult = request.Result;
            detail.EffectivenessFindings = findings;
            detail.SuccessorCapaId = successorId;

            await core.SignStepAsync(record, QualityStepKind.CapaEffectivenessChecked, SignatureMeaning.CapaEffectivenessChecked,
                new { request.Result, Findings = findings, SuccessorCapaId = successorId },
                request.Password, null, userId, ip, ct);

            detail.Status = CapaStatus.Closed;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<CapaDto> CancelAsync(int id, string reason, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            if (detail.Status is CapaStatus.Closed or CapaStatus.Cancelled)
                throw new BusinessRuleException(MessageCodes.CapaFinalCancellation, "A closed or cancelled CAPA cannot be cancelled.");
            detail.Status = CapaStatus.Cancelled;
            await core.CancelAsync(record, reason, password, userId, ip, ct);
        }, ct);

    private Task<CapaDto> ChangeAsync(int id, uint expectedVersion, int userId, string? permission,
        Func<QualityRecord, CapaDetail, Task> change, CancellationToken ct) => UnitOfWork.RunAsync(db, async () =>
    {
        if (permission is not null) await RequirePermissionAsync(userId, permission, ct);
        var current = await core.LockForWriteAsync(id, expectedVersion, userId, ct);
        if (current.Type != QualityRecordType.Capa ||
            (current.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section)))
            throw QualityRecordCore.Missing();
        var detail = await db.CapaDetails.SingleOrDefaultAsync(d => d.RecordId == id, ct) ?? throw QualityRecordCore.Missing();
        if (current.IsCancelled) throw WrongState();

        db.CurrentUserId = userId;
        await change(current, detail);
        var actions = await db.QualityActions.AsNoTracking().Where(a => a.RecordId == id).OrderBy(a => a.Id).ToListAsync(ct);
        return CapaDto.From(current, detail, actions);
    }, ct);

    private async Task EnsureLaboratoryMemberAsync(int targetUserId, int? sectionId, string code, string message, CancellationToken ct)
    {
        var activeLabUser = await db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == targetUserId && u.IsActive && u.Role != null && u.Role.IsActive && u.Role.Type != RoleType.Qa, ct);
        if (!activeLabUser || !await HoldsAsync(targetUserId, PermissionConstants.QualityView, ct) ||
            (sectionId is int section && !(await scope.GetMemberSectionIdsAsync(targetUserId, ct)).Contains(section)))
            throw new BusinessRuleException(code, message);
    }

    private static void RequireStatus(CapaDetail detail, CapaStatus expected)
    {
        if (detail.Status != expected) throw WrongState();
    }

    private static BusinessRuleException WrongState() =>
        new(MessageCodes.CapaWrongState, "This step is unavailable in the CAPA's current state.");

    private Task<bool> HoldsAsync(int userId, string permission, CancellationToken ct) =>
        permissions.UserHasPermissionAsync(userId, permission, ct);

    private async Task RequirePermissionAsync(int userId, string permission, CancellationToken ct)
    {
        if (!await HoldsAsync(userId, permission, ct)) throw QualityRecordCore.Denied();
    }
}
