namespace MicroLIMS.Domain.Entities;

// Site-wide settings of the Annual Monitoring Plan. One row; with none, the
// defaults below apply.
public class MonitoringSettings : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    // First day of the lab week for weekly periods. Sunday: the lab works Sunday to Thursday.
    public DayOfWeek LabWeekStartDay { get; set; } = DayOfWeek.Sunday;
    // bit0 = Monday ... bit6 = Sunday (same convention as KpiSetting.WorkingWeekdays).
    // 79 = Sunday to Thursday.
    public int WorkingWeekdays { get; set; } = 79;

    // Tolerance around a fixed-day target date, per category.
    public int WaterEarlyToleranceDays { get; set; }
    public int WaterLateToleranceDays { get; set; } = 2;
    public int EmEarlyToleranceDays { get; set; }
    public int EmLateToleranceDays { get; set; } = 2;
    public int AcEarlyToleranceDays { get; set; }
    public int AcLateToleranceDays { get; set; } = 2;

    // From this day of the month, incomplete monthly groups are highlighted.
    public int WarningDayOfMonth { get; set; } = 20;
    // Days after the window closes before Overdue becomes Missed.
    public int GraceDays { get; set; } = 7;

    // Spacing warnings between consecutive performed samples of one target.
    public int MonthlyMinGapDays { get; set; } = 14;
    public int MonthlyMaxGapDays { get; set; } = 45;
    public int WeeklyMinGapDays { get; set; } = 4;
    public int WeeklyMaxGapDays { get; set; } = 10;

    // Days to close a deviation, and working days allowed for the extra
    // sampling opened after an excursion.
    public int DeviationDueDays { get; set; } = 30;
    public int FollowUpWindowDays { get; set; } = 5;

    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
