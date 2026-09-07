using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentFileService : IDocumentFileService
{
    private readonly MicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IAuditEventService _auditEventService;
    private readonly IDocumentAuthorizationService _authService;

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public DocumentFileService(
        MicroLimsDbContext db,
        IFileStorageService storage,
        IAuditEventService auditEventService,
        IDocumentAuthorizationService authService)
    {
        _db = db;
        _storage = storage;
        _auditEventService = auditEventService;
        _authService = authService;
    }

    public async Task<RevisionFileDto> UploadRevisionFileAsync(
        int revisionId,
        FileRole fileRole,
        string originalFileName,
        string declaredContentType,
        byte[] content,
        int userId)
    {
        if (content == null || content.Length == 0)
            throw new ArgumentException("File content cannot be empty.", nameof(content));

        if (content.Length > MaxFileSizeBytes)
            throw new ArgumentException($"File size exceeds the maximum permitted limit of {MaxFileSizeBytes / (1024 * 1024)} MB.", nameof(content));

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original file name is required.", nameof(originalFileName));

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();

        string canonicalContentType;

        // Validate formats
        if (fileRole == FileRole.ControlledPdf)
        {
            if (ext != ".pdf")
                throw new ArgumentException("Controlled document file must be a PDF (.pdf).", nameof(originalFileName));

            // PDF magic bytes check (%PDF-)
            if (content.Length < 5 ||
                content[0] != 0x25 || content[1] != 0x50 || content[2] != 0x44 || content[3] != 0x46 || content[4] != 0x2D)
            {
                throw new ArgumentException("Invalid file format. The uploaded file is not a valid PDF document.", nameof(content));
            }

            canonicalContentType = "application/pdf";
        }
        else if (fileRole == FileRole.SourceFile)
        {
            if (ext != ".docx" && ext != ".doc")
                throw new ArgumentException("Source document file must be a Word document (.docx or .doc).", nameof(originalFileName));

            var (isValidWord, mimeType) = ValidateWordDocument(ext, content);
            if (!isValidWord)
            {
                throw new ArgumentException("Invalid file format. The uploaded file is not a valid Word document.", nameof(content));
            }

            canonicalContentType = mimeType;
        }
        else
        {
            canonicalContentType = string.IsNullOrWhiteSpace(declaredContentType)
                ? "application/octet-stream"
                : declaredContentType.Trim();
        }

        var canUpload = await _authService.CanUploadOrReplaceDraftFileAsync(revisionId, userId);
        if (!canUpload)
        {
            await _auditEventService.RecordUserEventAsync(
                actionCode: "UnauthorizedFileAccessAttempted",
                actionCategory: AuditActionCategory.Security,
                recordType: nameof(RevisionFile),
                documentRevisionId: revisionId,
                reason: $"Unauthorized attempt to upload or replace file on revision {revisionId} by user {userId}.",
                changes: new List<AuditFieldChange>
                {
                    new("RevisionId", revisionId.ToString(), null),
                    new("FileRole", fileRole.ToString(), null),
                    new("UserId", userId.ToString(), null)
                },
                entityId: revisionId.ToString());

            throw new UnauthorizedAccessException("You do not have permission to upload or replace files for this revision.");
        }

        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new KeyNotFoundException($"Document Revision {revisionId} not found.");

        // Rule DC-URS-024, DC-URS-025: Files can only be uploaded/replaced in Draft state
        if (revision.RevisionStatus != DocumentRevisionStatus.Draft)
        {
            throw new InvalidOperationException($"Files can only be added or replaced on revisions in Draft state. Current status is {revision.RevisionStatus}.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(content));
        var safeFileName = Path.GetFileName(originalFileName);
        var now = DateTime.UtcNow;

        RevisionFile newFile = null!;
        RevisionFile? existingActiveFile = null;

        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                existingActiveFile = await _db.RevisionFiles
                    .FirstOrDefaultAsync(f => f.DocumentRevisionId == revisionId && f.FileRole == fileRole && f.IsActive);

                var currentMaxVersion = await _db.RevisionFiles
                    .Where(f => f.DocumentRevisionId == revisionId && f.FileRole == fileRole)
                    .Select(f => (int?)f.FileVersion)
                    .MaxAsync() ?? 0;

                newFile = new RevisionFile
                {
                    DocumentRevisionId = revisionId,
                    FileRole = fileRole,
                    FileName = safeFileName,
                    ContentType = canonicalContentType,
                    SizeBytes = content.Length,
                    ContentSha256 = sha256,
                    StorageKey = "pending",
                    IsActive = true,
                    FileVersion = currentMaxVersion + 1,
                    UploadedAt = now,
                    UploadedByUserId = userId
                };

                if (existingActiveFile != null)
                {
                    existingActiveFile.IsActive = false;
                }

                _db.RevisionFiles.Add(newFile);
                _db.CurrentUserId = userId;
                await _db.SaveChangesAsync(); // Generates newFile.Id without filtered unique index collision
                break;
            }
            catch (DbUpdateException ex) when (attempt < maxRetries && IsUniqueConstraintViolation(ex))
            {
                _db.ChangeTracker.Clear();
                await Task.Delay(50 * attempt);
            }
        }

        // Relative storage key
        var storageKey = $"documents/{revisionId}/{newFile.Id}_{fileRole.ToString().ToLower()}{ext}";
        var savedPath = await _storage.SaveAsync(storageKey, content);
        newFile.StorageKey = savedPath;

        if (existingActiveFile != null)
        {
            existingActiveFile.SupersededByFileId = newFile.Id;
            await _db.SaveChangesAsync();

            // Record replacement audit event
            var changes = new List<AuditFieldChange>
            {
                new("SupersededFileId", existingActiveFile.Id.ToString(), newFile.Id.ToString()),
                new("PreviousFileName", existingActiveFile.FileName, newFile.FileName),
                new("PreviousSizeBytes", existingActiveFile.SizeBytes.ToString(), newFile.SizeBytes.ToString()),
                new("PreviousSha256", existingActiveFile.ContentSha256, newFile.ContentSha256),
                new("FileVersion", null, newFile.FileVersion.ToString())
            };

            await _auditEventService.RecordUserEventAsync(
                actionCode: "RevisionFileReplaced",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(RevisionFile),
                documentMasterId: revision.DocumentMasterId,
                documentRevisionId: revisionId,
                reason: $"Replacement of active {fileRole} file (v{newFile.FileVersion}) on Draft revision {revision.RevisionNumber}",
                changes: changes,
                entityId: newFile.Id.ToString());
        }
        else
        {
            await _db.SaveChangesAsync();

            var changes = new List<AuditFieldChange>
            {
                new("FileRole", null, fileRole.ToString()),
                new("FileName", null, newFile.FileName),
                new("SizeBytes", null, newFile.SizeBytes.ToString()),
                new("ContentSha256", null, newFile.ContentSha256),
                new("FileVersion", null, newFile.FileVersion.ToString())
            };

            await _auditEventService.RecordUserEventAsync(
                actionCode: "RevisionFileUploaded",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(RevisionFile),
                documentMasterId: revision.DocumentMasterId,
                documentRevisionId: revisionId,
                reason: $"Upload of initial {fileRole} file (v{newFile.FileVersion}) on Draft revision {revision.RevisionNumber}",
                changes: changes,
                entityId: newFile.Id.ToString());
        }

        var uploaderName = (await _db.Users.FindAsync(userId))?.FullName ?? "Unknown";
        return new RevisionFileDto(
            newFile.Id,
            newFile.DocumentRevisionId,
            newFile.FileRole,
            newFile.FileName,
            newFile.ContentType,
            newFile.SizeBytes,
            newFile.ContentSha256,
            newFile.IsActive,
            newFile.SupersededByFileId,
            newFile.UploadedAt,
            newFile.UploadedByUserId,
            uploaderName,
            newFile.FileVersion,
            newFile.IsApprovedFinalSource,
            newFile.GeneratedFromSourceFileId
        );
    }

    public async Task<(RevisionFileDto Metadata, byte[] Content)> GetFileContentAsync(int fileId, int userId, bool isDownload = false)
    {
        var file = await _db.RevisionFiles
            .Include(f => f.DocumentRevision).ThenInclude(r => r.DocumentMaster)
            .Include(f => f.UploadedByUser)
            .FirstOrDefaultAsync(f => f.Id == fileId)
            ?? throw new KeyNotFoundException($"File {fileId} not found.");

        if (file.FileRole == FileRole.SourceFile)
        {
            var canAccessSource = await _authService.CanAccessSourceFileAsync(fileId, userId);
            if (!canAccessSource)
            {
                await _auditEventService.RecordUserEventAsync(
                    actionCode: "UnauthorizedFileAccessAttempted",
                    actionCategory: AuditActionCategory.Security,
                    recordType: nameof(RevisionFile),
                    documentMasterId: file.DocumentRevision?.DocumentMasterId,
                    documentRevisionId: file.DocumentRevisionId,
                    reason: $"Unauthorized attempt to access source file {fileId} by user {userId}.",
                    changes: new List<AuditFieldChange>
                    {
                        new("FileId", fileId.ToString(), null),
                        new("FileRole", file.FileRole.ToString(), null),
                        new("UserId", userId.ToString(), null)
                    },
                    entityId: fileId.ToString());

                throw new UnauthorizedAccessException("Access to source/editable document files is restricted.");
            }
        }
        else
        {
            var canAccessPdf = await _authService.CanAccessControlledPdfAsync(fileId, userId);
            if (!canAccessPdf)
            {
                await _auditEventService.RecordUserEventAsync(
                    actionCode: "UnauthorizedFileAccessAttempted",
                    actionCategory: AuditActionCategory.Security,
                    recordType: nameof(RevisionFile),
                    documentMasterId: file.DocumentRevision?.DocumentMasterId,
                    documentRevisionId: file.DocumentRevisionId,
                    reason: $"Unauthorized attempt to access controlled PDF file {fileId} by user {userId}.",
                    changes: new List<AuditFieldChange>
                    {
                        new("FileId", fileId.ToString(), null),
                        new("FileRole", file.FileRole.ToString(), null),
                        new("UserId", userId.ToString(), null)
                    },
                    entityId: fileId.ToString());

                throw new UnauthorizedAccessException("You do not have permission to view or download this controlled document PDF.");
            }
        }

        // Controlled copy policy check (applies only to Controlled PDF copies)
        if (isDownload && file.FileRole == FileRole.ControlledPdf)
        {
            var downloadSetting = await _db.ConfigurationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == "DocumentControl.ControlledCopy.DownloadPermitted");

            var (isActive, role) = await GetUserRoleAsync(userId);
            // Admin and Doc Controller can always download
            var isPrivileged = role == RoleType.SystemAdministrator || role == RoleType.SectionHead;

            if (downloadSetting != null &&
                string.Equals(downloadSetting.SettingValue, "false", StringComparison.OrdinalIgnoreCase) &&
                !isPrivileged)
            {
                throw new InvalidOperationException("Controlled copy download is currently disabled by administrative policy.");
            }
        }

        byte[] content;
        try
        {
            content = await _storage.ReadAsync(file.StorageKey);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The document file could not be read from storage: {ex.Message}");
        }

        // SHA-256 Cryptographic Integrity Verification (DC-URS-198, DC-URS-199, DC-URS-200)
        var computedHash = Convert.ToHexString(SHA256.HashData(content));
        if (!string.Equals(computedHash, file.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            // Critical Data Integrity Fault
            await _auditEventService.RecordUserEventAsync(
                actionCode: "FileIntegrityVerificationFailed",
                actionCategory: AuditActionCategory.Security,
                recordType: nameof(RevisionFile),
                documentMasterId: file.DocumentRevision?.DocumentMasterId,
                documentRevisionId: file.DocumentRevisionId,
                reason: $"INTEGRITY FAILURE: Stored hash {file.ContentSha256} does not match computed hash {computedHash}.",
                changes: new List<AuditFieldChange>
                {
                    new("StoredHash", file.ContentSha256, null),
                    new("ComputedHash", null, computedHash),
                    new("FileId", file.Id.ToString(), null)
                },
                entityId: file.Id.ToString());

            throw new InvalidOperationException("Document integrity check failed. The file may have been altered or corrupted. Delivery has been blocked.");
        }

        // Log successful access
        var actionCode = file.FileRole == FileRole.SourceFile
            ? (isDownload ? "SourceFileDownloaded" : "SourceFileViewed")
            : (isDownload ? "ControlledFileDownloaded" : "ControlledFileViewed");

        await _auditEventService.RecordUserEventAsync(
            actionCode: actionCode,
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(RevisionFile),
            documentMasterId: file.DocumentRevision?.DocumentMasterId,
            documentRevisionId: file.DocumentRevisionId,
            entityId: file.Id.ToString());

        var dto = new RevisionFileDto(
            file.Id,
            file.DocumentRevisionId,
            file.FileRole,
            file.FileName,
            file.ContentType,
            file.SizeBytes,
            file.ContentSha256,
            file.IsActive,
            file.SupersededByFileId,
            file.UploadedAt,
            file.UploadedByUserId,
            file.UploadedByUser?.FullName ?? "Unknown",
            file.FileVersion,
            file.IsApprovedFinalSource,
            file.GeneratedFromSourceFileId
        );

        return (dto, content);
    }

    private static (bool IsValid, string CanonicalMimeType) ValidateWordDocument(string ext, byte[] content)
    {
        if (ext == ".docx")
        {
            // PK ZIP header: 0x50, 0x4B, 0x03, 0x04
            if (content.Length >= 4 &&
                content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04)
            {
                return (true, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            }
            return (false, string.Empty);
        }

        if (ext == ".doc")
        {
            // OLE Compound File Binary header: 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1
            if (content.Length >= 8 &&
                content[0] == 0xD0 && content[1] == 0xCF && content[2] == 0x11 && content[3] == 0xE0 &&
                content[4] == 0xA1 && content[5] == 0xB1 && content[6] == 0x1A && content[7] == 0xE1)
            {
                return (true, "application/msword");
            }
            return (false, string.Empty);
        }

        return (false, string.Empty);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("23505") ||
               msg.Contains("IX_RevisionFiles_DocumentRevisionId_FileRole_FileVersion", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool IsActive, RoleType? RoleType)> GetUserRoleAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.IsActive)
            return (false, null);

        return (true, user.Role?.Type);
    }
}
