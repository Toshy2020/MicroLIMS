using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class MediaStorageEntry
{
    public int Id { get; set; }

    public int MediaId { get; set; }
    public Media? Media { get; set; }

    public MediaStorageForm Form { get; set; }
    public int Quantity { get; set; }

    public int? EquipmentId { get; set; }
    public EquipmentInventory? Equipment { get; set; }

    public string? LocationText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
