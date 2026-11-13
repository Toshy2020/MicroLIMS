using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

public class TrustedDevice
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [AuditRedacted]
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? CreatedIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
}
