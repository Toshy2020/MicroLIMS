using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SharedTsbStateBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static TestDefinition Definition(string code, int minHours, int maxHours) => new()
    {
        Code = code,
        Steps =
        {
            new TestWorkflowStep
            {
                StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment,
                StepMedia =
                {
                    new TestWorkflowStepMedia
                    {
                        Id = 1, MediaProductId = 3, DisplayOrder = 1,
                        IncubationMinHours = minHours, IncubationMaxHours = maxHours, TempMin = 30m, TempMax = 35m
                    }
                }
            }
        }
    };

    private static Incubation TsbIncubation(int testOrderId, DateTime start) => new()
    {
        TestOrderId = testOrderId, StepName = "TSB Enrichment", MediaId = 20, StartedAt = start, IncubationStartUtc = start,
        Media = new Media { Id = 20, MaterialId = 10, LotNumber = "TSB-1", Material = new Material { Id = 10, MaterialName = "TSB", MediaProductId = 3 } }
    };

    private static Dictionary<int, (int MaterialId, int? MediaProductId)> MediaLookup(IEnumerable<Incubation> incubations) =>
        incubations.Where(i => i.MediaId.HasValue && i.Media != null)
            .GroupBy(i => i.MediaId!.Value)
            .ToDictionary(g => g.Key, g => (g.First().Media!.MaterialId, g.First().Media!.Material?.MediaProductId));

    private static (Sample Sample, PathogenSessionData Data) Scenario(
        (int OrderId, string Code, int MinHours)[] tests, params Incubation[] incubations)
    {
        var sample = new Sample { ReferenceNumber = "S-1" };
        foreach (var t in tests) sample.TestOrders.Add(new TestOrder { Id = t.OrderId, TestCode = t.Code });
        var defs = tests.ToDictionary(t => t.Code, t => Definition(t.Code, t.MinHours, Math.Max(t.MinHours, 24)));
        var data = new PathogenSessionData(defs, _ => "Unknown", incubations.ToList(), new(), new(), new());
        return (sample, data);
    }

    [Fact]
    public void NoSharedIncubation_IsNotStarted_AndListsTheTestsNeedingTsb()
    {
        var (sample, data) = Scenario(new[] { (11, "TAMC", 18) });

        var result = SharedTsbStateBuilder.Build(sample, data, MediaLookup(data.Incubations), locationCount: 3, Now);

        Assert.False(result.Dto.IsStarted);
        Assert.False(result.IsIncubating);
        Assert.False(result.IsCompleted);
        Assert.Equal(new[] { "TAMC" }, result.Dto.ApplicableTestCodes);
        Assert.Equal(3, result.Dto.ApplicableLocationCount);
        Assert.Equal("18 – 24 h", result.Dto.RequiredDurationRange);
        Assert.False(result.Dto.WindowNotConfigured);
    }

    [Fact]
    public void SharedIncubationWithUnconfiguredWindow_IsFlagged_AndHasNoReadyTime()
    {
        var (sample, data) = Scenario(new[] { (11, "TAMC", 0) }, TsbIncubation(11, Now.AddHours(-30)));

        var result = SharedTsbStateBuilder.Build(sample, data, MediaLookup(data.Incubations), 1, Now);

        Assert.True(result.Dto.IsStarted);
        Assert.True(result.Dto.WindowNotConfigured);
        Assert.Null(result.Dto.MinReadyAt);
        Assert.False(result.IsCompleted);
    }

    [Fact]
    public void TwoTestsSharingOneStart_AreCompleteOnlyWhenBothMinimumsHaveElapsed()
    {
        var start = Now.AddHours(-20);   // 20 h in: TAMC (min 18) is ready, TYMC (min 48) is not
        var (sample, data) = Scenario(new[] { (11, "TAMC", 18), (12, "TYMC", 48) },
            TsbIncubation(11, start), TsbIncubation(12, start));

        var result = SharedTsbStateBuilder.Build(sample, data, MediaLookup(data.Incubations), 1, Now);

        Assert.True(result.IsIncubating);
        Assert.False(result.IsCompleted);
        Assert.Equal(start.AddHours(48), result.Dto.MinReadyAt);
        Assert.Equal(new[] { "TAMC", "TYMC" }, result.Dto.ApplicableTestCodes.OrderBy(c => c));
        Assert.True(result.OwnTsbByOrder[11].IsComplete);
        Assert.False(result.OwnTsbByOrder[12].IsComplete);
    }
}
