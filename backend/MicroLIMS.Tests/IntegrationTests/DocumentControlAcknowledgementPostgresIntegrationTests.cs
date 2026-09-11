using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlAcknowledgementPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlAcknowledgementPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class PostgresSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 95000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _seq));
        }
    }

    private async Task<(int MasterId, int RevisionId, int FileId, int AssignmentId)> SeedAcknowledgementScenarioAsync(MicroLimsDbContext db)
    {
        var docType = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Code == "SOP");
        if (docType == null)
        {
            docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
            db.DocumentTypes.Add(docType);
            await db.SaveChangesAsync();
        }

        var dept = await db.DocumentDepartments.FirstOrDefaultAsync(d => d.Code == "QC");
        if (dept == null)
        {
            dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
            db.DocumentDepartments.Add(dept);
            await db.SaveChangesAsync();
        }

        var sec = await db.DocumentSections.FirstOrDefaultAsync(s => s.DepartmentId == dept.Id);
        if (sec == null)
        {
            sec = new DocumentSection { DepartmentId = dept.Id, Name = "Analytical", IsActive = true };
            db.DocumentSections.Add(sec);
            await db.SaveChangesAsync();
        }

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-ACK-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-ACK-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "Postgres Acknowledgement Test SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = _fixture.SeededUserId,
            CreatedByUserId = _fixture.SeededUserId,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddDays(-5),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev.Id;
        await db.SaveChangesAsync();

        var file = new RevisionFile
        {
            DocumentRevisionId = rev.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "SOP-ACK-Test_Rev01.pdf",
            ContentType = "application/pdf",
            SizeBytes = 250000,
            ContentSha256 = "f4c2e1b3a567890abcdef1234567890abcdef1234567890abcdef1234567890a",
            IsActive = true,
            UploadedByUserId = _fixture.SeededUserId,
            UploadedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.RevisionFiles.Add(file);
        await db.SaveChangesAsync();

        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev.Id,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-3),
            DueDateUtc = DateTime.UtcNow.AddDays(11),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        db.DocumentTrainingAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return (master.Id, rev.Id, file.Id, assignment.Id);
    }

    [PostgresFact]
    public async Task Postgres_AcknowledgeAssignment_PersistsEvidentiaryRecord_AndUpdatesAssignment()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, fileId, assignmentId) = await SeedAcknowledgementScenarioAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var service = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        var req = new AcknowledgementSubmissionRequest(
            AssignmentId: assignmentId,
            DocumentRevisionId: revId,
            ConfirmedLegalStatement: true,
            Comments: "Postgres live test acknowledgement",
            ClientIpAddress: "127.0.0.1",
            UserAgent: "xUnit Integration Test"
        );

        var result = await service.AcknowledgeAssignmentAsync(req, actingUserId: _fixture.SeededUserId);

        Assert.NotNull(result);
        Assert.True(result.AcknowledgementRecordId > 0);
        Assert.Equal(assignmentId, result.AssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, result.Status);

        // Verify with fresh DbContext from PostgreSQL
        await using var verifyDb = _fixture.CreateDbContext();
        var record = await verifyDb.DocumentAcknowledgementRecords
            .Include(r => r.DocumentTrainingAssignment)
            .Include(r => r.DocumentRevision)
            .Include(r => r.AcknowledgedByUser)
            .FirstOrDefaultAsync(r => r.Id == result.AcknowledgementRecordId);

        Assert.NotNull(record);
        Assert.Equal(assignmentId, record.DocumentTrainingAssignmentId);
        Assert.Equal(masterId, record.DocumentMasterId);
        Assert.Equal(revId, record.DocumentRevisionId);
        Assert.Equal(_fixture.SeededUserId, record.AcknowledgedByUserId);
        Assert.NotNull(record.StatementText);
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, record.DocumentTrainingAssignment.Status);
    }

    [PostgresFact]
    public async Task Postgres_DatabaseTrigger_BlocksDirectUpdateAndDeleteOnAcknowledgementRecords()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, fileId, assignmentId) = await SeedAcknowledgementScenarioAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var service = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        var req = new AcknowledgementSubmissionRequest(
            AssignmentId: assignmentId,
            DocumentRevisionId: revId,
            ConfirmedLegalStatement: true
        );

        var result = await service.AcknowledgeAssignmentAsync(req, actingUserId: _fixture.SeededUserId);

        // Test 1: Attempt direct SQL UPDATE on DocumentAcknowledgementRecords
        await using var directSqlDb = _fixture.CreateDbContext();
        var updateEx = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            directSqlDb.Database.ExecuteSqlRawAsync(
                @"UPDATE ""DocumentAcknowledgementRecords"" SET ""StatementText"" = 'TAMPERED STATEMENT' WHERE ""Id"" = {0}",
                result.AcknowledgementRecordId));

        Assert.Contains("append-only", updateEx.Message, StringComparison.OrdinalIgnoreCase);

        // Test 2: Attempt direct SQL DELETE on DocumentAcknowledgementRecords
        var deleteEx = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            directSqlDb.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""DocumentAcknowledgementRecords"" WHERE ""Id"" = {0}",
                result.AcknowledgementRecordId));

        Assert.Contains("append-only", deleteEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Postgres_UniqueIndex_PreventsDuplicateAcknowledgementRecordsForSameAssignment()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revId, fileId, assignmentId) = await SeedAcknowledgementScenarioAsync(db);

        var record1 = new DocumentAcknowledgementRecord
        {
            DocumentTrainingAssignmentId = assignmentId,
            DocumentMasterId = masterId,
            DocumentRevisionId = revId,
            AcknowledgedByUserId = _fixture.SeededUserId,
            StatementText = "First acknowledgement record",
            AcknowledgedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.DocumentAcknowledgementRecords.Add(record1);
        await db.SaveChangesAsync();

        // Attempt to insert second acknowledgement record for same assignment using fresh DbContext
        await using var duplicateDb = _fixture.CreateDbContext();
        var record2 = new DocumentAcknowledgementRecord
        {
            DocumentTrainingAssignmentId = assignmentId, // Duplicate assignment ID
            DocumentMasterId = masterId,
            DocumentRevisionId = revId,
            AcknowledgedByUserId = _fixture.SeededUserId,
            StatementText = "Second acknowledgement record",
            AcknowledgedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        duplicateDb.DocumentAcknowledgementRecords.Add(record2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [PostgresFact]
    public async Task Postgres_EndToEnd_ReadingProgressToAcknowledgementAndRetrainingPreservation()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, rev1Id, fileId, assignment1Id) = await SeedAcknowledgementScenarioAsync(db);

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        // 1. Reading progress update (50% and 100%) - verify status transitions to Reading, NOT Acknowledged
        await ackService.RecordReadingProgressAsync(
            new ReadingProgressUpdateRequest(assignment1Id, rev1Id, ProgressPercentage: 50),
            actingUserId: _fixture.SeededUserId);

        await using var verifyDb1 = _fixture.CreateDbContext();
        var assignCheck1 = await verifyDb1.DocumentTrainingAssignments.FirstAsync(a => a.Id == assignment1Id);
        Assert.Equal(TrainingAssignmentStatus.Reading, assignCheck1.Status);

        await ackService.RecordReadingProgressAsync(
            new ReadingProgressUpdateRequest(assignment1Id, rev1Id, ProgressPercentage: 100),
            actingUserId: _fixture.SeededUserId);

        await using var verifyDb2 = _fixture.CreateDbContext();
        var assignCheck2 = await verifyDb2.DocumentTrainingAssignments.FirstAsync(a => a.Id == assignment1Id);
        Assert.Equal(TrainingAssignmentStatus.Reading, assignCheck2.Status); // Still Reading!

        // 2. Explicit conscious legal acknowledgement
        var ackResult1 = await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignment1Id, rev1Id, ConfirmedLegalStatement: true),
            actingUserId: _fixture.SeededUserId);

        Assert.Equal(TrainingAssignmentStatus.Acknowledged, ackResult1.Status);

        // 3. Issue Revision 02 and trigger WP2 Retraining cascade
        await using var setupRev2Db = _fixture.CreateDbContext();
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = masterId,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow,
            CreatedByUserId = _fixture.SeededUserId
        };
        setupRev2Db.DocumentRevisions.Add(rev2);
        await setupRev2Db.SaveChangesAsync(); // Generate rev2.Id

        var rev1 = await setupRev2Db.DocumentRevisions.FirstAsync(r => r.Id == rev1Id);
        rev1.RevisionStatus = DocumentRevisionStatus.Superseded;

        var master = await setupRev2Db.DocumentMasters.FirstAsync(m => m.Id == masterId);
        master.CurrentEffectiveRevisionId = rev2.Id;
        await setupRev2Db.SaveChangesAsync();

        var cascadeResult = await trainingService.ProcessEffectiveRevisionCascadeAsync(
            effectiveRevisionId: rev2.Id,
            supersededRevisionId: rev1Id);

        Assert.Equal(1, cascadeResult.NewAssignmentsCreated);

        // 4. Assert Rev 01 acknowledgement record remains completely intact and retrievable in PostgreSQL
        await using var verifyCascadeDb = _fixture.CreateDbContext();
        var historicalAck = await verifyCascadeDb.DocumentAcknowledgementRecords
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment1Id);

        Assert.NotNull(historicalAck);
        Assert.Equal(rev1Id, historicalAck.DocumentRevisionId);

        // 5. Assert Rev 02 assignment is in Assigned state, linked to assignment 1, and requires its own acknowledgement
        var rev2Assignment = await verifyCascadeDb.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev2.Id && a.AssignedUserId == _fixture.SeededUserId);

        Assert.NotNull(rev2Assignment);
        Assert.Equal(TrainingAssignmentStatus.Assigned, rev2Assignment.Status);
        Assert.Equal(assignment1Id, rev2Assignment.SourceAssignmentId);

        // Acknowledge Rev 02
        var ackResult2 = await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(rev2Assignment.Id, rev2.Id, ConfirmedLegalStatement: true),
            actingUserId: _fixture.SeededUserId);

        Assert.Equal(TrainingAssignmentStatus.Acknowledged, ackResult2.Status);
        Assert.NotEqual(historicalAck.Id, ackResult2.AcknowledgementRecordId);
    }
}
