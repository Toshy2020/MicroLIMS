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
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlElectronicSignaturePostgresIntegrationTests
{
    private const string Password = "Part11-Valid-Password-2026!";
    private readonly PostgresTestFixture _fixture;

    public DocumentControlElectronicSignaturePostgresIntegrationTests(PostgresTestFixture fixture)
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

        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

        var author = new User
        {
            FullName = "Postgres Sig Author",
            Username = $"psig_a_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "Postgres Sig Reviewer",
            Username = $"psig_r_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        var approver = new User
        {
            FullName = "Postgres Sig Approver",
            Username = $"psig_p_{Guid.NewGuid():N}".Substring(0, 15),
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
            CompanyDocumentCode: $"SOP-SIG-{Guid.NewGuid():N}".Substring(0, 16),
            Title: "Postgres 21 CFR Part 11 Signature SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing",
            Keywords: new List<string> { "Postgres", "Part11", "Signature" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 24
        );

        var masterDto = await masterService.RegisterDocumentMasterAsync(req, author.Id);
        var revId = masterDto.Revisions[0].Id;

        // Attach controlled PDF
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Controlled PDF for Part 11 Postgres Integration Testing");
        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.ControlledPdf,
            "sop_sig_v1.pdf",
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
            ReviewNotes = "Postgres review completed with zero open issues"
        }, reviewer.Id);

        return (author.Id, reviewer.Id, approver.Id, masterDto.Id, revId);
    }

    [PostgresFact]
    public async Task Postgres_SignApproval_PersistsImmutableSignature_AndBindsToRevision()
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
            "Approved with 21 CFR Part 11 Electronic Signature in live PostgreSQL.",
            Password: Password
        ), approverId, "10.0.1.25");

        Assert.Equal(DocumentApprovalTaskStatus.Approved, result.Status);

        // Verify relational ElectronicSignature in PostgreSQL
        var signature = await db.ElectronicSignatures
            .FirstOrDefaultAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revisionId);

        Assert.NotNull(signature);
        Assert.Equal(approverId, signature.UserId);
        Assert.Equal("Reviewer", signature.RoleSnapshot);
        Assert.Equal(SignatureMeaning.Approved, signature.MeaningOfSignature);
        Assert.Equal("10.0.1.25", signature.IpAddress);
        Assert.Equal("Approved with 21 CFR Part 11 Electronic Signature in live PostgreSQL.", signature.Comment);

        // Verify DocumentRevision status in PostgreSQL
        var revInDb = await db.DocumentRevisions.FindAsync(revisionId);
        Assert.NotNull(revInDb);
        Assert.Equal(DocumentRevisionStatus.Effective, revInDb.RevisionStatus);

        // Verify Audit Trail in PostgreSQL
        var sigAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == revisionId && a.ActionCode == "ElectronicSignatureApplied");
        Assert.NotNull(sigAudit);
    }

    [PostgresFact]
    public async Task Postgres_ElectronicSignature_DirectSqlUpdate_IsProhibitedByTrigger()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, approverId, _, revisionId) = await SeedApprovalReadyDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);

        var task = await approvalService.CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(approverId), authorId);

        await approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Initial approval signature.",
            Password: Password
        ), approverId);

        var signature = await db.ElectronicSignatures
            .FirstAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revisionId);

        // Attempt direct raw SQL UPDATE on ElectronicSignatures table
        // This must be blocked by trg_electronicsignatures_immutable
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync($"UPDATE \"ElectronicSignatures\" SET \"Comment\" = 'Tampered' WHERE \"Id\" = {signature.Id}"));

        Assert.Contains("append-only: UPDATE and DELETE operations are prohibited by GMP regulations", ex.Message);
    }

    [PostgresFact]
    public async Task Postgres_ElectronicSignature_DirectSqlDelete_IsProhibitedByTrigger()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, approverId, _, revisionId) = await SeedApprovalReadyDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);

        var task = await approvalService.CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(approverId), authorId);

        await approvalService.ExecuteApprovalDecisionAsync(task.Id, new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Initial approval signature.",
            Password: Password
        ), approverId);

        var signature = await db.ElectronicSignatures
            .FirstAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revisionId);

        // Attempt direct raw SQL DELETE on ElectronicSignatures table
        // This must be blocked by trg_electronicsignatures_immutable
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync($"DELETE FROM \"ElectronicSignatures\" WHERE \"Id\" = {signature.Id}"));

        Assert.Contains("append-only: UPDATE and DELETE operations are prohibited by GMP regulations", ex.Message);
    }

    [PostgresFact]
    public async Task Postgres_SignApproval_AtomicTransaction_FailedPasswordMutatesNoRecords()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, approverId, _, revisionId) = await SeedApprovalReadyDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);

        var task = await approvalService.CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(approverId), authorId);

        var invalidRequest = new ExecuteApprovalDecisionRequest(
            DocumentApprovalDecision.Approve,
            "Approval with bad password in Postgres.",
            Password: "Definitively-Wrong-Password!"
        );

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, invalidRequest, approverId));

        // Check revision status still AwaitingApproval in live Postgres
        var revInDb = await db.DocumentRevisions.FindAsync(revisionId);
        Assert.NotNull(revInDb);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revInDb.RevisionStatus);

        // Check task still Pending in live Postgres
        var taskInDb = await db.DocumentApprovalTasks.FindAsync(task.Id);
        Assert.NotNull(taskInDb);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, taskInDb.Status);

        // Check zero signatures in live Postgres
        var sigCount = await db.ElectronicSignatures
            .CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revisionId);
        Assert.Equal(0, sigCount);
    }
}
