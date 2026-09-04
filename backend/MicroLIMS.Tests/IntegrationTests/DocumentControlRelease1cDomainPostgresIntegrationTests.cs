using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlRelease1cDomainPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlRelease1cDomainPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int MasterId, int RevisionId)> SeedDocumentAsync(MicroLimsDbContext db)
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
            MicroLimsDocumentId = $"DOC-R1C-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-R1C-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "Release 1c Controlled Test SOP",
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
            EffectiveDate = DateTime.UtcNow,
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev.Id;
        await db.SaveChangesAsync();

        return (master.Id, rev.Id);
    }

    [Fact]
    public async Task Postgres_PersistsTrainingAssignment_WithValidForeignKeys()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revisionId) = await SeedDocumentAsync(db);

        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = revisionId,
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

        Assert.True(assignment.Id > 0);

        var retrieved = await db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
            .Include(a => a.DocumentRevision)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.Id == assignment.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(masterId, retrieved.DocumentMasterId);
        Assert.Equal(revisionId, retrieved.DocumentRevisionId);
        Assert.Equal(_fixture.SeededUserId, retrieved.AssignedUserId);
        Assert.Equal(TrainingAssignmentStatus.Assigned, retrieved.Status);
    }

    [Fact]
    public async Task Postgres_PreventsDuplicateActiveAssignment_PerUserRevisionAndType()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revisionId) = await SeedDocumentAsync(db);

        var assignment1 = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = revisionId,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentTrainingAssignments.Add(assignment1);
        await db.SaveChangesAsync();

        var duplicateAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = revisionId,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentTrainingAssignments.Add(duplicateAssignment);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task Postgres_RejectsAssignment_WithInvalidRevisionForeignKey()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, _) = await SeedDocumentAsync(db);

        var invalidAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = 999999, // Non-existent revision
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId
        };

        db.DocumentTrainingAssignments.Add(invalidAssignment);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task Postgres_PersistsTrainingConfiguration_ForDocumentTypeAndMaster()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, _) = await SeedDocumentAsync(db);

        var config = new DocumentTrainingConfiguration
        {
            DocumentMasterId = masterId,
            RequiresReading = true,
            RequiresRetrainingOnRevision = true,
            DefaultGracePeriodDays = 10,
            DefaultAcknowledgementStatement = "Custom statement for master SOP",
            EscalationDaysBeforeDue = 2,
            EscalationDaysAfterDue = 1,
            ModifiedByUserId = _fixture.SeededUserId,
            ModifiedAtUtc = DateTime.UtcNow
        };

        db.DocumentTrainingConfigurations.Add(config);
        await db.SaveChangesAsync();

        Assert.True(config.Id > 0);

        var retrieved = await db.DocumentTrainingConfigurations
            .FirstOrDefaultAsync(c => c.Id == config.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(masterId, retrieved.DocumentMasterId);
        Assert.Equal(10, retrieved.DefaultGracePeriodDays);
        Assert.Equal("Custom statement for master SOP", retrieved.DefaultAcknowledgementStatement);
    }

    [Fact]
    public async Task Postgres_PersistsRoleCurriculum_AndItems_WithCascade()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, _) = await SeedDocumentAsync(db);

        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SystemAdministrator);

        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = role.Id,
            Name = "System Admin QA Curriculum",
            Description = "Automated test curriculum",
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var item = new DocumentRoleCurriculumItem
        {
            DocumentMasterId = masterId,
            IsMandatory = true,
            CustomGracePeriodDays = 21,
            AddedAtUtc = DateTime.UtcNow
        };
        curriculum.Items.Add(item);

        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        Assert.True(curriculum.Id > 0);
        Assert.True(item.Id > 0);

        // Delete curriculum, verify item cascades
        db.DocumentRoleCurricula.Remove(curriculum);
        await db.SaveChangesAsync();

        var orphanItem = await db.DocumentRoleCurriculumItems.FirstOrDefaultAsync(i => i.Id == item.Id);
        Assert.Null(orphanItem);
    }

    [Fact]
    public async Task Postgres_TransactionRollback_PreservesDataIntegrity()
    {
        await using var db = _fixture.CreateDbContext();
        var (masterId, revisionId) = await SeedDocumentAsync(db);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = masterId,
            DocumentRevisionId = revisionId,
            AssignedUserId = _fixture.SeededUserId,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentTrainingAssignments.Add(assignment);
        await db.SaveChangesAsync();

        // Roll back the transaction
        await transaction.RollbackAsync();

        var notFound = await db.DocumentTrainingAssignments.FirstOrDefaultAsync(a => a.Id == assignment.Id);
        Assert.Null(notFound);
    }
}
