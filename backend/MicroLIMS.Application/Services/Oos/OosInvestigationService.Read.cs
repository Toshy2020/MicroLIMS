using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Oos;

public sealed partial class OosInvestigationService
{
    public async Task<PagedResult<OosInvestigationListItemDto>> ListAsync(
        OosInvestigationFilter filter, CancellationToken ct = default)
    {
        var actor = db.CurrentUserId ?? 0;
        var query = await ScopedReadQueryAsync(actor, ct);

        if (filter.Status is OosInvestigationStatus status)
            query = query.Where(i => i.Status == status);
        if (filter.Kind is OosInvestigationKind kind)
            query = query.Where(i => i.Kind == kind);
        if (filter.SectionId is int sectionId)
            query = query.Where(i => i.SectionId == sectionId);
        if (filter.From is DateTime from)
            query = query.Where(i => i.OpenedAtUtc >= from);
        if (filter.To is DateTime to)
            query = query.Where(i => i.OpenedAtUtc <= to);

        var today = clock.LabToday;
        if (filter.Overdue is bool overdue)
        {
            query = overdue
                ? query.Where(i => i.Status != OosInvestigationStatus.Concluded &&
                    ((i.DueDate != null && i.DueDate < today) ||
                     (i.Status == OosInvestigationStatus.PhaseOne && i.PhaseOneDueDate != null && i.PhaseOneDueDate < today)))
                : query.Where(i => i.Status == OosInvestigationStatus.Concluded ||
                    ((i.DueDate == null || i.DueDate >= today) &&
                     (i.Status != OosInvestigationStatus.PhaseOne || i.PhaseOneDueDate == null || i.PhaseOneDueDate >= today)));
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var total = await query.CountAsync(ct);

        var paged = query
            .OrderByDescending(i => i.OpenedAtUtc)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var items = await ProjectListItems(paged, today).ToListAsync(ct);

        return new PagedResult<OosInvestigationListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<OosInvestigationListItemDto>> GetForSampleAsync(
        int sampleId, CancellationToken ct = default)
    {
        var actor = db.CurrentUserId ?? 0;
        var query = (await ScopedReadQueryAsync(actor, ct))
            .Where(i => i.SampleId == sampleId ||
                i.Results.Any(r => db.TestOrders.Any(o => o.Id == r.TestOrderId && o.SampleId == sampleId)) ||
                (from link in db.OosRetestPlanSamples
                 join plan in db.OosRetestPlans on link.PlanId equals plan.Id
                 where link.SampleId == sampleId && plan.InvestigationId == i.Id && plan.SignedAtUtc != null
                 select link.SampleId).Any())
            .OrderByDescending(i => i.OpenedAtUtc)
            .ThenByDescending(i => i.Id);

        return await ProjectListItems(query, clock.LabToday).ToListAsync(ct);
    }

    public async Task<OosInvestigationDetailDto> GetByIdAsync(int investigationId, CancellationToken ct = default)
    {
        var actor = db.CurrentUserId ?? 0;
        var today = clock.LabToday;
        var header = await (await ScopedReadQueryAsync(actor, ct))
            .Where(i => i.Id == investigationId)
            .Select(i => new
            {
                i.Id,
                i.Number,
                i.Kind,
                i.Status,
                i.SampleId,
                SampleReferenceNumber = db.Samples.Where(s => s.Id == i.SampleId).Select(s => s.ReferenceNumber).FirstOrDefault() ?? "",
                SampleCategory = db.Samples.Where(s => s.Id == i.SampleId).Select(s => s.Category).FirstOrDefault(),
                SampleDisplayName = db.Samples.Where(s => s.Id == i.SampleId)
                    .Select(s => s.Category == SampleCategory.AfterCleaning ? (s.Machine != null ? s.Machine.Name : "")
                        : s.Category == SampleCategory.Water ? (s.WaterSamplingPoint != null ? s.WaterSamplingPoint.Code : "")
                        : s.Category == SampleCategory.EnvironmentalMonitoring ? (s.Department != null ? s.Department.Name : "")
                        : (s.Item != null ? s.Item.Name : ""))
                    .FirstOrDefault() ?? "",
                BatchNumber = db.Samples.Where(s => s.Id == i.SampleId).Select(s => s.BatchNumber).FirstOrDefault(),
                i.SectionId,
                SectionName = db.DocumentSections.Where(s => s.Id == i.SectionId).Select(s => s.Name).FirstOrDefault() ?? "",
                i.OpenedAtUtc,
                i.OpenedByUserId,
                OpenedByName = db.Users.Where(u => u.Id == i.OpenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
                i.TemplateSnapshotJson,
                i.PhaseOneDueDate,
                i.DueDate,
                IsOverdue = i.Status != OosInvestigationStatus.Concluded &&
                    ((i.DueDate != null && i.DueDate < today) ||
                     (i.Status == OosInvestigationStatus.PhaseOne && i.PhaseOneDueDate != null && i.PhaseOneDueDate < today)),
                i.PhaseOneAssessment,
                i.PhaseOneCauseCategoryId,
                PhaseOneCauseCategoryName = i.PhaseOneCauseCategoryId == null ? null
                    : db.OosCauseCategories.Where(c => c.Id == i.PhaseOneCauseCategoryId).Select(c => c.Name).FirstOrDefault(),
                i.PhaseOneCauseDescription,
                i.PhaseOneSignedAtUtc,
                i.PhaseOneSignedByUserId,
                PhaseOneSignedByName = i.PhaseOneSignedByUserId == null ? null
                    : db.Users.Where(u => u.Id == i.PhaseOneSignedByUserId).Select(u => u.FullName).FirstOrDefault(),
                i.Conclusion,
                i.RootCauseCategoryId,
                RootCauseCategoryName = i.RootCauseCategoryId == null ? null
                    : db.OosCauseCategories.Where(c => c.Id == i.RootCauseCategoryId).Select(c => c.Name).FirstOrDefault(),
                i.RootCauseDescription,
                i.ConclusionSummary,
                i.ConcludedAtUtc,
                i.ConcludedByUserId,
                ConcludedByName = i.ConcludedByUserId == null ? null
                    : db.Users.Where(u => u.Id == i.ConcludedByUserId).Select(u => u.FullName).FirstOrDefault(),
                i.Version
            })
            .SingleOrDefaultAsync(ct)
            ?? throw InvestigationNotFound();

        var results = await db.OosInvestigationResults.AsNoTracking()
            .Where(r => r.InvestigationId == investigationId)
            .Join(db.TestOrders.AsNoTracking(), r => r.TestOrderId, o => o.Id, (r, o) => new { r, o })
            .OrderBy(x => x.r.Id)
            .Select(x => new OosInvestigationResultDto(
                x.r.Id,
                x.r.TestOrderId,
                x.o.TestCode,
                db.TestDefinitions.Where(td => td.Code == x.o.TestCode).Select(td => td.DisplayName).FirstOrDefault() ?? x.o.TestCode,
                x.r.OutcomeAtOpening,
                x.r.ResultSummaryAtOpening,
                x.r.AddedAtUtc,
                x.r.IsInvalidated,
                x.r.InvalidatedAtUtc))
            .ToListAsync(ct);

        var entries = await db.OosInvestigationEntrys.AsNoTracking()
            .Where(e => e.InvestigationId == investigationId)
            .OrderBy(e => e.Id)
            .Select(e => new OosInvestigationEntryDto(
                e.Id,
                e.Phase,
                e.Kind,
                e.ChecklistItemKey,
                e.Answer,
                e.Text,
                e.ParentEntryId,
                e.RecordedByUserId,
                db.Users.Where(u => u.Id == e.RecordedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
                e.RecordedAtUtc))
            .ToListAsync(ct);

        var planRows = await db.OosRetestPlans.AsNoTracking()
            .Where(p => p.InvestigationId == investigationId)
            .OrderBy(p => p.Revision)
            .Select(p => new
            {
                p.Id,
                p.Revision,
                p.Source,
                p.RetestCount,
                p.TestOrderIdsJson,
                p.AnalystUserIdsJson,
                p.JudgingRule,
                p.AmendmentReason,
                p.SignedAtUtc,
                p.SignedByUserId,
                SignedByName = p.SignedByUserId == null ? null
                    : db.Users.Where(u => u.Id == p.SignedByUserId).Select(u => u.FullName).FirstOrDefault(),
                p.SamplesCreatedAtUtc
            })
            .ToListAsync(ct);

        var planIds = planRows.Select(p => p.Id).ToArray();
        var planSamples = planIds.Length == 0
            ? new Dictionary<int, List<OosRetestPlanSampleDto>>()
            : (await (from link in db.OosRetestPlanSamples.AsNoTracking()
                      where planIds.Contains(link.PlanId)
                      join sample in db.Samples.AsNoTracking() on link.SampleId equals sample.Id
                      orderby sample.Id
                      select new
                      {
                          link.PlanId,
                          Sample = new OosRetestPlanSampleDto(sample.Id, sample.ReferenceNumber, sample.Status)
                      }).ToListAsync(ct))
                .GroupBy(x => x.PlanId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Sample).ToList());

        var plans = planRows.Select(p => new OosRetestPlanDetailDto(
            p.Id,
            p.Revision,
            p.Source,
            p.RetestCount,
            DeserializeIntList(p.TestOrderIdsJson),
            DeserializeIntList(p.AnalystUserIdsJson),
            p.JudgingRule,
            p.AmendmentReason,
            p.SignedAtUtc,
            p.SignedByUserId,
            p.SignedByName,
            p.SamplesCreatedAtUtc,
            planSamples.GetValueOrDefault(p.Id) ?? [])).ToList();

        var reopenings = await db.OosInvestigationReopenings.AsNoTracking()
            .Where(r => r.InvestigationId == investigationId)
            .OrderBy(r => r.Id)
            .Select(r => new OosInvestigationReopeningDto(
                r.Id,
                r.Reason,
                r.PreviousConclusion,
                r.PreviousRootCauseCategoryId == null ? null
                    : db.OosCauseCategories.Where(c => c.Id == r.PreviousRootCauseCategoryId).Select(c => c.Name).FirstOrDefault(),
                r.PreviousRootCauseDescription,
                r.PreviousConclusionSummary,
                r.PreviousConcludedByUserId == null ? null
                    : db.Users.Where(u => u.Id == r.PreviousConcludedByUserId).Select(u => u.FullName).FirstOrDefault(),
                r.PreviousConcludedAtUtc,
                r.ReopenedByUserId,
                db.Users.Where(u => u.Id == r.ReopenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
                r.ReopenedAtUtc))
            .ToListAsync(ct);

        var deviationRequests = await db.OosDeviationRequests.AsNoTracking()
            .Where(d => d.InvestigationId == investigationId)
            .OrderBy(d => d.Id)
            .Select(d => new OosDeviationRequestDetailDto(
                d.Id,
                d.Reason,
                d.RequestedByUserId,
                db.Users.Where(u => u.Id == d.RequestedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
                d.RequestedAtUtc,
                d.Status,
                d.AddressedComment,
                d.AddressedByUserId,
                d.AddressedByUserId == null ? null
                    : db.Users.Where(u => u.Id == d.AddressedByUserId).Select(u => u.FullName).FirstOrDefault(),
                d.AddressedAtUtc,
                d.Version))
            .ToListAsync(ct);

        var signatures = await db.ElectronicSignatures.AsNoTracking()
            .Where(s => (s.EntityType == nameof(OosInvestigation) && s.EntityId == investigationId) ||
                        (s.EntityType == nameof(OosRetestPlan) && planIds.Contains(s.EntityId)))
            .OrderBy(s => s.SignedAt)
            .ThenBy(s => s.Id)
            .Select(s => new
            {
                s.UserFullNameSnapshot,
                s.UsernameSnapshot,
                Role = s.RoleNameSnapshot ?? s.RoleSnapshot,
                s.SignedAt,
                Meaning = s.MeaningOfSignature.ToString(),
                s.Comment
            })
            .ToListAsync(ct);

        var timeline = signatures.Select(s => new OosSignedTimelineEntryDto(
            s.UserFullNameSnapshot,
            s.UsernameSnapshot,
            s.Role,
            s.SignedAt,
            FormatUtcWithZone(s.SignedAt),
            s.Meaning,
            ReportVocabulary.SignatureMeaning(s.Meaning),
            s.Comment)).ToList();

        return new OosInvestigationDetailDto(
            header.Id,
            header.Number,
            header.Kind,
            header.Status,
            header.SampleId,
            header.SampleReferenceNumber,
            header.SampleCategory.ToString(),
            header.SampleDisplayName,
            header.BatchNumber,
            header.SectionId,
            header.SectionName,
            header.OpenedAtUtc,
            header.OpenedByUserId,
            header.OpenedByName,
            header.PhaseOneDueDate,
            header.DueDate,
            header.IsOverdue,
            header.PhaseOneAssessment,
            header.PhaseOneCauseCategoryId,
            header.PhaseOneCauseCategoryName,
            header.PhaseOneCauseDescription,
            header.PhaseOneSignedAtUtc,
            header.PhaseOneSignedByUserId,
            header.PhaseOneSignedByName,
            header.Conclusion,
            header.RootCauseCategoryId,
            header.RootCauseCategoryName,
            header.RootCauseDescription,
            header.ConclusionSummary,
            header.ConcludedAtUtc,
            header.ConcludedByUserId,
            header.ConcludedByName,
            header.Version,
            ReadChecklistItems(header.TemplateSnapshotJson),
            results,
            entries,
            plans,
            reopenings,
            deviationRequests,
            timeline);
    }

    internal static string FormatUtcWithZone(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    internal static IQueryable<OosInvestigation> FilterAnalystOwned(
        IMicroLimsDbContext db, IQueryable<OosInvestigation> query, int actor)
    {
        return query.Where(i =>
            i.Results.Any(r =>
                db.TestOrders.Any(o => o.Id == r.TestOrderId && o.AssignedAnalystId == actor) ||
                (from o in db.TestOrders
                 join p in db.SamplePreparations on o.SampleId equals p.SampleId
                 where o.Id == r.TestOrderId && p.PreparedByUserId == actor
                 select o.Id).Any() ||
                db.Results.Any(res => res.TestOrderId == r.TestOrderId && res.EnteredByUserId == actor) ||
                db.CountTestReadings.Any(c => c.TestOrderId == r.TestOrderId && c.EnteredByUserId == actor) ||
                db.PathogenObservations.Any(po => po.TestOrderId == r.TestOrderId && po.ObservedByUserId == actor) ||
                db.WorkflowStepResults.Any(ws => ws.TestOrderId == r.TestOrderId && ws.SubmittedByUserId == actor) ||
                db.TestAnalyses.Any(ta => ta.TestOrderId == r.TestOrderId && ta.EnteredByUserId == actor) ||
                db.ConfirmatoryPlateObservations.Any(cp => cp.WorkflowStepResult != null &&
                    cp.WorkflowStepResult.TestOrderId == r.TestOrderId && cp.RecordedByUserId == actor)) ||
            (from plan in db.OosRetestPlans
             join link in db.OosRetestPlanSamples on plan.Id equals link.PlanId
             join o in db.TestOrders on link.SampleId equals o.SampleId
             where plan.InvestigationId == i.Id && plan.SignedAtUtc != null &&
                (o.AssignedAnalystId == actor ||
                 db.SamplePreparations.Any(p => p.SampleId == o.SampleId && p.PreparedByUserId == actor) ||
                 db.Results.Any(res => res.TestOrderId == o.Id && res.EnteredByUserId == actor) ||
                 db.CountTestReadings.Any(c => c.TestOrderId == o.Id && c.EnteredByUserId == actor) ||
                 db.PathogenObservations.Any(po => po.TestOrderId == o.Id && po.ObservedByUserId == actor) ||
                 db.WorkflowStepResults.Any(ws => ws.TestOrderId == o.Id && ws.SubmittedByUserId == actor) ||
                 db.TestAnalyses.Any(ta => ta.TestOrderId == o.Id && ta.EnteredByUserId == actor) ||
                 db.ConfirmatoryPlateObservations.Any(cp => cp.WorkflowStepResult != null &&
                     cp.WorkflowStepResult.TestOrderId == o.Id && cp.RecordedByUserId == actor))
             select o.Id).Any());
    }

    private async Task<IQueryable<OosInvestigation>> ScopedReadQueryAsync(int actor, CancellationToken ct)
    {
        if (!await permissions.UserHasPermissionAsync(actor, PermissionConstants.OosView, ct))
            throw new CodedAccessDeniedException(MessageCodes.OosInvestigationAccessDenied, "You do not have permission for this investigation action.");

        var query = db.OosInvestigations.AsNoTracking();
        var readable = await scope.GetReadableSectionIdsAsync(actor, ct);
        if (readable is not null)
            query = query.Where(i => readable.Contains(i.SectionId));

        var isAnalyst = await db.Users.AsNoTracking().AnyAsync(u => u.Id == actor && u.Role != null && u.Role.Type == RoleType.Analyst, ct);
        if (isAnalyst && !await permissions.UserHasPermissionAsync(actor, PermissionConstants.OosManage, ct))
            query = FilterAnalystOwned(db, query, actor);

        return query;
    }

    private IQueryable<OosInvestigationListItemDto> ProjectListItems(IQueryable<OosInvestigation> query, DateOnly today) =>
        query.Select(i => new OosInvestigationListItemDto(
            i.Id,
            i.Number,
            i.Kind,
            i.Status,
            i.SampleId,
            db.Samples.Where(s => s.Id == i.SampleId).Select(s => s.ReferenceNumber).FirstOrDefault() ?? "",
            db.Samples.Where(s => s.Id == i.SampleId)
                .Select(s => s.Category == SampleCategory.AfterCleaning ? (s.Machine != null ? s.Machine.Name : "")
                    : s.Category == SampleCategory.Water ? (s.WaterSamplingPoint != null ? s.WaterSamplingPoint.Code : "")
                    : s.Category == SampleCategory.EnvironmentalMonitoring ? (s.Department != null ? s.Department.Name : "")
                    : (s.Item != null ? s.Item.Name : ""))
                .FirstOrDefault() ?? "",
            db.Samples.Where(s => s.Id == i.SampleId).Select(s => s.BatchNumber).FirstOrDefault(),
            i.SectionId,
            db.DocumentSections.Where(s => s.Id == i.SectionId).Select(s => s.Name).FirstOrDefault() ?? "",
            i.OpenedAtUtc,
            db.Users.Where(u => u.Id == i.OpenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
            i.PhaseOneDueDate,
            i.DueDate,
            i.Status != OosInvestigationStatus.Concluded &&
                ((i.DueDate != null && i.DueDate < today) ||
                 (i.Status == OosInvestigationStatus.PhaseOne && i.PhaseOneDueDate != null && i.PhaseOneDueDate < today)),
            i.Conclusion,
            i.ConcludedAtUtc,
            i.RootCauseCategoryId == null ? null
                : db.OosCauseCategories.Where(c => c.Id == i.RootCauseCategoryId).Select(c => c.Name).FirstOrDefault(),
            db.OosInvestigationResults
                .Where(r => r.InvestigationId == i.Id)
                .Join(db.TestOrders, r => r.TestOrderId, o => o.Id, (_, o) => o.TestCode)
                .Distinct()
                .ToList(),
            i.Version));

    private static IReadOnlyList<OosChecklistTemplateItemDto> ReadChecklistItems(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson)) return [];
        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<OosChecklistTemplateItemDto>();
            foreach (var item in items.EnumerateArray())
            {
                var phase = item.TryGetProperty("phase", out var p) && p.TryGetInt32(out var pv) ? pv : 1;
                var key = item.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                var order = item.TryGetProperty("order", out var o) && o.TryGetInt32(out var ov) ? ov : 0;
                var text = item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(key))
                    list.Add(new OosChecklistTemplateItemDto(phase, key, order, text));
            }
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<int> DeserializeIntList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
