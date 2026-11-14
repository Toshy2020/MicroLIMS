namespace MicroLIMS.Domain.Entities;

// Weekly working-hours schedule in the laboratory's local time zone.
// StartMinute and EndMinute are minutes from local midnight (0..1440).
public class LabWorkingHours
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
}
