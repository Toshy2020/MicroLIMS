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

    // The sample and test a notification is about, when it is about exactly
    // one - lets the client open that sample/test directly. Plain columns with
    // no foreign key, like UserId: a notification records what was said, it is
    // not a dependent of the sample.
    public int? SampleId { get; set; }
    public int? TestOrderId { get; set; }

    public string Severity { get; set; } = "info";
    public bool IsRead { get; set; }
    public bool EmailSent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
