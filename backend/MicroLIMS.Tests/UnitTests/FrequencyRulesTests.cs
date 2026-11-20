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
    [InlineData("Daily", FrequencyType.Daily, 1)]
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
    [InlineData("when needed")]
    [InlineData("Every 13 months")]
    [InlineData("Every 0 weeks")]
    public void Parser_ReturnsNullForUnmappableText(string? text)
    {
        Assert.Null(LegacyFrequencyParser.Parse(text));
    }

    [Fact]
    public void Validate_CodesEachRefusalAndKeepsItsEnglish()
    {
        var cases = new (FrequencyRuleDto Rule, string Code, string Message)[]
        {
            (new(FrequencyType.Daily, 2),
                "Monitoring.FrequencyDailyIntervalNotAllowed", "A daily frequency cannot have an interval."),
            (new(FrequencyType.Daily, 1, DayOfWeek.Monday),
                "Monitoring.FrequencyDailyWeekdayNotAllowed", "A daily frequency cannot have a sampling weekday."),
            (new(FrequencyType.Daily, AnchorMonth: 2),
                "Monitoring.FrequencyDailyAnchorMonthNotAllowed", "A daily frequency cannot have an anchor month."),
            (new(FrequencyType.Daily, WeekOfMonth: 2),
                "Monitoring.FrequencyDailyWeekOfMonthNotAllowed", "A daily frequency cannot have a week of the month."),
            (new(FrequencyType.Weekly, 53),
                "Monitoring.FrequencyIntervalOutOfRange", "Frequency interval must be between 1 and 52."),
            (new(FrequencyType.Weekly, AnchorMonth: 2),
                "Monitoring.FrequencyWeeklyAnchorMonthNotAllowed", "A weekly frequency cannot have an anchor month."),
            (new(FrequencyType.Weekly, WeekOfMonth: 2),
                "Monitoring.FrequencyWeeklyWeekOfMonthNotAllowed", "A weekly frequency cannot have a week of the month."),
            (new(FrequencyType.Weekly),
                "Monitoring.FrequencyWeeklyWeekdayRequired", "A weekly frequency needs a sampling weekday."),
            (new(FrequencyType.Monthly, 13),
                "Monitoring.FrequencyMonthlyIntervalTooLong", "A monthly interval cannot exceed 12 months."),
            (new(FrequencyType.Monthly, 2, AnchorMonth: 13),
                "Monitoring.FrequencyAnchorMonthOutOfRange", "Anchor month must be between 1 and 12."),
            (new(FrequencyType.Monthly, AnchorMonth: 2),
                "Monitoring.FrequencyAnchorMonthRequiresMultiMonthInterval", "An anchor month only applies to every-N-months rules."),
            (new(FrequencyType.Monthly, WeekOfMonth: 5),
                "Monitoring.FrequencyWeekOfMonthOutOfRange", "Week of the month must be between 1 and 4."),
        };

        foreach (var (rule, code, message) in cases)
        {
            var problem = FrequencyRules.Validate(rule, weekdayRequired: true);
            Assert.NotNull(problem);
            Assert.Equal(code, problem.Code);
            Assert.Equal(message, problem.Message);
        }
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
    public void Validate_DailyTakesNoOtherFields()
    {
        Assert.Null(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Daily, 1), weekdayRequired: true));
        Assert.Null(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Daily)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Daily, 2)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Daily, 1, DayOfWeek.Sunday)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Daily, 1, null, null, 2)));
    }

    [Fact]
    public void Validate_WeekOfMonthIsOneToFourAndMonthlyOnly()
    {
        Assert.Null(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 2)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 5)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 0)));
        Assert.NotNull(FrequencyRules.Validate(new FrequencyRuleDto(FrequencyType.Weekly, 1, null, null, 2)));
    }

    [Fact]
    public void Describe_DailyAndWeekOfMonth()
    {
        Assert.Equal("Daily, working days", FrequencyRules.Describe(new FrequencyRuleDto(FrequencyType.Daily, 1)));
        Assert.Equal("Monthly, week 2", FrequencyRules.Describe(new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 2)));
    }

    [Fact]
    public void Apply_RoundTripsWeekOfMonth_AndClearResetsIt()
    {
        var dept = new Department();

        FrequencyRules.Apply(dept, new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 3));
        Assert.Equal(3, dept.FrequencyWeekOfMonth);
        Assert.Equal(3, FrequencyRules.From(dept)!.WeekOfMonth);

        FrequencyRules.Apply(dept, new FrequencyRuleDto(null));
        Assert.Null(dept.FrequencyWeekOfMonth);
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
