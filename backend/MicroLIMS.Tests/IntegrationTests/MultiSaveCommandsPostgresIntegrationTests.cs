using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Commands that save a record and then its audit event (or any other
// follow-on rows) in separate saves run as one transaction, so a failure
// at the audit event leaves no record behind that has no audit entry.
[Collection("PostgresDatabaseCollection")]
public class MultiSaveCommandsPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public MultiSaveCommandsPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Fails the save that writes an audit event whose action code starts
    // with the given text - the save after the record itself.
    private sealed class FailAuditEventSave(string actionCodePrefix) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>()
                .Any(e => e.State == EntityState.Added && e.Entity.ActionCode != null && e.Entity.ActionCode.StartsWith(actionCodePrefix)))
                throw new InvalidOperationException("audit event save failed");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [PostgresFact]
    public async Task CreateDepartment_FailingAtTheAuditEvent_SavesNoDepartment()
    {
        var code = "D" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
        await using (var db = _fixture.CreateDbContext(new FailAuditEventSave("DocumentDepartmentCreated")))
        {
            var service = new DocumentConfigurationService(db, new AuditEventService(db, new DatabaseSequenceHelper(db)), new DocumentAuthorizationService(db));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateDepartmentAsync(new CreateDocumentDepartmentRequest(code, "Transaction test"), _fixture.SeededUserId));
        }

        await using var check = _fixture.CreateDbContext();
        Assert.False(await check.DocumentDepartments.AnyAsync(d => d.Code == code));
    }

    [PostgresFact]
    public async Task ProcessDueEscalations_FailingAtTheAuditEvent_SavesNoEscalation()
    {
        var nowUtc = DateTime.UtcNow;
        int assignmentId;
        await using (var seed = _fixture.CreateDbContext())
            assignmentId = await SeedOverdueAssignmentAsync(seed, nowUtc.AddDays(-2));

        await using (var db = _fixture.CreateDbContext(new FailAuditEventSave("TrainingEscalationRaised_")))
        {
            var service = new DocumentEscalationService(db, new AuditEventService(db, new DatabaseSequenceHelper(db)), NullLogger<DocumentEscalationService>.Instance);

            var result = await service.ProcessDueEscalationsAsync(nowUtc);

            Assert.Equal(0, result.TotalEscalationsCreated);
            Assert.True(result.FailedCount > 0);
        }

        await using var check = _fixture.CreateDbContext();
        Assert.False(await check.DocumentEscalationRecords.AnyAsync(e => e.DocumentTrainingAssignmentId == assignmentId));
    }

    private async Task<int> SeedOverdueAssignmentAsync(MicroLIMS.Persistence.DbContext.MicroLimsDbContext db, DateTime dueDateUtc)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var docType = new DocumentType { Code = $"T{suffix[..5]}", Name = "Transaction test type", DefaultReviewCycleMonths = 24, IsActive = true };
        var department = new DocumentDepartment { Code = $"D{suffix[..5]}", Name = "Transaction test department", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(department);
        await db.SaveChangesAsync();

        var section = new DocumentSection { DepartmentId = department.Id, Code = $"S{suffix[..5]}", Name = "Transaction test section", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-TX-{suffix}",
            CompanyDocumentCode = $"SOP-TX-{suffix}",
            Title = "Transaction test SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = department.Id,
            SectionId = section.Id,
            DocumentOwnerUserId = _fixture.SeededUserId,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddDays(-20),
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = revision.Id;
        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = revision.Id,
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
        return assignment.Id;
    }
}
