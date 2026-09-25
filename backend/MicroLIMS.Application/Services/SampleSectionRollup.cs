using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Per-section review/approval (SampleSectionSignoff) and the Sample.Status
// rolled up from it. Every member expects the Sample to be loaded with
// TestOrders and SectionSignoffs.
//
// A sample tested by one section maps one-to-one onto the old
// sample-level statuses, so single-section samples behave exactly as
// before per-section sign-off existed.
public static class SampleSectionRollup
{
    // Every section with a test on the sample - superseded tests included,
    // since a section whose tests all moved to a retest is still part of
    // the sample - plus any section that already has a sign-off.
    public static List<int> SectionIds(Sample sample) =>
        sample.TestOrders.Select(t => t.SectionId)
            .Union(sample.SectionSignoffs.Select(s => s.SectionId))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

    // A section gets its own sign-off row once it moves past testing, so a
    // section without one shares the sample's own state - InTesting for a
    // sample still being tested, or whatever a sample recorded before
    // per-section sign-off existed was left in.
    public static SectionSignoffStatus StatusOf(Sample sample, int sectionId) =>
        sample.SectionSignoffs.FirstOrDefault(s => s.SectionId == sectionId)?.Status ?? FromSampleStatus(sample.Status);

    private static SectionSignoffStatus FromSampleStatus(SampleStatus status) => status switch
    {
        SampleStatus.UnderReview => SectionSignoffStatus.UnderReview,
        SampleStatus.UnderApproval => SectionSignoffStatus.UnderApproval,
        SampleStatus.Approved => SectionSignoffStatus.Approved,
        SampleStatus.Rejected => SectionSignoffStatus.Rejected,
        SampleStatus.RetestRequested => SectionSignoffStatus.RetestRequested,
        SampleStatus.Cancelled => SectionSignoffStatus.Cancelled,
        SampleStatus.Voided => SectionSignoffStatus.Voided,
        _ => SectionSignoffStatus.InTesting
    };

    public static SampleSectionSignoff GetOrAdd(Sample sample, int sectionId)
    {
        var row = sample.SectionSignoffs.FirstOrDefault(s => s.SectionId == sectionId);
        if (row is null)
        {
            // Starts from the state the section shares with the sample, and
            // keeps a review already recorded on the sample - the
            // reviewer-cannot-approve check depends on it.
            var status = FromSampleStatus(sample.Status);
            var reviewed = status is not (SectionSignoffStatus.InTesting or SectionSignoffStatus.UnderReview);
            row = new SampleSectionSignoff
            {
                SampleId = sample.Id,
                SectionId = sectionId,
                Status = status,
                ReviewedByUserId = reviewed ? sample.ReviewedByUserId : null,
                ReviewedAt = reviewed ? sample.ReviewedAt : null
            };
            sample.SectionSignoffs.Add(row);
        }
        return row;
    }

    public static List<TestOrder> CurrentOrders(Sample sample, int sectionId) =>
        sample.TestOrders.Where(t => !t.IsSuperseded && t.SectionId == sectionId).ToList();

    public static bool IsOpen(SectionSignoffStatus status) =>
        status is SectionSignoffStatus.InTesting or SectionSignoffStatus.UnderReview or SectionSignoffStatus.UnderApproval;

    // Sample.Status is the roll-up of OPEN work: while any lab is still
    // testing, in review or in approval, the sample sits at the least advanced
    // of them - even after another lab rejected - because the rest of the
    // system treats Rejected/Approved as "closed". What users see as the
    // sample's outcome is Overall(), where a rejection wins at once.
    public static SampleStatus? Compute(IReadOnlyCollection<SectionSignoffStatus> statuses)
    {
        if (statuses.Count == 0) return null;
        if (statuses.All(s => s == SectionSignoffStatus.Voided)) return SampleStatus.Voided;
        if (statuses.Contains(SectionSignoffStatus.InTesting)) return SampleStatus.InTesting;
        if (statuses.Contains(SectionSignoffStatus.UnderReview)) return SampleStatus.UnderReview;
        if (statuses.Contains(SectionSignoffStatus.UnderApproval)) return SampleStatus.UnderApproval;
        if (statuses.Contains(SectionSignoffStatus.Rejected)) return SampleStatus.Rejected;
        if (statuses.Contains(SectionSignoffStatus.RetestRequested)) return SampleStatus.RetestRequested;
        return SampleStatus.Approved;
    }

    public static void Apply(Sample sample)
    {
        var computed = Compute(SectionIds(sample).Select(id => StatusOf(sample, id)).ToList());
        if (computed is not null)
            sample.Status = computed.Value;
    }

    public static OverallSampleStatus Overall(Sample sample)
    {
        if (sample.Status == SampleStatus.Voided) return OverallSampleStatus.Voided;
        if (sample.Status == SampleStatus.Cancelled) return OverallSampleStatus.Cancelled;
        var statuses = SectionIds(sample).Select(id => StatusOf(sample, id)).ToList();
        if (statuses.Contains(SectionSignoffStatus.Rejected)) return OverallSampleStatus.Rejected;
        if (statuses.Any(IsOpen)) return OverallSampleStatus.InProgress;
        if (statuses.Contains(SectionSignoffStatus.RetestRequested)) return OverallSampleStatus.RetestRequested;
        return OverallSampleStatus.Approved;
    }

    // Called before a decision changes Sample.Status: every section without a
    // sign-off row shares the sample's state, so give each one its own row
    // first - otherwise a lab that never reached review would read as Rejected.
    public static void FreezeOpenSections(Sample sample)
    {
        foreach (var id in SectionIds(sample))
            GetOrAdd(sample, id);
    }

    // Picks the section an action applies to. With no section requested, a
    // sample with exactly one section in the required state (among those
    // the user may act on) needs no choice - which keeps every
    // single-section sample working without the caller naming a section.
    public static int ResolveSection(
        Sample sample, int? requestedSectionId, SectionSignoffStatus requiredStatus,
        IReadOnlyCollection<int>? userScope, string wrongStateMessage)
    {
        if (requestedSectionId is int requested)
        {
            if (!SectionIds(sample).Contains(requested))
                throw new InvalidOperationException("This sample has no tests in the selected laboratory section.");
            if (userScope is not null && !userScope.Contains(requested))
                throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
            if (StatusOf(sample, requested) != requiredStatus)
                throw new InvalidOperationException(wrongStateMessage);
            return requested;
        }

        var inState = SectionIds(sample).Where(id => StatusOf(sample, id) == requiredStatus).ToList();
        if (inState.Count == 0)
            throw new InvalidOperationException(wrongStateMessage);

        var candidates = userScope is null ? inState : inState.Where(userScope.Contains).ToList();
        if (candidates.Count == 0)
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        if (candidates.Count > 1)
            throw new InvalidOperationException("Choose a laboratory section - more than one section of this sample is waiting.");

        return candidates[0];
    }
}
