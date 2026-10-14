using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Work-unit analyst measures (spec 4.2). Everything is measured on work ASSIGNED to the analyst. There is no
// ranking: rows are alphabetical. A viewer without Kpi.ViewTeam only ever receives their own row (D8); that is
// enforced here, on the server, for every method.
public class AnalystWorkloadService
{
    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public AnalystWorkloadService(IMicroLimsDbContext db, WorkflowProfileService profiles, IUserSectionScopeService scope,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _profiles = profiles;
        _scope = scope;
        _time = timeProvider ?? TimeProvider.System;
    }

    private sealed record DeliveredRow(int TestOrderId, int SampleId, int SectionId, string TestCode, int AnalystId,
        string ControlNumber, SampleCategory Category, DateTime ApprovedAt, DateTime? SubmittedAt);

    private sealed record OpenRow(int TestOrderId, int SampleId, string TestCode, int? AnalystId,
        string ControlNumber, SampleCategory Category);

    private sealed record Loaded(List<DeliveredRow> Delivered, List<OpenRow> Open, WorkflowProfileResolver Resolver);

    private sealed class Acc
    {
        public int DeliveredTests, UnweightedTests, UnweightedOpenTests;
        public decimal DeliveredUnits, OpenUnits;
    }

    // Inclusive calendar dates in, half-open UTC window out: [from 00:00Z, to + 1 day 00:00Z).
    public async Task<KpiQuery> BuildQueryAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to, string? category,
        string? location, string? testCode, int? analystId, CancellationToken ct = default)
    {
        if (to < from) throw new InvalidOperationException("The end date must not be before the start date.");
        // Same blank handling as S2's KpiDashboardService.BuildQueryAsync: "" and whitespace mean "not set".
        static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        category = Blank(category); location = Blank(location); testCode = Blank(testCode);
        if (category is not null) KpiScope.ParseCategory(category);
        var sectionIds = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        return new KpiQuery(
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sectionIds, category, location, testCode, analystId, _time.GetUtcNow().UtcDateTime, userId, canViewTeam);
    }

    public async Task<AnalystWorkloadDto> GetAsync(KpiQuery q, CancellationToken ct)
    {
        var analystId = EffectiveAnalystId(q);
        var data = await LoadAsync(q, analystId, ct);
        var acc = Accumulate(data, q.Now);

        // Rows: active analysts in the viewer's scope, plus any assignee who delivered units in the period.
        var ids = new HashSet<int>(acc.Keys);
        var analysts = _db.Users.AsNoTracking().Where(u => u.IsActive && u.Role!.Type == RoleType.Analyst);
        if (analystId is int only) analysts = analysts.Where(u => u.Id == only);
        foreach (var id in await analysts.Select(u => u.Id).ToListAsync(ct))
            if (await KpiUserScope.InScopeAsync(_scope, id, q.SectionIds, ct)) ids.Add(id);
        var idList = ids.ToList();

        var names = await _db.Users.AsNoTracking().Where(u => idList.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var settings = await _db.KpiSettings.AsNoTracking().ToListAsync(ct);

        var filtered = !string.IsNullOrWhiteSpace(q.Category) || !string.IsNullOrWhiteSpace(q.Location) || !string.IsNullOrWhiteSpace(q.TestCode);
        var today = DateOnly.FromDateTime(q.Now);
        var periodFrom = DateOnly.FromDateTime(q.From);
        var periodEnd = DateOnly.FromDateTime(q.To).AddDays(-1);
        var capacityEnd = periodEnd < today ? periodEnd : today; // days that have not happened yet add no capacity
        var leaveFrom = periodFrom < today ? periodFrom : today;
        var leave = (await _db.AnalystAvailabilities.AsNoTracking()
                .Where(a => idList.Contains(a.UserId) && a.ToDate >= leaveFrom).ToListAsync(ct))
            .ToLookup(a => a.UserId);

        var rows = new List<AnalystWorkloadRowDto>();
        var accs = new List<Acc>();
        var usesDefaults = false;
        foreach (var id in idList)
        {
            var a = acc.TryGetValue(id, out var found) ? found : new Acc();
            accs.Add(a);

            var sections = await _scope.GetAccessibleSectionIdsAsync(id, ct);
            var setting = settings.Where(s => sections is null || sections.Contains(s.SectionId)).OrderBy(s => s.SectionId).FirstOrDefault();
            usesDefaults |= setting is null;
            var hours = setting?.HoursPerAnalystDay ?? KpiSettingDefaults.HoursPerAnalystDay;
            var mask = setting?.WorkingWeekdays ?? KpiSettingDefaults.WorkingWeekdays;
            var horizon = setting?.LoadHorizonDays ?? KpiSettingDefaults.LoadHorizonDays;
            var windows = leave[id].Select(l => (From: l.FromDate, To: l.ToDate, Fraction: l.FractionAvailable)).ToList();

            var capacity = CapacityCalculator.CapacityUnits(periodFrom, capacityEnd, hours, mask, windows);
            var forwardCapacity = CapacityCalculator.CapacityUnits(today, CapacityCalculator.HorizonEnd(today, horizon, mask), hours, mask, windows);
            rows.Add(ToRow(id, names.GetValueOrDefault(id, "(unknown user)"), a, capacity, forwardCapacity, filtered));
        }

        var unassigned = analystId is null ? data.Open.Count(o => o.AnalystId is null) : 0;
        var sorted = rows.OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var gaps = BuildGaps(accs, unassigned);
        // Capacity is the analyst's whole availability, so against a narrowed slice of work a ratio would read as under-load.
        if (filtered) gaps.Add(new KpiGap("CAPACITY_NOT_FILTERED", "Utilisation and load are shown for all work only; clear the category, location and test filters to see them", sorted.Count));
        return new AnalystWorkloadDto(q.From, q.To, usesDefaults, !q.CanViewTeam, unassigned, sorted, gaps);
    }

    private static AnalystWorkloadRowDto ToRow(int id, string name, Acc a, decimal capacity, decimal forwardCapacity, bool filtered)
    {
        // Unweighted work (e.g. profiles still Draft) contributes 0 units, which is "unknown", not "idle": show no ratio.
        var allDeliveredUnweighted = a.DeliveredTests > 0 && a.DeliveredUnits == 0 && a.UnweightedTests == a.DeliveredTests;
        var allOpenUnweighted = a.OpenUnits == 0 && a.UnweightedOpenTests > 0;
        decimal? utilisation = !filtered && !allDeliveredUnweighted && capacity > 0 ? a.DeliveredUnits / capacity : null;
        decimal? forwardLoad = !filtered && !allOpenUnweighted && forwardCapacity > 0 ? a.OpenUnits / forwardCapacity : null;
        // With no forward capacity at all, any open work is over capacity; a ratio would divide by zero.
        var over = !filtered && !allOpenUnweighted && (forwardCapacity > 0 ? a.OpenUnits / forwardCapacity > 1m : a.OpenUnits > 0);
        return new AnalystWorkloadRowDto(id, name, capacity, a.DeliveredUnits, utilisation, utilisation > 1m,
            a.OpenUnits, forwardCapacity, forwardLoad, over, null, null,
            a.DeliveredTests, a.UnweightedTests, a.UnweightedOpenTests, 0);
    }

    private static List<KpiGap> BuildGaps(IReadOnlyCollection<Acc> accs, int unassigned)
    {
        var gaps = new List<KpiGap>();
        var unweighted = accs.Sum(a => a.UnweightedTests + a.UnweightedOpenTests);
        if (unweighted > 0) gaps.Add(new KpiGap("UNWEIGHTED_TESTS", $"{unweighted} tests unweighted (no work-unit profile)", unweighted));
        if (unassigned > 0) gaps.Add(new KpiGap("UNASSIGNED_OPEN", $"{unassigned} open tests have no analyst assigned", unassigned));
        return gaps;
    }

    private static Dictionary<int, Acc> Accumulate(Loaded data, DateTime now)
    {
        var acc = new Dictionary<int, Acc>();
        Acc For(int id)
        {
            if (!acc.TryGetValue(id, out var a)) acc[id] = a = new Acc();
            return a;
        }

        foreach (var d in data.Delivered)
        {
            var a = For(d.AnalystId);
            a.DeliveredTests++;
            if (data.Resolver.Resolve(d.TestCode, d.ApprovedAt).WorkUnits is decimal units) a.DeliveredUnits += units;
            else a.UnweightedTests++;
        }
        foreach (var o in data.Open.Where(o => o.AnalystId != null))
        {
            var a = For(o.AnalystId!.Value);
            if (data.Resolver.Resolve(o.TestCode, now).WorkUnits is decimal units) a.OpenUnits += units;
            else a.UnweightedOpenTests++;
        }
        return acc;
    }

    private async Task<Loaded> LoadAsync(KpiQuery q, int? analystId, CancellationToken ct)
    {
        var resolver = await _profiles.LoadResolverAsync(ct);

        // Delivered: the test is approved and not superseded, and its own section's signoff approved it in the period.
        // The signoff (not ResultRecord.ApprovedAt) is used because ResultRecord.ApprovedAt is sample-level.
        var delivered = await Orders(q, analystId)
            .Where(t => t.Status == ApprovalStatus.Approved && !t.IsSuperseded && t.AssignedAnalystId != null)
            .Join(_db.SampleSectionSignoffs, t => new { t.SampleId, t.SectionId }, so => new { so.SampleId, so.SectionId }, (t, so) => new { t, so })
            .Where(x => x.so.Status == SectionSignoffStatus.Approved && x.so.ApprovalDecision == ApprovalDecision.Approve
                        && x.so.ApprovedAt >= q.From && x.so.ApprovedAt < q.To)
            .Select(x => new DeliveredRow(x.t.Id, x.t.SampleId, x.t.SectionId, x.t.TestCode, x.t.AssignedAnalystId!.Value,
                x.t.Sample!.ControlNumber, x.t.Sample.Category, x.so.ApprovedAt!.Value, x.so.SubmittedForReviewAt))
            .ToListAsync(ct);

        var open = await Orders(q, analystId)
            .Where(t => (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress) && !t.IsSuperseded)
            .Select(t => new OpenRow(t.Id, t.SampleId, t.TestCode, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category))
            .ToListAsync(ct);

        return new Loaded(delivered, open, resolver);
    }

    // D8: without Kpi.ViewTeam the only analyst a caller may look at is themselves.
    private static int? EffectiveAnalystId(KpiQuery q)
    {
        if (q.CanViewTeam) return q.AnalystId;
        if (q.AnalystId is int asked && asked != q.ViewerUserId)
            throw new UnauthorizedAccessException("You may only view your own workload figures.");
        return q.ViewerUserId;
    }

    // Reuses S2's shared filter (section scope, category, location, test code, analyst); the analyst is overridden by the D8 rule.
    private IQueryable<TestOrder> Orders(KpiQuery q, int? analystId) =>
        KpiScope.FilterOrders(_db.TestOrders.AsNoTracking(), q with { AnalystId = analystId });
}
