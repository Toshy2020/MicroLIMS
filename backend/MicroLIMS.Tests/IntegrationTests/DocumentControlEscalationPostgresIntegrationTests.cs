using System;
using System.Linq;
using System.Threading;
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
public class DocumentControlEscalationPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlEscalationPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class PostgresSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 98000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _seq));
        }
    }

    private async Task<(int MasterId, int RevisionId, int AssignmentId)> SeedEscalationScenarioAsync(
        MicroLimsDbContext db,
        DateTime dueDateUtc)
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
            MicroLimsDocumentId = $"DOC-ESC-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-ESC-{Guid.NewGuid():N}".Substring(0, 15),
            Title = "Escalation Integration Test Master",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = _fixture.SeededUserId,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddDays(-20),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev.Id;
        await db.SaveChangesAsync();

        var config = new DocumentTrainingConfiguration
        {
            DocumentMasterId = master.Id,
            RequiresReading = true,
            RequiresRetrainingOnRevision = true,
            DefaultGracePeriodDays = 14,
            EscalationDaysBeforeDue = 3,
            EscalationDaysAfterDue = 1,
            ModifiedByUserId = _fixture.SeededUserId,
            ModifiedAtUtc = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentTrainingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev.Id,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-10),
            DueDateUtc = dueDateUtc,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };
        db.DocumentTrainingAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return (master.Id, rev.Id, assignment.Id);
    }

    [PostgresFact]
    public async Task Postgres_ProcessDueEscalations_PersistsRecordsAndSetsOverdueStatus()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var (masterId, revId, assignmentId) = await SeedEscalationScenarioAsync(db, dueDateUtc: nowUtc.AddDays(-2));

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var service = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        var result = await service.ProcessDueEscalationsAsync(nowUtc);

        Assert.True(result.TotalEscalationsCreated >= 2); // Due + Overdue
        Assert.Equal(0, result.FailedCount);

        // Verify with a fresh context against PostgreSQL
        await using var verifyDb = _fixture.CreateDbContext();
        var records = await verifyDb.DocumentEscalationRecords
            .Where(e => e.DocumentTrainingAssignmentId == assignmentId)
            .ToListAsync();

        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.EscalationLevel == DocumentEscalationLevel.Due);
        Assert.Contains(records, r => r.EscalationLevel == DocumentEscalationLevel.Overdue);

        var updatedAssignment = await verifyDb.DocumentTrainingAssignments.FindAsync(assignmentId);
        Assert.Equal(TrainingAssignmentStatus.Overdue, updatedAssignment!.Status);
    }

    [PostgresFact]
    public async Task Postgres_UniqueIndex_BlocksDuplicateEscalationForSameAssignmentAndLevel()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var (masterId, revId, assignmentId) = await SeedEscalationScenarioAsync(db, dueDateUtc: nowUtc.AddDays(-2));

        var record1 = new DocumentEscalationRecord
        {
            DocumentTrainingAssignmentId = assignmentId,
            DocumentMasterId = masterId,
            DocumentRevisionId = revId,
            AssignedUserId = _fixture.SeededUserId,
            DueDateUtc = nowUtc.AddDays(-2),
            EscalationLevel = DocumentEscalationLevel.Due,
            Status = DocumentEscalationStatus.Raised,
            ScheduledTriggerUtc = nowUtc.AddDays(-2),
            ExecutedAtUtc = nowUtc,
            RecipientRoleOrTarget = "AssignedUser",
            EscalationReason = "Integration test 1",
            ProcessName = "TestWorker",
            CreatedAtUtc = nowUtc
        };
        db.DocumentEscalationRecords.Add(record1);
        await db.SaveChangesAsync();

        // Attempt to add a second record for the same assignment and level in a fresh DbContext
        await using var duplicateDb = _fixture.CreateDbContext();
        var record2 = new DocumentEscalationRecord
        {
            DocumentTrainingAssignmentId = assignmentId, // Duplicate
            DocumentMasterId = masterId,
            DocumentRevisionId = revId,
            AssignedUserId = _fixture.SeededUserId,
            DueDateUtc = nowUtc.AddDays(-2),
            EscalationLevel = DocumentEscalationLevel.Due, // Same level
            Status = DocumentEscalationStatus.Raised,
            ScheduledTriggerUtc = nowUtc.AddDays(-2),
            ExecutedAtUtc = nowUtc,
            RecipientRoleOrTarget = "AssignedUser",
            EscalationReason = "Integration test 2 duplicate",
            ProcessName = "TestWorker",
            CreatedAtUtc = nowUtc
        };
        duplicateDb.DocumentEscalationRecords.Add(record2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [PostgresFact]
    public async Task Postgres_SchedulerCatchUp_MissedWindowsExecuteOnceWithoutDuplication()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var (masterId, revId, assignmentId) = await SeedEscalationScenarioAsync(db, dueDateUtc: nowUtc.AddHours(-1));

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var service = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);

        // Cycle 1: System recovers from downtime and catches up
        var result1 = await service.ProcessDueEscalationsAsync(nowUtc);
        Assert.True(result1.TotalEscalationsCreated >= 1);

        // Cycle 2: Next scheduler tick 1 hour later
        var result2 = await service.ProcessDueEscalationsAsync(nowUtc.AddHours(1));
        Assert.Equal(0, result2.TotalEscalationsCreated);
        Assert.True(result2.SkippedAlreadyEscalatedCount >= 1);
    }

    [PostgresFact]
    public async Task Postgres_AcknowledgedAssignment_HaltsFurtherOverdueEscalations()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var (masterId, revId, assignmentId) = await SeedEscalationScenarioAsync(db, dueDateUtc: nowUtc.AddMinutes(-5));

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var escalationService = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);
        var ackService = new DocumentAcknowledgementService(db, audit, NullLogger<DocumentAcknowledgementService>.Instance);

        // 1. Initial escalation at Due Date
        var escResult1 = await escalationService.ProcessDueEscalationsAsync(nowUtc);
        Assert.Equal(1, escResult1.DueCreated);

        // 2. User consciously acknowledges assignment
        var ackResult = await ackService.AcknowledgeAssignmentAsync(
            new AcknowledgementSubmissionRequest(assignmentId, revId, ConfirmedLegalStatement: true),
            actingUserId: _fixture.SeededUserId,
            utcNowOverride: nowUtc);

        Assert.Equal(TrainingAssignmentStatus.Acknowledged, ackResult.Status);

        // 3. Time passes past T+1 day (Overdue window)
        var futureUtc = nowUtc.AddDays(2);
        var escResult2 = await escalationService.ProcessDueEscalationsAsync(futureUtc);

        // Acknowledged assignment must NOT receive overdue escalation
        Assert.Equal(0, escResult2.OverdueCreated);

        // 4. Historical Due escalation record remains intact
        await using var verifyDb = _fixture.CreateDbContext();
        var historicalEscalations = await verifyDb.DocumentEscalationRecords
            .Where(e => e.DocumentTrainingAssignmentId == assignmentId)
            .ToListAsync();

        Assert.Single(historicalEscalations);
        Assert.Equal(DocumentEscalationLevel.Due, historicalEscalations[0].EscalationLevel);
    }

    [PostgresFact]
    public async Task Postgres_RetrainingCascade_PreservesHistoricalEscalationsOnOldRevision()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var (masterId, rev1Id, assignment1Id) = await SeedEscalationScenarioAsync(db, dueDateUtc: nowUtc.AddDays(-2));

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var escalationService = new DocumentEscalationService(db, audit, NullLogger<DocumentEscalationService>.Instance);
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        // Escalate Rev 01 assignment
        await escalationService.ProcessDueEscalationsAsync(nowUtc);

        // Issue Revision 02
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = masterId,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = nowUtc,
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();

        var rev1 = await db.DocumentRevisions.FindAsync(rev1Id);
        rev1!.RevisionStatus = DocumentRevisionStatus.Superseded;

        var master = await db.DocumentMasters.FindAsync(masterId);
        master!.CurrentEffectiveRevisionId = rev2.Id;
        await db.SaveChangesAsync();

        // Process retraining cascade
        var cascadeResult = await trainingService.ProcessEffectiveRevisionCascadeAsync(
            effectiveRevisionId: rev2.Id,
            supersededRevisionId: rev1Id,
            utcNowOverride: nowUtc);

        // Verify Rev 01 escalation records are preserved and still point to Rev 01
        await using var verifyDb = _fixture.CreateDbContext();
        var rev1Escalations = await verifyDb.DocumentEscalationRecords
            .Where(e => e.DocumentTrainingAssignmentId == assignment1Id)
            .ToListAsync();

        Assert.NotEmpty(rev1Escalations);
        Assert.All(rev1Escalations, e => Assert.Equal(rev1Id, e.DocumentRevisionId));

        // Verify Rev 01 assignment is SupersededIncomplete
        var oldAssignment = await verifyDb.DocumentTrainingAssignments.FindAsync(assignment1Id);
        Assert.Equal(TrainingAssignmentStatus.SupersededIncomplete, oldAssignment!.Status);
    }
}
