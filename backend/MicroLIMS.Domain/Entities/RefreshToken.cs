namespace MicroLIMS.Domain.Entities;

// Long-lived opaque token that can be exchanged for a new short-lived
// JWT without re-entering a password. One row per issued token so any
// individual token (or all of a user's tokens) can be revoked.
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
