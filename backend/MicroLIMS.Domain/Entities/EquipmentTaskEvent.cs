using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class EquipmentTaskEvent
{
    public int Id { get; set; }

    public int EquipmentTaskId { get; set; }
    public EquipmentTask? EquipmentTask { get; set; }

    public EquipmentTaskEventType EventType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public int? PerformedByUserId { get; set; }
    public DateTime PerformedAt { get; set; }
    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }
}
