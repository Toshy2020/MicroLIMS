using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Column Master entity for HPLC testing (REQ-FP-011).
// Represents analytical chromatography columns, their serial numbers, section assignment,
// and compatible HPLC equipment.
public class ChromatographyColumn
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }

    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    // Optional many-to-many to compatible Equipment (only Type = Hplc)
    public List<Equipment> CompatibleEquipment { get; set; } = new();
}
