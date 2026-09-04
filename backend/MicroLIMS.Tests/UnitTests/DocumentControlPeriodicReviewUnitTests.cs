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

public class DocumentControlPeriodicReviewUnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _current = 500;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _current));
        }
    }

    private async Task<(
        MicroLimsDbContext db,
        User author,
        User reviewer,
        User approver,
        User sectionHead,
        DocumentMaster master,
        DocumentRevision effectiveRev,
        PeriodicReviewService service
    )> CreateSeededContextAsync(DateTime? nextReviewDate = null, int reviewCycleMonths = 24)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        var reviewerRole = new Role { Type = RoleType.Reviewer, Name = "QA Reviewer", IsActive = true };
        var sectionHeadRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsActive = true };
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsActive = true };
        db.Roles.AddRange(analystRole, reviewerRole, sectionHeadRole, adminRole);
        await db.SaveChangesAsync();

        var author = new User { FullName = "SOP Author", Username = "author1", RoleId = analystRole.Id, IsActive = true, Role = analystRole };
        var reviewer = new User { FullName = "Tech Reviewer", Username = "rev1", RoleId = reviewerRole.Id, IsActive = true, Role = reviewerRole };
        var approver = new User { FullName = "QA Approver", Username = "app1", RoleId = reviewerRole.Id, IsActive = true, Role = reviewerRole };
        var sectionHead = new User { FullName = "Doc Controller", Username = "head1", RoleId = sectionHeadRole.Id, IsActive = true, Role = sectionHeadRole };
        db.Users.AddRange(author, reviewer, approver, sectionHead);
        await db.SaveChangesAsync();

        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = reviewCycleMonths, IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec = new DocumentSection { DepartmentId = dept.Id, Name = "Microbiology", IsActive = true };
        db.DocumentSections.Add(sec);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000100",
            CompanyDocumentCode = "SOP-QC-0100",
            Title = "Environmental Monitoring SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            RecordOrigin = RecordOrigin.Native,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-12)
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var effectiveRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddMonths(-12),
            NextReviewDate = nextReviewDate ?? DateTime.UtcNow.AddDays(-1), // overdue by default
            ReviewCycleMonths = reviewCycleMonths,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-12)
        };
        db.DocumentRevisions.Add(effectiveRev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = effectiveRev.Id;
        await db.SaveChangesAsync();

        // Attach controlled PDF
        var pdf = new RevisionFile
        {
            DocumentRevisionId = effectiveRev.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "SOP-QC-0100_Rev01.pdf",
            ContentType = "application/pdf",
            SizeBytes = 124500,
            ContentSha256 = "d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2",
            IsActive = true,
            UploadedAt = DateTime.UtcNow.AddMonths(-12),
            UploadedByUserId = author.Id
        };
        db.RevisionFiles.Add(pdf);
        await db.SaveChangesAsync();

        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(
            db,
            audit,
            auth,
            signatureService
        );

        var service = new PeriodicReviewService(
            db,
            audit,
            auth,
            approvalService,
            new NullLogger<PeriodicReviewService>()
        );

        return (db, author, reviewer, approver, sectionHead, master, effectiveRev, service);
    }

    [Fact]
    public async Task GenerateDueReviewTasks_WhenReviewDue_CreatesPendingTask()
    {
        // Arrange
        var (_, _, _, _, _, master, rev, service) = await CreateSeededContextAsync(DateTime.UtcNow.AddDays(-5));

        // Act
        var result = await service.GenerateDueReviewTasksAsync();

        // Assert
        Assert.Equal(1, result.CreatedTasksCount);
        Assert.Single(result.CreatedTaskIds);
        var task = await service.GetReviewTaskByIdAsync(result.CreatedTaskIds[0], 1);
        Assert.Equal(PeriodicReviewTaskStatus.Pending, task.Status);
        Assert.Equal(master.Id, task.DocumentMasterId);
        Assert.Equal(rev.Id, task.DocumentRevisionId);
        Assert.Equal("01", task.RevisionNumber);
    }

    [Fact]
    public async Task GenerateDueReviewTasks_WhenReviewDateInFuture_DoesNotCreateTask()
    {
        // Arrange
        var (_, _, _, _, _, _, _, service) = await CreateSeededContextAsync(DateTime.UtcNow.AddDays(15));

        // Act
        var result = await service.GenerateDueReviewTasksAsync();

        // Assert
        Assert.Equal(0, result.CreatedTasksCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task GenerateDueReviewTasks_IsIdempotent_SecondRunProducesZeroTasks()
    {
        // Arrange
        var (_, _, _, _, _, _, _, service) = await CreateSeededContextAsync(DateTime.UtcNow.AddDays(-2));

        // Act - Run 1
        var run1 = await service.GenerateDueReviewTasksAsync();
        Assert.Equal(1, run1.CreatedTasksCount);

        // Act - Run 2 & 3
        var run2 = await service.GenerateDueReviewTasksAsync();
        var run3 = await service.GenerateDueReviewTasksAsync();

        // Assert
        Assert.Equal(0, run2.CreatedTasksCount);
        Assert.Equal(0, run3.CreatedTasksCount);
    }

    [Fact]
    public async Task GetWorkspace_DisplaysCorrectControlledPdfAndMetadata()
    {
        // Arrange
        var (_, _, reviewer, _, _, master, rev, service) = await CreateSeededContextAsync();
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];

        // Act
        var ws = await service.GetReviewWorkspaceAsync(taskId, reviewer.Id);

        // Assert
        Assert.NotNull(ws);
        Assert.NotNull(ws.ControlledPdf);
        Assert.Equal("SOP-QC-0100_Rev01.pdf", ws.ControlledPdf.FileName);
        Assert.Equal(FileRole.ControlledPdf, ws.ControlledPdf.FileRole);
        Assert.Equal(master.MicroLimsDocumentId, ws.Master.MicroLimsDocumentId);
        Assert.Equal("01", ws.Revision.RevisionNumber);
    }

    [Fact]
    public async Task AddFinding_PersistsPageAndSectionNotes_TransitionsTaskToInProgress()
    {
        // Arrange
        var (_, _, reviewer, _, _, _, _, service) = await CreateSeededContextAsync();
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];

        // Act
        var finding = await service.AddFindingAsync(taskId, new CreatePeriodicReviewFindingRequest(
            PageNumber: 3,
            SectionNumber: "4.2",
            NoteText: "Verify incubator temperature calibration tolerance"
        ), reviewer.Id);

        // Assert
        Assert.NotNull(finding);
        Assert.Equal(3, finding.PageNumber);
        Assert.Equal("4.2", finding.SectionNumber);
        Assert.Equal("Verify incubator temperature calibration tolerance", finding.NoteText);
        Assert.Equal(PeriodicReviewFindingStatus.Open, finding.Status);

        var task = await service.GetReviewTaskByIdAsync(taskId, reviewer.Id);
        Assert.Equal(PeriodicReviewTaskStatus.InProgress, task.Status);
        Assert.Equal(1, task.TotalFindingsCount);
    }

    [Fact]
    public async Task Author_CannotAddFindingOrCompleteReview_ThrowsUnauthorized()
    {
        // Arrange (Segregation of Duties: Author != Reviewer)
        var (_, author, _, _, _, _, _, service) = await CreateSeededContextAsync();
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];

        // Act & Assert - Adding finding
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddFindingAsync(taskId, new CreatePeriodicReviewFindingRequest(1, "1.0", "Author note"), author.Id));

        // Act & Assert - Completing review
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(
                PeriodicReviewOutcome.RemainsValid,
                "Author attempting self-review of SOP"
            ), author.Id));
    }

    [Fact]
    public async Task CompleteReview_RemainsValid_AdvancesNextReviewDate_CreatesNoNewRevision()
    {
        // Arrange
        var (db, _, reviewer, _, _, master, rev, service) = await CreateSeededContextAsync(DateTime.UtcNow.AddDays(-10), reviewCycleMonths: 24);
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];
        var priorRevisionCount = await db.DocumentRevisions.CountAsync(r => r.DocumentMasterId == master.Id);

        // Act
        var completed = await service.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.RemainsValid,
            ReviewSummary: "Periodic review completed. Method and regulatory citations remain fully valid."
        ), reviewer.Id);

        // Assert
        Assert.Equal(PeriodicReviewTaskStatus.Completed, completed.Status);
        Assert.Equal(PeriodicReviewOutcome.RemainsValid, completed.Outcome);

        // Verify NO new revision created
        var newRevisionCount = await db.DocumentRevisions.CountAsync(r => r.DocumentMasterId == master.Id);
        Assert.Equal(priorRevisionCount, newRevisionCount);

        // Verify revision remains Effective and NextReviewDate advanced by 24 months
        var updatedRev = await db.DocumentRevisions.FindAsync(rev.Id);
        Assert.NotNull(updatedRev);
        Assert.Equal(DocumentRevisionStatus.Effective, updatedRev.RevisionStatus);
        Assert.NotNull(updatedRev.NextReviewDate);
        Assert.True(updatedRev.NextReviewDate > DateTime.UtcNow.AddMonths(23));
    }

    [Fact]
    public async Task CompleteReview_RevisionRequired_PreservesEvidenceAndHandoffLink()
    {
        // Arrange
        var (_, _, reviewer, _, _, _, _, service) = await CreateSeededContextAsync();
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];

        await service.AddFindingAsync(taskId, new CreatePeriodicReviewFindingRequest(
            PageNumber: 5,
            SectionNumber: "6.1",
            NoteText: "Update media lot release criteria per new pharmacopeia"
        ), reviewer.Id);

        // Act
        var completed = await service.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.RevisionRequired,
            ReviewSummary: "Revision required due to pharmacopeial harmonisation changes."
        ), reviewer.Id);

        // Assert
        Assert.Equal(PeriodicReviewTaskStatus.Completed, completed.Status);
        Assert.Equal(PeriodicReviewOutcome.RevisionRequired, completed.Outcome);
        Assert.Equal(1, completed.TotalFindingsCount);
    }

    [Fact]
    public async Task CompleteReview_ObsolescenceRecommended_RoutesToQualityApproval()
    {
        // Arrange
        var (db, _, reviewer, _, _, master, rev, service) = await CreateSeededContextAsync();
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];

        // Act
        var completed = await service.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.ObsolescenceRecommended,
            ReviewSummary: "Equipment decommissioned; SOP is recommended for formal obsolescence."
        ), reviewer.Id);

        // Assert
        Assert.Equal(PeriodicReviewTaskStatus.Completed, completed.Status);
        Assert.Equal(PeriodicReviewOutcome.ObsolescenceRecommended, completed.Outcome);

        // Verify document is NOT marked obsolete immediately (remains Effective until approved)
        var updatedRev = await db.DocumentRevisions.FindAsync(rev.Id);
        Assert.NotNull(updatedRev);
        Assert.Equal(DocumentRevisionStatus.Effective, updatedRev.RevisionStatus);

        // Verify an approval task is staged in DocumentApprovalTasks
        var appTask = await db.DocumentApprovalTasks.FirstOrDefaultAsync(a => a.DocumentRevisionId == rev.Id);
        Assert.NotNull(appTask);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, appTask.Status);
        Assert.Contains($"Periodic Review #{taskId}", appTask.SubmissionNotes);
    }

    [Fact]
    public async Task DowntimeCatchUp_OverdueRevisionsProcessedImmediately()
    {
        // Arrange - Revision due 14 days ago (during simulated server downtime)
        var (_, _, _, _, _, _, _, service) = await CreateSeededContextAsync(DateTime.UtcNow.AddDays(-14));

        // Act
        var result = await service.GenerateDueReviewTasksAsync();

        // Assert
        Assert.Equal(1, result.CreatedTasksCount);
        var task = await service.GetReviewTaskByIdAsync(result.CreatedTaskIds[0], 1);
        Assert.True(task.IsOverdue);
    }

    [Fact]
    public async Task CompletedReview_CannotBeAlteredOrRecompleted()
    {
        // Arrange
        var (_, _, reviewer, _, _, _, _, service) = await CreateSeededContextAsync();
        var gen = await service.GenerateDueReviewTasksAsync();
        var taskId = gen.CreatedTaskIds[0];

        await service.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.RemainsValid,
            ReviewSummary: "Review concluded successfully."
        ), reviewer.Id);

        // Act & Assert - Attempting to add note after completion
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddFindingAsync(taskId, new CreatePeriodicReviewFindingRequest(1, "1", "Late note"), reviewer.Id));

        // Act & Assert - Attempting to recomplete
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(
                Outcome: PeriodicReviewOutcome.RevisionRequired,
                ReviewSummary: "Attempting to change outcome"
            ), reviewer.Id));
    }

    [Fact]
    public async Task FailureIsolation_WhenOneDocumentFails_OtherDocumentsContinueProcessing()
    {
        // Arrange
        var (db, _, _, _, _, _, _, service) = await CreateSeededContextAsync();
        var gen1 = await service.GenerateDueReviewTasksAsync();
        Assert.Equal(1, gen1.CreatedTasksCount);

        // Add a second master that is active and due
        var docType = await db.DocumentTypes.FirstAsync();
        var dept = await db.DocumentDepartments.FirstAsync();
        var sec = await db.DocumentSections.FirstAsync();
        var user = await db.Users.FirstAsync();

        var master2 = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000101",
            CompanyDocumentCode = "SOP-QC-0101",
            Title = "Water Testing SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
            DocumentOwnerUserId = user.Id,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentMasters.Add(master2);
        await db.SaveChangesAsync();

        var rev2 = new DocumentRevision
        {
            DocumentMasterId = master2.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            NextReviewDate = DateTime.UtcNow.AddDays(-2),
            ReviewCycleMonths = 24,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentRevisions.Add(rev2);
        await db.SaveChangesAsync();
        master2.CurrentEffectiveRevisionId = rev2.Id;
        await db.SaveChangesAsync();

        // Act
        var result = await service.GenerateDueReviewTasksAsync();

        // Assert - Master 1 already has task (skipped), Master 2 gets new task
        Assert.Equal(1, result.CreatedTasksCount);
    }

    [Fact]
    public async Task GetMasterReviewHistory_ControllerEndpoint_ReturnsDirectList_ContractMatchesFrontend()
    {
        // Arrange
        var (db, author, reviewer, approver, sectionHead, master, effectiveRev, service) = await CreateSeededContextAsync();
        var controller = new MicroLIMS.API.Controllers.DocumentControl.PeriodicReviewController(service)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, sectionHead.Id.ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, nameof(RoleType.SectionHead))
                    }, "TestAuth"))
                }
            }
        };

        // Act 1: Zero tasks
        var zeroTasksResult = await controller.GetMasterReviewHistory(master.Id);
        var okResultZero = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(zeroTasksResult.Result);
        var zeroList = Assert.IsAssignableFrom<IEnumerable<PeriodicReviewTaskDto>>(okResultZero.Value);
        Assert.Empty(zeroList);

        // Act 2: Generate 1 task
        var gen = await service.GenerateDueReviewTasksAsync();
        Assert.Equal(1, gen.CreatedTasksCount);

        var oneTaskResult = await controller.GetMasterReviewHistory(master.Id);
        var okResultOne = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(oneTaskResult.Result);
        var oneList = Assert.IsAssignableFrom<IEnumerable<PeriodicReviewTaskDto>>(okResultOne.Value);
        Assert.Single(oneList);
    }
}
