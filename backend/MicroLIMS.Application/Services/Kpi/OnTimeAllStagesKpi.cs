using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 2. Replaces the old max-across-sections stage window: each section is judged on its own timestamps.
public sealed class OnTimeAllStagesKpi : IKpi
{
    // Review and approval SLAs, in calendar hours.
    private static readonly TimeSpan StageLimit = TimeSpan.FromHours(24);

    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;

    public OnTimeAllStagesKpi(IMicroLimsDbContext db, WorkflowProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string Key => "on-time-all-stages";
    public string Name => "On time, all stages";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.HigherIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 2;
    public string Definition => "Share of approved samples that met every stage target: analyst stage within the longest per-workflow target of the sample's assigned tests (7 days where no target is configured), then review within 24 h and approval within 24 h, for every section you can see.";
    public string ClockEvents => "Analyst stage: SamplePreparation.PreparedAt to the section's submit-for-review time. Review: submit to reviewed. Approval: reviewed to approved. Review and approval are calendar hours, all limits inclusive.";
    public string Population => "Samples approved in the period (as for turnaround). Samples with no preparation record (finished-product physicochemical, environmental monitoring, retest clones) cannot be judged and are reported as a gap.";
    public string Source => "Sample, SampleSectionSignoff, SamplePreparation, TestOrder, workflow profile versions";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

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
                .Where(x => ids.Contains(x.SampleId) && x.SubmittedForReviewAt != null)
                .Select(x => new { x.SampleId, Submitted = x.SubmittedForReviewAt!.Value, x.ReviewedAt, x.ApprovedAt })
                .ToListAsync(ct))
            .ToLookup(x => x.SampleId);
        var codes = (await _db.TestOrders.AsNoTracking().Where(SectionReviewQueues.OrderIn(q.SectionIds))
                .Where(t => ids.Contains(t.SampleId) && !t.IsSuperseded && t.AssignedAnalystId != null)
                .Select(t => new { t.SampleId, t.TestCode })
                .ToListAsync(ct))
            .ToLookup(t => t.SampleId, t => t.TestCode);
        var resolver = await _profiles.LoadResolverAsync(ct);

        int noAssignment = 0, noTimeline = 0, defaultTests = 0, evaluated = 0, onTime = 0;
        var records = new List<KpiRecordDto>();
        var monthly = new List<(DateTime At, decimal Num, decimal Den)>();

        foreach (var s in samples.OrderBy(s => s.ApprovedAt).ThenBy(s => s.Id))
        {
            KpiRecordDto Rec(string inclusion, string? note) =>
                new(s.Id, null, s.ControlNumber, null, s.Category.ToString(), null, s.ApprovedAt, null, null, inclusion, note);

            if (!prepared.TryGetValue(s.Id, out var preparedAt))
            {
                noAssignment++;
                records.Add(Rec("Excluded", "No assignment time (no preparation record)"));
                continue;
            }
            var sectionRows = signoffs[s.Id].ToList();
            if (sectionRows.Count == 0)
            {
                noTimeline++;
                records.Add(Rec("Excluded", "No review timeline recorded"));
                continue;
            }

            // The profile in force when the sample was approved (D4), not today's.
            var target = AnalystStageTarget.DaysFor(codes[s.Id], s.ApprovedAt, resolver);
            defaultTests += target.TestsOnDefault;
            var deadline = preparedAt.AddDays((double)target.Days);

            var ok = sectionRows.All(x =>
                x.Submitted <= deadline
                && (x.ReviewedAt is null || x.ReviewedAt.Value - x.Submitted <= StageLimit)
                && (x.ReviewedAt is null || x.ApprovedAt is null || x.ApprovedAt.Value - x.ReviewedAt.Value <= StageLimit));
            evaluated++;
            if (ok) onTime++;
            monthly.Add((s.ApprovedAt, ok ? 1m : 0m, 1m));
            records.Add(Rec(ok ? "Numerator" : "Denominator", ok ? null : "At least one stage exceeded its limit"));
        }

        var gaps = KpiScope.Gaps(
            ("NO_ASSIGNMENT_TIME", $"{noAssignment} samples without an assignment time", noAssignment),
            ("INCOMPLETE_TIMELINE", $"{noTimeline} samples without a review timeline", noTimeline),
            ("DEFAULT_TARGET", $"{defaultTests} tests on the default 7-day target", defaultTests));

        if (evaluated == 0) return (KpiCore.Nothing(gaps), records);
        var core = new KpiCore(KpiScope.Rate(onTime, evaluated), onTime, evaluated, evaluated, gaps,
            KpiScope.MonthlySeries(monthly), KpiCore.NoExtras, KpiCore.NoFlags);
        return (core, records);
    }
}
