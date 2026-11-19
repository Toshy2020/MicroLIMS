using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Tests a reviewer returned to me that I have not yet redone (the TestReturnHelper rule, decided in SQL).
// The reason comes from the latest return event.
public sealed class ReturnedTestsSource : IMyWorkSource
{
    private readonly IMicroLimsDbContext _db;

    public ReturnedTestsSource(IMicroLimsDbContext db) => _db = db;

    public string Key => "returnedTests";

    public bool AppliesTo(MyWorkContext ctx) => true;

    private sealed record Row(int TestOrderId, int SampleId, string SampleRef, string TestCode, string? DisplayName, string? Reason,
        DateTime ReturnedAt, int SectionId, string SectionName, string Lab);

    private IQueryable<Domain.Entities.TestReturnEvent> Mine(MyWorkContext ctx) =>
        AnalystQueries.MyPendingReturns(_db, ctx.UserId).InSections(ctx, e => e.TestOrder!.SectionId, e => e.TestOrder!.Sample!.Category);

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var total = await Mine(ctx).CountAsync(ct);
        var rows = total == 0
            ? new List<Row>()
            : await Mine(ctx)
                .OrderBy(e => e.ReturnedAt).ThenBy(e => e.Id)
                .Select(e => new Row(
                    e.TestOrderId, e.TestOrder!.SampleId, e.TestOrder.Sample!.ReferenceNumber, e.TestOrder.TestCode,
                    _db.TestDefinitions.Where(d => d.Code == e.TestOrder.TestCode).Select(d => d.DisplayName).FirstOrDefault(),
                    e.Reason, e.ReturnedAt, e.TestOrder.SectionId, e.TestOrder.Section!.Name, e.TestOrder.Section.Code))
                .Take(take).ToListAsync(ct);

        var items = rows.Select(r => new MyWorkItem(
            $"returned:{r.TestOrderId}", "returned", r.SampleRef, $"Revise {r.DisplayName ?? r.TestCode}",
            r.Reason, null, MyWorkUrgencyLevel.Now, r.ReturnedAt, false, null,
            new MyWorkTarget("test-step", SampleId: r.SampleId, TestOrderId: r.TestOrderId),
            Array.Empty<string>(), r.SectionId, r.SectionName, r.Lab)).ToList();

        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["returned"] = total }, new Dictionary<string, int>());
    }

    // A returned test is always "Now".
    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct) =>
        new Dictionary<MyWorkUrgencyLevel, int> { [MyWorkUrgencyLevel.Now] = await Mine(ctx).CountAsync(ct) };
}
