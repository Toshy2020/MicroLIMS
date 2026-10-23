using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class EquipmentScheduleCalculatorTests
{
    private const int WorkingWeekdays = 79; // Sun–Thu working
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void AddInterval_months_clamps_month_end()
    {
        var result = EquipmentScheduleCalculator.AddInterval(new DateOnly(2026, 1, 31), 1, IntervalUnit.Months);
        Assert.Equal(new DateOnly(2026, 2, 28), result);
    }

    [Fact]
    public void DueAfter_moves_weekend_back()
    {
        var lastActual = new DateOnly(2026, 4, 24);
        var today = new DateOnly(2026, 4, 24);
        var result = EquipmentScheduleCalculator.DueAfter(
            lastActual, 1, IntervalUnit.Weeks, null, today, WorkingWeekdays, NoHolidays);
        Assert.Equal(new DateOnly(2026, 4, 30), result);
    }

    [Fact]
    public void DueAfter_moves_forward_when_previous_is_before_today()
    {
        var lastActual = new DateOnly(2026, 4, 24);
        var today = new DateOnly(2026, 5, 1);
        var result = EquipmentScheduleCalculator.DueAfter(
            lastActual, 1, IntervalUnit.Weeks, null, today, WorkingWeekdays, NoHolidays);
        Assert.Equal(new DateOnly(2026, 5, 3), result);
    }

    [Fact]
    public void DueAfter_moves_forward_when_previous_is_before_last_actual()
    {
        var lastActual = new DateOnly(2026, 4, 30);
        var today = new DateOnly(2026, 4, 28);
        var certificateNextDue = new DateOnly(2026, 5, 1);
        var result = EquipmentScheduleCalculator.DueAfter(
            lastActual, 1, IntervalUnit.Weeks, certificateNextDue, today, WorkingWeekdays, NoHolidays);
        Assert.Equal(new DateOnly(2026, 5, 3), result);
    }

    [Fact]
    public void DueAfter_certificate_wins()
    {
        var lastActual = new DateOnly(2026, 3, 10);
        var today = new DateOnly(2026, 3, 10);
        var certificateNextDue = new DateOnly(2027, 2, 15);
        var result = EquipmentScheduleCalculator.DueAfter(
            lastActual, 12, IntervalUnit.Months, certificateNextDue, today, WorkingWeekdays, NoHolidays);
        Assert.Equal(new DateOnly(2027, 2, 15), result);
    }

    [Fact]
    public void ProjectYear_weekly_counts()
    {
        var lastActual = new DateOnly(2025, 12, 28);
        var from = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 1, 1);
        var dates = EquipmentScheduleCalculator.ProjectYear(
            lastActual, 1, IntervalUnit.Weeks, null, 2026, from, today, WorkingWeekdays, NoHolidays);

        Assert.Equal(52, dates.Count);
        Assert.All(dates, d =>
        {
            Assert.Equal(2026, d.Year);
            Assert.True(d >= from);
            Assert.NotEqual(DayOfWeek.Friday, d.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Saturday, d.DayOfWeek);
        });
    }

    [Fact]
    public void ProjectYear_stops_at_year_end()
    {
        var lastActual = new DateOnly(2026, 10, 1);
        var from = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 1, 1);
        var dates = EquipmentScheduleCalculator.ProjectYear(
            lastActual, 6, IntervalUnit.Months, null, 2026, from, today, WorkingWeekdays, NoHolidays);

        Assert.Empty(dates);
    }

    [Fact]
    public void ProjectYear_monthly_does_not_drift_after_short_month()
    {
        var dates = EquipmentScheduleCalculator.ProjectYear(
            new DateOnly(2025, 12, 31), 1, IntervalUnit.Months, null, 2026,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), WorkingWeekdays, NoHolidays);

        Assert.Contains(new DateOnly(2026, 3, 31), dates); // Tuesday; would be 28 Mar after a Feb clamp
        Assert.Contains(new DateOnly(2026, 2, 26), dates); // 28 Feb is a Saturday, moved back to Thursday
    }

    [Fact]
    public void Describe_texts()
    {
        Assert.Equal("Weekly", EquipmentScheduleCalculator.Describe(1, IntervalUnit.Weeks));
        Assert.Equal("Every 2 weeks", EquipmentScheduleCalculator.Describe(2, IntervalUnit.Weeks));
        Assert.Equal("Monthly", EquipmentScheduleCalculator.Describe(1, IntervalUnit.Months));
        Assert.Equal("Every 6 months", EquipmentScheduleCalculator.Describe(6, IntervalUnit.Months));
    }

    [Fact]
    public void AddInterval_years()
    {
        var resultNormal = EquipmentScheduleCalculator.AddInterval(new DateOnly(2025, 3, 10), 1, IntervalUnit.Years);
        Assert.Equal(new DateOnly(2026, 3, 10), resultNormal);

        var resultClamp = EquipmentScheduleCalculator.AddInterval(new DateOnly(2024, 2, 29), 1, IntervalUnit.Years);
        Assert.Equal(new DateOnly(2025, 2, 28), resultClamp);
    }

    [Fact]
    public void Describe_years()
    {
        Assert.Equal("Yearly", EquipmentScheduleCalculator.Describe(1, IntervalUnit.Years));
        Assert.Equal("Every 2 years", EquipmentScheduleCalculator.Describe(2, IntervalUnit.Years));
        Assert.Equal("Every 3 years", EquipmentScheduleCalculator.Describe(3, IntervalUnit.Years));
    }

    [Fact]
    public void ProjectYear_with_1_years_from_2025_03_10_gives_2026_03_10_a_Tuesday()
    {
        var lastActual = new DateOnly(2025, 3, 10);
        var dates = EquipmentScheduleCalculator.ProjectYear(
            lastActual, 1, IntervalUnit.Years, null, 2026,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), WorkingWeekdays, NoHolidays);

        var due = Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 3, 10), due);
        Assert.Equal(DayOfWeek.Tuesday, due.DayOfWeek);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData("N/A")]
    [InlineData("na")]
    [InlineData("NA")]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("—")]
    [InlineData("none")]
    [InlineData("NONE")]
    [InlineData("nil")]
    [InlineData("NIL")]
    [InlineData("0")]
    [InlineData("unknown")]
    [InlineData("UNKNOWN")]
    [InlineData("  n/a  ")]
    public void EquipmentSerialHelper_identifies_placeholders(string? serial)
    {
        Assert.True(EquipmentSerialHelper.IsPlaceholder(serial));
        Assert.False(EquipmentSerialHelper.HasMeaningfulSerial(serial));
    }

    [Theory]
    [InlineData("SN-12345")]
    [InlineData("1")]
    [InlineData("01")]
    [InlineData("N/A-123")]
    public void EquipmentSerialHelper_recognizes_meaningful_serials(string serial)
    {
        Assert.False(EquipmentSerialHelper.IsPlaceholder(serial));
        Assert.True(EquipmentSerialHelper.HasMeaningfulSerial(serial));
    }
}
