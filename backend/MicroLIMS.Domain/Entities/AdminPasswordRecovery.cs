using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Dedicated entity for administrator-assisted password recovery requests.
// Stores SHA-256 hash of the one-time code (never plaintext).
public class AdminPasswordRecovery
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedAttempts { get; set; }
    public AdminPasswordRecoveryStatus Status { get; set; } = AdminPasswordRecoveryStatus.Pending;
    public string Reason { get; set; } = string.Empty;
}
