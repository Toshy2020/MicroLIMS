using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A single captured error, always attached to a parent Incident.
//
// Operational/technical data only - NOT a GxP record. Deliberately
// excluded from CaptureAuditEntries (see MicroLimsDbContext); see the
// note on Incident for why.
public class ErrorLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public ErrorSource Source { get; set; }

    // Auto-classified at write time (Phase 2). SeverityOverride is set
    // only when an admin disagrees with the classification; the effective
    // severity is SeverityOverride ?? Severity.
    public ErrorSeverity Severity { get; set; }
    public ErrorSeverity? SeverityOverride { get; set; }

    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }

    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }

    // Who was performing the action, when known. Null for anonymous
    // requests and for background/database-sourced entries.
    public int? UserId { get; set; }
    public User? User { get; set; }

    // Matches Incident.CorrelationId - see the note there.
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // jsonb. Extra structured context that varies by Source: browser and
    // user agent for Frontend, query text for Database, and so on.
    public string? RawContext { get; set; }
}
