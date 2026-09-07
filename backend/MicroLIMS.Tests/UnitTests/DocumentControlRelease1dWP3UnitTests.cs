using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlRelease1dWP3UnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 8000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(System.Threading.Interlocked.Increment(ref _seq));
        }
    }

    private static MicroLimsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: $"MicroLIMS_WP3_Unit_{Guid.NewGuid()}")
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static byte[] CreateValidDocx(string text = "Unit Test Valid DOCX")
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private static byte[] CreateValidDoc(string text = "Unit Test Valid DOC")
    {
        var header = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    [Fact]
    public void DocumentFilesController_EnforcesAuthorizeAttribute()
    {
        var authAttrs = typeof(DocumentFilesController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        Assert.NotEmpty(authAttrs);

        var allowAnonymousAttrs = typeof(DocumentFilesController)
            .GetMethod(nameof(DocumentFilesController.DownloadFile))?
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);

        Assert.True(allowAnonymousAttrs == null || allowAnonymousAttrs.Length == 0);
    }

    [Fact]
    public async Task CanAccessSourceFile_ActiveTechnicalReviewer_Allowed()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new DocumentAuthorizationService(db);

        var reviewerRole = new Role { Id = 1, Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
        db.Roles.Add(reviewerRole);

        var reviewer = new User { Id = 10, FullName = "Reviewer One", RoleId = reviewerRole.Id, Role = reviewerRole, IsActive = true };
        db.Users.Add(reviewer);

        var master = new DocumentMaster { Id = 1, Title = "SOP-1", DocumentOwnerUserId = 99 };
        var rev = new DocumentRevision { Id = 2, DocumentMasterId = master.Id, DocumentMaster = master, RevisionNumber = "01" };
        var file = new RevisionFile
        {
            Id = 100,
            DocumentRevisionId = rev.Id,
            DocumentRevision = rev,
            FileRole = FileRole.SourceFile,
            IsActive = true
        };
        var task = new DocumentReviewTask
        {
            Id = 50,
            DocumentRevisionId = rev.Id,
            AssignedReviewerUserId = reviewer.Id,
            Status = ReviewTaskStatus.Pending
        };

        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        db.RevisionFiles.Add(file);
        db.DocumentReviewTasks.Add(task);
        await db.SaveChangesAsync();

        var canAccess = await authService.CanAccessSourceFileAsync(file.Id, reviewer.Id);
        Assert.True(canAccess);

        // Also verify InProgress status
        task.Status = ReviewTaskStatus.InProgress;
        await db.SaveChangesAsync();
        var canAccessInProgress = await authService.CanAccessSourceFileAsync(file.Id, reviewer.Id);
        Assert.True(canAccessInProgress);
    }

    [Fact]
    public async Task CanAccessSourceFile_CompletedOrCancelledReviewer_Denied()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new DocumentAuthorizationService(db);

        var reviewerRole = new Role { Id = 1, Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
        db.Roles.Add(reviewerRole);

        var reviewer = new User { Id = 10, FullName = "Reviewer One", RoleId = reviewerRole.Id, Role = reviewerRole, IsActive = true };
        db.Users.Add(reviewer);

        var master = new DocumentMaster { Id = 1, Title = "SOP-1", DocumentOwnerUserId = 99 };
        var rev = new DocumentRevision { Id = 2, DocumentMasterId = master.Id, DocumentMaster = master, RevisionNumber = "01" };
        var file = new RevisionFile
        {
            Id = 100,
            DocumentRevisionId = rev.Id,
            DocumentRevision = rev,
            FileRole = FileRole.SourceFile,
            IsActive = true
        };
        var task = new DocumentReviewTask
        {
            Id = 50,
            DocumentRevisionId = rev.Id,
            AssignedReviewerUserId = reviewer.Id,
            Status = ReviewTaskStatus.Completed
        };

        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        db.RevisionFiles.Add(file);
        db.DocumentReviewTasks.Add(task);
        await db.SaveChangesAsync();

        var canAccessCompleted = await authService.CanAccessSourceFileAsync(file.Id, reviewer.Id);
        Assert.False(canAccessCompleted);

        task.Status = ReviewTaskStatus.Cancelled;
        await db.SaveChangesAsync();
        var canAccessCancelled = await authService.CanAccessSourceFileAsync(file.Id, reviewer.Id);
        Assert.False(canAccessCancelled);
    }

    [Fact]
    public async Task CanAccessSourceFile_ActiveApprover_Allowed()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new DocumentAuthorizationService(db);

        var analystRole = new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        db.Roles.Add(analystRole);

        var approver = new User { Id = 20, FullName = "Approver One", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        db.Users.Add(approver);

        var master = new DocumentMaster { Id = 1, Title = "SOP-1", DocumentOwnerUserId = 99 };
        var rev = new DocumentRevision { Id = 2, DocumentMasterId = master.Id, DocumentMaster = master, RevisionNumber = "01" };
        var file = new RevisionFile
        {
            Id = 100,
            DocumentRevisionId = rev.Id,
            DocumentRevision = rev,
            FileRole = FileRole.SourceFile,
            IsActive = true
        };
        var approvalTask = new DocumentApprovalTask
        {
            Id = 60,
            DocumentRevisionId = rev.Id,
            AssignedApproverUserId = approver.Id,
            Status = DocumentApprovalTaskStatus.Pending
        };

        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        db.RevisionFiles.Add(file);
        db.DocumentApprovalTasks.Add(approvalTask);
        await db.SaveChangesAsync();

        var canAccess = await authService.CanAccessSourceFileAsync(file.Id, approver.Id);
        Assert.True(canAccess);
    }

    [Fact]
    public async Task CanAccessSourceFile_CompletedApprover_Denied()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new DocumentAuthorizationService(db);

        var analystRole = new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        db.Roles.Add(analystRole);

        var approver = new User { Id = 20, FullName = "Approver One", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        db.Users.Add(approver);

        var master = new DocumentMaster { Id = 1, Title = "SOP-1", DocumentOwnerUserId = 99 };
        var rev = new DocumentRevision { Id = 2, DocumentMasterId = master.Id, DocumentMaster = master, RevisionNumber = "01" };
        var file = new RevisionFile
        {
            Id = 100,
            DocumentRevisionId = rev.Id,
            DocumentRevision = rev,
            FileRole = FileRole.SourceFile,
            IsActive = true
        };
        var approvalTask = new DocumentApprovalTask
        {
            Id = 60,
            DocumentRevisionId = rev.Id,
            AssignedApproverUserId = approver.Id,
            Status = DocumentApprovalTaskStatus.Approved
        };

        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        db.RevisionFiles.Add(file);
        db.DocumentApprovalTasks.Add(approvalTask);
        await db.SaveChangesAsync();

        var canAccess = await authService.CanAccessSourceFileAsync(file.Id, approver.Id);
        Assert.False(canAccess);
    }

    [Fact]
    public async Task CanAccessSourceFile_ReviewerOrApproverForDifferentDoc_Denied()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new DocumentAuthorizationService(db);

        var role = new Role { Id = 1, Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
        db.Roles.Add(role);

        var reviewer = new User { Id = 10, FullName = "Reviewer One", RoleId = role.Id, Role = role, IsActive = true };
        var approver = new User { Id = 20, FullName = "Approver One", RoleId = role.Id, Role = role, IsActive = true };
        db.Users.AddRange(reviewer, approver);

        var masterA = new DocumentMaster { Id = 1, Title = "SOP-A", DocumentOwnerUserId = 99 };
        var revA = new DocumentRevision { Id = 2, DocumentMasterId = masterA.Id, DocumentMaster = masterA, RevisionNumber = "01" };

        var masterB = new DocumentMaster { Id = 3, Title = "SOP-B", DocumentOwnerUserId = 99 };
        var revB = new DocumentRevision { Id = 4, DocumentMasterId = masterB.Id, DocumentMaster = masterB, RevisionNumber = "01" };

        var fileB = new RevisionFile
        {
            Id = 200,
            DocumentRevisionId = revB.Id,
            DocumentRevision = revB,
            FileRole = FileRole.SourceFile,
            IsActive = true
        };

        // Reviewer assigned to Doc A, Approver assigned to Doc A
        var reviewTaskA = new DocumentReviewTask
        {
            Id = 70,
            DocumentRevisionId = revA.Id,
            AssignedReviewerUserId = reviewer.Id,
            Status = ReviewTaskStatus.Pending
        };
        var approvalTaskA = new DocumentApprovalTask
        {
            Id = 71,
            DocumentRevisionId = revA.Id,
            AssignedApproverUserId = approver.Id,
            Status = DocumentApprovalTaskStatus.Pending
        };

        db.DocumentMasters.AddRange(masterA, masterB);
        db.DocumentRevisions.AddRange(revA, revB);
        db.RevisionFiles.Add(fileB);
        db.DocumentReviewTasks.Add(reviewTaskA);
        db.DocumentApprovalTasks.Add(approvalTaskA);
        await db.SaveChangesAsync();

        Assert.False(await authService.CanAccessSourceFileAsync(fileB.Id, reviewer.Id));
        Assert.False(await authService.CanAccessSourceFileAsync(fileB.Id, approver.Id));
    }

    [Fact]
    public async Task CanUploadOrReplaceDraftFile_ReviewerAndApprover_Denied()
    {
        using var db = CreateInMemoryDbContext();
        var authService = new DocumentAuthorizationService(db);

        var reviewerRole = new Role { Id = 1, Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
        var analystRole = new Role { Id = 2, Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        db.Roles.AddRange(reviewerRole, analystRole);

        var reviewer = new User { Id = 10, FullName = "Reviewer One", RoleId = reviewerRole.Id, Role = reviewerRole, IsActive = true };
        var approver = new User { Id = 20, FullName = "Approver One", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        db.Users.AddRange(reviewer, approver);

        var master = new DocumentMaster { Id = 1, Title = "SOP-1", DocumentOwnerUserId = 99 };
        var rev = new DocumentRevision { Id = 2, DocumentMasterId = master.Id, DocumentMaster = master, RevisionNumber = "01", RevisionStatus = DocumentRevisionStatus.Draft };
        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        Assert.False(await authService.CanUploadOrReplaceDraftFileAsync(rev.Id, reviewer.Id));
        Assert.False(await authService.CanUploadOrReplaceDraftFileAsync(rev.Id, approver.Id));
    }

    [Fact]
    public async Task UploadRevisionFile_ValidDocxAndDoc_AcceptsAndCanonicalizesMimeType()
    {
        using var db = CreateInMemoryDbContext();
        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var adminRole = new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "Admin", IsActive = true };
        var admin = new User { Id = 1, FullName = "Admin User", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Roles.Add(adminRole);
        db.Users.Add(admin);

        var master = new DocumentMaster { Id = 1, Title = "SOP-Word", DocumentOwnerUserId = admin.Id };
        var rev = new DocumentRevision { Id = 10, DocumentMasterId = 1, DocumentMaster = master, RevisionNumber = "01", RevisionStatus = DocumentRevisionStatus.Draft };
        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        var docxContent = CreateValidDocx();
        var docxResult = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "test.docx", "application/octet-stream", docxContent, admin.Id);

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", docxResult.ContentType);
        Assert.Equal(1, docxResult.FileVersion);
        Assert.True(docxResult.IsActive);

        var docContent = CreateValidDoc();
        var docResult = await fileService.UploadRevisionFileAsync(
            rev.Id, FileRole.SourceFile, "test2.doc", "application/octet-stream", docContent, admin.Id);

        Assert.Equal("application/msword", docResult.ContentType);
        Assert.Equal(2, docResult.FileVersion);
        Assert.True(docResult.IsActive);
    }

    [Fact]
    public async Task UploadRevisionFile_InvalidMagicBytesOrNonWord_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var adminRole = new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "Admin", IsActive = true };
        var admin = new User { Id = 1, FullName = "Admin User", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Roles.Add(adminRole);
        db.Users.Add(admin);

        var master = new DocumentMaster { Id = 1, Title = "SOP-Word", DocumentOwnerUserId = admin.Id };
        var rev = new DocumentRevision { Id = 10, DocumentMasterId = 1, DocumentMaster = master, RevisionNumber = "01", RevisionStatus = DocumentRevisionStatus.Draft };
        db.DocumentMasters.Add(master);
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        // Non-word extension
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7 sample");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "fake.pdf", "application/pdf", pdfBytes, admin.Id));

        // Corrupted docx (wrong magic bytes)
        var corruptedDocx = Encoding.UTF8.GetBytes("This is plain text pretending to be docx");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.UploadRevisionFileAsync(rev.Id, FileRole.SourceFile, "fake.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", corruptedDocx, admin.Id));
        Assert.Contains("valid Word document", ex.Message);
    }
}
