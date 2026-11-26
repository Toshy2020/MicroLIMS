using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public sealed partial class DeviationService(IMicroLimsDbContext db, QualityRecordCore core,
    PermissionService permissions, IUserSectionScopeService scope, SegregationOfDutiesGuard duties, ILabClock clock,
    QualityDueDateService dueDates, IQualityNotifier? notifier = null, ILogger<DeviationService>? logger = null)
{
    public async Task<DeviationDto> RaiseAsync(RaiseDeviationRequest request, uint? expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await UnitOfWork.RunAsync(db, () => CreateAsync(request, userId, ct), ct);
        await NotifyAsync(n => n.RaisedAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    private async Task<DeviationDto> CreateAsync(RaiseDeviationRequest request, int userId, CancellationToken ct,
        bool fromSource = false)
    {
        if (request.OccurredAtUtc == default || request.OccurredAtUtc.Kind != DateTimeKind.Utc)
            throw new BusinessRuleException(MessageCodes.DeviationOccurrenceUtc, "Enter the deviation occurrence time in UTC.");
        if (request.OccurredAtUtc > clock.UtcNow.UtcDateTime)
            throw new BusinessRuleException(MessageCodes.DeviationFutureOccurrence, "The deviation occurrence time cannot be in the future.");
        var detail = new DeviationDetail
        {
            Status = QualityDeviationStatus.Raised,
            OccurredAtUtc = request.OccurredAtUtc,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : QualityRecordCore.RequiredText(request.Location, 200),
            ImmediateAction = QualityRecordCore.RequiredText(request.ImmediateAction, 4000)
        };
        db.CurrentUserId = userId;
        var record = await core.CreateAsync(QualityRecordType.Deviation,
            new(request.Title, request.Description, request.SectionId, request.Links), userId, ct,
            initialDeviation: detail, fromSource: fromSource);
        return DeviationDto.From(record, detail);
    }

    public async Task<DeviationDto> AssessAsync(int id, AssessDeviationRequest request, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, QualityDeviationStatus.Raised);
            if (!request.Accept)
                throw new BusinessRuleException(MessageCodes.DeviationUseReject, "Use the rejection step to reject a deviation.");
            if (!await db.QualityClassifications.AsNoTracking().AnyAsync(c => c.Id == request.ClassificationId && c.IsActive, ct))
                throw new BusinessRuleException(MessageCodes.DeviationClassification, "Select an active deviation classification.");

            await EnsureOwnerAsync(record, request.OwnerUserId, ct);
            if (request.DueDate is DateOnly requestedDate && requestedDate < clock.LabToday)
                throw new BusinessRuleException(MessageCodes.DeviationPastDueDate, "The due date cannot be before the current laboratory date.");
            var dueDate = request.DueDate ?? await dueDates.ComputeDueAsync(QualityRecordType.Deviation, request.ClassificationId,
                clock.LabToday, ct);
            record.ClassificationId = request.ClassificationId;
            record.OwnerUserId = request.OwnerUserId;
            record.DueDate = dueDate;
            await core.SignStepAsync(record, QualityStepKind.DeviationAssessed, SignatureMeaning.DeviationAssessed,
                new { request.ClassificationId, request.OwnerUserId, DueDate = dueDate }, request.Password, null, userId, ip, ct);

            detail.Status = QualityDeviationStatus.Investigation;
            await db.SaveChangesAsync(ct);
        }, ct);
        await NotifyAsync(n => n.AssignedAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    public async Task<DeviationDto> RejectAsync(int id, string reason, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, QualityDeviationStatus.Raised);
            detail.RejectionReason = QualityRecordCore.RequiredText(reason, 4000);
            detail.Status = QualityDeviationStatus.Rejected;
            db.SetNextAuditReason(detail, detail.RejectionReason);
            await core.SignStepAsync(record, QualityStepKind.DeviationRejected, SignatureMeaning.DeviationRejected,
                new { detail.RejectionReason }, password, detail.RejectionReason, userId, ip, ct);
        }, ct);
        await NotifyAsync(n => n.RejectedAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    public Task<DeviationDto> SaveInvestigationAsync(int id, SaveDeviationInvestigationRequest request, uint expectedVersion,
        int userId, string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, null, async (record, detail) =>
        {
            if (await HoldsAsync(userId, PermissionConstants.QualityApprove, ct) ||
                (record.OwnerUserId != userId && !await HoldsAsync(userId, PermissionConstants.QualityInvestigate, ct)))
                throw QualityRecordCore.Denied();
            RequireStatus(detail, QualityDeviationStatus.Investigation);
            if (!await db.OosCauseCategories.AsNoTracking().AnyAsync(c => c.Id == request.RootCauseCategoryId && c.IsActive, ct))
                throw new BusinessRuleException(MessageCodes.DeviationCause, "Select an active root cause category.");

            detail.ImpactAssessment = QualityRecordCore.RequiredText(request.ImpactAssessment, 4000);
            detail.RootCauseCategoryId = request.RootCauseCategoryId;
            detail.RootCauseDescription = QualityRecordCore.RequiredText(request.RootCauseDescription, 4000);
            detail.ProposedActions = QualityRecordCore.RequiredText(request.ProposedActions, 4000);
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);

    public async Task<DeviationDto> SignLabConclusionAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityLabConclude, async (record, detail) =>
        {
            RequireStatus(detail, QualityDeviationStatus.Investigation);
            if (record.OwnerUserId == userId)
                throw new BusinessRuleException(MessageCodes.DeviationOwnerLabConclusion, "The owner cannot sign the laboratory conclusion.");
            if (string.IsNullOrWhiteSpace(detail.ImpactAssessment) || detail.RootCauseCategoryId is null
                || string.IsNullOrWhiteSpace(detail.RootCauseDescription) || string.IsNullOrWhiteSpace(detail.ProposedActions))
                throw new BusinessRuleException(MessageCodes.DeviationIncomplete, "Complete the investigation before signing the laboratory conclusion.");

            detail.Status = QualityDeviationStatus.LabConcluded;
            var links = await db.QualityRecordLinks.AsNoTracking().Where(l => l.RecordId == id).OrderBy(l => l.Id)
                .Select(l => new { l.TargetType, l.TargetId }).ToListAsync(ct);
            await core.SignStepAsync(record, QualityStepKind.DeviationLabConclusion, SignatureMeaning.QualityLabConclusion,
                new { detail.ImpactAssessment, detail.RootCauseCategoryId, detail.RootCauseDescription, detail.ProposedActions, Links = links },
                password, null, userId, ip, ct);
        }, ct);
        await NotifyAsync(n => n.AwaitingQaAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    public Task<DeviationDto> ApproveAsync(int id, ApproveDeviationRequest request, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, QualityDeviationStatus.LabConcluded);
            core.EnsureNotRaiserOrOwner(record, userId);
            detail.CapaRequired = request.CapaRequired;
            detail.Status = QualityDeviationStatus.Approved;
            await core.SignStepAsync(record, QualityStepKind.DeviationApproved, SignatureMeaning.QualityApproved,
                new { request.CapaRequired, LabConclusionId = record.Steps.Where(s => s.Kind == QualityStepKind.DeviationLabConclusion)
                    .Max(s => s.Id) }, request.Password, request.Comment, userId, ip, ct);
        }, ct);

    public async Task<DeviationDto> ReturnAsync(int id, string comment, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, QualityDeviationStatus.LabConcluded);
            core.EnsureNotRaiserOrOwner(record, userId);
            detail.Status = QualityDeviationStatus.Investigation;
            var reason = QualityRecordCore.RequiredText(comment, 2000);
            db.SetNextAuditReason(detail, reason);
            db.QualityComments.Add(new QualityComment
            {
                RecordId = id, AuthorUserId = userId, Text = reason, CreatedAtUtc = clock.UtcNow.UtcDateTime
            });
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);
        await NotifyAsync(n => n.ReturnedAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    public async Task<DeviationDto> CloseAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, QualityDeviationStatus.Approved);
            core.EnsureNotRaiserOrOwner(record, userId);
            var capaId = detail.CapaRequired == true ? await FindApprovedCapaAsync(id, userId, ct) : null;
            if (detail.CapaRequired == true && capaId is null)
                throw new BusinessRuleException(MessageCodes.DeviationCapaRequired, "A CAPA with an approved plan is required before closure.");

            var unfinishedAction = await db.QualityActions.AsNoTracking().Where(a => a.RecordId == id && a.DoneAtUtc == null)
                .OrderBy(a => a.Id).Select(a => a.Description).FirstOrDefaultAsync(ct);
            if (unfinishedAction is not null)
                throw new BusinessRuleException(MessageCodes.DeviationUnfinishedActions, $"Complete the unfinished action before closure: {unfinishedAction}",
                    new Dictionary<string, string> { ["action"] = unfinishedAction });

            detail.Status = QualityDeviationStatus.Closed;
            await core.SignStepAsync(record, QualityStepKind.DeviationClosed, SignatureMeaning.QualityClosed,
                new { detail.CapaRequired, CapaId = capaId }, password, null, userId, ip, ct);
        }, ct);
        await NotifyAsync(n => n.ClosedAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    public async Task<DeviationDto> CancelAsync(int id, string reason, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            if (detail.Status is not (QualityDeviationStatus.Raised or QualityDeviationStatus.Investigation
                or QualityDeviationStatus.LabConcluded or QualityDeviationStatus.Approved))
                throw new BusinessRuleException(MessageCodes.DeviationFinalCancellation, "A closed or rejected deviation cannot be cancelled.");
            detail.Status = QualityDeviationStatus.Cancelled;
            await core.CancelAsync(record, reason, password, userId, ip, ct);
        }, ct);
        await NotifyAsync(n => n.CancelledAsync(dto.Id, CancellationToken.None), dto.Id);
        return dto;
    }

    private Task<DeviationDto> ChangeAsync(int id, uint expectedVersion, int userId, string? permission,
        Func<QualityRecord, DeviationDetail, Task> change, CancellationToken ct) => UnitOfWork.RunAsync(db, async () =>
    {
        if (permission is not null) await RequirePermissionAsync(userId, permission, ct);
        var current = await core.LockForWriteAsync(id, expectedVersion, userId, ct);
        if (current.Type != QualityRecordType.Deviation ||
            (current.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section)))
            throw QualityRecordCore.Missing();
        var detail = await db.DeviationDetails.SingleOrDefaultAsync(d => d.RecordId == id, ct) ?? throw QualityRecordCore.Missing();
        if (current.IsCancelled) throw WrongState();

        db.CurrentUserId = userId;
        await change(current, detail);
        return DeviationDto.From(current, detail);
    }, ct);

    private async Task EnsureOwnerAsync(QualityRecord record, int ownerId, CancellationToken ct)
    {
        if (!await HoldsAsync(ownerId, PermissionConstants.QualityRaise, ct) ||
            await db.Users.AsNoTracking().AnyAsync(u => u.Id == ownerId && u.Role != null && u.Role.Type == RoleType.Qa, ct) ||
            (record.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(ownerId, ct)).Contains(section)))
            throw new BusinessRuleException(MessageCodes.DeviationInvalidOwner,
                "Select an active laboratory owner who can raise quality records and belongs to the deviation's section.");

        var links = db.QualityRecordLinks.AsNoTracking().Where(l => l.RecordId == record.Id && l.TargetId.HasValue);
        var investigations = links.Where(l => l.TargetType == QualityLinkTargetType.OosInvestigation)
            .Select(l => l.TargetId.GetValueOrDefault());
        var orders = await OosInvestigationService.InvestigationOrderIds(db, investigations)
            .Union(links.Where(l => l.TargetType == QualityLinkTargetType.TestOrder).Select(l => l.TargetId.GetValueOrDefault()))
            .ToListAsync(ct);
        if ((await duties.GetTestOrderIdsPerformedByAsync(ownerId, orders, ct)).Count > 0)
            throw new BusinessRuleException(MessageCodes.DeviationAnalystOwner, "The analyst who produced the result cannot own this deviation.");
    }

    internal static readonly System.Linq.Expressions.Expression<Func<QualityRecord, bool>> IsApprovedCapaExpression =
        r => r.Type == QualityRecordType.Capa && !r.IsCancelled && r.CapaDetail != null &&
            (r.CapaDetail.Status == CapaStatus.PlanApproved || r.CapaDetail.Status == CapaStatus.CompletionVerified ||
             r.CapaDetail.Status == CapaStatus.Closed);

    private async Task<int?> FindApprovedCapaAsync(int id, int userId, CancellationToken ct)
    {
        var readable = await core.ReadableRecordsAsync(userId, ct);
        var eligible = readable.Where(IsApprovedCapaExpression).Where(r =>
            r.CapaDetail!.SourceRecordId == id
                || r.Links.Any(l => l.TargetType == QualityLinkTargetType.QualityRecord && l.TargetId == id)
                || db.QualityRecordLinks.Any(l => l.RecordId == id && l.TargetType == QualityLinkTargetType.QualityRecord && l.TargetId == r.Id));
        var candidates = await eligible.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);
        // ChangeAsync already holds the deviation lock. Always acquire CAPA locks after it.
        foreach (var candidate in candidates)
        {
            await core.LockAsync(candidate, ct);
            if (await eligible.AnyAsync(r => r.Id == candidate, ct))
                return candidate;
        }
        return null;
    }

    private static void RequireStatus(DeviationDetail detail, QualityDeviationStatus expected)
    {
        if (detail.Status != expected) throw WrongState();
    }

    private static BusinessRuleException WrongState() =>
        new(MessageCodes.DeviationWrongState, "This step is unavailable in the deviation's current state.");

    private Task<bool> HoldsAsync(int userId, string permission, CancellationToken ct) =>
        permissions.UserHasPermissionAsync(userId, permission, ct);

    private async Task RequirePermissionAsync(int userId, string permission, CancellationToken ct)
    {
        if (!await HoldsAsync(userId, permission, ct)) throw QualityRecordCore.Denied();
    }

    private Task NotifyAsync(Func<IQualityNotifier, Task> send, int recordId) =>
        QualityNotifier.SendPostCommitAsync(notifier, send, logger, recordId);
}
