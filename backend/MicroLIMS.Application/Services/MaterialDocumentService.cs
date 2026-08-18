using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// ---- DTOs ----

public record UploadMaterialDocumentRequest(
    MaterialDocumentType DocumentType,
    string OriginalFileName,
    string DeclaredContentType,
    byte[] Content);

public record SupersedeMaterialDocumentRequest(
    string OriginalFileName,
    string DeclaredContentType,
    byte[] Content,
    string Reason);

public record VoidMaterialDocumentRequest(string Reason);

// ---- Document metadata DTO returned to clients ----

public record MaterialDocumentDto(
    int Id,
    int MaterialId,
    MaterialDocumentType DocumentType,
    string OriginalFileName,
    string FileExtension,
    string ContentType,
    long FileSizeBytes,
    string ContentSha256,
    int UploadedByUserId,
    string UploadedByName,
    DateTime UploadedAt,
    MaterialDocumentStatus Status,
    int? SupersededByDocumentId,
    DateTime? SupersededAt,
    int? SupersededByUserId,
    string? SupersessionReason,
    DateTime? VoidedAt,
    int? VoidedByUserId,
    string? VoidReason);

// ---- COA eligibility result ----

public record CoeEligibilityResult(bool IsEligible, bool CoaRequired, bool HasCurrentCoa);

// ---- Service ----

// Manages material lot documents (COA, SDS, etc.).
// All file I/O flows through IFileStorageService so the storage backend
// (local filesystem, Azure Blob, S3) is interchangeable without changing
// this service.
//
// Security invariants:
//  - StorageKey is generated server-side; user-provided filenames are
//    never used as filesystem paths.
//  - Callers receive metadata DTOs, never raw storage paths.
//  - File integrity (SHA-256) is verified on every content retrieval.
//  - Every file access is recorded in MaterialDocumentAccessLog.
public class MaterialDocumentService
{
    private static readonly HashSet<MaterialType> CoaRequiredTypes = new()
    {
        MaterialType.DehydratedMedia,
        MaterialType.LyophilizedMicroorganism,
        MaterialType.Supplement
    };

    private readonly MicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly MaterialDocumentFileValidator _validator;
    private readonly ILogger<MaterialDocumentService> _logger;

    public MaterialDocumentService(
        MicroLimsDbContext db,
        IFileStorageService storage,
        MaterialDocumentFileValidator validator,
        ILogger<MaterialDocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _validator = validator;
        _logger = logger;
    }

    // ---- List ----

