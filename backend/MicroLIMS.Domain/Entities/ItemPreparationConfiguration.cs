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

    // Display name of the diluent / neutralizer. When a source is set below,
    // the server fills it from the referenced master (ContainedInMedium reads
    // "Contained in the medium"). With no source it is the legacy free text
    // from before diluent/neutralizer traceability: the configuration keeps
    // working at confirmation, but no lot is picked and it is listed for a
    // Section Head to map.
    public string Diluent { get; set; } = string.Empty;
    public string Neutralizer { get; set; } = string.Empty;

    // Structured diluent: exactly the FK matching the source is set.
    public PreparationMaterialSource? DiluentSource { get; set; }
    public int? DiluentMediaProductId { get; set; }
    public MediaProduct? DiluentMediaProduct { get; set; }
    public int? DiluentSolutionMasterId { get; set; }
    public SolutionMaster? DiluentSolutionMaster { get; set; }
    public int? DiluentMaterialMasterEntryId { get; set; }
    public MaterialMasterEntry? DiluentMaterialMasterEntry { get; set; }

    // Structured neutralizer: same shape; ContainedInMedium sets no FK.
    public PreparationMaterialSource? NeutralizerSource { get; set; }
    public int? NeutralizerMediaProductId { get; set; }
    public MediaProduct? NeutralizerMediaProduct { get; set; }
    public int? NeutralizerSolutionMasterId { get; set; }
    public SolutionMaster? NeutralizerSolutionMaster { get; set; }
    public int? NeutralizerMaterialMasterEntryId { get; set; }
    public MaterialMasterEntry? NeutralizerMaterialMasterEntry { get; set; }

    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
