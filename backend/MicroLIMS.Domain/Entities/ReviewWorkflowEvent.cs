using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Review/approval lifecycle log for any gated record - Sample, Media
// lot, or Cryovial batch. Separate from TestOrder's WorkflowHistory,
// which stays the per-test step log.
//
// Polymorphic on (EntityType, EntityId) rather than a typed FK, matching
// ElectronicSignature's existing shape so one table serves every gated
// entity. That trades the cascade-delete FK for reach, which is the
// right trade for an append-only audit log: these rows should outlive
// the record they describe, not vanish with it.
public class ReviewWorkflowEvent
{
    public int Id { get; set; }

    // "Sample", "Media", or "Cryovial" - same vocabulary as
    // ElectronicSignature.EntityType so a record's signatures and its
    // lifecycle events can be looked up with the same pair of keys.
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }

    public ReviewWorkflowEventType EventType { get; set; }
    public int PerformedByUserId { get; set; }

    // Captured at write time so the timeline still reads correctly even
    // if the performer is later renamed, reassigned, or deactivated.
    public string PerformedByNameSnapshot { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }

    // Only set on ApprovalDecisionMade events.
    public ApprovalDecision? Decision { get; set; }
}
