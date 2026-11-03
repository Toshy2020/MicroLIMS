using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public record SampleDocumentDto(
    int Id, int SampleId, SampleDocumentType DocumentType, string OriginalFileName, string FileExtension,
    string ContentType, long FileSizeBytes, string ContentSha256, int UploadedByUserId, string UploadedByName,
    DateTime UploadedAt, SampleDocumentStatus Status, DateTime? VoidedAt, int? VoidedByUserId, string? VoidReason,
    int? TestOrderId = null);

// Documents attached to a received sample (the supplier's CoA, external lab report). Same file
// handling as MaterialDocumentService - server-built storage key, shared file
// validator, SHA-256 checked on every read - but actions are limited to
// users who can access the sample's laboratories or the test order. Uploads and
// voids write an audit event; reads are not audited (material documents keep a
// separate access log for that; samples do not).
public class SampleDocumentService
{
    public const string UploadedActionCode = "SAMPLE_DOCUMENT_UPLOADED";
    public const string VoidedActionCode = "SAMPLE_DOCUMENT_VOIDED";

    private readonly IMicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly MaterialDocumentFileValidator _validator;
    private readonly IAuditEventService _audit;
    private readonly IUserSectionScopeService _scope;
    private readonly ILogger<SampleDocumentService> _logger;
    private readonly TimeProvider _time;

    public SampleDocumentService(IMicroLimsDbContext db, IFileStorageService storage, MaterialDocumentFileValidator validator,
        IAuditEventService audit, IUserSectionScopeService scope, ILogger<SampleDocumentService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _storage = storage;
        _validator = validator;
        _audit = audit;
        _scope = scope;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<SampleDocumentDto>> ListAsync(int sampleId, int userId, CancellationToken ct = default)
    {
        await EnsureAccessAsync(sampleId, userId, ct);
        var docs = await _db.SampleDocuments.AsNoTracking()
            .Include(d => d.UploadedByUser)
            .Where(d => d.SampleId == sampleId)
            .OrderBy(d => d.Status).ThenByDescending(d => d.UploadedAt)
            .ToListAsync(ct);
        return docs.Select(d => ToDto(d)).ToList();
    }

    public Task<SampleDocumentDto> UploadAsync(int sampleId, string fileName, string contentType, byte[] content,
        SampleDocumentType type, int userId, int? testOrderId = null, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => UploadCoreAsync(sampleId, fileName, contentType, content, type, userId, testOrderId, ct));

    private async Task<SampleDocumentDto> UploadCoreAsync(int sampleId, string fileName, string contentType, byte[] content,
        SampleDocumentType type, int userId, int? testOrderId, CancellationToken ct)
    {
        if (type == SampleDocumentType.ExternalLabReport)
        {
            if (!testOrderId.HasValue)
                throw new InvalidOperationException("An external lab report must specify a test order.");
            var outsourcedTo = await _db.TestOrders.Where(t => t.Id == testOrderId.Value && t.SampleId == sampleId)
                .Select(t => t.OutsourcedToLab).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Test order {testOrderId.Value} is not an outsourced test of sample {sampleId}.");
            await _scope.EnsureTestOrderAccessAsync(userId, testOrderId.Value, ct);
        }
        else
        {
            if (testOrderId.HasValue)
                throw new InvalidOperationException("Only an external lab report belongs to a test order.");
            await EnsureAccessAsync(sampleId, userId, ct);
        }
        await EnsureSampleOpenAsync(sampleId, ct);

        var error = _validator.Validate(fileName, contentType, content.Length, content.Take(16).ToArray());
        if (error != null)
            throw new InvalidOperationException(error);

        _db.CurrentUserId = userId;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var document = new SampleDocument
        {
            SampleId = sampleId,
            TestOrderId = testOrderId,
            DocumentType = type,
            OriginalFileName = CleanFileName(fileName),
            StorageKey = "pending", // needs the Id, set below
            FileExtension = ext,
            ContentType = contentType.Split(';')[0].Trim().ToLowerInvariant(),
            FileSizeBytes = content.Length,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(content)),
            UploadedByUserId = userId,
            UploadedAt = _time.GetUtcNow().UtcDateTime,
            Status = SampleDocumentStatus.Current
        };
        _db.SampleDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        try
        {
            document.StorageKey = await _storage.SaveAsync($"sample-documents/{sampleId}/{document.Id}{ext}", content);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File storage failed for SampleDocument {Id} (sample {SampleId}). Rolling back DB record.", document.Id, sampleId);
            _db.SampleDocuments.Remove(document);
            try { await _db.SaveChangesAsync(ct); } catch { /* best-effort */ }
            throw new InvalidOperationException("Document storage failed. The upload was not completed.", ex);
        }

        var changes = new List<AuditFieldChange>
        {
            new AuditFieldChange("Document", null, document.OriginalFileName),
            new AuditFieldChange("Type", null, type.ToString()),
            new AuditFieldChange("Sha256", null, document.ContentSha256)
        };
        if (testOrderId.HasValue)
        {
            changes.Add(new AuditFieldChange("TestOrderId", null, testOrderId.Value.ToString()));
        }

        await _audit.RecordUserEventAsync(UploadedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            changes: changes,
            entityId: sampleId.ToString(), sampleId: sampleId, cancellationToken: ct);

        var uploader = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return ToDto(document, uploader);
    }

