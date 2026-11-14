using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Operational email send queue. Written inside the same transaction as its
// notification (or digest/system email) and processed after commit by the
// background worker with retry backoff.
public class EmailOutbox
{
    public int Id { get; set; }
    public int? NotificationLogId { get; set; }
    public NotificationLog? NotificationLog { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public EmailOutboxKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? LastError { get; set; }
}
