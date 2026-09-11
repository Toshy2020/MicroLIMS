using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

// Ambient details of the request a security event happened during.
// Defined here rather than taken from HttpContext because the Application
// layer has no ASP.NET reference; the API layer supplies the HTTP-backed
// implementation and tests supply a stub.
public interface ISecurityRequestContext
{
    // The technical per-request id from CorrelationIdMiddleware - the same
    // id Incident/ErrorLog use, NOT the GxP AuditLog Guid.
    string? CorrelationId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? RequestPath { get; }

    // "Api" when a request is in flight, "System" otherwise.
    string Source { get; }
}

// One meaningful security event to record. Note what is absent: there is
// no field for a token, a password, or any other credential, so none can
// reach the trail by accident.
public record SecurityEventRequest(
    string EventCode,
    SecurityEventOutcome Outcome = SecurityEventOutcome.Success,
    int? ActorUserId = null,
    bool ActorIsSystem = false,
    int? TargetUserId = null,
    string? TargetUsername = null,
    string? Reason = null,
    string? Metadata = null);

// Writes the Security Audit Trail.
//
// Records are appended to the change tracker WITHOUT saving, so a security
// event commits in the same transaction as the state change that caused
// it - an account disable and its AUTH_ACCOUNT_DISABLED event either both
// land or neither does. Callers that are not already saving must call
// SaveChangesAsync themselves.
public interface ISecurityAuditService
{
    void Record(SecurityEventRequest request);
}
