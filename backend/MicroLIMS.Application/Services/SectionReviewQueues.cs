using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// One section of one sample waiting in a review/approval queue. A
// single-section sample is exactly one entry, as the old one-row-per-sample
// queues were; a sample tested by two sections can wait in two.
public sealed record SectionQueueEntry(Sample Sample, int SectionId, SampleSectionSignoff? Signoff, List<TestOrder> Orders);

public static class SectionReviewQueues
{
    // Laboratory-section filters for dashboards and reports. A null list
    // means unrestricted (a system administrator): the filter is then left
    // out of the query entirely.
    public static Expression<Func<TestOrder, bool>> OrderIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? t => true : t => sectionIds.Contains(t.SectionId);

    public static Expression<Func<Sample, bool>> SampleIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? s => true : s => s.TestOrders.Any(t => sectionIds.Contains(t.SectionId));

    public static Expression<Func<Incubation, bool>> IncubationIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? i => true : i => i.TestOrder != null && sectionIds.Contains(i.TestOrder.SectionId);

    // Samples with at least one section (among sectionIds) in the given
    // state: its sign-off row is in that state, or it has no row yet and
    // shares the sample's own status (SampleSectionRollup.StatusOf).
    public static IQueryable<Sample> Candidates(IQueryable<Sample> samples, SectionSignoffStatus status, IReadOnlyCollection<int>? sectionIds)
    {
        var shared = status switch
        {
            SectionSignoffStatus.UnderReview => SampleStatus.UnderReview,
            SectionSignoffStatus.UnderApproval => SampleStatus.UnderApproval,
            _ => throw new ArgumentOutOfRangeException(nameof(status), "Only review and approval queues are supported.")
        };

        // Unrestricted viewers also see a sample with no tests at all by its
        // own status - it belongs to no section, but it is still visible.
        return sectionIds is null
            ? samples.Where(s => s.SectionSignoffs.Any(r => r.Status == status)
                || (s.Status == shared && (!s.TestOrders.Any() || s.TestOrders.Any(t => !s.SectionSignoffs.Any(r => r.SectionId == t.SectionId)))))
            : samples.Where(s => s.SectionSignoffs.Any(r => r.Status == status && sectionIds.Contains(r.SectionId))
                || (s.Status == shared && s.TestOrders.Any(t => sectionIds.Contains(t.SectionId) && !s.SectionSignoffs.Any(r => r.SectionId == t.SectionId))));
    }

    // Samples with a section (among sectionIds) waiting longer than the
    // threshold: review is timed from the section's submission, approval
    // from its review - or the sample-level clock for a section with no
    // sign-off row.
    public static async Task<List<int>> OverdueSampleIdsAsync(
        MicroLimsDbContext db, SectionSignoffStatus status, DateTime now, TimeSpan threshold, IReadOnlyCollection<int>? sectionIds)
    {
        var samples = await Candidates(db.Samples.AsNoTracking(), status, sectionIds)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .ToListAsync();
        var waiting = Entries(samples, status, sectionIds)
            .Select(e => (e.Sample, e.Signoff))
            .ToList();
        // A sample with no tests (visible only to unrestricted viewers - see
        // Candidates) is timed by its own sample-level clock.
        if (sectionIds is null)
            waiting.AddRange(samples.Where(s => s.TestOrders.Count == 0).Select(s => (s, (SampleSectionSignoff?)null)));
        if (waiting.Count == 0) return new List<int>();

        var cutoff = now - threshold;
        var reviewClocks = status == SectionSignoffStatus.UnderReview
            ? await SampleWorkflowQueues.GetReviewClockStartsAsync(db, waiting.Select(w => w.Sample.Id).Distinct().ToList())
            : new Dictionary<int, DateTime>();

        return waiting
            .Where(w =>
            {
                var start = status == SectionSignoffStatus.UnderReview
                    ? w.Signoff?.SubmittedForReviewAt ?? reviewClocks.GetValueOrDefault(w.Sample.Id, w.Sample.ReceivedAt)
                    : w.Signoff?.ReviewedAt ?? SampleWorkflowQueues.GetApprovalClockStart(w.Sample);
                return start <= cutoff;
            })
            .Select(w => w.Sample.Id)
            .Distinct()
            .ToList();
    }

    // The exact entries, from samples loaded with TestOrders and SectionSignoffs.
    public static List<SectionQueueEntry> Entries(IEnumerable<Sample> samples, SectionSignoffStatus status, IReadOnlyCollection<int>? sectionIds) =>
        samples.SelectMany(s => SampleSectionRollup.SectionIds(s)
                .Where(id => (sectionIds is null || sectionIds.Contains(id)) && SampleSectionRollup.StatusOf(s, id) == status)
                .Select(id => new SectionQueueEntry(
                    s, id, s.SectionSignoffs.FirstOrDefault(r => r.SectionId == id), SampleSectionRollup.CurrentOrders(s, id))))
            .ToList();
}
