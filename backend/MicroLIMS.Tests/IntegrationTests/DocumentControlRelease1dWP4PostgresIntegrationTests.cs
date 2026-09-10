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
public class DocumentControlRelease1dWP4PostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlRelease1dWP4PostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static byte[] CreateValidDocx(string text = "Postgres WP4 Valid DOCX Content")
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private async Task<(User Author, User Reviewer, DocumentMaster Master, DocumentRevision Revision)>
        SeedScenarioAsync(MicroLimsDbContext db)
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

        var author = new User
        {
            FullName = "WP4 PG Author",
            Username = $"auth_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "WP4 PG Reviewer",
            Username = $"rev_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = reviewerRole.Id,
            IsActive = true
        };

        db.Users.AddRange(author, reviewer);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-WP4-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-WP4-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "Postgres WP4 Multi-Cycle SOP",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = author.Id,
            CreatedByUserId = author.Id,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Draft,
            CreatedByUserId = author.Id
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();

        return (author, reviewer, master, rev);
    }

    [PostgresFact]
    public async Task Postgres_MultiCycleReview_EndToEndLifecycle_WithWordVersioning_AndMandatoryGateAcrossCycles()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        // 1. Author uploads Word source document (v1)
        var wordV1 = await fileService.UploadRevisionFileAsync(
            rev.Id,
            FileRole.SourceFile,
            "sop_water_testing_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("Word source v1 content"),
            author.Id);

        Assert.Equal(1, wordV1.FileVersion);
        Assert.True(wordV1.IsActive);

        // 2. Author submits for review (Cycle 1)
        var task1 = await reviewService.SubmitForReviewAsync(rev.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id,
            SubmissionNotes = "Cycle 1 ready for technical review",
            DueDate = DateTime.UtcNow.AddDays(7)
        }, author.Id);

        Assert.Equal(1, task1.ReviewCycleNumber);
        Assert.Equal(wordV1.Id, task1.ReviewedSourceFileId);
        Assert.Equal(ReviewTaskStatus.Pending, task1.Status);
        Assert.Equal(DocumentRevisionStatus.InReview, task1.RevisionStatus);

        // 3. Reviewer adds Finding 1 (Mandatory) and Finding 2 (Non-mandatory)
        var finding1 = await reviewService.AddReviewFindingAsync(task1.Id, new AddReviewFindingRequest
        {
            PageNumber = 2,
            SectionNumber = "3.2",
            CommentText = "Sample incubation temperature range must be explicitly +/- 1 C.",
            IsMandatory = true
        }, reviewer.Id);

        Assert.Equal(wordV1.Id, finding1.RevisionFileId);
        Assert.Equal(1, finding1.SourceFileVersion);
        Assert.True(finding1.IsMandatory);
        Assert.Equal(ReviewFindingStatus.Open, finding1.Status);

        var finding2 = await reviewService.AddReviewFindingAsync(task1.Id, new AddReviewFindingRequest
        {
            PageNumber = 4,
            SectionNumber = "5.1",
            CommentText = "Consider citing standard reference ISO 11133.",
            IsMandatory = false
        }, reviewer.Id);

        Assert.Equal(wordV1.Id, finding2.RevisionFileId);
        Assert.Equal(1, finding2.SourceFileVersion);
        Assert.False(finding2.IsMandatory);

        // 4. Reviewer returns revision for correction
        var returnedTask = await reviewService.DecideReviewAsync(task1.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Please update section 3.2 per mandatory finding."
        }, reviewer.Id);

        Assert.Equal(ReviewTaskStatus.ReturnedForCorrection, returnedTask.Status);
        Assert.Equal(ReviewDecision.ReturnForCorrection, returnedTask.Decision);

        var revAfterReturn = await db.DocumentRevisions.FindAsync(rev.Id);
        Assert.Equal(DocumentRevisionStatus.Draft, revAfterReturn!.RevisionStatus);

        // 5. Author responds to findings while in Draft
        var responded1 = await reviewService.RespondToFindingAsync(finding1.Id, new RespondToFindingRequest
        {
            Response = "Updated section 3.2 to specify 36 +/- 1 C incubation."
        }, author.Id);
        Assert.Equal(ReviewFindingStatus.AuthorResponded, responded1.Status);

        var responded2 = await reviewService.RespondToFindingAsync(finding2.Id, new RespondToFindingRequest
        {
            Response = "ISO 11133 reference added to references section."
        }, author.Id);
        Assert.Equal(ReviewFindingStatus.AuthorResponded, responded2.Status);

        // 6. Author uploads corrected Word document (v2)
        var wordV2 = await fileService.UploadRevisionFileAsync(
            rev.Id,
            FileRole.SourceFile,
            "sop_water_testing_v2.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("Word source v2 corrected content"),
            author.Id);

        Assert.Equal(2, wordV2.FileVersion);
        Assert.True(wordV2.IsActive);

        var refreshedV1 = await db.RevisionFiles.FindAsync(wordV1.Id);
        Assert.False(refreshedV1!.IsActive);
        Assert.Equal(wordV2.Id, refreshedV1.SupersededByFileId);

        // 7. Author resubmits revision for review (Cycle 2)
        var task2 = await reviewService.SubmitForReviewAsync(rev.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id,
            SubmissionNotes = "Cycle 2 submission: findings addressed in Word v2"
        }, author.Id);

        Assert.Equal(2, task2.ReviewCycleNumber);
        Assert.Equal(wordV2.Id, task2.ReviewedSourceFileId);
        Assert.Equal(ReviewTaskStatus.Pending, task2.Status);

        // 8. Reviewer attempts to complete review while Finding 1 (from Cycle 1) is still AuthorResponded (not Resolved)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.DecideReviewAsync(task2.Id, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview,
                ReviewNotes = "Attempting review completion with unresolved mandatory finding"
            }, reviewer.Id));

        Assert.Contains("mandatory review finding(s) remain unresolved", ex.Message);

        // 9. Reviewer verifies author correction and resolves Finding 1
        var verified = await reviewService.VerifyFindingAsync(finding1.Id, new VerifyFindingRequest
        {
            VerificationNotes = "Verified: Section 3.2 correctly updated in Word v2."
        }, reviewer.Id);
        Assert.Equal(ReviewFindingStatus.ReviewerVerified, verified.Status);

        var resolved = await reviewService.ResolveFindingAsync(finding1.Id, reviewer.Id);
        Assert.Equal(ReviewFindingStatus.Resolved, resolved.Status);

        // 10. Reviewer completes review in Cycle 2
        var completedTask = await reviewService.DecideReviewAsync(task2.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview,
            ReviewNotes = "Cycle 2 review complete. All mandatory findings resolved. Document accepted."
        }, reviewer.Id);

        Assert.Equal(ReviewTaskStatus.Completed, completedTask.Status);
        Assert.Equal(ReviewDecision.CompleteReview, completedTask.Decision);

        var revFinal = await db.DocumentRevisions.FindAsync(rev.Id);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revFinal!.RevisionStatus);

        // 11. Verify Audit Events recorded in PostgreSQL
        var auditEvents = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == rev.Id)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        Assert.Contains(auditEvents, a => a.ActionCode == "RevisionSubmittedForReview");
        Assert.Contains(auditEvents, a => a.ActionCode == "ReviewFindingCreated");
        Assert.Contains(auditEvents, a => a.ActionCode == "TechnicalReviewReturned");
        Assert.Contains(auditEvents, a => a.ActionCode == "ReviewFindingAuthorResponded");
        Assert.Contains(auditEvents, a => a.ActionCode == "ReviewFindingVerified");
        Assert.Contains(auditEvents, a => a.ActionCode == "ReviewFindingResolved");
        Assert.Contains(auditEvents, a => a.ActionCode == "TechnicalReviewCompleted");
    }

    [PostgresFact]
    public async Task Postgres_SegregationOfDuties_AuthorCannotReviewOwnWork_OrResolveFindings_EvenIfAdmin()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        await fileService.UploadRevisionFileAsync(
            rev.Id,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx(),
            author.Id);

        // 1. Author cannot assign themselves as reviewer
        var exSelfAssign = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.SubmitForReviewAsync(rev.Id, new SubmitForReviewRequest
            {
                ReviewerUserId = author.Id
            }, author.Id));
        Assert.Contains("Segregation of Duties Violation", exSelfAssign.Message);

        // Submit with legitimate reviewer
        var task = await reviewService.SubmitForReviewAsync(rev.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id
        }, author.Id);

        // 2. Author cannot create review findings
        var exCreateFinding = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
            {
                CommentText = "Author attempting to create finding",
                IsMandatory = true
            }, author.Id));
        Assert.Contains("Segregation of Duties Violation", exCreateFinding.Message);

        // Reviewer creates finding
        var finding = await reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Valid reviewer finding",
            IsMandatory = true
        }, reviewer.Id);

        // 3. Author cannot verify finding
        var exVerify = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.VerifyFindingAsync(finding.Id, new VerifyFindingRequest
            {
                VerificationNotes = "Author verifying"
            }, author.Id));
        Assert.Contains("Segregation of Duties Violation", exVerify.Message);

        // 4. Author cannot resolve finding
        var exResolve = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.ResolveFindingAsync(finding.Id, author.Id));
        Assert.Contains("Segregation of Duties Violation", exResolve.Message);

        // 5. Author cannot decide review
        var exDecide = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview
            }, author.Id));
        Assert.Contains("Segregation of Duties Violation", exDecide.Message);
    }

    [PostgresFact]
    public async Task Postgres_GetReviewTasksByRevisionId_ReturnsCompleteMultiCycleHistory_WithLineage()
    {
        await using var db = _fixture.CreateDbContext();
        var (author, reviewer, master, rev) = await SeedScenarioAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        var wordV1 = await fileService.UploadRevisionFileAsync(
            rev.Id,
            FileRole.SourceFile,
            "sop_cycle_test_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V1 Content"),
            author.Id);

        var task1 = await reviewService.SubmitForReviewAsync(rev.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id,
            SubmissionNotes = "Cycle 1 notes"
        }, author.Id);

        var finding = await reviewService.AddReviewFindingAsync(task1.Id, new AddReviewFindingRequest
        {
            CommentText = "Finding in Cycle 1",
            IsMandatory = true,
            PageNumber = 1,
            SectionNumber = "1.0"
        }, reviewer.Id);

        await reviewService.DecideReviewAsync(task1.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Return for correction"
        }, reviewer.Id);

        var wordV2 = await fileService.UploadRevisionFileAsync(
            rev.Id,
            FileRole.SourceFile,
            "sop_cycle_test_v2.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V2 Content"),
            author.Id);

        var task2 = await reviewService.SubmitForReviewAsync(rev.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id,
            SubmissionNotes = "Cycle 2 notes"
        }, author.Id);

        var tasks = await reviewService.GetReviewTasksByRevisionIdAsync(rev.Id, author.Id);

        Assert.Equal(2, tasks.Count);

        // Cycle 2 is first (descending by cycle)
        Assert.Equal(2, tasks[0].ReviewCycleNumber);
        Assert.Equal(wordV2.Id, tasks[0].ReviewedSourceFileId);
        Assert.Equal(ReviewTaskStatus.Pending, tasks[0].Status);

        // Cycle 1 is second
        Assert.Equal(1, tasks[1].ReviewCycleNumber);
        Assert.Equal(wordV1.Id, tasks[1].ReviewedSourceFileId);
        Assert.Equal(ReviewTaskStatus.ReturnedForCorrection, tasks[1].Status);
        Assert.Single(tasks[1].Findings);
        Assert.Equal(wordV1.Id, tasks[1].Findings[0].RevisionFileId);
        Assert.Equal(1, tasks[1].Findings[0].SourceFileVersion);
    }
}