    public async Task<(SampleDocumentDto Metadata, byte[] Content)> ReadContentAsync(int documentId, int userId, CancellationToken ct = default)
    {
        var document = await _db.SampleDocuments.AsNoTracking().Include(d => d.UploadedByUser)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException($"Document {documentId} not found.");
        await EnsureAccessAsync(document.SampleId, userId, ct);

        byte[] content;
        try
        {
            content = await _storage.ReadAsync(document.StorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File read failed for SampleDocument {Id} (key: {Key}).", document.Id, document.StorageKey);
            throw new InvalidOperationException("The document file could not be retrieved.");
        }

        if (!string.Equals(Convert.ToHexString(SHA256.HashData(content)), document.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("INTEGRITY FAILURE: SampleDocument {Id} (sample {SampleId}) does not match its stored hash.", document.Id, document.SampleId);
            throw new InvalidOperationException("Document integrity check failed. The file may have been altered. Please contact your System Administrator.");
        }
        return (ToDto(document), content);
    }

    public Task<SampleDocumentDto> VoidAsync(int documentId, string reason, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => VoidCoreAsync(documentId, reason, userId, ct));

    private async Task<SampleDocumentDto> VoidCoreAsync(int documentId, string reason, int userId, CancellationToken ct)
    {
        var document = await _db.SampleDocuments.Include(d => d.UploadedByUser).FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException($"Document {documentId} not found.");
        await EnsureAccessAsync(document.SampleId, userId, ct);
        await EnsureSampleOpenAsync(document.SampleId, ct);

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A void reason is required.");
        if (document.Status == SampleDocumentStatus.Voided)
            throw new InvalidOperationException("This document is already voided.");

        _db.CurrentUserId = userId;
        document.Status = SampleDocumentStatus.Voided;
        document.VoidedAt = _time.GetUtcNow().UtcDateTime;
        document.VoidedByUserId = userId;
        document.VoidReason = reason.Trim();
        await _db.SaveChangesAsync(ct);

        await _audit.RecordUserEventAsync(VoidedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: document.VoidReason,
            changes: new[] { new AuditFieldChange("Document", document.OriginalFileName, "Voided") },
            entityId: document.SampleId.ToString(), sampleId: document.SampleId, cancellationToken: ct);
        return ToDto(document);
    }

    // The sample-detail check (handles samples awaiting test selection); it passes silently for a
    // missing sample, so existence is checked here.
    private async Task EnsureAccessAsync(int sampleId, int userId, CancellationToken ct)
    {
        if (!await _db.Samples.AsNoTracking().AnyAsync(s => s.Id == sampleId, ct))
            throw new NotFoundException($"Sample {sampleId} not found.");
        await _scope.EnsureSampleAccessAsync(userId, sampleId, ct);
    }

    // A voided or cancelled sample is closed; its documents stay readable but cannot change.
    private async Task EnsureSampleOpenAsync(int sampleId, CancellationToken ct)
    {
        var status = await _db.Samples.AsNoTracking().Where(s => s.Id == sampleId).Select(s => s.Status).FirstAsync(ct);
        if (status is SampleStatus.Voided or SampleStatus.Cancelled)
            throw new InvalidOperationException("This sample is voided; its documents can no longer be changed.");
    }

    // Strips the path and every control character and double quote (header / log injection).
    private static string CleanFileName(string fileName) =>
        new string(Path.GetFileName(fileName).Where(c => c >= ' ' && c != '\u007f' && c != '"').ToArray()).Trim();

    private static SampleDocumentDto ToDto(SampleDocument d, string? uploadedByName = null) => new(
        d.Id, d.SampleId, d.DocumentType, d.OriginalFileName, d.FileExtension, d.ContentType, d.FileSizeBytes,
        d.ContentSha256, d.UploadedByUserId, uploadedByName ?? d.UploadedByUser?.FullName ?? "Unknown", d.UploadedAt, d.Status,
        d.VoidedAt, d.VoidedByUserId, d.VoidReason, d.TestOrderId);
}
