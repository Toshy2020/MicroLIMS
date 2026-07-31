namespace MicroLIMS.Domain.Entities;

// A persisted, per-user notification - lets "unread" state and delivery
// history survive across sessions, unlike the on-the-fly computed list
// DashboardNotificationService returns for the dashboard widget.
public class NotificationLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty; // MediaExpiry, IncubationReady, ApprovalWaiting, ReviewWaiting
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public bool IsRead { get; set; }
    public bool EmailSent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
