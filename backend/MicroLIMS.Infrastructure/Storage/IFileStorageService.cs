namespace MicroLIMS.Infrastructure.Storage;

// The seam between the application and wherever files physically live.
// Nothing outside the implementations touches File, FileStream or
// Directory, and only the DI registration names a concrete class.
//
// Contract:
// - SaveAsync returns the key callers persist. It is relative
//   ("documents/2/2_controlledpdf.pdf"), never a location, so moving files
//   does not invalidate stored references.
// - ReadAsync accepts that key, and also the base-path-combined values that
//   rows written before keys were relative still hold (see StorageKey).
// - ReadAsync throws StoredFileNotFoundException when no bytes exist for
//   the key, whichever provider is in use.
//
// Implementations: LocalFileStorageService (development) and
// S3FileStorageService (production, on Backblaze B2). What is still
// outstanding is recorded in docs/Storage_Migration_Implications.md.
public interface IFileStorageService
{
    Task<string> SaveAsync(string fileName, byte[] content);
    Task<byte[]> ReadAsync(string path);
}
