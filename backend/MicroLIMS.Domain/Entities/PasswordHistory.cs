namespace MicroLIMS.Domain.Entities;

// One row per password a user has ever set, so ChangePasswordAsync /
// ConfirmPasswordResetAsync can reject reuse of the last 5.
public class PasswordHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
