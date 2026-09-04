namespace MicroLIMS.Domain.Entities;

// Frozen Principle #5 - Traceability. Every change records User, Date,
// Time, Previous value, New value. Nothing is ever deleted.
//
// The reference columns below are populated automatically at capture
// time (see MicroLimsDbContext.CaptureAuditEntries) whenever the
// audited entity carries one of these values, so the Audit Search
// screen can filter by any of them without dynamic cross-table joins.
public class AuditLog
{
    public int Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public int? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? BatchNumber { get; set; }
    public string? ControlNumber { get; set; }
    public string? SampleReferenceNumber { get; set; }
    public string? MediaLotNumber { get; set; }
    public string? ReferenceStrainCode { get; set; }
    public string? CryovialCode { get; set; }
    public int? SampleId { get; set; }
    public int? TestOrderId { get; set; }

    // Semantic Event Layer (Release 1a - Document Control Foundation)
    public string? EventUid { get; set; }
    public Enums.ActorType? ActorType { get; set; }
    public string? SystemProcessName { get; set; }
    public string? ActionCode { get; set; }
    public Enums.AuditActionCategory? ActionCategory { get; set; }
    public string? Reason { get; set; }
    public string? SourceContext { get; set; }
    public Guid? CorrelationId { get; set; }
    public int? DocumentMasterId { get; set; }
    public int? DocumentRevisionId { get; set; }

    public ICollection<AuditEventChange> Changes { get; set; } = new List<AuditEventChange>();
}
