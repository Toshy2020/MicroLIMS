using MicroLIMS.Domain.Attributes;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class MfaChallenge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [AuditRedacted]
    public string TokenHash { get; set; } = string.Empty;
    public MfaChallengePurpose Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
