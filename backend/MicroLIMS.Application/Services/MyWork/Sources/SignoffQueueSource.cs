using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// The sample-and-section entries waiting for the caller's sign-off (review or approval), with the
// Segregation of Duties lock. The entries are projected in SQL here by the same rule SectionReviewQueues.Entries applies
// for the dashboards and the decision endpoints (keep the two in step), and the lock is decided for the whole list with ONE set-based guard call, so the
// number of SQL commands depends on the number of tables touched, never on the number of entries.
//
// Scope is the caller's own sections (MemberSectionIds, already narrowed to a chosen laboratory). Sections.ViewAll
// widens what a person may read, never what is theirs to decide, so it is not used here.
internal abstract class SignoffQueueSource
{

    private readonly IMicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _guard;

    protected SignoffQueueSource(IMicroLimsDbContext db)
    {
        _db = db;
        _guard = new SegregationOfDutiesGuard(db);
    }

    protected abstract SectionSignoffStatus Status { get; }
    protected abstract string GroupKey { get; }
    protected abstract string TargetType { get; }
    protected abstract string Verb { get; }
    protected abstract string Action { get; }

    // The lock reason when the caller performed a test of the entry: the decision's own refusal sentence.
    protected abstract string PerformedReason { get; }

    // When the entry entered its current status: the age that drives urgency.
    protected abstract DateTime StatusSince(SampleRow sample, SignoffRow? signoff, IReadOnlyDictionary<int, DateTime> reviewClocks);

    // A reason the caller may not decide this entry on top of having performed a test; null = none.
    protected virtual string? ExtraLockReason(SampleRow sample, SignoffRow? signoff, int userId) => null;

    protected virtual bool NeedsReviewClocks => false;

    protected sealed record OrderRow(int Id, int SectionId, bool IsSuperseded);
    protected sealed record SignoffRow(int SectionId, SectionSignoffStatus Status, DateTime? SubmittedForReviewAt,
        DateTime? ReviewedAt, int? ReviewedByUserId);
    protected sealed record SampleRow(int Id, string ReferenceNumber, SampleStatus Status, SampleCategory Category,
        DateTime ReceivedAt, DateTime? ReviewedAt, int? ReviewedByUserId, bool AwaitingTestSelection,
        string? PendingTargetSectionIds, List<OrderRow> Orders, List<SignoffRow> Signoffs);

    protected sealed record Entry(int SampleId, string Reference, int SectionId, string SectionName, string Lab,
        DateTime Since, string? LockReason);

