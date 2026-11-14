using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Services.MyWork;

// How pressing one item is. Pure functions of the times handed in; the caller
// supplies "now" and the end of the laboratory day from its injected clock.
public static class MyWorkUrgency
{
    // Past = Overdue; before the lab day ends = Today; later = Later; no due time = None.
    public static MyWorkUrgencyLevel ForDueDate(DateTime? dueAtUtc, DateTime nowUtc, DateTime endOfLabDayUtc)
    {
        if (dueAtUtc is not { } due) return MyWorkUrgencyLevel.None;
        if (due < nowUtc) return MyWorkUrgencyLevel.Overdue;
        return due <= endOfLabDayUtc ? MyWorkUrgencyLevel.Today : MyWorkUrgencyLevel.Later;
    }

    // An incubation that can be read now is Now; once it has waited longer than the
    // configured hours (0 = never) it is Overdue. One that is not ready yet is Today or
    // Later by its expected time, and None when no time is known.
    public static MyWorkUrgencyLevel ForReading(bool ready, DateTime? readySinceUtc, DateTime? expectedUtc,
        DateTime nowUtc, DateTime endOfLabDayUtc, int readOverdueHours)
    {
        if (ready)
        {
            return readOverdueHours > 0 && readySinceUtc is { } since && nowUtc - since > TimeSpan.FromHours(readOverdueHours)
                ? MyWorkUrgencyLevel.Overdue
                : MyWorkUrgencyLevel.Now;
        }

        if (expectedUtc is not { } expected) return MyWorkUrgencyLevel.None;
        return expected <= endOfLabDayUtc ? MyWorkUrgencyLevel.Today : MyWorkUrgencyLevel.Later;
    }

    // Overdue once the item has sat in its status longer than the workflow overdue window.
    public static MyWorkUrgencyLevel ForStatusAge(DateTime enteredStatusAtUtc, DateTime nowUtc) =>
        nowUtc - enteredStatusAtUtc > SampleWorkflowQueues.OverdueAfter
            ? MyWorkUrgencyLevel.Overdue
            : MyWorkUrgencyLevel.None;
}
