using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 8. The adverse-trend thresholds are constants (KpiConstants), not stored configuration.
// ponytail: make them per-section settings if a site needs different limits.
public sealed class EmExcursionRateKpi : IKpi
{
    private static readonly ResultLevel[] Leveled = { ResultLevel.WithinLimit, ResultLevel.AlertLevel, ResultLevel.ActionLevel };

    private readonly IMicroLimsDbContext _db;
    public EmExcursionRateKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "em-excursion-rate";
    public string Name => "EM excursion rate";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 8;
    public string Definition => "Environmental monitoring results at alert or action level, divided by all environmental monitoring results that have a level. The alert-level and action-level rates are also shown separately. A trend flag appears when the alert-level rate is above 2.5% or the action-level rate is above 0.15%, with at least 5 results.";
    public string ClockEvents => "The result entry time (ResultRecord.ResultEnteredAt) places a result in the period.";
    public string Population => "Environmental monitoring results at within-limit, alert or action level in your sections. Results without a level (detected/absent, limits not configured) and results of retest samples that re-count a location are excluded. The location filter matches the monitored location (room or point).";
    public string Source => "ResultRecord";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var category = KpiScope.ParseCategory(q.Category);
        if (category.HasValue && category.Value != SampleCategory.EnvironmentalMonitoring)
            return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var from = q.From;
        var to = q.To;
        var leveled = Leveled;
        var query = _db.ResultRecords.AsNoTracking().Where(KpiScope.ResultIn(q.SectionIds))
            .Where(r => r.Category == SampleCategory.EnvironmentalMonitoring && leveled.Contains(r.ResultLevel)
                && r.ResultEnteredAt >= from && r.ResultEnteredAt < to)
            .Where(r => !_db.Samples.Any(s => s.Id == r.SampleId && s.OriginSampleId != null));
        var location = q.Location;
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(r => r.SubjectName == location);
        var testCode = q.TestCode;
        if (!string.IsNullOrWhiteSpace(testCode)) query = query.Where(r => r.TestCode == testCode);
        if (q.AnalystId.HasValue)
        {
            var analystId = q.AnalystId.Value;
            query = query.Where(r => r.TestOrder!.AssignedAnalystId == analystId);
        }

        var rows = await query
            .Select(r => new { r.Id, r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, r.SubjectName, r.ResultLevel, r.ResultEnteredAt, r.NumericValue, r.Unit })
            .ToListAsync(ct);
        if (rows.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var alert = rows.Count(r => r.ResultLevel == ResultLevel.AlertLevel);
        var action = rows.Count(r => r.ResultLevel == ResultLevel.ActionLevel);
        var total = rows.Count;
        var alertRate = KpiScope.Rate(alert, total);
        var actionRate = KpiScope.Rate(action, total);

        var flags = new List<string>();
        if (total >= KpiConstants.MinSampleForRate
            && (alertRate > KpiConstants.EmAlertRateAdverse || actionRate > KpiConstants.EmActionRateAdverse))
            flags.Add("AdverseTrend");

        var series = rows
            .GroupBy(r => new DateTime(r.ResultEnteredAt.Year, r.ResultEnteredAt.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new KpiSeriesPoint(g.Key.ToString("yyyy-MM"), null, g.Key,
                KpiScope.Rate(g.Count(r => r.ResultLevel != ResultLevel.WithinLimit), g.Count()), g.Count(),
                KpiScope.Rate(g.Count(r => r.ResultLevel == ResultLevel.ActionLevel), g.Count())))
            .ToList();

        var core = new KpiCore(KpiScope.Rate(alert + action, total), alert + action, total, total, KpiCore.NoGaps, series,
            new Dictionary<string, decimal?> { ["alertRate"] = alertRate, ["actionRate"] = actionRate }, flags);
        var records = rows.OrderBy(r => r.ResultEnteredAt).ThenBy(r => r.Id)
            .Select(r => new KpiRecordDto(r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, SampleCategory.EnvironmentalMonitoring.ToString(), null,
                r.ResultEnteredAt, r.NumericValue, r.Unit, r.ResultLevel == ResultLevel.WithinLimit ? "Denominator" : "Numerator",
                $"{r.SubjectName}: {r.ResultLevel}"))
            .ToList();
        return (core, records);
    }
}
