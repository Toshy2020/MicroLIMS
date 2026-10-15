using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Spec 4.4: age is shown relative to each item's own target, because targets differ by workflow.
public sealed class BacklogAgingKpi : IKpi
{
    private static readonly string[] Stages = { "Testing", "Incubation", "In Review", "In Approval" };
    private static readonly string[] Buckets = { "within50", "50to100", "over100" };

    // Review and approval are held to the 24 h SLA.
    private const decimal GateTargetDays = 1m;

    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;

    public BacklogAgingKpi(IMicroLimsDbContext db, WorkflowProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string Key => "backlog-aging";
    public string Name => "Backlog aging";
    public string Unit => "tests";
    public KpiDirection Direction => KpiDirection.Neutral;
    public KpiGroup Group => KpiGroup.Flow;
    public int Order => 21;
    public string Definition => "Open work grouped by how much of its own target it has used: within 50%, 50 to 100%, or over 100%. Targets differ by workflow, so age in days alone would mislead.";
    public string ClockEvents => "Testing and incubation: now minus the preparation time (the receipt time if there is none), against the test's analyst-stage target (7 days where none is configured). In review: now minus submit-for-review, against 24 h. In approval: now minus review completed, against 24 h.";
    public string Population => "Open tests (waiting, running, incubating) and the samples currently waiting for review or approval in your sections. A snapshot at the time of the request.";
    public string Source => "TestOrder, SamplePreparation, Sample, SampleSectionSignoff, workflow profile versions";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private static decimal Ratio(TimeSpan age, decimal targetDays) => Math.Max(0m, (decimal)age.TotalDays) / targetDays;

    private static string Bucket(decimal ratio) => ratio <= 0.5m ? "within50" : ratio <= 1m ? "50to100" : "over100";

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var resolver = await _profiles.LoadResolverAsync(ct);
        var items = new List<(string Stage, string Bucket, KpiRecordDto Record)>();
        var defaultTests = 0;

        var tests = await KpiScope.FilterOrders(_db.TestOrders.AsNoTracking()
                .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)), q)
            .Select(t => new { t.Id, t.SampleId, t.TestCode, t.CurrentStep, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category, t.Sample.ReceivedAt })
            .ToListAsync(ct);
        var sampleIds = tests.Select(t => t.SampleId).Distinct().ToList();
        var prepared = await _db.SamplePreparations.AsNoTracking().Where(p => sampleIds.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt, ct);
        var names = await KpiScope.UserNamesAsync(_db, tests.Select(t => t.AssignedAnalystId), ct);

        foreach (var t in tests.OrderBy(t => t.Id))
        {
            var anchor = prepared.TryGetValue(t.SampleId, out var preparedAt) ? preparedAt : t.ReceivedAt;
            var target = resolver.Resolve(t.TestCode, q.Now).TargetAnalystDays;
            if (target is null) defaultTests++;
            var ratio = Ratio(q.Now - anchor, target ?? KpiConstants.DefaultTargetDays);
            var stage = t.CurrentStep == WorkflowStep.Incubating ? "Incubation" : "Testing";
            var bucket = Bucket(ratio);
            items.Add((stage, bucket, new KpiRecordDto(t.SampleId, t.Id, t.ControlNumber, t.TestCode, t.Category.ToString(),
                KpiScope.AnalystLabel(q, t.AssignedAnalystId, names), anchor, Math.Round(ratio, 2), "x target", "Denominator", $"{stage}: {bucket}")));
        }

        await AddGateAsync(q, SectionSignoffStatus.UnderReview, "In Review", items, ct);
        await AddGateAsync(q, SectionSignoffStatus.UnderApproval, "In Approval", items, ct);

        var series = new List<KpiSeriesPoint>();
        foreach (var stage in Stages)
            foreach (var bucket in Buckets)
            {
                var count = items.Count(i => i.Stage == stage && i.Bucket == bucket);
                series.Add(new KpiSeriesPoint(bucket, stage, null, count, count));
            }

        var gaps = KpiScope.Gaps(("DEFAULT_TARGET", $"{defaultTests} tests on the default 7-day target", defaultTests));
        var core = new KpiCore(items.Count, null, null, items.Count, gaps, series, KpiCore.NoExtras, KpiCore.NoFlags, IsCount: true);
        return (core, items.Select(i => i.Record).ToList());
    }

    // Sample-level gates reuse the review/approval queue definition so this view and the queues agree.
    private async Task AddGateAsync(KpiQuery q, SectionSignoffStatus status, string stage,
        List<(string Stage, string Bucket, KpiRecordDto Record)> items, CancellationToken ct)
    {
        var samples = await SectionReviewQueues.Candidates(KpiScope.FilterSamples(_db.Samples.AsNoTracking(), q), status, q.SectionIds)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, s.ReceivedAt })
            .ToListAsync(ct);
        if (samples.Count == 0) return;

        var ids = samples.Select(s => s.Id).ToList();
        var rows = (await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
                .Where(x => ids.Contains(x.SampleId) && x.Status == status)
                .Select(x => new { x.SampleId, x.SubmittedForReviewAt, x.ReviewedAt })
                .ToListAsync(ct))
            .ToLookup(x => x.SampleId);

        foreach (var s in samples.OrderBy(s => s.Id))
        {
            var anchors = rows[s.Id]
                .Select(x => status == SectionSignoffStatus.UnderReview ? x.SubmittedForReviewAt : x.ReviewedAt)
                .Where(a => a.HasValue).Select(a => a!.Value).ToList();
            var anchor = anchors.Count > 0 ? anchors.Min() : s.ReceivedAt;
            var ratio = Ratio(q.Now - anchor, GateTargetDays);
            var bucket = Bucket(ratio);
            items.Add((stage, bucket, new KpiRecordDto(s.Id, null, s.ControlNumber, null, s.Category.ToString(), null,
                anchor, Math.Round(ratio, 2), "x target", "Denominator", $"{stage}: {bucket}")));
        }
    }
}
