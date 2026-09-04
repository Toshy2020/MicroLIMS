using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentTrainingAssignment
{
    public int Id { get; set; }

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public int AssignedUserId { get; set; }
    public User AssignedUser { get; set; } = null!;

    public AssignmentType AssignmentType { get; set; } = AssignmentType.Reading;
    public TrainingAssignmentStatus Status { get; set; } = TrainingAssignmentStatus.Assigned;

    public DateTime AssignedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime DueDateUtc { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? StatementText { get; set; }
    public int? AcknowledgedByUserId { get; set; }
    public User? AcknowledgedByUser { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? SupersededAtUtc { get; set; }
    public string? ClosedReason { get; set; }

    public int? SourceAssignmentId { get; set; }
    public DocumentTrainingAssignment? SourceAssignment { get; set; }

    public string? AssignmentReason { get; set; }

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int? ModifiedByUserId { get; set; }
    public User? ModifiedByUser { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }

    public DocumentAcknowledgementRecord? AcknowledgementRecord { get; set; }
}
