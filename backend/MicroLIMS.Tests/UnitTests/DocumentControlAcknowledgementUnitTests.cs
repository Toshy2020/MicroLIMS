using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlAcknowledgementUnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _current = 6000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _current));
        }
    }

    private async Task<(
        MicroLimsDbContext db,
        User admin,
        User analyst1,
        User analyst2,
        DocumentMaster master,
        DocumentRevision rev1,
        RevisionFile pdfFile,
        DocumentTrainingAssignment assignment1,
        DocumentAcknowledgementService service
    )> CreateTestEnvironmentAsync(string? customStatement = null)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        var analystRole = new Role { Type = RoleType.Analyst, Name = "QC Analyst", IsActive = true };
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "System Admin", IsActive = true };
        db.Roles.AddRange(analystRole, adminRole);
        await db.SaveChangesAsync();

        var admin = new User { FullName = "Admin User", Username = "admin1", RoleId = adminRole.Id, IsActive = true, Role = adminRole };
        var analyst1 = new User { FullName = "Jane Doe", Username = "jdoe", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        var analyst2 = new User { FullName = "John Smith", Username = "jsmith", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        db.Users.AddRange(admin, analyst1, analyst2);
        await db.SaveChangesAsync();

        var docType = new DocumentType
        {
            Code = "SOP",
            Name = "Standard Operating Procedure",
            DefaultReviewCycleMonths = 24,
            IsActive = true
        };
        db.DocumentTypes.Add(docType);
        await db.SaveChangesAsync();

        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec = new DocumentSection { DepartmentId = dept.Id, Name = "Analytical", IsActive = true };
        db.DocumentSections.Add(sec);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-ACK-001",
            CompanyDocumentCode = "SOP-QC-0101",
            Title = "HPLC Calibration and Operation",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = admin.Id,
            CreatedByUserId = admin.Id,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev1 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddDays(-10),
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        db.DocumentRevisions.Add(rev1);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        var pdfFile = new RevisionFile
        {
            DocumentRevisionId = rev1.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "SOP-QC-0101_Rev01.pdf",
            ContentType = "application/pdf",
            SizeBytes = 450000,
            ContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            IsActive = true,
            UploadedByUserId = admin.Id,
            UploadedAt = DateTime.UtcNow.AddDays(-10)
        };
        db.RevisionFiles.Add(pdfFile);
        await db.SaveChangesAsync();

        if (customStatement != null)
        {
            var config = new DocumentTrainingConfiguration
            {
                DocumentMasterId = master.Id,
                RequiresReading = true,
                RequiresRetrainingOnRevision = true,
                DefaultGracePeriodDays = 14,
                DefaultAcknowledgementStatement = customStatement,
                ModifiedByUserId = admin.Id,
                ModifiedAtUtc = DateTime.UtcNow
            };
            db.DocumentTrainingConfigurations.Add(config);
            await db.SaveChangesAsync();
        }

        var assignment1 = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst1.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-5),
            DueDateUtc = DateTime.UtcNow.AddDays(9),
            CreatedByUserId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };
        db.DocumentTrainingAssignments.Add(assignment1);
        await db.SaveChangesAsync();

        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var service = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        return (db, admin, analyst1, analyst2, master, rev1, pdfFile, assignment1, service);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_ValidSubmission_RecordsEvidenceAndUpdatesStatus()
    {
        var (db, admin, analyst1, _, master, rev1, pdfFile, assignment1, service) =
            await CreateTestEnvironmentAsync();

        var fixedTime = new DateTime(2026, 9, 4, 15, 0, 0, DateTimeKind.Utc);
        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true,
            Comments: "Thoroughly reviewed sections 1 through 8.",
            ClientIpAddress: "192.168.1.50",
            UserAgent: "Mozilla/5.0 Chrome/120"
        );

        var result = await service.AcknowledgeAssignmentAsync(
            request,
            actingUserId: analyst1.Id,
            utcNowOverride: fixedTime);

        // Assert DTO result
        Assert.NotNull(result);
        Assert.True(result.AcknowledgementRecordId > 0);
        Assert.Equal(assignment1.Id, result.AssignmentId);
        Assert.Equal(rev1.Id, result.DocumentRevisionId);
        Assert.Equal(analyst1.Id, result.UserId);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, result.Status);
        Assert.Equal(fixedTime, result.AcknowledgedAtUtc);
        Assert.Equal(pdfFile.ContentSha256, result.ControlledFileHash);
        Assert.Contains("read, understood, and agree", result.StatementText);

        // Assert Assignment entity update
        await db.Entry(assignment1).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, assignment1.Status);
        Assert.Equal(fixedTime, assignment1.AcknowledgedAtUtc);
        Assert.Equal(analyst1.Id, assignment1.AcknowledgedByUserId);
        Assert.Equal(result.StatementText, assignment1.StatementText);
        Assert.Equal(fixedTime, assignment1.CompletedAtUtc);

        // Assert DocumentAcknowledgementRecord persisted in database
        var record = await db.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.Id == result.AcknowledgementRecordId);

        Assert.NotNull(record);
        Assert.Equal(assignment1.Id, record.DocumentTrainingAssignmentId);
        Assert.Equal(master.Id, record.DocumentMasterId);
        Assert.Equal(rev1.Id, record.DocumentRevisionId);
        Assert.Equal(analyst1.Id, record.AcknowledgedByUserId);
        Assert.Equal(fixedTime, record.AcknowledgedAtUtc);
        Assert.Equal(pdfFile.Id, record.ControlledFileId);
        Assert.Equal(pdfFile.ContentSha256, record.ControlledFileHash);
        Assert.Equal("Thoroughly reviewed sections 1 through 8.", record.Comments);
        Assert.Equal("192.168.1.50", record.ClientIpAddress);

        // Assert Audit Log emission
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "ReadingAssignmentAcknowledged");
        Assert.NotNull(audit);
        Assert.Equal(analyst1.Id, audit.UserId);
        Assert.Equal(master.Id, audit.DocumentMasterId);
        Assert.Equal(rev1.Id, audit.DocumentRevisionId);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_CapturesConfiguredCustomStatement()
    {
        string customText = "I formally confirm that I have reviewed SOP-QC-0101 Rev 01 and have received appropriate practical training.";
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync(customStatement: customText);

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        var result = await service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id);

        Assert.Equal(customText, result.StatementText);

        var record = await db.DocumentAcknowledgementRecords.FirstAsync(r => r.Id == result.AcknowledgementRecordId);
        Assert.Equal(customText, record.StatementText);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_ExplicitConfirmationFalse_ThrowsInvalidOperationException()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: false // Unchecked
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id));

        Assert.Contains("Explicit confirmation of the legal acknowledgement statement is required", ex.Message);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_UnassignedUser_ThrowsUnauthorizedAccessExceptionAndLogsAudit()
    {
        var (db, _, analyst1, analyst2, master, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        // analyst2 tries to acknowledge analyst1's assignment
        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst2.Id));

        Assert.Contains("not authorized to acknowledge an assignment assigned to another user", ex.Message);

        var rejectionAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "AcknowledgementRejected");
        Assert.NotNull(rejectionAudit);
        Assert.Equal(analyst2.Id, rejectionAudit.UserId);
        Assert.Contains("SECURITY VIOLATION", rejectionAudit.Reason);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_RevisionMismatch_ThrowsInvalidOperationExceptionAndLogsAudit()
    {
        var (db, admin, analyst1, _, master, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        // Create a different revision Rev 02
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow,
            CreatedByUserId = admin.Id
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();

        // Submit acknowledgement specifying Rev 02 instead of assigned Rev 01
        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev2.Id, // Mismatch
            ConfirmedLegalStatement: true
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id));

        Assert.Contains("Revision mismatch", ex.Message);

        var rejectionAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "AcknowledgementRejected");
        Assert.NotNull(rejectionAudit);
        Assert.Contains("REVISION MISMATCH", rejectionAudit.Reason);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_SupersededIncompleteAssignment_ThrowsInvalidOperationException()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        assignment1.Status = TrainingAssignmentStatus.SupersededIncomplete;
        assignment1.ClosedReason = "Superseded by Revision 02";
        await db.SaveChangesAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id));

        Assert.Contains("terminally closed as SupersededIncomplete", ex.Message);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_CancelledAssignment_ThrowsInvalidOperationException()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        assignment1.Status = TrainingAssignmentStatus.Cancelled;
        assignment1.ClosedReason = "Document Obsoleted";
        await db.SaveChangesAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id));

        Assert.Contains("terminally closed as Cancelled", ex.Message);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_VoidedDocumentMaster_ThrowsInvalidOperationException()
    {
        var (db, _, analyst1, _, master, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        master.RecordStatus = DocumentRecordStatus.Void;
        await db.SaveChangesAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id));

        Assert.Contains("Void", ex.Message);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_DraftRevision_ThrowsInvalidOperationException()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        rev1.RevisionStatus = DocumentRevisionStatus.Draft;
        await db.SaveChangesAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id));

        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public async Task AcknowledgeAssignmentAsync_RepeatCall_IsIdempotentAndDoesNotDuplicateEvidence()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        var request = new AcknowledgementSubmissionRequest(
            AssignmentId: assignment1.Id,
            DocumentRevisionId: rev1.Id,
            ConfirmedLegalStatement: true
        );

        // First call
        var firstResult = await service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id);
        Assert.NotNull(firstResult);

        // Second call (repeat / double-click)
        var secondResult = await service.AcknowledgeAssignmentAsync(request, actingUserId: analyst1.Id);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.AcknowledgementRecordId, secondResult.AcknowledgementRecordId);

        // Assert exactly ONE DocumentAcknowledgementRecord exists in the database
        var totalRecords = await db.DocumentAcknowledgementRecords
            .Where(r => r.DocumentTrainingAssignmentId == assignment1.Id)
            .CountAsync();
        Assert.Equal(1, totalRecords);

        // Assert audit log records idempotency event
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "AcknowledgementAlreadyRecorded");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task RecordReadingProgressAsync_AdvancesStatusToReading_DoesNotAcknowledgeEvenAt100Percent()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        Assert.Equal(TrainingAssignmentStatus.Assigned, assignment1.Status);

        // 1. Progress to 50%
        var req50 = new ReadingProgressUpdateRequest(assignment1.Id, rev1.Id, ProgressPercentage: 50, PageNumber: 5);
        var res50 = await service.RecordReadingProgressAsync(req50, actingUserId: analyst1.Id);

        Assert.Equal(TrainingAssignmentStatus.Reading, res50.Status);
        Assert.Equal(50, res50.ProgressPercentage);

        await db.Entry(assignment1).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.Reading, assignment1.Status);

        // 2. Progress to 100% (CRITICAL DC-URS-190 verification)
        var req100 = new ReadingProgressUpdateRequest(assignment1.Id, rev1.Id, ProgressPercentage: 100, PageNumber: 10);
        var res100 = await service.RecordReadingProgressAsync(req100, actingUserId: analyst1.Id);

        Assert.Equal(TrainingAssignmentStatus.Reading, res100.Status);
        Assert.Equal(100, res100.ProgressPercentage);
        Assert.Contains("strictly informational", res100.Note);

        // Verify status remains Reading and is NOT Acknowledged
        await db.Entry(assignment1).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.Reading, assignment1.Status);
        Assert.Null(assignment1.AcknowledgedAtUtc);
        Assert.Empty(await db.DocumentAcknowledgementRecords.ToListAsync());
    }

    [Fact]
    public async Task PresentAcknowledgementContextAsync_ReturnsCompleteContextAndEligibility()
    {
        var (db, _, analyst1, analyst2, master, rev1, pdfFile, assignment1, service) =
            await CreateTestEnvironmentAsync();

        // When queried by assigned user
        var contextForAssignee = await service.PresentAcknowledgementContextAsync(assignment1.Id, actingUserId: analyst1.Id);
        Assert.True(contextForAssignee.CanAcknowledge);
        Assert.Null(contextForAssignee.ValidationMessage);
        Assert.Equal(master.CompanyDocumentCode, contextForAssignee.CompanyDocumentCode);
        Assert.Equal(rev1.RevisionNumber, contextForAssignee.RevisionNumber);
        Assert.Equal(pdfFile.ContentSha256, contextForAssignee.ControlledFileSha256);
        Assert.Contains("read, understood, and agree", contextForAssignee.LegalStatementText);

        // When queried by different user
        var contextForOther = await service.PresentAcknowledgementContextAsync(assignment1.Id, actingUserId: analyst2.Id);
        Assert.False(contextForOther.CanAcknowledge);
        Assert.Contains("Only the assigned user", contextForOther.ValidationMessage);
    }

    [Fact]
    public async Task GetAcknowledgementStatusAsync_ReturnsNullWhenUnacknowledged_ReturnsRecordWhenAcknowledged()
    {
        var (db, _, analyst1, _, _, rev1, _, assignment1, service) =
            await CreateTestEnvironmentAsync();

        var statusBefore = await service.GetAcknowledgementStatusAsync(assignment1.Id, actingUserId: analyst1.Id);
        Assert.Null(statusBefore);

        await service.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignment1.Id, rev1.Id, ConfirmedLegalStatement: true),
            actingUserId: analyst1.Id);

        var statusAfter = await service.GetAcknowledgementStatusAsync(assignment1.Id, actingUserId: analyst1.Id);
        Assert.NotNull(statusAfter);
        Assert.Equal(assignment1.Id, statusAfter.AssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, statusAfter.Status);
    }

    [Fact]
    public async Task RetrainingCascade_PreservesHistoricalAcknowledgementOnSupersededRevision()
    {
        var (db, admin, analyst1, _, master, rev1, _, assignment1, ackService) =
            await CreateTestEnvironmentAsync();

        // 1. Analyst1 completes Rev 01 acknowledgement
        var ackResultRev1 = await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignment1.Id, rev1.Id, ConfirmedLegalStatement: true),
            actingUserId: analyst1.Id);

        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var trainingAssignmentService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        // 2. Issue Revision 02
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow,
            CreatedByUserId = admin.Id
        };
        db.DocumentRevisions.Add(rev2);
        rev1.RevisionStatus = DocumentRevisionStatus.Superseded;
        master.CurrentEffectiveRevisionId = rev2.Id;
        await db.SaveChangesAsync();

        // 3. Execute WP2 Retraining cascade for Rev 02
        var cascadeResult = await trainingAssignmentService.ProcessEffectiveRevisionCascadeAsync(
            effectiveRevisionId: rev2.Id,
            supersededRevisionId: rev1.Id);

        Assert.Equal(1, cascadeResult.NewAssignmentsCreated);

        // 4. Assert Rev 01 acknowledgement evidence remains completely intact and unmodified
        var rev1AckRecord = await db.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment1.Id);
        Assert.NotNull(rev1AckRecord);
        Assert.Equal(rev1.Id, rev1AckRecord.DocumentRevisionId);
        Assert.Equal(ackResultRev1.AcknowledgedAtUtc, rev1AckRecord.AcknowledgedAtUtc);

        // 5. Assert new assignment on Rev 02 starts in Assigned state and requires its own conscious acknowledgement
        var rev2Assignment = await db.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev2.Id && a.AssignedUserId == analyst1.Id);

        Assert.NotNull(rev2Assignment);
        Assert.Equal(TrainingAssignmentStatus.Assigned, rev2Assignment.Status);
        Assert.Null(rev2Assignment.AcknowledgedAtUtc);
        Assert.Equal(assignment1.Id, rev2Assignment.SourceAssignmentId);

        // 6. Analyst1 acknowledges Rev 02 independently
        var ackResultRev2 = await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(rev2Assignment.Id, rev2.Id, ConfirmedLegalStatement: true),
            actingUserId: analyst1.Id);

        Assert.NotNull(ackResultRev2);
        Assert.Equal(rev2.Id, ackResultRev2.DocumentRevisionId);

        // Assert two distinct evidentiary records exist for the two distinct revisions
        var allRecords = await db.DocumentAcknowledgementRecords
            .Where(r => r.AcknowledgedByUserId == analyst1.Id)
            .OrderBy(r => r.DocumentRevisionId)
            .ToListAsync();

        Assert.Equal(2, allRecords.Count);
        Assert.Equal(rev1.Id, allRecords[0].DocumentRevisionId);
        Assert.Equal(rev2.Id, allRecords[1].DocumentRevisionId);
    }
}