    protected async Task<List<Entry>> LoadAsync(MyWorkContext ctx, CancellationToken ct)
    {
        if (ctx.MemberSectionIds.Count == 0) return new List<Entry>();
        var sectionIds = ctx.MemberSectionIds.ToArray();

        var samples = await SectionReviewQueues.Candidates(_db.Samples.AsNoTracking(), Status, sectionIds)
            .AsSplitQuery()
            .Select(s => new SampleRow(s.Id, s.ReferenceNumber, s.Status, s.Category, s.ReceivedAt,
                s.ReviewedAt, s.ReviewedByUserId, s.AwaitingTestSelection, s.PendingTargetSectionIds,
                s.TestOrders.Where(t => sectionIds.Contains(t.SectionId))
                    .Select(t => new OrderRow(t.Id, t.SectionId, t.IsSuperseded)).ToList(),
                s.SectionSignoffs.Where(r => sectionIds.Contains(r.SectionId))
                    .Select(r => new SignoffRow(r.SectionId, r.Status, r.SubmittedForReviewAt, r.ReviewedAt, r.ReviewedByUserId)).ToList()))
            .ToListAsync(ct);

        // Use the same section membership and status fallback as SampleSectionRollup.
        var entries = (from sample in samples
                       from sectionId in sample.Orders.Select(o => o.SectionId)
                           .Union(sample.Signoffs.Select(r => r.SectionId))
                           .Union(SampleSectionRollup.PendingSectionIds(sample.AwaitingTestSelection, sample.PendingTargetSectionIds))
                           .OrderBy(id => id)
                       where sectionIds.Contains(sectionId)
                       let signoff = sample.Signoffs.FirstOrDefault(r => r.SectionId == sectionId)
                       where (signoff?.Status ?? SampleSectionRollup.FromSampleStatus(sample.Status)) == Status
                       select new
                       {
                           Sample = sample,
                           SectionId = sectionId,
                           Signoff = signoff,
                           OrderIds = sample.Orders.Where(o => o.SectionId == sectionId && !o.IsSuperseded)
                               .Select(o => o.Id).ToArray()
                       }).ToList();
        if (ctx.PhyschemSectionId is int fpId && ctx.ReadablePhyschemCategories is { } allowed)
        {
            entries = entries
                .Where(e => e.SectionId != fpId || allowed.Contains(e.Sample.Category))
                .ToList();
        }
        if (entries.Count == 0) return new List<Entry>();

        var performed = await _guard.GetTestOrderIdsPerformedByAsync(
            ctx.UserId, entries.SelectMany(e => e.OrderIds).Distinct().ToArray(), ct);
        var reviewClocks = NeedsReviewClocks
            ? await SampleWorkflowQueues.GetReviewClockStartsAsync(_db, entries.Select(e => e.Sample.Id).Distinct().ToArray(), ct)
            : new Dictionary<int, DateTime>();
        var entrySectionIds = entries.Select(e => e.SectionId).Distinct().ToArray();
        var sections = await _db.DocumentSections.AsNoTracking()
            .Where(s => entrySectionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Code })
            .ToDictionaryAsync(s => s.Id, ct);

        return entries.Select(e =>
        {
            var section = sections.GetValueOrDefault(e.SectionId);
            var reason = e.OrderIds.Any(performed.Contains)
                ? PerformedReason
                : ExtraLockReason(e.Sample, e.Signoff, ctx.UserId);
            return new Entry(e.Sample.Id, e.Sample.ReferenceNumber, e.SectionId, section?.Name ?? string.Empty, section?.Code ?? string.Empty,
                StatusSince(e.Sample, e.Signoff, reviewClocks), reason);
        }).ToList();
    }

    protected async Task<(List<MyWorkItem> Items, int Open, int Locked)> ItemsAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var entries = await LoadAsync(ctx, ct);
        var items = entries
            .OrderBy(e => e.LockReason != null)
            .ThenBy(e => MyWorkUrgency.ForStatusAge(e.Since, ctx.NowUtc))
            .ThenBy(e => e.Since)
            .ThenBy(e => e.SampleId)
            .Take(take)
            .Select(e => new MyWorkItem(
                $"{GroupKey}:{e.SampleId}:{e.SectionId}", GroupKey, e.Reference, $"{Verb} {e.Reference}", e.SectionName, null,
                MyWorkUrgency.ForStatusAge(e.Since, ctx.NowUtc), e.Since, e.LockReason != null, e.LockReason,
                new MyWorkTarget(TargetType, SampleId: e.SampleId),
                e.LockReason != null ? Array.Empty<string>() : new[] { Action }, e.SectionId, e.SectionName, e.Lab))
            .ToList();
        return (items, entries.Count(e => e.LockReason == null), entries.Count(e => e.LockReason != null));
    }

    protected async Task<Dictionary<MyWorkUrgencyLevel, int>> UrgencyCountsAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var open = (await LoadAsync(ctx, ct)).Where(e => e.LockReason == null).ToList();
        var counts = new Dictionary<MyWorkUrgencyLevel, int>();
        foreach (var level in open.Select(e => MyWorkUrgency.ForStatusAge(e.Since, ctx.NowUtc)))
            counts[level] = counts.GetValueOrDefault(level) + 1;
        return counts;
    }
}
