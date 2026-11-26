using System.Globalization;
using System.Linq.Expressions;
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
    QualityLinkService links,
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
                : query.Where(CombineOr(QualityConfigService.IsNotOpenExpression, r => r.DueDate == null || r.DueDate >= today));
        }

        if (filter.From is DateTime from)
        {
            var fromUtc = ToUtc(from);
            query = query.Where(r => r.RaisedAtUtc >= fromUtc);
        }
        if (filter.To is DateTime to)
        {
            if (to.TimeOfDay == TimeSpan.Zero)
            {
                var toExclusive = ToUtc(to).AddDays(1);
                query = query.Where(r => r.RaisedAtUtc < toExclusive);
            }
            else
            {
                var toUtc = ToUtc(to);
                query = query.Where(r => r.RaisedAtUtc <= toUtc);
            }
        }

        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize, defaultPageSize: 25);
        var total = await query.CountAsync(ct);

        var paged = query
            .OrderByDescending(r => r.RaisedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var items = await MaterializeListItemsAsync(paged, today, ct);

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

        var status = ResolveStatusString(row.IsCancelled, row.Deviation?.Status, row.Capa?.Status,
            row.ChangeControl?.Status, row.Complaint?.Status);
        var isOpen = QualityConfigService.IsOpenStatus(row.IsCancelled, row.Deviation?.Status, row.Capa?.Status,
            row.ChangeControl?.Status, row.Complaint?.Status);
        var actionExecutionActive = isOpen && QualityConfigService.IsActionExecutionActive(
            row.Capa?.Status, row.ChangeControl?.Status);

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
                a.Version,
                actionExecutionActive && a.DoneAtUtc == null && a.DueDate < today))
            .ToListAsync(ct);

        var attachments = await db.QualityAttachments.AsNoTracking()
            .Where(a => a.RecordId == id)
            .OrderBy(a => a.Id)
            .Select(a => new QualityAttachmentDto(
                a.Id, a.OriginalFileName, a.ContentType, a.FileSizeBytes,
                a.UploadedByUserId, a.UploadedAtUtc, a.VoidedAtUtc, a.VoidedByUserId, a.VoidReason,
                db.Users.Where(u => u.Id == a.UploadedByUserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                a.VoidedByUserId == null ? null : db.Users.Where(u => u.Id == a.VoidedByUserId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);

        var affected = row.ChangeControl is null
            ? []
            : await links.ResolveAffectedAsync(id, userId, ct);

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
                row.ChangeControl.RejectionReason, row.ChangeControl.PostChangeReview, row.ChangeControl.UnchangedJustification,
                affected),
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

    private const int DashboardListCap = 50;

    public async Task<QualityTrendReportDto> GetTrendsAsync(string groupBy, DateTime? from, DateTime? to,
        int repeatWindowDays, int userId, CancellationToken ct = default)
    {
        var normalized = (groupBy ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("type" or "classification" or "cause" or "section" or "item" or "source"))
            throw new BusinessRuleException(MessageCodes.QualityTrendGroupByInvalid,
                "Choose a valid trend grouping: type, classification, cause, section, item, or source.");

        var windowDays = Math.Clamp(repeatWindowDays, 1, 3650);
        var labToday = clock.LabToday;
        var toIsDateOnly = to is DateTime tv && tv.TimeOfDay == TimeSpan.Zero;
        DateTime toExclusiveUtc;
        DateTime toDtoUtc;
        if (to is DateTime t)
        {
            if (toIsDateOnly)
            {
                toExclusiveUtc = ToUtc(t).AddDays(1);
                toDtoUtc = toExclusiveUtc.AddSeconds(-1);
            }
            else
            {
                toDtoUtc = ToUtc(t);
                toExclusiveUtc = toDtoUtc.AddTicks(1);
            }
        }
        else
        {
            toExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(labToday.AddDays(1).ToDateTime(TimeOnly.MinValue), clock.LabTimeZone);
            toDtoUtc = toExclusiveUtc.AddSeconds(-1);
        }

        DateTime fromUtc = from is DateTime f
            ? ToUtc(f)
            : (to is DateTime
                ? toDtoUtc.Date.AddDays(-365)
                : TimeZoneInfo.ConvertTimeToUtc(labToday.AddDays(-365).ToDateTime(TimeOnly.MinValue), clock.LabTimeZone));

        if (toDtoUtc < fromUtc || (toDtoUtc - fromUtc) > TimeSpan.FromDays(365 * 5 + 2))
            throw new BusinessRuleException(MessageCodes.QualityTrendWindowTooLarge,
                "The trend date range cannot exceed 5 years.");

        var access = await core.GetReadAccessAsync(userId, ct);
        var query = core.ReadableRecords(userId, access)
            .Where(r => r.RaisedAtUtc >= fromUtc && r.RaisedAtUtc < toExclusiveUtc);

        var groups = normalized is "type" or "classification" or "section" or "cause"
            ? await GroupTrendsInSqlAsync(query, normalized, ct)
            : await GroupTrendsWithLinksAsync(query, normalized, ct);

        var repeatCauses = await ComputeRepeatCausesAsync(query, access, windowDays, ct);

        return new QualityTrendReportDto(
            normalized,
            fromUtc,
            toDtoUtc,
            windowDays,
            groups,
            repeatCauses);
    }

    private async Task<List<QualityTrendGroupDto>> GroupTrendsInSqlAsync(
        IQueryable<QualityRecord> query,
        string groupBy,
        CancellationToken ct)
    {
        var rawCounts = groupBy switch
        {
            "type" => await query
                .GroupBy(r => r.Type.ToString())
                .Select(g => new SqlTrendCounts(
                    g.Key,
                    g.Count(),
                    g.AsQueryable().Count(QualityConfigService.IsOpenExpression),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed)
                        || (r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed)
                        || (r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed))),
                    g.Count(r => r.IsCancelled),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Rejected)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Rejected))),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult != null),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult == CapaEffectiveness.Effective)))
                .ToListAsync(ct),
            "classification" => await query
                .GroupBy(r => r.ClassificationId == null ? "unclassified" : r.ClassificationId.Value.ToString())
                .Select(g => new SqlTrendCounts(
                    g.Key,
                    g.Count(),
                    g.AsQueryable().Count(QualityConfigService.IsOpenExpression),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed)
                        || (r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed)
                        || (r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed))),
                    g.Count(r => r.IsCancelled),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Rejected)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Rejected))),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult != null),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult == CapaEffectiveness.Effective)))
                .ToListAsync(ct),
            "section" => await query
                .GroupBy(r => r.SectionId == null ? "site-wide" : r.SectionId.Value.ToString())
                .Select(g => new SqlTrendCounts(
                    g.Key,
                    g.Count(),
                    g.AsQueryable().Count(QualityConfigService.IsOpenExpression),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed)
                        || (r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed)
                        || (r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed))),
                    g.Count(r => r.IsCancelled),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Rejected)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Rejected))),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult != null),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult == CapaEffectiveness.Effective)))
                .ToListAsync(ct),
            _ => await query
                .GroupBy(r => r.DeviationDetail != null && r.DeviationDetail.RootCauseCategoryId != null
                    ? r.DeviationDetail.RootCauseCategoryId.Value.ToString()
                    : "unassigned")
                .Select(g => new SqlTrendCounts(
                    g.Key,
                    g.Count(),
                    g.AsQueryable().Count(QualityConfigService.IsOpenExpression),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed)
                        || (r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed)
                        || (r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed))),
                    g.Count(r => r.IsCancelled),
                    g.Count(r => !r.IsCancelled && (
                        (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Rejected)
                        || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Rejected))),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult != null),
                    g.Count(r => !r.IsCancelled && r.Type == QualityRecordType.Capa && r.CapaDetail != null
                        && r.CapaDetail.Status == CapaStatus.Closed && r.CapaDetail.EffectivenessResult == CapaEffectiveness.Effective)))
                .ToListAsync(ct)
        };

        var closedRows = await query
            .Where(r => !r.IsCancelled && (
                (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed)
                || (r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed)
                || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed)
                || (r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed)))
            .Select(r => new
            {
                Key = groupBy == "type" ? r.Type.ToString()
                    : groupBy == "classification" ? (r.ClassificationId == null ? "unclassified" : r.ClassificationId.Value.ToString())
                    : groupBy == "section" ? (r.SectionId == null ? "site-wide" : r.SectionId.Value.ToString())
                    : (r.DeviationDetail != null && r.DeviationDetail.RootCauseCategoryId != null
                        ? r.DeviationDetail.RootCauseCategoryId.Value.ToString()
                        : "unassigned"),
                r.RaisedAtUtc,
                r.DueDate,
                ClosedAtUtc = r.Steps
                    .Where(s => s.Kind == QualityStepKind.DeviationClosed
                        || s.Kind == QualityStepKind.CapaEffectivenessChecked
                        || s.Kind == QualityStepKind.ChangeClosed
                        || s.Kind == QualityStepKind.ComplaintClosed)
                    .OrderByDescending(s => s.Id)
                    .Select(s => (DateTime?)s.SignedAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var closedByKey = closedRows
            .Where(c => c.ClosedAtUtc.HasValue)
            .GroupBy(c => c.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var labels = await ResolveSqlGroupLabelsAsync(groupBy, rawCounts.Select(c => c.Key).ToList(), ct);

        return rawCounts.Select(c =>
        {
            double? avgClose = null;
            double? onTime = null;
            if (closedByKey.TryGetValue(c.Key, out var closedList) && closedList.Count > 0)
            {
                avgClose = Math.Round(closedList.Average(r => (r.ClosedAtUtc!.Value - r.RaisedAtUtc).TotalDays), 2);
                var withDue = closedList.Where(r => r.DueDate.HasValue).ToList();
                if (withDue.Count > 0)
                    onTime = Math.Round((double)withDue.Count(r => DateOnly.FromDateTime(clock.ToLabLocal(r.ClosedAtUtc!.Value)) <= r.DueDate!.Value) / withDue.Count, 4);
            }
            double? effRate = c.CapaEffTotal == 0 ? null : Math.Round((double)c.CapaEffPass / c.CapaEffTotal, 4);
            return new QualityTrendGroupDto(
                c.Key,
                labels.GetValueOrDefault(c.Key, c.Key),
                c.TotalCount,
                c.OpenCount,
                c.ClosedCount,
                avgClose,
                onTime,
                effRate,
                c.CancelledCount,
                c.RejectedCount);
        })
        .OrderByDescending(g => g.TotalCount)
        .ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    private async Task<Dictionary<string, string>> ResolveSqlGroupLabelsAsync(string groupBy, List<string> keys, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (groupBy == "type")
        {
            foreach (var k in keys) result[k] = ReportVocabulary.Humanize(k);
            return result;
        }
        var ids = keys.Select(k => int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? (int?)id : null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();

        if (groupBy == "classification")
        {
            result["unclassified"] = "Unclassified";
            if (ids.Length > 0)
            {
                var rows = await db.QualityClassifications.AsNoTracking().Where(c => ids.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name }).ToListAsync(ct);
                foreach (var r in rows) result[r.Id.ToString(CultureInfo.InvariantCulture)] = r.Name;
            }
        }
        else if (groupBy == "section")
        {
            result["site-wide"] = "Site-wide";
            if (ids.Length > 0)
            {
                var rows = await db.DocumentSections.AsNoTracking().Where(s => ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.Name }).ToListAsync(ct);
                foreach (var r in rows) result[r.Id.ToString(CultureInfo.InvariantCulture)] = r.Name;
            }
        }
        else if (groupBy == "cause")
        {
            result["unassigned"] = "Unassigned";
            if (ids.Length > 0)
            {
                var rows = await db.OosCauseCategories.AsNoTracking().Where(c => ids.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name }).ToListAsync(ct);
                foreach (var r in rows) result[r.Id.ToString(CultureInfo.InvariantCulture)] = r.Name;
            }
        }
        return result;
    }

    private async Task<List<QualityTrendGroupDto>> GroupTrendsWithLinksAsync(
        IQueryable<QualityRecord> query,
        string groupBy,
        CancellationToken ct)
    {
        var records = await query.Select(r => new TrendLinkRecordRow(
            r.Id,
            r.Type,
            r.RaisedAtUtc,
            r.DueDate,
            r.IsCancelled,
            (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Rejected)
                || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Rejected),
            !r.IsCancelled && (
                (r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Closed)
                || (r.CapaDetail != null && r.CapaDetail.Status == CapaStatus.Closed)
                || (r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Closed)
                || (r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Closed)),
            QualityConfigService.IsOpenStatus(
                r.IsCancelled,
                r.DeviationDetail != null ? r.DeviationDetail.Status : null,
                r.CapaDetail != null ? r.CapaDetail.Status : null,
                r.ChangeControlDetail != null ? r.ChangeControlDetail.Status : null,
                r.ComplaintDetail != null ? r.ComplaintDetail.Status : null),
            r.CapaDetail != null ? r.CapaDetail.EffectivenessResult : null,
            r.ComplaintDetail != null ? r.ComplaintDetail.ItemId : null,
            r.ComplaintDetail != null && r.ComplaintDetail.ItemId != null
                ? db.Items.Where(i => i.Id == r.ComplaintDetail.ItemId).Select(i => i.Name).FirstOrDefault()
                : null,
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
        var itemMap = groupBy == "item"
            ? await LoadRecordItemsAsync(recordIds, ct)
            : new Dictionary<int, List<(int ItemId, string ItemName)>>();

        var sourceLinks = groupBy == "source" && recordIds.Length > 0
            ? (await db.QualityRecordLinks.AsNoTracking()
                .Where(l => recordIds.Contains(l.RecordId) && l.TargetId != null
                    && (l.TargetType == QualityLinkTargetType.OosInvestigation || l.TargetType == QualityLinkTargetType.MonitoringDeviation))
                .Select(l => new { l.RecordId, l.TargetType })
                .ToListAsync(ct))
                .GroupBy(l => l.RecordId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TargetType).ToList())
            : new Dictionary<int, List<QualityLinkTargetType>>();

        IEnumerable<(string Key, string Label)> KeysFor(TrendLinkRecordRow r)
        {
            if (groupBy == "item")
            {
                var yielded = new HashSet<int>();
                if (itemMap.TryGetValue(r.Id, out var linkedItems))
                {
                    foreach (var (itemId, itemName) in linkedItems)
                    {
                        if (yielded.Add(itemId))
                            yield return (itemId.ToString(CultureInfo.InvariantCulture), itemName);
                    }
                }
                if (r.ComplaintItemId is int cmpItem && yielded.Add(cmpItem))
                    yield return (cmpItem.ToString(CultureInfo.InvariantCulture), r.ComplaintItemName ?? $"Item {cmpItem}");
                if (yielded.Count == 0)
                    yield return ("none", "None");
                yield break;
            }

            if (r.Type == QualityRecordType.Complaint)
            {
                yield return ("complaint", "Complaint");
                yield break;
            }
            if (sourceLinks.TryGetValue(r.Id, out var types))
            {
                if (types.Contains(QualityLinkTargetType.OosInvestigation)) { yield return ("oos", "OOS"); yield break; }
                if (types.Contains(QualityLinkTargetType.MonitoringDeviation)) { yield return ("excursion", "Excursion"); yield break; }
            }
            yield return ("other", "Other");
        }

        return records
            .SelectMany(r => KeysFor(r).Select(k => new { k.Key, k.Label, Record = r }))
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
                var capas = distinct.Where(r => !r.IsCancelled && !r.IsRejected && r.IsClosed && r.Type == QualityRecordType.Capa && r.CapaEffectiveness.HasValue).ToList();
                double? effRate = capas.Count == 0
                    ? null
                    : Math.Round((double)capas.Count(r => r.CapaEffectiveness == CapaEffectiveness.Effective) / capas.Count, 4);

                return new QualityTrendGroupDto(
                    g.Key.Key,
                    g.Key.Label,
                    distinct.Count,
                    distinct.Count(r => r.IsOpen),
                    distinct.Count(r => r.IsClosed),
                    avgClose,
                    onTime,
                    effRate,
                    distinct.Count(r => r.IsCancelled),
                    distinct.Count(r => !r.IsCancelled && r.IsRejected));
            })
            .OrderByDescending(g => g.TotalCount)
            .ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<int, List<(int ItemId, string ItemName)>>> LoadRecordItemsAsync(int[] recordIds, CancellationToken ct)
    {
        var result = new Dictionary<int, List<(int ItemId, string ItemName)>>();
        if (recordIds.Length == 0) return result;

        void Add(int recordId, int itemId, string itemName)
        {
            if (!result.TryGetValue(recordId, out var list))
            {
                list = [];
                result[recordId] = list;
            }
            if (!list.Any(x => x.ItemId == itemId))
                list.Add((itemId, itemName));
        }

        var directItems = await (
            from l in db.QualityRecordLinks.AsNoTracking()
            join i in db.Items.AsNoTracking() on l.TargetId equals i.Id
            where recordIds.Contains(l.RecordId) && l.TargetType == QualityLinkTargetType.Item && l.TargetId != null
            select new { l.RecordId, ItemId = i.Id, ItemName = i.Name }).ToListAsync(ct);
        foreach (var x in directItems) Add(x.RecordId, x.ItemId, x.ItemName);

        var sampleItems = await (
            from l in db.QualityRecordLinks.AsNoTracking()
            join s in db.Samples.AsNoTracking() on l.TargetId equals s.Id
            join i in db.Items.AsNoTracking() on s.ItemId equals i.Id
            where recordIds.Contains(l.RecordId) && l.TargetType == QualityLinkTargetType.Sample && l.TargetId != null && s.ItemId != null
            select new { l.RecordId, ItemId = i.Id, ItemName = i.Name }).ToListAsync(ct);
        foreach (var x in sampleItems) Add(x.RecordId, x.ItemId, x.ItemName);

        var orderItems = await (
            from l in db.QualityRecordLinks.AsNoTracking()
            join o in db.TestOrders.AsNoTracking() on l.TargetId equals o.Id
            join s in db.Samples.AsNoTracking() on o.SampleId equals s.Id
            join i in db.Items.AsNoTracking() on s.ItemId equals i.Id
            where recordIds.Contains(l.RecordId) && l.TargetType == QualityLinkTargetType.TestOrder && l.TargetId != null && s.ItemId != null
            select new { l.RecordId, ItemId = i.Id, ItemName = i.Name }).ToListAsync(ct);
        foreach (var x in orderItems) Add(x.RecordId, x.ItemId, x.ItemName);

        var oosItems = await (
            from l in db.QualityRecordLinks.AsNoTracking()
            join inv in db.OosInvestigations.AsNoTracking() on l.TargetId equals inv.Id
            join s in db.Samples.AsNoTracking() on inv.SampleId equals s.Id
            join i in db.Items.AsNoTracking() on s.ItemId equals i.Id
            where recordIds.Contains(l.RecordId) && l.TargetType == QualityLinkTargetType.OosInvestigation && l.TargetId != null && s.ItemId != null
            select new { l.RecordId, ItemId = i.Id, ItemName = i.Name }).ToListAsync(ct);
        foreach (var x in oosItems) Add(x.RecordId, x.ItemId, x.ItemName);

        return result;
    }

    private async Task<List<QualityRepeatCauseDto>> ComputeRepeatCausesAsync(
        IQueryable<QualityRecord> query,
        QualityRecordCore.ReadAccess access,
        int windowDays,
        CancellationToken ct)
    {
        var candidates = await query
            .Where(r => !r.IsCancelled
                && (r.DeviationDetail == null || (r.DeviationDetail.Status != QualityDeviationStatus.Rejected && r.DeviationDetail.Status != QualityDeviationStatus.Cancelled))
                && (r.ChangeControlDetail == null || (r.ChangeControlDetail.Status != ChangeControlStatus.Rejected && r.ChangeControlDetail.Status != ChangeControlStatus.Cancelled))
                && r.DeviationDetail != null && r.DeviationDetail.RootCauseCategoryId != null)
            .Select(r => new RepeatCauseRecordRow(
                r.Id,
                r.Number,
                r.RaisedAtUtc,
                r.DeviationDetail!.RootCauseCategoryId!.Value,
                db.OosCauseCategories.Where(c => c.Id == r.DeviationDetail.RootCauseCategoryId).Select(c => c.Name).FirstOrDefault(),
                r.ComplaintDetail != null ? r.ComplaintDetail.ItemId : null,
                r.ComplaintDetail != null && r.ComplaintDetail.ItemId != null
                    ? db.Items.Where(i => i.Id == r.ComplaintDetail.ItemId).Select(i => i.Name).FirstOrDefault()
                    : null))
            .ToListAsync(ct);

        if (candidates.Count < 2) return [];

        var recordIds = candidates.Select(r => r.Id).ToArray();
        var itemMap = await LoadRecordItemsAsync(recordIds, ct);

        var equipLinks = await (
            from l in db.QualityRecordLinks.AsNoTracking()
            join e in db.Equipment.AsNoTracking() on l.TargetId equals e.Id
            where recordIds.Contains(l.RecordId) && l.TargetType == QualityLinkTargetType.Equipment && l.TargetId != null
            select new { l.RecordId, e.Id, e.Code, e.Name, e.SectionId })
            .ToListAsync(ct);
        var equipByRecord = equipLinks.GroupBy(e => e.RecordId).ToDictionary(g => g.Key, g => g.ToList());

        var pairs = new List<(int CauseId, string CauseName, QualityLinkTargetType TargetType, int TargetId, string TargetLabel, RepeatCauseRecordRow Record)>();
        foreach (var r in candidates)
        {
            var causeName = r.CauseCategoryName ?? $"Cause {r.CauseCategoryId}";
            var seenItems = new HashSet<int>();
            if (itemMap.TryGetValue(r.Id, out var items))
            {
                foreach (var (itemId, itemName) in items)
                {
                    if (seenItems.Add(itemId))
                        pairs.Add((r.CauseCategoryId, causeName, QualityLinkTargetType.Item, itemId, itemName, r));
                }
            }
            if (r.ComplaintItemId is int cmpItem && seenItems.Add(cmpItem))
                pairs.Add((r.CauseCategoryId, causeName, QualityLinkTargetType.Item, cmpItem, r.ComplaintItemName ?? $"Item {cmpItem}", r));

            if (equipByRecord.TryGetValue(r.Id, out var equips))
            {
                foreach (var eq in equips.DistinctBy(e => e.Id))
                {
                    var label = access.ReadsSection(eq.SectionId) ? eq.Name : $"{eq.Code} (Restricted)";
                    pairs.Add((r.CauseCategoryId, causeName, QualityLinkTargetType.Equipment, eq.Id, label, r));
                }
            }
        }

        var window = TimeSpan.FromDays(windowDays);
        var repeatCauses = new List<QualityRepeatCauseDto>();

        foreach (var group in pairs.GroupBy(x => (x.CauseId, x.CauseName, x.TargetType, x.TargetId, x.TargetLabel)))
        {
            var ordered = group.Select(x => x.Record).DistinctBy(r => r.Id).OrderBy(r => r.RaisedAtUtc).ToList();
            if (ordered.Count < 2) continue;

            // Find the largest (tie-break: latest) contiguous cluster within windowDays
            int bestStart = -1, bestCount = 0;
            var start = 0;
            for (var end = 0; end < ordered.Count; end++)
            {
                while (ordered[end].RaisedAtUtc - ordered[start].RaisedAtUtc > window)
                    start++;
                var count = end - start + 1;
                if (count >= 2 && count >= bestCount)
                {
                    bestCount = count;
                    bestStart = start;
                }
            }

            if (bestCount >= 2)
            {
                var cluster = ordered.Skip(bestStart).Take(bestCount).ToList();
                repeatCauses.Add(new QualityRepeatCauseDto(
                    group.Key.CauseId,
                    group.Key.CauseName,
                    group.Key.TargetType,
                    group.Key.TargetId,
                    group.Key.TargetLabel,
                    cluster.Count,
                    cluster[0].RaisedAtUtc,
                    cluster[^1].RaisedAtUtc,
                    cluster.Select(r => r.Number).ToList()));
            }
        }

        return repeatCauses.OrderByDescending(r => r.OccurrenceCount).ToList();
    }

    public async Task<QualityDashboardDto> GetDashboardAsync(int userId, CancellationToken ct = default)
    {
        var access = await core.GetReadAccessAsync(userId, ct);
        if (!access.Holds(PermissionConstants.QualityView))
            return new QualityDashboardDto([], [], [], [], [], []);

        var today = clock.LabToday;
        var openQuery = core.ReadableRecords(userId, access).Where(QualityConfigService.IsOpenExpression);

        var isQa = access.Holds(PermissionConstants.QualityApprove);
        var isLabConclude = access.Holds(PermissionConstants.QualityLabConclude);
        var isInvestigate = access.Holds(PermissionConstants.QualityInvestigate);

        IQueryable<QualityRecord>? waitingQuery = null;
        if (isQa)
        {
            waitingQuery = openQuery.Where(r =>
                (r.DeviationDetail != null && (
                    r.DeviationDetail.Status == QualityDeviationStatus.Raised
                    || ((r.DeviationDetail.Status == QualityDeviationStatus.LabConcluded
                            || (r.DeviationDetail.Status == QualityDeviationStatus.Approved
                                && (r.DeviationDetail.CapaRequired == false
                                    || openQuery.Where(DeviationService.IsApprovedCapaExpression).Any(c =>
                                        c.CapaDetail!.SourceRecordId == r.Id
                                        || c.Links.Any(l => l.TargetType == QualityLinkTargetType.QualityRecord && l.TargetId == r.Id)
                                        || r.Links.Any(l => l.TargetType == QualityLinkTargetType.QualityRecord && l.TargetId == c.Id)))))
                        && r.RaisedByUserId != userId && r.OwnerUserId != userId
                        && !r.Actions.Any(a => a.DoneAtUtc == null))))
                || (r.CapaDetail != null && r.RaisedByUserId != userId && r.OwnerUserId != userId && (
                    (r.CapaDetail.Status == CapaStatus.Plan
                        && r.Actions.Any()
                        && r.CapaDetail.EffectivenessCriteria != null && r.CapaDetail.EffectivenessCriteria != ""
                        && r.CapaDetail.EffectivenessMethod != null && r.CapaDetail.EffectivenessMethod != ""
                        && r.CapaDetail.EffectivenessPlannedDate != null
                        && !db.AuditLogs.Any(a =>
                            a.EntityName == nameof(QualityRecord)
                            && a.EntityId == r.Id.ToString()
                            && a.Action == "Update"
                            && a.Reason != null
                            && a.Id > (db.AuditLogs
                                .Where(s => (s.EntityName == nameof(CapaDetail) && s.EntityId == r.Id.ToString())
                                    || (s.EntityName == nameof(QualityAction) && r.Actions.Any(act => act.Id.ToString() == s.EntityId)))
                                .Select(s => (int?)s.Id)
                                .Max() ?? 0)))
                    || (r.CapaDetail.Status == CapaStatus.PlanApproved && !r.Actions.Any(a => a.DoneAtUtc == null))
                    || (r.CapaDetail.Status == CapaStatus.CompletionVerified && r.CapaDetail.EffectivenessPlannedDate != null && r.CapaDetail.EffectivenessPlannedDate <= today)))
                || (r.ChangeControlDetail != null && r.RaisedByUserId != userId && r.OwnerUserId != userId && (
                    r.ChangeControlDetail.Status == ChangeControlStatus.Assessed
                    || (r.ChangeControlDetail.Status == ChangeControlStatus.ApprovedForImplementation && !r.Actions.Any(a => a.DoneAtUtc == null))))
                || (r.ComplaintDetail != null && (
                    r.ComplaintDetail.Status == ComplaintStatus.Received
                    || ((r.ComplaintDetail.Status == ComplaintStatus.LabReported || r.ComplaintDetail.Status == ComplaintStatus.Concluded)
                        && r.RaisedByUserId != userId && r.OwnerUserId != userId
                        && !r.Actions.Any(a => a.DoneAtUtc == null)))));
        }
        else if (isLabConclude || isInvestigate)
        {
            waitingQuery = openQuery.Where(r =>
                (isLabConclude && r.DeviationDetail != null && r.DeviationDetail.Status == QualityDeviationStatus.Investigation
                    && r.DeviationDetail.RootCauseCategoryId != null && r.OwnerUserId != userId)
                || (isLabConclude && r.ComplaintDetail != null && r.ComplaintDetail.Status == ComplaintStatus.Investigation
                    && r.ComplaintDetail.Findings != null && r.OwnerUserId != userId)
                || (isInvestigate && r.ChangeControlDetail != null && r.ChangeControlDetail.Status == ChangeControlStatus.Proposed
                    && r.ChangeControlDetail.ImpactSummary != null));
        }

        var (waiting, waitingTotal) = waitingQuery is null
            ? ((IReadOnlyList<QualityRecordListItemDto>)[], 0)
            : await TakeDashboardRecordsAsync(waitingQuery, today, ct);

        var (myOpenRecords, myOpenRecordsTotal) = await TakeDashboardRecordsAsync(
            openQuery.Where(r => r.OwnerUserId == userId || r.RaisedByUserId == userId), today, ct);

        var (overdueRecords, overdueRecordsTotal) = await TakeDashboardRecordsAsync(
            openQuery.Where(r => r.DueDate != null && r.DueDate < today), today, ct);

        var (effectivenessDue, effectivenessDueTotal) = await TakeDashboardRecordsAsync(
            openQuery.Where(r => r.CapaDetail != null
                && r.CapaDetail.Status == CapaStatus.CompletionVerified
                && r.CapaDetail.EffectivenessPlannedDate != null
                && r.CapaDetail.EffectivenessPlannedDate <= today), today, ct);

        var (myOpenActions, myOpenActionsTotal) = await TakeDashboardActionsAsync(
            openQuery, a => a.DoneAtUtc == null && a.ResponsibleUserId == userId, today, ct);

        var (overdueActions, overdueActionsTotal) = await TakeDashboardActionsAsync(
            openQuery.Where(QualityConfigService.IsActionExecutionActiveExpression),
            a => a.DoneAtUtc == null && a.DueDate < today, today, ct);

        IReadOnlyList<QualityRecordListItemDto> labOpenRecords = [];
        var labOpenRecordsTotal = 0;
        IReadOnlyList<QualityDashboardActionDto> labOpenActions = [];
        var labOpenActionsTotal = 0;

        if (access.Role == RoleType.SectionHead && access.Sections is { Count: > 0 } sections)
        {
            var labOpenQuery = openQuery.Where(r => r.SectionId != null && sections.Contains(r.SectionId.Value));
            (labOpenRecords, labOpenRecordsTotal) = await TakeDashboardRecordsAsync(labOpenQuery, today, ct);
            (labOpenActions, labOpenActionsTotal) = await TakeDashboardActionsAsync(
                labOpenQuery, a => a.DoneAtUtc == null, today, ct);
        }

        return new QualityDashboardDto(
            waiting,
            myOpenRecords,
            myOpenActions,
            overdueRecords,
            overdueActions,
            effectivenessDue,
            waitingTotal,
            myOpenRecordsTotal,
            myOpenActionsTotal,
            overdueRecordsTotal,
            overdueActionsTotal,
            effectivenessDueTotal,
            labOpenRecords,
            labOpenRecordsTotal,
            labOpenActions,
            labOpenActionsTotal);
    }

    private async Task<(IReadOnlyList<QualityRecordListItemDto> Items, int TotalCount)> TakeDashboardRecordsAsync(
        IQueryable<QualityRecord> query,
        DateOnly today,
        CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        if (total == 0) return ([], 0);
        var paged = query
            .OrderBy(r => r.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(r => r.RaisedAtUtc)
            .Take(DashboardListCap);
        var items = await MaterializeListItemsAsync(paged, today, ct);
        return (items, total);
    }

    private async Task<(IReadOnlyList<QualityDashboardActionDto> Items, int TotalCount)> TakeDashboardActionsAsync(
        IQueryable<QualityRecord> recordQuery,
        Expression<Func<QualityAction, bool>> actionFilter,
        DateOnly today,
        CancellationToken ct)
    {
        var actionsQuery = recordQuery.SelectMany(
            r => r.Actions.AsQueryable().Where(actionFilter),
            (r, a) => new
            {
                r.Id,
                r.Number,
                r.Type,
                r.Title,
                ActionId = a.Id,
                a.Kind,
                a.Description,
                a.DueDate,
                IsExecutionActive = (r.CapaDetail == null || r.CapaDetail.Status != CapaStatus.Plan)
                    && (r.ChangeControlDetail == null || (r.ChangeControlDetail.Status != ChangeControlStatus.Proposed
                        && r.ChangeControlDetail.Status != ChangeControlStatus.Assessed))
            });

        var total = await actionsQuery.CountAsync(ct);
        if (total == 0) return ([], 0);
        var items = await actionsQuery
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.ActionId)
            .Take(DashboardListCap)
            .Select(x => new QualityDashboardActionDto(
                x.Id,
                x.Number,
                x.Type,
                x.Title,
                x.ActionId,
                x.Kind,
                x.Description,
                x.DueDate,
                x.IsExecutionActive && x.DueDate < today))
            .ToListAsync(ct);
        return (items, total);
    }

    private async Task<List<QualityRecordListItemDto>> MaterializeListItemsAsync(IQueryable<QualityRecord> query, DateOnly today, CancellationToken ct)
    {
        var rows = await query.Select(r => new
        {
            r.Id,
            r.Number,
            r.Type,
            r.Title,
            r.SectionId,
            SectionName = r.SectionId == null ? null : db.DocumentSections.Where(s => s.Id == r.SectionId).Select(s => s.Name).FirstOrDefault(),
            r.ClassificationId,
            ClassificationName = r.ClassificationId == null ? null : db.QualityClassifications.Where(c => c.Id == r.ClassificationId).Select(c => c.Name).FirstOrDefault(),
            r.RaisedByUserId,
            RaisedByName = r.RaisedByUserId == null ? "System" : db.Users.Where(u => u.Id == r.RaisedByUserId).Select(u => u.FullName).FirstOrDefault(),
            r.RaisedAtUtc,
            r.OwnerUserId,
            OwnerName = r.OwnerUserId == null ? null : db.Users.Where(u => u.Id == r.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
            r.DueDate,
            r.IsCancelled,
            r.Version,
            DevStatus = r.DeviationDetail != null ? (QualityDeviationStatus?)r.DeviationDetail.Status : null,
            CapaStatus = r.CapaDetail != null ? (CapaStatus?)r.CapaDetail.Status : null,
            CcStatus = r.ChangeControlDetail != null ? (ChangeControlStatus?)r.ChangeControlDetail.Status : null,
            CmpStatus = r.ComplaintDetail != null ? (ComplaintStatus?)r.ComplaintDetail.Status : null
        }).ToListAsync(ct);

        return rows.Select(r => new QualityRecordListItemDto(
            r.Id,
            r.Number,
            r.Type,
            r.Title,
            ResolveStatusString(r.IsCancelled, r.DevStatus, r.CapaStatus, r.CcStatus, r.CmpStatus),
            r.SectionId,
            r.SectionName,
            r.ClassificationId,
            r.ClassificationName,
            r.RaisedByUserId,
            r.RaisedByName,
            r.RaisedAtUtc,
            r.OwnerUserId,
            r.OwnerName,
            r.DueDate,
            QualityConfigService.IsOpenStatus(r.IsCancelled, r.DevStatus, r.CapaStatus, r.CcStatus, r.CmpStatus)
                && r.DueDate != null && r.DueDate < today,
            r.IsCancelled,
            r.Version)).ToList();
    }

    private static Expression<Func<QualityRecord, bool>> CombineOr(
        Expression<Func<QualityRecord, bool>> left,
        Expression<Func<QualityRecord, bool>> right)
    {
        var param = left.Parameters[0];
        var replacedRight = new ParameterReplacer(right.Parameters[0], param).Visit(right.Body);
        return Expression.Lambda<Func<QualityRecord, bool>>(Expression.OrElse(left.Body, replacedRight), param);
    }

    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }

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

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime()
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    internal static string FormatUtcWithZone(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    private sealed record SqlTrendCounts(
        string Key,
        int TotalCount,
        int OpenCount,
        int ClosedCount,
        int CancelledCount,
        int RejectedCount,
        int CapaEffTotal,
        int CapaEffPass);

    private sealed record TrendLinkRecordRow(
        int Id,
        QualityRecordType Type,
        DateTime RaisedAtUtc,
        DateOnly? DueDate,
        bool IsCancelled,
        bool IsRejected,
        bool IsClosed,
        bool IsOpen,
        CapaEffectiveness? CapaEffectiveness,
        int? ComplaintItemId,
        string? ComplaintItemName,
        DateTime? ClosedAtUtc);

    private sealed record RepeatCauseRecordRow(
        int Id,
        string Number,
        DateTime RaisedAtUtc,
        int CauseCategoryId,
        string? CauseCategoryName,
        int? ComplaintItemId,
        string? ComplaintItemName);
}
