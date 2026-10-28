using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One version of a workflow profile (KPI remodel S1, spec section 2). Rows are
// append-only: a Draft is signed in place once (Draft -> Effective) and then
// never changes; a correction is a new version. IVersionedEntity exists only so
// that signing a Draft can be guarded by If-Match; Version maps to xmin.
public class WorkflowProfileVersion : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public WorkflowType WorkflowType { get; set; }

    // null = the default for the whole WorkflowType; set = override for that one test.
    public string? TestCode { get; set; }

    // 1 unit = 1 standard analyst-hour. null = inherit from the type default.
    public decimal? WorkUnits { get; set; }

    // Analyst-stage target in days. null = inherit from the type default.
    public decimal? TargetAnalystDays { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public WorkflowProfileStatus Status { get; set; }
    public string ReasonForChange { get; set; } = string.Empty;

    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }

    // Plain int, no FK. 0 = created by a migration.
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
