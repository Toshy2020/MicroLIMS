namespace MicroLIMS.Domain.Entities;

// An immutable PDF of a record, frozen at the moment its final decision
// was signed. GMP expects the released version of a record to be
// reproducible exactly as issued, even if the underlying rows are later
// amended - re-rendering on demand would silently reflect those
// amendments, so the bytes are stored instead.
//
// APPEND-ONLY, like ElectronicSignature: no service method or endpoint
// updates or deletes a row here, and none should be added. ContentSha256
// is what makes that verifiable - if the stored file no longer hashes to
// this value, the archive has been tampered with.
public class ArchivedRecord
{
    public int Id { get; set; }

    // Same (EntityType, EntityId) vocabulary as ElectronicSignature and
    // ReviewWorkflowEvent, so one record's signatures, lifecycle events
    // and archived documents are all reachable by the same pair of keys.
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }

    // The human-facing identifier printed on the document itself
    // (sample reference number, media lot number, cryovial code).
    public string DocumentId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;

    // What decision caused this archive to be cut, so an auditor can see
    // why this version exists without opening the file.
    public string Reason { get; set; } = string.Empty;

    public int GeneratedByUserId { get; set; }
    public string GeneratedByNameSnapshot { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
