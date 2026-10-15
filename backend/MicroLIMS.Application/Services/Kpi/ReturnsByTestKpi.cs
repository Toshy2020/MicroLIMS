using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// TestReturnEvent only has a free-text reason, so returns cannot be grouped by cause. They are grouped by test
// code (D6: never by person); the reason text is shown in the record list only.
public sealed class ReturnsByTestKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public ReturnsByTestKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "returns-by-test";
    public string Name => "Returns by test";
    public string Unit => "tests";
    public KpiDirection Direction => KpiDirection.Neutral;
    public KpiGroup Group => KpiGroup.Quality;
    public int Order => 30;
    public string Definition => "Number of times a test was returned to the analyst in the period, grouped by test code in alphabetical order. A return is a reviewer sending a result back; it is a process signal and is never grouped by person.";
    public string ClockEvents => "The time of the return (TestReturnEvent.ReturnedAt) places it in the period.";
    public string Population => "Return events for tests in your sections and the chosen filters. The reviewer's reason is shown in the record list only.";
    public string Source => "TestReturnEvent, TestOrder";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var from = q.From;
        var to = q.To;
        var orderIds = KpiScope.FilterOrders(_db.TestOrders.AsNoTracking(), q).Select(t => t.Id);
        var rows = await _db.TestReturnEvents.AsNoTracking()
            .Where(e => e.ReturnedAt >= from && e.ReturnedAt < to && orderIds.Contains(e.TestOrderId))
            .Select(e => new
            {
                e.Id, e.TestOrderId, e.TestOrder!.SampleId, e.TestOrder.TestCode, e.Reason, e.ReturnedAt, e.AssignedAnalystId,
                ControlNumber = e.TestOrder.Sample!.ControlNumber, e.TestOrder.Sample.Category
            })
            .ToListAsync(ct);
        var names = await KpiScope.UserNamesAsync(_db, rows.Select(r => r.AssignedAnalystId), ct);

        var series = rows.GroupBy(r => r.TestCode).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KpiSeriesPoint(g.Key, null, null, g.Count(), g.Count())).ToList();
        var core = new KpiCore(rows.Count, null, null, rows.Count, KpiCore.NoGaps, series, KpiCore.NoExtras, KpiCore.NoFlags, IsCount: true);
        var records = rows.OrderBy(r => r.ReturnedAt).ThenBy(r => r.Id)
            .Select(r => new KpiRecordDto(r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, r.Category.ToString(),
                KpiScope.AnalystLabel(q, r.AssignedAnalystId, names), r.ReturnedAt, 1m, "returns", "Denominator", r.Reason))
            .ToList();
        return (core, records);
    }
}
