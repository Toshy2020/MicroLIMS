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

    // Release 1d: Explicit sequential version within (DocumentRevisionId, FileRole)
    public int FileVersion { get; set; } = 1;

    // Release 1d: Identifies the final approved Word source file for the revision (strictly SourceFile)
    public bool IsApprovedFinalSource { get; set; } = false;

    // Release 1d: Provenance linkage - for ControlledPdf, references the exact Word source used for generation
    public int? GeneratedFromSourceFileId { get; set; }
    public RevisionFile? GeneratedFromSourceFile { get; set; }

    public int? SupersededByFileId { get; set; }
    public RevisionFile? SupersededByFile { get; set; }

    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
