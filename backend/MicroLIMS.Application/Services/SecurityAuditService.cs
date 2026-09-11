using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Writes the Security Audit Trail.
//
// SecurityAuditEvent is on the CaptureAuditEntries deny-list, so nothing
// written here reaches the GxP audit trail - this is security evidence,
// kept separate from regulated quality evidence in both directions.
public class SecurityAuditService : ISecurityAuditService
{
    // Match the column lengths in SecurityAuditEventConfiguration. An
    // oversized value would fail the insert, and losing the tail of a
    // reason beats losing the whole security event.
    private const int EventCodeMaxLength = 100;
    private const int CorrelationIdMaxLength = 100;
    private const int UsernameMaxLength = 256;
    private const int IpAddressMaxLength = 100;
    private const int UserAgentMaxLength = 500;
    private const int RequestPathMaxLength = 500;
    private const int SourceMaxLength = 50;
    private const int ReasonMaxLength = 1000;

    private readonly MicroLimsDbContext _db;
    private readonly ISecurityRequestContext _requestContext;

    public SecurityAuditService(MicroLimsDbContext db, ISecurityRequestContext requestContext)
    {
        _db = db;
        _requestContext = requestContext;
    }

    // Appended without saving - see ISecurityAuditService for why.
    public void Record(SecurityEventRequest request)
    {
        _db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            EventCode = Trim(request.EventCode, EventCodeMaxLength) ?? string.Empty,
            Outcome = request.Outcome,
            ActorUserId = request.ActorUserId,
            ActorIsSystem = request.ActorIsSystem,
            TargetUserId = request.TargetUserId,
            TargetUsername = Trim(request.TargetUsername, UsernameMaxLength),
            CorrelationId = Trim(_requestContext.CorrelationId, CorrelationIdMaxLength),
            IpAddress = Trim(_requestContext.IpAddress, IpAddressMaxLength),
            UserAgent = Trim(_requestContext.UserAgent, UserAgentMaxLength),
            RequestPath = Trim(_requestContext.RequestPath, RequestPathMaxLength),
            Source = Trim(_requestContext.Source, SourceMaxLength) ?? "System",
            Reason = Trim(request.Reason, ReasonMaxLength),
            Metadata = request.Metadata
        });
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

// Used when no request is in flight - background work, and the unit tests
// that exercise the services directly.
public sealed class SystemSecurityRequestContext : ISecurityRequestContext
{
    public string? CorrelationId => null;
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string? RequestPath => null;
    public string Source => "System";
}
