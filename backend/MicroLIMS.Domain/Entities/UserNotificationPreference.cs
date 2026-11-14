using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Per-user email delivery channel preference for a notification catalogue type.
// In-app delivery is always enabled and cannot be turned off.
public class UserNotificationPreference
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Type { get; set; } = string.Empty;
    public EmailMode EmailMode { get; set; }
}
