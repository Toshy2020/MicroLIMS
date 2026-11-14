using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Open incubations on my tests (assigned to me, or started by me), split by the one ready-to-read rule
// (SampleWorkflowQueues.IsIncubationReadyAt). Ready -> "readyToRead"; not ready but expected before the
// laboratory day ends -> "endingToday". An incubation whose test was returned to me is listed as returned instead.
public sealed class IncubationSource : IMyWorkSource
{
    private readonly IMicroLimsDbContext _db;

    public IncubationSource(IMicroLimsDbContext db) => _db = db;

    public string Key => "incubations";

    public bool AppliesTo(MyWorkContext ctx) => true;

    private sealed record Row(int Id, int TestOrderId, int SampleId, string SampleRef, string TestCode, string? DisplayName,
        string StepName, DateTime? Expected, DateTime? End, int? OverriddenBy, DateTime? OverriddenAt,
        int SectionId, string SectionName, string Lab);

    private IQueryable<Incubation> Mine(MyWorkContext ctx)
    {
        var userId = ctx.UserId;
        var returned = AnalystQueries.MyPendingReturns(_db, userId);
        return _db.Incubations.AsNoTracking()
            .Where(SampleWorkflowQueues.IsOpenIncubationOnActiveTest)
            .Where(i => i.TestOrder!.AssignedAnalystId == userId || i.StartedByUserId == userId)
            .Where(i => !returned.Any(r => r.TestOrderId == i.TestOrderId))
            .InSections(ctx, i => i.TestOrder!.SectionId);
    }

    private IQueryable<Incubation> Ready(MyWorkContext ctx) =>
        Mine(ctx).Where(SampleWorkflowQueues.IsIncubationReadyAt(ctx.NowUtc));

    private IQueryable<Incubation> Ending(MyWorkContext ctx)
    {
        var endOfDay = ctx.EndOfLabDayUtc;
        return Mine(ctx)
            .Where(AnalystQueries.Not(SampleWorkflowQueues.IsIncubationReadyAt(ctx.NowUtc)))
            .Where(i => (i.ExpectedReadingAt ?? i.IncubationEndUtc) != null && (i.ExpectedReadingAt ?? i.IncubationEndUtc) <= endOfDay);
    }

    // Ready incubations that have waited longer than the configured hours (0 = never):
    // the earliest moment one became ready is before now minus the hours.
    private IQueryable<Incubation> ReadyAndOverdue(MyWorkContext ctx)
    {
        var threshold = ctx.NowUtc.AddHours(-ctx.ReadOverdueHours);
        return Ready(ctx).Where(i =>
            (i.ExpectedReadingAt != null && i.ExpectedReadingAt < threshold)
            || (i.IncubationEndUtc != null && i.IncubationEndUtc < threshold)
            || (i.MinimumDurationOverriddenByUserId != null && i.MinimumDurationOverriddenAt != null && i.MinimumDurationOverriddenAt < threshold));
    }

    private async Task<(int Ready, int Overdue, int Ending)> CountsAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var ready = await Ready(ctx).CountAsync(ct);
        var overdue = ctx.ReadOverdueHours > 0 && ready > 0 ? await ReadyAndOverdue(ctx).CountAsync(ct) : 0;
        var ending = await Ending(ctx).CountAsync(ct);
        return (ready, overdue, ending);
    }

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var (ready, _, ending) = await CountsAsync(ctx, ct);
        var items = new List<MyWorkItem>();

        if (ready > 0)
        {
            // Overdue ones first (when the setting is on), then the earliest scheduled.
            IOrderedQueryable<Incubation> ordered;
            if (ctx.ReadOverdueHours > 0)
            {
                var overdue = ReadyAndOverdue(ctx);
                ordered = Ready(ctx).OrderBy(i => overdue.Any(o => o.Id == i.Id) ? 0 : 1);
            }
            else
            {
                ordered = Ready(ctx).OrderBy(i => 0);
            }

            var rows = await Project(ordered
                    .ThenBy(i => i.ExpectedReadingAt ?? i.IncubationEndUtc ?? i.MinimumDurationOverriddenAt)
                    .ThenBy(i => i.Id))
                .Take(take).ToListAsync(ct);
            items.AddRange(rows.Select(r => ToItem(ctx, r, ready: true)));
        }

        if (ending > 0)
        {
            var rows = await Project(Ending(ctx).OrderBy(i => i.ExpectedReadingAt ?? i.IncubationEndUtc).ThenBy(i => i.Id))
                .Take(take).ToListAsync(ct);
            items.AddRange(rows.Select(r => ToItem(ctx, r, ready: false)));
        }

        return new MyWorkSourceResult(items,
            new Dictionary<string, int> { ["readyToRead"] = ready, ["endingToday"] = ending },
            new Dictionary<string, int>());
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var (ready, overdue, ending) = await CountsAsync(ctx, ct);
        return new Dictionary<MyWorkUrgencyLevel, int>
        {
            [MyWorkUrgencyLevel.Overdue] = overdue,
            [MyWorkUrgencyLevel.Now] = ready - overdue,
            [MyWorkUrgencyLevel.Today] = ending,
        };
    }

    private IQueryable<Row> Project(IQueryable<Incubation> query) =>
        query.Select(i => new Row(
            i.Id, i.TestOrderId!.Value, i.TestOrder!.SampleId, i.TestOrder.Sample!.ReferenceNumber, i.TestOrder.TestCode,
            _db.TestDefinitions.Where(d => d.Code == i.TestOrder.TestCode).Select(d => d.DisplayName).FirstOrDefault(),
            i.StepName, i.ExpectedReadingAt, i.IncubationEndUtc, i.MinimumDurationOverriddenByUserId, i.MinimumDurationOverriddenAt,
            i.TestOrder.SectionId, i.TestOrder.Section!.Name, i.TestOrder.Section.Code));

    private static MyWorkItem ToItem(MyWorkContext ctx, Row r, bool ready)
    {
        var now = ctx.NowUtc;
        // It became ready at the earliest of the conditions that hold now.
        DateTime? readySince = null;
        if (ready)
        {
            var moments = new List<DateTime>(3);
            if (r.Expected is { } e && e <= now) moments.Add(e);
            if (r.End is { } n && n <= now) moments.Add(n);
            if (r.OverriddenBy is not null && r.OverriddenAt is { } o) moments.Add(o);
            if (moments.Count > 0) readySince = moments.Min();
        }

        var expected = r.Expected ?? r.End;
        var urgency = MyWorkUrgency.ForReading(ready, readySince, expected, now, ctx.EndOfLabDayUtc, ctx.ReadOverdueHours);
        return new MyWorkItem(
            $"incubation:{r.Id}", ready ? "readyToRead" : "endingToday", r.SampleRef, $"Read {r.DisplayName ?? r.TestCode}",
            $"{r.StepName} · {r.SectionName}", expected, urgency, readySince ?? expected ?? now, false, null,
            new MyWorkTarget("incubation-read", SampleId: r.SampleId, TestOrderId: r.TestOrderId, IncubationId: r.Id),
            ready ? new[] { "read" } : Array.Empty<string>(), r.SectionId, r.SectionName, r.Lab);
    }
}
