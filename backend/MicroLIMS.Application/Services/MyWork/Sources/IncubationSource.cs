using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Open incubations on my tests (assigned to me, or started by me), split by the one ready-to-read rule
// (SampleWorkflowQueues.IsIncubationReadyAt). Ready -> "readyToRead"; not ready and expected before the
// laboratory day ends -> "endingToday"; not ready and expected after the laboratory day ends up to
// now + 24 hours -> "next24Hours". An incubation whose test was returned to me is listed as returned instead.
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

    private IQueryable<Incubation> NotReadyWithDue(MyWorkContext ctx) =>
        Mine(ctx)
            .Where(AnalystQueries.Not(SampleWorkflowQueues.IsIncubationReadyAt(ctx.NowUtc)))
            .Where(i => (i.ExpectedReadingAt ?? i.IncubationEndUtc) != null);

    private IQueryable<Incubation> Ending(MyWorkContext ctx)
    {
        var endOfDay = ctx.EndOfLabDayUtc;
        return NotReadyWithDue(ctx)
            .Where(i => (i.ExpectedReadingAt ?? i.IncubationEndUtc) <= endOfDay);
    }

    private IQueryable<Incubation> Next24Hours(MyWorkContext ctx)
    {
        var endOfDay = ctx.EndOfLabDayUtc;
        var horizon = ctx.NowUtc.AddHours(24);
        return NotReadyWithDue(ctx)
            .Where(i => (i.ExpectedReadingAt ?? i.IncubationEndUtc) > endOfDay
                && (i.ExpectedReadingAt ?? i.IncubationEndUtc) <= horizon);
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

    private async Task<(int Ready, int Overdue, int Ending, int Next24)> CountsAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var ready = await Ready(ctx).CountAsync(ct);
        var overdue = ctx.ReadOverdueHours > 0 && ready > 0 ? await ReadyAndOverdue(ctx).CountAsync(ct) : 0;
        var ending = await Ending(ctx).CountAsync(ct);
        var next24 = await Next24Hours(ctx).CountAsync(ct);
        return (ready, overdue, ending, next24);
    }

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var (ready, _, ending, next24) = await CountsAsync(ctx, ct);
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
            items.AddRange(rows.Select(r => ToItem(ctx, r, "readyToRead")));
        }

        if (ending > 0)
        {
            var rows = await Project(Ending(ctx).OrderBy(i => i.ExpectedReadingAt ?? i.IncubationEndUtc).ThenBy(i => i.Id))
                .Take(take).ToListAsync(ct);
            items.AddRange(rows.Select(r => ToItem(ctx, r, "endingToday")));
        }

        if (next24 > 0)
        {
            var rows = await Project(Next24Hours(ctx).OrderBy(i => i.ExpectedReadingAt ?? i.IncubationEndUtc).ThenBy(i => i.Id))
                .Take(take).ToListAsync(ct);
            items.AddRange(rows.Select(r => ToItem(ctx, r, "next24Hours")));
        }

        return new MyWorkSourceResult(items,
            new Dictionary<string, int>
            {
                ["readyToRead"] = ready,
                ["endingToday"] = ending,
                ["next24Hours"] = next24,
            },
            new Dictionary<string, int>());
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var (ready, overdue, ending, next24) = await CountsAsync(ctx, ct);
        return new Dictionary<MyWorkUrgencyLevel, int>
        {
            [MyWorkUrgencyLevel.Overdue] = overdue,
            [MyWorkUrgencyLevel.Now] = ready - overdue,
            [MyWorkUrgencyLevel.Today] = ending,
            [MyWorkUrgencyLevel.Later] = next24,
        };
    }

    private IQueryable<Row> Project(IQueryable<Incubation> query) =>
        query.Select(i => new Row(
            i.Id, i.TestOrderId!.Value, i.TestOrder!.SampleId, i.TestOrder.Sample!.ReferenceNumber, i.TestOrder.TestCode,
            _db.TestDefinitions.Where(d => d.Code == i.TestOrder.TestCode).Select(d => d.DisplayName).FirstOrDefault(),
            i.StepName, i.ExpectedReadingAt, i.IncubationEndUtc, i.MinimumDurationOverriddenByUserId, i.MinimumDurationOverriddenAt,
            i.TestOrder.SectionId, i.TestOrder.Section!.Name, i.TestOrder.Section.Code));

    private static MyWorkItem ToItem(MyWorkContext ctx, Row r, string groupKey)
    {
        var ready = groupKey == "readyToRead";
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
            $"incubation:{r.Id}", groupKey, r.SampleRef, $"Read {r.DisplayName ?? r.TestCode}",
            $"{r.StepName} · {r.SectionName}", expected, urgency, readySince ?? expected ?? now, false, null,
            new MyWorkTarget("incubation-read", SampleId: r.SampleId, TestOrderId: r.TestOrderId, IncubationId: r.Id),
            ready ? new[] { "read" } : Array.Empty<string>(), r.SectionId, r.SectionName, r.Lab);
    }
}
