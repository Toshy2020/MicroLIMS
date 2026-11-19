using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.MyWork;

public class MyWorkUrgencyTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndOfDay = new(2026, 11, 14, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ForDueDate_Past_Overdue() =>
        Assert.Equal(MyWorkUrgencyLevel.Overdue, MyWorkUrgency.ForDueDate(Now.AddMinutes(-1), Now, EndOfDay));

    [Fact]
    public void ForDueDate_BeforeEndOfLabDay_Today() =>
        Assert.Equal(MyWorkUrgencyLevel.Today, MyWorkUrgency.ForDueDate(EndOfDay.AddMinutes(-1), Now, EndOfDay));

    [Fact]
    public void ForDueDate_AfterEndOfLabDay_Later() =>
        Assert.Equal(MyWorkUrgencyLevel.Later, MyWorkUrgency.ForDueDate(EndOfDay.AddMinutes(1), Now, EndOfDay));

    [Fact]
    public void ForDueDate_Null_None() =>
        Assert.Equal(MyWorkUrgencyLevel.None, MyWorkUrgency.ForDueDate(null, Now, EndOfDay));

    [Fact]
    public void ForReading_Ready_Now() =>
        Assert.Equal(MyWorkUrgencyLevel.Now, MyWorkUrgency.ForReading(true, Now.AddHours(-1), Now.AddHours(-1), Now, EndOfDay, 4));

    [Fact]
    public void ForReading_ReadyLongerThanLimit_Overdue() =>
        Assert.Equal(MyWorkUrgencyLevel.Overdue, MyWorkUrgency.ForReading(true, Now.AddHours(-5), Now.AddHours(-5), Now, EndOfDay, 4));

    [Fact]
    public void ForReading_LimitZero_NeverOverdue() =>
        Assert.Equal(MyWorkUrgencyLevel.Now, MyWorkUrgency.ForReading(true, Now.AddDays(-30), Now.AddDays(-30), Now, EndOfDay, 0));

    [Fact]
    public void ForReading_NotReady_ExpectedToday_Today() =>
        Assert.Equal(MyWorkUrgencyLevel.Today, MyWorkUrgency.ForReading(false, null, EndOfDay.AddMinutes(-30), Now, EndOfDay, 4));

    [Fact]
    public void ForStatusAge_23h59_None_24h01_Overdue()
    {
        Assert.Equal(MyWorkUrgencyLevel.None, MyWorkUrgency.ForStatusAge(Now - SampleWorkflowQueues.OverdueAfter + TimeSpan.FromMinutes(1), Now));
        Assert.Equal(MyWorkUrgencyLevel.Overdue, MyWorkUrgency.ForStatusAge(Now - SampleWorkflowQueues.OverdueAfter - TimeSpan.FromMinutes(1), Now));
    }
}
