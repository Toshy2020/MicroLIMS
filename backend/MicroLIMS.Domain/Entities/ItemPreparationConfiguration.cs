using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Per-item preparation protocol (Product/RM/PM). Configured once, then
// confirmed by the analyst on every sample of that Item - the confirmed
// values are copied onto SamplePreparation so later edits here never
// alter historical records.
//
// A config auto-created from an analyst's first manual entry starts as
// PendingReview and is usable immediately; Section Head review happens
// after the fact so testing is never blocked.
public class ItemPreparationConfiguration
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Amount { get; set; }
    // No Unit here - the reporting unit lives on Specification per test
    // (see the 2026-09 Preparation Configuration simplification); Amount
    // is a bare quantity, not itself unit-typed.

    public string Technique { get; set; } = string.Empty; // "PourPlate" or "Filtration"
    public decimal? FiltrationVolume { get; set; }
    public decimal? WashingVolume { get; set; }

    // Free text as of the 2026-09 simplification - previously an FK to
    // DiluentType, which also gated a GPT-released Media lot selection via
    // RequiresBatchTracking/DiluentMediaId. That lot-tracking mechanism was
    // dropped as unused (no DiluentType had ever required it); if diluent
    // batch traceability is needed again, it needs redesigning from here.
    public string Diluent { get; set; } = string.Empty;
    public string Neutralizer { get; set; } = string.Empty;

    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
