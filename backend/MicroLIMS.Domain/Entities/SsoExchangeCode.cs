using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

/// <summary>
/// Single-use short-lived exchange code returned in the frontend redirect fragment
/// after an OIDC callback so MicroLIMS tokens never appear in a URL.
/// </summary>
public class SsoExchangeCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [AuditRedacted]
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
