namespace MicroLIMS.Domain.Entities;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsValid => UsedAt is null && ExpiresAt > DateTime.UtcNow;
}
