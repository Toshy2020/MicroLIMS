namespace MicroLIMS.Domain.Entities;

// An absent row leaves the competency gate off for the laboratory.
public class CompetencySectionSetting : IVersionedEntity
{
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public bool GateEnabled { get; set; }
    public int? ReminderDaysBeforeExpiry { get; set; }
    public DateTime ChangedAt { get; set; }
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public uint Version { get; set; }
}
