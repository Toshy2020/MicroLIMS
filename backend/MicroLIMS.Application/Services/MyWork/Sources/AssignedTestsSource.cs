using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Tests assigned to me with nothing open to wait for: no open incubation (those are listed as ready or
// ending, or are still incubating) and no pending return (those are listed as returned). Never listed twice.
public sealed class AssignedTestsSource : IMyWorkSource
{
    private readonly IMicroLimsDbContext _db;

    public AssignedTestsSource(IMicroLimsDbContext db) => _db = db;

    public string Key => "assignedTests";

    public bool AppliesTo(MyWorkContext ctx) => true;

    private sealed record Row(int TestOrderId, int SampleId, string SampleRef, string TestCode, string? DisplayName, DateTime ReceivedAt,
        int SectionId, string SectionName, string Lab);

    private IQueryable<TestOrder> Mine(MyWorkContext ctx)
    {
        var userId = ctx.UserId;
        var returned = AnalystQueries.MyPendingReturns(_db, userId);
        return _db.TestOrders.AsNoTracking()
            .Where(t => t.AssignedAnalystId == userId)
            .Where(AnalystQueries.IsActiveTest)
            .Where(t => !t.Incubations.Any(i => i.CompletedAt == null))
            .Where(t => !returned.Any(r => r.TestOrderId == t.Id))
            .InSections(ctx, t => t.SectionId);
    }

    // Overdue once the sample has been open longer than the workflow window (ForStatusAge); otherwise no urgency.
    private IQueryable<TestOrder> Overdue(MyWorkContext ctx)
    {
        var cutoff = ctx.NowUtc - SampleWorkflowQueues.OverdueAfter;
        return Mine(ctx).Where(t => t.Sample!.ReceivedAt < cutoff);
    }

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var total = await Mine(ctx).CountAsync(ct);
        var rows = total == 0
            ? new List<Row>()
            : await Mine(ctx)
                .OrderBy(t => t.Sample!.ReceivedAt).ThenBy(t => t.Id)
                .Select(t => new Row(
                    t.Id, t.SampleId, t.Sample!.ReferenceNumber, t.TestCode,
                    _db.TestDefinitions.Where(d => d.Code == t.TestCode).Select(d => d.DisplayName).FirstOrDefault(),
                    t.Sample.ReceivedAt, t.SectionId, t.Section!.Name, t.Section.Code))
                .Take(take).ToListAsync(ct);

        var items = rows.Select(r => new MyWorkItem(
            $"test:{r.TestOrderId}", "toPerform", r.SampleRef, $"Perform {r.DisplayName ?? r.TestCode}",
            r.SectionName, null, MyWorkUrgency.ForStatusAge(r.ReceivedAt, ctx.NowUtc), r.ReceivedAt, false, null,
            new MyWorkTarget("test-step", SampleId: r.SampleId, TestOrderId: r.TestOrderId),
            Array.Empty<string>(), r.SectionId, r.SectionName, r.Lab)).ToList();

        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["toPerform"] = total }, new Dictionary<string, int>());
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var total = await Mine(ctx).CountAsync(ct);
        var overdue = total == 0 ? 0 : await Overdue(ctx).CountAsync(ct);
        return new Dictionary<MyWorkUrgencyLevel, int>
        {
            [MyWorkUrgencyLevel.Overdue] = overdue,
            [MyWorkUrgencyLevel.None] = total - overdue,
        };
    }
}
