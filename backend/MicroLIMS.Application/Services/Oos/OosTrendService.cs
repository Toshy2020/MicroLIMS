using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosTrendService(
    IMicroLimsDbContext db,
    IUserSectionScopeService scope,
    PermissionService permissions,
    ILabClock clock)
{
    private sealed record TrendRow(
        int InvestigationId,
        string Key,
        string Label,
        OosInvestigationStatus Status,
        OosConclusion? Conclusion,
        DateTime OpenedAtUtc,
        DateTime? ConcludedAtUtc);

    public async Task<OosTrendReportDto> GetTrendsAsync(
        string groupBy,
        DateTime? from = null,
        DateTime? to = null,
        OosInvestigationKind? kind = null,
        int? sectionId = null,
        CancellationToken ct = default)
    {
        var actor = db.CurrentUserId ?? 0;
        if (!await permissions.UserHasPermissionAsync(actor, PermissionConstants.OosView, ct))
            throw new CodedAccessDeniedException(MessageCodes.OosInvestigationAccessDenied, "You do not have permission for this investigation action.");

        var normalized = (groupBy ?? "").Trim().ToLowerInvariant();
        if (normalized is not ("product" or "test" or "analyst" or "instrument" or "cause" or "conclusion"))
            throw new BusinessRuleException(MessageCodes.OosTrendGroupByInvalid,
                "Choose a valid trend grouping: product, test, analyst, instrument, cause, or conclusion.");

        var baseQuery = db.OosInvestigations.AsNoTracking();
        var readable = await scope.GetReadableSectionIdsAsync(actor, ct);
        if (readable is not null)
            baseQuery = baseQuery.Where(i => readable.Contains(i.SectionId));

        var isAnalyst = await db.Users.AsNoTracking().AnyAsync(u => u.Id == actor && u.Role != null && u.Role.Type == RoleType.Analyst, ct);
        if (isAnalyst && !await permissions.UserHasPermissionAsync(actor, PermissionConstants.OosManage, ct))
            baseQuery = OosInvestigationService.FilterAnalystOwned(db, baseQuery, actor);

        if (kind is OosInvestigationKind k)
            baseQuery = baseQuery.Where(i => i.Kind == k);
        if (sectionId is int sec)
            baseQuery = baseQuery.Where(i => i.SectionId == sec);
        if (from is DateTime f)
            baseQuery = baseQuery.Where(i => i.OpenedAtUtc >= f);
        if (to is DateTime t)
            baseQuery = baseQuery.Where(i => i.OpenedAtUtc <= t);

        var rows = normalized switch
        {
            "product" => await QueryByProductAsync(baseQuery, ct),
            "test" => await QueryByTestAsync(baseQuery, ct),
            "analyst" => await QueryByAnalystAsync(baseQuery, ct),
            "instrument" => await QueryByInstrumentAsync(baseQuery, ct),
            "cause" => await QueryByCauseAsync(baseQuery, ct),
            _ => await QueryByConclusionAsync(baseQuery, ct)
        };

        var settings = await db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new MonitoringSettings();
        var holidays = (await db.LabCalendarDates.AsNoTracking().Select(d => d.Date).ToListAsync(ct)).ToHashSet();

        var groups = rows
            .GroupBy(r => (r.Key, r.Label))
            .Select(g =>
            {
                var distinct = g.DistinctBy(x => x.InvestigationId).ToList();
                var durations = distinct
                    .Where(x => x.Status == OosInvestigationStatus.Concluded && x.ConcludedAtUtc.HasValue)
                    .Select(x => WorkingDaysBetween(
                        DateOnly.FromDateTime(clock.ToLabLocal(x.OpenedAtUtc)),
                        DateOnly.FromDateTime(clock.ToLabLocal(x.ConcludedAtUtc!.Value)),
                        settings.WorkingWeekdays, holidays))
                    .OrderBy(d => d)
                    .ToList();

                return new OosTrendGroupDto(
                    g.Key.Key,
                    g.Key.Label,
                    distinct.Count,
                    distinct.Count(x => x.Status != OosInvestigationStatus.Concluded),
                    distinct.Count(x => x.Status == OosInvestigationStatus.Concluded),
                    distinct.Count(x => x.Conclusion == OosConclusion.Confirmed),
                    distinct.Count(x => x.Conclusion == OosConclusion.Invalidated),
                    distinct.Count(x => x.Conclusion == OosConclusion.ExcursionClosed),
                    Median(durations));
            })
            .OrderByDescending(g => g.TotalCount)
            .ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OosTrendReportDto(normalized, from, to, groups);
    }

    private Task<List<TrendRow>> QueryByProductAsync(IQueryable<OosInvestigation> query, CancellationToken ct) =>
        (from i in query
         join s in db.Samples.AsNoTracking() on i.SampleId equals s.Id
         select new TrendRow(
             i.Id,
             s.ItemId != null ? $"item:{s.ItemId}" : $"cat:{s.Category}",
             s.Item != null ? s.Item.Name
                 : s.WaterSamplingPoint != null ? s.WaterSamplingPoint.Code
                 : s.Department != null ? s.Department.Name
                 : s.Machine != null ? s.Machine.Name
                 : s.Category.ToString(),
             i.Status,
             i.Conclusion,
             i.OpenedAtUtc,
             i.ConcludedAtUtc)).ToListAsync(ct);

    private Task<List<TrendRow>> QueryByTestAsync(IQueryable<OosInvestigation> query, CancellationToken ct) =>
        (from i in query
         join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
         join o in db.TestOrders.AsNoTracking() on r.TestOrderId equals o.Id
         select new TrendRow(
             i.Id,
             o.TestCode,
             db.TestDefinitions.Where(td => td.Code == o.TestCode).Select(td => td.DisplayName).FirstOrDefault() ?? o.TestCode,
             i.Status,
             i.Conclusion,
             i.OpenedAtUtc,
             i.ConcludedAtUtc)).Distinct().ToListAsync(ct);

    private Task<List<TrendRow>> QueryByAnalystAsync(IQueryable<OosInvestigation> query, CancellationToken ct) =>
        (from i in query
         join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
         join o in db.TestOrders.AsNoTracking() on r.TestOrderId equals o.Id
         where o.AssignedAnalystId != null
         join u in db.Users.AsNoTracking() on o.AssignedAnalystId equals u.Id
         select new TrendRow(
             i.Id,
             u.Id.ToString(CultureInfo.InvariantCulture),
             u.FullName,
             i.Status,
             i.Conclusion,
             i.OpenedAtUtc,
             i.ConcludedAtUtc)).Distinct().ToListAsync(ct);

    private Task<List<TrendRow>> QueryByInstrumentAsync(IQueryable<OosInvestigation> query, CancellationToken ct)
    {
        var analyses = from i in query
                       join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
                       join a in db.TestAnalyses.AsNoTracking() on r.TestOrderId equals a.TestOrderId
                       where a.EquipmentId != null
                       join e in db.Equipment.AsNoTracking() on a.EquipmentId equals e.Id
                       select new TrendRow(i.Id, e.Id.ToString(CultureInfo.InvariantCulture), e.Name, i.Status, i.Conclusion, i.OpenedAtUtc, i.ConcludedAtUtc);

        var incubations = from i in query
                          join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
                          join inc in db.Incubations.AsNoTracking() on r.TestOrderId equals inc.TestOrderId
                          where inc.IncubatorEquipmentId != null
                          join e in db.Equipment.AsNoTracking() on inc.IncubatorEquipmentId equals e.Id
                          select new TrendRow(i.Id, e.Id.ToString(CultureInfo.InvariantCulture), e.Name, i.Status, i.Conclusion, i.OpenedAtUtc, i.ConcludedAtUtc);

        var hplc = from i in query
                   join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
                   join hs in db.HplcRunSamples.AsNoTracking() on r.TestOrderId equals hs.TestOrderId
                   where hs.RemovedAt == null
                   join hr in db.HplcRuns.AsNoTracking() on hs.HplcRunId equals hr.Id
                   join e in db.Equipment.AsNoTracking() on hr.EquipmentId equals e.Id
                   select new TrendRow(i.Id, e.Id.ToString(CultureInfo.InvariantCulture), e.Name, i.Status, i.Conclusion, i.OpenedAtUtc, i.ConcludedAtUtc);

        var icp = from i in query
                  join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
                  join isamp in db.IcpRunSamples.AsNoTracking() on r.TestOrderId equals isamp.TestOrderId
                  where isamp.RemovedAt == null
                  join ir in db.IcpRuns.AsNoTracking() on isamp.IcpRunId equals ir.Id
                  join e in db.Equipment.AsNoTracking() on ir.EquipmentId equals e.Id
                  select new TrendRow(i.Id, e.Id.ToString(CultureInfo.InvariantCulture), e.Name, i.Status, i.Conclusion, i.OpenedAtUtc, i.ConcludedAtUtc);

        return analyses.Union(incubations).Union(hplc).Union(icp).ToListAsync(ct);
    }

    private Task<List<TrendRow>> QueryByCauseAsync(IQueryable<OosInvestigation> query, CancellationToken ct) =>
        (from i in query
         let catId = i.RootCauseCategoryId ?? i.PhaseOneCauseCategoryId
         select new TrendRow(
             i.Id,
             catId != null ? catId.Value.ToString(CultureInfo.InvariantCulture) : "unassigned",
             catId != null
                 ? (db.OosCauseCategories.Where(c => c.Id == catId).Select(c => c.Name).FirstOrDefault() ?? "Unassigned")
                 : "Unassigned",
             i.Status,
             i.Conclusion,
             i.OpenedAtUtc,
             i.ConcludedAtUtc)).ToListAsync(ct);

    private static Task<List<TrendRow>> QueryByConclusionAsync(IQueryable<OosInvestigation> query, CancellationToken ct) =>
        query.Select(i => new TrendRow(
            i.Id,
            i.Conclusion != null ? i.Conclusion.ToString()! : "Open",
            i.Conclusion != null ? ReportVocabulary.Humanize(i.Conclusion.ToString()) : "Open",
            i.Status,
            i.Conclusion,
            i.OpenedAtUtc,
            i.ConcludedAtUtc)).ToListAsync(ct);

    private static int WorkingDaysBetween(DateOnly start, DateOnly end, int workingWeekdays, IReadOnlySet<DateOnly> holidays)
    {
        if (end <= start) return 0;
        var count = 0;
        for (var d = start.AddDays(1); d <= end; d = d.AddDays(1))
        {
            if (PlanWindowCalculator.IsWorkingDay(d, workingWeekdays, holidays))
                count++;
        }
        return count;
    }

    private static double? Median(List<int> sorted)
    {
        if (sorted.Count == 0) return null;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
