using MicroLIMS.Domain.Attributes;

namespace MicroLIMS.Domain.Entities;

public class User : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    [AuditRedacted]
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; } = true;
    // When the account was last deactivated; cleared on reactivation. Measures how long the user's
    // document responsibilities have waited for reassignment. Null for accounts deactivated before
    // this was recorded (the migration backfills it from the audit trail where it can).
    public DateTime? DeactivatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Account locking (gap analysis "Missing Security").
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool IsLocked => LockedUntil is not null && LockedUntil > DateTime.UtcNow;

    // Password policy + login tracking.
    public DateTime? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
}
