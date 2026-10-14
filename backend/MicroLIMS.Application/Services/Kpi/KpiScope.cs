using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// What one KPI computed for ONE window, before the prior period, status and
// "too few records" flag are added by KpiScope.ComputeAsync.
public sealed record KpiCore(decimal? Value, decimal? Numerator, decimal? Denominator, int N,
    IReadOnlyList<KpiGap> Gaps, IReadOnlyList<KpiSeriesPoint> Series,
    IReadOnlyDictionary<string, decimal?> Extras, IReadOnlyList<string> Flags, bool IsCount = false)
{
    public static readonly IReadOnlyList<KpiGap> NoGaps = Array.Empty<KpiGap>();
    public static readonly IReadOnlyList<KpiSeriesPoint> NoSeries = Array.Empty<KpiSeriesPoint>();
    public static readonly IReadOnlyList<string> NoFlags = Array.Empty<string>();
    public static readonly IReadOnlyDictionary<string, decimal?> NoExtras = new Dictionary<string, decimal?>();

    // Nothing to measure. A count KPI still reports 0; every other KPI reports NoData with a null value.
    public static KpiCore Nothing(IReadOnlyList<KpiGap>? gaps = null, bool isCount = false) =>
        new(isCount ? 0m : null, null, null, 0, gaps ?? NoGaps, NoSeries, NoExtras, NoFlags, isCount);
}

public static class KpiScope
{
    // ---- section filters (null = unrestricted; an empty list matches nothing) ----

    public static Expression<Func<ResultRecord, bool>> ResultIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? r => true : r => sectionIds.Contains(r.TestOrder!.SectionId);

    // Media-challenge incubations have no TestOrder; they are never an analyst reading.
    public static Expression<Func<Incubation, bool>> IncubationIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? i => i.TestOrderId != null : i => i.TestOrder != null && sectionIds.Contains(i.TestOrder.SectionId);

    public static Expression<Func<SampleSectionSignoff, bool>> SignoffIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? x => true : x => sectionIds.Contains(x.SectionId);

