using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

internal sealed class ApprovalQueue : SignoffQueueSource
{
    public ApprovalQueue(IMicroLimsDbContext db) : base(db) { }
    protected override SectionSignoffStatus Status => SectionSignoffStatus.UnderApproval;
    protected override string GroupKey => "approval";
    protected override string TargetType => "sample-approval";
    protected override string Verb => "Approve";
    protected override string Action => "approve";
    protected override string PerformedReason => SignoffRefusalSentences.CannotApproveTested;

    // Approval is timed from the section's review, or the sample-level clock for a section with no sign-off row.
    protected override DateTime StatusSince(Sample sample, SampleSectionSignoff? signoff, IReadOnlyDictionary<int, DateTime> reviewClocks) =>
        signoff?.ReviewedAt ?? SampleWorkflowQueues.GetApprovalClockStart(sample);

    // The decision refuses a section the approver reviewed; a section with no sign-off row falls back to the
    // sample's own reviewer, as the decision does when it creates the row.
    protected override string? ExtraLockReason(Sample sample, SampleSectionSignoff? signoff, int userId) =>
        (signoff is null ? sample.ReviewedByUserId : signoff.ReviewedByUserId) == userId ? SignoffRefusalSentences.CannotApproveReviewed : null;

    public Task<(List<MyWorkItem> Items, int Open, int Locked)> ListAsync(MyWorkContext ctx, int take, CancellationToken ct) => ItemsAsync(ctx, take, ct);
    public Task<Dictionary<MyWorkUrgencyLevel, int>> CountsAsync(MyWorkContext ctx, CancellationToken ct) => UrgencyCountsAsync(ctx, ct);
}

// Awaiting my approval: one item per sample and section under approval in my sections (Samples.Approve), plus
// media lots whose evaluation conformed and that still await release (Media.Release). Own work is listed
// locked and not counted: a test I performed or a section I reviewed (the sample decision refuses both), a
// lot I prepared or whose challenge I read (the media decision refuses both).
public sealed class ApprovalSource : IMyWorkSource
{
    public const string MediaLockReason = "You prepared or evaluated this media lot";

    private readonly IMicroLimsDbContext _db;
    private readonly ApprovalQueue _queue;

    public ApprovalSource(IMicroLimsDbContext db)
    {
        _db = db;
        _queue = new ApprovalQueue(db);
    }

    public string Key => "approval";

    public bool AppliesTo(MyWorkContext ctx) => Approves(ctx) || Releases(ctx);

    private static bool Approves(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.SamplesApprove);
    private static bool Releases(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.MediaRelease);

    private sealed record MediaRow(int Id, string LotNumber, string MaterialName, int SectionId, string SectionName, string Lab, DateTime Since);

    // Media awaiting a release decision: the evaluation completed Conform and no decision yet
    // (MediaReleaseService.GetAwaitingApprovalAsync), in the caller's own sections.
    private IQueryable<Media> Awaiting(MyWorkContext ctx)
    {
        var sectionIds = ctx.MemberSectionIds.ToArray();
        return _db.Media.AsNoTracking()
            .Where(m => m.ApprovalStatus == ApprovalGateStatus.PendingReview
                && sectionIds.Contains(m.Material!.SectionId)
                && _db.MediaEvaluations.Any(e => e.MediaId == m.Id && e.Status == MediaEvaluationStatus.Completed && e.Outcome == EvaluationOutcome.Conform));
    }

    // The media decision refuses whoever prepared the lot or read any challenge of its evaluation
    // (SegregationOfDutiesGuard.DidUserPrepareOrEvaluateMediaAsync).
    private IQueryable<Media> Mine(IQueryable<Media> media, int userId, bool locked)
    {
        Expression<Func<Media, bool>> isMine = m => m.PreparedByUserId == userId
            || _db.MediaEvaluationChallenges.Any(c => c.MediaEvaluation!.MediaId == m.Id && c.ReadByUserId == userId);
        return media.Where(locked ? isMine : AnalystQueries.Not(isMine));
    }

