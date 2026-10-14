using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Where the time goes. Population is tile 1's samples. Incubation happens inside the testing stage, so the
// stages overlap; they are four separate measurements, not slices that add up.
public sealed class StageTatKpi : IKpi
{
    private static readonly string[] Stages = { "Testing", "Incubation", "Review", "Approval" };

    private readonly IMicroLimsDbContext _db;
    public StageTatKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "stage-tat";
    public string Name => "Time in each stage";
    public string Unit => "days";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Flow;
    public int Order => 20;
    public string Definition => "Median and 90th percentile days spent in testing, incubation, review and approval for samples approved in the period. This is a breakdown: read the stage values, there is no single headline number. Incubation runs inside testing, so the stages overlap.";
    public string ClockEvents => "Testing: preparation to the latest section submit-for-review. Incubation: start to completion of each incubation window. Review: submit to reviewed (longest section). Approval: reviewed to approved (longest section).";
    public string Population => "Samples approved in the period (as for turnaround). A stage with no recorded clock for a sample contributes nothing for that sample.";
    public string Source => "Sample, SamplePreparation, SampleSectionSignoff, Incubation";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var samples = await KpiScope.ApprovedSamples(_db, q)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, ApprovedAt = s.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (samples.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var ids = samples.Select(s => s.Id).ToList();
        var prepared = await _db.SamplePreparations.AsNoTracking().Where(p => ids.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt, ct);
        var signoffs = (await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
                .Where(x => ids.Contains(x.SampleId))
                .Select(x => new { x.SampleId, x.SubmittedForReviewAt, x.ReviewedAt, x.ApprovedAt })
                .ToListAsync(ct))
            .ToLookup(x => x.SampleId);
        var incubations = await _db.Incubations.AsNoTracking().Where(KpiScope.IncubationIn(q.SectionIds))
            .Where(i => i.CompletedAt != null && ids.Contains(i.TestOrder!.SampleId))
            .Select(i => new { i.TestOrder!.SampleId, i.StartedAt, CompletedAt = i.CompletedAt!.Value })
            .ToListAsync(ct);

        var byStage = Stages.ToDictionary(s => s, _ => new List<(int SampleId, decimal Days)>());
        foreach (var s in samples)
        {
            var rows = signoffs[s.Id].ToList();
            var submitted = rows.Where(x => x.SubmittedForReviewAt != null).Select(x => x.SubmittedForReviewAt!.Value).ToList();
            if (prepared.TryGetValue(s.Id, out var preparedAt) && submitted.Count > 0)
                byStage["Testing"].Add((s.Id, KpiScope.Days(preparedAt, submitted.Max())));

            var review = rows.Where(x => x.SubmittedForReviewAt != null && x.ReviewedAt != null)
                .Select(x => KpiScope.Days(x.SubmittedForReviewAt!.Value, x.ReviewedAt!.Value)).ToList();
            if (review.Count > 0) byStage["Review"].Add((s.Id, review.Max()));

            var approval = rows.Where(x => x.ReviewedAt != null && x.ApprovedAt != null)
                .Select(x => KpiScope.Days(x.ReviewedAt!.Value, x.ApprovedAt!.Value)).ToList();
            if (approval.Count > 0) byStage["Approval"].Add((s.Id, approval.Max()));
        }
        foreach (var i in incubations)
            byStage["Incubation"].Add((i.SampleId, KpiScope.Days(i.StartedAt, i.CompletedAt)));

        var series = Stages.Select(stage =>
        {
            var days = byStage[stage].Select(x => x.Days).ToList();
            return new KpiSeriesPoint(stage, null, null, KpiScope.Percentile(days, 0.5m), days.Count, KpiScope.Percentile(days, 0.9m));
        }).ToList();

        var control = samples.ToDictionary(s => s.Id);
        var records = Stages
            .SelectMany(stage => byStage[stage].Select(x => (Stage: stage, x.SampleId, x.Days)))
            .OrderBy(x => x.SampleId).ThenBy(x => Array.IndexOf(Stages, x.Stage))
            .Select(x => new KpiRecordDto(x.SampleId, null, control[x.SampleId].ControlNumber, null, control[x.SampleId].Category.ToString(),
                null, control[x.SampleId].ApprovedAt, x.Days, "days", "Denominator", x.Stage))
            .ToList();

        // Value stays null: the answer is the per-stage series.
        return (new KpiCore(null, null, null, samples.Count, KpiCore.NoGaps, series, KpiCore.NoExtras, KpiCore.NoFlags), records);
    }
}
