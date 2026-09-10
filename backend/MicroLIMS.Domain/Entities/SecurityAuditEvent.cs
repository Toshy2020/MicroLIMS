using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One meaningful authentication / identity / session event - the Security
// Audit Trail, which is a separate domain from the GxP audit trail.
//
// NOT a GxP record, and equally NOT ordinary debug logging: this is
// security evidence. It is excluded from CaptureAuditEntries (see
// MicroLimsDbContext) alongside Incident and ErrorLog, both so that a
// security event never becomes regulated quality evidence and so that
// writing one cannot recursively audit itself.
//
// What lives here versus AuditLog:
//   AuditLog            - regulated laboratory and quality activity:
//                         samples, results, review, approval, signatures,
//                         controlled documents. Immutable by DB trigger.
//   SecurityAuditEvent  - who signed in, whose access was withdrawn, whose
//                         session was revoked. Append-only by convention.
//
// Rows record *events*, never database CRUD. "RefreshToken UPDATE" is not
// a security event; AUTH_SESSION_REVOKED is.
//
// Never store a credential here - no passwords, no raw refresh tokens, no
// JWTs, no signing keys. Nothing on this entity is shaped to hold one.
public class SecurityAuditEvent
{
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // One of SecurityEventCodes - e.g. AUTH_LOGIN_SUCCESS. Stored as text
    // rather than an enum so adding a code needs no migration, matching
    // how AuditLog.ActionCode is handled.
    public string EventCode { get; set; } = string.Empty;

    public SecurityEventOutcome Outcome { get; set; } = SecurityEventOutcome.Success;

    // Who performed the action. Null when nobody is authenticated yet (a
    // failed login) or when the system itself acted (automatic lockout) -
    // ActorIsSystem distinguishes those two cases.
    public int? ActorUserId { get; set; }
    public User? Actor { get; set; }

    public bool ActorIsSystem { get; set; }

    // Who the action was done to. Equal to ActorUserId for self-service
    // events such as a user changing their own password; different when an
    // administrator acts on somebody else (see AUTH_ACCOUNT_DISABLED).
    public int? TargetUserId { get; set; }
    public User? Target { get; set; }

    // The username as supplied or as it stood at the time. Kept as a
    // snapshot because a failed login may name an account that does not
    // exist, and because a user can later be renamed or deleted.
    public string? TargetUsername { get; set; }

    // The per-request id from CorrelationIdMiddleware. Deliberately the
    // technical string id shared with Incident/ErrorLog, NOT the Guid
    // AuditLog.CorrelationId used to group GxP document-control events -
    // the two are different systems and must never be mixed.
    public string? CorrelationId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestPath { get; set; }

    // Where the event came from - "Api" for a request-driven event,
    // "System" for one raised by the application itself.
    public string Source { get; set; } = string.Empty;

    // Short human-readable justification or cause: the reason an admin
    // typed when disabling an account, or "Automatic lockout after
    // repeated failed logins". Free text, never a credential.
    public string? Reason { get; set; }

    // Optional structured detail, e.g. { "revokedSessionCount": 3 }.
    // jsonb, matching ErrorLog.RawContext.
    public string? Metadata { get; set; }
}
