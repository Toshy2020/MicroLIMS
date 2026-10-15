using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 3. ResultRecord.ApprovedAt is sample-level (null for a lab that closed earlier), so approval time comes
// from the section signoff, and tests are counted as TestOrder rows (a ResultRecord can have many rows per test).
public sealed class RightFirstTimeKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;

    public RightFirstTimeKpi(IMicroLimsDbContext db, WorkflowProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string Key => "right-first-time";
    public string Name => "Right first time";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.HigherIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 3;
    public string Definition => "Work units of approved tests that were never returned to the analyst, divided by the work units of all approved tests. Tests without a work-unit profile are left out and listed as a gap; they are never given a default weight.";
    public string ClockEvents => "A test is counted in the period in which its section's sign-off was approved. Its work units are those in force on that date. A return is any TestReturnEvent for the test.";
    public string Population => "Non-superseded tests with status Approved whose section sign-off was approved in the period. Pathogen observation tests are excluded because a send-back there is not recorded as a return.";
    public string Source => "TestOrder, SampleSectionSignoff, TestReturnEvent, workflow profile versions";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var from = q.From;
        var to = q.To;
        var signoffs = await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
            .Where(x => x.Status == SectionSignoffStatus.Approved && x.ApprovedAt != null && x.ApprovedAt >= from && x.ApprovedAt < to)
            .Select(x => new { x.SampleId, x.SectionId, ApprovedAt = x.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (signoffs.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var sampleIds = signoffs.Select(s => s.SampleId).Distinct().ToList();
        var orders = await KpiScope.FilterOrders(_db.TestOrders.AsNoTracking()
                .Where(t => sampleIds.Contains(t.SampleId) && t.Status == ApprovalStatus.Approved && !t.IsSuperseded), q)
            .Select(t => new { t.Id, t.SampleId, t.SectionId, t.TestCode, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category })
            .ToListAsync(ct);

        var approvedAt = signoffs.GroupBy(s => (s.SampleId, s.SectionId)).ToDictionary(g => g.Key, g => g.Max(s => s.ApprovedAt));
        var orderIds = orders.Select(o => o.Id).ToList();
        var returned = (await _db.TestReturnEvents.AsNoTracking().Where(e => orderIds.Contains(e.TestOrderId))
            .Select(e => e.TestOrderId).Distinct().ToListAsync(ct)).ToHashSet();
        var codes = orders.Select(o => o.TestCode).Distinct().ToList();
        var types = await _db.TestDefinitions.AsNoTracking().Where(d => codes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, d => d.WorkflowType, ct);
        var names = await KpiScope.UserNamesAsync(_db, orders.Select(o => o.AssignedAnalystId), ct);
        var resolver = await _profiles.LoadResolverAsync(ct);

        decimal num = 0m, den = 0m;
        int counted = 0, observation = 0, unweighted = 0;
        var records = new List<KpiRecordDto>();
        var monthly = new List<(DateTime At, decimal Num, decimal Den)>();

        foreach (var o in orders.OrderBy(o => o.Id))
        {
            if (!approvedAt.TryGetValue((o.SampleId, o.SectionId), out var at)) continue;   // no matching approved sign-off
            KpiRecordDto Rec(string inclusion, decimal? units, string? note) =>
                new(o.SampleId, o.Id, o.ControlNumber, o.TestCode, o.Category.ToString(),
                    KpiScope.AnalystLabel(q, o.AssignedAnalystId, names), at, units, "units", inclusion, note);

            if (types.TryGetValue(o.TestCode, out var type) && type == WorkflowType.Observation)
            {
                observation++;
                records.Add(Rec("Excluded", null, "Observation test: send-backs are not recorded as returns"));
                continue;
            }
            var units = resolver.Resolve(o.TestCode, at).WorkUnits;
            if (units is null)
            {
                unweighted++;
                records.Add(Rec("Excluded", null, "No work-unit profile in force on the approval date"));
                continue;
            }

            var wasReturned = returned.Contains(o.Id);
            counted++;
            den += units.Value;
            if (!wasReturned) num += units.Value;
            monthly.Add((at, wasReturned ? 0m : units.Value, units.Value));
            records.Add(Rec(wasReturned ? "Denominator" : "Numerator", units, wasReturned ? "Returned to the analyst at least once" : null));
        }

        var gaps = KpiScope.Gaps(
            ("UNWEIGHTED_TESTS", $"{unweighted} tests unweighted", unweighted),
            ("OBSERVATION_NOT_TRACKED", $"{observation} observation tests not tracked for returns", observation));
        if (counted == 0) return (KpiCore.Nothing(gaps), records);
        return (new KpiCore(KpiScope.Rate(num, den), num, den, counted, gaps, KpiScope.MonthlySeries(monthly), KpiCore.NoExtras, KpiCore.NoFlags), records);
    }
}
