using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Per-section review and approval of a sample. Each laboratory section
// reviews and approves only its own tests; Sample.Status is rolled up from
// these rows (SampleSectionRollup). A row is created the first time the
// section reaches review - until then the section counts as InTesting.
//
// The signatures themselves stay on the Sample (EntityType "Sample") so the
// audit trail, summary and traceability views keep finding them; the two
// *SignatureId columns tie each one to the section it was given for.
public class SampleSectionSignoff
{
    public int Id { get; set; }

    public int SampleId { get; set; }
    public Sample? Sample { get; set; }

    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    public SectionSignoffStatus Status { get; set; } = SectionSignoffStatus.InTesting;

    // Clock start for this section's review queue.
    public DateTime? SubmittedForReviewAt { get; set; }

    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewSignatureId { get; set; }
    public ElectronicSignature? ReviewSignature { get; set; }

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ApprovalDecision? ApprovalDecision { get; set; }
    public int? ApprovalSignatureId { get; set; }
    public ElectronicSignature? ApprovalSignature { get; set; }

    // Printed under this section's block on the Certificate of Analysis.
    public string? CertificateRemarks { get; set; }
}
