using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class CalendarEvent : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public CalendarEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;

    // Exactly one of Date or Month is set.
    public DateOnly? Date { get; set; }
    public string? Month { get; set; }

    public string? Note { get; set; }

    public int? EquipmentInventoryId { get; set; }
    public EquipmentInventory? EquipmentInventory { get; set; }

    public CalendarEventStatus Status { get; set; } = CalendarEventStatus.Active;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    public string? CancelReason { get; set; }

    public List<CalendarEventDocument> Documents { get; set; } = new();
}
