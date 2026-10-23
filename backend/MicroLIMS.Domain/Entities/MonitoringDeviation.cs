using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A tracked record of something that went wrong in the monitoring plan: an
// occurrence that was missed, or an approved result over its action limit.
// Opened by the plan itself, closed by two people with a reason. Never deleted.
public class MonitoringDeviation : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    // "MD-2026-00012"; set from the id once the row exists.
    public string Number { get; set; } = string.Empty;
    public DeviationKind Kind { get; set; }
    public DeviationStatus Status { get; set; } = DeviationStatus.Open;

    public int MonitoringOccurrenceId { get; set; }
    public MonitoringOccurrence? MonitoringOccurrence { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    // The lab date by which it should be closed.
    public DateOnly DueDate { get; set; }

    // Extra sampling the plan opened because of an excursion.
    public int? FollowUpOccurrenceId { get; set; }
    public MonitoringOccurrence? FollowUpOccurrence { get; set; }

    public DeviationClosure? Closure { get; set; }
    public string? ClosureReason { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public int? ClosureSignatureId { get; set; }
    public ElectronicSignature? ClosureSignature { get; set; }
    public int? ClosureSecondSignatureId { get; set; }
    public ElectronicSignature? ClosureSecondSignature { get; set; }
}
