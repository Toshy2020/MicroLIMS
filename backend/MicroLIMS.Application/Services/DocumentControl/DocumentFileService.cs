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
        }
        else if (fileRole == FileRole.SourceFile)
        {
            if (ext != ".docx" && ext != ".doc")
                throw new ArgumentException("Source document file must be a Word document (.docx or .doc).", nameof(originalFileName));
        }

        var canUpload = await _authService.CanUploadOrReplaceDraftFileAsync(revisionId, userId);
        if (!canUpload)
            throw new UnauthorizedAccessException("You do not have permission to upload or replace files for this revision.");

        var revision = await _db.DocumentRevisions
            .Include(r => r.Files)
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

        var existingActiveFile = revision.Files.FirstOrDefault(f => f.FileRole == fileRole && f.IsActive);

        var newFile = new RevisionFile
        {
            DocumentRevisionId = revisionId,
            FileRole = fileRole,
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(declaredContentType) ? "application/octet-stream" : declaredContentType.Trim(),
            SizeBytes = content.Length,
            ContentSha256 = sha256,
            StorageKey = "pending",
            IsActive = true,
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
                new("PreviousSha256", existingActiveFile.ContentSha256, newFile.ContentSha256)
            };

            await _auditEventService.RecordUserEventAsync(
                actionCode: "RevisionFileReplaced",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(RevisionFile),
                documentMasterId: revision.DocumentMasterId,
                documentRevisionId: revisionId,
                reason: $"Replacement of active {fileRole} file on Draft revision {revision.RevisionNumber}",
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
                new("ContentSha256", null, newFile.ContentSha256)
            };

            await _auditEventService.RecordUserEventAsync(
                actionCode: "RevisionFileUploaded",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(RevisionFile),
                documentMasterId: revision.DocumentMasterId,
                documentRevisionId: revisionId,
                reason: $"Upload of initial {fileRole} file on Draft revision {revision.RevisionNumber}",
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
            uploaderName
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
                throw new UnauthorizedAccessException("Access to source/editable document files is restricted.");
        }
        else
        {
            var canAccessPdf = await _authService.CanAccessControlledPdfAsync(fileId, userId);
            if (!canAccessPdf)
                throw new UnauthorizedAccessException("You do not have permission to view or download this controlled document PDF.");
        }

        // Controlled copy policy check
        if (isDownload)
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
        await _auditEventService.RecordUserEventAsync(
            actionCode: isDownload ? "ControlledFileDownloaded" : "ControlledFileViewed",
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
            file.UploadedByUser?.FullName ?? "Unknown"
        );

        return (dto, content);
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
