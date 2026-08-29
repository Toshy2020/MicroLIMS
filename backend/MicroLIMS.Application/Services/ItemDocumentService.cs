using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class ItemDocumentService
{
    private readonly MicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ILogger<ItemDocumentService> _logger;

    public ItemDocumentService(
        MicroLimsDbContext db,
        IFileStorageService storage,
        ILogger<ItemDocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<List<ItemDocumentDto>> GetDocumentsForItemAsync(int itemId)
    {
        var itemExists = await _db.Items.AnyAsync(i => i.Id == itemId);
        if (!itemExists)
            throw new InvalidOperationException($"Item {itemId} not found.");

        var docs = await _db.ItemDocuments
            .Include(d => d.UploadedByUser)
            .Where(d => d.ItemId == itemId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return docs.Select(MapToDto).ToList();
    }

    public async Task<ItemDocumentDto> UploadDocumentAsync(
        int itemId,
        ItemDocumentType documentType,
        string version,
        DateTime? effectiveDate,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength,
        int userId)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found.");

        if (fileLength <= 0 || fileLength > 25 * 1024 * 1024)
            throw new InvalidOperationException("File size must be between 1 byte and 25 MB.");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = ".pdf";

        // Read stream into byte array and calculate SHA256 hash
        byte[] contentBytes;
        using (var ms = new MemoryStream())
        {
            await fileStream.CopyToAsync(ms);
            contentBytes = ms.ToArray();
        }

        string sha256Hex;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(contentBytes);
            sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        var storageKey = $"item-documents/{itemId}/{Guid.NewGuid():N}{ext}";
        var savedPath = await _storage.SaveAsync(storageKey, contentBytes);

        // The date input arrives with DateTimeKind.Unspecified (no timezone
        // in a plain HTML date value) - Npgsql refuses to write that into a
        // "timestamp with time zone" column, so treat it as UTC explicitly.
        if (effectiveDate.HasValue && effectiveDate.Value.Kind == DateTimeKind.Unspecified)
            effectiveDate = DateTime.SpecifyKind(effectiveDate.Value, DateTimeKind.Utc);

        // Find existing Current document of the same type to supersede
        var existingCurrentDoc = await _db.ItemDocuments
            .FirstOrDefaultAsync(d => d.ItemId == itemId && d.DocumentType == documentType && d.Status == MaterialDocumentStatus.Current);

        var newDoc = new ItemDocument
        {
            ItemId = itemId,
            DocumentType = documentType,
            OriginalFileName = originalFileName,
            StorageKey = savedPath,
            FileExtension = ext,
            ContentType = contentType,
            FileSizeBytes = fileLength,
            ContentSha256 = sha256Hex,
            Version = string.IsNullOrWhiteSpace(version) ? "Rev 01" : version,
            EffectiveDate = effectiveDate,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        };

        _db.ItemDocuments.Add(newDoc);
        await _db.SaveChangesAsync();

        if (existingCurrentDoc != null)
        {
            existingCurrentDoc.Status = MaterialDocumentStatus.Superseded;
            existingCurrentDoc.SupersededByDocumentId = newDoc.Id;
            existingCurrentDoc.SupersededAt = DateTime.UtcNow;
            existingCurrentDoc.SupersededByUserId = userId;
            existingCurrentDoc.SupersessionReason = $"Superseded by new version '{newDoc.Version}'";
            await _db.SaveChangesAsync();
        }

        var uploader = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        newDoc.UploadedByUser = uploader;

        _logger.LogInformation("Item {ItemId} document uploaded: ID {DocId}, Type {Type}, Version {Version}", itemId, newDoc.Id, documentType, newDoc.Version);
        return MapToDto(newDoc);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetDocumentContentAsync(int documentId, int userId, bool isDownload)
    {
        var doc = await _db.ItemDocuments.FirstOrDefaultAsync(d => d.Id == documentId)
            ?? throw new InvalidOperationException($"Document {documentId} not found.");

        _db.ItemDocumentAccessLogs.Add(new ItemDocumentAccessLog
        {
            DocumentId = documentId,
            UserId = userId,
            Action = isDownload ? MaterialDocumentAccessAction.Download : MaterialDocumentAccessAction.View,
            AccessedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var bytes = await _storage.ReadAsync(doc.StorageKey);
        return (new MemoryStream(bytes), doc.ContentType, doc.OriginalFileName);
    }

    private static ItemDocumentDto MapToDto(ItemDocument doc) =>
        new ItemDocumentDto(
            doc.Id,
            doc.ItemId,
            doc.DocumentType,
            doc.OriginalFileName,
            doc.Version,
            doc.EffectiveDate,
            doc.FileSizeBytes,
            doc.UploadedByUserId,
            doc.UploadedByUser?.FullName ?? $"User #{doc.UploadedByUserId}",
            doc.UploadedAt,
            doc.Status,
            doc.SupersededByDocumentId,
            doc.SupersededAt
        );
}
