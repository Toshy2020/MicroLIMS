namespace MicroLIMS.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Account locking (gap analysis "Missing Security").
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool IsLocked => LockedUntil is not null && LockedUntil > DateTime.UtcNow;
}
