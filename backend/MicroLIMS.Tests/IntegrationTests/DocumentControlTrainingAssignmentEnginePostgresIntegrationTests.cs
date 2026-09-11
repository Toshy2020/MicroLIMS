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
public class DocumentControlTrainingAssignmentEnginePostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlTrainingAssignmentEnginePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private class PostgresSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 90000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _seq));
        }
    }

    private async Task<(int MasterId, int Rev1Id)> SeedMasterWithEffectiveRevisionAsync(MicroLimsDbContext db)
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
            sec = new DocumentSection { DepartmentId = dept.Id, Name = "Microbiology", IsActive = true };
            db.DocumentSections.Add(sec);
            await db.SaveChangesAsync();
        }

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-ENG-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-ENG-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "Postgres Engine Test SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = _fixture.SeededUserId,
            CreatedByUserId = _fixture.SeededUserId,
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
            EffectiveDate = DateTime.UtcNow.AddDays(-20),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentRevisions.Add(rev1);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        return (master.Id, rev1.Id);
    }

    [PostgresFact]
    public async Task Postgres_UniqueIndex_EnforcesSingleAssignmentPerUserRevisionAndType()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, rev1Id) = await SeedMasterWithEffectiveRevisionAsync(db);

        var assignment1 = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = rev1Id,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.DocumentTrainingAssignments.Add(assignment1);
        await db.SaveChangesAsync();

        // Attempt duplicate assignment with exact same (RevisionId, UserId, AssignmentType)
        var duplicateAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = rev1Id,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.DocumentTrainingAssignments.Add(duplicateAssignment);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [PostgresFact]
    public async Task Postgres_EndToEndCascade_WithEffectiveDateWorkerActivation()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, rev1Id) = await SeedMasterWithEffectiveRevisionAsync(db);

        // Seed role curriculum
        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = user.RoleId,
            Name = $"Curriculum-{Guid.NewGuid():N}".Substring(0, 16),
            IsActive = true,
            CreatedByUserId = user.Id
        };
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        db.DocumentRoleCurriculumItems.Add(new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = curriculum.Id,
            DocumentMasterId = masterId,
            IsMandatory = true
        });
        await db.SaveChangesAsync();

        // 1. Completed assignment on Rev 01 for user
        var oldCompletedAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = rev1Id,
            AssignedUserId = user.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Acknowledged,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-20),
            DueDateUtc = DateTime.UtcNow.AddDays(-6),
            AcknowledgedAtUtc = DateTime.UtcNow.AddDays(-15),
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentTrainingAssignments.Add(oldCompletedAssignment);
        await db.SaveChangesAsync();

        // 2. Add Revision 02 as FutureEffective with past effective date (matured)
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = masterId,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = DateTime.UtcNow.AddMinutes(-5),
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();

        // Setup services
        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);
        var effectiveDateService = new DocumentEffectiveDateService(
            db,
            audit,
            NullLogger<DocumentEffectiveDateService>.Instance,
            trainingService);

        // Execute matured revisions activation
        var result = await effectiveDateService.ProcessMaturedRevisionsAsync(DateTime.UtcNow);

        Assert.Equal(1, result.SuccessfullyActivatedCount);

        // Verify Rev 02 is Effective in Postgres
        await using var verifyDb = _fixture.CreateDbContext();
        var refreshedRev2 = await verifyDb.DocumentRevisions.FirstAsync(r => r.Id == rev2.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshedRev2.RevisionStatus);

        var refreshedRev1 = await verifyDb.DocumentRevisions.FirstAsync(r => r.Id == rev1Id);
        Assert.Equal(DocumentRevisionStatus.Superseded, refreshedRev1.RevisionStatus);

        // Verify that new assignment was created on Rev 02 linked to old assignment via SourceAssignmentId
        var newAssignment = await verifyDb.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev2.Id && a.AssignedUserId == user.Id);

        Assert.NotNull(newAssignment);
        Assert.Equal(oldCompletedAssignment.Id, newAssignment.SourceAssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Assigned, newAssignment.Status);
    }

    [PostgresFact]
    public async Task Postgres_Obsolescence_CancelsOpenAssignmentsAcrossRevisions()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, rev1Id) = await SeedMasterWithEffectiveRevisionAsync(db);

        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = rev1Id,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.DocumentTrainingAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var audit = new AuditEventService(db, new PostgresSequenceHelper());
        var trainingService = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        int cancelled = await trainingService.HandleDocumentObsolescenceAsync(
            documentMasterId: masterId,
            reason: "Superseded by corporate SOP",
            actingUserId: _fixture.SeededUserId);

        Assert.Equal(1, cancelled);

        await using var verifyDb = _fixture.CreateDbContext();
        var refreshed = await verifyDb.DocumentTrainingAssignments.FirstAsync(a => a.Id == assignment.Id);
        Assert.Equal(TrainingAssignmentStatus.Cancelled, refreshed.Status);
        Assert.Equal("Superseded by corporate SOP", refreshed.ClosedReason);
    }
}
