using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Append-only history of what happened to an occurrence.
public class MonitoringOccurrenceEvent
{
    public int Id { get; set; }
    public int MonitoringOccurrenceId { get; set; }
    public MonitoringOccurrence? MonitoringOccurrence { get; set; }

    public OccurrenceEventType EventType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public int PerformedByUserId { get; set; }
    public DateTime PerformedAt { get; set; }
    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }
    // The second person who signed a skip or window change.
    public int? SecondElectronicSignatureId { get; set; }
    public ElectronicSignature? SecondElectronicSignature { get; set; }
}
