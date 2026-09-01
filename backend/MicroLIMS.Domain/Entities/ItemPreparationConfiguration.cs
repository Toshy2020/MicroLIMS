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
    public string Unit { get; set; } = string.Empty; // ml/gm/bottle/cap/25cm2

    public string Technique { get; set; } = string.Empty; // "PourPlate" or "Filtration"
    public decimal? FiltrationVolume { get; set; }
    public decimal? WashingVolume { get; set; }

    public int DiluentTypeId { get; set; }
    public DiluentType? DiluentType { get; set; }
    public int? DiluentMediaId { get; set; } // set only when DiluentType.RequiresBatchTracking
    public Media? DiluentMedia { get; set; }

    public int NeutralizerId { get; set; }
    public Neutralizer? Neutralizer { get; set; }

    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
