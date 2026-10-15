using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 6. A snapshot at query.Now: the period does not apply and there is no prior (history is not reconstructable).
// Tests waiting for review or approval are sample-level and appear in backlog-aging, not here.
public sealed class WipBacklogKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public WipBacklogKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "wip-backlog";
    public string Name => "WIP backlog";
    public string Unit => "tests";
    public KpiDirection Direction => KpiDirection.Neutral;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 6;
    public string Definition => "Tests that are open right now: waiting, running or incubating. Tests that have a result and wait for review or approval are shown in the backlog aging section instead.";
    public string ClockEvents => "A snapshot at the time of the request; the chosen period does not apply.";
    public string Population => "Test orders with status Pending or InProgress that are not superseded, within your sections and the chosen filters.";
    public string Source => "TestOrder";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var tests = await KpiScope.FilterOrders(_db.TestOrders.AsNoTracking()
                .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)), q)
            .Select(t => new { t.Id, t.SampleId, t.TestCode, t.CurrentStep, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category })
            .ToListAsync(ct);
        var names = await KpiScope.UserNamesAsync(_db, tests.Select(t => t.AssignedAnalystId), ct);

        var series = tests.GroupBy(t => t.CurrentStep).OrderBy(g => g.Key)
            .Select(g => new KpiSeriesPoint(g.Key.ToString(), null, null, g.Count(), g.Count())).ToList();
        var core = new KpiCore(tests.Count, null, null, tests.Count, KpiCore.NoGaps, series, KpiCore.NoExtras, KpiCore.NoFlags, IsCount: true);
        var records = tests.OrderBy(t => t.SampleId).ThenBy(t => t.Id)
            .Select(t => new KpiRecordDto(t.SampleId, t.Id, t.ControlNumber, t.TestCode, t.Category.ToString(),
                KpiScope.AnalystLabel(q, t.AssignedAnalystId, names), null, null, null, "Denominator", t.CurrentStep.ToString()))
            .ToList();
        return (core, records);
    }
}
