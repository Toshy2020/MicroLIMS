using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

/// <summary>
/// Short-lived OIDC authorization state persisted across the provider redirect.
/// </summary>
public class SsoPendingRequest
{
    public int Id { get; set; }
    [AuditRedacted]
    public string StateHash { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    [AuditRedacted]
    public string CodeVerifier { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
