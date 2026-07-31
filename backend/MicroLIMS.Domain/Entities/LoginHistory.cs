namespace MicroLIMS.Domain.Entities;

// One login attempt (success or failure) - required for GMP audit of
// system access (gap analysis "Missing Security - Audit login history").
public class LoginHistory
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
