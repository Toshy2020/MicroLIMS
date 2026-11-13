using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

public class MfaRecoveryCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [AuditRedacted]
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