    public async Task<List<MaterialDocumentDto>> GetDocumentsAsync(int materialId, int requestingUserId)
    {
        // Verify material exists and record a View access event.
        var materialExists = await _db.Materials.AnyAsync(m => m.Id == materialId);
        if (!materialExists)
            throw new InvalidOperationException($"Material {materialId} not found.");

        var docs = await _db.MaterialDocuments
            .Where(d => d.MaterialId == materialId)
            .OrderBy(d => d.Status == MaterialDocumentStatus.Current ? 0 : 1)   // Current first
            .ThenByDescending(d => d.UploadedAt)
            .ToListAsync();

        var userIds = docs.Select(d => d.UploadedByUserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        await RecordAccessAsync(0, materialId, requestingUserId, MaterialDocumentAccessAction.View);

        return docs.Select(d => ToDto(d, userMap.GetValueOrDefault(d.UploadedByUserId, "Unknown"))).ToList();
    }

    // ---- Upload ----

    public async Task<MaterialDocumentDto> UploadAsync(int materialId, UploadMaterialDocumentRequest request, int uploadingUserId)
    {
        // Verify material.
        var material = await _db.Materials.FindAsync(materialId)
            ?? throw new InvalidOperationException($"Material {materialId} not found.");

        // Server-side file validation.
        var firstBytes = request.Content.Take(16).ToArray();
        var error = _validator.Validate(request.OriginalFileName, request.DeclaredContentType, request.Content.Length, firstBytes);
        if (error != null)
            throw new InvalidOperationException(error);

        var ext = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        var sha256 = Convert.ToHexString(SHA256.HashData(request.Content));

        // Allocate a temporary document so we have an ID for the storage key.
        // We insert with a placeholder StorageKey, then update it after we know the Id.
        var document = new MaterialDocument
        {
            MaterialId = materialId,
            DocumentType = request.DocumentType,
            OriginalFileName = SanitiseFileName(request.OriginalFileName),
            StorageKey = "pending",  // overwritten below
            FileExtension = ext,
            ContentType = NormaliseMime(request.DeclaredContentType),
            FileSizeBytes = request.Content.Length,
            ContentSha256 = sha256,
            UploadedByUserId = uploadingUserId,
            UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        };

        _db.MaterialDocuments.Add(document);
        await _db.SaveChangesAsync();

        // Generate final storage key using the document Id (collision-free, no path traversal).
        var storageKey = $"material-documents/{materialId}/{document.Id}{ext}";
        string? savedPath = null;
        try
        {
            savedPath = await _storage.SaveAsync(storageKey, request.Content);
            document.StorageKey = savedPath;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File storage failed for MaterialDocument {Id} (material {MaterialId}). Rolling back DB record.", document.Id, materialId);
            // Clean up the DB record — the file either was not written or the path is unknown.
            _db.MaterialDocuments.Remove(document);
            try { await _db.SaveChangesAsync(); } catch { /* best-effort */ }
            throw new InvalidOperationException("Document storage failed. The upload was not completed.", ex);
        }

        await RecordAccessAsync(document.Id, materialId, uploadingUserId, MaterialDocumentAccessAction.Upload);

        _logger.LogInformation("MaterialDocument {Id} uploaded for material {MaterialId} by user {UserId}.", document.Id, materialId, uploadingUserId);

        return await LoadDtoAsync(document.Id);
    }

    // ---- Content retrieval ----

    public async Task<(MaterialDocumentDto Metadata, byte[] Content)> GetContentAsync(int documentId, int materialId, int requestingUserId)
    {
        var document = await _db.MaterialDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.MaterialId == materialId)
            ?? throw new InvalidOperationException($"Document {documentId} not found for material {materialId}.");

        byte[] content;
        try
        {
            content = await _storage.ReadAsync(document.StorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File read failed for MaterialDocument {Id} (key: {Key}).", document.Id, document.StorageKey);
            throw new InvalidOperationException("The document file could not be retrieved.");
        }

        // Integrity verification.
        var computedHash = Convert.ToHexString(SHA256.HashData(content));
        if (!string.Equals(computedHash, document.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "INTEGRITY FAILURE: MaterialDocument {Id} (material {MaterialId}) stored hash {Stored} does not match computed hash {Computed}.",
                document.Id, document.MaterialId, document.ContentSha256, computedHash);
            await RecordAccessAsync(document.Id, materialId, requestingUserId, MaterialDocumentAccessAction.Download);
            throw new InvalidOperationException("Document integrity check failed. The file may have been altered. Please contact your System Administrator.");
        }

        await RecordAccessAsync(document.Id, materialId, requestingUserId, MaterialDocumentAccessAction.Download);

        var userName = await _db.Users
            .Where(u => u.Id == document.UploadedByUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Unknown";

        return (ToDto(document, userName), content);
    }

    // ---- Supersession ----

    // Atomically marks the old document Superseded and creates a new Current document.
    // Both operations commit in the same SaveChanges to ensure consistency.
    public async Task<MaterialDocumentDto> SupersedeAsync(int documentId, int materialId, SupersedeMaterialDocumentRequest request, int actingUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A supersession reason is required.");

        var old = await _db.MaterialDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.MaterialId == materialId)
            ?? throw new InvalidOperationException($"Document {documentId} not found for material {materialId}.");

        if (old.Status == MaterialDocumentStatus.Voided)
            throw new InvalidOperationException("A voided document cannot be superseded. Upload a new document instead.");

        // Validate the replacement file.
        var firstBytes = request.Content.Take(16).ToArray();
        var error = _validator.Validate(request.OriginalFileName, request.DeclaredContentType, request.Content.Length, firstBytes);
        if (error != null)
            throw new InvalidOperationException(error);

        var ext = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        var sha256 = Convert.ToHexString(SHA256.HashData(request.Content));
        var now = DateTime.UtcNow;

        // Create new document record (placeholder StorageKey, same as UploadAsync).
        var newDoc = new MaterialDocument
        {
            MaterialId = materialId,
            DocumentType = old.DocumentType,  // inherit type unless changed by caller
            OriginalFileName = SanitiseFileName(request.OriginalFileName),
            StorageKey = "pending",
            FileExtension = ext,
            ContentType = NormaliseMime(request.DeclaredContentType),
            FileSizeBytes = request.Content.Length,
            ContentSha256 = sha256,
            UploadedByUserId = actingUserId,
            UploadedAt = now,
            Status = MaterialDocumentStatus.Current
        };
        _db.MaterialDocuments.Add(newDoc);

        // Update old document.
        old.Status = MaterialDocumentStatus.Superseded;
        old.SupersededAt = now;
        old.SupersededByUserId = actingUserId;
        old.SupersessionReason = request.Reason.Trim();

        await _db.SaveChangesAsync();

        // Wire the supersession link now that the new Id is available.
        old.SupersededByDocumentId = newDoc.Id;

        // Store the replacement file.
        var storageKey = $"material-documents/{materialId}/{newDoc.Id}{ext}";
        try
        {
            var savedPath = await _storage.SaveAsync(storageKey, request.Content);
            newDoc.StorageKey = savedPath;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File storage failed during supersession for MaterialDocument {OldId} → {NewId}.", documentId, newDoc.Id);
            // Rollback both records to avoid an orphan new document.
            old.Status = MaterialDocumentStatus.Current;
            old.SupersededAt = null;
            old.SupersededByUserId = null;
            old.SupersessionReason = null;
            old.SupersededByDocumentId = null;
            _db.MaterialDocuments.Remove(newDoc);
            try { await _db.SaveChangesAsync(); } catch { /* best-effort */ }
            throw new InvalidOperationException("Replacement file storage failed. The supersession was not completed.", ex);
        }

        await RecordAccessAsync(documentId, materialId, actingUserId, MaterialDocumentAccessAction.Supersede);
        await RecordAccessAsync(newDoc.Id, materialId, actingUserId, MaterialDocumentAccessAction.Upload);

        _logger.LogInformation("MaterialDocument {OldId} superseded by {NewId} for material {MaterialId} by user {UserId}.", documentId, newDoc.Id, materialId, actingUserId);

        return await LoadDtoAsync(newDoc.Id);
    }

    // ---- Void ----

    public async Task<MaterialDocumentDto> VoidAsync(int documentId, int materialId, VoidMaterialDocumentRequest request, int actingUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A void reason is required.");

        var document = await _db.MaterialDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.MaterialId == materialId)
            ?? throw new InvalidOperationException($"Document {documentId} not found for material {materialId}.");

        if (document.Status == MaterialDocumentStatus.Voided)
            throw new InvalidOperationException("This document is already voided.");

        document.Status = MaterialDocumentStatus.Voided;
        document.VoidedAt = DateTime.UtcNow;
        document.VoidedByUserId = actingUserId;
        document.VoidReason = request.Reason.Trim();

        await _db.SaveChangesAsync();

        await RecordAccessAsync(document.Id, materialId, actingUserId, MaterialDocumentAccessAction.Void);

        _logger.LogInformation("MaterialDocument {Id} voided for material {MaterialId} by user {UserId}.", document.Id, materialId, actingUserId);

        var userName = await _db.Users
            .Where(u => u.Id == document.UploadedByUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Unknown";

        return ToDto(document, userName);
    }

    // ---- COA Eligibility ----

    // Returns whether this material has at least one Current (non-voided, non-superseded) COA.
    // Used by the lot details UI to show the eligibility banner.
    // The enforcement gate remains in MaterialService.ConsumeAsync.
    public async Task<CoeEligibilityResult> GetCOAEligibilityAsync(int materialId)
    {
        var material = await _db.Materials.FindAsync(materialId)
            ?? throw new InvalidOperationException($"Material {materialId} not found.");

        var required = CoaRequiredTypes.Contains(material.MaterialType);
        if (!required)
            return new CoeEligibilityResult(IsEligible: true, CoaRequired: false, HasCurrentCoa: false);

        var hasCurrentCoa = await _db.MaterialDocuments.AnyAsync(d =>
            d.MaterialId == materialId &&
            d.DocumentType == MaterialDocumentType.COA &&
            d.Status == MaterialDocumentStatus.Current);

        return new CoeEligibilityResult(
            IsEligible: hasCurrentCoa,
            CoaRequired: true,
            HasCurrentCoa: hasCurrentCoa);
    }

    // ---- Private helpers ----

    private async Task RecordAccessAsync(int documentId, int materialId, int userId, MaterialDocumentAccessAction action)
    {
        _db.MaterialDocumentAccessLogs.Add(new MaterialDocumentAccessLog
        {
            DocumentId = documentId,
            MaterialId = materialId,
            UserId = userId,
            AccessedAt = DateTime.UtcNow,
            Action = action
        });
        try { await _db.SaveChangesAsync(); } catch (Exception ex)
        {
            // Access log failure must never block the primary operation.
            _logger.LogWarning(ex, "Failed to record document access log for document {Id}.", documentId);
        }
    }

    private async Task<MaterialDocumentDto> LoadDtoAsync(int documentId)
    {
        var doc = await _db.MaterialDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId)
            ?? throw new InvalidOperationException($"Document {documentId} not found.");

        var userName = await _db.Users
            .Where(u => u.Id == doc.UploadedByUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Unknown";

        return ToDto(doc, userName);
    }

    private static MaterialDocumentDto ToDto(MaterialDocument d, string? uploadedByName = null) => new(
        d.Id, d.MaterialId, d.DocumentType,
        d.OriginalFileName, d.FileExtension, d.ContentType, d.FileSizeBytes,
        d.ContentSha256,
        d.UploadedByUserId, uploadedByName ?? d.UploadedByUser?.FullName ?? "Unknown", d.UploadedAt,
        d.Status,
        d.SupersededByDocumentId, d.SupersededAt, d.SupersededByUserId, d.SupersessionReason,
        d.VoidedAt, d.VoidedByUserId, d.VoidReason);

    // Strip directory separators from user-provided filenames before storing as display name.
    private static string SanitiseFileName(string name) =>
        Path.GetFileName(name).Trim();

    private static string NormaliseMime(string mime) =>
        mime.Split(';')[0].Trim().ToLowerInvariant();
}
