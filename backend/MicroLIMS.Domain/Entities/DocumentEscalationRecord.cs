using System;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentEscalationRecord
{
    public int Id { get; set; }

    public int DocumentTrainingAssignmentId { get; set; }
    public DocumentTrainingAssignment DocumentTrainingAssignment { get; set; } = null!;

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public int AssignedUserId { get; set; }
    public User AssignedUser { get; set; } = null!;

    public AssignmentType AssignmentType { get; set; } = AssignmentType.Reading;
    public DateTime DueDateUtc { get; set; }

    public DocumentEscalationLevel EscalationLevel { get; set; }
    public DocumentEscalationStatus Status { get; set; } = DocumentEscalationStatus.Raised;

    public DateTime ScheduledTriggerUtc { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

    public string RecipientRoleOrTarget { get; set; } = "AssignedUser";
    public string EscalationReason { get; set; } = string.Empty;

    public DateTime? ResolvedAtUtc { get; set; }
    public int? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }
    public string? ResolutionReason { get; set; }

    public string ProcessName { get; set; } = "DocumentEffectiveDateWorker";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
