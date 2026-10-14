namespace MicroLIMS.Domain.Entities;

// Capacity assumptions for one laboratory section (spec 4.5). No row means the page falls back to the
// documented defaults and says so. The defaults below repeat KpiSettingDefaults in Application.
public class KpiSetting : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    // 0 < x <= 24. Provisional until the Section Head confirms it.
    public decimal HoursPerAnalystDay { get; set; } = 6.0m;
    // bit0 = Monday ... bit6 = Sunday; 1..127. 31 = Mon-Fri.
    public int WorkingWeekdays { get; set; } = 31;
    // Working days looked ahead for forward load; 1..30.
    public int LoadHorizonDays { get; set; } = 5;

    public int UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
