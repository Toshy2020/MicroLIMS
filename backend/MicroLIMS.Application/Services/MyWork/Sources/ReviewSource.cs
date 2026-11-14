using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Awaiting my review: one item per sample and section under review in my sections, for holders of
// Samples.Review. A sample where I performed any test of the section is listed locked and not counted.
internal sealed class ReviewQueue : SignoffQueueSource
{
    public ReviewQueue(IMicroLimsDbContext db) : base(db) { }
    protected override SectionSignoffStatus Status => SectionSignoffStatus.UnderReview;
    protected override string GroupKey => "review";
    protected override string TargetType => "sample-review";
    protected override string Verb => "Review";
    protected override string Action => "review";
    protected override bool NeedsReviewClocks => true;

    // Review is timed from the section's submission, falling back to the sample-level review clock.
    protected override DateTime StatusSince(Sample sample, SampleSectionSignoff? signoff, IReadOnlyDictionary<int, DateTime> reviewClocks) =>
        signoff?.SubmittedForReviewAt ?? reviewClocks.GetValueOrDefault(sample.Id, sample.ReceivedAt);

    public Task<(List<MyWorkItem> Items, int Open, int Locked)> ListAsync(MyWorkContext ctx, int take, CancellationToken ct) => ItemsAsync(ctx, take, ct);
    public Task<Dictionary<MyWorkUrgencyLevel, int>> CountsAsync(MyWorkContext ctx, CancellationToken ct) => UrgencyCountsAsync(ctx, ct);
}

public sealed class ReviewSource : IMyWorkSource
{
    private readonly ReviewQueue _queue;

    public ReviewSource(IMicroLimsDbContext db) => _queue = new ReviewQueue(db);

    public string Key => "review";

    public bool AppliesTo(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.SamplesReview);

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var (items, open, locked) = await _queue.ListAsync(ctx, take, ct);
        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["review"] = open }, new Dictionary<string, int> { ["review"] = locked });
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct) =>
        await _queue.CountsAsync(ctx, ct);
}