    private IQueryable<MediaRow> Rows(IQueryable<Media> media) =>
        media
            .Select(m => new
            {
                m.Id,
                m.LotNumber,
                m.Material!.MaterialName,
                m.Material.SectionId,
                SectionName = m.Material.Section!.Name,
                Lab = m.Material.Section.Code,
                Since = _db.MediaEvaluations
                    .Where(e => e.MediaId == m.Id && e.Status == MediaEvaluationStatus.Completed && e.Outcome == EvaluationOutcome.Conform)
                    .Max(e => e.CompletedAt) ?? m.PreparedAt
            })
            .OrderBy(r => r.Since)
            .ThenBy(r => r.Id)
            .Select(r => new MediaRow(r.Id, r.LotNumber, r.MaterialName, r.SectionId, r.SectionName, r.Lab, r.Since));

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var items = new List<MyWorkItem>();
        var open = 0;
        var locked = 0;

        if (Approves(ctx))
        {
            var (sampleItems, sampleOpen, sampleLocked) = await _queue.ListAsync(ctx, take, ct);
            items.AddRange(sampleItems);
            open += sampleOpen;
            locked += sampleLocked;
        }

        if (Releases(ctx) && ctx.MemberSectionIds.Count > 0)
        {
            var awaiting = Awaiting(ctx);
            var openCount = await Mine(awaiting, ctx.UserId, false).CountAsync(ct);
            var lockedCount = await Mine(awaiting, ctx.UserId, true).CountAsync(ct);
            var openRows = openCount == 0
                ? new List<MediaRow>()
                : await Rows(Mine(awaiting, ctx.UserId, false)).Take(take).ToListAsync(ct);
            var lockedRows = lockedCount == 0 || openRows.Count >= take
                ? new List<MediaRow>()
                : await Rows(Mine(awaiting, ctx.UserId, true)).Take(take - openRows.Count).ToListAsync(ct);
            open += openCount;
            locked += lockedCount;

            items.AddRange(openRows.Select(r => ToItem(ctx, r, false)));
            items.AddRange(lockedRows.Select(r => ToItem(ctx, r, true)));
        }

        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["approval"] = open }, new Dictionary<string, int> { ["approval"] = locked });
    }

    private static MyWorkItem ToItem(MyWorkContext ctx, MediaRow r, bool locked) => new(
        $"media:{r.Id}", "approval", r.LotNumber, $"Release {r.MaterialName}", r.SectionName, null,
        MyWorkUrgency.ForStatusAge(r.Since, ctx.NowUtc), r.Since, locked, locked ? MediaLockReason : null,
        new MyWorkTarget("media-approval", MediaId: r.Id),
        locked ? Array.Empty<string>() : new[] { "approve" }, r.SectionId, r.SectionName, r.Lab);

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var counts = Approves(ctx) ? await _queue.CountsAsync(ctx, ct) : new Dictionary<MyWorkUrgencyLevel, int>();

        if (Releases(ctx) && ctx.MemberSectionIds.Count > 0)
        {
            var overdueBefore = ctx.NowUtc - SampleWorkflowQueues.OverdueAfter;
            var unlocked = Mine(Awaiting(ctx), ctx.UserId, false);
            var open = await unlocked.CountAsync(ct);
            var overdue = open == 0
                ? 0
                : await unlocked.CountAsync(m => (_db.MediaEvaluations
                    .Where(e => e.MediaId == m.Id && e.Status == MediaEvaluationStatus.Completed && e.Outcome == EvaluationOutcome.Conform)
                    .Max(e => e.CompletedAt) ?? m.PreparedAt) < overdueBefore, ct);
            counts[MyWorkUrgencyLevel.Overdue] = counts.GetValueOrDefault(MyWorkUrgencyLevel.Overdue) + overdue;
            counts[MyWorkUrgencyLevel.None] = counts.GetValueOrDefault(MyWorkUrgencyLevel.None) + open - overdue;
        }

        return counts;
    }
}
