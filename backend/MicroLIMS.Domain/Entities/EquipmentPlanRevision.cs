using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class EquipmentPlanRevision : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int Year { get; set; }
    public int RevisionNumber { get; set; }
    public PlanRevisionStatus Status { get; set; } = PlanRevisionStatus.Draft;
    public string? Reason { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int GeneratedByUserId { get; set; }
    public int? ApprovalSignatureId { get; set; }
    public ElectronicSignature? ApprovalSignature { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public List<EquipmentTask> CreatedTasks { get; set; } = new();
}
