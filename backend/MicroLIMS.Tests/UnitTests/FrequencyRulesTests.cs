using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class FrequencyRulesTests
{
    [Theory]
    [InlineData("Weekly", FrequencyType.Weekly, 1)]
    [InlineData("fortnightly", FrequencyType.Weekly, 2)]
    [InlineData("Monthly", FrequencyType.Monthly, 1)]
    [InlineData("Quarterly", FrequencyType.Monthly, 3)]
    [InlineData("Annually", FrequencyType.Monthly, 12)]
    [InlineData("Every 2 months", FrequencyType.Monthly, 2)]
    [InlineData("every 3 weeks", FrequencyType.Weekly, 3)]
    [InlineData("  6-monthly ", FrequencyType.Monthly, 6)]
    public void Parser_MapsKnownText(string text, FrequencyType type, int interval)
    {
        var rule = LegacyFrequencyParser.Parse(text);

        Assert.NotNull(rule);
        Assert.Equal(type, rule!.Type);
        Assert.Equal(interval, rule.Interval);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Daily")]
    [InlineData("when needed")]
    [InlineData("Every 13 months")]
    [InlineData("Every 0 weeks")]
    public void Parser_ReturnsNullForUnmappableText(string? text)
    {
        Assert.Null(LegacyFrequencyParser.Parse(text));
    }

    [Fact]
    public void Validate_WeeklyWaterPointNeedsWeekday()
    {
        var rule = new FrequencyRuleDto(FrequencyType.Weekly, 1);

        Assert.NotNull(FrequencyRules.Validate(rule, weekdayRequired: true));
        Assert.Null(FrequencyRules.Validate(rule with { SamplingWeekday = DayOfWeek.Monday }, weekdayRequired: true));
        Assert.Null(FrequencyRules.Validate(rule));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public void Validate_RejectsIntervalOutOfRange(int interval)
    {
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Weekly, interval)));
    }

    [Fact]
    public void Validate_AnchorMonthOnlyForEveryNMonths()
    {
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Monthly, 1, null, 3)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Monthly, 3, null, 13)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Weekly, 1, null, 3)));
        Assert.Null(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Monthly, 3, null, 3)));
    }

    [Fact]
    public void Apply_NullLeavesUnchanged_NoTypeClears()
    {
        var dept = new Department { FrequencyType = FrequencyType.Monthly, FrequencyInterval = 2 };

        FrequencyRules.Apply(dept, null);
        Assert.Equal(FrequencyType.Monthly, dept.FrequencyType);

        FrequencyRules.Apply(dept, new FrequencyRuleDto(null));
        Assert.Null(dept.FrequencyType);
        Assert.Null(dept.FrequencyInterval);
    }

    [Fact]
    public void Resolve_RoomRuleOverridesDepartment()
    {
        var dept = new Department { FrequencyType = FrequencyType.Monthly, FrequencyInterval = 1 };
        var room = new Room { FrequencyType = FrequencyType.Weekly, FrequencyInterval = 1 };

        Assert.Equal(FrequencySource.Own, FrequencyRules.Resolve(room, dept).Source);
        Assert.Equal(FrequencySource.Department, FrequencyRules.Resolve(new Room(), dept).Source);
        Assert.Equal(FrequencySource.None, FrequencyRules.Resolve(new Room(), new Department()).Source);
    }
}
