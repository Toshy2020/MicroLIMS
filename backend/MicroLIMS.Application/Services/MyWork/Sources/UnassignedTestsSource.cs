using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Open tests nobody holds, in two views: "available" (a member of the section takes one) and "unassigned" (a user
// who may assign puts someone on it). Both list the same kind of test; they differ in who sees it and what the row offers.
public abstract class UnassignedTestsSourceBase : IMyWorkSource
{
    protected readonly IMicroLimsDbContext _db;

    protected UnassignedTestsSourceBase(IMicroLimsDbContext db) => _db = db;

    public abstract string Key { get; }
    protected abstract string GroupKey { get; }
    protected abstract string Verb { get; }
    protected abstract string Action { get; }
    protected abstract string TargetType { get; }
    protected abstract bool Holds(MyWorkContext ctx);
    protected abstract IQueryable<TestOrder> InScope(IQueryable<TestOrder> query, MyWorkContext ctx);
    protected virtual Task<bool> EnabledAsync(CancellationToken ct) => Task.FromResult(true);

    public bool AppliesTo(MyWorkContext ctx) => Holds(ctx);

    private sealed record Row(int TestOrderId, int SampleId, string SampleRef, string TestCode, string? DisplayName, DateTime ReceivedAt,
        int SectionId, string SectionName, string Lab);

    private IQueryable<TestOrder> Open(MyWorkContext ctx) => InScope(AnalystQueries.UnassignedOpenTests(_db), ctx);

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        if (!await EnabledAsync(ct))
            return new MyWorkSourceResult(Array.Empty<MyWorkItem>(), new Dictionary<string, int>(), new Dictionary<string, int>());

        var total = await Open(ctx).CountAsync(ct);
        var rows = total == 0
            ? new List<Row>()
            : await Open(ctx)
                .OrderBy(t => t.Sample!.ReceivedAt).ThenBy(t => t.Id)
                .Select(t => new Row(
                    t.Id, t.SampleId, t.Sample!.ReferenceNumber, t.TestCode,
                    _db.TestDefinitions.Where(d => d.Code == t.TestCode).Select(d => d.DisplayName).FirstOrDefault(),
                    t.Sample.ReceivedAt, t.SectionId, t.Section!.Name, t.Section.Code))
                .Take(take).ToListAsync(ct);

        var items = rows.Select(r => new MyWorkItem(
            $"{GroupKey}:{r.TestOrderId}", GroupKey, r.SampleRef, $"{Verb} {r.DisplayName ?? r.TestCode}",
            r.SectionName, null, MyWorkUrgency.ForStatusAge(r.ReceivedAt, ctx.NowUtc), r.ReceivedAt, false, null,
            new MyWorkTarget(TargetType, SampleId: r.SampleId, TestOrderId: r.TestOrderId),
            new[] { Action }, r.SectionId, r.SectionName, r.Lab)).ToList();

        return new MyWorkSourceResult(items, new Dictionary<string, int> { [GroupKey] = total }, new Dictionary<string, int>());
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        if (!await EnabledAsync(ct))
            return new Dictionary<MyWorkUrgencyLevel, int>();

        var total = await Open(ctx).CountAsync(ct);
        var cutoff = ctx.NowUtc - SampleWorkflowQueues.OverdueAfter;
        var overdue = total == 0 ? 0 : await Open(ctx).Where(t => t.Sample!.ReceivedAt < cutoff).CountAsync(ct);
        return new Dictionary<MyWorkUrgencyLevel, int>
        {
            [MyWorkUrgencyLevel.Overdue] = overdue,
            [MyWorkUrgencyLevel.None] = total - overdue,
        };
    }
}

// Unassigned tests for users who may assign them (Samples.AssignAnalyst): the sections they can read, target the
// assignment dialog. Not affected by the self-assignment setting, which only governs Take.
public sealed class UnassignedTestsSource : UnassignedTestsSourceBase
{
    public UnassignedTestsSource(IMicroLimsDbContext db) : base(db) { }

    public override string Key => "unassignedTests";
    protected override string GroupKey => "unassigned";
    protected override string Verb => "Assign";
    protected override string Action => "assign";
    protected override string TargetType => "sample-assign";
    protected override bool Holds(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.SamplesAssignAnalyst);
    protected override IQueryable<TestOrder> InScope(IQueryable<TestOrder> query, MyWorkContext ctx) => query.InSections(ctx, t => t.SectionId);
}
