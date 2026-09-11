using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers.DocumentControl;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlRelease1dWP3PostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlRelease1dWP3PostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static byte[] CreateValidDocx(string text = "Postgres WP3 Valid DOCX Content")
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private static byte[] CreateValidDoc(string text = "Postgres WP3 Valid DOC Content")
    {
        var header = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private async Task<(User Author, User Reviewer, User Approver, User RandomUser, DocumentMaster Master, DocumentRevision Revision)>
        SeedScenarioAsync(MicroLimsDbContext db)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync();
        }

        var reviewerRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Reviewer);
        if (reviewerRole == null)
        {
            reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
            db.Roles.Add(reviewerRole);
            await db.SaveChangesAsync();
        }

        var author = new User
        {
            FullName = "WP3 Author",
            Username = $"auth_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "WP3 Reviewer",
            Username = $"rev_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        var approver = new User
        {
            FullName = "WP3 Approver",
            Username = $"app_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        var randomUser = new User
        {
            FullName = "WP3 Random User",
            Username = $"rnd_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };

        db.Users.AddRange(author, reviewer, approver, randomUser);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-WP3-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-WP3-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "WP3 Authorization & Lineage Test SOP",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = author.Id,
            CreatedByUserId = author.Id,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Draft,
            CreatedByUserId = author.Id
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        return (author, reviewer, approver, randomUser, master, rev);
    }

    [PostgresFact]
    public async Task Test01_ActiveTechnicalReviewer_CanDownloadWordSourceFile_ForAssignedTask()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx("Source File Content v1 for Review");
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_Source.docx", "application/octet-stream", content, author.Id);

        // Assign active review task to reviewer
        var reviewTask = new DocumentReviewTask
        {
            DocumentRevisionId = rev.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            Status = ReviewTaskStatus.Pending,
            AssignedAt = DateTime.UtcNow
        };
        db.DocumentReviewTasks.Add(reviewTask);
        await db.SaveChangesAsync();

        // Download as active reviewer
        var (metadata, downloadedBytes) = await fileService.GetFileContentAsync(uploadedFile.Id, reviewer.Id, isDownload: true);

        Assert.Equal(uploadedFile.Id, metadata.Id);
        Assert.Equal(FileRole.SourceFile, metadata.FileRole);
        Assert.Equal(1, metadata.FileVersion);
        Assert.Equal(content, downloadedBytes);

        // Verify SourceFileDownloaded audit event
        var auditLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "SourceFileDownloaded" && a.DocumentRevisionId == rev.Id);
        Assert.NotNull(auditLog);
    }

    [PostgresFact]
    public async Task Test02_UnassignedTechnicalReviewer_IsDeniedDownloadOfWordSourceFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx();
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_Source.docx", "application/octet-stream", content, author.Id);

        // Reviewer is NOT assigned to rev.Id
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(uploadedFile.Id, reviewer.Id, isDownload: true));
        Assert.Contains("restricted", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify UnauthorizedFileAccessAttempted audit event
        var auditLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "UnauthorizedFileAccessAttempted" && a.DocumentRevisionId == rev.Id);
        Assert.NotNull(auditLog);
    }

    [PostgresFact]
    public async Task Test03_ReviewerAssignedToDocA_CannotDownloadWordSourceForDocB()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorA, reviewer, _, _, masterA, revA) = await SeedScenarioAsync(db);
        var (authorB, _, _, _, masterB, revB) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var contentB = CreateValidDocx("Doc B Content");
        var fileB = await fileService.UploadRevisionFileAsync(
            revB.Id, FileRole.SourceFile, "DocB.docx", "application/octet-stream", contentB, authorB.Id);

        // Assign reviewer ONLY to Doc A
        var reviewTaskA = new DocumentReviewTask
        {
            DocumentRevisionId = revA.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = authorA.Id,
            Status = ReviewTaskStatus.Pending,
            AssignedAt = DateTime.UtcNow
        };
        db.DocumentReviewTasks.Add(reviewTaskA);
        await db.SaveChangesAsync();

        // Attempt download of Doc B
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(fileB.Id, reviewer.Id, isDownload: true));
    }

    [PostgresFact]
    public async Task Test04_ReviewerWithCompletedOrCancelledTask_CannotDownloadWordSource()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx();
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_Source.docx", "application/octet-stream", content, author.Id);

        var reviewTask = new DocumentReviewTask
        {
            DocumentRevisionId = rev.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            Status = ReviewTaskStatus.Completed,
            AssignedAt = DateTime.UtcNow.AddDays(-2),
            DecisionAt = DateTime.UtcNow.AddDays(-1)
        };
        db.DocumentReviewTasks.Add(reviewTask);
        await db.SaveChangesAsync();

        // Completed -> Denied
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(uploadedFile.Id, reviewer.Id, isDownload: true));

        // Cancelled -> Denied
        reviewTask.Status = ReviewTaskStatus.Cancelled;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(uploadedFile.Id, reviewer.Id, isDownload: true));
    }

    [PostgresFact]
    public async Task Test05_ActiveApprover_CanDownloadFinalWordSourceFile_ForAssignedApprovalTask()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, approver, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx("Final Approved Word Content");
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "FinalApproved.docx", "application/octet-stream", content, author.Id);

        // Assign pending approval task to approver
        var approvalTask = new DocumentApprovalTask
        {
            DocumentRevisionId = rev.Id,
            AssignedApproverUserId = approver.Id,
            AssignedByUserId = author.Id,
            Status = DocumentApprovalTaskStatus.Pending,
            AssignedAt = DateTime.UtcNow
        };
        db.DocumentApprovalTasks.Add(approvalTask);
        await db.SaveChangesAsync();

        var (metadata, downloadedBytes) = await fileService.GetFileContentAsync(uploadedFile.Id, approver.Id, isDownload: true);

        Assert.Equal(uploadedFile.Id, metadata.Id);
        Assert.Equal(content, downloadedBytes);
        Assert.Equal(1, metadata.FileVersion);
    }

    [PostgresFact]
    public async Task Test06_UnassignedApprover_IsDeniedDownloadOfWordSourceFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, approver, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx();
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "FinalApproved.docx", "application/octet-stream", content, author.Id);

        // Approver has no assigned task for rev.Id
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(uploadedFile.Id, approver.Id, isDownload: true));
    }

    [PostgresFact]
    public async Task Test07_ApproverAssignedToDocA_CannotDownloadWordSourceForDocB()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorA, _, approver, _, masterA, revA) = await SeedScenarioAsync(db);
        var (authorB, _, _, _, masterB, revB) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var contentB = CreateValidDocx("Doc B Content");
        var fileB = await fileService.UploadRevisionFileAsync(
            revB.Id, FileRole.SourceFile, "DocB.docx", "application/octet-stream", contentB, authorB.Id);

        var approvalTaskA = new DocumentApprovalTask
        {
            DocumentRevisionId = revA.Id,
            AssignedApproverUserId = approver.Id,
            AssignedByUserId = authorA.Id,
            Status = DocumentApprovalTaskStatus.Pending,
            AssignedAt = DateTime.UtcNow
        };
        db.DocumentApprovalTasks.Add(approvalTaskA);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(fileB.Id, approver.Id, isDownload: true));
    }

    [PostgresFact]
    public async Task Test08_TechnicalReviewer_CannotUploadReplacementWordSourceFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var reviewTask = new DocumentReviewTask
        {
            DocumentRevisionId = rev.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            Status = ReviewTaskStatus.Pending,
            AssignedAt = DateTime.UtcNow
        };
        db.DocumentReviewTasks.Add(reviewTask);
        await db.SaveChangesAsync();

        var content = CreateValidDocx("Reviewer trying to replace author source");
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "Hacked.docx", "application/octet-stream", content, reviewer.Id));

        Assert.Contains("permission to upload or replace", ex.Message);
    }

    [PostgresFact]
    public async Task Test09_Approver_CannotUploadReplacementWordSourceFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, approver, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var approvalTask = new DocumentApprovalTask
        {
            DocumentRevisionId = rev.Id,
            AssignedApproverUserId = approver.Id,
            AssignedByUserId = author.Id,
            Status = DocumentApprovalTaskStatus.Pending,
            AssignedAt = DateTime.UtcNow
        };
        db.DocumentApprovalTasks.Add(approvalTask);
        await db.SaveChangesAsync();

        var content = CreateValidDocx("Approver trying to upload source");
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "ApproverDoc.docx", "application/octet-stream", content, approver.Id));

        Assert.Contains("permission to upload or replace", ex.Message);
    }

    [PostgresFact]
    public async Task Test10_UnauthenticatedOrAnonymousRequest_ToDownloadWordSourceFile_IsRejected()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx();
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "Source.docx", "application/octet-stream", content, author.Id);

        // User ID 0 (anonymous/unauthenticated)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(uploadedFile.Id, userId: 0, isDownload: true));

        // Controller authorization attribute check
        var authAttrs = typeof(DocumentFilesController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        Assert.NotEmpty(authAttrs);
    }

    [PostgresFact]
    public async Task Test11_RandomAuthenticatedUser_GuessingFileId_CannotDownloadWordSourceFile()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, randomUser, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx();
        var uploadedFile = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "Source.docx", "application/octet-stream", content, author.Id);

        // Random authenticated user guessing the fileId
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fileService.GetFileContentAsync(uploadedFile.Id, randomUser.Id, isDownload: true));

        Assert.Contains("restricted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Test12_InitialWordUpload_CreatesFileVersion1_Active_SupersededNull()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var content = CreateValidDocx("Initial File Version 1 Content");
        var result = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_Initial.docx", "application/octet-stream", content, author.Id);

        Assert.Equal(1, result.FileVersion);
        Assert.True(result.IsActive);
        Assert.Null(result.SupersededByFileId);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);

        // Verify DB persistence
        var dbFile = await db.RevisionFiles.FindAsync(result.Id);
        Assert.NotNull(dbFile);
        Assert.Equal(1, dbFile.FileVersion);
        Assert.True(dbFile.IsActive);
        Assert.Null(dbFile.SupersededByFileId);

        // Verify audit log
        var auditLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "RevisionFileUploaded" && a.DocumentRevisionId == rev.Id);
        Assert.NotNull(auditLog);
    }

    [PostgresFact]
    public async Task Test13_SecondWordUpload_CreatesFileVersion2_MarksV1Inactive_AndSetsSupersededBy()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var contentV1 = CreateValidDocx("Initial File Version 1 Content");
        var v1 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v1.docx", "application/octet-stream", contentV1, author.Id);

        var contentV2 = CreateValidDocx("Corrected File Version 2 Content");
        var v2 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v2.docx", "application/octet-stream", contentV2, author.Id);

        Assert.Equal(2, v2.FileVersion);
        Assert.True(v2.IsActive);
        Assert.Null(v2.SupersededByFileId);

        // Verify v1 in DB
        var dbV1 = await db.RevisionFiles.FindAsync(v1.Id);
        Assert.NotNull(dbV1);
        Assert.Equal(1, dbV1.FileVersion);
        Assert.False(dbV1.IsActive);
        Assert.Equal(v2.Id, dbV1.SupersededByFileId);

        // Verify replacement audit event
        var auditLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "RevisionFileReplaced" && a.DocumentRevisionId == rev.Id);
        Assert.NotNull(auditLog);
    }

    [PostgresFact]
    public async Task Test14_ThirdWordUpload_CreatesFileVersion3_MarksV2Inactive_AndSetsSupersededBy()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var v1 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v1.docx", "application/octet-stream", CreateValidDocx("v1"), author.Id);
        var v2 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v2.docx", "application/octet-stream", CreateValidDocx("v2"), author.Id);
        var v3 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v3.docx", "application/octet-stream", CreateValidDocx("v3"), author.Id);

        Assert.Equal(3, v3.FileVersion);
        Assert.True(v3.IsActive);

        var dbV1 = await db.RevisionFiles.FindAsync(v1.Id);
        var dbV2 = await db.RevisionFiles.FindAsync(v2.Id);
        var dbV3 = await db.RevisionFiles.FindAsync(v3.Id);

        Assert.NotNull(dbV1);
        Assert.NotNull(dbV2);
        Assert.NotNull(dbV3);

        Assert.False(dbV1.IsActive);
        Assert.Equal(v2.Id, dbV1.SupersededByFileId);

        Assert.False(dbV2.IsActive);
        Assert.Equal(v3.Id, dbV2.SupersededByFileId);

        Assert.True(dbV3.IsActive);
        Assert.Null(dbV3.SupersededByFileId);
    }

    [PostgresFact]
    public async Task Test15_SupersededPhysicalFiles_ArePreservedOnDisk_AndSha256Verified()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var bytes1 = CreateValidDocx("Historical Version 1 Bytes");
        var bytes2 = CreateValidDocx("Historical Version 2 Bytes");

        var v1 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v1.docx", "application/octet-stream", bytes1, author.Id);
        var v2 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "SOP_v2.docx", "application/octet-stream", bytes2, author.Id);

        var dbV1 = await db.RevisionFiles.FindAsync(v1.Id);
        var dbV2 = await db.RevisionFiles.FindAsync(v2.Id);

        // Both physical files exist in storage
        var readV1 = await storage.ReadAsync(dbV1!.StorageKey);
        var readV2 = await storage.ReadAsync(dbV2!.StorageKey);

        Assert.Equal(bytes1, readV1);
        Assert.Equal(bytes2, readV2);

        // SHA-256 integrity verification passes for both
        var hash1 = Convert.ToHexString(SHA256.HashData(readV1));
        var hash2 = Convert.ToHexString(SHA256.HashData(readV2));
        Assert.Equal(dbV1.ContentSha256, hash1);
        Assert.Equal(dbV2.ContentSha256, hash2);

        // Corrupt v1 storage to verify tamper detection
        await storage.SaveAsync(dbV1.StorageKey, Encoding.UTF8.GetBytes("Tampered malicious bytes"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fileService.GetFileContentAsync(v1.Id, author.Id, isDownload: true));
        Assert.Contains("integrity check failed", ex.Message);

        var integrityLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "FileIntegrityVerificationFailed" && a.DocumentRevisionId == rev.Id);
        Assert.NotNull(integrityLog);
    }

    [PostgresFact]
    public async Task Test16_UploadingNonWordFile_AsSourceFile_IsRejectedWithArgumentException()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7 text");
        var exeBytes = Encoding.UTF8.GetBytes("MZ executable");
        var txtBytes = Encoding.UTF8.GetBytes("plain text");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "file.pdf", "application/pdf", pdfBytes, author.Id));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "file.exe", "application/octet-stream", exeBytes, author.Id));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "file.txt", "text/plain", txtBytes, author.Id));
    }

    [PostgresFact]
    public async Task Test17_UploadingInvalidOrCorruptWordFile_IsRejectedWithArgumentException()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        // Corrupt docx (starts with wrong bytes)
        var badDocx = Encoding.UTF8.GetBytes("NOT A ZIP ARCHIVE AT ALL");
        var exDocx = await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "bad.docx", "application/octet-stream", badDocx, author.Id));
        Assert.Contains("valid Word document", exDocx.Message);

        // Corrupt doc (starts with wrong bytes)
        var badDoc = Encoding.UTF8.GetBytes("NOT AN OLE COMPOUND FILE");
        var exDoc = await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "bad.doc", "application/octet-stream", badDoc, author.Id));
        Assert.Contains("valid Word document", exDoc.Message);
    }

    [PostgresFact]
    public async Task Test18_ClientProvidedFileVersion_IsIgnoredAndCalculatedServerSide()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        // In UploadRevisionFileAsync API and service signature, there is no client-provided FileVersion argument.
        // It is strictly calculated as currentMaxVersion + 1.
        var content1 = CreateValidDocx("Content 1");
        var result1 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "Doc1.docx", "application/octet-stream", content1, author.Id);
        Assert.Equal(1, result1.FileVersion);

        var content2 = CreateValidDocx("Content 2");
        var result2 = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "Doc2.docx", "application/octet-stream", content2, author.Id);
        Assert.Equal(2, result2.FileVersion);
    }

    [PostgresFact]
    public async Task Test19_ConcurrentSourceFileUploads_HandleVersionCollisionGracefully()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, _, _, _, master, rev) = await SeedScenarioAsync(db);

        var storage = new InMemoryFileStorageService();

        // Run multiple concurrent uploads with separate DbContext instances to simulate concurrent HTTP requests
        var tasks = Enumerable.Range(1, 3).Select(async i =>
        {
            await using var taskDb = _fixture.CreateDbContext();
            var seqHelper = _fixture.CreateSequenceHelper(taskDb);
            var audit = new AuditEventService(taskDb, seqHelper);
            var auth = new DocumentAuthorizationService(taskDb);
            var service = new DocumentFileService(taskDb, storage, audit, auth);

            var content = CreateValidDocx($"Concurrent Payload {i}");
            return await service.UploadRevisionFileAsync(
                rev.Id, FileRole.SourceFile, $"Concurrent_{i}.docx", "application/octet-stream", content, author.Id);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // All succeeded
        Assert.Equal(3, results.Length);

        // File versions must be distinct (1, 2, 3)
        var versions = results.Select(r => r.FileVersion).OrderBy(v => v).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, versions);

        // Verify in DB that only one file is active and unique constraint IX_RevisionFiles_DocumentRevisionId_FileRole_FileVersion is honored
        await using var verifyDb = _fixture.CreateDbContext();
        var files = await verifyDb.RevisionFiles
            .Where(f => f.DocumentRevisionId == rev.Id && f.FileRole == FileRole.SourceFile)
            .OrderBy(f => f.FileVersion)
            .ToListAsync();

        Assert.Equal(3, files.Count);
        Assert.Equal(1, files.Count(f => f.IsActive));
        Assert.Equal(2, files.Count(f => !f.IsActive));

        // Distinct FileVersions
        Assert.Equal(new[] { 1, 2, 3 }, files.Select(f => f.FileVersion));
    }
}
