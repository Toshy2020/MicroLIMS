using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlApprovalUnitTests
{
    private async Task<(
        MicroLimsDbContext db,
        User author,
        User controller,
        User reviewer,
        User approver,
        User admin,
        DocumentMaster master,
        DocumentRevision revision,
        DocumentApprovalService approvalService,
        DocumentRevisionService revisionService
    )> CreateSeededContextWithApprovalReadyRevisionAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        // Roles
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsActive = true };
        var controllerRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsActive = true };
        var reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Quality Reviewer", IsActive = true };
        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        db.Roles.AddRange(adminRole, controllerRole, reviewerRole, analystRole);
        await db.SaveChangesAsync();

        var defaultPassword = "Approver-Password-1!";
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

        // Users
        var admin = new User { FullName = "System Admin", Username = "admin1", RoleId = adminRole.Id, IsActive = true, PasswordHash = defaultPasswordHash, Role = adminRole };
        var controller = new User { FullName = "Document Controller", Username = "controller1", RoleId = controllerRole.Id, IsActive = true, PasswordHash = defaultPasswordHash, Role = controllerRole };
        var author = new User { FullName = "SOP Author", Username = "author1", RoleId = analystRole.Id, IsActive = true, PasswordHash = defaultPasswordHash, Role = analystRole };
        var reviewer = new User { FullName = "Technical Reviewer", Username = "reviewer1", RoleId = reviewerRole.Id, IsActive = true, PasswordHash = defaultPasswordHash, Role = reviewerRole };
        var approver = new User { FullName = "QA Approver", Username = "approver1", RoleId = reviewerRole.Id, IsActive = true, PasswordHash = defaultPasswordHash, Role = reviewerRole };
        db.Users.AddRange(admin, controller, author, reviewer, approver);
        await db.SaveChangesAsync();

        // Document Type, Department, Section
        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var section = new DocumentSection { Name = "Microbiology", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        // Document Master
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000042",
            CompanyDocumentCode = "SOP-QC-042",
            Title = "Environmental Monitoring SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = section.Id,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        // Document Revision in AwaitingApproval status
        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.AwaitingApproval,
            RevisionType = RevisionType.Major,
            ReasonForRevision = "Initial procedure baseline creation.",
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();

        // Attach Controlled PDF
        var pdfFile = new RevisionFile
        {
            DocumentRevisionId = revision.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "sop_qc_042_v01.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            ContentSha256 = "abc123sha256",
            IsActive = true,
            UploadedByUserId = author.Id,
            UploadedAt = DateTime.UtcNow.AddDays(-9)
        };
        db.RevisionFiles.Add(pdfFile);

        // Add Completed Technical Review Task with no open mandatory findings
        var reviewTask = new DocumentReviewTask
        {
            DocumentRevisionId = revision.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            AssignedAt = DateTime.UtcNow.AddDays(-8),
            Status = ReviewTaskStatus.Completed,
            Decision = ReviewDecision.CompleteReview,
            DecisionAt = DateTime.UtcNow.AddDays(-5),
            DecisionByUserId = reviewer.Id,
            ReviewNotes = "Technical review completed successfully."
        };
        db.DocumentReviewTasks.Add(reviewTask);
        await db.SaveChangesAsync();

        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        return (db, author, controller, reviewer, approver, admin, master, revision, approvalService, revisionService);
    }

    [Fact]
    public async Task CreateApprovalTask_AwaitingApprovalRevision_SucceedsAndAudits()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(
            approver.Id,
            DateTime.UtcNow.AddDays(7),
            "Please approve SOP"
        ), author.Id);

        Assert.NotNull(task);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, task.Status);
        Assert.Equal(approver.Id, task.AssignedApproverUserId);
        Assert.Equal(author.Id, task.AssignedByUserId);

        var audit = await db.AuditLogs
            .Where(a => a.ActionCode == "ApprovalTaskCreated" && a.DocumentRevisionId == revision.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CreateApprovalTask_DraftRevision_ThrowsInvalidOperation()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        revision.RevisionStatus = DocumentRevisionStatus.Draft;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id));

        Assert.Contains("Status must be 'AwaitingApproval'", ex.Message);
    }

    [Fact]
    public async Task CreateApprovalTask_WhenTechnicalReviewNotCompleted_ThrowsInvalidOperation()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        // Remove review tasks
        var reviews = await db.DocumentReviewTasks.Where(r => r.DocumentRevisionId == revision.Id).ToListAsync();
        db.DocumentReviewTasks.RemoveRange(reviews);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id));

        Assert.Contains("Revision has not completed technical review", ex.Message);
    }

    [Fact]
    public async Task CreateApprovalTask_DuplicatePendingTask_ThrowsInvalidOperation()
    {
        var (_, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id));

        Assert.Contains("already pending", ex.Message);
    }

    [Fact]
    public async Task CreateApprovalTask_IneligibleApproverRole_ThrowsInvalidOperation()
    {
        var (_, author, controller, _, _, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        // Author has Analyst role (not Reviewer, SectionHead, or Admin)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(author.Id), controller.Id));

        Assert.Contains("is not eligible to be assigned as an approver", ex.Message);
    }

    [Fact]
    public async Task CreateApprovalTask_SegregationOfDuties_AuthorCannotBeApprover_ThrowsUnauthorized()
    {
        var (db, author, controller, _, _, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        // Change author role to Reviewer to isolate SoD check
        var reviewerRole = await db.Roles.FirstAsync(r => r.Type == RoleType.Reviewer);
        author.RoleId = reviewerRole.Id;
        author.Role = reviewerRole;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(author.Id), controller.Id));

        Assert.Contains("Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver", ex.Message);
    }

    [Fact]
    public async Task CreateApprovalTask_SegregationOfDuties_ReviewerCannotBeApprover_ThrowsUnauthorized()
    {
        var (_, author, _, reviewer, _, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(reviewer.Id), author.Id));

        Assert.Contains("Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver", ex.Message);
    }

    [Fact]
    public async Task CreateApprovalTask_SegregationOfDuties_AdminCannotBypass_ThrowsUnauthorized()
    {
        var (db, _, controller, _, _, admin, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        // If admin was the author, admin cannot be assigned as approver
        revision.CreatedByUserId = admin.Id;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(admin.Id), controller.Id));

        Assert.Contains("Segregation of Duties Violation", ex.Message);
    }

    [Fact]
    public async Task ApprovalDossier_AggregatesFullEvidenceAndEmitsAudit()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var dossier = await approvalService.GetApprovalDossierAsync(task.Id, approver.Id);

        Assert.NotNull(dossier);
        Assert.NotNull(dossier.Task);
        Assert.NotNull(dossier.Revision);
        Assert.NotNull(dossier.Master);
        Assert.NotNull(dossier.ControlledPdf);
        Assert.NotNull(dossier.ReviewHistory);
        Assert.Single(dossier.ReviewHistory);
        Assert.NotNull(dossier.Readiness);
        Assert.True(dossier.Readiness.IsReady);

        // Verify dossier viewed audit
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "ApprovalDossierViewed" && a.DocumentRevisionId == revision.Id);

        Assert.NotNull(audit);
    }

    [Fact]
    public async Task ValidateApprovalReadiness_ReportsAllGaps()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Deactivate PDF to test gap reporting
        var pdf = await db.RevisionFiles.FirstAsync(f => f.DocumentRevisionId == revision.Id);
        pdf.IsActive = false;
        await db.SaveChangesAsync();

        var readiness = await approvalService.ValidateApprovalReadinessAsync(task.Id, approver.Id);

        Assert.False(readiness.IsReady);
        Assert.Contains(readiness.ValidationErrors, e => e.Contains("Controlled PDF"));
    }

    [Fact]
    public async Task ExecuteDecision_ReturnForCorrection_RevertsToDraft_RequiresJustification()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Short justification fails
        await Assert.ThrowsAsync<ArgumentException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.ReturnForCorrection, "Short"), approver.Id));

        // Valid justification succeeds
        var result = await approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.ReturnForCorrection,
            "Please clarify section 4.2 temperature tolerance limits."
        ), approver.Id);

        Assert.Equal(DocumentApprovalTaskStatus.ReturnedForCorrection, result.Status);

        var dbRev = await db.DocumentRevisions.FindAsync(revision.Id);
        Assert.Equal(DocumentRevisionStatus.Draft, dbRev!.RevisionStatus);

        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "RevisionReturnedByApprover" && a.DocumentRevisionId == revision.Id);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task ExecuteDecision_Decline_CancelsRevision_RequiresJustification()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Short justification fails
        await Assert.ThrowsAsync<ArgumentException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Decline, "No"), approver.Id));

        // Valid decline succeeds
        var result = await approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Decline,
            "Method superseded by corporate quality standard QS-900."
        ), approver.Id);

        Assert.Equal(DocumentApprovalTaskStatus.Declined, result.Status);

        var dbRev = await db.DocumentRevisions.FindAsync(revision.Id);
        Assert.Equal(DocumentRevisionStatus.Cancelled, dbRev!.RevisionStatus);
        Assert.Equal(approver.Id, dbRev.CancelledByUserId);

        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "RevisionDeclinedByApprover" && a.DocumentRevisionId == revision.Id);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task ExecuteDecision_Approve_ImmediateEffective_SupersedesPriorEffective()
    {
        var (db, author, _, _, approver, _, master, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var result = await approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Fully approved for GMP operations.",
            Password: "Approver-Password-1!"
        ), approver.Id);

        Assert.Equal(DocumentApprovalTaskStatus.Approved, result.Status);

        var dbRev = await db.DocumentRevisions.FindAsync(revision.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, dbRev!.RevisionStatus);
        Assert.NotNull(dbRev.EffectiveDate);
        Assert.NotNull(dbRev.NextReviewDate);

        var dbMaster = await db.DocumentMasters.FindAsync(master.Id);
        Assert.Equal(revision.Id, dbMaster!.CurrentEffectiveRevisionId);

        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentRevisionApproved" && a.DocumentRevisionId == revision.Id);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task ExecuteDecision_Approve_FutureEffective_PreservesEffectivePrior()
    {
        var (db, author, _, reviewer, approver, _, master, rev1, approvalService, revisionService) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        // 1. Approve Rev 1 as immediate Effective
        var task1 = await approvalService.CreateApprovalTaskAsync(rev1.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);
        await approvalService.ExecuteApprovalDecisionAsync(task1.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Rev 1 Effective", Password: "Approver-Password-1!"), approver.Id);

        // 2. Create Rev 2 from Effective Rev 1
        var rev2Dto = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType.Minor,
            "Mandatory annual update with new incubation parameters.",
            "Summary of updates"
        ), author.Id);

        // Attach PDF to Rev 2
        var pdf2 = new RevisionFile
        {
            DocumentRevisionId = rev2Dto.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "sop_qc_042_v01.1.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            ContentSha256 = "def456sha256",
            IsActive = true,
            UploadedByUserId = author.Id,
            UploadedAt = DateTime.UtcNow
        };
        db.RevisionFiles.Add(pdf2);

        // Complete Impact Assessment for Rev 2
        await revisionService.SaveImpactAssessmentAsync(rev2Dto.Id, new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: false,
            ProcedureOrMethodDetails: null,
            TrainingImpact: true,
            TrainingDetails: "Staff training required for new parameters",
            FormsOrTemplatesImpact: false,
            FormsOrTemplatesDetails: null,
            SpecificationsImpact: false,
            SpecificationsDetails: null,
            EquipmentImpact: false,
            EquipmentDetails: null,
            MaterialsOrMediaImpact: false,
            MaterialsOrMediaDetails: null,
            ValidationImpact: false,
            ValidationDetails: null,
            RegulatoryCommitmentImpact: false,
            RegulatoryCommitmentDetails: null,
            RelatedDocumentsImpact: false,
            RelatedDocumentsDetails: null
        ), author.Id);

        // Add completed review task for Rev 2 and advance status
        var reviewTask2 = new DocumentReviewTask
        {
            DocumentRevisionId = rev2Dto.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            AssignedAt = DateTime.UtcNow,
            Status = ReviewTaskStatus.Completed,
            Decision = ReviewDecision.CompleteReview,
            DecisionAt = DateTime.UtcNow,
            DecisionByUserId = reviewer.Id,
            ReviewNotes = "Rev 2 review completed."
        };
        db.DocumentReviewTasks.Add(reviewTask2);

        var dbRev2 = await db.DocumentRevisions.FindAsync(rev2Dto.Id);
        dbRev2!.RevisionStatus = DocumentRevisionStatus.AwaitingApproval;
        await db.SaveChangesAsync();

        // 3. Create Approval Task for Rev 2
        var task2 = await approvalService.CreateApprovalTaskAsync(rev2Dto.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // 4. Approve Rev 2 with Future Effective Date (+30 days)
        var futureDate = DateTime.UtcNow.AddDays(30);
        var result = await approvalService.ExecuteApprovalDecisionAsync(task2.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Approved for future activation",
            EffectiveDate: futureDate,
            Password: "Approver-Password-1!"
        ), approver.Id);

        Assert.Equal(DocumentApprovalTaskStatus.Approved, result.Status);

        // Rev 2 must be FutureEffective
        Assert.Equal(DocumentRevisionStatus.FutureEffective, dbRev2.RevisionStatus);

        // Prior Rev 1 MUST REMAIN Effective! (DC-URS-065, DC-URS-177)
        var dbRev1 = await db.DocumentRevisions.FindAsync(rev1.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, dbRev1!.RevisionStatus);

        var dbMaster = await db.DocumentMasters.FindAsync(master.Id);
        Assert.Equal(rev1.Id, dbMaster!.CurrentEffectiveRevisionId);
    }

    [Fact]
    public async Task ExecuteDecision_SegregationOfDuties_AuthorCannotDecide_ThrowsUnauthorized()
    {
        var (_, author, _, _, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Author self-approving"), author.Id));

        Assert.Contains("Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision", ex.Message);
    }

    [Fact]
    public async Task ExecuteDecision_SegregationOfDuties_ReviewerCannotDecide_ThrowsUnauthorized()
    {
        var (_, author, _, reviewer, approver, _, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Reviewer approving"), reviewer.Id));

        Assert.Contains("Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision", ex.Message);
    }

    [Fact]
    public async Task ExecuteDecision_SegregationOfDuties_AdminCannotBypass_ThrowsUnauthorized()
    {
        var (db, author, _, _, approver, admin, _, revision, approvalService, _) =
            await CreateSeededContextWithApprovalReadyRevisionAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // If admin was the author, admin cannot approve
        revision.CreatedByUserId = admin.Id;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Admin self-approving"), admin.Id));

        Assert.Contains("Segregation of Duties Violation", ex.Message);
    }
}
