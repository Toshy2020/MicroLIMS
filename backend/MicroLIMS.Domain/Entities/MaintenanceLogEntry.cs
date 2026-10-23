using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class MaintenanceLogEntry
{
    public int Id { get; set; }

    public int EquipmentInventoryId { get; set; }
    public EquipmentInventory? EquipmentInventory { get; set; }

    public MaintenanceEntryType EntryType { get; set; }
    public DateOnly PerformedOn { get; set; }
    public int PerformedByUserId { get; set; }

    public string WorkDone { get; set; } = string.Empty;
    public string? PartsReplaced { get; set; }
    public string? Findings { get; set; }

    public int ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }

    public DateTime RecordedAt { get; set; }

    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public int? CalendarEventId { get; set; }
    public CalendarEvent? CalendarEvent { get; set; }
}
