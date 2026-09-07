using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlRelease1dWP4UnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _seq = 9000;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(System.Threading.Interlocked.Increment(ref _seq));
        }
    }

    private static MicroLimsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: $"MicroLIMS_WP4_Unit_{Guid.NewGuid()}")
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static byte[] CreateValidDocx(string text = "Unit Test Valid DOCX Content")
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private async Task<(MicroLimsDbContext db, IDocumentReviewService reviewService, IDocumentFileService fileService, int authorId, int reviewerId, int masterId, int revId)>
        SetupHarnessAsync()
    {
        var db = CreateInMemoryDbContext();
        var seqHelper = new TestDbSequenceHelper();
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        var reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
        db.Roles.AddRange(analystRole, reviewerRole);
        await db.SaveChangesAsync();

        var author = new User
        {
            FullName = "WP4 Test Author",
            Username = $"auth_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "hash",
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "WP4 Test Reviewer",
            Username = $"rev_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "hash",
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        db.Users.AddRange(author, reviewer);
        await db.SaveChangesAsync();

        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec = new DocumentSection { DepartmentId = dept.Id, Name = "Microbiology", IsActive = true };
        db.DocumentSections.Add(sec);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-WP4-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-WP4-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "WP4 Multi-Cycle Review SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = sec.Id,
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

        return (db, reviewService, fileService, author.Id, reviewer.Id, master.Id, rev.Id);
    }

    [Fact]
    public async Task SubmitForReview_SucceedsWithOnlyWordSource_AndSetsCycle1AndReviewedSourceFileId()
    {
        var (db, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        var docxBytes = CreateValidDocx("Word source draft v1");
        var wordFile = await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_procedure_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            docxBytes,
            authorId);

        var task = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId,
            SubmissionNotes = "Cycle 1 ready for technical review",
            DueDate = DateTime.UtcNow.AddDays(5)
        }, authorId);

        Assert.NotNull(task);
        Assert.Equal(1, task.ReviewCycleNumber);
        Assert.Equal(wordFile.Id, task.ReviewedSourceFileId);
        Assert.Equal(ReviewTaskStatus.Pending, task.Status);
        Assert.Equal(DocumentRevisionStatus.InReview, task.RevisionStatus);
        Assert.Equal(reviewerId, task.AssignedReviewerUserId);

        var revInDb = await db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.InReview, revInDb!.RevisionStatus);
    }

    [Fact]
    public async Task SubmitForReview_Throws_WhenNoWordSourceOrPdfAttached()
    {
        var (_, reviewService, _, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
            {
                ReviewerUserId = reviewerId,
                SubmissionNotes = "Draft with no files"
            }, authorId));

        Assert.Contains("Cannot submit revision for technical review without an active editable Word source file", ex.Message);
    }

    [Fact]
    public async Task AddReviewFinding_SnapshotsSourceFileVersionAndRevisionFileId()
    {
        var (_, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        var wordFile = await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_test_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx(),
            authorId);

        var task = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId,
            SubmissionNotes = "Cycle 1"
        }, authorId);

        var finding = await reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            PageNumber = 3,
            SectionNumber = "2.1",
            CommentText = "Sample incubation temperature must be specified as 35 +/- 2 C.",
            IsMandatory = true
        }, reviewerId);

        Assert.NotNull(finding);
        Assert.Equal(wordFile.Id, finding.RevisionFileId);
        Assert.Equal(1, finding.SourceFileVersion);
        Assert.Equal(ReviewFindingStatus.Open, finding.Status);
        Assert.True(finding.IsMandatory);

        var taskDto = await reviewService.GetReviewTaskByIdAsync(task.Id, reviewerId);
        Assert.Equal(ReviewTaskStatus.InProgress, taskDto.Status);
        Assert.Equal(1, taskDto.TotalFindingsCount);
        Assert.Equal(1, taskDto.OpenMandatoryFindingsCount);
    }

    [Fact]
    public async Task RespondToFinding_Succeeds_WhenRevisionIsDraft_AfterReturnForCorrection()
    {
        var (db, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx(),
            authorId);

        var task = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        var finding = await reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Please clarify safety precautions.",
            IsMandatory = true
        }, reviewerId);

        // Reviewer returns revision for correction
        var returnedTask = await reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Please address safety findings and re-upload."
        }, reviewerId);

        Assert.Equal(ReviewTaskStatus.ReturnedForCorrection, returnedTask.Status);
        var revInDb = await db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.Draft, revInDb!.RevisionStatus);

        // Author responds while revision is in Draft state
        var responded = await reviewService.RespondToFindingAsync(finding.Id, new RespondToFindingRequest
        {
            Response = "Added section 4.3 with PPE and chemical handling requirements."
        }, authorId);

        Assert.Equal(ReviewFindingStatus.AuthorResponded, responded.Status);
        Assert.Equal("Added section 4.3 with PPE and chemical handling requirements.", responded.AuthorResponse);
        Assert.NotNull(responded.AuthorResponseAt);
    }

    [Fact]
    public async Task Author_CannotVerifyOrResolveFinding_SegregationOfDutiesEnforced()
    {
        var (_, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx(),
            authorId);

        var task = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        var finding = await reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Mandatory correction",
            IsMandatory = true
        }, reviewerId);

        // Author attempts to verify -> throws UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.VerifyFindingAsync(finding.Id, new VerifyFindingRequest { VerificationNotes = "Author verifying self" }, authorId));

        // Author attempts to resolve -> throws UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.ResolveFindingAsync(finding.Id, authorId));
    }

    [Fact]
    public async Task SubmitForReview_Cycle2_IncrementsReviewCycleNumber_AndPinsNewWordVersion()
    {
        var (db, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        var wordV1 = await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V1 Content"),
            authorId);

        var task1 = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        Assert.Equal(1, task1.ReviewCycleNumber);
        Assert.Equal(wordV1.Id, task1.ReviewedSourceFileId);

        // Return for correction
        await reviewService.DecideReviewAsync(task1.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Revise per findings."
        }, reviewerId);

        // Author uploads corrected Word document (v2)
        var wordV2 = await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v2.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V2 Corrected Content"),
            authorId);

        Assert.Equal(2, wordV2.FileVersion);

        // Author resubmits for review (Cycle 2)
        var task2 = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId,
            SubmissionNotes = "Cycle 2 submission with updated document v2"
        }, authorId);

        Assert.Equal(2, task2.ReviewCycleNumber);
        Assert.Equal(wordV2.Id, task2.ReviewedSourceFileId);
        Assert.Equal(ReviewTaskStatus.Pending, task2.Status);

        var tasks = await reviewService.GetReviewTasksByRevisionIdAsync(revId, authorId);
        Assert.Equal(2, tasks.Count);
        Assert.Equal(2, tasks[0].ReviewCycleNumber);
        Assert.Equal(1, tasks[1].ReviewCycleNumber);
    }

    [Fact]
    public async Task CompleteReview_BlockedAcrossCycles_WhenMandatoryFindingFromCycle1IsUnresolved()
    {
        var (_, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V1 Content"),
            authorId);

        var task1 = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        // Reviewer creates mandatory finding in Cycle 1
        var finding1 = await reviewService.AddReviewFindingAsync(task1.Id, new AddReviewFindingRequest
        {
            CommentText = "Critical safety missing.",
            IsMandatory = true
        }, reviewerId);

        // Return for correction
        await reviewService.DecideReviewAsync(task1.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Fix safety finding."
        }, reviewerId);

        // Author responds and uploads v2
        await reviewService.RespondToFindingAsync(finding1.Id, new RespondToFindingRequest
        {
            Response = "Safety section added."
        }, authorId);

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v2.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V2 Content"),
            authorId);

        // Author submits Cycle 2
        var task2 = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        // Reviewer attempts to complete review in Cycle 2 WITHOUT resolving finding 1 from Cycle 1
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.DecideReviewAsync(task2.Id, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview,
                ReviewNotes = "Attempting to complete with unresolved cycle 1 finding"
            }, reviewerId));

        Assert.Contains("mandatory review finding(s) remain unresolved", ex.Message);
    }

    [Fact]
    public async Task CompleteReview_SucceedsAcrossCycles_OnceAllMandatoryFindingsAreResolved()
    {
        var (db, reviewService, fileService, authorId, reviewerId, _, revId) = await SetupHarnessAsync();

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx(),
            authorId);

        var task1 = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        var finding1 = await reviewService.AddReviewFindingAsync(task1.Id, new AddReviewFindingRequest
        {
            CommentText = "Cycle 1 mandatory comment",
            IsMandatory = true
        }, reviewerId);

        await reviewService.DecideReviewAsync(task1.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Return"
        }, reviewerId);

        await reviewService.RespondToFindingAsync(finding1.Id, new RespondToFindingRequest { Response = "Resolved" }, authorId);

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v2.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx("V2"),
            authorId);

        var task2 = await reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        // Reviewer verifies and resolves finding 1
        await reviewService.VerifyFindingAsync(finding1.Id, new VerifyFindingRequest
        {
            VerificationNotes = "Verified in docx v2"
        }, reviewerId);

        await reviewService.ResolveFindingAsync(finding1.Id, reviewerId);

        // Reviewer completes review in Cycle 2
        var completedTask = await reviewService.DecideReviewAsync(task2.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview,
            ReviewNotes = "All findings resolved across cycles. Document accepted."
        }, reviewerId);

        Assert.Equal(ReviewTaskStatus.Completed, completedTask.Status);
        Assert.Equal(ReviewDecision.CompleteReview, completedTask.Decision);

        var revInDb = await db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revInDb!.RevisionStatus);
    }

    [Fact]
    public async Task SegregationOfDuties_AuthorCannotAssignSelfAsReviewer()
    {
        var (_, reviewService, fileService, authorId, _, _, revId) = await SetupHarnessAsync();

        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.SourceFile,
            "sop_v1.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateValidDocx(),
            authorId);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest
            {
                ReviewerUserId = authorId,
                SubmissionNotes = "Self-assigning review"
            }, authorId));

        Assert.Contains("Segregation of Duties Violation", ex.Message);
    }
}
