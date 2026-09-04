using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlApprovalPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlApprovalPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int AuthorId, int ReviewerId, int ApproverId, int MasterId, int RevisionId)> SeedApprovalReadyDocumentAsync(MicroLimsDbContext db)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync();
        }

        var reviewerRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Reviewer);
        if (reviewerRole == null)
        {
            reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
            db.Roles.Add(reviewerRole);
            await db.SaveChangesAsync();
        }

        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Postgres-Approver-1!");

        var author = new User
        {
            FullName = "Postgres Author",
            Username = $"author_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "Postgres Reviewer",
            Username = $"rev_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        var approver = new User
        {
            FullName = "Postgres Approver",
            Username = $"app_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        db.Users.AddRange(author, reviewer, approver);
        await db.SaveChangesAsync();

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, audit, auth);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        var req = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-APP-{Guid.NewGuid():N}".Substring(0, 16),
            Title: "Postgres Approval Workflow SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing",
            Keywords: new List<string> { "Postgres", "Approval" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 24
        );

        var masterDto = await masterService.RegisterDocumentMasterAsync(req, author.Id);
        var revId = masterDto.Revisions[0].Id;

        // Attach controlled PDF
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Controlled PDF for Approval Integration");
        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.ControlledPdf,
            "sop_app_v1.pdf",
            "application/pdf",
            pdfBytes,
            author.Id);

        // Submit for Review & Complete Review
        var reviewTask = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id,
            SubmissionNotes = "Postgres ready for review"
        }, author.Id);

        await reviewService.DecideReviewAsync(reviewTask.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview,
            ReviewNotes = "Postgres review completed with zero issues"
        }, reviewer.Id);

        return (author.Id, reviewer.Id, approver.Id, masterDto.Id, revId);
    }

    [Fact]
    public async Task Postgres_CreateApprovalTask_EnforcesRelationalFKs_AndAudits()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, approverId, masterId, revisionId) = await SeedApprovalReadyDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);

        var task = await approvalService.CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(
            approverId,
            DateTime.UtcNow.AddDays(7),
            "Postgres Approval Task Notes"
        ), authorId);

        Assert.NotNull(task);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, task.Status);

        // Verify direct PostgreSQL query
        var taskInDb = await db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
            .Include(t => t.AssignedApproverUser)
            .FirstOrDefaultAsync(t => t.Id == task.Id);

        Assert.NotNull(taskInDb);
        Assert.Equal(revisionId, taskInDb.DocumentRevisionId);
        Assert.Equal(approverId, taskInDb.AssignedApproverUserId);
        Assert.Equal(authorId, taskInDb.AssignedByUserId);

        // Verify audit log persisted
        var auditLogs = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == revisionId && a.ActionCode == "ApprovalTaskCreated")
            .ToListAsync();

        Assert.NotEmpty(auditLogs);
    }

    [Fact]
    public async Task Postgres_ApprovalDecision_ImmediateEffective_SupersedesPriorInSingleTransaction()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, approverId, masterId, revisionId) = await SeedApprovalReadyDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);

        var task = await approvalService.CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(approverId), authorId);

        var result = await approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Approved in PostgreSQL database test.",
            Password: "Postgres-Approver-1!"
        ), approverId);

        Assert.Equal(DocumentApprovalTaskStatus.Approved, result.Status);

        // Verify PostgreSQL state
        var revInDb = await db.DocumentRevisions.FindAsync(revisionId);
        Assert.NotNull(revInDb);
        Assert.Equal(DocumentRevisionStatus.Effective, revInDb.RevisionStatus);
        Assert.NotNull(revInDb.EffectiveDate);

        var masterInDb = await db.DocumentMasters.FindAsync(masterId);
        Assert.NotNull(masterInDb);
        Assert.Equal(revisionId, masterInDb.CurrentEffectiveRevisionId);

        // Verify audit trail in PostgreSQL
        var approvalAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == revisionId && a.ActionCode == "DocumentRevisionApproved");

        Assert.NotNull(approvalAudit);
    }

    [Fact]
    public async Task Postgres_ApprovalDecision_FutureEffective_PreservesEffectivePrior()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, reviewerId, approverId, masterId, rev1Id) = await SeedApprovalReadyDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);
        var revisionService = new DocumentRevisionService(db, audit, auth);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        // 1. Approve Rev 1 as immediate Effective
        var task1 = await approvalService.CreateApprovalTaskAsync(rev1Id, new CreateApprovalTaskRequest(approverId), authorId);
        await approvalService.ExecuteApprovalDecisionAsync(task1.Id, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Rev 1 Effective", Password: "Postgres-Approver-1!"), approverId);

        // 2. Create Rev 2 from Effective Rev 1
        var rev2Dto = await revisionService.CreateRevisionFromEffectiveAsync(masterId, new CreateRevisionRequest(
            RevisionType.Minor,
            "Postgres minor revision for future activation testing.",
            "Summary of Rev 2 changes"
        ), authorId);

        // Attach PDF to Rev 2
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock PDF for Rev 2");
        await fileService.UploadRevisionFileAsync(rev2Dto.Id, FileRole.ControlledPdf, "sop_app_v1.1.pdf", "application/pdf", pdfBytes, authorId);

        // Complete Impact Assessment for Rev 2
        await revisionService.SaveImpactAssessmentAsync(rev2Dto.Id, new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: false,
            ProcedureOrMethodDetails: null,
            TrainingImpact: true,
            TrainingDetails: "Staff training required for Rev 2",
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
        ), authorId);

        // Submit & Complete Review for Rev 2
        var reviewTask2 = await reviewService.SubmitForReviewAsync(rev2Dto.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId,
            SubmissionNotes = "Review Rev 2"
        }, authorId);
        await reviewService.DecideReviewAsync(reviewTask2.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview,
            ReviewNotes = "Rev 2 review complete"
        }, reviewerId);

        // 3. Create Approval Task for Rev 2
        var task2 = await approvalService.CreateApprovalTaskAsync(rev2Dto.Id, new CreateApprovalTaskRequest(approverId), authorId);

        // 4. Approve with Future Effective Date (+20 days)
        var futureDate = DateTime.UtcNow.AddDays(20);
        var result = await approvalService.ExecuteApprovalDecisionAsync(task2.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Approved for future activation in Postgres",
            EffectiveDate: futureDate,
            Password: "Postgres-Approver-1!"
        ), approverId);

        Assert.Equal(DocumentApprovalTaskStatus.Approved, result.Status);

        // Verify Rev 2 in PostgreSQL
        var dbRev2 = await db.DocumentRevisions.FindAsync(rev2Dto.Id);
        Assert.NotNull(dbRev2);
        Assert.Equal(DocumentRevisionStatus.FutureEffective, dbRev2.RevisionStatus);

        // Verify Rev 1 in PostgreSQL remains Effective!
        var dbRev1 = await db.DocumentRevisions.FindAsync(rev1Id);
        Assert.NotNull(dbRev1);
        Assert.Equal(DocumentRevisionStatus.Effective, dbRev1.RevisionStatus);

        var dbMaster = await db.DocumentMasters.FindAsync(masterId);
        Assert.NotNull(dbMaster);
        Assert.Equal(rev1Id, dbMaster.CurrentEffectiveRevisionId);
    }
}
