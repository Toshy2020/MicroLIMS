namespace MicroLIMS.Domain.Entities;

// Per-section switch controlling whether analysts holding Tests.SelfAssign may take unassigned
// tests in that section from My Work. Absent row means enabled. ChangedByUserId is nullable so
// rows carried over from the old laboratory-wide setting by migration need not invent an actor
// (null = carried over from the old laboratory-wide setting).
public class SectionSelfAssignmentSetting : IVersionedEntity
{
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public bool Enabled { get; set; }
    public DateTime ChangedAt { get; set; }
    // null = carried over from the old laboratory-wide setting
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public uint Version { get; set; }
}
