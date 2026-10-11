using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SectionHeadQueueBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static SectionQueueEntry Entry(int sampleId, DateTime submittedAt, params TestOrder[] orders)
    {
        var sample = new Sample { Id = sampleId, ReferenceNumber = $"S-{sampleId}", Item = new Item { Name = $"Item {sampleId}" }, ReceivedAt = submittedAt };
        return new SectionQueueEntry(sample, 7, new SampleSectionSignoff { SubmittedForReviewAt = submittedAt, ReviewedAt = submittedAt }, orders.ToList());
    }

    [Fact]
    public void ReviewQueue_SortsOldestFirst_AndNamesAssignedAnalyst()
    {
        var young = Entry(1, Now.AddHours(-2), new TestOrder { Id = 10, TestCode = "TAMC", AssignedAnalystId = 5 });
        var old = Entry(2, Now.AddHours(-30), new TestOrder { Id = 20, TestCode = "TYMC" });

        var q = SectionHeadQueueBuilder.BuildReviewQueue(new[] { young, old }, Now, new(), new(), Array.Empty<ReviewResultRecordRow>(),
            new() { [5] = "Mazen" }, new() { [7] = "Micro" });

        Assert.Equal(new[] { 2, 1 }, q.Select(i => i.SampleId));
        Assert.Equal(30, q[0].AgeHours);
        Assert.Empty(q[0].AnalystNames);
        Assert.Equal(new[] { "Mazen" }, q[1].AnalystNames);
        Assert.Equal("Micro", q[1].SectionName);
    }

    [Fact]
    public void ReviewQueue_FallsBackToResultRecordSnapshotName_WhenNoAssignee()
    {
        var entry = Entry(1, Now.AddHours(-1), new TestOrder { Id = 10, TestCode = "TAMC" });
        var records = new[] { new ReviewResultRecordRow(10, 9, "Snapshot Name", Now) };

        var q = SectionHeadQueueBuilder.BuildReviewQueue(new[] { entry }, Now, new(), new(), records, new() { [9] = "Live Name" }, new());

        Assert.Equal(new[] { "Snapshot Name" }, q[0].AnalystNames);
    }

    [Fact]
    public void DelayedAttentionItems_OnlyIncludeItemsOlderThan24Hours()
    {
        var q = SectionHeadQueueBuilder.BuildReviewQueue(
            new[] { Entry(1, Now.AddHours(-2)), Entry(2, Now.AddHours(-25)) }, Now, new(), new(), Array.Empty<ReviewResultRecordRow>(), new(), new());

        var items = SectionHeadQueueBuilder.BuildDelayedReviewAttentionItems(q);

        var item = Assert.Single(items);
        Assert.Equal((2, "DelayedReview", "Medium", "Scientific review delayed by 25h"), (item.SampleId, item.ActionType, item.Urgency, item.Reason));
    }

    [Fact]
    public void RetestAttentionItems_NameTheOriginSample_WhenItIsKnown()
    {
        var withOrigin = new Sample { Id = 1, ReferenceNumber = "S-1", OriginSampleId = 99, ReceivedAt = Now, TestOrders = { new TestOrder { TestCode = "A" }, new TestOrder { TestCode = "B", IsSuperseded = true } } };
        var orphan = new Sample { Id = 2, ReferenceNumber = "S-2", OriginSampleId = 98, ReceivedAt = Now };

        var items = SectionHeadQueueBuilder.BuildRetestAttentionItems(new[] { withOrigin, orphan }, new() { [99] = "S-OLD" });

        Assert.Equal("Retest sample in progress (origin S-OLD)", items[0].Reason);
        Assert.Equal(new[] { "A" }, items[0].TestCodes);
        Assert.Equal("Retest sample in progress", items[1].Reason);
    }

    [Fact]
    public void SampleDisplayName_FallsBackToReferenceNumber_ThenLiteralSample()
    {
        Assert.Equal("S-1", SampleDisplayName.For(new Sample { ReferenceNumber = "S-1" }));
        Assert.Equal("Sample", SampleDisplayName.For(new Sample { ReferenceNumber = " " }));
        Assert.Equal("Sample", SampleDisplayName.For(null));
    }
}
