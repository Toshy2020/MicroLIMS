using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SessionStatusResolverTests
{
    private static (string Status, string Display) Resolve(
        int completed, int required, SampleStatus sample = SampleStatus.Received,
        bool tsbIncubating = false, bool tsbCompleted = false, int tsbTests = 0) =>
        SessionStatusResolver.Resolve(completed, required, sample, tsbIncubating, tsbCompleted, tsbTests);

    [Fact]
    public void AllCellsComplete_IsReadyForReview_UntilTheSampleIsApproved()
    {
        Assert.Equal(("READY_FOR_REVIEW", "Ready for Technical Review"), Resolve(4, 4));
        Assert.Equal(("COMPLETED", "Completed & Approved"), Resolve(4, 4, SampleStatus.Approved));
    }

    [Fact]
    public void SomeCellsComplete_ShowsProgress_EvenWhileTsbIsIncubating()
    {
        Assert.Equal(("RESULTS_IN_PROGRESS", "Results In Progress (1/4)"), Resolve(1, 4, tsbIncubating: true, tsbTests: 2));
    }

    [Fact]
    public void NoResultsYet_FollowsTheTsbStage()
    {
        Assert.Equal(("TSB_INCUBATING", "TSB Enrichment Incubating"), Resolve(0, 4, tsbIncubating: true, tsbTests: 2));
        Assert.Equal(("TSB_COMPLETED", "TSB Complete, Downstream Testing"), Resolve(0, 4, tsbCompleted: true, tsbTests: 2));
    }

    [Fact]
    public void NoTsbTests_MeansDownstreamTesting_AndTsbTestsNotStartedMeansNotStarted()
    {
        Assert.Equal(("DOWNSTREAM_TESTING", "Testing In Progress"), Resolve(0, 4, tsbTests: 0));
        Assert.Equal(("NOT_STARTED", "Not Started"), Resolve(0, 4, tsbTests: 2));
    }

    [Fact]
    public void ZeroRequiredCells_IsNeverReadyForReview()
    {
        Assert.Equal(("DOWNSTREAM_TESTING", "Testing In Progress"), Resolve(0, 0));
    }
}
