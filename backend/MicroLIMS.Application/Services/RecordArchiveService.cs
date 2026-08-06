using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Freezes a rendered PDF of a record at the moment its final decision is
// signed, stores the bytes, and records a SHA-256 of them so the archive
// is tamper-evident.
//
// Archiving must never be able to undo a decision that has already been
// signed: if rendering or disk IO fails, the failure is logged and the
// decision stands. A missing archive is a recoverable gap (it can be cut
// again); a rolled-back approval after the signature was written would be
// a far worse outcome.
public class RecordArchiveService
{
    private readonly MicroLimsDbContext _db;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IFileStorageService _storage;
    private readonly ILogger<RecordArchiveService> _logger;

    public RecordArchiveService(MicroLimsDbContext db, IPdfGenerator pdfGenerator, IFileStorageService storage, ILogger<RecordArchiveService> logger)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ArchivedRecord?> ArchiveAsync(
        string entityType, int entityId, ReportDocument document, string reason, int userId)
    {
        try
        {
            var bytes = await _pdfGenerator.GenerateReportAsync(document);

            var safeId = new string(document.DocumentId.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
            var fileName = $"{entityType}_{safeId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            var path = await _storage.SaveAsync(fileName, bytes);

            var performedByName = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? "Unknown";

            var archived = new ArchivedRecord
            {
                EntityType = entityType,
                EntityId = entityId,
                DocumentId = document.DocumentId,
                FileName = fileName,
                StoragePath = path,
                SizeBytes = bytes.LongLength,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                Reason = reason,
                GeneratedByUserId = userId,
                GeneratedByNameSnapshot = performedByName
            };

            _db.ArchivedRecords.Add(archived);
            await _db.SaveChangesAsync();
            return archived;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to archive {EntityType} {EntityId} ({DocumentId}) after: {Reason}. " +
                "The decision itself stands - the archived copy can be regenerated.",
                entityType, entityId, document.DocumentId, reason);
            return null;
        }
    }

    public Task<List<ArchivedRecord>> GetForEntityAsync(string entityType, int entityId) =>
        _db.ArchivedRecords
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.GeneratedAt)
            .ToListAsync();

    // Reads an archived file back and re-hashes it. A mismatch means the
    // stored file no longer matches what was signed for.
    public async Task<(ArchivedRecord Record, byte[] Bytes, bool IntegrityOk)?> ReadAsync(int archivedRecordId)
    {
        var record = await _db.ArchivedRecords.FirstOrDefaultAsync(a => a.Id == archivedRecordId);
        if (record is null) return null;

        var bytes = await _storage.ReadAsync(record.StoragePath);
        var ok = Convert.ToHexString(SHA256.HashData(bytes)) == record.ContentSha256;
        if (!ok)
            _logger.LogError("Archived record {Id} ({DocumentId}) failed its integrity check - stored bytes do not match the recorded SHA-256.",
                record.Id, record.DocumentId);

        return (record, bytes, ok);
    }
}
