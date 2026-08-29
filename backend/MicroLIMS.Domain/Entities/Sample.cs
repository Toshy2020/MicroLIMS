using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Sample
{
    public int Id { get; set; }

    // System-generated internal reference number - replaces the old
    // paper "Samples Receiving Record Page No." concept entirely.
    // Format: {CategoryCode}{MM}{YY}{seq:D3}, e.g. FP0107026.
    public string ReferenceNumber { get; set; } = string.Empty;

    public SampleCategory Category { get; set; }
    public int? ItemId { get; set; }              // Product/RM/PM only
    public Item? Item { get; set; }
    public int? WaterSamplingPointId { get; set; } // Water only
    public WaterSamplingPoint? WaterSamplingPoint { get; set; }
    public int? DepartmentId { get; set; }         // EM only
    public Department? Department { get; set; }
    public int? MachineId { get; set; }            // After Cleaning only
    public Machine? Machine { get; set; }
    public string? PreviousProductName { get; set; } // After Cleaning only (free text)
    public string? PreviousProductBatchNumber { get; set; } // After Cleaning only (free text)
    public int? WaterDepartmentId { get; set; }    // Water only (batch model)
    public WaterDepartment? WaterDepartment { get; set; }

    // Product only - descriptive, does not affect assigned tests.
    public string? ProductionStage { get; set; }

    public int CauseOfTestingId { get; set; }
    public CauseOfTesting? CauseOfTesting { get; set; }

    public string? SampleQuantity { get; set; }    // not collected for EM/After Cleaning
    public string SampledBy { get; set; } = string.Empty; // free text
    public string? BatchNumber { get; set; }        // Product/RM/PM only
    public string ControlNumber { get; set; } = string.Empty;
    public DateTime? MfgDate { get; set; }           // Product/RM/PM only
    public DateTime? ExpDate { get; set; }           // Product/RM/PM only

    public int ReceivedByUserId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public SampleStatus Status { get; set; } = SampleStatus.Received;

    // EM/After Cleaning start here and need the checkbox preparation
    // step before any TestOrder exists; every other category is Ready immediately.
    public SamplePreparationStatus PreparationStatus { get; set; } = SamplePreparationStatus.Ready;

    // Water storage condition, captured at Test Preparation.
    public string? StorageCondition { get; set; } // "Refrigerator" or "RoomTemperature"
    public int? StorageTimeHours { get; set; }     // only if refrigerated

    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ApprovalDecision? ApprovalDecision { get; set; }

    // Customer-facing remark printed on the Certificate of Analysis,
    // typed by the Approver at the point of approval only - deliberately
    // separate from ElectronicSignature.Comment, which is internal QA
    // context (deviations/CAPA/conditional-release reasoning) shared by
    // both the Review and Approval steps and never meant to leave the
    // building on a controlled document. Never auto-derived; null/empty
    // renders as "No remarks." on the certificate. Lives here (not on
    // ElectronicSignature) for the same reason ApprovedByUserId/ApprovedAt
    // do: one current value per Sample, overwritten if a retested sample
    // is approved again, not a per-signing-event audit row.
    public string? CertificateRemarks { get; set; }

    // Set only on a Sample spun up as an OOS retest (RetestRetainedSample's
    // new sample, or either of NewSampleRequest's two new samples) -
    // points back at the Sample whose approval decision created it. Null
    // for every ordinarily-received Sample. Doubles as the OOS tracking
    // page's query key (WHERE OriginSampleId IS NOT NULL).
    public int? OriginSampleId { get; set; }
    public Sample? OriginSample { get; set; }

    // System-generated group code for out-of-specification investigation tracking (e.g. OOS0826001).
    // Generated on the root sample upon its first OOS decision and inherited by all descendant retests.
    public string? OosGroupCode { get; set; }

    public List<TestOrder> TestOrders { get; set; } = new();
    public List<SampleLocation> Locations { get; set; } = new();
    public SamplePreparation? SamplePreparation { get; set; }
}
