using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Each sample's DueAt / IsOverdue (shown per row in the workspace register)
// must follow the same rule as the Overdue tile and filter, so a row never
// says "overdue" while the tile disagrees.
public class SampleDueTimeRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OpenSample_IsDueTwentyFourHoursAfterReceipt()
    {
        var received = Now.AddHours(-5);
        Assert.Equal(received.AddHours(24), SampleWorkflowQueues.DueAt(SampleStatus.InTesting, received));
        Assert.False(SampleWorkflowQueues.IsOverdueAt(SampleStatus.InTesting, received, Now));
    }

    [Fact]
    public void OpenSample_PastItsDueTime_IsOverdue()
    {
        Assert.True(SampleWorkflowQueues.IsOverdueAt(SampleStatus.Received, Now.AddHours(-25), Now));
    }

    [Theory]
    [InlineData(SampleStatus.Approved)]
    [InlineData(SampleStatus.Rejected)]
    [InlineData(SampleStatus.RetestRequested)]
    [InlineData(SampleStatus.Cancelled)]
    [InlineData(SampleStatus.Voided)]
    public void ClosedSample_HasNoDueTimeAndIsNeverOverdue(SampleStatus status)
    {
        var received = Now.AddDays(-10);
        Assert.Null(SampleWorkflowQueues.DueAt(status, received));
        Assert.False(SampleWorkflowQueues.IsOverdueAt(status, received, Now));
    }

    [Theory]
    [InlineData(SampleStatus.Received, -25)]
    [InlineData(SampleStatus.InTesting, -23)]
    [InlineData(SampleStatus.UnderReview, -48)]
    [InlineData(SampleStatus.Approved, -48)]
    public void InMemoryRule_MatchesTheQueryRule(SampleStatus status, int receivedHoursAgo)
    {
        var sample = new Sample { Status = status, ReceivedAt = Now.AddHours(receivedHoursAgo) };
        var queryRule = SampleWorkflowQueues.IsOverdue(Now).Compile();
        Assert.Equal(queryRule(sample), SampleWorkflowQueues.IsOverdueAt(sample.Status, sample.ReceivedAt, Now));
    }
}