    public static SampleCategory? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        if (Enum.TryParse<SampleCategory>(category.Trim(), true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new InvalidOperationException($"Unknown sample category '{category}'.");
    }

    // Sample-level filters. Location keeps the legacy meaning (item name or water point code).
    public static IQueryable<Sample> FilterSamples(IQueryable<Sample> samples, KpiQuery q)
    {
        samples = samples.Where(SectionReviewQueues.SampleIn(q.SectionIds));
        var category = ParseCategory(q.Category);
        if (category.HasValue)
        {
            var c = category.Value;
            samples = samples.Where(s => s.Category == c);
        }
        var location = q.Location;
        if (!string.IsNullOrWhiteSpace(location))
            samples = samples.Where(s => s.Item!.Name == location || s.WaterSamplingPoint!.Code == location);
        var testCode = q.TestCode;
        if (!string.IsNullOrWhiteSpace(testCode))
            samples = samples.Where(s => s.TestOrders.Any(t => t.TestCode == testCode));
        if (q.AnalystId.HasValue)
        {
            var a = q.AnalystId.Value;
            samples = samples.Where(s => s.TestOrders.Any(t => t.AssignedAnalystId == a));
        }
        return samples;
    }

    public static IQueryable<TestOrder> FilterOrders(IQueryable<TestOrder> orders, KpiQuery q)
    {
        orders = orders.Where(SectionReviewQueues.OrderIn(q.SectionIds));
        var category = ParseCategory(q.Category);
        if (category.HasValue)
        {
            var c = category.Value;
            orders = orders.Where(t => t.Sample!.Category == c);
        }
        var location = q.Location;
        if (!string.IsNullOrWhiteSpace(location))
            orders = orders.Where(t => t.Sample!.Item!.Name == location || t.Sample!.WaterSamplingPoint!.Code == location);
        var testCode = q.TestCode;
        if (!string.IsNullOrWhiteSpace(testCode))
            orders = orders.Where(t => t.TestCode == testCode);
        if (q.AnalystId.HasValue)
        {
            var a = q.AnalystId.Value;
            orders = orders.Where(t => t.AssignedAnalystId == a);
        }
        return orders;
    }

    // Samples with an Approve decision whose ApprovedAt falls in the window.
    // Retest clones (OriginSampleId set) are excluded: their ReceivedAt is the
    // spin-off time, so their "turnaround" is not the lab's turnaround.
    // A Reject also stamps ApprovedAt, which is why ApprovalDecision is checked.
    public static IQueryable<Sample> ApprovedSamples(IMicroLimsDbContext db, KpiQuery q)
    {
        var from = q.From;
        var to = q.To;
        return FilterSamples(db.Samples.AsNoTracking()
            .Where(s => s.ApprovalDecision == ApprovalDecision.Approve
                && s.ApprovedAt != null && s.ApprovedAt >= from && s.ApprovedAt < to
                && s.OriginSampleId == null), q);
    }

    // ---- maths ----

    public static decimal Days(DateTime from, DateTime to) => Math.Round((decimal)(to - from).TotalDays, 2);

    public static decimal? Rate(decimal numerator, decimal denominator) =>
        denominator == 0 ? null : Math.Round(numerator / denominator, 4);

    // Linear interpolation between closest ranks (Excel PERCENTILE.INC).
    public static decimal? Percentile(IReadOnlyCollection<decimal> values, decimal p)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(v => v).ToArray();
        var rank = p * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return Math.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo), 2);
    }

    public static IReadOnlyList<KpiSeriesPoint> MonthlySeries(IEnumerable<(DateTime At, decimal Num, decimal Den)> items) =>
        items.GroupBy(i => new DateTime(i.At.Year, i.At.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new KpiSeriesPoint(g.Key.ToString("yyyy-MM"), null, g.Key, Rate(g.Sum(x => x.Num), g.Sum(x => x.Den)), g.Count()))
            .ToList();

    public static IReadOnlyList<KpiGap> Gaps(params (string Code, string Message, int Count)[] gaps) =>
        gaps.Where(g => g.Count > 0).Select(g => new KpiGap(g.Code, g.Message, g.Count)).ToList();

    // ---- result assembly ----

    public static KpiResult ToResult(KpiCore cur, KpiCore? prior, string unit)
    {
        var status = cur.N == 0 && !cur.IsCount ? KpiStatus.NoData
            : cur.Gaps.Count > 0 ? KpiStatus.Partial
            : KpiStatus.Ok;
        // The spec shows a prior only when it has records of its own.
        var priorDto = prior is { N: > 0 } ? new KpiPrior(prior.Value, prior.N, prior.Numerator, prior.Denominator) : null;
        return new KpiResult(cur.Value, cur.Numerator, cur.Denominator, cur.N, priorDto, status,
            unit == "%" && cur.N < KpiConstants.MinSampleForRate, cur.Gaps, cur.Series, cur.Extras, cur.Flags);
    }

    public static async Task<KpiResult> ComputeAsync(KpiQuery q, string unit,
        Func<KpiQuery, CancellationToken, Task<KpiCore>> core, CancellationToken ct, bool withPrior = true)
    {
        var current = await core(q, ct);
        // ponytail: the prior pass also builds its record list; fine at lab scale, split Core from Records if a KPI gets slow.
        KpiCore? prior = withPrior ? await core(q.PriorPeriod(), ct) : null;
        return ToResult(current, prior, unit);
    }

    public static KpiRecordPageDto Page(string key, IReadOnlyList<KpiRecordDto> all, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return new KpiRecordPageDto(key, page, pageSize, all.Count, all.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    // ---- names ----

    public static async Task<Dictionary<int, string>> UserNamesAsync(IMicroLimsDbContext db, IEnumerable<int?> ids, CancellationToken ct)
    {
        var set = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (set.Count == 0) return new Dictionary<int, string>();
        return await db.Users.AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // D8: another analyst's name is only shown to someone who may see the team.
    public static string? AnalystLabel(KpiQuery q, int? analystId, IReadOnlyDictionary<int, string> names) =>
        analystId is { } id && (q.CanViewTeam || id == q.ViewerUserId) && names.TryGetValue(id, out var name) ? name : null;
}
