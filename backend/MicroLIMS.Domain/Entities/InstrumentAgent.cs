using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

// Identity of an on-site edge agent that pushes instrument data (e.g.
// ChemStation HPLC results) into the LIMS. Carries its own API key
// (stored only as a hash) and is bound to exactly one Equipment row.
public class InstrumentAgent : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    // IANA zone of the instrument PC; instrument timestamps are local.
    public string TimeZoneId { get; set; } = string.Empty;

    [AuditRedacted]
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RevokedByUserId { get; set; }
    public string? RevokeReason { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
