using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public sealed class ComplaintService(
    IMicroLimsDbContext db,
    QualityRecordCore core,
    PermissionService permissions,
    IUserSectionScopeService scope,
    ILabClock clock,
    QualityDueDateService dueDates,
    IQualityNotifier? notifier = null,
    ILogger<ComplaintService>? logger = null)
{
    public async Task<ComplaintDto> ReceiveAsync(ReceiveComplaintRequest request, int userId,
        CancellationToken ct = default)
    {
        var dto = await UnitOfWork.RunAsync(db, async () =>
        {
            await RequirePermissionAsync(userId, PermissionConstants.QualityApprove, ct);

            var source = QualityRecordCore.RequiredText(request.Source, 200);
            if (request.ReceivedOn == default || request.ReceivedOn > clock.LabToday)
                throw new BusinessRuleException(MessageCodes.ComplaintFutureReceivedOn,
                    "The complaint received date cannot be in the future.");
            var externalReference = string.IsNullOrWhiteSpace(request.ExternalReference)
                ? null
                : QualityRecordCore.RequiredText(request.ExternalReference, 200);
            var lotText = string.IsNullOrWhiteSpace(request.LotText)
                ? null
                : QualityRecordCore.RequiredText(request.LotText, 200);
            var complaintText = QualityRecordCore.RequiredText(request.ComplaintText, 4000);

            if (request.ItemId is int itemId &&
                !await db.Items.AsNoTracking().AnyAsync(i => i.Id == itemId && i.IsActive, ct))
                throw QualityRecordCore.Missing();

            db.CurrentUserId = userId;
            var record = await core.CreateAsync(QualityRecordType.Complaint,
                new(request.Title, complaintText, request.SectionId, null), userId, ct);

            var detail = new ComplaintDetail
            {
                RecordId = record.Id,
                Status = ComplaintStatus.Received,
                Source = source,
                ReceivedOn = request.ReceivedOn,
                ExternalReference = externalReference,
                ItemId = request.ItemId,
                LotText = lotText,
                ComplaintText = complaintText
            };
            db.ComplaintDetails.Add(detail);
            await db.SaveChangesAsync(ct);
            return ComplaintDto.From(record, detail);
        }, ct);
        await QualityNotifier.SendPostCommitAsync(notifier, n => n.RaisedAsync(dto.Id, ct), logger, dto.Id);
        return dto;
    }

    public async Task<ComplaintDto> RequestInvestigationAsync(int id, RequestComplaintInvestigationRequest request,
        uint expectedVersion, int userId, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, ComplaintStatus.Received);
            if (!await db.QualityClassifications.AsNoTracking()
                    .AnyAsync(c => c.Id == request.ClassificationId && c.IsActive, ct))
                throw new BusinessRuleException(MessageCodes.ComplaintClassification,
                    "Select an active complaint classification.");

            await EnsureOwnerAsync(record, request.OwnerUserId, ct);
            if (request.DueDate is DateOnly requestedDate && requestedDate < clock.LabToday)
                throw new BusinessRuleException(MessageCodes.ComplaintPastDueDate,
                    "The due date cannot be before the current laboratory date.");
            var dueDate = request.DueDate ?? await dueDates.ComputeDueAsync(
                QualityRecordType.Complaint, request.ClassificationId, clock.LabToday, ct);

            detail.WhatToTest = QualityRecordCore.RequiredText(request.WhatToTest, 4000);
            detail.Status = ComplaintStatus.Investigation;
            record.ClassificationId = request.ClassificationId;
            record.OwnerUserId = request.OwnerUserId;
            record.DueDate = dueDate;
            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);
        await QualityNotifier.SendPostCommitAsync(notifier, n => n.AssignedAsync(dto.Id, ct), logger, dto.Id);
        return dto;
    }

    public Task<ComplaintDto> SaveInvestigationAsync(int id, SaveComplaintInvestigationRequest request,
        uint expectedVersion, int userId, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, null, async (record, detail) =>
        {
            if (await HoldsAsync(userId, PermissionConstants.QualityApprove, ct) ||
                (record.OwnerUserId != userId && !await HoldsAsync(userId, PermissionConstants.QualityInvestigate, ct)))
                throw QualityRecordCore.Denied();
            RequireStatus(detail, ComplaintStatus.Investigation);

            detail.Findings = QualityRecordCore.RequiredText(request.Findings, 4000);

            if (request.SampleIds is { Count: > 0 } sampleIds)
            {
                var targets = new QualityLinkTargets(db, permissions, scope, core);
                foreach (var sampleId in sampleIds.Distinct())
                {
                    await targets.EnsureReadableAsync(QualityLinkTargetType.Sample, sampleId, userId, ct);
                    if (!await db.QualityRecordLinks.AsNoTracking().AnyAsync(l =>
                            l.RecordId == id && l.TargetType == QualityLinkTargetType.Sample && l.TargetId == sampleId, ct))
                    {
                        db.QualityRecordLinks.Add(new QualityRecordLink
                        {
                            RecordId = id,
                            TargetType = QualityLinkTargetType.Sample,
                            TargetId = sampleId,
                            AddedByUserId = userId,
                            AddedAtUtc = clock.UtcNow.UtcDateTime
                        });
                    }
                }
            }

            core.AdvanceVersion(record);
            await db.SaveChangesAsync(ct);
        }, ct);

    public async Task<ComplaintDto> SignLabReportAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityLabConclude, async (record, detail) =>
        {
            RequireStatus(detail, ComplaintStatus.Investigation);
            if (record.OwnerUserId == userId)
                throw new BusinessRuleException(MessageCodes.ComplaintOwnerLabReport,
                    "The owner cannot sign the laboratory conclusion.");
            if (string.IsNullOrWhiteSpace(detail.Findings))
                throw new BusinessRuleException(MessageCodes.ComplaintIncomplete,
                    "Complete the investigation findings before signing the laboratory report.");

            detail.Status = ComplaintStatus.LabReported;
            var links = await db.QualityRecordLinks.AsNoTracking()
                .Where(l => l.RecordId == id)
                .OrderBy(l => l.Id)
                .Select(l => new { l.TargetType, l.TargetId })
                .ToListAsync(ct);

            await core.SignStepAsync(record, QualityStepKind.ComplaintLabReport, SignatureMeaning.QualityLabConclusion,
                new { detail.WhatToTest, detail.Findings, Links = links }, password, null, userId, ip, ct);
        }, ct);
        await QualityNotifier.SendPostCommitAsync(notifier, n => n.AwaitingQaAsync(dto.Id, ct), logger, dto.Id);
        return dto;
    }

    public Task<ComplaintDto> ConcludeAsync(int id, ConcludeComplaintRequest request, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, ComplaintStatus.LabReported);
            core.EnsureNotRaiserOrOwner(record, userId);

            if (!Enum.IsDefined(request.Outcome))
                throw new BusinessRuleException(MessageCodes.ComplaintOutcomeInvalid,
                    "Select a valid complaint outcome.");

            var summary = QualityRecordCore.RequiredText(request.Summary, 4000);
            detail.Outcome = request.Outcome;
            detail.ConclusionSummary = summary;
            detail.Status = ComplaintStatus.Concluded;

            await core.SignStepAsync(record, QualityStepKind.ComplaintConcluded, SignatureMeaning.ComplaintConcluded,
                new
                {
                    detail.Outcome,
                    detail.ConclusionSummary,
                    LabReportId = record.Steps.Where(s => s.Kind == QualityStepKind.ComplaintLabReport).Max(s => s.Id)
                }, request.Password, null, userId, ip, ct);
        }, ct);

    public async Task<ComplaintDto> CloseAsync(int id, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default)
    {
        var dto = await ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            RequireStatus(detail, ComplaintStatus.Concluded);
            core.EnsureNotRaiserOrOwner(record, userId);

            var unfinishedAction = await db.QualityActions.AsNoTracking()
                .Where(a => a.RecordId == id && a.DoneAtUtc == null)
                .OrderBy(a => a.Id)
                .Select(a => a.Description)
                .FirstOrDefaultAsync(ct);
            if (unfinishedAction is not null)
                throw new BusinessRuleException(MessageCodes.ComplaintUnfinishedActions,
                    $"Complete the unfinished action before closure: {unfinishedAction}",
                    new Dictionary<string, string> { ["action"] = unfinishedAction });

            detail.Status = ComplaintStatus.Closed;
            await core.SignStepAsync(record, QualityStepKind.ComplaintClosed, SignatureMeaning.QualityClosed,
                new { detail.Outcome, detail.ConclusionSummary }, password, null, userId, ip, ct);
        }, ct);
        await QualityNotifier.SendPostCommitAsync(notifier, n => n.ClosedAsync(dto.Id, ct), logger, dto.Id);
        return dto;
    }

    public Task<ComplaintDto> CancelAsync(int id, string reason, string password, uint expectedVersion, int userId,
        string? ip = null, CancellationToken ct = default) =>
        ChangeAsync(id, expectedVersion, userId, PermissionConstants.QualityApprove, async (record, detail) =>
        {
            if (detail.Status is not (ComplaintStatus.Received or ComplaintStatus.Investigation
                or ComplaintStatus.LabReported or ComplaintStatus.Concluded))
                throw new BusinessRuleException(MessageCodes.ComplaintFinalCancellation,
                    "A closed complaint cannot be cancelled.");

            detail.Status = ComplaintStatus.Cancelled;
            await core.CancelAsync(record, reason, password, userId, ip, ct);
        }, ct);

    private Task<ComplaintDto> ChangeAsync(int id, uint expectedVersion, int userId, string? permission,
        Func<QualityRecord, ComplaintDetail, Task> change, CancellationToken ct) => UnitOfWork.RunAsync(db, async () =>
    {
        if (permission is not null) await RequirePermissionAsync(userId, permission, ct);
        var current = await core.LockForWriteAsync(id, expectedVersion, userId, ct);
        if (current.Type != QualityRecordType.Complaint ||
            (current.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section)))
            throw QualityRecordCore.Missing();
        var detail = await db.ComplaintDetails.SingleOrDefaultAsync(d => d.RecordId == id, ct)
            ?? throw QualityRecordCore.Missing();
        if (current.IsCancelled) throw WrongState();

        db.CurrentUserId = userId;
        await change(current, detail);
        return ComplaintDto.From(current, detail);
    }, ct);

    private async Task EnsureOwnerAsync(QualityRecord record, int ownerId, CancellationToken ct)
    {
        if (!await HoldsAsync(ownerId, PermissionConstants.QualityRaise, ct) ||
            await db.Users.AsNoTracking().AnyAsync(u => u.Id == ownerId && u.Role != null && u.Role.Type == RoleType.Qa, ct) ||
            (record.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(ownerId, ct)).Contains(section)))
            throw new BusinessRuleException(MessageCodes.ComplaintInvalidOwner,
                "Select an active laboratory owner who can raise quality records and belongs to the complaint's section.");
    }

    private static void RequireStatus(ComplaintDetail detail, ComplaintStatus expected)
    {
        if (detail.Status != expected) throw WrongState();
    }

    private static BusinessRuleException WrongState() =>
        new(MessageCodes.ComplaintWrongState, "This step is unavailable in the complaint's current state.");

    private Task<bool> HoldsAsync(int userId, string permission, CancellationToken ct) =>
        permissions.UserHasPermissionAsync(userId, permission, ct);

    private async Task RequirePermissionAsync(int userId, string permission, CancellationToken ct)
    {
        if (!await HoldsAsync(userId, permission, ct)) throw QualityRecordCore.Denied();
    }
}
