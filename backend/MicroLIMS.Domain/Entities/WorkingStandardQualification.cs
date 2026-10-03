using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One qualification or requalification of an in-house working standard
// (spec 2026-10-03). The assay runs on an HPLC run through an HplcRunSample
// whose WorkingStandardQualificationId points here. Approval creates the
// WorkingStandard Material lot (Initial) or updates it (Requalification).
// Audited by MicroLimsDbContext.SaveChanges like every entity.
public class WorkingStandardQualification : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public WorkingStandardQualificationKind Kind { get; set; }
    public WorkingStandardQualificationStatus Status { get; set; }

    // Initial: null until approval. Requalification: the lot being requalified.
    public int? WorkingStandardMaterialId { get; set; }
    public Material? WorkingStandardMaterial { get; set; }

    public int MaterialMasterEntryId { get; set; } // ReferenceStandard category
    public MaterialMasterEntry? MaterialMasterEntry { get; set; }

    public int? SourceSampleId { get; set; } // received RawMaterial sample with an approved assay
    public Sample? SourceSample { get; set; }
    public string SourceMaterialName { get; set; } = string.Empty;
    public string SourceBatchNumber { get; set; } = string.Empty;

    public decimal? QuantityGrams { get; set; } // Initial only
    public string? Location { get; set; }       // Initial only
    public decimal? MoisturePercent { get; set; }

    // Set at assignment: the method analyte whose standard entry matches.
    public int? HplcMethodAnalyteId { get; set; }

    public decimal? MeanAssayPercent { get; set; }
    public decimal? RsdPercent { get; set; }
    public decimal? PotencyPercent { get; set; }
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
    public string? ReplicateAssaysJson { get; set; } // per-replicate as-is assay %, for the record

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? PreparedByUserId { get; set; }
    public DateTime? PreparedAt { get; set; }
    public int? PreparedSignatureId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedSignatureId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedSignatureId { get; set; }
    public int? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedSignatureId { get; set; }
    public string? RejectReason { get; set; }
    public string? ReturnReason { get; set; } // last return to Draft

    public List<WorkingStandardDocument> Documents { get; set; } = new();

    public bool IsOpen => Status is WorkingStandardQualificationStatus.Draft
        or WorkingStandardQualificationStatus.Assayed or WorkingStandardQualificationStatus.Reviewed;
}

// Attachment on a qualification. Never replaced in place: a new upload of the
// same kind sets SupersededByDocumentId on the previous current one.
public class WorkingStandardDocument
{
    public int Id { get; set; }
    public int WorkingStandardQualificationId { get; set; }
    public WorkingStandardQualification? WorkingStandardQualification { get; set; }
    public WorkingStandardDocumentKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int? SupersededByDocumentId { get; set; }
}
