using MicroLIMS.Domain.Attributes;
using MicroLIMS.Domain.Enums;

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
    // Takes the current time from the caller's injected clock (CLAUDE.md Rule 5.6).
    public bool IsLockedAt(DateTime nowUtc) => LockedUntil is not null && LockedUntil > nowUtc;

    // Password policy + login tracking.
    public DateTime? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? MfaRequiredSince { get; set; }

    // Single Sign-On linking + emergency access.
    public SignInMethod SignInMethod { get; set; } = SignInMethod.Local;
    public string? DirectoryUsername { get; set; }
    public string? DirectorySubjectId { get; set; }
    public bool IsEmergencyAccount { get; set; }
    public DateTime? SigningPasswordSetAt { get; set; }
    public DateTime? DirectorySignInRefusedAt { get; set; }

    /// <summary>
    /// Normalizes a company directory username for case-insensitive matching and unique storage.
    /// </summary>
    public static string? NormalizeDirectoryUsername(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
