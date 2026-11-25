using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosTrendService(
    IMicroLimsDbContext db,
    OosInvestigationService investigations,
    ILabClock clock)
{
    private sealed class TrendRow
    {
        public int InvestigationId { get; init; }
        public string? Code { get; init; }
        public int? GroupId { get; init; }
        public string? Label { get; init; }
        public OosInvestigationStatus Status { get; init; }
        public OosConclusion? Conclusion { get; init; }
        public DateTime OpenedAtUtc { get; init; }
        public DateTime? ConcludedAtUtc { get; init; }
    }

    public async Task<OosTrendReportDto> GetTrendsAsync(
        string groupBy,
        DateTime? from = null,
        DateTime? to = null,
        OosInvestigationKind? kind = null,
        int? sectionId = null,
        CancellationToken ct = default)
    {
        var baseQuery = await investigations.ScopedReadQueryAsync(db.CurrentUserId ?? 0, ct);
        var normalized = (groupBy ?? "").Trim().ToLowerInvariant();
        if (normalized is not ("product" or "test" or "analyst" or "instrument" or "cause" or "conclusion"))
            throw new BusinessRuleException(MessageCodes.OosTrendGroupByInvalid,
                "Choose a valid trend grouping: product, test, analyst, instrument, cause, or conclusion.");

        if (kind is OosInvestigationKind k)
            baseQuery = baseQuery.Where(i => i.Kind == k);
        if (sectionId is int sec)
            baseQuery = baseQuery.Where(i => i.SectionId == sec);

        var fromUtc = from is DateTime f ? OosReadDates.ToUtc(f) : (DateTime?)null;
        var toUtc = to is DateTime t ? OosReadDates.ToUtc(t) : (DateTime?)null;
        if (fromUtc.HasValue)
            baseQuery = baseQuery.Where(i => i.OpenedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            baseQuery = baseQuery.Where(i => i.OpenedAtUtc <= toUtc.Value);

        var rows = normalized switch
        {
            "product" => QueryByProduct(baseQuery),
            "test" => QueryByTest(baseQuery),
            "analyst" => QueryByAnalyst(baseQuery),
            "instrument" => QueryByInstrument(baseQuery),
            "cause" => QueryByCause(baseQuery),
            _ => QueryByConclusion(baseQuery)
        };

        // A single investigation may contribute several orders to the same group.
        var distinct = rows.Distinct();
        var counts = await distinct
            .GroupBy(r => new { r.Code, r.GroupId, r.Label })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.GroupId,
                g.Key.Label,
                Total = g.Count(),
                Open = g.Count(r => r.Status != OosInvestigationStatus.Concluded),
                Concluded = g.Count(r => r.Status == OosInvestigationStatus.Concluded),
                Confirmed = g.Count(r => r.Conclusion == OosConclusion.Confirmed),
                Invalidated = g.Count(r => r.Conclusion == OosConclusion.Invalidated),
                ExcursionClosed = g.Count(r => r.Conclusion == OosConclusion.ExcursionClosed)
            }).ToListAsync(ct);

        var completed = await distinct
            .Where(r => r.Status == OosInvestigationStatus.Concluded && r.ConcludedAtUtc.HasValue)
            .Select(r => new { r.Code, r.GroupId, r.Label, r.OpenedAtUtc, End = r.ConcludedAtUtc })
            .ToListAsync(ct);

        var medians = new Dictionary<(string? Code, int? GroupId, string? Label), double?>();
        if (completed.Count != 0)
        {
            var workingWeekdays = await db.MonitoringSettings.AsNoTracking()
                .Select(s => (int?)s.WorkingWeekdays).FirstOrDefaultAsync(ct)
                ?? new MonitoringSettings().WorkingWeekdays;
            var holidays = (await db.LabCalendarDates.AsNoTracking().Select(d => d.Date).ToListAsync(ct)).ToHashSet();

            medians = completed.GroupBy(r => (r.Code, r.GroupId, r.Label))
                .ToDictionary(g => g.Key, g => Median(g.Select(r => WorkingDaysBetween(
                    DateOnly.FromDateTime(clock.ToLabLocal(r.OpenedAtUtc)),
                    DateOnly.FromDateTime(clock.ToLabLocal(r.End.GetValueOrDefault())),
                    workingWeekdays, holidays)).OrderBy(d => d).ToList()));
        }

        var groups = counts.Select(g =>
            {
                var (key, label) = DisplayGroup(normalized, g.Code, g.GroupId, g.Label);
                return new OosTrendGroupDto(key, label, g.Total, g.Open, g.Concluded, g.Confirmed, g.Invalidated,
                    g.ExcursionClosed, medians.GetValueOrDefault((g.Code, g.GroupId, g.Label)));
            })
            .OrderByDescending(g => g.TotalCount)
            .ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OosTrendReportDto(normalized, fromUtc, toUtc, groups);
    }

    private static (string Key, string Label) DisplayGroup(string groupBy, string? code, int? id, string? label)
    {
        var number = id?.ToString(CultureInfo.InvariantCulture);
        return groupBy switch
        {
            "product" when code == "item" => ($"item:{number}", label ?? ""),
            "product" => ($"cat:{(SampleCategory?)id}", label ?? ((SampleCategory)id.GetValueOrDefault()).ToString()),
            "test" => (code ?? "", label ?? ""),
            "conclusion" when id is int conclusion => (((OosConclusion)conclusion).ToString(), ReportVocabulary.Humanize(((OosConclusion)conclusion).ToString())),
            "conclusion" => ("Open", "Open"),
            _ => (number ?? "unassigned", label ?? "Unassigned")
        };
    }

    private IQueryable<TrendRow> QueryByProduct(IQueryable<OosInvestigation> query) =>
        from i in query
        join s in db.Samples.AsNoTracking() on i.SampleId equals s.Id
        select new TrendRow
        {
            InvestigationId = i.Id,
            Code = s.ItemId != null ? "item" : "cat",
            GroupId = s.ItemId ?? (int)s.Category,
            Label = s.Item != null ? s.Item.Name
                : s.WaterSamplingPoint != null ? s.WaterSamplingPoint.Code
                : s.Department != null ? s.Department.Name
                : s.Machine != null ? s.Machine.Name
                : s.Category.ToString(),
            Status = i.Status, Conclusion = i.Conclusion, OpenedAtUtc = i.OpenedAtUtc, ConcludedAtUtc = i.ConcludedAtUtc
        };

    private IQueryable<TrendRow> QueryByTest(IQueryable<OosInvestigation> query) =>
        from i in query
        join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
        join o in db.TestOrders.AsNoTracking() on r.TestOrderId equals o.Id
        select new TrendRow
        {
            InvestigationId = i.Id,
            Code = o.TestCode,
            GroupId = null,
            Label = db.TestDefinitions.Where(td => td.Code == o.TestCode).Select(td => td.DisplayName).FirstOrDefault() ?? o.TestCode,
            Status = i.Status, Conclusion = i.Conclusion, OpenedAtUtc = i.OpenedAtUtc, ConcludedAtUtc = i.ConcludedAtUtc
        };

    private IQueryable<TrendRow> QueryByAnalyst(IQueryable<OosInvestigation> query) =>
        from i in query
        join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
        join o in db.TestOrders.AsNoTracking() on r.TestOrderId equals o.Id
        where o.AssignedAnalystId != null
        join u in db.Users.AsNoTracking() on o.AssignedAnalystId equals u.Id
        select new TrendRow
        {
            InvestigationId = i.Id, Code = null, GroupId = u.Id, Label = u.FullName,
            Status = i.Status, Conclusion = i.Conclusion, OpenedAtUtc = i.OpenedAtUtc, ConcludedAtUtc = i.ConcludedAtUtc
        };

    private IQueryable<TrendRow> QueryByInstrument(IQueryable<OosInvestigation> query)
    {
        var involvedOrders = from i in query
                             join r in db.OosInvestigationResults.AsNoTracking() on i.Id equals r.InvestigationId
                             select new { Investigation = i, r.TestOrderId };
        var analyses = from row in involvedOrders
                       join a in db.TestAnalyses.AsNoTracking() on row.TestOrderId equals a.TestOrderId
                       where a.EquipmentId != null
                       select new { InvestigationId = row.Investigation.Id, a.EquipmentId };

        var incubations = from row in involvedOrders
                          join inc in db.Incubations.AsNoTracking() on row.TestOrderId equals inc.TestOrderId
                          where inc.IncubatorEquipmentId != null
                          select new { InvestigationId = row.Investigation.Id, EquipmentId = inc.IncubatorEquipmentId };

        var hplc = from row in involvedOrders
                   join hs in db.HplcRunSamples.AsNoTracking() on row.TestOrderId equals hs.TestOrderId
                   where hs.RemovedAt == null
                   join hr in db.HplcRuns.AsNoTracking() on hs.HplcRunId equals hr.Id
                   select new { InvestigationId = row.Investigation.Id, EquipmentId = (int?)hr.EquipmentId };

        var icp = from row in involvedOrders
                  join sample in db.IcpRunSamples.AsNoTracking() on row.TestOrderId equals sample.TestOrderId
                  where sample.RemovedAt == null
                  join run in db.IcpRuns.AsNoTracking() on sample.IcpRunId equals run.Id
                  select new { InvestigationId = row.Investigation.Id, EquipmentId = (int?)run.EquipmentId };

        return from link in analyses.Concat(incubations).Concat(hplc).Concat(icp)
               join i in db.OosInvestigations.AsNoTracking() on link.InvestigationId equals i.Id
               join e in db.Equipment.AsNoTracking() on link.EquipmentId equals e.Id
               select new TrendRow
               {
                   InvestigationId = i.Id, Code = null, GroupId = e.Id, Label = e.Name,
                   Status = i.Status, Conclusion = i.Conclusion, OpenedAtUtc = i.OpenedAtUtc, ConcludedAtUtc = i.ConcludedAtUtc
               };
    }

    private IQueryable<TrendRow> QueryByCause(IQueryable<OosInvestigation> query) =>
        from i in query
        let id = i.RootCauseCategoryId ?? i.PhaseOneCauseCategoryId
        select new TrendRow
        {
            InvestigationId = i.Id,
            Code = null,
            GroupId = id,
            Label = db.OosCauseCategories.Where(c => c.Id == id).Select(c => c.Name).FirstOrDefault(),
            Status = i.Status, Conclusion = i.Conclusion, OpenedAtUtc = i.OpenedAtUtc, ConcludedAtUtc = i.ConcludedAtUtc
        };

    private static IQueryable<TrendRow> QueryByConclusion(IQueryable<OosInvestigation> query) =>
        query.Select(i => new TrendRow
        {
            InvestigationId = i.Id, Code = null, GroupId = (int?)i.Conclusion, Label = null,
            Status = i.Status, Conclusion = i.Conclusion, OpenedAtUtc = i.OpenedAtUtc, ConcludedAtUtc = i.ConcludedAtUtc
        });

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
