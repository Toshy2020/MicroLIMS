using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class EquipmentTask : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public int EquipmentInventoryId { get; set; }
    public EquipmentInventory? EquipmentInventory { get; set; }

    public EquipmentTaskType TaskType { get; set; }
    public int Year { get; set; }
    public int Sequence { get; set; } = 1;

    public bool IsUnplanned { get; set; }

    public int? CreatedByRevisionId { get; set; }
    public EquipmentPlanRevision? CreatedByRevision { get; set; }

    public int? RetiredByRevisionId { get; set; }
    public EquipmentPlanRevision? RetiredByRevision { get; set; }

    public DateOnly OriginalDueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? CompletedOn { get; set; }

    public int? CalibrationDocumentId { get; set; }
    public EquipmentDocument? CalibrationDocument { get; set; }

    public int? MaintenanceLogEntryId { get; set; }
    public MaintenanceLogEntry? MaintenanceLogEntry { get; set; }

    public string IntervalSnapshot { get; set; } = string.Empty;

    public List<EquipmentTaskEvent> Events { get; set; } = new();
}
