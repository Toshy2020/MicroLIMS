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

public sealed class ChangeControlService(
    IMicroLimsDbContext db,
    QualityRecordCore core,
    PermissionService permissions,
    IUserSectionScopeService scope,
    ILabClock clock,
    QualityDueDateService dueDates,
    QualityConfigService config)
{
    internal static bool IsAllowedAffectedType(QualityLinkTargetType targetType) => targetType is
        QualityLinkTargetType.Item or
        QualityLinkTargetType.Specification or
        QualityLinkTargetType.TestDefinition or
        QualityLinkTargetType.Equipment or
        QualityLinkTargetType.DocumentMaster;

    public Task<ChangeControlDto> ProposeAsync(ProposeChangeControlRequest request, int userId,
        CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        db.CurrentUserId = userId;
        var reason = QualityRecordCore.RequiredText(request.Reason, 4000);
        var record = await core.CreateAsync(QualityRecordType.ChangeControl,
            new(request.Title, request.Description, request.SectionId, null), userId, ct);

        var targets = new QualityLinkTargets(db, permissions, scope, core);
        var affectedLinks = new List<QualityRecordLink>();
        var seen = new HashSet<(QualityLinkTargetType, int?, string?)>();
        foreach (var item in request.Affected ?? [])
        {
            var link = await BuildAffectedLinkAsync(record.Id, item, userId, record.RaisedAtUtc, targets, ct);
            if (!seen.Add((link.TargetType, link.TargetId, link.NewRecordCode)))
                throw new BusinessRuleException(MessageCodes.ChangeControlDuplicateAffected,
                    "This affected record is already listed.");
            affectedLinks.Add(link);
            db.QualityRecordLinks.Add(link);
        }

        record.DueDate = await dueDates.ComputeDueAsync(QualityRecordType.ChangeControl, null, clock.LabToday, ct);
        var detail = new ChangeControlDetail
        {
            RecordId = record.Id,
            Status = ChangeControlStatus.Proposed,
            Reason = reason
        };
        db.ChangeControlDetails.Add(detail);
        await db.SaveChangesAsync(ct);
        return ChangeControlDto.From(record, detail, affectedLinks, []);
    }, ct);

    public Task<ChangeControlDto> AddAffectedAsync(int id, ChangeControlAffectedInput input, uint expectedVersion,
        int userId, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityRaise, async (record, detail) =>
        {
            if (detail.Status is not (ChangeControlStatus.Proposed or ChangeControlStatus.Assessed))
                throw new BusinessRuleException(MessageCodes.ChangeControlAffectedFrozen,
                    "Affected records cannot be added after change control approval.");

            var targets = new QualityLinkTargets(db, permissions, scope, core);
            var link = await BuildAffectedLinkAsync(id, input, userId, clock.UtcNow.UtcDateTime, targets, ct);

            if (await db.QualityRecordLinks.AsNoTracking().AnyAsync(l =>
                    l.RecordId == id && l.TargetType == link.TargetType &&
                    l.TargetId == link.TargetId && l.NewRecordCode == link.NewRecordCode, ct))
                throw new BusinessRuleException(MessageCodes.ChangeControlDuplicateAffected,
                    "This affected record is already listed.");

            db.QualityRecordLinks.Add(link);
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<ChangeControlDto> SaveAssessmentAsync(int id, SaveChangeControlAssessmentRequest request,
        uint expectedVersion, int userId, string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityInvestigate, async (record, detail) =>
        {
            RequireStatus(detail, ChangeControlStatus.Proposed);
            var impactSummary = QualityRecordCore.RequiredText(request.ImpactSummary, 4000);
            var today = clock.LabToday;

            var normalized = new List<(string Description, int ResponsibleUserId, DateOnly DueDate)>();
            foreach (var task in request.Tasks ?? [])
            {
                var description = QualityRecordCore.RequiredText(task.Description, 4000);
                await EnsureLaboratoryMemberAsync(task.ResponsibleUserId, record.SectionId,
                    MessageCodes.ChangeControlTaskResponsibleInvalid,
                    "Select an active laboratory user in the change control's section as the task owner.", ct);
                if (task.DueDate < today)
                    throw new BusinessRuleException(MessageCodes.ChangeControlTaskPastDueDate,
                        "A task due date cannot be before the current laboratory date.");
                normalized.Add((description, task.ResponsibleUserId, task.DueDate));
            }

            var existing = await db.QualityActions
                .Where(a => a.RecordId == id && a.Kind == QualityActionKind.ChangeTask)
                .OrderBy(a => a.Id).ToListAsync(ct);
            if (normalized.Count < existing.Count)
                throw new BusinessRuleException(MessageCodes.ChangeControlTaskCannotBeRemoved,
                    "Existing change control tasks cannot be removed.");

            for (var i = 0; i < existing.Count; i++)
            {
                existing[i].Description = normalized[i].Description;
                existing[i].ResponsibleUserId = normalized[i].ResponsibleUserId;
                existing[i].DueDate = normalized[i].DueDate;
            }
            for (var i = existing.Count; i < normalized.Count; i++)
            {
                db.QualityActions.Add(new QualityAction
                {
                    RecordId = id,
                    Kind = QualityActionKind.ChangeTask,
                    Description = normalized[i].Description,
                    ResponsibleUserId = normalized[i].ResponsibleUserId,
                    DueDate = normalized[i].DueDate
                });
            }

            foreach (var answer in request.Answers ?? [])
                await config.RecordAnswerInternalAsync(record, answer.ItemKey, answer.Answer, answer.Comment, userId, ct);

            detail.ImpactSummary = impactSummary;
            record.OwnerUserId ??= userId;
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<ChangeControlDto> SignAssessmentAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityInvestigate, async (record, detail) =>
        {
            RequireStatus(detail, ChangeControlStatus.Proposed);
            if (string.IsNullOrWhiteSpace(detail.ImpactSummary))
                throw new BusinessRuleException(MessageCodes.ChangeControlAssessmentIncomplete,
                    "Save the impact assessment before signing.");

            if (!await config.AreAllActiveItemsAnsweredAsync(record, ct))
                throw new BusinessRuleException(MessageCodes.ChangeControlChecklistIncomplete,
                    "Answer every active checklist item before signing the impact assessment.");

            var tasks = await db.QualityActions.AsNoTracking()
                .Where(a => a.RecordId == id && a.Kind == QualityActionKind.ChangeTask)
                .OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.Description, a.ResponsibleUserId, a.DueDate })
                .ToListAsync(ct);

            var answers = await db.QualityChecklistAnswers.AsNoTracking()
                .Where(a => a.RecordId == id &&
                    !db.QualityChecklistAnswers.Any(later =>
                        later.RecordId == id && later.ItemKey == a.ItemKey && later.Id > a.Id))
                .OrderBy(a => a.ItemKey)
                .Select(a => new { a.ItemKey, a.ItemText, a.Answer, a.Comment })
                .ToListAsync(ct);

            record.OwnerUserId ??= userId;
            await core.SignStepAsync(record, QualityStepKind.ChangeImpactAssessed, SignatureMeaning.ChangeImpactAssessed,
                new { detail.ImpactSummary, Tasks = tasks, Answers = answers }, password, null, userId, ip, ct);

            detail.Status = ChangeControlStatus.Assessed;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<ChangeControlDto> ApproveAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, ChangeControlStatus.Assessed);
            core.EnsureNotRaiserOrOwner(record, userId);

            await core.SignStepAsync(record, QualityStepKind.ChangeApproved, SignatureMeaning.ChangeApproved,
                new { detail.Reason, detail.ImpactSummary }, password, null, userId, ip, ct);

            detail.Status = ChangeControlStatus.ApprovedForImplementation;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<ChangeControlDto> RejectAsync(int id, string reason, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            if (detail.Status is not (ChangeControlStatus.Proposed or ChangeControlStatus.Assessed))
                throw WrongState();
            core.EnsureNotRaiserOrOwner(record, userId);

            var rejectionReason = QualityRecordCore.RequiredText(reason, 4000);
            detail.RejectionReason = rejectionReason;
            db.SetNextAuditReason(detail, rejectionReason);

            await core.SignStepAsync(record, QualityStepKind.ChangeRejected, SignatureMeaning.ChangeRejected,
                new { RejectionReason = rejectionReason }, password, rejectionReason, userId, ip, ct);

            detail.Status = ChangeControlStatus.Rejected;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<ChangeControlDto> CompleteTaskAsync(int actionId, CompleteChangeControlTaskRequest request,
        uint expectedVersion, int userId, string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        var recordId = await db.QualityActions.AsNoTracking()
            .Where(a => a.Id == actionId && a.Kind == QualityActionKind.ChangeTask)
            .Select(a => (int?)a.RecordId).SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();

        var record = await core.LockForWriteAsync(recordId, expectedVersion, userId, ct);
        if (record.Type != QualityRecordType.ChangeControl ||
            (record.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section)))
            throw QualityRecordCore.Missing();
        var detail = await db.ChangeControlDetails.SingleOrDefaultAsync(d => d.RecordId == recordId, ct)
            ?? throw QualityRecordCore.Missing();
        if (record.IsCancelled) throw WrongState();
        RequireStatus(detail, ChangeControlStatus.ApprovedForImplementation);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "QualityActions" WHERE "Id" = {actionId} FOR UPDATE""", ct);
        var action = await db.QualityActions.SingleAsync(a => a.Id == actionId && a.RecordId == recordId, ct);
        if (action.ResponsibleUserId != userId)
            throw QualityRecordCore.Denied();
        if (action.DoneAtUtc is not null)
            throw new BusinessRuleException(MessageCodes.ChangeControlTaskAlreadyDone,
                "This change control task is already completed.");

        var evidence = QualityRecordCore.RequiredText(request.Evidence, 4000);
        if (request.EvidenceLinkId is int linkId &&
            !await db.QualityRecordLinks.AsNoTracking().AnyAsync(l => l.Id == linkId && l.RecordId == recordId, ct))
            throw new BusinessRuleException(MessageCodes.ChangeControlTaskEvidenceLinkInvalid,
                "The selected evidence link does not belong to this change control.");

        db.CurrentUserId = userId;
        action.DoneAtUtc = clock.UtcNow.UtcDateTime;
        action.DoneByUserId = userId;
        action.Evidence = evidence;
        action.EvidenceLinkId = request.EvidenceLinkId;
        core.AdvanceVersion(record);
        await db.SaveChangesAsync(ct);

        return await ProjectDtoAsync(record, detail, ct);
    }, ct);

    public Task<ChangeControlDto> CloseAsync(int id, CloseChangeControlRequest request, uint expectedVersion,
        int userId, string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, ChangeControlStatus.ApprovedForImplementation);
            core.EnsureNotRaiserOrOwner(record, userId);

            var unfinishedTask = await db.QualityActions.AsNoTracking()
                .Where(a => a.RecordId == id && a.Kind == QualityActionKind.ChangeTask && a.DoneAtUtc == null)
                .OrderBy(a => a.Id).Select(a => a.Description).FirstOrDefaultAsync(ct);
            if (unfinishedTask is not null)
                throw new BusinessRuleException(MessageCodes.ChangeControlUnfinishedTasks,
                    $"Complete the unfinished task before closure: {unfinishedTask}",
                    new Dictionary<string, string> { ["task"] = unfinishedTask });

            var hasUnchangedGatedTarget = await db.QualityRecordLinks.AsNoTracking().AnyAsync(l =>
                l.RecordId == id && l.IsAffectedByChange &&
                (l.TargetType == QualityLinkTargetType.Specification || l.TargetType == QualityLinkTargetType.TestDefinition) &&
                l.ChangedAtUtc == null, ct);

            string? justification = null;
            if (string.IsNullOrWhiteSpace(request.UnchangedJustification))
            {
                if (hasUnchangedGatedTarget)
                    throw new BusinessRuleException(MessageCodes.ChangeControlUnchangedJustificationRequired,
                        "Record a justification for affected specifications or test definitions that were not changed.");
            }
            else
            {
                justification = QualityRecordCore.RequiredText(request.UnchangedJustification, 4000);
            }

            var review = QualityRecordCore.RequiredText(request.PostChangeReview, 4000);
            detail.PostChangeReview = review;
            detail.UnchangedJustification = justification;

            await core.SignStepAsync(record, QualityStepKind.ChangeClosed, SignatureMeaning.QualityClosed,
                new { PostChangeReview = review, UnchangedJustification = justification },
                request.Password, null, userId, ip, ct);

            detail.Status = ChangeControlStatus.Closed;
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<ChangeControlDto> CancelAsync(int id, string reason, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            if (detail.Status is ChangeControlStatus.Closed or ChangeControlStatus.Rejected or ChangeControlStatus.Cancelled)
                throw new BusinessRuleException(MessageCodes.ChangeControlFinalCancellation,
                    "A closed, rejected, or cancelled change control cannot be cancelled.");
            detail.Status = ChangeControlStatus.Cancelled;
            await core.CancelAsync(record, reason, password, userId, ip, ct);
        }, ct);

    private async Task<QualityRecordLink> BuildAffectedLinkAsync(int recordId, ChangeControlAffectedInput input,
        int userId, DateTime addedAtUtc, QualityLinkTargets targets, CancellationToken ct)
    {
        if (!IsAllowedAffectedType(input.TargetType))
            throw new BusinessRuleException(MessageCodes.ChangeControlInvalidAffectedType,
                "Affected records must be an item, specification, test definition, equipment, or controlled document.");

        string? newCode = null;
        if (input.TargetId is int targetId)
        {
            await targets.EnsureReadableAsync(input.TargetType, targetId, userId, ct);
        }
        else if (!string.IsNullOrWhiteSpace(input.NewRecordCode))
        {
            newCode = QualityRecordCore.RequiredText(input.NewRecordCode, 100);
        }
        else
        {
            throw new BusinessRuleException(MessageCodes.ChangeControlInvalidAffectedTarget,
                "Specify an existing record or a new record code for each affected item.");
        }

        return new QualityRecordLink
        {
            RecordId = recordId,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            NewRecordCode = newCode,
            IsAffectedByChange = true,
            AddedByUserId = userId,
            AddedAtUtc = addedAtUtc
        };
    }

    private Task<ChangeControlDto> ChangeAsync(int id, uint expectedVersion, int userId, string? permission,
        Func<QualityRecord, ChangeControlDetail, Task> change, CancellationToken ct) => UnitOfWork.RunAsync(db, async () =>
    {
        if (permission is not null) await RequirePermissionAsync(userId, permission, ct);
        var current = await core.LockForWriteAsync(id, expectedVersion, userId, ct);
        if (current.Type != QualityRecordType.ChangeControl ||
            (current.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section)))
            throw QualityRecordCore.Missing();
        var detail = await db.ChangeControlDetails.SingleOrDefaultAsync(d => d.RecordId == id, ct)
            ?? throw QualityRecordCore.Missing();
        if (current.IsCancelled) throw WrongState();

        db.CurrentUserId = userId;
        await change(current, detail);
        return await ProjectDtoAsync(current, detail, ct);
    }, ct);

    private async Task<ChangeControlDto> ProjectDtoAsync(QualityRecord record, ChangeControlDetail detail, CancellationToken ct)
    {
        var affectedLinks = await db.QualityRecordLinks.AsNoTracking()
            .Where(l => l.RecordId == record.Id && l.IsAffectedByChange)
            .OrderBy(l => l.Id).ToListAsync(ct);
        var tasks = await db.QualityActions.AsNoTracking()
            .Where(a => a.RecordId == record.Id && a.Kind == QualityActionKind.ChangeTask)
            .OrderBy(a => a.Id).ToListAsync(ct);
        return ChangeControlDto.From(record, detail, affectedLinks, tasks);
    }

    private async Task EnsureLaboratoryMemberAsync(int targetUserId, int? sectionId, string code, string message, CancellationToken ct)
    {
        var activeLabUser = await db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == targetUserId && u.IsActive && u.Role != null && u.Role.IsActive && u.Role.Type != RoleType.Qa, ct);
        if (!activeLabUser || !await HoldsAsync(targetUserId, PermissionConstants.QualityView, ct) ||
            (sectionId is int section && !(await scope.GetMemberSectionIdsAsync(targetUserId, ct)).Contains(section)))
            throw new BusinessRuleException(code, message);
    }

    private static void RequireStatus(ChangeControlDetail detail, ChangeControlStatus expected)
    {
        if (detail.Status != expected) throw WrongState();
    }

    private static BusinessRuleException WrongState() =>
        new(MessageCodes.ChangeControlWrongState, "This step is unavailable in the change control's current state.");

    private async Task RequirePermissionAsync(int userId, string code, CancellationToken ct)
    {
        if (!await HoldsAsync(userId, code, ct)) throw QualityRecordCore.Denied();
    }

    private Task<bool> HoldsAsync(int userId, string code, CancellationToken ct) =>
        permissions.UserHasPermissionAsync(userId, code, ct);
}
