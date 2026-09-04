namespace MicroLIMS.Domain.Entities;

public class RevisionImpactAssessment
{
    public int Id { get; set; }

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    // 1. Procedure or Method
    public bool ProcedureOrMethodImpact { get; set; }
    public string? ProcedureOrMethodDetails { get; set; }

    // 2. Training
    public bool TrainingImpact { get; set; }
    public string? TrainingDetails { get; set; }

    // 3. Forms or Templates
    public bool FormsOrTemplatesImpact { get; set; }
    public string? FormsOrTemplatesDetails { get; set; }

    // 4. Specifications
    public bool SpecificationsImpact { get; set; }
    public string? SpecificationsDetails { get; set; }

    // 5. Equipment
    public bool EquipmentImpact { get; set; }
    public string? EquipmentDetails { get; set; }

    // 6. Materials or Media
    public bool MaterialsOrMediaImpact { get; set; }
    public string? MaterialsOrMediaDetails { get; set; }

    // 7. Validation
    public bool ValidationImpact { get; set; }
    public string? ValidationDetails { get; set; }

    // 8. Regulatory Commitment
    public bool RegulatoryCommitmentImpact { get; set; }
    public string? RegulatoryCommitmentDetails { get; set; }

    // 9. Related Documents
    public bool RelatedDocumentsImpact { get; set; }
    public string? RelatedDocumentsDetails { get; set; }

    public bool IsComplete { get; set; }

    public int CompletedByUserId { get; set; }
    public User CompletedByUser { get; set; } = null!;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
