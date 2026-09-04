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
public class DocumentControlReviewPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlReviewPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int AuthorId, int ReviewerId, int MasterId, int RevisionId)> SeedDocumentAndUsersAsync(MicroLimsDbContext db)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync();
        }

        var author = new User
        {
            FullName = "Postgres Author",
            Username = $"author_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "Postgres Reviewer",
            Username = $"reviewer_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        db.Users.AddRange(author, reviewer);
        await db.SaveChangesAsync();

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, audit, auth);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var req = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-PG-{Guid.NewGuid():N}".Substring(0, 16),
            Title: "Postgres Integrated SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Procedures",
            Keywords: new List<string> { "Postgres", "Test" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 24
        );

        var master = await masterService.RegisterDocumentMasterAsync(req, author.Id);
        var revId = master.Revisions[0].Id;

        // Attach controlled PDF
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Postgres Integration Controlled PDF");
        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.ControlledPdf,
            "sop_pg_v1.pdf",
            "application/pdf",
            pdfBytes,
            author.Id);

        return (author.Id, reviewer.Id, master.Id, revId);
    }

    [Fact]
    public async Task Postgres_CompleteReviewLifecycle_WithAuditing_AndMandatoryGate()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, reviewerId, _, revId) = await SeedDocumentAndUsersAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var reviewService = new DocumentReviewService(db, audit, auth);

        // 1. Submit for review
        var task = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId,
            SubmissionNotes = "Ready for live postgres review"
        }, authorId);

        Assert.NotNull(task);
        Assert.Equal(ReviewTaskStatus.Pending, task.Status);
        Assert.Equal(DocumentRevisionStatus.InReview, task.RevisionStatus);

        // 2. Add finding in live Postgres
        var finding = await reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            PageNumber = 1,
            SectionNumber = "1.0",
            CommentText = "Please update revision scope reference.",
            IsMandatory = true
        }, reviewerId);

        Assert.Equal(ReviewFindingStatus.Open, finding.Status);

        // 3. Author responds
        var responded = await reviewService.RespondToFindingAsync(finding.Id, new RespondToFindingRequest
        {
            Response = "Scope reference updated per requirement."
        }, authorId);
        Assert.Equal(ReviewFindingStatus.AuthorResponded, responded.Status);

        // 4. Verification that gate blocks early completion
        var gateEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview
            }, reviewerId));
        Assert.Contains("mandatory review finding(s) remain unresolved", gateEx.Message);

        // 5. Reviewer verifies & resolves finding
        await reviewService.VerifyFindingAsync(finding.Id, new VerifyFindingRequest { VerificationNotes = "Approved change" }, reviewerId);
        await reviewService.ResolveFindingAsync(finding.Id, reviewerId);

        // 6. Reviewer completes review
        var completed = await reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview,
            ReviewNotes = "All findings resolved."
        }, reviewerId);

        Assert.Equal(ReviewTaskStatus.Completed, completed.Status);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, completed.RevisionStatus);

        // 7. Verify live database state
        var revInDb = await db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revInDb!.RevisionStatus);

        var taskInDb = await db.DocumentReviewTasks.Include(t => t.Findings).FirstOrDefaultAsync(t => t.Id == task.Id);
        Assert.NotNull(taskInDb);
        Assert.Equal(ReviewTaskStatus.Completed, taskInDb.Status);
        Assert.Single(taskInDb.Findings);
        Assert.Equal(ReviewFindingStatus.Resolved, taskInDb.Findings.First().Status);

        // 8. Verify audit logs
        var auditLogs = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == revId)
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Contains(auditLogs, a => a.ActionCode == "RevisionSubmittedForReview");
        Assert.Contains(auditLogs, a => a.ActionCode == "ReviewFindingCreated");
        Assert.Contains(auditLogs, a => a.ActionCode == "ReviewFindingAuthorResponded");
        Assert.Contains(auditLogs, a => a.ActionCode == "ReviewFindingVerified");
        Assert.Contains(auditLogs, a => a.ActionCode == "ReviewFindingResolved");
        Assert.Contains(auditLogs, a => a.ActionCode == "TechnicalReviewCompleted");
    }

    [Fact]
    public async Task Postgres_SoD_AuthorCannotReviewOrResolveFindings()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, reviewerId, _, revId) = await SeedDocumentAndUsersAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var reviewService = new DocumentReviewService(db, audit, auth);

        // Test 1: Author cannot assign self as reviewer
        var exSelfAssign = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = authorId }, authorId));
        Assert.Contains("Segregation of Duties", exSelfAssign.Message);

        // Test 2: Admin cannot assign author as reviewer
        var exAdminAssign = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = authorId }, _fixture.SeededUserId));
        Assert.Contains("Segregation of Duties", exAdminAssign.Message);

        // Submit properly
        var task = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = reviewerId }, authorId);

        // Test 3: Author cannot add reviewer finding
        var exAuthorAddFinding = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest { CommentText = "Author comment" }, authorId));
        Assert.Contains("Segregation of Duties", exAuthorAddFinding.Message);

        // Reviewer adds finding
        var finding = await reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest { CommentText = "Reviewer note" }, reviewerId);

        // Test 4: Author cannot resolve reviewer finding
        var exAuthorResolve = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.ResolveFindingAsync(finding.Id, authorId));
        Assert.Contains("Segregation of Duties", exAuthorResolve.Message);

        // Test 5: Author cannot complete technical review
        var exAuthorComplete = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest { Decision = ReviewDecision.CompleteReview }, authorId));
        Assert.Contains("Segregation of Duties", exAuthorComplete.Message);
    }
}
