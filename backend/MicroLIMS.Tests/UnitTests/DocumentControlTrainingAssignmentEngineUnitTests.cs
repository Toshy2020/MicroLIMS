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
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlTrainingAssignmentEngineUnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _current = 5000;
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
        User inactiveAnalyst,
        Role analystRole,
        DocumentMaster master,
        DocumentRevision rev1,
        TrainingAssignmentService service
    )> CreateTestEnvironmentAsync(int? defaultGracePeriod = null)
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
        var analyst1 = new User { FullName = "Analyst One", Username = "analyst1", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        var analyst2 = new User { FullName = "Analyst Two", Username = "analyst2", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        var inactiveAnalyst = new User { FullName = "Inactive User", Username = "inactive1", RoleId = analystRole.Id, IsActive = false, Role = analystRole };
        db.Users.AddRange(admin, analyst1, analyst2, inactiveAnalyst);
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
            MicroLimsDocumentId = "DOC-TEST-001",
            CompanyDocumentCode = "SOP-QC-0001",
            Title = "Analytical Testing Procedure",
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
            EffectiveDate = DateTime.UtcNow.AddDays(-30),
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentRevisions.Add(rev1);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        if (defaultGracePeriod.HasValue)
        {
            var config = new DocumentTrainingConfiguration
            {
                DocumentMasterId = master.Id,
                RequiresReading = true,
                RequiresRetrainingOnRevision = true,
                DefaultGracePeriodDays = defaultGracePeriod.Value,
                ModifiedByUserId = admin.Id,
                ModifiedAtUtc = DateTime.UtcNow
            };
            db.DocumentTrainingConfigurations.Add(config);
            await db.SaveChangesAsync();
        }

        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var service = new TrainingAssignmentService(db, audit, NullLogger<TrainingAssignmentService>.Instance);

        return (db, admin, analyst1, analyst2, inactiveAnalyst, analystRole, master, rev1, service);
    }

    [Fact]
    public async Task ProcessEffectiveRevisionCascadeAsync_CurriculumDriven_GeneratesAssignmentsForActiveUsers()
    {
        var (db, admin, analyst1, analyst2, inactiveAnalyst, analystRole, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        // Setup role curriculum mapping SOP-QC-0001 to QC Analyst role
        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = analystRole.Id,
            Name = "QC Analyst Curriculum",
            IsActive = true,
            CreatedByUserId = admin.Id
        };
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        var curriculumItem = new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = curriculum.Id,
            DocumentMasterId = master.Id,
            IsMandatory = true
        };
        db.DocumentRoleCurriculumItems.Add(curriculumItem);
        await db.SaveChangesAsync();

        var result = await service.ProcessEffectiveRevisionCascadeAsync(rev1.Id, supersededRevisionId: null);

        Assert.Equal(2, result.TotalEligibleUsers); // analyst1 and analyst2
        Assert.Equal(2, result.NewAssignmentsCreated);
        Assert.Equal(0, result.ExistingAssignmentsSkipped);
        Assert.Equal(0, result.SupersededAssignmentsClosed);

        var assignments = await db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == rev1.Id)
            .ToListAsync();

        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, a => a.AssignedUserId == analyst1.Id && a.Status == TrainingAssignmentStatus.Assigned);
        Assert.Contains(assignments, a => a.AssignedUserId == analyst2.Id && a.Status == TrainingAssignmentStatus.Assigned);
        Assert.DoesNotContain(assignments, a => a.AssignedUserId == inactiveAnalyst.Id);
    }

    [Fact]
    public async Task ProcessEffectiveRevisionCascadeAsync_HonorsCurriculumItemCustomGracePeriod()
    {
        var (db, admin, analyst1, _, _, analystRole, master, rev1, service) =
            await CreateTestEnvironmentAsync(defaultGracePeriod: 14);

        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = analystRole.Id,
            Name = "QC Analyst Curriculum",
            IsActive = true,
            CreatedByUserId = admin.Id
        };
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        // Custom grace period = 5 days (overriding master config of 14 days)
        var curriculumItem = new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = curriculum.Id,
            DocumentMasterId = master.Id,
            IsMandatory = true,
            CustomGracePeriodDays = 5
        };
        db.DocumentRoleCurriculumItems.Add(curriculumItem);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var result = await service.ProcessEffectiveRevisionCascadeAsync(rev1.Id, supersededRevisionId: null, utcNowOverride: now);

        var assignment = await db.DocumentTrainingAssignments
            .FirstAsync(a => a.DocumentRevisionId == rev1.Id && a.AssignedUserId == analyst1.Id);

        // Due date = rev1.EffectiveDate + 5 days
        Assert.Equal(rev1.EffectiveDate!.Value.AddDays(5), assignment.DueDateUtc);
    }

    [Fact]
    public async Task ProcessEffectiveRevisionCascadeAsync_Idempotent_PreventsDuplicateAssignments()
    {
        var (db, admin, analyst1, _, _, analystRole, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = analystRole.Id,
            Name = "QC Analyst Curriculum",
            IsActive = true,
            CreatedByUserId = admin.Id
        };
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        db.DocumentRoleCurriculumItems.Add(new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = curriculum.Id,
            DocumentMasterId = master.Id,
            IsMandatory = true
        });
        await db.SaveChangesAsync();

        // First run
        var firstResult = await service.ProcessEffectiveRevisionCascadeAsync(rev1.Id);
        Assert.Equal(2, firstResult.NewAssignmentsCreated);
        Assert.Equal(0, firstResult.ExistingAssignmentsSkipped);

        // Second run (idempotency check)
        var secondResult = await service.ProcessEffectiveRevisionCascadeAsync(rev1.Id);
        Assert.Equal(0, secondResult.NewAssignmentsCreated);
        Assert.Equal(2, secondResult.ExistingAssignmentsSkipped);

        var totalAssignments = await db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == rev1.Id)
            .CountAsync();
        Assert.Equal(2, totalAssignments);
    }

    [Fact]
    public async Task ProcessEffectiveRevisionCascadeAsync_WhenRequiresReadingFalse_ProducesZeroAssignments()
    {
        var (db, admin, analyst1, _, _, analystRole, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        // Explicitly set RequiresReading = false
        var config = new DocumentTrainingConfiguration
        {
            DocumentMasterId = master.Id,
            RequiresReading = false,
            RequiresRetrainingOnRevision = false,
            ModifiedByUserId = admin.Id,
            ModifiedAtUtc = DateTime.UtcNow
        };
        db.DocumentTrainingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = analystRole.Id,
            Name = "QC Analyst Curriculum",
            IsActive = true,
            CreatedByUserId = admin.Id
        };
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        db.DocumentRoleCurriculumItems.Add(new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = curriculum.Id,
            DocumentMasterId = master.Id,
            IsMandatory = true
        });
        await db.SaveChangesAsync();

        var result = await service.ProcessEffectiveRevisionCascadeAsync(rev1.Id);

        Assert.Equal(0, result.NewAssignmentsCreated);
        Assert.Empty(await db.DocumentTrainingAssignments.ToListAsync());
    }

    [Fact]
    public async Task ProcessEffectiveRevisionCascadeAsync_RetrainingCascade_LinksSourceAndClosesOpenOldAssignments()
    {
        var (db, admin, analyst1, analyst2, _, analystRole, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        // Prior assignments on Rev 01:
        // analyst1 completed Rev 01
        var completedOldAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst1.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Acknowledged,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-30),
            DueDateUtc = DateTime.UtcNow.AddDays(-16),
            AcknowledgedAtUtc = DateTime.UtcNow.AddDays(-20),
            StatementText = "I acknowledge that I have read and understood SOP-QC-0001 Rev 01.",
            CreatedByUserId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentTrainingAssignments.Add(completedOldAssignment);

        // analyst2 had an open, uncompleted assignment on Rev 01
        var openOldAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst2.Id,
            AssignmentType = AssignmentType.Reading,
            Status = TrainingAssignmentStatus.Assigned,
            AssignedDateUtc = DateTime.UtcNow.AddDays(-30),
            DueDateUtc = DateTime.UtcNow.AddDays(-16),
            CreatedByUserId = admin.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
        db.DocumentTrainingAssignments.Add(openOldAssignment);
        await db.SaveChangesAsync();

        // Create Rev 02
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentRevisions.Add(rev2);
        rev1.RevisionStatus = DocumentRevisionStatus.Superseded;
        master.CurrentEffectiveRevisionId = rev2.Id;
        await db.SaveChangesAsync();

        // Execute cascade for Rev 02 superseding Rev 01
        var result = await service.ProcessEffectiveRevisionCascadeAsync(
            effectiveRevisionId: rev2.Id,
            supersededRevisionId: rev1.Id);

        // Assert open old assignment was closed terminally
        await db.Entry(openOldAssignment).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.SupersededIncomplete, openOldAssignment.Status);
        Assert.NotNull(openOldAssignment.SupersededAtUtc);
        Assert.Equal("Superseded by Revision 02", openOldAssignment.ClosedReason);

        // Assert completed old assignment was untouched
        await db.Entry(completedOldAssignment).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, completedOldAssignment.Status);

        // Assert retraining assignment created for analyst1 with SourceAssignmentId
        var newAssignmentForAnalyst1 = await db.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev2.Id && a.AssignedUserId == analyst1.Id);

        Assert.NotNull(newAssignmentForAnalyst1);
        Assert.Equal(completedOldAssignment.Id, newAssignmentForAnalyst1.SourceAssignmentId);
        Assert.Equal(TrainingAssignmentStatus.Assigned, newAssignmentForAnalyst1.Status);
        Assert.Contains("Retraining cascade", newAssignmentForAnalyst1.AssignmentReason);
    }

    [Fact]
    public async Task AssignToUsersAsync_ManuallyAssignsTargetUsers_WithCustomGracePeriod()
    {
        var (db, admin, analyst1, analyst2, _, _, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        var request = new ManualTrainingAssignmentRequest(
            DocumentRevisionId: rev1.Id,
            UserIds: new List<int> { analyst1.Id, analyst2.Id },
            AssignmentType: AssignmentType.Reading,
            CustomGracePeriodDays: 7,
            AssignmentReason: "Mandatory retraining following audit finding"
        );

        var now = DateTime.UtcNow;
        var result = await service.AssignToUsersAsync(request, actingUserId: admin.Id, utcNowOverride: now);

        Assert.Equal(2, result.NewAssignmentsCreated);
        Assert.Equal(0, result.ExistingAssignmentsSkipped);

        var assignments = await db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == rev1.Id)
            .ToListAsync();

        Assert.Equal(2, assignments.Count);
        foreach (var a in assignments)
        {
            Assert.Equal(admin.Id, a.CreatedByUserId);
            Assert.Equal(now.AddDays(7), a.DueDateUtc);
            Assert.Equal("Mandatory retraining following audit finding", a.AssignmentReason);
        }
    }

    [Fact]
    public async Task AssignToUsersAsync_ThrowsWhenRevisionNotEffective()
    {
        var (db, admin, analyst1, _, _, _, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        rev1.RevisionStatus = DocumentRevisionStatus.Draft;
        await db.SaveChangesAsync();

        var request = new ManualTrainingAssignmentRequest(
            DocumentRevisionId: rev1.Id,
            UserIds: new List<int> { analyst1.Id },
            AssignmentType: AssignmentType.Reading
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignToUsersAsync(request, actingUserId: admin.Id));
    }

    [Fact]
    public async Task AssignToGroupAsync_AssignsAllActiveUsersInRole()
    {
        var (db, admin, analyst1, analyst2, inactiveAnalyst, analystRole, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        var request = new BulkGroupAssignmentRequest(
            DocumentRevisionId: rev1.Id,
            RoleId: analystRole.Id,
            DepartmentId: null,
            AssignmentType: AssignmentType.Reading,
            CustomGracePeriodDays: 10,
            AssignmentReason: "Annual departmental review"
        );

        var result = await service.AssignToGroupAsync(request, actingUserId: admin.Id);

        Assert.Equal(2, result.NewAssignmentsCreated); // Only active analysts
        var created = await db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == rev1.Id)
            .ToListAsync();

        Assert.DoesNotContain(created, a => a.AssignedUserId == inactiveAnalyst.Id);
    }

    [Fact]
    public async Task HandleDocumentObsolescenceAsync_CancelsOpenAssignmentsAndIgnoresCompleted()
    {
        var (db, admin, analyst1, analyst2, _, _, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        // 1 open assignment
        var openAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst1.Id,
            Status = TrainingAssignmentStatus.Assigned,
            CreatedByUserId = admin.Id
        };
        // 1 completed assignment
        var completedAssignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst2.Id,
            Status = TrainingAssignmentStatus.Acknowledged,
            CreatedByUserId = admin.Id
        };
        db.DocumentTrainingAssignments.AddRange(openAssignment, completedAssignment);
        await db.SaveChangesAsync();

        int cancelledCount = await service.HandleDocumentObsolescenceAsync(
            documentMasterId: master.Id,
            reason: "Method retired by ISO update",
            actingUserId: admin.Id);

        Assert.Equal(1, cancelledCount);

        await db.Entry(openAssignment).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.Cancelled, openAssignment.Status);
        Assert.Equal("Method retired by ISO update", openAssignment.ClosedReason);
        Assert.Equal(admin.Id, openAssignment.ModifiedByUserId);

        await db.Entry(completedAssignment).ReloadAsync();
        Assert.Equal(TrainingAssignmentStatus.Acknowledged, completedAssignment.Status);
    }

    [Fact]
    public async Task IsTrainedOnSupersededOnlyAsync_ReturnsTrueOnlyWhenQualifiedOnSupersededRevision()
    {
        var (db, admin, analyst1, analyst2, _, _, master, rev1, service) =
            await CreateTestEnvironmentAsync();

        // Rev 01 superseded, Rev 02 effective
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

        // analyst1 completed Rev 01, has no completion on Rev 02
        db.DocumentTrainingAssignments.Add(new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev1.Id,
            AssignedUserId = analyst1.Id,
            Status = TrainingAssignmentStatus.Acknowledged,
            CreatedByUserId = admin.Id
        });

        // analyst2 has NO assignments
        await db.SaveChangesAsync();

        bool analyst1Result = await service.IsTrainedOnSupersededOnlyAsync(analyst1.Id, master.Id);
        bool analyst2Result = await service.IsTrainedOnSupersededOnlyAsync(analyst2.Id, master.Id);

        Assert.True(analyst1Result);
        Assert.False(analyst2Result);

        // When analyst1 completes Rev 02, IsTrainedOnSupersededOnlyAsync becomes false
        db.DocumentTrainingAssignments.Add(new DocumentTrainingAssignment
        {
            DocumentMasterId = master.Id,
            DocumentRevisionId = rev2.Id,
            AssignedUserId = analyst1.Id,
            Status = TrainingAssignmentStatus.Acknowledged,
            CreatedByUserId = admin.Id
        });
        await db.SaveChangesAsync();

        bool analyst1UpdatedResult = await service.IsTrainedOnSupersededOnlyAsync(analyst1.Id, master.Id);
        Assert.False(analyst1UpdatedResult);
    }

    [Fact]
    public async Task DocumentEffectiveDateService_IntegrationHook_TriggersCascadeWhenRevisionMatures()
    {
        var (db, admin, analyst1, _, _, analystRole, master, rev1, trainingService) =
            await CreateTestEnvironmentAsync();

        // Setup role curriculum
        var curriculum = new DocumentRoleCurriculum
        {
            RoleId = analystRole.Id,
            Name = "QC Curriculum",
            IsActive = true,
            CreatedByUserId = admin.Id
        };
        db.DocumentRoleCurricula.Add(curriculum);
        await db.SaveChangesAsync();

        db.DocumentRoleCurriculumItems.Add(new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = curriculum.Id,
            DocumentMasterId = master.Id,
            IsMandatory = true
        });
        await db.SaveChangesAsync();

        // Create FutureEffective Revision 02
        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = DateTime.UtcNow.AddMinutes(-5), // Matured
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();

        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var effectiveDateService = new DocumentEffectiveDateService(
            db,
            audit,
            NullLogger<DocumentEffectiveDateService>.Instance,
            trainingService);

        var result = await effectiveDateService.ProcessMaturedRevisionsAsync(DateTime.UtcNow);

        Assert.Equal(1, result.SuccessfullyActivatedCount);
        Assert.Equal(0, result.FailedRevisionsCount);

        await db.Entry(rev2).ReloadAsync();
        Assert.Equal(DocumentRevisionStatus.Effective, rev2.RevisionStatus);

        // Verify that assignments were generated automatically for Rev 02
        var assignments = await db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == rev2.Id)
            .ToListAsync();

        Assert.NotEmpty(assignments);
        Assert.Contains(assignments, a => a.AssignedUserId == analyst1.Id);
    }

    [Fact]
    public async Task DocumentEffectiveDateService_FailureIsolation_LogsAuditAndDoesNotRollbackCommittedRevision()
    {
        var (db, admin, analyst1, _, _, analystRole, master, rev1, _) =
            await CreateTestEnvironmentAsync();

        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = DateTime.UtcNow.AddMinutes(-5),
            CreatedByUserId = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();

        var failingTrainingService = new FailingMockTrainingAssignmentService();
        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var effectiveDateService = new DocumentEffectiveDateService(
            db,
            audit,
            NullLogger<DocumentEffectiveDateService>.Instance,
            failingTrainingService);

        var result = await effectiveDateService.ProcessMaturedRevisionsAsync(DateTime.UtcNow);

        // Revision activation succeeded even though training cascade failed
        Assert.Equal(1, result.SuccessfullyActivatedCount);
        Assert.Equal(0, result.FailedRevisionsCount);

        await db.Entry(rev2).ReloadAsync();
        Assert.Equal(DocumentRevisionStatus.Effective, rev2.RevisionStatus);

        // Verify SystemProcessError audit log was recorded
        var auditLogs = await db.AuditLogs
            .Where(a => a.ActionCode == "SystemProcessError")
            .ToListAsync();
        Assert.NotEmpty(auditLogs);
    }

    private class FailingMockTrainingAssignmentService : ITrainingAssignmentService
    {
        public Task<TrainingAssignmentGenerationResultDto> ProcessEffectiveRevisionCascadeAsync(
            int effectiveRevisionId, int? supersededRevisionId = null, DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated unexpected error in training assignment cascade.");
        }

        public Task<TrainingAssignmentGenerationResultDto> AssignToUsersAsync(
            ManualTrainingAssignmentRequest request, int actingUserId, DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TrainingAssignmentGenerationResultDto> AssignToGroupAsync(
            BulkGroupAssignmentRequest request, int actingUserId, DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> HandleDocumentObsolescenceAsync(
            int documentMasterId, int? documentRevisionId = null, string? reason = null, int? actingUserId = null, DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(int GracePeriodDays, DateTime DueDateUtc)> CalculateDueDateAsync(
            int documentMasterId, int? customGracePeriodDays = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> IsTrainedOnSupersededOnlyAsync(
            int userId, int documentMasterId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DocumentTrainingAssignmentDto?> GetAssignmentByIdAsync(
            int assignmentId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<DocumentTrainingAssignmentDto>> GetAssignmentsAsync(
            DocumentTrainingAssignmentFilter filter, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<DocumentTrainingAssignmentDto>> GetUserAssignmentsAsync(
            int userId, TrainingAssignmentStatus? status = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
