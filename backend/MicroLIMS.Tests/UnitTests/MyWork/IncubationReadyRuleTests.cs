using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.MyWork;

// The SQL predicate My Work filters with must give the same answer as the in-memory rule the
// dashboards use, for every combination of the three arms.
public class IncubationReadyRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PredicateMatchesRule_ForEveryCombinationOfExpectedEndAndOverride()
    {
        var predicate = SampleWorkflowQueues.IsIncubationReadyAt(Now).Compile();
        DateTime?[] moments = { null, Now.AddMinutes(-1), Now, Now.AddMinutes(1) };
        int?[] overrides = { null, 7 };

        var checkedCombinations = 0;
        foreach (var expected in moments)
        foreach (var end in moments)
        foreach (var over in overrides)
        {
            var incubation = new Incubation { ExpectedReadingAt = expected, IncubationEndUtc = end, MinimumDurationOverriddenByUserId = over };
            Assert.Equal(SampleWorkflowQueues.IsIncubationReadyToRead(incubation, Now), predicate(incubation));
            checkedCombinations++;
        }

        Assert.Equal(32, checkedCombinations);
    }
}
