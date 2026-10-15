using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 7.
public sealed class IncubationReadsOnTimeKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public IncubationReadsOnTimeKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "incubation-reads-on-time";
    public string Name => "Incubation reads on time";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.HigherIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 7;
    public string Definition => "Share of completed incubation windows that were read no later than the expected reading time plus a 4 hour grace.";
    public string ClockEvents => "Expected: Incubation.ExpectedReadingAt (the end of the declared incubation window). Actual: Incubation.CompletedAt. On time when actual <= expected + 4 h.";
    public string Population => "Incubation windows of tests, completed in the period, within your sections. Windows with no expected time are listed as a gap. Media-challenge incubations are excluded. A transfer from stage 1 to stage 2 also completes a window and counts as a read (known limitation).";
    public string Source => "Incubation, TestOrder";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var from = q.From;
        var to = q.To;
        var orderIds = KpiScope.FilterOrders(_db.TestOrders.AsNoTracking(), q).Select(t => (int?)t.Id);
        var rows = await _db.Incubations.AsNoTracking().Where(KpiScope.IncubationIn(q.SectionIds))
            .Where(i => i.CompletedAt != null && i.CompletedAt >= from && i.CompletedAt < to && orderIds.Contains(i.TestOrderId))
            .Select(i => new
            {
                i.Id, TestOrderId = i.TestOrderId!.Value, i.TestOrder!.SampleId, i.TestOrder.TestCode, i.StepName,
                ControlNumber = i.TestOrder.Sample!.ControlNumber, i.TestOrder.Sample.Category,
                CompletedAt = i.CompletedAt!.Value, i.ExpectedReadingAt
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        int judged = 0, onTime = 0, noExpected = 0;
        var records = new List<KpiRecordDto>();
        var monthly = new List<(DateTime At, decimal Num, decimal Den)>();
        foreach (var r in rows.OrderBy(r => r.CompletedAt).ThenBy(r => r.Id))
        {
            KpiRecordDto Rec(string inclusion, string? note) =>
                new(r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, r.Category.ToString(), null, r.CompletedAt, null, null, inclusion, note);

            if (r.ExpectedReadingAt is null)
            {
                noExpected++;
                records.Add(Rec("Excluded", $"{r.StepName}: no expected reading time recorded"));
                continue;
            }
            var ok = r.CompletedAt <= r.ExpectedReadingAt.Value.AddHours(KpiConstants.IncubationGraceHours);
            judged++;
            if (ok) onTime++;
            monthly.Add((r.CompletedAt, ok ? 1m : 0m, 1m));
            records.Add(Rec(ok ? "Numerator" : "Denominator", ok ? null : $"{r.StepName}: read after the grace period"));
        }

        var gaps = KpiScope.Gaps(("NO_EXPECTED_READING", $"{noExpected} incubation windows without an expected reading time", noExpected));
        if (judged == 0) return (KpiCore.Nothing(gaps), records);
        return (new KpiCore(KpiScope.Rate(onTime, judged), onTime, judged, judged, gaps, KpiScope.MonthlySeries(monthly), KpiCore.NoExtras, KpiCore.NoFlags), records);
    }
}
