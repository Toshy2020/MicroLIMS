using System;

namespace MicroLIMS.Domain.Entities;

public class DocumentAcknowledgementRecord
{
    public int Id { get; set; }

    public int DocumentTrainingAssignmentId { get; set; }
    public DocumentTrainingAssignment DocumentTrainingAssignment { get; set; } = null!;

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public int AcknowledgedByUserId { get; set; }
    public User AcknowledgedByUser { get; set; } = null!;

    public string StatementText { get; set; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; set; } = DateTime.UtcNow;

    public int? ControlledFileId { get; set; }
    public RevisionFile? ControlledFile { get; set; }
    public string? ControlledFileHash { get; set; }

    public string? Comments { get; set; }
    public string? ClientIpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
