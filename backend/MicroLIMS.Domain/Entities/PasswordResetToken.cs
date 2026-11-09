using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [AuditRedacted]
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Takes the current time from the caller's injected clock (CLAUDE.md Rule 5.6).
    public bool IsValidAt(DateTime nowUtc) => UsedAt is null && ExpiresAt > nowUtc;
}
