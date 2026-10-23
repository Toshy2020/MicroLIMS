using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class CalendarEventDocument
{
    public int Id { get; set; }

    public int CalendarEventId { get; set; }
    public CalendarEvent? CalendarEvent { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public MaterialDocumentStatus Status { get; set; } = MaterialDocumentStatus.Current;

    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
