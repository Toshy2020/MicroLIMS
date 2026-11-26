using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public sealed partial class QualityConfigService
{
    public Task<QualityExtensionDto> RequestAsync(int recordId, int? actionId, DateOnly requestedDueDate, string reason,
        uint expectedVersion, int userId, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        var record = await core.LockForWriteAsync(recordId, expectedVersion, userId, ct);
        await EnsureMemberAsync(record, userId, ct);
        await EnsureOpenAsync(recordId, ct);
        var action = await LockActionAsync(recordId, actionId, ct);
        var allowedActionRequest = action is null
            ? record.OwnerUserId == userId
            : action.ResponsibleUserId == userId || record.OwnerUserId == userId;
        if (!allowedActionRequest)
            throw QualityRecordCore.Denied();
        EnsureLater(requestedDueDate, action?.DueDate ?? record.DueDate);

        var request = new QualityExtensionRequest
        {
            RecordId = recordId, ActionId = actionId, RequestedDueDate = requestedDueDate,
            Reason = QualityRecordCore.RequiredText(reason, 4000), RequestedByUserId = userId,
            RequestedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.QualityExtensionRequests.Add(request);
        core.AdvanceVersion(record);
        await db.SaveChangesAsync(ct);
        var requestedByName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return ToExtensionDto(request, record.Version, requestedByName, null);
    }, ct);

    public Task<QualityExtensionDto> DecideAsync(int requestId, bool approve, string? comment, string password,
        uint expectedVersion, int userId, string? ip = null, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequireAsync(userId, PermissionConstants.QualityApprove, ct);
        var readable = await core.ReadableRecordsAsync(userId, ct);
        var recordId = await readable.Where(r => r.ExtensionRequests.Any(e => e.Id == requestId))
            .Select(r => (int?)r.Id).SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();
        await core.LockAsync(recordId, ct);
        // Reload the record's current version under the lock; only the extension request If-Match is enforced here.
        var recordVersion = await readable.Where(r => r.Id == recordId).Select(r => (uint?)r.Version)
            .SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();
        var record = await core.LoadForWriteAsync(recordId, recordVersion, userId, ct);
        await EnsureOpenAsync(recordId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "QualityExtensionRequests" WHERE "Id" = {requestId} FOR UPDATE""", ct);
        var request = await db.QualityExtensionRequests.SingleAsync(e => e.Id == requestId && e.RecordId == recordId, ct);
        if (request.Version != expectedVersion) throw QualityRecordCore.Stale();
        if (request.Decision is not null)
            throw new BusinessRuleException(MessageCodes.QualityExtensionAlreadyDecided, "This extension request has already been decided.");
        if (request.RequestedByUserId == userId)
            throw new BusinessRuleException(MessageCodes.QualityExtensionSelfDecision, "You cannot decide your own extension request.");
        core.EnsureNotRaiserOrOwner(record, userId);

        var action = await LockActionAsync(recordId, request.ActionId, ct);
        var previousDueDate = action?.DueDate ?? record.DueDate;
        var decision = approve ? QualityExtensionDecision.Approved : QualityExtensionDecision.Refused;
        comment = comment is null ? null : QualityRecordCore.RequiredText(comment, 4000);
        if (approve) EnsureLater(request.RequestedDueDate, previousDueDate);

        void ApplyApprovedDate()
        {
            if (!approve) return;
            if (action is null)
            {
                record.DueDate = request.RequestedDueDate;
                db.SetNextAuditReason(record, comment ?? request.Reason);
            }
            else
            {
                action.DueDate = request.RequestedDueDate;
                db.SetNextAuditReason(action, comment ?? request.Reason);
            }
        }

        QualityRecordStep step;
        try
        {
            step = await core.SignStepAsync(record, QualityStepKind.QualityExtensionDecided,
                SignatureMeaning.QualityExtensionDecided, new
                {
                    RequestId = request.Id, request.ActionId, request.RequestedByUserId, request.RequestedAtUtc,
                    request.Reason, request.RequestedDueDate, PreviousDueDate = previousDueDate,
                    DueDate = approve ? request.RequestedDueDate : previousDueDate, Decision = decision, Comment = comment
                }, password, comment, userId, ip, ct, afterVerification: ApplyApprovedDate);
        }
        catch (SignatureVerificationException)
        {
            throw new BusinessRuleException(MessageCodes.QualityExtensionSignatureFailed, "The signature could not be verified.");
        }
        request.Decision = decision;
        request.DecidedByUserId = userId;
        request.DecidedAtUtc = step.SignedAtUtc;
        request.DecisionComment = comment;
        request.SignatureId = step.SignatureId;
        db.SetNextAuditReason(request, comment ?? request.Reason);
        await db.SaveChangesAsync(ct);
        var requestedByName = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.RequestedByUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;
        var decidedByName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return ToExtensionDto(request, record.Version, requestedByName, decidedByName);
    }, ct);

    public async Task<IReadOnlyList<QualityExtensionDto>> ListExtensionsAsync(int recordId, int page, int pageSize,
        int userId, CancellationToken ct = default)
    {
        var records = (await core.ReadableRecordsAsync(userId, ct)).Where(r => r.Id == recordId);
        if (!await records.AnyAsync(ct)) throw QualityRecordCore.Missing();
        var (skip, take) = PageWindow(page, pageSize);
        return await records.SelectMany(r => r.ExtensionRequests, (r, e) => new { r.Version, e })
            .OrderByDescending(x => x.e.Id).Skip(skip).Take(take)
            .Select(x => new QualityExtensionDto(x.e.Id, x.e.RecordId, x.e.ActionId, x.e.RequestedDueDate, x.e.Reason,
                x.e.RequestedByUserId, x.e.RequestedAtUtc, x.e.Decision, x.e.DecidedByUserId, x.e.DecidedAtUtc,
                x.e.DecisionComment, x.e.SignatureId, x.e.Version, x.Version,
                db.Users.Where(u => u.Id == x.e.RequestedByUserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                x.e.DecidedByUserId == null ? null : db.Users.Where(u => u.Id == x.e.DecidedByUserId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<QualityOverdueDto> ListOverdueAsync(DateOnly labDate, int userId, int page = 1, int pageSize = 100,
        CancellationToken ct = default)
    {
        await core.EnsureViewAsync(userId, ct);
        var open = (await core.ReadableRecordsAsync(userId, ct)).Where(IsOpenExpression);
        var (skip, take) = PageWindow(page, pageSize);
        var records = await open.Where(r => r.DueDate < labDate).OrderBy(r => r.DueDate).ThenBy(r => r.Id)
            .Skip(skip).Take(take).Select(r => new OverdueQualityRecordDto(r.Id, r.Number, r.Title,
                r.DueDate.GetValueOrDefault(), r.OwnerUserId)).ToListAsync(ct);
        var actions = await open.Where(IsActionExecutionActiveExpression)
            .SelectMany(r => r.Actions.Where(a => a.DoneAtUtc == null && a.DueDate < labDate),
                (r, a) => new { r.Id, r.Number, a })
            .OrderBy(x => x.a.DueDate).ThenBy(x => x.a.Id).Skip(skip).Take(take)
            .Select(x => new OverdueQualityActionDto(x.Id, x.Number, x.a.Id, x.a.Description, x.a.DueDate, x.a.ResponsibleUserId))
            .ToListAsync(ct);
        return new QualityOverdueDto(records, actions);
    }

    private async Task<QualityAction?> LockActionAsync(int recordId, int? actionId, CancellationToken ct)
    {
        if (actionId is not int id) return null;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT "Id" FROM "QualityActions" WHERE "RecordId" = {recordId} AND "Id" = {id} FOR UPDATE""", ct);
        return await db.QualityActions.SingleOrDefaultAsync(a => a.RecordId == recordId && a.Id == id, ct)
            ?? throw QualityRecordCore.Missing();
    }

    private static void EnsureLater(DateOnly requested, DateOnly? current)
    {
        if (requested == default || requested <= current)
            throw new BusinessRuleException(MessageCodes.QualityExtensionDate, "Enter a date later than the current due date.");
    }

    private static QualityExtensionDto ToExtensionDto(QualityExtensionRequest e, uint version,
        string requestedByName, string? decidedByName) =>
        new(e.Id, e.RecordId, e.ActionId, e.RequestedDueDate, e.Reason, e.RequestedByUserId, e.RequestedAtUtc,
            e.Decision, e.DecidedByUserId, e.DecidedAtUtc, e.DecisionComment, e.SignatureId, e.Version, version,
            requestedByName, decidedByName);
}
