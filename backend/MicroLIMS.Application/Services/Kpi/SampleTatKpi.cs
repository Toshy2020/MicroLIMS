using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 1. End event is Sample.ApprovedAt, not ReviewWorkflowEvent: that table is shared with Media and
// Cryovial, holds one event per section of a multi-lab sample, and its Timestamp ignores TimeProvider.
public sealed class SampleTatKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public SampleTatKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "sample-tat";
    public string Name => "Sample turnaround time";
    public string Unit => "days";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 1;
    public string Definition => "Median calendar days from receipt to final approval, with the 90th percentile alongside. Percentiles are linearly interpolated.";
    public string ClockEvents => "Start: Sample.ReceivedAt. End: Sample.ApprovedAt (decision Approve).";
    public string Population => "Samples approved in the period, excluding retest samples created from an earlier sample, within your sections and the chosen filters. A sample tested by several laboratories counts as approved when the last laboratory approves, so a single-laboratory view still shows the time to that final approval. The record list shows every counted sample.";
    public string Source => "Sample";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var samples = await KpiScope.ApprovedSamples(_db, q)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, s.ReceivedAt, ApprovedAt = s.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (samples.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var rows = samples.OrderBy(s => s.ApprovedAt).ThenBy(s => s.Id)
            .Select(s => (Sample: s, Days: KpiScope.Days(s.ReceivedAt, s.ApprovedAt))).ToList();
        var days = rows.Select(r => r.Days).ToList();
        var core = new KpiCore(KpiScope.Percentile(days, 0.5m), null, null, days.Count, KpiCore.NoGaps, KpiCore.NoSeries,
            new Dictionary<string, decimal?> { ["p90"] = KpiScope.Percentile(days, 0.9m) }, KpiCore.NoFlags);
        var records = rows.Select(r => new KpiRecordDto(r.Sample.Id, null, r.Sample.ControlNumber, null, r.Sample.Category.ToString(),
            null, r.Sample.ApprovedAt, r.Days, "days", "Denominator", null)).ToList();
        return (core, records);
    }
}
