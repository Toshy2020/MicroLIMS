using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One year's monitoring programme. A Draft is built by the plan engine,
// then signed into Approved; a later revision supersedes it. Never edited
// after approval.
public class MonitoringPlanRevision : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int Year { get; set; }
    public int RevisionNumber { get; set; }
    public PlanRevisionStatus Status { get; set; } = PlanRevisionStatus.Draft;
    // Required when approving a revision after the first of the year.
    public string? Reason { get; set; }
    // Occurrences whose window ended before this date are not part of the revision.
    public DateOnly EffectiveFrom { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int GeneratedByUserId { get; set; }
    public int? ApprovalSignatureId { get; set; }
    public ElectronicSignature? ApprovalSignature { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public List<MonitoringOccurrence> CreatedOccurrences { get; set; } = new();
}
