using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One prepared lot of a MediaType - the Media Preparation module record.
// Not usable in routine testing until BOTH its auto-assigned
// MediaEvaluation completes with Outcome == Conform (see
// MediaEvaluationEngine) AND a Section Head signs for its release (see
// MediaReleaseService). The evaluation qualifies the lot; the signature
// releases it. MediaReleaseService.DecideAsync is the ONLY place
// IsReleasedForUse is ever set true.
public class Media
{
    public int Id { get; set; }

    // The dehydrated media Material consumed to prepare this lot -
    // ManufacturerLot/ManufacturerName below are copied from it at
    // preparation time (see MediaPreparationService.PrepareAsync).
    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    public string LotNumber { get; set; } = string.Empty; // auto: {Material.Code}/{seq}/{yy}, resets yearly
    public string ManufacturerLot { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public decimal TotalWeight { get; set; }
    public string TotalVolume { get; set; } = string.Empty;
    public int? AutoclaveEquipmentId { get; set; }
    public Equipment? AutoclaveEquipment { get; set; }
    public string AutoclaveProgram { get; set; } = string.Empty;
    public string LoadType { get; set; } = string.Empty; // e.g. "liquid (100 ml)" / "agar (500 ml)"
    public decimal Temperature { get; set; }
    public int CycleTime { get; set; }
    public int CycleNumber { get; set; }
    public decimal Ph { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime PreparedAt { get; set; } = DateTime.UtcNow;

    // Who prepared this lot - required for segregation of duties at the
    // release gate (you cannot approve a lot you prepared). Mirrors
    // Material.CreatedByUserId.
    public int PreparedByUserId { get; set; }

    public MediaStatus Status { get; set; } = MediaStatus.Prepared;

    // A Conform evaluation no longer releases a lot on its own - it only
    // makes it eligible. A Section Head must then sign for the release
    // (MediaReleaseService.DecideAsync), which is the ONLY place
    // IsReleasedForUse is ever set true.
    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public bool IsReleasedForUse { get; set; } // set true only by MediaReleaseService.DecideAsync on an approved release
}
