using System.Globalization;
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
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Quality;

public sealed class QualityQueryService(
    IMicroLimsDbContext db,
    QualityRecordCore core,
    ILabClock clock)
{
    public async Task<PagedResult<QualityRecordListItemDto>> ListAsync(QualityRecordFilter filter, int userId,
        CancellationToken ct = default)
    {
        var query = await core.ReadableRecordsAsync(userId, ct);

        if (filter.Type is QualityRecordType type)
            query = query.Where(r => r.Type == type);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = ApplyStatusFilter(query, filter.Status);
        if (filter.ClassificationId is int classId)
            query = query.Where(r => r.ClassificationId == classId);
        if (filter.SectionId is int secId)
            query = query.Where(r => r.SectionId == secId);
        if (filter.OwnerUserId is int ownerId)
            query = query.Where(r => r.OwnerUserId == ownerId);

        var today = clock.LabToday;
        if (filter.Overdue is bool overdue)
        {
            query = overdue
                ? query.Where(QualityConfigService.IsOpenExpression).Where(r => r.DueDate != null && r.DueDate < today)
                : query.Where(r => !(!r.IsCancelled
                        && (r.DeviationDetail == null || (r.DeviationDetail.Status != QualityDeviationStatus.Rejected
                            && r.DeviationDetail.Status != QualityDeviationStatus.Closed && r.DeviationDetail.Status != QualityDeviationStatus.Cancelled))
                        && (r.CapaDetail == null || (r.CapaDetail.Status != CapaStatus.Closed && r.CapaDetail.Status != CapaStatus.Cancelled))
                        && (r.ChangeControlDetail == null || (r.ChangeControlDetail.Status != ChangeControlStatus.Closed
                            && r.ChangeControlDetail.Status != ChangeControlStatus.Rejected && r.ChangeControlDetail.Status != ChangeControlStatus.Cancelled))
                        && (r.ComplaintDetail == null || (r.ComplaintDetail.Status != ComplaintStatus.Closed
                            && r.ComplaintDetail.Status != ComplaintStatus.Cancelled)))
                    || r.DueDate == null || r.DueDate >= today);
        }

        if (filter.From is DateTime from)
        {
            var fromUtc = ToUtc(from);
            query = query.Where(r => r.RaisedAtUtc >= fromUtc);
        }
        if (filter.To is DateTime to)
        {
            var toUtc = ToUtc(to);
            query = query.Where(r => r.RaisedAtUtc <= toUtc);
        }

        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize, defaultPageSize: 25);
        var total = await query.CountAsync(ct);

        var paged = query
            .OrderByDescending(r => r.RaisedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var items = await ProjectListItems(paged, today).ToListAsync(ct);

        return new PagedResult<QualityRecordListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<QualityRecordDetailDto> GetDetailAsync(int id, int userId, CancellationToken ct = default)
    {
        var today = clock.LabToday;
        var readable = (await core.ReadableRecordsAsync(userId, ct)).Where(r => r.Id == id);

        var row = await readable.Select(r => new
        {
            r.Id,
            r.Number,
            r.Type,
            r.Title,
            r.Description,
            r.SectionId,
            SectionName = r.SectionId == null ? null : db.DocumentSections.Where(s => s.Id == r.SectionId).Select(s => s.Name).FirstOrDefault(),
            r.ClassificationId,
            ClassificationName = r.ClassificationId == null ? null : db.QualityClassifications.Where(c => c.Id == r.ClassificationId).Select(c => c.Name).FirstOrDefault(),
            r.RaisedByUserId,
            RaisedByName = r.RaisedByUserId == null ? null : db.Users.Where(u => u.Id == r.RaisedByUserId).Select(u => u.FullName).FirstOrDefault(),
            r.RaisedAtUtc,
            r.OwnerUserId,
            OwnerName = r.OwnerUserId == null ? null : db.Users.Where(u => u.Id == r.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
            r.DueDate,
            r.IsCancelled,
            r.CancelReason,
            r.Version,
            LinksCount = r.Links.Count,
            Deviation = r.DeviationDetail == null ? null : new
            {
                r.DeviationDetail.Status,
                r.DeviationDetail.OccurredAtUtc,
                r.DeviationDetail.Location,
                r.DeviationDetail.ImmediateAction,
                r.DeviationDetail.ImpactAssessment,
                r.DeviationDetail.RootCauseCategoryId,
                RootCauseCategoryName = r.DeviationDetail.RootCauseCategoryId == null ? null
                    : db.OosCauseCategories.Where(c => c.Id == r.DeviationDetail.RootCauseCategoryId).Select(c => c.Name).FirstOrDefault(),
                r.DeviationDetail.RootCauseDescription,
                r.DeviationDetail.ProposedActions,
                r.DeviationDetail.CapaRequired,
                r.DeviationDetail.RejectionReason
            },
            Capa = r.CapaDetail == null ? null : new
            {
                r.CapaDetail.Status,
                r.CapaDetail.SourceRecordId,
                SourceRecordNumber = r.CapaDetail.SourceRecordId == null ? null
                    : db.QualityRecords.Where(sr => sr.Id == r.CapaDetail.SourceRecordId).Select(sr => sr.Number).FirstOrDefault(),
                r.CapaDetail.EffectivenessCriteria,
                r.CapaDetail.EffectivenessMethod,
                r.CapaDetail.EffectivenessPlannedDate,
                r.CapaDetail.EffectivenessResult,
                r.CapaDetail.EffectivenessFindings,
                r.CapaDetail.SuccessorCapaId,
                SuccessorCapaNumber = r.CapaDetail.SuccessorCapaId == null ? null
                    : db.QualityRecords.Where(sr => sr.Id == r.CapaDetail.SuccessorCapaId).Select(sr => sr.Number).FirstOrDefault()
            },
            ChangeControl = r.ChangeControlDetail == null ? null : new
            {
                r.ChangeControlDetail.Status,
                r.ChangeControlDetail.Reason,
                r.ChangeControlDetail.ImpactSummary,
                r.ChangeControlDetail.RejectionReason,
                r.ChangeControlDetail.PostChangeReview,
                r.ChangeControlDetail.UnchangedJustification
            },
            Complaint = r.ComplaintDetail == null ? null : new
            {
                r.ComplaintDetail.Status,
                r.ComplaintDetail.Source,
                r.ComplaintDetail.ReceivedOn,
                r.ComplaintDetail.ExternalReference,
                r.ComplaintDetail.ItemId,
                ItemName = r.ComplaintDetail.ItemId == null ? null
                    : db.Items.Where(i => i.Id == r.ComplaintDetail.ItemId).Select(i => i.Name).FirstOrDefault(),
                r.ComplaintDetail.LotText,
                r.ComplaintDetail.ComplaintText,
                r.ComplaintDetail.WhatToTest,
                r.ComplaintDetail.Findings,
                r.ComplaintDetail.Outcome,
                r.ComplaintDetail.ConclusionSummary
            }
        }).SingleOrDefaultAsync(ct) ?? throw QualityRecordCore.Missing();

        var timelineRows = await (
            from step in db.QualityRecordSteps.AsNoTracking()
            join sig in db.ElectronicSignatures.AsNoTracking() on step.SignatureId equals sig.Id
            where step.RecordId == id
            orderby step.SignedAtUtc, step.Id
            select new
            {
                step.Id,
                step.Kind,
                sig.UserFullNameSnapshot,
                sig.UsernameSnapshot,
                Role = sig.RoleNameSnapshot ?? sig.RoleSnapshot,
                step.SignedAtUtc,
                Meaning = sig.MeaningOfSignature.ToString(),
                step.Comment
            }).ToListAsync(ct);

        var timeline = timelineRows.Select(s => new QualitySignedTimelineEntryDto(
            s.Id,
            s.Kind,
            s.UserFullNameSnapshot,
            s.UsernameSnapshot,
            s.Role,
            s.SignedAtUtc,
            FormatUtcWithZone(s.SignedAtUtc),
            s.Meaning,
            ReportVocabulary.SignatureMeaning(s.Meaning),
            s.Comment)).ToList();

        var actions = await db.QualityActions.AsNoTracking()
            .Where(a => a.RecordId == id)
            .OrderBy(a => a.Id)
            .Select(a => new QualityActionDetailDto(
                a.Id,
                a.Kind,
                a.Description,
                a.ResponsibleUserId,
                db.Users.Where(u => u.Id == a.ResponsibleUserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                a.DueDate,
                a.DoneAtUtc,
                a.DoneByUserId,
                a.DoneByUserId == null ? null : db.Users.Where(u => u.Id == a.DoneByUserId).Select(u => u.FullName).FirstOrDefault(),
                a.Evidence,
                a.EvidenceLinkId,
                a.Version))
            .ToListAsync(ct);

        var attachments = await db.QualityAttachments.AsNoTracking()
            .Where(a => a.RecordId == id)
            .OrderBy(a => a.Id)
            .Select(a => new QualityAttachmentDto(
                a.Id, a.OriginalFileName, a.ContentType, a.FileSizeBytes,
                a.UploadedByUserId, a.UploadedAtUtc, a.VoidedAtUtc, a.VoidedByUserId, a.VoidReason))
            .ToListAsync(ct);

        var status = ResolveStatusString(row.IsCancelled, row.Deviation?.Status, row.Capa?.Status,
            row.ChangeControl?.Status, row.Complaint?.Status);
        var isOpen = IsOpenStatus(row.IsCancelled, row.Deviation?.Status, row.Capa?.Status,
            row.ChangeControl?.Status, row.Complaint?.Status);

        var pathSteps = BuildPathSteps(row.Type, status, row.RaisedAtUtc, row.RaisedByName, timeline);

        return new QualityRecordDetailDto(
            row.Id,
            row.Number,
            row.Type,
            row.Title,
            row.Description,
            status,
            row.SectionId,
            row.SectionName,
            row.ClassificationId,
            row.ClassificationName,
            row.RaisedByUserId,
            row.RaisedByName ?? "System",
            row.RaisedAtUtc,
            row.OwnerUserId,
            row.OwnerName,
            row.DueDate,
            isOpen && row.DueDate < today,
            row.IsCancelled,
            row.CancelReason,
            row.Version,
            row.Deviation is null ? null : new QualityDeviationDetailDto(
                row.Deviation.Status, row.Deviation.OccurredAtUtc, row.Deviation.Location, row.Deviation.ImmediateAction,
                row.Deviation.ImpactAssessment, row.Deviation.RootCauseCategoryId, row.Deviation.RootCauseCategoryName,
                row.Deviation.RootCauseDescription, row.Deviation.ProposedActions, row.Deviation.CapaRequired,
                row.Deviation.RejectionReason),
            row.Capa is null ? null : new QualityCapaDetailDto(
                row.Capa.Status, row.Capa.SourceRecordId, row.Capa.SourceRecordNumber,
                row.Capa.EffectivenessCriteria, row.Capa.EffectivenessMethod, row.Capa.EffectivenessPlannedDate,
                row.Capa.EffectivenessResult, row.Capa.EffectivenessFindings,
                row.Capa.SuccessorCapaId, row.Capa.SuccessorCapaNumber),
            row.ChangeControl is null ? null : new QualityChangeControlDetailDto(
                row.ChangeControl.Status, row.ChangeControl.Reason, row.ChangeControl.ImpactSummary,
                row.ChangeControl.RejectionReason, row.ChangeControl.PostChangeReview, row.ChangeControl.UnchangedJustification),
            row.Complaint is null ? null : new QualityComplaintDetailDto(
                row.Complaint.Status, row.Complaint.Source, row.Complaint.ReceivedOn, row.Complaint.ExternalReference,
                row.Complaint.ItemId, row.Complaint.ItemName, row.Complaint.LotText, row.Complaint.ComplaintText,
                row.Complaint.WhatToTest, row.Complaint.Findings, row.Complaint.Outcome, row.Complaint.ConclusionSummary),
            pathSteps,
            timeline,
            actions,
            attachments,
            row.LinksCount);
    }

    public async Task<QualityTrendReportDto> GetTrendsAsync(string groupBy, DateTime? from, DateTime? to,
        int repeatWindowDays, int userId, CancellationToken ct = default)
    {
        var normalized = (groupBy ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("type" or "classification" or "cause" or "section" or "item" or "source"))
            throw new BusinessRuleException(MessageCodes.QualityTrendGroupByInvalid,
                "Choose a valid trend grouping: type, classification, cause, section, item, or source.");

        var windowDays = Math.Clamp(repeatWindowDays, 1, 3650);
        DateTime? fromUtc = from is DateTime f ? ToUtc(f) : null;
        DateTime? toUtc = to is DateTime t ? ToUtc(t) : null;

        var query = await core.ReadableRecordsAsync(userId, ct);
        if (fromUtc.HasValue) query = query.Where(r => r.RaisedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(r => r.RaisedAtUtc <= toUtc.Value);

        var records = await query.Select(r => new TrendRecordRow(
            r.Id,
            r.Number,
            r.Type,
            r.SectionId,
            r.SectionId == null ? null : db.DocumentSections.Where(s => s.Id == r.SectionId).Select(s => s.Name).FirstOrDefault(),
            r.ClassificationId,
            r.ClassificationId == null ? null : db.QualityClassifications.Where(c => c.Id == r.ClassificationId).Select(c => c.Name).FirstOrDefault(),
            r.RaisedAtUtc,
            r.DueDate,
            r.IsCancelled,
            r.DeviationDetail != null ? r.DeviationDetail.RootCauseCategoryId : null,
            r.DeviationDetail != null && r.DeviationDetail.RootCauseCategoryId != null
                ? db.OosCauseCategories.Where(c => c.Id == r.DeviationDetail.RootCauseCategoryId).Select(c => c.Name).FirstOrDefault()
                : null,
            r.CapaDetail != null ? r.CapaDetail.EffectivenessResult : null,
            r.ComplaintDetail != null ? r.ComplaintDetail.ItemId : null,
            r.ComplaintDetail != null && r.ComplaintDetail.ItemId != null
                ? db.Items.Where(i => i.Id == r.ComplaintDetail.ItemId).Select(i => i.Name).FirstOrDefault()
                : null,
            r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed
                || r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed
                || r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed
                || r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed,
            !r.IsCancelled
                && (r.DeviationDetail == null || (r.DeviationDetail.Status != QualityDeviationStatus.Rejected
                    && r.DeviationDetail.Status != QualityDeviationStatus.Closed && r.DeviationDetail.Status != QualityDeviationStatus.Cancelled))
                && (r.CapaDetail == null || (r.CapaDetail.Status != CapaStatus.Closed && r.CapaDetail.Status != CapaStatus.Cancelled))
                && (r.ChangeControlDetail == null || (r.ChangeControlDetail.Status != ChangeControlStatus.Closed
                    && r.ChangeControlDetail.Status != ChangeControlStatus.Rejected && r.ChangeControlDetail.Status != ChangeControlStatus.Cancelled))
                && (r.ComplaintDetail == null || (r.ComplaintDetail.Status != ComplaintStatus.Closed
                    && r.ComplaintDetail.Status != ComplaintStatus.Cancelled)),
            r.Steps
                .Where(s => s.Kind == QualityStepKind.DeviationClosed
                    || s.Kind == QualityStepKind.CapaEffectivenessChecked
                    || s.Kind == QualityStepKind.ChangeClosed
                    || s.Kind == QualityStepKind.ComplaintClosed)
                .OrderByDescending(s => s.Id)
                .Select(s => (DateTime?)s.SignedAtUtc)
                .FirstOrDefault()
        )).ToListAsync(ct);

        var recordIds = records.Select(r => r.Id).ToArray();
        var links = recordIds.Length == 0
            ? new List<TrendLinkRow>()
            : await db.QualityRecordLinks.AsNoTracking()
                .Where(l => recordIds.Contains(l.RecordId) && l.TargetId != null &&
                    (l.TargetType == QualityLinkTargetType.Item
                     || l.TargetType == QualityLinkTargetType.Equipment
                     || l.TargetType == QualityLinkTargetType.OosInvestigation
                     || l.TargetType == QualityLinkTargetType.MonitoringDeviation))
                .Select(l => new TrendLinkRow(
                    l.RecordId,
                    l.TargetType,
                    l.TargetId!.Value,
                    l.TargetType == QualityLinkTargetType.Item
                        ? db.Items.Where(i => i.Id == l.TargetId).Select(i => i.Name).FirstOrDefault()
                        : null,
                    l.TargetType == QualityLinkTargetType.Equipment
                        ? db.Equipment.Where(e => e.Id == l.TargetId).Select(e => e.Name).FirstOrDefault()
                        : null))
                .ToListAsync(ct);

        var linksByRecord = links.GroupBy(l => l.RecordId).ToDictionary(g => g.Key, g => g.ToList());

        var grouped = records
            .SelectMany(r => ResolveGroupKeys(normalized, r, linksByRecord.GetValueOrDefault(r.Id))
                .Select(k => new { k.Key, k.Label, Record = r }))
            .GroupBy(x => (x.Key, x.Label))
            .Select(g =>
            {
                var distinct = g.Select(x => x.Record).DistinctBy(r => r.Id).ToList();
                var closed = distinct.Where(r => r.IsClosed && r.ClosedAtUtc.HasValue).ToList();
                double? avgClose = closed.Count == 0
                    ? null
                    : Math.Round(closed.Average(r => (r.ClosedAtUtc!.Value - r.RaisedAtUtc).TotalDays), 2);
                var withDue = closed.Where(r => r.DueDate.HasValue).ToList();
                double? onTime = withDue.Count == 0
                    ? null
                    : Math.Round((double)withDue.Count(r => DateOnly.FromDateTime(clock.ToLabLocal(r.ClosedAtUtc!.Value)) <= r.DueDate!.Value) / withDue.Count, 4);
                var capas = distinct.Where(r => r.Type == QualityRecordType.Capa && r.CapaEffectiveness.HasValue).ToList();
                double? effRate = capas.Count == 0
                    ? null
                    : Math.Round((double)capas.Count(r => r.CapaEffectiveness == CapaEffectiveness.Effective) / capas.Count, 4);

                return new QualityTrendGroupDto(
                    g.Key.Key,
                    g.Key.Label,
                    distinct.Count,
                    distinct.Count(r => r.IsOpen),
                    closed.Count,
                    avgClose,
                    onTime,
                    effRate);
            })
            .OrderByDescending(g => g.TotalCount)
            .ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repeatCauses = new List<QualityRepeatCauseDto>();
        var window = TimeSpan.FromDays(windowDays);
        var causeTargetPairs = records
            .Where(r => r.CauseCategoryId.HasValue)
            .SelectMany(r => linksByRecord.GetValueOrDefault(r.Id, [])
                .Where(l => l.TargetType is QualityLinkTargetType.Item or QualityLinkTargetType.Equipment)
                .Select(l => new
                {
                    CauseId = r.CauseCategoryId!.Value,
                    CauseName = r.CauseCategoryName ?? $"Cause {r.CauseCategoryId}",
                    l.TargetType,
                    l.TargetId,
                    TargetLabel = l.TargetType == QualityLinkTargetType.Item
                        ? (l.ItemName ?? $"Item {l.TargetId}")
                        : (l.EquipmentName ?? $"Equipment {l.TargetId}"),
                    Record = r
                }))
            .GroupBy(x => (x.CauseId, x.CauseName, x.TargetType, x.TargetId, x.TargetLabel));

        foreach (var pair in causeTargetPairs)
        {
            var ordered = pair.Select(x => x.Record).DistinctBy(r => r.Id).OrderBy(r => r.RaisedAtUtc).ToList();
            if (ordered.Count < 2) continue;

            var hasRepeat = false;
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].RaisedAtUtc - ordered[i - 1].RaisedAtUtc <= window)
                {
                    hasRepeat = true;
                    break;
                }
            }
            if (hasRepeat)
            {
                repeatCauses.Add(new QualityRepeatCauseDto(
                    pair.Key.CauseId,
                    pair.Key.CauseName,
                    pair.Key.TargetType,
                    pair.Key.TargetId,
                    pair.Key.TargetLabel,
                    ordered.Count,
                    ordered[0].RaisedAtUtc,
                    ordered[^1].RaisedAtUtc,
                    ordered.Select(r => r.Number).ToList()));
            }
        }

        return new QualityTrendReportDto(
            normalized,
            fromUtc,
            toUtc,
            windowDays,
            grouped,
            repeatCauses.OrderByDescending(r => r.OccurrenceCount).ToList());
    }

    public async Task<QualityDashboardDto> GetDashboardAsync(int userId, CancellationToken ct = default)
    {
        var access = await core.GetReadAccessAsync(userId, ct);
        if (!access.Holds(PermissionConstants.QualityView))
            return new QualityDashboardDto([], [], [], [], [], []);

        var today = clock.LabToday;
        var openQuery = core.ReadableRecords(userId, access).Where(QualityConfigService.IsOpenExpression);

        var openRows = await openQuery
            .OrderBy(r => r.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(r => r.RaisedAtUtc)
            .Select(r => new
            {
                Dto = new QualityRecordListItemDto(
                    r.Id,
                    r.Number,
                    r.Type,
                    r.Title,
                    r.IsCancelled ? "Cancelled"
                        : r.DeviationDetail != null ? r.DeviationDetail.Status.ToString()
                        : r.CapaDetail != null ? r.CapaDetail.Status.ToString()
                        : r.ChangeControlDetail != null ? r.ChangeControlDetail.Status.ToString()
                        : r.ComplaintDetail != null ? r.ComplaintDetail.Status.ToString()
                        : "Unknown",
                    r.SectionId,
                    r.SectionId == null ? null : db.DocumentSections.Where(s => s.Id == r.SectionId).Select(s => s.Name).FirstOrDefault(),
                    r.ClassificationId,
                    r.ClassificationId == null ? null : db.QualityClassifications.Where(c => c.Id == r.ClassificationId).Select(c => c.Name).FirstOrDefault(),
                    r.RaisedByUserId,
                    r.RaisedByUserId == null ? "System" : db.Users.Where(u => u.Id == r.RaisedByUserId).Select(u => u.FullName).FirstOrDefault(),
                    r.RaisedAtUtc,
                    r.OwnerUserId,
                    r.OwnerUserId == null ? null : db.Users.Where(u => u.Id == r.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
                    r.DueDate,
                    r.DueDate != null && r.DueDate < today,
                    r.IsCancelled,
                    r.Version),
                DevStatus = r.DeviationDetail != null ? (QualityDeviationStatus?)r.DeviationDetail.Status : null,
                HasDevInvestigation = r.DeviationDetail != null && r.DeviationDetail.RootCauseCategoryId != null,
                CapaStatus = r.CapaDetail != null ? (CapaStatus?)r.CapaDetail.Status : null,
                CapaEffectivenessDue = r.CapaDetail != null ? r.CapaDetail.EffectivenessPlannedDate : null,
                CcStatus = r.ChangeControlDetail != null ? (ChangeControlStatus?)r.ChangeControlDetail.Status : null,
                HasCcAssessment = r.ChangeControlDetail != null && r.ChangeControlDetail.ImpactSummary != null,
                CmpStatus = r.ComplaintDetail != null ? (ComplaintStatus?)r.ComplaintDetail.Status : null,
                HasCmpFindings = r.ComplaintDetail != null && r.ComplaintDetail.Findings != null,
                HasUnfinishedActions = r.Actions.Any(a => a.DoneAtUtc == null)
            })
            .ToListAsync(ct);

        var isQa = access.Holds(PermissionConstants.QualityApprove);
        var isLabConclude = access.Holds(PermissionConstants.QualityLabConclude);
        var isInvestigate = access.Holds(PermissionConstants.QualityInvestigate);

        var waiting = new List<QualityRecordListItemDto>();
        foreach (var item in openRows)
        {
            var r = item.Dto;
            var notRaiserOrOwner = r.RaisedByUserId != userId && r.OwnerUserId != userId;
            if (isQa)
            {
                var qaReady =
                    item.DevStatus == QualityDeviationStatus.Raised
                    || (item.DevStatus is QualityDeviationStatus.LabConcluded or QualityDeviationStatus.Approved && notRaiserOrOwner && !item.HasUnfinishedActions)
                    || (item.CapaStatus == CapaStatus.Plan && notRaiserOrOwner)
                    || (item.CapaStatus == CapaStatus.PlanApproved && notRaiserOrOwner && !item.HasUnfinishedActions)
                    || (item.CapaStatus == CapaStatus.CompletionVerified && notRaiserOrOwner && item.CapaEffectivenessDue <= today)
                    || (item.CcStatus == ChangeControlStatus.Assessed && notRaiserOrOwner)
                    || (item.CcStatus == ChangeControlStatus.ApprovedForImplementation && notRaiserOrOwner && !item.HasUnfinishedActions)
                    || item.CmpStatus == ComplaintStatus.Received
                    || (item.CmpStatus is ComplaintStatus.LabReported or ComplaintStatus.Concluded && notRaiserOrOwner && !item.HasUnfinishedActions);
                if (qaReady) waiting.Add(r);
            }
            else if (isLabConclude || isInvestigate)
            {
                var labReady =
                    (isLabConclude && item.DevStatus == QualityDeviationStatus.Investigation && item.HasDevInvestigation && r.OwnerUserId != userId)
                    || (isLabConclude && item.CmpStatus == ComplaintStatus.Investigation && item.HasCmpFindings && r.OwnerUserId != userId)
                    || (isInvestigate && item.CcStatus == ChangeControlStatus.Proposed && item.HasCcAssessment);
                if (labReady) waiting.Add(r);
            }
        }

        var openActions = await openQuery
            .SelectMany(r => r.Actions.Where(a => a.DoneAtUtc == null),
                (r, a) => new
                {
                    a.ResponsibleUserId,
                    a.DueDate,
                    a.Id,
                    Dto = new QualityDashboardActionDto(
                        r.Id, r.Number, r.Type, r.Title, a.Id, a.Kind, a.Description, a.DueDate, a.DueDate < today)
                })
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        return new QualityDashboardDto(
            waiting,
            openRows.Where(x => x.Dto.OwnerUserId == userId || x.Dto.RaisedByUserId == userId).Select(x => x.Dto).ToList(),
            openActions.Where(x => x.ResponsibleUserId == userId).Select(x => x.Dto).ToList(),
            openRows.Where(x => x.Dto.IsOverdue).Select(x => x.Dto).ToList(),
            openActions.Where(x => x.Dto.IsOverdue).Select(x => x.Dto).ToList(),
            openRows.Where(x => x.CapaStatus == CapaStatus.CompletionVerified && x.CapaEffectivenessDue <= today)
                .Select(x => x.Dto).ToList());
    }

    private IQueryable<QualityRecordListItemDto> ProjectListItems(IQueryable<QualityRecord> query, DateOnly today) =>
        query.Select(r => new QualityRecordListItemDto(
            r.Id,
            r.Number,
            r.Type,
            r.Title,
            r.IsCancelled ? "Cancelled"
                : r.DeviationDetail != null ? r.DeviationDetail.Status.ToString()
                : r.CapaDetail != null ? r.CapaDetail.Status.ToString()
                : r.ChangeControlDetail != null ? r.ChangeControlDetail.Status.ToString()
                : r.ComplaintDetail != null ? r.ComplaintDetail.Status.ToString()
                : "Unknown",
            r.SectionId,
            r.SectionId == null ? null : db.DocumentSections.Where(s => s.Id == r.SectionId).Select(s => s.Name).FirstOrDefault(),
            r.ClassificationId,
            r.ClassificationId == null ? null : db.QualityClassifications.Where(c => c.Id == r.ClassificationId).Select(c => c.Name).FirstOrDefault(),
            r.RaisedByUserId,
            r.RaisedByUserId == null ? "System" : db.Users.Where(u => u.Id == r.RaisedByUserId).Select(u => u.FullName).FirstOrDefault(),
            r.RaisedAtUtc,
            r.OwnerUserId,
            r.OwnerUserId == null ? null : db.Users.Where(u => u.Id == r.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
            r.DueDate,
            !r.IsCancelled
                && (r.DeviationDetail == null || (r.DeviationDetail.Status != QualityDeviationStatus.Rejected
                    && r.DeviationDetail.Status != QualityDeviationStatus.Closed && r.DeviationDetail.Status != QualityDeviationStatus.Cancelled))
                && (r.CapaDetail == null || (r.CapaDetail.Status != CapaStatus.Closed && r.CapaDetail.Status != CapaStatus.Cancelled))
                && (r.ChangeControlDetail == null || (r.ChangeControlDetail.Status != ChangeControlStatus.Closed
                    && r.ChangeControlDetail.Status != ChangeControlStatus.Rejected && r.ChangeControlDetail.Status != ChangeControlStatus.Cancelled))
                && (r.ComplaintDetail == null || (r.ComplaintDetail.Status != ComplaintStatus.Closed
                    && r.ComplaintDetail.Status != ComplaintStatus.Cancelled))
                && r.DueDate != null && r.DueDate < today,
            r.IsCancelled,
            r.Version));

    private static IQueryable<QualityRecord> ApplyStatusFilter(IQueryable<QualityRecord> query, string status)
    {
        var trimmed = status.Trim();
        if (string.Equals(trimmed, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return query.Where(r => r.IsCancelled);

        var hasDev = Enum.TryParse<QualityDeviationStatus>(trimmed, true, out var devStatus);
        var hasCapa = Enum.TryParse<CapaStatus>(trimmed, true, out var capaStatus);
        var hasCc = Enum.TryParse<ChangeControlStatus>(trimmed, true, out var ccStatus);
        var hasCmp = Enum.TryParse<ComplaintStatus>(trimmed, true, out var cmpStatus);

        if (!hasDev && !hasCapa && !hasCc && !hasCmp)
            return query.Where(_ => false);

        return query.Where(r => !r.IsCancelled && (
            (hasDev && r.DeviationDetail != null && r.DeviationDetail.Status == devStatus) ||
            (hasCapa && r.CapaDetail != null && r.CapaDetail.Status == capaStatus) ||
            (hasCc && r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ccStatus) ||
            (hasCmp && r.ComplaintDetail != null && r.ComplaintDetail.Status == cmpStatus)));
    }

    private static IEnumerable<(string Key, string Label)> ResolveGroupKeys(
        string groupBy,
        TrendRecordRow r,
        List<TrendLinkRow>? links)
    {
        switch (groupBy)
        {
            case "type":
                yield return (r.Type.ToString(), ReportVocabulary.Humanize(r.Type.ToString()));
                yield break;
            case "classification":
                yield return r.ClassificationId is int cid
                    ? (cid.ToString(CultureInfo.InvariantCulture), r.ClassificationName ?? $"Classification {cid}")
                    : ("unclassified", "Unclassified");
                yield break;
            case "cause":
                yield return r.CauseCategoryId is int causeId
                    ? (causeId.ToString(CultureInfo.InvariantCulture), r.CauseCategoryName ?? $"Cause {causeId}")
                    : ("unassigned", "Unassigned");
                yield break;
            case "section":
                yield return r.SectionId is int secId
                    ? (secId.ToString(CultureInfo.InvariantCulture), r.SectionName ?? $"Section {secId}")
                    : ("site-wide", "Site-wide");
                yield break;
            case "item":
                var yielded = false;
                if (r.ComplaintItemId is int cmpItem)
                {
                    yielded = true;
                    yield return (cmpItem.ToString(CultureInfo.InvariantCulture), r.ComplaintItemName ?? $"Item {cmpItem}");
                }
                if (links is not null)
                {
                    foreach (var l in links)
                    {
                        if (l.TargetType == QualityLinkTargetType.Item)
                        {
                            var itemId = l.TargetId;
                            if (itemId == r.ComplaintItemId) continue;
                            yielded = true;
                            yield return (itemId.ToString(CultureInfo.InvariantCulture), l.ItemName ?? $"Item {itemId}");
                        }
                    }
                }
                if (!yielded) yield return ("none", "None");
                yield break;
            default:
                if (r.Type == QualityRecordType.Complaint)
                {
                    yield return ("complaint", "Complaint");
                    yield break;
                }
                if (links is not null)
                {
                    var hasOos = false;
                    var hasExcursion = false;
                    foreach (var l in links)
                    {
                        if (l.TargetType == QualityLinkTargetType.OosInvestigation) hasOos = true;
                        else if (l.TargetType == QualityLinkTargetType.MonitoringDeviation) hasExcursion = true;
                    }
                    if (hasOos) { yield return ("oos", "OOS"); yield break; }
                    if (hasExcursion) { yield return ("excursion", "Excursion"); yield break; }
                }
                yield return ("other", "Other");
                yield break;
        }
    }

    private static IReadOnlyList<QualityPathStepDto> BuildPathSteps(
        QualityRecordType type,
        string currentStatus,
        DateTime raisedAtUtc,
        string? raisedByName,
        IReadOnlyList<QualitySignedTimelineEntryDto> timeline)
    {
        var specs = type switch
        {
            QualityRecordType.Deviation => new (string Key, string Label, QualityStepKind? StepKind)[]
            {
                ("Raised", "Raised", null),
                ("Investigation", "Assessed / Investigation", QualityStepKind.DeviationAssessed),
                ("LabConcluded", "Laboratory conclusion", QualityStepKind.DeviationLabConclusion),
                ("Approved", "QA approved", QualityStepKind.DeviationApproved),
                ("Closed", "Closed", QualityStepKind.DeviationClosed)
            },
            QualityRecordType.Capa => new (string Key, string Label, QualityStepKind? StepKind)[]
            {
                ("Plan", "Plan", null),
                ("PlanApproved", "Plan approved", QualityStepKind.CapaPlanApproved),
                ("CompletionVerified", "Completion verified", QualityStepKind.CapaCompletionVerified),
                ("Closed", "Effectiveness checked / Closed", QualityStepKind.CapaEffectivenessChecked)
            },
            QualityRecordType.ChangeControl => new (string Key, string Label, QualityStepKind? StepKind)[]
            {
                ("Proposed", "Proposed", null),
                ("Assessed", "Impact assessed", QualityStepKind.ChangeImpactAssessed),
                ("ApprovedForImplementation", "Approved for implementation", QualityStepKind.ChangeApproved),
                ("Closed", "Closed", QualityStepKind.ChangeClosed)
            },
            _ => new (string Key, string Label, QualityStepKind? StepKind)[]
            {
                ("Received", "Received", null),
                ("Investigation", "Investigation", null),
                ("LabReported", "Laboratory report", QualityStepKind.ComplaintLabReport),
                ("Concluded", "Concluded", QualityStepKind.ComplaintConcluded),
                ("Closed", "Closed", QualityStepKind.ComplaintClosed)
            }
        };

        var currentIndex = Array.FindIndex(specs, s => string.Equals(s.Key, currentStatus, StringComparison.OrdinalIgnoreCase));
        var steps = new List<QualityPathStepDto>(specs.Length);
        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            var sig = spec.StepKind is QualityStepKind kind
                ? timeline.LastOrDefault(t => t.Kind == kind)
                : null;
            var completed = i == 0 || sig is not null || (currentIndex >= 0 && i <= currentIndex);
            var isCurrent = currentIndex >= 0 && i == currentIndex;
            var completedAt = i == 0 ? raisedAtUtc : sig?.SignedAtUtc;
            var completedBy = i == 0 ? (raisedByName ?? "System") : sig?.SignerFullName;
            steps.Add(new QualityPathStepDto(spec.Key, spec.Label, completed, isCurrent, completedAt, completedBy));
        }
        return steps;
    }

    private static string ResolveStatusString(bool isCancelled, QualityDeviationStatus? dev, CapaStatus? capa,
        ChangeControlStatus? cc, ComplaintStatus? cmp)
    {
        if (isCancelled) return "Cancelled";
        if (dev.HasValue) return dev.Value.ToString();
        if (capa.HasValue) return capa.Value.ToString();
        if (cc.HasValue) return cc.Value.ToString();
        if (cmp.HasValue) return cmp.Value.ToString();
        return "Unknown";
    }

    private static bool IsOpenStatus(bool isCancelled, QualityDeviationStatus? dev, CapaStatus? capa,
        ChangeControlStatus? cc, ComplaintStatus? cmp)
    {
        if (isCancelled) return false;
        if (dev is QualityDeviationStatus.Rejected or QualityDeviationStatus.Closed or QualityDeviationStatus.Cancelled) return false;
        if (capa is CapaStatus.Closed or CapaStatus.Cancelled) return false;
        if (cc is ChangeControlStatus.Closed or ChangeControlStatus.Rejected or ChangeControlStatus.Cancelled) return false;
        if (cmp is ComplaintStatus.Closed or ComplaintStatus.Cancelled) return false;
        return true;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime()
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    internal static string FormatUtcWithZone(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    private sealed record TrendLinkRow(
        int RecordId,
        QualityLinkTargetType TargetType,
        int TargetId,
        string? ItemName,
        string? EquipmentName);

    private sealed record TrendRecordRow(
        int Id,
        string Number,
        QualityRecordType Type,
        int? SectionId,
        string? SectionName,
        int? ClassificationId,
        string? ClassificationName,
        DateTime RaisedAtUtc,
        DateOnly? DueDate,
        bool IsCancelled,
        int? CauseCategoryId,
        string? CauseCategoryName,
        CapaEffectiveness? CapaEffectiveness,
        int? ComplaintItemId,
        string? ComplaintItemName,
        bool IsClosed,
        bool IsOpen,
        DateTime? ClosedAtUtc);
}
