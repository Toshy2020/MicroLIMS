using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class RevisionFile
{
    public int Id { get; set; }

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public FileRole FileRole { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int? SupersededByFileId { get; set; }
    public RevisionFile? SupersededByFile { get; set; }

    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
